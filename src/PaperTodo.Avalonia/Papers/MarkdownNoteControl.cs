using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class MarkdownNoteControl : Grid
{
    private const int MaximumLength = 100000;

    private readonly PaperData _paper;
    private readonly AppState _state;
    private readonly PaperThemePalette _palette;
    private readonly Action _changed;
    private readonly TextBox _editor;
    private readonly ScrollViewer _previewScroller;
    private readonly StackPanel _preview;
    private bool _refreshing;

    public MarkdownNoteControl(
        PaperData paper,
        AppState state,
        PaperThemePalette palette,
        Action changed)
    {
        _paper = paper;
        _state = state;
        _palette = palette;
        _changed = changed;

        _preview = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(11, 8)
        };
        _previewScroller = new ScrollViewer
        {
            Content = _preview,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _previewScroller.DoubleTapped += (_, e) =>
        {
            EnterEditor();
            e.Handled = true;
        };

        _editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            MaxLength = MaximumLength,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = new Thickness(10, 7),
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Foreground = palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                13,
                _state.NoteTextSize,
                OverallFontScales.Normalize(_state.Zoom) *
                OverallFontScales.Normalize(_paper.TextZoom)),
            FontWeight = _state.NoteTextBold ? FontWeight.SemiBold : FontWeight.Normal,
            PlaceholderText = TextCatalog.Current.NotePlaceholder,
            IsVisible = false
        };
        _editor.TextChanged += (_, _) =>
        {
            if (_refreshing)
            {
                return;
            }

            PaperTextCodec.ApplyEditorText(_paper, _editor.Text ?? string.Empty);
            _changed();
        };
        _editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                ExitEditor();
                e.Handled = true;
            }
        };
        _editor.LostFocus += (_, _) =>
        {
            if (_editor.IsVisible)
            {
                ExitEditor();
            }
        };

        Children.Add(_previewScroller);
        Children.Add(_editor);
        RefreshFromModel();
    }

    public void RefreshFromModel()
    {
        var text = PaperTextCodec.ToEditorText(_paper);
        if (!_editor.IsFocused)
        {
            _refreshing = true;
            try
            {
                _editor.Text = text;
            }
            finally
            {
                _refreshing = false;
            }
        }

        RebuildPreview(text);
        if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
        {
            _previewScroller.IsVisible = false;
            _editor.IsVisible = true;
        }
        else if (!_editor.IsFocused)
        {
            _editor.IsVisible = false;
            _previewScroller.IsVisible = true;
        }
    }

    private void EnterEditor()
    {
        if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
        {
            return;
        }

        _previewScroller.IsVisible = false;
        _editor.IsVisible = true;
        _editor.Focus();
        _editor.CaretIndex = _editor.Text?.Length ?? 0;
    }

    private void ExitEditor()
    {
        if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
        {
            return;
        }

        RebuildPreview(_editor.Text ?? string.Empty);
        _editor.IsVisible = false;
        _previewScroller.IsVisible = true;
    }

    private void RebuildPreview(string source)
    {
        _preview.Children.Clear();
        if (string.IsNullOrWhiteSpace(source))
        {
            _preview.Children.Add(new TextBlock
            {
                Text = TextCatalog.Current.NotePlaceholder,
                Foreground = _palette.WeakTextBrush,
                Opacity = 0.62,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var inCode = false;
        var codeLines = new List<string>();
        foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw ?? string.Empty;
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    AddCodeBlock(codeLines);
                    codeLines.Clear();
                    inCode = false;
                }
                else
                {
                    inCode = true;
                }
                continue;
            }

            if (inCode)
            {
                codeLines.Add(line);
                continue;
            }

            AddPreviewLine(line);
        }

        if (codeLines.Count > 0)
        {
            AddCodeBlock(codeLines);
        }
    }

    private void AddPreviewLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            _preview.Children.Add(new Border { Height = 7 });
            return;
        }

        var trimmed = line.TrimStart();
        var indent = Math.Min(24, (line.Length - trimmed.Length) * 4);
        if (trimmed.StartsWith("### ", StringComparison.Ordinal))
        {
            AddText(trimmed[4..], 14, FontWeight.SemiBold, indent, 4);
            return;
        }
        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            AddText(trimmed[3..], 16, FontWeight.SemiBold, indent, 5);
            return;
        }
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            AddText(trimmed[2..], 19, FontWeight.Bold, indent, 7);
            return;
        }
        if (trimmed.StartsWith("- [ ] ", StringComparison.OrdinalIgnoreCase))
        {
            AddText("☐  " + trimmed[6..], BaseFontSize(), FontWeight.Normal, indent, 1);
            return;
        }
        if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            AddText("☑  " + trimmed[6..], BaseFontSize(), FontWeight.Normal, indent, 1, 0.6);
            return;
        }
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
            trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            AddText("•  " + trimmed[2..], BaseFontSize(), FontWeight.Normal, indent + 5, 1);
            return;
        }
        if (trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            _preview.Children.Add(new Border
            {
                BorderBrush = _palette.ActiveBrush,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(8, 2, 0, 2),
                Margin = new Thickness(indent, 2, 0, 2),
                Child = new TextBlock
                {
                    Text = trimmed[2..],
                    Foreground = _palette.WeakTextBrush,
                    FontSize = BaseFontSize(),
                    TextWrapping = TextWrapping.Wrap
                }
            });
            return;
        }

        AddText(StripSimpleInlineMarkers(trimmed), BaseFontSize(), FontWeight.Normal, indent, 1);
    }

    private void AddCodeBlock(IReadOnlyCollection<string> lines)
    {
        _preview.Children.Add(new Border
        {
            Background = _palette.HoverBrush,
            BorderBrush = _palette.DividerBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 3),
            Child = new TextBlock
            {
                Text = string.Join(Environment.NewLine, lines),
                Foreground = _palette.TextBrush,
                FontSize = Math.Max(11, BaseFontSize() - 1),
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            }
        });
    }

    private void AddText(
        string text,
        double fontSize,
        FontWeight weight,
        double left,
        double vertical,
        double opacity = 1)
    {
        _preview.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = _palette.TextBrush,
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(left, vertical, 0, vertical),
            Opacity = opacity
        });
    }

    private double BaseFontSize() => VisualTextSizes.FontSize(
        13,
        _state.NoteTextSize,
        OverallFontScales.Normalize(_state.Zoom) *
        OverallFontScales.Normalize(_paper.TextZoom));

    private static string StripSimpleInlineMarkers(string text) => text
        .Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("__", string.Empty, StringComparison.Ordinal)
        .Replace("`", string.Empty, StringComparison.Ordinal);
}
