using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Build
{
    /// <summary>
    /// The version name and the Android version code. Google Play refuses an upload whose version code is not higher
    /// than every earlier one, so the code only ever goes up. It is kept in three places and the highest wins:
    /// VersionCode.txt next to this script (versioned), EditorPrefs (this PC only, survives a sync that brings an older
    /// file) and PlayerSettings. VersionCode.txt is committed with the code of every released build (23 = 0.3.9), so a fresh
    /// PC or a wiped EditorPrefs never builds a code Play has already seen (full review 2026-11-09 nr 93).
    /// </summary>
    public static class BamVersion
    {
        /// <summary>The version players see. Raise it here when a release deserves a new number.</summary>
        public const string Name = "0.3.8";

        const string PrefsKey = "BAM.Build.VersionCode";
        const string FileName = "VersionCode.txt";

        static string FilePath
        {
            get
            {
                // Next to this script, wherever the folder ends up.
                string[] hits = AssetDatabase.FindAssets("BamVersion t:MonoScript", new[] { "Assets/BAM/Editor/Build" });
                string dir = hits.Length > 0
                    ? Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(hits[0]))
                    : "Assets/BAM/Editor/Build";
                return Path.Combine(ProjectRoot, dir, FileName);
            }
        }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>The highest code seen anywhere, at least 1.</summary>
        public static int Current
        {
            get
            {
                int code = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
                code = Math.Max(code, EditorPrefs.GetInt(PrefsKey, 0));
                code = Math.Max(code, ReadFile());
                return code;
            }
        }

        /// <summary>Makes sure all three places hold <see cref="Current"/>; never lowers anything.</summary>
        public static void Sync()
        {
            Store(Current);
        }

        /// <summary>Raises the code by one and returns the new value.</summary>
        public static int Raise()
        {
            int next = Current + 1;
            Store(next);
            Debug.Log("[BAM.Build] Version code raised to " + next);
            return next;
        }

        /// <summary>Puts the code back after a build that failed, so failed attempts do not use up numbers.</summary>
        public static void Restore(int code)
        {
            PlayerSettings.Android.bundleVersionCode = code;
            EditorPrefs.SetInt(PrefsKey, code);
            WriteFile(code);
            Debug.Log("[BAM.Build] Version code set back to " + code + " (the build did not finish)");
        }

        static void Store(int code)
        {
            if (PlayerSettings.Android.bundleVersionCode != code)
            {
                Debug.Log("[BAM.Build] Android version code: " + PlayerSettings.Android.bundleVersionCode + " -> " + code);
                PlayerSettings.Android.bundleVersionCode = code;
            }
            if (EditorPrefs.GetInt(PrefsKey, 0) != code) EditorPrefs.SetInt(PrefsKey, code);
            if (ReadFile() != code) WriteFile(code);
        }

        static int ReadFile()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return 0;
                return int.TryParse(File.ReadAllText(path).Trim(), out int v) ? v : 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Build] Could not read " + FileName + ": " + e.Message);
                return 0;
            }
        }

        static void WriteFile(int code)
        {
            try
            {
                File.WriteAllText(FilePath, code + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Build] Could not write " + FileName + ": " + e.Message);
            }
        }
    }
}
