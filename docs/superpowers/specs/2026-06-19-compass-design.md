# Compass — v1 Design

- **Date:** 2026-06-19
- **Status:** Approved (brainstorming complete; next step: implementation plan)
- **Owner:** Yovan Collins (yovanmc)

## 1. Overview

Compass is a personal, single-user Windows desktop app that recommends what game to play
next from your own Steam backlog. It pulls your owned games and playtime from Steam into
local SQLite, enriches each game with IGDB metadata, builds a taste profile from the games
you've actually played, and ranks your unplayed backlog by similarity to that taste.

The recommendation engine is the deliberate centerpiece. It is designed as a clean,
well-bounded, reusable unit so it can be lifted into future projects unchanged.

### v1 scope (build only this)

1. Pull owned games + playtime from Steam into local SQLite.
2. Enrich each game with IGDB metadata (genres, themes, game_modes, keywords), matching
   Steam appIDs to IGDB entries.
3. Content-based recommender: build a taste profile from high-playtime games, score the
   unplayed backlog by similarity, and present a ranked "what to play next" with an
   explanation per recommendation.
4. One core view: the recommendations + the backlog/unmatched lists + a Sync action.

### Deferred (explicitly NOT in v1)

Mood/time filters, ambient resurfacing, achievements/completion tracking, RAWG as a second
metadata source, in-app manual match UI, live weight-tuning sliders, negative-signal
modeling (tried-and-dropped pushing similar games down), DPAPI-encrypted secret storage for
a shipped build (wired at ship time, not now).

## 2. Locked product decisions

These were settled during brainstorming and are not open:

| Decision | Choice |
| --- | --- |
| Taste signal from playtime | Log-scaled influence with a "played floor"; recent (2-week) playtime as a light boost, not the core. |
| Backlog (candidate) pool | Every owned game **below** the played-floor, including true-0h and tried-then-dropped. Tried-dropped treated the same as never-played in v1. |
| Recommendation presentation | Ranked list with an explainable "why" per item (top shared features + nearest loved games). |
| IGDB matching tail | `external_games` appID match → confidence-gated name match → anything unmatched is **surfaced in a visible "Unmatched (N) — not scored" list**, never silently dropped. No manual-match UI in v1. |
| Sync model | Manual "Sync" button with progress; everything cached in SQLite so the app is usable offline between syncs. |
| Scorer | **Affinity-weighted k-nearest-neighbor** (option A), with the centroid computed internally so a hybrid is a near-zero-cost later switch. |
| Played-floor default | ~120 minutes (configurable). |
| UI toolkit | WPF on `net10.0-windows` with WPF-UI (Fluent dark), matching the VideoShelf/VideoTriage house style. |

## 3. Architecture

Four projects, with the recommender deliberately isolated:

```
Compass.sln
├─ Compass.Recommender   class library — PURE, the reusable through-line
├─ Compass.Core          domain: playtime→affinity, game→features, sync orchestration
├─ Compass.Data          SQLite repos + Steam client + IGDB client + matcher
└─ Compass.App           WPF UI (.NET 10), single view
```

Dependency direction: `App → Core → Data`, and `Core → Recommender`.

**`Compass.Recommender` depends on nothing** — not Steam, IGDB, SQLite, or even the concept
of "games." It speaks only in feature vectors and affinities. That is the clean boundary
that lets it be reused elsewhere. Everything game-specific lives in Core; everything I/O
lives in Data.

## 4. The recommender unit

### Feature model

Each game becomes a sparse vector over **namespaced** IGDB features:
`genre:strategy`, `theme:science-fiction`, `mode:single-player`, `keyword:base-building`.
Namespacing prevents collisions (a genre "Adventure" vs a keyword "adventure") and lets
categories be weighted differently.

### Feature weighting (TF-IDF style)

Per-feature weight = `categoryBaseWeight × IDF`, where:

- `IDF(f) = log(N / (1 + docFreq(f)))` computed over the whole owned library, so features
  present on nearly every game (e.g. `mode:single-player`) carry little signal while rare,
  distinctive features (e.g. `keyword:deck-building`) carry a lot.
- `categoryBaseWeight` ranks genres/themes above the noisier, high-cardinality keyword
  space. Defaults are configurable via `RecommenderOptions`.

Each game vector is **L2-normalized** so games with many features don't dominate purely on
magnitude.

### Scorer — affinity-weighted k-nearest-neighbor (option A)

A candidate's score = the affinity-weighted average of its cosine similarity to its **top-k
most similar played games** (k≈3–5, configurable). Rationale:

- Preserves taste **clusters**. A diverse library (e.g. cozy sims *and* brutal roguelikes)
  is not averaged into a beige "centroid" that recommends middle-of-the-road titles; a
  candidate that strongly matches *one* cluster still scores high.
- The nearest loved games **are** the explanation, so the "why" comes for free.

The single-centroid vector is still computed internally and exposed, so switching to a
hybrid scorer (`α·centroid + (1−α)·kNN`) later is a near-zero-cost change.

### Explainability

For each recommendation, the engine returns:

- The top shared features, ranked by their term-wise contribution to the similarity
  (mapped back to human names: "Strategy · Sci-Fi · Turn-based").
- The nearest loved games that drove the score ("similar to Into the Breach and Slay the
  Spire, which you played a lot").

### Interface (illustrative — not final code)

```csharp
// Pure. Knows nothing about games/Steam/IGDB/SQLite.
public interface IRecommender
{
    RankedResult Recommend(
        IReadOnlyList<ProfileItem> liked,       // id + feature vector + affinity
        IReadOnlyList<CandidateItem> candidates, // id + feature vector
        RecommenderOptions options);             // k, category weights, scorer mode
}
```

`ProfileItem` carries an item id, its feature vector, and a scalar affinity. `CandidateItem`
carries an item id and feature vector. `RankedResult` is an ordered list of
`(itemId, score, topFeatures, nearestLikedItemIds)`. IDF fitting over the combined
liked+candidate corpus happens inside the unit (a fit/transform step), so callers pass raw
feature sets, not pre-weighted vectors.

### Testing the unit (hardest-tested asset)

Synthetic feature vectors with known cosine outcomes; IDF down-weighting of ubiquitous
features; kNN cluster preservation (a two-cluster profile ranks a cluster-matching candidate
above a centroid-matching-but-cluster-orthogonal one); explanation correctness; edge cases
(empty profile, zero-feature candidate, single liked game).

## 5. Domain mapping (Core)

- **Affinity from playtime:** for games above the played-floor,
  `affinity = log(1 + minutes_forever)`, plus a small additive bump derived from
  `playtime_2weeks` (recent activity). Games below the floor have zero affinity → candidates,
  not signals.
- **Played-floor:** configurable, default ~120 minutes. At or above = taste signal; below =
  backlog candidate (including tried-and-dropped).
- **Game → feature vector:** reads cached IGDB genres/themes/game_modes/keywords into the
  namespaced sparse feature set consumed by the recommender.

## 6. Data layer and the matching sub-problem

### SQLite schema (sketch)

- `games` — `steam_appid` (PK), `name`, `playtime_forever_min`, `playtime_2weeks_min`,
  `igdb_id` (nullable), `match_method` (`appid` | `name` | `none`), `match_confidence`,
  `last_synced`.
- `igdb_games` — `igdb_id` (PK), `name`.
- `features` — `feature_key` (PK, e.g. `genre:strategy`), `category`, `name`.
- `game_features` — (`igdb_id`, `feature_key`) join.
- `sync_log` — `timestamp`, counts (owned, matched, unmatched, enriched).

Metadata is cached long-term (it rarely changes); playtime is refreshed on each sync.
Schema version is tracked so migrations are explicit.

### Matching pipeline (3 tiers — the "surface the tail" decision)

1. **Steam appID → IGDB** via the `external_games` mapping (authoritative). Batched.
2. **Fuzzy name match** fallback — normalize names (strip ™/®, edition suffixes,
   punctuation, casing), score with a token-sort string similarity, accept only above a
   confidence threshold; record `match_method` and `match_confidence`.
3. **Unmatched tail** → a visible "Unmatched (N) — not scored yet" list. No manual-match UI
   in v1.

### Build-time verifications (do NOT assume — confirm against live docs when coding)

- **IGDB `external_games` shape:** IGDB has been migrating the Steam-source identifier from
  an `external_games.category` enum toward an `external_game_source` field. Confirm the
  current query shape for the Steam source before writing the matcher.
- **IGDB rate limits + Apicalypse batching:** historically ~4 requests/second and ~8
  concurrent, with `limit`/`offset` paging. Confirm current limits; throttle and back off on
  HTTP 429.
- **Steam endpoint:** `IPlayerService/GetOwnedGames` with `include_appinfo=1` and
  `include_played_free_games=1`. Confirm field names (`playtime_forever`, `playtime_2weeks`
  are in minutes).

### Clients

- **Steam:** `GET .../IPlayerService/GetOwnedGames/v1/?key={key}&steamid={id}&include_appinfo=1&include_played_free_games=1&format=json`.
- **IGDB v4:** Apicalypse query bodies POSTed to `https://api.igdb.com/v4/{endpoint}` with
  `Client-ID` and `Authorization: Bearer {token}` headers. The bearer token is obtained via
  the Twitch **client-credentials** OAuth flow
  (`POST https://id.twitch.tv/oauth2/token?client_id=...&client_secret=...&grant_type=client_credentials`)
  and cached until `expires_in`.

## 7. Sync, error handling, secrets

### Sync flow

One background job with progress reporting:

1. `GetOwnedGames` → upsert `games` + playtime.
2. Match new/unmatched games (appID → name → unmatched).
3. Enrich matched-but-bare games with IGDB metadata; cache features.
4. Recompute recommendations.

### Error handling (fail safe)

- API failures surface a clear message and **keep the last-good cached data** — a failed
  sync never wipes existing data.
- Rate limits: throttle to the confirmed IGDB limit; retry with backoff on HTTP 429.
- Missing secrets: detected at startup; show a friendly "run your user-secrets block"
  message rather than crashing.

### Secrets / configuration

- `UserSecretsId` in the App `.csproj`. Bind `Steam:ApiKey`, `Igdb:ClientId`,
  `Igdb:ClientSecret` from configuration, with **placeholder** defaults in the committed
  config so the app builds without real keys.
- SteamID64 `76561198170842711` is **not** a secret → plain `appsettings.json` under
  `Steam:SteamId64`.
- `.gitignore` covers `appsettings.local.json` / `.env` from commit #1 (already in place).
- A ready-to-run `dotnet user-secrets set ...` block is left in the repo (README or app
  project) for Yovan to inject the real values himself. The agent never asks for real keys
  in chat and never commits them.
- Shipped build (deferred): encrypt keys via Windows DPAPI under `%LOCALAPPDATA%\Compass\`.

## 8. UI — one view

WPF on `net10.0-windows` with WPF-UI (Fluent dark), matching VideoShelf/VideoTriage. A single
window contains:

- A **Sync** button with progress.
- The ranked **"What to play next"** list: cover art, name, match score, and a one-line
  "why."
- The **backlog** and **Unmatched (N)** lists.

Two to three visibly distinct color/layout options will be shown via `show_widget` for Yovan
to pick during the build (per his design-options preference) — not decided in this spec.

## 9. Testing strategy

- **Recommender:** the hardest-tested asset (see §4). Pure unit tests, no I/O.
- **Matcher:** name-normalization + similarity scoring on fixtures, no live API.
- **Clients:** thin; live Steam/IGDB calls are flagged for Yovan to verify on his machine
  (the agent's environment cannot fully exercise live Windows/.NET + live APIs).
- **App:** build + launch + screenshot verification inside a Sonnet subagent returning a
  text verdict (Yovan's standard practice).

## 10. Repo / process notes

- Local git repo at `C:\Agent Projects\Compass`; public GitHub repo `yovanmc/Compass`
  created and first-pushed during implementation, after the placeholder-secrets config
  exists and a "nothing secret staged" check passes.
- Commits authored as `yovanmc <yovanmc@users.noreply.github.com>` (global git identity).
- Implementation proceeds via writing-plans → Sonnet subagents with verification.
