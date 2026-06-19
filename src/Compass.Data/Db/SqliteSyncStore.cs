using Compass.Core.Model;
using Compass.Core.Sync;

namespace Compass.Data.Db;

public sealed class SqliteSyncStore : ISyncStore
{
    private readonly GameRepository _games;
    private readonly SyncLogRepository _log;

    public SqliteSyncStore(CompassDb db)
    {
        _games = new GameRepository(db);
        _log = new SyncLogRepository(db);
    }

    public void UpsertOwned(IReadOnlyList<OwnedGame> games)
        => _games.UpsertOwnedGames(games.Select(g =>
            (g.AppId, g.Name, g.PlaytimeForeverMinutes, g.Playtime2WeeksMinutes)));

    public IReadOnlyList<int> GetUnmatchedAppIds() => _games.GetUnmatchedAppIds();

    public void SaveMatches(IReadOnlyList<MatchOutcome> outcomes)
    {
        foreach (var o in outcomes)
            if (o.IgdbId is long id)
            {
                _games.SetMatch(o.SteamAppId, id, o.Method, o.Confidence);
                _games.UpsertIgdbGame(id, o.Name);
            }
    }

    public IReadOnlyList<(int appId, long igdbId, string name)> GetMatchedNeedingEnrichment()
        => _games.GetMatchedNeedingEnrichment();

    public void SaveMetadata(IReadOnlyList<IgdbGameMetadata> metadata)
    {
        foreach (var m in metadata)
        {
            _games.UpsertIgdbGame(m.IgdbId, m.Name);
            _games.ReplaceGameFeatures(m.IgdbId, m.Features.Select(f =>
                (FeatureKey.Build(f.Category, f.Name), f.Category, f.Name)));
        }
    }

    public IReadOnlyList<Game> LoadLibrary() => _games.LoadLibrary();

    public void AppendSyncLog(SyncReport report) => _log.Append(report);
}
