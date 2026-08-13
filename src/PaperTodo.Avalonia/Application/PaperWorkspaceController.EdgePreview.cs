using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;
using System.Diagnostics;

namespace PaperTodo.Avalonia.Application;

internal sealed partial class PaperWorkspaceController
{
    private const double EdgePreviewTransferStableMilliseconds = 50;
    private const double EdgePreviewPointerToleranceDip = 2;
    private const double EdgePreviewPollMilliseconds = 16;
    private const double EdgePreviewLayoutSuppressionMilliseconds =
        EdgeCapsuleLayout.SlotMoveMilliseconds + 50;

    private readonly EdgeCapsuleHoverIntentPredictor _edgePreviewIntentPredictor = new();
    private readonly LinkedList<string> _edgePreviewContentOwners = new();
    private DispatcherTimer? _edgePreviewTimer;
    private EdgeCapsulePreviewLayoutSession? _edgePreviewSession;
    private EdgePreviewActivationIntent? _edgePreviewActivationIntent;
    private EdgePreviewCorridorExitIntent? _edgePreviewCorridorExitIntent;
    private EdgePreviewLayoutSuppressionAnchor? _edgePreviewLayoutSuppressionAnchor;

    private readonly record struct EdgePreviewActivationIntent(
        string TargetPaperId,
        string ExpectedOwnerPaperId,
        DeviceScreenPoint StableAnchor,
        long CandidateSinceTimestamp,
        long StableSinceTimestamp);

    private readonly record struct EdgePreviewCorridorExitIntent(
        string OwnerPaperId,
        long CorridorSinceTimestamp,
        long? NoTargetIntentSinceTimestamp);

    private readonly record struct EdgePreviewLayoutSuppressionAnchor(
        DeviceScreenPoint Point,
        double DpiScaleX,
        double DpiScaleY,
        string QueueKey,
        long CreatedAtTimestamp);

    private readonly record struct EdgePreviewPointerResolution(
        string? TargetPaperId,
        bool OwnerContains,
        bool TransferRectangleContains);

    private void InitializeEdgeCapsulePreviewRuntime()
    {
        _edgePreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(EdgePreviewPollMilliseconds)
        };
        _edgePreviewTimer.Tick += OnEdgeCapsulePreviewTimerTick;
    }

    private void EnsureEdgeCapsulePreviewRuntimeState()
    {
        var timer = _edgePreviewTimer;
        if (timer is null)
        {
            return;
        }

        var state = _state;
        var shouldRun = _started &&
            !_disposed &&
            state is not null &&
            state.UseCapsuleMode &&
            state.UseDeepCapsuleMode &&
            state.ExperimentalEdgeCapsuleHoverPreview &&
            _edges.Surfaces.Any(surface => surface.Nodes.Count > 0);
        if (shouldRun)
        {
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void StopEdgeCapsulePreviewRuntime()
    {
        _edgePreviewTimer?.Stop();
        ResetEdgeCapsulePreviewState(clearContent: true);
    }

    private void ResetEdgeCapsulePreviewState(bool clearContent)
    {
        _edgePreviewSession = null;
        _edgePreviewActivationIntent = null;
        _edgePreviewCorridorExitIntent = null;
        _edgePreviewLayoutSuppressionAnchor = null;
        _edgePreviewIntentPredictor.Reset();
        if (clearContent)
        {
            ClearEdgeCapsulePreviewContentCache();
        }
    }

    private void OnEdgeCapsulePreviewTimerTick(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _edgePreviewTimer))
        {
            return;
        }

        var state = _state;
        if (!_started ||
            _disposed ||
            state is null ||
            !state.UseCapsuleMode ||
            !state.UseDeepCapsuleMode ||
            !state.ExperimentalEdgeCapsuleHoverPreview)
        {
            if (_edgePreviewSession is not null)
            {
                CloseEdgeCapsulePreview(animate: false);
            }
            EnsureEdgeCapsulePreviewRuntimeState();
            return;
        }

        if (!WindowsPointerPosition.TryGet(out var pointer))
        {
            return;
        }

        ClearEdgeCapsulePreviewLayoutSuppressionWhenPointerMoves(pointer);
        var session = _edgePreviewSession;
        if (session is null)
        {
            _edgePreviewActivationIntent = null;
            _edgePreviewCorridorExitIntent = null;
            if (!TryResolveInitialEdgePreviewTarget(pointer, out var targetPaperId) ||
                IsEdgeCapsulePreviewLayoutSuppressedFor(targetPaperId, pointer))
            {
                return;
            }

            ObserveEdgeCapsulePreviewPointer(targetPaperId, pointer);
            OpenOrTransferEdgeCapsulePreview(targetPaperId, pointer);
            return;
        }

        if (!TryGetEdgePreviewPaper(session.OwnerPaperId, out var owner) ||
            !CanEnterEdgeCapsulePreview(owner))
        {
            CloseEdgeCapsulePreview(animate: true);
            return;
        }

        ObserveEdgeCapsulePreviewPointer(session.OwnerPaperId, pointer);
        var resolution = ResolveEdgeCapsulePreviewPointer(session, pointer);
        if (resolution.OwnerContains)
        {
            _edgePreviewActivationIntent = null;
            _edgePreviewCorridorExitIntent = null;
            return;
        }

        if (resolution.TargetPaperId is { } targetPaperId)
        {
            _edgePreviewCorridorExitIntent = null;
            if (IsEdgeCapsulePreviewLayoutSuppressedFor(targetPaperId, pointer))
            {
                _edgePreviewActivationIntent = null;
                return;
            }

            AdvanceEdgeCapsulePreviewActivationIntent(
                session,
                targetPaperId,
                pointer);
            return;
        }

        _edgePreviewActivationIntent = null;
        var exitPolicy = EdgeCapsulePreviewExitPolicy.Resolve(
            resolution.TransferRectangleContains,
            state.ExperimentalEdgeCapsuleHoverIntent);
        if (exitPolicy == EdgeCapsulePreviewExitPolicyDecision.ImmediateClose)
        {
            _edgePreviewCorridorExitIntent = null;
            CloseEdgeCapsulePreview(animate: true);
            return;
        }

        AdvanceEdgeCapsulePreviewCorridorExitIntent(
            session,
            pointer,
            exitPolicy);
    }

    private bool TryResolveInitialEdgePreviewTarget(
        DeviceScreenPoint pointer,
        out string paperId)
    {
        var state = _state;
        if (state is not null)
        {
            foreach (var paper in state.Papers)
            {
                if (!CanEnterEdgeCapsulePreview(paper) ||
                    !TryGetEdgeCapsuleFrame(paper.Id, out var frame) ||
                    !Contains(frame.InteractiveBounds, pointer))
                {
                    continue;
                }

                paperId = paper.Id;
                return true;
            }
        }

        paperId = string.Empty;
        return false;
    }

    private EdgePreviewPointerResolution ResolveEdgeCapsulePreviewPointer(
        EdgeCapsulePreviewLayoutSession session,
        DeviceScreenPoint pointer)
    {
        string? targetPaperId = null;
        var ownerContains = false;
        var corridorNodes = new List<EdgeCapsulePreviewCorridorNode>(
            session.QueuePaperIds.Count);
        var previousNodeValid = false;

        foreach (var paperId in session.QueuePaperIds)
        {
            if (!TryGetEdgePreviewPaper(paperId, out var paper) ||
                !CanEnterEdgeCapsulePreview(paper) ||
                !TryGetEdgeCapsuleFrame(paperId, out var frame))
            {
                previousNodeValid = false;
                continue;
            }

            corridorNodes.Add(new EdgeCapsulePreviewCorridorNode(
                frame.InteractiveBounds,
                previousNodeValid));
            previousNodeValid = true;

            if (string.Equals(
                    paperId,
                    session.OwnerPaperId,
                    StringComparison.Ordinal))
            {
                ownerContains = Contains(frame.InteractiveBounds, pointer);
            }
            else if (targetPaperId is null &&
                Contains(frame.InteractiveBounds, pointer))
            {
                targetPaperId = paperId;
            }
        }

        if (targetPaperId is null && _state is { } state)
        {
            foreach (var paper in state.Papers)
            {
                if (string.Equals(
                        QueueStorageKey(paper),
                        session.QueueKey,
                        StringComparison.Ordinal) ||
                    !CanEnterEdgeCapsulePreview(paper) ||
                    !TryGetEdgeCapsuleFrame(paper.Id, out var frame) ||
                    !Contains(frame.InteractiveBounds, pointer))
                {
                    continue;
                }

                targetPaperId = paper.Id;
                break;
            }
        }

        var transferRectangleContains = corridorNodes.Count > 0 &&
            EdgeCapsulePreviewCorridor.Contains(
                corridorNodes.ToArray(),
                pointer);
        return new EdgePreviewPointerResolution(
            targetPaperId,
            ownerContains,
            transferRectangleContains);
    }

    private void AdvanceEdgeCapsulePreviewActivationIntent(
        EdgeCapsulePreviewLayoutSession session,
        string targetPaperId,
        DeviceScreenPoint pointer)
    {
        var state = _state;
        if (state is null ||
            !TryGetEdgeCapsuleFrame(targetPaperId, out var frame))
        {
            _edgePreviewActivationIntent = null;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var current = _edgePreviewActivationIntent;
        if (!current.HasValue ||
            !string.Equals(
                current.Value.TargetPaperId,
                targetPaperId,
                StringComparison.Ordinal) ||
            !string.Equals(
                current.Value.ExpectedOwnerPaperId,
                session.OwnerPaperId,
                StringComparison.Ordinal))
        {
            _edgePreviewActivationIntent = new EdgePreviewActivationIntent(
                targetPaperId,
                session.OwnerPaperId,
                pointer,
                now,
                now);
            return;
        }

        var intent = current.Value;
        if (EdgeCapsulePreviewPointerMovedBeyondTolerance(
                intent.StableAnchor,
                pointer,
                frame.DpiScaleX,
                frame.DpiScaleY))
        {
            intent = intent with
            {
                StableAnchor = pointer,
                StableSinceTimestamp = now
            };
            _edgePreviewActivationIntent = intent;
            return;
        }

        _edgePreviewActivationIntent = intent;
        var candidateElapsed = Stopwatch.GetElapsedTime(
            intent.CandidateSinceTimestamp,
            now).TotalMilliseconds;
        var stableElapsed = Stopwatch.GetElapsedTime(
            intent.StableSinceTimestamp,
            now).TotalMilliseconds;
        if (stableElapsed < EdgePreviewTransferStableMilliseconds)
        {
            return;
        }

        if (state.ExperimentalEdgeCapsuleHoverIntent)
        {
            var decision = _edgePreviewIntentPredictor.Evaluate(
                EdgeCapsuleHoverIntentMode.Transfer,
                state.ExperimentalEdgeCapsuleHoverIntentSensitivity,
                frame.Bounds,
                pointer,
                candidateElapsed,
                stableElapsed);
            if (decision != EdgeCapsuleHoverIntentDecision.NoExtraDelay)
            {
                return;
            }
        }

        _edgePreviewActivationIntent = null;
        OpenOrTransferEdgeCapsulePreview(targetPaperId, pointer);
    }

    private void AdvanceEdgeCapsulePreviewCorridorExitIntent(
        EdgeCapsulePreviewLayoutSession session,
        DeviceScreenPoint pointer,
        EdgeCapsulePreviewExitPolicyDecision exitPolicy)
    {
        var state = _state;
        if (state is null)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var current = _edgePreviewCorridorExitIntent;
        var intent = !current.HasValue ||
            !string.Equals(
                current.Value.OwnerPaperId,
                session.OwnerPaperId,
                StringComparison.Ordinal)
            ? new EdgePreviewCorridorExitIntent(
                session.OwnerPaperId,
                now,
                null)
            : current.Value;

        var corridorElapsed = Stopwatch.GetElapsedTime(
            intent.CorridorSinceTimestamp,
            now).TotalMilliseconds;
        if (exitPolicy == EdgeCapsulePreviewExitPolicyDecision.FixedWait)
        {
            _edgePreviewCorridorExitIntent = intent;
            if (corridorElapsed >= EdgeCapsulePreviewExitPolicy.FixedWaitMilliseconds)
            {
                _edgePreviewCorridorExitIntent = null;
                CloseEdgeCapsulePreview(animate: true);
            }
            return;
        }

        var keepAlive = new List<DeviceScreenRect>(session.QueuePaperIds.Count);
        foreach (var paperId in session.QueuePaperIds)
        {
            if (!TryGetEdgePreviewPaper(paperId, out var paper) ||
                !CanEnterEdgeCapsulePreview(paper) ||
                !TryGetEdgeCapsuleFrame(paperId, out var frame))
            {
                continue;
            }
            keepAlive.Add(frame.Bounds);
        }

        var noTargetElapsed = intent.NoTargetIntentSinceTimestamp.HasValue
            ? Stopwatch.GetElapsedTime(
                intent.NoTargetIntentSinceTimestamp.Value,
                now).TotalMilliseconds
            : 0;
        var decision = _edgePreviewIntentPredictor.EvaluateCorridorExit(
            state.ExperimentalEdgeCapsuleHoverIntentSensitivity,
            keepAlive.ToArray(),
            pointer,
            noTargetElapsed);
        switch (decision)
        {
            case EdgeCapsuleCorridorExitDecision.KeepAlive:
                intent = intent with { NoTargetIntentSinceTimestamp = null };
                _edgePreviewCorridorExitIntent = intent;
                break;
            case EdgeCapsuleCorridorExitDecision.ConfirmNoTargetIntent:
                intent = intent with
                {
                    NoTargetIntentSinceTimestamp =
                        intent.NoTargetIntentSinceTimestamp ?? now
                };
                _edgePreviewCorridorExitIntent = intent;
                break;
            case EdgeCapsuleCorridorExitDecision.CloseForNoTargetIntent:
                _edgePreviewCorridorExitIntent = null;
                CloseEdgeCapsulePreview(animate: true);
                break;
        }
    }

    private void OpenOrTransferEdgeCapsulePreview(
        string paperId,
        DeviceScreenPoint pointer)
    {
        var state = _state;
        if (state is null ||
            !TryGetEdgePreviewPaper(paperId, out var paper) ||
            !CanEnterEdgeCapsulePreview(paper) ||
            !TryGetEdgeCapsuleFrame(paperId, out var currentFrame) ||
            !Contains(currentFrame.InteractiveBounds, pointer))
        {
            return;
        }

        var basePlan = BuildEdgeCapsuleBasePlan();
        var queueKey = QueueStorageKey(paper);
        var parsedQueue = ParseQueueKey(queueKey);
        var screen = ResolveScreen(parsedQueue.MonitorDeviceName);
        if (screen is null)
        {
            return;
        }

        var monitor = ToMonitorGeometry(screen, parsedQueue.MonitorDeviceName);
        var size = EdgeCapsulePreviewContent.Measure(paper, monitor);
        var next = EdgeCapsulePreviewLayoutCoordinator.OpenOrTransfer(
            basePlan,
            _edgePreviewSession,
            queueKey,
            paperId,
            size,
            PaperLayoutDefaults.CapsuleHeight,
            DeepCapsuleGapSizes.Value(state.DeepCapsuleGapSize));
        if (next is null)
        {
            return;
        }

        _edgePreviewSession = next;
        _edgePreviewActivationIntent = null;
        _edgePreviewCorridorExitIntent = null;
        EnsureEdgeCapsulePreviewContent(paper);
        RecordEdgeCapsulePreviewLayoutSuppression(
            paperId,
            queueKey,
            pointer);
        ArrangeEdgeCapsules(
            animate: true,
            EdgeCapsuleTransitionReason.Preview);
    }

    private void CloseEdgeCapsulePreview(bool animate, bool arrange = true)
    {
        var session = _edgePreviewSession;
        if (session is null)
        {
            return;
        }

        DeviceScreenPoint? pointer = null;
        if (WindowsPointerPosition.TryGet(out var currentPointer))
        {
            pointer = currentPointer;
        }

        _edgePreviewSession = null;
        _edgePreviewActivationIntent = null;
        _edgePreviewCorridorExitIntent = null;
        _edgePreviewLayoutSuppressionAnchor = null;
        _edgePreviewIntentPredictor.Reset();
        if (pointer.HasValue)
        {
            RecordEdgeCapsulePreviewLayoutSuppression(
                session.OwnerPaperId,
                session.QueueKey,
                pointer.Value);
        }

        if (arrange)
        {
            ArrangeEdgeCapsules(
                animate,
                EdgeCapsuleTransitionReason.Preview);
        }
    }

    private EdgeCapsuleQueuePlan BuildEdgeCapsuleBasePlan()
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        var collapsed = state.Papers
            .Where(paper => paper.IsVisible && paper.IsCollapsed)
            .Select(paper => new EdgeCapsuleQueueMember(
                paper.Id,
                QueueStorageKey(paper)));
        return EdgeCapsuleQueueCoordinator.Build(
            collapsed,
            state.UseCapsuleCollapseAll);
    }

    private EdgeCapsuleQueuePlan ApplyEdgeCapsulePreviewLayout(
        EdgeCapsuleQueuePlan basePlan)
    {
        var session = _edgePreviewSession;
        var state = _state;
        if (session is null)
        {
            return basePlan;
        }
        if (state is null ||
            !state.ExperimentalEdgeCapsuleHoverPreview ||
            !TryGetEdgePreviewPaper(session.OwnerPaperId, out var owner) ||
            !CanEnterEdgeCapsulePreview(owner) ||
            !basePlan.Placements.ContainsKey(session.OwnerPaperId))
        {
            ResetEdgeCapsulePreviewState(clearContent: false);
            return basePlan;
        }

        var currentQueueKey = QueueStorageKey(owner);
        var currentQueue = basePlan.Queues.FirstOrDefault(queue =>
            string.Equals(queue.Key, currentQueueKey, StringComparison.Ordinal));
        if (currentQueue is null)
        {
            ResetEdgeCapsulePreviewState(clearContent: false);
            return basePlan;
        }

        var currentIds = currentQueue.Papers
            .Select(paper => paper.Id)
            .ToArray();
        if (!string.Equals(
                session.QueueKey,
                currentQueueKey,
                StringComparison.Ordinal) ||
            !session.QueuePaperIds.SequenceEqual(
                currentIds,
                StringComparer.Ordinal))
        {
            session = EdgeCapsulePreviewLayoutCoordinator.OpenOrTransfer(
                basePlan,
                null,
                currentQueueKey,
                owner.Id,
                session.Size,
                PaperLayoutDefaults.CapsuleHeight,
                DeepCapsuleGapSizes.Value(state.DeepCapsuleGapSize));
            if (session is null)
            {
                ResetEdgeCapsulePreviewState(clearContent: false);
                return basePlan;
            }
            _edgePreviewSession = session;
        }

        return EdgeCapsulePreviewLayoutCoordinator.Apply(basePlan, session);
    }

    private EdgeCapsuleModel ApplyEdgeCapsulePreviewState(
        PaperData paper,
        EdgeCapsuleModel model)
    {
        if (_edgePreviewSession is not { } session ||
            !string.Equals(
                session.OwnerPaperId,
                paper.Id,
                StringComparison.Ordinal))
        {
            return model;
        }

        var result = EdgeCapsuleReducer.Reduce(
            model,
            EdgeCapsuleIntent.PreviewChanged(open: true));
        return result.Accepted ? result.Model : model;
    }

    private EdgeCapsulePreviewSize ResolveEdgeCapsulePreviewSize(
        PaperData paper,
        MonitorGeometry monitor)
    {
        if (_edgePreviewSession is { } session &&
            string.Equals(
                session.OwnerPaperId,
                paper.Id,
                StringComparison.Ordinal))
        {
            return session.Size.Normalize(
                Math.Max(
                    EdgeCapsulePreviewSize.MinimumWidthDip,
                    monitor.LocalWorkAreaDip.Width - 16),
                Math.Max(
                    EdgeCapsulePreviewSize.MinimumHeightDip,
                    monitor.LocalWorkAreaDip.Height - 16));
        }

        return new EdgeCapsulePreviewSize(
            EdgeCapsulePreviewSize.MinimumWidthDip,
            EdgeCapsulePreviewSize.MinimumHeightDip);
    }

    private void PrepareEdgeCapsulePreviewNode(
        PaperData paper,
        EdgeCapsuleNodeHost node)
    {
        node.SetTitle(string.IsNullOrWhiteSpace(paper.Title) ? paper.Type : paper.Title);
        if (_edgePreviewSession is { } session &&
            string.Equals(
                session.OwnerPaperId,
                paper.Id,
                StringComparison.Ordinal))
        {
            EnsureEdgeCapsulePreviewContent(paper, node);
        }
    }

    private void EnsureEdgeCapsulePreviewContent(PaperData paper)
    {
        if (TryGetEdgeCapsuleNode(paper.Id, out var node))
        {
            EnsureEdgeCapsulePreviewContent(paper, node);
        }
    }

    private void EnsureEdgeCapsulePreviewContent(
        PaperData paper,
        EdgeCapsuleNodeHost node)
    {
        if (!node.HasPreviewContent)
        {
            var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
            node.SetPreviewContent(EdgeCapsulePreviewContent.Create(
                paper,
                state,
                () => OnEdgeCapsulePreviewContentChanged(paper),
                ActivateLinkedPaperFromEdgeCapsulePreview));
        }
        TouchEdgeCapsulePreviewContent(paper.Id);
    }

    private void OnEdgeCapsulePreviewContentChanged(PaperData paper)
    {
        QueueSaveCurrentState();
        RefreshReminderSchedule();
        Dispatcher.UIThread.Post(() => RefreshEdgeCapsulePreviewContent(paper));
    }

    private void RefreshEdgeCapsulePreviewContent(PaperData paper)
    {
        if (!_edgePreviewContentOwners.Contains(paper.Id) ||
            !TryGetEdgeCapsuleNode(paper.Id, out var node) ||
            _state is not { } state)
        {
            return;
        }

        node.SetTitle(string.IsNullOrWhiteSpace(paper.Title) ? paper.Type : paper.Title);
        node.SetPreviewContent(EdgeCapsulePreviewContent.Create(
            paper,
            state,
            () => OnEdgeCapsulePreviewContentChanged(paper),
            ActivateLinkedPaperFromEdgeCapsulePreview));
    }

    private void TouchEdgeCapsulePreviewContent(string paperId)
    {
        var existing = _edgePreviewContentOwners.Find(paperId);
        if (existing is not null)
        {
            _edgePreviewContentOwners.Remove(existing);
        }
        _edgePreviewContentOwners.AddLast(paperId);

        while (_edgePreviewContentOwners.Count > 2)
        {
            var oldest = _edgePreviewContentOwners.First;
            if (oldest is null)
            {
                break;
            }
            _edgePreviewContentOwners.RemoveFirst();
            if (TryGetEdgeCapsuleNode(oldest.Value, out var node))
            {
                node.SetPreviewContent(null);
            }
        }
    }

    private void ClearEdgeCapsulePreviewContentCache()
    {
        foreach (var surface in _edges.Surfaces)
        {
            foreach (var node in surface.Nodes)
            {
                node.SetPreviewContent(null);
            }
        }
        _edgePreviewContentOwners.Clear();
    }

    private void ActivateLinkedPaperFromEdgeCapsulePreview(string paperId)
    {
        var state = _state;
        if (state is null)
        {
            return;
        }

        var paper = state.Papers.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, paperId, StringComparison.Ordinal));
        if (paper is null)
        {
            return;
        }

        CloseEdgeCapsulePreview(animate: false, arrange: false);
        paper.IsVisible = true;
        paper.IsCollapsed = false;
        if (!_papers.TryGet(paper.Id, out var surface))
        {
            surface = CreatePaperSurface(paper);
        }
        surface.Show();
        surface.RefreshFromModel();
        surface.Window.Activate();
        ArrangeEdgeCapsules(
            animate: true,
            EdgeCapsuleTransitionReason.Preview);
        SaveCurrentState();
    }

    private bool TryGetEdgePreviewPaper(string paperId, out PaperData paper)
    {
        paper = _state?.Papers.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, paperId, StringComparison.Ordinal))!;
        return paper is not null;
    }

    private bool CanEnterEdgeCapsulePreview(PaperData paper)
    {
        var state = _state;
        return state is not null &&
            state.ExperimentalEdgeCapsuleHoverPreview &&
            state.UseCapsuleMode &&
            state.UseDeepCapsuleMode &&
            paper.IsVisible &&
            paper.IsCollapsed;
    }

    private bool TryGetEdgeCapsuleNode(
        string paperId,
        out EdgeCapsuleNodeHost node)
    {
        foreach (var surface in _edges.Surfaces)
        {
            if (surface.TryGetNode(paperId, out node))
            {
                return true;
            }
        }
        node = null!;
        return false;
    }

    private bool TryGetEdgeCapsuleFrame(
        string paperId,
        out EdgeCapsulePresentationFrame frame)
    {
        if (TryGetEdgeCapsuleNode(paperId, out var node) &&
            node.AppliedFrame.Visible &&
            !node.AppliedFrame.InteractiveBounds.IsEmpty)
        {
            frame = node.AppliedFrame;
            return true;
        }

        frame = EdgeCapsulePresentationFrame.Hidden;
        return false;
    }

    private void ObserveEdgeCapsulePreviewPointer(
        string referencePaperId,
        DeviceScreenPoint pointer)
    {
        var state = _state;
        if (state is null ||
            !state.ExperimentalEdgeCapsuleHoverIntent ||
            !TryGetEdgeCapsuleFrame(referencePaperId, out var frame))
        {
            return;
        }

        _edgePreviewIntentPredictor.Observe(
            pointer,
            Stopwatch.GetTimestamp(),
            frame.DpiScaleX,
            frame.DpiScaleY);
    }

    private void RecordEdgeCapsulePreviewLayoutSuppression(
        string paperId,
        string queueKey,
        DeviceScreenPoint pointer)
    {
        if (!TryGetEdgeCapsuleFrame(paperId, out var frame))
        {
            _edgePreviewLayoutSuppressionAnchor = null;
            _edgePreviewIntentPredictor.Reset();
            return;
        }

        var now = Stopwatch.GetTimestamp();
        _edgePreviewLayoutSuppressionAnchor = new EdgePreviewLayoutSuppressionAnchor(
            pointer,
            frame.DpiScaleX,
            frame.DpiScaleY,
            queueKey,
            now);
        _edgePreviewIntentPredictor.Reset(
            pointer,
            now,
            frame.DpiScaleX,
            frame.DpiScaleY);
    }

    private void ClearEdgeCapsulePreviewLayoutSuppressionWhenPointerMoves(
        DeviceScreenPoint pointer)
    {
        if (_edgePreviewLayoutSuppressionAnchor is not { } anchor)
        {
            return;
        }

        if (Stopwatch.GetElapsedTime(
                anchor.CreatedAtTimestamp,
                Stopwatch.GetTimestamp()).TotalMilliseconds >=
                EdgePreviewLayoutSuppressionMilliseconds ||
            EdgeCapsulePreviewPointerMovedBeyondTolerance(
                anchor.Point,
                pointer,
                anchor.DpiScaleX,
                anchor.DpiScaleY))
        {
            _edgePreviewLayoutSuppressionAnchor = null;
        }
    }

    private bool IsEdgeCapsulePreviewLayoutSuppressedFor(
        string paperId,
        DeviceScreenPoint pointer)
    {
        if (_edgePreviewLayoutSuppressionAnchor is not { } anchor ||
            !TryGetEdgePreviewPaper(paperId, out var paper))
        {
            return false;
        }

        if (Stopwatch.GetElapsedTime(
                anchor.CreatedAtTimestamp,
                Stopwatch.GetTimestamp()).TotalMilliseconds >=
                EdgePreviewLayoutSuppressionMilliseconds ||
            EdgeCapsulePreviewPointerMovedBeyondTolerance(
                anchor.Point,
                pointer,
                anchor.DpiScaleX,
                anchor.DpiScaleY))
        {
            _edgePreviewLayoutSuppressionAnchor = null;
            return false;
        }

        return string.Equals(
            anchor.QueueKey,
            QueueStorageKey(paper),
            StringComparison.Ordinal);
    }

    private static bool EdgeCapsulePreviewPointerMovedBeyondTolerance(
        DeviceScreenPoint anchor,
        DeviceScreenPoint pointer,
        double dpiScaleX,
        double dpiScaleY)
    {
        var deltaX = (pointer.X - anchor.X) /
            NormalizeEdgeCapsulePreviewDpiScale(dpiScaleX);
        var deltaY = (pointer.Y - anchor.Y) /
            NormalizeEdgeCapsulePreviewDpiScale(dpiScaleY);
        var tolerance = EdgePreviewPointerToleranceDip;
        return deltaX * deltaX + deltaY * deltaY >
            tolerance * tolerance;
    }

    private static double NormalizeEdgeCapsulePreviewDpiScale(double scale) =>
        double.IsFinite(scale) ? Math.Max(1, scale) : 1;

    private static bool Contains(
        DeviceScreenRect bounds,
        DeviceScreenPoint pointer) =>
        !bounds.IsEmpty &&
        pointer.X >= bounds.Left &&
        pointer.X < bounds.Right &&
        pointer.Y >= bounds.Top &&
        pointer.Y < bounds.Bottom;
}
