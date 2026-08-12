using System.Globalization;

namespace PaperTodo.Avalonia.Localization;

internal static class TextCatalog
{
    private static readonly IReadOnlyDictionary<string, TextSet> TextByLanguage =
        new Dictionary<string, TextSet>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = new(
                "PaperTodo",
                "显示全部",
                "隐藏全部",
                "退出",
                "此便笺由插件提供。Avalonia 插件界面完成迁移前，正文仅供占位显示；已保存的插件内容不会在这里打开或修改。",
                "插件"),
            ["en"] = new(
                "PaperTodo",
                "Show all",
                "Hide all",
                "Exit",
                "This note is provided by a plugin. Until its Avalonia interface is available, the body is read-only; saved plugin content is not opened or changed here.",
                "Provider"),
            ["ja"] = new(
                "PaperTodo",
                "すべて表示",
                "すべて非表示",
                "終了",
                "このメモはプラグインによって提供されています。Avalonia 版のプラグイン画面が利用可能になるまで本文は読み取り専用で、保存済みのプラグイン内容はここでは開いたり変更したりしません。",
                "プロバイダー"),
            ["ko"] = new(
                "PaperTodo",
                "모두 표시",
                "모두 숨기기",
                "종료",
                "이 메모는 플러그인에서 제공합니다. Avalonia 플러그인 화면을 사용할 수 있을 때까지 본문은 읽기 전용이며, 저장된 플러그인 내용은 여기에서 열거나 변경하지 않습니다.",
                "공급자")
        };

    public static TextSet Current
    {
        get
        {
            var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return TextByLanguage.TryGetValue(language, out var text)
                ? text
                : TextByLanguage["en"];
        }
    }
}

internal sealed record TextSet(
    string ApplicationName,
    string ShowAll,
    string HideAll,
    string Exit,
    string PluginBodyReadOnly,
    string PluginProviderLabel);
