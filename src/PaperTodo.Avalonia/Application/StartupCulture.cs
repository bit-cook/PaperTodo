using System.Globalization;

namespace PaperTodo.Avalonia.Application;

internal static class StartupCulture
{
    public static void Apply(string? language)
    {
        if (!TryResolve(language, out var culture))
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static bool TryResolve(string? language, out CultureInfo culture)
    {
        culture = null!;
        var value = (language ?? string.Empty).Trim().Replace('_', '-');
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var requested = CultureInfo.GetCultureInfo(value);
            if (requested.TwoLetterISOLanguageName is not ("zh" or "en" or "ja" or "ko"))
            {
                return false;
            }

            culture = requested.IsNeutralCulture
                ? CultureInfo.GetCultureInfo(requested.TwoLetterISOLanguageName switch
                {
                    "zh" => "zh-CN",
                    "ja" => "ja-JP",
                    "ko" => "ko-KR",
                    _ => "en-US"
                })
                : requested;
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
