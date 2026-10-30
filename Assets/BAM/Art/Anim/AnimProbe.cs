namespace BAM.Art
{
    /// <summary>
    /// Animation Foundation 1.0: what one hero view's swings actually did, measured in the running game for the debug overlay
    /// (AnimDebugOverlay) and the Unity measuring runs. Only numbers, written from BodyView's own calls; no allocations, nothing
    /// the rules or online read.
    ///
    /// The release error: the rules let a shot go on a tick; the game draws it from the shooter's hand that frame and asks the
    /// view where its hand is (UnitView.Muzzle). That call is the rules' release as the screen sees it, so the view compares it
    /// with its own blow: error = windup - (swing time at the end of this frame). Positive = the picture's blow comes that much
    /// after the shot left (late), negative = the blow was shown that much before it (early: a hit in the picture before the
    /// rules'). Within one frame (16.7 ms at 60 fps) is in sync; frame times are quantised, so nothing finer is measurable.
    /// </summary>
    sealed class AnimProbe
    {
        public int Swings, Blows, Releases, Doubles, Restarts, Locks;
        /// <summary>The last and the largest (by size, with its sign) release error, ms.</summary>
        public float LastErrMs, WorstErrMs;
        /// <summary>Bit per clip slot (BodyView's CIdle..CWalk) that had no clip and plays the idle instead.</summary>
        public int Missing;
        bool released, blown;

        public void Reset() { released = blown = false; }

        /// <summary>A new swing. One whose blow never showed was cut short by the next one (only an attack speed above the
        /// windup can do that).</summary>
        public void OnSwing(bool lastStillWinding)
        {
            if (lastStillWinding && !blown) Restarts++;
            Swings++;
            released = blown = false;
        }

        /// <summary>The swing's clip passed its blow (AttackClipTime(windup)).</summary>
        public void OnBlow() { if (!blown) { blown = true; Blows++; } }

        /// <summary>The rules let this swing's shot go (UnitView.Muzzle at the shot's first frame); t = the swing's time before
        /// this frame, dt = this frame's step.</summary>
        public void OnRelease(float windup, float t, float dt)
        {
            if (released) { Doubles++; return; }
            released = true;
            Releases++;
            LastErrMs = (windup - (t + dt)) * 1000f;
            if (LastErrMs * LastErrMs > WorstErrMs * WorstErrMs) WorstErrMs = LastErrMs;
        }
    }
}
