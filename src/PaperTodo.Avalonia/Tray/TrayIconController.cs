using Avalonia.Controls;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Tray;

internal sealed class TrayIconController : IDisposable
{
    private readonly Func<StartupCommand, ValueTask> _commandSink;
    private readonly Action _settingsRequested;
    private TrayIcon? _trayIcon;
    private bool _disposed;

    public TrayIconController(
        Func<StartupCommand, ValueTask> commandSink,
        Action settingsRequested)
    {
        _commandSink = commandSink;
        _settingsRequested = settingsRequested;
    }

    public void Show()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = true;
            return;
        }

        var text = TextCatalog.Current;
        var newTodo = new NativeMenuItem(text.NewTodo);
        var newNote = new NativeMenuItem(text.NewNote);
        var show = new NativeMenuItem(text.ShowAll);
        var hide = new NativeMenuItem(text.HideAll);
        var settings = new NativeMenuItem(text.Settings);
        var exit = new NativeMenuItem(text.Exit);

        newTodo.Click += (_, _) => Dispatch(StartupCommandKind.NewTodo);
        newNote.Click += (_, _) => Dispatch(StartupCommandKind.NewNote);
        show.Click += (_, _) => Dispatch(StartupCommandKind.Show);
        hide.Click += (_, _) => Dispatch(StartupCommandKind.Hide);
        settings.Click += (_, _) => _settingsRequested();
        exit.Click += (_, _) => Dispatch(StartupCommandKind.Exit);

        var menu = new NativeMenu();
        menu.Items.Add(newTodo);
        menu.Items.Add(newNote);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(show);
        menu.Items.Add(hide);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(settings);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        _trayIcon = new TrayIcon
        {
            Icon = ApplicationIconLoader.Load(),
            ToolTipText = text.ApplicationName,
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
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
