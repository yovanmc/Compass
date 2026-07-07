# Compass — North Star (2026-07-07 grilling session)

## What this builds up to be (end-state vision, owner-locked 2026-07-07)
**Compass, finished, is a standalone occasional-use game picker that shares its extracted recommender core with Curio.** Two decisions define the end state:

- **Standalone product, permanently.** Compass remains a living app for "what do I play tonight" even after Curio integrates the recommender for media — games and media library are distinct use cases with distinct feature vectors. It does not get absorbed or archived as a donor.
- **The core gets extracted for real.** Curio's end-state vision (locked this session) names the Compass pure-recommender core as its media-recommendation engine. That makes **two consumers**, which is exactly the standing trigger for the NuGet/shared-library extraction — so the extraction genuinely happens at that Curio milestone, and `Compass.Recommender`'s purity rule becomes a shared-library contract, not just project hygiene.
- **Finished Compass, concretely:** live pull verified and working; the real backlog in the DB; recommendations trusted enough that the owner actually consults it on game nights; relevance feedback tuning the model over time. Small, done, occasionally used — like a good kitchen tool.
- **Never:** multi-store aggregation, social features, non-Steam sources, portfolio positioning.

**v-final test:** on a real game night, it recommends something from your actual backlog, you play it, and the pick was better than scrolling the library.

## Path to v-final (rough build outline, 2026-07-07)
Smallest path in the portfolio. The park governs timing; this is the whole remaining map.

**Step 1 — v5: the 30-minute live-pull verification (the one sanctioned action).** Run the real Steam + IGDB pull on the owner's machine; fix whatever auth/fetch/appID-matching bugs surface (code that has never run once usually has some). Success = the owner's actual backlog in the local DB.

**Step 2 — U1: short real-usage window.** Use it on a few game nights; log friction; nothing speculative.

**Step 3 — v6: friction fixes only**, seeded by U1. Then Compass is functionally v-final as an app: dormant-but-working, consulted on game nights, relevance feedback slowly tuning the model.

**Step 4 — The extraction milestone (triggered by Curio, not by Compass).** When Curio's Phase-4 recommender integration begins, extract `Compass.Recommender` into a shared library (two consumers now exist — the standing trigger fires); Compass becomes consumer #1 of the shared package; purity rule becomes the library's contract. Why Curio-triggered: extraction before a second consumer is speculative packaging — the standing decision holds until the trigger is real.

**Maintenance forever after:** nothing, unless it breaks on a game night.

## North Star (operating identity)
An **occasional-use backlog picker** ("what do I play tonight") built around a deliberately pure, reusable recommender core (kNN+IDF+MMR, zero package references). Status as of this session: **PARKED entirely, by owner decision** — no development, nothing queued. The park governs *when* anything happens; the vision above governs *what* it builds toward.

## Owner decisions locked this session
1. **The problem is real:** owner confirms backlog-choice paralysis recurs regularly.
2. **Parked anyway, including the v5 live-pull verification.** Owner chose "park entirely" with the pushback in view.

## The recorded contradiction (read this first on unpark)
These two answers conflict: a regularly recurring problem + a working tool parked *unverified*. The live Steam pull has never run once — the user-secrets file is untouched since the day the keys went in (2026-06-19), no real DB exists anywhere, and the recommender has only ever seen its baked-in 40-game sample. Four milestones of engineering are real in the test suite and unreal in the world. Thirty minutes closes that gap.

**The one sanctioned action while parked:** a single ~30-minute live-pull session (v5) on the owner's machine, whenever the mood strikes — ideally the next time "what do I play tonight" actually bites. That session either makes the whole project real or surfaces the auth/fetch/appID bugs it almost certainly has (code that has never run once usually doesn't run once). Nothing else is permitted under the park.

## On unpark (only after v5 has run)
- U1: short real-usage window — use it to pick games a few evenings; log friction.
- v6: fixes seeded ONLY by U1 findings (standing decision, reaffirmed).

## Non-goals (standing, reaffirmed — do not re-propose)
- Speculative perf/bug work before real usage.
- Portfolio positioning polish (owner declared Compass explicitly not a portfolio artifact, 2026-07-03).
- Recommender NuGet extraction until a second consumer actually exists.
- Any impurity leaking into `Compass.Recommender` (no Steam/IGDB/DB/feedback types).

## Hygiene backlog (batch with any future touch)
- CLAUDE.md is stale and self-contradictory (claims no ROADMAP, no CI — both false since 2026-07-02/03).
- Delete merged remote branches (`ci/github-actions`, `docs/how-this-was-built`, `docs/roadmap-onboarding`); resolve the unclear `docs/quiet-build` branch (last commit 2026-07-02).

## Kill criteria
None needed. Compass is finished software with one unverified seam. The pure recommender core is a durable reusable asset regardless of whether the app is ever used. The only failure mode left is pretending v1–v4 are "done" while the pull stays forever unrun — the contradiction section above exists so that pretense has to be re-read every time the project is touched.
