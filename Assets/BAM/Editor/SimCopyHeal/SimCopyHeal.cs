using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// When a sim field the generated copy uses was renamed or removed, BAM.Sim no longer compiles because of SimCopy.g.cs. This editor
// script needs nothing from BAM.Sim, so it still runs then: it puts an empty SimCopy.g.cs in place, BAM.Sim compiles again, and
// BAM.Sim.Editor (SimCopyAuto) makes the real one for the classes as they are now.
namespace BAM.EditorTools
{
    [InitializeOnLoad]
    static class SimCopyHeal
    {
        const string Path = "Assets/BAM/Sim/SimCopy.g.cs";

        static SimCopyHeal() { CompilationPipeline.assemblyCompilationFinished += Done; }

        static void Done(string assembly, CompilerMessage[] messages)
        {
            if (!assembly.EndsWith("BAM.Sim.dll")) return;
            foreach (var m in messages)
            {
                if (m.type != CompilerMessageType.Error || m.file == null || !m.file.Replace('\\', '/').EndsWith("SimCopy.g.cs")) continue;
                File.WriteAllText(Path, "// emptied by SimCopyHeal after it stopped compiling; SimCopyAuto makes it again\n");
                Debug.LogWarning("[BAM] SimCopy.g.cs no longer fitted the sim classes; emptied, it is made again after this compile");
                EditorApplication.delayCall += () => AssetDatabase.ImportAsset(Path);
                return;
            }
        }
    }
}
