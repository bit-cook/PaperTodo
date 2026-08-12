using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class TodoEditorControl : Grid
{
    private const int MaxUndoDepth = 100;

    private readonly PaperData _paper;
    private readonly AppState _state;
    private readonly PaperThemePalette _palette;
    private readonly Action _changed;
    private readonly StackPanel _rows = new() { Spacing = 1 };
    private readonly List<Border> _rowControls = [];
    private readonly List<List<PaperItem>> _undo = [];
    private readonly List<List<PaperItem>> _redo = [];
    private string? _dragItemId;
    private Point _dragStartPoint;
    private int _dragInsertIndex = -1;
    private bool _dragStarted;
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
        if (_state.EnableToolTips)
        {
            ToolTip.SetTip(add, TextCatalog.Current.AddTodo);
        }
        Children.Add(add);
        Grid.SetRow(add, 1);

        ContextMenu = BuildContextMenu();
        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        _refreshing = true;
        try
        {
            _rows.Children.Clear();
            _rowControls.Clear();
            foreach (var item in _paper.Items.OrderBy(item => item.Order))
            {
                var row = CreateRow(item);
                _rowControls.Add(row);
                _rows.Children.Add(row);
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

    private Border CreateRow(PaperItem item)
    {
        var metrics = TodoVisualSizes.Metrics(_state.TodoVisualSize, _state.Zoom);
        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            MinHeight = metrics.RowMinHeight,
            Padding = new Thickness(2, 0),
            Opacity = item.Done ? 0.58 : 1
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var drag = new Border
        {
            Width = 15,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "⋮",
                Foreground = _palette.WeakTextBrush,
                Opacity = 0.45,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        drag.PointerPressed += (_, e) => BeginRowDrag(item, drag, e);
        drag.PointerMoved += (_, e) => ContinueRowDrag(e);
        drag.PointerReleased += (_, e) => EndRowDrag(e);
        grid.Children.Add(drag);

        var check = new CheckBox
        {
            IsChecked = item.Done,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1, 0, 4, 0),
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

            PushUndo();
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
        Grid.SetColumn(check, 1);

        var editor = new TextBox
        {
            Text = item.Text ?? string.Empty,
            MaxLength = 5000,
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
        Grid.SetColumn(editor, 2);

        var statusHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (_state.ExperimentalTodoReminders && _state.ExperimentalTodoReminderShowButton)
        {
            var reminder = new Button
            {
                Content = item.ReminderAt.HasValue ? "⏰" : "◷",
                Padding = new Thickness(3, 0),
                MinWidth = metrics.CheckColumnWidth,
                MinHeight = metrics.RowMinHeight,
                Background = Brushes.Transparent,
                BorderThickness = default,
                Foreground = item.ReminderAt.HasValue ? _palette.ActiveBrush : _palette.WeakTextBrush,
                Opacity = item.ReminderAt.HasValue ? 1 : 0.48,
                FontSize = Math.Max(10, metrics.GhostTextFontSize)
            };
            reminder.Click += (_, _) => ToggleQuickReminder(item);
            if (_state.EnableToolTips)
            {
                ToolTip.SetTip(reminder, ReminderToolTip(item));
            }
            statusHost.Children.Add(reminder);
        }

        if (!string.IsNullOrWhiteSpace(item.LinkedPaperId) || !string.IsNullOrWhiteSpace(item.LinkedPath))
        {
            statusHost.Children.Add(new TextBlock
            {
                Text = "↗",
                Foreground = _palette.WeakTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0),
                FontSize = Math.Max(10, metrics.GhostTextFontSize)
            });
        }
        grid.Children.Add(statusHost);
        Grid.SetColumn(statusHost, 3);

        var delete = new Button
        {
            Content = "×",
            MinWidth = metrics.CheckColumnWidth,
            MinHeight = metrics.RowMinHeight,
            Padding = default,
            Margin = new Thickness(1, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            FontSize = metrics.TrashGlyphFontSize
        };
        delete.Click += (_, _) => RemoveItem(item);
        if (_state.EnableToolTips)
        {
            ToolTip.SetTip(delete, TextCatalog.Current.DeleteTodo);
        }
        grid.Children.Add(delete);
        Grid.SetColumn(delete, 4);

        row.Child = grid;
        row.PointerEntered += (_, _) => row.Background = _palette.HoverBrush;
        row.PointerExited += (_, _) =>
        {
            if (!_dragStarted)
            {
                row.Background = Brushes.Transparent;
            }
        };
        return row;
    }

    private ContextMenu BuildContextMenu()
    {
        var undo = new MenuItem { Header = "Undo" };
        undo.Click += (_, _) => Undo();
        var redo = new MenuItem { Header = "Redo" };
        redo.Click += (_, _) => Redo();
        return new ContextMenu
        {
            ItemsSource = new object[] { undo, redo }
        };
    }

    private void BeginRowDrag(PaperItem item, Control handle, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragItemId = item.Id;
        _dragStartPoint = e.GetPosition(_rows);
        _dragInsertIndex = item.Order;
        _dragStarted = false;
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    private void ContinueRowDrag(PointerEventArgs e)
    {
        if (_dragItemId is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_rows);
        if (!_dragStarted && Math.Abs(point.Y - _dragStartPoint.Y) < 4)
        {
            return;
        }

        if (!_dragStarted)
        {
            PushUndo();
            _dragStarted = true;
        }

        _dragInsertIndex = ResolveInsertionIndex(point.Y);
        for (var index = 0; index < _rowControls.Count; index++)
        {
            _rowControls[index].Background = index == Math.Min(_dragInsertIndex, _rowControls.Count - 1)
                ? _palette.HoverBrush
                : Brushes.Transparent;
        }
        e.Handled = true;
    }

    private void EndRowDrag(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        if (_dragItemId is null)
        {
            return;
        }

        var itemId = _dragItemId;
        var started = _dragStarted;
        var insertion = _dragInsertIndex;
        _dragItemId = null;
        _dragStarted = false;
        _dragInsertIndex = -1;

        if (!started)
        {
            return;
        }

        var ordered = _paper.Items.OrderBy(item => item.Order).ToList();
        var originalIndex = ordered.FindIndex(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (originalIndex < 0)
        {
            RefreshFromModel();
            return;
        }

        var item = ordered[originalIndex];
        ordered.RemoveAt(originalIndex);
        var target = Math.Clamp(insertion, 0, ordered.Count + 1);
        if (target > originalIndex)
        {
            target--;
        }
        target = Math.Clamp(target, 0, ordered.Count);
        ordered.Insert(target, item);
        _paper.Items = ordered;
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
        e.Handled = true;
    }

    private int ResolveInsertionIndex(double y)
    {
        for (var index = 0; index < _rowControls.Count; index++)
        {
            var bounds = _rowControls[index].Bounds;
            if (y < bounds.Y + bounds.Height / 2)
            {
                return index;
            }
        }
        return _rowControls.Count;
    }

    private void ToggleQuickReminder(PaperItem item)
    {
        PushUndo();
        if (item.ReminderAt.HasValue && !item.ReminderTriggered)
        {
            item.ReminderAt = null;
            item.ReminderTriggered = false;
        }
        else
        {
            var minutes = Math.Clamp(
                _state.ExperimentalTodoReminderQuickMinutes,
                ExperimentalTodoReminderOptions.MinimumQuickMinutes,
                ExperimentalTodoReminderOptions.MaximumQuickMinutes);
            item.ReminderAt = DateTimeOffset.Now.AddMinutes(minutes);
            item.ReminderTriggered = false;
        }
        RefreshFromModel();
        _changed();
    }

    private string ReminderToolTip(PaperItem item)
    {
        if (item.ReminderAt is { } reminderAt)
        {
            return reminderAt.ToLocalTime().ToString("g");
        }

        var minutes = Math.Clamp(
            _state.ExperimentalTodoReminderQuickMinutes,
            ExperimentalTodoReminderOptions.MinimumQuickMinutes,
            ExperimentalTodoReminderOptions.MaximumQuickMinutes);
        return $"+{minutes} min";
    }

    private void AddItem()
    {
        PushUndo();
        var item = new PaperItem { Order = _paper.Items.Count };
        _paper.Items.Add(item);
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private void InsertAfter(PaperItem item)
    {
        PushUndo();
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
        PushUndo();
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

        PushUndo();
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

    private void PushUndo()
    {
        _undo.Add(CloneItems(_paper.Items));
        if (_undo.Count > MaxUndoDepth)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Add(CloneItems(_paper.Items));
        var snapshot = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        RestoreSnapshot(snapshot);
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Add(CloneItems(_paper.Items));
        var snapshot = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        RestoreSnapshot(snapshot);
    }

    private void RestoreSnapshot(IReadOnlyCollection<PaperItem> snapshot)
    {
        _paper.Items = CloneItems(snapshot);
        TodoRules.NormalizeOrders(_paper.Items);
        RefreshFromModel();
        _changed();
    }

    private static List<PaperItem> CloneItems(IEnumerable<PaperItem> source)
    {
        var result = new List<PaperItem>();
        foreach (var item in source)
        {
            var clone = new PaperItem
            {
                Id = item.Id,
                Text = item.Text,
                Done = item.Done,
                Order = item.Order,
                ReminderAt = item.ReminderAt,
                ReminderTriggered = item.ReminderTriggered
            };
            clone.RestoreQuickLaunch(item.LinkedPaperId, item.LinkedPath);
            result.Add(clone);
        }
        return result;
    }
}
