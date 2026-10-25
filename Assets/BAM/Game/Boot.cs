using UnityEngine;

namespace BAM.Game
{
    /// <summary>Starts the whole game from code after the (empty) first scene loads. Owner: the main thread.</summary>
    static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            Application.targetFrameRate = 60;
        }
    }
}
