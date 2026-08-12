using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PaperTodo.Avalonia.Papers;
using PaperTodo.Avalonia.Tray;

namespace PaperTodo.Avalonia.Application;

/// <summary>
/// Starts the real Avalonia paper surface from the published executable, lets the compositor
/// render it, verifies the Win32 host and embedded icon, and then exits without loading user state.
/// This keeps CI tied to the actual product window instead of a generic framework test window.
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

        var state = new AppState
        {
            Theme = "light",
            ColorScheme = ColorSchemes.Warm,
            UseCapsuleMode = true,
            UseDeepCapsuleMode = true
        };
        var paper = new PaperData
        {
            Type = PaperTypes.Todo,
            Title = "PaperTodo",
            Width = PaperLayoutDefaults.TodoDefaultWidth,
            Height = 180,
            IsVisible = true,
            Items =
            [
                new PaperItem
                {
                    Text = "Native AOT UI smoke",
                    Order = 0
                }
            ]
        };
        var window = new PaperSurfaceWindow(new PaperSurfaceDescriptor(
            paper,
            state,
            new PixelPoint(100, 100),
            new Size(paper.Width, paper.Height),
            IsVisible: true,
            AlwaysOnTop: false))
        {
            Icon = ApplicationIconLoader.Load()
        };

        window.Opened += (_, _) =>
        {
            window.RequestAnimationFrame(_ =>
                window.RequestAnimationFrame(_ => Complete(window, desktop)));
        };
        window.Show();
    }

    private static void Complete(
        PaperSurfaceWindow window,
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
