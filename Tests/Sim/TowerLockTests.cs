using System;

namespace BAM.Sim
{
    // Tower aggro holds (Rickard 2026-11-02 "man ska inte kunna tappa aggro från tornet om man har fått det förrän man går ut ur
    // range"): once a tower shoots at a hero it stays on that hero until the hero leaves its reach or dies (Match.TowerLocked).
    public sealed partial class Match
    {
        /// <summary>Place a unit and keep it there: stunned, with hp to spare so the tower never kills it by accident.</summary>
        void Still(Unit u, float x, float y) { Place(u, x, y); AddStatus(u, St.Stun, 999); u.Hp = u.MaxHp = 1e6f; }
        /// <summary>The player under the enemy tower and being shot at, every other hero stunned far away, no minions.</summary>
        static Match LockScene(out Unit p, out Unit tw)
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Moa, HeroId.Fenn }, Enemies = new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta }, Seed = 7 });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            p = m.Player; tw = m.Towers[1 - p.Team];
            float px = 2;
            foreach (var h in m.Heroes) if (h != p) { m.Still(h, px, m.Map.H / 2f); px += 1.2f; }
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
            m.Still(p, tw.X, tw.Y + dir * (tw.Range - 1.5f));
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            return m;
        }

        static void TowerLockChecks()
        {
            var m = LockScene(out var p, out var tw);
            Check(tw.Target == p, "LOCK: the tower shoots the lone hero in its reach");
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1;

            // a minion walks into the reach: the tower stays on the hero
            var mn = m.NewMinion(MinionKind.Melee, p.Team, tw.X + 1f, tw.Y + dir * (tw.R + 1.2f)); m.Still(mn, mn.X, mn.Y);
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            Check(tw.Target == p, "LOCK: a minion in reach does not take the aggro off the hero");

            // an ally hero starts a fight with a tower defender under the tower: the first hero keeps the aggro
            var ally = m.Heroes.Find(h => h != p && h.Team == p.Team); var foe = m.Heroes.Find(h => h.Team != p.Team);
            m.Still(ally, tw.X - 1.5f, tw.Y + dir * (tw.R + 1.5f)); m.Still(foe, tw.X - 1.5f, tw.Y + dir * (tw.R + 2.5f));
            for (int k = 0; k < 120; k++) { if (k % 20 == 0) { m.DealDamage(ally, foe, 1, DmgKind.Auto); m.DealDamage(foe, ally, 1, DmgKind.Auto); } m.Step(PlayerInput.None); }
            Check(tw.Target == p, "LOCK: another hero fighting under the tower does not take the aggro");

            // the old 2 s fight memory runs out: still locked
            for (int k = 0; k < 300; k++) m.Step(PlayerInput.None);
            Check(tw.Target == p, "LOCK: the aggro holds long after the last fight while the hero stays in reach");

            // the hero leaves the reach: the tower turns to someone in reach (the ally who is fighting there)
            m.Still(p, tw.X, tw.Y + dir * (tw.Range + p.R + 2f));
            for (int k = 0; k < 120; k++) { if (k % 20 == 0) m.DealDamage(ally, foe, 1, DmgKind.Auto); m.Step(PlayerInput.None); }
            Check(tw.Target != p, "LOCK: leaving the reach ends the aggro");
            Check(tw.Target == ally, "LOCK: the tower then takes the hero fighting under it");

            // the aggro also ends when the locked hero dies
            m.Still(p, tw.X + 1.5f, tw.Y + dir * (tw.R + 1f));
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            Check(tw.Target == ally, "LOCK: coming back into reach does not steal the aggro from the locked ally");
            ally.Hp = 1; m.DealDamage(foe, ally, 1e6f, DmgKind.Auto);
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            Check(tw.Target != ally, "LOCK: the aggro ends when the locked hero dies");
        }
    }
}
