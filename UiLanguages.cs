using System.Globalization;

namespace PaperTodo;

public static class UiLanguages
{
    public const string System = "system";
    public const string ChineseSimplified = "zh-CN";
    public const string English = "en-US";
    public const string Japanese = "ja-JP";
    public const string Korean = "ko-KR";

#if PAPERTODO_DEFAULT_ENGLISH
    public const string Default = English;
#else
    public const string Default = System;
#endif

    public static string Normalize(string? language)
    {
        return language is ChineseSimplified or English or Japanese or Korean
            ? language
            : System;
    }
}

internal static class UiLanguageText
{
    public static string SettingLabel => Localized(
        zh: "界面语言",
        en: "Interface language",
        ja: "表示言語",
        ko: "인터페이스 언어");

    public static string SettingTip => Localized(
        zh: "选择界面语言；重启 PaperTodo 后生效。",
        en: "Choose the interface language; restart PaperTodo to apply.",
        ja: "表示言語を選択します。PaperTodo の再起動後に反映されます。",
        ko: "인터페이스 언어를 선택합니다. PaperTodo를 다시 시작하면 적용됩니다.");

    public static string SystemLabel => Localized(
        zh: "跟随系统",
        en: "Follow system",
        ja: "システムに従う",
        ko: "시스템 설정 따름");

    public const string ChineseSimplifiedLabel = "简体中文";
    public const string EnglishLabel = "English";
    public const string JapaneseLabel = "日本語";
    public const string KoreanLabel = "한국어";

    private static string Localized(string zh, string en, string ja, string ko)
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "en" => en,
            "ja" => ja,
            "ko" => ko,
            _ => zh
        };
    }
}
