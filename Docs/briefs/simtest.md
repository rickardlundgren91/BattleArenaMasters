# Brief: test the rules with bot matches ("Testa reglerna med botmatcher")

_Written for the 7-hero offline MVP; current scope in `Docs/DECISIONS.md`._

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md` and this brief in
`/mnt/project-files/bam-unity/` first. Effort: medium (Opus). Your folders: `/mnt/project-files/bam-tools/simrunner/`
(the runner) and `/mnt/project-files/bam-work/simtest/` (builds, results, scripts).

## Goal
A fast headless runner for the new rules engine (BAM.Sim) on .NET 8, and old-vs-new numbers side by side, so the main
thread and the bots thread see whether the new game plays like the old one.

## The new runner
- A .NET 8 console project (LangVersion 9) that compiles every `.cs` under a sim folder given as an argument: now
  `/mnt/project-files/bam-work/sim-dev/src/` plus `/mnt/project-files/bam-unity/Assets/BAM/Sim/Ids.cs` and `HeroCatalog.cs`;
  later `Assets/BAM/Sim/` alone (the main thread tells you when it moves). No UnityEngine anywhere.
- `/mnt/project-files` is mounted noexec: run builds with `dotnet path/to/runner.dll`; put obj/bin where it works (a local
  folder is fine).
- A match: `Match.Create(new MatchConfig { Seed = s, Difficulty = ..., AllyDifficulty = ..., PlayerIsBot = true,
  SlotOrder = true, PlayerHero/Allies/Enemies drawn from the 7 heroes with the seed })`, then `Step(PlayerInput.None)` until
  `State == MatchState.Over` or `Time >= 900` (a timeout, winner 0). Read `Winner`, `Time`, `Kills`, and per hero `Kills`,
  `Deaths`, `Assists`, `Level`.
- Options like `--sim <dir> --tier Platinum | --tiers Gold:Bronze --seed0 1 --n 300 --par 8 --out file`. Easy/Normal/Hard
  map to Bronze/Gold/Platinum; for Silver ask the main thread for a config field.
- Output one line per match in the old runner's format (see the old `Runner.cs`) so `summarize.py` reads both.
- Checks: determinism (the same seed twice gives the same result and the same position hash every 600 ticks), garbage
  (`GC.GetAllocatedBytesForCurrentThread` per tick after warm-up; the goal is 0), speed (ticks per second). Report any
  static mutable state you find in the sim (matches must be able to run in parallel threads).

## The old runner (the baseline)
`/mnt/project-files/bam-ref/oldsim/src/` (`bin/runner.dll`, source `Runner.cs`), data in
`/mnt/project-files/bam-ref/oldsim/dump/abc2_gamedata.json`. Command (copy the folder somewhere local if dotnet refuses
the mount):
`cd .../oldsim/src && ABC2_POOL="Bram,Fenn,Tyra,Moa,Knut,Tikta,Blast" ABC2_ITEMS=0 ABC2_BUFFSPOTS=0 ABC2_CORE=0 dotnet bin/runner.dll ../dump/abc2_gamedata.json skeppad <Tier or A:B> <seed0> <n> 0.016666668`.
Earlier results and `summarize.py` are in `/mnt/project-files/bam-ref/oldsim/baseline/`. Known old numbers: Platinum vs
Platinum (400 matches) mean 227.7 s, median 218.3 s, 8.1 kills per match; Platinum beats Bronze in 94.5 % of matches.

## Report
A script that prints old vs new side by side: match time (mean, median, p10, p90), kills per match, timeouts, side and tier
win rates, per-hero win rate and K/D. Results go to `/mnt/project-files/bam-work/simtest/`.

## Steps
1. Get the old runner going and reproduce the baseline (a few hundred matches is enough).
2. Write the new runner; it compiles once the main thread has finished the rules (it tells you; until then a missing-file
   error is expected).
3. Run the comparison and hand over to the main thread with `message_thread` (numbers, how to run it). Later, run it when
   the main thread or the bots thread ("Porta bottarna") asks.
