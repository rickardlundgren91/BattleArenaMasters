using System;

namespace BAM.Sim
{
    // Fenn's Shadow Veil (Heroes.cs FennS.Veil, ABC2's Omega, Rickard 2026-11-11 "fenn blir omega"): decoy + invisible + faster,
    // the first attack out of it is the Shadow Strike (harder, slows).
    public sealed partial class Match
    {
        static void FennVeilChecks()
        {
            var m = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            if (!FennS.Veil) return;
            var f = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            foreach (var u in m.Heroes) if (u.Team == f.Team && u != f) Place(u, x - 8, y + 6);
            Place(f, x, y); Place(a, x + 9, y); Place(c, x + 9, y + 1); Place(far, x - 8, y);
            int foe = Teams.Other(f.Team);

            // the tap works with nobody in reach (an escape too) and needs no aim
            f.Charge = f.HDef.MaxCharge;
            Check(m.TapHasTarget(f), "VEIL: the button is lit with no enemy in reach");
            Check(!CanDragAim(HeroId.Fenn), "VEIL: nothing to drag-aim");
            Check(m.TryTapSpecial(f), "VEIL: the tap casts it");
            Check(f.Has(St.Veil) && f.Has(St.SpeedPct) && f.Charge == 0, "VEIL: he is veiled and faster, the charge is spent");
            Check(m.HasEvent(EventType.FennVeil), "VEIL: the FennVeil event");
            m.FlushClones();
            Check(m.Units.Exists(u => u.IsClone && u.Alive && u.Owner == f), "VEIL: his decoy appears");
            Check(!m.Units.Exists(u => u.Kind == UnitKind.Wolf && u.Alive), "VEIL: no wolf");

            // nobody on the other team sees him: not close, not after a hit, not in their tower's range; his own team does
            Place(a, x + 1, y); f.RevealT = 2;
            Check(!m.IsVisibleTo(f, foe) && m.IsVisibleTo(f, f.Team), "VEIL: unseen by the enemy next to him and after a hit, seen by his team");
            Check(!m.EnemiesInRange(a, 5, Pick.Any, false, new System.Collections.Generic.List<Unit>()).Contains(f), "VEIL: enemies cannot pick him");
            Check(m.HiddenFromFoes(f), "VEIL: the view draws him half seen for his own team (HiddenFromFoes)");

            // a veiled Fenn next to an enemy does not swing by himself (Rickard 2026-11-11: he could not escape); SPECIAL strikes
            m.Events.Clear(); f.AtkTimer = 99; var fBot = f.Bot; f.Bot = null; // as a player
            m.UpdateAttack(f, Rules.Tick);
            Check(f.Has(St.Veil) && !m.Projs.Exists(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done), "VEIL: no auto attack while veiled, even with an enemy in reach");
            Check(m.VeilStrikeHasTarget(f) && f.VeilStrikeReady, "VEIL: SPECIAL is lit for the strike with an enemy in reach");
            Place(a, x + 6, y); f.AddRaw(St.Veil, 1, 0, 0, f, false);
            Check(!m.VeilStrikeHasTarget(f) && !m.TryTapSpecial(f) && f.Has(St.Veil), "VEIL: nobody in reach: SPECIAL does nothing, still veiled");
            Place(a, x + 1, y); f.AddRaw(St.Veil, 1, 0, 0, f, false); f.AddRaw(St.SpeedPct, 1, FennS.VeilSpeed, 0, f, false);
            Check(m.TryTapSpecial(f), "VEIL: SPECIAL while veiled is the Shadow Strike");
            var ps = m.Projs.Find(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done);
            Check(ps != null && ps.Big && ps.Tgt == a && !f.Has(St.Veil), "VEIL: it strikes the enemy and ends the Veil");
            ps.Done = true; m.Projs.Remove(ps); f.Bot = fBot;
            f.AddRaw(St.Veil, 1, 0, 0, f, false); f.AddRaw(St.SpeedPct, 1, FennS.VeilSpeed, 0, f, false);

            // the Shadow Strike: the first auto attack ends the Veil, hits StrikeBonus harder and slows
            m.Events.Clear();
            m.AutoAttack(f, a);
            var p = m.Projs.Find(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done);
            float plain = f.StatDmg() * m.HeroAttackCoeff(f, a, ref dummyBig);
            Check(!f.Has(St.Veil) && m.IsVisibleTo(f, foe), "VEIL: attacking ends it, he is seen again");
            Check(!f.Has(St.SpeedPct), "VEIL: its speed goes with it");
            Check(m.HasEvent(EventType.VeilStrike) && !m.HasEvent(EventType.Ambush), "VEIL: a VeilStrike event, not an ambush");
            Check(p != null && p.Big && Near(p.Dmg, plain * (1 + FennS.StrikeBonus), 0.05f), $"VEIL: the strike deals {1 + FennS.StrikeBonus:0.00}x ({p?.Dmg:0.0} vs {plain:0.0})");
            p.Done = true; m.HitWith(p);
            Check(a.Has(St.SlowPct), "VEIL: the strike slows its target");
            m.AutoAttack(f, a);
            var p2 = m.Projs.Find(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done && q != p);
            Check(p2 != null && !p2.Big && p2.Ticks != FennS.StrikeTag, "VEIL: the next attack is a normal one");

            // left alone it runs out after VeilTime and he is seen again (VeilEnd)
            var m2 = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var f2 = m2.Player; Place(f2, x, y);
            foreach (var u in m2.Heroes) if (u != f2) Place(u, u.Team == f2.Team ? x - 8 : x + 9, y + (u.Team == f2.Team ? 6 : 0));
            f2.Charge = f2.HDef.MaxCharge; m2.TryTapSpecial(f2);
            bool ended = false; float t0 = m2.Time;
            for (int k = 0; k < (int)((FennS.VeilTime + 0.5f) / Rules.Tick) && !ended; k++) { m2.Step(PlayerInput.None); if (m2.HasEvent(EventType.VeilEnd)) ended = true; }
            Check(ended && !f2.Has(St.Veil) && Math.Abs(m2.Time - t0 - FennS.VeilTime) < 0.2f, $"VEIL: it runs out after {FennS.VeilTime} s ({m2.Time - t0:0.00})");
        }
        static bool dummyBig;
    }
}
