using System.Globalization;

namespace PaperTodo;

public static class PaperTitleRules
{
    public const int MaxTitleLength = 20;
    public const int DefaultMaxTitleLength = 6;
    public const int MinConfigurableTitleLength = 2;
    public const int MaxConfigurableTitleLength = MaxTitleLength;

    public static int NormalizeMaxTitleLength(int value)
    {
        if (value <= 0)
        {
            return DefaultMaxTitleLength;
        }

        return Math.Clamp(value, MinConfigurableTitleLength, MaxConfigurableTitleLength);
    }

    public static string CleanCustomTitle(string? title, int maxLength = MaxTitleLength)
    {
        var cleaned = (title ?? "").Trim();
        cleaned = string.Join("", cleaned.Where(character => !char.IsControl(character)));
        return TakeTextElements(cleaned, Math.Clamp(maxLength, 1, MaxTitleLength));
    }

    private static string TakeTextElements(string text, int maxLength)
    {
        var indexes = StringInfo.ParseCombiningCharacters(text);
        return indexes.Length <= maxLength ? text : text[..indexes[maxLength]];
    }
}
