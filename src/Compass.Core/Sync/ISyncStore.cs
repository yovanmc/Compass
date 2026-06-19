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
}
