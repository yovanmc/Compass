using Compass.Core.Sync;
using Microsoft.Data.Sqlite;

namespace Compass.Data.Db;

/// <summary>
/// Loads the baked-in sample library into the SQLite database in a single
/// transaction so the app is fully runnable without real Steam/IGDB API keys.
/// </summary>
public sealed class SampleDataProvider
{
    private readonly CompassDb _db;

    public SampleDataProvider(CompassDb db) => _db = db;

    /// <summary>
    /// Writes all <paramref name="games"/> into the database inside one transaction.
    /// <list type="bullet">
    ///   <item>Every game gets a <c>games</c> row (upsert: name + playtimes only).</item>
    ///   <item>Matched games (igdbName != null &amp;&amp; featureKeys non-empty) also get:
    ///     a synthesized <c>igdb_id = 1_000_000 + appId</c>,
    ///     match_method='appid', match_confidence=1.0,
    ///     an <c>igdb_games</c> row, <c>features</c> vocab rows, and
    ///     <c>game_features</c> link rows.</item>
    ///   <item>Unmatched games get only the <c>games</c> row (igdb_id NULL, method 'none').</item>
    /// </list>
    /// Idempotent: safe to call again — upserts on all tables.
    /// </summary>
    public void Load(IReadOnlyList<SampleGame> games)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        foreach (var g in games)
        {
            // 1. Upsert games row (owned info only; do NOT clobber igdb_id on conflict).
            UpsertGame(conn, tx, g);

            bool isMatched = g.IgdbName is not null && g.FeatureKeys.Count > 0;
            if (!isMatched)
                continue;

            // Synthesized igdb_id: stable, collision-free with real IGDB ids.
            long igdbId = 1_000_000L + g.AppId;

            // 2. Set match columns on the games row.
            SetMatch(conn, tx, g.AppId, igdbId);

            // 3. Upsert igdb_games row.
            UpsertIgdbGame(conn, tx, igdbId, g.IgdbName!);

            // 4. Upsert each feature into the vocab table + link game_features.
            foreach (var key in g.FeatureKeys)
            {
                var (category, humanName) = ParseFeatureKey(key);
                UpsertFeature(conn, tx, key, category, humanName);
                InsertGameFeature(conn, tx, igdbId, key);
            }
        }

        tx.Commit();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void UpsertGame(SqliteConnection conn, SqliteTransaction tx, SampleGame g)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        // Mirror the same ON CONFLICT clause used in GameRepository.UpsertOwnedGames:
        // only update name/playtimes/last_synced, never clobber igdb_id or match columns.
        cmd.CommandText = """
            INSERT INTO games (steam_appid, name, playtime_forever_min, playtime_2weeks_min, last_synced)
            VALUES ($id, $name, $forever, $two, $now)
            ON CONFLICT(steam_appid) DO UPDATE SET
                name = excluded.name,
                playtime_forever_min = excluded.playtime_forever_min,
                playtime_2weeks_min = excluded.playtime_2weeks_min,
                last_synced = excluded.last_synced
            """;
        cmd.Parameters.AddWithValue("$id", g.AppId);
        cmd.Parameters.AddWithValue("$name", g.Name);
        cmd.Parameters.AddWithValue("$forever", g.PlaytimeForeverMin);
        cmd.Parameters.AddWithValue("$two", g.Playtime2WeeksMin);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static void SetMatch(SqliteConnection conn, SqliteTransaction tx, int appId, long igdbId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE games
            SET igdb_id=$igdb, match_method='appid', match_confidence=1.0
            WHERE steam_appid=$id
            """;
        cmd.Parameters.AddWithValue("$igdb", igdbId);
        cmd.Parameters.AddWithValue("$id", appId);
        cmd.ExecuteNonQuery();
    }

    private static void UpsertIgdbGame(SqliteConnection conn, SqliteTransaction tx, long igdbId, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO igdb_games (igdb_id, name) VALUES ($id, $name)
            ON CONFLICT(igdb_id) DO UPDATE SET name = excluded.name
            """;
        cmd.Parameters.AddWithValue("$id", igdbId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    private static void UpsertFeature(SqliteConnection conn, SqliteTransaction tx,
        string key, string category, string humanName)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO features (feature_key, category, name) VALUES ($k, $cat, $n)
            ON CONFLICT(feature_key) DO UPDATE SET name = excluded.name, category = excluded.category
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$cat", category);
        cmd.Parameters.AddWithValue("$n", humanName);
        cmd.ExecuteNonQuery();
    }

    private static void InsertGameFeature(SqliteConnection conn, SqliteTransaction tx, long igdbId, string key)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT OR IGNORE INTO game_features (igdb_id, feature_key) VALUES ($id, $k)";
        cmd.Parameters.AddWithValue("$id", igdbId);
        cmd.Parameters.AddWithValue("$k", key);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Splits "genre:turn-based-strategy" → ("genre", "Turn Based Strategy").
    /// The part before ':' is the category; the part after is title-cased with hyphens
    /// replaced by spaces, matching the style used by real IGDB feature names.
    /// </summary>
    private static (string category, string humanName) ParseFeatureKey(string key)
    {
        var colonIdx = key.IndexOf(':');
        if (colonIdx < 0)
            return ("", key);

        var category = key[..colonIdx];
        var rawName = key[(colonIdx + 1)..];

        // "turn-based-strategy" → "Turn Based Strategy"
        var humanName = string.Join(" ",
            rawName.Split('-').Select(word =>
                word.Length == 0 ? word :
                char.ToUpperInvariant(word[0]) + word[1..]));

        return (category, humanName);
    }
}
