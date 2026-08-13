using Avalonia.Controls;
using Avalonia.Threading;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleQueueSurfaceRegistry : IDisposable
{
    private readonly Dictionary<EdgeCapsuleQueueKey, EdgeCapsuleQueueSurface> _surfaces = new();
    private bool _disposed;

    public IReadOnlyCollection<IEdgeCapsuleQueueSurface> Surfaces => _surfaces.Values;

    public event EventHandler<EdgeCapsuleReorderRequestedEventArgs>? ReorderRequested;
    public event Action<IEdgeCapsuleQueueSurface>? SurfaceShown;
    public event Action? Disposing;

    public EdgeCapsuleQueueSurface GetOrCreate(EdgeCapsuleQueueKey queue)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);

        queue = queue.Normalize();
        if (_surfaces.TryGetValue(queue, out var existing))
        {
            return existing;
        }

        var created = new EdgeCapsuleQueueSurface(queue);
        created.ReorderRequested += OnSurfaceReorderRequested;
        created.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.IsVisibleProperty && created.IsVisible)
            {
                SurfaceShown?.Invoke(created);
            }
        };
        created.Closed += (_, _) =>
        {
            created.ReorderRequested -= OnSurfaceReorderRequested;
            _surfaces.Remove(queue);
        };
        _surfaces.Add(queue, created);
        return created;
    }

    private void OnSurfaceReorderRequested(
        object? sender,
        EdgeCapsuleReorderRequestedEventArgs e) =>
        ReorderRequested?.Invoke(sender, e);

    public bool TryGet(EdgeCapsuleQueueKey queue, out IEdgeCapsuleQueueSurface surface)
    {
        if (_surfaces.TryGetValue(queue.Normalize(), out var found))
        {
            surface = found;
            return true;
        }

        surface = null!;
        return false;
    }

    public void CloseAll()
    {
        Dispatcher.UIThread.VerifyAccess();
        foreach (var surface in _surfaces.Values.ToArray())
        {
            surface.Close();
        }

        _surfaces.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Disposing?.Invoke();
        Disposing = null;
        ReorderRequested = null;
        SurfaceShown = null;
        if (Dispatcher.UIThread.CheckAccess())
        {
            CloseAll();
        }
    }
}
