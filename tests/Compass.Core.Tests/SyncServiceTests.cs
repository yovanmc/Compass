using Compass.Core.Model;
using Compass.Core.Sync;
using FluentAssertions;
using Xunit;

// ── Fakes ──────────────────────────────────────────────────────────────────

sealed class FakeSteam : ISteamClient
{
    private readonly IReadOnlyList<OwnedGame> _games;
    public FakeSteam(params OwnedGame[] games) => _games = games;
    public Task<IReadOnlyList<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct)
        => Task.FromResult(_games);
}

sealed class FakeMatcher : IGameMatcher
{
    public Dictionary<int, MatchOutcome> Outcomes { get; } = new();

    public Task<IReadOnlyList<MatchOutcome>> MatchAsync(
        IReadOnlyList<(int appId, string name)> games, CancellationToken ct)
    {
        var results = new List<MatchOutcome>();
        foreach (var (appId, name) in games)
        {
            if (Outcomes.TryGetValue(appId, out var o))
                results.Add(o);
            else
                results.Add(new MatchOutcome(appId, null, name, "none", 0.0));
        }
        return Task.FromResult<IReadOnlyList<MatchOutcome>>(results);
    }
}

sealed class FakeIgdb : IIgdbClient
{
    public Dictionary<long, IgdbGameMetadata> Metadata { get; } = new();

    public Task<IReadOnlyList<IgdbMatch>> MatchBySteamAppIdsAsync(
        IReadOnlyList<int> appIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IgdbMatch>>(Array.Empty<IgdbMatch>());

    public Task<IReadOnlyList<(long igdbId, string name)>> SearchByNameAsync(
        string name, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<(long, string)>>(Array.Empty<(long, string)>());

    public Task<IReadOnlyList<IgdbGameMetadata>> GetMetadataAsync(
        IReadOnlyList<long> igdbIds, CancellationToken ct)
    {
        var results = igdbIds
            .Where(Metadata.ContainsKey)
            .Select(id => Metadata[id])
            .ToList();
        return Task.FromResult<IReadOnlyList<IgdbGameMetadata>>(results);
    }
}

sealed class InMemoryStore : ISyncStore
{
    // appId → (name, forever, twoWeeks)
    private readonly Dictionary<int, (string name, int forever, int twoWeeks)> _owned = new();
    // appId → (igdbId, name, method, confidence)
    private readonly Dictionary<int, (long igdbId, string name, string method, double confidence)> _matches = new();
    // igdbId → list of feature keys
    private readonly Dictionary<long, List<string>> _features = new();
    private readonly List<SyncReport> _log = new();

    public void UpsertOwned(IReadOnlyList<OwnedGame> games)
    {
        foreach (var g in games)
            _owned[g.AppId] = (g.Name, g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes);
    }

    public IReadOnlyList<int> GetUnmatchedAppIds()
        => _owned.Keys.Where(id => !_matches.ContainsKey(id)).OrderBy(id => id).ToList();

    public void SaveMatches(IReadOnlyList<MatchOutcome> outcomes)
    {
        foreach (var o in outcomes)
            if (o.IgdbId is long igdbId)
                _matches[o.SteamAppId] = (igdbId, o.Name, o.Method, o.Confidence);
    }

    public IReadOnlyList<(int appId, long igdbId, string name)> GetMatchedNeedingEnrichment()
    {
        var result = new List<(int, long, string)>();
        foreach (var (appId, (igdbId, name, _, _)) in _matches)
        {
            if (!_features.ContainsKey(igdbId) || _features[igdbId].Count == 0)
            {
                var gameName = _owned.TryGetValue(appId, out var o) ? o.name : name;
                result.Add((appId, igdbId, gameName));
            }
        }
        return result;
    }

    public void SaveMetadata(IReadOnlyList<IgdbGameMetadata> metadata)
    {
        foreach (var m in metadata)
        {
            var keys = m.Features
                .Select(f => FeatureKey.Build(f.Category, f.Name))
                .ToList();
            _features[m.IgdbId] = keys;
        }
    }

    public IReadOnlyList<Game> LoadLibrary()
    {
        var games = new List<Game>();
        foreach (var (appId, (name, forever, twoWeeks)) in _owned)
        {
            long? igdbId = _matches.TryGetValue(appId, out var m) ? m.igdbId : null;
            MatchMethod method = MatchMethod.None;
            double confidence = 0;
            if (igdbId.HasValue)
            {
                method = m.method == "appid" ? MatchMethod.AppId
                       : m.method == "name" ? MatchMethod.Name
                       : MatchMethod.None;
                confidence = m.confidence;
            }

            var featureKeys = igdbId.HasValue && _features.TryGetValue(igdbId.Value, out var fk)
                ? (IReadOnlyList<string>)fk
                : Array.Empty<string>();

            games.Add(new Game
            {
                SteamAppId = appId,
                Name = name,
                PlaytimeForeverMinutes = forever,
                Playtime2WeeksMinutes = twoWeeks,
                IgdbId = igdbId,
                MatchMethod = method,
                MatchConfidence = confidence,
                FeatureKeys = featureKeys,
            });
        }
        return games;
    }

    public void AppendSyncLog(SyncReport report) => _log.Add(report);
}

// ── Tests ─────────────────────────────────────────────────────────────────

public class SyncServiceTests
{
    [Fact]
    public async Task Sync_Owns_Matches_Enriches_AndReports()
    {
        var store = new InMemoryStore();
        var steam = new FakeSteam(new OwnedGame(10, "Doom", 6000, 0), new OwnedGame(11, "Mystery", 0, 0));
        var igdb = new FakeIgdb();
        igdb.Metadata[555] = new IgdbGameMetadata(555, "Doom", new[]
        {
            new IgdbFeature("genre", 5, "Shooter"), new IgdbFeature("theme", 1, "Action"),
        });
        var matcher = new FakeMatcher
        {
            Outcomes =
            {
                [10] = new MatchOutcome(10, 555, "Doom", "appid", 1.0),
                [11] = new MatchOutcome(11, null, "Mystery", "none", 0.0),
            }
        };

        var svc = new SyncService(steam, igdb, matcher, store);
        var report = await svc.SyncAsync(default);

        report.Owned.Should().Be(2);
        report.Matched.Should().Be(1);
        report.Unmatched.Should().Be(1);
        report.Enriched.Should().Be(1);

        var lib = store.LoadLibrary();
        lib.Single(g => g.SteamAppId == 10).FeatureKeys.Should().Contain("genre:shooter");
        lib.Single(g => g.SteamAppId == 11).FeatureKeys.Should().BeEmpty();
    }
}
