namespace PaperTodo;

public static class EdgeCapsuleHoverIntentSensitivities
{
    public const string VeryLow = "veryLow";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string VeryHigh = "veryHigh";

    public static string Normalize(string? sensitivity) =>
        sensitivity is VeryLow or Low or High or VeryHigh
            ? sensitivity
            : Medium;
}
