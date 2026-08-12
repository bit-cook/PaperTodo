using System.Security.Cryptography;

namespace PaperTodo.Avalonia.Application;

internal static class AotLmdbSmokeTest
{
    private const string Argument = "--aot-smoke-lmdb";

    public static bool IsRequested(IReadOnlyList<string> arguments) =>
        arguments.Count == 1 &&
        string.Equals(arguments[0], Argument, StringComparison.OrdinalIgnoreCase);

    public static int Run()
    {
        var smokeDirectory = Path.Combine(
            Path.GetTempPath(),
            $"PaperTodo-aot-smoke-lmdb-{Guid.NewGuid():N}");
        var exitCode = 0;

        try
        {
            Directory.CreateDirectory(smokeDirectory);
            var databasePath = Path.Combine(smokeDirectory, "images.lmdb");
            var blob = new byte[] { 0x5a };
            var asset = new NoteImageAsset
            {
                Id = "1",
                NoteId = "aot-smoke",
                Mime = "application/octet-stream",
                Width = 1,
                Height = 1,
                Sha256 = Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant(),
                ByteLength = blob.Length,
                CreatedAt = DateTimeOffset.UnixEpoch,
                OriginalName = "aot-smoke.bin"
            };

            using (var database = LmdbImageDatabase.Open(databasePath))
            {
                database.AddImages([new LmdbImageWrite(asset, blob)], nextImageNumber: 2);
                var index = database.ReadIndex();
                if (index.Assets.Count != 1 ||
                    index.CorruptedImageIds.Count != 0 ||
                    index.NextImageNumber != 2 ||
                    !string.Equals(index.Assets[0].Id, asset.Id, StringComparison.Ordinal) ||
                    index.Assets[0].ByteLength != blob.Length)
                {
                    throw new InvalidDataException("The AOT LMDB smoke index did not round-trip.");
                }

                if (!database.TryReadBlob(asset.Id, out var storedBlob) ||
                    !storedBlob.AsSpan().SequenceEqual(blob))
                {
                    throw new InvalidDataException("The AOT LMDB smoke blob did not round-trip.");
                }
            }

        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"PaperTodo LMDB AOT smoke failed: {exception}");
            exitCode = 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(smokeDirectory))
                {
                    Directory.Delete(smokeDirectory, recursive: true);
                }
            }
            catch (Exception cleanupException)
            {
                Console.Error.WriteLine(
                    $"PaperTodo LMDB AOT smoke cleanup failed: {cleanupException}");
                exitCode = 1;
            }
        }

        return exitCode;
    }
}
