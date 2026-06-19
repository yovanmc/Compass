# Compass — v4 Design (Insights, Feedback & Polish)

- **Date:** 2026-06-19
- **Status:** Approved (brainstorming complete; next step: implementation plan)
- **Owner:** Yovan Collins (yovanmc)
- **Builds on:** [v3 design](2026-06-19-compass-v3-design.md) (shipped to public `yovanmc/Compass`).

## 1. Overview

v3 deepened the recommendation engine (MMR diversity, a pure `RecommenderEvaluator`) and made
it runnable without API keys (baked-in sample library). v4 makes the engine's intelligence
**visible and interactive**, and finishes the remaining keyless polish. Everything here is
buildable and verifiable offline; none of it needs the owner's Steam/IGDB keys.

The keyless boundary is unchanged: the missing keys block only the *live data pipeline* (real
sync, IGDB match-rate, real cover fidelity). Every v4 surface operates on whatever is already in
the SQLite cache — which, until the owner syncs, is the v3 sample library. With sample data
loaded, the entire v4 feature set is a full live demo.

### v4 scope (four cohesive pieces)

1. **Recommender Insights** — a new unified **Insights** page whose lower section surfaces the
   v3 evaluator's metrics for the current library (recall@k, intra-list diversity, feature
   coverage, score spread) plus a **live scorer-comparison** (NearestNeighbor / Centroid /
   Hybrid × diversity sweeps). Turns "I built an offline evaluation harness" into a visible
   product feature.
2. **"Your taste"** — the upper section of the same Insights page: a profile derived purely from
   the cache (top genres/themes by affinity, playtime distribution, backlog composition, the
   played games that most drive your recommendations).
3. **Relevance-feedback loop** — explicit **More like this / Less like this** controls on the
   detail panel that persist and feed the taste model, making the recommender interactive instead
   of static. "Less like this" is a *soft* negative, distinct from the existing hard **Hide**.
4. **Keyless loose ends** — real Steam-CDN cover thumbs in "More like this", empty-state
   onboarding when the cache is empty, and the two non-blocking nits from the v3 final review.

### Out of scope (still deferred)

Live-data concerns (real sync round-trip, IGDB match-rate tuning, real recommendation sanity)
remain owner-keys-gated and are explicitly **not** faked. Also deferred: mood/time filters,
achievements, RAWG, DPAPI secret storage, manual IGDB match UI, SQL-side library querying,
rich/interactive charts. Relevance-feedback **controls** live on the **detail panel** only for
this version (Recommend rows/cards get at most a small indicator, not the controls).

## 2. Locked decisions (from brainstorming)

| Decision | Choice |
| --- | --- |
| Insights information architecture | **One unified "Insights" nav page** with two sections: "Your taste" (top) + "Recommender health" (bottom). |
| Visualization rendering | **Native WPF shapes** (extend the v3 contribution-bar / `ScoreRing` pattern). No charting library, no new dependency. |
| Relevance-feedback model | **Persisted soft signals, distinct from Hide.** `+1` (more) and `−1` (less) per game, stored in SQLite, fed to the engine as extra liked/disliked profile items with a tunable strength. `−1` down-ranks without excluding. |
| Scorer-comparison form | **Live in-app comparison** (table + bars), recomputed when the owner tunes. No exported artifact this version. |
| Charts/data source | Insights reflects the **current cache** (sample library until a real sync). Empty cache → onboarding empty-state. |
| Schema | One **additive** column (`games.feedback`); everything else is read-only aggregation + existing `settings` k/v. |

## 3. Architecture

The 4-project layering (`App → Data → Core → Recommender`) and the **pure** `Compass.Recommender`
are preserved. v4 introduces no new engine primitives — it reuses `RecommenderEvaluator`,
`ContentRecommender`, and the existing scoring path.

- **`Compass.Recommender` (pure):** unchanged. (The scorer-comparison and health metrics are
  *orchestration* over existing engine calls; that orchestration is a Core concern, not an engine
  primitive, so nothing crosses into the pure unit.)
- **`Compass.Core`:** a new `InsightsService` (taste aggregation + health/comparison metrics);
  the relevance-feedback wiring inside `RecommendationService`; a `FeedbackWeight` config field;
  a `Game.Feedback` field.
- **`Compass.Data`:** an additive `games.feedback` column + idempotent migration;
  `GameRepository.SetFeedback(appId, value)`; `ISyncStore.SetFeedback`.
- **`Compass.App`:** a new **Insights** page (`InsightsView` + `InsightsViewModel`); detail-panel
  feedback controls; a `FeedbackWeight` Settings knob; the loose-ends polish.

## 4. Recommender Insights (health section) — `Compass.Core` `InsightsService`

`InsightsService.ComputeHealth(IReadOnlyList<Game> library, RecommenderConfig cfg)` returns a
`RecommenderHealth` record, computed purely by orchestrating existing engine types
(`GameFeatureExtractor.ToVector`, `AffinityCalculator`, `RecommenderEvaluator`,
`RecommendationService`/`ContentRecommender`). No I/O.

**`RecommenderHealth`:**
- `double RecallAt10` — `RecommenderEvaluator.LeaveOneOutRecallAtK(liked, backlogPool, 10, options)`
  using the current config's options (the played set as `liked`, the backlog as the shared pool).
- `double IntraListDiversity` — `RecommenderEvaluator.IntraListDiversity` over the top-N
  recommendation vectors at the current δ, where **N = 10** (matching recall@10; if fewer than 10
  recommendations exist, use all of them).
- `int DistinctFeatureCoverage` — over the same top-N.
- `(double Min, double Max, double Mean, double Stdev) ScoreSpread` — over the top-N relevance scores.
- `IReadOnlyList<ScorerComparisonRow> Comparison` — one row per `(ScorerMode, δ)` permutation
  over `{NearestNeighbor, Centroid, Hybrid} × {0.0, cfg.Diversity}` (de-duplicated if
  `cfg.Diversity == 0`), each carrying that permutation's `RecallAt10`, `IntraListDiversity`, and
  `DistinctFeatureCoverage`. Each permutation is a fresh `RecommenderOptions` run.

`ScorerComparisonRow(string Mode, double Delta, double RecallAt10, double IntraListDiversity, int DistinctFeatureCoverage)`.

If the library has no played games or no backlog candidates, the metrics return their natural
zeros and the App shows the empty-state. Complexity is a handful of engine passes over a personal
library — negligible.

**Verification (headline, keyless):** unit tests assert each metric is computed correctly and
that the comparison contains the expected permutations, with documented floors on the sample
fixture. These are the v4 engine-quality regression guards.

## 5. "Your taste" (taste section) — `InsightsService`

`InsightsService.AnalyzeTaste(IReadOnlyList<Game> library, RecommenderConfig cfg)` returns a pure
`TasteProfile` record:

- `IReadOnlyList<(string Name, double Weight)> TopGenres` / `TopThemes` — for each **played**
  game (affinity-positive), accumulate that game's affinity into each of its `genre:` / `theme:`
  features; sort descending; humanize the names. (Your most-played taste ranks highest.)
- `IReadOnlyList<(string Label, int Count)> PlaytimeDistribution` — owned games bucketed by
  `playtime_forever_min`: `0`, `<2h`, `2–10h`, `10–50h`, `50h+`.
- `LibraryComposition Composition` — counts of Played / Backlog / Unmatched / Hidden using the
  same status rule as the rest of the app (played ≥ floor; backlog < floor with features;
  unmatched = no IGDB/features; hidden = `NotInterested`).
- `IReadOnlyList<(string Name, int Count)> MostInfluential` — run `RecommendationService.Recommend`
  on the library, tally how often each played game appears in recommendations' `WhyLikedNames`
  (the "nearest loved" list), sort by frequency. Shows which of your games most drive your recs.

`LibraryComposition(int Played, int Backlog, int Unmatched, int Hidden)`.

All pure aggregation over the cache; each sub-result is independently unit-testable on the sample
fixture.

## 6. Relevance-feedback loop

### 6a. Persistence (`Compass.Data`)

- Additive column `games.feedback INTEGER NOT NULL DEFAULT 0` (`−1` / `0` / `+1`). Added via an
  **idempotent migration** in `CompassDb.Initialize()`: check `PRAGMA table_info(games)` and run
  `ALTER TABLE games ADD COLUMN feedback INTEGER NOT NULL DEFAULT 0` only if the column is absent.
  Additive with a default, so existing v2/v3 databases upgrade safely with no data loss.
- `Game` gains `int Feedback`. `GameRepository.LoadLibrary` selects it; `SetFeedback(int appId,
  int value)` upserts it. `ISyncStore.SetFeedback(int, int)` + `SqliteSyncStore` delegate.
- `ClearLibrary` already deletes the `games` rows, so feedback is cleared with the library;
  `SetNotInterested` and `SetFeedback` are independent flags on the same row.

### 6b. Engine wiring (`Compass.Core` `RecommendationService.Recommend`)

The pure engine has no concept of "feedback" — it only consumes weighted `liked` / `disliked`
`ProfileItem`s. `RecommendationService` translates feedback into those signals while keeping the
engine pure:

- A game with `Feedback == +1` and a non-empty vector is added to **liked** with weight
  `cfg.FeedbackWeight × log(1 + cfg.PlayedFloorMinutes)` (≈ "treat it like a game played right at
  the floor," scaled by the knob). It **also remains a candidate** so it can still be recommended.
- A game with `Feedback == −1` and a non-empty vector is added to **disliked** with the same base
  weight. It **remains a candidate** (distinct from `NotInterested`, which hard-excludes) — so it
  still appears, just down-ranked, and games similar to it are pushed away (modulated by the
  existing `NegativeWeight` λ in the engine's disliked handling).
- `NotInterested` continues to hard-exclude + contribute a negative signal, exactly as today.
  Precedence: `NotInterested` wins over feedback (a hidden game is excluded regardless of any
  stale feedback value).

`FeedbackWeight` is a **Core-level config consumed by `RecommendationService`'s profile-building**,
*not* a `RecommenderOptions` field — the engine stays unaware of feedback. Default `1.0`, range
`0–2`, live-tunable.

### 6c. App (detail panel + Settings)

- `DetailViewModel`: a `Feedback` state (int) + `MoreLikeThisCommand` / `LessLikeThisCommand`
  (tri-state toggles: clicking the active one clears to `0`). Each calls `_store.SetFeedback`,
  then triggers a **refresh-without-close** callback so Recommend/Library re-rank behind the
  scrim while the detail panel stays open showing the same game with updated feedback state.
  (This is a new callback distinct from the existing `onChangedAndClose`, which Hide still uses to
  close. `ShellViewModel.OnGameChosen` supplies both: `onChangedAndClose` = refresh + close;
  `onLibraryChanged` = refresh + keep open.)
- `DetailView.xaml`: two themed toggle controls next to the existing "Hide this game" button —
  active state uses the ice-cyan accent (more) / a muted negative tone (less), matching the
  existing chromeless local-brush button pattern (no WPF-UI re-template).
- Settings: a **"Feedback weight"** slider (0–2, `StringFormat {0:0.00}`) in the SCORING section,
  wired through the existing `RecommenderConfigState` + `ConfigChanged` live-re-rank path.

## 7. Insights page (`Compass.App`)

- `InsightsView : Page` + `InsightsViewModel`, resolving `InsightsService`, `ISyncStore`, and
  `RecommenderConfigState`. On construction and on `ConfigChanged` / `LibraryReplaced`, it loads
  the library and recomputes `TasteProfile` + `RecommenderHealth`, exposing observable
  collections for the bars/tables.
- A new **Insights** `NavigationViewItem` (registered in DI + `PageProvider`, `TargetPageType =
  typeof(InsightsView)`), placed between Library and Settings.
- **Layout is shown as 2–3 visibly different mockups before implementation** (per the owner's
  preference for distinct options), but in all cases: native-shape horizontal bars for
  genres/themes and the comparison, a small bucketed histogram for playtime, stat readouts for the
  health metrics, all on the black-glass theme (ice-cyan bars, gold reserved for scores).
- **Empty-state:** when the library is empty, both sections collapse to a single onboarding panel
  ("No data yet — add your API keys and Sync, or load the built-in sample data in Settings").

## 8. Keyless loose ends

- **"More like this" cover thumbs:** `SimilarRow` becomes an `ObservableObject` with
  `[ObservableProperty] string? coverPath`; `DetailViewModel` kicks off async cover loads for each
  similar row via the existing `ICoverProvider` (the same keyless Steam-CDN path the cards already
  use), replacing the placeholder boxes. Null cover → existing placeholder remains.
- **Empty-state onboarding:** a shared panel shown on Recommend / Library / Insights when the
  library count is `0`, pointing at Sync (needs keys) or Settings → Load sample data (keyless).
- **Nits from the v3 final review:**
  - `DetailViewModel` implements `IDisposable` and disposes `_coverCts`; `ShellViewModel` disposes
    the replaced `ActiveDetail` in `OnActiveDetailChanging` (alongside the existing
    `GameChosen -= OnGameChosen` unsubscribe).
  - The duplicated Settings button `ControlTemplate` (Reset / Load / Clear) is extracted into one
    shared `Style` resource (Settings-view or App-level resources) and referenced by all three.

## 9. Settings / config changes (`FeedbackWeight`)

- `RecommenderConfig` gains `double FeedbackWeight` (default `1.0`).
- `RecommenderSettingsService.Load/Save` gains the `Recommender:FeedbackWeight` key (same pattern
  as the other knobs).
- `RecommendationService.Recommend` reads `cfg.FeedbackWeight` when building feedback signals
  (§6b). `appsettings.json` adds `"FeedbackWeight": 1.0` under `Recommender`.
- The Settings page adds the "Feedback weight" slider (§6c) in the SCORING section.

## 10. Architecture integrity

- `Compass.Recommender` stays pure: no feedback/insights concepts leak into it; metrics are
  orchestration over existing engine calls.
- `Compass.Core`: `InsightsService` (pure), feedback translation in `RecommendationService`, the
  `FeedbackWeight` field, `Game.Feedback`.
- `Compass.Data`: the additive `feedback` column + idempotent migration + `SetFeedback`.
- `Compass.App`: the Insights page, detail feedback controls, the Feedback-weight knob, the
  loose-ends polish. No re-templating of WPF-UI themed controls; theme (black-glass + ice-cyan +
  gold) unchanged.

## 11. Testing / offline verification

- **Engine/Core (headline, keyless):** unit tests for `InsightsService` — taste aggregation
  correctness (top genres/themes/distribution/composition/most-influential on the sample
  fixture), health metric correctness + the comparison's expected permutations with documented
  floors; feedback wiring (`+1` lifts similar games and keeps the game a candidate; `−1`
  down-ranks **without** excluding; `NotInterested` still hard-excludes and wins over feedback);
  `FeedbackWeight` mapping; the settings round-trip for the new key.
- **Data:** `feedback` column persistence round-trip; the **idempotent migration on a pre-v4
  database** (a DB created without the column gains it on `Initialize()` with no data loss);
  `SetFeedback` independent of `SetNotInterested`.
- **UI:** static review + the sample-data smoke-launch (the app is runnable keyless, so the owner
  can eyeball the Insights page, feedback controls, and cover thumbs directly). Insights *pixels*
  remain owner-eyeballed (headless capture stays unreliable); its **numeric content is
  test-verified**. Note that an empty-DB smoke-launch does not exercise the new page until data is
  loaded — the smoke-launch seeds sample data first.

## 12. Process notes

- Branch `feat/compass-v4`; merge to `main` and push to the public repo on completion. Commits as
  `yovanmc <yovanmc@users.noreply.github.com>` (plain `git commit`).
- Implementation via writing-plans → Sonnet subagents with per-phase spec + quality review.
  Engine/Core quality is validated by unit tests (numbers), compensating for the headless-capture
  constraint on UI verification. Distinct Insights-page layout options are shown via inline
  mockups before that page is built.
