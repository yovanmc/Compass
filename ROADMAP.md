# Compass — ROADMAP
<!-- roadmap-schema: whitelist-v3 -->

## Definition

Single-user Steam **backlog manager + recommendation engine** for Windows: pulls the Steam library
and playtime into local SQLite, enriches it with IGDB metadata, models taste from what was actually
played, and recommends what to play next from the unplayed backlog. C#/.NET 10 WPF (+ WPF-UI, Fluent dark)
· SQLite · Steam Web API + IGDB v4 (+ keyless Steam cover art).
**`Compass.Recommender` is a deliberately PURE kNN+IDF+MMR unit** (knows nothing about Steam/IGDB/
games, only feature vectors + affinities) meant for reuse in other projects.
**Status: PARKED ENTIRELY.** No development, nothing queued. The one sanctioned action while parked is
the single ~30-min v5 live-pull verification session. Read `NORTHSTAR.md` before planning.
**Vision:** a standalone occasional-use game picker ("what do I play tonight") whose pure recommender
core is extracted into a shared library once Curio integrates it as the second consumer.
**Purpose:** a daily-use tool, NOT a portfolio artifact. Features beyond fixes wait for usage evidence.
Repo: github.com/yovanmc/Compass (PUBLIC) · this clone: `C:\Agent Projects\Compass`.

## Milestones

| # | Title | Status | Ready | Plan | Notes |
|---|-------|--------|-------|------|-------|
| v5 | Live-pull verification | [ ] | DEFERRED: project parked, this is the one sanctioned action | — | Keys wired, real pull has NEVER run. Verify Steam auth + library fetch, IGDB auth, appID match-rate, `external_games category=1` mapping. Fix whatever breaks. Run-and-fix milestone |
| U1 | Usage window (~3 weeks) | [ ] | BLOCKED: v5 | — | Use Compass at real "what do I play tonight" moments. Friction/perf/bugs logged as the v6 backlog. Usage-before-features gate |
| v6 | Usage-fed fixes (perf + bugs) | [ ] | BLOCKED: U1 | — | Backlog = the U1 log only. Speculative perf work declined |
| X | Recommender extraction | [ ] | BLOCKED: Curio recommender integration begins | — | Extract `Compass.Recommender` into a shared library once a second consumer exists. Compass becomes consumer #1 and the purity rule becomes the library contract |
| H | Remote branch cleanup | [ ] | BACKLOG | — | Delete merged remote branches (`ci/github-actions`, `docs/how-this-was-built`, `docs/roadmap-onboarding`) and resolve the unclear `docs/quiet-build` branch. Batch with any future touch |

## Pointers

- Vision, non-goals and the extraction trigger: [NORTHSTAR.md](NORTHSTAR.md) · Commands, conventions and safety: [CLAUDE.md](CLAUDE.md)
- Build: `dotnet build Compass.slnx -v minimal` · Test: `dotnet test Compass.slnx` · CI: `.github/workflows/ci.yml`
- Secrets: User Secrets (`compass-72b1f6a2-2026`) hold `Steam:ApiKey`, `Igdb:ClientId`, `Igdb:ClientSecret`. Never committed, placeholders only in `appsettings.json`
- Land work: branch → PR → `gh pr checks <#> --watch` → merge `--merge --delete-branch`. Commit as `yovanmc <yovanmc@users.noreply.github.com>`, plain `git commit` (never `--author`)
- Keyless demo: Settings → **Load sample data** (~40-game sample) / **Clear library** (reversible)
- WAL SQLite in tests needs `SqliteConnection.ClearAllPools()` in cleanup. `SQLitePCLRaw` is pinned to clear the bundled-SQLite CVE
- `Compass.Recommender` stays PURE: no Steam/IGDB/game types leak into it
- Declined: speculative perf/bug work before real usage, portfolio positioning, Recommender extraction before a second consumer exists
