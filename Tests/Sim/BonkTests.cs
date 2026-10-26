using System;

namespace BAM.Sim
{
    // Bonk (Hero3.cs): Ground Slam, Boulder Toss knockback, Heavy Club.
    public sealed partial class Match
    {
        static void Bonk()
        {
            var m = Make(HeroId.Bonk, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var b = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            Place(b, x, y); Place(a, x + 1.2f, y); Place(c, x, y + 1.5f); Place(far, x - 4, y);

            // Heavy Club: every 4th swing is a power hit
            bool[] bigs = new bool[8];
            for (int i = 0; i < 8; i++) { bool big = false; float k = m.HeroAttackCoeff(b, a, ref big); bigs[i] = big && Near(k, 1 + BonkS.PowerBonus); }
            Check(!bigs[0] && !bigs[1] && !bigs[2] && bigs[3] && !bigs[4] && bigs[7], "every 4th swing is a power hit");

            // Ground Slam: a wind-up, then everyone round him takes the hit and a stun; nobody in reach: no cast
            b.Charge = b.HDef.MaxCharge;
            Check(m.TryCastSpecial(b, float.NaN, 0), "the slam casts with enemies in reach");
            Check(a.Hp == a.MaxHp, "nothing lands before the wind-up");
            for (int k = 0; k * Rules.Tick < BonkS.SlamWindup + 0.05f; k++) m.UpdateProjectiles(Rules.Tick);
            float want = b.StatDmg() * BonkS.SlamCoeff;
            Check(Near(a.MaxHp - a.Hp, want * a.TakenMult) && a.Has(St.Stun), "the slam hits and stuns an enemy beside him");
            Check(c.Has(St.Stun) && far.Hp == far.MaxHp && !far.Has(St.Stun), "the slam reaches 2 tiles, not 4");
            Place(a, x + 4, y); Place(c, x + 5, y); Place(far, x - 4, y); b.Charge = b.HDef.MaxCharge;
            Check(!m.TryCastSpecial(b, float.NaN, 0), "no slam with nobody in reach");

            // Boulder Toss: the first enemy on the line is hit and knocked back 1.5 tiles along it
            Place(a, x + 3, y); Place(c, x + 4.5f, y); b.RemoveAll(St.Casting); b.RemoveAll(St.Channel);
            Check(m.TryCastSpecial(b, 0, 1), "the boulder casts");
            float ax0 = a.X;
            for (int k = 0; k < 90; k++) { m.UpdateProjectiles(Rules.Tick); if (a.KnockT > 0) m.KnockStep(a, Rules.Tick); }
            Check(a.Hp < a.MaxHp && c.Hp == c.MaxHp, "the boulder stops in the first enemy");
            Check(Near(a.X - ax0, BonkS.KnockDist, 0.05f) && Near(a.Y, y), $"knocked back 1.5 tiles along the line: {a.X - ax0:0.00}");
        }
    }
}
