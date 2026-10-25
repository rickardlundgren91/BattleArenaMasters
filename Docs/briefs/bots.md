# Brief: the bots ("Porta bottarna")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md` and this brief in
`/mnt/project-files/bam-unity/` first. Effort: medium (Opus). Your folder: `Bots/` inside the rules engine (now
`/mnt/project-files/bam-work/sim-dev/src/Bots/`, later `Assets/BAM/Sim/Bots/` when the main thread moves the rules into
the project; it tells you). The rest of BAM.Sim belongs to the main thread.

## Goal
Bots that play the new game exactly like the old game's bots: Easy = old Bronze, Normal = old Gold, Hard = old Platinum
(all bots of a match share one tier). Port the old bot code faithfully; do not invent new behaviour. Success = the new
game's bot-vs-bot numbers match the old game's (the test thread's runner, `Docs/briefs/simtest.md`).

## The new rules engine (BAM.Sim)
- Read `Match.cs`, `Unit.cs`, `Proj.cs`, `Defs.cs`, `Arena.cs`, `Combat.cs`, `Events.cs` in the sim folder. The main thread
  is still writing `Movement.cs`, `Minions.cs`, `Heroes.cs`, `Tutorial.cs`; it tells you when everything compiles.
- C# 9, `namespace BAM.Sim`, no UnityEngine. Deterministic: random numbers only from `Match.Rng`. No allocations per tick
  (reuse lists; no LINQ, lambdas, closures, `new` in per-tick code). 60 ticks per second.
- Map space: x to the right, y down the map (row 0 = the top). Team 0 starts at the bottom (y 33.5), team 1 at the top
  (y 1.5), towers at (7.5, 29.5) and (7.5, 5.5), the lane runs along x = 7.5. The old game had its teams 1/2 the other way
  round in places: check every "team == 1" in the old code.
- Your contract (Match.cs already uses it):
  ```csharp
  public sealed class BotBrain
  {
      public readonly Unit H; public readonly BotTier Tier; public float Skill;
      public BotBrain(Unit h, BotTier tier, Match m);
      public void Think(float dt); // once per tick for a living hero that can act (not stunned) and is not casting
  }
  ```
  Move with `m.MoveUnit(h, dx, dy, dt)` (a direction; length 1 = full speed). Cast with `m.TryCastSpecial(h, aimAngle, aimK)`:
  `aimAngle` NaN = the tap form (the rules pick the target as in the old game, D1), otherwise the drag form along `aimAngle`
  (map-space radians) with `aimK` 0..1 = the drag length share. Paths: `m.Map.FindPath(...)` into a `PathBuf` you own.
  Lead aim for taps is already in the rules (`Match.LeadAim` uses `BotTier.AimLead/AimNoise` and `Skill`).
- Read freely: `Match`, `Unit`, `Proj` (red warnings are Projs with `IsWarning`: WarnLine/WarnCircle, X/Y, Ang, D,
  Radius, TimeLeft), `Match.IsVisibleTo` (bots react only to what their team sees, like the old `sees()`),
  `Unit.LastSeenX/Y/T`, `Unit.CastSeen`, `Unit.MoVx/MoVy`. The tiers' old values are in `Defs.cs` (`BotTier`); put any other
  per-tier tables in your own files.
- Need something from the rules that is not there? Ask the main thread with `message_thread` (thread "Rebuild BAM in
  Unity for Play testing"). Do not edit files outside `Bots/`.

## The old bots (read-only, the source of truth)
`/mnt/project-files/bam-ref/xyz/ABC2/Game/`: `SimAI.cs` (aiHero: lane, fights, retreat, dodging, bushes), `SimTeamplay.cs`,
`SimThreat.cs`, `SimBotDrag.cs` (when and where bots drag), `SimBotRoles.cs`, `SimCoopTier.cs`, `SimHeroes6.cs`
(`newHeroSpecialWanted`), `SimSpecials2.cs` (`v2Wanted`, `sureShot`, `dragTier`), `SimTelegraph.cs`, `SimAim.cs`. Old game
data: `/mnt/project-files/bam-ref/oldsim/dump/abc2_gamedata.json`.
Not in the new game (drop those branches): items, buff spots, the Arena Core, battle spells, WoW classes, the RL brain
(`SimBrain*`), multiplayer, every hero except Bram, Fenn, Tyra, Moa, Knut, Tikta and Blast. All old `ABC2_*` switches stay
at their defaults: keep only the default path, with the tuning values (TK, TK3) that path uses.
Rule changes that matter for bots: Fenn's wolf passes units but walls stop it (it walks round them, needs a clear line to
bite); his tap calls it to follow him and bite, his drag throws it up to 6 tiles where it homes on an enemy hero (else it
bites minions), so a bot drags it at a hero it can reach in a clear line. Fenn's passive makes a 3 s decoy clone (no
damage, gone at the first hit) when an enemy hero hits him, once per 18 s.

## Steps
1. Read the old bot code; map every old helper to the new API in `/mnt/project-files/bam-work/bots/NOTES.md`.
2. Write `Bots/*.cs`, allocation-free in `Think`.
3. When the main thread says the rules compile: run the test runner, compare old and new numbers, fix what the port broke.
4. Hand over as in ARCHITECTURE ("Working as a helper thread"): what is done, the numbers old vs new, what is left.
