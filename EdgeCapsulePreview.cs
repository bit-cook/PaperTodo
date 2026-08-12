using System.Windows;

namespace PaperTodo;

/// <summary>
/// Size is the complete visible card rectangle in DIPs, including the host-owned close segment.
/// The host normalizes it before CreateContent is invoked and freezes it for the preview session.
/// </summary>
internal sealed record EdgeCapsulePreviewDescriptor(
    EdgeCapsulePreviewSize Size,
    Func<EdgeCapsulePreviewSize, FrameworkElement> CreateContent,
    Action<bool>? SetVisibility = null,
    Action? PrepareForActivation = null,
    bool DeferContentCreation = false);

internal sealed record EdgeCapsulePreviewRequest(
    EdgeCapsulePreviewSize Size,
    FrameworkElement Content,
    Action<bool>? SetVisibility = null,
    Action? PrepareForActivation = null,
    Func<FrameworkElement>? CreateDeferredContent = null);



internal sealed record EdgeCapsulePreviewContext(
    PaperData Paper,
    Func<string> ReadTitle,
    bool PaperExpanded,
    Func<string> ReadMarkdownText,
    Func<string, bool, bool> SetTodoDone,
    Func<string, bool> OpenTodoLinkedTarget,
    Func<Style> ReadTodoCheckStyle,
    Func<string> ReadPluginStatus,
    Action<string> OpenExternal,
    EdgeCapsulePreviewInvalidationSource InvalidationSource)
{
    public string Title => ReadTitle();
}

/// <summary>
/// Internal content seam for edge preview cards. Built-in Todo/Markdown and protocol 1.8 plugin
/// adapters replace only the descriptor; queue, host, transition and input code remain shared.
/// </summary>
internal interface IEdgeCapsulePreviewProvider
{
    EdgeCapsulePreviewDescriptor Describe(EdgeCapsulePreviewContext context);
}

internal static class EdgeCapsulePreviewInteraction
{
    public static readonly DependencyProperty ConsumesPointerProperty =
        DependencyProperty.RegisterAttached(
            "ConsumesPointer",
            typeof(bool),
            typeof(EdgeCapsulePreviewInteraction),
            new FrameworkPropertyMetadata(false));

    public static void SetConsumesPointer(DependencyObject element, bool value) =>
        element.SetValue(ConsumesPointerProperty, value);

    public static bool GetConsumesPointer(DependencyObject element) =>
        (bool)element.GetValue(ConsumesPointerProperty);
}

internal sealed class DefaultEdgeCapsulePreviewProvider : IEdgeCapsulePreviewProvider
{
    public static DefaultEdgeCapsulePreviewProvider Instance { get; } = new();

    private DefaultEdgeCapsulePreviewProvider()
    {
    }

    public EdgeCapsulePreviewDescriptor Describe(EdgeCapsulePreviewContext context)
    {
        var title = context.Title;
        var status = context.ReadPluginStatus();
        var width = EdgeCapsulePreviewMeasure.MeasureWidth(
            title,
            status,
            minimum: EdgeCapsulePreviewSize.MinimumWidthDip,
            maximum: 440);
        var height = Math.Clamp(
            150 + EdgeCapsulePreviewMeasure.EstimateWrappedLines(
                status,
                Math.Max(72, width - 40)) * AppTypography.Scale(20),
            160,
            280);

        return new EdgeCapsulePreviewDescriptor(
            new EdgeCapsulePreviewSize(width, height),
            size => new PluginFallbackEdgeCapsulePreviewView(context, size));
    }
}
