using System;
using BAM.Core;
using UnityEngine;
using UnityEngine.Networking;
#if BAM_PLAY_UPDATE && UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif

namespace BAM.UI
{
    // Rickard 2026-10-30: several testers kept playing an old version (Google Play updates apps on its own only every few
    // hours). Once per start the game reads the newest version code from the website (version.json, {"latest": 12}); when
    // this build's Android version code is lower, Home shows a "New version" dialog whose UPDATE opens BAM's Google Play page.
    // No internet, no file, the Editor or an unreadable code: nothing is shown.
    //
    // Full review 2026-11-13 nr 125: with Google's In-App Updates package (com.google.play.appupdate) installed, BAM_PLAY_UPDATE is
    // defined (BAM.UI.asmdef versionDefines) and Google Play itself says whether a newer version is out for this very player
    // (closed test or store), so version.json no longer has to be raised by hand. UPDATE then runs Play's own full-screen update
    // (download, install, restart) without leaving the game for the store page. When Play cannot answer (an app not installed
    // from Play, no Play Store, an error), the website check runs as before. Without the package nothing here changes.
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
#if BAM_PLAY_UPDATE && UNITY_ANDROID && !UNITY_EDITOR
            if (AskPlay(mine)) return; // the website only when Play cannot be asked
#endif
            CheckWebsite(mine);
        }

        static void CheckWebsite(int mine)
        {
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
                "Update", StartUpdate,
                UiKit.Muted, UiKit.Good, () => dialog.Hide());
        }

        static void StartUpdate()
        {
#if BAM_PLAY_UPDATE && UNITY_ANDROID && !UNITY_EDITOR
            if (PlayUpdate()) return;
#endif
            OpenStorePage();
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

#if BAM_PLAY_UPDATE && UNITY_ANDROID && !UNITY_EDITOR
        static AppUpdateManager playUpdates;
        static AppUpdateInfo playInfo; // Play's answer when it has a newer version for this player

        /// <summary>Asks Google Play for a newer version; false when it cannot be asked (then the website is asked instead).</summary>
        static bool AskPlay(int mine)
        {
            try
            {
                playUpdates = new AppUpdateManager();
                var op = playUpdates.GetAppUpdateInfo();
                op.Completed += done =>
                {
                    try
                    {
                        if (!done.IsSuccessful)
                        {
                            Debug.Log("[BAM.UI] Google Play update check failed (" + done.Error + "), asking the website");
                            CheckWebsite(mine);
                            return;
                        }
                        var info = done.GetResult();
                        Debug.Log("[BAM.UI] Version code " + mine + ", Google Play: " + info.UpdateAvailability + ", newest " + info.AvailableVersionCode);
                        var now = AppUpdateOptions.ImmediateAppUpdateOptions();
                        // an update the player started and left: Google asks apps to resume it straight away
                        if (info.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress) { playInfo = info; PlayUpdate(); return; }
                        if (info.UpdateAvailability != UpdateAvailability.UpdateAvailable || info.AvailableVersionCode <= mine) return;
                        playInfo = info.IsUpdateTypeAllowed(now) ? info : null; // not allowed: UPDATE opens the store page
                        updateLatest = info.AvailableVersionCode;
                    }
                    catch (Exception e) { Debug.Log("[BAM.UI] Google Play update check failed: " + e.Message); }
                };
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("[BAM.UI] Google Play update check could not start: " + e.Message);
                return false;
            }
        }

        /// <summary>Play's full-screen update (it restarts the game when installed); false when it cannot start.</summary>
        static bool PlayUpdate()
        {
            if (playUpdates == null || playInfo == null) return false;
            try
            {
                var req = playUpdates.StartUpdate(playInfo, AppUpdateOptions.ImmediateAppUpdateOptions());
                req.Completed += r =>
                {
                    // Canceled = the player said no in Play's screen; Home shows the notice again at the next start
                    if (r.Status == AppUpdateStatus.Failed) { Debug.Log("[BAM.UI] Google Play update failed (" + r.Error + "), opening the store page"); OpenStorePage(); }
                };
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("[BAM.UI] Google Play update could not start: " + e.Message);
                return false;
            }
        }
#endif

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
