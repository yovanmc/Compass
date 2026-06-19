using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class FeedbackMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"compass-mig-{Guid.NewGuid():N}.db");

    [Fact]
    public void Initialize_AddsFeedbackColumn_ToPreV4Db_PreservingRows()
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = _path }.ToString();
        using (var c = new SqliteConnection(cs))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE games (
                    steam_appid INTEGER PRIMARY KEY, name TEXT NOT NULL,
                    playtime_forever_min INTEGER NOT NULL DEFAULT 0,
                    playtime_2weeks_min INTEGER NOT NULL DEFAULT 0,
                    igdb_id INTEGER NULL, match_method TEXT NOT NULL DEFAULT 'none',
                    match_confidence REAL NOT NULL DEFAULT 0, last_synced TEXT NULL,
                    not_interested INTEGER NOT NULL DEFAULT 0);
                INSERT INTO games (steam_appid, name) VALUES (10, 'Keeper');
                PRAGMA user_version = 2;
                """;
            cmd.ExecuteNonQuery();
        }

        new CompassDb(_path).Initialize();

        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var q = conn.CreateCommand();
        q.CommandText = "SELECT feedback FROM games WHERE steam_appid=10";
        Convert.ToInt32(q.ExecuteScalar()).Should().Be(0);
        using var v = conn.CreateCommand();
        v.CommandText = "PRAGMA user_version";
        Convert.ToInt32(v.ExecuteScalar()).Should().Be(3);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var p in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(p)) File.Delete(p);
    }
}
