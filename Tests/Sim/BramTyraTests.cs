using System;

namespace BAM.Sim
{
    // Bram's Anvil Drop and Backlash, Tyra's Arrow Volley and Featherstep (Hero5.cs), and Blast's tap picking minions too.
    public sealed partial class Match
    {
        static void BramTyra()
        {
            var m = Make(HeroId.Bram, new[] { HeroId.Tyra, HeroId.Fenn }, new[] { HeroId.Moa, HeroId.Knut, HeroId.Tikta });
            var b = m.Player; var a = m.Foe(HeroId.Moa); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            Place(b, x, y); Place(a, x + 3, y); Place(c, x + 3, y + 0.8f); Place(far, x - 8, y);

            // Anvil Drop: lands after 0.6 s on the aimed point, hurts and stuns both enemies there
            b.Charge = b.HDef.MaxCharge;
            Check(m.TryCastSpecial(b, 0, 3f / BramS.Reach), "Anvil Drop casts");
            Proj d = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Drop && !p.Done) d = p;
            Check(d != null && Near(d.X, x + 3) && Near(-d.T, BramS.DropDelay), "the anvil waits 0.6 s on the aimed point");
            Check(a.Hp == a.MaxHp, "nothing before it lands");
            for (int i = 0; i * Rules.Tick < BramS.DropDelay + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(a.Hp < a.MaxHp && c.Hp < c.MaxHp && a.Has(St.Stun) && c.Has(St.Stun), "both enemies under it are hurt and stunned");

            // Backlash: every EchoEvery-th hit (every hit since 2026-11-10) sends an echo back towards Bram that hits the first other enemy
            Place(a, x + 3, y); Place(c, x + 1.5f, y); b.PassHits = 0;
            for (int i = 0; i < BramS.EchoEvery - 1; i++) m.BramBacklash(b, a);
            bool none = true; foreach (var p in m.Projs) if (p.Mode == ProjMode.Echo && !p.Done) none = false;
            Check(none, "no echo before the EchoEvery-th hit");
            m.BramBacklash(b, a);
            for (int i = 0; i < 30; i++) m.UpdateProjectiles(Rules.Tick);
            Check(Near(c.MaxHp - c.Hp, b.StatDmg() * BramS.EchoCoeff * c.TakenMult, 0.05f) && a.Hp == a.MaxHp, "the echo hits the enemy between, not the target");

            // Featherstep: once every 10 s
            var t = m.Units.Find(u => u.Kind == UnitKind.Hero && u.Hero == HeroId.Tyra);
            Place(t, x, y - 2); m.Featherstep(t, a, 10);
            Check(t.Has(St.SpeedPct), "Featherstep speeds Tyra up");
            t.ClearStatuses(); m.Featherstep(t, a, 10);
            Check(!t.Has(St.SpeedPct), "Featherstep waits 10 s");

            // Arrow Volley: five arrows, each enemy hurt once
            Place(t, x, y); Place(a, x + 2.5f, y); Place(c, x + 2.5f, y + 0.5f); t.Charge = t.HDef.MaxCharge;
            Check(m.TryCastSpecial(t, float.NaN, 0), "the volley casts");
            int n = 0; foreach (var p in m.Projs) if (p.Mode == ProjMode.Volley && !p.Done) n++;
            Check(n == TyraS.VolleyArrows, "five arrows");
            for (int i = 0; i < 40; i++) m.UpdateProjectiles(Rules.Tick);
            float one = t.StatDmg() * TyraS.VolleyCoeff;
            Check(Near(a.MaxHp - a.Hp, one * a.TakenMult, 0.05f) && Near(c.MaxHp - c.Hp, one * c.TakenMult, 0.05f), "each enemy in the cone is hit once");
            Place(a, x + 6, y); Place(c, x + 6, y + 1); t.Charge = t.HDef.MaxCharge; t.ClearStatuses();
            Check(!m.TryCastSpecial(t, float.NaN, 0), "no volley with nobody in her range");

            // Knut's Anchor Slam (tap): a wind-up, then a hit and a 40 % slow on everyone round him; nobody in reach: no cast
            var kn = m.Units.Find(u => u.Kind == UnitKind.Hero && u.Hero == HeroId.Knut);
            var mates = m.Units.FindAll(u => u.Kind == UnitKind.Hero && u.Team == kn.Team && u != kn);
            foreach (var u in mates) Place(u, x - 8, y + 4);
            Place(kn, x, y); Place(b, x + 1.2f, y); Place(t, x + 4, y); kn.Charge = kn.HDef.MaxCharge;
            Check(m.TryCastSpecial(kn, float.NaN, 0), "the anchor slam casts with an enemy in reach");
            Check(b.Hp == b.MaxHp, "nothing before the wind-up");
            for (int i = 0; i * Rules.Tick < KnutS.SlamWindup + 0.05f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(Near(b.MaxHp - b.Hp, kn.StatDmg() * KnutS.SlamCoeff * b.TakenMult, 0.05f) && b.Has(St.SlowPct) && t.Hp == t.MaxHp, "the slam hits and slows the enemy beside him, not one 4 tiles away");
            Place(b, x + 4, y); kn.Charge = kn.HDef.MaxCharge; kn.ClearStatuses();
            Check(!m.TryCastSpecial(kn, float.NaN, 0), "no slam with nobody in reach");
        }

    }
}
