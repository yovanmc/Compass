using Compass.Core.Model;
using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class GameRepositoryTests : IDisposable
{
    private readonly string _path;
    private readonly CompassDb _db;
    private readonly GameRepository _repo;

    public GameRepositoryTests()
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

    [Fact]
    public void Upsert_ThenLoad_RoundTripsPlaytime()
    {
        _repo.UpsertOwnedGames(new[] { (10, "Game Ten", 600, 30) });
        var lib = _repo.LoadLibrary();
        lib.Should().ContainSingle();
        lib[0].SteamAppId.Should().Be(10);
        lib[0].PlaytimeForeverMinutes.Should().Be(600);
        lib[0].Playtime2WeeksMinutes.Should().Be(30);
        lib[0].MatchMethod.Should().Be(MatchMethod.None);
    }

    [Fact]
    public void Reupsert_UpdatesPlaytime_PreservesMatch()
    {
        _repo.UpsertOwnedGames(new[] { (10, "Game Ten", 600, 0) });
        _repo.SetMatch(10, igdbId: 555, method: "appid", confidence: 1.0);
        _repo.UpsertOwnedGames(new[] { (10, "Game Ten", 999, 0) }); // playtime grew
        var g = _repo.LoadLibrary().Single();
        g.PlaytimeForeverMinutes.Should().Be(999);
        g.IgdbId.Should().Be(555);
        g.MatchMethod.Should().Be(MatchMethod.AppId);
    }

    [Fact]
    public void Features_AreNamespaced_AndJoinedIntoLibrary()
    {
        _repo.UpsertOwnedGames(new[] { (10, "Game Ten", 600, 0) });
        _repo.SetMatch(10, 555, "appid", 1.0);
        _repo.UpsertIgdbGame(555, "Game Ten");
        _repo.ReplaceGameFeatures(555, new[]
        {
            ("genre:strategy", "genre", "Strategy"),
            ("theme:sci-fi", "theme", "Science fiction"),
        });
        var g = _repo.LoadLibrary().Single();
        g.FeatureKeys.Should().BeEquivalentTo(new[] { "genre:strategy", "theme:sci-fi" });
    }

    [Fact]
    public void GetUnmatchedAppIds_ReturnsOnlyUnmatched()
    {
        _repo.UpsertOwnedGames(new[] { (10, "A", 0, 0), (11, "B", 0, 0) });
        _repo.SetMatch(10, 555, "appid", 1.0);
        _repo.GetUnmatchedAppIds().Should().BeEquivalentTo(new[] { 11 });
    }

    [Fact]
    public void SetNotInterested_RoundTripsInLibrary()
    {
        _repo.UpsertOwnedGames(new[] { (10, "Doom", 600, 0) });
        _repo.SetNotInterested(10, true);
        _repo.LoadLibrary().Single(g => g.SteamAppId == 10).NotInterested.Should().BeTrue();
        _repo.SetNotInterested(10, false);
        _repo.LoadLibrary().Single(g => g.SteamAppId == 10).NotInterested.Should().BeFalse();
    }
}
