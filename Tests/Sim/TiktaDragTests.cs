using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Tikta's dragged needles and their red warning (precision TIKTA-01/03/04, 2026-10-29): the warning shows where the needles
    // still in her hand will fly, it stays while one of hers flies, and a needle ends at the wall it meets or at exactly 5 tiles.
    public sealed partial class Match
    {
        static Match TiktaScene(out Unit t, out float cx, out float cy)
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tikta, Allies = new[] { HeroId.Moa, HeroId.Fenn }, Enemies = new[] { HeroId.Bram, HeroId.Knut, HeroId.Blast }, Seed = 7 });
            for (int i = 0; i < m.Units.Count; i++) { var u = m.Units[i]; if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            t = m.Player;
            float px = 2;
            foreach (var h in m.Heroes) if (h != t) { Place(h, px, m.Map.H - 1.5f); px += 1.2f; m.AddStatus(h, St.Stun, 999); }
            cx = cy = -1;
            for (int y = 4; y < m.Map.H - 4 && cx < 0; y++)
                for (int x = 4; x < m.Map.W - 4 && cx < 0; x++)
                {
                    bool ok = true;
                    for (int dy = -3; dy <= 3 && ok; dy++)
                        for (int dx = -3; dx <= 3 && ok; dx++)
                            if (!m.Map.Free(x + 0.5f + dx, y + 0.5f + dy, 0.5f) || m.Map.InGrass(x + 0.5f + dx, y + 0.5f + dy)) ok = false;
                    if (ok) { cx = x + 0.5f; cy = y + 0.5f; }
                }
            t.Charge = t.HDef.MaxCharge;
            return m;
        }

        static PlayerInput TiktaDrag(float ang) { var i = PlayerInput.None; i.Special = true; i.AimAngle = ang; i.AimK = 1; return i; }

        static void TiktaDrag()
        {
            // TIKTA-01: walking sideways through the drag, every needle leaves on the warning's line
            var m = TiktaScene(out var t, out float cx, out float cy);
            Place(t, cx - 2.5f, cy); t.Charge = t.HDef.MaxCharge;
            var seen = new HashSet<int>(); float worst = 0; int launched = 0;
            for (int k = 0; k < 50; k++)
            {
                var inp = k == 0 ? TiktaDrag(0) : PlayerInput.None; inp.MoveY = 1;
                m.Step(inp);
                Proj w = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.WarnLine && p.Src == t && !p.Done) w = p;
                foreach (var p in m.Projs)
                    if (p.Mode == ProjMode.NeedleLine && p.T >= 0 && seen.Add(p.Serial))
                    {
                        launched++;
                        if (w == null) { worst = 99; continue; }
                        worst = Math.Max(worst, Math.Abs(p.Y - w.Y)); // both run along +x: their y is the line
                    }
            }
            Check(launched == 4 && worst <= 0.02f, $"drag while walking: all 4 needles leave on the warning's line (worst {worst:0.000} tiles off)");

            // TIKTA-03: a stun after the first needle cuts the rest, but the warning stays while needle 1 flies, then goes
            m = TiktaScene(out t, out cx, out cy);
            Place(t, cx - 2.5f, cy); t.Charge = t.HDef.MaxCharge;
            bool warnWhileFlying = true, warnAfter = false;
            for (int k = 0; k < 40; k++)
            {
                if (k == 6) m.AddStatus(t, St.Stun, 0.5f);
                m.Step(k == 0 ? TiktaDrag(0) : PlayerInput.None);
                bool fly = false, warn = false;
                foreach (var p in m.Projs) { if (p.Done) continue; if (p.Mode == ProjMode.NeedleLine && p.T >= 0) fly = true; if (p.Mode == ProjMode.WarnLine && p.Src == t) warn = true; }
                if (k > 6 && fly && !warn) warnWhileFlying = false;
                if (k > 6 && !fly && warn) warnAfter = true;
            }
            Check(warnWhileFlying && !warnAfter, "stun mid-drag: the warning stays while needle 1 flies and goes with it");

            // TIKTA-04: a needle ends at exactly 5 tiles, and a needle into a wall sparks where the line meets it
            m = TiktaScene(out t, out cx, out cy);
            Place(t, cx - 2.5f, cy); t.Charge = t.HDef.MaxCharge;
            float maxD = 0;
            for (int k = 0; k < 50; k++)
            {
                m.Step(k == 0 ? TiktaDrag(0) : PlayerInput.None);
                foreach (var p in m.Projs) if (p.Mode == ProjMode.NeedleLine && p.T >= 0) maxD = Math.Max(maxD, p.D);
                foreach (var p in m.projPool) if (p.Mode == ProjMode.NeedleLine && p.Src == t) maxD = Math.Max(maxD, p.D); // ended this tick
            }
            Check(Math.Abs(maxD - TiktaS.LineReach) <= 0.001f, $"a dragged needle stops at the reach: {maxD:0.000} of {TiktaS.LineReach}");

            m = TiktaScene(out t, out _, out _);
            float bx = -1, by = -1, wallAt = -1;
            for (int y = 3; y < m.Map.H - 3 && bx < 0; y++)
                for (int x = 3; x < m.Map.W - 6 && bx < 0; x++)
                {
                    float sx = x + 0.5f, sy = y + 0.5f;
                    if (!m.Map.Free(sx, sy, 0.3f)) continue;
                    for (float d = 2f; d <= 4f; d += 0.005f)
                    {
                        if (!m.Map.ShotBlocked(sx, sy, sx + d, sy)) continue;
                        bool clear = true; for (float d2 = 0; d2 < 2f; d2 += 0.05f) if (m.Map.WallAt(sx + d2, sy)) clear = false;
                        if (clear) { bx = sx; by = sy; wallAt = d; }
                        break;
                    }
                }
            float worstWall = 0; int blocked = 0;
            for (float sh = 0; sh < 0.27f; sh += 0.03f)
            {
                Place(t, bx - sh, by); t.Charge = t.HDef.MaxCharge; t.RemoveAll(St.Channel);
                for (int k = 0; k < 50; k++)
                {
                    m.Step(k == 0 ? TiktaDrag(0) : PlayerInput.None);
                    foreach (var e in m.Events) if (e.Type == EventType.Blocked && e.Kind == (byte)ProjKind.TiktaNeedleSpecial) { blocked++; worstWall = Math.Max(worstWall, Math.Abs(bx + wallAt - e.X)); }
                }
            }
            Check(blocked >= 36 && blocked % 4 == 0 && worstWall <= 0.02f, $"needles into a wall spark at the wall: {blocked} sparks, worst {worstWall:0.000} tiles off");
        }
    }
}
