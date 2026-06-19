using System.Text.Json;
using Compass.Core.Sync;

namespace Compass.Data.Steam;

public sealed class SteamClient : ISteamClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey, _steamId64;

    public SteamClient(HttpClient http, string apiKey, string steamId64)
        => (_http, _apiKey, _steamId64) = (http, apiKey, steamId64);

    public async Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct)
    {
        var url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                  $"?key={_apiKey}&steamid={_steamId64}" +
                  "&include_appinfo=1&include_played_free_games=1&format=json";
        var json = await _http.GetStringAsync(url, ct);
        return ParseOwnedGames(json);
    }

    public static IReadOnlyList<OwnedGame> ParseOwnedGames(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("response", out var resp) ||
            !resp.TryGetProperty("games", out var games) ||
            games.ValueKind != JsonValueKind.Array)
            return Array.Empty<OwnedGame>();

        var list = new List<OwnedGame>();
        foreach (var g in games.EnumerateArray())
        {
            int appId = g.GetProperty("appid").GetInt32();
            string name = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            int forever = g.TryGetProperty("playtime_forever", out var f) ? f.GetInt32() : 0;
            int two = g.TryGetProperty("playtime_2weeks", out var t) ? t.GetInt32() : 0;
            list.Add(new OwnedGame(appId, name, forever, two));
        }
        return list;
    }
}
