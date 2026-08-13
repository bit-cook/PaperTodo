using Avalonia.Threading;

namespace PaperTodo.Avalonia.Application;

internal readonly record struct PaperTrayEntry(
    string Id,
    string Title,
    string Type,
    bool IsVisible,
    bool IsCollapsed);

internal sealed partial class PaperWorkspaceController
{
    internal IReadOnlyList<PaperTrayEntry> GetTrayPapers()
    {
        Dispatcher.UIThread.VerifyAccess();
        var state = _state;
        if (_disposed || !_started || state is null)
        {
            return Array.Empty<PaperTrayEntry>();
        }

        return state.Papers
            .Select(paper => new PaperTrayEntry(
                paper.Id,
                paper.Title,
                paper.Type,
                paper.IsVisible,
                paper.IsCollapsed))
            .ToArray();
    }

    internal void ActivatePaperFromTray(string paperId)
    {
        Dispatcher.UIThread.VerifyAccess();
        ActivateLinkedPaperFromQuickLaunch(paperId);
    }
}
