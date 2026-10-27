using System.Collections;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// Keeps the game in Android's immersive full screen. A Google sign-in popup, a system dialog, the notification shade or a
    /// trip to another app can bring back the navigation bar, and Unity does not always hide it again, so every time the app
    /// gets focus back this hides the status and navigation bars again (now and once more a moment later, after the popup's
    /// window has gone). Swiping from the edge still shows the bars for a moment (sticky immersive). Owner: the main thread.
    /// </summary>
    sealed class ImmersiveMode : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            var go = new GameObject("BAM.ImmersiveMode");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<ImmersiveMode>();
        }

        void Start() => Apply();

        void OnApplicationFocus(bool focus)
        {
            if (focus) Reapply();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Reapply();
        }

        void Reapply()
        {
            Apply();
            StopAllCoroutines();
            StartCoroutine(ApplyLater());
        }

        IEnumerator ApplyLater()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Apply();
            yield return new WaitForSecondsRealtime(1.5f);
            Apply();
        }

        // SYSTEM_UI_FLAG_ LAYOUT_STABLE | LAYOUT_HIDE_NAVIGATION | LAYOUT_FULLSCREEN | HIDE_NAVIGATION | FULLSCREEN | IMMERSIVE_STICKY
        const int ImmersiveFlags = 0x100 | 0x200 | 0x400 | 0x2 | 0x4 | 0x1000;

        static void Apply()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        using var window = activity.Call<AndroidJavaObject>("getWindow");
                        using var decor = window.Call<AndroidJavaObject>("getDecorView");
                        decor.Call("setSystemUiVisibility", ImmersiveFlags);
                    }
                    catch (System.Exception e) { Debug.LogWarning("[BAM.Game] Immersive mode: " + e.Message); }
                }));
            }
            catch (System.Exception e) { Debug.LogWarning("[BAM.Game] Immersive mode: " + e.Message); }
#endif
        }
    }
}
