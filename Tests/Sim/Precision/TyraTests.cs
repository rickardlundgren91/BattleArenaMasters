using System;

namespace BAM.Sim
{
    // Tyra's shots (precision TYRA-01, TYRA-11): no arrow flies past its reach, and the bolt tests the wall before the enemies.
    public sealed partial class Match
    {
        static void PrecTyra()
        {
            const float Up = -(float)Math.PI / 2;
            // the volley: five arrows straight up the open lane past a minion-free target 2 tiles out; each ends on 4.0
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var foe = m.PFoe(HeroId.Bram);
                POff(m.PFoe(HeroId.Knut)); POff(m.PFoe(HeroId.Tikta)); POff(m.PAlly(HeroId.Moa)); POff(m.PAlly(HeroId.Fenn));
                PPlace(t, 7.5f, 30.5f); PDummy(foe); PPlace(foe, 7.5f, 28.5f); foe.Hp = foe.MaxHp = 1e6f;
                t.Charge = t.HDef.MaxCharge;
                // the arrows' own objects, read until each is done (a done shot leaves Projs in its last tick, and the pool reuses it)
                var mine = new System.Collections.Generic.List<Proj>(); var done = new System.Collections.Generic.HashSet<Proj>(); float maxD = 0;
                m.PRun(60, PTap(), PlayerInput.None, null, (mm, k) =>
                {
                    if (k == 0) foreach (var p in mm.Projs) if (p.Src == t && p.Mode == ProjMode.Volley) mine.Add(p);
                    foreach (var p in mine) if (!done.Contains(p)) { maxD = Math.Max(maxD, p.D); if (p.Done) done.Add(p); }
                });
                Verdict("Tyra_VolleyReach", "proj", maxD > 3.9f && maxD <= t.Range + 1e-4f, "P2",
                    $"volley arrows flew {F(maxD)} tiles, reach {F(t.Range)} (TYRA-01: was 4.2, a full 0.3-tile step past the ring)");
            }
            // the bolt: up the open lane past the old 9.0, on to the enemy tower, where it stops without hurting it (2026-10-30)
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player;
                foreach (var h in m.Heroes) if (h != t) POff(h);
                foreach (var u in m.Units) if (u.Kind == UnitKind.Minion) POff(u);
                PPlace(t, 7.5f, 31.5f); t.Charge = t.HDef.MaxCharge;
                var tower = m.Units.Find(u => u.Kind == UnitKind.Tower && u.Team != t.Team); tower.Alive = true; float towerHp = tower.Hp; // the harness turns towers off
                Proj bolt = null; bool over = false; float maxD = 0, endY = 0; int blocked = 0;
                m.PRun(150, PDrag(Up), PlayerInput.None, null, (mm, k) =>
                {
                    if (k == 0) foreach (var p in mm.Projs) if (p.Src == t && p.Mode == ProjMode.Bolt) bolt = p;
                    foreach (var e in mm.Events) if (e.Type == EventType.Blocked && e.A == t) { blocked++; endY = e.Y; }
                    if (bolt != null && !over) { maxD = Math.Max(maxD, bolt.D); over = bolt.Done; }
                });
                bool atTower = blocked == 1 && Math.Abs(endY - tower.Y) <= tower.R + TyraS.Hitbox + 0.05f;
                Verdict("Tyra_BoltReach", "proj", maxD > TyraS.Length + 0.5f && maxD <= TyraS.Flight + 1e-4f && atTower && tower.Hp == towerHp, "P2",
                    $"bolt flew {F(maxD)} tiles (old length {F(TyraS.Length)}), stopped {(atTower ? "at" : "NOT at")} the enemy tower (y {F(endY)}, tower {F(tower.Y)}), tower hp {F(towerHp)} -> {F(tower.Hp)}");
            }
            // the bolt into the wall column at x 3..4 (rows 24-31) with an enemy right behind it: blocked, no hit
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var foe = m.PFoe(HeroId.Bram);
                foreach (var h in m.Heroes) if (h != t && h != foe) POff(h);
                PPlace(t, 5.5f, 27.5f); PDummy(foe); PPlace(foe, 2.5f, 27.5f); t.Charge = t.HDef.MaxCharge;
                int hits = 0, blocked = 0;
                var ev = m.PRun(60, PDrag((float)Math.PI), PlayerInput.None);
                foreach (var e in ev) { if (e.Type == EventType.Hit && e.A == t && (ProjKind)e.Kind == ProjKind.TyraBolt) hits++; if (e.Type == EventType.Blocked && e.A == t) blocked++; }
                Verdict("Tyra_BoltWallFirst", "walls", hits == 0 && blocked == 1, "P0", $"bolt at an enemy behind a 1-tile wall: {hits} hits, {blocked} blocked (TYRA-11)");
            }
        }
    }
}
