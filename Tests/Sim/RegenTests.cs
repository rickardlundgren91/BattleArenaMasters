using System;

namespace BAM.Sim
{
    // Regen (Rickard 2026-10-30): once a hero heals out of combat, hitting minions or being hit by them does not stop it;
    // only a fight with a hero or a tower does (Unit.LastFight).
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
            Check(!h.InFight(m.Time) && h.InCombat(m.Time), "hitting a minion and being hit by one keeps the regen going (the bots still see a fight)");
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
