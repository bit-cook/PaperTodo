namespace PaperTodo;

internal enum GlobalShortcutGroup
{
    General,
    Labs,
    EdgeLeft,
    EdgeRight
}

internal enum ExperimentalShortcutKind
{
    None,
    CurrentPaperPassive,
    AllSurfacesPassive,
    LockAllPapers,
    AllPapersTransparent,
    AllCapsulesTransparent,
    CurrentPaperTransparent
}

internal sealed record GlobalShortcutDefinition(
    string Id,
    string LabelKey,
    string DefaultGesture,
    GlobalShortcutGroup Group,
    StartupCommandKind StartupCommandKind = StartupCommandKind.None,
    string PreferredCapsuleSide = "",
    int EdgeOrdinal = 0,
    bool DefaultEnabled = false,
    ExperimentalShortcutKind ExperimentalKind = ExperimentalShortcutKind.None)
{
    public bool IsEdgeCapsule =>
        EdgeOrdinal is >= 1 and <= 9 &&
        PreferredCapsuleSide is DeepCapsuleSides.Left or DeepCapsuleSides.Right;

    public bool IsExecutable =>
        StartupCommandKind != StartupCommandKind.None ||
        IsEdgeCapsule ||
        ExperimentalKind != ExperimentalShortcutKind.None;
}

internal static class GlobalShortcutCatalog
{
    public const string Show = "startup.show";
    public const string Hide = "startup.hide";
    public const string Toggle = "startup.toggle";
    public const string NewTodo = "startup.newTodo";
    public const string NewNote = "startup.newNote";
    public const string Exit = "startup.exit";
    public const string CurrentPaperPassive = "labs.passiveCurrent";
    public const string AllSurfacesPassive = "labs.passiveAll";
    public const string LockAllPapers = "labs.lockAllPapers";
    public const string AllPapersTransparent = "labs.transparentAllPapers";
    public const string AllCapsulesTransparent = "labs.transparentAllCapsules";
    public const string CurrentPaperTransparent = "labs.transparentCurrentPaper";

    public static IReadOnlyList<GlobalShortcutDefinition> Definitions { get; } = BuildDefinitions();

    private static readonly Dictionary<string, GlobalShortcutDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

    public static GlobalShortcutDefinition? Find(string id) => ById.GetValueOrDefault(id);

    public static Dictionary<string, string> NormalizeBindings(Dictionary<string, string>? source)
    {
        source ??= new Dictionary<string, string>();
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in Definitions.Where(item => !item.IsEdgeCapsule))
        {
            if (!source.TryGetValue(definition.Id, out var configured))
            {
                configured = definition.DefaultGesture;
            }

            normalized[definition.Id] = ShortcutGesture.TryParse(configured, out var gesture)
                ? gesture.ToStorageString()
                : "";
        }

        foreach (var group in new[] { GlobalShortcutGroup.EdgeLeft, GlobalShortcutGroup.EdgeRight })
        {
            var groupDefinitions = Definitions.Where(item => item.Group == group).ToArray();
            ShortcutGesture.TryParse(groupDefinitions[0].DefaultGesture, out var defaultGesture);
            var modifiers = defaultGesture.Modifiers;
            foreach (var definition in groupDefinitions)
            {
                if (source.TryGetValue(definition.Id, out var configured) &&
                    ShortcutGesture.TryParse(configured, out var configuredGesture) &&
                    ShortcutGesture.HasEdgePrefixModifiers(configuredGesture.Modifiers))
                {
                    modifiers = configuredGesture.Modifiers;
                    break;
                }
            }

            foreach (var definition in groupDefinitions)
            {
                normalized[definition.Id] = ShortcutGesture.ForEdgeOrdinal(
                    modifiers,
                    definition.EdgeOrdinal).ToStorageString();
            }
        }

        return normalized;
    }

    public static Dictionary<string, bool> NormalizeEnabled(Dictionary<string, bool>? source)
    {
        source ??= new Dictionary<string, bool>();
        var normalized = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var definition in Definitions.Where(item => !item.IsEdgeCapsule))
        {
            normalized[definition.Id] = source.TryGetValue(definition.Id, out var enabled)
                ? enabled
                : definition.DefaultEnabled;
        }

        foreach (var group in new[] { GlobalShortcutGroup.EdgeLeft, GlobalShortcutGroup.EdgeRight })
        {
            var groupDefinitions = DefinitionsInGroup(group);
            var groupEnabled = false;
            var hasConfigured = false;
            foreach (var definition in groupDefinitions)
            {
                if (!source.TryGetValue(definition.Id, out var enabled))
                {
                    continue;
                }

                hasConfigured = true;
                groupEnabled |= enabled;
            }

            if (!hasConfigured)
            {
                groupEnabled = groupDefinitions[0].DefaultEnabled;
            }

            foreach (var definition in groupDefinitions)
            {
                normalized[definition.Id] = groupEnabled;
            }
        }

        return normalized;
    }

    public static IReadOnlyList<GlobalShortcutDefinition> DefinitionsInGroup(GlobalShortcutGroup group) =>
        Definitions.Where(item => item.Group == group).ToArray();

    public static GlobalShortcutDefinition EdgeSequenceUiDefinition(GlobalShortcutGroup group)
    {
        if (group is not (GlobalShortcutGroup.EdgeLeft or GlobalShortcutGroup.EdgeRight))
        {
            throw new ArgumentOutOfRangeException(nameof(group));
        }

        return DefinitionsInGroup(group)[0];
    }

    public static GlobalShortcutGroup OppositeEdgeGroup(GlobalShortcutGroup group) => group switch
    {
        GlobalShortcutGroup.EdgeLeft => GlobalShortcutGroup.EdgeRight,
        GlobalShortcutGroup.EdgeRight => GlobalShortcutGroup.EdgeLeft,
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    public static bool TryGetEdgePrefixModifiers(
        IReadOnlyDictionary<string, string> bindings,
        GlobalShortcutGroup group,
        out ShortcutModifiers modifiers)
    {
        modifiers = ShortcutModifiers.None;
        foreach (var definition in DefinitionsInGroup(group))
        {
            if (bindings.TryGetValue(definition.Id, out var configured) &&
                ShortcutGesture.TryParse(configured, out var gesture) &&
                ShortcutGesture.HasEdgePrefixModifiers(gesture.Modifiers))
            {
                modifiers = gesture.Modifiers;
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyCollection<string> ExecutableIds { get; } =
        Definitions.Where(definition => definition.IsExecutable)
            .Select(definition => definition.Id)
            .ToArray();

    private static IReadOnlyList<GlobalShortcutDefinition> BuildDefinitions()
    {
        var definitions = new List<GlobalShortcutDefinition>
        {
            new(Show, "ShortcutShowAll", "", GlobalShortcutGroup.General, StartupCommandKind.Show),
            new(Hide, "ShortcutHideAll", "", GlobalShortcutGroup.General, StartupCommandKind.Hide),
            new(Toggle, "ShortcutToggleVisibility", "", GlobalShortcutGroup.General, StartupCommandKind.Toggle),
            new(NewTodo, "ShortcutNewTodo", "", GlobalShortcutGroup.General, StartupCommandKind.NewTodo),
            new(NewNote, "ShortcutNewNote", "", GlobalShortcutGroup.General, StartupCommandKind.NewNote),
            new(Exit, "ShortcutExit", "", GlobalShortcutGroup.General, StartupCommandKind.Exit),
            new(CurrentPaperPassive, "LabsCurrentPaperPassive", "Ctrl+Alt+Shift+P", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.CurrentPaperPassive),
            new(AllSurfacesPassive, "LabsAllSurfacesPassive", "Ctrl+Alt+Shift+A", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.AllSurfacesPassive),
            new(LockAllPapers, "LabsLockAllPapers", "Ctrl+Alt+Shift+L", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.LockAllPapers),
            new(AllPapersTransparent, "LabsAllPapersTransparent", "Ctrl+Alt+Shift+O", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.AllPapersTransparent),
            new(AllCapsulesTransparent, "LabsAllCapsulesTransparent", "Ctrl+Alt+Shift+C", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.AllCapsulesTransparent),
            new(CurrentPaperTransparent, "LabsCurrentPaperTransparent", "Ctrl+Alt+Shift+T", GlobalShortcutGroup.Labs,
                ExperimentalKind: ExperimentalShortcutKind.CurrentPaperTransparent)
        };

        for (var ordinal = 1; ordinal <= 9; ordinal++)
        {
            definitions.Add(new GlobalShortcutDefinition(
                $"edge.left.{ordinal}",
                "ShortcutEdgeLeftSequence",
                $"Ctrl+Shift+{ordinal}",
                GlobalShortcutGroup.EdgeLeft,
                PreferredCapsuleSide: DeepCapsuleSides.Left,
                EdgeOrdinal: ordinal));
        }

        for (var ordinal = 1; ordinal <= 9; ordinal++)
        {
            definitions.Add(new GlobalShortcutDefinition(
                $"edge.right.{ordinal}",
                "ShortcutEdgeRightSequence",
                $"Ctrl+Alt+{ordinal}",
                GlobalShortcutGroup.EdgeRight,
                PreferredCapsuleSide: DeepCapsuleSides.Right,
                EdgeOrdinal: ordinal));
        }

        return definitions;
    }
}
