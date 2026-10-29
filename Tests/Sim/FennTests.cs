using System;

namespace BAM.Sim
{
    // Fenn's thrown wolf (FENN-01/02, Precision & Polish 2026-10-29): it turns only onto a hero inside the red strip it shows,
    // never onto one behind Fenn, and it never lands more than 6 tiles from where it left, even when its prey dashes away.
    public sealed partial class Match
    {
        // a drag throw along +x from where Fenn stands; returns the landing and calls each(wolf) every tick of the flight
        static SimEvent? FennThrow(Match m, Unit f, float k, Action<Unit> each, out float from)
        {
            float x0 = f.X, y0 = f.Y; f.Charge = f.HDef.MaxCharge; f.Facing = 0; from = -1;
            if (!m.TryCastSpecial(f, 0, k)) return null;
            Unit w = null; foreach (var u in m.Units) if (u.Kind == UnitKind.Wolf && u.Alive && u.Owner == f) w = u;
            for (int i = 0; i < 60; i++)
            {
                each?.Invoke(w);
                m.Step(PlayerInput.None);
                foreach (var e in m.Events) if (e.Type == EventType.WolfPounce) { from = SimMath.Hypot(e.X - x0, e.Y - y0); return e; }
            }
            return null;
        }

        static void FennWolf()
        {
            Match Scene(out Unit f, out Unit e)
            {
                var mm = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                f = mm.Player; e = mm.Foe(HeroId.Bram);
                foreach (var u in mm.Heroes) { u.Bot = null; if (u != f && u != e) Place(u, 0.5f, 0.5f); }
                Place(f, 2, 13); // an open row of the arena, out of both towers' reach
                return mm;
            }
            // the throw lands where the aim showed it (open ground)
            foreach (float k in new[] { 0f, 0.5f, 1f })
            {
                var m = Scene(out var f, out var e); Place(e, 0.5f, 34);
                float want = m.WolfReach(2, 13, 0, Math.Min(FennS.ThrowReach, Math.Max(1.5f, k * FennS.ThrowReach)));
                FennThrow(m, f, k, null, out float d);
                Check(Near(d, want, 0.001f), $"the thrown wolf lands on the aim preview (k {k})");
            }
            // FENN-01: a hero just behind Fenn is not bitten by a throw aimed forward
            foreach (float back in new[] { 0.6f, 0.9f, 1.2f })
            {
                var m = Scene(out var f, out var e); Place(e, 2 - back, 13);
                var ev = FennThrow(m, f, 1, null, out float d);
                Check(ev.HasValue && ev.Value.B != e && Near(d, FennS.ThrowReach, 0.001f), $"the wolf flies on past a hero {back} behind Fenn");
            }
            // it still turns onto a hero inside the strip
            {
                var m = Scene(out var f, out var e); Place(e, 5, 13.9f);
                var ev = FennThrow(m, f, 1, null, out float d);
                Check(ev.HasValue && ev.Value.B == e && e.Has(St.Root), "the wolf turns onto a hero inside the red strip and roots it");
            }
            // FENN-02: the prey dashes 3.5 tiles further once the wolf has locked on, or runs away: never past 6 tiles
            {
                var m = Scene(out var f, out var e); Place(e, 6.5f, 13.9f); bool dashed = false;
                FennThrow(m, f, 1, w => { if (!dashed && w.WolfPrey == e) { dashed = true; e.X = e.PX = e.X + 3.5f; } }, out float d);
                Check(dashed && d >= 0 && d <= FennS.ThrowReach + 0.001f, "a dash after lock-on never pulls the wolf past 6 tiles");
                var m2 = Scene(out var f2, out var e2); Place(e2, 7, 13.9f);
                FennThrow(m2, f2, 1, w => { if (w.WolfPrey == e2) { e2.X += 0.1f; e2.PX = e2.X; } }, out float d2);
                Check(d2 >= 0 && d2 <= FennS.ThrowReach + 0.001f, "a prey running away never pulls the wolf past 6 tiles");
            }
        }
    }
}
