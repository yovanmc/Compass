# Compass

A personal, single-user game backlog manager and recommendation engine for Windows.

Compass pulls your own Steam library and playtime into a local SQLite database, enriches
each game with IGDB metadata, models your taste from what you've actually played, and
recommends what to play next from your unplayed backlog.

The recommendation engine is the point: it is built as a clean, well-bounded, reusable unit
(`Compass.Recommender`) that knows nothing about Steam, IGDB, or games — only feature
vectors and affinities — so it can be reused across other projects.

## Stack

- C# / .NET 10, WPF (`net10.0-windows`) with WPF-UI (Fluent dark theme)
- SQLite for local storage
- Steam Web API (owned games + playtime) and IGDB v4 (metadata)

## Status

v1 in development. See [the design doc](docs/superpowers/specs/2026-06-19-compass-design.md)
for scope and architecture.

## Secrets / configuration

Secrets are **never** committed. Development uses .NET User Secrets (see the design doc and
the `dotnet user-secrets set` block left in the app project). Your SteamID64 is not a secret
and lives in `appsettings.json`.
