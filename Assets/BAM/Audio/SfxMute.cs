namespace BAM.Audio
{
    /// <summary>
    /// Which code-made (synth) sounds are silenced. Rickard 2026-10-26: "galet många visslor och läten, bort med dom",
    /// so all of them are off until real recorded clips arrive from the paid AI tool. Music is not affected.
    /// To bring one back, add it to Unmuted. A recorded clip in Resources/BAM/Sfx/&lt;SfxId name&gt; always plays.
    /// </summary>
    public static class SfxMute
    {
        /// <summary>false = no synth sound is muted (the old behaviour).</summary>
        public static readonly bool MuteSynth = true;

        /// <summary>Synth sounds that still play while MuteSynth is on. Empty: total silence apart from music.</summary>
        static readonly SfxId[] Unmuted =
        {
            // e.g. SfxId.UiTap,
        };

        public static bool IsMuted(SfxId id)
        {
            if (!MuteSynth) return false;
            for (int i = 0; i < Unmuted.Length; i++) if (Unmuted[i] == id) return false;
            return true;
        }
    }
}
