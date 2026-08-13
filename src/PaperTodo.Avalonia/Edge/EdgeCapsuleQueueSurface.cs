using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;

namespace PaperTodo.Avalonia.Edge;

/// <summary>
/// One stable native surface for one physical (monitor, edge) queue. Every paper is represented
/// by an inner composition node, so steady-state queue motion never mutates native window bounds.
/// </summary>
internal sealed class EdgeCapsuleQueueSurface : Window, IEdgeCapsuleQueueSurface
{
    private const double ReorderThresholdDip = 5;

    private readonly Canvas _motionPlane;
    private readonly Dictionary<string, EdgeCapsuleNodeHost> _nodes = new(StringComparer.Ordinal);
    private DeviceScreenRect _hostBounds;
    private DeviceScreenRect _interactiveBounds;
    private double _dpiScaleX = 1;
    private double _dpiScaleY = 1;
    private Win32Properties.CustomWndProcHookCallback? _wndProcHook;
    private bool _animationFrameRequested;
    private string? _gesturePaperId;
    private DeviceScreenPoint _gestureDownPoint;
    private int _gesturePointerOffsetY;
    private EdgeCapsulePresentationFrame _gestureOriginalFrame = EdgeCapsulePresentationFrame.Hidden;
    private bool _gestureReordering;
    private int _gestureTargetIndex;

    private const uint WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);

    public EdgeCapsuleQueueSurface(EdgeCapsuleQueueKey key)
    {
        Key = key.Normalize();
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        SizeToContent = SizeToContent.Manual;

        _motionPlane = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent
        };
        Content = _motionPlane;

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        PointerExited += OnPointerExited;
        Opened += OnOpened;
    }

    public EdgeCapsuleQueueKey Key { get; }

    Window IEdgeCapsuleQueueSurface.Window => this;

    public IReadOnlyCollection<EdgeCapsuleNodeHost> Nodes => _nodes.Values;

    public DeviceScreenRect HostBounds => _hostBounds;

    public DeviceScreenRect InteractiveBounds => _interactiveBounds;

    public event EventHandler<EdgeCapsuleTransparentHitTestEventArgs>? TransparentHitTestRequested;
    public event EventHandler<EdgeCapsuleReorderRequestedEventArgs>? ReorderRequested;

    public EdgeCapsuleNodeHost AttachPaper(string paperId, Control chrome)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(paperId);
        ArgumentNullException.ThrowIfNull(chrome);
        if (_nodes.ContainsKey(paperId))
        {
            throw new InvalidOperationException($"Edge capsule '{paperId}' is already attached.");
        }

        var node = new EdgeCapsuleNodeHost(paperId, chrome);
        node.BodyPointerPressed += OnNodeBodyPointerPressed;
        node.BodyPointerMoved += OnNodeBodyPointerMoved;
        node.BodyPointerReleased += OnNodeBodyPointerReleased;
        node.BodyPointerCaptureLost += OnNodeBodyPointerCaptureLost;
        _motionPlane.Children.Add(node.Root);
        _nodes.Add(paperId, node);
        return node;
    }

    public bool DetachPaper(string paperId)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!_nodes.Remove(paperId, out var node))
        {
            return false;
        }

        if (string.Equals(_gesturePaperId, paperId, StringComparison.Ordinal))
        {
            ResetReorderGesture();
        }
        UnsubscribeNode(node);
        _motionPlane.Children.Remove(node.Root);
        node.Dispose();
        RebuildInteractiveBounds();
        return true;
    }

    public bool TryGetNode(string paperId, out EdgeCapsuleNodeHost node) =>
        _nodes.TryGetValue(paperId, out node!);

    public void ApplyHostBounds(DeviceScreenRect hostBounds) =>
        ApplyHostBounds(hostBounds, _dpiScaleX, _dpiScaleY);

    private void ApplyHostBounds(
        DeviceScreenRect hostBounds,
        double dpiScaleX,
        double dpiScaleY)
    {
        Dispatcher.UIThread.VerifyAccess();
        dpiScaleX = Math.Max(1, dpiScaleX);
        dpiScaleY = Math.Max(1, dpiScaleY);
        if (hostBounds.IsEmpty)
        {
            Hide();
            _hostBounds = default;
            _interactiveBounds = default;
            _dpiScaleX = dpiScaleX;
            _dpiScaleY = dpiScaleY;
            return;
        }

        if (!_hostBounds.IsEmpty &&
            !EdgeCapsuleMotionEnvelopePolicy.Contains(_hostBounds, hostBounds))
        {
            hostBounds = EdgeCapsuleMotionEnvelopePolicy.UnionWallPinned(
                _hostBounds,
                hostBounds,
                Key.Edge,
                Key.Edge == EdgeCapsuleEdge.Left ? hostBounds.Left : hostBounds.Right);
        }

        if (EdgeCapsuleMotionEnvelopePolicy.Contains(_hostBounds, hostBounds))
        {
            hostBounds = _hostBounds;
        }

        var boundsChanged = _hostBounds != hostBounds;
        var scaleChanged =
            Math.Abs(_dpiScaleX - dpiScaleX) > double.Epsilon ||
            Math.Abs(_dpiScaleY - dpiScaleY) > double.Epsilon;
        if (!boundsChanged && !scaleChanged)
        {
            return;
        }

        _hostBounds = hostBounds;
        _dpiScaleX = dpiScaleX;
        _dpiScaleY = dpiScaleY;
        if (boundsChanged)
        {
            Position = new PixelPoint(hostBounds.Left, hostBounds.Top);
        }

        Width = hostBounds.Width / _dpiScaleX;
        Height = hostBounds.Height / _dpiScaleY;
        if (!IsVisible)
        {
            Show();
        }
    }

    public void Apply(
        string paperId,
        EdgeCapsulePresentationFrame frame,
        EdgeCapsuleMotion motion)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!_nodes.TryGetValue(paperId, out var node))
        {
            throw new KeyNotFoundException($"Edge capsule '{paperId}' is not attached.");
        }

        if (frame.Visible)
        {
            ApplyHostBounds(frame.HostBounds, frame.DpiScaleX, frame.DpiScaleY);
        }

        if (node.Apply(frame, _hostBounds, motion))
        {
            RequestSharedAnimationFrame();
        }
        RebuildInteractiveBounds();
    }

    private void OnNodeBodyPointerPressed(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point)
    {
        if (_gesturePaperId is not null ||
            !node.AppliedFrame.Visible ||
            node.AppliedFrame.InteractiveBounds.IsEmpty)
        {
            return;
        }

        _gesturePaperId = node.PaperId;
        _gestureDownPoint = point;
        _gestureOriginalFrame = node.AppliedFrame;
        _gesturePointerOffsetY = Math.Clamp(
            (int)Math.Round(point.Y - node.AppliedFrame.Bounds.Top),
            0,
            Math.Max(0, node.AppliedFrame.Bounds.Height - 1));
        _gestureReordering = false;
        _gestureTargetIndex = CurrentQueueIndex(node.PaperId);
    }

    private void OnNodeBodyPointerMoved(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point,
        bool leftButtonPressed)
    {
        if (!string.Equals(_gesturePaperId, node.PaperId, StringComparison.Ordinal))
        {
            return;
        }

        if (!leftButtonPressed)
        {
            CancelReorderGesture(node, restore: true);
            return;
        }

        if (!_gestureReordering)
        {
            var scaleX = Math.Max(1, _gestureOriginalFrame.DpiScaleX);
            var scaleY = Math.Max(1, _gestureOriginalFrame.DpiScaleY);
            var dx = (point.X - _gestureDownPoint.X) / scaleX;
            var dy = (point.Y - _gestureDownPoint.Y) / scaleY;
            if (dx * dx + dy * dy < ReorderThresholdDip * ReorderThresholdDip)
            {
                return;
            }
            _gestureReordering = true;
        }

        var frame = DragFrame(_gestureOriginalFrame, point, _gesturePointerOffsetY);
        node.Apply(
            frame,
            _hostBounds,
            EdgeCapsuleMotion.Snap(EdgeCapsuleTransitionReason.Drag));
        _gestureTargetIndex = ResolveReorderTargetIndex(node.PaperId, point.Y);
        RebuildInteractiveBounds();
    }

    private void OnNodeBodyPointerReleased(
        EdgeCapsuleNodeHost node,
        DeviceScreenPoint point)
    {
        if (!string.Equals(_gesturePaperId, node.PaperId, StringComparison.Ordinal))
        {
            return;
        }

        var wasReordering = _gestureReordering;
        var targetIndex = _gestureTargetIndex;
        var originalFrame = _gestureOriginalFrame;
        ResetReorderGesture();

        if (!wasReordering)
        {
            node.InvokeBody();
            return;
        }

        var request = new EdgeCapsuleReorderRequestedEventArgs(
            Key,
            node.PaperId,
            targetIndex);
        ReorderRequested?.Invoke(this, request);
        if (!request.Handled && originalFrame.Visible)
        {
            node.Apply(
                originalFrame,
                _hostBounds,
                EdgeCapsuleMotion.Animate(EdgeCapsuleTransitionReason.Drag));
            RequestSharedAnimationFrame();
            RebuildInteractiveBounds();
        }
    }

    private void OnNodeBodyPointerCaptureLost(EdgeCapsuleNodeHost node)
    {
        if (string.Equals(_gesturePaperId, node.PaperId, StringComparison.Ordinal))
        {
            CancelReorderGesture(node, restore: true);
        }
    }

    private void CancelReorderGesture(EdgeCapsuleNodeHost node, bool restore)
    {
        var wasReordering = _gestureReordering;
        ResetReorderGesture();
        if (restore && wasReordering && node.RestoreRestingFrame(_hostBounds, animate: true))
        {
            RequestSharedAnimationFrame();
            RebuildInteractiveBounds();
        }
    }

    private void ResetReorderGesture()
    {
        _gesturePaperId = null;
        _gestureDownPoint = default;
        _gesturePointerOffsetY = 0;
        _gestureOriginalFrame = EdgeCapsulePresentationFrame.Hidden;
        _gestureReordering = false;
        _gestureTargetIndex = 0;
    }

    private int CurrentQueueIndex(string paperId)
    {
        var ordered = _nodes.Values
            .Where(node => node.AppliedFrame.Visible)
            .OrderBy(node => node.AppliedFrame.Bounds.Top)
            .ThenBy(node => node.PaperId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (string.Equals(ordered[index].PaperId, paperId, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return 0;
    }

    private int ResolveReorderTargetIndex(string sourcePaperId, double pointerY)
    {
        var peers = _nodes.Values
            .Where(node =>
                !string.Equals(node.PaperId, sourcePaperId, StringComparison.Ordinal) &&
                node.AppliedFrame.Visible &&
                !node.AppliedFrame.Bounds.IsEmpty)
            .OrderBy(node => node.AppliedFrame.Bounds.Top)
            .ThenBy(node => node.PaperId, StringComparer.Ordinal)
            .ToArray();
        var target = 0;
        foreach (var peer in peers)
        {
            var midpoint = peer.AppliedFrame.Bounds.Top +
                peer.AppliedFrame.Bounds.Height / 2.0;
            if (pointerY < midpoint)
            {
                break;
            }
            target++;
        }
        return Math.Clamp(target, 0, peers.Length);
    }

    private EdgeCapsulePresentationFrame DragFrame(
        EdgeCapsulePresentationFrame original,
        DeviceScreenPoint pointer,
        int pointerOffsetY)
    {
        var height = original.Bounds.Height;
        var minTop = _hostBounds.Top;
        var maxTop = Math.Max(minTop, _hostBounds.Bottom - height);
        var top = Math.Clamp(
            (int)Math.Round(pointer.Y) - pointerOffsetY,
            minTop,
            maxTop);
        var bounds = new DeviceScreenRect(
            original.Bounds.Left,
            top,
            original.Bounds.Right,
            top + height);
        return original with
        {
            Surface = EdgeCapsuleSurfaceKind.DockedHovered,
            Bounds = bounds,
            InteractiveBounds = EdgeCapsuleGeometry.InteractiveBoundsForAppliedBounds(
                bounds,
                original.Edge,
                original.DpiScaleX,
                original.DpiScaleY,
                EdgeCapsuleLayout.WindowChromeMargin),
            ContentOpacity = 1,
            OutlineVisible = true,
            IsHitTestVisible = true
        };
    }

    private void RequestSharedAnimationFrame()
    {
        if (_animationFrameRequested)
        {
            return;
        }

        _animationFrameRequested = true;
        RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _animationFrameRequested = false;
        if (!IsVisible)
        {
            return;
        }

        var nowTimestamp = Stopwatch.GetTimestamp();
        var hasActiveAnimation = false;
        foreach (var node in _nodes.Values)
        {
            hasActiveAnimation |= node.AdvanceAnimation(nowTimestamp);
        }

        RebuildInteractiveBounds();
        if (hasActiveAnimation)
        {
            RequestSharedAnimationFrame();
        }
    }

    public bool IsInteractiveDevicePoint(PixelPoint point)
    {
        foreach (var node in _nodes.Values)
        {
            if (node.ContainsDevicePoint(point))
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildInteractiveBounds()
    {
        var visible = _nodes.Values
            .Select(node => node.AppliedFrame.InteractiveBounds)
            .Where(bounds => !bounds.IsEmpty)
            .ToArray();
        if (visible.Length == 0)
        {
            _interactiveBounds = default;
            return;
        }

        _interactiveBounds = new DeviceScreenRect(
            visible.Min(bounds => bounds.Left),
            visible.Min(bounds => bounds.Top),
            visible.Max(bounds => bounds.Right),
            visible.Max(bounds => bounds.Bottom));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_gesturePaperId is null)
        {
            UpdatePointerHover(e);
        }
        RaiseTransparentHitTest(e);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_gesturePaperId is null)
        {
            UpdatePointerHover(e);
        }
        RaiseTransparentHitTest(e);
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_gesturePaperId is not null)
        {
            return;
        }

        var animated = false;
        foreach (var node in _nodes.Values)
        {
            animated |= node.UpdatePointerState(false, _hostBounds);
        }
        if (animated)
        {
            RequestSharedAnimationFrame();
        }
        RebuildInteractiveBounds();
    }

    private void UpdatePointerHover(PointerEventArgs e)
    {
        var devicePoint = global::Avalonia.VisualExtensions.PointToScreen(
            this,
            e.GetPosition(this));
        var animated = false;
        foreach (var node in _nodes.Values)
        {
            animated |= node.UpdatePointerState(
                node.ContainsDevicePoint(devicePoint),
                _hostBounds);
        }
        if (animated)
        {
            RequestSharedAnimationFrame();
        }
        RebuildInteractiveBounds();
    }

    private void RaiseTransparentHitTest(PointerEventArgs e)
    {
        var devicePoint = global::Avalonia.VisualExtensions.PointToScreen(
            this,
            e.GetPosition(this));
        var hit = new EdgeCapsuleTransparentHitTestEventArgs(devicePoint)
        {
            IsTransparent = !IsInteractiveDevicePoint(devicePoint)
        };
        TransparentHitTestRequested?.Invoke(this, hit);
        if (hit.IsTransparent)
        {
            e.Handled = false;
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_wndProcHook is not null)
        {
            return;
        }

        _wndProcHook = WndProc;
        Win32Properties.AddWndProcHookCallback(this, _wndProcHook);
    }

    private IntPtr WndProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var packed = unchecked((long)lParam);
        var x = unchecked((short)(packed & 0xffff));
        var y = unchecked((short)((packed >> 16) & 0xffff));
        var point = new PixelPoint(x, y);
        if (IsInteractiveDevicePoint(point))
        {
            return IntPtr.Zero;
        }

        var hit = new EdgeCapsuleTransparentHitTestEventArgs(point)
        {
            IsTransparent = true
        };
        TransparentHitTestRequested?.Invoke(this, hit);
        if (!hit.IsTransparent)
        {
            return IntPtr.Zero;
        }

        handled = true;
        return HtTransparent;
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationFrameRequested = false;
        ResetReorderGesture();
        PointerMoved -= OnPointerMoved;
        PointerPressed -= OnPointerPressed;
        PointerExited -= OnPointerExited;
        Opened -= OnOpened;
        if (_wndProcHook is not null)
        {
            Win32Properties.RemoveWndProcHookCallback(this, _wndProcHook);
            _wndProcHook = null;
        }

        foreach (var node in _nodes.Values)
        {
            UnsubscribeNode(node);
            node.Dispose();
        }

        _nodes.Clear();
        ReorderRequested = null;
        base.OnClosed(e);
    }

    private void UnsubscribeNode(EdgeCapsuleNodeHost node)
    {
        node.BodyPointerPressed -= OnNodeBodyPointerPressed;
        node.BodyPointerMoved -= OnNodeBodyPointerMoved;
        node.BodyPointerReleased -= OnNodeBodyPointerReleased;
        node.BodyPointerCaptureLost -= OnNodeBodyPointerCaptureLost;
    }
}
