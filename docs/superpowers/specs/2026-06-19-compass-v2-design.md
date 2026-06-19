# Compass — v2 Design (Browse, Inspect, Tune)

- **Date:** 2026-06-19
- **Status:** Approved (brainstorming complete; next step: implementation plan)
- **Owner:** Yovan Collins (yovanmc)
- **Builds on:** [v1 design](2026-06-19-compass-design.md) (shipped, public `yovanmc/Compass`).

## 1. Overview

v1 is a single screen: recommendation cards + an unmatched expander + a count footer. v2
turns Compass into a full app you can browse, inspect, and tune — without needing live API
keys, because the app already runs entirely off the SQLite cache. Everything here is
buildable and verifiable against seeded fixture data; only the live sync round-trip and
IGDB match-rate tuning still need the owner's keys (unchanged from v1).

### v2 scope (four cohesive areas, delivered phased)

1. **App shell** — a left navigation rail (WPF-UI `NavigationView`): Recommend · Library ·
   Settings, plus a right-side slide-over detail panel.
2. **Library & browsing** — see the whole owned library with search, sort, and filters.
3. **Game detail** — a slide-over panel: full metadata, playtime, match info, and the score
   breakdown.
4. **Cover art** — keyless Steam capsule images, cached locally, with a fallback chain.
5. **Recommender depth & tuning** — negative signals (explicit "Not interested" + optional
   implicit tried-and-dropped) and a live-tuning Settings page.

Delivered in roughly that order; the recommender work lands last because it benefits from
the detail view to visualize "why."

### Out of scope (still deferred)

Mood/time filters, ambient resurfacing, achievements/completion tracking, RAWG as a second
metadata source, in-app manual IGDB match UI, DPAPI secret storage (ship time), SQL-side
library querying (revisit only if in-memory proves too slow).

## 2. Locked decisions (from brainstorming)

| Decision | Choice |
| --- | --- |
| App shell | WPF-UI `NavigationView` left rail (Recommend / Library / Settings). |
| Detail presentation | Slide-over panel from the right, overlaying the active page. |
| Recommend card style | Poster grid (portrait cover + name scrim + gold ring badge). |
| Library card style | Compact rows by default, with a toggle to the poster grid. |
| Cover art source | Steam capsule, keyless: `library_600x900.jpg` → `header.jpg` → placeholder; cached to `%LOCALAPPDATA%\Compass\covers\`. |
| Negative signals | Explicit "Not interested/Hide" (strong) + optional implicit tried-and-dropped (weak, Settings toggle, default OFF). |
| Negative scoring | `finalScore = positiveScore − λ · negSim`, clamped ≥ 0. |
| Library filtering | In-memory over cached `LoadLibrary()` (move to SQL only if needed). |
| Settings persistence | SQLite `settings` table, layered over `appsettings.json` defaults; live re-rank. |
| Schema | Migrate v1 → v2 (additive), bump `Schema.Version` to 2. |

## 3. App shell (NavigationView)

`MainWindow` becomes a shell hosting a WPF-UI `NavigationView`. The current recommendations
view becomes the **Recommend** page (`RecommendView` + `RecommendViewModel`, extracted from
today's `MainViewModel`). New pages: **Library** and **Settings**. The missing-keys banner
and Sync button move to a persistent top bar that's visible across pages (sync affects all
views). A right-side **`DetailPanel`** (a Grid overlay with a slide-in transform, NOT a
re-templated control) overlays whichever page is active; it's shown/hidden by the shell VM.

`MainViewModel` is refactored into:
- `ShellViewModel` — owns navigation, the Sync command/status, `MissingSecrets`, and the
  active detail item.
- `RecommendViewModel` — the poster-grid recommendations (was `MainViewModel`'s rec logic).
- `LibraryViewModel`, `SettingsViewModel`, `DetailViewModel` — new.

**Build-time verify:** confirm the WPF-UI 4.3.0 `NavigationView` API (item model, selection,
content hosting) before use — do not assume.

## 4. Library page

Compact rows by default; a toggle switches to the poster grid (reusing the Recommend poster
card). Controls:

- **Search** — substring match on name (case-insensitive).
- **Status filter** — All / Played / Backlog / Unmatched / Hidden.
- **Facet filter** — by genre or theme (from the cached feature vocabulary).
- **Sort** — Score (recommendation score; 0 for played/unscored) / Playtime / Name /
  Recently played (`playtime_2weeks` desc).

A pure `LibraryQuery` (in `Compass.Core`) takes the loaded library + a `LibraryFilter`
(search text, status, facet, sort) and returns the filtered/sorted rows in memory. It is
unit-tested independently of the UI and the DB. Clicking a row opens the detail slide-over.

## 5. Game detail slide-over

`DetailView` bound to `DetailViewModel`, populated from a `Game` + its recommendation result
(if it's a scored backlog game). Shows:

- Cover, name, status badge, total + recent playtime, match method + confidence.
- Features grouped by category: Genres / Themes / Game modes / Keywords.
- **Score breakdown** (backlog games): final score, top contributing features (as bars from
  `Recommendation.TopFeatures` contributions), nearest loved games
  (`NearestLikedItemIds` → names), and any negatives that penalized it
  (`PenalizedByItemIds` → names).
- **"Not interested"** toggle (sets/clears `not_interested`, triggers a re-rank).

## 6. Cover art

`ICoverProvider` (Core interface) → `SteamCoverProvider` (Data):

- For an appID, resolve a local cache path `%LOCALAPPDATA%\Compass\covers\{appid}.jpg`.
- If missing, download (no auth) `library_600x900.jpg`; on 404, try `header.jpg`; on failure,
  return null (UI shows the placeholder block from v1).
- Steam capsule base: `https://cdn.cloudflare.steamstatic.com/steam/apps/{appid}/...`
  (confirm the exact host/path at build; the `header.jpg` form is the most reliable).
- Lazy: covers load on demand as cards/detail render; the resolved path is exposed to the VM.

**Offline verification:** the fallback-chain decision logic is unit-tested against a fake
provider (no network). For screenshots, the verify step seeds a few cached placeholder image
files so the poster grid renders with art; missing-cover cards render the placeholder.

## 7. Recommender depth (pure-unit extension + tuning)

### Unit extension (keeps `Compass.Recommender` pure and reusable)

- New signature: `Recommend(IReadOnlyList<ProfileItem> liked, IReadOnlyList<ProfileItem> disliked, IReadOnlyList<CandidateItem> candidates, RecommenderOptions options)`.
- `RecommenderOptions.NegativeWeight` (λ, default 0.0 so behavior is unchanged unless set).
- For each candidate: `positiveScore` exactly as v1; `negSim` = the same affinity-weighted
  kNN similarity computed against the **disliked** set; `finalScore = max(0, positiveScore − λ · negSim)`.
- `Recommendation.PenalizedByItemIds` lists the disliked neighbors that drove `negSim` (for
  the "why").
- Empty `disliked` ⇒ identical output to v1. Disliked items are just more feature vectors —
  the unit gains no knowledge of games/Steam/IGDB.

### Domain wiring (Core)

`RecommendationService.Recommend(library, cfg)`:
- Excludes `not_interested` games from candidates entirely.
- Builds the **disliked** set: explicit `not_interested` games (full weight) + optionally
  (when `cfg.UseImplicitNegatives`) tried-and-dropped games at a reduced weight. A
  tried-and-dropped game is defined as playtime in **[30 min, floor)** — long enough to count
  as "actually sampled it," short enough to be below the played floor. 30 min is a fixed
  constant (`SampledThresholdMinutes`), not a user knob, to avoid knob sprawl; true-0h and
  barely-touched (<30 min) games remain neutral backlog candidates.
- Maps `PenalizedByItemIds` back to names for the detail view.

### Settings page (live tuning)

Knobs that re-rank instantly on change: played-floor (min), k, scorer mode (NearestNeighbor /
Centroid / Hybrid) + hybrid α, the four category weights (genre/theme/mode/keyword), λ
(negative weight), and the implicit-negatives toggle. Values persist in the SQLite `settings`
table and layer over the `appsettings.json` defaults; a "Reset to defaults" clears the
overrides.

## 8. Data / schema — migration v1 → v2

- `ALTER TABLE games ADD COLUMN not_interested INTEGER NOT NULL DEFAULT 0;` (additive).
- `CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);`.
- Bump `Schema.Version` to 2. Implement the `CompassDb.RunMigrations` v1→v2 step: read
  `PRAGMA user_version`; if 1, apply the additive changes and set `user_version = 2`. Both
  changes are additive and safe; a fresh DB gets them from the base `Schema.Sql` too (keep
  the base schema and the migration in sync so new and upgraded DBs converge).
- A `SettingsRepository` (Data) does typed get/set over the `settings` table;
  `not_interested` get/set lives on `GameRepository`.

## 9. Testing / offline verification

- **Recommender:** negatives penalize candidates similar to disliked; `finalScore` clamps at
  0; empty disliked reproduces v1 rankings; `PenalizedByItemIds` is populated correctly.
- **LibraryQuery:** search/status/facet filters and each sort order, on synthetic libraries.
- **Settings + migration:** `settings` round-trip; a v1 DB (user_version=1) upgrades to v2
  in place with data preserved; `not_interested` get/set round-trip.
- **Cover provider:** fallback-chain logic (portrait → landscape → null) against a fake
  HTTP/file layer; no live network in tests.
- **UI:** seeded-data screenshots inside a Sonnet subagent (text verdict): Library (rows +
  poster toggle, a filter applied), the detail slide-over (with score breakdown), the poster
  grid with cached cover placeholders, and a Settings change re-ranking the Recommend view.

## 10. Architecture integrity

- `Compass.Recommender` stays pure (disliked = more `ProfileItem`s; no new dependencies).
- `Compass.Core`: `LibraryQuery`, `ICoverProvider` interface, recommender wiring, settings
  model.
- `Compass.Data`: `SteamCoverProvider`, `SettingsRepository`, the schema migration, the
  `not_interested` repo methods.
- `Compass.App`: `ShellViewModel` + per-page VMs/views, the `DetailPanel`, cover binding.
- HTTP clients (incl. the cover provider) via `IHttpClientFactory`. No re-templating of
  WPF-UI themed controls (custom overlay/controls only). Theme/accent unchanged
  (black-glass + ice-cyan + gold).

## 11. Process notes

- Branch `feat/compass-v2`; merge to `main` and push to the existing public repo on
  completion. Commits as `yovanmc <yovanmc@users.noreply.github.com>`.
- Implementation via writing-plans → Sonnet subagents with per-phase spec + quality review,
  screenshot verification in a Sonnet subagent (text verdict).
