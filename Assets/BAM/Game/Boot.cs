using BAM.Analytics;
using BAM.Art;
using BAM.Audio;
using BAM.Core;
using BAM.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BAM.Game
{
    /// <summary>Starts the whole game from code after the (empty) first scene loads. Owner: the main thread.</summary>
    static class Boot
    {
        static int appliedQuality = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            // Editor tools such as BAM > Art Preview play a scene of their own (marked with an object of this name, since an
            // unsaved scene did not always report buildIndex -1); the game only starts from a scene in the build.
            if (SceneManager.GetActiveScene().buildIndex < 0 || GameObject.Find("BAM Tool Scene") != null)
            {
                Debug.Log("[BAM.Game] Not started in this scene. Open Assets/BAM/Scenes/Main.unity and press Play to play the game.");
                return;
            }
            Application.targetFrameRate = BamSettings.Fps;

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
            Application.targetFrameRate = BamSettings.Fps; // 60, or 30 if the player picked it in Settings
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
