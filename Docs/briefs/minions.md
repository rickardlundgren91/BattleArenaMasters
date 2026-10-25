# Brief: minions and the guided match ("Porta minionerna och övningsmatchen")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md` and this brief in
`/mnt/project-files/bam-unity/` first. Effort: medium (Opus). Your files: `Minions.cs` and `Tutorial.cs` in the rules
engine (now `/mnt/project-files/bam-work/sim-dev/src/`, later `Assets/BAM/Sim/`; the main thread tells you when it moves).
Both files hold the main thread's simple stand-ins so the rules compile: replace them completely. Do not edit other files;
ask the main thread ("Rebuild BAM in Unity for Play testing", `message_thread`) for anything you need there.

## Rules for the code
C# 9, `namespace BAM.Sim`, `public sealed partial class Match`, no UnityEngine. Deterministic: random numbers only from
`Rng` (Match.Rng). No allocations per tick (reuse lists, no LINQ, lambdas or closures). Map space: x right, y down the map;
team 0 starts at the bottom (its tower at (7.5, 29.5)), team 1 at the top (tower at (7.5, 5.5)). The old game's code is
the source of truth: port it faithfully. Read `Match.cs`, `Unit.cs`, `Arena.cs`, `Combat.cs`, `Movement.cs` first.

## 1. Lane minions (`Minions.cs`)
Port the old minion brain: `/mnt/project-files/bam-ref/xyz/ABC2/Game/SimMinionAI.cs` (the state machine: Advance,
Approach, Attack, Reposition, Return; 0.2 s target scan with lock and switch rules, own attack spot round the target,
stuck sidestep, friendly separation, sliding round towers, staying near the lane), `SimCreepLeash.cs` (no chasing into
bushes or far from the lane) and the wrapper `aiMinion` in `SimAI.cs` (its `tutStatic` branch: a static minion walks back
to its own spot, here `(SpawnX, SpawnY)`).
- Hook: `void AiMinion(Unit m, float dt)`, called every tick for a living minion that is not stunned. It decrements
  `m.AiT` itself. Also define `const float MnScan = 0.2f` (Match.NewMinion uses it).
- State lives on the Unit: `Wp`, `MbState`, `MbTgt`, `MbScanT`, `MbLockUntil`, `MbAng`, `MbStuckT`, `MbSideT`,
  `MbSideSign`, `MbTowerSide`, `MbDx`, `MbDy`, `MbGx/MbGy` (NaN = no goal yet), `MbPx/MbPy`, `AiT`, `Path` (PathBuf). Add
  fields to Unit only through the main thread.
- The lane: `Arena.LaneX/LaneY` are the waypoint centres in team 0's walking order (team 1 walks them in reverse);
  `Arena.LaneDist`, `Arena.LaneNearest`. Old `m.id` -> `Serial`; `minionAI.VisionRange` -> `Rules.MinionVision`,
  `MaxDistanceToLane` -> `Rules.MinionMaxToLane`.
- Already done elsewhere: the attack itself and its target pick (`Combat.UpdateAttack` uses `MbTgt` when it is in reach),
  the push between bodies with the old minion weights (`Movement.Separate`). Use `EnemiesInRange`, `IsVisibleTo`,
  `Map.LosClear`, `Map.Free`, `Map.FindPath`, `MoveUnit`, `SpeedOf`, `Nearest`.

## 2. The guided match (`Tutorial.cs`)
Port the old "first battle": `SimLoop.cs` (search `startTutorial`, `tutLine`, `updateTutorial`, `tutDamage`,
`tutorialDone`). The camera part (`tutCamY`) belongs to the match screen, not to you. Positions are the old ones mirrored
(y -> 35 - y): Tyra starts at (7.5, 19.0), the mark at (7.5, 16.2), the practice target at (7.5, 12.0), the guard Knut
walks at y 7.5 between x 9.0 and 10.3 at pace 0.5.
- Hooks the rules already call: `void StartTutorial()` (from the Match constructor; the towers exist, nothing else),
  `void UpdateTutorial(float dt)` (every tick while playing, before the units act), `float TutDamage(Unit src, Unit tgt,
  float amount, DmgKind kind)` (DealDamage calls it for `tgt == Tut.Tower` or `tgt == Tut.Dummy`).
- StartTutorial: `NoStars = true` before making units, the player's Tyra (team 0, `IsPlayer`, `Player`, no bot,
  `DmgMult = 2`, `TakenMult = 0.5`: the old onboarding "FirstOnboardingMatch"), `State = MatchState.Play`, `Time = 0` (no
  countdown), both towers disarmed for good, the practice target (a team 1 melee minion: `DmgOverride = 0`, `TutStatic`,
  `TutUnkillable`, disarmed, untargetable until the second step, health `Rules.TutorialMinionHp`), the guard (a team 1
  Knut hero with `Dummy = true`, disarmed and untargetable). No waves (the rules skip them while `Tut != null`).
- The public API the match screen uses (keep these names):
  ```csharp
  public sealed class Tutorial
  {
      public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepTower = 3;
      public const float OpenHold = 1.3f, OpeningTime = 2.4f;
      public int Step;            // the moment the player is in
      public string Line;         // the one hint to show now, null = none (English, as in the old game)
      public bool Opening;        // the first 2.4 s: the camera looks at the enemy tower
      public Unit Dummy, Guard, Tower;
      public float HitAt;         // the practice target was last hit at this time (the screen shakes it)
      public bool Done;           // the enemy tower fell: the guided match is complete
  }
  ```
  Everything else (counters, the burst) may be internal. The tower falls to exactly six of Tyra's auto attacks in the
  tower step, as in the old `tutDamage`; when it falls the rules end the match (team 0 wins) and you set `Done`.

## Steps
1. Read the old code and the new files. 2. Write `Minions.cs` and `Tutorial.cs`. 3. Compile with the test runner
(`Docs/briefs/simtest.md`, thread "Testa reglerna med botmatcher") or `dotnet build` of your own small project; play a
few matches and one guided match headless (feed `PlayerInput` that walks Tyra up the lane and taps the special when
`Player.SpecialReady`) and check the steps run to `Done`. 4. Hand over to the main thread: what is done, what you checked,
anything that differs from the old game and why.
