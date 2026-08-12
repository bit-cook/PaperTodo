namespace PaperTodo;

/// <summary>
/// Legacy WPF adapter used while the executable remains the migration behavior baseline. The
/// persistence and compatibility rules live in Core; only live Windows monitor and key facts stay
/// on this side of the boundary.
/// </summary>
internal sealed class WpfStateStorePlatform : IStateStorePlatform
{
    public string NormalizeMonitorDeviceName(string? deviceName) =>
        WindowWorkAreaHelper.NormalizeQueueMonitorDeviceName(deviceName);

    public Dictionary<string, string> NormalizeGlobalHotkeys(
        Dictionary<string, string>? source) =>
        GlobalShortcutCatalog.NormalizeBindings(source);

    public Dictionary<string, bool> NormalizeGlobalHotkeyEnabled(
        Dictionary<string, bool>? source) =>
        GlobalShortcutCatalog.NormalizeEnabled(source);

    public double NormalizeDeepCapsuleStartTopMargin(
        double value,
        string monitorDeviceName,
        double gapDip)
    {
        var area = EdgeCapsuleWpfWorkAreas.LocalWorkAreaForQueue(monitorDeviceName);
        return EdgeCapsuleLayout.NormalizeStartTopMargin(
            value,
            area,
            slotCount: 1,
            gap: gapDip);
    }
}
