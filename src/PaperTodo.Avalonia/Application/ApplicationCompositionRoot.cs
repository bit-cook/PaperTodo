using Avalonia.Controls.ApplicationLifetimes;
using PaperTodo.Avalonia.Edge;
using PaperTodo.Avalonia.Papers;
using PaperTodo.Avalonia.Tray;

namespace PaperTodo.Avalonia.Application;

internal static class ApplicationCompositionRoot
{
    public static ApplicationLifecycleController Create(
        IClassicDesktopStyleApplicationLifetime desktop,
        AvaloniaLaunchContext launch)
    {
        var paperSurfaces = new PaperSurfaceRegistry();
        var edgeSurfaces = new EdgeCapsuleQueueSurfaceRegistry();
        var stateStorePlatform = new AvaloniaStateStorePlatform();
        var stateStore = new StateStore(
            stateStorePlatform,
            AppContext.BaseDirectory,
            "PaperTodo could not load data.json or its recovery backup.");
        var workspace = new PaperWorkspaceController(
            stateStore,
            stateStorePlatform,
            paperSurfaces,
            edgeSurfaces);
        paperSurfaces.LinkedPaperRequested += workspace.ActivateLinkedPaperFromQuickLaunch;
        edgeSurfaces.ReorderRequested += workspace.ReorderEdgeCapsuleWithinQueue;
        workspace.AttachVirtualDesktopRuntime();

        return new ApplicationLifecycleController(
            desktop,
            launch,
            workspace,
            commandSink => new TrayIconController(commandSink, workspace.ShowSettings));
    }
}
