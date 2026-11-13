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
    }
}
