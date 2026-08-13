using Avalonia.Threading;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    internal void ActivateLinkedPaperFromQuickLaunch(string paperId)
    {
        Dispatcher.UIThread.VerifyAccess();
        var state = _state;
        if (_disposed || !_started || state is null || string.IsNullOrWhiteSpace(paperId))
        {
            return;
        }

        var paper = state.Papers.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, paperId, StringComparison.Ordinal));
        if (paper is null)
        {
            return;
        }

        if (_edgePreviewSession is not null)
        {
            CloseEdgeCapsulePreview(animate: false, arrange: false);
        }

        paper.IsVisible = true;
        paper.IsCollapsed = false;
        if (!_papers.TryGet(paper.Id, out var surface))
        {
            surface = CreatePaperSurface(paper);
        }

        surface.Show();
        surface.RefreshFromModel();
        surface.Window.Activate();
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
    }
}
