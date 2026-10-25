using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Build
{
    /// <summary>
    /// The working copy gives textures, models and fonts a .meta with only a GUID (bam-tools/metagen.py), which Unity reads
    /// as importer version 1 and warns about ("below the supported minimum"). This saves those .meta files again with Unity's
    /// own full import settings, once, so the warnings stop. It runs by itself after scripts load and after new files arrive.
    /// </summary>
    [InitializeOnLoad]
    static class BamMetaUpgrade
    {
        const string Root = "Assets/BAM";
        static bool queued;
        // files already tried this session: a .meta Unity leaves as it was is not tried again on every editor update
        static readonly HashSet<string> tried = new HashSet<string>();

        static BamMetaUpgrade() => Queue();

        internal static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
            var paths = new List<string>();
            foreach (string meta in Directory.GetFiles(Root, "*.meta", SearchOption.AllDirectories))
            {
                string asset = meta.Substring(0, meta.Length - 5).Replace('\\', '/');
                if (Directory.Exists(asset) || tried.Contains(asset) || !GuidOnly(meta)) continue;
                tried.Add(asset);
                paths.Add(asset);
            }
            if (paths.Count == 0) return;
            AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeMetadata);
            Debug.Log("[BAM.Build] Saved full import settings in " + paths.Count + " .meta files (the 'object at version 1' warnings stop after this)");
        }

        // only "fileFormatVersion: 2" and "guid: ...": no importer settings yet
        static bool GuidOnly(string meta)
        {
            foreach (string line in File.ReadLines(meta))
                if (line.Trim().Length > 0 && !line.StartsWith("fileFormatVersion:") && !line.StartsWith("guid:")) return false;
            return true;
        }
    }

    sealed class BamMetaUpgradeOnImport : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string p in imported)
                if (p.StartsWith("Assets/BAM/")) { BamMetaUpgrade.Queue(); return; }
        }
    }
}
