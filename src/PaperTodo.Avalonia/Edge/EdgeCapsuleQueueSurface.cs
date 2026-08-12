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
    private readonly Canvas _motionPlane;
    private readonly Dictionary<string, EdgeCapsuleNodeHost> _nodes = new(StringComparer.Ordinal);
    private DeviceScreenRect _hostBounds;
    private DeviceScreenRect _interactiveBounds;
    private double _dpiScaleX = 1;
    private double _dpiScaleY = 1;
    private Win32Properties.CustomWndProcHookCallback? _wndProcHook;
    private bool _animationFrameRequested;

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
        Opened += OnOpened;
    }

    public EdgeCapsuleQueueKey Key { get; }

    Window IEdgeCapsuleQueueSurface.Window => this;

    public IReadOnlyCollection<EdgeCapsuleNodeHost> Nodes => _nodes.Values;

    public DeviceScreenRect HostBounds => _hostBounds;

    public DeviceScreenRect InteractiveBounds => _interactiveBounds;

    public event EventHandler<EdgeCapsuleTransparentHitTestEventArgs>? TransparentHitTestRequested;

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

        // Requested targets are allowed to grow the transparent motion envelope but never to
        // shrink it. This keeps the HWND stable while composition nodes move within it.
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

        // Frame geometry is expressed in physical device pixels. RenderScaling is not reliable
        // before the first Show on a mixed-DPI desktop, so use the planner's monitor DPI.
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

        // WM_NCHITTEST and hover consume the same sampled applied frames that describe the
        // compositor's cubic transition, never the target frames ahead of the visible nodes.
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

    private void OnPointerMoved(object? sender, PointerEventArgs e) =>
        RaiseTransparentHitTest(e);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        RaiseTransparentHitTest(e);

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
        if (_wndProcHook is not null)
        {
            Win32Properties.RemoveWndProcHookCallback(this, _wndProcHook);
            _wndProcHook = null;
        }

        foreach (var node in _nodes.Values)
        {
            node.Dispose();
        }

        _nodes.Clear();
        base.OnClosed(e);
    }
}
