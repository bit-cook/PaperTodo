using System.Runtime.InteropServices;

namespace PaperTodo;

/// <summary>
/// AOT-safe physical cursor sampler shared by the Avalonia edge-preview coordinator. The returned
/// point is always in Windows desktop device pixels, matching EdgeCapsulePresentationFrame bounds.
/// </summary>
public static partial class WindowsPointerPosition
{
    public static bool TryGet(out DeviceScreenPoint point)
    {
        if (GetCursorPos(out var native))
        {
            point = new DeviceScreenPoint(native.X, native.Y);
            return true;
        }

        point = default;
        return false;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
