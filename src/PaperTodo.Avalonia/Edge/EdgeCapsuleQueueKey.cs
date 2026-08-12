namespace PaperTodo.Avalonia.Edge;

internal readonly record struct EdgeCapsuleQueueKey(
    string MonitorDeviceName,
    EdgeCapsuleEdge Edge)
{
    public EdgeCapsuleQueueKey Normalize() => new(
        (MonitorDeviceName ?? string.Empty).Trim(),
        Edge);
}
