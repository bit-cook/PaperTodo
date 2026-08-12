using Avalonia;

namespace PaperTodo.Avalonia.Papers;

internal readonly record struct PaperSurfaceDescriptor(
    PaperData Paper,
    PixelPoint Position,
    Size Size,
    bool IsVisible,
    bool AlwaysOnTop)
{
    public string PaperId => Paper.Id;
}
