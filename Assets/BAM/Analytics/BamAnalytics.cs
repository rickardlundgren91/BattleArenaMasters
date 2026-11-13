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
    // Only four custom events exist; they must match Docs/play/privacy-policy.md, Docs/play/data-safety.md and the Event Manager
    // on the Unity Dashboard (Docs/play/unity-analytics-sv.md: an event with a parameter the Event Manager lacks is Invalid, lost):
    //   matchStart   {heroName, difficulty, tutorial, mode, humans, versus, spell}
    //   matchEnd     {heroName, difficulty, tutorial, won, quit, matchSeconds, kills, deaths, heroLevel, mode, humans, versus, spell,
    //                 item1, item2}
    //   itemPicked   {itemName, heroName, heroLevel, mode, offerSeconds}
    //   tutorialStep {stepIndex, stepName}
    // mode: "classic" (against bots, the tutorial too), "online" (with other phones; difficulty is then the room's bots) or the
    // PRACTICE map's name ("grove"); humans: the people in the match; versus: one of them on the other team (full review
    // 2026-11-13 nr 110: before, an online match looked like a bot match and Grove sent nothing).
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

        /// <summary>start: the match as it begins (GameFlow.Result), for its mode, people and spell.</summary>
        public static void MatchStarted(MatchSetup setup, MatchResult start)
        {
            if (!Allowed) return;
            Sdk.MatchStart(Played(setup).ToString(), setup.Difficulty.ToString(), setup.Tutorial, Mode(setup, start.Online), start.Humans,
                start.Versus, start.Spell.ToString());
        }

        public static void MatchEnded(MatchSetup setup, MatchResult result)
        {
            if (!Allowed) return;
            Sdk.MatchEnd(result.Hero.ToString(), setup.Difficulty.ToString(), setup.Tutorial, result.Won, result.Quit,
                Mathf.RoundToInt(result.Seconds), result.Kills, result.Deaths, result.Level, Mode(setup, result.Online), result.Humans,
                result.Versus, result.Spell.ToString(), Item(result.Item1), Item(result.Item2));
        }

        /// <summary>The player took an item from its choice of three; offerSeconds: how long the choice stood (match time).</summary>
        public static void ItemPicked(MatchSetup setup, bool online, BAM.Sim.ItemId item, BAM.Sim.HeroId hero, int level, float offerSeconds)
        {
            if (!Allowed) return;
            Sdk.ItemPicked(Item(item), hero.ToString(), level, Mode(setup, online), (float)Math.Round(offerSeconds, 1));
        }

        static string Mode(MatchSetup setup, bool online) =>
            online ? "online" : setup.Map != BAM.Sim.MapId.Runes && !setup.Tutorial ? setup.Map.ToString().ToLowerInvariant() : "classic";
        static string Item(BAM.Sim.ItemId id) => id == BAM.Sim.ItemId.None ? "none" : id.ToString();

        public static void TutorialStep(int step, string name)
        {
            if (!Allowed) return;
            Sdk.TutorialStep(step, name ?? "");
        }

        /// <summary>The hero actually played: the guided match is always Tyra, whatever was last picked in the menu.</summary>
        static BAM.Sim.HeroId Played(MatchSetup setup) => setup.Tutorial ? BAM.Sim.HeroId.Tyra : setup.Hero;

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
            // events from before Unity Services finished starting (the first tutorial begins 0.6 s after the Yes), sent once it
            // has, and only if sharing is still on then
            static readonly System.Collections.Generic.List<CustomEvent> pending = new System.Collections.Generic.List<CustomEvent>(8);
            const int MaxPending = 8;

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
                Flush();
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
                        if (!ready) { pending.Clear(); return; } // the flag stays, so it is tried again next start
                        // the player turned sharing back on while we waited: no deletion, collection runs (Start() returned early)
                        if (BamSettings.AnalyticsConsent == Consent.Granted || PlayerPrefs.GetInt(DeletePendingKey, 0) != 1)
                        {
                            if (BamSettings.AnalyticsConsent == Consent.Granted) Debug.Log("[BAM.Analytics] started");
                            Flush();
                            return;
                        }
                    }
                    pending.Clear();
                    AnalyticsService.Instance.RequestDataDeletion();
                    Clear();
                    Debug.Log("[BAM.Analytics] data deletion requested");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[BAM.Analytics] deletion request failed, will retry next start: " + e.Message);
                }
            }

            /// <summary>Sends the events kept while starting, if sharing is still on; drops them otherwise.</summary>
            static void Flush()
            {
                if (ready && BamSettings.AnalyticsConsent == Consent.Granted)
                    for (int i = 0; i < pending.Count; i++)
                    {
                        try { AnalyticsService.Instance.RecordEvent(pending[i]); }
                        catch (Exception ex) { Debug.LogWarning("[BAM.Analytics] RecordEvent failed: " + ex.Message); }
                    }
                pending.Clear();
            }

            static void Clear()
            {
                PlayerPrefs.DeleteKey(DeletePendingKey);
                PlayerPrefs.Save();
            }

            public static void MatchStart(string hero, string difficulty, bool tutorial, string mode, int humans, bool versus, string spell)
            {
                var e = New("matchStart");
                if (e == null) return;
                e["heroName"] = hero;
                e["difficulty"] = difficulty;
                e["tutorial"] = tutorial;
                e["mode"] = mode;
                e["humans"] = humans;
                e["versus"] = versus;
                e["spell"] = spell;
                Record(e);
                Upload();
            }

            public static void MatchEnd(string hero, string difficulty, bool tutorial, bool won, bool quit, int seconds, int kills, int deaths, int level,
                string mode, int humans, bool versus, string spell, string item1, string item2)
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
                e["mode"] = mode;
                e["humans"] = humans;
                e["versus"] = versus;
                e["spell"] = spell;
                e["item1"] = item1;
                e["item2"] = item2;
                Record(e);
                Upload();
            }

            // rides along with the next match event or the automatic upload, as the tutorial steps do
            public static void ItemPicked(string item, string hero, int level, string mode, float offerSeconds)
            {
                var e = New("itemPicked");
                if (e == null) return;
                e["itemName"] = item;
                e["heroName"] = hero;
                e["heroLevel"] = level;
                e["mode"] = mode;
                e["offerSeconds"] = offerSeconds;
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
                return ready || starting ? new CustomEvent(name) : null;
            }

            static void Record(CustomEvent e)
            {
                if (!ready) { if (pending.Count < MaxPending) pending.Add(e); return; }
                try { AnalyticsService.Instance.RecordEvent(e); }
                catch (Exception ex) { Debug.LogWarning("[BAM.Analytics] RecordEvent failed: " + ex.Message); }
            }

            // A match event goes out at once instead of with the next automatic upload (about a minute later): a phone closed on
            // the result card, or one that hangs there, would otherwise lose it (bot analysis 2026-11-01: 5 of 41 matches on 0.3.1
            // sent no matchEnd). Tutorial steps ride along with the next match event or the automatic upload.
            static void Upload()
            {
                if (!ready) return; // still starting: Start() records the kept events and the automatic upload sends them
                try { AnalyticsService.Instance.Flush(); }
                catch (Exception ex) { Debug.LogWarning("[BAM.Analytics] Flush failed: " + ex.Message); }
            }
        }
#else
        // Package not installed yet: consent is still stored and passed to the engine, nothing is collected.
        static class Sdk
        {
            public static void Start() => Debug.Log("[BAM.Analytics] consent granted, but com.unity.services.analytics is not installed: nothing is sent");
            public static void RequestDeletion() { PlayerPrefs.DeleteKey(DeletePendingKey); PlayerPrefs.Save(); }
            public static void MatchStart(string hero, string difficulty, bool tutorial, string mode, int humans, bool versus, string spell) { }
            public static void MatchEnd(string hero, string difficulty, bool tutorial, bool won, bool quit, int seconds, int kills, int deaths, int level,
                string mode, int humans, bool versus, string spell, string item1, string item2) { }
            public static void ItemPicked(string item, string hero, int level, string mode, float offerSeconds) { }
            public static void TutorialStep(int step, string name) { }
        }
#endif
    }
}
