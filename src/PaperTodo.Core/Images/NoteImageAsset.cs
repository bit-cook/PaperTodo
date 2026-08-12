namespace PaperTodo;

/// <summary>
/// Durable metadata for an image blob stored in PaperTodo's LMDB asset store.
/// Property names and defaults are part of the existing on-disk JSON contract.
/// </summary>
public sealed class NoteImageAsset
{
    public string Id { get; set; } = "";
    public string NoteId { get; set; } = "";
    public string Mime { get; set; } = "image/png";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Sha256 { get; set; } = "";
    public int ByteLength { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? OriginalName { get; set; }
}
