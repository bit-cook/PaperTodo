using System.Windows;

namespace PaperTodo;

/// <summary>
/// WPF-only conversions at the Core coordinate boundary. No WPF point or rectangle escapes into
/// the shared geometry/state-machine assembly.
/// </summary>
internal static class ScreenGeometryWpfExtensions
{
    public static DeviceScreenPoint ToDeviceScreenPoint(this Point point) =>
        new(point.X, point.Y);

    public static Point ToWpfPoint(this DeviceScreenPoint point) =>
        new(point.X, point.Y);

    public static Point ToWpfPoint(this GlobalScreenDipPoint point) =>
        new(point.X, point.Y);
}

internal static class EdgeCapsuleWpfWorkAreas
{
    // Work area of a specific monitor device (empty => primary), with nearest-monitor fallback.
    public static Rect WorkAreaForQueue(string? monitorDeviceName)
    {
        var normalizedMonitor =
            WindowWorkAreaHelper.NormalizeQueueMonitorDeviceName(monitorDeviceName);
        if (!string.IsNullOrEmpty(normalizedMonitor))
        {
            var resolved = WindowWorkAreaHelper.WorkAreaForDevice(normalizedMonitor);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        return SystemParameters.WorkArea;
    }

    // Edge hosts lay out in the target monitor's local 96-DPI coordinate space, then Core converts
    // the finished rectangle to physical pixels.
    public static DipRect LocalWorkAreaForQueue(string? monitorDeviceName)
    {
        if (WindowWorkAreaHelper.TryGetMonitorGeometryForDevice(
                monitorDeviceName,
                out var geometry))
        {
            return geometry.LocalWorkAreaDip;
        }

        var area = SystemParameters.WorkArea;
        return DipRect.FromPositionAndSize(0, 0, area.Width, area.Height);
    }
}
