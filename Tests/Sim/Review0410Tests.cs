using System;

namespace BAM.Sim
{
    // Release review 0.4.1 (2026-11-12): a tower locked on Fenn went on shooting him through his Veil (TowerLocked kept him), though
    // the Veil beats every sight, a tower's too (Heroes.cs FennS.Veil). Now it lets him go as if he had left its reach.
    public sealed partial class Match
    {
        static void Review0410()
        {
            if (!FennS.Veil) return;
            var m = Create(new MatchConfig { PlayerHero = HeroId.Fenn, Allies = new[] { HeroId.Moa, HeroId.Tyra }, Enemies = new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta }, Seed = 7 });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            var f = m.Player; var tw = m.Towers[1 - f.Team];
            float px = 2;
            foreach (var h in m.Heroes) if (h != f) { m.Still(h, px, m.Map.H / 2f); px += 1.2f; }
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
            m.Still(f, tw.X, tw.Y + dir * (tw.Range - 1.5f));
            for (int k = 0; k < 120; k++) m.Step(PlayerInput.None);
            Check(tw.Target == f && m.TowerLocked(tw), "REVIEW 0.4.1: the tower is locked on Fenn under it");

            // he veils: the tower lets him go, never shoots him while he is unseen, and takes his decoy
            m.Projs.Clear(); // no shot of before still on its way
            m.CastFennVeil(f);
            bool shotAt = false, hit = false, decoy = false;
            for (int k = 0; k < (int)(2f / Rules.Tick); k++)
            {
                m.Step(PlayerInput.None);
                if (m.Projs.Exists(q => q.Src == tw && q.Tgt == f && !q.Done)) shotAt = true;
                if (m.Events.Exists(e => e.Type == EventType.Damage && e.A == tw && e.B == f)) hit = true;
                if (tw.Target != null && tw.Target.IsClone && tw.Target.Owner == f) decoy = true;
            }
            Check(f.Has(St.Veil), "REVIEW 0.4.1: still veiled after 2 s");
            Check(tw.Target != f && !m.TowerLocked(tw), "REVIEW 0.4.1: a tower lets a veiled Fenn go (no lock on whom it cannot see)");
            Check(!shotAt && !hit, "REVIEW 0.4.1: no tower shot at the veiled Fenn");
            Check(decoy, "REVIEW 0.4.1: the tower takes his decoy instead");

            // the Veil runs out: seen again, the tower takes him again
            for (int k = 0; k < (int)((FennS.VeilTime + 0.5f) / Rules.Tick) && f.Has(St.Veil); k++) m.Step(PlayerInput.None);
            for (int k = 0; k < 60; k++) m.Step(PlayerInput.None);
            Check(!f.Has(St.Veil) && tw.Target == f, "REVIEW 0.4.1: seen again, he is the tower's target again");
        }
    }
}
