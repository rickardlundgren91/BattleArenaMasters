using System;
namespace BAM.Sim
{
    public sealed partial class Match
    {
        /// <summary>The hero review of 2026-11-13 (bam-work/review/hjaltar-2026-11-13/, Rickard "ja" to Ragn's ring and text, then
        /// "fixa alla punkter"). Run with the live forms (Fenn's Veil, Bram's Hammer Line).</summary>
        public static void HeroReview1113()
        {
            RagnRingStays();
            FennStrikeChecks();
        }

        /// <summary>Ragn's red ring lies on his landing spot for the whole leap, not only for the 0.3 s warning before it.</summary>
        static void RagnRingStays()
        {
            var m = OneTapScene(HeroId.Ragn, out var p, out _, out _);
            m.Telegraph = true;
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 4f, p.Y); m.AddStatus(e, St.Stun, 999);
            int ringTicks = 0, leapTicks = 0; bool landed = false;
            for (int k = 0; k < 90 && !landed; k++)
            {
                m.Step(k == 0 ? TapIn() : PlayerInput.None);
                foreach (var ev in m.Events) if (ev.Type == EventType.RagnLand && ev.A == p) landed = true;
                if (!p.RagnLeaping) continue;
                leapTicks++;
                foreach (var q in m.Projs)
                    if (!q.Done && q.Src == p && q.Mode == ProjMode.WarnCircle && SimMath.Hypot(q.X - p.RagnToX, q.Y - p.RagnToY) < 0.05f) { ringTicks++; break; }
            }
            Check(landed && leapTicks > 0 && ringTicks == leapTicks, $"RAGN ring: the red ring lies where he lands for his whole leap ({ringTicks} of {leapTicks} ticks, landed {landed})");
            Check(HeroCatalog.Get(HeroId.Ragn).SpecialText.EndsWith(HeroInfo.DragLine), "RAGN text: the hold card says a drag aims the leap");
        }

        /// <summary>Fenn's Shadow Strike never goes into a tower (only the base in reach: SPECIAL stays grey and does nothing), and a
        /// drag picks whom it hits: the enemy nearest the aimed spot, while the tap still rolls among all enemies in reach.</summary>
        static void FennStrikeChecks()
        {
            var m = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var f = m.Player; f.Bot = null;
            foreach (var u in m.Units) if (u.Kind == UnitKind.Minion) u.Alive = false;
            Unit tower = m.Units.Find(u => u.Kind == UnitKind.Tower && u.Team != f.Team);
            foreach (var u in m.Heroes) if (u != f) { Place(u, u.Team == f.Team ? 3 : m.Map.W - 3, 8); m.AddStatus(u, St.Stun, 999); }
            // beside the enemy base, nobody else near
            Place(f, tower.X, tower.Y + tower.R + f.R + 0.6f);
            f.AddRaw(St.Veil, 2, 0, 0, f, false);
            Check(m.EnemiesInRange(f, f.Range, Pick.Any, true, new System.Collections.Generic.List<Unit>()).Contains(tower), "FENN tower: the enemy base is in his reach for this check");
            Check(!m.VeilStrikeHasTarget(f) && !m.TryTapSpecial(f) && f.Has(St.Veil), "FENN tower: with only the tower in reach SPECIAL is grey and the strike does not go into it");
            Check(!m.TryAimSpecial(f, 0, 1) && f.Has(St.Veil), "FENN tower: a dragged strike does not go into it either");

            // two enemies in reach on either side: a drag toward one strikes that one
            var a = m.Foe(HeroId.Bram); var b = m.Foe(HeroId.Knut);
            float x = 7.5f, y = 20;
            Place(f, x, y); Place(a, x + 2f, y); Place(b, x - 2f, y); m.AddStatus(a, St.Stun, 999); m.AddStatus(b, St.Stun, 999);
            foreach (var side in new[] { 0, 1 })
            {
                f.AddRaw(St.Veil, 2, 0, 0, f, false);
                var want = side == 0 ? a : b; float ang = side == 0 ? 0 : SimMath.PI;
                var aim = m.AimFor(f, ang, DragShare(HeroId.Fenn, 0.7f));
                var marks = m.AimTargets(f, aim, new System.Collections.Generic.List<Unit>());
                Check(marks.Count == 1 && marks[0] == want, $"FENN drag: the preview marks the enemy nearest the aimed spot ({(side == 0 ? "east" : "west")})");
                Check(m.TryAimSpecial(f, ang, 0.7f), "FENN drag: a drag while veiled strikes");
                var ps = m.Projs.Find(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done);
                Check(ps != null && ps.Big && ps.Tgt == want && !f.Has(St.Veil), $"FENN drag: it strikes the enemy it was dragged toward ({(side == 0 ? "east" : "west")})");
                if (ps != null) { ps.Done = true; m.Projs.Remove(ps); }
            }
            Check(m.DragAimNow(f) == false && !CanDragAim(HeroId.Fenn), "FENN drag: unveiled, nothing to drag (the Veil is cast where he stands)");
            f.AddRaw(St.Veil, 2, 0, 0, f, false);
            Check(m.DragAimNow(f), "FENN drag: veiled, SPECIAL drags for the strike");
            Check(HeroCatalog.Get(HeroId.Fenn).SpecialText.Contains("drag to pick"), "FENN text: the hold card says a drag picks whom he strikes");
        }
    }
}
