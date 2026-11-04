using System;

namespace BAM.Sim
{
    // Both bases stop every hero's straight special and Fenn's thrown wolf (Match.TowerReach; tester 2026-11-02, Rickard
    // "alla heroes ska stanna mot basen, inte orättvist", own tower too: "ja"): it used to stop only Tyra's bolt at the enemy tower.
    public sealed partial class Match
    {
        static readonly HeroId[] TowerStopHeroes = { HeroId.Tyra, HeroId.Tikta, HeroId.Knut, HeroId.Bonk, HeroId.Fenn };

        /// <summary>The hero just below the enemy tower, an enemy hero just above it on the same line (hidden behind the base), every
        /// other hero parked stunned at the bottom edge, no minions.</summary>
        static Match TowerScene(HeroId hero, out Unit p, out Unit e, out Unit tw)
        {
            var others = Array.FindAll(OneTapHeroes, x => x != hero);
            var m = Create(new MatchConfig { PlayerHero = hero, Allies = new[] { others[0], others[1] }, Enemies = new[] { others[2], others[3], others[4] }, Seed = 11 });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            p = m.Player; tw = m.Towers[1 - p.Team];
            float px = 2; e = null;
            foreach (var h in m.Heroes) if (h != p) { Place(h, px, m.Map.H - 1.5f); px += 1.2f; m.AddStatus(h, St.Stun, 999); if (e == null && h.Team != p.Team) e = h; }
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1; // from the tower toward the middle of the map
            Place(p, tw.X, tw.Y + dir * (Rules.TowerBody + p.R + 0.1f));
            Place(e, tw.X, tw.Y - dir * (tw.R + e.R + 0.15f)); m.AddStatus(e, St.Stun, 999); // inside the wall (Rules.TowerBody) so Knut and Tikta have it in tap reach; the first step pushes it out behind
            p.Charge = p.HDef.MaxCharge;
            return m;
        }

        static void TowerStopChecks()
        {
            TowerBodyChecks();
            foreach (var hero in TowerStopHeroes)
            {
                // the one tap never picks a target hidden behind the enemy base (Bonk's tap is the slam round him, not a shot)
                if (hero != HeroId.Bonk)
                {
                    var m1 = TowerScene(hero, out var p1, out var e1, out var tw1);
                    bool inReach = SimMath.Dist(p1, e1) <= TapReach(hero) + e1.R;
                    Check(inReach && !m1.TapHasTarget(p1), $"BASE {hero}: an enemy right behind the enemy tower is not a tap target (in reach {inReach})");
                }

                // the old drag form aimed straight through the tower: stopped on it, the enemy behind is not hit
                var m = TowerScene(hero, out var p, out var e, out var tw);
                m.OneTap = false;
                float ang = SimMath.Atan2(e.Y - p.Y, e.X - p.X);
                var drag = PlayerInput.None; drag.Special = true; drag.AimAngle = ang; drag.AimK = 1;
                bool blocked = false, landed = false; float ly = 0; int hits = 0;
                for (int k = 0; k < 120; k++)
                {
                    m.Step(k == 0 ? drag : PlayerInput.None);
                    foreach (var ev in m.Events) { if (ev.Type == EventType.Blocked && ev.A == p) blocked = true; if (ev.Type == EventType.Hit && ev.A == p && ev.B == e && ev.Dmg != DmgKind.Auto) hits++; }
                    foreach (var ev in m.Events) if (ev.Type == EventType.WolfPounce && !landed && ev.A != null && ev.A.Owner == p) { landed = true; ly = ev.Y; }
                }
                Check(hits == 0, $"BASE {hero}: the enemy behind the enemy tower takes no hit from the special ({hits} hits)");
                if (hero == HeroId.Fenn)
                    Check(landed && Math.Abs(ly - tw.Y) >= tw.R + Defs.Wolf.Radius - 0.05f && Math.Sign(ly - tw.Y) == Math.Sign(p.Y - tw.Y),
                        $"BASE Fenn: the thrown wolf lands on the base's near side ({(landed ? ly - tw.Y : 0):0.00} from its middle)");
                else Check(blocked, $"BASE {hero}: the special puffs out on the enemy tower");

                // the own tower stops its team's shots too (Rickard 2026-11-02 "ja"): the same line through the player's own base misses
                if (hero == HeroId.Fenn) continue;
                m = TowerScene(hero, out p, out e, out tw);
                m.OneTap = false;
                var own = m.Towers[p.Team];
                float dir = own.Y < m.Map.H / 2f ? 1 : -1;
                Place(p, own.X, own.Y + dir * (Rules.TowerBody + p.R + 0.1f));
                Place(e, own.X, own.Y - dir * (own.R + e.R + 0.15f)); m.AddStatus(e, St.Stun, 999);
                ang = SimMath.Atan2(e.Y - p.Y, e.X - p.X);
                drag.AimAngle = ang;
                hits = 0; blocked = false;
                for (int k = 0; k < 120; k++) { m.Step(k == 0 ? drag : PlayerInput.None); foreach (var ev in m.Events) { if (ev.Type == EventType.Hit && ev.A == p && ev.B == e && ev.Dmg != DmgKind.Auto) hits++; if (ev.Type == EventType.Blocked && ev.A == p) blocked = true; } }
                Check(hits == 0 && blocked, $"BASE {hero}: the hero's own tower stops its special too ({hits} hits, puff {blocked})");
            }
        }

        // Heroes cannot walk into the tower model's wall (tester Cajsa 2026-11-04 "jag kan gå rätt igenom dem"): a hero running straight
        // at either tower, from below, from the side and on a slant, stops at its wall (Rules.TowerBody), not at the hit body (0.55)
        static void TowerBodyChecks()
        {
            foreach (var hero in new[] { HeroId.Tyra, HeroId.Bonk })
                for (int t = 0; t < 2; t++)
                    for (int way = 0; way < 3; way++)
                    {
                        var m = TowerScene(hero, out var p, out var e, out var tw);
                        tw = m.Towers[t];
                        float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
                        float ox = way == 0 ? 0 : way == 1 ? 2.5f : 1.8f, oy = way == 0 ? 2.5f : way == 1 ? 0 : 1.8f;
                        Place(p, tw.X + ox, tw.Y + dir * oy); p.Hp = p.MaxHp = 1e6f;
                        float closest = 99;
                        for (int k = 0; k < 180; k++)
                        {
                            var mv = PlayerInput.None; float dx = tw.X - p.X, dy = tw.Y - p.Y, d = SimMath.Hypot(dx, dy);
                            if (d > 1e-4f) { mv.MoveX = dx / d; mv.MoveY = dy / d; }
                            m.Step(mv);
                            closest = Math.Min(closest, SimMath.Dist(p, tw));
                        }
                        Check(closest >= Rules.TowerBody + p.R - 0.02f,
                            $"TOWER BODY {hero} tower {t} way {way}: the hero stops at the wall ({closest:0.00} from the middle, wall {Rules.TowerBody + p.R:0.00})");
                    }
        }
    }
}
