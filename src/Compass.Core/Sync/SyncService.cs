using Compass.Core.Model;

namespace Compass.Core.Sync;

public sealed class SyncService
{
    private readonly ISteamClient _steam;
    private readonly IIgdbClient _igdb;
    private readonly IGameMatcher _matcher;
    private readonly ISyncStore _store;

    public SyncService(ISteamClient steam, IIgdbClient igdb, IGameMatcher matcher, ISyncStore store)
        => (_steam, _igdb, _matcher, _store) = (steam, igdb, matcher, store);

    public async Task<SyncReport> SyncAsync(CancellationToken ct, IProgress<string>? progress = null)
    {
        progress?.Report("Fetching Steam library…");
        var owned = await _steam.GetOwnedGamesAsync(ct);
        _store.UpsertOwned(owned);

        progress?.Report("Matching to IGDB…");
        var unmatchedIds = _store.GetUnmatchedAppIds().ToHashSet();
        var toMatch = owned.Where(o => unmatchedIds.Contains(o.AppId))
                           .Select(o => (o.AppId, o.Name)).ToList();
        var outcomes = await _matcher.MatchAsync(toMatch, ct);
        _store.SaveMatches(outcomes);

        progress?.Report("Enriching metadata…");
        var needs = _store.GetMatchedNeedingEnrichment();
        var meta = needs.Count > 0
            ? await _igdb.GetMetadataAsync(needs.Select(n => n.igdbId).Distinct().ToList(), ct)
            : Array.Empty<IgdbGameMetadata>();
        _store.SaveMetadata(meta);

        var matched = outcomes.Count(o => o.IgdbId is not null);
        var report = new SyncReport(
            Owned: owned.Count,
            Matched: matched,
            Unmatched: outcomes.Count - matched,
            Enriched: meta.Count,
            At: DateTimeOffset.UtcNow);
        _store.AppendSyncLog(report);
        progress?.Report("Done.");
        return report;
    }
}
