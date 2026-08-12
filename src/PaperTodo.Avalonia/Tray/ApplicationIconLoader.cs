using Avalonia.Controls;
using Avalonia.Platform;

namespace PaperTodo.Avalonia.Tray;

internal static class ApplicationIconLoader
{
    private static readonly Uri EmbeddedIconUri = new(
        "avares://PaperTodo.Avalonia/Assets/PaperTodo.ico");

    public static WindowIcon Load()
    {
        var externalPath = Path.Combine(AppContext.BaseDirectory, "PaperTodo.ico");
        if (File.Exists(externalPath))
        {
            try
            {
                using var external = File.OpenRead(externalPath);
                return new WindowIcon(external);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        using var embedded = AssetLoader.Open(EmbeddedIconUri);
        return new WindowIcon(embedded);
    }
}
