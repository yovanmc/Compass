using System.Text;
using System.Text.Json;
using Compass.Core.Sync;

namespace Compass.Data.Igdb;

public sealed class IgdbClient : IIgdbClient
{
    private const string Base = "https://api.igdb.com/v4/";

    // LIVE-VERIFY: IGDB is migrating external_games from the 'category' enum to an
    // 'external_game_source' field. Steam has historically been category=1. If live
    // appID matching returns ZERO matches, confirm the current field/value at
    // https://api-docs.igdb.com and update this constant/clause. The Tier-2 name-match
    // fallback covers gaps if this drifts.
    private const int SteamExternalCategory = 1;

    private readonly HttpClient _http;
    private readonly TwitchTokenProvider _tokens;
    private readonly string _clientId;

    // IGDB rate limit: ~4 req/s, up to 8 open. Keep 4 concurrent to stay under burst.
    private readonly SemaphoreSlim _rate = new(4, 4);

    public IgdbClient(HttpClient http, TwitchTokenProvider tokens, string clientId)
        => (_http, _tokens, _clientId) = (http, tokens, clientId);

    private async Task<JsonDocument> QueryAsync(string endpoint, string apicalypse, CancellationToken ct)
    {
        await _rate.WaitAsync(ct);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Base + endpoint)
                {
                    Content = new StringContent(apicalypse, Encoding.UTF8, "text/plain")
                };
                req.Headers.Add("Client-ID", _clientId);
                req.Headers.Add("Authorization", $"Bearer {await _tokens.GetTokenAsync(ct)}");

                using var resp = await _http.SendAsync(req, ct);
                if ((int)resp.StatusCode == 429 && attempt < 5)
                {
                    // Exponential-ish backoff on rate-limit
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), ct);
                    continue;
                }
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct);
                return JsonDocument.Parse(json);
            }
        }
        finally { _rate.Release(); }
    }

    public async Task<IReadOnlyList<IgdbMatch>> MatchBySteamAppIdsAsync(
        IReadOnlyList<int> appIds, CancellationToken ct)
    {
        var matches = new List<IgdbMatch>();
        foreach (var chunk in Chunk(appIds, 200))
        {
            var uids = string.Join(',', chunk.Select(a => $"\"{a}\""));
            // Query external_games: uid is the Steam appId string, category=SteamExternalCategory.
            // Request game as an object with id+name so we get both in one call.
            var body =
                $"fields uid, game, game.name; where category = {SteamExternalCategory} & uid = ({uids}); limit 500;";
            using var doc = await QueryAsync("external_games", body, ct);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // uid is a string in external_games (the Steam appId as a string)
                if (!el.TryGetProperty("uid", out var uidEl)) continue;
                if (!int.TryParse(uidEl.GetString(), out var appId)) continue;
                if (!el.TryGetProperty("game", out var gameEl)) continue;

                long igdbId;
                string name = "";
                if (gameEl.ValueKind == JsonValueKind.Object)
                {
                    // game was expanded: { "id": 123, "name": "..." }
                    if (!gameEl.TryGetProperty("id", out var idEl)) continue;
                    igdbId = idEl.GetInt64();
                    name = gameEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                }
                else if (gameEl.ValueKind == JsonValueKind.Number)
                {
                    // game is a bare id (shouldn't happen when fields includes game.name, but guard it)
                    igdbId = gameEl.GetInt64();
                }
                else continue; // unexpected shape

                matches.Add(new IgdbMatch(appId, igdbId, name));
            }
        }
        return matches;
    }

    public async Task<IReadOnlyList<(long igdbId, string name)>> SearchByNameAsync(
        string name, CancellationToken ct)
    {
        // Sanitize the name for the Apicalypse search string
        var safe = name.Replace("\"", " ");
        var body = $"search \"{safe}\"; fields id, name; limit 10;";
        using var doc = await QueryAsync("games", body, ct);
        return doc.RootElement.EnumerateArray()
            .Where(e => e.TryGetProperty("id", out _))
            .Select(e => (
                e.GetProperty("id").GetInt64(),
                e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""))
            .ToList();
    }

    public async Task<IReadOnlyList<IgdbGameMetadata>> GetMetadataAsync(
        IReadOnlyList<long> igdbIds, CancellationToken ct)
    {
        var result = new List<IgdbGameMetadata>();
        foreach (var chunk in Chunk(igdbIds, 200))
        {
            var ids = string.Join(',', chunk);
            var body =
                "fields id, name, genres.name, themes.name, game_modes.name, keywords.name; " +
                $"where id = ({ids}); limit 500;";
            using var doc = await QueryAsync("games", body, ct);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("id", out var idProp)) continue;
                var id = idProp.GetInt64();
                var nm = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var feats = new List<IgdbFeature>();
                AddFeatures(el, "genres", "genre", feats);
                AddFeatures(el, "themes", "theme", feats);
                AddFeatures(el, "game_modes", "mode", feats);
                AddFeatures(el, "keywords", "keyword", feats);
                result.Add(new IgdbGameMetadata(id, nm, feats));
            }
        }
        return result;
    }

    private static void AddFeatures(
        JsonElement game, string igdbField, string category, List<IgdbFeature> into)
    {
        if (!game.TryGetProperty(igdbField, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        foreach (var f in arr.EnumerateArray())
        {
            if (!f.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetInt64();
            var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            into.Add(new IgdbFeature(category, id, name));
        }
    }

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> src, int size)
    {
        for (int i = 0; i < src.Count; i += size)
            yield return src.Skip(i).Take(size).ToList();
    }
}
