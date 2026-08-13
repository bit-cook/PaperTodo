using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    internal void ReorderEdgeCapsuleWithinQueue(
        object? sender,
        EdgeCapsuleReorderRequestedEventArgs e)
    {
        Dispatcher.UIThread.VerifyAccess();
        var state = _state;
        if (_disposed || !_started || state is null)
        {
            return;
        }

        var slots = new List<(int GlobalIndex, PaperData Paper)>();
        for (var index = 0; index < state.Papers.Count; index++)
        {
            var paper = state.Papers[index];
            if (!paper.IsVisible ||
                !paper.IsCollapsed ||
                ParseQueueKey(QueueStorageKey(paper)).Normalize() != e.Queue)
            {
                continue;
            }
            slots.Add((index, paper));
        }

        var sourceIndex = slots.FindIndex(slot => string.Equals(
            slot.Paper.Id,
            e.PaperId,
            StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            return;
        }

        var reordered = slots.Select(slot => slot.Paper).ToList();
        var source = reordered[sourceIndex];
        reordered.RemoveAt(sourceIndex);
        var targetIndex = Math.Clamp(e.TargetIndex, 0, reordered.Count);
        reordered.Insert(targetIndex, source);

        for (var index = 0; index < slots.Count; index++)
        {
            state.Papers[slots[index].GlobalIndex] = reordered[index];
        }

        e.Handled = true;
        if (_edgePreviewSession is not null)
        {
            CloseEdgeCapsulePreview(animate: false, arrange: false);
        }
        ArrangeEdgeCapsules(
            animate: true,
            EdgeCapsuleTransitionReason.Drag);
        SaveCurrentState();
    }
}
