using System;
using System.Windows;
using System.Windows.Controls;

namespace PaperTodo;

public sealed partial class AppController
{
    private UIElement BuildSettingsSidebarNotePage()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(2, 4, 6, 0)
        };

        content.Children.Add(SettingsSectionLabel(
            SettingsSidebarLocalized("Markdown", "Markdown", "Markdown", "Markdown")));
        content.Children.Add(WrapWithHint(
            SettingsFieldLabel(Strings.Get("TrayMarkdownRenderMode")),
            BuildSettingsHintTooltip(SettingsSidebarLocalized(
                "关闭：显示原文。\n基础：保留 Markdown 标记，同时淡化语法标记并显示列表圆点、分割线等轻量排版。\n完全：编辑时按标题、列表、引用和代码块等最终排版，隐藏多数标记；光标所在块显示标记以便编辑，失焦后整篇只读渲染。",
                "Off: show source text.\nBasic: keep Markdown markers while fading syntax and showing lightweight layout such as list bullets and dividers.\nFull: render headings, lists, quotes and code blocks while editing, hiding most markers. The block at the caret reveals its markers for editing; the whole note becomes read-only rendered content when unfocused.",
                "オフ：原文を表示します。\n基本：Markdown 記号を残しつつ、構文記号を薄く表示し、リストの丸印や区切り線などの軽い整形を適用します。\n完全：編集中も見出し・リスト・引用・コードブロックを最終表示に近い形で整形し、多くの記号を隠します。カーソルのあるブロックでは編集用に記号を表示し、フォーカスが外れると全体を読み取り専用で表示します。",
                "끄기: 원문을 표시합니다.\n기본: Markdown 기호를 유지하면서 문법 기호를 흐리게 하고 목록 점과 구분선 같은 가벼운 서식을 표시합니다.\n전체: 편집 중에도 제목, 목록, 인용, 코드 블록을 최종 표시와 가깝게 서식화하고 대부분의 기호를 숨깁니다. 커서가 있는 블록은 편집을 위해 기호를 표시하며, 포커스를 잃으면 전체 노트를 읽기 전용으로 렌더링합니다."))));

        UIElement? markdownAnimationRow = null;
        content.Children.Add(CreateSettingsSidebarMarkdownRenderSelector(isFullRender =>
        {
            if (markdownAnimationRow != null)
            {
                markdownAnimationRow.Visibility = isFullRender
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }));

        markdownAnimationRow = WrapWithHint(
            SettingsToggle(
                Strings.Get("SettingsMarkdownEditAnimation"),
                State.MarkdownEditAnimationEnabled,
                ToggleMarkdownEditAnimation),
            "TipMarkdownEditAnimation");
        markdownAnimationRow.Visibility =
            State.MarkdownRenderMode == MarkdownRenderModes.Full
                ? Visibility.Visible
                : Visibility.Collapsed;
        content.Children.Add(markdownAnimationRow);

        content.Children.Add(SettingsSectionLabel(Strings.Get("SettingsExternalOpen")));
        content.Children.Add(WrapWithHint(
            SettingsFieldLabel(Strings.Get("SettingsExternalMarkdownExtension")),
            "TipExternalExtension"));
        content.Children.Add(CreateExternalMarkdownExtensionEditor());

        if (State.AdvancedSettingsMode)
        {
            content.Children.Add(AdvancedSettingsBlock(
                SettingsSectionLabel(
                    SettingsSidebarLocalized("图片", "Images", "画像", "이미지")),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsAutoCompressLargeImages"),
                        State.AutoCompressLargeImages,
                        ToggleAutoCompressLargeImages)),
                    "TipAutoCompressLargeImages")));

            content.Children.Add(AdvancedSettingsBlock(
                SettingsSectionLabel(Strings.Get("SettingsScriptCapsule")),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsPersistentPowerShellProcess"),
                        State.UsePersistentPowerShellProcess,
                        TogglePersistentPowerShellProcess)),
                    "TipPersistentPowerShellProcess"),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsPreferPowerShell7"),
                        State.PreferPowerShell7,
                        TogglePreferPowerShell7)),
                    "TipPreferPowerShell7"),
                WrapWithHint(
                    MarkAdvancedSetting(SettingsToggle(
                        Strings.Get("SettingsHideScriptRunWindow"),
                        State.HideScriptRunWindow,
                        ToggleHideScriptRunWindow)),
                    "TipHideScriptRunWindow")));
        }

        return WithSettingsPageRestoreFooter(
            content,
            RestoreSettingsSidebarNoteDefaults);
    }

    private UIElement CreateSettingsSidebarMarkdownRenderSelector(
        Action<bool> onFullModeChanged)
    {
        var segments = new[]
        {
            (MarkdownRenderModes.Off, Strings.Get("MarkdownRenderOff")),
            (MarkdownRenderModes.Basic, Strings.Get("MarkdownRenderBasic")),
            (MarkdownRenderModes.Full, Strings.Get("MarkdownRenderFull"))
        };

        return CreateSegmentSelector(
            segments,
            State.MarkdownRenderMode,
            mode =>
            {
                SetMarkdownRenderMode(mode);
                onFullModeChanged(
                    State.MarkdownRenderMode == MarkdownRenderModes.Full);
            });
    }

    private void ToggleMarkdownEditAnimation()
    {
        State.MarkdownEditAnimationEnabled = !State.MarkdownEditAnimationEnabled;
        SaveNow();

        foreach (var window in _windows.Values)
        {
            window.UpdateMarkdownEditAnimation();
        }
    }

    private void RestoreSettingsSidebarNoteDefaults()
    {
        State.MarkdownRenderMode = MarkdownRenderModes.Basic;
        State.MarkdownEditAnimationEnabled = true;
        State.ExternalMarkdownExtension = ExternalMarkdownFileExtensions.Default;
        State.AutoCompressLargeImages = true;
        State.UsePersistentPowerShellProcess = false;
        State.PreferPowerShell7 = true;
        State.HideScriptRunWindow = true;
        _imageStore.AutoCompressLargeImages = true;

        PaperWindow.StopPersistentScriptProcesses();
        foreach (var window in _windows.Values)
        {
            window.UpdateMarkdownRenderMode();
            window.UpdateMarkdownEditAnimation();
            window.UpdateExternalMarkdownExtension();
        }

        SaveNow();
        RebuildTrayMenu();
        RefreshSettingsWindowContent();
    }
}
