using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleChrome : Grid
{
    private readonly Border _body;
    private readonly Border _close;
    private readonly TextBlock _title;

    public EdgeCapsuleChrome()
    {
        ClipToBounds = false;
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto");

        _body = new Border
        {
            Height = PaperLayoutDefaults.CapsuleHeight,
            Background = new SolidColorBrush(Color.FromArgb(245, 252, 247, 228)),
            CornerRadius = new CornerRadius(
                EdgeCapsuleLayout.CornerRadius,
                EdgeCapsuleLayout.CornerRadius,
                EdgeCapsuleLayout.CornerRadius,
                EdgeCapsuleLayout.CornerRadius),
            Padding = new Thickness(14, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        _title = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = EdgeCapsulePreviewSize.MaximumWidthDip
        };
        _body.Child = _title;

        _close = new Border
        {
            Width = 0,
            Height = PaperLayoutDefaults.CapsuleHeight,
            Background = new SolidColorBrush(Color.FromArgb(245, 238, 118, 96)),
            Child = new TextBlock
            {
                Text = "×",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Children.Add(_body);
        Children.Add(_close);
        Grid.SetColumn(_close, 1);
    }

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
        _body.BorderBrush = outlineVisible ? Brushes.White : null;
        _body.BorderThickness = outlineVisible ? new Thickness(1) : default;
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
