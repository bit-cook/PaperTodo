using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PaperTodo.Avalonia.Application;

namespace PaperTodo.Avalonia;

internal sealed partial class App : global::Avalonia.Application
{
    private static AvaloniaLaunchContext? s_launchContext;
    private ApplicationLifecycleController? _lifecycleController;

    internal static void InstallLaunchContext(AvaloniaLaunchContext launchContext)
    {
        ArgumentNullException.ThrowIfNull(launchContext);
        if (Interlocked.CompareExchange(ref s_launchContext, launchContext, null) is not null)
        {
            throw new InvalidOperationException("The Avalonia launch context is already installed.");
        }
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            throw new InvalidOperationException("PaperTodo requires a desktop application lifetime.");
        }

        desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
        var launch = s_launchContext ?? throw new InvalidOperationException(
            "PaperTodo was started without a launch context.");

        if (AotUiSmokeTest.IsRequested(launch.InitialArguments))
        {
            // This path intentionally initializes Avalonia and a real compositor-backed HWND,
            // but never reads data.json, creates the tray icon or starts product services.
            base.OnFrameworkInitializationCompleted();
            AotUiSmokeTest.Start(desktop);
            return;
        }

        _lifecycleController = ApplicationCompositionRoot.Create(desktop, launch);
        desktop.Exit += OnDesktopExit;

        base.OnFrameworkInitializationCompleted();
        _lifecycleController.Start();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        desktopCleanup();
        return;

        void desktopCleanup()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit -= OnDesktopExit;
            }

            _lifecycleController?.Dispose();
            _lifecycleController = null;
        }
    }
}
