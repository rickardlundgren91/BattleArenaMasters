using UnityEditor;
using UnityEngine;

namespace BAM.Audio.Editor
{
    /// <summary>
    /// Import rules for the recorded sound effects (Resources/BAM/Sfx; the music is left alone). BamAudio reads each sound clip's samples once to
    /// match its loudness to the others (AudioClip.GetData), which only works when the clip is decompressed on load; a clip left
    /// compressed in memory filled the Console with "Cannot get data on compressed samples" (Rickard 2026-10-26, BonkStun).
    /// </summary>
    sealed class SfxImport : AssetPostprocessor
    {
        const string SfxDir = "Assets/BAM/Audio/Resources/BAM/Sfx/";
        static readonly string[] Platforms = { "Android", "Standalone", "iPhone" };

        // a new number imports every sound again with these rules
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SfxDir)) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            ai.defaultSampleSettings = s;
            foreach (string p in Platforms)
                if (ai.ContainsSampleSettingsOverride(p)) ai.ClearSampleSettingOverride(p);
            ai.loadInBackground = false;
        }
    }
}
