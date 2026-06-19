namespace Compass.Data.Covers;

public sealed class HttpCoverDownloader : ICoverDownloader
{
    private readonly HttpClient _http;
    public HttpCoverDownloader(HttpClient http) => _http = http;

    public async Task<byte[]?> TryDownloadAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch { return null; }
    }
}
