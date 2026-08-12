namespace PaperTodo;

/// <summary>
/// OS facts used while normalizing persisted state. Persistence owns the compatibility rules;
/// this boundary supplies only monitor identity, work-area clamping and shortcut syntax.
/// </summary>
public interface IStateStorePlatform
{
    string NormalizeMonitorDeviceName(string? deviceName);

    Dictionary<string, string> NormalizeGlobalHotkeys(Dictionary<string, string>? source);

    Dictionary<string, bool> NormalizeGlobalHotkeyEnabled(Dictionary<string, bool>? source);

    double NormalizeDeepCapsuleStartTopMargin(
        double value,
        string monitorDeviceName,
        double gapDip);
}

internal sealed class StateStorePlatformDefaults : IStateStorePlatform
{
    internal static StateStorePlatformDefaults Instance { get; } = new();

    private StateStorePlatformDefaults()
    {
    }

    public string NormalizeMonitorDeviceName(string? deviceName) =>
        (deviceName ?? "").Trim();

    public Dictionary<string, string> NormalizeGlobalHotkeys(
        Dictionary<string, string>? source) =>
        GlobalShortcutCatalog.NormalizeBindings(source);

    public Dictionary<string, bool> NormalizeGlobalHotkeyEnabled(
        Dictionary<string, bool>? source) =>
        GlobalShortcutCatalog.NormalizeEnabled(source);

    public double NormalizeDeepCapsuleStartTopMargin(
        double value,
        string monitorDeviceName,
        double gapDip) =>
        double.IsFinite(value) ? value : EdgeCapsuleLayout.StartTopMargin;
}
