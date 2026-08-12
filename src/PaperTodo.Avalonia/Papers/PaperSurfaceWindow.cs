using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperSurfaceWindow : Window, IPaperSurface
{
    private readonly ContentPresenter _presenter;
    private readonly PaperEditorControl _editor;
    private PixelPoint _dragStartWindowPosition;
    private PixelPoint _dragStartPointerPosition;
    private bool _dragging;

    public PaperSurfaceWindow(PaperSurfaceDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.PaperId);
        PaperId = descriptor.PaperId;
        Paper = descriptor.Paper;

        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Position = descriptor.Position;
        Width = Math.Max(1, descriptor.Size.Width);
        Height = Math.Max(1, descriptor.Size.Height);
        Topmost = descriptor.AlwaysOnTop;

        _editor = new PaperEditorControl(Paper);
        _presenter = new ContentPresenter
        {
            Content = _editor,
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch
        };
        Content = _presenter;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    public string PaperId { get; }

    public PaperData Paper { get; }

    Window IPaperSurface.Window => this;

    public void ApplyDescriptor(PaperSurfaceDescriptor descriptor)
    {
        if (!string.Equals(PaperId, descriptor.PaperId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A paper surface cannot change its paper identity.", nameof(descriptor));
        }

        Position = descriptor.Position;
        Width = Math.Max(1, descriptor.Size.Width);
        Height = Math.Max(1, descriptor.Size.Height);
        Topmost = descriptor.AlwaysOnTop;

        if (descriptor.IsVisible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void RefreshFromModel() => _editor.RefreshFromModel();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.GetPosition(this).Y > 24)
        {
            return;
        }

        _dragStartWindowPosition = Position;
        _dragStartPointerPosition = global::Avalonia.VisualExtensions.PointToScreen(
            this,
            e.GetPosition(this));
        _dragging = true;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = global::Avalonia.VisualExtensions.PointToScreen(
            this,
            e.GetPosition(this));
        Position = new PixelPoint(
            _dragStartWindowPosition.X + current.X - _dragStartPointerPosition.X,
            _dragStartWindowPosition.Y + current.Y - _dragStartPointerPosition.Y);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        var stored = LegacyPaperGeometryAdapter.ToStoredPosition(Position);
        Paper.X = stored.X;
        Paper.Y = stored.Y;
    }
}
