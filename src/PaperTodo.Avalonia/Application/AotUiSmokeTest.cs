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
/// loading user state. This intentionally tests basic editability rather than merely window startup.
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
                    Complete(todoWindow, noteWindow, todoPaper, notePaper, desktop)));
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

    private static void Complete(
        PaperSurfaceWindow todoWindow,
        PaperSurfaceWindow noteWindow,
        PaperData todoPaper,
        PaperData notePaper,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        var todoHandle = todoWindow.TryGetPlatformHandle();
        var noteHandle = noteWindow.TryGetPlatformHandle();
        var todoEditor = todoWindow
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.IsVisible && box.MaxLength == 5000);
        var noteEditor = noteWindow
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(box => box.IsVisible && box.MaxLength == 100000);

        var succeeded = todoWindow.IsVisible &&
            noteWindow.IsVisible &&
            todoHandle is not null &&
            todoHandle.Handle != IntPtr.Zero &&
            string.Equals(todoHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase) &&
            noteHandle is not null &&
            noteHandle.Handle != IntPtr.Zero &&
            string.Equals(noteHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase) &&
            todoPaper.Items.Count == 1 &&
            todoEditor is not null &&
            noteEditor is not null;

        if (succeeded)
        {
            todoEditor!.Text = "Todo basic editing works";
            noteEditor!.Text = "Note basic editing works";
            succeeded = string.Equals(
                    todoPaper.Items[0].Text,
                    "Todo basic editing works",
                    StringComparison.Ordinal) &&
                string.Equals(
                    notePaper.Content,
                    "Note basic editing works",
                    StringComparison.Ordinal);
        }

        todoWindow.Close();
        noteWindow.Close();
        desktop.Shutdown(succeeded ? 0 : 1);
    }
}