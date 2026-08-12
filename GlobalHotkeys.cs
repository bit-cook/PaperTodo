using System.Windows.Input;
using System.Windows.Interop;

namespace PaperTodo;

/// <summary>
/// WPF owns only the message-only HWND. Gesture policy and native registrations are shared by the
/// Core and Windows platform assemblies used by the Avalonia executable.
/// </summary>
internal sealed class GlobalHotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly WindowsGlobalHotkeyRegistrar _registrar;
    private bool _disposed;

    public GlobalHotkeyManager()
    {
        var parameters = new HwndSourceParameters("PaperTodo.GlobalHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080
        };
        _source = new HwndSource(parameters);
        _registrar = new WindowsGlobalHotkeyRegistrar(_source.Handle);
        _registrar.Invoked += OnInvoked;
        _source.AddHook(WindowHook);
    }

    public event Action<string>? Invoked;

    public IReadOnlyDictionary<string, string> ActiveBindings => _registrar.ActiveBindings;

    public bool TryApply(
        IReadOnlyDictionary<string, string> desiredBindings,
        IReadOnlyCollection<string> activeCommandIds,
        bool distinguishNumpadDigits,
        out string? failedCommandId,
        out GlobalShortcutRegistrationFailure failure) =>
        _registrar.TryApply(
            desiredBindings,
            activeCommandIds,
            distinguishNumpadDigits,
            out failedCommandId,
            out failure);

    private void OnInvoked(string commandId) => Invoked?.Invoke(commandId);

    private nint WindowHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_registrar.ProcessWindowMessage(msg, wParam))
        {
            handled = true;
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.RemoveHook(WindowHook);
        _registrar.Invoked -= OnInvoked;
        _registrar.Dispose();
        _source.Dispose();
    }
}

internal static class WpfShortcutGesture
{
    public static ShortcutGesture Create(Key key, ModifierKeys modifiers) =>
        new((ShortcutKey)(uint)KeyInterop.VirtualKeyFromKey(key), ConvertModifiers(modifiers));

    public static ShortcutKey ConvertKey(Key key) =>
        (ShortcutKey)(uint)KeyInterop.VirtualKeyFromKey(key);

    public static ShortcutModifiers ConvertModifiers(ModifierKeys modifiers)
    {
        var result = ShortcutModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= ShortcutModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= ShortcutModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= ShortcutModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= ShortcutModifiers.Windows;
        return result;
    }
}
