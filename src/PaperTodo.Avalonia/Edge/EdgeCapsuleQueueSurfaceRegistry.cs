using Avalonia.Controls;
using Avalonia.Threading;

namespace PaperTodo.Avalonia.Edge;

internal sealed class EdgeCapsuleQueueSurfaceRegistry : IDisposable
{
    private readonly Dictionary<EdgeCapsuleQueueKey, EdgeCapsuleQueueSurface> _surfaces = new();
    private bool _disposed;

    public IReadOnlyCollection<IEdgeCapsuleQueueSurface> Surfaces => _surfaces.Values;

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
        created.Closed += (_, _) => _surfaces.Remove(queue);
        _surfaces.Add(queue, created);
        return created;
    }

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
        if (Dispatcher.UIThread.CheckAccess())
        {
            CloseAll();
        }
    }
}
