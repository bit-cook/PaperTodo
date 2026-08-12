using Avalonia;
using PaperTodo.Avalonia.Application;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia;

internal static class Program
{
    private const string SingleInstanceMutexName = "PaperTodo-SingleInstance-Mutex";
    private const string SingleInstancePipeName = "PaperTodo-SingleInstance-Activate";

    [STAThread]
    public static int Main(string[] args)
    {
        if (PaperTextCodecSafetyCheck.IsRequested(args))
        {
            return PaperTextCodecSafetyCheck.Run();
        }

        if (AotLmdbSmokeTest.IsRequested(args))
        {
            return AotLmdbSmokeTest.Run();
        }

        var startupCommand = StartupCommand.Parse(args);
        StartupCulture.Apply(startupCommand.DefaultLanguage);

        using var singleInstance = new SingleInstanceHelper(
            SingleInstanceMutexName,
            SingleInstancePipeName);
        if (!singleInstance.TryAcquire())
        {
            singleInstance.SignalPrimaryInstance(args);
            return 0;
        }

        var launch = new AvaloniaLaunchContext(args);
        App.InstallLaunchContext(launch);
        singleInstance.StartListener(launch.EnqueueForwardedArguments);

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            launch.StopAcceptingCommands();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
