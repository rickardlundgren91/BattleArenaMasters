using System;

namespace BAM.Sim
{
    // Bram as ABC2's Silver (Heroes.cs BramS.Silver, Rickard 2026-11-11): Hammer Line (a charge, then a thrown hammer that flies straight) + Focus.
    public sealed partial class Match
    {
        static void Hold(Unit u, float x, float y) { u.X = u.PX = x; u.Y = u.PY = y; }
        static void SilverChecks()
        {
            if (!BramS.Silver) return;
            var m = Make(HeroId.Bram, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta });
            var b = m.Player; var a = m.Foe(HeroId.Knut); var c = m.Foe(HeroId.Fenn); var far = m.Foe(HeroId.Tikta);
            foreach (var u in m.Heroes) if (u != b) Place(u, u.Team == b.Team ? 2 : 13, u.Team == b.Team ? 30 : 4);
            float x = 3, y = 20;
            Place(b, x, y); Place(a, x + 2, y); Place(c, x + 4.5f, y + 0.2f);
            b.Charge = b.HDef.MaxCharge;
            Check(m.TryCastSpecial(b, 0, 1), "SILVER: the Hammer Line beam is cast");
            var p = m.Projs.Find(q => q.Mode == ProjMode.HammerLine && !q.Done);
            Check(p != null && p.Kind == ProjKind.BramBolt && p.T < 0, "SILVER: the hammer waits in his hand (the charge)");
            Check(m.Projs.Exists(q => q.Mode == ProjMode.WarnLine && !q.Done), "SILVER: a red warning line shows during the charge");
            Check(b.Has(St.Disarm) && b.Has(St.Casting), "SILVER: he stands and charges");
            float ha = a.Hp, hc = c.Hp; int hits = 0, chargeTicks = 0;
            for (int k = 0; k < 100 && p != null && !p.Done; k++)
            {
                bool charging = p.T < -Rules.Tick * 0.5f;
                Hold(a, x + 2, y); Hold(c, x + 4.5f, y + 0.2f); // they stand still (their bots would walk out of the line)
                m.Events.Clear(); m.Step(PlayerInput.None);
                foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == b && (e.B == a || e.B == c)) hits++;
                if (charging && hits == 0) chargeTicks++;
            }
            Check(Math.Abs(chargeTicks * Rules.Tick - BramS.BeamCharge) < 0.1f, $"SILVER: nothing is hit during the {BramS.BeamCharge} s charge ({chargeTicks * Rules.Tick:0.00})");
            Check(a.Hp < ha && c.Hp < hc && hits == 2, $"SILVER: the thrown hammer hits both enemies in the line, each once ({hits})");
            Check(p.Done && p.D <= BramS.LineReach + 0.01f, "SILVER: the bolt reaches at most LineReach");

            // a stun during the charge cuts the bolt
            var m2 = Make(HeroId.Bram, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta });
            var b2 = m2.Player; var a2 = m2.Foe(HeroId.Knut);
            foreach (var u in m2.Heroes) if (u != b2) Place(u, u.Team == b2.Team ? 2 : 13, u.Team == b2.Team ? 30 : 4);
            Place(b2, x, y); Place(a2, x + 2, y); b2.Charge = b2.HDef.MaxCharge;
            m2.TryCastSpecial(b2, 0, 1); var p2 = m2.Projs.Find(q => q.Mode == ProjMode.HammerLine && !q.Done);
            float ha2 = a2.Hp; m2.AddStatus(b2, St.Stun, 0.5f);
            for (int k = 0; k < 40; k++) { Hold(a2, x + 2, y); m2.Step(PlayerInput.None); }
            Check(p2.Done && a2.Hp >= ha2, "SILVER: a stun during the charge cuts the bolt");

            // Focus: the same target again hits harder, step by step up to FocusMax; a new target starts again
            float f0 = m.BramFocus(b, a), f1 = m.BramFocus(b, a), f2 = m.BramFocus(b, a);
            Check(f0 == 0 && Near(f1, BramS.FocusStep) && Near(f2, 2 * BramS.FocusStep), $"SILVER: Focus grows on the same target ({f0}, {f1}, {f2})");
            for (int i = 0; i < 10; i++) m.BramFocus(b, a);
            Check(Near(m.BramFocus(b, a), BramS.FocusStep * BramS.FocusMax), "SILVER: Focus stops at FocusMax");
            Check(Near(BramS.FocusStep * BramS.FocusMax, 0.8f) && HeroCatalog.Get(HeroId.Bram).PassiveDesc.Contains("10% harder, up to 80%"), "SILVER: Focus tops out at +80 % in 10 % steps, as the hold card says (Rickard 2026-11-13)");
            Check(m.BramFocus(b, c) == 0, "SILVER: a new target starts again");
            // as the old game's Sharpshooter: a swing at Fenn's clone neither counts nor starts it again (hero review 2026-11-13)
            m.BramFocus(b, c); far.IsClone = true; float fc = m.BramFocus(b, far); far.IsClone = false;
            Check(fc == 0 && Near(m.BramFocus(b, c), 2 * BramS.FocusStep), "SILVER: a swing at Fenn's clone leaves Focus as it was");
            // a death ends it (hero review 2026-11-16): the first swing after his respawn starts again, even at the same target
            Check(b.FocusSteps == 2, $"SILVER: the view sees his Focus steps ({b.FocusSteps})");
            m.Revive(b);
            Check(b.FocusSteps == 0 && m.BramFocus(b, c) == 0, "SILVER: Focus starts again after a respawn");
            Check(!m.Projs.Exists(q => q.Mode == ProjMode.Echo && !q.Done), "SILVER: no Backlash echo");
        }
    }
}
