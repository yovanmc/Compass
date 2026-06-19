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

    // Future schema bumps add versioned steps here; v1 is the base schema only.
    private static void RunMigrations(SqliteConnection conn, SqliteTransaction tx) { }

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
