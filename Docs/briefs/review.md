# Brief: check the new rules against the old game ("Granska reglerna mot gamla spelet")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md` and this brief in
`/mnt/project-files/bam-unity/` first. Effort: medium (Opus). Your folder: `/mnt/project-files/bam-work/review/`. You only
read the rules and report; the main thread fixes.

## Goal
The new rules engine (BAM.Sim) must play exactly like the old game, except for the decided changes. Find every place
where the port differs: a wrong number, a missing condition, a changed order of operations, an off-by-one, a wrong team
or y direction, a timer that counts differently, a unit kind that is included or left out by mistake.

## What to compare
- New: the files in `/mnt/project-files/bam-work/sim-dev/src/` (later `Assets/BAM/Sim/`; the main thread tells you):
  `Defs.cs`, `Arena.cs`, `Unit.cs`, `Proj.cs`, `Events.cs`, `Match.cs`, `Combat.cs` now; `Movement.cs`, `Heroes.cs`,
  `Minions.cs`, `Tutorial.cs` and `Bots/` as they land.
- Old (read-only, the source of truth): `/mnt/project-files/bam-ref/xyz/ABC2/Game/Sim*.cs` (`Sim.cs` dealDamage, kill,
  statuses; `SimLoop.cs` the tick, waves, levels, tutorial; `SimCombat.cs` movement, separation, attacks, shots, towers;
  `SimHeroes6.cs` the seven heroes; `SimDualSpecials.cs` tap/drag forms; `SimSpecials2.cs`; `SimMoaInstinct.cs`;
  `SimPhantomStep.cs`; `SimInterrupt.cs`; `SimTelegraph.cs`; `SimLos.cs`; `SimMinionAI.cs`; `SimCreepLeash.cs`;
  `SimPacing.cs`; `SimAmbush.cs`; the map in `SimMaps.cs`/`SimArena*.cs`) and the old data
  `/mnt/project-files/bam-ref/oldsim/dump/abc2_gamedata.json` (heroes' scripts, units, match rules).
- The old code has many switches (`ABC2_*` environment variables, `G.*` flags): only their default values count. Old
  features that are out (items, buff spots, Arena Core, spells, WoW classes, the RL brain, multiplayer, other heroes) are
  not differences.
- Decided changes (not differences): Fenn's wolf has a tap and a thrown form (6 tiles, walls stop it), Fenn's clone is a
  3 s decoy that deals no damage (DECISIONS.md "Rule changes"); the map is mirrored so the player's team is at the bottom
  (old team numbers and y directions are flipped: check that every flip is right); red warnings exist as zones.

## Output
`/mnt/project-files/bam-work/review/FINDINGS.md`: one entry per difference with the new file and line, the old file and
line, what differs, what it changes in a match, a severity (bug / deviation worth a look / harmless) and the fix you
suggest. Send the main thread a short note with `message_thread` ("Rebuild BAM in Unity for Play testing") after each
batch: first the files that exist now, later each new file when the main thread says it landed.
