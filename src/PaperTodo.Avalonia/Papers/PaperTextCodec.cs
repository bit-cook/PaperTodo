namespace PaperTodo.Avalonia.Papers;

internal static class PaperTextCodec
{
    public static bool CanEditBody(PaperData paper) =>
        paper.Type == PaperTypes.Todo ||
        paper.Type == PaperTypes.Note &&
        string.Equals(
            paper.BodyProviderId,
            PaperBodyProviderIds.Markdown,
            StringComparison.Ordinal);

    public static string ToEditorText(PaperData paper)
    {
        if (!CanEditBody(paper))
        {
            // Non-Markdown note content is provider-owned opaque state. Never expose it through
            // the basic text editor, where an ordinary edit could corrupt the provider payload.
            return string.Empty;
        }

        if (paper.Type == PaperTypes.Note)
        {
            return paper.Content ?? string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            paper.Items
                .OrderBy(item => item.Order)
                .Select(item => item.Done ? $"[x] {item.Text}" : $"[ ] {item.Text}"));
    }

    public static void ApplyEditorText(PaperData paper, string text)
    {
        if (!CanEditBody(paper))
        {
            return;
        }

        if (paper.Type == PaperTypes.Note)
        {
            paper.Content = text;
            return;
        }

        var previous = paper.Items.OrderBy(item => item.Order).ToArray();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var parsed = new List<ParsedTodoLine>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var done = line.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase);
            var body = done || line.StartsWith("[ ] ", StringComparison.Ordinal)
                ? line[4..]
                : line;
            if (string.IsNullOrWhiteSpace(body) && lines.Length == 1)
            {
                continue;
            }

            parsed.Add(new ParsedTodoLine(body, done));
        }

        var aligned = AlignExistingItems(previous, parsed);
        var updated = new List<PaperItem>(parsed.Count);
        for (var index = 0; index < parsed.Count; index++)
        {
            var line = parsed[index];
            var item = aligned[index] ?? new PaperItem();
            var textChanged = !string.Equals(item.Text, line.Text, StringComparison.Ordinal);
            var becameDone = !item.Done && line.Done;

            // Editing a row must retain its domain identity and attached link/reminder metadata.
            // The sequence alignment also leaves genuinely inserted lines unassigned, preventing
            // an insertion above a reminder from silently moving that reminder to the new row.
            item.Text = line.Text;
            item.Done = line.Done;
            item.Order = index;
            if (becameDone || textChanged && item.ReminderTriggered)
            {
                item.ReminderAt = null;
                item.ReminderTriggered = false;
            }

            updated.Add(item);
        }

        paper.Items = updated;
    }

    private static PaperItem?[] AlignExistingItems(
        IReadOnlyList<PaperItem> previous,
        IReadOnlyList<ParsedTodoLine> current)
    {
        var result = new PaperItem?[current.Count];
        var previousMatched = new bool[previous.Count];
        var currentMatched = new bool[current.Count];
        var previousSignatures = IndexUniqueSignatures(
            previous.Select(item => new TodoLineSignature(item.Text ?? string.Empty, item.Done)));
        var currentSignatures = IndexUniqueSignatures(
            current.Select(line => new TodoLineSignature(line.Text, line.Done)));

        // Exact unique rows retain identity even when the text editor moves them across the list.
        // A purely order-preserving diff would interpret a swap as two text substitutions and
        // silently leave reminders/links attached to the wrong visible text.
        foreach (var (signature, previousIndex) in previousSignatures)
        {
            if (previousIndex < 0 ||
                !currentSignatures.TryGetValue(signature, out var currentIndex) ||
                currentIndex < 0)
            {
                continue;
            }

            previousMatched[previousIndex] = true;
            currentMatched[currentIndex] = true;
            result[currentIndex] = previous[previousIndex];
        }

        var remainingPrevious = Enumerable.Range(0, previous.Count)
            .Where(index => !previousMatched[index])
            .Select(index => previous[index])
            .ToArray();
        var remainingCurrentIndices = Enumerable.Range(0, current.Count)
            .Where(index => !currentMatched[index])
            .ToArray();
        var remainingCurrent = remainingCurrentIndices
            .Select(index => current[index])
            .ToArray();
        var alignedRemaining = AlignRemainingItems(remainingPrevious, remainingCurrent);
        for (var index = 0; index < remainingCurrentIndices.Length; index++)
        {
            result[remainingCurrentIndices[index]] = alignedRemaining[index];
        }

        return result;
    }

    private static Dictionary<TodoLineSignature, int> IndexUniqueSignatures(
        IEnumerable<TodoLineSignature> signatures)
    {
        var result = new Dictionary<TodoLineSignature, int>();
        var index = 0;
        foreach (var signature in signatures)
        {
            if (!result.TryAdd(signature, index))
            {
                // -1 means the visible signature is ambiguous; keep those rows in the ordered
                // alignment instead of guessing which duplicate owns hidden metadata.
                result[signature] = -1;
            }
            index++;
        }
        return result;
    }

    private static PaperItem?[] AlignRemainingItems(
        IReadOnlyList<PaperItem> previous,
        IReadOnlyList<ParsedTodoLine> current)
    {
        var previousCount = previous.Count;
        var currentCount = current.Count;
        var costs = new double[previousCount + 1, currentCount + 1];
        var operations = new AlignmentOperation[previousCount + 1, currentCount + 1];
        for (var oldIndex = 1; oldIndex <= previousCount; oldIndex++)
        {
            costs[oldIndex, 0] = oldIndex;
            operations[oldIndex, 0] = AlignmentOperation.Delete;
        }
        for (var newIndex = 1; newIndex <= currentCount; newIndex++)
        {
            costs[0, newIndex] = newIndex;
            operations[0, newIndex] = AlignmentOperation.Insert;
        }

        for (var oldIndex = 1; oldIndex <= previousCount; oldIndex++)
        {
            for (var newIndex = 1; newIndex <= currentCount; newIndex++)
            {
                var substitution = costs[oldIndex - 1, newIndex - 1] +
                    SubstitutionCost(previous[oldIndex - 1], current[newIndex - 1]);
                var deletion = costs[oldIndex - 1, newIndex] + 1;
                var insertion = costs[oldIndex, newIndex - 1] + 1;

                // Prefer preserving an existing row on an exact tie. A substitution always costs
                // less than delete+insert, while exact neighbours anchor bulk insert/delete edits.
                if (substitution <= deletion && substitution <= insertion)
                {
                    costs[oldIndex, newIndex] = substitution;
                    operations[oldIndex, newIndex] = AlignmentOperation.Substitute;
                }
                else if (deletion <= insertion)
                {
                    costs[oldIndex, newIndex] = deletion;
                    operations[oldIndex, newIndex] = AlignmentOperation.Delete;
                }
                else
                {
                    costs[oldIndex, newIndex] = insertion;
                    operations[oldIndex, newIndex] = AlignmentOperation.Insert;
                }
            }
        }

        var result = new PaperItem?[currentCount];
        var oldCursor = previousCount;
        var newCursor = currentCount;
        while (oldCursor > 0 || newCursor > 0)
        {
            switch (operations[oldCursor, newCursor])
            {
                case AlignmentOperation.Substitute:
                    result[newCursor - 1] = previous[oldCursor - 1];
                    oldCursor--;
                    newCursor--;
                    break;
                case AlignmentOperation.Delete:
                    oldCursor--;
                    break;
                case AlignmentOperation.Insert:
                    newCursor--;
                    break;
                default:
                    throw new InvalidOperationException("Todo row alignment did not make progress.");
            }
        }

        return result;
    }

    private static double SubstitutionCost(PaperItem previous, ParsedTodoLine current)
    {
        if (string.Equals(previous.Text, current.Text, StringComparison.Ordinal))
        {
            return previous.Done == current.Done ? 0 : 0.2;
        }

        var previousText = previous.Text ?? string.Empty;
        var currentText = current.Text;
        var comparisonLength = Math.Min(64, Math.Min(previousText.Length, currentText.Length));
        var commonPrefix = 0;
        while (commonPrefix < comparisonLength &&
               previousText[commonPrefix] == currentText[commonPrefix])
        {
            commonPrefix++;
        }

        var remaining = comparisonLength - commonPrefix;
        var commonSuffix = 0;
        while (commonSuffix < remaining &&
               previousText[previousText.Length - 1 - commonSuffix] ==
               currentText[currentText.Length - 1 - commonSuffix])
        {
            commonSuffix++;
        }

        var denominator = Math.Max(1, Math.Min(64, Math.Max(previousText.Length, currentText.Length)));
        var similarity = (double)(commonPrefix + commonSuffix) / denominator;
        var cost = 0.55 + 0.4 * (1 - similarity);
        return previous.Done == current.Done ? cost : Math.Min(0.99, cost + 0.04);
    }

    private readonly record struct ParsedTodoLine(string Text, bool Done);

    private readonly record struct TodoLineSignature(string Text, bool Done);

    private enum AlignmentOperation : byte
    {
        None,
        Substitute,
        Delete,
        Insert
    }
}
