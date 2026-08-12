namespace PaperTodo;

public static class ColorSchemes
{
    public const string Warm = "warm";
    public const string Ink = "ink";
    public const string Forest = "forest";
    public const string Rose = "rose";

    public static readonly string[] All = [Warm, Ink, Forest, Rose];

    public static bool IsValid(string? id) => id is Warm or Ink or Forest or Rose;

    public static string Normalize(string? id) => IsValid(id) ? id! : Warm;
}
