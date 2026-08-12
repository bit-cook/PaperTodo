using System.Globalization;

namespace PaperTodo;

public static class PaperTitles
{
    // Hard storage / edit cap in Unicode text elements (≈ full-width characters for CJK).
    // Titles are never stored longer than this regardless of the user setting.
    public const int MaxTitleLength = PaperTitleRules.MaxTitleLength;

    // User-configurable display/edit cap (Settings → 标题最大长度), within MaxTitleLength.
    public const int DefaultMaxTitleLength = PaperTitleRules.DefaultMaxTitleLength;
    public const int MinConfigurableTitleLength = PaperTitleRules.MinConfigurableTitleLength;
    public const int MaxConfigurableTitleLength = MaxTitleLength;

    public static int NormalizeMaxTitleLength(int value)
    {
        return PaperTitleRules.NormalizeMaxTitleLength(value);
    }

    public static string DefaultTitle(string paperType, int number)
    {
        var prefix = DefaultTitlePrefix(paperType);
        return prefix + Math.Max(1, number).ToString(CultureInfo.InvariantCulture);
    }

    public static string DefaultTitlePrefix(string paperType)
    {
        return paperType == PaperTypes.Note
            ? Strings.Get("PaperKindNote")
            : Strings.Get("PaperKindTodo");
    }

    public static string CleanCustomTitle(string? title)
    {
        return CleanCustomTitle(title, MaxTitleLength);
    }

    public static string CleanCustomTitle(string? title, int maxLength)
    {
        return PaperTitleRules.CleanCustomTitle(title, maxLength);
    }

    public static string EffectiveTitle(PaperData paper, int fallbackNumber)
    {
        var title = CleanCustomTitle(paper.Title);
        return string.IsNullOrWhiteSpace(title)
            ? DefaultTitle(paper.Type, fallbackNumber)
            : title;
    }

    public static string CapsuleText(PaperData paper, int fallbackNumber)
    {
        return EffectiveTitle(paper, fallbackNumber);
    }

}
