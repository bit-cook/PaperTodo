using System.Runtime.InteropServices;

namespace PaperTodo;

internal sealed partial class WindowsGlobalHotkeyRegistrar(nint windowHandle) : IGlobalHotkeyRegistrar
{
    public const int HotkeyWindowMessage = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly Dictionary<int, string> _commandByNativeId = new();
    private readonly Dictionary<ShortcutGesture, int> _nativeIdByGesture = new();
    private Dictionary<string, string> _activeBindings = new(StringComparer.Ordinal);
    private int _nextNativeId = 1;
    private bool _disposed;

    public event Action<string>? Invoked;

    public IReadOnlyDictionary<string, string> ActiveBindings => _activeBindings;

    public bool TryApply(
        IReadOnlyDictionary<string, string> desiredBindings,
        IReadOnlyCollection<string> activeCommandIds,
        bool distinguishNumpadDigits,
        out string? failedCommandId,
        out GlobalShortcutRegistrationFailure failure)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        failedCommandId = null;
        failure = GlobalShortcutRegistrationFailure.None;
        var activeIds = activeCommandIds.ToHashSet(StringComparer.Ordinal);
        var desiredCommands = new List<(string CommandId, string Text, ShortcutGesture Gesture)>();
        var desiredRegistrations = new List<(string CommandId, ShortcutGesture Gesture)>();
        var commandByGesture = new Dictionary<ShortcutGesture, string>();
        foreach (var pair in desiredBindings)
        {
            if (!activeIds.Contains(pair.Key) ||
                string.IsNullOrWhiteSpace(pair.Value) ||
                !ShortcutGesture.TryParse(pair.Value, out var gesture) ||
                gesture.Key == ShortcutKey.None)
            {
                continue;
            }

            var definition = GlobalShortcutCatalog.Find(pair.Key);
            var includeDigitAlias =
                !distinguishNumpadDigits &&
                definition?.IsEdgeCapsule != true &&
                gesture.IsDigitKey;
            foreach (var registrationGesture in gesture.RegistrationGestures(includeDigitAlias))
            {
                if (!commandByGesture.TryAdd(registrationGesture, pair.Key))
                {
                    failedCommandId = pair.Key;
                    failure = GlobalShortcutRegistrationFailure.RegistrationFailed;
                    return false;
                }

                desiredRegistrations.Add((pair.Key, registrationGesture));
            }

            desiredCommands.Add((pair.Key, pair.Value, gesture));
        }

        var newlyRegistered = new List<ShortcutGesture>();
        foreach (var binding in desiredRegistrations)
        {
            if (_nativeIdByGesture.ContainsKey(binding.Gesture))
            {
                continue;
            }

            if (!TryRegisterGesture(binding.Gesture, out var nativeId, out failure))
            {
                failedCommandId = binding.CommandId;
                foreach (var registeredGesture in newlyRegistered)
                {
                    TryUnregisterGesture(registeredGesture);
                }

                return false;
            }

            _nativeIdByGesture[binding.Gesture] = nativeId;
            _commandByNativeId[nativeId] = "";
            newlyRegistered.Add(binding.Gesture);
        }

        var activeByGesture = desiredRegistrations
            .ToDictionary(binding => binding.Gesture, binding => binding.CommandId);

        foreach (var pair in _nativeIdByGesture.ToArray())
        {
            if (activeByGesture.TryGetValue(pair.Key, out var commandId))
            {
                _commandByNativeId[pair.Value] = commandId;
                continue;
            }

            TryUnregisterGesture(pair.Key);
        }

        _activeBindings = desiredCommands
            .ToDictionary(binding => binding.CommandId, binding => binding.Text, StringComparer.Ordinal);
        return true;
    }

    public bool ProcessWindowMessage(int message, nint wParam)
    {
        if (message != HotkeyWindowMessage ||
            !_commandByNativeId.TryGetValue(unchecked((int)wParam), out var commandId))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(commandId))
        {
            Invoked?.Invoke(commandId);
        }

        return true;
    }

    private bool TryRegisterGesture(
        ShortcutGesture gesture,
        out int nativeId,
        out GlobalShortcutRegistrationFailure failure)
    {
        nativeId = _nextNativeId++;
        failure = GlobalShortcutRegistrationFailure.None;
        if (RegisterHotKey(
                windowHandle,
                nativeId,
                NativeModifiers(gesture.Modifiers) | ModNoRepeat,
                (uint)gesture.Key))
        {
            return true;
        }

        failure = Marshal.GetLastPInvokeError() == ErrorHotkeyAlreadyRegistered
            ? GlobalShortcutRegistrationFailure.SystemOccupied
            : GlobalShortcutRegistrationFailure.RegistrationFailed;
        return false;
    }

    private bool TryUnregisterGesture(ShortcutGesture gesture)
    {
        if (!_nativeIdByGesture.TryGetValue(gesture, out var nativeId))
        {
            return true;
        }

        _commandByNativeId[nativeId] = "";
        if (!UnregisterHotKey(windowHandle, nativeId))
        {
            return false;
        }

        _nativeIdByGesture.Remove(gesture);
        _commandByNativeId.Remove(nativeId);
        return true;
    }

    private static uint NativeModifiers(ShortcutModifiers modifiers)
    {
        var result = 0u;
        if (modifiers.HasFlag(ShortcutModifiers.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(ShortcutModifiers.Control)) result |= ModControl;
        if (modifiers.HasFlag(ShortcutModifiers.Shift)) result |= ModShift;
        if (modifiers.HasFlag(ShortcutModifiers.Windows)) result |= ModWin;
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var gesture in _nativeIdByGesture.Keys.ToArray())
        {
            TryUnregisterGesture(gesture);
        }

        _activeBindings.Clear();
        Invoked = null;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint windowHandle, int id);
}
