using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace PaperTodo;

internal readonly record struct VirtualDesktopProbeResult(
    bool ManagerAvailable,
    bool CurrentDesktopResolved,
    int HResult)
{
    public bool IsUsable =>
        ManagerAvailable &&
        CurrentDesktopResolved &&
        HResult >= 0;
}

// Uses only the documented Windows 10+ IVirtualDesktopManager surface. No internal shell
// interfaces or build-specific vtable layouts are involved.
internal sealed partial class VirtualDesktopAdapter : IDisposable
{
    private static readonly Guid VirtualDesktopManagerClassId =
        new("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    private static readonly Guid VirtualDesktopManagerInterfaceId =
        new("A5CD92FF-29BE-454C-8D04-D82879FB3F1B");

    private const uint ClassContextInProcessServer = 0x1;
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsPopup = unchecked((int)0x80000000);

    private static readonly VirtualDesktopComWrappers ComWrappers = new();

    private IVirtualDesktopManager? _manager;
    private bool _activationAttempted;
    private bool _disposed;

    public int LastHResult { get; private set; }

    public VirtualDesktopProbeResult Probe()
    {
        if (!TryGetManager(out _))
        {
            return new VirtualDesktopProbeResult(
                ManagerAvailable: false,
                CurrentDesktopResolved: false,
                LastHResult);
        }

        var resolved = TryGetCurrentDesktopId(out _);
        return new VirtualDesktopProbeResult(
            ManagerAvailable: true,
            CurrentDesktopResolved: resolved,
            LastHResult);
    }

    public bool TryIsWindowOnCurrentDesktop(
        nint window,
        out bool onCurrentDesktop)
    {
        onCurrentDesktop = false;
        if (window == 0 || !TryGetManager(out var manager))
        {
            LastHResult = window == 0 ? EInvalidArg : LastHResult;
            return false;
        }

        try
        {
            var result = manager.IsWindowOnCurrentVirtualDesktop(window, out var onCurrent);
            LastHResult = result;
            if (result < 0)
            {
                return false;
            }

            onCurrentDesktop = onCurrent != 0;
            return true;
        }
        catch (Exception ex)
        {
            LastHResult = Marshal.GetHRForException(ex);
            return false;
        }
    }

    public bool TryGetWindowDesktopId(nint window, out Guid desktopId)
    {
        desktopId = Guid.Empty;
        if (window == 0 || !TryGetManager(out var manager))
        {
            LastHResult = window == 0 ? EInvalidArg : LastHResult;
            return false;
        }

        try
        {
            var result = manager.GetWindowDesktopId(window, out desktopId);
            LastHResult = result;
            return result >= 0 && desktopId != Guid.Empty;
        }
        catch (Exception ex)
        {
            LastHResult = Marshal.GetHRForException(ex);
            desktopId = Guid.Empty;
            return false;
        }
    }

    public bool TryMoveWindowToDesktop(nint window, Guid desktopId)
    {
        if (window == 0 || desktopId == Guid.Empty || !TryGetManager(out var manager))
        {
            LastHResult = window == 0 || desktopId == Guid.Empty
                ? EInvalidArg
                : LastHResult;
            return false;
        }

        try
        {
            var result = manager.MoveWindowToDesktop(window, ref desktopId);
            LastHResult = result;
            return result >= 0;
        }
        catch (Exception ex)
        {
            LastHResult = Marshal.GetHRForException(ex);
            return false;
        }
    }

    public bool TryGetCurrentDesktopId(out Guid desktopId)
    {
        desktopId = Guid.Empty;
        if (!TryGetManager(out _))
        {
            return false;
        }

        var foreground = GetForegroundWindow();
        if (foreground != 0 &&
            TryIsWindowOnCurrentDesktop(foreground, out var foregroundIsCurrent) &&
            foregroundIsCurrent &&
            TryGetWindowDesktopId(foreground, out desktopId))
        {
            return true;
        }

        var referenceWindow = CreateWindowEx(
            WsExToolWindow | WsExNoActivate,
            "Static",
            "",
            WsPopup,
            -32000,
            -32000,
            1,
            1,
            0,
            0,
            0,
            0);
        if (referenceWindow == 0)
        {
            LastHResult = HResultFromWin32(Marshal.GetLastPInvokeError());
            return false;
        }

        try
        {
            return TryGetWindowDesktopId(referenceWindow, out desktopId);
        }
        finally
        {
            _ = DestroyWindow(referenceWindow);
        }
    }

    private unsafe bool TryGetManager(out IVirtualDesktopManager manager)
    {
        manager = null!;
        if (_disposed)
        {
            LastHResult = EFail;
            return false;
        }

        if (_manager != null)
        {
            manager = _manager;
            return true;
        }

        if (_activationAttempted)
        {
            return false;
        }

        _activationAttempted = true;
        nint interfacePointer = 0;
        try
        {
            var classId = VirtualDesktopManagerClassId;
            var interfaceId = VirtualDesktopManagerInterfaceId;
            var result = CoCreateInstance(
                in classId,
                0,
                ClassContextInProcessServer,
                in interfaceId,
                out interfacePointer);
            LastHResult = result;
            if (result < 0 || interfacePointer == 0)
            {
                return false;
            }

            var created = ComWrappers.GetOrCreateObjectForComInstance(
                interfacePointer,
                CreateObjectFlags.UniqueInstance);
            if (created is not IVirtualDesktopManager createdManager)
            {
                LastHResult = EFail;
                ComWrappers.Release(created);
                return false;
            }

            _manager = createdManager;
            manager = createdManager;
            LastHResult = 0;
            return true;
        }
        catch (Exception ex)
        {
            LastHResult = Marshal.GetHRForException(ex);
            return false;
        }
        finally
        {
            if (interfacePointer != 0)
            {
                ComInterfaceMarshaller<IVirtualDesktopManager>.Free((void*)interfacePointer);
            }
        }
    }

    private static int HResultFromWin32(int error) => error <= 0
        ? EFail
        : unchecked((int)(0x80070000u | ((uint)error & 0x0000FFFFu)));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var manager = _manager;
        _manager = null;
        if (manager != null)
        {
            try
            {
                ComWrappers.Release(manager);
            }
            catch
            {
                // COM teardown is best effort during feature disable and app exit.
            }
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint classContext,
        in Guid interfaceId,
        out nint instance);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport(
        "user32.dll",
        EntryPoint = "CreateWindowExW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint window);

    private sealed class VirtualDesktopComWrappers : StrategyBasedComWrappers
    {
        public void Release(object instance) => ReleaseObjects(new[] { instance });
    }
}

[GeneratedComInterface(Options = ComInterfaceOptions.ComObjectWrapper)]
[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
internal partial interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, out int onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

    [PreserveSig]
    int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
}
