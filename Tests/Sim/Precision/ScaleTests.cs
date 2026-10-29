using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Scale tests of the BAM Precision Test Suite (thread "Kör headless precisionstester", 2026-10-29): the same rules as CoreTests at
// bot-match scale (determinism over 200 matches and in parallel threads, D1 spread, LOS against exact geometry, every hero's auto,
// HitKey truth per shot). Helpers are prefixed S to stay clear of Harness.cs. Run by Harness.RunPrecision ("Scale").
namespace BAM.Sim
{
    public sealed partial class Match
    {
        // ------------------------------------------------------------------ bookkeeping
        // results go into the shared harness (Harness.cs): Verdict / Info. Ids are prefixed Scale_ and carry the finding id they guard.
        static string sArea = "Scale";
        static readonly Dictionary<string, string> sPrio = new Dictionary<string, string>
        {
            { "DET", "P0" }, { "D1", "P0" }, { "LOS", "P0" }, { "HIT", "P0" }, { "ZERO", "P0" }, { "INT", "P1" },
        };
        static void SCheck(bool ok, string id, string what)
        {
            string fam = id.Split('-')[0];
            Verdict("Scale_" + id, sArea, ok, sPrio.TryGetValue(fam, out var p) ? p : "P2", what);
        }
        static int sNote;
        static void SInfo(string s) => Info($"Scale_{sArea}_note{++sNote}", sArea, s.Trim());
        static void SHead(string s) { sArea = s.Split(' ')[0]; }

        // ------------------------------------------------------------------ a quiet test match
        /// <summary>A match in Play with no bots (heroes stand still unless moved), no waves, towers stunned, no minions.</summary>
        static Match SMake(HeroId player, HeroId[] allies, HeroId[] enemies, int seed = 7)
        {
            var m = Create(new MatchConfig { PlayerHero = player, Allies = allies, Enemies = enemies, Seed = seed, PlayerIsBot = true });
            for (int i = 0; i < m.Units.Count; i++)
            {
                var u = m.Units[i];
                if (u.Kind == UnitKind.Minion) u.Alive = false;
                u.ClearStatuses(); u.Bot = null;
                if (u.Kind == UnitKind.Tower) { u.Hp = u.MaxHp = 1e9f; u.AddRaw(St.Stun, 1e9f, 0, 0, null, false); u.AddRaw(St.Untargetable, 1e9f, 0, 0, null, false); }
            }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f;
            m.Events.Clear();
            return m;
        }
        // parks every hero but the named ones far away in the bottom-right corner, stunned and untargetable
        void SPark(params Unit[] keep)
        {
            int k = 0;
            foreach (var h in Heroes)
            {
                if (Array.IndexOf(keep, h) >= 0) continue;
                h.X = h.PX = 1.5f + (k % 3) * 1.2f; h.Y = h.PY = 33.5f - (k / 3) * 0.0f; k++;
                h.ClearStatuses(); h.AddRaw(St.Stun, 1e9f, 0, 0, null, false); h.AddRaw(St.Untargetable, 1e9f, 0, 0, null, false);
            }
        }
        static void SPlace(Unit u, float x, float y) { u.X = u.PX = x; u.Y = u.PY = y; u.Hp = u.MaxHp; }
        Unit SFoe(HeroId h) { foreach (var u in Heroes) if (u.Hero == h && u.Team != Player.Team) return u; return null; }
        Unit SAlly(HeroId h) { foreach (var u in Heroes) if (u.Hero == h && u.Team == Player.Team && u != Player) return u; return null; }
        void SRevealAll() { foreach (var u in Units) u.RevealT = 99; }
        void SStep(int n = 1, bool reveal = true)
        {
            for (int i = 0; i < n; i++) { if (reveal) SRevealAll(); Step(PlayerInput.None); }
        }
        static float SDist(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        static float SAngTo(Unit a, float x, float y) => SimMath.Atan2(y - a.Y, x - a.X);
        bool SCast(Unit h, float ang, float k = 1)
        {
            h.Charge = h.HDef.MaxCharge;
            return TryCastSpecial(h, ang, k);
        }
        void SFreeze(Unit u) { u.AddRaw(St.Stun, 1e9f, 0, 0, null, false); } // cannot act or attack; still hittable

        // ======================================================================================== 1. determinism
        struct SRunResult { public ulong TickHash, EventHash; public int Winner; public float Time; public long Ticks; }

        static ulong SMix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
        static ulong SBits(float f) => (uint)BitConverter.SingleToInt32Bits(f);
        static ulong SHashEvents(ulong h, List<SimEvent> ev)
        {
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                h = SMix(h, (ulong)e.Type); h = SMix(h, (ulong)e.ASerial); h = SMix(h, (ulong)e.BSerial); h = SMix(h, SBits(e.V));
                h = SMix(h, e.Kind); h = SMix(h, SBits(e.X)); h = SMix(h, SBits(e.Y)); h = SMix(h, e.Big ? 1UL : 0UL);
            }
            return h;
        }

        static MatchConfig SBotConfig(int seed, int heroes, Difficulty d)
        {
            var cfg = new MatchConfig { Seed = seed, Difficulty = d, PlayerIsBot = true, SlotOrder = true };
            var r = new Random(seed * 7919 + 13);
            var pool = Enumerable.Range(0, heroes).Select(i => (HeroId)i).OrderBy(_ => r.Next()).ToList();
            cfg.PlayerHero = pool[0];
            if (heroes > HeroCatalog.Count) { cfg.Allies = new[] { pool[1], pool[2] }; cfg.Enemies = new[] { pool[3], pool[4], pool[5] }; }
            return cfg;
        }

        static SRunResult SRun(MatchConfig cfg, int maxTicks, Func<Match, long, PlayerInput[]> inputs = null)
        {
            var m = Create(cfg);
            ulong th = 14695981039346656037UL, eh = th;
            long n = 0;
            while (m.State != MatchState.Over && n < maxTicks)
            {
                if (inputs != null) m.Step(inputs(m, n)); else m.Step(PlayerInput.None);
                th = SMix(th, m.Checksum()); eh = SHashEvents(eh, m.Events); n++;
            }
            return new SRunResult { TickHash = th, EventHash = eh, Winner = m.Winner, Time = m.Time, Ticks = n };
        }

        static void ScaleDeterminism(int nSeeds)
        {
            SHead("DET  determinism (same seed + same inputs = same match)");
            const int maxTicks = 60 * 900;
            foreach (int heroes in new[] { HeroCatalog.Count, 10 })
            {
                var cfgs = Enumerable.Range(1, nSeeds).Select(s => SBotConfig(s, heroes, (Difficulty)(s % 3))).ToArray();
                var a = new SRunResult[nSeeds]; var b = new SRunResult[nSeeds]; var c = new SRunResult[nSeeds];
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < nSeeds; i++) a[i] = SRun(SBotConfig(i + 1, heroes, (Difficulty)((i + 1) % 3)), maxTicks);
                for (int i = 0; i < nSeeds; i++) b[i] = SRun(SBotConfig(i + 1, heroes, (Difficulty)((i + 1) % 3)), maxTicks);
                Parallel.For(0, nSeeds, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i => c[i] = SRun(SBotConfig(i + 1, heroes, (Difficulty)((i + 1) % 3)), maxTicks));
                int diffSerial = 0, diffPar = 0; long ticks = 0; int timeouts = 0;
                for (int i = 0; i < nSeeds; i++)
                {
                    ticks += a[i].Ticks; if (a[i].Winner < 0) timeouts++;
                    if (a[i].TickHash != b[i].TickHash || a[i].EventHash != b[i].EventHash) { diffSerial++; SInfo($"seed {i + 1}: serial runs differ"); }
                    if (a[i].TickHash != c[i].TickHash || a[i].EventHash != c[i].EventHash) { diffPar++; SInfo($"seed {i + 1}: parallel run differs"); }
                }
                SInfo($"{heroes} heroes: {nSeeds} full bot matches x3 ({ticks} ticks each pass, {timeouts} timeouts), {sw.Elapsed.TotalSeconds:0.0} s");
                SCheck(diffSerial == 0, "DET-1", $"{heroes}-hero pool: the same seed twice gives the same Checksum every tick and the same event stream ({nSeeds - diffSerial}/{nSeeds})");
                SCheck(diffPar == 0, "DET-2", $"{heroes}-hero pool: {Environment.ProcessorCount} matches in parallel threads equal the serial runs (no shared static state) ({nSeeds - diffPar}/{nSeeds})");
            }

            // online: two humans with seeded random thumbs; the phone's own seat (LocalSeat) must not change the rules
            int onlineDiff = 0, seatDiff = 0;
            for (int s = 1; s <= Math.Max(4, nSeeds / 5); s++)
            {
                Func<int, MatchConfig> mk = local => new MatchConfig
                {
                    Seed = s, Difficulty = Difficulty.Hard, LocalSeat = local,
                    Seats = new[] { new SeatConfig { Hero = (HeroId)(s % 10), Team = 0, Spell = SpellId.Blink }, new SeatConfig { Hero = (HeroId)((s * 3 + 1) % 10), Team = 1, Spell = SpellId.Shield } },
                };
                Func<Match, long, PlayerInput[]> thumbs = (m, n) =>
                {
                    var r = new Random(s * 1000003 + (int)n);
                    var arr = new PlayerInput[2];
                    for (int k = 0; k < 2; k++)
                    {
                        var p = PlayerInput.None;
                        float a = (float)(r.NextDouble() * Math.PI * 2);
                        p.MoveX = (float)Math.Cos(a); p.MoveY = (float)Math.Sin(a) + (k == 0 ? -0.3f : 0.3f);
                        if (r.NextDouble() < 0.01) { p.Special = true; if (r.NextDouble() < 0.5) { p.AimAngle = a; p.AimK = (float)r.NextDouble(); } }
                        if (r.NextDouble() < 0.002) p.Spell = true;
                        arr[k] = p;
                    }
                    return arr;
                };
                var r0 = SRun(mk(0), 60 * 600, thumbs); var r1 = SRun(mk(0), 60 * 600, thumbs); var r2 = SRun(mk(1), 60 * 600, thumbs);
                if (r0.TickHash != r1.TickHash || r0.EventHash != r1.EventHash) onlineDiff++;
                if (r0.TickHash != r2.TickHash || r0.EventHash != r2.EventHash) seatDiff++;
            }
            SCheck(onlineDiff == 0, "DET-3", "online seats with the same thumbs twice: identical every tick");
            SCheck(seatDiff == 0, "DET-4", "online: the phone's own seat (LocalSeat 0 vs 1) does not change the match");

            // sanity: the Checksum notices a one-ulp nudge of one hero
            var m1 = Create(SBotConfig(3, 8, Difficulty.Hard)); var m2 = Create(SBotConfig(3, 8, Difficulty.Hard));
            for (int i = 0; i < 600; i++) { m1.Step(PlayerInput.None); m2.Step(PlayerInput.None); }
            ulong c1 = m1.Checksum(); m2.Heroes[2].X = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(m2.Heroes[2].X) + 1);
            SCheck(c1 != m2.Checksum(), "DET-5", "Checksum changes when one hero moves by one float ulp");
        }

        // ======================================================================================== 2. D1 random targeting
        static void ScaleD1()
        {
            SHead("D1   a hero's auto attack picks a random valid target (seeded)");
            // scene: Tyra (range 4) at (5, 12); five valid enemies, four invalid ones (behind a wall, unseen in grass, 0.02 out of
            // range, untargetable). Everyone but Tyra is stunned (no attacks, no walking); health refilled every swing.
            Func<int, (Match m, Unit tyra, Unit[] valid, Unit[] invalid)> scene = seed =>
            {
                var m = SMake(HeroId.Tyra, new[] { HeroId.Fenn, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta }, seed);
                var t = m.Player;
                m.SPark(t, m.SFoe(HeroId.Bram), m.SFoe(HeroId.Knut), m.SFoe(HeroId.Tikta));
                SPlace(t, 5.0f, 12.0f);
                var v1 = m.SFoe(HeroId.Bram); SPlace(v1, 6.6f, 12.0f);
                var v2 = m.SFoe(HeroId.Knut); SPlace(v2, 5.0f, 13.8f);
                var v3 = m.NewMinion(MinionKind.Melee, Teams.Enemy, 5.6f, 10.6f);
                var v4 = m.NewMinion(MinionKind.Ranged, Teams.Enemy, 7.2f, 13.2f);
                // the edge of reach: SDist <= Range + R counts (Docs/DECISIONS D1 "reachable")
                var v5 = m.NewMinion(MinionKind.Siege, Teams.Enemy, 0, 12.0f); v5.X = v5.PX = 5.0f + t.Range + v5.R - 0.01f;
                var bad1 = m.SFoe(HeroId.Tikta); SPlace(bad1, 2.55f, 9.6f); // behind the x=3 wall (revealed: only the wall stops it)
                var bad2 = m.NewMinion(MinionKind.Melee, Teams.Enemy, 2.2f, 12.6f); // in grass, 2.9 from Tyra: unseen
                var bad3 = m.NewMinion(MinionKind.Melee, Teams.Enemy, 0, 11.3f); bad3.X = bad3.PX = 5.0f + t.Range + bad3.R + 0.02f;
                var bad4 = m.NewMinion(MinionKind.Ranged, Teams.Enemy, 6.0f, 11.0f); bad4.AddRaw(St.Untargetable, 1e9f, 0, 0, null, false);
                var all = new[] { v1, v2, v3, v4, v5, bad1, bad2, bad3, bad4 };
                foreach (var u in all) { m.SFreeze(u); if (u.Kind == UnitKind.Minion) u.MaxHp = u.Hp = 1e6f; }
                return (m, t, new[] { v1, v2, v3, v4, v5 }, new[] { bad1, bad2, bad3, bad4 });
            };

            Func<int, int, List<int>> picks = (seed, n) =>
            {
                var (m, t, valid, invalid) = scene(seed);
                var all = valid.Concat(invalid).ToArray();
                var res = new List<int>(n);
                int guard = 0;
                while (res.Count < n && guard++ < n * 400)
                {
                    foreach (var u in all) { u.Hp = u.MaxHp; u.RevealT = 0; }
                    invalid[0].RevealT = 99; // only the wall stops this one
                    t.Hp = t.MaxHp;
                    m.Step(PlayerInput.None);
                    foreach (var e in m.Events)
                        if (e.Type == EventType.AttackStart && e.A == t) { int ix = Array.IndexOf(all, e.B); res.Add(ix); if (ix < 0 && res.Count(q => q < 0) <= 3) SInfo($"other target: {e.B.Kind} #{e.B.Serial} at ({e.B.X:0.00},{e.B.Y:0.00}) team {e.B.Team}"); }
                }
                return res;
            };

            const int N = 6000;
            var p = picks(11, N);
            var counts = new int[9];
            foreach (int i in p) if (i >= 0) counts[i]++;
            int unknown = p.Count(i => i < 0);
            SInfo($"{p.Count} swings; valid picks {string.Join(" ", counts.Take(5))}; invalid picks {string.Join(" ", counts.Skip(5))}; other {unknown}");
            double exp = p.Count / 5.0, chi = 0; for (int i = 0; i < 5; i++) chi += (counts[i] - exp) * (counts[i] - exp) / exp;
            SCheck(counts.Skip(5).Sum() == 0 && unknown == 0, "D1-1", "never a target behind a wall, unseen in grass, 0.02 tile out of reach or untargetable");
            SCheck(counts.Take(5).All(c => c > 0) && chi < 18.47, "D1-2", $"the five valid targets (heroes, minions, the reach edge Dist = Range + R) are picked evenly: chi2 = {chi:0.00} (4 dof, p = 0.001 bound 18.47)");
            var again = picks(11, 300); var other = picks(12, 300);
            SCheck(again.SequenceEqual(p.Take(300)), "D1-3", "the same seed gives the same sequence of picks");
            SCheck(!other.SequenceEqual(p.Take(300)), "D1-4", "another seed gives another sequence");

            // a target that dies before the shot lands: the shot goes to another random valid target in reach, never an invalid one
            int retarget = 0, retInvalid = 0, dropped = 0, droppedDmg = 0;
            for (int s = 0; s < 300; s++)
            {
                var (m, t, valid, invalid) = scene(100 + s);
                foreach (var u in valid.Concat(invalid)) u.Hp = u.MaxHp;
                invalid[0].RevealT = 99;
                Unit first = null;
                for (int k = 0; k < 400 && first == null; k++) { m.Step(PlayerInput.None); foreach (var e in m.Events) if (e.Type == EventType.AttackStart && e.A == t) first = e.B; }
                Proj shot = m.Projs.FirstOrDefault(q => q.Src == t && q.Mode == ProjMode.Auto && !q.Done);
                if (first == null || shot == null) continue;
                bool alone = s % 3 == 0;
                if (alone) foreach (var u in valid) if (u != first) { u.Alive = false; }
                first.Alive = false; first.Hp = 0; // dies before the release
                m.Step(PlayerInput.None);
                if (shot.Done) { dropped++; if (m.Events.Any(e => e.Type == EventType.Damage && e.A == t)) droppedDmg++; continue; }
                retarget++;
                if (Array.IndexOf(valid, shot.Tgt) < 0) retInvalid++;
            }
            SInfo($"target killed before release: {retarget} shots retargeted, {dropped} dropped (no valid target left)");
            SCheck(retarget > 0 && retInvalid == 0, "D1-5", "a shot whose target died goes to another valid target (never behind a wall / unseen / out of reach / untargetable)");
            SCheck(dropped > 0 && droppedDmg == 0, "D1-6", "with nobody valid left the shot is dropped and deals nothing");
        }

        // ======================================================================================== 3. LOS / walls
        // exact geometry: does a straight segment cross a wall's inside? (axis-aligned wall squares; the two middle boulders by
        // fine sampling of their capsule). Returns the length of the segment inside walls.
        float SChordInWalls(float ax, float ay, float bx, float by)
        {
            float total = 0;
            int x0 = (int)Math.Floor(Math.Min(ax, bx)) - 1, x1 = (int)Math.Floor(Math.Max(ax, bx)) + 1;
            int y0 = (int)Math.Floor(Math.Min(ay, by)) - 1, y1 = (int)Math.Floor(Math.Max(ay, by)) + 1;
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                {
                    if (Map.Tile(tx, ty) != Arena.Wall) continue;
                    if (SIsRockTile(tx, ty)) continue;
                    total += SClip(ax, ay, bx, by, tx, ty, tx + 1, ty + 1);
                }
            // boulders: sample every 0.002 tile
            float d = SDist(ax, ay, bx, by); int n = Math.Max(1, (int)(d / 0.002f));
            int inside = 0;
            for (int i = 0; i <= n; i++)
            {
                float k = (float)i / n, x = ax + (bx - ax) * k, y = ay + (by - ay) * k;
                int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
                if (SIsRockTile(tx, ty) && Map.WallAt(x, y)) inside++;
            }
            return total + inside * (d / Math.Max(1, n));
        }
        bool SIsRockTile(int tx, int ty)
        {
            if (Map.Tile(tx, ty) != Arena.Wall) return false;
            // the rock tiles: walls within 1.5 rows of the middle and 4.5 columns of the centre (Arena's constructor)
            return Math.Abs(ty + 0.5f - Map.H / 2f) <= 1.5f && Math.Abs(tx + 0.5f - Map.W / 2f) < 4.5f && Map.H == Arena.ArenaH;
        }
        // Liang-Barsky: length of segment a-b inside the open box
        static float SClip(float ax, float ay, float bx, float by, float minx, float miny, float maxx, float maxy)
        {
            float dx = bx - ax, dy = by - ay, t0 = 0, t1 = 1;
            float[] p = { -dx, dx, -dy, dy }, q = { ax - minx, maxx - ax, ay - miny, maxy - ay };
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-9f) { if (q[i] <= 0) return 0; continue; }
                float r = q[i] / p[i];
                if (p[i] < 0) { if (r > t1) return 0; if (r > t0) t0 = r; }
                else { if (r < t0) return 0; if (r < t1) t1 = r; }
            }
            return t1 > t0 ? (t1 - t0) * (float)Math.Sqrt(dx * dx + dy * dy) : 0;
        }

        static void ScaleLos()
        {
            SHead("LOS  sight lines and shots stop at walls");
            var m = SMake(HeroId.Tyra, new[] { HeroId.Bram, HeroId.Moa }, new[] { HeroId.Fenn, HeroId.Knut, HeroId.Tikta });

            // 3a. the sampled sight line (0.1 tile steps, centre and +-0.1 edges) against exact geometry
            var r = new Random(5);
            int n = 0, leaks = 0, centreLeaks = 0, overBlock = 0; float worst = 0; string worstAt = "";
            while (n < 40000)
            {
                float ax = (float)(r.NextDouble() * 15), ay = (float)(r.NextDouble() * 35), bx, by;
                double a = r.NextDouble() * Math.PI * 2, len = 0.5 + r.NextDouble() * 6;
                bx = ax + (float)(Math.Cos(a) * len); by = ay + (float)(Math.Sin(a) * len);
                if (!m.Map.Free(ax, ay, 0.215f) || !m.Map.Free(bx, by, 0.215f)) continue; // both ends where a body can stand
                n++;
                bool sampled = m.Map.LosClear(ax, ay, bx, by);
                float chord = m.SChordInWalls(ax, ay, bx, by);
                if (sampled && chord > 1e-4f) { centreLeaks++; if (chord > worst) { worst = chord; worstAt = $"({ax:0.000},{ay:0.000})->({bx:0.000},{by:0.000})"; } }
                if (!sampled && chord <= 1e-4f) overBlock++; // the +-0.1 edges graze a wall the centre misses (by design, LosHalf)
                if (sampled && chord > 0.05f) leaks++;
            }
            SInfo($"{n} random sight lines between standable points: centre line crosses a wall but LOS says clear in {centreLeaks} ({100.0 * centreLeaks / n:0.000} %), worst chord {worst:0.0000} tile at {worstAt}");
            SInfo($"the 0.2-wide line is blocked although its centre line is clear in {overBlock} ({100.0 * overBlock / n:0.00} %) - LosHalf 0.1 by design");
            SCheck(leaks == 0, "LOS-1", $"no sight line passes more than 0.05 tile of wall (worst {worst:0.0000} tile; corner clipping below the 0.1 sampling step is the tolerance)");

            // 3b. a target that ducks behind a wall after the swing starts: the auto attack must stop at the wall (every hero)
            var heroes = Enumerable.Range(0, 10).Select(i => (HeroId)i).ToArray();
            foreach (var hid in heroes)
            {
                foreach (bool beforeRelease in new[] { true, false })
                {
                    var foe = hid == HeroId.Bram ? HeroId.Knut : HeroId.Bram;
                    var mm = SMake(hid, new[] { hid == HeroId.Moa ? HeroId.Tikta : HeroId.Moa, hid == HeroId.Tyra ? HeroId.Tikta : HeroId.Tyra }, new[] { foe, HeroId.Fenn == hid || foe == HeroId.Fenn ? HeroId.Blast : HeroId.Fenn, HeroId.Oaken == hid ? HeroId.Hilde : HeroId.Oaken });
                    var s = mm.Player; var t = mm.SFoe(foe);
                    mm.SPark(s, t);
                    SPlace(s, 4.6f, 7.0f); SPlace(t, 4.6f + Math.Min(1.8f, s.Range * 0.7f), 7.0f); mm.SFreeze(t);
                    s.AtkTimer = 99;
                    bool started = false; int k = 0;
                    for (; k < 120 && !started; k++) { mm.SStep(); started = mm.Events.Any(e => e.Type == EventType.AttackStart && e.A == s); }
                    var shot = mm.Projs.FirstOrDefault(q => q.Src == s && !q.Done && (q.Mode == ProjMode.Auto || q.Mode == ProjMode.Smash));
                    if (!beforeRelease) for (int j = 0; j < 200 && shot != null && shot.T < 0 && !shot.Done; j++) mm.SStep();
                    float hp0 = t.Hp; bool alreadyHit = shot == null || shot.Done;
                    SPlace(t, 2.6f, 7.0f); t.Hp = hp0;
                    bool blocked = false, hit = false;
                    for (int j = 0; j < 240; j++)
                    {
                        mm.SStep();
                        foreach (var e in mm.Events)
                        {
                            if (e.Type == EventType.Blocked && e.A == s) blocked = true;
                            if (e.Type == EventType.Hit && e.A == s && e.B == t) hit = true;
                        }
                        s.AtkTimer = 0; // one swing only
                    }
                    string when = beforeRelease ? "in the hand" : "in flight";
                    if (!started || alreadyHit) continue; // Bonk's slam has no flight
                    SCheck(t.Hp == hp0 && !hit, "LOS-2", $"{hid} auto, target ducks behind the wall {when}: damage {hp0 - t.Hp:0.0}, Hit event {hit}, Blocked event {blocked}");
                }
            }

            // 3c. skillshots and area specials at a target right behind a wall (sweep over positions near the x=3 wall and
            // its ends): count hits where the exact centre line from the caster crosses a wall
            var cases = new (HeroId hero, string name, bool drag)[]
            {
                (HeroId.Tyra, "Tyra bolt (drag)", true), (HeroId.Tikta, "Tikta needles (drag)", true), (HeroId.Bonk, "Bonk boulder (drag)", true),
                (HeroId.Knut, "Knut hook (drag)", true), (HeroId.Oaken, "Oaken quake (drag)", true),
                (HeroId.Knut, "Knut anchor slam (tap)", false), (HeroId.Bonk, "Bonk ground slam (tap)", false), (HeroId.Oaken, "Oaken slam (tap)", false),
            };
            var rr = new Random(9);
            foreach (var c in cases)
            {
                int tried = 0, hitsThroughWall = 0, clearShots = 0, clearHits = 0; string ex = ""; var miss = new List<string>();
                for (int it = 0; it < 400; it++)
                {
                    bool rocks = it % 2 == 1;
                    float sx, sy, tx, ty;
                    if (!rocks) { sx = 4.3f + (float)rr.NextDouble() * 2.5f; sy = 3.5f + (float)rr.NextDouble() * 9f; tx = 2.5f + (float)rr.NextDouble() * 0.15f; ty = sy + (float)(rr.NextDouble() * 4 - 2); }
                    else { sx = 7.5f + (float)(rr.NextDouble() * 1.5 - 0.75); sy = 17.5f + (float)(rr.NextDouble() * 2 - 1); tx = 9.3f + (float)rr.NextDouble() * 0.4f; ty = 17.5f + (float)(rr.NextDouble() * 3 - 1.5); }
                    if (!c.drag) { tx = sx - 1.0f - (float)rr.NextDouble() * 0.6f; if (!rocks) { sx = 4.35f + (float)rr.NextDouble() * 0.5f; tx = 2.55f + (float)rr.NextDouble() * 0.2f; } else { sx = 8.9f; sy = 17.5f + (float)(rr.NextDouble() * 2 - 1); tx = 10.0f + (float)rr.NextDouble() * 0.3f; } }
                    var mm = SMake(c.hero, new[] { c.hero == HeroId.Moa ? HeroId.Tyra : HeroId.Moa, c.hero == HeroId.Tyra ? HeroId.Fenn : HeroId.Tyra }, new[] { HeroId.Bram == c.hero ? HeroId.Blast : HeroId.Bram, HeroId.Hilde, HeroId.Blast == c.hero ? HeroId.Fenn : HeroId.Blast });
                    var s = mm.Player; var t = mm.SFoe(HeroId.Bram == c.hero ? HeroId.Blast : HeroId.Bram);
                    mm.SPark(s, t);
                    if (!mm.Map.Free(sx, sy, s.R) || !mm.Map.Free(tx, ty, t.R)) continue;
                    SPlace(s, sx, sy); SPlace(t, tx, ty); mm.SFreeze(t); s.AddRaw(St.Disarm, 1e9f, 0, 0, null, false);
                    Unit helper = null;
                    if (!c.drag)
                    {   // the tap slams need some enemy in reach with a clear line to cast at all: a minion on the open side
                        helper = mm.NewMinion(MinionKind.Siege, Teams.Enemy, sx + 0.9f, sy); mm.SFreeze(helper);
                        if (!mm.Map.Free(helper.X, helper.Y, helper.R)) continue;
                    }
                    if (!mm.SCast(s, c.drag ? SAngTo(s, tx, ty) : float.NaN, 1)) continue;
                    tried++;
                    float hp0 = t.Hp;
                    for (int j = 0; j < 150; j++) { mm.SStep(); t.X = t.PX = tx; t.Y = t.PY = ty; } // the target holds still (a hook would pull it)
                    bool got = t.Hp < hp0;
                    // what the rules intend: the caster's (or the slam centre's) straight line to the target crosses a wall
                    float chord = mm.SChordInWalls(sx, sy, tx, ty);
                    float reachCut = c.name.StartsWith("Oaken quake") || c.name.StartsWith("Bonk boulder") ? 0 : 0; _ = reachCut;
                    if (chord > 0.05f) { if (got) { hitsThroughWall++; if (ex == "") ex = $"caster ({sx:0.00},{sy:0.00}) target ({tx:0.00},{ty:0.00}) chord {chord:0.00}"; } }
                    else { clearShots++; if (got) clearHits++; else if (miss.Count < 4) miss.Add($"d {SDist(sx, sy, tx, ty):0.00} wide-LOS {(mm.Map.LosClear(sx, sy, tx, ty) ? "clear" : "blocked")} caster ({sx:0.00},{sy:0.00}) target ({tx:0.00},{ty:0.00})"); }
                }
                SCheck(hitsThroughWall == 0, "LOS-3", $"{c.name}: {tried} casts, target hurt through a wall {hitsThroughWall} times{(ex != "" ? " e.g. " + ex : "")} (clear line: {clearHits}/{clearShots} hit)");
            }
        }

        // ======================================================================================== 4. same tick, same kind (view HitKey)
        // A faithful copy of MatchViews.SyncProjs' hit bookkeeping (Assets/BAM/Game/MatchViews.cs:101-104, 190, 271-311, 476 at
        // d4afddec): a shot the rules drop in a tick is drawn as a hit (MarkHit: impact instead of a fizzle for the kinds that can
        // miss, WorldViews.CanMiss) when ANY Hit event of the same attacker and kind came that tick.
        sealed class SViewReplica
        {
            sealed class Rec { public Proj P; public int Serial, Src; public ProjKind ViewKind; public ProjMode Mode; }
            readonly Dictionary<int, Rec> live = new Dictionary<int, Rec>();
            public static int HitKey(int src, ProjKind k) => src * 64 + (int)(k == ProjKind.TyraVolley ? ProjKind.TyraArrow : k);
            public static bool CanMiss(ProjKind k) => k == ProjKind.TyraBolt || k == ProjKind.TyraVolley || k == ProjKind.TiktaNeedleSpecial || k == ProjKind.BramHammerSpecial;
            public int Seed;
            public long Dropped, FalseHit, FalseHitVisible, MissedHit, Ambiguous, FalseTicks, Volleys;
            public readonly Dictionary<string, long> FalseByKind = new Dictionary<string, long>(), DroppedByKind = new Dictionary<string, long>();
            public readonly List<string> Examples = new List<string>(), MissedEx = new List<string>();

            public void AfterStep(Match m)
            {
                var keys = new HashSet<int>();
                var byShot = new HashSet<int>();
                foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A != null) { keys.Add(HitKey(e.A.Serial, (ProjKind)e.Kind)); int sh = PShotOf(e); if (sh != 0) byShot.Add(sh); }
                var seen = new HashSet<int>();
                foreach (var p in m.Projs)
                {
                    if (p.Done || !p.IsShot) continue;
                    seen.Add(p.Serial);
                    if (!live.ContainsKey(p.Serial))
                        live[p.Serial] = new Rec { P = p, Serial = p.Serial, Src = p.Src != null ? p.Src.Serial : 0, Mode = p.Mode, ViewKind = p.Mode == ProjMode.Volley && p.Kind == ProjKind.TyraArrow ? ProjKind.TyraVolley : p.Kind };
                }
                var gone = live.Values.Where(r => !seen.Contains(r.Serial)).ToList();
                long falseBefore = FalseHitVisible;
                var perKey = gone.GroupBy(r => HitKey(r.Src, r.ViewKind)).ToDictionary(g => g.Key, g => g.Count());
                foreach (var r in gone)
                {
                    live.Remove(r.Serial);
                    Dropped++;
                    string kn = r.ViewKind.ToString();
                    DroppedByKind[kn] = DroppedByKind.TryGetValue(kn, out long dk) ? dk + 1 : 1;
                    bool view = PViewKeysByShot ? byShot.Contains(r.Serial) : keys.Contains(HitKey(r.Src, r.ViewKind));
                    bool truth = r.P.Serial == r.Serial && Truth(m, r);
                    if (view && !truth)
                    {
                        FalseHit++; if (CanMiss(r.ViewKind)) FalseHitVisible++;
                        FalseByKind[kn] = FalseByKind.TryGetValue(kn, out long fk) ? fk + 1 : 1;
                        string why = m.Events.Any(e => e.Type == EventType.Blocked && e.ASerial == r.Src && Near(e.X, r.P.PX, r.P.X) && Near(e.Y, r.P.PY, r.P.Y)) ? "it hit a WALL" : "it MISSED (reach end / target gone)";
                        string s = $"seed {Seed} t={m.Time:0.000}s shot #{r.Serial} {kn} ({r.Mode}) of unit #{r.Src}: {why}, but another {kn} of the same attacker hit in the same tick -> drawn as a hit";
                        if (Examples.Count < 12) Examples.Add(s);
                    }
                    if (!view && truth) { MissedHit++; if (MissedEx.Count < 3) MissedEx.Add($"seed {Seed} t={m.Time:0.000}s shot #{r.Serial} {kn} ({r.Mode}) of unit #{r.Src} at ({r.P.X:0.00},{r.P.Y:0.00}): hits this tick " + string.Join("; ", m.Events.Where(e => e.Type == EventType.Hit && e.ASerial == r.Src).Select(e => $"B#{e.BSerial} kind {(ProjKind)e.Kind} shot {PShotOf(e)} at ({e.X:0.00},{e.Y:0.00})"))); }
                    if (perKey[HitKey(r.Src, r.ViewKind)] > 1) Ambiguous++;
                }
                if (FalseHitVisible > falseBefore) FalseTicks++;
                foreach (var e in m.Events) if (e.Type == EventType.Cast && e.A != null && e.A.Hero == HeroId.Tyra && !e.Big) Volleys++;
            }
            static bool Near(float a, float b, float c) => Math.Abs(a - b) < 0.02f || Math.Abs(a - c) < 0.02f;

            // did THIS shot land a hit in the tick it was dropped? (from the event stream and the shot's own last state)
            static bool Truth(Match m, Rec r)
            {
                var p = r.P;
                foreach (var e in m.Events)
                    if (e.Type == EventType.Blocked && e.ASerial == r.Src && e.Kind == (byte)p.Kind && Near(e.X, p.PX, p.X) && Near(e.Y, p.PY, p.Y)) return false;
                foreach (var e in m.Events)
                {
                    if (e.Type != EventType.Hit || e.ASerial != r.Src || e.Kind != (byte)p.Kind || e.B == null) continue;
                    if (PViewKeysByShot && PShotOf(e) != r.Serial) continue; // after the SHOT-01 fix every hit a flying shot makes carries its stamp: another stamp or none is not this shot's
                    switch (p.Mode)
                    {
                        case ProjMode.Auto: case ProjMode.Needle: case ProjMode.Hammer:
                            if (p.Tgt != null && e.BSerial == p.Tgt.Serial) return true; break;
                        default:
                            if (SDist(e.X, e.Y, p.X, p.Y) <= p.Radius + e.B.R + 0.01f) return true; break;
                    }
                }
                return false;
            }
        }

        static void ScaleHitKey(int nMatches)
        {
            SHead("HIT  same attacker, same shot kind, same tick (the view's HitKey = srcSerial*64 + kind)");

            // 4a. Tyra's Arrow Volley: five arrows fly the same reach and are all dropped in the same tick; put an enemy so that only
            // the middle arrow touches it in that last tick, sweep its distance
            int casesWithFalse = 0, sweep = 0; long falseArrows = 0; string ex = "";
            for (float d = 3.4f; d <= 4.6f; d += 0.01f)
            {
                var m = SMake(HeroId.Tyra, new[] { HeroId.Bram, HeroId.Moa }, new[] { HeroId.Tikta, HeroId.Knut, HeroId.Fenn });
                var t = m.Player; var f = m.SFoe(HeroId.Tikta);
                m.SPark(t, f);
                SPlace(t, 7.5f, 22f); SPlace(f, 7.5f, 22f - d); m.SFreeze(f); t.AddRaw(St.Disarm, 1e9f, 0, 0, null, false);
                if (!m.SCast(t, float.NaN)) continue; // the tap: Arrow Volley at the only enemy in her range
                sweep++;
                var v = new SViewReplica();
                long before = 0;
                for (int j = 0; j < 90; j++) { m.SStep(); f.X = f.PX = 7.5f; f.Y = f.PY = 22f - d; v.AfterStep(m); }
                if (v.FalseHit > before) { casesWithFalse++; falseArrows += v.FalseHit; if (ex == "") ex = $"enemy {d:0.00} tiles ahead: " + v.Examples[0]; }
            }

            // 4b. Tikta's dragged needles: the first one hits a wall in the same tick a later one hits an enemy that stepped into the line
            int tCases = 0, tFalse = 0; string tex = "";
            for (float e = 0.6f; e <= 2.6f; e += 0.02f)
            {
                var m = SMake(HeroId.Tikta, new[] { HeroId.Bram, HeroId.Moa }, new[] { HeroId.Tyra, HeroId.Knut, HeroId.Fenn });
                var k = m.Player; m.SPark(k); k.AddRaw(St.Disarm, 1e9f, 0, 0, null, false);
                SPlace(k, 8.5f, 7.0f);
                var mn = m.NewMinion(MinionKind.Siege, Teams.Enemy, 8.5f - e, 8.2f); m.SFreeze(mn);
                if (!m.SCast(k, SimMath.PI, 1)) continue; // west, at the x=3 wall 4.5 tiles away (reach 5)
                tCases++;
                var v = new SViewReplica(); bool moved = false;
                for (int j = 0; j < 90; j++)
                {
                    m.SStep(); v.AfterStep(m);
                    var n0 = m.Projs.FirstOrDefault(q => q.Mode == ProjMode.NeedleLine && q.T >= 0 && !q.Done);
                    if (!moved && n0 != null && n0.X < mn.X - 0.6f) { mn.Y = mn.PY = 7.0f; moved = true; } // steps in after the first needle passed
                }
                if (v.FalseHit > 0) { tFalse++; if (tex == "") tex = $"enemy {e:0.00} tiles out: " + v.Examples[0]; }
            }
            SCheck(tFalse == 0, "HIT-2_TIKTA-02", $"[{(PViewKeysByShot ? "view per shot" : "view HitKey")}] " + $"Tikta dragged needles (scripted: needle 1 hits the wall the tick needle 2 hits an enemy that stepped in): {tFalse} of {tCases} sweep positions draw the wall needle as a hit (view rule copy, NOT TESTED in Unity){(tex != "" ? "; " + tex : "")}");

            // 4c. how often in real bot matches (all heroes, Hard bots both sides)
            var total = new SViewReplica();
            var lk = new object();
            var reps = new SViewReplica[nMatches];
            Parallel.For(0, nMatches, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var cfg = SBotConfig(1000 + i, 10, Difficulty.Hard);
                var m = Create(cfg); var v = new SViewReplica { Seed = 1000 + i };
                for (int n = 0; n < 60 * 900 && m.State != MatchState.Over; n++) { m.Step(PlayerInput.None); v.AfterStep(m); }
                reps[i] = v;
            });
            foreach (var v in reps)
            {
                total.FalseTicks += v.FalseTicks; total.Volleys += v.Volleys;
                total.Dropped += v.Dropped; total.FalseHit += v.FalseHit; total.FalseHitVisible += v.FalseHitVisible; total.MissedHit += v.MissedHit; total.Ambiguous += v.Ambiguous;
                foreach (var kv in v.FalseByKind) total.FalseByKind[kv.Key] = (total.FalseByKind.TryGetValue(kv.Key, out long x) ? x : 0) + kv.Value;
                foreach (var kv in v.DroppedByKind) total.DroppedByKind[kv.Key] = (total.DroppedByKind.TryGetValue(kv.Key, out long x) ? x : 0) + kv.Value;
                foreach (var s in v.Examples) if (total.Examples.Count < 10) total.Examples.Add(s);
                foreach (var s in v.MissedEx) if (total.MissedEx.Count < 3) total.MissedEx.Add(s);
            }
            SInfo($"{nMatches} Hard bot matches (10-hero pool): {total.Dropped} shots ended; drawn as hit without hitting: " + string.Join(", ", total.FalseByKind.OrderByDescending(k => k.Value).Select(kv => $"{kv.Key} {kv.Value}/{total.DroppedByKind[kv.Key]}")));
            foreach (var s in total.Examples.Take(2)) SInfo("e.g. " + s);
            long vol = total.FalseByKind.TryGetValue("TyraVolley", out long fv) ? fv : 0, tik = total.FalseByKind.TryGetValue("TiktaNeedleSpecial", out long ft) ? ft : 0;
            SCheck(vol == 0, "HIT-3_SHOT-01", $"[{(PViewKeysByShot ? "view per shot" : "view HitKey")}] {nMatches} Hard bot matches: {vol} of {(total.DroppedByKind.TryGetValue("TyraVolley", out long dv) ? dv : 0)} Tyra volley arrows drawn as hits without hitting ({total.FalseTicks} ticks, {total.Volleys} volleys; view rule copy, NOT TESTED in Unity)");
            SCheck(tik == 0, "HIT-4_TIKTA-02", $"{nMatches} Hard bot matches: {tik} of {(total.DroppedByKind.TryGetValue("TiktaNeedleSpecial", out long dt) ? dt : 0)} Tikta special needles drawn as hits without hitting");
            SCheck(total.FalseHitVisible == 0, "HIT-5", $"bot matches: no visible false hit-confirm of any kind ({total.FalseHitVisible}; PLAN gate = 0)");
            foreach (var x in total.MissedEx) SInfo("real hit drawn as a miss: " + x);
            SCheck(total.MissedHit == 0, "HIT-6", $"bot matches: no real hit drawn as a miss ({total.MissedHit})");
        }

        // ======================================================================================== 5. block, miss, shield = no damage
        static void ScaleZeroDamage()
        {
            SHead("ZERO a blocked, missed or shielded hit deals no damage (and what else it still does)");

            // 5a. misses: skillshots aimed 40 degrees off a target in the open
            var miss = new (HeroId h, string n)[] { (HeroId.Tyra, "Tyra bolt"), (HeroId.Tikta, "Tikta needles"), (HeroId.Bonk, "Bonk boulder"), (HeroId.Knut, "Knut hook"), (HeroId.Oaken, "Oaken quake") };
            foreach (var c in miss)
            {
                var m = SMake(c.h, new[] { c.h == HeroId.Moa ? HeroId.Tyra : HeroId.Moa, c.h == HeroId.Tyra ? HeroId.Fenn : HeroId.Tyra }, new[] { HeroId.Bram, HeroId.Hilde, HeroId.Blast });
                var s = m.Player; var t = m.SFoe(HeroId.Bram); m.SPark(s, t);
                SPlace(s, 7.5f, 22f); SPlace(t, 7.5f, 19.5f); m.SFreeze(t); s.AddRaw(St.Disarm, 1e9f, 0, 0, null, false);
                float ang = -SimMath.PI / 2 + 0.7f; float hp0 = t.Hp, ch0;
                m.SCast(s, ang, 1); ch0 = s.Charge;
                bool hitEv = false, dmgEv = false;
                for (int j = 0; j < 150; j++) { m.SStep(); foreach (var e in m.Events) { if (e.Type == EventType.Hit && e.B == t) hitEv = true; if (e.Type == EventType.Damage && e.B == t) dmgEv = true; } }
                SCheck(t.Hp == hp0 && !hitEv && !dmgEv, "ZERO-1", $"{c.n} aimed 40 deg off: damage {hp0 - t.Hp:0.0}, Hit event {hitEv}, Damage event {dmgEv}");
            }

            // 5b. blocked: an auto into a wall (covered per hero in LOS-2); here the side effects of a blocked auto
            {
                var m = SMake(HeroId.Knut, new[] { HeroId.Moa, HeroId.Tyra }, new[] { HeroId.Bram, HeroId.Hilde, HeroId.Blast });
                var s = m.Player; var t = m.SFoe(HeroId.Bram); m.SPark(s, t);
                SPlace(s, 4.6f, 7f); SPlace(t, 5.9f, 7f); m.SFreeze(t); s.AtkTimer = 99;
                for (int j = 0; j < 60 && !m.Events.Any(e => e.Type == EventType.AttackStart && e.A == s); j++) m.SStep();
                SPlace(t, 2.6f, 7f); float ch0 = s.Charge, hp0 = t.Hp;
                for (int j = 0; j < 30; j++) { m.SStep(); s.AtkTimer = 0; }
                SCheck(t.Hp == hp0 && s.Charge == ch0 && !t.Has(St.SlowFlat), "ZERO-2", $"blocked Knut auto: damage {hp0 - t.Hp:0.0}, charge +{s.Charge - ch0}, on-hit slow {t.Has(St.SlowFlat)}");
            }

            string lastSide = "";
            // 5c. shields and invulnerability: a hit fully soaked; and a partly soaked one must be exact
            foreach (var mode in new[] { "Moa shield", "invulnerable" })
                foreach (var hid in new[] { HeroId.Knut, HeroId.Tikta, HeroId.Bram, HeroId.Tyra, HeroId.Blast, HeroId.Bonk, HeroId.Oaken })
                {
                    var m = SMake(hid, new[] { hid == HeroId.Moa ? HeroId.Tyra : HeroId.Moa, hid == HeroId.Fenn ? HeroId.Tyra : HeroId.Fenn }, new[] { HeroId.Bram == hid ? HeroId.Knut : HeroId.Bram, HeroId.Hilde, HeroId.Tyra == hid ? HeroId.Fenn : HeroId.Tyra });
                    var s = m.Player; var t = m.SFoe(HeroId.Bram == hid ? HeroId.Knut : HeroId.Bram); var moa = m.SAlly(HeroId.Moa) ?? m.Player;
                    m.SPark(s, t);
                    SPlace(s, 7.5f, 21f); SPlace(t, 7.5f, 21f - Math.Min(1.5f, s.Range * 0.6f)); m.SFreeze(t);
                    // the shield's giver is an enemy-team Moa in the real game; any By makes it a shield. A Moa-made shield also
                    // makes the target CC immune (that is intended: slows/stuns are blocked)
                    if (mode == "Moa shield") t.AddRaw(St.Shield, 60, 0, 1e6f, null, false); else t.AddRaw(St.Invulnerable, 60, 0, 0, null, false);
                    s.PassHits = hid == HeroId.Bram ? 3 : 0; // Bram: the next hit is the 4th (Backlash)
                    if (hid == HeroId.Tyra) s.FeatherReadyAt = 0;
                    float hp0 = t.Hp, ch0 = s.Charge; int hits = 0, zeroHits = 0, dmg = 0, echo = 0;
                    s.AtkTimer = 99;
                    for (int j = 0; j < 240; j++)
                    {
                        m.SStep(); t.Hp = Math.Max(t.Hp, 1);
                        foreach (var e in m.Events)
                        {
                            if (e.Type == EventType.Hit && e.A == s && e.B == t) { hits++; if (e.V == 0) zeroHits++; }
                            if (e.Type == EventType.Damage && e.B == t) dmg++;
                        }
                        if (m.Projs.Any(q => q.Mode == ProjMode.Echo && !q.Done)) echo++;
                        if (hits >= 1) s.AtkTimer = 0;
                    }
                    string side = $"charge +{s.Charge - ch0}, slow on target {t.Has(St.SlowFlat)}, Tikta mark {t.FindBy(St.TiktaMark, s) >= 0}, Bram echo launched {echo > 0}, Tyra Featherstep {s.Has(St.SpeedPct)}";
                    SCheck(t.Hp == hp0 && dmg == 0 && hits > 0 && hits == zeroHits, "ZERO-3", $"{hid} auto into {mode}: damage {hp0 - t.Hp:0.0}, Damage events {dmg}, Hit events {hits} (all V=0: {hits == zeroHits})");
                    if (side != lastSide) { SInfo($"0-damage hit side effects ({hid}, {mode}): {side}"); lastSide = side; }
                }

            // 5d. a partly soaked hit is exact: 30 shield, a 50-ish hit
            {
                var m = SMake(HeroId.Knut, new[] { HeroId.Moa, HeroId.Tyra }, new[] { HeroId.Bram, HeroId.Hilde, HeroId.Blast });
                var s = m.Player; var t = m.SFoe(HeroId.Bram); m.SPark(s, t);
                SPlace(s, 7.5f, 21f); SPlace(t, 7.5f, 19.8f); m.SFreeze(t); t.AddRaw(St.Shield, 60, 0, 10, null, false);
                float hp0 = t.Hp; s.AtkTimer = 99; float dealt = -1, raw = 0;
                for (int j = 0; j < 60 && dealt < 0; j++)
                {
                    var p = m.Projs.FirstOrDefault(q => q.Src == s && q.Mode == ProjMode.Auto && !q.Done); if (p != null) raw = p.Dmg;
                    m.SStep(); foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == s) dealt = e.V;
                }
                float want = Math.Max(0, raw * t.TakenMult - 10);
                SCheck(Math.Abs((hp0 - t.Hp) - want) < 0.01f && Math.Abs(dealt - want) < 0.01f && t.ShieldAmount == 0, "ZERO-4", $"a {raw * t.TakenMult:0.00} hit into a 10 shield takes {hp0 - t.Hp:0.00} health (want {want:0.00}), Hit V {dealt:0.00}, shield left {t.ShieldAmount}");
            }

            // 5e. Fenn's decoy swings: 0 damage, but a Hit event (the target flinches; MatchViews plays the hit FX)
            {
                var m = SMake(HeroId.Fenn, new[] { HeroId.Moa, HeroId.Tyra }, new[] { HeroId.Bram, HeroId.Hilde, HeroId.Blast });
                var s = m.Player; var t = m.SFoe(HeroId.Bram); m.SPark(s, t);
                SPlace(s, 7.5f, 22f); SPlace(t, 7.5f, 20.4f); m.SFreeze(s); // Fenn himself does not swing
                s.ClonePending = true; m.FlushClones();
                var clone = m.Units.First(u => u.IsClone);
                float hp0 = t.Hp; int hits = 0; float sumV = 0;
                for (int j = 0; j < 150; j++) { m.SStep(); foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == clone) { hits++; sumV += e.V; } }
                SCheck(t.Hp == hp0 && sumV == 0, "ZERO-5", $"Fenn's decoy: {hits} swings landed as Hit events with V {sumV}, target damage {hp0 - t.Hp:0.0}");
            }
        }

        // ======================================================================================== 6. interrupt before release
        static void ScaleInterrupt()
        {
            SHead("INT  a stun / root / death before the release");

            // 6a. auto attacks: stunned during the wind-up (the shot still in the hand). The old game (SimInterrupt.cs) cut only
            // specials, never an auto in the hand, and BAM does the same; MatchViews.cs:298 says "cancelled in the hand (stun, death)".
            // So this is reported, not judged: the intended rule needs a decision.
            int autoStunLands = 0, autoStunTested = 0; var stunLanded = new List<string>();
            foreach (var hid in Enumerable.Range(0, 10).Select(i => (HeroId)i))
                foreach (var how in new[] { "stun", "death" })
                {
                    var foe = hid == HeroId.Bram ? HeroId.Knut : HeroId.Bram;
                    var m = SMake(hid, new[] { hid == HeroId.Moa ? HeroId.Tyra : HeroId.Moa, hid == HeroId.Fenn ? HeroId.Tyra : HeroId.Fenn }, new[] { foe, HeroId.Hilde == hid ? HeroId.Blast : HeroId.Hilde, HeroId.Oaken == hid ? HeroId.Blast : HeroId.Oaken });
                    var s = m.Player; var t = m.SFoe(foe); m.SPark(s, t);
                    SPlace(s, 7.5f, 21f); SPlace(t, 7.5f, 21f - Math.Min(1.6f, s.Range * 0.6f)); m.SFreeze(t);
                    s.AtkTimer = 99;
                    float windup = 0; bool started = false;
                    for (int j = 0; j < 60 && !started; j++) { m.SStep(); foreach (var e in m.Events) if (e.Type == EventType.AttackStart && e.A == s) { started = true; windup = e.V; } }
                    var shot = m.Projs.FirstOrDefault(q => q.Src == s && !q.Done && (q.Mode == ProjMode.Auto || q.Mode == ProjMode.Smash));
                    if (!started || shot == null || shot.T >= -Rules.Tick) { SInfo($"  {hid,-6} {how}: wind-up {windup:0.000} s, no tick left before the release to test"); continue; }
                    float hp0 = t.Hp, inHand = -shot.T;
                    if (how == "stun") m.AddStatus(s, St.Stun, 1.0f); else { s.Alive = false; s.Hp = 0; s.RespawnT = 99; }
                    bool hit = false;
                    for (int j = 0; j < 50; j++) { m.SStep(); s.AtkTimer = 0; foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == s) hit = true; }
                    string line = $"{hid} auto ({windup:0.000} s wind-up), {how} {inHand:0.000} s before the release: damage {hp0 - t.Hp:0.0}, Hit {hit}";
                    if (how == "stun") { autoStunLands += hit ? 1 : 0; autoStunTested++; if (hit) stunLanded.Add($"{hid} {inHand:0.00}s"); }
                    else SCheck(t.Hp == hp0 && !hit, "INT-2", line + " (a shot in the hand dies with its shooter)");
                }

            SInfo($"INT-1 BONK-01/CHAINMAP-03 (HELD for Rickard, no verdict): a stun in the auto's wind-up did NOT stop it for {autoStunLands} of {autoStunTested} heroes (time left in the hand: {string.Join(", ", stunLanded)}); old game the same");
            // 6b. specials with a wind-up: stun / root during it; one tick before release; a Moa-shielded caster is not cut
            var sp = new (HeroId h, string n, bool drag)[]
            {
                (HeroId.Tyra, "Tyra bolt", true), (HeroId.Tyra, "Tyra volley", false), (HeroId.Tikta, "Tikta dragged needles", true), (HeroId.Tikta, "Tikta needle storm (tap)", false),
                (HeroId.Bonk, "Bonk boulder", true), (HeroId.Bonk, "Bonk ground slam", false), (HeroId.Knut, "Knut anchor slam", false),
                (HeroId.Oaken, "Oaken slam", false), (HeroId.Oaken, "Oaken quake", true), (HeroId.Bram, "Bram Anvil Drop", true),
            };
            foreach (var c in sp)
                foreach (var how in new[] { "stun at once", "root at once", "stun one tick before release", "stun, but Moa-shielded" })
                {
                    var m = SMake(c.h, new[] { c.h == HeroId.Moa ? HeroId.Tyra : HeroId.Moa, c.h == HeroId.Fenn ? HeroId.Tyra : HeroId.Fenn }, new[] { HeroId.Bram == c.h ? HeroId.Knut : HeroId.Bram, HeroId.Hilde, HeroId.Blast });
                    var s = m.Player; var t = m.SFoe(HeroId.Bram == c.h ? HeroId.Knut : HeroId.Bram); m.SPark(s, t);
                    SPlace(s, 7.5f, 21f); SPlace(t, 7.5f, 19.9f); m.SFreeze(t); s.AddRaw(St.Disarm, 1e9f, 0, 0, null, false);
                    if (how.Contains("Moa")) s.AddRaw(St.Shield, 60, 0, 1e6f, m.SAlly(HeroId.Moa), false);
                    float hp0 = t.Hp;
                    if (!m.SCast(s, c.drag ? -SimMath.PI / 2 : float.NaN, c.n.Contains("Anvil") ? 0.25f : 1)) { SCheck(false, "INT-3", $"{c.n}: did not cast (set-up)"); continue; }
                    var w = m.Projs.Where(q => q.Src == s && !q.Done && !q.IsWarning).ToList();
                    float minT = w.Count > 0 ? w.Min(q => q.T) : 0; int preHits = 0;
                    if (how.StartsWith("stun one tick"))
                    {
                        var first = w.Where(q => q.T < 0).OrderBy(q => -q.T).FirstOrDefault(); // the next to release
                        if (first == null) continue;
                        while (first.T < -1.5f * Rules.Tick && !first.Done) { m.SStep(); preHits += m.Events.Count(e => e.Type == EventType.Hit && e.A == s && e.B == t); }
                    }
                    if (how.StartsWith("root")) m.AddStatus(s, St.Root, 1.0f); else m.AddStatus(s, St.Stun, 1.0f);
                    bool interrupt = m.Events.Any(e => e.Type == EventType.Interrupt && e.A == s);
                    bool warnLeft = m.Projs.Any(q => q.Src == s && !q.Done && q.IsWarning && q.T < q.Dur && w.Any(x => !x.Done && x.T < 0));
                    int tHits = preHits + m.Events.Count(e => e.Type == EventType.Hit && e.A == s && e.B == t);
                    for (int j = 0; j < 150; j++) { m.SStep(); foreach (var e in m.Events) { if (e.Type == EventType.Interrupt && e.A == s) interrupt = true; if (e.Type == EventType.Hit && e.A == s && e.B == t) tHits++; } }
                    float dmg = hp0 - t.Hp; int needleHits = tHits;
                    bool shielded = how.Contains("Moa");
                    bool storm = c.n.Contains("storm");
                    string line = $"{c.n}, {how} (in hand {-minT:0.00} s): damage to target {dmg:0.0}, Interrupt event {interrupt}, charge now {s.Charge}";
                    if (shielded) SCheck(dmg > 0, "INT-4", line + " -> the CC-immune caster finishes the special");
                    else if (c.n.Contains("Anvil") || (storm && how.StartsWith("stun one tick"))) { } // Anvil Drop is never cut (already falling); the storm's first needle leaves at once
                    else if (c.h == HeroId.Tikta) SCheck(needleHits == 1 && interrupt, "INT-3", line + $", needles that hit {needleHits} (the first needle leaves at the cast; the other three must be cut)");
                    else SCheck(dmg == 0 && interrupt, "INT-3", line);
                }
        }

    }
}

namespace BAM.Sim
{
    public sealed partial class Match
    {
        static void PrecScale()
        {
            int nDet = int.TryParse(Environment.GetEnvironmentVariable("BAM_PREC_DET"), out int d) ? d : 40;
            int nScan = int.TryParse(Environment.GetEnvironmentVariable("BAM_PREC_SCAN"), out int sc) ? sc : 100;
            ScaleDeterminism(nDet); ScaleD1(); ScaleLos(); ScaleHitKey(nScan); ScaleZeroDamage(); ScaleInterrupt();
        }
    }
}
