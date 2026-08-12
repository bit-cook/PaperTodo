using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperSurfaceWindow : Window, IPaperSurface
{
    private const double RadiusShell = 16;
    private const double ResizeHandle = 6;

    private readonly AppState _state;
    private readonly PaperThemePalette _palette;
    private readonly Border _paperChrome;
    private readonly TextBox _title;
    private readonly PaperEditorControl _editor;

    public PaperSurfaceWindow(PaperSurfaceDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.PaperId);
        PaperId = descriptor.PaperId;
        Paper = descriptor.Paper;
        _state = descriptor.State;
        _palette = PaperThemePalette.Resolve(_state);

        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        CanResize = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Position = descriptor.Position;
        Width = Math.Max(PaperLayoutDefaults.MinWidth, descriptor.Size.Width);
        Height = Math.Max(PaperLayoutDefaults.MinHeight, descriptor.Size.Height);
        MinWidth = PaperLayoutDefaults.MinWidth;
        MinHeight = PaperLayoutDefaults.MinHeight;
        Topmost = descriptor.AlwaysOnTop;

        var root = new Grid();

        _paperChrome = new Border
        {
            Background = _palette.PaperBrush,
            BorderBrush = _palette.PaperBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(RadiusShell),
            ClipToBounds = true
        };

        var shell = new Grid
        {
            RowDefinitions = new RowDefinitions($"{PaperLayoutDefaults.TopBarHeight},*")
        };

        var topBar = new Border
        {
            Background = _palette.TopBarBrush,
            BorderBrush = _palette.DividerBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        var topBarGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        var dragHandle = new Border
        {
            Width = 26,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = Paper.Type == PaperTypes.Todo ? "✓" : "M",
                Foreground = _palette.WeakTextBrush,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        dragHandle.PointerPressed += BeginMoveFromPointer;
        ToolTip.SetTip(dragHandle, TextCatalog.Current.MovePaper);
        topBarGrid.Children.Add(dragHandle);

        _title = new TextBox
        {
            Text = Paper.Title ?? string.Empty,
            MaxLength = PaperTitleRules.NormalizeMaxTitleLength(_state.MaxTitleLength),
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(2, 0),
            Margin = new Thickness(0),
            Foreground = _palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                12,
                _state.TitleTextSize,
                OverallFontScales.Normalize(_state.Zoom)),
            FontWeight = _state.TitleTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        _title.TextChanged += (_, _) =>
        {
            var text = _title.Text ?? string.Empty;
            if (!string.Equals(Paper.Title, text, StringComparison.Ordinal))
            {
                Paper.Title = text;
                Changed?.Invoke();
            }
        };
        _title.LostFocus += (_, _) =>
        {
            var cleaned = PaperTitleRules.CleanCustomTitle(
                _title.Text,
                PaperTitleRules.NormalizeMaxTitleLength(_state.MaxTitleLength));
            if (!string.Equals(cleaned, _title.Text, StringComparison.Ordinal))
            {
                _title.Text = cleaned;
            }
        };
        topBarGrid.Children.Add(_title);
        Grid.SetColumn(_title, 1);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        if (_state.ShowTopBarNewTodoButton)
        {
            actions.Children.Add(CreateTopBarButton(
                "+✓",
                TextCatalog.Current.NewTodo,
                () => NewTodoRequested?.Invoke()));
        }

        if (_state.ShowTopBarNewNoteButton)
        {
            actions.Children.Add(CreateTopBarButton(
                "+M",
                TextCatalog.Current.NewNote,
                () => NewNoteRequested?.Invoke()));
        }

        var close = CreateTopBarButton(
            "×",
            TextCatalog.Current.HidePaper,
            () => CloseRequested?.Invoke());
        close.FontSize = 15;
        actions.Children.Add(close);

        topBarGrid.Children.Add(actions);
        Grid.SetColumn(actions, 2);

        topBar.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, topBar) || ReferenceEquals(e.Source, topBarGrid))
            {
                BeginMoveFromPointer(topBar, e);
            }
        };

        topBar.Child = topBarGrid;
        shell.Children.Add(topBar);

        _editor = new PaperEditorControl(Paper, _state, _palette, () => Changed?.Invoke());
        shell.Children.Add(_editor);
        Grid.SetRow(_editor, 1);

        _paperChrome.Child = shell;
        root.Children.Add(_paperChrome);

        AddResizeHandles(root);
        Content = root;
        ContextMenu = CreateContextMenu();

        Activated += (_, _) => Opacity = 1;
        Deactivated += (_, _) =>
        {
            if (_state.ExperimentalInactivePaperOpacity)
            {
                Opacity = ExperimentalOpacityLevels.Normalize(
                    _state.ExperimentalInactivePaperOpacityLevel,
                    ExperimentalOpacityLevels.DefaultInactivePaper);
            }
        };
    }

    public string PaperId { get; }

    public PaperData Paper { get; }

    Window IPaperSurface.Window => this;

    public event Action? Changed;
    public event Action? CloseRequested;
    public event Action? CollapseRequested;
    public event Action? DeleteRequested;
    public event Action? NewTodoRequested;
    public event Action? NewNoteRequested;

    public void ApplyDescriptor(PaperSurfaceDescriptor descriptor)
    {
        if (!string.Equals(PaperId, descriptor.PaperId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A paper surface cannot change its paper identity.",
                nameof(descriptor));
        }

        Position = descriptor.Position;
        Width = Math.Max(PaperLayoutDefaults.MinWidth, descriptor.Size.Width);
        Height = Math.Max(PaperLayoutDefaults.MinHeight, descriptor.Size.Height);
        Topmost = descriptor.AlwaysOnTop;

        if (descriptor.IsVisible)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void RefreshFromModel()
    {
        _title.Text = Paper.Title ?? string.Empty;
        _editor.RefreshFromModel();
    }

    private Button CreateTopBarButton(string glyph, string tooltip, Action action)
    {
        var button = new Button
        {
            Content = glyph,
            MinWidth = 25,
            MinHeight = PaperLayoutDefaults.TopBarHeight,
            Padding = new Thickness(4, 0),
            Margin = default,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            FontSize = 11,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => action();
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private ContextMenu CreateContextMenu()
    {
        var pin = new MenuItem
        {
            Header = TextCatalog.Current.AlwaysOnTop,
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = Paper.AlwaysOnTop
        };
        pin.Click += (_, _) =>
        {
            Paper.AlwaysOnTop = pin.IsChecked;
            Topmost = Paper.AlwaysOnTop;
            Changed?.Invoke();
        };

        var collapse = new MenuItem
        {
            Header = TextCatalog.Current.CollapsePaper,
            IsEnabled = _state.UseCapsuleMode && _state.UseDeepCapsuleMode
        };
        collapse.Click += (_, _) => CollapseRequested?.Invoke();

        var hide = new MenuItem { Header = TextCatalog.Current.HidePaper };
        hide.Click += (_, _) => CloseRequested?.Invoke();

        var delete = new MenuItem { Header = TextCatalog.Current.DeletePaper };
        delete.Click += (_, _) => DeleteRequested?.Invoke();

        return new ContextMenu
        {
            ItemsSource = new object[]
            {
                pin,
                new Separator(),
                collapse,
                hide,
                new Separator(),
                delete
            }
        };
    }

    private void BeginMoveFromPointer(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private void AddResizeHandles(Grid root)
    {
        AddResizeHandle(root, WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, ResizeHandle);
        AddResizeHandle(root, WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, ResizeHandle);
        AddResizeHandle(root, WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, ResizeHandle, double.NaN);
        AddResizeHandle(root, WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, ResizeHandle, double.NaN);
        AddResizeHandle(root, WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, ResizeHandle * 2, ResizeHandle * 2);
        AddResizeHandle(root, WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, ResizeHandle * 2, ResizeHandle * 2);
        AddResizeHandle(root, WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, ResizeHandle * 2, ResizeHandle * 2);
        AddResizeHandle(root, WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, ResizeHandle * 2, ResizeHandle * 2);
    }

    private void AddResizeHandle(
        Grid root,
        WindowEdge edge,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        double width,
        double height)
    {
        var handle = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };
        if (!double.IsNaN(width))
        {
            handle.Width = width;
        }
        if (!double.IsNaN(height))
        {
            handle.Height = height;
        }

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            BeginResizeDrag(edge, e);
            e.Handled = true;
        };
        root.Children.Add(handle);
    }
}
