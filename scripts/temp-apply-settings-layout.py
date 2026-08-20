from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    p = ROOT / path
    raw = p.read_text(encoding="utf-8-sig")
    return raw.replace("\r\n", "\n")


def write(path, text):
    (ROOT / path).write_text(text, encoding="utf-8", newline="\n")


def replace_once(text, old, new, label):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected 1 match, got {count}")
    return text.replace(old, new, 1)


def patch_settings():
    path = "AppController.Settings.cs"
    text = read(path)

    old_selectors = '''    private UIElement CreateTodoVisualSizeSegmentSelector()
    {
        var segments = new[]
        {
            (TodoVisualSizes.Small, Strings.Get("TodoVisualSizeSmall")),
            (TodoVisualSizes.Medium, Strings.Get("TodoVisualSizeMedium")),
            (TodoVisualSizes.Large, Strings.Get("TodoVisualSizeLarge"))
        };

        return CreateSegmentSelector(segments, TodoVisualSizes.Normalize(State.TodoVisualSize), SetTodoVisualSize);
    }

    private UIElement CreateVisualTextSizeSegmentSelector(string activeSize, Action<string> onSelect)
    {
        var segments = new[]
        {
            (VisualTextSizes.Small, Strings.Get("TodoVisualSizeSmall")),
            (VisualTextSizes.Medium, Strings.Get("TodoVisualSizeMedium")),
            (VisualTextSizes.Large, Strings.Get("TodoVisualSizeLarge"))
        };

        return CreateSegmentSelector(segments, VisualTextSizes.Normalize(activeSize), onSelect);
    }

'''
    text = replace_once(text, old_selectors, "", "remove old text-size segments")

    old_language_anchor = '''        leftColumn.Children.Add(SettingsSectionLabel(Strings.Get("SettingsGeneral")));
        leftColumn.Children.Add(WrapWithHint(SettingsToggle(Strings.Get("TrayStartup"), SystemSettingsHelper.IsStartupEnabled(), ToggleStartup), "TipStartup"));
'''
    new_language_anchor = '''        leftColumn.Children.Add(SettingsSectionLabel(Strings.Get("SettingsGeneral")));
        leftColumn.Children.Add(CreateUiLanguageSettingsRow());
        leftColumn.Children.Add(WrapWithHint(SettingsToggle(Strings.Get("TrayStartup"), SystemSettingsHelper.IsStartupEnabled(), ToggleStartup), "TipStartup"));
'''
    text = replace_once(text, old_language_anchor, new_language_anchor, "general language row")

    old_typography = '''        void AddTextStyleEditor(
            StackPanel column,
            string sectionKey,
            string tipKey,
            string activeSize,
            Action<string> setSize,
            bool isBold,
            Action toggleBold,
            bool leadingDivider)
        {
            if (leadingDivider)
            {
                column.Children.Add(SettingsSoftDivider());
            }

            // One shared tip on the section title: size + bold are the same style group.
            column.Children.Add(SettingsSectionLabelWithHint(Strings.Get(sectionKey), tipKey));
            column.Children.Add(CreateVisualTextSizeSegmentSelector(activeSize, setSize));
            column.Children.Add(SettingsToggle(Strings.Get("SettingsTextBold"), isBold, toggleBold));
        }

        AddTextStyleEditor(
            rightColumn,
            "SettingsNoteBodyText",
            "TipNoteBodyTextStyle",
            State.NoteTextSize,
            SetNoteTextSize,
            State.NoteTextBold,
            ToggleNoteTextBold,
            leadingDivider: false);

        rightColumn.Children.Add(SettingsSoftDivider());
        rightColumn.Children.Add(SettingsSectionLabelWithHint(
            Strings.Get("SettingsTodoBodyText"),
            "TipTodoBodyTextStyle"));
        rightColumn.Children.Add(CreateTodoVisualSizeSegmentSelector());
        rightColumn.Children.Add(SettingsToggle(
            Strings.Get("SettingsTextBold"),
            State.TodoTextBold,
            ToggleTodoTextBold));

        AddTextStyleEditor(
            rightColumn,
            "SettingsTitleText",
            "TipTitleTextStyle",
            State.TitleTextSize,
            SetTitleTextSize,
            State.TitleTextBold,
            ToggleTitleTextBold,
            leadingDivider: true);
        AddTextStyleEditor(
            rightColumn,
            "SettingsCapsuleText",
            "TipCapsuleTextStyle",
            State.CapsuleTextSize,
            SetCapsuleTextSize,
            State.CapsuleTextBold,
            ToggleCapsuleTextBold,
            leadingDivider: true);
'''
    new_typography = '''        void AddTextStyleEditor(
            StackPanel column,
            string sectionKey,
            string tipKey,
            UIElement sizeSelector,
            bool isBold,
            Action toggleBold,
            bool leadingDivider)
        {
            if (leadingDivider)
            {
                column.Children.Add(SettingsSoftDivider());
            }

            column.Children.Add(CreateTextStyleRow(
                Strings.Get(sectionKey),
                tipKey,
                sizeSelector,
                isBold,
                toggleBold));
        }

        AddTextStyleEditor(
            rightColumn,
            "SettingsNoteBodyText",
            "TipNoteBodyTextStyle",
            CreateVisualTextSizeSelector(State.NoteTextSize, SetNoteTextSize),
            State.NoteTextBold,
            ToggleNoteTextBold,
            leadingDivider: false);

        AddTextStyleEditor(
            rightColumn,
            "SettingsTodoBodyText",
            "TipTodoBodyTextStyle",
            CreateTodoVisualSizeSelector(),
            State.TodoTextBold,
            ToggleTodoTextBold,
            leadingDivider: true);

        AddTextStyleEditor(
            rightColumn,
            "SettingsTitleText",
            "TipTitleTextStyle",
            CreateVisualTextSizeSelector(State.TitleTextSize, SetTitleTextSize),
            State.TitleTextBold,
            ToggleTitleTextBold,
            leadingDivider: true);
        AddTextStyleEditor(
            rightColumn,
            "SettingsCapsuleText",
            "TipCapsuleTextStyle",
            CreateVisualTextSizeSelector(State.CapsuleTextSize, SetCapsuleTextSize),
            State.CapsuleTextBold,
            ToggleCapsuleTextBold,
            leadingDivider: true);
'''
    text = replace_once(text, old_typography, new_typography, "inline typography rows")

    old_hover = '''        options.Children.Add(new Border
        {
            Margin = new Thickness(0, 4, 0, 0),
            Child = WrapWithHint(
                SettingsFieldLabel(
                    Strings.Get("LabsEdgeCapsuleHoverIntentSensitivity")),
                "TipLabsEdgeCapsuleHoverIntentSensitivity")
        });
        options.Children.Add(CreateSegmentSelector(
            [
                (EdgeCapsuleHoverIntentSensitivities.VeryLow,
                    Strings.Get("EdgeCapsuleHoverIntentSensitivityVeryLow")),
                (EdgeCapsuleHoverIntentSensitivities.Low,
                    Strings.Get("EdgeCapsuleHoverIntentSensitivityLow")),
                (EdgeCapsuleHoverIntentSensitivities.Medium,
                    Strings.Get("EdgeCapsuleHoverIntentSensitivityMedium")),
                (EdgeCapsuleHoverIntentSensitivities.High,
                    Strings.Get("EdgeCapsuleHoverIntentSensitivityHigh")),
                (EdgeCapsuleHoverIntentSensitivities.VeryHigh,
                    Strings.Get("EdgeCapsuleHoverIntentSensitivityVeryHigh"))
            ],
            EdgeCapsuleHoverIntentSensitivities.Normalize(
                State.ExperimentalEdgeCapsuleHoverIntentSensitivity),
            SetExperimentalEdgeCapsuleHoverIntentSensitivity));
'''
    new_hover = '''        options.Children.Add(CompactSettingsField(
            Strings.Get("LabsEdgeCapsuleHoverIntentSensitivity"),
            CreateEdgeCapsuleHoverIntentSensitivitySelector(),
            editorWidth: 132,
            tipKey: "TipLabsEdgeCapsuleHoverIntentSensitivity",
            topMargin: 4));
'''
    text = replace_once(text, old_hover, new_hover, "hover intent dropdown")

    old_tether_layout = '''        options.Children.Add(SettingsFieldLabel(
            Strings.Get("LabsWindowTetherPreferredEdge"),
            topMargin: 8));
        options.Children.Add(CreateLabsWindowTetherEdgeSelector());
'''
    new_tether_layout = '''        options.Children.Add(CompactSettingsField(
            Strings.Get("LabsWindowTetherPreferredEdge"),
            CreateWindowTetherEdgeSelector(),
            editorWidth: 132,
            topMargin: 8));
'''
    text = replace_once(text, old_tether_layout, new_tether_layout, "tether edge dropdown")

    old_tether_method = '''    private UIElement CreateLabsWindowTetherEdgeSelector()
    {
        var segments = new[]
        {
            (ExperimentalWindowTetherOptions.Auto,
                Strings.Get("LabsWindowTetherEdgeAuto")),
            (ExperimentalWindowTetherOptions.Left,
                Strings.Get("LabsWindowTetherEdgeLeft")),
            (ExperimentalWindowTetherOptions.Right,
                Strings.Get("LabsWindowTetherEdgeRight")),
            (ExperimentalWindowTetherOptions.Top,
                Strings.Get("LabsWindowTetherEdgeTop")),
            (ExperimentalWindowTetherOptions.Bottom,
                Strings.Get("LabsWindowTetherEdgeBottom"))
        };
        return CreateSegmentSelector(
            segments,
            ExperimentalWindowTetherOptions.NormalizeEdge(
                State.ExperimentalWindowTetherPreferredEdge),
            SetExperimentalWindowTetherPreferredEdge);
    }

'''
    text = replace_once(text, old_tether_method, "", "remove old tether segment selector")

    old_restore = '''        State.EnableToolTips = true;
        State.EnableAnimations = true;
        State.FullscreenTopmostMode = FullscreenTopmostModes.Avoid;
'''
    new_restore = '''        State.EnableToolTips = true;
        State.EnableAnimations = true;
        State.UiLanguage = UiLanguages.Default;
        State.FullscreenTopmostMode = FullscreenTopmostModes.Avoid;
'''
    text = replace_once(text, old_restore, new_restore, "language restore default")

    forbidden = [
        "CreateTodoVisualSizeSegmentSelector()",
        "CreateVisualTextSizeSegmentSelector(",
        "CreateLabsWindowTetherEdgeSelector()",
    ]
    for token in forbidden:
        if token in text:
            raise RuntimeError(f"stale settings selector remains: {token}")

    required = [
        "CreateUiLanguageSettingsRow()",
        "CreateVisualTextSizeSelector(State.NoteTextSize, SetNoteTextSize)",
        "CreateTodoVisualSizeSelector()",
        "CreateEdgeCapsuleHoverIntentSensitivitySelector()",
        "CreateWindowTetherEdgeSelector()",
        "State.UiLanguage = UiLanguages.Default;",
    ]
    for token in required:
        if token not in text:
            raise RuntimeError(f"required settings change missing: {token}")

    write(path, text)


def patch_readme(path, english=False):
    text = read(path)
    text, n1 = re.subn(r'^PaperTodo\.exe --language[^\n]*\n', '', text, flags=re.M)
    if n1 != 1:
        raise RuntimeError(f"{path}: expected one language command line, got {n1}")

    if english:
        text, n2 = re.subn(
            r'\n`--language` accepts `zh-CN`, `en-US`, `ja-JP`, `ko-KR`, and their regional variants\..*?does not switch an already-running instance or write the choice to `data\.json`\.\n',
            '\n', text, count=1, flags=re.S)
        if n2 != 1:
            raise RuntimeError(f"{path}: expected one language CLI paragraph, got {n2}")
        if "**Interface language**" not in text:
            anchors = [
                "- **Launch at startup**, normal floating hints, animations\n",
                "- **Launch at startup**, tooltips, animations\n",
            ]
            for anchor in anchors:
                if anchor in text:
                    text = text.replace(
                        anchor,
                        anchor + "- **Interface language** — Follow system / 简体中文 / English / 日本語 / 한국어; restart to apply\n",
                        1)
                    break
            else:
                raise RuntimeError(f"{path}: settings anchor not found")
    else:
        text, n2 = re.subn(
            r'\n`--language` 支持 `zh-CN`、`en-US`、`ja-JP`、`ko-KR` 及对应区域变体.*?不会写入 `data\.json`。\n',
            '\n', text, count=1, flags=re.S)
        if n2 != 1:
            raise RuntimeError(f"{path}: expected one language CLI paragraph, got {n2}")
        anchor = "- **开机自启动**、普通悬浮提示、动画\n"
        if "**界面语言**" not in text:
            if anchor not in text:
                raise RuntimeError(f"{path}: settings anchor not found")
            text = text.replace(
                anchor,
                anchor + "- **界面语言** — 跟随系统 / 简体中文 / English / 日本語 / 한국어；重启后生效\n",
                1)

    if "PaperTodo.exe --language" in text or "`--language`" in text:
        raise RuntimeError(f"{path}: stale current language CLI documentation remains")
    write(path, text)


def main():
    patch_settings()
    patch_readme("README.md")
    patch_readme("README.en.md", english=True)
    print("settings layout patch verified")


if __name__ == "__main__":
    main()
