using System;

namespace BAM.Sim
{
    // Bram as ABC2's Silver (Heroes.cs BramS.Silver, Rickard 2026-11-11): Hammer Line + Focus.
    public sealed partial class Match
    {
        static void SilverChecks()
        {
            if (!BramS.Silver) return;
            var m = Make(HeroId.Bram, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta });
            var b = m.Player; var a = m.Foe(HeroId.Knut); var c = m.Foe(HeroId.Fenn); var far = m.Foe(HeroId.Tikta);
            foreach (var u in m.Heroes) if (u != b) Place(u, u.Team == b.Team ? 2 : 13, u.Team == b.Team ? 30 : 4);
            float x = 3, y = 20;
            Place(b, x, y); Place(a, x + 2, y); Place(c, x + 4.5f, y + 0.2f);
            b.Charge = b.HDef.MaxCharge;
            Check(m.TryCastSpecial(b, 0, 1), "SILVER: the Hammer Line is thrown");
            var p = m.Projs.Find(q => q.Mode == ProjMode.HammerLine && !q.Done);
            Check(p != null, "SILVER: a HammerLine shot flies");
            float ha = a.Hp, hc = c.Hp; int hits = 0;
            for (int k = 0; k < 100 && p != null && !p.Done; k++) { m.Events.Clear(); m.HammerLineUpdate(p, Rules.Tick); foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == b) hits++; }
            Check(a.Hp < ha && c.Hp < hc && hits == 2, $"SILVER: it hits both enemies on its way, each once ({hits})");
            Check(p.D <= BramS.LineReach + 0.01f, "SILVER: it flies at most LineReach");

            // Focus: the same target again hits harder, step by step up to FocusMax; a new target starts again
            float f0 = m.BramFocus(b, a), f1 = m.BramFocus(b, a), f2 = m.BramFocus(b, a);
            Check(f0 == 0 && Near(f1, BramS.FocusStep) && Near(f2, 2 * BramS.FocusStep), $"SILVER: Focus grows on the same target ({f0}, {f1}, {f2})");
            for (int i = 0; i < 10; i++) m.BramFocus(b, a);
            Check(Near(m.BramFocus(b, a), BramS.FocusStep * BramS.FocusMax), "SILVER: Focus stops at FocusMax");
            Check(m.BramFocus(b, c) == 0, "SILVER: a new target starts again");
            Check(!m.Projs.Exists(q => q.Mode == ProjMode.Echo && !q.Done), "SILVER: no Backlash echo");
        }
    }
}
