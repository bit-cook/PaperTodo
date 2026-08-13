using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleFloatingDragWindow : Window
{
    private readonly Border _card;
    private readonly IBrush _normalBorder;
    private readonly IBrush _activeBorder;

    public EdgeCapsuleFloatingDragWindow(PaperData paper, AppState state)
    {
        var palette = PaperThemePalette.Resolve(state);
        _normalBorder = palette.PaperBorderBrush;
        _activeBorder = palette.ActiveBrush;

        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Width = 238;
        Height = Math.Max(34, PaperLayoutDefaults.CapsuleHeight + 4);
        SizeToContent = SizeToContent.Manual;

        var title = string.IsNullOrWhiteSpace(paper.Title)
            ? paper.Type == PaperTypes.Todo ? "Todo" : "Note"
            : paper.Title;
        var text = new TextBlock
        {
            Text = title,
            Foreground = palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                12,
                state.CapsuleTextSize,
                OverallFontScales.Normalize(state.Zoom)),
            FontWeight = state.CapsuleTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        _card = new Border
        {
            Background = palette.PaperBrush,
            BorderBrush = _normalBorder,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(EdgeCapsuleLayout.CornerRadius),
            Padding = new Thickness(13, 0),
            Child = text
        };
        Content = _card;
        Opacity = 0.94;
    }

    public void MoveTo(DeviceScreenPoint pointer)
    {
        Position = new PixelPoint(
            checked((int)Math.Round(pointer.X - Width / 2.0)),
            checked((int)Math.Round(pointer.Y - Height / 2.0)));
    }

    public void SetTargetReady(bool ready)
    {
        _card.BorderBrush = ready ? _activeBorder : _normalBorder;
        _card.BorderThickness = ready ? new Thickness(2.5) : new Thickness(1.5);
        Opacity = ready ? 1 : 0.94;
    }
}
