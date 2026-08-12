using Avalonia.Controls;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperEditorControl : Grid
{
    private readonly PaperData _paper;
    private readonly Control _body;

    public PaperEditorControl(
        PaperData paper,
        AppState state,
        PaperThemePalette palette,
        Action changed)
    {
        _paper = paper;
        Background = Brushes.Transparent;

        if (paper.Type == PaperTypes.Todo)
        {
            _body = new TodoEditorControl(paper, state, palette, changed);
        }
        else if (PaperTextCodec.CanEditBody(paper))
        {
            _body = new MarkdownNoteControl(paper, state, palette, changed);
        }
        else
        {
            var text = TextCatalog.Current;
            _body = new TextBox
            {
                Text = $"{text.PluginBodyReadOnly}{Environment.NewLine}{Environment.NewLine}" +
                    $"{text.PluginProviderLabel}: {paper.BodyProviderId}",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                BorderThickness = default,
                IsReadOnly = true,
                Padding = new Avalonia.Thickness(10, 8),
                Foreground = palette.WeakTextBrush
            };
        }

        Children.Add(_body);
    }

    public void RefreshFromModel()
    {
        switch (_body)
        {
            case TodoEditorControl todo:
                todo.RefreshFromModel();
                break;
            case MarkdownNoteControl note:
                note.RefreshFromModel();
                break;
        }
    }
}
