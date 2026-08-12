using Avalonia.Media;

namespace PaperTodo.Avalonia.Papers;

internal sealed record PaperThemePalette(
    Color Paper,
    Color PaperBorder,
    Color Text,
    Color WeakText,
    Color Active,
    Color CheckBox,
    Color Tint,
    Color Danger)
{
    public IBrush PaperBrush => Brush(Paper);
    public IBrush PaperBorderBrush => Brush(PaperBorder);
    public IBrush TextBrush => Brush(Text);
    public IBrush WeakTextBrush => Brush(WeakText);
    public IBrush ActiveBrush => Brush(Active);
    public IBrush CheckBoxBrush => Brush(CheckBox);
    public IBrush DangerBrush => Brush(Danger);
    public IBrush TopBarBrush => TintBrush(IsDarkColor(Paper) ? (byte)18 : (byte)12);
    public IBrush DividerBrush => TintBrush(IsDarkColor(Paper) ? (byte)34 : (byte)28);
    public IBrush HoverBrush => TintBrush(IsDarkColor(Paper) ? (byte)48 : (byte)32);

    public IBrush TintBrush(byte alpha) =>
        Brush(Color.FromArgb(alpha, Tint.R, Tint.G, Tint.B));

    public IBrush DangerBrushWithAlpha(byte alpha) =>
        Brush(Color.FromArgb(alpha, Danger.R, Danger.G, Danger.B));

    public static PaperThemePalette Resolve(AppState state)
    {
        var dark = IsDark(state);
        return (ColorSchemes.Normalize(state.ColorScheme), dark) switch
        {
            (ColorSchemes.Ink, false) => Create(
                246, 247, 249, 208, 214, 222, 38, 44, 54, 118, 126, 138,
                90, 108, 134, 170, 180, 194, 70, 90, 120, 188, 84, 80),
            (ColorSchemes.Ink, true) => Create(
                26, 28, 32, 60, 66, 76, 222, 227, 234, 138, 146, 158,
                132, 156, 188, 96, 106, 120, 180, 200, 228, 224, 116, 108),
            (ColorSchemes.Forest, false) => Create(
                243, 248, 241, 200, 218, 198, 38, 50, 42, 110, 128, 112,
                88, 130, 96, 168, 192, 168, 70, 110, 80, 188, 96, 76),
            (ColorSchemes.Forest, true) => Create(
                26, 30, 27, 58, 70, 60, 220, 228, 220, 134, 148, 136,
                124, 168, 134, 92, 110, 94, 180, 208, 186, 222, 124, 104),
            (ColorSchemes.Rose, false) => Create(
                253, 245, 246, 228, 205, 210, 54, 38, 42, 140, 114, 120,
                158, 104, 118, 216, 184, 192, 150, 80, 96, 188, 82, 78),
            (ColorSchemes.Rose, true) => Create(
                33, 28, 30, 78, 64, 68, 232, 220, 223, 152, 132, 137,
                190, 134, 148, 96, 78, 82, 224, 180, 190, 230, 114, 100),
            (ColorSchemes.Warm, true) => Create(
                33, 31, 28, 76, 69, 61, 231, 224, 212, 146, 137, 123,
                168, 142, 106, 110, 100, 85, 230, 223, 211, 230, 110, 90),
            _ => Create(
                255, 249, 234, 224, 206, 167, 51, 41, 30, 138, 122, 99,
                140, 115, 80, 180, 160, 120, 120, 92, 48, 176, 90, 70)
        };
    }

    private static bool IsDark(AppState state)
    {
        if (string.Equals(state.Theme, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(state.Theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool IsDarkColor(Color color) =>
        color.R + color.G + color.B < 360;

    private static IBrush Brush(Color color) => new SolidColorBrush(color);

    private static PaperThemePalette Create(
        byte paperR, byte paperG, byte paperB,
        byte borderR, byte borderG, byte borderB,
        byte textR, byte textG, byte textB,
        byte weakR, byte weakG, byte weakB,
        byte activeR, byte activeG, byte activeB,
        byte checkR, byte checkG, byte checkB,
        byte tintR, byte tintG, byte tintB,
        byte dangerR, byte dangerG, byte dangerB) =>
        new(
            Color.FromRgb(paperR, paperG, paperB),
            Color.FromRgb(borderR, borderG, borderB),
            Color.FromRgb(textR, textG, textB),
            Color.FromRgb(weakR, weakG, weakB),
            Color.FromRgb(activeR, activeG, activeB),
            Color.FromRgb(checkR, checkG, checkB),
            Color.FromRgb(tintR, tintG, tintB),
            Color.FromRgb(dangerR, dangerG, dangerB));
}
