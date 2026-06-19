using Compass.Core.Sync;
using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class SqliteSyncStoreTests : IDisposable
{
    private readonly string _path;
    private readonly CompassDb _db;

    public SqliteSyncStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"compass-{Guid.NewGuid():N}.db");
        _db = new CompassDb(_path);
        _db.Initialize();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + "-wal")) File.Delete(_path + "-wal");
        if (File.Exists(_path + "-shm")) File.Delete(_path + "-shm");
    }

    [Fact]
    public void FullRoundTrip()
    {
        var store = new SqliteSyncStore(_db);
        store.UpsertOwned(new[] { new OwnedGame(10, "Doom", 6000, 0) });
        store.GetUnmatchedAppIds().Should().Contain(10);
        store.SaveMatches(new[] { new MatchOutcome(10, 555, "Doom", "appid", 1.0) });
        store.GetMatchedNeedingEnrichment().Should().ContainSingle(x => x.igdbId == 555);
        store.SaveMetadata(new[] { new IgdbGameMetadata(555, "Doom",
            new[] { new IgdbFeature("genre", 5, "Shooter") }) });
        store.LoadLibrary().Single().FeatureKeys.Should().Contain("genre:shooter");
    }

    [Fact]
    public void AppendSyncLog_DoesNotThrow()
    {
        var store = new SqliteSyncStore(_db);
        var report = new Compass.Core.Model.SyncReport(5, 4, 1, 3, DateTimeOffset.UtcNow);
        var act = () => store.AppendSyncLog(report);
        act.Should().NotThrow();
    }

    [Fact]
    public void SaveMatches_SkipsOutcomesWithNoIgdbId()
    {
        var store = new SqliteSyncStore(_db);
        store.UpsertOwned(new[] { new OwnedGame(20, "Unknown", 0, 0) });
        store.SaveMatches(new[] { new MatchOutcome(20, null, "Unknown", "none", 0.0) });
        // still unmatched after saving null-igdb outcome
        store.GetUnmatchedAppIds().Should().Contain(20);
    }
}
