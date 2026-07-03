# Compass — ROADMAP

> Source of truth for what to build next. Follows the `/roadmap` workflow
> (top-tier session plans/researches · pinned cheap subagents implement · ping at every phase handoff).

**Legend:** ✅ Merged · 📝 Plan ready (execute next) · 🔬 Researching/Planning · [ ] Not started (plan first)

## Definition

Single-user Steam **backlog manager + recommendation engine** for Windows: pulls the owner's Steam
library + playtime into local SQLite, enriches with IGDB metadata, models taste from what he actually
played, recommends what to play next from the unplayed backlog. C#/.NET 10 WPF (+ WPF-UI, Fluent dark)
· SQLite · Steam Web API + IGDB v4 (+ Data Dragon-style keyless Steam cover art).
**`Compass.Recommender` is a deliberately PURE kNN+IDF+MMR unit** (knows nothing about Steam/IGDB/
games — only feature vectors + affinities) meant for reuse in other projects.
Repo: github.com/yovanmc/Compass (PUBLIC) · this clone: `C:\Agent Projects\Compass` ·
Plans/specs: `docs/superpowers/`. **Purpose (owner 2026-07-03): daily-use tool** ("what do I play
tonight") + roadmap-convention onboarding — NOT a portfolio artifact.

## Conventions

- Build: `dotnet build Compass.slnx -v minimal` · Test: `dotnet test Compass.slnx`
- CI: GitHub Actions build + test (`.github/workflows/ci.yml`, added PR #4).
- Branch → PR → `gh pr checks <#> --watch` → merge `--merge --delete-branch`; commit as
  `yovanmc <yovanmc@users.noreply.github.com>`, plain `git commit` (never `--author`).
- Secrets: User Secrets (`compass-72b1f6a2-2026`) — `Steam:ApiKey`, `Igdb:ClientId`,
  `Igdb:ClientSecret` **all wired (verified present 2026-07-03)**; never committed, placeholders only
  in `appsettings.json`. SteamID64 `76561198170842711`.
- Keyless demo: Settings → **Load sample data** (~40-game sample) / **Clear library** (reversible).
- Screenshot verify: pinned cheap subagent returns a text verdict; PNGs never enter the orchestrator.
- WAL SQLite in tests needs `SqliteConnection.ClearAllPools()` in cleanup; `SQLitePCLRaw` pinned to
  clear the bundled-SQLite CVE.

## Milestones

| # | Title | Status | Plan | PR | Notes |
|---|-------|--------|------|----|-------|
| v1 | Core app (library pull, recommend, SQLite) | ✅ Merged | `docs/superpowers/plans/2026-06-19-compass-v1.md` | — | Shipped 2026-06-19 (pre-dates this ROADMAP; design in `...-compass-design.md`) |
| v2 | Browse/inspect/tune (NavigationView shell, Library facets, Settings tuning, detail slide-over) | ✅ Merged | `docs/superpowers/plans/2026-06-19-compass-v2.md` | — | Shipped 2026-06-19 |
| v3 | Engine depth | ✅ Merged | `docs/superpowers/plans/2026-06-19-compass-v3.md` | — | Shipped 2026-06-19 |
| v4 | Insights + relevance feedback | ✅ Merged | `docs/superpowers/plans/2026-06-19-compass-v4.md` | — | Shipped 2026-06-19: MMR diversity slider, RecommenderEvaluator (leave-one-out recall@k + xUnit quality floor, keyless), baked-in sample library, contribution bars + More-like-this, Insights taste profile |
| v5 | Live-pull verification | [ ] Not started | — | — | Keys are wired (verified 2026-07-03) but a real pull has NEVER run. Verify: Steam auth + library fetch, IGDB auth, appID match-rate, the `external_games category=1` mapping; fix whatever breaks; recommendations over the real backlog. Run-and-fix milestone |
| U1 | Usage window (~3 weeks) | [ ] Not started | — | — | Owner uses Compass at real "what do I play tonight" moments. Blockers fixed immediately; friction/perf/bugs logged as the v6 backlog. Usage-before-features gate |
| v6 | Usage-fed fixes (perf + bugs) | [ ] Not started | — | — | Backlog = the U1 log. Speculative perf work DECLINED (owner 2026-07-03 — no observed defects yet) |

## Decision log & gotchas

### Open / planned & declined
- **Sequencing (owner 2026-07-03):** v5 → U1 → v6. Thin roadmap is deliberate — Compass is a
  daily-use tool, not a portfolio piece; features beyond fixes wait for usage evidence.
- **Declined (don't re-propose):** speculative perf/bug milestone before real usage · portfolio
  positioning work (README/case-study polish) · Recommender NuGet extraction (reuse stays
  copy-the-project until a second consumer actually exists).

### Durable constraints & lineage
- `Compass.Recommender` stays **PURE** (no Steam/IGDB/game types) — reuse asset; don't leak domain
  types into it.
- Live pull was UNVERIFIED as of 2026-07-03 despite wired keys — memory + this file were corrected
  the same day (the stale "pending owner keys" claim cost a planning cycle; trust the secrets store,
  verify empirically).
