using System;

namespace BAM.Sim
{
    // Fenn 2.0 (Hero5.cs): Shadow Escape. (Pack Hunt went with his old wolf, full review 2026-11-13 nr 116.)
    public sealed partial class Match
    {
        static void Fenn2()
        {
            var m = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var f = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            foreach (var u in m.Heroes) if (u.Team == f.Team && u != f) Place(u, x - 8, y + 6);
            Place(f, x, y); Place(a, x + 2.5f, y); Place(c, x + 1.6f, y + 1.2f); Place(far, x - 8, y);

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
