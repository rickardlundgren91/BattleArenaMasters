using System.IO;
using UnityEditor;
using UnityEngine;

// Keeps Assets/BAM/Sim/SimCopy.g.cs (the guest prediction's generated match copy) current: after every compile, when the sim
// classes no longer match it (a field was added or changed), it is made again and Unity compiles once more. Without a current file
// the game still plays online, only the guest sees its hero answer late as before (SimMirror.Available is false).
namespace BAM.Sim.EditorTools
{
    [InitializeOnLoad]
    static class SimCopyAuto
    {
        const string Path = "Assets/BAM/Sim/SimCopy.g.cs";

        static SimCopyAuto() { EditorApplication.delayCall += Check; }

        static void Check()
        {
            if (SimMirror.Available) return;
            string hash = SimMirror.Hash(), key = "BAM.SimCopy.tried";
            if (SessionState.GetString(key, "") == hash) return; // tried for these classes already: do not loop
            SessionState.SetString(key, hash);
            Write();
        }

        [MenuItem("BAM/Online/Regenerate SimCopy")]
        static void Write()
        {
            try
            {
                File.WriteAllText(Path, SimCopyGen.Generate());
                AssetDatabase.ImportAsset(Path);
                Debug.Log("[BAM] SimCopy.g.cs made again for the sim classes as they are now (hash " + SimMirror.Hash() + ")");
            }
            catch (System.Exception e) { Debug.LogError("[BAM] SimCopy.g.cs could not be made, the guest plays without prediction: " + e.Message); }
        }
    }
}
