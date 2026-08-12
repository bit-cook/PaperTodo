namespace PaperTodo.Avalonia.Application;

internal interface IApplicationWorkspace : IDisposable
{
    event Action<StartupCommand>? CommandRequested;

    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask SaveWithoutStartingAsync(CancellationToken cancellationToken);

    ValueTask ExecuteAsync(StartupCommand command, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
