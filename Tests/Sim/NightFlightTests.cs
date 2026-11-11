using System;

namespace BAM.Sim
{
    // Ravna's Night Flight + Restless (Hero8.cs RavnaS.NightFlight, ABC2's Ron, Rickard 2026-11-11 "ron blir ravna").
    public sealed partial class Match
    {
        static void NightFlightChecks()
        {
            if (!RavnaS.NightFlight) return;
            var m = Make(HeroId.Ravna, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var r = m.Player; var a = m.Foe(HeroId.Bram); var b = m.Foe(HeroId.Knut);
            foreach (var u in m.Heroes) if (u != r) Place(u, u.Team == r.Team ? 2 : 13, u.Team == r.Team ? 30 : 4);

            // find a wall with free ground on both sides along +x (some row in the middle of the map)
            float wx0 = -1, wy = -1, wx1 = -1;
            for (float y = 8; y < m.Map.H - 8 && wx0 < 0; y += 0.5f)
                for (float x = 1.5f; x < m.Map.W - 4; x += 0.25f)
                {
                    if (!m.Map.Free(x, y, r.R) || m.Map.Free(x + 0.5f, y, r.R)) continue;
                    float e = x + 0.5f; while (e < m.Map.W - 1 && !m.Map.Free(e, y, r.R)) e += 0.25f;
                    if (e - x < 3 && e < m.Map.W - 1.5f && !m.TowerAt(e, y, r.R)) { wx0 = x; wy = y; wx1 = e; break; }
                }
            Check(wx0 > 0, "FLIGHT: test map has a thin wall");
            Place(r, wx0 - 1, wy); r.Charge = r.HDef.MaxCharge;
            Check(m.TryCastSpecial(r, 0, 1), "FLIGHT: cast towards the wall");
            Check(r.Has(St.Untargetable), "FLIGHT: the flock cannot be hit");
            float t0 = m.Time; bool past = false;
            for (int k = 0; k < (int)(RavnaS.FlockTime / Rules.Tick) + 3 && r.RavnaFlying; k++) { m.RavnaTick(r, Rules.Tick); m.Time += Rules.Tick; if (r.X > wx1) past = true; }
            Check(past, $"FLIGHT: it flies through the wall (wall {wx0:0.0}-{wx1:0.0}, x {r.X:0.0})");
            Check(!r.RavnaFlying && m.Map.Free(r.X, r.Y, r.R), "FLIGHT: she lands on free ground");

            // every enemy on the way is torn once and slowed
            var m2 = Make(HeroId.Ravna, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var r2 = m2.Player; var a2 = m2.Foe(HeroId.Bram); var b2 = m2.Foe(HeroId.Knut);
            foreach (var u in m2.Heroes) if (u != r2) Place(u, u.Team == r2.Team ? 2 : 13, u.Team == r2.Team ? 30 : 4);
            float y0 = 20, x0 = 3;
            Place(r2, x0, y0); Place(a2, x0 + 2, y0); Place(b2, x0 + 4, y0);
            r2.Charge = r2.HDef.MaxCharge; m2.TryCastSpecial(r2, 0, 1);
            float ha = a2.Hp, hb = b2.Hp; int hits = 0;
            for (int k = 0; k < 60 && r2.RavnaFlying; k++) { m2.Events.Clear(); m2.RavnaTick(r2, Rules.Tick); m2.Time += Rules.Tick; foreach (var e in m2.Events) if (e.Type == EventType.Hit && e.A == r2) hits++; }
            Check(a2.Hp < ha && b2.Hp < hb, "FLIGHT: both enemies on the way are torn");
            Check(a2.Has(St.SlowPct) && b2.Has(St.SlowPct), "FLIGHT: and slowed");
            Check(hits == 2, $"FLIGHT: each once ({hits} hits)");

            // Restless: walking RunPerCharge tiles fills one hit's worth of charge
            var m3 = Make(HeroId.Ravna, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var r3 = m3.Player; Place(r3, 7.5f, 20); r3.Charge = 0; float walked = 0;
            for (int k = 0; k < 400 && walked < RavnaS.RunPerCharge; k++) { float ox = r3.X, oy = r3.Y; m3.MoveUnit(r3, 0, k / 40 % 2 == 0 ? 1 : -1, Rules.Tick); walked += SimMath.Dist(r3, ox, oy); }
            Check(Math.Abs(r3.Charge - walked / RavnaS.RunPerCharge) < 0.05f && r3.Charge > 0.9f, $"FLIGHT: Restless charges by walking ({r3.Charge:0.00} after {walked:0.0} tiles)");
        }
    }
}
