using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleChrome : Grid
{
    private readonly Border _body;
    private readonly Border _close;
    private readonly TextBlock _title;

    public EdgeCapsuleChrome(AppState state)
    {
        var palette = PaperThemePalette.Resolve(state);
        ClipToBounds = false;
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto");

        _body = new Border
        {
            Height = PaperLayoutDefaults.CapsuleHeight,
            Background = palette.PaperBrush,
            BorderBrush = palette.PaperBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(EdgeCapsuleLayout.CornerRadius),
            Padding = new Thickness(14, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
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
        _body.Child = _title;
        _body.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BodyInvoked?.Invoke();
                e.Handled = true;
            }
        };

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

    public void SetTitle(string? title) => _title.Text = title ?? string.Empty;

    public void ApplyShape(
        EdgeCapsuleEdge edge,
        double bodyWidthDip,
        double closeWidthDip,
        double contentOpacity,
        bool outlineVisible)
    {
        var radius = EdgeCapsuleLayout.CornerRadius;
        _body.Width = Math.Max(1, bodyWidthDip);
        _body.Opacity = Math.Clamp(contentOpacity, 0, 1);
        _body.BorderThickness = outlineVisible ? new Thickness(2) : new Thickness(1);
        _close.Width = Math.Max(0, closeWidthDip);
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
