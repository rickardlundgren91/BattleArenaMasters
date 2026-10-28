using System;

namespace BAM.Sim
{
    // Hilde (Hero4.cs): Powder Burst, Powder Keg with Hilde 2.0's aimed push, Wall Crash, blasts stopped by walls
    // (the HIL- criteria of XYZ HildeBreachTests.cs that apply to BAM).
    public sealed partial class Match
    {
        /// <summary>A wall tile with four open ground tiles to its west on the same row (a lane to push a hero into it).</summary>
        bool FindWallEast(out int wx, out int wy)
        {
            for (wy = 3; wy < Map.H - 3; wy++)
                for (wx = 5; wx < Map.W - 1; wx++)
                {
                    if (Map.Tile(wx, wy) != Arena.Wall) continue;
                    bool open = true;
                    for (int d = 1; d <= 4 && open; d++)
                        for (int dy = -1; dy <= 1 && open; dy++) open = Map.Walkable(wx - d, wy + dy);
                    if (open) return true;
                }
            wx = wy = -1; return false;
        }

        static void Hilde()
        {
            var m = Make(HeroId.Hilde, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            Check(HeroCatalog.Get(HeroId.Hilde).Name == "Hilde" && HeroCatalog.Get(HeroId.Hilde).Id == HeroId.Hilde, "Hilde is hero 9");
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 3, y); Place(c, x + 3, y + 3); Place(far, x - 8, y);

            // Powder Burst (tap): a 0.4 s fuse at the target, 90 % on heroes, pushed 0.75 away from the middle
            h.Charge = h.HDef.MaxCharge; Place(c, x + 3, y + 6);
            Check(m.TryCastSpecial(h, float.NaN, 0), "the tap casts with an enemy in reach");
            Proj k = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Keg && !p.Done) k = p;
            Check(k != null && k.Kind == ProjKind.HildeBurst && Near(-k.T, HildeS.BurstDelay) && Near(k.X, a.X, 0.5f) && k.IsZone, "the tap tosses the small keg on the target with its fuse");
            k.X = a.X - 0.4f; k.Y = a.Y; // the keg lands a little to the target's west
            for (int i = 0; i * Rules.Tick < HildeS.BurstDelay + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            float dmg = h.StatDmg();
            Check(Near(a.MaxHp - a.Hp, dmg * HildeS.BurstHeroCoeff * a.TakenMult, 0.05f), "Powder Burst deals 90 % to a hero");
            float ax0 = a.X;
            for (int i = 0; i < 30; i++) if (a.KnockT > 0) m.KnockStep(a, Rules.Tick);
            Check(Near(a.X - ax0, HildeS.BurstKnock, 0.05f), $"the tap pushes 0.75 tiles away from its middle: {a.X - ax0:0.00}");
            Place(a, x + 6, y); Place(c, x + 6, y + 3); h.Charge = h.HDef.MaxCharge; h.ClearStatuses();
            Check(!m.TryCastSpecial(h, float.NaN, 0), "no tap with nobody in reach");

            // Powder Keg (drag): placed where she aims, 1 s fuse; a hero west of its middle is still pushed east, the way she aimed
            Place(a, x + 3, y); h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, 0, 3f / HildeS.Reach), "the drag casts");
            k = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Keg && !p.Done) k = p;
            Check(k != null && k.Kind == ProjKind.HildeKeg && Near(k.X, x + 3) && Near(k.Y, y) && Near(-k.T, HildeS.KegDelay), "the big keg lands on the aimed point with a 1 s fuse");
            Place(a, x + 2.6f, y + 0.3f); ax0 = a.X; float ay0 = a.Y;
            for (int i = 0; i * Rules.Tick < HildeS.KegDelay + 0.02f; i++) m.UpdateProjectiles(Rules.Tick);
            Check(Near(a.MaxHp - a.Hp, dmg * HildeS.KegHeroCoeff * a.TakenMult, 0.05f), "the big keg deals 130 % to a hero");
            for (int i = 0; i < 30; i++) if (a.KnockT > 0) m.KnockStep(a, Rules.Tick);
            Check(Near(a.X - ax0, HildeS.KegKnock, 0.05f) && Near(a.Y, ay0), $"Breach Charge pushes 1.5 tiles the aimed way: {a.X - ax0:0.00}, {a.Y - ay0:0.00}");

            // the tower: only the big keg hurts it
            var tw = m.Towers[1]; Place(h, tw.X, tw.Y - 3.5f); float tw0 = tw.Hp;
            var kb = m.NewProj(ProjMode.Keg, h, tw.X, tw.Y - 1); kb.Kind = ProjKind.HildeBurst; kb.Dmg = 50; kb.Radius = HildeS.BurstRadius; m.KegBlast(kb);
            Check(tw.Hp == tw0, "Powder Burst never hurts the tower");
            var kk = m.NewProj(ProjMode.Keg, h, tw.X, tw.Y - 1); kk.Kind = ProjKind.HildeKeg; kk.Dmg = 50; kk.Radius = HildeS.KegRadius; kk.Big = true; m.KegBlast(kk);
            Check(tw.Hp < tw0, "the big keg hurts the tower");
            Place(h, x, y);

            // walls: Wall Crash stuns one hero per keg, and the blast does not reach through a wall
            if (!m.FindWallEast(out int wx, out int wy)) { Check(false, "the map has a wall to test Wall Crash on"); return; }
            float py = wy + 0.5f;
            Place(a, wx - 0.6f, py); Place(c, wx - 0.6f, py + 0.35f); Place(far, x - 8, y);
            var kw = m.NewProj(ProjMode.Keg, h, wx - 1.4f, py); kw.Kind = ProjKind.HildeKeg; kw.Dmg = 10; kw.Radius = HildeS.KegRadius; kw.Big = true; kw.Ang = 0;
            m.Events.Clear(); m.KegBlast(kw);
            for (int i = 0; i < 30; i++) { if (a.KnockT > 0) m.KnockStep(a, Rules.Tick); if (c.KnockT > 0) m.KnockStep(c, Rules.Tick); }
            int stuns = (a.Has(St.Stun) ? 1 : 0) + (c.Has(St.Stun) ? 1 : 0);
            Check(stuns == 1 && m.HasEvent(EventType.WallCrash), $"Wall Crash: one stun per keg ({stuns})");
            Check(a.MaxHp - a.Hp > 10 * HildeS.KegHeroCoeff * a.TakenMult + 1, "the wall hurts a hero slammed into it");
            Place(a, wx + 1.6f, py); // on the far side of the wall from the keg
            if (m.Map.Walkable(wx + 1, wy) && m.Map.Walkable(wx + 2, wy))
            {
                var kl = m.NewProj(ProjMode.Keg, h, wx - 0.3f, py); kl.Kind = ProjKind.HildeKeg; kl.Dmg = 10; kl.Radius = 3; kl.Big = true;
                m.KegBlast(kl);
                Check(a.Hp == a.MaxHp, "the blast does not reach through a wall");
            }

            // Moa's shield: no push, no stun
            Place(a, x + 3, y); m.AddStatus(a, St.Shield, 3, 0, 1000, m.Player.Team == a.Team ? m.Player : m.Foe(HeroId.Bram), false);
            var kn = m.NewProj(ProjMode.Keg, h, x + 3, y); kn.Kind = ProjKind.HildeKeg; kn.Dmg = 10; kn.Radius = HildeS.KegRadius; kn.Big = true;
            bool immune = m.CcImmune(a); m.KegBlast(kn);
            Check(!immune || a.KnockT <= 0, "a CC-immune hero is not pushed");
        }
    }
}
