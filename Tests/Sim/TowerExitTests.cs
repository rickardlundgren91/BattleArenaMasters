using System;
using System.Linq;

namespace BAM.Sim
{
    // Review 2026-11-13 (klutter, synlighet och gameplay, nr 1): the player's middle seat starts 4 tiles straight behind its own tower,
    // and a revive and a Recall land there too. Holding the stick within about 8 degrees of straight up from there, a hero stood
    // stuck behind its own tower for good: the tower's push had a side part that pointed back at the tower's middle line when the
    // body was behind the tower (Movement.Separate). Now it always points away from the line. Failed at fc01ecc, passes with the fix.
    public sealed partial class Match
    {
        // a normal match from its countdown, the player holding the stick at deg from straight towards the enemy tower; returns the
        // seconds of play until the player is 1.5 tiles past its own tower, or -1
        static float WalkOut(HeroId hero, float deg, float seconds, bool dieFirst = false)
        {
            var ids = HeroCatalog.GameIds.Where(x => x != hero).ToArray();
            var m = Create(new MatchConfig { Seed = 5, PlayerHero = hero, Allies = ids.Take(2).ToArray(), Enemies = ids.Skip(2).Take(3).ToArray(), Difficulty = Difficulty.Easy });
            var p = m.Player; var own = m.Towers[p.Team]; var foe = m.Towers[1 - p.Team];
            float dir = Math.Sign(foe.Y - own.Y), a = deg * (float)Math.PI / 180f;
            var go = PlayerInput.None; go.MoveX = (float)Math.Sin(a); go.MoveY = dir * (float)Math.Cos(a);
            int guard = 0;
            while (m.State != MatchState.Play && guard++ < 600) m.Step(PlayerInput.None);
            if (dieFirst)
            {
                m.Kill(p, null);
                guard = 0;
                while (!p.Alive && guard++ < 60 * 30) m.Step(PlayerInput.None);
                if (!p.Alive) return -2f;
            }
            float t0 = m.Time;
            for (int k = 0; k < (int)(seconds / Rules.Tick); k++)
            {
                m.Step(go);
                if ((p.Y - own.Y) * dir > 1.5f) return m.Time - t0;
            }
            return -1f;
        }

        static void TowerExitChecks()
        {
            foreach (var hero in HeroCatalog.GameIds)
                foreach (float deg in new[] { 0f, 5f, -5f })
                {
                    float t = WalkOut(hero, deg, 6f);
                    Check(t > 0f && t < 3.5f, $"TOWER EXIT: {hero} with the stick {deg:0} degrees from straight up walks past its own tower from the start ({(t < 0 ? "stuck" : t.ToString("0.0") + " s")})");
                }
            float r = WalkOut(HeroId.Tyra, 0f, 6f, true);
            Check(r > 0f && r < 3.5f, $"TOWER EXIT: after a revive the hero walks straight out past its own tower too ({(r < 0 ? "stuck" : r.ToString("0.0") + " s")})");
        }
    }
}
