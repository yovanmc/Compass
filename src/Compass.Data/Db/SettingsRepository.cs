using Compass.Core.Sync;
using Microsoft.Data.Sqlite;

namespace Compass.Data.Db;

public sealed class SettingsRepository : ISettingsStore
{
    private readonly CompassDb _db;
    public SettingsRepository(CompassDb db) => _db = db;

    public string? Get(string key)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key=$k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key,value) VALUES ($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings";
        var d = new Dictionary<string, string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) d[r.GetString(0)] = r.GetString(1);
        return d;
    }
}
