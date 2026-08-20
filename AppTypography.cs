using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace PaperTodo;

public static class AppTypography
{
    private const string SymbolFallback = "Segoe UI Symbol, Segoe UI Emoji";
    private const string DefaultCodeFontFamilyName = "Cascadia Mono, Consolas, Microsoft YaHei UI, Segoe UI Symbol, Segoe UI Emoji";

    private sealed record CustomFontFace(FontFamily Family, FontWeight Weight);

    private static string _preset = UiFontPresets.Default;
    private static CustomFontFace? _customFontFace;
    private static CustomFontFace? _customBoldFontFace;
    private static bool _customFontEnhancedBold;
    private static string _textRenderingProfile = TextRenderingProfiles.Standard;
    private static double _scale = 1.0;

    public static XmlLanguage Language => XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);

    public static FontFamily UiFontFamily => _customFontFace?.Family ?? ResolveUiFontFamily();

    public static FontFamily ContentFontFamily => _customFontFace?.Family ?? ResolveContentFontFamily();

    public static FontFamily CodeFontFamily => new(DefaultCodeFontFamilyName);

    public static FontFamily SymbolFontFamily { get; } = new(SymbolFallback);

    public static bool UsesCustomTextRendering =>
        _textRenderingProfile != TextRenderingProfiles.Standard;

    // Standard follows WPF defaults. Soft keeps the current layout-stable smoothing path, while
    // Sharp uses the pixel-aligned Display path that was used by the earlier rendering experiment.
    public static TextFormattingMode TextFormattingMode =>
        _textRenderingProfile == TextRenderingProfiles.Sharp
            ? TextFormattingMode.Display
            : TextFormattingMode.Ideal;
    public static TextRenderingMode TextRenderingMode =>
        UsesCustomTextRendering ? TextRenderingMode.Grayscale : TextRenderingMode.Auto;
    public static TextHintingMode TextHintingMode =>
        _textRenderingProfile == TextRenderingProfiles.Soft
            ? TextHintingMode.Animated
            : TextHintingMode.Auto;

    public static bool HasCustomFont => _customFontFace != null;

    public static bool HasCustomBoldFont => _customBoldFontFace != null;

    /// <summary>
    /// Enhanced bold is armed in settings, both custom faces are present, and the caller wants bold.
    /// </summary>
    public static bool UsesCustomBoldFace(bool bold) =>
        bold &&
        _customFontEnhancedBold &&
        _customFontFace != null &&
        _customBoldFontFace != null;

    public static double ScaleFactor => _scale;

    public static double Scale(double fontSize)
    {
        return Math.Round(fontSize * _scale, 1, MidpointRounding.AwayFromZero);
    }

    public static double FitChrome(double normalSize)
    {
        return _scale <= 1.0
            ? normalSize
            : Math.Ceiling(normalSize * _scale);
    }

    public static void Configure(
        string? preset,
        double scale = 1.0,
        bool customFontEnhancedBold = false,
        string? textRenderingProfile = null)
    {
        _preset = UiFontPresets.Normalize(preset);
        _scale = OverallFontScales.Normalize(scale);
        _customFontEnhancedBold = customFontEnhancedBold;
        _textRenderingProfile = TextRenderingProfiles.Normalize(textRenderingProfile);
        _customFontFace = TryLoadCustomFontFaceFromCandidates(CustomRegularFontCandidates());
        _customBoldFontFace = TryLoadCustomFontFaceFromCandidates(CustomBoldFontCandidates());
    }

    /// <summary>
    /// Family for UI chrome or body text. content=true: notes / todos; content=false: titles, capsules, settings chrome.
    /// When enhanced bold is active, bold runs use papertodo_bold.
    /// </summary>
    public static FontFamily FontFamilyFor(bool content, bool bold)
    {
        if (UsesCustomBoldFace(bold))
        {
            return _customBoldFontFace!.Family;
        }

        return content ? ContentFontFamily : UiFontFamily;
    }

    /// <summary>
    /// Paper title face — same as other chrome (capsule labels, etc.).
    /// </summary>
    public static FontFamily FontFamilyForTitle(bool bold) => FontFamilyFor(content: false, bold: bold);

    /// <summary>
    /// Weight for bold runs. Preserve the face's designed weight so WPF selects the real bold
    /// face when regular and bold files share the same internal family name.
    /// </summary>
    public static FontWeight FontWeightFor(bool bold)
    {
        if (UsesCustomBoldFace(bold))
        {
            return _customBoldFontFace!.Weight;
        }

        return bold ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public static FontWeight HeadingFontWeightFor(bool bold)
    {
        if (UsesCustomBoldFace(bold))
        {
            return _customBoldFontFace!.Weight;
        }

        return bold ? FontWeights.Bold : FontWeights.SemiBold;
    }

    public static void ApplyTextRendering(DependencyObject target)
    {
        if (!UsesCustomTextRendering)
        {
            ClearTextRendering(target);
            return;
        }

        TextOptions.SetTextFormattingMode(target, TextFormattingMode);
        TextOptions.SetTextRenderingMode(target, TextRenderingMode);
        TextOptions.SetTextHintingMode(target, TextHintingMode);
        target.ClearValue(RenderOptions.ClearTypeHintProperty);
    }

    private static void ClearTextRendering(DependencyObject target)
    {
        target.ClearValue(TextOptions.TextFormattingModeProperty);
        target.ClearValue(TextOptions.TextRenderingModeProperty);
        target.ClearValue(TextOptions.TextHintingModeProperty);
        target.ClearValue(RenderOptions.ClearTypeHintProperty);
    }

    // YaHei / DengXian: selected face leads all scripts; Segoe is missing-glyph only.
    private const string YaHeiFontFamilyName =
        "Microsoft YaHei UI, Microsoft YaHei, Microsoft JhengHei UI, Microsoft JhengHei, Yu Gothic UI, Malgun Gothic, Meiryo, Segoe UI, " + SymbolFallback;
    private const string DengXianFontFamilyName =
        "DengXian, Microsoft YaHei UI, Microsoft YaHei, Microsoft JhengHei UI, Microsoft JhengHei, Yu Gothic UI, Malgun Gothic, Meiryo, Segoe UI, " + SymbolFallback;
    // System default chrome (titles, capsules, settings): YaHei UI first.
    private const string DefaultChromeFontFamilyName =
        "Microsoft YaHei UI, Microsoft YaHei, Segoe UI, " + SymbolFallback;

    private static FontFamily ResolveUiFontFamily()
    {
        return _preset switch
        {
            UiFontPresets.YaHei => new FontFamily(YaHeiFontFamilyName),
            UiFontPresets.DengXian => new FontFamily(DengXianFontFamilyName),
            _ => new FontFamily(DefaultChromeFontFamilyName)
        };
    }

    // Notes and todo items only: under system default keep Segoe-first regional body chains.
    private static FontFamily ResolveContentFontFamily()
    {
        return _preset switch
        {
            UiFontPresets.YaHei => new FontFamily(YaHeiFontFamilyName),
            UiFontPresets.DengXian => new FontFamily(DengXianFontFamilyName),
            _ => new FontFamily(DefaultContentFontFamilyName())
        };
    }

    private static string DefaultContentFontFamilyName()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return lang switch
        {
            "ja" => "Segoe UI, Yu Gothic UI, Meiryo, Microsoft YaHei UI, Microsoft YaHei, Malgun Gothic, " + SymbolFallback,
            "ko" => "Segoe UI, Malgun Gothic, Microsoft YaHei UI, Microsoft YaHei, Yu Gothic UI, Meiryo, " + SymbolFallback,
            _ => "Segoe UI, Microsoft YaHei UI, Microsoft YaHei, Microsoft JhengHei UI, Microsoft JhengHei, Yu Gothic UI, Malgun Gothic, Meiryo, " + SymbolFallback
        };
    }

    private static IEnumerable<string> CustomRegularFontCandidates()
    {
        yield return "papertodo.ttf";
        yield return "papertodo.otf";
    }

    private static IEnumerable<string> CustomBoldFontCandidates()
    {
        yield return "papertodo_bold.ttf";
        yield return "papertodo_bold.otf";
        yield return "PaperTodo_Bold.ttf";
        yield return "PaperTodo_Bold.otf";
    }

    private static CustomFontFace? TryLoadCustomFontFaceFromCandidates(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var face = TryLoadCustomFontFace(fileName);
            if (face != null)
            {
                return face;
            }
        }

        return null;
    }

    private static CustomFontFace? TryLoadCustomFontFace(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var collection = Fonts.GetFontFamilies(new Uri(path, UriKind.Absolute));
            var family = collection.FirstOrDefault();
            if (family == null)
            {
                return null;
            }

            var weight = FontWeights.Normal;
            foreach (var typeface in family.GetTypefaces())
            {
                if (typeface.Weight.ToOpenTypeWeight() > weight.ToOpenTypeWeight())
                {
                    weight = typeface.Weight;
                }
            }

            return new CustomFontFace(family, weight);
        }
        catch
        {
            return null;
        }
    }
}
