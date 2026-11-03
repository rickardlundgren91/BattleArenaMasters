using System;

namespace BAM.Sim
{
    // Regen (Rickard 2026-11-03, as ABC): any fight stops it, minions too (Unit.LastFight). From 2026-10-30 to 11-03 only a
    // fight with a hero or a tower did.
    public sealed partial class Match
    {
        static void RegenFights()
        {
            var m = Make(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var foe = m.Foe(HeroId.Bram);
            var mob = m.NewMinion(MinionKind.Melee, Teams.Enemy, h.X + 1, h.Y);
            m.Time += 10;
            Check(!h.InFight(m.Time), "a hero 10 s from any fight regenerates");
            m.DealDamage(h, mob, 5, DmgKind.Auto); m.DealDamage(mob, h, 5, DmgKind.Auto);
            Check(h.InFight(m.Time) && h.InCombat(m.Time), "hitting a minion and being hit by one stops the regen too");
            m.DealDamage(h, foe, 5, DmgKind.Auto);
            Check(h.InFight(m.Time), "hitting an enemy hero stops the regen");
            m.Time += 10; m.DealDamage(foe, h, 5, DmgKind.Auto);
            Check(h.InFight(m.Time), "being hit by an enemy hero stops the regen");
            m.Time += 10; m.DealDamage(h, m.Towers[Teams.Enemy], 5, DmgKind.Auto);
            Check(h.InFight(m.Time), "hitting a tower stops the regen");
            m.Time += 10; m.DealDamage(m.Towers[Teams.Enemy], h, 5, DmgKind.Auto);
            Check(h.InFight(m.Time), "a tower's shot stops the regen");
            m.Time += Rules.HeroOutOfCombat + 0.01f;
            Check(!h.InFight(m.Time), "3 s after the last hero or tower fight the regen starts again");
        }
    }
}
