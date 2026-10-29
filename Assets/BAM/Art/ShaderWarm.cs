using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// PERF-04: the first time a shader is drawn in a new way (a hit flash, a fade, a ghost), the phone's driver builds it then
    /// and the frame hitches. A recorded list of everything one match drew (a GraphicsStateCollection per graphics API, in
    /// Resources/BAMArt/Shaders) is built a few states per frame from the moment the game starts, in the menu, before any match.
    /// Without the file nothing happens. The file comes from one match on Rickard's phone with the recording build
    /// (BAM > Build shader recording APK and run on phone (USB), scripting define BAM_SHADER_TRACE): it records from start to
    /// the end of a match, sends the list to the Unity editor (and saves a copy on the phone), and starts a new one: one match on
    /// Low and one on High give both files.
    /// </summary>
    sealed class ShaderWarm : MonoBehaviour
    {
        const int PerFrame = 4; // a few states each frame: the menu stays smooth while they build
#pragma warning disable CS0649 // only the recording build leaves it empty
        GraphicsStateCollection list;
#pragma warning restore CS0649
        bool done;

        // per graphics API and quality (Low and High draw with different anti-aliasing, so the driver builds different states)
        static string FileName => "BAM_" + SystemInfo.graphicsDeviceType + "_" + (ArtKit.High ? "High" : "Low");

#if BAM_SHADER_TRACE
        static GraphicsStateCollection trace;
#endif

        void Start()
        {
#if BAM_SHADER_TRACE
            trace = new GraphicsStateCollection();
            trace.BeginTrace();
            Debug.Log("[BAM.Art] shader recording on: play one match to its end");
            done = true;
            return;
#else
            // the editor's imported collection, or the recorded file kept as text (.json / .bytes)
            Object found = Resources.Load("BAMArt/Shaders/" + FileName);
            list = found as GraphicsStateCollection;
            if (list == null && found is TextAsset text)
            {
                list = new GraphicsStateCollection();
                if (!list.LoadFromJson(text.text)) list = null;
            }
            if (list == null || list.totalGraphicsStateCount <= 0) { done = true; return; }
            Debug.Log("[BAM.Art] warming " + list.totalGraphicsStateCount + " shader states from " + FileName);
#endif
        }

        void Update()
        {
            if (done) return;
            if (list.completedWarmupCount >= list.totalGraphicsStateCount) { done = true; return; }
            list.WarmUpProgressively(PerFrame);
        }

        /// <summary>The end of a match (ArtKit.ClearEffects): the recording build sends what it saw.</summary>
        internal static void MatchEnded()
        {
#if BAM_SHADER_TRACE
            if (trace == null || !trace.isTracing) return;
            trace.EndTrace();
            string copy = System.IO.Path.Combine(Application.persistentDataPath, FileName + ".graphicsstate");
            trace.SaveToFile(copy);
            trace.SendToEditor(FileName);
            Debug.Log("[BAM.Art] shader recording: " + trace.totalGraphicsStateCount + " states sent to the editor as " + FileName + " (copy " + copy + ")");
            trace = new GraphicsStateCollection();
            trace.BeginTrace(); // the next match, perhaps on the other quality
#endif
        }
    }
}
