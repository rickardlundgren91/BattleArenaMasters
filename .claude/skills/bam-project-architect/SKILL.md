---
name: bam-project-architect
description: Use for planning, reviewing or changing Battle Arena Masters Unity rebuild code, architecture, scope and implementation workflows.
---
# BAM Project Architect — Unity 6.6 rebuild

## Authoritative source
Read the latest main and inspect `CLAUDE.md`, `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, `Docs/CONTRACTS.md`, the relevant `Docs/briefs/` and actual `Assets/BAM/` files. Report the exact SHA and affected modules. The private `rickardlundgren91/Battle-Arena-Masters` repository is the **player build**; `rickardlundgren91/XYZ` is the *old reference*, not the target. The public `rickardlundgren91/BattleArenaMasters` is the website — never put game instructions or assets there.

## Scope and contracts
- Unity 6000.6.5f1, URP 17.6, portrait 3v3, Low/High graphics. Never hardcode the hero list or scope: read the live code. Heroes = `Assets/BAM/Sim/HeroCatalog.cs` (`All` = playable, `Coming` = hidden); online play = `Assets/BAM/Online/` (protocol in `Mp/MpWire.cs`); progression/rating in the current code. Docs can lag behind code; when they disagree, code wins and you flag the doc. New game systems need Rickard's explicit yes. Online rule changes bump `MpWire.Protocol` and add one line to `Docs/PROTOCOL.md` (the history is no longer in the code comment).
- Protect D1 seeded-random eligible auto-target selection, visibility, line of sight, actual sim geometry, Tap/Drag rules, fixed 60 Hz tick, single tower objective, and explicitly approved Fenn wolf/clone rules.
- Keep `BAM.Sim` deterministic and free of UnityEngine. Respect separate assemblies: `Sim/`, `Core/`, `Game/`, `Art/`, `Audio/`, `UI/`, `Analytics/` and `Editor/`. Avoid circular dependencies and changing module-owned interfaces without a hand-off.
- New source and `.meta` additions under `Assets/BAM` require the project's metagen process. All permitted code changes need the repo's compilecheck, then a real Unity/Android test by Rickard. Compilation does not prove Unity behavior.
- Respect license registry, analytics consent, keystore secrecy, Android build flow, no ProjectSettings hand-edit, and `Docs/ARCHITECTURE.md` owner/handoff rules.

## Reporting and authorization
Classify claims as CURRENT CODE, LOCKED DECISION, PROPOSAL, or NOT VERIFIED. Include current SHA, exact paths, smallest safe plan, expected gameplay/graphics effects, tests actually run, tests still needed, and owner approvals. Do not implement, commit, merge, deploy or overwrite the real Unity project without appropriate explicit user authorization and project-owner coordination.
