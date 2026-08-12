using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Settings;

internal sealed class SettingsWindow : Window
{
    private readonly AppState _state;
    private readonly Action _apply;
    private readonly TextBlock _error;
    private readonly CheckBox _capsuleMode;
    private readonly CheckBox _edgeCapsuleMode;
    private readonly CheckBox _animations;
    private readonly CheckBox _toolTips;
    private readonly CheckBox _autoMoveCompleted;
    private readonly CheckBox _autoClearCompleted;
    private readonly CheckBox _showNewTodo;
    private readonly CheckBox _showNewNote;
    private readonly ComboBox _theme;
    private readonly ComboBox _colorScheme;
    private readonly Slider _zoom;
    private readonly ComboBox _todoSize;
    private readonly ComboBox _noteSize;
    private readonly ComboBox _titleSize;
    private readonly ComboBox _capsuleSize;
    private readonly CheckBox _todoBold;
    private readonly CheckBox _noteBold;
    private readonly CheckBox _titleBold;
    private readonly CheckBox _capsuleBold;
    private readonly CheckBox _inactiveOpacity;
    private readonly Slider _inactiveOpacityValue;
    private readonly CheckBox _distinguishNumpad;
    private readonly Dictionary<string, ShortcutRow> _shortcutRows = new(StringComparer.Ordinal);

    public SettingsWindow(AppState state, Action apply)
    {
        _state = state;
        _apply = apply;
        var text = TextCatalog.Current;

        Title = $"{text.ApplicationName} · {text.Settings}";
        Width = 590;
        Height = 650;
        MinWidth = 500;
        MinHeight = 520;
        CanResize = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _capsuleMode = SettingCheck(text.CapsuleMode, state.UseCapsuleMode);
        _edgeCapsuleMode = SettingCheck(text.EdgeCapsuleMode, state.UseDeepCapsuleMode);
        _animations = SettingCheck(text.Animations, state.EnableAnimations);
        _toolTips = SettingCheck(text.ToolTips, state.EnableToolTips);
        _autoMoveCompleted = SettingCheck(text.AutoMoveCompleted, state.AutoMoveCompletedTodosToBottom);
        _autoClearCompleted = SettingCheck(text.AutoClearCompleted, state.AutoClearCompletedTodos);
        _showNewTodo = SettingCheck(text.TopBarNewTodo, state.ShowTopBarNewTodoButton);
        _showNewNote = SettingCheck(text.TopBarNewNote, state.ShowTopBarNewNoteButton);

        _theme = SettingCombo(["system", "light", "dark"], state.Theme);
        _colorScheme = SettingCombo(ColorSchemes.All, ColorSchemes.Normalize(state.ColorScheme));
        _zoom = SettingSlider(OverallFontScales.Minimum, OverallFontScales.Maximum, state.Zoom);
        _todoSize = SettingCombo([TodoVisualSizes.Small, TodoVisualSizes.Medium, TodoVisualSizes.Large], TodoVisualSizes.Normalize(state.TodoVisualSize));
        _noteSize = SettingCombo([VisualTextSizes.Small, VisualTextSizes.Medium, VisualTextSizes.Large], VisualTextSizes.Normalize(state.NoteTextSize));
        _titleSize = SettingCombo([VisualTextSizes.Small, VisualTextSizes.Medium, VisualTextSizes.Large], VisualTextSizes.Normalize(state.TitleTextSize));
        _capsuleSize = SettingCombo([VisualTextSizes.Small, VisualTextSizes.Medium, VisualTextSizes.Large], VisualTextSizes.Normalize(state.CapsuleTextSize));
        _todoBold = SettingCheck(text.Bold, state.TodoTextBold);
        _noteBold = SettingCheck(text.Bold, state.NoteTextBold);
        _titleBold = SettingCheck(text.Bold, state.TitleTextBold);
        _capsuleBold = SettingCheck(text.Bold, state.CapsuleTextBold);
        _inactiveOpacity = SettingCheck(text.InactiveOpacity, state.ExperimentalInactivePaperOpacity);
        _inactiveOpacityValue = SettingSlider(
            ExperimentalOpacityLevels.Minimum,
            ExperimentalOpacityLevels.Maximum,
            ExperimentalOpacityLevels.Normalize(
                state.ExperimentalInactivePaperOpacityLevel,
                ExperimentalOpacityLevels.DefaultInactivePaper));
        _distinguishNumpad = SettingCheck(text.DistinguishNumpad, state.DistinguishNumpadShortcutDigits);

        var tabs = new TabControl
        {
            Margin = new Thickness(14, 14, 14, 8),
            ItemsSource = new object[]
            {
                new TabItem { Header = text.Behavior, Content = BuildBehaviorTab() },
                new TabItem { Header = text.Appearance, Content = BuildAppearanceTab() },
                new TabItem { Header = text.Shortcuts, Content = BuildShortcutsTab() }
            }
        };

        _error = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var applyButton = new Button
        {
            Content = text.Apply,
            MinWidth = 84,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        applyButton.Click += (_, _) => ApplySettings();
        var closeButton = new Button
        {
            Content = text.Close,
            MinWidth = 84,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        closeButton.Click += (_, _) => Close();

        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(14, 0, 14, 14),
            ColumnSpacing = 8
        };
        footer.Children.Add(_error);
        footer.Children.Add(applyButton);
        Grid.SetColumn(applyButton, 1);
        footer.Children.Add(closeButton);
        Grid.SetColumn(closeButton, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(tabs);
        root.Children.Add(footer);
        Grid.SetRow(footer, 1);
        Content = root;
    }

    private Control BuildBehaviorTab()
    {
        var panel = SettingsPanel();
        panel.Children.Add(Section(TextCatalog.Current.EdgeAndWindow));
        panel.Children.Add(_capsuleMode);
        panel.Children.Add(_edgeCapsuleMode);
        panel.Children.Add(_animations);
        panel.Children.Add(_toolTips);
        panel.Children.Add(Section(TextCatalog.Current.TodoBehavior));
        panel.Children.Add(_autoMoveCompleted);
        panel.Children.Add(_autoClearCompleted);
        panel.Children.Add(Section(TextCatalog.Current.TopBar));
        panel.Children.Add(_showNewTodo);
        panel.Children.Add(_showNewNote);
        return Scroller(panel);
    }

    private Control BuildAppearanceTab()
    {
        var text = TextCatalog.Current;
        var panel = SettingsPanel();
        panel.Children.Add(Section(text.Theme));
        panel.Children.Add(Labeled(text.Theme, _theme));
        panel.Children.Add(Labeled(text.ColorScheme, _colorScheme));
        panel.Children.Add(Labeled(text.Zoom, _zoom));
        panel.Children.Add(Section(text.Text));
        panel.Children.Add(SizeBoldRow(text.TodoSize, _todoSize, _todoBold));
        panel.Children.Add(SizeBoldRow(text.NoteSize, _noteSize, _noteBold));
        panel.Children.Add(SizeBoldRow(text.TitleSize, _titleSize, _titleBold));
        panel.Children.Add(SizeBoldRow(text.CapsuleSize, _capsuleSize, _capsuleBold));
        panel.Children.Add(Section(text.WindowOpacity));
        panel.Children.Add(_inactiveOpacity);
        panel.Children.Add(Labeled(text.InactiveOpacityLevel, _inactiveOpacityValue));
        return Scroller(panel);
    }

    private Control BuildShortcutsTab()
    {
        var text = TextCatalog.Current;
        var panel = SettingsPanel();
        panel.Children.Add(new TextBlock
        {
            Text = text.ShortcutHint,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var definition in GlobalShortcutCatalog.Definitions
                     .Where(definition => definition.Group == GlobalShortcutGroup.General))
        {
            var enabled = new CheckBox
            {
                IsChecked = _state.GlobalHotkeyEnabled.TryGetValue(definition.Id, out var on) && on,
                VerticalAlignment = VerticalAlignment.Center
            };
            var gesture = new TextBox
            {
                Text = _state.GlobalHotkeys.TryGetValue(definition.Id, out var value) ? value : definition.DefaultGesture,
                PlaceholderText = "Ctrl+Alt+…",
                MinWidth = 190
            };
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Margin = new Thickness(0, 3)
            };
            row.Children.Add(enabled);
            var label = new TextBlock
            {
                Text = ShortcutLabel(definition.Id),
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(label);
            Grid.SetColumn(label, 1);
            row.Children.Add(gesture);
            Grid.SetColumn(gesture, 2);
            panel.Children.Add(row);
            _shortcutRows[definition.Id] = new ShortcutRow(enabled, gesture);
        }

        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
        panel.Children.Add(_distinguishNumpad);
        return Scroller(panel);
    }

    private void ApplySettings()
    {
        var text = TextCatalog.Current;
        var normalizedBindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, row) in _shortcutRows)
        {
            var raw = (row.Gesture.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                normalizedBindings[id] = string.Empty;
                if (row.Enabled.IsChecked == true)
                {
                    _error.Text = $"{text.InvalidShortcut}: {ShortcutLabel(id)}";
                    return;
                }
                continue;
            }

            if (!ShortcutGesture.TryParse(raw, out var parsed))
            {
                _error.Text = $"{text.InvalidShortcut}: {ShortcutLabel(id)} · {raw}";
                return;
            }

            normalizedBindings[id] = parsed.ToStorageString();
        }

        _state.UseCapsuleMode = _capsuleMode.IsChecked == true;
        _state.UseDeepCapsuleMode = _edgeCapsuleMode.IsChecked == true;
        _state.EnableAnimations = _animations.IsChecked == true;
        _state.EnableToolTips = _toolTips.IsChecked == true;
        _state.AutoMoveCompletedTodosToBottom = _autoMoveCompleted.IsChecked == true;
        _state.AutoClearCompletedTodos = _autoClearCompleted.IsChecked == true;
        _state.ShowTopBarNewTodoButton = _showNewTodo.IsChecked == true;
        _state.ShowTopBarNewNoteButton = _showNewNote.IsChecked == true;
        _state.Theme = (_theme.SelectedItem as string) ?? "system";
        _state.ColorScheme = ColorSchemes.Normalize(_colorScheme.SelectedItem as string);
        _state.Zoom = OverallFontScales.Normalize(_zoom.Value);
        _state.TodoVisualSize = TodoVisualSizes.Normalize(_todoSize.SelectedItem as string);
        _state.NoteTextSize = VisualTextSizes.Normalize(_noteSize.SelectedItem as string);
        _state.TitleTextSize = VisualTextSizes.Normalize(_titleSize.SelectedItem as string);
        _state.CapsuleTextSize = VisualTextSizes.Normalize(_capsuleSize.SelectedItem as string);
        _state.TodoTextBold = _todoBold.IsChecked == true;
        _state.NoteTextBold = _noteBold.IsChecked == true;
        _state.TitleTextBold = _titleBold.IsChecked == true;
        _state.CapsuleTextBold = _capsuleBold.IsChecked == true;
        _state.ExperimentalInactivePaperOpacity = _inactiveOpacity.IsChecked == true;
        _state.ExperimentalInactivePaperOpacityLevel = ExperimentalOpacityLevels.Normalize(
            _inactiveOpacityValue.Value,
            ExperimentalOpacityLevels.DefaultInactivePaper);
        _state.DistinguishNumpadShortcutDigits = _distinguishNumpad.IsChecked == true;

        foreach (var (id, value) in normalizedBindings)
        {
            _state.GlobalHotkeys[id] = value;
            _state.GlobalHotkeyEnabled[id] = _shortcutRows[id].Enabled.IsChecked == true;
        }

        _error.Text = string.Empty;
        _apply();
    }

    private string ShortcutLabel(string id)
    {
        var text = TextCatalog.Current;
        return id switch
        {
            GlobalShortcutCatalog.Show => text.ShowAll,
            GlobalShortcutCatalog.Hide => text.HideAll,
            GlobalShortcutCatalog.Toggle => text.ToggleVisibility,
            GlobalShortcutCatalog.NewTodo => text.NewTodo,
            GlobalShortcutCatalog.NewNote => text.NewNote,
            GlobalShortcutCatalog.Exit => text.Exit,
            _ => id
        };
    }

    private static StackPanel SettingsPanel() => new()
    {
        Spacing = 5,
        Margin = new Thickness(14)
    };

    private static ScrollViewer Scroller(Control content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 12, 0, 4)
    };

    private static CheckBox SettingCheck(string text, bool value) => new()
    {
        Content = text,
        IsChecked = value,
        Margin = new Thickness(0, 2)
    };

    private static ComboBox SettingCombo(IEnumerable<string> values, string selected) => new()
    {
        ItemsSource = values.ToArray(),
        SelectedItem = selected,
        MinWidth = 160,
        HorizontalAlignment = HorizontalAlignment.Right
    };

    private static Slider SettingSlider(double minimum, double maximum, double value) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = Math.Clamp(value, minimum, maximum),
        Width = 220,
        HorizontalAlignment = HorizontalAlignment.Right
    };

    private static Control Labeled(string label, Control control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 3)
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        });
        grid.Children.Add(control);
        Grid.SetColumn(control, 1);
        return grid;
    }

    private static Control SizeBoldRow(string label, ComboBox size, CheckBox bold)
    {
        var group = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        group.Children.Add(size);
        group.Children.Add(bold);
        return Labeled(label, group);
    }

    private sealed record ShortcutRow(CheckBox Enabled, TextBox Gesture);
}
