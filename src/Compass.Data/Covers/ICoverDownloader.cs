namespace Compass.Data.Covers;

public interface ICoverDownloader
{
    /// Try to download bytes for a URL; null if not available (e.g. 404).
    Task<byte[]?> TryDownloadAsync(string url, CancellationToken ct);
}
