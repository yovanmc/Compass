using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class SchemaTests
{
    private static string TempDb() => Path.Combine(Path.GetTempPath(), $"compass-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
        if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
    }

    [Fact]
    public void Initialize_CreatesTables_AndSetsUserVersion()
    {
        var path = TempDb();
        try
        {
            var db = new CompassDb(path);
            db.Initialize();
            using var conn = db.OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            var tables = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) tables.Add(r.GetString(0));
            tables.Should().Contain(new[] { "games", "igdb_games", "features", "game_features", "sync_log" });

            using var v = conn.CreateCommand();
            v.CommandText = "PRAGMA user_version";
            Convert.ToInt32(v.ExecuteScalar()).Should().Be(Schema.Version);
        }
        finally { DeleteDb(path); }
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        var path = TempDb();
        try
        {
            var db = new CompassDb(path);
            db.Initialize();
            db.Initialize(); // must not throw
        }
        finally { DeleteDb(path); }
    }
}
