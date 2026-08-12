namespace PaperTodo;

/// <summary>
/// A rectangle in framework-neutral device-independent pixels.
/// </summary>
public readonly record struct DipRect(
    double Left,
    double Top,
    double Right,
    double Bottom)
{
    public static DipRect FromPositionAndSize(
        double left,
        double top,
        double width,
        double height) =>
        new(left, top, left + Math.Max(0, width), top + Math.Max(0, height));

    public double Width => Math.Max(0, Right - Left);
    public double Height => Math.Max(0, Bottom - Top);
    public bool IsEmpty => Width == 0 || Height == 0;
}

// UI frameworks expose physical screen pixels and desktop DIPs through visually similar point
// types. These wrappers make the coordinate-space crossing explicit without taking a framework
// dependency in Core.
public readonly record struct DeviceScreenPoint(double X, double Y)
;

public readonly record struct DeviceScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
    public bool IsEmpty => Width == 0 || Height == 0;

    public DeviceScreenRect WithVerticalEdges(int top, int bottom) =>
        new(Left, top, Right, bottom);
}

public readonly record struct GlobalScreenDipPoint(double X, double Y)
;

public readonly record struct MonitorGeometry(
    string DeviceName,
    DeviceScreenRect WorkArea,
    double DpiScaleX,
    double DpiScaleY)
{
    public DipRect LocalWorkAreaDip => DipRect.FromPositionAndSize(
        0,
        0,
        WorkArea.Width / Math.Max(1, DpiScaleX),
        WorkArea.Height / Math.Max(1, DpiScaleY));

    public double DeviceYToLocalDip(double deviceY) =>
        (deviceY - WorkArea.Top) / Math.Max(1, DpiScaleY);
}
