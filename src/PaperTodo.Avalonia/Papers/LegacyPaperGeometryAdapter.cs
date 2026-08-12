using System.Runtime.InteropServices;
using Avalonia;

namespace PaperTodo.Avalonia.Papers;

/// <summary>
/// data.json stores ordinary paper coordinates in the WPF system-DPI desktop space. The Avalonia
/// window is PerMonitorV2 and uses physical PixelPoint positions, so this conversion must use the
/// system scale and must never reuse the window's current per-monitor render scale.
/// </summary>
internal static partial class LegacyPaperGeometryAdapter
{
    public static PixelPoint ToDevicePosition(double xDip, double yDip)
    {
        var scale = SystemDpiScale();
        return new PixelPoint(
            (int)Math.Round(xDip * scale),
            (int)Math.Round(yDip * scale));
    }

    public static Point ToStoredPosition(PixelPoint point)
    {
        var scale = SystemDpiScale();
        return new Point(point.X / scale, point.Y / scale);
    }

    private static double SystemDpiScale()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        try
        {
            return Math.Max(1, GetDpiForSystem() / 96.0);
        }
        catch (EntryPointNotFoundException)
        {
            return 1;
        }
        catch (DllNotFoundException)
        {
            return 1;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();
}
