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
