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
    private readonly Border _titleHost;
    private readonly TextBlock _titleText;
    private readonly TextBox _titleEditBox;
    private readonly Button _pinButton;
    private readonly PaperEditorControl _editor;
    private ExternalMarkdownEditorSession? _externalMarkdownSession;
    private string _titleBeforeEdit = string.Empty;

    public PaperSurfaceWindow(PaperSurfaceDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.PaperId);
        PaperId = descriptor.PaperId;
        Paper = descriptor.Paper;
        _state = descriptor.State;
        _palette = PaperThemePalette.Resolve(_state);

        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = !_state.HidePapersFromTaskbar;
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

        _pinButton = CreateTopBarButton(
            "⌖",
            TextCatalog.Current.AlwaysOnTop,
            TogglePin);
        _pinButton.MinWidth = 27;
        UpdatePinVisual();
        topBarGrid.Children.Add(_pinButton);

        _titleText = new TextBlock
        {
            Text = DisplayTitle(),
            Foreground = _palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                12,
                _state.TitleTextSize,
                OverallFontScales.Normalize(_state.Zoom)),
            FontWeight = _state.TitleTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(3, 0, 4, 0)
        };
        _titleEditBox = new TextBox
        {
            Text = Paper.Title ?? string.Empty,
            MaxLength = PaperTitleRules.NormalizeMaxTitleLength(_state.MaxTitleLength),
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(2, 0),
            Margin = new Thickness(0),
            Foreground = _palette.TextBrush,
            FontSize = _titleText.FontSize,
            FontWeight = _titleText.FontWeight,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false
        };
        _titleEditBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                EndTitleEdit(commit: true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                EndTitleEdit(commit: false);
                e.Handled = true;
            }
        };
        _titleEditBox.LostFocus += (_, _) =>
        {
            if (_titleEditBox.IsVisible)
            {
                EndTitleEdit(commit: true);
            }
        };

        var titleLayer = new Grid();
        titleLayer.Children.Add(_titleText);
        titleLayer.Children.Add(_titleEditBox);
        _titleHost = new Border
        {
            Background = Brushes.Transparent,
            Child = titleLayer
        };
        _titleHost.PointerPressed += (_, e) =>
        {
            if (_titleEditBox.IsVisible ||
                !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.ClickCount >= 2)
            {
                BeginTitleEdit();
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
            e.Handled = true;
        };
        if (_state.EnableToolTips)
        {
            ToolTip.SetTip(_titleHost, TextCatalog.Current.MovePaper);
        }
        topBarGrid.Children.Add(_titleHost);
        Grid.SetColumn(_titleHost, 1);

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
        if (Paper.Type == PaperTypes.Note &&
            PaperTextCodec.CanEditBody(Paper) &&
            _state.ShowTopBarExternalOpenButton)
        {
            actions.Children.Add(CreateTopBarButton(
                "↗",
                TextCatalog.Current.OpenExternalEditor,
                OpenExternalEditor));
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
            if ((ReferenceEquals(e.Source, topBar) || ReferenceEquals(e.Source, topBarGrid)) &&
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
                e.Handled = true;
            }
        };
        topBar.Child = topBarGrid;
        shell.Children.Add(topBar);

        _editor = new PaperEditorControl(
            Paper,
            _state,
            _palette,
            () => Changed?.Invoke(),
            paperId => LinkedPaperRequested?.Invoke(paperId));
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
    public event Action<string>? LinkedPaperRequested;

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
        UpdatePinVisual();

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
        _titleText.Text = DisplayTitle();
        if (!_titleEditBox.IsVisible)
        {
            _titleEditBox.Text = Paper.Title ?? string.Empty;
        }
        UpdatePinVisual();
        _editor.RefreshFromModel();
    }

    private string DisplayTitle() =>
        string.IsNullOrWhiteSpace(Paper.Title)
            ? (Paper.Type == PaperTypes.Todo ? "Todo" : "Note")
            : Paper.Title;

    private void TogglePin()
    {
        Paper.AlwaysOnTop = !Paper.AlwaysOnTop;
        Topmost = Paper.AlwaysOnTop;
        UpdatePinVisual();
        Changed?.Invoke();
    }

    private void UpdatePinVisual()
    {
        _pinButton.Foreground = Paper.AlwaysOnTop
            ? _palette.ActiveBrush
            : _palette.WeakTextBrush;
        _pinButton.Opacity = Paper.AlwaysOnTop ? 1 : 0.72;
    }

    private void BeginTitleEdit()
    {
        if (_titleEditBox.IsVisible)
        {
            return;
        }

        _titleBeforeEdit = Paper.Title ?? string.Empty;
        _titleEditBox.Text = _titleBeforeEdit;
        _titleText.IsVisible = false;
        _titleEditBox.IsVisible = true;
        _titleEditBox.Focus();
        _titleEditBox.SelectAll();
    }

    private void EndTitleEdit(bool commit)
    {
        if (!_titleEditBox.IsVisible)
        {
            return;
        }

        var value = commit
            ? PaperTitleRules.CleanCustomTitle(
                _titleEditBox.Text,
                PaperTitleRules.NormalizeMaxTitleLength(_state.MaxTitleLength))
            : _titleBeforeEdit;
        var changed = !string.Equals(Paper.Title, value, StringComparison.Ordinal);
        Paper.Title = value;
        _titleEditBox.Text = value;
        _titleEditBox.IsVisible = false;
        _titleText.Text = DisplayTitle();
        _titleText.IsVisible = true;
        Focus();
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private void OpenExternalEditor()
    {
        if (Paper.Type != PaperTypes.Note || !PaperTextCodec.CanEditBody(Paper))
        {
            return;
        }

        _externalMarkdownSession ??= new ExternalMarkdownEditorSession(
            Paper,
            _state.ExternalMarkdownExtension,
            () =>
            {
                _editor.RefreshFromModel();
                Changed?.Invoke();
            });
        _externalMarkdownSession.Open();
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
        if (_state.EnableToolTips)
        {
            ToolTip.SetTip(button, tooltip);
        }
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
            UpdatePinVisual();
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

        var items = new List<object> { pin };
        if (Paper.Type == PaperTypes.Note && PaperTextCodec.CanEditBody(Paper))
        {
            var external = new MenuItem { Header = TextCatalog.Current.OpenExternalEditor };
            external.Click += (_, _) => OpenExternalEditor();
            items.Add(external);
        }
        items.Add(new Separator());
        items.Add(collapse);
        items.Add(hide);
        items.Add(new Separator());
        items.Add(delete);
        return new ContextMenu { ItemsSource = items };
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

    protected override void OnClosed(EventArgs e)
    {
        _externalMarkdownSession?.Dispose();
        _externalMarkdownSession = null;
        base.OnClosed(e);
    }
}
