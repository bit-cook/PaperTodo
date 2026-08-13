using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleChrome : Grid
{
    private const double PreviewHeaderDragHeightDip = 34;

    private readonly Border _body;
    private readonly Border _close;
    private readonly Grid _bodyContent;
    private readonly TextBlock _title;
    private readonly ContentControl _previewHost;
    private bool _previewVisible;
    private bool _bodyPointerCaptured;

    public EdgeCapsuleChrome(AppState state)
    {
        var palette = PaperThemePalette.Resolve(state);
        ClipToBounds = false;
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto");

        _bodyContent = new Grid();
        _title = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = EdgeCapsulePreviewSize.MaximumWidthDip,
            Foreground = palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                12,
                state.CapsuleTextSize,
                OverallFontScales.Normalize(state.Zoom)),
            FontWeight = state.CapsuleTextBold ? FontWeight.SemiBold : FontWeight.Normal
        };
        _previewHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false
        };
        _bodyContent.Children.Add(_title);
        _bodyContent.Children.Add(_previewHost);

        _body = new Border
        {
            Height = PaperLayoutDefaults.CapsuleHeight,
            Background = palette.PaperBrush,
            BorderBrush = palette.PaperBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(EdgeCapsuleLayout.CornerRadius),
            Padding = new Thickness(14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true,
            Child = _bodyContent
        };
        _body.PointerPressed += OnBodyPointerPressed;
        _body.PointerMoved += OnBodyPointerMoved;
        _body.PointerReleased += OnBodyPointerReleased;
        _body.PointerCaptureLost += OnBodyPointerCaptureLost;

        _close = new Border
        {
            Width = 0,
            Height = PaperLayoutDefaults.CapsuleHeight,
            Background = palette.DangerBrushWithAlpha(220),
            Child = new TextBlock
            {
                Text = "×",
                FontSize = 18,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _close.PointerPressed += (_, e) =>
        {
            if (_close.Width > 0 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                CloseInvoked?.Invoke();
                e.Handled = true;
            }
        };

        Children.Add(_body);
        Children.Add(_close);
        Grid.SetColumn(_close, 1);
    }

    public event Action? BodyInvoked;
    public event Action? CloseInvoked;
    public event Action<DeviceScreenPoint>? BodyPointerPressed;
    public event Action<DeviceScreenPoint, bool>? BodyPointerMoved;
    public event Action<DeviceScreenPoint>? BodyPointerReleased;
    public event Action? BodyPointerCaptureLost;

    public bool HasPreviewContent => _previewHost.Content is Control;

    public void SetTitle(string? title) => _title.Text = title ?? string.Empty;

    public void SetPreviewContent(Control? content)
    {
        if (ReferenceEquals(_previewHost.Content, content))
        {
            return;
        }

        _previewHost.Content = content;
    }

    public void InvokeBody() => BodyInvoked?.Invoke();

    private void OnBodyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var current = e.GetCurrentPoint(_body);
        if (!current.Properties.IsLeftButtonPressed ||
            (_previewVisible && current.Position.Y > PreviewHeaderDragHeightDip))
        {
            return;
        }

        _bodyPointerCaptured = true;
        e.Pointer.Capture(_body);
        BodyPointerPressed?.Invoke(ToDevicePoint(e));
        e.Handled = true;
    }

    private void OnBodyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_bodyPointerCaptured)
        {
            return;
        }

        BodyPointerMoved?.Invoke(
            ToDevicePoint(e),
            e.GetCurrentPoint(_body).Properties.IsLeftButtonPressed);
        e.Handled = true;
    }

    private void OnBodyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_bodyPointerCaptured)
        {
            return;
        }

        var point = ToDevicePoint(e);
        _bodyPointerCaptured = false;
        e.Pointer.Capture(null);
        BodyPointerReleased?.Invoke(point);
        e.Handled = true;
    }

    private void OnBodyPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_bodyPointerCaptured)
        {
            return;
        }

        _bodyPointerCaptured = false;
        BodyPointerCaptureLost?.Invoke();
    }

    private DeviceScreenPoint ToDevicePoint(PointerEventArgs e)
    {
        var point = global::Avalonia.VisualExtensions.PointToScreen(
            _body,
            e.GetPosition(_body));
        return new DeviceScreenPoint(point.X, point.Y);
    }

    public void ApplyShape(
        EdgeCapsuleEdge edge,
        double bodyWidthDip,
        double bodyHeightDip,
        double closeWidthDip,
        double contentOpacity,
        bool outlineVisible,
        EdgeCapsuleSurfaceKind surface)
    {
        var radius = EdgeCapsuleLayout.CornerRadius;
        _previewVisible = surface == EdgeCapsuleSurfaceKind.DockedPreview;
        _body.Width = Math.Max(1, bodyWidthDip);
        _body.Height = Math.Max(1, bodyHeightDip);
        _body.Opacity = Math.Clamp(contentOpacity, 0, 1);
        _body.BorderThickness = outlineVisible ? new Thickness(2) : new Thickness(1);
        _body.Padding = _previewVisible ? default : new Thickness(14, 0);
        _title.IsVisible = !_previewVisible;
        _previewHost.IsVisible = _previewVisible && _previewHost.Content is not null;

        _close.Width = Math.Max(0, closeWidthDip);
        _close.Height = Math.Max(1, bodyHeightDip);
        _close.Opacity = closeWidthDip > 0 ? 1 : 0;

        _body.CornerRadius = edge == EdgeCapsuleEdge.Left
            ? new CornerRadius(0, radius, radius, 0)
            : new CornerRadius(radius, 0, 0, radius);
        _close.CornerRadius = edge == EdgeCapsuleEdge.Left
            ? new CornerRadius(0, radius, radius, 0)
            : new CornerRadius(radius, 0, 0, radius);

        if (edge == EdgeCapsuleEdge.Left)
        {
            Grid.SetColumn(_close, 0);
            Grid.SetColumn(_body, 1);
        }
        else
        {
            Grid.SetColumn(_body, 0);
            Grid.SetColumn(_close, 1);
        }
    }
}
