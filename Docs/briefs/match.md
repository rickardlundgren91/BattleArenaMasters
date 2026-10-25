# Brief: the match screen ("Bygg matchskärmen")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md`, `Docs/DECISIONS.md`
and this brief in `/mnt/project-files/bam-unity/` first. Effort: medium (Opus). Your folder: `Assets/BAM/Game/` (BAM.Game).
`Boot.cs` and `GameFlow.cs` exist: keep what Boot does and replace GameFlow's placeholder match.

## Goal
A playable match on the phone. The rules (BAM.Sim) run at a fixed 60 Hz; the game layer draws them with ArtKit, plays
sounds with BamAudio, reads touch input, moves the camera, runs the HUD, the guided match, pause, the result screen and
the analytics events. Portrait, camera angled from above, 60 fps, no per-frame garbage.

## What exists
- Art (graphics thread): `Assets/BAM/Art/ArtKit.cs` and CONTRACTS.md: arena, unit views, projectiles, zones, aim, fx, bars.
- Audio (done): `BamAudio`, `SfxId`. UI (menus thread): `UiKit` (`CreateCanvas`, buttons, Lilita One), `Screens` (pause,
  result, hint bubble). Core: `MatchSetup`, `MatchResult`, `IGameFlow`/`Flow.Game`, `BamSettings` (LeftHanded, Graphics).
- The rules: now in `/mnt/project-files/bam-work/sim-dev/src/` (read `Match.cs`, `Unit.cs`, `Proj.cs`, `Events.cs`,
  `Defs.cs`). The main thread moves them into `Assets/BAM/Sim/` when they compile and tells you; until then write against
  these files.

## The rules' API in short
- `Match.Create(new MatchConfig { PlayerHero, Difficulty, Seed, Tutorial })`. Call `Step(in PlayerInput)` once per 1/60 s
  of unpaused time (at most 4 per frame); draw between ticks by interpolating `PX/PY -> X/Y` of units and projs.
- `State`: Countdown (Time -5..0, show 3-2-1) -> Play -> Over (`Winner`, 0 = the player's team). Also `Time`, `Level`,
  `Kills[2]`, `Stars[2]` (team stars 0-3), `Player`, `Towers`, `Units`, `Heroes`, `Projs`, `Events`, `SpecialDeniedAt`.
- `Unit`: `Serial` (never reused: key views by it), `Kind`, `Team`, `Hero`, `Minion`, `Alive`, `Hp`, `MaxHp`, `X`, `Y`,
  `Facing`, `Level`, `Charge01`, `SpecialReady`, `Flags`, `IsClone` (show Illusion only to its own team), `RespawnT`,
  `ShieldAmount`, `WalkT`/`MovedAt` (walk animation), `HDef` (TapReach, Drag, DragReach, DragWidth for the aim).
- `Proj`: `Flying` (a shot in the air), `IsZone`/`IsWarning` with `Zone` (ZoneKind: HoneyField, FireLane, BombFuse and the
  red warnings WarnLine/WarnCircle), `X`, `Y`, `Ang`, `ZoneLength`, `Radius`, `TimeLeft`, `Dur`, `Kind`, `Src`, `Tgt`
  (Knut's anchor: `Mode == ProjMode.Hook`, tether it to `Src`), `Serial`.
- `Events` (meanings in Events.cs), read after every Step: `AttackStart` -> `UnitView.PlayAttack(V)` on A for EVERY attack,
  `Hit` -> a hit effect on B every time (ARCHITECTURE rule 5), `Blocked` -> a small hit at X/Y, `Damage`/`Heal`,
  `Death`, `Respawn`, `Cast`, `Interrupt`, `LevelUp`, `TowerDown`, `CloneSpawn`/`CloneEnd`, `WolfSummon`/`WolfPounce`,
  `Shield`, `Ambush`, `Explosion`, `MatchStart`, `MatchOver`, `SpecialDenied`, `Ignite`, `Pull` -> sounds and effects.
- Visibility: `Match.IsVisibleTo(u, 0)` false -> hide u and its bar; `Match.HiddenFromFoes(Player)` -> the player's hero
  half transparent (unseen in grass).
- Map to world: point (x, y) -> `(x, 0, 35 - y)`; facing f -> forward `(cos f, 0, -sin f)`.
- `PlayerInput`: `MoveX/MoveY` = the joystick in map space (up on the screen = -y), length up to 1. `Special = true` on the
  tick a tap ends or a drag is released; `AimAngle` NaN for a tap, else the drag direction in map space; `AimK` = drag
  length share 0..1. Dragging back onto the button cancels. While the special is ready, show the `TapReach` ring.
  `BamSettings.LeftHanded` mirrors the joystick and the buttons.
- Guided match (`MatchConfig.Tutorial`, Tyra): `Match.Tut` with `Line` (the one hint to show, null = none), `Step`,
  `Opening` (the first 2.4 s: the camera rests 1.3 s on the enemy tower, then glides down to Tyra), `Dummy` (draw it as a
  practice target), `Guard`, `Tower`, `Done` (the tower fell: show the result). Port the old tutorial camera (`tutCamY`
  in old `SimLoop.cs`).

## Deliverables
1. Match driver in GameFlow: start (arena, views, HUD), step, pause (no stepping, `Screens.ShowPause`), restart, quit,
   result (`Screens.ShowResult` with a `MatchResult`), Android back = pause.
2. Views: one pooled view per unit/proj/zone by Serial, released when gone; heroes stay all match (`SetDead`).
3. Input: a floating joystick on the left half (right half when left-handed), the special button with charge ring and a
   ready glow, tap/drag/cancel, the aim preview.
4. Camera: follows the player's hero; while it is dead it follows a living teammate (no steering) and comes back at respawn.
5. HUD (essentials only, `UiKit`): timer, team kills, team stars, the player's respawn countdown, pause button,
   "Special not ready / no target" feedback on `SpecialDeniedAt`, the guided match's line via `Screens.ShowHint`.
6. Sounds for events (`BamAudio.PlayAt`, listener at the camera focus) and analytics (`BamAnalytics`: MatchStarted,
   MatchEnded, TutorialStep).

## Old references (read-only, ideas not code)
`/mnt/project-files/bam-ref/xyz/ABC2/Game/`: `Hud.cs`, `HudV3.cs`, `HudAim.cs`, `HudDualSpecials.cs`, `HudTelegraph.cs`,
`CombatFeel.cs`, `AbcGame.cs`, `Match.cs`.

## Steps
1. Read the docs and the APIs. 2. Write the game layer. 3. When the rules are in `Assets/BAM/Sim/`: `metagen.py`, then
`compilecheck.py` must say `all assemblies compile`. 4. Hand over to the main thread with `message_thread`: what is done,
what Rickard should test in Unity (exact clicks), what is left. Ask the graphics thread ("Gör spelets grafik i kod") for
missing art through the main thread.
