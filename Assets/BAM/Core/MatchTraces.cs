using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BAM.Core
{
    /// <summary>
    /// The match traces (BAM.Sim.MatchTrace) waiting on this phone to be sent: one file each under persistentDataPath/traces,
    /// the newest MaxKept kept. The game saves one per match; BAM.Online sends them after the match reports and deletes each the
    /// server took. For the bots to learn from real players (Rickard 2026-11-09). Nothing here may ever break a match: every
    /// error is only logged.
    /// </summary>
    public static class MatchTraces
    {
        /// <summary>The online protocol number, set by BAM.Online at boot (0 = none); a trace replays only with its own rules.</summary>
        public static int Protocol;

        /// <summary>Send traces at all. On while testing; Rickard decides the privacy text and any switch in Settings.</summary>
        public static bool Share = true;

        /// <summary>Fills a new trace's player bands (MatchTrace.Rank, Played, HeroPlayed) from the player's totals. Set by
        /// BAM.Online at boot; null = left unknown. Called once as the match begins, before its result is counted.</summary>
        public static Action<BAM.Sim.MatchTrace> Describe;

        public const int MaxKept = 10;
        /// <summary>A trace bigger than this is dropped, not sent (a normal match is 5-25 kB).</summary>
        public const int MaxBytes = 96 * 1024;

        static string Dir => Path.Combine(Application.persistentDataPath, "traces");

        public static void Save(byte[] data)
        {
            try
            {
                if (data == null || data.Length == 0 || data.Length > MaxBytes) return;
                Directory.CreateDirectory(Dir);
                string name = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + UnityEngine.Random.Range(0, 1 << 24).ToString("x6") + ".bmt";
                File.WriteAllBytes(Path.Combine(Dir, name), data);
                var all = Pending();
                for (int i = 0; i < all.Count - MaxKept; i++) File.Delete(Path.Combine(Dir, all[i]));
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Game] match trace not saved: " + e.Message); }
        }

        /// <summary>The waiting traces' file names, oldest first.</summary>
        public static List<string> Pending()
        {
            var l = new List<string>();
            try
            {
                if (!Directory.Exists(Dir)) return l;
                foreach (var f in Directory.GetFiles(Dir, "*.bmt")) l.Add(Path.GetFileName(f));
                l.Sort(StringComparer.Ordinal);
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Game] match traces: " + e.Message); }
            return l;
        }

        public static byte[] Read(string name)
        {
            try { return File.ReadAllBytes(Path.Combine(Dir, name)); }
            catch { return null; }
        }

        public static void Remove(string name)
        {
            try { File.Delete(Path.Combine(Dir, name)); } catch { }
        }
    }
}
