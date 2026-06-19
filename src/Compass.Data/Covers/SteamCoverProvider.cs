using Compass.Core.Covers;

namespace Compass.Data.Covers;

public sealed class SteamCoverProvider : ICoverProvider
{
    // LIVE-VERIFY: Steam capsule host/path. These public (keyless) forms are the long-standing
    // CDN URLs; confirm host at build if a fetch 404s for known-good appids.
    public static string PortraitUrl(int appId)  => $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
    public static string LandscapeUrl(int appId) => $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";

    private readonly ICoverDownloader _downloader;
    private readonly string _cacheDir;

    public SteamCoverProvider(ICoverDownloader downloader, string cacheDir)
    {
        _downloader = downloader;
        _cacheDir = cacheDir;
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string?> GetCoverPathAsync(int appId, CancellationToken ct)
    {
        var path = Path.Combine(_cacheDir, $"{appId}.jpg");
        if (File.Exists(path)) return path;

        foreach (var url in new[] { PortraitUrl(appId), LandscapeUrl(appId) })
        {
            var bytes = await _downloader.TryDownloadAsync(url, ct);
            if (bytes is { Length: > 0 })
            {
                await File.WriteAllBytesAsync(path, bytes, ct);
                return path;
            }
        }
        return null;
    }

    public static string DefaultCacheDir()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Compass", "covers");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
