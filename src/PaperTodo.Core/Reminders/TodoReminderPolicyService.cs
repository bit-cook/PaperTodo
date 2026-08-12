using System.Text;

namespace PaperTodo;

/// <summary>
/// A pending reminder together with the domain objects that own it. Keeping the references in the
/// DTO lets an application adapter preserve the existing in-memory transaction semantics without
/// looking objects up again by potentially duplicated persisted IDs.
/// </summary>
public sealed record TodoReminderCandidate(
    PaperData Paper,
    PaperItem Item,
    DateTimeOffset ReminderAt)
{
    public string PaperId => Paper.Id;

    public string ItemId => Item.Id;
}

public sealed record TodoReminderPoll(
    DateTimeOffset ReminderAt,
    TimeSpan Delay);

public sealed record TodoReminderDueBatch(
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<TodoReminderCandidate> Reminders)
{
    public int Count => Reminders.Count;
}

public sealed record TodoReminderEvaluation(
    bool IsActive,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<TodoReminderCandidate> PendingReminders,
    TodoReminderDueBatch DueBatch,
    TodoReminderPoll? NextPoll);

public readonly record struct TodoReminderSurfaceOutcome(
    bool TargetSurfaceSucceeded,
    bool NotificationSurfaceSucceeded)
{
    public bool AnySurfaceSucceeded =>
        TargetSurfaceSucceeded || NotificationSurfaceSucceeded;
}

public enum TodoReminderTriggerDisposition
{
    None,
    Retry,
    Commit
}

/// <summary>
/// Describes the domain transaction after presentation was attempted. Persistence and UI refresh
/// remain effects owned by the application adapter, while their required ordering is explicit.
/// </summary>
public sealed record TodoReminderTriggerTransaction(
    TodoReminderTriggerDisposition Disposition,
    TodoReminderDueBatch Batch,
    TimeSpan? RetryAfter)
{
    public bool ShouldPlaySound =>
        Disposition == TodoReminderTriggerDisposition.Commit;

    public bool ShouldMarkBatchTriggered =>
        Disposition == TodoReminderTriggerDisposition.Commit;

    public bool RequiresImmediateSave =>
        Disposition == TodoReminderTriggerDisposition.Commit;
}

/// <summary>
/// Framework-neutral reminder selection, polling, batching, text and trigger-transaction policy.
/// Timers, tray notifications, sounds and paper surfaces are deliberately application effects.
/// </summary>
public sealed class TodoReminderPolicyService
{
    public static readonly TimeSpan MinimumPollInterval =
        TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan MaximumPollInterval =
        TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DeliveryRetryInterval =
        TimeSpan.FromSeconds(30);
    public const int MaximumCompactTextLength = 90;

    private static readonly char[] TextLineSeparators = ['\r', '\n', '\t'];
    private readonly TimeProvider _timeProvider;

    public TodoReminderPolicyService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public TodoReminderEvaluation Evaluate(
        AppState state,
        bool isShuttingDown = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        var now = _timeProvider.GetUtcNow();
        if (isShuttingDown || !state.ExperimentalTodoReminders)
        {
            var empty = Array.Empty<TodoReminderCandidate>();
            return new TodoReminderEvaluation(
                IsActive: false,
                now,
                empty,
                new TodoReminderDueBatch(now, empty),
                NextPoll: null);
        }

        var pending = SelectPendingReminders(state);
        var due = new List<TodoReminderCandidate>();
        DateTimeOffset? nextReminderAt = null;
        foreach (var candidate in pending)
        {
            if (candidate.ReminderAt <= now)
            {
                due.Add(candidate);
            }

            if (!nextReminderAt.HasValue || candidate.ReminderAt < nextReminderAt.Value)
            {
                nextReminderAt = candidate.ReminderAt;
            }
        }

        var pendingSnapshot = pending.ToArray();
        var dueBatch = new TodoReminderDueBatch(now, due.ToArray());
        var nextPoll = nextReminderAt is { } reminderAt
            ? new TodoReminderPoll(
                reminderAt,
                ClampPollDelay(reminderAt - now))
            : null;
        return new TodoReminderEvaluation(
            IsActive: true,
            now,
            pendingSnapshot,
            dueBatch,
            nextPoll);
    }

    public TodoReminderTriggerTransaction PlanTriggerTransaction(
        TodoReminderDueBatch batch,
        TodoReminderSurfaceOutcome surfaceOutcome)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Count == 0)
        {
            return new TodoReminderTriggerTransaction(
                TodoReminderTriggerDisposition.None,
                batch,
                RetryAfter: null);
        }

        if (!surfaceOutcome.AnySurfaceSucceeded)
        {
            return new TodoReminderTriggerTransaction(
                TodoReminderTriggerDisposition.Retry,
                batch,
                DeliveryRetryInterval);
        }

        return new TodoReminderTriggerTransaction(
            TodoReminderTriggerDisposition.Commit,
            batch,
            RetryAfter: null);
    }

    /// <summary>
    /// Applies only the in-memory part of a successful transaction. The application adapter must
    /// then refresh affected surfaces and synchronously persist when RequiresImmediateSave is true.
    /// </summary>
    public static bool ApplyTriggerTransaction(
        TodoReminderTriggerTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!transaction.ShouldMarkBatchTriggered)
        {
            return false;
        }

        foreach (var candidate in transaction.Batch.Reminders)
        {
            candidate.Item.ReminderTriggered = true;
        }

        return true;
    }

    public static string CompactText(
        string? text,
        string unnamedText)
    {
        ArgumentNullException.ThrowIfNull(unnamedText);

        var builder = new StringBuilder();
        foreach (var part in (text ?? string.Empty).Split(
                     TextLineSeparators,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                _ = builder.Append(' ');
            }
            _ = builder.Append(trimmed);
        }

        var compact = builder.ToString();
        if (string.IsNullOrWhiteSpace(compact))
        {
            return unnamedText;
        }

        return compact.Length <= MaximumCompactTextLength
            ? compact
            : compact[..(MaximumCompactTextLength - 1)] + "…";
    }

    private static List<TodoReminderCandidate> SelectPendingReminders(
        AppState state)
    {
        var pending = new List<TodoReminderCandidate>();
        foreach (var paper in state.Papers)
        {
            if (paper.Type != PaperTypes.Todo)
            {
                continue;
            }

            foreach (var item in paper.Items)
            {
                if (item.Done ||
                    item.ReminderTriggered ||
                    item.ReminderAt is not { } reminderAt)
                {
                    continue;
                }

                pending.Add(new TodoReminderCandidate(
                    paper,
                    item,
                    reminderAt));
            }
        }

        return pending;
    }

    private static TimeSpan ClampPollDelay(TimeSpan delay)
    {
        if (delay <= MinimumPollInterval)
        {
            return MinimumPollInterval;
        }

        return delay >= MaximumPollInterval
            ? MaximumPollInterval
            : delay;
    }
}
