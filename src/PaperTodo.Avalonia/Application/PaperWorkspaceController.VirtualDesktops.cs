using Avalonia.Controls;
using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;
using PaperTodo.Avalonia.Papers;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    private readonly object _virtualDesktopGate = new();
    private VirtualDesktopAdapter? _virtualDesktopAdapter;
    private DispatcherTimer? _virtualDesktopTimer;
    private Guid _lastObservedVirtualDesktopId;
    private bool _virtualDesktopRuntimeAttached;
    private bool _virtualDesktopAdapterUsable;

    internal void AttachVirtualDesktopRuntime()
    {
        if (_virtualDesktopRuntimeAttached)
        {
            return;
        }

        _virtualDesktopRuntimeAttached = true;
        _papers.SurfaceShown += OnVirtualDesktopPaperSurfaceShown;
        _edges.SurfaceShown += OnVirtualDesktopEdgeSurfaceShown;
        _papers.Disposing += DisposeVirtualDesktopRuntime;

        _virtualDesktopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _virtualDesktopTimer.Tick += OnVirtualDesktopTimerTick;
        _virtualDesktopTimer.Start();
    }

    private void OnVirtualDesktopPaperSurfaceShown(IPaperSurface surface)
    {
        var state = _state;
        if (state is null ||
            !VirtualDesktopIntegrationAvailable(state) ||
            (!state.ExperimentalVirtualDesktopMoveOnShow &&
             !state.ExperimentalVirtualDesktopMoveOnCapsuleActivation))
        {
            return;
        }

        TryMoveWindowToCurrentVirtualDesktop(surface.Window);
    }

    private void OnVirtualDesktopEdgeSurfaceShown(IEdgeCapsuleQueueSurface surface)
    {
        var state = _state;
        if (state is null ||
            !VirtualDesktopIntegrationAvailable(state) ||
            !state.ExperimentalVirtualDesktopMoveOnCapsuleActivation)
        {
            return;
        }

        TryMoveWindowToCurrentVirtualDesktop(surface.Window);
    }

    private void OnVirtualDesktopTimerTick(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _virtualDesktopTimer))
        {
            return;
        }

        var state = _state;
        if (state is null || !VirtualDesktopIntegrationAvailable(state))
        {
            ReleaseVirtualDesktopAdapter();
            _lastObservedVirtualDesktopId = Guid.Empty;
            return;
        }

        var adapter = EnsureVirtualDesktopAdapter();
        if (adapter is null ||
            !adapter.TryGetCurrentDesktopId(out var currentDesktopId) ||
            currentDesktopId == Guid.Empty)
        {
            return;
        }

        if (_lastObservedVirtualDesktopId == currentDesktopId)
        {
            return;
        }

        _lastObservedVirtualDesktopId = currentDesktopId;

        // Paper windows follow the existing WPF contract and move when they are explicitly shown.
        // The shared edge queue has no per-paper HWND anymore, so keep that single queue surface on
        // the active desktop; otherwise a user can switch desktops and lose all capsule affordances.
        if (!state.ExperimentalVirtualDesktopMoveOnCapsuleActivation)
        {
            return;
        }

        foreach (var surface in _edges.Surfaces.ToArray())
        {
            TryMoveWindowToVirtualDesktop(surface.Window, currentDesktopId);
        }
    }

    private static bool VirtualDesktopIntegrationAvailable(AppState state) =>
        // Avalonia has not yet implemented the WPF-only window-switcher hiding style, so a legacy
        // HidePapersFromWindowSwitcher=true value must not silently disable this otherwise usable
        // documented IVirtualDesktopManager path.
        state.ExperimentalVirtualDesktopIntegration;

    private VirtualDesktopAdapter? EnsureVirtualDesktopAdapter()
    {
        lock (_virtualDesktopGate)
        {
            if (_virtualDesktopAdapter is not null)
            {
                return _virtualDesktopAdapterUsable ? _virtualDesktopAdapter : null;
            }

            var adapter = new VirtualDesktopAdapter();
            var probe = adapter.Probe();
            _virtualDesktopAdapter = adapter;
            _virtualDesktopAdapterUsable = probe.IsUsable;
            return probe.IsUsable ? adapter : null;
        }
    }

    private bool TryMoveWindowToCurrentVirtualDesktop(Window window)
    {
        var adapter = EnsureVirtualDesktopAdapter();
        return adapter is not null &&
            adapter.TryGetCurrentDesktopId(out var desktopId) &&
            desktopId != Guid.Empty &&
            TryMoveWindowToVirtualDesktop(window, desktopId);
    }

    private bool TryMoveWindowToVirtualDesktop(Window window, Guid desktopId)
    {
        var adapter = EnsureVirtualDesktopAdapter();
        if (adapter is null)
        {
            return false;
        }

        var handle = window.TryGetPlatformHandle();
        if (handle is null ||
            handle.Handle == IntPtr.Zero ||
            !string.Equals(handle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (adapter.TryIsWindowOnCurrentDesktop(handle.Handle, out var alreadyCurrent) &&
            alreadyCurrent &&
            adapter.TryGetCurrentDesktopId(out var currentDesktopId) &&
            currentDesktopId == desktopId)
        {
            return true;
        }

        return adapter.TryMoveWindowToDesktop(handle.Handle, desktopId);
    }

    private void ReleaseVirtualDesktopAdapter()
    {
        lock (_virtualDesktopGate)
        {
            _virtualDesktopAdapter?.Dispose();
            _virtualDesktopAdapter = null;
            _virtualDesktopAdapterUsable = false;
        }
    }

    private void DisposeVirtualDesktopRuntime()
    {
        if (!_virtualDesktopRuntimeAttached)
        {
            return;
        }

        _virtualDesktopRuntimeAttached = false;
        _papers.SurfaceShown -= OnVirtualDesktopPaperSurfaceShown;
        _edges.SurfaceShown -= OnVirtualDesktopEdgeSurfaceShown;
        _papers.Disposing -= DisposeVirtualDesktopRuntime;

        if (_virtualDesktopTimer is not null)
        {
            _virtualDesktopTimer.Stop();
            _virtualDesktopTimer.Tick -= OnVirtualDesktopTimerTick;
            _virtualDesktopTimer = null;
        }

        _lastObservedVirtualDesktopId = Guid.Empty;
        ReleaseVirtualDesktopAdapter();
    }
}
