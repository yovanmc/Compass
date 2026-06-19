using Compass.Core.Model;

namespace Compass.Data.Db;

public sealed class SyncLogRepository
{
    private readonly CompassDb _db;
    public SyncLogRepository(CompassDb db) => _db = db;

    public void Append(SyncReport report)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sync_log (ran_at, owned, matched, unmatched, enriched)
            VALUES ($at, $o, $m, $u, $e)
            """;
        cmd.Parameters.AddWithValue("$at", report.At.ToString("o"));
        cmd.Parameters.AddWithValue("$o", report.Owned);
        cmd.Parameters.AddWithValue("$m", report.Matched);
        cmd.Parameters.AddWithValue("$u", report.Unmatched);
        cmd.Parameters.AddWithValue("$e", report.Enriched);
        cmd.ExecuteNonQuery();
    }

    public SyncReport? Latest()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ran_at, owned, matched, unmatched, enriched FROM sync_log ORDER BY id DESC LIMIT 1";
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new SyncReport(
            r.GetInt32(1),
            r.GetInt32(2),
            r.GetInt32(3),
            r.GetInt32(4),
            DateTimeOffset.Parse(r.GetString(0)));
    }
}
