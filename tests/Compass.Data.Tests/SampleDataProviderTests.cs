using Compass.Core.Sync;
using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Compass.Data.Tests;

public class SampleDataProviderTests : IDisposable
{
    private readonly string _path;
    private readonly CompassDb _db;
    private readonly GameRepository _repo;

    public SampleDataProviderTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"compass-{Guid.NewGuid():N}.db");
        _db = new CompassDb(_path);
        _db.Initialize();
        _repo = new GameRepository(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + "-wal")) File.Delete(_path + "-wal");
        if (File.Exists(_path + "-shm")) File.Delete(_path + "-shm");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<SampleGame> MinimalSample() => new[]
    {
        // Matched game with features
        new SampleGame(100, "Strategy Classic", 600, 30, "Strategy Classic",
            new[] { "genre:strategy", "keyword:4x", "mode:singleplayer" }),
        // Matched game with different features
        new SampleGame(101, "RPG Adventure", 300, 0, "RPG Adventure",
            new[] { "genre:rpg", "theme:fantasy", "mode:singleplayer" }),
        // Unmatched — no igdbName, no features
        new SampleGame(102, "Mystery Game", 0, 0, null, Array.Empty<string>()),
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_InsertsAllGamesRows()
    {
        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());

        var library = _repo.LoadLibrary();
        library.Should().HaveCount(3);
        library.Select(g => g.SteamAppId).Should().Contain(new[] { 100, 101, 102 });
    }

    [Fact]
    public void Load_MatchedGames_HaveFeaturesPopulated()
    {
        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());

        var library = _repo.LoadLibrary();
        var strategy = library.Single(g => g.SteamAppId == 100);
        strategy.FeatureKeys.Should().Contain(new[] { "genre:strategy", "keyword:4x", "mode:singleplayer" });
        strategy.MatchMethod.Should().Be(Compass.Core.Model.MatchMethod.AppId);
        strategy.MatchConfidence.Should().BeApproximately(1.0, 0.001);
        strategy.IgdbId.Should().Be(1_000_000L + 100);
    }

    [Fact]
    public void Load_UnmatchedGames_HaveNoFeaturesAndNullIgdbId()
    {
        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());

        var library = _repo.LoadLibrary();
        var mystery = library.Single(g => g.SteamAppId == 102);
        mystery.FeatureKeys.Should().BeEmpty();
        mystery.IgdbId.Should().BeNull();
        mystery.MatchMethod.Should().Be(Compass.Core.Model.MatchMethod.None);
    }

    [Fact]
    public void Load_IsIdempotent()
    {
        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());
        // Calling again must not throw or duplicate rows
        var act = () => provider.Load(MinimalSample());
        act.Should().NotThrow();

        var library = _repo.LoadLibrary();
        library.Should().HaveCount(3, "loading twice must not duplicate games");
    }

    [Fact]
    public void Load_FullSampleLibrary_ProducesExpectedCount()
    {
        // Verify the real embedded fixture round-trips through the DB.
        var games = SampleLibrary.Load();
        var provider = new SampleDataProvider(_db);
        provider.Load(games);

        var library = _repo.LoadLibrary();
        library.Should().HaveCount(games.Count,
            "every sample game should have a corresponding games row");

        var matchedWithFeatures = library.Where(g => g.FeatureKeys.Count > 0).ToList();
        matchedWithFeatures.Should().NotBeEmpty("matched games must have features after Load");
    }

    [Fact]
    public void ClearLibrary_RemovesAllOwnedDataRows()
    {
        // Pre-insert a settings row to verify it survives the clear.
        InsertSetting("test_key", "test_value");

        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());

        // Sanity: something was loaded.
        _repo.LoadLibrary().Should().NotBeEmpty();

        _repo.ClearLibrary();

        // All four owned-data tables must be empty.
        _repo.LoadLibrary().Should().BeEmpty("games table must be cleared");
        CountRows("igdb_games").Should().Be(0, "igdb_games must be cleared");
        CountRows("features").Should().Be(0, "features must be cleared");
        CountRows("game_features").Should().Be(0, "game_features must be cleared");
    }

    [Fact]
    public void ClearLibrary_LeavesSettingsIntact()
    {
        InsertSetting("keep_me", "yes");

        var provider = new SampleDataProvider(_db);
        provider.Load(MinimalSample());
        _repo.ClearLibrary();

        // Settings row must survive.
        CountRows("settings").Should().Be(1, "settings table must not be cleared");
        ReadSetting("keep_me").Should().Be("yes");
    }

    [Fact]
    public void SqliteSyncStore_LoadSampleData_AndClearLibrary_DelegateCorrectly()
    {
        // Verify the ISyncStore facade delegates correctly to the underlying repos.
        var store = new SqliteSyncStore(_db);
        var games = SampleLibrary.Load();

        store.LoadSampleData(games);
        var library = store.LoadLibrary();
        library.Should().HaveCount(games.Count);
        library.Any(g => g.FeatureKeys.Count > 0).Should().BeTrue();

        store.ClearLibrary();
        store.LoadLibrary().Should().BeEmpty();
    }

    // ── DB helpers ────────────────────────────────────────────────────────────

    private void InsertSetting(string key, string value)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($k, $v)";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private string? ReadSetting(string key)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    private int CountRows(string table)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
