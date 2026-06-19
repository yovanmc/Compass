using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class FeedbackPersistenceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"compass-fb-{Guid.NewGuid():N}.db");
    private CompassDb Db() => new(_path);

    [Fact]
    public void SetFeedback_RoundTrips_AndIsIndependentOfHide()
    {
        var db = Db();
        db.Initialize();
        var repo = new GameRepository(db);
        repo.UpsertOwnedGames(new[] { (5, "Test Game", 300, 0) });

        repo.SetFeedback(5, 1);
        repo.LoadLibrary().Single(g => g.SteamAppId == 5).Feedback.Should().Be(1);

        repo.SetNotInterested(5, true);
        var g = repo.LoadLibrary().Single(x => x.SteamAppId == 5);
        g.Feedback.Should().Be(1);
        g.NotInterested.Should().BeTrue();

        repo.SetFeedback(5, -1);
        repo.LoadLibrary().Single(x => x.SteamAppId == 5).Feedback.Should().Be(-1);

        repo.SetFeedback(5, 0);
        repo.LoadLibrary().Single(x => x.SteamAppId == 5).Feedback.Should().Be(0);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var p in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(p)) File.Delete(p);
    }
}
