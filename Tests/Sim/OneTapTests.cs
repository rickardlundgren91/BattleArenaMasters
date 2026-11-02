using System;

namespace BAM.Sim
{
    // One special per hero, tapped at a random target in reach (Rules.OneTapSpecial, Sim/OneTap.cs, Rickard 2026-11-01).
    public sealed partial class Match
    {
        static readonly HeroId[] OneTapHeroes = { HeroId.Bram, HeroId.Fenn, HeroId.Tyra, HeroId.Moa, HeroId.Knut, HeroId.Tikta, HeroId.Blast, HeroId.Bonk };

        /// <summary>The hero alone in an open spot (cx, cy) with every other hero parked stunned along the top edge, no minions.</summary>
        static Match OneTapScene(HeroId hero, out Unit p, out float cx, out float cy)
        {
            var others = Array.FindAll(OneTapHeroes, x => x != hero);
            var m = Create(new MatchConfig { PlayerHero = hero, Allies = new[] { others[0], others[1] }, Enemies = new[] { others[2], others[3], others[4] }, Seed = 11 });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            p = m.Player; float px = 2;
            foreach (var h in m.Heroes) if (h != p) { Place(h, px, m.Map.H - 1.5f); px += 1.2f; m.AddStatus(h, St.Stun, 999); }
            cx = -1; cy = -1;
            for (int y = 8; y < m.Map.H - 8 && cx < 0; y++)
                for (int x = 4; x < m.Map.W - 4 && cx < 0; x++)
                {
                    bool ok = true;
                    for (int dy = -3; dy <= 3 && ok; dy++)
                        for (int dx = -3; dx <= 3 && ok; dx++)
                            if (!m.Map.Free(x + 0.5f + dx, y + 0.5f + dy, 0.5f) || m.Map.InGrass(x + 0.5f + dx, y + 0.5f + dy)) ok = false;
                    if (ok) { cx = x + 0.5f; cy = y + 0.5f; }
                }
            Place(p, cx - 1.5f, cy);
            p.Charge = p.HDef.MaxCharge;
            return m;
        }

        static PlayerInput TapIn() { var i = PlayerInput.None; i.Special = true; i.AimAngle = float.NaN; return i; }

        static void OneTapChecks()
        {
            Check(Rules.OneTapSpecial, "ONETAP: the one-tap special is on");
            foreach (var hero in OneTapHeroes)
            {
                if (hero == HeroId.Moa) continue;
                // nobody in reach: grey, the tap does nothing, the charge stays
                var m = OneTapScene(hero, out var p, out float cx, out float cy);
                Check(m.OneTap && !m.TapHasTarget(p), $"ONETAP {hero}: no enemy in reach, the button is grey");
                for (int k = 0; k < 30; k++) m.Step(k == 0 ? TapIn() : PlayerInput.None);
                Check(p.SpecialReady, $"ONETAP {hero}: a tap with nobody in reach keeps the charge");

                // an enemy hero 2.9 tiles off: the tap casts the kept (drag) form at it
                m = OneTapScene(hero, out p, out cx, out cy);
                Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                Place(e, cx + 1.4f, cy); m.AddStatus(e, St.Stun, 999);
                Check(m.TapHasTarget(p), $"ONETAP {hero}: an enemy in reach lights the button");
                bool cast = false, dragForm = false; float ax = 0, ay = 0;
                m.Step(TapIn());
                foreach (var ev in m.Events) if (ev.Type == EventType.Cast && ev.A == p) { cast = true; dragForm = ev.Big; ax = ev.X; ay = ev.Y; }
                Check(cast && dragForm == (hero != HeroId.Bonk) && !p.SpecialReady, $"ONETAP {hero}: a tap casts the hero's one form (cast {cast}, drag form {dragForm}; Bonk slams)");
                if (hero == HeroId.Bonk)
                {
                    bool big = false; foreach (var q in m.Projs) if (q.Mode == ProjMode.Slam && q.Src == p && Math.Abs(q.Radius - BonkS.SlamRadiusOneTap) < 1e-4f) big = true;
                    int hit = 0; for (int k = 0; k < 40; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) if (ev.Type == EventType.Hit && ev.A == p && ev.B == e && ev.Kind == (byte)ProjKind.BonkSlam) hit++; }
                    Check(big && hit == 1 && e.Has(St.Stun), $"ONETAP Bonk: the big Ground Slam ({BonkS.SlamRadiusOneTap} tiles) hits and stuns an enemy 2.9 tiles off ({hit} hits)");
                    continue;
                }
                // aimed at the target: the cast's spot lies on the line to it
                float side = Math.Abs((ax - p.X) * (e.Y - p.Y) - (ay - p.Y) * (e.X - p.X)) / Math.Max(1e-3f, SimMath.Dist(p, e));
                Check(side < 0.05f, $"ONETAP {hero}: aimed at the enemy ({side:0.000} tiles off its line)");
                if (hero == HeroId.Blast || hero == HeroId.Bram)
                    Check(SimMath.Hypot(ax - e.X, ay - e.Y) < 0.05f, $"ONETAP {hero}: lands on the target's spot ({SimMath.Hypot(ax - e.X, ay - e.Y):0.000} off)");
                // a drag sent anyway (an old HUD) is a tap too
                m = OneTapScene(hero, out p, out cx, out cy);
                Place(m.Heroes.Find(h => h.Team != p.Team), cx + 1.4f, cy);
                var drag = PlayerInput.None; drag.Special = true; drag.AimAngle = SimMath.PI; drag.AimK = 1;
                bool toward = false;
                m.Step(drag);
                foreach (var ev in m.Events) if (ev.Type == EventType.Cast && ev.A == p) toward = ev.X > p.X - 0.01f;
                Check(toward, $"ONETAP {hero}: a dragged aim is ignored, the tap picks the target");
            }

            // Moa: the field on a random ally hero in her range, on herself with none near; never grey
            {
                var m = OneTapScene(HeroId.Moa, out var p, out float cx, out float cy);
                Check(m.TapHasTarget(p), "ONETAP Moa: always lit (she can shield herself)");
                m.Step(TapIn());
                Proj f = null; foreach (var q in m.Projs) if (q.Mode == ProjMode.HoneyField && q.Src == p) f = q;
                Check(f != null && SimMath.Hypot(f.X - p.X, f.Y - p.Y) < 0.05f, "ONETAP Moa: alone, the field lands on herself");
                float sh = 0; for (int i = 0; i < p.StCount; i++) if (p.Sts[i].Type == St.Shield) sh += p.Sts[i].Amount;
                Check(Math.Abs(sh - p.MaxHp * MoaS.FieldShieldOneTap) < 1, $"ONETAP Moa: the field's shield is 10 % ({sh:0} of {p.MaxHp:0})");
                float left = 0; for (int i = 0; i < p.StCount; i++) if (p.Sts[i].Type == St.Shield) left = Math.Max(left, p.Sts[i].T);
                Check(Math.Abs(left - MoaS.FieldShieldTimeOneTap) < 0.05f, $"ONETAP Moa: the field's shield lasts 5 s ({left:0.00} s left right after the cast)");

                m = OneTapScene(HeroId.Moa, out p, out cx, out cy);
                Unit a = null; foreach (var h in m.Heroes) if (h.Team == p.Team && h != p) { a = h; break; }
                Place(a, cx + 1.4f, cy);
                m.Step(TapIn());
                f = null; foreach (var q in m.Projs) if (q.Mode == ProjMode.HoneyField && q.Src == p) f = q;
                Check(f != null && SimMath.Hypot(f.X - a.X, f.Y - a.Y) < 0.05f, "ONETAP Moa: with an ally near, the field lands on the ally");
            }

            // bots tap their one special in real matches, on both teams
            int casts = 0, dragged = 0, seeds = 0;
            for (int s = 1; s <= 4; s++)
            {
                var m = Create(new MatchConfig { PlayerHero = OneTapHeroes[s], Allies = new[] { OneTapHeroes[(s + 1) % 8], OneTapHeroes[(s + 2) % 8] }, Enemies = new[] { OneTapHeroes[(s + 3) % 8], OneTapHeroes[(s + 4) % 8], OneTapHeroes[(s + 5) % 8] }, Seed = s, PlayerIsBot = true, Difficulty = Difficulty.Hard });
                int[] team = new int[2];
                while (m.State != MatchState.Over && m.Time < 240)
                {
                    m.Step(PlayerInput.None);
                    foreach (var ev in m.Events) if (ev.Type == EventType.Cast) { casts++; if (ev.Big) dragged++; team[ev.A.Team == m.Player.Team ? 0 : 1]++; }
                }
                if (team[0] > 0 && team[1] > 0) seeds++;
            }
            Check(seeds == 4 && dragged <= casts, $"ONETAP: bots on both teams cast their one form ({casts} casts in 4 matches, both teams in {seeds})");
        }
    }
}
