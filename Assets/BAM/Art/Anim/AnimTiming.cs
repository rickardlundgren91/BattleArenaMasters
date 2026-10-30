using System;

namespace BAM.Art
{
    /// <summary>
    /// Animation Foundation 1.0: the timing maths of a hero's swing and cast, kept free of UnityEngine so it runs and is tested
    /// headless (Tests/Anim). BodyView plays its clips by these plans; the rules never read them. Every number here is the one
    /// BodyView used before (AnimFix.BlowTiming, 2026-10-28), moved here unchanged, with why it is what it is.
    ///
    /// The one promise: the clip reaches its measured blow exactly <c>windup</c> seconds into the swing, which is when the rules
    /// let the shot go (Combat.AutoAttack: the shot waits <c>-SpawnDelay</c> and leaves on the first tick it reaches 0).
    /// </summary>
    public static class AnimTiming
    {
        /// <summary>The rules' fixed step (BAM.Sim.Rules.Tick), repeated here so this file needs nothing else.</summary>
        public const float Tick = 1f / 60f;
        /// <summary>The recovery's slowest pace: at 1.0 the Mixamo recoveries looked sluggish next to the 1.8x windup.</summary>
        public const float RecoverRate = 1.2f;
        /// <summary>The recovery's fastest pace: above 2.5 a heavy slam reads as fast-forward (Rickard 2026-10-27 on Knut).</summary>
        public const float RecoverMax = 2.5f;
        /// <summary>The special's clip pace before and after its peak when there is no wind-up to stretch it over.</summary>
        public const float CastRate = 1.15f;
        /// <summary>How long a cut swing or cast holds where it stopped while it fades out (Interrupt).</summary>
        public const float CutFade = 0.15f;

        /// <summary>One swing's plan: which stretch of the attack clip plays and at what pace. Valid is false when the clip has
        /// too little before its blow; BodyView then plays the clip at its own pace (the pre-2026-10-28 way).</summary>
        public struct AttackPlan
        {
            public bool Valid;
            /// <summary>Clip seconds: where the windup starts, the blow, where the recovery stops.</summary>
            public float From, Blow, To;
            /// <summary>Clip seconds per swing second before the blow (Speed) and after it (RecRate).</summary>
            public float Speed, RecRate;
            /// <summary>Swing seconds: the whole swing (windup + recovery) and its fade-out at the end.</summary>
            public float Total, Fade;
        }

        /// <summary>
        /// The swing for a clip of <paramref name="len"/> s with its blow measured at <paramref name="blowMeasured"/> (-1 = not
        /// measured: <paramref name="releaseFrac"/> of the clip) and its body back up at <paramref name="settle"/> (-1 = unknown).
        ///
        /// Windup (t in [0, w]):  clip = From + t * Speed,  From = Blow - Pre,  Speed = Pre / w,
        ///   Pre = min(Blow - Start, clamp(1.8 w, 0.25, 0.45)): about 1.8x the clip's own pace (a Mixamo swing's last 0.25-0.45 s
        ///   before its blow is the readable part; the whole clip at the old 4x cap read as a twitch).
        /// Recovery (t > w):      clip = min(To, Blow + (t - w) * RecRate),
        ///   Budget = max(0.3, gap - w - 0.1): the time to the next windup at this fight's pace, less a 0.1 s margin;
        ///   RecRate = clamp((Settle - Blow) / Budget, 1.2, 2.5); To = min(Settle, Blow + 1.6, Blow + Budget * RecRate, len).
        /// Total = w + max(0.35, (To - Blow) / RecRate): at least 0.35 s after the blow so the fade never starts on it.
        /// Hence clip(w) = Blow exactly, for any w > 0: the blow is on the rules' release whatever the windup.
        /// </summary>
        public static AttackPlan PlanAttack(float windup, float len, float startFrac, float releaseFrac, float blowMeasured,
            float settle, float drawAt, bool bow, float gap)
        {
            var p = new AttackPlan();
            float start0 = startFrac * len;
            float blow = blowMeasured > 0f ? blowMeasured : releaseFrac * len;
            if (bow && drawAt > 0f) blow = drawAt; // Tyra's arrow leaves at full draw, not at her hand's fastest moment
            blow = Clamp(blow, Math.Min(start0 + 0.05f, len), len);
            float pre = Math.Min(blow - start0, Clamp(windup * 1.8f, 0.25f, 0.45f));
            if (pre < 0.02f) return p;
            p.From = blow - pre;
            p.Blow = blow;
            // played on until the body is back up (the clip's Settle), at most 1.6 s of clip after the blow; 0.5 s without a measure
            float to = Math.Min(len, Math.Min(settle > blow ? settle : blow + 0.5f, blow + 1.6f));
            float budget = Math.Max(0.3f, gap - windup - 0.1f);
            p.RecRate = Clamp((to - blow) / budget, RecoverRate, RecoverMax);
            p.To = Math.Min(to, blow + budget * p.RecRate);
            p.Speed = pre / windup;
            float play = (p.To - blow) / p.RecRate, rec = Math.Max(0.35f, play);
            p.Total = windup + rec;
            // a clip that ends on its blow holds it and lowers in the time left (fade 40 % of the recovery, 0.12-0.35 s)
            p.Fade = Clamp(Math.Max(rec * 0.4f, rec - play), 0.12f, 0.35f);
            if (bow) p.Fade = Math.Max(0.12f, rec - play - 0.06f);
            p.Valid = true;
            return p;
        }

        /// <summary>Clip seconds <paramref name="t"/> swing seconds into a planned swing of <paramref name="windup"/> s. Never goes
        /// back, and passes the blow once, at t = windup (no second release in the picture).</summary>
        public static float AttackClipTime(in AttackPlan p, float windup, float t)
            => t < windup ? p.From + t * p.Speed : Math.Min(p.To, p.Blow + (t - windup) * p.RecRate);

        /// <summary>One special's plan: the stretch of the cast clip round its peak, the peak landing <c>lead</c> s in.</summary>
        public struct CastPlan
        {
            public bool Valid;
            /// <summary>Clip seconds: start, peak, end; clip seconds per cast second before the peak (Pre).</summary>
            public float From, Peak, To, Pre;
            /// <summary>Cast seconds: when the peak shows, the whole cast, its fade-out.</summary>
            public float BlowAt, Len, Fade;
        }

        /// <summary>
        /// The cast for a clip of <paramref name="len"/> s, its peak measured at <paramref name="blowMeasured"/> (-1: 40 % in),
        /// for a special whose effect comes <paramref name="lead"/> s after the cast (the rules' wind-up: Knut's slam, Tyra's
        /// arrows, Bonk's boulder; 0.12 s for one that acts at once).
        ///   From = max(0, Peak - 1.15 lead), Pre = (Peak - From) / lead, BlowAt = (Peak - From) / Pre = lead (when Peak > 0),
        ///   after the peak at 1.15x to min(Settle, Peak + 1.2) (Peak + 0.6 without a measure), at least 0.35 s after the peak.
        /// </summary>
        public static CastPlan PlanCast(float len, float blowMeasured, float settle, float drawAt, bool bow, float lead)
        {
            var c = new CastPlan();
            float blow = blowMeasured > 0f ? blowMeasured : 0.4f * len;
            if (bow && drawAt > 0f) blow = drawAt;
            c.From = Math.Max(0f, blow - lead * CastRate);
            c.To = Math.Min(len, settle > blow ? Math.Min(settle, blow + 1.2f) : blow + 0.6f);
            if (c.To - c.From < 0.1f) return c;
            c.Peak = blow;
            c.Pre = lead > 0.01f && blow > c.From ? (blow - c.From) / lead : CastRate;
            c.BlowAt = (blow - c.From) / c.Pre;
            float play = c.BlowAt + Math.Max(0f, c.To - blow) / CastRate;
            c.Len = Math.Max(play, c.BlowAt + 0.35f);
            c.Fade = Clamp(Math.Max((c.Len - c.BlowAt) * 0.4f, c.Len - play), 0.12f, 0.35f);
            c.Valid = true;
            return c;
        }

        /// <summary>Clip seconds <paramref name="t"/> cast seconds into a planned cast.</summary>
        public static float CastClipTime(in CastPlan c, float t)
            => t < c.BlowAt ? c.From + t * c.Pre : Math.Min(c.To, c.Peak + (t - c.BlowAt) * CastRate);

        /// <summary>
        /// The tick (counted from the AttackStart tick, which is 0) on which the rules let a shot with this windup go: the shot's
        /// clock starts at -windup and gains one Tick per Step, the AttackStart step included, and leaves when it is no longer
        /// below 0 (Combat.UpdateProjectiles). Float sums as the rules make them, so 0.2 s is 12 or 13 ticks as the rules have it.
        /// </summary>
        public static int ReleaseTick(float windup)
        {
            float t = -windup;
            int k = 0;
            do { t += Tick; } while (t < 0f && ++k < 100000);
            return k;
        }

        /// <summary>The view's clock for a swing read from the rules' clock instead of summed frame times (the proposed
        /// sim-clock sync, not wired: it needs the game to hand the tick count to Art): seconds since the AttackStart tick plus
        /// GameFlow's interpolation share <paramref name="alpha"/> of the next tick. It reaches ReleaseTick(windup) * Tick on
        /// the very frame the release tick steps, the frame the shot is first drawn, at any frame rate and online.</summary>
        public static float SimElapsed(long startTick, long ticksNow, float alpha)
            => (ticksNow - startTick + Math.Min(Math.Max(alpha, 0f), MaxAlpha)) * Tick;

        /// <summary>alpha is a share of the next tick, so it stays below 1: at 1 float rounding could show the blow a frame before
        /// its tick has stepped.</summary>
        const float MaxAlpha = 0.999f;

        /// <summary>The blend weight a value eases toward its goal with, MoveTowards at <paramref name="rate"/> per second.</summary>
        public static float Approach(float from, float to, float rate, float dt)
        {
            float d = to - from, s = rate * dt;
            return Math.Abs(d) <= s ? to : from + Math.Sign(d) * s;
        }

        /// <summary>Smoothstep on [0, 1]: the eased blend in and out of swings (AnimFix.Life).</summary>
        public static float Smooth(float t) { t = Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

        /// <summary>A swing or cast that stays on longer than its plan (a NaN or an infinite total, which the plans never make) is
        /// a stuck state; the view ends it after this much more (AnimProbe watchdog).</summary>
        public const float LockGrace = 1f;

        public static bool Stuck(float t, float total) => !(total < 1e6f) || t > total + LockGrace;

        // Mathf.Clamp's exact order (min first), so the numbers match BodyView's old ones bit for bit
        static float Clamp(float v, float min, float max) { if (v < min) v = min; else if (v > max) v = max; return v; }
    }
}
