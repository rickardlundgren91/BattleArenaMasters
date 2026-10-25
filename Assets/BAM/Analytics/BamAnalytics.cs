using System;
using BAM.Core;
using UnityEngine;
using UnityEngine.UnityConsent;
#if BAM_UGS_ANALYTICS
using Unity.Services.Analytics;
using Unity.Services.Core;
#endif

namespace BAM.Analytics
{
    // CONTRACT between BAM.Analytics (owner: the Play thread) and BAM.Game. Docs/CONTRACTS.md explains it.
    // Nothing is collected or sent unless BamSettings.AnalyticsConsent == Granted. Every call is cheap and safe without consent.
    //
    // Unity Analytics 6.1+ on Unity 6.2+ takes consent through the engine's EndUserConsent API (the old
    // StartDataCollection/StopDataCollection throw once EndUserConsent is used, so they are never called here).
    // Unity Services are initialized only after the player said yes, so a player who says no never talks to Unity at all.
    // Only three custom events exist; they must match Docs/play/privacy-policy.md and Docs/play/data-safety.md:
    //   matchStart   {heroName, difficulty, tutorial}
    //   matchEnd     {heroName, difficulty, tutorial, won, quit, matchSeconds, kills, deaths, heroLevel}
    //   tutorialStep {stepIndex, stepName}
    // BAM_UGS_ANALYTICS comes from a versionDefine in BAM.Analytics.asmdef (com.unity.services.analytics >= 6.1.0), so the
    // project compiles, and simply sends nothing, before the package is installed.
    public static class BamAnalytics
    {
        // Set when the player turned sharing off after it was on; kept until the deletion request reached the SDK,
        // so a phone that was offline or closed the app in between still asks Unity to delete the data next start.
        const string DeletePendingKey = "bam.analytics.delete";

        static bool inited;
        static Consent applied;

        /// <summary>Once at boot. Starts the analytics SDK only if consent is Granted, and follows BamSettings.Changed after that.</summary>
        public static void Init()
        {
            if (inited) return;
            inited = true;
            applied = BamSettings.AnalyticsConsent;
            ApplyEngineConsent(applied);
            BamSettings.Changed += OnSettingsChanged;
            if (applied == Consent.Granted) Sdk.Start();
            else if (PlayerPrefs.GetInt(DeletePendingKey, 0) == 1) Sdk.RequestDeletion();
        }

        public static void MatchStarted(MatchSetup setup)
        {
            if (!Allowed) return;
            Sdk.MatchStart(setup.Hero.ToString(), setup.Difficulty.ToString(), setup.Tutorial);
        }

        public static void MatchEnded(MatchSetup setup, MatchResult result)
        {
            if (!Allowed) return;
            Sdk.MatchEnd(setup.Hero.ToString(), setup.Difficulty.ToString(), setup.Tutorial, result.Won, result.Quit,
                Mathf.RoundToInt(result.Seconds), result.Kills, result.Deaths, result.Level);
        }

        public static void TutorialStep(int step, string name)
        {
            if (!Allowed) return;
            Sdk.TutorialStep(step, name ?? "");
        }

        static bool Allowed => inited && BamSettings.AnalyticsConsent == Consent.Granted;

        static void OnSettingsChanged()
        {
            var now = BamSettings.AnalyticsConsent;
            if (now == applied) return;
            var before = applied;
            applied = now;
            ApplyEngineConsent(now);
            if (now == Consent.Granted)
            {
                PlayerPrefs.DeleteKey(DeletePendingKey);
                PlayerPrefs.Save();
                Sdk.Start();
            }
            else if (before == Consent.Granted)
            {
                // Turning sharing off also asks Unity to delete what was already collected (privacy policy, section 5).
                PlayerPrefs.SetInt(DeletePendingKey, 1);
                PlayerPrefs.Save();
                Sdk.RequestDeletion();
            }
        }

        // Unknown is sent as Denied: the engine treats Unspecified as "consent handled elsewhere", which is not what we want.
        static void ApplyEngineConsent(Consent c)
        {
            try
            {
                var state = EndUserConsent.GetConsentState();
                state.AnalyticsIntent = c == Consent.Granted ? ConsentStatus.Granted : ConsentStatus.Denied;
                state.AdsIntent = ConsentStatus.Denied; // no ads in BAM
                EndUserConsent.SetConsentState(state);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Analytics] could not set consent state: " + e.Message);
            }
        }

#if BAM_UGS_ANALYTICS
        static class Sdk
        {
            static bool starting, ready;

            // Nothing is sent from the Editor, so testing in Unity never fills the dashboard; the log shows what would go.
            static bool Live => !Application.isEditor;

            public static async void Start()
            {
                if (!Live) { Debug.Log("[BAM.Analytics] consent granted (Editor: nothing is sent)"); return; }
                if (ready || starting) return;
                starting = true;
                try
                {
                    if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                    while (UnityServices.State == ServicesInitializationState.Initializing) await System.Threading.Tasks.Task.Delay(50);
                    ready = UnityServices.State == ServicesInitializationState.Initialized;
                    if (!ready) Debug.LogWarning("[BAM.Analytics] Unity Services did not initialize (state " + UnityServices.State + ")");
                    else Debug.Log("[BAM.Analytics] started");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BAM.Analytics] Unity Services init failed, analytics off: " + e.Message);
                }
                finally
                {
                    starting = false;
                }
                // The player may have said no while we waited: EndUserConsent already stopped collection,
                // and the pending deletion request goes out now that the SDK can take it.
                if (ready && BamSettings.AnalyticsConsent != Consent.Granted && PlayerPrefs.GetInt(DeletePendingKey, 0) == 1)
                    RequestDeletion();
            }

            public static async void RequestDeletion()
            {
                if (!Live) { Debug.Log("[BAM.Analytics] consent withdrawn, data deletion requested (Editor: nothing is sent)"); Clear(); return; }
                try
                {
                    if (!ready)
                    {
                        if (starting) return; // Start() sends it when init finishes
                        // Initializing with consent Denied collects nothing; it is only needed to deliver the deletion request.
                        starting = true;
                        try
                        {
                            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                            while (UnityServices.State == ServicesInitializationState.Initializing) await System.Threading.Tasks.Task.Delay(50);
                            ready = UnityServices.State == ServicesInitializationState.Initialized;
                        }
                        finally { starting = false; }
                        if (!ready) return; // the flag stays, so it is tried again next start
                    }
                    AnalyticsService.Instance.RequestDataDeletion();
                    Clear();
                    Debug.Log("[BAM.Analytics] data deletion requested");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BAM.Analytics] deletion request failed, will retry next start: " + e.Message);
                }
            }

            static void Clear()
            {
                PlayerPrefs.DeleteKey(DeletePendingKey);
                PlayerPrefs.Save();
            }

            public static void MatchStart(string hero, string difficulty, bool tutorial)
            {
                var e = New("matchStart");
                if (e == null) return;
                e["heroName"] = hero;
                e["difficulty"] = difficulty;
                e["tutorial"] = tutorial;
                Record(e);
            }

            public static void MatchEnd(string hero, string difficulty, bool tutorial, bool won, bool quit, int seconds, int kills, int deaths, int level)
            {
                var e = New("matchEnd");
                if (e == null) return;
                e["heroName"] = hero;
                e["difficulty"] = difficulty;
                e["tutorial"] = tutorial;
                e["won"] = won;
                e["quit"] = quit;
                e["matchSeconds"] = seconds;
                e["kills"] = kills;
                e["deaths"] = deaths;
                e["heroLevel"] = level;
                Record(e);
            }

            public static void TutorialStep(int step, string name)
            {
                var e = New("tutorialStep");
                if (e == null) return;
                e["stepIndex"] = step;
                e["stepName"] = name;
                Record(e);
            }

            static CustomEvent New(string name)
            {
                if (!Live) { Debug.Log("[BAM.Analytics] event " + name + " (Editor: not sent)"); return null; }
                return ready ? new CustomEvent(name) : null;
            }

            static void Record(CustomEvent e)
            {
                try { AnalyticsService.Instance.RecordEvent(e); }
                catch (Exception ex) { Debug.LogWarning("[BAM.Analytics] RecordEvent failed: " + ex.Message); }
            }
        }
#else
        // Package not installed yet: consent is still stored and passed to the engine, nothing is collected.
        static class Sdk
        {
            public static void Start() => Debug.Log("[BAM.Analytics] consent granted, but com.unity.services.analytics is not installed: nothing is sent");
            public static void RequestDeletion() { PlayerPrefs.DeleteKey(DeletePendingKey); PlayerPrefs.Save(); }
            public static void MatchStart(string hero, string difficulty, bool tutorial) { }
            public static void MatchEnd(string hero, string difficulty, bool tutorial, bool won, bool quit, int seconds, int kills, int deaths, int level) { }
            public static void TutorialStep(int step, string name) { }
        }
#endif
    }
}
