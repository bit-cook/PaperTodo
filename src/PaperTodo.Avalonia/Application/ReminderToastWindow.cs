using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia.Application;

internal sealed class ReminderToastWindow : Window
{
    private readonly DispatcherTimer _dismissTimer;
    private readonly Action _activateTarget;

    public ReminderToastWindow(
        AppState state,
        string title,
        string message,
        int count,
        Action activateTarget)
    {
        _activateTarget = activateTarget;
        var palette = PaperThemePalette.Resolve(state);

        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Topmost = true;
        Width = 360;
        MinHeight = 92;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        var heading = new TextBlock
        {
            Text = count > 1 ? $"{title} · {count}" : title,
            Foreground = palette.TextBrush,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var body = new TextBlock
        {
            Text = message,
            Foreground = palette.TextBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 48,
            Margin = new Thickness(0, 5, 0, 0)
        };
        var hint = new TextBlock
        {
            Text = "PaperTodo",
            Foreground = palette.WeakTextBrush,
            FontSize = 10,
            Opacity = 0.7,
            Margin = new Thickness(0, 6, 0, 0)
        };
        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(heading);
        stack.Children.Add(body);
        stack.Children.Add(hint);

        var card = new Border
        {
            Background = palette.PaperBrush,
            BorderBrush = palette.ActiveBrush,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 11),
            Child = stack
        };
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            _activateTarget();
            Close();
            e.Handled = true;
        };
        Content = card;

        _dismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            Close();
        };
        Opened += (_, _) => _dismissTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _dismissTimer.Stop();
        base.OnClosed(e);
    }
}
