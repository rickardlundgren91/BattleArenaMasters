using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BAM.Sim
{
    // The BAM Precision Test Suite's harness (issue #2, part 8). A partial Match, so the tests reach the rules directly, as the
    // hero tests do. Each test records PASS, FAIL with a priority (P0-P3, the issue's scale) or INFO (a measurement). A FAIL is a
    // finding for review, not a broken build: run.sh exits 0 unless --strict.
    public sealed partial class Match
    {
        internal sealed class PrecResult { public string Id, Area, Status, Prio, Detail; }
        internal static readonly List<PrecResult> PrecResults = new List<PrecResult>();

        static void Pass(string id, string area, string detail) => PrecResults.Add(new PrecResult { Id = id, Area = area, Status = "PASS", Prio = "", Detail = detail });
        static void Fail(string id, string area, string prio, string detail) => PrecResults.Add(new PrecResult { Id = id, Area = area, Status = "FAIL", Prio = prio, Detail = detail });
        static void Info(string id, string area, string detail) => PrecResults.Add(new PrecResult { Id = id, Area = area, Status = "INFO", Prio = "", Detail = detail });
        static void Verdict(string id, string area, bool ok, string prio, string detail) { if (ok) Pass(id, area, detail); else Fail(id, area, prio, detail); }

        // ---------- set-ups ----------
        /// <summary>A match in play with no minions, no waves and no towers shooting; the player is a person (seat 0, no bot).</summary>
        static Match PMake(HeroId player, HeroId[] allies, HeroId[] enemies, int seed = 7)
        {
            var m = Create(new MatchConfig { PlayerHero = player, Allies = allies, Enemies = enemies, Seed = seed });
            for (int i = 0; i < m.Units.Count; i++) { var u = m.Units[i]; if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.spawns.Clear(); m.WaveT = 1e9f;
            m.Towers[0].Alive = false; m.Towers[1].Alive = false; // no tower shots in the measurements (Alive false, no TowerDown)
            m.State = MatchState.Play; m.Time = 10;
            m.Events.Clear();
            return m;
        }

        /// <summary>Takes a hero out of the measurement: dead, never back.</summary>
        static void POff(Unit u) { u.Alive = false; u.Hp = 0; u.RespawnT = 1e9f; u.X = u.PX = 0.5f; u.Y = u.PY = 0.5f; }

        /// <summary>A hero that only stands there: no bot, no seat, full health, no statuses.</summary>
        static void PDummy(Unit u) { u.Bot = null; if (u.Seat >= 0 && !u.IsPlayer) u.Seat = -1; }

        static void PPlace(Unit u, float x, float y) { u.X = u.PX = x; u.Y = u.PY = y; u.Hp = u.MaxHp; u.ClearStatuses(); u.Alive = true; u.KnockT = 0; }

        Unit PFoe(HeroId h) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Hero == h && Heroes[i].Team != Player.Team) return Heroes[i]; return null; }
        Unit PAlly(HeroId h) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Hero == h && Heroes[i].Team == Player.Team && Heroes[i] != Player) return Heroes[i]; return null; }

        /// <summary>Steps n ticks with input (the special only on the first) and returns every event, in order.</summary>
        List<SimEvent> PRun(int n, PlayerInput first, PlayerInput rest, List<SimEvent> into = null, Action<Match, int> each = null)
        {
            into = into ?? new List<SimEvent>();
            for (int k = 0; k < n; k++)
            {
                Step(k == 0 ? first : rest);
                into.AddRange(Events);
                each?.Invoke(this, k);
            }
            return into;
        }

        static PlayerInput PTap() { var i = PlayerInput.None; i.Special = true; return i; }
        static PlayerInput PDrag(float ang, float k = 1) { var i = PlayerInput.None; i.Special = true; i.AimAngle = ang; i.AimK = k; return i; }

        /// <summary>A bot match of 6 bots (the player too), as the stability runs do.</summary>
        static Match PBotMatch(int seed, HeroId hero)
        {
            return Create(new MatchConfig { PlayerHero = hero, Seed = seed, PlayerIsBot = true, Difficulty = Difficulty.Hard, AllyDifficulty = Difficulty.Hard });
        }

        static ulong PEventHash(List<SimEvent> ev)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                h = Mix(h, (ulong)e.Type); h = Mix(h, (ulong)e.ASerial); h = Mix(h, (ulong)e.BSerial);
                h = Mix(h, Bits(e.X)); h = Mix(h, Bits(e.Y)); h = Mix(h, Bits(e.V)); h = Mix(h, e.Kind); h = Mix(h, e.Big ? 1UL : 0UL); h = Mix(h, (ulong)e.Dmg);
            }
            return h;
        }

        // MatchViews' hit rule: by shooter + kind (HitKey, main up to 133cee3) or, once SimEvent.Shot exists (the SHOT-01 fix), by the
        // shot itself. Read by reflection so the suite builds and models the view on both sides of that patch.
        static readonly System.Reflection.FieldInfo PShotField = typeof(SimEvent).GetField("Shot");
        internal static bool PViewKeysByShot => PShotField != null;
        internal static int PShotOf(SimEvent e) => PShotField != null ? (int)PShotField.GetValue(e) : 0;

        static string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        // ---------- the run ----------
        public static int RunPrecision(string reportPath, bool strict)
        {
            var tests = new (string name, Action run)[]
            {
                ("Math", PrecMath), ("Targeting", PrecTargeting), ("Walls", PrecWalls), ("Hits", PrecHits),
                ("Interrupts", PrecInterrupts), ("Input", PrecInput), ("Geometry", PrecGeometry), ("Release", PrecRelease),
                ("BotMatches", PrecBotMatches), ("Determinism", PrecDeterminism), ("Lockstep", PrecLockstep), ("Scale", PrecScale), ("Tyra", PrecTyra), ("BramKnut", PrecBramKnut), ("Shots", PrecShots), ("Aim", PrecAim),
            };
            foreach (var t in tests)
            {
                try { t.run(); }
                catch (Exception ex) { Fail("CRASH_" + t.name, t.name, "P0", "the test threw: " + ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n')[0]); }
            }

            int pass = 0, fail = 0, info = 0;
            var md = new StringBuilder();
            md.AppendLine("| # | Test | Area | Result | Detail |");
            md.AppendLine("|---|---|---|---|---|");
            int n = 0;
            foreach (var r in PrecResults)
            {
                n++;
                if (r.Status == "PASS") pass++; else if (r.Status == "FAIL") fail++; else info++;
                string res = r.Status == "FAIL" ? "FAIL " + r.Prio : r.Status;
                Console.WriteLine($"{res,-9} {r.Id,-46} {r.Detail}");
                md.AppendLine($"| {n} | `{r.Id}` | {r.Area} | {res} | {r.Detail.Replace("|", "/")} |");
            }
            string sum = $"precision tests: {pass} pass, {fail} fail, {info} info ({PrecResults.Count} results)";
            Console.WriteLine(sum);
            md.AppendLine().AppendLine(sum);
            if (!string.IsNullOrEmpty(reportPath)) File.WriteAllText(reportPath, md.ToString());
            return strict && fail > 0 ? 1 : 0;
        }
    }
}

static class PrecisionMain
{
    static int Main(string[] a) => BAM.Sim.Match.RunPrecision(a.Length > 0 ? a[0] : null, a.Length > 1 && a[1] == "--strict");
}
