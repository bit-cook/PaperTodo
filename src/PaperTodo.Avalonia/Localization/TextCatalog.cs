using System.Globalization;

namespace PaperTodo.Avalonia.Localization;

internal static class TextCatalog
{
    private static readonly IReadOnlyDictionary<string, TextSet> TextByLanguage =
        new Dictionary<string, TextSet>(StringComparer.OrdinalIgnoreCase)
        {
            ["zh"] = new(
                "PaperTodo", "显示全部", "隐藏全部", "切换显示", "新建 Todo", "新建 Note", "退出",
                "移动纸片", "隐藏纸片", "折叠为胶囊", "删除纸片", "置顶", "添加待办", "删除待办",
                "还没有待办，点击下面的 + 添加。", "写点什么……",
                "此便笺由插件提供。Avalonia 插件界面完成迁移前，正文仅供占位显示；已保存的插件内容不会在这里打开或修改。", "插件",
                "设置", "行为", "外观", "快捷键", "应用", "关闭", "边缘与窗口", "Todo 行为", "顶栏",
                "启用胶囊", "启用贴边胶囊", "启用动画", "显示工具提示", "完成后移到底部", "自动清理已完成待办",
                "顶栏显示新建 Todo", "顶栏显示新建 Note", "主题", "配色", "整体缩放", "文字", "Todo 字号", "Note 字号",
                "标题字号", "胶囊字号", "粗体", "窗口透明度", "非活动纸片透明", "非活动透明度", "区分小键盘数字",
                "只注册 Avalonia 当前已经实现的通用快捷键；贴边和实验室快捷键保留原配置，不会被此页覆盖。", "快捷键无效",
                "Todo 提醒", "快速提醒（分钟）", "Markdown 模式"),
            ["en"] = new(
                "PaperTodo", "Show all", "Hide all", "Toggle visibility", "New Todo", "New Note", "Exit",
                "Move paper", "Hide paper", "Collapse to capsule", "Delete paper", "Always on top", "Add todo", "Delete todo",
                "No todos yet. Use + below to add one.", "Write something…",
                "This note is provided by a plugin. Until its Avalonia interface is available, the body is read-only; saved plugin content is not opened or changed here.", "Provider",
                "Settings", "Behavior", "Appearance", "Shortcuts", "Apply", "Close", "Edge & window", "Todo behavior", "Top bar",
                "Enable capsules", "Enable edge capsules", "Enable animations", "Show tooltips", "Move completed to bottom", "Auto-clear completed todos",
                "Show New Todo on top bar", "Show New Note on top bar", "Theme", "Color scheme", "Overall scale", "Text", "Todo size", "Note size",
                "Title size", "Capsule size", "Bold", "Window opacity", "Dim inactive papers", "Inactive opacity", "Distinguish numpad digits",
                "Only general shortcuts already implemented by Avalonia are edited here. Edge and Labs bindings are preserved.", "Invalid shortcut",
                "Todo reminders", "Quick reminder (minutes)", "Markdown mode"),
            ["ja"] = new(
                "PaperTodo", "すべて表示", "すべて非表示", "表示を切り替え", "Todo を追加", "Note を追加", "終了",
                "付箋を移動", "付箋を隠す", "カプセルに折りたたむ", "付箋を削除", "常に手前", "Todo を追加", "Todo を削除",
                "Todo はまだありません。下の + から追加できます。", "内容を入力…",
                "このメモはプラグインによって提供されています。Avalonia 版のプラグイン画面が利用可能になるまで本文は読み取り専用で、保存済みのプラグイン内容はここでは開いたり変更したりしません。", "プロバイダー",
                "設定", "動作", "外観", "ショートカット", "適用", "閉じる", "エッジとウィンドウ", "Todo の動作", "トップバー",
                "カプセルを有効化", "エッジカプセルを有効化", "アニメーション", "ツールチップ", "完了項目を下へ移動", "完了 Todo を自動削除",
                "トップバーに新規 Todo", "トップバーに新規 Note", "テーマ", "配色", "全体スケール", "文字", "Todo サイズ", "Note サイズ",
                "タイトルサイズ", "カプセルサイズ", "太字", "ウィンドウ透明度", "非アクティブ付箋を薄く", "非アクティブ透明度", "テンキー数字を区別",
                "Avalonia で実装済みの一般ショートカットのみ編集します。エッジ/Labs 設定は保持されます。", "無効なショートカット",
                "Todo リマインダー", "クイックリマインダー（分）", "Markdown モード"),
            ["ko"] = new(
                "PaperTodo", "모두 표시", "모두 숨기기", "표시 전환", "새 Todo", "새 Note", "종료",
                "메모 이동", "메모 숨기기", "캡슐로 접기", "메모 삭제", "항상 위", "할 일 추가", "할 일 삭제",
                "할 일이 없습니다. 아래 + 버튼으로 추가하세요.", "내용 입력…",
                "이 메모는 플러그인에서 제공합니다. Avalonia 플러그인 화면을 사용할 수 있을 때까지 본문은 읽기 전용이며, 저장된 플러그인 내용은 여기에서 열거나 변경하지 않습니다.", "공급자",
                "설정", "동작", "모양", "단축키", "적용", "닫기", "가장자리 및 창", "Todo 동작", "상단 바",
                "캡슐 사용", "가장자리 캡슐 사용", "애니메이션 사용", "도구 설명 표시", "완료 항목을 아래로", "완료 Todo 자동 정리",
                "상단 바에 새 Todo", "상단 바에 새 Note", "테마", "색상", "전체 배율", "텍스트", "Todo 크기", "Note 크기",
                "제목 크기", "캡슐 크기", "굵게", "창 투명도", "비활성 메모 흐리게", "비활성 투명도", "숫자 키패드 구분",
                "Avalonia에서 구현된 일반 단축키만 편집합니다. 가장자리/Labs 설정은 유지됩니다.", "잘못된 단축키",
                "Todo 알림", "빠른 알림(분)", "Markdown 모드")
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
    string ToggleVisibility,
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
    string PluginProviderLabel,
    string Settings,
    string Behavior,
    string Appearance,
    string Shortcuts,
    string Apply,
    string Close,
    string EdgeAndWindow,
    string TodoBehavior,
    string TopBar,
    string CapsuleMode,
    string EdgeCapsuleMode,
    string Animations,
    string ToolTips,
    string AutoMoveCompleted,
    string AutoClearCompleted,
    string TopBarNewTodo,
    string TopBarNewNote,
    string Theme,
    string ColorScheme,
    string Zoom,
    string Text,
    string TodoSize,
    string NoteSize,
    string TitleSize,
    string CapsuleSize,
    string Bold,
    string WindowOpacity,
    string InactiveOpacity,
    string InactiveOpacityLevel,
    string DistinguishNumpad,
    string ShortcutHint,
    string InvalidShortcut,
    string TodoReminders,
    string QuickReminderMinutes,
    string MarkdownMode);
