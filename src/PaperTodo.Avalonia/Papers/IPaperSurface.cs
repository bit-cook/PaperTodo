using Avalonia.Controls;

namespace PaperTodo.Avalonia.Papers;

internal interface IPaperSurface
{
    string PaperId { get; }

    Window Window { get; }

    bool IsVisible { get; }

    PaperData Paper { get; }

    void Show();

    void Hide();

    void Close();

    void RefreshFromModel();
}
