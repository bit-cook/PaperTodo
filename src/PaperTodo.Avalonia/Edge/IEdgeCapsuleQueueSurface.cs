using Avalonia;
using Avalonia.Controls;

namespace PaperTodo.Avalonia.Edge;

internal interface IEdgeCapsuleQueueSurface
{
    EdgeCapsuleQueueKey Key { get; }

    Window Window { get; }

    IReadOnlyCollection<EdgeCapsuleNodeHost> Nodes { get; }

    DeviceScreenRect HostBounds { get; }

    DeviceScreenRect InteractiveBounds { get; }

    event EventHandler<EdgeCapsuleTransparentHitTestEventArgs>? TransparentHitTestRequested;
    event EventHandler<EdgeCapsuleReorderRequestedEventArgs>? ReorderRequested;

    EdgeCapsuleNodeHost AttachPaper(string paperId, Control chrome);

    bool DetachPaper(string paperId);

    bool TryGetNode(string paperId, out EdgeCapsuleNodeHost node);

    void ApplyHostBounds(DeviceScreenRect hostBounds);

    void Apply(string paperId, EdgeCapsulePresentationFrame frame, EdgeCapsuleMotion motion);

    bool IsInteractiveDevicePoint(PixelPoint point);

    void Close();
}

internal sealed class EdgeCapsuleTransparentHitTestEventArgs(PixelPoint devicePoint) : EventArgs
{
    public PixelPoint DevicePoint { get; } = devicePoint;

    public bool IsTransparent { get; set; }
}

internal sealed class EdgeCapsuleReorderRequestedEventArgs(
    EdgeCapsuleQueueKey queue,
    string paperId,
    int targetIndex) : EventArgs
{
    public EdgeCapsuleQueueKey Queue { get; } = queue.Normalize();
    public string PaperId { get; } = paperId;
    public int TargetIndex { get; } = Math.Max(0, targetIndex);
    public bool Handled { get; set; }
}
