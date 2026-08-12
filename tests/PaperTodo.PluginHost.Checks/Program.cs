using PaperTodo.PluginHost;

return PluginHostChecks.Run();

internal static class PluginHostChecks
{
    internal static int Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PaperTodo.PluginHost.Checks",
            Guid.NewGuid().ToString("N"));
        var plugins = Path.Combine(root, "plugins");
        Directory.CreateDirectory(plugins);
        try
        {
            var data = Path.Combine(plugins, "data");
            Directory.CreateDirectory(data);
            var legacyData = Path.Combine(data, "legacy.native.provider.json");
            File.WriteAllText(legacyData, "do-not-touch");

            WritePackage(
                plugins,
                "valid.web",
                """
                {
                  // Comments, trailing commas and future fields remain forward compatible.
                  "kind": "web",
                  "id": "valid.web",
                  "name": "Valid Web",
                  "version": "2.1.0",
                  "apiVersion": "1.8",
                  "stateVersion": 2,
                  "entry": "web/index.html",
                  "miniEntry": "web/mini.html",
                  "miniSize": { "width": 300, "height": 190 },
                  "capabilities": ["textZoom", "noteLinks"],
                  "requires": ["backgroundUpdates"],
                  "permissions": ["papers.read", "todos.update"],
                  "settings": [
                    { "id": "enabled", "type": "boolean", "default": true, "quick": true },
                    {
                      "id": "mode", "type": "select", "default": "clock",
                      "options": [
                        { "value": "clock", "name": "Clock" },
                        { "value": "date", "name": "Date" }
                      ]
                    },
                    { "id": "scale", "type": "number", "default": 1, "min": 0.5, "max": 2, "step": 0.5 }
                  ],
                  "startupPaper": {
                    "enabledSetting": "enabled",
                    "instanceKey": "main",
                    "presentation": "capsule",
                    "title": "Clock"
                  },
                  "futureField": { "keptByFutureHost": true },
                }
                """,
                ("web/index.html", "index"),
                ("web/mini.html", "mini"));

            WritePackage(
                plugins,
                "legacy.native",
                """
                {
                  "kind": "native",
                  "id": "legacy.native",
                  "name": "Legacy Native",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "Legacy.dll"
                }
                """,
                ("Legacy.dll", "managed-binary-placeholder"));

            WritePackage(
                plugins,
                "missing.native",
                """
                {
                  "kind": "native",
                  "id": "missing.native",
                  "name": "Missing Legacy Native",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "Removed.dll"
                }
                """);

            WritePackage(
                plugins,
                "future.kind",
                """
                {
                  "kind": "process-v2",
                  "id": "future.kind",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "future.bin"
                }
                """,
                ("future.bin", "future"));

            var outside = Path.Combine(plugins, "outside.html");
            File.WriteAllText(outside, "outside");
            WritePackage(
                plugins,
                "escape.web",
                """
                {
                  "kind": "web",
                  "id": "escape.web",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "../outside.html"
                }
                """);

            WritePackage(
                plugins,
                "old-api.web",
                """
                {
                  "kind": "web",
                  "id": "old-api.web",
                  "version": "1.0.0",
                  "apiVersion": "1.7",
                  "entry": "index.html"
                }
                """,
                ("index.html", "old"));

            WritePackage(
                plugins,
                "bad-setting.web",
                """
                {
                  "kind": "web",
                  "id": "bad-setting.web",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "index.html",
                  "settings": [
                    { "id": "count", "type": "number", "default": 11, "min": 0, "max": 10 }
                  ]
                }
                """,
                ("index.html", "bad"));

            WritePackage(
                plugins,
                "bad-mini.web",
                """
                {
                  "kind": "web",
                  "id": "bad-mini.web",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "web/index.html",
                  "miniEntry": "other/mini.html"
                }
                """,
                ("web/index.html", "index"),
                ("other/mini.html", "mini"));

            WritePackage(
                plugins,
                "bad-permission.web",
                """
                {
                  "kind": "web",
                  "id": "bad-permission.web",
                  "version": "1.0.0",
                  "apiVersion": "1.8",
                  "entry": "index.html",
                  "permissions": ["filesystem.all"]
                }
                """,
                ("index.html", "bad"));

            var hidden = Path.Combine(plugins, "_ignored.web");
            Directory.CreateDirectory(hidden);
            File.WriteAllText(Path.Combine(hidden, "plugin.json"), "not-json");

            var beforeFiles = Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(data, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var snapshot = new PluginPackageRegistry(plugins).Scan();
            var afterFiles = Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(data, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            var valid = Required(snapshot, "valid.web");
            Assert(valid.IsCompatibleWeb, "valid Web package should be compatible");
            var validManifest = valid.Manifest
                ?? throw new InvalidOperationException("compatible Web descriptor has no manifest");
            Assert(validManifest.ApiVersion == "1.8" && validManifest.StateVersion == 2,
                "normalized Web manifest metadata should be exposed");
            Assert(validManifest.Capabilities ==
                   (WebPluginCapabilities.TextZoom | WebPluginCapabilities.NoteLinks),
                "known capabilities should be preserved");
            Assert(validManifest.Permissions.SetEquals(new[] { "papers.read", "todos.update" }),
                "known permissions should be preserved");
            Assert(validManifest.Settings.Count == 3 && validManifest.StartupPaper != null,
                "settings/defaults and startup declaration should survive validation");

            Assert(Required(snapshot, "legacy.native").Compatibility ==
                   PluginCompatibility.NativeManagedUnsupported,
                "managed native package should remain discoverable but incompatible");
            Assert(Required(snapshot, "missing.native").Compatibility ==
                   PluginCompatibility.NativeManagedUnsupported,
                "a legacy native provider must remain identifiable after its entry is removed");
            Assert(Required(snapshot, "future.kind").Compatibility ==
                   PluginCompatibility.UnsupportedKind,
                "unknown package kind should remain discoverable but incompatible");
            Assert(Required(snapshot, "escape.web").Compatibility ==
                   PluginCompatibility.InvalidManifest,
                "entry traversal must be rejected");
            Assert(Required(snapshot, "old-api.web").Compatibility ==
                   PluginCompatibility.UnsupportedApiVersion,
                "Web packages must use API 1.8");
            Assert(Required(snapshot, "bad-setting.web").Compatibility ==
                   PluginCompatibility.InvalidManifest,
                "invalid setting defaults must be rejected");
            Assert(Required(snapshot, "bad-mini.web").Compatibility ==
                   PluginCompatibility.InvalidManifest,
                "miniEntry must remain under the entry directory");
            Assert(Required(snapshot, "bad-permission.web").Compatibility ==
                   PluginCompatibility.InvalidManifest,
                "unknown permissions must be rejected");
            Assert(!snapshot.TryGet("_ignored.web", out _),
                "reserved hidden directories must not be scanned");

            var missing = snapshot.ResolveProvider("legacy.missing.provider");
            Assert(missing.Id == "legacy.missing.provider" &&
                   missing.Compatibility == PluginCompatibility.LegacyProviderUnavailable,
                "an unknown saved provider id must receive a stable incompatible descriptor");
            Assert(File.ReadAllText(legacyData) == "do-not-touch" &&
                   beforeFiles.SequenceEqual(afterFiles, StringComparer.Ordinal),
                "scanning must not rewrite or populate plugins/data");
            Assert(snapshot.Issues.Count >= 5,
                "invalid packages should expose actionable scan issues");

            Console.WriteLine("PaperTodo PluginHost compatibility checks passed.");
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
                // A failed cleanup must not hide a protocol assertion.
            }
        }
    }

    private static PluginPackageDescriptor Required(PluginCatalogSnapshot snapshot, string id)
    {
        Assert(snapshot.TryGet(id, out var descriptor), $"descriptor '{id}' was not discovered");
        return descriptor;
    }

    private static void WritePackage(
        string pluginRoot,
        string id,
        string manifest,
        params (string Path, string Content)[] files)
    {
        var directory = Path.Combine(pluginRoot, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), manifest);
        foreach (var file in files)
        {
            var path = Path.Combine(directory, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
