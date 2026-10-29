using System;

namespace BAM.Sim
{
    // Fenn 2.0 (Hero5.cs): Pack Hunt (the tapped wolf's first prey: the nearest enemy hero or minion, held) and Shadow Escape.
    public sealed partial class Match
    {
        static void Fenn2()
        {
            var m = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var f = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            foreach (var u in m.Heroes) if (u.Team == f.Team && u != f) Place(u, x - 8, y + 6);
            Place(f, x, y); Place(a, x + 2.5f, y); Place(c, x + 1.6f, y + 1.2f); Place(far, x - 8, y);

            // Pack Hunt: the nearest enemy from where the wolf appears is its prey at once, and it keeps it when another comes closer
            f.Charge = f.HDef.MaxCharge; f.Facing = 0;
            Check(m.TryCastSpecial(f, float.NaN, 0), "the wolf is called");
            Unit w = m.Units.Find(u => u.Kind == UnitKind.Wolf && u.Alive && u.Owner == f);
            float da = SimMath.Dist(a, w.X, w.Y), dc = SimMath.Dist(c, w.X, w.Y);
            Check(w.WolfPrey == (da < dc ? a : c), "the tapped wolf takes the nearest enemy as its prey at once");
            var first = w.WolfPrey; var other = first == a ? c : a;
            Place(other, w.X + 0.5f, w.Y);
            m.AiWolf(w, Rules.Tick);
            Check(w.WolfPrey == first, "it keeps its prey when another enemy comes closer");
            first.Alive = false; m.AiWolf(w, Rules.Tick);
            Check(w.WolfPrey == null, "a dead prey is let go; then the old rule picks");
            first.Alive = true;

            // no prey in sight: none
            Place(a, x + 7, y); Place(c, x + 7, y + 1); f.Charge = f.HDef.MaxCharge; f.ClearStatuses();
            m.TryCastSpecial(f, float.NaN, 0); w = m.Units.Find(u => u.Kind == UnitKind.Wolf && u.Alive && u.Owner == f);
            Check(w.WolfPrey == null, "no prey with nobody in the wolf's sight");

            // Shadow Escape: an enemy hero's auto attack on the decoy speeds Fenn up; a special does not
            Place(a, x + 2, y); f.ClearStatuses();
            f.ClonePending = true; m.FlushClones();
            var d = m.Units.Find(u => u.IsClone && u.Alive && u.Owner == f);
            m.DealDamage(a, d, 5, DmgKind.Special);
            Check(!f.Has(St.SpeedPct) && !d.Alive, "a special on the decoy gives no Shadow Escape");
            f.ClonePending = true; m.FlushClones();
            d = m.Units.Find(u => u.IsClone && u.Alive && u.Owner == f);
            m.DealDamage(a, d, 5, DmgKind.Auto);
            Check(f.Has(St.SpeedPct) && !d.Alive, "an enemy hero's auto attack on the decoy speeds Fenn up");
        }
    }
}
