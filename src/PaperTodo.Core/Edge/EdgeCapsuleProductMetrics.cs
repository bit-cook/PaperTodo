namespace PaperTodo;

/// <summary>
/// Stable product dimensions consumed by the framework-neutral edge-capsule planner.
/// UI frameworks may expose the same values through their own layout defaults, but edge policy
/// must not take a dependency on a WPF or Avalonia visual type.
/// </summary>
internal static class EdgeCapsuleProductMetrics
{
    public const double CompactHeightDip = 46;
    public const double MinimumFloatingWidthDip = 92;
}
