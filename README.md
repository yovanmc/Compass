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

v4 (insights & relevance feedback) is in `main`. It builds on the v2 browse/inspect/tune
app — a `NavigationView` shell with **Recommend**, **Library** (search / status / genre·theme
facet / sort, rows or poster grid), and **Settings** (live recommender tuning) pages, plus a
right-side game **detail** slide-over with keyless Steam cover art — and deepens the engine:

- **MMR diversity re-ranking** — a live **Diversity** slider re-orders the top results for
  variety instead of near-duplicates. It re-ranks order only (the shown match score stays the
  relevance score); `Diversity = 0` reproduces the v2 ranking exactly.
- **Evaluation harness** — a pure `RecommenderEvaluator` (leave-one-out recall@k, intra-list
  diversity, feature coverage, score spread) with an xUnit quality-floor suite, so engine
  quality is measurable and regression-guarded **without any API keys**.
- **Baked-in sample library** — Settings → **Load sample data** populates the cache with a
  ~40-game sample (real Steam appids) so the whole app is runnable and demoable keyless;
  **Clear library** makes it reversible.
- **Detail insight** — the score breakdown now shows real per-feature **contribution bars**
  (width ∝ magnitude) and a **More like this** section listing the nearest games; clicking one
  re-opens detail for it.
- **Insights page** — a dedicated page showing your **taste profile** (top genres/themes by
  playtime, playtime distribution, library composition, and the games that most drive your
  recommendations) and **recommender health** (recall@10, intra-list diversity, feature coverage,
  and a live scorer comparison) — all computed keyless from the cache.
- **Relevance feedback** — **More like this / Less like this** on any game persistently nudges
  your taste profile (a soft signal, distinct from Hide), tunable via a **Feedback weight** knob.

Everything above runs entirely off the local SQLite cache. Live Steam/IGDB sync still needs your
own API keys (see below). See the design docs in [`docs/superpowers/specs`](docs/superpowers/specs)
for scope and architecture.

## Secrets / configuration

Secrets are **never** committed. Development uses .NET User Secrets (see the design doc and
the `dotnet user-secrets set` block left in the app project). Your SteamID64 is not a secret
and lives in `appsettings.json`.

## Local setup (run once)

Inject real keys via .NET User Secrets (stored outside the repo). Run from `src/Compass.App`:

```bash
dotnet user-secrets set "Steam:ApiKey"      "YOUR_STEAM_WEB_API_KEY"
dotnet user-secrets set "Igdb:ClientId"     "YOUR_TWITCH_CLIENT_ID"
dotnet user-secrets set "Igdb:ClientSecret" "YOUR_TWITCH_CLIENT_SECRET"
```

Get keys: Steam Web API key at https://steamcommunity.com/dev/apikey ; IGDB uses a Twitch
dev app (Client ID + Secret) at https://dev.twitch.tv/console/apps. SteamID64 is already set
(non-secret) in `appsettings.json`.
