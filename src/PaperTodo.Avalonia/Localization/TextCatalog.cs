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
                "新建 Todo",
                "新建 Note",
                "退出",
                "移动纸片",
                "隐藏纸片",
                "折叠为胶囊",
                "删除纸片",
                "置顶",
                "添加待办",
                "删除待办",
                "还没有待办，点击下面的 + 添加。",
                "写点什么……",
                "此便笺由插件提供。Avalonia 插件界面完成迁移前，正文仅供占位显示；已保存的插件内容不会在这里打开或修改。",
                "插件"),
            ["en"] = new(
                "PaperTodo",
                "Show all",
                "Hide all",
                "New Todo",
                "New Note",
                "Exit",
                "Move paper",
                "Hide paper",
                "Collapse to capsule",
                "Delete paper",
                "Always on top",
                "Add todo",
                "Delete todo",
                "No todos yet. Use + below to add one.",
                "Write something…",
                "This note is provided by a plugin. Until its Avalonia interface is available, the body is read-only; saved plugin content is not opened or changed here.",
                "Provider"),
            ["ja"] = new(
                "PaperTodo",
                "すべて表示",
                "すべて非表示",
                "Todo を追加",
                "Note を追加",
                "終了",
                "付箋を移動",
                "付箋を隠す",
                "カプセルに折りたたむ",
                "付箋を削除",
                "常に手前に表示",
                "Todo を追加",
                "Todo を削除",
                "Todo はまだありません。下の + から追加できます。",
                "内容を入力…",
                "このメモはプラグインによって提供されています。Avalonia 版のプラグイン画面が利用可能になるまで本文は読み取り専用で、保存済みのプラグイン内容はここでは開いたり変更したりしません。",
                "プロバイダー"),
            ["ko"] = new(
                "PaperTodo",
                "모두 표시",
                "모두 숨기기",
                "새 Todo",
                "새 Note",
                "종료",
                "메모 이동",
                "메모 숨기기",
                "캡슐로 접기",
                "메모 삭제",
                "항상 위",
                "할 일 추가",
                "할 일 삭제",
                "할 일이 없습니다. 아래 + 버튼으로 추가하세요.",
                "내용 입력…",
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
    string NewTodo,
    string NewNote,
    string Exit,
    string MovePaper,
    string HidePaper,
    string CollapsePaper,
    string DeletePaper,
    string AlwaysOnTop,
    string AddTodo,
    string DeleteTodo,
    string TodoPlaceholder,
    string NotePlaceholder,
    string PluginBodyReadOnly,
    string PluginProviderLabel);
