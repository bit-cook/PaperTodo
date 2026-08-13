using Avalonia.Controls;
using Avalonia.Threading;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperSurfaceRegistry : IDisposable
{
    private readonly Dictionary<string, IPaperSurface> _surfaces = new(StringComparer.Ordinal);
    private bool _disposed;

    public IReadOnlyCollection<IPaperSurface> Surfaces => _surfaces.Values;

    public event Action<string>? LinkedPaperRequested;
    public event Action<IPaperSurface>? SurfaceShown;
    public event Action? Disposing;

    public PaperSurfaceWindow Create(PaperSurfaceDescriptor descriptor)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_surfaces.ContainsKey(descriptor.PaperId))
        {
            throw new InvalidOperationException($"Paper surface '{descriptor.PaperId}' already exists.");
        }

        var surface = new PaperSurfaceWindow(descriptor);
        surface.LinkedPaperRequested += paperId => LinkedPaperRequested?.Invoke(paperId);
        surface.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.IsVisibleProperty && surface.IsVisible)
            {
                SurfaceShown?.Invoke(surface);
            }
        };
        surface.Closed += (_, _) => _surfaces.Remove(descriptor.PaperId);
        _surfaces.Add(descriptor.PaperId, surface);
        if (descriptor.IsVisible)
        {
            surface.Show();
        }

        return surface;
    }

    public bool TryGet(string paperId, out IPaperSurface surface) =>
        _surfaces.TryGetValue(paperId, out surface!);

    public void ShowAll()
    {
        Dispatcher.UIThread.VerifyAccess();
        foreach (var surface in _surfaces.Values)
        {
            surface.Show();
        }
    }

    public void HideAll()
    {
        Dispatcher.UIThread.VerifyAccess();
        foreach (var surface in _surfaces.Values)
        {
            surface.Hide();
        }
    }

    public void ToggleVisibility()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_surfaces.Values.Any(surface => surface.IsVisible))
        {
            HideAll();
        }
        else
        {
            ShowAll();
        }
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
        LinkedPaperRequested = null;
        SurfaceShown = null;
        if (Dispatcher.UIThread.CheckAccess())
        {
            CloseAll();
        }
    }
}
