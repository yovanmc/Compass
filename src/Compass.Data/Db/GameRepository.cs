using Compass.Core.Model;
using Microsoft.Data.Sqlite;

namespace Compass.Data.Db;

public sealed class GameRepository
{
    private readonly CompassDb _db;
    public GameRepository(CompassDb db) => _db = db;

    public void UpsertOwnedGames(IEnumerable<(int appId, string name, int forever, int twoWeeks)> games)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var g in games)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            // Only update playtime + name + last_synced on conflict; do NOT clobber igdb_id or match columns.
            cmd.CommandText = """
                INSERT INTO games (steam_appid, name, playtime_forever_min, playtime_2weeks_min, last_synced)
                VALUES ($id, $name, $forever, $two, $now)
                ON CONFLICT(steam_appid) DO UPDATE SET
                    name = excluded.name,
                    playtime_forever_min = excluded.playtime_forever_min,
                    playtime_2weeks_min = excluded.playtime_2weeks_min,
                    last_synced = excluded.last_synced
                """;
            cmd.Parameters.AddWithValue("$id", g.appId);
            cmd.Parameters.AddWithValue("$name", g.name);
            cmd.Parameters.AddWithValue("$forever", g.forever);
            cmd.Parameters.AddWithValue("$two", g.twoWeeks);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SetMatch(int appId, long igdbId, string method, double confidence)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE games SET igdb_id=$igdb, match_method=$m, match_confidence=$c WHERE steam_appid=$id";
        cmd.Parameters.AddWithValue("$igdb", igdbId);
        cmd.Parameters.AddWithValue("$m", method);
        cmd.Parameters.AddWithValue("$c", confidence);
        cmd.Parameters.AddWithValue("$id", appId);
        cmd.ExecuteNonQuery();
    }

    public void UpsertIgdbGame(long igdbId, string name)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO igdb_games (igdb_id, name) VALUES ($id, $name)
            ON CONFLICT(igdb_id) DO UPDATE SET name = excluded.name
            """;
        cmd.Parameters.AddWithValue("$id", igdbId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    public void ReplaceGameFeatures(long igdbId, IEnumerable<(string key, string category, string name)> features)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        // Delete existing game_features for this igdb_id first
        using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM game_features WHERE igdb_id=$id";
            del.Parameters.AddWithValue("$id", igdbId);
            del.ExecuteNonQuery();
        }

        // Insert features (upsert vocab) and game_features rows separately
        foreach (var (key, category, name) in features)
        {
            using var fcmd = conn.CreateCommand();
            fcmd.Transaction = tx;
            fcmd.CommandText = """
                INSERT INTO features (feature_key, category, name) VALUES ($k, $cat, $n)
                ON CONFLICT(feature_key) DO UPDATE SET name = excluded.name
                """;
            fcmd.Parameters.AddWithValue("$k", key);
            fcmd.Parameters.AddWithValue("$cat", category);
            fcmd.Parameters.AddWithValue("$n", name);
            fcmd.ExecuteNonQuery();

            using var gcmd = conn.CreateCommand();
            gcmd.Transaction = tx;
            gcmd.CommandText = "INSERT OR IGNORE INTO game_features (igdb_id, feature_key) VALUES ($id, $k)";
            gcmd.Parameters.AddWithValue("$id", igdbId);
            gcmd.Parameters.AddWithValue("$k", key);
            gcmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public IReadOnlyList<Game> LoadLibrary()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT g.steam_appid, g.name, g.playtime_forever_min, g.playtime_2weeks_min,
                   g.igdb_id, g.match_method, g.match_confidence,
                   GROUP_CONCAT(gf.feature_key) AS feature_keys
            FROM games g
            LEFT JOIN game_features gf ON gf.igdb_id = g.igdb_id
            GROUP BY g.steam_appid
            ORDER BY g.name
            """;
        var list = new List<Game>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var keys = r.IsDBNull(7) ? Array.Empty<string>() : r.GetString(7).Split(',');
            list.Add(new Game
            {
                SteamAppId = r.GetInt32(0),
                Name = r.GetString(1),
                PlaytimeForeverMinutes = r.GetInt32(2),
                Playtime2WeeksMinutes = r.GetInt32(3),
                IgdbId = r.IsDBNull(4) ? null : r.GetInt64(4),
                MatchMethod = ParseMethod(r.GetString(5)),
                MatchConfidence = r.GetDouble(6),
                FeatureKeys = keys,
            });
        }
        return list;
    }

    public IReadOnlyList<int> GetUnmatchedAppIds()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT steam_appid FROM games WHERE igdb_id IS NULL ORDER BY steam_appid";
        var ids = new List<int>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) ids.Add(r.GetInt32(0));
        return ids;
    }

    /// Matched games that have no cached features yet.
    public IReadOnlyList<(int appId, long igdbId, string name)> GetMatchedNeedingEnrichment()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT g.steam_appid, g.igdb_id, g.name FROM games g
            WHERE g.igdb_id IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM game_features gf WHERE gf.igdb_id = g.igdb_id)
            ORDER BY g.steam_appid
            """;
        var list = new List<(int, long, string)>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add((r.GetInt32(0), r.GetInt64(1), r.GetString(2)));
        return list;
    }

    private static MatchMethod ParseMethod(string s) => s switch
    {
        "appid" => MatchMethod.AppId,
        "name" => MatchMethod.Name,
        _ => MatchMethod.None
    };
}
