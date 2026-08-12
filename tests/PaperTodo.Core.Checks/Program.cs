using PaperTodo;

return StateStoreChecks.Run();

internal static class StateStoreChecks
{
    internal static int Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PaperTodo.Core.Checks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            EmptyDirectoryStartsEmpty(root);
            UnknownFieldsRemainCompatible(root);
            ExplicitNullLegacyValuesAreRepaired(root);
            OpaquePluginProviderStateRoundTrips(root);
            CorruptPrimaryRecoversWithoutDestroyingSources(root);
            DoubleCorruptionNeverReturnsAnEmptyState(root);
            OlderWritesNeverReplaceNewerWrites(root);
            Console.WriteLine("PaperTodo Core persistence checks passed.");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // A failed cleanup must not hide a compatibility assertion.
            }
        }
    }

    private static void ExplicitNullLegacyValuesAreRepaired(string root)
    {
        var directory = FreshDirectory(root, "explicit-null");
        File.WriteAllText(
            Path.Combine(directory, "data.json"),
            """
            {
              "papers": [
                {
                  "id": "legacy-null-note",
                  "type": "note",
                  "items": null,
                  "content": null,
                  "bodyHeaderText": null,
                  "bodyCapsuleText": null
                }
              ],
              "globalHotkeys": null,
              "globalHotkeyEnabled": null,
              "deepCapsuleQueueStartTopMargins": null
            }
            """);

        var state = new StateStore(baseDirectory: directory).Load();
        var paper = state.Papers.Single();
        Assert(paper.Items.Count == 0 &&
               paper.Content == string.Empty &&
               paper.BodyHeaderText == string.Empty &&
               paper.BodyCapsuleText == string.Empty &&
               state.GlobalHotkeys.Count > 0 &&
               state.GlobalHotkeyEnabled.Count > 0 &&
               state.DeepCapsuleQueueStartTopMargins.Count == 0,
            "explicit nulls accepted by earlier data.json readers must reach load normalization");
    }

    private static void OpaquePluginProviderStateRoundTrips(string root)
    {
        var directory = FreshDirectory(root, "plugin-provider");
        File.WriteAllText(
            Path.Combine(directory, "data.json"),
            """
            {
              "papers": [
                {
                  "id": "legacy-note",
                  "type": "note",
                  "title": "legacy",
                  "bodyProviderId": "legacy.native.provider",
                  "bodyHeaderText": "header",
                  "bodyCapsuleText": "capsule",
                  "items": [],
                  "content": "opaque provider state"
                }
              ]
            }
            """);

        var store = new StateStore(baseDirectory: directory);
        var state = store.Load();
        var note = state.Papers.Single();
        Assert(note.BodyProviderId == "legacy.native.provider" &&
               note.Content == "opaque provider state" &&
               note.BodyCapsuleText == "capsule",
            "loading must preserve an unavailable plugin provider and its opaque note state");

        store.SaveJsonSync(store.SerializeState(state), version: 1);
        var reloaded = new StateStore(baseDirectory: directory).Load().Papers.Single();
        Assert(reloaded.BodyProviderId == "legacy.native.provider" &&
               reloaded.Content == "opaque provider state" &&
               reloaded.BodyCapsuleText == "capsule",
            "saving must not silently convert an unavailable provider to Markdown or erase data");
    }

    private static void EmptyDirectoryStartsEmpty(string root)
    {
        var directory = FreshDirectory(root, "empty");
        var state = new StateStore(baseDirectory: directory).Load();
        Assert(state.Papers.Count == 0, "a new data directory should start empty");
        Assert(!File.Exists(Path.Combine(directory, "data.json")),
            "loading an empty directory must not create data.json");
    }

    private static void UnknownFieldsRemainCompatible(string root)
    {
        var directory = FreshDirectory(root, "unknown-field");
        File.WriteAllText(
            Path.Combine(directory, "data.json"),
            """
            {
              "papers": [
                {
                  "id": "paper-1",
                  "type": "todo",
                  "title": "kept",
                  "items": [],
                  "content": "",
                  "futurePaperField": { "value": 42 }
                }
              ],
              "futureRootField": true
            }
            """);

        var state = new StateStore(baseDirectory: directory).Load();
        Assert(state.Papers.Count == 1 && state.Papers[0].Title == "kept",
            "unknown fields must not make an older-compatible state unreadable");
    }

    private static void CorruptPrimaryRecoversWithoutDestroyingSources(string root)
    {
        var directory = FreshDirectory(root, "backup-recovery");
        var primaryPath = Path.Combine(directory, "data.json");
        var backupPath = Path.Combine(directory, "data.backup.json");
        const string corruptPrimary = "{ definitely not json";
        const string healthyBackup = """
            {
              "papers": [
                {
                  "id": "recovered",
                  "type": "note",
                  "title": "backup",
                  "items": [],
                  "content": "safe"
                }
              ]
            }
            """;
        File.WriteAllText(primaryPath, corruptPrimary);
        File.WriteAllText(backupPath, healthyBackup);

        var store = new StateStore(baseDirectory: directory);
        var recovered = store.Load();
        Assert(recovered.Papers.Single().Id == "recovered",
            "a healthy backup should recover a corrupt primary");
        store.SaveJsonSync(store.SerializeState(recovered), version: 1);

        Assert(Directory.EnumerateFiles(directory, "data.failed_load.*.json").Any(
                path => File.ReadAllText(path) == corruptPrimary),
            "the failed primary must be preserved before replacement");
        Assert(Directory.EnumerateFiles(directory, "data.backup.used_for_recovery.*.json").Any(
                path => File.ReadAllText(path) == healthyBackup),
            "the backup used for recovery must be preserved");
    }

    private static void DoubleCorruptionNeverReturnsAnEmptyState(string root)
    {
        var directory = FreshDirectory(root, "double-corrupt");
        var primaryPath = Path.Combine(directory, "data.json");
        var backupPath = Path.Combine(directory, "data.backup.json");
        File.WriteAllText(primaryPath, "bad-primary");
        File.WriteAllText(backupPath, "bad-backup");

        var threw = false;
        try
        {
            _ = new StateStore(baseDirectory: directory).Load();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert(threw, "two corrupt snapshots must fail instead of returning empty state");
        Assert(File.ReadAllText(primaryPath) == "bad-primary" &&
               File.ReadAllText(backupPath) == "bad-backup",
            "failed loading must not rewrite either corrupt snapshot");
    }

    private static void OlderWritesNeverReplaceNewerWrites(string root)
    {
        var directory = FreshDirectory(root, "save-order");
        var store = new StateStore(baseDirectory: directory);
        store.SaveJsonSync("newest", version: 8);
        store.SaveJsonSync("stale", version: 7);
        Assert(File.ReadAllText(store.FilePath) == "newest",
            "a stale save must not overwrite a newer committed version");
    }

    private static string FreshDirectory(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
