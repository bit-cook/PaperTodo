using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using System.Diagnostics;

namespace PaperTodo.Avalonia.Edge;

/// <summary>
/// Framework host for one paper's chrome. Width and height reserve the maximum queue capacity
/// once; applied frames mutate only the render-thread composition visual.
/// </summary>
internal sealed class EdgeCapsuleNodeHost : IDisposable
{
    private readonly Border _root;
    private readonly EdgeCapsuleChrome? _chrome;
    private CompositionVisual? _visual;
    private EdgeCapsuleTransition? _transition;
    private bool _disposed;

    public EdgeCapsuleNodeHost(string paperId, Control chrome)
    {
        PaperId = paperId;
        _chrome = chrome as EdgeCapsuleChrome;
        _root = new Border
        {
            Width = EdgeCapsulePreviewSize.MaximumWidthDip,
            Height = EdgeCapsulePreviewSize.MaximumHeightDip,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = chrome,
            IsVisible = false
        };
        _root.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    public string PaperId { get; }

    public Control Root => _root;

    public EdgeCapsulePresentationFrame AppliedFrame { get; private set; } =
        EdgeCapsulePresentationFrame.Hidden;

    public bool Apply(
        EdgeCapsulePresentationFrame frame,
        DeviceScreenRect queueHostBounds,
        EdgeCapsuleMotion motion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!frame.IsUsable)
        {
            throw new ArgumentException("The edge capsule presentation frame is not usable.", nameof(frame));
        }

        if (!frame.Visible)
        {
            _transition = null;
            AppliedFrame = frame;
            _root.IsVisible = false;
            if (_visual is not null)
            {
                StopMotion(_visual);
            }
            return false;
        }

        if (!EdgeCapsuleMotionEnvelopePolicy.Contains(queueHostBounds, frame.Bounds))
        {
            throw new ArgumentException(
                "The frame must remain within its stable queue surface.",
                nameof(frame));
        }

        _root.IsVisible = true;
        var nowTimestamp = Stopwatch.GetTimestamp();
        var transitionWasActive = _transition.HasValue;
        if (_transition is EdgeCapsuleTransition activeTransition)
        {
            var activeSample = EdgeCapsuleTransitionPolicy.Sample(
                activeTransition,
                nowTimestamp);
            AppliedFrame = activeSample.Frame;
            transitionWasActive = !activeSample.IsComplete;
            if (activeSample.IsComplete)
            {
                _transition = null;
            }
        }

        var targetFrame = frame with { HostBounds = queueHostBounds };
        var target = ToTargetPresentation(targetFrame);
        var transition = EdgeCapsuleTransitionPolicy.Create(
            AppliedFrame,
            target,
            motion,
            transitionWasActive,
            nowTimestamp,
            Stopwatch.Frequency);
        var bodyWidthDip = frame.BodyWindowWidthDevice / Math.Max(1, frame.DpiScaleX);
        var closeWidthDip = Math.Max(
            0,
            frame.Bounds.Width / Math.Max(1, frame.DpiScaleX) - bodyWidthDip);
        _chrome?.ApplyShape(
            frame.Edge,
            bodyWidthDip,
            closeWidthDip,
            frame.ContentOpacity,
            frame.OutlineVisible);
        EnsureVisual();

        var scale = Math.Max(1, frame.DpiScaleX);
        var scaleY = Math.Max(1, frame.DpiScaleY);
        var targetOffset = new Vector3D(
            (frame.Bounds.Left - queueHostBounds.Left) / scale,
            (frame.Bounds.Top - queueHostBounds.Top) / scaleY,
            0);
        var targetSize = new Vector(
            frame.Bounds.Width / scale,
            frame.Bounds.Height / scaleY);

        if (_visual is null)
        {
            _transition = null;
            AppliedFrame = targetFrame;
            return false;
        }

        if (transition is EdgeCapsuleTransition animated)
        {
            var durationMilliseconds = Math.Max(
                1,
                (int)Math.Round(
                    animated.DurationTimestampTicks * 1000.0 / Stopwatch.Frequency));
            AnimateTo(_visual, targetOffset, targetSize, (float)frame.Opacity, durationMilliseconds);
            _transition = animated;
            return true;
        }

        _transition = null;
        AppliedFrame = EdgeCapsuleTransitionPolicy.ResolveSettledFrame(AppliedFrame, target);
        StopMotion(_visual);
        _visual.Offset = targetOffset;
        _visual.Size = targetSize;
        _visual.Opacity = (float)frame.Opacity;
        return false;
    }

    public bool AdvanceAnimation(long nowTimestamp)
    {
        if (_transition is not EdgeCapsuleTransition transition)
        {
            return false;
        }

        var sample = EdgeCapsuleTransitionPolicy.Sample(transition, nowTimestamp);
        AppliedFrame = sample.Frame;
        if (!sample.IsComplete)
        {
            return true;
        }

        _transition = null;
        return false;
    }

    public bool ContainsDevicePoint(PixelPoint point)
    {
        var bounds = AppliedFrame.InteractiveBounds;
        return AppliedFrame.Visible &&
            !bounds.IsEmpty &&
            point.X >= bounds.Left &&
            point.X < bounds.Right &&
            point.Y >= bounds.Top &&
            point.Y < bounds.Bottom;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        EnsureVisual();

    private void EnsureVisual()
    {
        _visual ??= ElementComposition.GetElementVisual(_root);
    }

    private static void AnimateTo(
        CompositionVisual visual,
        Vector3D offset,
        Vector size,
        float opacity,
        int durationMilliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(1, durationMilliseconds));
        var easing = new CubicEaseOut();
        var offsetAnimation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(1, offset, easing);

        var sizeAnimation = visual.Compositor.CreateVectorKeyFrameAnimation();
        sizeAnimation.Duration = duration;
        sizeAnimation.InsertKeyFrame(1, size, easing);

        var opacityAnimation = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = duration;
        opacityAnimation.InsertKeyFrame(1, opacity, easing);

        visual.StartAnimation("Offset", offsetAnimation);
        visual.StartAnimation("Size", sizeAnimation);
        visual.StartAnimation("Opacity", opacityAnimation);
    }

    private static void StopMotion(CompositionVisual visual)
    {
        visual.StopAnimation("Offset");
        visual.StopAnimation("Size");
        visual.StopAnimation("Opacity");
    }

    private static EdgeCapsuleTargetPresentation ToTargetPresentation(
        EdgeCapsulePresentationFrame frame) => new(
            frame.Visible,
            frame.Surface,
            frame.Bounds,
            frame.HostBounds,
            frame.InteractiveBounds,
            frame.Edge,
            frame.BodyWindowWidthDevice,
            frame.WallDeviceX,
            frame.DpiScaleX,
            frame.DpiScaleY,
            frame.MaximumCloseWidthDip,
            frame.Opacity,
            frame.ContentOpacity,
            frame.OutlineVisible,
            frame.IsHitTestVisible,
            frame.CloseSegmentActsAsContent,
            frame.UsesFixedMotionHost);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transition = null;
        _root.AttachedToVisualTree -= OnAttachedToVisualTree;
        if (_visual is not null)
        {
            StopMotion(_visual);
            _visual = null;
        }
    }
}
