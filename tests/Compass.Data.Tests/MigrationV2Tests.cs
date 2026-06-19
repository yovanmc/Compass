using Compass.Data.Db;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

public class MigrationV2Tests
{
    private static string TempDb() => Path.Combine(Path.GetTempPath(), $"compass-{Guid.NewGuid():N}.db");
    private static void Cleanup(string p)
    {
        SqliteConnection.ClearAllPools();
        foreach (var f in new[] { p, p + "-wal", p + "-shm" }) if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public void FreshDb_IsVersion2_WithNewObjects()
    {
        var p = TempDb();
        try
        {
            var db = new CompassDb(p); db.Initialize();
            using var conn = db.OpenConnection();
            using var v = conn.CreateCommand(); v.CommandText = "PRAGMA user_version";
            Convert.ToInt32(v.ExecuteScalar()).Should().Be(3);
            using var c = conn.CreateCommand();
            c.CommandText = "SELECT COUNT(*) FROM pragma_table_info('games') WHERE name='not_interested'";
            Convert.ToInt32(c.ExecuteScalar()).Should().Be(1);
            using var s = conn.CreateCommand();
            s.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='settings'";
            s.ExecuteScalar().Should().Be("settings");
        }
        finally { Cleanup(p); }
    }

    [Fact]
    public void V1Db_UpgradesInPlace_PreservingData()
    {
        var p = TempDb();
        try
        {
            // Build a minimal v1-shaped DB by hand (user_version=1, no not_interested/settings).
            using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = p }.ToString()))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE games (steam_appid INTEGER PRIMARY KEY, name TEXT NOT NULL,
                        playtime_forever_min INTEGER NOT NULL DEFAULT 0, playtime_2weeks_min INTEGER NOT NULL DEFAULT 0,
                        igdb_id INTEGER NULL, match_method TEXT NOT NULL DEFAULT 'none', match_confidence REAL NOT NULL DEFAULT 0, last_synced TEXT NULL);
                    INSERT INTO games (steam_appid, name, playtime_forever_min) VALUES (10, 'Doom', 600);
                    PRAGMA user_version = 1;";
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var db = new CompassDb(p); db.Initialize(); // should migrate 1 -> 2
            using var c2 = db.OpenConnection();
            using var ver = c2.CreateCommand(); ver.CommandText = "PRAGMA user_version";
            Convert.ToInt32(ver.ExecuteScalar()).Should().Be(3);
            using var col = c2.CreateCommand();
            col.CommandText = "SELECT not_interested FROM games WHERE steam_appid=10";
            Convert.ToInt32(col.ExecuteScalar()).Should().Be(0);   // defaulted
            using var nm = c2.CreateCommand(); nm.CommandText = "SELECT name FROM games WHERE steam_appid=10";
            nm.ExecuteScalar().Should().Be("Doom");                 // data preserved
        }
        finally { Cleanup(p); }
    }
}
