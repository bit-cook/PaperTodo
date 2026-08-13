using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    private const double EdgeQueueDropZoneDip = 72;

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

        var source = state.Papers.FirstOrDefault(paper =>
            string.Equals(paper.Id, e.PaperId, StringComparison.Ordinal));
        if (source is null || !source.IsVisible || !source.IsCollapsed)
        {
            return;
        }

        if (WindowsPointerPosition.TryGet(out var pointer) &&
            TryResolveEdgeDropQueue(pointer, source.Id, out var targetQueue, out var targetIndex) &&
            targetQueue.Normalize() != e.Queue.Normalize())
        {
            TransferEdgeCapsuleToQueue(
                source,
                targetQueue,
                targetIndex);
            e.Handled = true;
            CompleteEdgeCapsuleDragTransaction();
            return;
        }

        ReorderEdgeCapsuleInQueue(state, e.Queue, source, e.TargetIndex);
        e.Handled = true;
        CompleteEdgeCapsuleDragTransaction();
    }

    private void ReorderEdgeCapsuleInQueue(
        AppState state,
        EdgeCapsuleQueueKey queue,
        PaperData source,
        int requestedTargetIndex)
    {
        var slots = new List<(int GlobalIndex, PaperData Paper)>();
        for (var index = 0; index < state.Papers.Count; index++)
        {
            var paper = state.Papers[index];
            if (!paper.IsVisible ||
                !paper.IsCollapsed ||
                ParseQueueKey(QueueStorageKey(paper)).Normalize() != queue.Normalize())
            {
                continue;
            }
            slots.Add((index, paper));
        }

        var sourceIndex = slots.FindIndex(slot => string.Equals(
            slot.Paper.Id,
            source.Id,
            StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            return;
        }

        var reordered = slots.Select(slot => slot.Paper).ToList();
        reordered.RemoveAt(sourceIndex);
        var targetIndex = Math.Clamp(requestedTargetIndex, 0, reordered.Count);
        reordered.Insert(targetIndex, source);

        for (var index = 0; index < slots.Count; index++)
        {
            state.Papers[slots[index].GlobalIndex] = reordered[index];
        }
    }

    private void TransferEdgeCapsuleToQueue(
        PaperData source,
        EdgeCapsuleQueueKey targetQueue,
        int requestedTargetIndex)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        targetQueue = targetQueue.Normalize();

        state.Papers.Remove(source);
        source.CapsuleMonitorDeviceName = targetQueue.MonitorDeviceName;
        source.CapsuleSide = targetQueue.Edge == EdgeCapsuleEdge.Left
            ? DeepCapsuleSides.Left
            : DeepCapsuleSides.Right;

        var targetSlots = new List<int>();
        for (var index = 0; index < state.Papers.Count; index++)
        {
            var candidate = state.Papers[index];
            if (candidate.IsVisible &&
                candidate.IsCollapsed &&
                ParseQueueKey(QueueStorageKey(candidate)).Normalize() == targetQueue)
            {
                targetSlots.Add(index);
            }
        }

        var targetIndex = Math.Clamp(requestedTargetIndex, 0, targetSlots.Count);
        var globalInsertIndex = targetSlots.Count == 0
            ? state.Papers.Count
            : targetIndex < targetSlots.Count
                ? targetSlots[targetIndex]
                : targetSlots[^1] + 1;
        state.Papers.Insert(
            Math.Clamp(globalInsertIndex, 0, state.Papers.Count),
            source);
    }

    private bool TryResolveEdgeDropQueue(
        DeviceScreenPoint pointer,
        string sourcePaperId,
        out EdgeCapsuleQueueKey queue,
        out int targetIndex)
    {
        foreach (var surface in _edges.Surfaces)
        {
            if (surface.InteractiveBounds.IsEmpty ||
                !ContainsDevicePoint(surface.InteractiveBounds, pointer))
            {
                continue;
            }

            queue = surface.Key.Normalize();
            targetIndex = ResolveTargetQueueIndex(surface, sourcePaperId, pointer.Y);
            return true;
        }

        var screens = _stateStorePlatform.Screens;
        if (screens is not null)
        {
            foreach (var screen in screens.All)
            {
                var work = screen.WorkingArea;
                if (pointer.X < work.X || pointer.X >= work.Right ||
                    pointer.Y < work.Y || pointer.Y >= work.Bottom)
                {
                    continue;
                }

                var threshold = Math.Max(
                    32,
                    EdgeQueueDropZoneDip * Math.Max(1, screen.Scaling));
                EdgeCapsuleEdge? edge = null;
                if (pointer.X <= work.X + threshold)
                {
                    edge = EdgeCapsuleEdge.Left;
                }
                else if (pointer.X >= work.Right - threshold)
                {
                    edge = EdgeCapsuleEdge.Right;
                }

                if (!edge.HasValue)
                {
                    break;
                }

                queue = new EdgeCapsuleQueueKey(
                    screen.DisplayName ?? string.Empty,
                    edge.Value).Normalize();
                if (_edges.TryGet(queue, out var targetSurface))
                {
                    targetIndex = ResolveTargetQueueIndex(
                        targetSurface,
                        sourcePaperId,
                        pointer.Y);
                }
                else
                {
                    targetIndex = 0;
                }
                return true;
            }
        }

        queue = default;
        targetIndex = 0;
        return false;
    }

    private static int ResolveTargetQueueIndex(
        IEdgeCapsuleQueueSurface surface,
        string sourcePaperId,
        double pointerY)
    {
        var peers = surface.Nodes
            .Where(node =>
                !string.Equals(node.PaperId, sourcePaperId, StringComparison.Ordinal) &&
                node.AppliedFrame.Visible &&
                !node.AppliedFrame.Bounds.IsEmpty)
            .OrderBy(node => node.AppliedFrame.Bounds.Top)
            .ThenBy(node => node.PaperId, StringComparer.Ordinal)
            .ToArray();
        var index = 0;
        foreach (var peer in peers)
        {
            var midpoint = peer.AppliedFrame.Bounds.Top +
                peer.AppliedFrame.Bounds.Height / 2.0;
            if (pointerY < midpoint)
            {
                break;
            }
            index++;
        }
        return Math.Clamp(index, 0, peers.Length);
    }

    private static bool ContainsDevicePoint(
        DeviceScreenRect bounds,
        DeviceScreenPoint point) =>
        !bounds.IsEmpty &&
        point.X >= bounds.Left &&
        point.X < bounds.Right &&
        point.Y >= bounds.Top &&
        point.Y < bounds.Bottom;

    private void CompleteEdgeCapsuleDragTransaction()
    {
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
