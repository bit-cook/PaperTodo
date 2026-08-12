using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperEditorControl : Grid
{
    private readonly PaperData _paper;
    private readonly AppState _state;
    private readonly Action _changed;
    private readonly Control _body;
    private readonly bool _canEditBody;
    private bool _refreshing;

    public PaperEditorControl(
        PaperData paper,
        AppState state,
        PaperThemePalette palette,
        Action changed)
    {
        _paper = paper;
        _state = state;
        _changed = changed;
        _canEditBody = PaperTextCodec.CanEditBody(paper);
        Background = Brushes.Transparent;

        if (paper.Type == PaperTypes.Todo)
        {
            _body = new TodoEditorControl(paper, state, palette, changed);
            Children.Add(_body);
            return;
        }

        var body = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = default,
            IsReadOnly = !_canEditBody,
            Padding = new Thickness(10, 8),
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Foreground = _canEditBody ? palette.TextBrush : palette.WeakTextBrush,
            FontSize = VisualTextSizes.FontSize(
                13,
                _state.NoteTextSize,
                OverallFontScales.Normalize(_state.Zoom) *
                OverallFontScales.Normalize(_paper.TextZoom)),
            FontWeight = _state.NoteTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            Watermark = _canEditBody ? TextCatalog.Current.NotePlaceholder : null
        };
        if (_canEditBody)
        {
            body.TextChanged += OnBodyTextChanged;
        }

        _body = body;
        Children.Add(_body);
        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        if (_body is TodoEditorControl todo)
        {
            todo.RefreshFromModel();
            return;
        }

        if (_body is not TextBox body)
        {
            return;
        }

        _refreshing = true;
        try
        {
            body.Text = _canEditBody
                ? PaperTextCodec.ToEditorText(_paper)
                : PluginBodyPlaceholder();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnBodyTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_refreshing || sender is not TextBox body)
        {
            return;
        }

        PaperTextCodec.ApplyEditorText(_paper, body.Text ?? string.Empty);
        _changed();
    }

    private string PluginBodyPlaceholder()
    {
        var text = TextCatalog.Current;
        return $"{text.PluginBodyReadOnly}{Environment.NewLine}{Environment.NewLine}" +
            $"{text.PluginProviderLabel}: {_paper.BodyProviderId}";
    }
}
