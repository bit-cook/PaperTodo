using Avalonia.Controls;
using PaperTodo.Avalonia.Application;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Tray;

internal sealed class TrayIconController : IDisposable
{
    private readonly Func<StartupCommand, ValueTask> _commandSink;
    private readonly Action _settingsRequested;
    private readonly Func<IReadOnlyList<PaperTrayEntry>> _paperProvider;
    private readonly Action<string> _paperActivationRequested;
    private TrayIcon? _trayIcon;
    private bool _disposed;

    public TrayIconController(
        Func<StartupCommand, ValueTask> commandSink,
        Action settingsRequested,
        Func<IReadOnlyList<PaperTrayEntry>> paperProvider,
        Action<string> paperActivationRequested)
    {
        _commandSink = commandSink;
        _settingsRequested = settingsRequested;
        _paperProvider = paperProvider;
        _paperActivationRequested = paperActivationRequested;
    }

    public void Show()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = true;
            return;
        }

        var menu = new NativeMenu();
        PopulateMenu(menu);
        menu.NeedsUpdate += OnMenuNeedsUpdate;

        _trayIcon = new TrayIcon
        {
            Icon = ApplicationIconLoader.Load(),
            ToolTipText = TextCatalog.Current.ApplicationName,
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => Dispatch(StartupCommandKind.Toggle);
    }

    public void Hide()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
        }
    }

    private void OnMenuNeedsUpdate(object? sender, EventArgs e)
    {
        if (!_disposed && sender is NativeMenu menu)
        {
            PopulateMenu(menu);
        }
    }

    private void PopulateMenu(NativeMenu menu)
    {
        var text = TextCatalog.Current;
        var papers = _paperProvider();

        menu.Items.Clear();
        menu.Items.Add(CreateCommandItem(text.NewTodo, StartupCommandKind.NewTodo));
        menu.Items.Add(CreateCommandItem(text.NewNote, StartupCommandKind.NewNote));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateCommandItem(text.ShowAll, StartupCommandKind.Show));
        menu.Items.Add(CreateCommandItem(text.HideAll, StartupCommandKind.Hide));

        if (papers.Count > 0)
        {
            menu.Items.Add(new NativeMenuItemSeparator());
            foreach (var paper in papers)
            {
                var paperId = paper.Id;
                var paperItem = new NativeMenuItem(FormatPaperHeader(paper));
                paperItem.Click += (_, _) => _paperActivationRequested(paperId);
                menu.Items.Add(paperItem);
            }
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        var settings = new NativeMenuItem(text.Settings);
        settings.Click += (_, _) => _settingsRequested();
        menu.Items.Add(settings);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateCommandItem(text.Exit, StartupCommandKind.Exit));
    }

    private NativeMenuItem CreateCommandItem(string header, StartupCommandKind kind)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => Dispatch(kind);
        return item;
    }

    private static string FormatPaperHeader(PaperTrayEntry paper)
    {
        var marker = !paper.IsVisible
            ? "○"
            : paper.IsCollapsed
                ? "◐"
                : "●";
        var title = string.IsNullOrWhiteSpace(paper.Title)
            ? string.IsNullOrWhiteSpace(paper.Type) ? "Paper" : paper.Type
            : paper.Title.Trim();
        return $"{marker} {title}";
    }

    private void Dispatch(StartupCommandKind kind)
    {
        _ = _commandSink(new StartupCommand(kind));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_trayIcon?.Menu is NativeMenu menu)
        {
            menu.NeedsUpdate -= OnMenuNeedsUpdate;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
