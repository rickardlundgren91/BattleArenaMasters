using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // SpecialPreview_MatchesSim (issue #2 part 8, Precision AIM-01..06, BRAM-01/02, TYRA-02, BONK-04/05, BLAST-03): the drag aim and its
    // red hit marks come from Match.AimFor / AimTargets (Sim/Aim.cs). These checks cast the real drag in random scenes and compare.
    public sealed partial class Match
    {
        static readonly HeroId[] AimHeroes = { HeroId.Bram, HeroId.Fenn, HeroId.Tyra, HeroId.Moa, HeroId.Knut, HeroId.Tikta, HeroId.Blast, HeroId.Bonk, HeroId.Hilde, HeroId.Oaken };

        static Match AimScene(HeroId hero, int seed)
        {
            var others = new List<HeroId>();
            foreach (var h in AimHeroes) if (h != hero && h != HeroId.Moa && h != HeroId.Fenn) others.Add(h);
            var m = PMake(hero, new[] { others[0], others[1] }, new[] { others[2], others[3], others[4] }, seed);
            foreach (var u in m.Heroes) { if (u != m.Player) PDummy(u); m.AddStatus(u, St.Disarm, 1e6f); }
            return m;
        }

        static bool AimSpot(Arena map, Random rnd, float r, out float x, out float y)
        {
            for (int i = 0; i < 200; i++)
            {
                x = (float)(rnd.NextDouble() * 13.4 + 0.8); y = (float)(rnd.NextDouble() * 14 + 10.5);
                if (map.Free(x, y, r + 0.05f)) return true;
            }
            x = y = 0; return false;
        }

        static void PrecAim()
        {
            var map = new Arena();
            // 1. who is hit: the marked set (AimTargets) against what the cast really does, random scenes with walls about
            int scenes = 0, same = 0; string firstBad = null; var perHero = new Dictionary<HeroId, int>();
            var rnd = new Random(29);
            var pred = new List<Unit>();
            foreach (var hero in AimHeroes)
            {
                if (hero == HeroId.Fenn && FennVeilDefault) continue; // Shadow Veil has nothing to aim (Heroes.cs)
                int bad = 0;
                for (int sc = 0; sc < 120; sc++)
                {
                    var m = AimScene(hero, 100 + sc);
                    var s = m.Player;
                    if (!AimSpot(map, rnd, s.R, out float sx, out float sy)) continue;
                    PPlace(s, sx, sy); m.AddStatus(s, St.Disarm, 1e6f); s.Charge = s.HDef.MaxCharge;
                    bool allies = hero == HeroId.Moa;
                    var targets = new List<Unit>();
                    foreach (var u in m.Heroes)
                    {
                        if (u == s) continue;
                        if ((u.Team == s.Team) != allies) { POff(u); continue; }
                        // somewhere round the aim: within the drag's reach plus a little
                        float reach = s.HDef.DragReach + 1.5f, ux = 0, uy = 0; bool ok = false;
                        for (int t = 0; t < 60 && !ok; t++)
                        {
                            float a = (float)(rnd.NextDouble() * Math.PI * 2), d = (float)(rnd.NextDouble() * reach);
                            ux = sx + SimMath.Cos(a) * d; uy = sy + SimMath.Sin(a) * d;
                            ok = map.Free(ux, uy, u.R + 0.02f) && SimMath.Hypot(ux - sx, uy - sy) > s.R + u.R + 0.1f;
                        }
                        if (!ok) { POff(u); continue; }
                        PPlace(u, ux, uy); m.AddStatus(u, St.Disarm, 1e6f);
                        if (allies) u.Hp = u.MaxHp * 0.5f;
                        targets.Add(u);
                    }
                    if (targets.Count == 0) continue;
                    // aim at a random target (so there is something to hit) with some spread, at a random length
                    var aimAt = targets[rnd.Next(targets.Count)];
                    float ang = SimMath.Atan2(aimAt.Y - sy, aimAt.X - sx) + (float)((rnd.NextDouble() - 0.5) * 0.8);
                    float k = (float)rnd.NextDouble();
                    var info = m.AimFor(s, ang, k);
                    m.AimTargets(s, info, pred);
                    var seen = new HashSet<Unit>(); foreach (var u in targets) if (m.IsVisibleTo(u, s.Team)) seen.Add(u);
                    var ev = m.PRun(150, PDrag(ang, k), PlayerInput.None);
                    var got = new HashSet<Unit>();
                    if (hero == HeroId.Fenn)
                    {   // the wolf's first bite (later it hunts on its own)
                        foreach (var e in ev) if (e.Type == EventType.Hit && e.A == s && e.Kind == (byte)ProjKind.WolfBite) { got.Add(e.B); break; }
                    }
                    else foreach (var u in targets) if (allies ? u.Has(St.Shield) : u.Hp < u.MaxHp) got.Add(u);
                    var want = new HashSet<Unit>(); foreach (var u in pred) want.Add(u);
                    got.IntersectWith(seen); want.IntersectWith(seen);
                    scenes++;
                    if (got.SetEquals(want)) { same++; continue; }
                    bad++;
                    if (firstBad == null)
                        firstBad = $"{hero} at ({F(sx)},{F(sy)}) ang {F(ang)} k {F(k)}: marked {want.Count}, hit {got.Count}"
                            + (got.Count > 0 ? $" (hit e.g. {Desc(s, info, First(got, want))})" : "") + (want.Count > 0 ? $" (marked e.g. {Desc(s, info, First(want, got))})" : "");
                }
                perHero[hero] = bad;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in perHero) if (kv.Value > 0) sb.Append($" {kv.Key} {kv.Value}");
            Verdict("SpecialPreview_HitSet_EqualsSim", "aim", scenes > 600 && same == scenes, "P1",
                $"{scenes} random drag scenes (10 heroes, walls about): marked set == hit set in {same}{(sb.Length > 0 ? "; off:" + sb : "")}{(firstBad != null ? "; first: " + firstBad : "")}");

            // 2. a line's drawn end against where the rules really stop it at a wall: Oaken's strip exactly (the same
            // LaneReach); a shot's Blocked spot (the last free point of its last tick) between one tick's flight short of the drawn end
            // and 0.1 past it (the sight line's sampling)
            int lines = 0, wrong = 0; string badAt = null; var worstBy = new Dictionary<HeroId, float>(); var wrongBy = new Dictionary<HeroId, int>();
            var lineHeroes = new[] { HeroId.Tyra, HeroId.Knut, HeroId.Tikta, HeroId.Bonk, HeroId.Oaken }; // Blast's drag is the bomb since 2026-10-30
            for (int c = 0; c < 6000 && lines < 300; c++)
            {
                var hero = lineHeroes[c % lineHeroes.Length];
                var m = AimScene(hero, 7);
                var s = m.Player;
                if (!AimSpot(map, rnd, s.R, out float sx, out float sy)) continue;
                float ang = (float)(rnd.NextDouble() * Math.PI * 2);
                PPlace(s, sx, sy);
                var info = m.AimFor(s, ang, 1);
                float full = hero == HeroId.Tyra ? map.EdgeReach(sx, sy, ang, TyraS.Flight) : s.HDef.DragReach; // Tyra's bolt: to the arena's edge
                if (info.Length >= full - 1e-3f) continue; // only casts a wall cuts
                foreach (var u in m.Heroes) if (u != s) POff(u);
                s.Charge = s.HDef.MaxCharge; m.AddStatus(s, St.Disarm, 1e6f);
                float strip = -1, blocked = -1, speed = 0;
                m.Step(PDrag(ang, 1));
                for (int t = 0; t < 150 && strip < 0 && blocked < 0; t++)
                {
                    foreach (var p in m.Projs)
                    {
                        if (p.Src != s || p.Done) continue;
                        if (p.Mode == ProjMode.FireLane || p.Mode == ProjMode.Quake) strip = p.D;
                        if (p.IsShot) speed = Math.Max(speed, p.Mode == ProjMode.Hook ? KnutS.OutSpeed : p.Speed);
                    }
                    foreach (var e in m.Events) if (e.Type == EventType.Blocked && e.A == s) blocked = SimMath.Hypot(e.X - sx, e.Y - sy);
                    m.Step(PlayerInput.None);
                }
                lines++;
                float gap = strip >= 0 ? info.Length - strip : blocked >= 0 ? info.Length - blocked : 99;
                bool ok = strip >= 0 ? Math.Abs(gap) <= 0.02f : gap >= -0.1f && gap <= speed * Rules.Tick + 0.1f;
                worstBy[hero] = Math.Max(worstBy.TryGetValue(hero, out float wb) ? wb : 0, Math.Abs(gap));
                if (!ok) { wrong++; wrongBy[hero] = (wrongBy.TryGetValue(hero, out int wn) ? wn : 0) + 1; if (badAt == null) badAt = $"{hero} at ({F(sx)},{F(sy)}) ang {F(ang)}: aim {F(info.Length)}, strip {F(strip)}, blocked at {F(blocked)}"; }
            }
            Verdict("SpecialPreview_StopsAtWall", "aim", lines > 100 && wrong == 0, "P1",
                $"{lines} wall-cut drags (5 line heroes): {wrong} where the aim's end is not where the rules stop ({WrongList(wrongBy)}){(badAt != null ? " (first: " + badAt + ")" : "")}; worst |gap| per hero:{WorstList(worstBy)}");

            // 3. Anvil Drop's warning stays until the anvil lands, even when Bram dies at once (BRAM-01)
            {
                var m = AimScene(HeroId.Bram, 3);
                var s = m.Player; PPlace(s, 7.5f, 14.5f); s.Charge = s.HDef.MaxCharge;
                m.Step(PDrag(0, 0.6f));
                s.Hp = 0; s.Alive = false; s.RespawnT = 1e9f;
                int warnTicks = 0, ticks = 0; bool landed = false;
                for (int t = 0; t < 60 && !landed; t++)
                {
                    m.Step(PlayerInput.None); ticks++;
                    bool warn = false, drop = false;
                    foreach (var p in m.Projs) { if (p.Mode == ProjMode.WarnCircle && p.Src == s && !p.Done) warn = true; if (p.Mode == ProjMode.Drop && !p.Done) drop = true; }
                    if (warn) warnTicks++;
                    if (!drop) landed = true;
                }
                Verdict("Warning_AnvilOutlivesBram", "aim", landed && warnTicks >= ticks - 1, "P1", $"Bram dead one tick after the cast: warning shown {warnTicks} of {ticks} ticks until the anvil landed");
            }

            // 4. Tyra's bolt warning: as long as the bolt flies to the wall, and gone when the bolt is (TYRA-02)
            {
                int cases = 0, okLen = 0, okEnd = 0;
                for (int c = 0; c < 400 && cases < 40; c++)
                {
                    var m = AimScene(HeroId.Tyra, 5);
                    var s = m.Player;
                    if (!AimSpot(map, rnd, s.R, out float sx, out float sy)) continue;
                    float ang = (float)(rnd.NextDouble() * Math.PI * 2);
                    PPlace(s, sx, sy); foreach (var u in m.Heroes) if (u != s) POff(u);
                    var info = m.AimFor(s, ang, 1);
                    if (info.Length >= TyraS.Length - 1e-3f) continue;
                    s.Charge = s.HDef.MaxCharge; m.AddStatus(s, St.Disarm, 1e6f);
                    m.Step(PDrag(ang, 1));
                    Proj w = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.WarnLine && p.Src == s) w = p;
                    if (w == null) continue;
                    cases++;
                    if (Math.Abs(w.D - info.Length) < 1e-4f) okLen++;
                    bool boltGone = false, warnAfter = false;
                    for (int t = 0; t < 90; t++)
                    {
                        m.Step(PlayerInput.None);
                        bool bolt = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.Bolt && !p.Done) bolt = true;
                        if (!bolt && !boltGone) { boltGone = true; continue; }
                        if (boltGone && !w.Done && w.Mode == ProjMode.WarnLine && w.Src == s) warnAfter = true;
                    }
                    if (!warnAfter) okEnd++;
                }
                Verdict("Warning_TyraBolt_LengthAndLife", "aim", cases >= 20 && okLen == cases && okEnd == cases, "P2",
                    $"{cases} bolts into walls: warning length == aim length in {okLen}, warning gone with the bolt in {okEnd}");
            }
        }

        static string WrongList(Dictionary<HeroId, int> d) { var sb = new System.Text.StringBuilder(); foreach (var kv in d) sb.Append($"{kv.Key} {kv.Value} "); return sb.ToString().Trim(); }

        static string WorstList(Dictionary<HeroId, float> d) { var sb = new System.Text.StringBuilder(); foreach (var kv in d) sb.Append($" {kv.Key} {F(kv.Value)}"); return sb.ToString(); }

        static Unit First(HashSet<Unit> a, HashSet<Unit> b) { foreach (var u in a) if (!b.Contains(u)) return u; foreach (var u in a) return u; return null; }

        static string Desc(Unit s, in AimInfo a, Unit u)
        {
            if (u == null) return "-";
            float ca = SimMath.Cos(a.Ang), sa = SimMath.Sin(a.Ang), dx = u.X - a.X, dy = u.Y - a.Y;
            return $"{u.Hero} R {F(u.R)} along {F(dx * ca + dy * sa)} side {F(-dx * sa + dy * ca)} len {F(a.Length)} circle d {F(SimMath.Hypot(u.X - a.CX, u.Y - a.CY))}";
        }
    }
}
