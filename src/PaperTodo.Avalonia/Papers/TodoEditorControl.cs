using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class TodoEditorControl : Grid
{
    private readonly PaperData _paper;
    private readonly AppState _state;
    private readonly PaperThemePalette _palette;
    private readonly Action _changed;
    private readonly StackPanel _rows = new() { Spacing = 1 };
    private bool _refreshing;

    public TodoEditorControl(
        PaperData paper,
        AppState state,
        PaperThemePalette palette,
        Action changed)
    {
        _paper = paper;
        _state = state;
        _palette = palette;
        _changed = changed;

        RowDefinitions = new RowDefinitions("*,Auto");
        Background = Brushes.Transparent;

        var scroller = new ScrollViewer
        {
            Content = _rows,
            Padding = new Thickness(6, 5, 6, 3)
        };
        Children.Add(scroller);

        var metrics = TodoVisualSizes.Metrics(_state.TodoVisualSize, _state.Zoom);
        var add = new Button
        {
            Content = "+",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = metrics.AppendMinHeight,
            Padding = new Thickness(10, 2),
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            FontSize = metrics.AppendGlyphFontSize
        };
        add.Click += (_, _) => AddItem();
        ToolTip.SetTip(add, TextCatalog.Current.AddTodo);
        Children.Add(add);
        Grid.SetRow(add, 1);

        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        _refreshing = true;
        try
        {
            _rows.Children.Clear();
            foreach (var item in _paper.Items.OrderBy(item => item.Order))
            {
                _rows.Children.Add(CreateRow(item));
            }

            if (_paper.Items.Count == 0)
            {
                _rows.Children.Add(new TextBlock
                {
                    Text = TextCatalog.Current.TodoPlaceholder,
                    Foreground = _palette.WeakTextBrush,
                    Margin = new Thickness(8, 6),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Control CreateRow(PaperItem item)
    {
        var metrics = TodoVisualSizes.Metrics(_state.TodoVisualSize, _state.Zoom);
        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            MinHeight = metrics.RowMinHeight,
            Padding = new Thickness(3, 0),
            Opacity = item.Done ? 0.58 : 1
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var check = new CheckBox
        {
            IsChecked = item.Done,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 4, 0),
            Foreground = _palette.ActiveBrush
        };
        check.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing)
            {
                return;
            }

            var done = check.IsChecked == true;
            if (item.Done == done)
            {
                return;
            }

            item.Done = done;
            if (done)
            {
                item.ReminderAt = null;
                item.ReminderTriggered = false;
            }

            if (done && _state.AutoMoveCompletedTodosToBottom)
            {
                MoveCompletedToBottom(item);
                RefreshFromModel();
            }
            else
            {
                row.Opacity = done ? 0.58 : 1;
            }

            _changed();
        };
        grid.Children.Add(check);

        var editor = new TextBox
        {
            Text = item.Text ?? string.Empty,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(2, metrics.TextVerticalPadding),
            Foreground = _palette.TextBrush,
            FontSize = metrics.TextFontSize,
            FontWeight = _state.TodoTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        editor.TextChanged += (_, _) =>
        {
            if (_refreshing)
            {
                return;
            }

            var text = editor.Text ?? string.Empty;
            if (string.Equals(item.Text, text, StringComparison.Ordinal))
            {
                return;
            }

            item.Text = text;
            if (item.ReminderTriggered)
            {
                item.ReminderAt = null;
                item.ReminderTriggered = false;
            }

            _changed();
        };
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                InsertAfter(item);
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key is Key.Up or Key.Down)
            {
                MoveItem(item, e.Key == Key.Up ? -1 : 1);
                e.Handled = true;
            }
        };
        grid.Children.Add(editor);
        Grid.SetColumn(editor, 1);

        var status = new TextBlock
        {
            Text = StatusText(item),
            Foreground = _palette.WeakTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0),
            FontSize = Math.Max(10, metrics.GhostTextFontSize - 1)
        };
        grid.Children.Add(status);
        Grid.SetColumn(status, 2);

        var delete = new Button
        {
            Content = "×",
            MinWidth = metrics.CheckColumnWidth,
            MinHeight = metrics.RowMinHeight,
            Padding = default,
            Margin = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            FontSize = metrics.TrashGlyphFontSize
        };
        delete.Click += (_, _) => RemoveItem(item);
        ToolTip.SetTip(delete, TextCatalog.Current.DeleteTodo);
        grid.Children.Add(delete);
        Grid.SetColumn(delete, 3);

        row.Child = grid;
        row.PointerEntered += (_, _) => row.Background = _palette.HoverBrush;
        row.PointerExited += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    private static string StatusText(PaperItem item)
    {
        var reminder = item.ReminderAt.HasValue ? "⏰" : string.Empty;
        var link = !string.IsNullOrWhiteSpace(item.LinkedPaperId) ||
                   !string.IsNullOrWhiteSpace(item.LinkedPath)
            ? "↗"
            : string.Empty;
        return reminder + link;
    }

    private void AddItem()
    {
        var item = new PaperItem { Order = _paper.Items.Count };
        _paper.Items.Add(item);
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private void InsertAfter(PaperItem item)
    {
        var ordered = _paper.Items.OrderBy(candidate => candidate.Order).ToList();
        var index = ordered.FindIndex(candidate => candidate.Id == item.Id);
        var created = new PaperItem();
        ordered.Insert(Math.Max(0, index + 1), created);
        _paper.Items = ordered;
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private void RemoveItem(PaperItem item)
    {
        _paper.Items.RemoveAll(candidate => candidate.Id == item.Id);
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private void MoveItem(PaperItem item, int offset)
    {
        var ordered = _paper.Items.OrderBy(candidate => candidate.Order).ToList();
        var index = ordered.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0)
        {
            return;
        }

        var target = Math.Clamp(index + offset, 0, ordered.Count - 1);
        if (target == index)
        {
            return;
        }

        ordered.RemoveAt(index);
        ordered.Insert(target, item);
        _paper.Items = ordered;
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private void MoveCompletedToBottom(PaperItem changedItem)
    {
        var ordered = _paper.Items.OrderBy(item => item.Order).ToList();
        ordered.RemoveAll(item => item.Id == changedItem.Id);
        ordered.Add(changedItem);
        _paper.Items = ordered;
        TodoRules.NormalizeOrders(_paper.Items);
    }
}
