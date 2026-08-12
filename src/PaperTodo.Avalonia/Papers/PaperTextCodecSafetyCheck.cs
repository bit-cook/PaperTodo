namespace PaperTodo.Avalonia.Papers;

/// <summary>
/// Reflection-free, no-UI regression check for the provider-state safety boundary. It runs before
/// single-instance, state-store and Avalonia initialization and never touches user files.
/// </summary>
internal static class PaperTextCodecSafetyCheck
{
    private const string Argument = "--check-paper-editor-safety";

    public static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(arguments[0], Argument, StringComparison.OrdinalIgnoreCase);

    public static int Run()
    {
        try
        {
            var pluginNote = new PaperData
            {
                Type = PaperTypes.Note,
                BodyProviderId = "sample.web",
                Content = "opaque-provider-state"
            };
            Require(!PaperTextCodec.CanEditBody(pluginNote), "plugin note must be read-only");
            Require(PaperTextCodec.ToEditorText(pluginNote).Length == 0,
                "plugin state must not be exposed as editor text");
            PaperTextCodec.ApplyEditorText(pluginNote, "replacement");
            Require(pluginNote.Content == "opaque-provider-state",
                "plugin state must not be changed by the basic editor codec");

            var markdownNote = new PaperData
            {
                Type = PaperTypes.Note,
                BodyProviderId = PaperBodyProviderIds.Markdown,
                Content = "before"
            };
            Require(PaperTextCodec.CanEditBody(markdownNote), "Markdown note must be editable");
            PaperTextCodec.ApplyEditorText(markdownNote, "after");
            Require(markdownNote.Content == "after" &&
                    PaperTextCodec.ToEditorText(markdownNote) == "after",
                "Markdown note must round-trip editor text");

            var reminderAt = DateTimeOffset.UtcNow.AddHours(1);
            var keptItem = new PaperItem
            {
                Id = "kept-id",
                Text = "kept",
                Order = 0,
                ReminderAt = reminderAt
            };
            keptItem.LinkPaper("linked-paper");
            var todo = new PaperData { Type = PaperTypes.Todo, Items = [keptItem] };
            Require(PaperTextCodec.CanEditBody(todo), "Todo must be editable");
            PaperTextCodec.ApplyEditorText(todo, "[ ] edited");
            Require(todo.Items.Count == 1 &&
                    ReferenceEquals(todo.Items[0], keptItem) &&
                    todo.Items[0].Id == "kept-id" &&
                    todo.Items[0].Text == "edited" &&
                    todo.Items[0].ReminderAt == reminderAt &&
                    todo.Items[0].LinkedPaperId == "linked-paper",
                "editing a Todo row must preserve its identity, reminder and link metadata");

            var anchoredFirst = new PaperItem
                { Id = "first", Text = "first", Order = 0, ReminderAt = reminderAt };
            anchoredFirst.LinkPaper("anchored-link");
            var anchoredSecond = new PaperItem
                { Id = "second", Text = "second", Order = 1 };
            todo.Items = [anchoredFirst, anchoredSecond];
            PaperTextCodec.ApplyEditorText(todo, "[ ] inserted\n[ ] first\n[ ] second");
            Require(todo.Items.Count == 3 &&
                    !ReferenceEquals(todo.Items[0], anchoredFirst) &&
                    ReferenceEquals(todo.Items[1], anchoredFirst) &&
                    ReferenceEquals(todo.Items[2], anchoredSecond) &&
                    todo.Items[1].ReminderAt == reminderAt,
                "inserting a Todo row must not transfer existing row metadata to the insertion");

            PaperTextCodec.ApplyEditorText(todo, "[ ] first\n[ ] second");
            Require(todo.Items.Count == 2 &&
                    ReferenceEquals(todo.Items[0], anchoredFirst) &&
                    ReferenceEquals(todo.Items[1], anchoredSecond),
                "deleting an inserted Todo row must retain the anchored row identities");

            PaperTextCodec.ApplyEditorText(todo, "[ ] second\n[ ] first");
            Require(todo.Items.Count == 2 &&
                    ReferenceEquals(todo.Items[0], anchoredSecond) &&
                    ReferenceEquals(todo.Items[1], anchoredFirst) &&
                    todo.Items[1].ReminderAt == reminderAt &&
                    todo.Items[1].LinkedPaperId == "anchored-link",
                "reordering exact Todo rows must move their identities and metadata together");

            PaperTextCodec.ApplyEditorText(todo, "[ ] second\n[x] first");
            Require(ReferenceEquals(todo.Items[1], anchoredFirst) &&
                    todo.Items[1].Done &&
                    todo.Items[1].ReminderAt == null &&
                    todo.Items[1].LinkedPaperId == "anchored-link",
                "completing a Todo must retain links while applying the legacy reminder policy");

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"PaperTodo editor safety check failed: {exception}");
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
