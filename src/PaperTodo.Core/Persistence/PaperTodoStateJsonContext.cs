using System.Text.Json.Serialization;

namespace PaperTodo;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(PaperData))]
[JsonSerializable(typeof(PaperItem))]
internal partial class PaperTodoStateJsonContext : JsonSerializerContext
{
}
