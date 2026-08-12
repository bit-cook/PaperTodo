using Avalonia.Threading;
using System.Diagnostics;
using System.Text;

namespace PaperTodo.Avalonia.Papers;

/// <summary>
/// Bridges one plain Note to the user's system-associated external editor. The temp file is scoped
/// to the paper id and watched for saves; external changes are marshalled back to the Avalonia UI
/// thread before mutating the shared PaperData model.
/// </summary>
internal sealed class ExternalMarkdownEditorSession : IDisposable
{
    private readonly PaperData _paper;
    private readonly Action _changed;
    private readonly string _filePath;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _reloadTimer;
    private bool _disposed;

    public ExternalMarkdownEditorSession(
        PaperData paper,
        string? configuredExtension,
        Action changed)
    {
        _paper = paper;
        _changed = changed;

        var directory = Path.Combine(
            Path.GetTempPath(),
            "PaperTodo",
            "external-markdown");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(
            directory,
            paper.Id + NormalizeExtension(configuredExtension));
        File.WriteAllText(
            _filePath,
            paper.Content ?? string.Empty,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        _reloadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _reloadTimer.Tick += OnReloadTimerTick;

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite |
                NotifyFilters.Size |
                NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnExternalFileChanged;
        _watcher.Created += OnExternalFileChanged;
        _watcher.Renamed += OnExternalFileChanged;
    }

    public string FilePath => _filePath;

    public bool Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            Process.Start(new ProcessStartInfo(_filePath)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "PaperTodo Avalonia failed to open external Markdown editor for '{0}': {1}",
                _filePath,
                exception);
            return false;
        }
    }

    private void OnExternalFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private void OnReloadTimerTick(object? sender, EventArgs e)
    {
        _reloadTimer.Stop();
        if (_disposed || !File.Exists(_filePath))
        {
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(_filePath, Encoding.UTF8);
        }
        catch (IOException)
        {
            // Editors commonly save through a short replace/rename transaction. Re-read once the
            // file has settled instead of treating the transient sharing violation as data loss.
            _reloadTimer.Start();
            return;
        }
        catch (UnauthorizedAccessException)
        {
            _reloadTimer.Start();
            return;
        }

        if (string.Equals(text, _paper.Content, StringComparison.Ordinal))
        {
            return;
        }

        _paper.Content = text;
        _changed();
    }

    private static string NormalizeExtension(string? value)
    {
        var extension = value?.Trim();
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".md";
        }

        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        // The suffix is only a filename/association hint. Do not impose a Markdown-only allowlist;
        // reject only values that cannot safely be part of one temporary filename.
        if (extension.Length > 32 ||
            extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            extension.Contains('/') ||
            extension.Contains('\\'))
        {
            return ".md";
        }

        return extension;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reloadTimer.Stop();
        _reloadTimer.Tick -= OnReloadTimerTick;
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnExternalFileChanged;
        _watcher.Created -= OnExternalFileChanged;
        _watcher.Renamed -= OnExternalFileChanged;
        _watcher.Dispose();

        try
        {
            File.Delete(_filePath);
        }
        catch
        {
            // The external editor may still hold the file open. Temp cleanup is best-effort and
            // must never interfere with shutting down or hiding the paper.
        }
    }
}
