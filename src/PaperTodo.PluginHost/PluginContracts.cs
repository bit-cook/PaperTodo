using System.Text.Json;

namespace PaperTodo.PluginHost;

public enum PluginPackageKind
{
    BuiltIn,
    Web,
    NativeManaged,
    Unknown
}

public enum PluginCompatibility
{
    Compatible,
    NativeManagedUnsupported,
    UnsupportedKind,
    UnsupportedApiVersion,
    InvalidManifest,
    LegacyProviderUnavailable
}

[Flags]
public enum WebPluginCapabilities
{
    None = 0,
    TextZoom = 1 << 0,
    NoteLinks = 1 << 1
}

[Flags]
public enum WebPluginRuntimeRequirements
{
    None = 0,
    BackgroundUpdates = 1 << 0
}

public sealed record PluginSettingOption(string Value, string Name);

public sealed record PluginSetting(
    string Id,
    string Type,
    string Name,
    string Description,
    JsonElement Default,
    bool Quick,
    double? Min,
    double? Max,
    double? Step,
    int? MaxLength,
    string Suffix,
    string Placeholder,
    IReadOnlyList<PluginSettingOption> Options);

public sealed record PluginStartupPaper(
    string EnabledSetting,
    string InstanceKey,
    string Presentation,
    string Title);

public readonly record struct PluginMiniSize(double Width, double Height);

public sealed record WebPluginManifest(
    string Id,
    string Name,
    string Description,
    Version Version,
    string ApiVersion,
    int StateVersion,
    string Entry,
    string MiniEntry,
    PluginMiniSize? MiniSize,
    WebPluginCapabilities Capabilities,
    IReadOnlySet<string> Permissions,
    WebPluginRuntimeRequirements RuntimeRequirements,
    IReadOnlyList<PluginSetting> Settings,
    PluginStartupPaper? StartupPaper);

public sealed record PluginPackageDescriptor(
    string Id,
    string DisplayName,
    PluginPackageKind Kind,
    PluginCompatibility Compatibility,
    string IncompatibilityReason,
    string PluginDirectory,
    string ManifestPath,
    string EntryPath,
    string MiniEntryPath,
    WebPluginManifest? Manifest)
{
    public bool IsCompatibleWeb =>
        Kind == PluginPackageKind.Web &&
        Compatibility == PluginCompatibility.Compatible &&
        Manifest != null;
}

public sealed record PluginScanIssue(string SourcePath, string Message);

public sealed record PluginCatalogSnapshot(
    string PluginRoot,
    string DataRoot,
    IReadOnlyList<PluginPackageDescriptor> Descriptors,
    IReadOnlyList<PluginScanIssue> Issues)
{
    public bool TryGet(string? providerId, out PluginPackageDescriptor descriptor)
    {
        var id = string.IsNullOrWhiteSpace(providerId)
            ? PaperBodyProviderIds.Markdown
            : providerId.Trim();
        descriptor = Descriptors.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal))!;
        return descriptor != null;
    }

    public PluginPackageDescriptor ResolveProvider(string? providerId)
    {
        var id = string.IsNullOrWhiteSpace(providerId)
            ? PaperBodyProviderIds.Markdown
            : providerId.Trim();
        if (TryGet(id, out var descriptor))
        {
            return descriptor;
        }

        return new PluginPackageDescriptor(
            id,
            id,
            PluginPackageKind.Unknown,
            PluginCompatibility.LegacyProviderUnavailable,
            "The saved provider is not installed or is not compatible with the Native AOT host.",
            "",
            "",
            "",
            "",
            null);
    }
}
