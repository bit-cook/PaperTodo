using Avalonia.Controls;
using Avalonia.Threading;

namespace PaperTodo.Avalonia.Application;

/// <summary>
/// Owns the Windows registrations attached to PaperTodo's invisible infrastructure TopLevel.
/// Only commands already implemented by the Avalonia lifecycle are admitted; edge and labs
/// bindings remain unregistered until their corresponding runtime behavior is migrated.
/// </summary>
internal sealed class AvaloniaGlobalHotkeyController : IDisposable
{
    private static readonly IReadOnlyDictionary<string, StartupCommandKind> SupportedCommands =
        new Dictionary<string, StartupCommandKind>(StringComparer.Ordinal)
        {
            [GlobalShortcutCatalog.Show] = StartupCommandKind.Show,
            [GlobalShortcutCatalog.Hide] = StartupCommandKind.Hide,
            [GlobalShortcutCatalog.Toggle] = StartupCommandKind.Toggle,
            [GlobalShortcutCatalog.NewTodo] = StartupCommandKind.NewTodo,
            [GlobalShortcutCatalog.NewNote] = StartupCommandKind.NewNote,
            [GlobalShortcutCatalog.Exit] = StartupCommandKind.Exit
        };

    private readonly TopLevel _host;
    private readonly WindowsGlobalHotkeyRegistrar _registrar;
    private readonly Action<StartupCommand> _commandSink;
    private Win32Properties.CustomWndProcHookCallback? _wndProcHook;
    private bool _disposed;

    private AvaloniaGlobalHotkeyController(
        TopLevel host,
        AppState state,
        Action<StartupCommand> commandSink)
    {
        Dispatcher.UIThread.VerifyAccess();
        _host = host;
        _commandSink = commandSink;

        var platformHandle = host.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The Avalonia infrastructure TopLevel has no Windows handle.");
        }

        if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException(
                $"Global hotkeys require an HWND, not '{platformHandle.HandleDescriptor}'.");
        }

        _registrar = new WindowsGlobalHotkeyRegistrar(platformHandle.Handle);
        try
        {
            _registrar.Invoked += OnInvoked;
            ApplyState(state);
            _wndProcHook = WndProc;
            Win32Properties.AddWndProcHookCallback(_host, _wndProcHook);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public static AvaloniaGlobalHotkeyController? TryStart(
        TopLevel host,
        AppState state,
        Action<StartupCommand> commandSink)
    {
        try
        {
            return new AvaloniaGlobalHotkeyController(host, state, commandSink);
        }
        catch (Exception exception)
        {
            // Hotkey setup is auxiliary. Never turn a registration or HWND problem into a state
            // load failure, and never enter the save path with replacement/empty data.
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia global hotkey initialization failed: {0}",
                exception);
            return null;
        }
    }

    private void ApplyState(AppState state)
    {
        var activeCommandIds = SupportedCommands.Keys
            .Where(commandId => state.GlobalHotkeyEnabled.TryGetValue(commandId, out var enabled) && enabled)
            .ToArray();

        var unsupportedEnabled = GlobalShortcutCatalog.Definitions
            .Where(definition =>
                !SupportedCommands.ContainsKey(definition.Id) &&
                state.GlobalHotkeyEnabled.TryGetValue(definition.Id, out var enabled) &&
                enabled)
            .Select(definition => definition.Id)
            .ToArray();
        if (unsupportedEnabled.Length > 0)
        {
            System.Diagnostics.Trace.TraceWarning(
                "PaperTodo Avalonia left unsupported edge/labs hotkeys unregistered: {0}",
                string.Join(", ", unsupportedEnabled));
        }

        if (_registrar.TryApply(
                state.GlobalHotkeys,
                activeCommandIds,
                state.DistinguishNumpadShortcutDigits,
                out var failedCommandId,
                out var failure))
        {
            System.Diagnostics.Trace.TraceInformation(
                "PaperTodo Avalonia registered {0} global hotkey command(s).",
                _registrar.ActiveBindings.Count);
            return;
        }

        // TryApply deliberately retains prior registrations on failure. At first startup there
        // are no prior registrations, but it can have registered an earlier subset before the
        // failing binding. Dispose this registrar so startup is all-or-nothing and no hidden
        // partial shortcut set survives without a settings UI to explain it.
        throw new InvalidOperationException(
            $"Global hotkey '{failedCommandId ?? "<unknown>"}' could not be registered: {failure}.");
    }

    private void OnInvoked(string commandId)
    {
        if (_disposed || !SupportedCommands.TryGetValue(commandId, out var kind))
        {
            return;
        }

        // Win32 normally invokes this hook on Avalonia's UI thread. Always post explicitly so
        // command execution never re-enters lifecycle work from inside the native WndProc.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _commandSink(new StartupCommand(kind));
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    "PaperTodo Avalonia global hotkey command '{0}' failed: {1}",
                    commandId,
                    exception);
            }
        });
    }

    private IntPtr WndProc(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (!_disposed &&
            _registrar.ProcessWindowMessage(unchecked((int)message), wParam))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Dispatcher.UIThread.VerifyAccess();
        _disposed = true;

        // Release the OS registrations before removing the message hook or closing the HWND.
        _registrar.Invoked -= OnInvoked;
        _registrar.Dispose();
        if (_wndProcHook is not null)
        {
            Win32Properties.RemoveWndProcHookCallback(_host, _wndProcHook);
            _wndProcHook = null;
        }
    }
}
