using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class PaperEditorControl : Grid
{
    private readonly PaperData _paper;
    private readonly TextBox _title;
    private readonly TextBox _body;
    private readonly bool _canEditBody;

    public PaperEditorControl(PaperData paper)
    {
        _paper = paper;
        _canEditBody = PaperTextCodec.CanEditBody(paper);
        RowDefinitions = new RowDefinitions("24,*");
        Background = new SolidColorBrush(Color.FromRgb(255, 249, 218));

        _title = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = default,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(8, 2),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _title.TextChanged += (_, _) => _paper.Title = _title.Text ?? string.Empty;

        _body = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = default,
            IsReadOnly = !_canEditBody,
            Padding = new Thickness(8),
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        if (_canEditBody)
        {
            _body.TextChanged += OnBodyTextChanged;
        }
        else
        {
            _body.Foreground = Brushes.DimGray;
        }

        Children.Add(_title);
        Children.Add(_body);
        Grid.SetRow(_body, 1);
        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        _title.Text = _paper.Title;
        _body.Text = _canEditBody
            ? PaperTextCodec.ToEditorText(_paper)
            : PluginBodyPlaceholder();
    }

    private void OnBodyTextChanged(object? sender, TextChangedEventArgs e) =>
        PaperTextCodec.ApplyEditorText(_paper, _body.Text ?? string.Empty);

    private string PluginBodyPlaceholder()
    {
        var text = TextCatalog.Current;
        return $"{text.PluginBodyReadOnly}{Environment.NewLine}{Environment.NewLine}" +
            $"{text.PluginProviderLabel}: {_paper.BodyProviderId}";
    }
}
