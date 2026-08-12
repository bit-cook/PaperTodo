using PaperTodo;

return TodoReminderPolicyChecks.Run();

internal static class TodoReminderPolicyChecks
{
    private static readonly DateTimeOffset Baseline =
        new(2026, 4, 5, 10, 30, 0, TimeSpan.Zero);

    internal static int Run()
    {
        CandidateSelectionPreservesDomainOrder();
        PollDelayUsesTheInjectedClockAndLegacyBounds();
        DueBatchAdvancesWithTheInjectedClock();
        DisabledOrShuttingDownEvaluationIsInactive();
        TextCompactionPreservesLegacyPresentationRules();
        TriggerTransactionCommitsTheWholeBatchAfterAnySurfaceSucceeds();
        FailedDeliveryKeepsTheWholeBatchPendingForRetry();
        EmptyBatchProducesNoTransaction();

        Console.WriteLine("PaperTodo reminder policy checks passed.");
        return 0;
    }

    private static void CandidateSelectionPreservesDomainOrder()
    {
        var clock = new AdjustableTimeProvider(Baseline);
        var service = new TodoReminderPolicyService(clock);
        var first = Item("first", Baseline.AddMinutes(5));
        var second = Item("second", Baseline.AddMinutes(1));
        var state = EnabledState(
            TodoPaper("paper-a",
                Item("done", Baseline.AddSeconds(-1), done: true),
                first,
                Item("triggered", Baseline.AddSeconds(-1), triggered: true)),
            new PaperData
            {
                Id = "note",
                Type = PaperTypes.Note,
                Items = [Item("note-item", Baseline.AddSeconds(-1))]
            },
            TodoPaper("paper-b",
                new PaperItem { Id = "without-time" },
                second));

        var evaluation = service.Evaluate(state);

        Assert(evaluation.IsActive, "enabled reminders should produce an active evaluation");
        Assert(evaluation.PendingReminders.Count == 2,
            "only unfinished, untriggered todo reminders should be selected");
        Assert(ReferenceEquals(evaluation.PendingReminders[0].Item, first) &&
               ReferenceEquals(evaluation.PendingReminders[1].Item, second),
            "candidate order must remain paper order followed by item order");
        Assert(evaluation.NextPoll?.ReminderAt == second.ReminderAt,
            "the next poll should target the earliest selected reminder");
    }

    private static void PollDelayUsesTheInjectedClockAndLegacyBounds()
    {
        var clock = new AdjustableTimeProvider(Baseline);
        var service = new TodoReminderPolicyService(clock);

        var almostDue = service.Evaluate(EnabledState(
            TodoPaper("paper", Item("soon", Baseline.AddMilliseconds(100)))));
        Assert(almostDue.NextPoll?.Delay == TodoReminderPolicyService.MinimumPollInterval,
            "a due or nearly due reminder should use the 250 ms minimum poll");

        var exact = service.Evaluate(EnabledState(
            TodoPaper("paper", Item("middle", Baseline.AddSeconds(12)))));
        Assert(exact.NextPoll?.Delay == TimeSpan.FromSeconds(12),
            "a delay inside the polling bounds should remain exact");

        var distant = service.Evaluate(EnabledState(
            TodoPaper("paper", Item("later", Baseline.AddMinutes(10)))));
        Assert(distant.NextPoll?.Delay == TodoReminderPolicyService.MaximumPollInterval,
            "a distant reminder should be polled again after at most one minute");

        clock.Advance(TimeSpan.FromSeconds(11.9));
        var afterAdvance = service.Evaluate(EnabledState(
            TodoPaper("paper", Item("clock-controlled", Baseline.AddSeconds(12)))));
        Assert(afterAdvance.NextPoll?.Delay == TodoReminderPolicyService.MinimumPollInterval,
            "polling must be derived from the injected clock rather than wall time");
    }

    private static void DueBatchAdvancesWithTheInjectedClock()
    {
        var clock = new AdjustableTimeProvider(Baseline);
        var service = new TodoReminderPolicyService(clock);
        var later = Item("later", Baseline.AddSeconds(20));
        var alreadyDue = Item("already-due", Baseline.AddMinutes(-2));
        var dueNow = Item("due-now", Baseline);
        var state = EnabledState(TodoPaper(
            "paper",
            later,
            alreadyDue,
            dueNow));

        var initial = service.Evaluate(state);
        Assert(initial.DueBatch.Count == 2,
            "reminders at or before the clock snapshot should be due");
        Assert(initial.DueBatch.Reminders[0].ItemId == "already-due" &&
               initial.DueBatch.Reminders[1].ItemId == "due-now",
            "the due batch must preserve domain enumeration order, not re-sort by time");

        clock.Advance(TimeSpan.FromSeconds(20));
        var advanced = service.Evaluate(state);
        Assert(advanced.DueBatch.Count == 3 &&
               advanced.DueBatch.Reminders[0].ItemId == "later",
            "advancing the injected clock should make the later reminder due in domain order");
    }

    private static void DisabledOrShuttingDownEvaluationIsInactive()
    {
        var service = new TodoReminderPolicyService(
            new AdjustableTimeProvider(Baseline));
        var disabled = new AppState
        {
            Papers = [TodoPaper("paper", Item("item", Baseline))]
        };

        var disabledEvaluation = service.Evaluate(disabled);
        Assert(!disabledEvaluation.IsActive &&
               disabledEvaluation.PendingReminders.Count == 0 &&
               disabledEvaluation.DueBatch.Count == 0 &&
               disabledEvaluation.NextPoll == null,
            "a disabled feature must not select or schedule reminders");

        disabled.ExperimentalTodoReminders = true;
        var shutdownEvaluation = service.Evaluate(disabled, isShuttingDown: true);
        Assert(!shutdownEvaluation.IsActive && shutdownEvaluation.NextPoll == null,
            "shutdown must suppress reminder selection and polling");
    }

    private static void TextCompactionPreservesLegacyPresentationRules()
    {
        Assert(TodoReminderPolicyService.CompactText(
                "\t  First line  \r\n Second line\tThird  line ",
                "unnamed") == "First line Second line Third  line",
            "line breaks and tabs should become one separator while inner spaces remain intact");
        Assert(TodoReminderPolicyService.CompactText(" \r\n\t ", "未命名") == "未命名",
            "blank reminder text should use the caller-provided localized fallback");

        var longText = new string('x', TodoReminderPolicyService.MaximumCompactTextLength + 5);
        var compact = TodoReminderPolicyService.CompactText(longText, "unnamed");
        Assert(compact.Length == TodoReminderPolicyService.MaximumCompactTextLength &&
               compact.EndsWith('…'),
            "long reminder text should be capped at 90 UTF-16 code units with an ellipsis");
    }

    private static void TriggerTransactionCommitsTheWholeBatchAfterAnySurfaceSucceeds()
    {
        var service = new TodoReminderPolicyService(
            new AdjustableTimeProvider(Baseline));
        var first = Item("first", Baseline);
        var second = Item("second", Baseline.AddMinutes(-1));
        var batch = service.Evaluate(EnabledState(
            TodoPaper("paper", first, second))).DueBatch;

        var transaction = service.PlanTriggerTransaction(
            batch,
            new TodoReminderSurfaceOutcome(
                TargetSurfaceSucceeded: false,
                NotificationSurfaceSucceeded: true));

        Assert(transaction.Disposition == TodoReminderTriggerDisposition.Commit &&
               transaction.ShouldPlaySound &&
               transaction.ShouldMarkBatchTriggered &&
               transaction.RequiresImmediateSave &&
               transaction.RetryAfter == null,
            "either successful presentation channel should commit the full due batch");
        Assert(TodoReminderPolicyService.ApplyTriggerTransaction(transaction),
            "a commit transaction should apply its domain mutation");
        Assert(first.ReminderTriggered && second.ReminderTriggered,
            "a successful trigger must mark every item from the evaluated batch");
    }

    private static void FailedDeliveryKeepsTheWholeBatchPendingForRetry()
    {
        var service = new TodoReminderPolicyService(
            new AdjustableTimeProvider(Baseline));
        var item = Item("pending", Baseline);
        var batch = service.Evaluate(EnabledState(
            TodoPaper("paper", item))).DueBatch;

        var transaction = service.PlanTriggerTransaction(
            batch,
            new TodoReminderSurfaceOutcome(
                TargetSurfaceSucceeded: false,
                NotificationSurfaceSucceeded: false));

        Assert(transaction.Disposition == TodoReminderTriggerDisposition.Retry &&
               transaction.RetryAfter == TodoReminderPolicyService.DeliveryRetryInterval &&
               !transaction.ShouldPlaySound &&
               !transaction.ShouldMarkBatchTriggered &&
               !transaction.RequiresImmediateSave,
            "failure of both presentation channels should request the legacy 30 second retry");
        Assert(!TodoReminderPolicyService.ApplyTriggerTransaction(transaction) &&
               !item.ReminderTriggered,
            "a retry transaction must leave the reminder pending");
    }

    private static void EmptyBatchProducesNoTransaction()
    {
        var service = new TodoReminderPolicyService(
            new AdjustableTimeProvider(Baseline));
        var batch = service.Evaluate(EnabledState()).DueBatch;

        var transaction = service.PlanTriggerTransaction(
            batch,
            new TodoReminderSurfaceOutcome(
                TargetSurfaceSucceeded: true,
                NotificationSurfaceSucceeded: true));

        Assert(transaction.Disposition == TodoReminderTriggerDisposition.None &&
               transaction.RetryAfter == null &&
               !TodoReminderPolicyService.ApplyTriggerTransaction(transaction),
            "an empty due batch should neither commit nor retry");
    }

    private static AppState EnabledState(params PaperData[] papers) => new()
    {
        ExperimentalTodoReminders = true,
        Papers = [.. papers]
    };

    private static PaperData TodoPaper(
        string id,
        params PaperItem[] items) => new()
    {
        Id = id,
        Type = PaperTypes.Todo,
        Items = [.. items]
    };

    private static PaperItem Item(
        string id,
        DateTimeOffset? reminderAt,
        bool done = false,
        bool triggered = false) => new()
    {
        Id = id,
        Text = id,
        ReminderAt = reminderAt,
        Done = done,
        ReminderTriggered = triggered
    };

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

internal sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    internal AdjustableTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void Advance(TimeSpan duration)
    {
        _utcNow += duration;
    }
}
