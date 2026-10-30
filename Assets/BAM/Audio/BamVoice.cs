using System.Collections.Generic;
using BAM.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BAM.Audio
{
    /// <summary>
    /// Spoken lines (Rickard 2026-10-30: "röst till tutorial"): one recorded clip per line in Resources/BAM/Voice/&lt;key&gt;,
    /// on its own source so a line is never stolen by the effects. A new line cuts the one before it. A missing file is
    /// silent (logged once), so the game runs the same until Rickard drops his recordings in: zero code per file.
    /// The music and the world's sounds dip while a line is spoken.
    /// </summary>
    public static class BamVoice
    {
        const string Folder = "BAM/Voice/";
        // a voice sits a little over the effects at the same Sfx volume
        const float Level = 1f;

        static AudioSource src;
        static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>Speaks the line in Resources/BAM/Voice/key (any audio type Unity imports); nothing if there is no file.</summary>
        public static void Say(string key)
        {
            if (!BamAudio.Ready || string.IsNullOrEmpty(key) || missing.Contains(key)) return;
            var clip = Resources.Load<AudioClip>(Folder + key);
            if (clip == null) { missing.Add(key); Debug.Log("[BAM.Audio] no voice file at Resources/" + Folder + key + ", the line is silent"); return; }
            if (src == null)
            {
                var host = new GameObject("BAM.Voice");
                Object.DontDestroyOnLoad(host);
                src = host.AddComponent<AudioSource>();
                src.playOnAwake = false; src.loop = false; src.spatialBlend = 0f; src.dopplerLevel = 0f; src.priority = 0;
            }
            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Min(1f, Level * BamSettings.SfxVolume);
            if (src.volume <= 0.005f) return;
            src.Play();
            BamAudio.Duck(clip.length);
        }

        /// <summary>Cuts the line being spoken (Skip, pause, leaving the match).</summary>
        public static void Stop() { if (src != null) src.Stop(); }
    }
}
