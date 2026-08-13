using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    private const double EdgeFloatingPullOutThresholdDip = 42;
    private const double EdgeFloatingDragScanMilliseconds = 75;

    private readonly HashSet<EdgeCapsuleNodeHost> _floatingDragSubscribedNodes = [];
    private DispatcherTimer? _floatingDragScanTimer;
    private EdgeFloatingDragGesture? _floatingDragGesture;
    private EdgeCapsuleFloatingDragWindow? _floatingDragWindow;
    private bool _floatingDragRuntimeAttached;

    private sealed class EdgeFloatingDragGesture(
        EdgeCapsuleNodeHost node,
        EdgeCapsuleQueueKey sourceQueue,
        DeviceScreenPoint downPoint)
    {
        public EdgeCapsuleNodeHost Node { get; } = node;
        public EdgeCapsuleQueueKey SourceQueue { get; } = sourceQueue.Normalize();
        public DeviceScreenPoint DownPoint { get; } = downPoint;
        public bool PulledOut { get; set; }
    }

    internal void AttachFloatingEdgeDragRuntime()
    {
        if (_floatingDragRuntimeAttached)
        {
            return;
        }

        _floatingDragRuntimeAttached = true;
        _edges.Disposing += DisposeFloatingEdgeDragRuntime;
        _floatingDragScanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(EdgeFloatingDragScanMilliseconds)
        };
        _floatingDragScanTimer.Tick += OnFloatingDragScanTick;
        _floatingDragScanTimer.Start();
        RefreshFloatingDragSubscriptions();
    }

    private void OnFloatingDragScanTick(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _floatingDragScanTimer))
        {
            return;
        }
        RefreshFloatingDragSubscriptions();
    }

    private void RefreshFloatingDragSubscriptions()
    {
        if (_disposed)
        {
            return;
        }

        var currentNodes = _edges.Surfaces
            .SelectMany(surface => surface.Nodes)
            .ToHashSet();
        foreach (var node in currentNodes)
        {
            if (_floatingDragSubscribedNodes.Add(node))
            {
                node.BodyPointerPressed += OnFloatingDragPointerPressed;
                node.BodyPointerMoved += OnFloatingDragPointerMoved;
                node.BodyPointerReleased += OnFloatingDragPointerReleased;
                node.BodyPointerCaptureLost += OnFloatingDragPointerCaptureLost;
            }
        }

        foreach (var node in _floatingDragSubscribedNodes
                     .Where(node => !currentNodes.Contains(node))
                     .ToArray())
        {
            if (_floatingDragGesture?.Node == node)
            {
                EndFloatingDragVisual();
            }
            UnsubscribeFloatingDragNode(node);
            _floatingDragSubscribedNodes.Remove(node);
        }
    }

    private void OnFloatingDragPointerPressed(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point)
    {
        EndFloatingDragVisual();
        var sourceSurface = _edges.Surfaces.FirstOrDefault(surface =>
            surface.Nodes.Contains(node));
        if (sourceSurface is null ||
            _state?.Papers.Any(paper =>
                string.Equals(paper.Id, node.PaperId, StringComparison.Ordinal) &&
                paper.IsVisible &&
                paper.IsCollapsed) != true)
        {
            return;
        }

        _floatingDragGesture = new EdgeFloatingDragGesture(
            node,
            sourceSurface.Key,
            point);
    }

    private void OnFloatingDragPointerMoved(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point,
        bool leftButtonPressed)
    {
        var gesture = _floatingDragGesture;
        if (gesture is null || gesture.Node != node)
        {
            return;
        }
        if (!leftButtonPressed)
        {
            EndFloatingDragVisual();
            return;
        }

        if (!gesture.PulledOut)
        {
            var frame = node.AppliedFrame;
            var scaleX = Math.Max(1, frame.DpiScaleX);
            var scaleY = Math.Max(1, frame.DpiScaleY);
            var inward = gesture.SourceQueue.Edge == EdgeCapsuleEdge.Left
                ? (point.X - gesture.DownPoint.X) / scaleX
                : (gesture.DownPoint.X - point.X) / scaleX;
            var vertical = Math.Abs(point.Y - gesture.DownPoint.Y) / scaleY;
            if (inward < EdgeFloatingPullOutThresholdDip ||
                inward < vertical * 0.8)
            {
                return;
            }

            var state = _state;
            var paper = state?.Papers.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, node.PaperId, StringComparison.Ordinal));
            if (state is null || paper is null)
            {
                EndFloatingDragVisual();
                return;
            }

            if (_edgePreviewSession is not null)
            {
                CloseEdgeCapsulePreview(animate: false);
            }

            gesture.PulledOut = true;
            node.Root.Opacity = 0.16;
            var proxy = new EdgeCapsuleFloatingDragWindow(paper, state);
            _floatingDragWindow = proxy;
            proxy.MoveTo(point);
            proxy.Show();
        }

        var window = _floatingDragWindow;
        if (window is null)
        {
            return;
        }

        window.MoveTo(point);
        var ready = TryResolveEdgeDropQueue(
                point,
                node.PaperId,
                out var targetQueue,
                out _) &&
            targetQueue.Normalize() != gesture.SourceQueue;
        window.SetTargetReady(ready);
    }

    private void OnFloatingDragPointerReleased(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point)
    {
        if (_floatingDragGesture?.Node == node)
        {
            EndFloatingDragVisual();
        }
    }

    private void OnFloatingDragPointerCaptureLost(EdgeCapsuleNodeHost node)
    {
        if (_floatingDragGesture?.Node == node)
        {
            EndFloatingDragVisual();
        }
    }

    private void EndFloatingDragVisual()
    {
        var gesture = _floatingDragGesture;
        _floatingDragGesture = null;
        if (gesture is not null)
        {
            gesture.Node.Root.Opacity = 1;
        }

        var proxy = _floatingDragWindow;
        _floatingDragWindow = null;
        if (proxy is not null)
        {
            proxy.Close();
        }
        EnsureEdgeCapsulePreviewRuntimeState();
    }

    private void UnsubscribeFloatingDragNode(EdgeCapsuleNodeHost node)
    {
        node.BodyPointerPressed -= OnFloatingDragPointerPressed;
        node.BodyPointerMoved -= OnFloatingDragPointerMoved;
        node.BodyPointerReleased -= OnFloatingDragPointerReleased;
        node.BodyPointerCaptureLost -= OnFloatingDragPointerCaptureLost;
    }

    private void DisposeFloatingEdgeDragRuntime()
    {
        if (!_floatingDragRuntimeAttached)
        {
            return;
        }

        _floatingDragRuntimeAttached = false;
        _edges.Disposing -= DisposeFloatingEdgeDragRuntime;
        if (_floatingDragScanTimer is not null)
        {
            _floatingDragScanTimer.Stop();
            _floatingDragScanTimer.Tick -= OnFloatingDragScanTick;
            _floatingDragScanTimer = null;
        }
        EndFloatingDragVisual();
        foreach (var node in _floatingDragSubscribedNodes.ToArray())
        {
            UnsubscribeFloatingDragNode(node);
        }
        _floatingDragSubscribedNodes.Clear();
    }
}
