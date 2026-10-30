using System;

namespace BAM.Sim
{
    // Knockback cooldown (Rules.KnockCooldown, Rickard 2026-10-30): a unit knocked back is not knocked again for 2 s from that
    // knock's start; it still takes the hit (and Bonk's stun), only the slide is skipped.
    public sealed partial class Match
    {
        static void KnockCooldown()
        {
            var m = Make(HeroId.Hilde, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 3, y); Place(c, x + 3, y + 6); Place(far, x - 8, y);

            Proj Keg(float kx) { var k = m.NewProj(ProjMode.Keg, h, kx, y); k.Kind = ProjKind.HildeBurst; k.Dmg = 10; k.Radius = HildeS.BurstRadius; return k; }
            void Slide() { for (int i = 0; i < 30; i++) if (a.KnockT > 0) m.KnockStep(a, Rules.Tick); }

            float ax0 = a.X; m.KegBlast(Keg(a.X - 0.4f)); Slide();
            Check(Near(a.X - ax0, HildeS.BurstKnock, 0.05f), $"the first knock slides: {a.X - ax0:0.00}");
            float hp0 = a.Hp; ax0 = a.X; m.Time += 1; m.KegBlast(Keg(a.X - 0.4f)); Slide();
            Check(Near(a.X, ax0) && a.Hp < hp0, $"1 s later the hit lands but there is no new slide: {a.X - ax0:0.00}");
            ax0 = a.X; m.Time += 1.05f; m.KegBlast(Keg(a.X - 0.4f)); Slide();
            Check(Near(a.X - ax0, HildeS.BurstKnock, 0.05f), $"after 2 s it can be knocked again: {a.X - ax0:0.00}");

            // Bonk's boulder on a knock-immune hero: no slide, the stun still lands
            a.ClearStatuses(); ax0 = a.X; m.Knock(h, a, 0, BonkS.KnockDist, BonkS.KnockStun, null);
            Check(a.KnockT <= 0 && Near(a.X, ax0) && a.Has(St.Stun), "a boulder on a knock-immune hero stuns it without a slide");

            // death clears the cooldown
            m.Time += 0.1f; m.Knock(h, c, 0, 1, 0, null); Check(c.KnockT > 0, "another hero is knocked as usual");
            m.Kill(c, h); Check(!m.KnockImmune(c), "a hero that dies has no knock cooldown left");
        }
    }
}
