using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PaperTodo.Avalonia.Tray;

namespace PaperTodo.Avalonia.Application;

/// <summary>
/// Starts a real Avalonia window from the published executable, lets the compositor render it,
/// verifies the Win32 host and embedded icon, and then exits without loading user state.
/// </summary>
internal static class AotUiSmokeTest
{
    private const string Argument = "--aot-smoke-ui";

    public static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(arguments[0], Argument, StringComparison.OrdinalIgnoreCase);

    public static void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Dispatcher.UIThread.VerifyAccess();

        var window = new Window
        {
            Title = "PaperTodo Native AOT UI smoke",
            Width = 320,
            Height = 120,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Icon = ApplicationIconLoader.Load(),
            Content = new Border
            {
                Background = Brushes.White,
                Child = new TextBlock
                {
                    Text = "PaperTodo",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        window.Opened += (_, _) =>
        {
            // Wait for two real animation ticks rather than a dispatcher delay. This roots and
            // exercises the Win32, Skia/ANGLE, compiled-XAML and resource paths that the
            // LMDB-only smoke deliberately does not touch.
            window.RequestAnimationFrame(_ =>
                window.RequestAnimationFrame(_ => Complete(window, desktop)));
        };
        window.Show();
    }

    private static void Complete(
        Window window,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var handle = window.TryGetPlatformHandle();
        var succeeded = window.IsVisible &&
            handle is not null &&
            handle.Handle != IntPtr.Zero &&
            string.Equals(handle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase);

        window.Close();
        desktop.Shutdown(succeeded ? 0 : 1);
    }
}
