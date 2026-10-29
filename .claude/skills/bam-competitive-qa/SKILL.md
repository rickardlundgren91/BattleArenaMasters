---
name: bam-competitive-qa
description: Use for BAM combat rules, deterministic sim tests, bot matchmaking, hero balance, gameplay regression and future ranked or tournament design.
---
# BAM Combat QA & Competitive Design

Read latest `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, `Docs/CONTRACTS.md`, `Assets/BAM/Sim/` and existing `Tests/` before specifying or running a test. Never hardcode the hero list or scope: read the live code. Heroes = `Assets/BAM/Sim/HeroCatalog.cs` (`All` = playable, `Coming` = hidden); online play = `Assets/BAM/Online/` (protocol in `Mp/MpWire.cs`); progression/rating in the current code. Docs can lag behind code; when they disagree, code wins and you flag the doc. Only claim a system (online PvP, rating, mastery, tournaments) exists if you found it in code. For systems not in code, label everything DESIGN ONLY and request Rickard's scope decision before coding. Online rule changes bump `MpWire.Protocol`.

## Tests
- Preserve deterministic fixed-60-Hz sim, seeded D1 auto targeting among eligible visible units, proper wall LOS, Tap/Drag, minion/tower objectives, Fenn's actual 6-tile wolf throw/decoy.
- For changes, use fixed seeds, side swaps, matching hero compositions, identical baseline and candidate builds, error margins and explicit sample sizes. Distinguish headless rule checks from Unity input/performance/visual tests.
- Cover no extra hits, wall collision, false damage VFX, null/dead units, cross-frame event timing, replay determinism, result correctness, rewards only if such features genuinely exist.
- For a future competitive design, evaluate matchmaking integrity, skill-based rating, ranking abuse, disconnects and fairness as proposed requirements, never as deployed state.

Deliver traceable evidence: commit SHA, test names and commands, number of runs, observed outputs, failing seeds, expected vs actual and unresolved risks. Don't announce "all tests green" without a real run. Work with the dedicated sim owner/main thread for any rule changes.
