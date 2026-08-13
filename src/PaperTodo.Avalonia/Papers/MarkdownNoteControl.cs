using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PaperTodo.Avalonia.Localization;

namespace PaperTodo.Avalonia.Papers;

internal sealed class MarkdownNoteControl : Grid
{
    private const int MaximumLength = 100000;
    private const double PreviewImageMaximumWidth = 520;

    private readonly PaperData _paper;
    private readonly AppState _state;
    private readonly PaperThemePalette _palette;
    private readonly Action _changed;
    private readonly TextBox _editor;
    private readonly ScrollViewer _previewScroller;
    private readonly StackPanel _preview;
    private readonly TextBlock _imageStatus;
    private readonly Button _modeButton;
    private bool _refreshing;
    private bool _editing;
    private bool _initialized;

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

        RowDefinitions = new RowDefinitions("*,Auto");
        Background = Brushes.Transparent;

        _preview = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(11, 8),
            Background = Brushes.Transparent
        };
        _preview.PointerPressed += (_, e) =>
        {
            if (_state.MarkdownRenderMode == MarkdownRenderModes.Off ||
                !e.GetCurrentPoint(_preview).Properties.IsLeftButtonPressed)
            {
                return;
            }

            SetEditing(editing: true, focus: true, moveCaretToEnd: true);
            e.Handled = true;
        };

        _previewScroller = new ScrollViewer
        {
            Content = _preview,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent
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
            if (e.Key == Key.Escape && _state.MarkdownRenderMode != MarkdownRenderModes.Off)
            {
                SetEditing(editing: false, focus: false);
                e.Handled = true;
            }
        };

        Children.Add(_previewScroller);
        Children.Add(_editor);

        _modeButton = new Button
        {
            Content = "✎",
            Padding = new Thickness(7, 1),
            Margin = new Thickness(8, 1, 2, 5),
            MinWidth = 30,
            MinHeight = 24,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _modeButton.Click += (_, _) =>
        {
            if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
            {
                _editor.Focus();
                return;
            }

            SetEditing(!_editing, focus: !_editing, moveCaretToEnd: false);
        };

        var imageButton = new Button
        {
            Content = "▧+",
            Padding = new Thickness(7, 1),
            Margin = new Thickness(2, 1, 4, 5),
            MinHeight = 24,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Foreground = _palette.WeakTextBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        imageButton.Click += async (_, _) => await PickImagesAsync();
        if (_state.EnableToolTips)
        {
            ToolTip.SetTip(imageButton, TextCatalog.Current.InsertImage);
        }

        _imageStatus = new TextBlock
        {
            Foreground = _palette.DangerBrush,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 4)
        };
        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };
        toolbar.Children.Add(_modeButton);
        toolbar.Children.Add(imageButton);
        Grid.SetColumn(imageButton, 1);
        toolbar.Children.Add(_imageStatus);
        Grid.SetColumn(_imageStatus, 2);
        Children.Add(toolbar);
        Grid.SetRow(toolbar, 1);

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
        if (!_initialized)
        {
            // A brand-new blank note must be immediately writable. Existing notes retain the
            // PaperTodo preview-first presentation, but a single click enters editing.
            _editing = _state.MarkdownRenderMode == MarkdownRenderModes.Off ||
                string.IsNullOrEmpty(text);
            _initialized = true;
        }
        else if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
        {
            _editing = true;
        }

        ApplyEditingPresentation();
    }

    private void SetEditing(
        bool editing,
        bool focus,
        bool moveCaretToEnd = false)
    {
        if (_state.MarkdownRenderMode == MarkdownRenderModes.Off)
        {
            editing = true;
        }

        if (!editing)
        {
            RebuildPreview(_editor.Text ?? string.Empty);
        }

        _editing = editing;
        ApplyEditingPresentation();
        if (!focus || !_editing)
        {
            return;
        }

        _editor.Focus();
        if (moveCaretToEnd)
        {
            _editor.CaretIndex = _editor.Text?.Length ?? 0;
        }
        else
        {
            _editor.CaretIndex = Math.Clamp(
                _editor.CaretIndex,
                0,
                _editor.Text?.Length ?? 0);
        }
    }

    private void ApplyEditingPresentation()
    {
        _editor.IsVisible = _editing;
        _previewScroller.IsVisible = !_editing;
        _modeButton.Content = _editing ? "◎" : "✎";
        _modeButton.IsVisible = _state.MarkdownRenderMode != MarkdownRenderModes.Off;
    }

    private async Task PickImagesAsync()
    {
        _imageStatus.Text = string.Empty;
        var topLevel = TopLevel.GetTopLevel(this);
        var provider = topLevel?.StorageProvider;
        if (provider?.CanOpen != true)
        {
            _imageStatus.Text = TextCatalog.Current.ImageImportUnavailable;
            return;
        }

        var selected = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = TextCatalog.Current.InsertImage,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(TextCatalog.Current.ImageFiles)
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp"]
                }
            ]
        });
        if (selected.Count == 0)
        {
            return;
        }

        var references = new List<string>(selected.Count);
        try
        {
            foreach (var selectedFile in selected)
            {
                using var file = selectedFile;
                var path = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var asset = AvaloniaNoteImageRuntime.Store.ImportImageFile(_paper.Id, path);
                references.Add(MarkdownImageReferences.CreateReference(asset.Id));
            }
        }
        catch (Exception exception)
        {
            _imageStatus.Text = exception.Message;
        }

        if (references.Count == 0)
        {
            return;
        }

        InsertImageReferences(references);
        SetEditing(editing: true, focus: true, moveCaretToEnd: false);
    }

    private void InsertImageReferences(IReadOnlyList<string> references)
    {
        var insertion = string.Join(Environment.NewLine, references);
        var current = _editor.Text ?? PaperTextCodec.ToEditorText(_paper);
        var caret = _editing
            ? Math.Clamp(_editor.CaretIndex, 0, current.Length)
            : current.Length;

        var prefix = caret > 0 && current[caret - 1] is not '\r' and not '\n'
            ? Environment.NewLine
            : string.Empty;
        var suffix = caret < current.Length && current[caret] is not '\r' and not '\n'
            ? Environment.NewLine
            : string.Empty;
        var inserted = prefix + insertion + suffix;
        var updated = current.Insert(caret, inserted);

        _refreshing = true;
        try
        {
            _editor.Text = updated;
            _editor.CaretIndex = Math.Min(updated.Length, caret + inserted.Length);
        }
        finally
        {
            _refreshing = false;
        }

        PaperTextCodec.ApplyEditorText(_paper, updated);
        _changed();
        RebuildPreview(updated);
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
        if (MarkdownImageReferences.TryParseReferenceLine(trimmed, out var imageReference))
        {
            AddImage(imageReference);
            return;
        }

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

    private void AddImage(MarkdownImageReference reference)
    {
        var store = AvaloniaNoteImageRuntime.Store;
        if (!store.TryGetAsset(reference.ImageId, out var asset) ||
            !store.TryGetBitmap(reference.ImageId, out var bitmap))
        {
            AddText(
                $"[image {reference.ImageId} unavailable]",
                Math.Max(10, BaseFontSize() - 1),
                FontWeight.Normal,
                0,
                3,
                0.65);
            return;
        }

        var desiredWidth = ResolveImageWidth(reference.DisplayOptions, asset);
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = desiredWidth,
            MaxHeight = Math.Min(720, Math.Max(80, asset.Height)),
            Margin = new Thickness(0, 4, 0, 5)
        };
        _preview.Children.Add(image);
    }

    private static double ResolveImageWidth(
        MarkdownImageDisplayOptions options,
        NoteImageAsset asset)
    {
        double requested;
        if (options.WidthAttribute is { } widthAttribute)
        {
            requested = widthAttribute.IsPercent
                ? PreviewImageMaximumWidth * widthAttribute.Value / 100.0
                : widthAttribute.Value;
        }
        else if (options.LabelWidth is { } labelWidth)
        {
            requested = labelWidth;
        }
        else if (options.LabelScalePercent is { } scale)
        {
            requested = asset.Width * scale / 100.0;
        }
        else
        {
            requested = asset.Width;
        }

        return Math.Clamp(requested, 48, PreviewImageMaximumWidth);
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