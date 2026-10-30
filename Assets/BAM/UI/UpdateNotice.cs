using System;
using BAM.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace BAM.UI
{
    // Rickard 2026-10-30: several testers kept playing an old version (Google Play updates apps on its own only every few
    // hours). Once per start the game reads the newest version code from the website (version.json, {"latest": 12}); when
    // this build's Android version code is lower, Home shows a "New version" dialog whose UPDATE opens BAM's Google Play page.
    // No internet, no file, the Editor or an unreadable code: nothing is shown.
    public static partial class Screens
    {
        const string VersionUrl = "https://battlearenamasters.com/version.json";
        static int updateLatest; // the website's code when it is newer than this build, else 0
        static bool updateAsked, updateShown;

        [Serializable]
        class VersionFile { public int latest; }

        static void CheckForUpdate()
        {
            if (updateAsked) return;
            updateAsked = true;
            int mine = BuildCode();
            if (mine <= 0) return;
            // The minute in the address gets past the website's cache, so a new number reaches players within minutes.
            var req = UnityWebRequest.Get(VersionUrl + "?t=" + DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute);
            req.timeout = 8;
            req.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (req.result != UnityWebRequest.Result.Success) return;
                    var file = JsonUtility.FromJson<VersionFile>(req.downloadHandler.text);
                    if (file == null) return;
                    Debug.Log("[BAM.UI] Version code " + mine + ", newest on the website " + file.latest);
                    if (file.latest > mine) updateLatest = file.latest;
                }
                catch (Exception e) { Debug.Log("[BAM.UI] Version check failed: " + e.Message); }
                finally { req.Dispose(); }
            };
        }

        /// <summary>From Tick: shows the dialog once, on Home, when nothing else is open. No allocations until then.</summary>
        static void TickUpdateNotice()
        {
            if (updateLatest == 0 || updateShown) return;
            if (stack.Count == 0 || stack[stack.Count - 1] != mainMenu || dialog.Open) return;
            if (Flow.Game != null && Flow.Game.InMatch) return;
            updateShown = true;
            dialog.Show("New version!",
                "A newer version of BAM is on Google Play. Update to get the latest heroes and fixes, and to play online with everyone.",
                "Later", null,
                "Update", OpenStorePage,
                UiKit.Muted, UiKit.Good, () => dialog.Hide());
        }

        static void OpenStorePage()
        {
            string id = Application.identifier;
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.OpenURL("market://details?id=" + id);
#else
            Application.OpenURL("https://play.google.com/store/apps/details?id=" + id);
#endif
        }

        /// <summary>This build's Android version code (BamVersion raises it for every build), 0 when it cannot be read.</summary>
        static int BuildCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var info = pm.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0))
                    return info.Get<int>("versionCode");
            }
            catch (Exception e)
            {
                Debug.Log("[BAM.UI] Could not read the version code: " + e.Message);
                return 0;
            }
#else
            return 0;
#endif
        }
    }
}
