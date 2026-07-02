# Compass — agent/developer runbook

No ROADMAP.md in this repo. State lives in `docs/superpowers/specs/` (design docs) and
`docs/superpowers/plans/` (phased implementation plans), one pair per version (v1–v4). Check
the latest-dated plan file for current status and the phase checklist convention.

## What this is

Single-user Windows WPF app: pulls a Steam library + IGDB metadata into local SQLite, models
taste from playtime, recommends what to play next from the unplayed backlog. C#/.NET 10.
Solution `Compass.slnx`, 4-project layering `App → Data → Core → Recommender`:
- `src/Compass.Recommender` — **pure** engine (feature vectors/affinities only; no Steam/IGDB/DB
  knowledge), reusable standalone.
- `src/Compass.Core` — orchestration (RecommendationService, InsightsService, feedback).
- `src/Compass.Data` — SQLite storage/migrations.
- `src/Compass.App` — WPF-UI (Fluent dark theme) shell, MVVM (CommunityToolkit.Mvvm).

Public repo. v4 (insights + relevance feedback) is on `main`. Everything runs keyless off the
local SQLite cache (including a baked-in ~40-game sample library via Settings → Load sample
data). Live Steam/IGDB sync is gated on the owner's own API keys (Steam Web API key, IGDB/Twitch
Client ID+Secret) injected via `dotnet user-secrets` — not present in this environment.

## Commands

```powershell
# Build (Debug)
dotnet build src/Compass.App/Compass.App.csproj -c Debug

# Full test suite (xUnit + FluentAssertions, 3 test projects)
dotnet test

# Single test project
dotnet test tests/Compass.Core.Tests
dotnet test tests/Compass.Data.Tests
dotnet test tests/Compass.Recommender.Tests

# Run the app (from src/Compass.App), with an isolated temp DB for smoke-testing
dotnet run --project src/Compass.App -- --db <path-to-temp.db>

# One-time local secrets setup (never commit these)
dotnet user-secrets set "Steam:ApiKey"      "..."   # from src/Compass.App
dotnet user-secrets set "Igdb:ClientId"     "..."
dotnet user-secrets set "Igdb:ClientSecret" "..."
```

No `.github/workflows` — no CI. Verification is manual/local only.

## Verification harness

No dedicated `verify/` scripts or screenshot tooling in this repo. The plans use a recurring
**smoke-launch** pattern instead of screenshots: launch with a fresh temp `--db`, confirm the
process stays alive ≥5s with a non-zero `MainWindowHandle` and no XAML-parse crash, then kill
it. An empty `--db` renders the app's empty-state (expected, not a bug) — populated-page checks
require the owner to manually run Settings → Load sample data afterward; that step can't be
automated headlessly.

## Conventions & safety

- Work on a `feat/compass-vN` branch; land via **fast-forward merge to `main`** (not PR/squash),
  re-verify build+test on `main` after merging, then push `origin main`, then delete the feature
  branch. **Pause for owner confirmation before the public push** (standing pre-push rule — this
  is a public repo).
- Commit identity: `yovanmc <yovanmc@users.noreply.github.com>`, plain `git commit` (no
  `-c`/`--author` overrides).
- TDD phase loop per plan step: write failing test → run, confirm FAIL → implement → run,
  confirm PASS → commit. Conventional-commit style messages (`feat(core): ...`, `test(data):
  ...`, `fix(app): ...`, `docs: ...`).
- **Keep `Compass.Recommender` pure.** No Steam/IGDB/DB/feedback/insights concepts may leak into
  it — new metrics/features orchestrate existing engine primitives from `Compass.Core` instead of
  adding new ones to the engine itself. This is the one durable architectural constraint checked
  in every plan.
- Never commit secrets. Steam API key / IGDB Client ID+Secret go through `dotnet user-secrets`
  only; SteamID64 is non-secret and lives in `appsettings.json`.

## Cross-cutting gotchas

- Additive-only DB migrations (e.g. v4 added one `games.feedback` column) — no destructive schema
  rewrites; migration tests assert the version bump.
- The engine has no native "feedback" concept — explicit more/less-like-this is translated into
  weighted liked/disliked signal in `Compass.Core.RecommendationService`, gated so it doesn't
  double-count with the existing implicit-negative (`NotInterested`) branch.
- `Diversity = 0` must reproduce the pre-MMR ranking exactly — the diversity slider re-orders
  results only, it never changes the displayed match score.
- Large-library performance (scrolling/recompute cost) has bitten before (see `307bd02`) — watch
  recompute cost when touching the Recommend/Library hot path.
- Headless app testing is limited to process-alive + handle checks; there is no automated way to
  verify rendered UI content without the owner eyeballing a populated run.
