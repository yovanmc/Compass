namespace Compass.Data.Db;

public static class Schema
{
    public const int Version = 2;

    public const string Sql = """
        CREATE TABLE IF NOT EXISTS games (
            steam_appid           INTEGER PRIMARY KEY,
            name                  TEXT NOT NULL,
            playtime_forever_min  INTEGER NOT NULL DEFAULT 0,
            playtime_2weeks_min   INTEGER NOT NULL DEFAULT 0,
            igdb_id               INTEGER NULL,
            match_method          TEXT NOT NULL DEFAULT 'none',
            match_confidence      REAL NOT NULL DEFAULT 0,
            last_synced           TEXT NULL,
            not_interested        INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS igdb_games (
            igdb_id   INTEGER PRIMARY KEY,
            name      TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS features (
            feature_key  TEXT PRIMARY KEY,
            category     TEXT NOT NULL,
            name         TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS game_features (
            igdb_id      INTEGER NOT NULL,
            feature_key  TEXT NOT NULL,
            PRIMARY KEY (igdb_id, feature_key)
        );
        CREATE INDEX IF NOT EXISTS ix_game_features_igdb ON game_features(igdb_id);
        CREATE TABLE IF NOT EXISTS sync_log (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            ran_at     TEXT NOT NULL,
            owned      INTEGER NOT NULL,
            matched    INTEGER NOT NULL,
            unmatched  INTEGER NOT NULL,
            enriched   INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS settings (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        );
        """;
}
