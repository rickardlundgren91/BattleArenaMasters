---
name: bam-development-director
description: Use for Battle Arena Masters Unity rebuild development across architecture, VFX, 3D animation, Android performance, combat QA and gameplay marketing.
---
# BAM Master Skill — Rickard's player-version Unity repository

The authoritative build repository is `rickardlundgren91/Battle-Arena-Masters` (private). Read current main's `CLAUDE.md`, `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, `Docs/CONTRACTS.md` and affected `Assets/BAM/` code **before** advising, implementing or reviewing. The old `rickardlundgren91/XYZ` is only a reference/test bed. The public `rickardlundgren91/BattleArenaMasters` is the website, not the Unity game.

## Scope and fixed rules
Unity 6.6/URP 17.6, portrait 3v3. Never hardcode the hero list or scope: read the live code. Heroes = `Assets/BAM/Sim/HeroCatalog.cs` (`All` = playable, `Coming` = hidden); online play = `Assets/BAM/Online/` (protocol in `Mp/MpWire.cs`); progression/rating in the current code. Docs can lag behind code; when they disagree, code wins and you flag the doc. D1 seeded-random eligible auto targeting; line of sight; Tap/Drag specials; fixed tick; one tower to destroy. Fenn's approved 6-tile thrown wolf stops on walls, and the clone is a non-damaging 3-second decoy. New systems (items, tournaments, ranked changes, new heroes) need Rickard's explicit yes. Never smuggle these in through an art or QA task.

Every attack and confirmed hit must have a visible animation/effect on both Low and High, regardless of unit type. Sim events are the source for animation, projectile and hit logic; Art must not invent gameplay. Never change competitive rules to fix a graphical symptom.

## Choose a workflow
**Art/VFX:** Review real `BAM.Art` methods, `ArtKit`, `UnitView`, `ProjKind`, `ZoneKind`, `FxKind` and actual view code. Analyze motion, release/contact, true hazard geometry, silhouette, feedback, special identity, sensory overload and mobile cost. Require 3v3 shots at true camera zoom and Android review. Old XYZ `View3D*.cs` is not automatically part of the new project.

**3D/models:** Validate provenance in `Docs/LICENSES.md`; check Tripo/Meshy/Mixamo, scale, UVs, rig, separate accessories, hit timing, idle/run/turn/backpedal, import checks, `.meta` and mobile geometry.

**Performance:** Verify actual bottleneck on a specified device using Unity Profiler and device metrics; target 60 FPS subject to real tests and documented budgets. Don't remove mandatory attack cues; reduce optional particles or waste first.

**Combat/competitive:** Preserve `BAM.Sim` determinism. Use existing `Tests/` and headless sim runner only when genuinely available. State actual sample sizes. Future ELO/ranked/tournaments require separately approved design.

**Highlights:** Use genuine available footage or give a capture/edit plan. Don't claim videos were created or publish on user's behalf.

## Safeguards
Do not change ProjectSettings manually, reveal keystores, send analytics before consent, or add assets with unclear rights. Keep ownership and handoffs defined in `Docs/ARCHITECTURE.md`. Work by review/proposal unless explicitly permitted; never merge or upload a build without owner approvals. The main thread controls compiled commits and synchronization to Rickard's PC; only Rickard verifies Unity and Android visually. Report main SHA, evidence, files, actual tests, untested claims and signoff needed.
