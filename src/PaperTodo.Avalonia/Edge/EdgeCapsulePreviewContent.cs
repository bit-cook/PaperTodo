using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PaperTodo.Avalonia.Papers;
using System.Diagnostics;

namespace PaperTodo.Avalonia.Edge;

internal static class EdgeCapsulePreviewContent
{
    public static EdgeCapsulePreviewSize Measure(
        PaperData paper,
        MonitorGeometry monitor)
    {
        var maximumWidth = Math.Max(
            EdgeCapsulePreviewSize.MinimumWidthDip,
            monitor.LocalWorkAreaDip.Width - 16);
        var maximumHeight = Math.Max(
            EdgeCapsulePreviewSize.MinimumHeightDip,
            monitor.LocalWorkAreaDip.Height - 16);

        double width;
        double height;
        if (paper.Type == PaperTypes.Todo)
        {
            var visibleRows = Math.Clamp(paper.Items.Count, 1, 9);
            width = 340;
            height = 60 + visibleRows * 31;
        }
        else if (PaperTextCodec.CanEditBody(paper))
        {
            var text = PaperTextCodec.ToEditorText(paper);
            var lineCount = Math.Clamp(
                text.Count(character => character == '\n') + 1,
                1,
                14);
            width = 370;
            height = 82 + lineCount * 18;
        }
        else
        {
            width = 320;
            height = 170;
        }

        return new EdgeCapsulePreviewSize(width, height)
            .Normalize(maximumWidth, maximumHeight);
    }

    public static Control Create(
        PaperData paper,
        AppState state,
        Action changed,
        Action<string> openLinkedPaper)
    {
        var palette = PaperThemePalette.Resolve(state);
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(11, 9)
        };

        var header = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(paper.Title)
                ? paper.Type == PaperTypes.Todo ? "Todo" : "Note"
                : paper.Title,
            Foreground = palette.TextBrush,
            FontSize = VisualTextSizes.FontSize(
                13,
                state.TitleTextSize,
                OverallFontScales.Normalize(state.Zoom)),
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(2, 0, 2, 6)
        };
        root.Children.Add(header);

        Control body = paper.Type == PaperTypes.Todo
            ? BuildTodoPreview(paper, state, palette, changed, openLinkedPaper)
            : PaperTextCodec.CanEditBody(paper)
                ? BuildMarkdownPreview(paper, state, palette)
                : BuildPluginFallback(paper, palette);
        root.Children.Add(body);
        Grid.SetRow(body, 1);
        return root;
    }

    private static Control BuildTodoPreview(
        PaperData paper,
        AppState state,
        PaperThemePalette palette,
        Action changed,
        Action<string> openLinkedPaper)
    {
        var panel = new StackPanel { Spacing = 1 };
        var items = paper.Items
            .OrderBy(item => item.Order)
            .ToArray();
        if (items.Length == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "—",
                Foreground = palette.WeakTextBrush,
                Margin = new Thickness(3, 5),
                Opacity = 0.7
            });
        }

        var metrics = TodoVisualSizes.Metrics(state.TodoVisualSize, state.Zoom);
        foreach (var item in items)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                MinHeight = Math.Max(25, metrics.RowMinHeight),
                ColumnSpacing = 5
            };
            var check = new CheckBox
            {
                IsChecked = item.Done,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = palette.ActiveBrush
            };
            check.IsCheckedChanged += (_, _) =>
            {
                var done = check.IsChecked == true;
                if (item.Done == done)
                {
                    return;
                }

                item.Done = done;
                if (done)
                {
                    item.ReminderAt = null;
                    item.ReminderTriggered = false;
                }

                if (done && state.AutoClearCompletedTodos)
                {
                    paper.Items.RemoveAll(candidate => candidate.Id == item.Id);
                    if (paper.Items.Count == 0)
                    {
                        paper.Items.Add(new PaperItem());
                    }
                }
                else if (state.AutoMoveCompletedTodosToBottom)
                {
                    var ordered = paper.Items
                        .OrderBy(candidate => candidate.Order)
                        .ToList();
                    ordered.RemoveAll(candidate => candidate.Id == item.Id);
                    if (done)
                    {
                        ordered.Add(item);
                    }
                    else
                    {
                        var firstDone = ordered.FindIndex(candidate => candidate.Done);
                        ordered.Insert(firstDone < 0 ? ordered.Count : firstDone, item);
                    }
                    paper.Items = ordered;
                }

                TodoRules.NormalizeOrders(paper.Items);
                changed();
            };
            row.Children.Add(check);

            var text = new TextBlock
            {
                Text = item.Text ?? string.Empty,
                Foreground = item.Done ? palette.WeakTextBrush : palette.TextBrush,
                FontSize = metrics.TextFontSize,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = item.Done ? 0.64 : 1
            };
            row.Children.Add(text);
            Grid.SetColumn(text, 1);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (item.ReminderAt.HasValue && !item.ReminderTriggered)
            {
                actions.Children.Add(new TextBlock
                {
                    Text = "◷",
                    Foreground = palette.ActiveBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11
                });
            }

            if (!string.IsNullOrWhiteSpace(item.LinkedPaperId))
            {
                var button = LinkButton(palette, "↗");
                var linkedPaperId = item.LinkedPaperId;
                button.Click += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(linkedPaperId))
                    {
                        openLinkedPaper(linkedPaperId);
                    }
                };
                actions.Children.Add(button);
            }
            else if (!string.IsNullOrWhiteSpace(item.LinkedPath))
            {
                var button = LinkButton(palette, "↗");
                var path = item.LinkedPath;
                button.Click += (_, _) => OpenPath(path);
                actions.Children.Add(button);
            }

            row.Children.Add(actions);
            Grid.SetColumn(actions, 2);
            panel.Children.Add(row);
        }

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Button LinkButton(PaperThemePalette palette, string glyph) => new()
    {
        Content = glyph,
        Padding = new Thickness(3, 0),
        MinWidth = 22,
        MinHeight = 22,
        Background = Brushes.Transparent,
        BorderThickness = default,
        Foreground = palette.WeakTextBrush,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            (!File.Exists(path) && !Directory.Exists(path)))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Trace.TraceWarning(
                "PaperTodo Avalonia could not open linked path '{0}': {1}",
                path,
                exception.Message);
        }
    }

    private static Control BuildMarkdownPreview(
        PaperData paper,
        AppState state,
        PaperThemePalette palette)
    {
        var source = PaperTextCodec.ToEditorText(paper);
        var panel = new StackPanel { Spacing = 2 };
        if (string.IsNullOrWhiteSpace(source))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "—",
                Foreground = palette.WeakTextBrush,
                Opacity = 0.7
            });
        }
        else
        {
            var inCode = false;
            var code = new List<string>();
            foreach (var raw in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var line = raw ?? string.Empty;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCode)
                    {
                        panel.Children.Add(CodeBlock(code, palette));
                        code.Clear();
                    }
                    inCode = !inCode;
                    continue;
                }

                if (inCode)
                {
                    code.Add(line);
                    continue;
                }

                panel.Children.Add(MarkdownLine(trimmed, state, palette));
            }

            if (code.Count > 0)
            {
                panel.Children.Add(CodeBlock(code, palette));
            }
        }

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static Control MarkdownLine(
        string line,
        AppState state,
        PaperThemePalette palette)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new Border { Height = 6 };
        }

        var baseSize = VisualTextSizes.FontSize(
            12,
            state.NoteTextSize,
            OverallFontScales.Normalize(state.Zoom));
        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            return TextLine(line[4..], palette.TextBrush, baseSize + 1, FontWeight.SemiBold);
        }
        if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            return TextLine(line[3..], palette.TextBrush, baseSize + 2, FontWeight.SemiBold);
        }
        if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            return TextLine(line[2..], palette.TextBrush, baseSize + 4, FontWeight.Bold);
        }
        if (line.StartsWith("- [ ] ", StringComparison.OrdinalIgnoreCase))
        {
            return TextLine("☐  " + line[6..], palette.TextBrush, baseSize, FontWeight.Normal);
        }
        if (line.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            var done = TextLine("☑  " + line[6..], palette.WeakTextBrush, baseSize, FontWeight.Normal);
            done.Opacity = 0.68;
            return done;
        }
        if (line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("* ", StringComparison.Ordinal))
        {
            return TextLine("•  " + line[2..], palette.TextBrush, baseSize, FontWeight.Normal);
        }
        if (line.StartsWith("> ", StringComparison.Ordinal))
        {
            return new Border
            {
                BorderBrush = palette.ActiveBrush,
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(7, 1, 0, 1),
                Child = TextLine(line[2..], palette.WeakTextBrush, baseSize, FontWeight.Normal)
            };
        }

        return TextLine(
            StripInlineMarkers(line),
            palette.TextBrush,
            baseSize,
            state.NoteTextBold ? FontWeight.SemiBold : FontWeight.Normal);
    }

    private static TextBlock TextLine(
        string text,
        IBrush brush,
        double fontSize,
        FontWeight weight) => new()
    {
        Text = text,
        Foreground = brush,
        FontSize = fontSize,
        FontWeight = weight,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(1, 1)
    };

    private static Border CodeBlock(
        IReadOnlyCollection<string> lines,
        PaperThemePalette palette) => new()
    {
        Background = palette.HoverBrush,
        BorderBrush = palette.DividerBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(7, 5),
        Margin = new Thickness(1, 2),
        Child = new TextBlock
        {
            Text = string.Join(Environment.NewLine, lines),
            Foreground = palette.TextBrush,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        }
    };

    private static string StripInlineMarkers(string text) => text
        .Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("__", string.Empty, StringComparison.Ordinal)
        .Replace("~~", string.Empty, StringComparison.Ordinal)
        .Replace("`", string.Empty, StringComparison.Ordinal);

    private static Control BuildPluginFallback(
        PaperData paper,
        PaperThemePalette palette)
    {
        var text = !string.IsNullOrWhiteSpace(paper.BodyHeaderText)
            ? paper.BodyHeaderText
            : !string.IsNullOrWhiteSpace(paper.BodyCapsuleText)
                ? paper.BodyCapsuleText
                : paper.BodyProviderId;
        return new TextBlock
        {
            Text = text,
            Foreground = palette.WeakTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 4),
            Opacity = 0.82
        };
    }
}
