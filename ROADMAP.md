# Compass — ROADMAP

> Source of truth for what to build next. Follows the `/roadmap` workflow
> (top-tier session plans/researches · pinned cheap subagents implement · ping at every phase handoff).

## North Star (2026-07-07) — read NORTHSTAR.md before planning

**Status: PARKED ENTIRELY** (owner decision 2026-07-07). No development, nothing queued.
The **one sanctioned action while parked** is the single ~30-min v5 live-pull verification
session (see below) — nothing else is permitted under the park.

**Vision, one line:** Compass ends as a standalone occasional-use game picker ("what do I play
tonight"); its pure recommender core gets extracted into a shared library once Curio integrates
it for media recommendations — Curio is consumer #2, which fires the standing extraction
trigger (see Decision log).

**The 4-step path to v-final:**
1. **v5 — pull-verify:** the one sanctioned action. Run the real Steam+IGDB pull, fix whatever
   breaks.
2. **U1 — short usage window:** use it on a few game nights, log friction only.
3. **v6 — friction fixes only**, seeded strictly by U1. Compass is then functionally v-final.
4. **Extraction milestone — triggered BY Curio, not by Compass:** when Curio's recommender
   integration begins, extract `Compass.Recommender` into a shared library (two consumers now
   exist). Then maintenance forever — nothing, unless it breaks on a game night.

Full context, the recorded contradiction, and owner decisions: **`NORTHSTAR.md`**.

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

Shipped-milestone build detail (v1–v4) is condensed here; full prose lives in
[`docs/roadmap-archive-2026-07.md`](docs/roadmap-archive-2026-07.md).

| # | Title | Status | Notes |
|---|-------|--------|-------|
| v1 | Core app (library pull, recommend, SQLite) | ✅ Merged | Shipped 2026-06-19 |
| v2 | Browse/inspect/tune (NavigationView shell, facets, tuning, detail slide-over) | ✅ Merged | Shipped 2026-06-19 |
| v3 | Engine depth | ✅ Merged | Shipped 2026-06-19 |
| v4 | Insights + relevance feedback | ✅ Merged | Shipped 2026-06-19: MMR diversity, RecommenderEvaluator, sample library, contribution bars, Insights page |
| v5 | Live-pull verification | [ ] Not started — **PARKED, but the one sanctioned action** | Keys wired (verified 2026-07-03), real pull has NEVER run. Verify Steam auth + library fetch, IGDB auth, appID match-rate, `external_games category=1` mapping; fix whatever breaks. Run-and-fix milestone |
| U1 | Usage window (~3 weeks) | [ ] Not started — PARKED, gated on v5 | Owner uses Compass at real "what do I play tonight" moments. Friction/perf/bugs logged as the v6 backlog. Usage-before-features gate |
| v6 | Usage-fed fixes (perf + bugs) | [ ] Not started — PARKED, gated on U1 | Backlog = the U1 log only. Speculative perf work DECLINED |

## Decision log & gotchas

### Open / planned & declined
- **Sequencing (owner 2026-07-03, reaffirmed 2026-07-07 park):** v5 → U1 → v6. Thin roadmap is
  deliberate — Compass is a daily-use tool, not a portfolio piece; features beyond fixes wait for
  usage evidence.
- **Declined (don't re-propose):** speculative perf/bug milestone before real usage · portfolio
  positioning work (README/case-study polish) · Recommender NuGet extraction until a second
  consumer actually exists (Curio names itself as that second consumer in its own North Star —
  extraction still waits for Curio to actually reach that milestone, not for this line to exist).

### Durable constraints & lineage
- `Compass.Recommender` stays **PURE** (no Steam/IGDB/game types) — reuse asset; don't leak domain
  types into it. This becomes a shared-library contract (not just project hygiene) once extraction
  happens.
- Live pull was UNVERIFIED as of 2026-07-03 despite wired keys — memory + this file were corrected
  the same day (the stale "pending owner keys" claim cost a planning cycle; trust the secrets store,
  verify empirically). Still unverified as of the 2026-07-07 park — see `NORTHSTAR.md`'s recorded
  contradiction.

### Hygiene backlog (batch with any future touch)
- Delete merged remote branches (`ci/github-actions`, `docs/how-this-was-built`,
  `docs/roadmap-onboarding`); resolve the unclear `docs/quiet-build` branch (last commit
  2026-07-02). Deliberately not done in this session — cleanup stays a backlog item.
