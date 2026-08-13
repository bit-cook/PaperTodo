using Avalonia.Media.Imaging;
using System.Globalization;
using System.Security.Cryptography;

namespace PaperTodo.Avalonia.Papers;

internal sealed class AvaloniaNoteImageStore : IDisposable
{
    private const int MaxStoredDimension = 4096;
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private const int MaxImageCount = 1000;
    private const long MaxTotalImageBytes = 120L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Dictionary<string, NoteImageAsset> _assets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Bitmap> _bitmaps = new(StringComparer.Ordinal);
    private readonly HashSet<string> _corrupted = new(StringComparer.Ordinal);
    private LmdbImageDatabase? _database;
    private long _totalImageBytes;
    private int _nextImageNumber = 1;
    private bool _loaded;
    private bool _writeDisabled;
    private bool _disposed;

    public string FilePath { get; } = Path.Combine(AppContext.BaseDirectory, "note-assets.lmdb");

    public bool IsWriteDisabled => _writeDisabled;

    public void Load()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            if (!File.Exists(FilePath))
            {
                return;
            }

            try
            {
                OpenDatabaseLocked();
            }
            catch
            {
                _writeDisabled = true;
                _database?.Dispose();
                _database = null;
            }
        }
    }

    public bool TryGetAsset(string imageId, out NoteImageAsset asset)
    {
        lock (_gate)
        {
            EnsureLoadedLocked();
            return _assets.TryGetValue(imageId, out asset!);
        }
    }

    public bool TryGetBitmap(string imageId, out Bitmap bitmap)
    {
        lock (_gate)
        {
            EnsureLoadedLocked();
            if (_corrupted.Contains(imageId) ||
                !_assets.TryGetValue(imageId, out var asset))
            {
                bitmap = null!;
                return false;
            }

            if (_bitmaps.TryGetValue(imageId, out bitmap!))
            {
                return true;
            }

            if (_database is null ||
                !_database.TryReadBlob(imageId, out var bytes) ||
                bytes.Length != asset.ByteLength ||
                !VerifySha256(bytes, asset.Sha256))
            {
                _corrupted.Add(imageId);
                bitmap = null!;
                return false;
            }

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                bitmap = new Bitmap(stream);
                _bitmaps[imageId] = bitmap;
                return true;
            }
            catch
            {
                // Codec/OOM failures are presentation failures, not proof that the LMDB record is
                // corrupt. Keep the durable bytes untouched so another run can retry them.
                bitmap = null!;
                return false;
            }
        }
    }

    public NoteImageAsset ImportImageFile(string noteId, string path)
    {
        if (string.IsNullOrWhiteSpace(noteId))
        {
            throw new InvalidDataException("The target note id is empty.");
        }
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("The selected image file does not exist.", path);
        }
        if (!IsSupportedImageFile(path))
        {
            throw new InvalidDataException("The selected image format is not supported.");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length is <= 0 or > MaxImageBytes)
        {
            throw new InvalidDataException($"Images must be 8 MB or smaller.");
        }

        var mime = DetectMime(bytes)
            ?? throw new InvalidDataException("The selected image format is not supported.");
        int width;
        int height;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var bitmap = new Bitmap(stream);
            width = bitmap.PixelSize.Width;
            height = bitmap.PixelSize.Height;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("The selected image could not be decoded.", exception);
        }

        if (width <= 0 || height <= 0 ||
            width > MaxStoredDimension || height > MaxStoredDimension)
        {
            throw new InvalidDataException($"Images must be no larger than {MaxStoredDimension} × {MaxStoredDimension} pixels.");
        }

        lock (_gate)
        {
            EnsureLoadedLocked();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_writeDisabled)
            {
                throw new InvalidOperationException("The note image database is unavailable for writing.");
            }
            if (_assets.Count >= MaxImageCount)
            {
                throw new InvalidDataException($"The image library is limited to {MaxImageCount} images.");
            }
            if (_totalImageBytes + bytes.Length > MaxTotalImageBytes)
            {
                throw new InvalidDataException("The note image library has reached its 120 MB storage limit.");
            }

            EnsureDatabaseLocked();
            var id = AllocateImageIdLocked();
            var asset = new NoteImageAsset
            {
                Id = id,
                NoteId = noteId,
                Mime = mime,
                Width = width,
                Height = height,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                ByteLength = bytes.Length,
                OriginalName = Path.GetFileName(path),
                CreatedAt = DateTimeOffset.UtcNow
            };
            _database!.AddImages(
                [new LmdbImageWrite(asset, bytes)],
                _nextImageNumber);
            _assets.Add(id, asset);
            _totalImageBytes += bytes.Length;
            return asset;
        }
    }

    private void EnsureLoadedLocked()
    {
        if (!_loaded)
        {
            Load();
        }
    }

    private void EnsureDatabaseLocked()
    {
        if (_database is not null)
        {
            return;
        }
        OpenDatabaseLocked();
    }

    private void OpenDatabaseLocked()
    {
        _database?.Dispose();
        _database = LmdbImageDatabase.Open(FilePath);
        var index = _database.ReadIndex();

        _assets.Clear();
        _corrupted.Clear();
        _totalImageBytes = 0;
        foreach (var asset in index.Assets)
        {
            if (!MarkdownImageReferences.IsValidImageId(asset.Id) ||
                asset.ByteLength <= 0 ||
                asset.ByteLength > MaxImageBytes ||
                asset.Width <= 0 || asset.Height <= 0 ||
                asset.Width > MaxStoredDimension || asset.Height > MaxStoredDimension)
            {
                _corrupted.Add(asset.Id);
                continue;
            }
            _assets[asset.Id] = asset;
            _totalImageBytes += asset.ByteLength;
        }
        _corrupted.UnionWith(index.CorruptedImageIds);
        _nextImageNumber = Math.Max(1, index.NextImageNumber);
        _writeDisabled = _assets.Count > MaxImageCount ||
            _totalImageBytes > MaxTotalImageBytes;
    }

    private string AllocateImageIdLocked()
    {
        while (_nextImageNumber <= 99_999_999)
        {
            var number = _nextImageNumber++;
            var id = number < 1000
                ? number.ToString("000", CultureInfo.InvariantCulture)
                : number.ToString(CultureInfo.InvariantCulture);
            if (!_assets.ContainsKey(id) && !_corrupted.Contains(id))
            {
                return id;
            }
        }
        throw new InvalidOperationException("The note image id space is exhausted.");
    }

    private static bool VerifySha256(byte[] bytes, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedImageFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp";

    private static string? DetectMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }
        if (bytes.Length >= 6 &&
            (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
        {
            return "image/bmp";
        }
        if (bytes.Length >= 4 &&
            ((bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2A && bytes[3] == 0x00) ||
             (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0x00 && bytes[3] == 0x2A)))
        {
            return "image/tiff";
        }
        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }
        return null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            foreach (var bitmap in _bitmaps.Values)
            {
                bitmap.Dispose();
            }
            _bitmaps.Clear();
            _database?.Dispose();
            _database = null;
        }
    }
}

internal static class AvaloniaNoteImageRuntime
{
    private static readonly object Gate = new();
    private static AvaloniaNoteImageStore? _store;

    public static AvaloniaNoteImageStore Store
    {
        get
        {
            lock (Gate)
            {
                _store ??= CreateStore();
                return _store;
            }
        }
    }

    private static AvaloniaNoteImageStore CreateStore()
    {
        var store = new AvaloniaNoteImageStore();
        store.Load();
        return store;
    }

    public static void DisposeShared()
    {
        lock (Gate)
        {
            _store?.Dispose();
            _store = null;
        }
    }
}
