using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PaperTodo.Avalonia.Papers;
using PaperTodo.Avalonia.Tray;

namespace PaperTodo.Avalonia.Application;

/// <summary>
/// Starts real Todo and Note paper surfaces from the published executable, verifies that their
/// actual product editors can mutate the shared models, checks the native HWNDs, and exits without
/// loading user state. Failure codes identify the exact basic interaction contract that failed.
/// Text propagation is verified after a UI frame because Avalonia routed text events are not
/// required to update the model in the same property-set call stack.
/// </summary>
internal static class AotUiSmokeTest
{
    private const string Argument = "--aot-smoke-ui";

    public static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(arguments[0], Argument, StringComparison.OrdinalIgnoreCase);

    public static void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Dispatcher.UIThread.VerifyAccess();

        var state = new AppState
        {
            Theme = "light",
            ColorScheme = ColorSchemes.Warm,
            MarkdownRenderMode = MarkdownRenderModes.Enhanced,
            UseCapsuleMode = true,
            UseDeepCapsuleMode = true
        };
        var todoPaper = new PaperData
        {
            Type = PaperTypes.Todo,
            Title = "Todo smoke",
            Width = PaperLayoutDefaults.TodoDefaultWidth,
            Height = 180,
            IsVisible = true,
            Items = []
        };
        var notePaper = new PaperData
        {
            Type = PaperTypes.Note,
            Title = "Note smoke",
            Width = PaperLayoutDefaults.NoteDefaultWidth,
            Height = 180,
            IsVisible = true,
            Content = string.Empty
        };

        var todoWindow = CreateWindow(todoPaper, state, new PixelPoint(100, 100));
        var noteWindow = CreateWindow(notePaper, state, new PixelPoint(420, 100));
        todoWindow.Icon = ApplicationIconLoader.Load();
        noteWindow.Icon = ApplicationIconLoader.Load();

        var opened = 0;
        void OnOpened()
        {
            opened++;
            if (opened != 2)
            {
                return;
            }

            todoWindow.RequestAnimationFrame(_ =>
                todoWindow.RequestAnimationFrame(_ =>
                    BeginEditingCheck(todoWindow, noteWindow, todoPaper, notePaper, desktop)));
        }

        todoWindow.Opened += (_, _) => OnOpened();
        noteWindow.Opened += (_, _) => OnOpened();
        todoWindow.Show();
        noteWindow.Show();
    }

    private static PaperSurfaceWindow CreateWindow(
        PaperData paper,
        AppState state,
        PixelPoint position) =>
        new(new PaperSurfaceDescriptor(
            paper,
            state,
            position,
            new Size(paper.Width, paper.Height),
            IsVisible: true,
            AlwaysOnTop: false));

    private static void BeginEditingCheck(
        PaperSurfaceWindow todoWindow,
        PaperSurfaceWindow noteWindow,
        PaperData todoPaper,
        PaperData notePaper,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var failure = ValidateStructure(todoWindow, noteWindow, todoPaper, out var todoEditor, out var noteEditor);
        if (failure != 0)
        {
            Finish(todoWindow, noteWindow, desktop, failure);
            return;
        }

        todoEditor!.Text = "Todo basic editing works";
        todoWindow.RequestAnimationFrame(_ =>
        {
            if (!string.Equals(
                    todoPaper.Items[0].Text,
                    "Todo basic editing works",
                    StringComparison.Ordinal))
            {
                Finish(todoWindow, noteWindow, desktop, 15);
                return;
            }

            noteEditor!.Text = "Note basic editing works";
            noteWindow.RequestAnimationFrame(_ =>
            {
                var noteSynced = string.Equals(
                    notePaper.Content,
                    "Note basic editing works",
                    StringComparison.Ordinal);
                Finish(todoWindow, noteWindow, desktop, noteSynced ? 0 : 16);
            });
        });
    }

    private static int ValidateStructure(
        PaperSurfaceWindow todoWindow,
        PaperSurfaceWindow noteWindow,
        PaperData todoPaper,
        out TextBox? todoEditor,
        out TextBox? noteEditor)
    {
        todoEditor = null;
        noteEditor = null;
        var todoHandle = todoWindow.TryGetPlatformHandle();
        var noteHandle = noteWindow.TryGetPlatformHandle();

        if (!todoWindow.IsVisible || !noteWindow.IsVisible)
        {
            return 10;
        }
        if (todoHandle is null || todoHandle.Handle == IntPtr.Zero ||
            !string.Equals(todoHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase) ||
            noteHandle is null || noteHandle.Handle == IntPtr.Zero ||
            !string.Equals(noteHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
        {
            return 11;
        }
        if (todoPaper.Items.Count != 1)
        {
            return 12;
        }

        todoEditor = todoWindow
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.IsVisible && box.MaxLength == 5000);
        noteEditor = noteWindow
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.IsVisible && box.MaxLength == 100000);
        if (todoEditor is null)
        {
            return 13;
        }
        return noteEditor is null ? 14 : 0;
    }

    private static void Finish(
        PaperSurfaceWindow todoWindow,
        PaperSurfaceWindow noteWindow,
        IClassicDesktopStyleApplicationLifetime desktop,
        int exitCode)
    {
        todoWindow.Close();
        noteWindow.Close();
        desktop.Shutdown(exitCode);
    }
}