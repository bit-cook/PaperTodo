using System.Text;

namespace PaperTodo;

internal static class MarkdownInlineSyntax
{
    private const char MaskedPunctuation = '\uE000';

    public static bool IsEscaped(string text, int index)
    {
        if (index <= 0 || index >= text.Length)
        {
            return false;
        }

        var slashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            slashes++;
        }
        return (slashes & 1) != 0;
    }

    public static bool IsEscapable(char value) =>
        value is >= '!' and <= '/' or
        >= ':' and <= '@' or
        >= '[' and <= '`' or
        >= '{' and <= '~';

    public static int IndexOfUnescaped(string text, char value, int startIndex)
    {
        var search = Math.Max(0, startIndex);
        while (search < text.Length)
        {
            var index = text.IndexOf(value, search);
            if (index < 0 || !IsEscaped(text, index))
            {
                return index;
            }
            search = index + 1;
        }
        return -1;
    }

    public static int IndexOfUnescaped(string text, string value, int startIndex)
    {
        var search = Math.Max(0, startIndex);
        while (search <= text.Length - value.Length)
        {
            var index = text.IndexOf(value, search, StringComparison.Ordinal);
            if (index < 0 || !IsEscaped(text, index))
            {
                return index;
            }
            search = index + 1;
        }
        return -1;
    }

    public static string MaskEscapedPunctuation(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\\') < 0)
        {
            return text;
        }

        char[]? chars = null;
        for (var i = 0; i < text.Length; i++)
        {
            if (!IsEscapable(text[i]) || !IsEscaped(text, i))
            {
                continue;
            }

            chars ??= text.ToCharArray();
            var marker = text[i];
            do
            {
                chars[i] = MaskedPunctuation;
                i++;
            }
            while (i < text.Length && text[i] == marker);
            i--;
        }
        return chars == null ? text : new string(chars);
    }

    public static string Unescape(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\\') < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' &&
                !IsEscaped(text, i) &&
                i + 1 < text.Length &&
                IsEscapable(text[i + 1]))
            {
                builder.Append(text[++i]);
            }
            else
            {
                builder.Append(text[i]);
            }
        }
        return builder.ToString();
    }
}
