using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Bram and Knut (Precision & Polish 2026-10-29, BRAM-03/06, KNUT-01/02): the anvil never lands dud inside a wall, the echo hits
    // the real enemy behind Fenn's decoy at first contact, Knut stands until his catch is in, and the anchor never slips a wall corner.
    public sealed partial class Match
    {
        static Match PBramKnutScene(HeroId player)
        {
            var m = PMake(player, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Fenn, HeroId.Knut, HeroId.Tikta });
            foreach (var u in m.Heroes) PDummy(u);
            return m;
        }

        static void PrecBramKnut()
        {
            // BRAM-03: Anvil Drop aimed at the middle of a wall tile, Knut standing against that wall's face inside the ring
            {
                var m = PBramKnutScene(HeroId.Bram); var b = m.Player; var k = m.PFoe(HeroId.Knut);
                foreach (var u in m.Heroes) if (u != b && u != k) POff(u);
                int faces = 0, duds = 0;
                for (int ty = 1; ty < m.Map.H - 1; ty++)
                    for (int tx = 1; tx < m.Map.W - 1; tx++)
                    {
                        if (m.Map.Tile(tx, ty) != Arena.Wall || !m.Map.Walkable(tx + 1, ty)) continue;
                        float wx = tx + 0.5f, wy = ty + 0.5f, kx = tx + 1 + k.R + 0.02f, bx = kx + 2.5f;
                        if (!m.Map.Free(kx, wy, k.R) || !m.Map.Free(bx, wy, b.R) || !m.Map.LosClear(bx, wy, kx, wy)) continue;
                        faces++;
                        PPlace(b, bx, wy); PPlace(k, kx, wy); m.Projs.Clear(); b.Charge = b.HDef.MaxCharge;
                        m.TryCastSpecial(b, SimMath.PI, (bx - wx) / BramS.Reach);
                        for (int i = 0; i * Rules.Tick < BramS.DropDelay + 0.05f; i++) m.UpdateProjectiles(Rules.Tick);
                        if (k.Hp >= k.MaxHp) duds++;
                    }
                Verdict("BRAM03_anvil_on_wall_lands_at_its_face", "Bram", faces > 0 && duds == 0, "P2", $"{duds}/{faces} anvils aimed into a wall hit nobody standing at its face (want 0)");
            }

            // BRAM-06: the echo's hit on an enemy right behind Fenn's decoy lands at first contact, not a tick late
            {
                var m = PBramKnutScene(HeroId.Bram); var b = m.Player; var a = m.PFoe(HeroId.Knut); var c = m.PFoe(HeroId.Tikta); var f = m.PFoe(HeroId.Fenn);
                float x = 7.5f, y = 20, worst = 0; int n = 0;
                PPlace(f, x + 1, y + 3); f.CloneReadyAt = 0; m.CloneOnHurt(f, b, DmgKind.Auto, DmgFlags.None); m.FlushClones();
                Unit clone = null; foreach (var u in m.Units) if (u.IsClone && u.Alive) clone = u;
                for (float cpos = 0.4f; clone != null && cpos < 1.3f; cpos += 0.01f)
                {
                    PPlace(b, x, y); PPlace(a, x + 2.25f, y); PPlace(c, x + cpos, y); PPlace(f, x + 1, y + 3);
                    clone.X = clone.PX = x + 1.45f; clone.Y = clone.PY = y; clone.Alive = true; clone.Hp = clone.MaxHp;
                    m.Projs.Clear(); b.PassHits = BramS.EchoEvery - 1; m.BramBacklash(b, a);
                    Proj e = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Echo && !p.Done) e = p;
                    float ideal = 2.25f - cpos - c.R - BramS.EchoHitbox;
                    for (int i = 0; e != null && i < 40 && !e.Done; i++) m.UpdateProjectiles(Rules.Tick);
                    if (e != null && c.Hp < c.MaxHp) { n++; worst = Math.Max(worst, e.D - ideal); }
                }
                Verdict("BRAM06_echo_through_decoy_hits_at_contact", "Bram", n > 0 && worst <= 0.01f, "P3", $"{n} layouts hit, worst {F(worst)} tiles past first contact (want <= 0.01; a tick is {F(BramS.EchoSpeed * Rules.Tick)})");
            }

            // BRAM-05 (Rickard 2026-10-29 "Nej, bara synliga"): the tap hammer never bounces onto an enemy hero hidden in the grass
            {
                var m = PBramKnutScene(HeroId.Bram); var b = m.Player; var a = m.PFoe(HeroId.Knut); var hid = m.PFoe(HeroId.Fenn);
                foreach (var u in m.Heroes) if (u != b && u != a && u != hid) POff(u);
                int tried = 0, bounced = 0;
                for (int ty = 1; ty < m.Map.H - 1; ty++)
                    for (int tx = 1; tx < m.Map.W - 1; tx++)
                    {
                        if (m.Map.Tile(tx, ty) != Arena.Grass) continue;
                        float hx = tx + 0.5f, hy = ty + 0.5f;
                        foreach (int dir in new[] { 1, -1 })
                        {
                            float ax = hx - dir * 1.5f, bx = hx - dir * 4.2f;
                            if (!m.Map.Free(ax, hy, a.R) || !m.Map.Free(bx, hy, b.R) || m.Map.InGrass(ax, hy) || !m.Map.LosClear(bx, hy, hx, hy)) continue;
                            PPlace(b, bx, hy); PPlace(a, ax, hy); PPlace(hid, hx, hy); hid.RevealT = a.RevealT = b.RevealT = 0;
                            if (m.IsVisibleTo(hid, b.Team)) continue;
                            tried++; m.Projs.Clear();
                            var p = m.NewProj(ProjMode.Hammer, b, b.X, b.Y); p.Kind = ProjKind.BramHammerSpecial; p.Tgt = a; p.Speed = BramS.Speed; p.Dmg = 10; p.Radius = BramS.LineHitbox;
                            for (int i = 0; i < 90 && !p.Done; i++) m.UpdateProjectiles(Rules.Tick);
                            if (hid.Hp < hid.MaxHp) bounced++;
                        }
                    }
                Verdict("BRAM05_hammer_never_bounces_onto_hidden_hero", "Bram", tried > 0 && bounced == 0, "P2", $"{bounced}/{tried} layouts bounced onto a grass-hidden hero (want 0)");
            }

            // KNUT-01: a catch at full range: Knut is still in Casting (no walking) on every tick of the pull
            {
                var m = PBramKnutScene(HeroId.Knut); var kn = m.Player; var t = m.PFoe(HeroId.Tikta);
                foreach (var u in m.Heroes) if (u != kn && u != t) POff(u);
                PPlace(kn, 1, 11.5f); PPlace(t, 1 + 7.12f, 11.5f); kn.Charge = kn.HDef.MaxCharge;
                float free = 0; bool caught = false;
                m.PRun(90, PDrag(0, 1), PlayerInput.None, null, (mm, k) =>
                {
                    Proj h = null; foreach (var p in mm.Projs) if (p.Mode == ProjMode.Hook && !p.Done) h = p;
                    if (h != null && h.Caught != null) { caught = true; if (!kn.Has(St.Casting)) free += Rules.Tick; }
                });
                Verdict("KNUT01_knut_stands_until_the_catch_is_in", "Knut", caught && free < 1e-4f, "P3", $"caught={caught}, ticks Knut could walk with the catch on the chain: {F(free)} s (want 0)");
            }

            // KNUT-02: the anchor's out flight never crosses a wall between two ticks, from every free half tile in 72 directions
            {
                var m = PBramKnutScene(HeroId.Knut); var kn = m.Player;
                foreach (var u in m.Heroes) if (u != kn) POff(u);
                int casts = 0, slips = 0;
                for (float y = 2.5f; y < m.Map.H - 2; y += 0.5f)
                    for (float x = 1.5f; x < m.Map.W - 1; x += 0.5f)
                    {
                        if (!m.Map.Free(x, y, kn.R)) continue;
                        for (int a = 0; a < 72; a++)
                        {
                            PPlace(kn, x, y); m.Projs.Clear(); casts++;
                            var p = m.NewProj(ProjMode.Hook, kn, x, y);
                            p.Kind = ProjKind.KnutAnchor; p.Ang = a / 72f * SimMath.PI * 2; p.Out = true; p.Dmg = 1; p.Radius = KnutS.HookRadius;
                            float px = x, py = y;
                            for (int i = 0; i < 120 && p.Out && !p.Done; i++)
                            {
                                p.T += Rules.Tick; m.HookUpdate(p, Rules.Tick);
                                if (!m.Map.LineClear(px, py, p.X, p.Y, 0) || m.Map.WallAt(p.X, p.Y)) { slips++; break; }
                                px = p.X; py = p.Y;
                            }
                        }
                    }
                Verdict("KNUT02_anchor_never_crosses_a_wall", "Knut", slips == 0, "P3", $"{slips}/{casts} out flights crossed or entered a wall (want 0)");
            }
        }
    }
}
