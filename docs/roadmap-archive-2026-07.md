# Compass — shipped-milestone archive (v1–v4)

Verbose per-milestone detail moved out of `ROADMAP.md` on 2026-07-07 (North Star integration
session) to keep the live roadmap short. This is history, not planning input — see
`NORTHSTAR.md` and `ROADMAP.md` for what's current.

The authoritative build detail for each shipped milestone lives in its dated plan/spec pair
under `docs/superpowers/`:

| # | Plan | Design spec |
|---|------|-------------|
| v1 | `docs/superpowers/plans/2026-06-19-compass-v1.md` | `docs/superpowers/specs/2026-06-19-compass-design.md` |
| v2 | `docs/superpowers/plans/2026-06-19-compass-v2.md` | `docs/superpowers/specs/2026-06-19-compass-v2-design.md` |
| v3 | `docs/superpowers/plans/2026-06-19-compass-v3.md` | `docs/superpowers/specs/2026-06-19-compass-v3-design.md` |
| v4 | `docs/superpowers/plans/2026-06-19-compass-v4.md` | `docs/superpowers/specs/2026-06-19-compass-v4-design.md` |

## What shipped, condensed

- **v1 — Core app** (2026-06-19): library pull, recommend, SQLite storage. Pre-dates the
  ROADMAP.md convention; design lives in the v1 design doc above.
- **v2 — Browse/inspect/tune** (2026-06-19): `NavigationView` shell (Recommend / Library /
  Settings), Library search/status/genre·theme facets + sort (rows or poster grid), live
  recommender tuning in Settings, right-side game detail slide-over.
- **v3 — Engine depth** (2026-06-19): see design doc for scope; deepened the recommender beyond
  the v1 baseline ahead of v4's evaluation and insights layer.
- **v4 — Insights + relevance feedback** (2026-06-19): MMR diversity slider (re-ranks order only;
  `Diversity = 0` reproduces pre-MMR ranking exactly), `RecommenderEvaluator` (leave-one-out
  recall@k + xUnit quality-floor suite, keyless), baked-in ~40-game sample library (Settings →
  Load sample data / Clear library, reversible), detail-view contribution bars + "More like
  this", Insights page (taste profile, recommender health metrics).

The README's "Status" section also describes v4 in prose if more color is needed.
