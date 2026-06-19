using Microsoft.Data.Sqlite;

namespace Compass.Data.Db;

public sealed class CompassDb
{
    private readonly string _connectionString;

    public CompassDb(string dbPath)
        => _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys=ON";
            pragma.ExecuteNonQuery();
        }
        using (var wal = conn.CreateCommand())
        {
            wal.CommandText = "PRAGMA journal_mode=WAL";
            wal.ExecuteScalar();
        }
        return conn;
    }

    public void Initialize()
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        // Execute each CREATE TABLE / CREATE INDEX statement individually
        // because Microsoft.Data.Sqlite may not support all multi-statement batches
        // when a transaction is involved. Split on ';' and run each non-empty statement.
        foreach (var stmt in SplitStatements(Schema.Sql))
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = stmt;
            cmd.ExecuteNonQuery();
        }

        RunMigrations(conn, tx);

        using (var setVer = conn.CreateCommand())
        {
            setVer.Transaction = tx;
            setVer.CommandText = $"PRAGMA user_version = {Schema.Version}";
            setVer.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static void RunMigrations(SqliteConnection conn, SqliteTransaction tx)
    {
        int current;
        using (var get = conn.CreateCommand())
        {
            get.Transaction = tx;
            get.CommandText = "PRAGMA user_version";
            current = Convert.ToInt32(get.ExecuteScalar());
        }

        if (current < 2)
        {
            // v1 -> v2: additive.
            // settings table is covered by Schema.Sql's IF NOT EXISTS (already run above).
            // Add not_interested only if it's missing (a v1 games table won't have it).
            bool hasCol;
            using (var chk = conn.CreateCommand())
            {
                chk.Transaction = tx;
                chk.CommandText = "SELECT COUNT(*) FROM pragma_table_info('games') WHERE name='not_interested'";
                hasCol = Convert.ToInt32(chk.ExecuteScalar()) > 0;
            }
            if (!hasCol)
            {
                using var alter = conn.CreateCommand();
                alter.Transaction = tx;
                alter.CommandText = "ALTER TABLE games ADD COLUMN not_interested INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }
    }

    public static string DefaultDbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Compass");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "compass.db");
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        foreach (var part in sql.Split(';'))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                yield return trimmed;
        }
    }
}
