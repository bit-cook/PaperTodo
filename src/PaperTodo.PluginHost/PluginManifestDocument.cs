using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaperTodo.PluginHost;

internal sealed class PluginManifestDocument
{
    public string Kind { get; set; } = "web";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string ApiVersion { get; set; } = "";
    public int StateVersion { get; set; } = 1;
    public string[]? Requires { get; set; } = [];
    public string[]? Permissions { get; set; } = [];
    public string Entry { get; set; } = "index.html";
    public string MiniEntry { get; set; } = "";
    public PluginMiniSizeDocument? MiniSize { get; set; }
    public string[]? Capabilities { get; set; } = [];
    public PluginSettingDocument[]? Settings { get; set; } = [];
    public PluginStartupPaperDocument? StartupPaper { get; set; }
}

internal sealed class PluginMiniSizeDocument
{
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 220;
}

internal sealed class PluginSettingDocument
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonElement Default { get; set; }
    public bool Quick { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public int? MaxLength { get; set; }
    public string Suffix { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public PluginSettingOptionDocument[]? Options { get; set; } = [];
}

internal sealed class PluginSettingOptionDocument
{
    public string Value { get; set; } = "";
    public string Name { get; set; } = "";
}

internal sealed class PluginStartupPaperDocument
{
    public string EnabledSetting { get; set; } = "";
    public string InstanceKey { get; set; } = "main";
    public string Presentation { get; set; } = "capsule";
    public string Title { get; set; } = "";
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PluginManifestDocument))]
internal sealed partial class PluginManifestJsonContext : JsonSerializerContext;
