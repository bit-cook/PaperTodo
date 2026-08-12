using System.Collections.Frozen;
using System.Text.Json;

namespace PaperTodo.PluginHost;

public sealed class PluginPackageRegistry
{
    public const string SupportedApiVersion = "1.8";

    private static readonly FrozenSet<string> KnownPermissions = new[]
    {
        "papers.read",
        "papers.observe",
        "papers.create",
        "papers.delete",
        "todos.read",
        "todos.observe",
        "todos.append",
        "todos.update",
        "todos.delete",
        "notes.read",
        "notes.observe",
        "notes.append",
        "notes.replace"
    }.ToFrozenSet(StringComparer.Ordinal);

    public PluginPackageRegistry(string? pluginRoot = null)
    {
        PluginRoot = Path.GetFullPath(pluginRoot ?? Path.Combine(AppContext.BaseDirectory, "plugins"));
    }

    public string PluginRoot { get; }

    public PluginCatalogSnapshot Scan()
    {
        var descriptors = new List<PluginPackageDescriptor>
        {
            new(
                PaperBodyProviderIds.Markdown,
                "Markdown",
                PluginPackageKind.BuiltIn,
                PluginCompatibility.Compatible,
                "",
                "",
                "",
                "",
                "",
                null)
        };
        var issues = new List<PluginScanIssue>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PaperBodyProviderIds.Markdown
        };

        if (Directory.Exists(PluginRoot))
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(
                        PluginRoot,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                issues.Add(new PluginScanIssue(PluginRoot, ex.GetBaseException().Message));
                directories = [];
            }

            foreach (var directory in directories)
            {
                var folderName = Path.GetFileName(directory);
                if (string.IsNullOrEmpty(folderName) ||
                    folderName[0] is '.' or '_' ||
                    string.Equals(folderName, "data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var manifestPath = Path.Combine(directory, "plugin.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var descriptor = ReadDescriptor(directory, manifestPath, issues);
                if (!ids.Add(descriptor.Id))
                {
                    issues.Add(new PluginScanIssue(
                        manifestPath,
                        $"Duplicate plugin id '{descriptor.Id}'."));
                    continue;
                }

                descriptors.Add(descriptor);
            }
        }

        return new PluginCatalogSnapshot(
            PluginRoot,
            Path.Combine(PluginRoot, "data"),
            descriptors,
            issues);
    }

    private static PluginPackageDescriptor ReadDescriptor(
        string directory,
        string manifestPath,
        ICollection<PluginScanIssue> issues)
    {
        PluginManifestDocument? document = null;
        try
        {
            document = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath),
                PluginManifestJsonContext.Default.PluginManifestDocument)
                ?? throw new InvalidDataException("plugin.json deserialized to null.");
            return ValidateDescriptor(directory, manifestPath, document);
        }
        catch (PluginManifestException ex)
        {
            issues.Add(new PluginScanIssue(manifestPath, ex.Message));
            return IncompatibleDescriptor(
                directory,
                manifestPath,
                document,
                ex.Compatibility,
                ex.Message);
        }
        catch (Exception ex)
        {
            var message = ex.GetBaseException().Message;
            issues.Add(new PluginScanIssue(manifestPath, message));
            return IncompatibleDescriptor(
                directory,
                manifestPath,
                document,
                PluginCompatibility.InvalidManifest,
                message);
        }
    }

    private static PluginPackageDescriptor ValidateDescriptor(
        string directory,
        string manifestPath,
        PluginManifestDocument document)
    {
        var id = ValidateId(document.Id);
        if (!string.Equals(Path.GetFileName(directory), id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Plugin folder name must match plugin id '{id}'.");
        }

        var kindText = document.Kind?.Trim().ToLowerInvariant() ?? "";
        var kind = kindText switch
        {
            "web" => PluginPackageKind.Web,
            "native" => PluginPackageKind.NativeManaged,
            _ => PluginPackageKind.Unknown
        };
        var displayName = string.IsNullOrWhiteSpace(document.Name)
            ? id
            : document.Name.Trim();
        var apiVersion = NormalizeApiVersion(document.ApiVersion);
        if (document.StateVersion < 1)
        {
            throw new InvalidDataException("stateVersion must be at least 1.");
        }
        if (!Version.TryParse(document.Version, out var version))
        {
            throw new InvalidDataException($"Plugin version '{document.Version}' is not valid.");
        }

        var settings = ValidateSettings(document.Settings);
        var permissions = ParsePermissions(document.Permissions);
        var capabilities = ParseCapabilities(document.Capabilities);
        var requirements = ParseRuntimeRequirements(document.Requires);
        var startup = ValidateStartupPaper(document.StartupPaper, settings);

        var entryPath = ResolveContainedPath(directory, document.Entry, "entry");
        var miniEntry = document.MiniEntry?.Trim() ?? "";
        if (kind == PluginPackageKind.NativeManaged)
        {
            if (miniEntry.Length > 0)
            {
                throw new InvalidDataException("miniEntry is only valid for Web plugins.");
            }
            return new PluginPackageDescriptor(
                id,
                displayName,
                kind,
                PluginCompatibility.NativeManagedUnsupported,
                "Managed native plugins are preserved but cannot run in the Native AOT host.",
                Path.GetFullPath(directory),
                Path.GetFullPath(manifestPath),
                entryPath,
                "",
                null);
        }

        if (kind == PluginPackageKind.Unknown)
        {
            return new PluginPackageDescriptor(
                id,
                displayName,
                kind,
                PluginCompatibility.UnsupportedKind,
                $"Plugin kind '{document.Kind}' is not supported by the Native AOT host.",
                Path.GetFullPath(directory),
                Path.GetFullPath(manifestPath),
                entryPath,
                "",
                null);
        }

        if (!File.Exists(entryPath))
        {
            throw new FileNotFoundException("Plugin entry was not found.", entryPath);
        }

        var miniEntryPath = "";
        PluginMiniSize? miniSize = null;
        if (miniEntry.Length > 0)
        {
            miniEntryPath = ResolveContainedPath(directory, miniEntry, "miniEntry");
            if (!File.Exists(miniEntryPath))
            {
                throw new FileNotFoundException("Plugin mini entry was not found.", miniEntryPath);
            }

            var entryDirectory = Path.GetDirectoryName(entryPath)
                ?? throw new InvalidDataException("Web plugin entry has no containing directory.");
            EnsureContained(entryDirectory, miniEntryPath, "miniEntry must stay inside the Web entry directory.");
            if (document.MiniSize != null)
            {
                miniSize = ValidateMiniSize(document.MiniSize);
            }
        }
        else if (document.MiniSize != null)
        {
            throw new InvalidDataException("miniSize requires miniEntry.");
        }

        if (!string.Equals(apiVersion, SupportedApiVersion, StringComparison.Ordinal))
        {
            throw new PluginManifestException(
                PluginCompatibility.UnsupportedApiVersion,
                $"Web plugin API {apiVersion} is unsupported; this host requires {SupportedApiVersion}.");
        }

        var manifest = new WebPluginManifest(
            id,
            displayName,
            document.Description?.Trim() ?? "",
            version,
            apiVersion,
            document.StateVersion,
            document.Entry.Trim(),
            miniEntry,
            miniSize,
            capabilities,
            permissions,
            requirements,
            settings,
            startup);
        return new PluginPackageDescriptor(
            id,
            displayName,
            PluginPackageKind.Web,
            PluginCompatibility.Compatible,
            "",
            Path.GetFullPath(directory),
            Path.GetFullPath(manifestPath),
            entryPath,
            miniEntryPath,
            manifest);
    }

    private static PluginPackageDescriptor IncompatibleDescriptor(
        string directory,
        string manifestPath,
        PluginManifestDocument? document,
        PluginCompatibility compatibility,
        string reason)
    {
        var folderName = Path.GetFileName(directory);
        var id = IsValidId(document?.Id?.Trim())
            ? document!.Id.Trim()
            : IsValidId(folderName)
                ? folderName
                : $"invalid:{folderName}";
        var kind = document?.Kind?.Trim().ToLowerInvariant() switch
        {
            "web" => PluginPackageKind.Web,
            "native" => PluginPackageKind.NativeManaged,
            _ => PluginPackageKind.Unknown
        };
        var displayName = string.IsNullOrWhiteSpace(document?.Name)
            ? id
            : document.Name.Trim();
        return new PluginPackageDescriptor(
            id,
            displayName,
            kind,
            compatibility,
            reason,
            Path.GetFullPath(directory),
            Path.GetFullPath(manifestPath),
            "",
            "",
            null);
    }

    private static string ValidateId(string? value)
    {
        var id = value?.Trim() ?? "";
        if (!IsValidId(id))
        {
            throw new InvalidDataException(
                "Plugin id must contain 3-120 ASCII letters, digits, '.', '_' or '-'.");
        }
        if (string.Equals(id, PaperBodyProviderIds.Markdown, StringComparison.Ordinal) ||
            string.Equals(id, "data", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The plugin id is reserved by PaperTodo.");
        }
        return id;
    }

    private static bool IsValidId(string? value) =>
        value is { Length: >= 3 and <= 120 } &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or '-');

    private static string NormalizeApiVersion(string? value)
    {
        var parts = value?.Trim().Split('.', StringSplitOptions.None);
        if (parts is not { Length: 2 } ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            major < 0 || minor < 0)
        {
            throw new InvalidDataException(
                "apiVersion must be a quoted major.minor string such as \"1.8\".");
        }
        return $"{major}.{minor}";
    }

    private static IReadOnlyList<PluginSetting> ValidateSettings(PluginSettingDocument[]? source)
    {
        var settings = new List<PluginSetting>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var quickCount = 0;
        foreach (var document in source ?? [])
        {
            var id = document.Id?.Trim() ?? "";
            var type = document.Type?.Trim().ToLowerInvariant() ?? "";
            if (id.Length is < 1 or > 80 || !IsSettingId(id) || !ids.Add(id))
            {
                throw new InvalidDataException(
                    $"Plugin setting id '{id}' is invalid or duplicated.");
            }
            if (type is not ("boolean" or "string" or "number" or "select"))
            {
                throw new InvalidDataException(
                    $"Plugin setting '{id}' has unsupported type '{type}'.");
            }
            if (document.Quick && ++quickCount > 3)
            {
                throw new InvalidDataException("A plugin may expose at most three quick settings.");
            }
            if (document.MaxLength is < 0)
            {
                throw new InvalidDataException($"Plugin setting '{id}' maxLength cannot be negative.");
            }
            if (document.Min.HasValue && document.Max.HasValue && document.Min > document.Max)
            {
                throw new InvalidDataException($"Plugin setting '{id}' min cannot exceed max.");
            }
            if (document.Step is <= 0)
            {
                throw new InvalidDataException($"Plugin setting '{id}' step must be greater than zero.");
            }

            var options = ValidateOptions(id, type, document.Options);
            ValidateDefault(id, type, document.Default, document, options);
            settings.Add(new PluginSetting(
                id,
                type,
                string.IsNullOrWhiteSpace(document.Name) ? id : document.Name.Trim(),
                document.Description?.Trim() ?? "",
                document.Default.ValueKind == JsonValueKind.Undefined
                    ? default
                    : document.Default.Clone(),
                document.Quick,
                document.Min,
                document.Max,
                document.Step,
                document.MaxLength,
                document.Suffix?.Trim() ?? "",
                document.Placeholder?.Trim() ?? "",
                options));
        }
        return settings;
    }

    private static IReadOnlyList<PluginSettingOption> ValidateOptions(
        string settingId,
        string type,
        PluginSettingOptionDocument[]? source)
    {
        var options = new List<PluginSettingOption>();
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in source ?? [])
        {
            var value = option.Value?.Trim() ?? "";
            if (value.Length == 0 || !values.Add(value))
            {
                throw new InvalidDataException(
                    $"Select setting '{settingId}' contains an empty or duplicated option.");
            }
            options.Add(new PluginSettingOption(
                value,
                string.IsNullOrWhiteSpace(option.Name) ? value : option.Name.Trim()));
        }
        if (type == "select" && options.Count == 0)
        {
            throw new InvalidDataException(
                $"Select setting '{settingId}' must declare at least one option.");
        }
        return options;
    }

    private static void ValidateDefault(
        string id,
        string type,
        JsonElement value,
        PluginSettingDocument setting,
        IReadOnlyList<PluginSettingOption> options)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        switch (type)
        {
            case "boolean" when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return;
            case "string" when value.ValueKind == JsonValueKind.String:
                if (setting.MaxLength is > 0 && (value.GetString()?.Length ?? 0) > setting.MaxLength)
                {
                    throw new InvalidDataException($"Plugin setting '{id}' default exceeds maxLength.");
                }
                return;
            case "number" when value.ValueKind == JsonValueKind.Number &&
                               value.TryGetDouble(out var number) &&
                               double.IsFinite(number):
                if (setting.Min.HasValue && number < setting.Min ||
                    setting.Max.HasValue && number > setting.Max)
                {
                    throw new InvalidDataException($"Plugin setting '{id}' default is outside its range.");
                }
                if (setting.Step is > 0)
                {
                    var origin = setting.Min ?? 0;
                    var steps = (number - origin) / setting.Step.Value;
                    if (Math.Abs(steps - Math.Round(steps)) > 1e-9)
                    {
                        throw new InvalidDataException($"Plugin setting '{id}' default is not aligned to step.");
                    }
                }
                return;
            case "select" when value.ValueKind == JsonValueKind.String &&
                               options.Any(option => string.Equals(
                                   option.Value,
                                   value.GetString(),
                                   StringComparison.Ordinal)):
                return;
        }

        throw new InvalidDataException(
            $"Plugin setting '{id}' default does not match type '{type}'.");
    }

    private static bool IsSettingId(string id) => id.All(character =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static IReadOnlySet<string> ParsePermissions(string[]? source)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in source ?? [])
        {
            var value = raw?.Trim() ?? "";
            if (value.Length == 0)
            {
                continue;
            }
            if (!KnownPermissions.Contains(value))
            {
                throw new InvalidDataException($"Unknown plugin permission '{value}'.");
            }
            result.Add(value);
        }
        return result.ToFrozenSet(StringComparer.Ordinal);
    }

    private static WebPluginCapabilities ParseCapabilities(string[]? source)
    {
        var result = WebPluginCapabilities.None;
        foreach (var raw in source ?? [])
        {
            result |= raw?.Trim().ToLowerInvariant() switch
            {
                null or "" => WebPluginCapabilities.None,
                "textzoom" => WebPluginCapabilities.TextZoom,
                "notelinks" => WebPluginCapabilities.NoteLinks,
                _ => throw new InvalidDataException($"Unknown plugin capability '{raw}'.")
            };
        }
        return result;
    }

    private static WebPluginRuntimeRequirements ParseRuntimeRequirements(string[]? source)
    {
        var result = WebPluginRuntimeRequirements.None;
        foreach (var raw in source ?? [])
        {
            result |= raw?.Trim() switch
            {
                null or "" => WebPluginRuntimeRequirements.None,
                "backgroundUpdates" => WebPluginRuntimeRequirements.BackgroundUpdates,
                _ => throw new InvalidDataException($"Unknown required plugin feature '{raw}'.")
            };
        }
        return result;
    }

    private static PluginStartupPaper? ValidateStartupPaper(
        PluginStartupPaperDocument? source,
        IReadOnlyList<PluginSetting> settings)
    {
        if (source == null)
        {
            return null;
        }
        var enabledSetting = source.EnabledSetting?.Trim() ?? "";
        var instanceKey = source.InstanceKey?.Trim() ?? "";
        var presentation = source.Presentation?.Trim().ToLowerInvariant() ?? "";
        var title = source.Title?.Trim() ?? "";
        if (instanceKey.Length is < 1 or > 80 || !IsSettingId(instanceKey))
        {
            throw new InvalidDataException(
                "startupPaper.instanceKey must contain 1-80 ASCII letters, digits, '.', '_' or '-'.");
        }
        if (presentation is not ("capsule" or "expanded"))
        {
            throw new InvalidDataException(
                "startupPaper.presentation must be 'capsule' or 'expanded'.");
        }
        if (title.Length > 120)
        {
            throw new InvalidDataException("startupPaper.title cannot exceed 120 characters.");
        }
        if (!settings.Any(setting =>
                setting.Type == "boolean" &&
                string.Equals(setting.Id, enabledSetting, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"startupPaper.enabledSetting '{enabledSetting}' must reference a boolean setting.");
        }
        return new PluginStartupPaper(enabledSetting, instanceKey, presentation, title);
    }

    private static PluginMiniSize ValidateMiniSize(PluginMiniSizeDocument source)
    {
        if (!double.IsFinite(source.Width) ||
            !double.IsFinite(source.Height) ||
            source.Width is < 120 or > 480 ||
            source.Height is < 90 or > 420)
        {
            throw new InvalidDataException(
                "miniSize must be within 120x90 and 480x420 DIPs.");
        }
        return new PluginMiniSize(source.Width, source.Height);
    }

    private static string ResolveContainedPath(string directory, string? relativePath, string field)
    {
        var value = relativePath?.Trim() ?? "";
        if (value.Length == 0 || Path.IsPathRooted(value))
        {
            throw new InvalidDataException($"Plugin {field} must be a relative file path.");
        }
        var portablePath = value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(directory, portablePath));
        EnsureContained(directory, path, $"Plugin {field} must stay inside its plugin directory.");
        return path;
    }

    private static void EnsureContained(string rootDirectory, string path, string message)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        if (!candidate.StartsWith(root, comparison))
        {
            throw new InvalidDataException(message);
        }
    }

    private sealed class PluginManifestException(
        PluginCompatibility compatibility,
        string message) : Exception(message)
    {
        public PluginCompatibility Compatibility { get; } = compatibility;
    }
}
