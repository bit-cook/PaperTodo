using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PaperTodo.Avalonia.Tray;

namespace PaperTodo.Avalonia.Application;

internal sealed class ApplicationLifecycleController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly AvaloniaLaunchContext _launch;
    private readonly IApplicationWorkspace _workspace;
    private readonly TrayIconController _tray;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _started;
    private bool _stopping;
    private bool _disposed;

    public ApplicationLifecycleController(
        IClassicDesktopStyleApplicationLifetime desktop,
        AvaloniaLaunchContext launch,
        IApplicationWorkspace workspace,
        Func<Func<StartupCommand, ValueTask>, TrayIconController> trayFactory)
    {
        _desktop = desktop;
        _launch = launch;
        _workspace = workspace;
        _tray = trayFactory(ExecuteAsync);
        _workspace.CommandRequested += OnWorkspaceCommandRequested;
    }

    public void Start()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_started)
        {
            return;
        }

        _started = true;
        _ = StartCoreAsync();
    }

    public ValueTask ExecuteAsync(StartupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _ = ExecuteCoreAsync(command));
            return ValueTask.CompletedTask;
        }

        return new ValueTask(ExecuteCoreAsync(command));
    }

    private async Task StartCoreAsync()
    {
        try
        {
            var startup = StartupCommand.Parse(_launch.InitialArguments);
            if (startup.Kind == StartupCommandKind.Exit)
            {
                // Match the existing no-primary exit contract: acquiring the mutex only to
                // service `exit` must save real state without creating the tray, materializing
                // persisted windows or creating a default Todo.
                await _workspace.SaveWithoutStartingAsync(_shutdown.Token);
                await StopCoreAsync(exitCode: 0);
                return;
            }

            _tray.Show();
            await _workspace.StartAsync(_shutdown.Token);
            await ExecuteCoreAsync(startup);
            if (!_stopping)
            {
                // Commands received while state and monitor data were loading stayed queued in
                // the launch context. Only expose the workspace after the initial command has
                // completed, matching the WPF single-instance startup transaction.
                _launch.AttachReceiver(OnForwardedArguments);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia startup failed: {0}",
                exception);
            await StopCoreAsync(exitCode: 1);
        }
    }

    private void OnForwardedArguments(IReadOnlyList<string> arguments)
    {
        var command = StartupCommand.Parse(arguments, StartupCommandKind.Show);
        Dispatcher.UIThread.Post(() => _ = ExecuteCoreAsync(command));
    }

    private void OnWorkspaceCommandRequested(StartupCommand command) =>
        _ = ExecuteAsync(command);

    private async Task ExecuteCoreAsync(StartupCommand command)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_stopping)
        {
            return;
        }

        if (command.Kind == StartupCommandKind.Exit)
        {
            await StopCoreAsync(exitCode: 0);
            return;
        }

        await _workspace.ExecuteAsync(command, _shutdown.Token);
    }

    private async Task StopCoreAsync(int exitCode)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        _launch.DetachReceiver(OnForwardedArguments);
        _shutdown.Cancel();

        try
        {
            await _workspace.StopAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia shutdown failed: {0}",
                exception);
            exitCode = 1;
        }

        _tray.Hide();
        _desktop.Shutdown(exitCode);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _launch.DetachReceiver(OnForwardedArguments);
        _workspace.CommandRequested -= OnWorkspaceCommandRequested;
        _shutdown.Cancel();
        _tray.Dispose();
        _workspace.Dispose();
        _shutdown.Dispose();
    }
}
