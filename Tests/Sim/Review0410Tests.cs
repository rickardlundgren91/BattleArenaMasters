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

            // he veils: towers see everything in their reach since 2026-11-18 (Rickard "Dom ska se allt"): it stays on him and shoots
            // him, not his decoy (until then it let him go and took the decoy, release review 0.4.1)
            m.Projs.Clear(); // no shot of before still on its way
            m.CastFennVeil(f);
            bool hit = false, decoy = false;
            for (int k = 0; k < (int)(2f / Rules.Tick); k++)
            {
                m.Step(PlayerInput.None);
                if (m.Events.Exists(e => e.Type == EventType.Damage && e.A == tw && e.B == f)) hit = true;
                if (tw.Target != null && tw.Target.IsClone && tw.Target.Owner == f) decoy = true;
            }
            Check(f.Has(St.Veil), "REVIEW 0.4.1: still veiled after 2 s");
            Check(tw.Target == f && m.TowerLocked(tw), "TOWER SEES ALL: the tower keeps its lock on a veiled Fenn");
            Check(hit && !decoy, "TOWER SEES ALL: it shoots the veiled Fenn, not his decoy");
            Check(m.IsVisibleTo(f, tw.Team), "TOWER SEES ALL: the tower's team sees a veiled Fenn in its reach");
        }
    }
}
