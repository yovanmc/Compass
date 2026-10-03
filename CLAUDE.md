# Compass runbook

**Read `NORTHSTAR.md` before planning anything here.** Compass is parked, and that file names the
one action sanctioned while parked.

`ROADMAP.md` is the source of truth for status.

## What this is

Single-user Windows WPF app: pulls a Steam library + IGDB metadata into local SQLite, models
taste from playtime, recommends what to play next from the unplayed backlog. C#/.NET 10,
WPF-UI (Fluent dark) with CommunityToolkit.Mvvm. Solution `Compass.slnx`, layering
`App → Data → Core → Recommender`: `Compass.Recommender` is the **pure** engine (feature
vectors and affinities only), `Compass.Core` orchestrates, `Compass.Data` owns SQLite storage
and migrations, `Compass.App` is the shell.

Public repo. Everything runs keyless off the local SQLite cache (including a baked-in ~40-game
sample library via Settings → Load sample data). Live Steam/IGDB sync needs Yovan's own API keys
through `dotnet user-secrets` (setup in README, Local setup). They are absent from this environment.

## Commands

```powershell
dotnet build src/Compass.App/Compass.App.csproj -c Debug -v minimal
dotnet test
dotnet run --project src/Compass.App -- --db <path-to-temp.db>   # isolated temp DB for smoke tests
```

Test gate: `.github/workflows/ci.yml` · whole · ci · 1.4 min [V 2026-09-28 4a1e40dc]

CI builds with `-warnaserror` and tests on push and PR to `main`, the default branch. The smoke
launch below covers what CI cannot (rendered UI content).

## Verification harness

No `verify/` scripts or screenshot tooling. Verify with a **smoke launch**: start with a fresh
temp `--db`, confirm the process stays alive ≥5s with a non-zero `MainWindowHandle` and no
XAML-parse crash, then kill it. An empty `--db` renders the empty state (expected). Headless
checks stop at process-alive + handle. Populated pages need Yovan to run Settings → Load
sample data and look.

## Conventions & safety

- **Pause for Yovan's confirmation before the public push** (standing pre-push rule for this
  public repo).
- Conventional-commit style messages (`feat(core): ...`, `test(data): ...`, `fix(app): ...`,
  `docs: ...`).
- **Keep `Compass.Recommender` pure.** Steam, IGDB, DB, feedback and insights concepts stay out
  of it. New metrics and features orchestrate existing engine primitives from `Compass.Core`
  instead of adding new ones to the engine itself. This is the one durable architectural constraint.
- Secrets stay out of git. Steam API key and IGDB Client ID+Secret go through `dotnet user-secrets`
  only. SteamID64 is non-secret and lives in `appsettings.json`.

## Cross-cutting gotchas

- Additive-only DB migrations, no destructive schema rewrites. Migration tests assert the
  version bump.
- The engine has no native "feedback" concept. Explicit more/less-like-this is translated into
  weighted liked/disliked signal in `RecommendationService` (`src/Compass.Core/Taste/`), gated so
  it doesn't double-count with the existing implicit-negative (`NotInterested`) branch.
- `Diversity = 0` must reproduce the pre-MMR ranking exactly. The diversity slider re-orders
  results only and never changes the displayed match score.
- Large-library scrolling and recompute cost is fragile. Watch it on the Recommend/Library hot
  path.
