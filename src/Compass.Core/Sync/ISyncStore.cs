using Compass.Core.Model;

namespace Compass.Core.Sync;

public interface ISyncStore
{
    void UpsertOwned(IReadOnlyList<OwnedGame> games);
    IReadOnlyList<int> GetUnmatchedAppIds();
    void SaveMatches(IReadOnlyList<MatchOutcome> outcomes);
    IReadOnlyList<(int appId, long igdbId, string name)> GetMatchedNeedingEnrichment();
    void SaveMetadata(IReadOnlyList<IgdbGameMetadata> metadata);
    IReadOnlyList<Game> LoadLibrary();
    void AppendSyncLog(SyncReport report);
    void SetNotInterested(int appId, bool value);
    void SetFeedback(int appId, int value);

    /// <summary>
    /// Loads the baked-in sample library into the store.
    /// Implementations that back a real database should write all four
    /// owned-data tables. The in-memory test fake only needs enough fidelity
    /// to keep existing tests green.
    /// </summary>
    void LoadSampleData(IReadOnlyList<SampleGame> games);

    /// <summary>
    /// Removes all owned game data (games, igdb_games, features, game_features).
    /// Settings and sync_log are left intact.
    /// </summary>
    void ClearLibrary();
}
