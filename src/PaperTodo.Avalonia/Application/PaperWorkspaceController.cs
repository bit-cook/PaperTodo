using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using PaperTodo.Avalonia.Edge;
using PaperTodo.Avalonia.Papers;
using PaperTodo.Avalonia.Settings;
using PaperTodo.PluginHost;

namespace PaperTodo.Avalonia.Application;

internal sealed class PaperWorkspaceController : IApplicationWorkspace
{
    private readonly StateStore _stateStore;
    private readonly AvaloniaStateStorePlatform _stateStorePlatform;
    private readonly PaperSurfaceRegistry _papers;
    private readonly EdgeCapsuleQueueSurfaceRegistry _edges;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _reminderTimer;
    private readonly TodoReminderPolicyService _reminderPolicy = new();
    private AvaloniaGlobalHotkeyController? _globalHotkeys;
    private PluginCatalogSnapshot? _pluginCatalog;
    private Screens? _observedScreens;
    private SettingsWindow? _settingsWindow;
    private ReminderToastWindow? _reminderToast;
    private AppState? _state;
    private long _saveVersion;
    private bool _started;
    private bool _disposed;

    public event Action<StartupCommand>? CommandRequested;

    public PaperWorkspaceController(
        StateStore stateStore,
        AvaloniaStateStorePlatform stateStorePlatform,
        PaperSurfaceRegistry papers,
        EdgeCapsuleQueueSurfaceRegistry edges)
    {
        _stateStore = stateStore;
        _stateStorePlatform = stateStorePlatform;
        _papers = papers;
        _edges = edges;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveCurrentState();
        };

        _reminderTimer = new DispatcherTimer();
        _reminderTimer.Tick += OnReminderTimerTick;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _stateStorePlatform.InitializeScreensAsync(cancellationToken);
        _observedScreens = _stateStorePlatform.Screens;
        if (_observedScreens is not null)
        {
            _observedScreens.Changed += OnScreensChanged;
        }

        _state = _stateStore.Load();
        foreach (var paper in _state.Papers)
        {
            if (!paper.IsCollapsed)
            {
                CreatePaperSurface(paper);
            }
        }

        _pluginCatalog = new PluginPackageRegistry().Scan();
        ReportPluginCompatibility(_state, _pluginCatalog);
        ArrangeEdgeCapsules(animate: false);
        _started = true;
        cancellationToken.ThrowIfCancellationRequested();
        RestartGlobalHotkeys();
        RefreshReminderSchedule();
    }

    public async ValueTask SaveWithoutStartingAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started || _state is not null)
        {
            throw new InvalidOperationException(
                "The exit-only save path cannot run after the workspace has started.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _stateStorePlatform.InitializeScreensAsync(cancellationToken);
        var state = _stateStore.Load();
        var json = _stateStore.SerializeState(state);
        _stateStore.SaveJsonSync(json, Interlocked.Increment(ref _saveVersion));
    }

    private static void ReportPluginCompatibility(AppState state, PluginCatalogSnapshot catalog)
    {
        foreach (var paper in state.Papers.Where(item => item.Type == PaperTypes.Note))
        {
            var provider = catalog.ResolveProvider(paper.BodyProviderId);
            if (provider.Compatibility != PluginCompatibility.Compatible)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "PaperTodo Avalonia note '{0}' provider '{1}' is unavailable: {2}",
                    paper.Id,
                    provider.Id,
                    provider.IncompatibilityReason);
            }
        }

        foreach (var issue in catalog.Issues)
        {
            System.Diagnostics.Trace.TraceWarning(
                "PaperTodo Avalonia plugin manifest issue at '{0}': {1}",
                issue.SourcePath,
                issue.Message);
        }
    }

    private async void OnScreensChanged(object? sender, EventArgs e)
    {
        if (!_started || _disposed || !ReferenceEquals(sender, _observedScreens))
        {
            return;
        }

        try
        {
            var screens = _observedScreens;
            if (screens is null)
            {
                return;
            }

            await screens.RequestScreenDetails();
            if (_started && !_disposed && ReferenceEquals(screens, _observedScreens))
            {
                _edges.CloseAll();
                ArrangeEdgeCapsules(animate: false);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia screen refresh failed: {0}",
                exception);
        }
    }

    public ValueTask ExecuteAsync(StartupCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            throw new InvalidOperationException("The paper workspace has not started.");
        }

        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        if (state.Papers.Count == 0)
        {
            switch (command.Kind)
            {
                case StartupCommandKind.None:
                case StartupCommandKind.Show:
                    CreatePaper(PaperTypes.Todo, visible: true);
                    return ValueTask.CompletedTask;
                case StartupCommandKind.Hide:
                case StartupCommandKind.Toggle:
                    CreatePaper(PaperTypes.Todo, visible: false);
                    return ValueTask.CompletedTask;
            }
        }

        switch (command.Kind)
        {
            case StartupCommandKind.Show:
                SetAllPaperVisibility(visible: true);
                break;
            case StartupCommandKind.Hide:
                SetAllPaperVisibility(visible: false);
                break;
            case StartupCommandKind.Toggle:
                ToggleAllPaperVisibility();
                break;
            case StartupCommandKind.NewTodo:
                CreatePaper(PaperTypes.Todo, visible: true);
                break;
            case StartupCommandKind.NewNote:
                CreatePaper(PaperTypes.Note, visible: true);
                break;
        }

        return ValueTask.CompletedTask;
    }

    public void ShowSettings()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed || !_started || _state is null)
        {
            return;
        }

        if (_settingsWindow is not null)
        {
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        var settings = new SettingsWindow(_state, ApplySettings);
        _settingsWindow = settings;
        settings.Closed += (_, _) =>
        {
            if (ReferenceEquals(_settingsWindow, settings))
            {
                _settingsWindow = null;
            }
        };
        settings.Show();
        settings.Activate();
    }

    private void ApplySettings()
    {
        Dispatcher.UIThread.VerifyAccess();
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");

        RestartGlobalHotkeys();
        _papers.CloseAll();
        foreach (var paper in state.Papers)
        {
            if (!paper.IsCollapsed)
            {
                CreatePaperSurface(paper);
            }
        }

        _edges.CloseAll();
        ArrangeEdgeCapsules(animate: false);
        SaveCurrentState();
        RefreshReminderSchedule();
    }

    private void RestartGlobalHotkeys()
    {
        _globalHotkeys?.Dispose();
        _globalHotkeys = null;
        if (_state is null)
        {
            return;
        }

        _globalHotkeys = AvaloniaGlobalHotkeyController.TryStart(
            _stateStorePlatform.InfrastructureTopLevel,
            _state,
            command => CommandRequested?.Invoke(command));
    }

    private void SetAllPaperVisibility(bool visible)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        foreach (var paper in state.Papers)
        {
            paper.IsVisible = visible;
            if (paper.IsCollapsed)
            {
                continue;
            }

            if (!_papers.TryGet(paper.Id, out var surface))
            {
                surface = CreatePaperSurface(paper);
            }

            if (visible)
            {
                surface.Show();
            }
            else
            {
                surface.Hide();
            }
        }

        ArrangeEdgeCapsules(animate: true);
        QueueSaveCurrentState();
    }

    private void ToggleAllPaperVisibility()
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        SetAllPaperVisibility(!state.Papers.Any(paper => paper.IsVisible));
    }

    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        _globalHotkeys?.Dispose();
        _globalHotkeys = null;
        _reminderTimer.Stop();
        CloseReminderToast();
        CloseSettings();
        DetachScreens();
        _saveTimer.Stop();
        _papers.CloseAll();
        _edges.CloseAll();
        if (_state is not null)
        {
            SaveCurrentState();
        }

        _started = false;
        _pluginCatalog = null;
        return ValueTask.CompletedTask;
    }

    private void CreatePaper(string type, bool visible)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        var offset = state.Papers.Count * 22;
        var isTodo = type == PaperTypes.Todo;
        var paper = new PaperData
        {
            Type = isTodo ? PaperTypes.Todo : PaperTypes.Note,
            Title = isTodo ? "Todo" : "Note",
            X = 120 + offset,
            Y = 120 + offset,
            Width = isTodo ? PaperLayoutDefaults.TodoDefaultWidth : PaperLayoutDefaults.NoteDefaultWidth,
            Height = isTodo ? PaperLayoutDefaults.TodoDefaultHeight : PaperLayoutDefaults.NoteDefaultHeight,
            IsVisible = visible,
            CapsuleSide = state.DeepCapsuleSide,
            CapsuleMonitorDeviceName = state.DeepCapsuleMonitorDeviceName
        };
        state.Papers.Add(paper);
        CreatePaperSurface(paper);
        SaveCurrentState();
        RefreshReminderSchedule();
    }

    private PaperSurfaceWindow CreatePaperSurface(PaperData paper)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        var surface = _papers.Create(new PaperSurfaceDescriptor(
            paper,
            state,
            LegacyPaperGeometryAdapter.ToDevicePosition(paper.X, paper.Y),
            new Size(Math.Max(1, paper.Width), Math.Max(1, paper.Height)),
            paper.IsVisible && !paper.IsCollapsed,
            paper.AlwaysOnTop));

        surface.PositionChanged += (_, _) =>
        {
            var stored = LegacyPaperGeometryAdapter.ToStoredPosition(surface.Position);
            paper.X = stored.X;
            paper.Y = stored.Y;
            QueueSaveCurrentState();
        };
        surface.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.ClientSizeProperty)
            {
                paper.Width = surface.ClientSize.Width;
                paper.Height = surface.ClientSize.Height;
                QueueSaveCurrentState();
            }
        };
        surface.Changed += OnPaperChanged;
        surface.NewTodoRequested += () => CreatePaper(PaperTypes.Todo, visible: true);
        surface.NewNoteRequested += () => CreatePaper(PaperTypes.Note, visible: true);
        surface.CloseRequested += () =>
        {
            if (state.UseCapsuleMode && state.UseDeepCapsuleMode)
            {
                CollapsePaper(paper, surface);
            }
            else
            {
                HidePaper(paper, surface);
            }
        };
        surface.CollapseRequested += () => CollapsePaper(paper, surface);
        surface.DeleteRequested += () => DeletePaper(paper, surface);
        return surface;
    }

    private void OnPaperChanged()
    {
        QueueSaveCurrentState();
        RefreshReminderSchedule();
    }

    private void HidePaper(PaperData paper, IPaperSurface surface)
    {
        paper.IsVisible = false;
        surface.Hide();
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
    }

    private void CollapsePaper(PaperData paper, IPaperSurface surface)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        if (!state.UseCapsuleMode || !state.UseDeepCapsuleMode)
        {
            HidePaper(paper, surface);
            return;
        }

        paper.IsVisible = true;
        paper.IsCollapsed = true;
        surface.Close();
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
    }

    private void DeletePaper(PaperData paper, IPaperSurface surface)
    {
        var state = _state ?? throw new InvalidOperationException("The state is not loaded.");
        state.Papers.RemoveAll(candidate => candidate.Id == paper.Id);
        surface.Close();
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
        RefreshReminderSchedule();
    }

    private void ExpandCollapsedPaper(PaperData paper)
    {
        if (!paper.IsCollapsed)
        {
            return;
        }

        paper.IsCollapsed = false;
        paper.IsVisible = true;
        if (!_papers.TryGet(paper.Id, out var surface))
        {
            surface = CreatePaperSurface(paper);
        }

        surface.Show();
        surface.Window.Activate();
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
    }

    private void HideCollapsedPaper(PaperData paper)
    {
        paper.IsVisible = false;
        ArrangeEdgeCapsules(animate: true);
        SaveCurrentState();
    }

    private void ArrangeEdgeCapsules(bool animate)
    {
        var state = _state;
        if (state is null || !state.UseCapsuleMode || !state.UseDeepCapsuleMode)
        {
            _edges.CloseAll();
            return;
        }

        var collapsed = state.Papers
            .Where(paper => paper.IsVisible && paper.IsCollapsed)
            .ToArray();
        var members = collapsed.Select(paper =>
            new EdgeCapsuleQueueMember(paper.Id, QueueStorageKey(paper)));
        var plan = EdgeCapsuleQueueCoordinator.Build(members, state.UseCapsuleCollapseAll);
        var desiredQueues = plan.Queues.ToDictionary(
            queue => ParseQueueKey(queue.Key).Normalize(),
            queue => queue.Papers.Select(paper => paper.Id).ToHashSet(StringComparer.Ordinal));

        foreach (var existing in _edges.Surfaces.ToArray())
        {
            if (!desiredQueues.TryGetValue(existing.Key.Normalize(), out var desiredPaperIds))
            {
                existing.Close();
                continue;
            }

            foreach (var node in existing.Nodes.ToArray())
            {
                if (!desiredPaperIds.Contains(node.PaperId))
                {
                    existing.DetachPaper(node.PaperId);
                }
            }
        }

        foreach (var queue in plan.Queues)
        {
            var queueKey = ParseQueueKey(queue.Key);
            var screen = ResolveScreen(queueKey.MonitorDeviceName);
            if (screen is null)
            {
                continue;
            }

            var monitor = ToMonitorGeometry(screen, queueKey.MonitorDeviceName);
            var surface = _edges.GetOrCreate(queueKey);
            foreach (var queuePaper in queue.Papers)
            {
                var paper = collapsed.First(item => item.Id == queuePaper.Id);
                if (!surface.TryGetNode(paper.Id, out _))
                {
                    var chrome = new EdgeCapsuleChrome(state);
                    chrome.SetTitle(string.IsNullOrWhiteSpace(paper.Title) ? paper.Type : paper.Title);
                    chrome.BodyInvoked += () => ExpandCollapsedPaper(paper);
                    chrome.CloseInvoked += () => HideCollapsedPaper(paper);
                    surface.AttachPaper(paper.Id, chrome);
                }

                var placement = plan.Placements[paper.Id];
                var model = EdgeCapsuleReducer.Reduce(
                    EdgeCapsuleModel.Initial,
                    EdgeCapsuleIntent.Attach(
                        placement,
                        EdgeCapsulePaperForm.Collapsed,
                        retracted: false)).Model;
                var margin = state.DeepCapsuleQueueStartTopMargins.TryGetValue(
                    queue.Key,
                    out var stored)
                    ? stored
                    : state.DeepCapsuleStartTopMargin;
                var maximumPreviewHeight = Math.Min(
                    EdgeCapsulePreviewSize.MaximumHeightDip,
                    monitor.LocalWorkAreaDip.Height);
                var layout = EdgeCapsuleLayoutService.Calculate(new EdgeCapsuleLayoutFacts(
                    monitor,
                    queueKey.Edge,
                    placement,
                    margin,
                    DeepCapsuleGapSizes.Value(state.DeepCapsuleGapSize),
                    PaperLayoutDefaults.CapsuleWidth,
                    MaximumCloseWidthDip: 21,
                    HostWidthDip: Math.Min(
                        EdgeCapsulePreviewSize.MaximumWidthDip,
                        monitor.LocalWorkAreaDip.Width),
                    HostHeightDip: Math.Max(
                        PaperLayoutDefaults.CapsuleHeight,
                        maximumPreviewHeight),
                    HeightDip: PaperLayoutDefaults.CapsuleHeight,
                    PreviewWidthDip: EdgeCapsulePreviewSize.MinimumWidthDip,
                    PreviewHeightDip: EdgeCapsulePreviewSize.MinimumHeightDip,
                    MaximumPreviewHeightDip: maximumPreviewHeight,
                    UsesFixedMotionHost: true,
                    CloseSegmentActsAsContent: false,
                    RestingContentOpacity: 1,
                    ForcedContentOpacity: null));
                var target = EdgeCapsuleTargetPlanner.Calculate(model, layout).Docked;
                var motion = animate
                    ? EdgeCapsuleMotion.Animate(EdgeCapsuleTransitionReason.Placement)
                    : EdgeCapsuleMotion.Snap(EdgeCapsuleTransitionReason.Placement);
                surface.Apply(paper.Id, target.ToFrame(), motion);
            }
        }
    }

    private void OnReminderTimerTick(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _reminderTimer))
        {
            return;
        }

        _reminderTimer.Stop();
        ProcessDueReminders();
    }

    private void RefreshReminderSchedule()
    {
        _reminderTimer.Stop();
        var state = _state;
        if (!_started || _disposed || state is null)
        {
            return;
        }

        var evaluation = _reminderPolicy.Evaluate(state);
        if (!evaluation.IsActive || evaluation.NextPoll is not { } nextPoll)
        {
            return;
        }

        _reminderTimer.Interval = nextPoll.Delay;
        _reminderTimer.Start();
    }

    private void ProcessDueReminders()
    {
        var state = _state;
        if (!_started || _disposed || state is null)
        {
            return;
        }

        var evaluation = _reminderPolicy.Evaluate(state);
        if (evaluation.DueBatch.Count == 0)
        {
            RefreshReminderSchedule();
            return;
        }

        var first = evaluation.DueBatch.Reminders[0];
        var targetSucceeded = TryRevealReminderTarget(first);
        var notificationSucceeded = TryShowReminderToast(evaluation.DueBatch);
        var transaction = _reminderPolicy.PlanTriggerTransaction(
            evaluation.DueBatch,
            new TodoReminderSurfaceOutcome(targetSucceeded, notificationSucceeded));

        if (transaction.Disposition == TodoReminderTriggerDisposition.Retry)
        {
            _reminderTimer.Interval = transaction.RetryAfter ?? TodoReminderPolicyService.DeliveryRetryInterval;
            _reminderTimer.Start();
            return;
        }

        if (TodoReminderPolicyService.ApplyTriggerTransaction(transaction))
        {
            foreach (var group in evaluation.DueBatch.Reminders.GroupBy(
                         candidate => candidate.PaperId,
                         StringComparer.Ordinal))
            {
                if (_papers.TryGet(group.Key, out var surface))
                {
                    surface.RefreshFromModel();
                }
            }

            if (transaction.RequiresImmediateSave)
            {
                SaveCurrentState();
            }
        }

        RefreshReminderSchedule();
    }

    private bool TryRevealReminderTarget(TodoReminderCandidate candidate)
    {
        try
        {
            var paper = candidate.Paper;
            paper.IsVisible = true;
            if (paper.IsCollapsed)
            {
                paper.IsCollapsed = false;
            }

            if (!_papers.TryGet(paper.Id, out var surface))
            {
                surface = CreatePaperSurface(paper);
            }

            surface.Show();
            surface.RefreshFromModel();
            surface.Window.Activate();
            ArrangeEdgeCapsules(animate: true);
            return true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia failed to reveal reminder target: {0}",
                exception);
            return false;
        }
    }

    private bool TryShowReminderToast(TodoReminderDueBatch batch)
    {
        try
        {
            var state = _state;
            if (state is null || batch.Count == 0)
            {
                return false;
            }

            CloseReminderToast();
            var first = batch.Reminders[0];
            var title = string.IsNullOrWhiteSpace(first.Paper.Title)
                ? "Todo"
                : first.Paper.Title;
            var message = TodoReminderPolicyService.CompactText(
                first.Item.Text,
                "Todo");
            var toast = new ReminderToastWindow(
                state,
                title,
                message,
                batch.Count,
                () => TryRevealReminderTarget(first));
            _reminderToast = toast;
            toast.Closed += (_, _) =>
            {
                if (ReferenceEquals(_reminderToast, toast))
                {
                    _reminderToast = null;
                }
            };
            toast.Show();
            return true;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                "PaperTodo Avalonia failed to show reminder toast: {0}",
                exception);
            return false;
        }
    }

    private void CloseReminderToast()
    {
        if (_reminderToast is null)
        {
            return;
        }

        var toast = _reminderToast;
        _reminderToast = null;
        toast.Close();
    }

    private void QueueSaveCurrentState()
    {
        if (_state is null || _disposed)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveCurrentState()
    {
        _saveTimer.Stop();
        if (_state is null)
        {
            return;
        }

        var json = _stateStore.SerializeState(_state);
        _stateStore.SaveJsonSync(json, Interlocked.Increment(ref _saveVersion));
    }

    private static string QueueStorageKey(PaperData paper)
    {
        var side = DeepCapsuleSides.Normalize(paper.CapsuleSide);
        return $"{paper.CapsuleMonitorDeviceName}|{side}";
    }

    private static EdgeCapsuleQueueKey ParseQueueKey(string value)
    {
        var separator = value.LastIndexOf('|');
        var monitor = separator >= 0 ? value[..separator] : string.Empty;
        var side = separator >= 0 ? value[(separator + 1)..] : value;
        return new EdgeCapsuleQueueKey(
            monitor,
            side == DeepCapsuleSides.Left ? EdgeCapsuleEdge.Left : EdgeCapsuleEdge.Right);
    }

    private Screen? ResolveScreen(string monitorDeviceName)
    {
        var screens = _stateStorePlatform.Screens;
        if (screens is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(monitorDeviceName)
            ? screens.Primary
            : screens.All.FirstOrDefault(screen => string.Equals(
                screen.DisplayName,
                monitorDeviceName,
                StringComparison.Ordinal)) ?? screens.Primary;
    }

    private static MonitorGeometry ToMonitorGeometry(Screen screen, string monitorDeviceName) =>
        new(
            string.IsNullOrWhiteSpace(monitorDeviceName)
                ? screen.DisplayName ?? string.Empty
                : monitorDeviceName,
            new DeviceScreenRect(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Right,
                screen.WorkingArea.Bottom),
            Math.Max(1, screen.Scaling),
            Math.Max(1, screen.Scaling));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _saveTimer.Stop();
        _reminderTimer.Stop();
        _globalHotkeys?.Dispose();
        _globalHotkeys = null;
        CloseReminderToast();
        CloseSettings();
        _pluginCatalog = null;
        DetachScreens();
        CommandRequested = null;
        _papers.Dispose();
        _edges.Dispose();
        _stateStorePlatform.Dispose();
    }

    private void CloseSettings()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        var settings = _settingsWindow;
        _settingsWindow = null;
        settings.Close();
    }

    private void DetachScreens()
    {
        if (_observedScreens is not null)
        {
            _observedScreens.Changed -= OnScreensChanged;
            _observedScreens = null;
        }
    }
}
