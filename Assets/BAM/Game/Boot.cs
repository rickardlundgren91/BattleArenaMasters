using BAM.Analytics;
using BAM.Art;
using BAM.Audio;
using BAM.Core;
using BAM.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BAM.Game
{
    /// <summary>Starts the whole game from code after the (empty) first scene loads. Owner: the main thread.</summary>
    static class Boot
    {
        static int appliedQuality = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            Application.targetFrameRate = 60;

            var host = new GameObject("BAM.Game");
            Object.DontDestroyOnLoad(host);
            var flow = host.AddComponent<GameFlow>();
            flow.Camera = CreateCamera();

            ArtKit.Init();
            ApplyQuality();
            BamSettings.Changed += ApplyQuality;
            // BamAudio puts the AudioListener on its own object, so the camera above has none.
            BamAudio.Init();
            Screens.Init();
            BamAnalytics.Init();
            Flow.Game = flow;

            Screens.ShowConsentIfNeeded(Screens.ShowMainMenu);
            Debug.Log("[BAM.Game] " + BamInfo.AppName + " " + Application.version + " started");
        }

        // Quality index 0 is Low and 1 is High (BAM > Setup Project makes exactly these two).
        static void ApplyQuality()
        {
            int q = (int)BamSettings.Graphics;
            if (q == appliedQuality) return;
            appliedQuality = q;
            if (q < QualitySettings.count) QualitySettings.SetQualityLevel(q, true);
            ArtKit.ApplyQuality(BamSettings.Graphics);
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("BAM.Camera");
            Object.DontDestroyOnLoad(go);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.18f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            return cam;
        }
    }
}
