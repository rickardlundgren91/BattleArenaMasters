using System;

namespace BAM.Sim
{
    // Heroes hit minions harder (Rickard 2026-11-19 "Creepsen gör man för lite dmg på"): Rules.HeroVsMinion on a hero's hits on a
    // minion, nothing on the tower's hits on a minion (Combat.DealDamage).
    public sealed partial class Match
    {
        static void CreepDamageChecks()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Moa, HeroId.Bram }, Enemies = new[] { HeroId.Knut, HeroId.Tikta, HeroId.Fenn }, Seed = 3 });
            m.State = MatchState.Play; m.Time = 10;
            var p = m.Player;
            m.SpawnWave();
            for (int k = 0; k < 10; k++) m.Step(PlayerInput.None);
            var mn = m.Units.Find(u => u.Kind == UnitKind.Minion && u.Alive && u.Team != p.Team);
            Check(mn != null, "CREEPS: an enemy minion to hit");
            if (mn == null) return;
            float before = mn.Hp;
            float dealt = m.DealDamage(p, mn, 10, DmgKind.Auto);
            Check(Math.Abs(dealt - 10 * Rules.HeroVsMinion) < 0.01f && Math.Abs(before - mn.Hp - 10 * Rules.HeroVsMinion) < 0.01f, $"CREEPS: a hero's 10 on a minion takes {10 * Rules.HeroVsMinion} (took {before - mn.Hp})");
            before = mn.Hp;
            m.DealDamage(m.Towers[p.Team], mn, 10, DmgKind.Auto);
            Check(Math.Abs(before - mn.Hp - 10) < 0.01f, $"CREEPS: a tower's 10 on a minion stays 10 (took {before - mn.Hp})");
        }
    }
}
