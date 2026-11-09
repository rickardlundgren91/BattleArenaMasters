using UnityEditor;
using UnityEngine;

namespace BAM.Audio.Editor
{
    /// <summary>
    /// Import rules for the recorded sound effects (Resources/BAM/Sfx; the music is left alone). BamAudio reads each sound clip's samples once to
    /// match its loudness to the others (AudioClip.GetData), which only works when the clip is decompressed on load; a clip left
    /// compressed in memory filled the Console with "Cannot get data on compressed samples" (Rickard 2026-10-26, BonkStun).
    /// Every effect is mono: they are played at a point (BamAudio.PlayAt) or centred, so a stereo clip only doubled its decompressed
    /// size in memory (about 17 MB for all of them; full review 2026-11-09 nr 86). The sample rate stays each clip's own.
    /// </summary>
    sealed class SfxImport : AssetPostprocessor
    {
        const string SfxDir = "Assets/BAM/Audio/Resources/BAM/Sfx/";
        static readonly string[] Platforms = { "Android", "Standalone", "iPhone" };

        // a new number imports every sound again with these rules (2: mono)
        public override uint GetVersion() => 2;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SfxDir)) return; // the music loop lives in Resources/BAM/Music, outside SfxDir: its own hand-written meta (streaming Vorbis) stays
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
            foreach (string p in Platforms)
                if (ai.ContainsSampleSettingsOverride(p)) ai.ClearSampleSettingOverride(p);
            ai.loadInBackground = false;
        }
    }
}
