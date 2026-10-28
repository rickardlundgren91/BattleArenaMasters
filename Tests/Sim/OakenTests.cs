using System;

namespace BAM.Sim
{
    // Oaken 3.0 (Hero6.cs): the slam, the quake with its knock and wall rule, Stored Impact, and the hero list.
    public sealed partial class Match
    {
        static void Oaken()
        {
            var m = Make(HeroId.Oaken, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            Check(HeroCatalog.Get(HeroId.Oaken).Name == "Oaken" && HeroCatalog.Get(HeroId.Oaken).Id == HeroId.Oaken && h.HDef.Tank && Near(h.HDef.Hp, 330), "Oaken is hero 10, a 330 hp tank");
            float x = 7.5f, y = 20, dmg = h.StatDmg();
            Place(h, x, y); Place(a, x + 1.2f, y); Place(c, x + 6, y + 6); Place(far, x - 8, y);

            // the slam (tap): a 0.6 s wind-up where he stands, then 140 % round him
            h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, float.NaN, 0), "the slam casts with an enemy in reach");
            Proj s = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Worldbreaker && !p.Done) s = p;
            Check(s != null && s.Kind == ProjKind.OakenSlam && Near(-s.T, OakenS.SlamWindup) && h.Has(St.Casting) && h.Has(St.Disarm), "the slam winds up 0.6 s and he stands still");
            for (int i = 0; i * Rules.Tick < OakenS.SlamWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(Near(a.MaxHp - a.Hp, dmg * OakenS.SlamCoeff * a.TakenMult, 0.05f), $"the slam deals 140 %: {a.MaxHp - a.Hp:0.0}");
            Place(a, x + 4, y); h.Charge = h.HDef.MaxCharge; h.ClearStatuses();
            Check(!m.TryCastSpecial(h, float.NaN, 0), "no slam with nobody in reach");

            // a stun during the wind-up cuts it
            Place(a, x + 1.2f, y); h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, float.NaN, 0), "the slam casts again");
            m.AddStatus(h, St.Stun, 1, 0, 0, a); m.InterruptSpecial(h);
            for (int i = 0; i * Rules.Tick < OakenS.SlamWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(a.Hp == a.MaxHp, "a stun in the wind-up cuts the slam");

            // the quake (drag): 0.5 s, 110 % along the line, heroes knocked 0.6 along it, minions too are hit
            Place(h, x, y); Place(a, x + 2.5f, y + 0.3f); h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, 0, 1), "the quake casts");
            Proj q = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Quake && !p.Done) q = p;
            Check(q != null && q.Kind == ProjKind.OakenQuake && Near(-q.T, OakenS.QuakeWindup), "the quake winds up 0.5 s");
            float ax0 = a.X, ay0 = a.Y;
            m.Events.Clear();
            for (int i = 0; i * Rules.Tick < OakenS.QuakeWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(Near(a.MaxHp - a.Hp, dmg * OakenS.QuakeCoeff * a.TakenMult, 0.05f), $"the quake deals 110 %: {a.MaxHp - a.Hp:0.0}");
            Check(m.HasEvent(EventType.Quake) && m.HasEvent(EventType.Knock), "the quake shows itself and its knock");
            for (int i = 0; i < 30; i++) if (a.KnockT > 0) m.KnockStep(a, Rules.Tick);
            Check(Near(a.X - ax0, OakenS.QuakeKnock, 0.05f) && Near(a.Y, ay0), $"the quake knocks 0.6 tiles along the line: {a.X - ax0:0.00}");
            Place(a, x + 5, y); h.Charge = h.HDef.MaxCharge; h.ClearStatuses();
            Check(m.TryCastSpecial(h, 0, 1), "the quake casts with nobody in it");
            for (int i = 0; i * Rules.Tick < OakenS.QuakeWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(a.Hp == a.MaxHp, "the quake ends at 3.3 tiles");

            // walls stop the quake: nothing behind a wall is hit
            if (m.FindWallEast(out int wx, out int wy))
            {
                float py = wy + 0.5f;
                Place(h, wx - 2.5f, py); Place(a, wx + 1.6f, py); h.Charge = h.HDef.MaxCharge;
                if (m.Map.Walkable(wx + 1, wy) && m.Map.Walkable(wx + 2, wy))
                {
                    Check(m.TryCastSpecial(h, 0, 1), "the quake casts towards a wall");
                    for (int i = 0; i * Rules.Tick < OakenS.QuakeWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
                    Check(a.Hp == a.MaxHp, "the quake does not run through a wall");
                }
                // a knock into a wall hurts (7 % of max health), and never stuns
                Place(h, wx - 2.0f, py); Place(a, wx - 0.75f, py); h.Charge = h.HDef.MaxCharge; h.ClearStatuses();
                Check(m.TryCastSpecial(h, 0, 1), "the quake casts at a hero by a wall");
                for (int i = 0; i * Rules.Tick < OakenS.QuakeWindup + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
                for (int i = 0; i < 30; i++) if (a.KnockT > 0) m.KnockStep(a, Rules.Tick);
                Check(a.MaxHp - a.Hp > dmg * OakenS.QuakeCoeff * a.TakenMult + 1 && !a.Has(St.Stun), $"a knock into a wall hurts but does not stun ({a.MaxHp - a.Hp:0.0})");
            }
            else Check(false, "the map has a wall to test the quake on");

            // Stored Impact: after 4 s without losing health, an enemy hero's hit is cut 30 % and part goes into his next attack
            Place(h, x, y); Place(a, x + 1, y); Place(c, x + 1, y + 1); h.ClearStatuses();
            m.Time += 10; h.OakenLastLoss = -99; h.OakenReadyAt = 0;
            float hp0 = h.Hp; m.DealDamage(a, h, 100, DmgKind.Auto);
            Check(Near(hp0 - h.Hp, 70 * h.TakenMult, 0.5f) || Near(hp0 - h.Hp, 100 * h.TakenMult * 0.7f, 0.5f), $"Stored Impact cuts the hit by 30 %: {hp0 - h.Hp:0.0}");
            float stored = h.OakenStored;
            Check(stored > 0 && stored <= h.StatDmg() * OakenS.StorageCap + 0.01f, $"part of it is stored, at most 30 % of his attack: {stored:0.0}");
            hp0 = h.Hp; m.DealDamage(a, h, 100, DmgKind.Auto);
            Check(Near(hp0 - h.Hp, 100 * h.TakenMult, 0.5f), "the next hit is not cut (16 s cooldown)");
            float c0 = c.Hp; m.OakenRelease(h, c);
            Check(Near(c0 - c.Hp, stored * c.TakenMult, 0.1f) && h.OakenStored == 0, "his next attack hit adds what was stored");
            m.Time += 20; h.OakenLastLoss = m.Time - 1; hp0 = h.Hp; m.DealDamage(a, h, 50, DmgKind.Auto);
            Check(Near(hp0 - h.Hp, 50 * h.TakenMult, 0.5f), "not ready within 4 s of losing health");
        }
    }
}
