using Compass.Core.Model;
using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class SyncLogRepositoryTests : IDisposable
{
    private readonly string _path;
    private readonly CompassDb _db;

    public SyncLogRepositoryTests()
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
    public void Append_ThenLatest_ReturnsLastReport()
    {
        var repo = new SyncLogRepository(_db);
        repo.Append(new SyncReport(100, 90, 10, 5, DateTimeOffset.UtcNow));
        var latest = repo.Latest();
        latest!.Owned.Should().Be(100);
        latest.Unmatched.Should().Be(10);
    }
}
