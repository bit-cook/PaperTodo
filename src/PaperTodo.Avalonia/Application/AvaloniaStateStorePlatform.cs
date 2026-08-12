using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PaperTodo.Avalonia.Application;

internal sealed class AvaloniaStateStorePlatform : IStateStorePlatform, IDisposable
{
    private Window? _screenProbe;
    private Screens? _screens;
    private bool _disposed;

    public async ValueTask InitializeScreensAsync(CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_screens is not null)
        {
            return;
        }

        // Avalonia exposes Screens through a TopLevel. PaperTodo deliberately has no main
        // window, so keep one invisible infrastructure TopLevel alive for monitor changes and
        // for the all-docked-paper startup case where no ordinary paper window is created.
        _screenProbe = new Window
        {
            Width = 1,
            Height = 1,
            Position = new PixelPoint(-32_000, -32_000),
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowDecorations = global::Avalonia.Controls.WindowDecorations.None,
            ShowActivated = false,
            ShowInTaskbar = false,
            CanResize = false,
            Opacity = 0,
            Background = Brushes.Transparent
        };
        _screenProbe.Show();
        _screens = _screenProbe.Screens;
        await _screens.RequestScreenDetails();
        cancellationToken.ThrowIfCancellationRequested();
        _screenProbe.Hide();
    }

    public Screens? Screens => _screens;

    public TopLevel InfrastructureTopLevel => _screenProbe ??
        throw new InvalidOperationException("The monitor infrastructure TopLevel is not initialized.");

    public string NormalizeMonitorDeviceName(string? deviceName)
    {
        var value = (deviceName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return TryFindScreen(value, out var screen) && screen.IsPrimary
            ? string.Empty
            : value;
    }

    public Dictionary<string, string> NormalizeGlobalHotkeys(
        Dictionary<string, string>? source) =>
        GlobalShortcutCatalog.NormalizeBindings(source);

    public Dictionary<string, bool> NormalizeGlobalHotkeyEnabled(
        Dictionary<string, bool>? source) =>
        GlobalShortcutCatalog.NormalizeEnabled(source);

    public double NormalizeDeepCapsuleStartTopMargin(
        double value,
        string monitorDeviceName,
        double gapDip)
    {
        var area = ResolveLocalWorkArea(monitorDeviceName);
        return EdgeCapsuleLayout.NormalizeStartTopMargin(
            value,
            area,
            slotCount: 1,
            gap: gapDip);
    }

    private DipRect ResolveLocalWorkArea(string monitorDeviceName)
    {
        if (TryFindScreen(monitorDeviceName, out var screen) ||
            TryGetScreens(out var screens) && (screen = screens.Primary) is not null)
        {
            var scaling = Math.Max(1, screen.Scaling);
            return DipRect.FromPositionAndSize(
                0,
                0,
                screen.WorkingArea.Width / scaling,
                screen.WorkingArea.Height / scaling);
        }

        return DipRect.FromPositionAndSize(0, 0, 1920, 1080);
    }

    private bool TryFindScreen(string monitorDeviceName, out Screen screen)
    {
        screen = null!;
        if (!TryGetScreens(out var screens))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(monitorDeviceName))
        {
            if (screens.Primary is not null)
            {
                screen = screens.Primary;
                return true;
            }

            return false;
        }

        foreach (var candidate in screens.All)
        {
            if (string.Equals(
                candidate.DisplayName,
                monitorDeviceName,
                StringComparison.Ordinal))
            {
                screen = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetScreens(out Screens screens)
    {
        if (_screens is null)
        {
            screens = null!;
            return false;
        }

        screens = _screens;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _screens = null;
        if (_screenProbe is not null && Dispatcher.UIThread.CheckAccess())
        {
            _screenProbe.Close();
        }

        _screenProbe = null;
    }
}
