using System.Net.Http.Json;

namespace Compass.Data.Igdb;

public sealed class TwitchTokenProvider
{
    private readonly HttpClient _http;
    private readonly string _clientId, _clientSecret;
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TwitchTokenProvider(HttpClient http, string clientId, string clientSecret)
        => (_http, _clientId, _clientSecret) = (http, clientId, clientSecret);

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        // Fast path: token still valid
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

        await _gate.WaitAsync(ct);
        try
        {
            // Double-check after acquiring gate
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

            var url = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}" +
                      $"&client_secret={_clientSecret}&grant_type=client_credentials";
            using var resp = await _http.PostAsync(url, content: null, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<TwitchTokenResponse>(ct)
                       ?? throw new InvalidOperationException("Empty Twitch token response");
            _token = body.access_token;
            // Refresh 60s before actual expiry to avoid races
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(body.expires_in - 60);
            return _token;
        }
        finally { _gate.Release(); }
    }

    private sealed record TwitchTokenResponse(string access_token, int expires_in, string token_type);
}
