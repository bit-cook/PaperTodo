using PaperTodo;

internal static partial class Program
{
    static Program()
    {
        const string title = "📌本周计划表";
        Check(title.Length > 6,
            "Unicode title fixture must exceed six UTF-16 code units");
        Check(!PaperTitles.ExceedsTextElementLimit(title, 6),
            "Six Unicode title elements remain valid for external writes");
        Check(PaperTitles.CleanCustomTitle(title, 6) == title,
            "External title validation stays aligned with title cleanup");
        Check(PaperTitles.ExceedsTextElementLimit(title + "新", 6),
            "A seventh Unicode title element exceeds the configured limit");
    }
}
