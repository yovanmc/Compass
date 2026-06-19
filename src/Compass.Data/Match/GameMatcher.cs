using Compass.Core.Sync;

namespace Compass.Data.Match;

public sealed class GameMatcher : IGameMatcher
{
    private readonly IIgdbClient _igdb;
    private readonly double _threshold;

    public GameMatcher(IIgdbClient igdb, double nameConfidenceThreshold)
        => (_igdb, _threshold) = (igdb, nameConfidenceThreshold);

    public async Task<IReadOnlyList<MatchOutcome>> MatchAsync(
        IReadOnlyList<(int appId, string name)> games, CancellationToken ct)
    {
        var outcomes = new List<MatchOutcome>(games.Count);
        var appIds = games.Select(g => g.appId).ToList();

        // Tier 1: external_games appID lookup.
        // IMPORTANT: external_games can return multiple rows per appId (regional entries, etc.).
        // Build a dedup-safe lookup by taking the first match per appId (last-wins via loop).
        var tier1Results = await _igdb.MatchBySteamAppIdsAsync(appIds, ct);
        var tier1 = new Dictionary<int, IgdbMatch>();
        foreach (var m in tier1Results)
            tier1[m.SteamAppId] = m; // last-write-wins; any valid match is fine

        foreach (var (appId, name) in games)
        {
            if (tier1.TryGetValue(appId, out var m))
            {
                outcomes.Add(new MatchOutcome(appId, m.IgdbId, name, "appid", 1.0));
                continue;
            }

            // Tier 2: name search + similarity gate
            var candidates = await _igdb.SearchByNameAsync(name, ct);
            var normTarget = NameNormalizer.Normalize(name);
            (long id, string n, double score) best = (0, "", 0);
            foreach (var (id, cn) in candidates)
            {
                var score = StringSimilarity.TokenSortRatio(normTarget, NameNormalizer.Normalize(cn));
                if (score > best.score) best = (id, cn, score);
            }

            if (best.score >= _threshold)
                outcomes.Add(new MatchOutcome(appId, best.id, name, "name", best.score));
            else
                // Tier 3: unmatched; best.score may be 0 if no candidates at all
                outcomes.Add(new MatchOutcome(appId, null, name, "none", best.score));
        }

        return outcomes;
    }
}
