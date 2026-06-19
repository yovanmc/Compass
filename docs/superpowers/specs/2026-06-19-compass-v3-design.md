# Compass — v3 Design (Recommender Depth & Evaluation)

- **Date:** 2026-06-19
- **Status:** Approved (brainstorming complete; next step: implementation plan)
- **Owner:** Yovan Collins (yovanmc)
- **Builds on:** [v2 design](2026-06-19-compass-v2-design.md) (shipped to public `yovanmc/Compass`).

## 1. Overview

v2 made Compass a browsable, inspectable, tunable app running entirely off the SQLite
cache. v3 deepens **the recommendation engine itself** — the deliberate reusable through-line
of the project — and makes its quality **measurable and demonstrable without API keys**. Every
item here is buildable and verifiable offline; none of it needs the owner's Steam/IGDB keys.

The framing matters: the missing keys block only the *data pipeline* (live pull, IGDB
match-rate, real cover fidelity). The *engine* operates purely on feature-vectors and
affinities, so it can be improved and validated keyless. A baked-in sample library also makes
the whole app runnable and demoable with zero keys.

### v3 scope (four cohesive pieces)

1. **MMR diversity re-ranking** — the engine gains a diversity pass so the top-N isn't a set of
   near-duplicates. Exposed as a live Settings knob.
2. **Evaluation harness + sample data** — a pure `RecommenderEvaluator` (leave-one-out recall,
   intra-list diversity, feature coverage, score spread), an xUnit eval suite over a fixture,
   and a ~40-game baked-in sample library loadable into the cache for a keyless app.
3. **Contribution bars** — the detail view's "Top factors" shows real per-feature contribution
   magnitudes (already computed by the engine, currently discarded) as bars, not flat chips.
4. **"More like this"** — a detail-panel section listing the nearest games to the current one,
   reusing the engine; clicking re-opens detail for that game.

### Out of scope (still deferred)

Mood/time filters, ambient resurfacing, achievements, RAWG, DPAPI secret storage, manual IGDB
match UI, SQL-side library querying. Live-data concerns (match-rate tuning, real recommendation
sanity) remain owner-keys-gated and are explicitly **not** faked here.

## 2. Locked decisions (from brainstorming)

| Decision | Choice |
| --- | --- |
| Eval + sample form | Baked-in ~40-game sample library (loadable into cache) + xUnit eval suite over a fixture. |
| Diversity control | Live Settings knob ("Diversity" slider), default ≈ 0.3 (non-zero out of the box). |
| MMR semantics | Re-rank order only; the displayed gold-ring score stays the **relevance** score. |
| Backward compat | `Diversity = 0` reproduces v2 ranking exactly. |
| Sample appids | Real Steam appids (covers work; a later real sync merges naturally). |
| Destructive-op safety | "Load sample data" confirms before overwriting a non-empty library; a companion "Clear library" makes it reversible. |
| "More like this" | A section inside the detail slide-over; entries re-open detail via the existing selection path. |
| Schema | No DB schema change (settings is k/v; sample data uses existing tables). |

## 3. MMR diversity re-ranking (`Compass.Recommender`, pure)

The recommender currently scores every candidate by relevance and returns the top-N by score.
v3 inserts a greedy **Maximal Marginal Relevance** selection after relevance scoring:

```
Given relevance-scored candidates C and a diversity weight δ ∈ [0,1]:
  selected = []
  while |selected| < N and C not empty:
      pick d = argmax_{d ∈ C} [ (1−δ)·relevance(d) − δ·max_{s ∈ selected} cosine(vec(d), vec(s)) ]
      (for the first pick, the diversity term is 0)
      move d from C to selected
  return selected
```

- `cosine` reuses the engine's existing **L2-normalized** feature vectors (cosine = dot product).
- **`RecommenderOptions.Diversity`** (double, default `0.3`). `Diversity = 0` ⇒ pure relevance
  ordering identical to v2 (critical: lets the eval harness A/B δ=0 vs δ>0, and keeps existing
  behavior opt-out-able).
- Each `Recommendation` still carries its **relevance `Score`** unchanged; only the **order**
  reflects MMR. The UI's gold ring continues to mean "match strength," not "shown position."
- Complexity is O(N·k) over the candidate set — negligible for a personal library.
- Lives entirely in the pure unit; it is a feature-vector operation with no domain knowledge.

**Unit tests:** δ=0 reproduces the v2 ranking exactly; with 3 near-identical candidates + 1
distinct, δ>0 surfaces the distinct one earlier; intra-list diversity (§4) is monotonically
non-decreasing as δ rises on a fixture.

## 4. Evaluation harness + sample data

### 4a. `RecommenderEvaluator` (pure, in `Compass.Recommender`)

Reusable, BCL-only, operating on `ProfileItem`/`CandidateItem` + the engine — no games/Steam/
IGDB. Metrics:

- **Leave-one-out recall@k** — for each liked item with a non-empty vector: remove it from the
  liked set, add it to the candidate set, run the engine, and check whether it ranks in the
  top-k. Aggregate the hit-rate. (Does the engine recognize the owner's own taste?)
- **Intra-list diversity** — average pairwise `1 − cosine` over the top-N. (Variety; validates MMR.)
- **Distinct-feature coverage** — count of distinct feature keys appearing across the top-N
  (how broad the slate is).
- **Score spread** — min / max / mean / stdev of the top-N relevance scores (interpretability).

Each metric is a small pure function returning a number (or a struct of numbers). No I/O.

### 4b. xUnit eval suite

Runs `RecommenderEvaluator` against the **sample fixture** and asserts sane bounds — e.g.
leave-one-out recall@10 above an empirically-chosen floor on the curated data, and intra-list
diversity rising with δ. These are **regression guards on engine quality** and, given the
headless-capture constraint, are the headline validation for the v3 engine work. They live in
the existing `Compass.Recommender.Tests` project (or a sibling) and reuse the sample fixture.

### 4c. Sample library (baked-in)

- A handcrafted **~40-game** dataset as an **embedded resource** (JSON): each game has a real
  Steam `appid`, name, `playtime_forever_min`, `playtime_2weeks_min`, and IGDB-style feature
  keys (`genre:`/`theme:`/`mode:`/`keyword:`). Designed with believable taste clusters (a
  played core + an unplayed backlog + a few unmatched) so recommendations are meaningful and
  the eval metrics are non-trivial.
- A **`SampleDataProvider`** (Data layer) parses the resource and writes it into the SQLite
  cache via the existing repositories — `games`, `igdb_games`, `features`, `game_features` —
  exactly as a real sync would. After loading, the entire app (Recommend / Library / Detail /
  Settings) works keyless.
- The same dataset (or a subset) is exposed as the **eval fixture** for §4a/§4b, so "what you
  test" and "what you can run in the app" are the same data.

### 4d. Load / Clear library (Settings)

- **"Load sample data"** button (Settings page). On click: if the library is non-empty, confirm
  first (honors the verify-before-destroy principle); then `SampleDataProvider` populates the
  cache and all page VMs refresh.
- **"Clear library"** button: empties the owned-games tables (`games`, `igdb_games`,
  `features`, `game_features`) so the sample load is reversible. Also behind a confirm. Settings
  overrides (the `settings` table) are left untouched.
- Implemented as a small `ISampleDataService` / repository method set; the confirm dialogs use
  WPF-UI message dialogs or a simple in-app confirm (no re-templating of themed controls).

## 5. Contribution bars (detail view)

The engine already returns `Recommendation.TopFeatures` as
`IReadOnlyList<FeatureContribution>` where `FeatureContribution(string FeatureKey, double
Contribution)`. `RecommendationService` currently **discards the magnitude**
(`r.TopFeatures.Select(f => Humanize(f.FeatureKey))`).

- Add a Core DTO `WhyFeature(string Name, double Contribution)` (humanized name + magnitude).
- `GameRecommendation` carries `IReadOnlyList<WhyFeature> WhyFeatures` (replacing the bare
  `IReadOnlyList<string>`); the name-only "why" line used by the Recommend card derives names
  from this list (`.Select(w => w.Name)`), so the card is unchanged.
- `DetailViewModel.TopFeatures` exposes each factor's name + a **normalized bar fraction**
  (`contribution / maxContribution`, clamped to [0,1]).
- `DetailView` renders "Top factors" as labeled horizontal bars (width ∝ fraction) instead of
  flat chips. Theme unchanged (ice-cyan bars on `#141414`).

**Tests:** service-level — contributions are plumbed through in order, names humanized,
magnitudes preserved; the normalized fraction math is correct.

## 6. "More like this" (detail view)

- A new detail section computing the **N nearest games** to the current game: reuse the engine
  with the current game as the sole "liked" seed against the rest of the library as candidates
  (or a direct cosine ranking against the seed vector). Prefer reusing
  `IRecommender.Recommend(liked: [seed], candidates: others, options)` so there is no new
  scoring path; expose a thin `RecommendationService.SimilarTo(library, appId, k)` that returns
  `GameRecommendation`s (or lightweight rows) for the top-k.
- Each entry: cover thumb + name + similarity score. Clicking routes through the **existing**
  `GameChosen → ShellViewModel.ActiveDetail` path so the detail slide-over re-opens for the
  chosen game. `DetailViewModel` raises a `GameChosen(int appId)` event the shell wires to its
  `OnGameChosen` handler (same mechanism the Library/Recommend pages already use).
- Games with empty feature vectors (unmatched) yield no neighbors → the section hides.

## 7. Settings / config changes (Diversity knob)

- `RecommenderConfig` gains `double Diversity` (default from `appsettings.json`, ≈ 0.3).
- `RecommenderSettingsService.Load/Save` gains the `Recommender:Diversity` key (same pattern as
  the other knobs).
- `RecommendationService` maps `cfg.Diversity` → `RecommenderOptions.Diversity`.
- The Settings page adds a **"Diversity"** slider (0–1, `StringFormat {0:0.00}`) in the SCORING
  section, live-re-ranking via the existing `RecommenderConfigState` + `ConfigChanged` path.
- `appsettings.json` adds `"Diversity": 0.3` under `Recommender`.

## 8. Architecture integrity

- `Compass.Recommender` stays pure: MMR, `RecommenderEvaluator`, and the similarity path are all
  feature-vector operations; no new dependencies.
- `Compass.Core`: the `WhyFeature` DTO, `SimilarTo`, the `Diversity` config field/mapping.
- `Compass.Data`: `SampleDataProvider` + clear-library repository methods (existing schema).
- `Compass.App`: Diversity slider, contribution bars, "more like this" section + its re-open
  wiring, Load/Clear sample-data commands. No re-templating of WPF-UI themed controls; theme
  (black-glass + ice-cyan + gold) unchanged.

## 9. Testing / offline verification

- **Engine (headline):** unit tests — MMR δ=0 ≡ v2; δ>0 increases diversity and surfaces the
  outlier; `RecommenderEvaluator` metric correctness; the eval suite's quality-floor assertions
  on the sample fixture; contribution plumbing; `SimilarTo` correctness.
- **Data:** `SampleDataProvider` round-trips into the schema; clear-library empties the right
  tables and leaves `settings` intact; settings round-trip for the new `Diversity` key.
- **UI:** static review + the launch smoke-test; because the sample data makes the app runnable
  keyless, the owner can eyeball contribution bars / "more like this" / the Diversity slider
  directly. (Screenshot capture remains environment-flaky; not relied upon.)

## 10. Process notes

- Branch `feat/compass-v3`; merge to `main` and push to the public repo on completion. Commits as
  `yovanmc <yovanmc@users.noreply.github.com>`.
- Implementation via writing-plans → Sonnet subagents with per-phase spec + quality review.
  Engine quality is validated by the eval suite (numbers), compensating for the headless-capture
  constraint on UI verification.
