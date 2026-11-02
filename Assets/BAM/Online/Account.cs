using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace BAM.Online
{
    /// <summary>
    /// The player's online account (2026-10-26, Rickard: "a unique player and name for every player in Google Play").
    /// The account IS the player's Google Play Games account: Play Games signs in on the phone, its server code signs in to Unity
    /// Authentication, and Unity keeps one player ID per Google account. The same Google account therefore gives the same
    /// player, name, level and friends on every phone and after a reinstall. There is no anonymous account and no backup code
    /// (Speltest had both: an anonymous account was lost with the phone, and the backup code was a username and password).
    /// In the Unity editor, where Play Games cannot run, a test account signs in anonymously instead.
    ///
    /// Online is something the player turns on (<see cref="Enabled"/>): until then nothing is sent and the game is the offline
    /// 0.1 game. Everything here is best effort: no network or a service not set up just leaves <see cref="State"/> showing why.
    /// </summary>
    public static class Account
    {
        public enum Status
        {
            Off,            // online not turned on, or the account was deleted
            Working,        // signing in or talking to the server
            NeedsSignIn,    // Play Games said no (cancelled, or no Play Games on the phone): show a sign-in button
            NeedsName,      // signed in, but the player has no name yet
            Ready,          // signed in with a name
            Offline,        // could not reach Unity; tries again on Retry or the next start
        }

        const string EnabledKey = "bam.online.on", PlayerKey = "bam.online.player";
        // Play Games may never call back (a sign-in screen that errors, e.g. 403 access_denied while the OAuth app is in Testing);
        // a silent sign-in gets less time than one where the player is looking at a Google screen
        const int SilentTimeoutMs = 30000, InteractiveTimeoutMs = 120000, ServicesTimeoutMs = 30000;

        static Task running;
        static string lastError = "";

        public static Status State { get; private set; } = Status.Off;
        /// <summary>Why the last attempt failed, for a small line under the button. Empty when nothing failed.</summary>
        public static string LastError => lastError;
        public static string PlayerId => State == Status.Ready || State == Status.NeedsName ? AuthenticationService.Instance.PlayerId : null;
        /// <summary>Signed in, but Unity's session ran out (a long time in the background): the next server call would fail.</summary>
        internal static bool SessionLost => (State == Status.Ready || State == Status.NeedsName)
            && (!AuthenticationService.Instance.IsSignedIn || AuthenticationService.Instance.IsExpired);
        /// <summary>The player's unique name, null before one is chosen.</summary>
        public static string Name { get; internal set; }
        /// <summary>The Play Games name, offered as the first name suggestion.</summary>
        public static string PlatformName { get; private set; } = "";

        /// <summary>Raised on the main thread whenever State or Name changes.</summary>
        public static event Action Changed;

        /// <summary>The Play Games sign-in, set by BAM.Online.PlayGames on Android. Null in the editor and in tests.</summary>
        public static IPlatformSignIn Platform;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>At boot (and from the "Go online" button). Does nothing until the player turned online on.</summary>
        public static void Start(bool interactive = false)
        {
            if (!Enabled || running != null) return;
            // a sign-in that finished without waiting (Play Games answered at once) already ran its finally: keeping its Task
            // here would block every later Start
            var t = SignIn(interactive);
            if (!t.IsCompleted) running = t;
        }

        /// <summary>The player chose to go online: from the first-time question or a sign-in button.</summary>
        public static void TurnOn()
        {
            Enabled = true;
            Start(interactive: true);
        }

        static async Task SignIn(bool interactive)
        {
            Set(Status.Working);
            try
            {
                // Analytics (BamAnalytics) may have started Unity Services a moment before: then the state is Initializing and
                // going on at once gave "Singleton is not initialized" on the first server call (Rickard's phone, 2026-10-27)
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                float until = Time.realtimeSinceStartup + ServicesTimeoutMs / 1000f;
                while (UnityServices.State == ServicesInitializationState.Initializing && Time.realtimeSinceStartup < until) await Task.Delay(50);
                if (UnityServices.State != ServicesInitializationState.Initialized) throw new Exception("Unity Services did not start (" + UnityServices.State + ")");
                var auth = AuthenticationService.Instance;
                // after a long pause the session may have expired: IsSignedIn stays true, but every call fails until we sign in again
                if (auth.IsSignedIn && auth.IsExpired) { Social.Stop(); auth.SignOut(); }   // Friends shuts down on sign-out: stop ours with it
                if (!auth.IsSignedIn)
                {
                    if (Platform != null)
                    {
                        var asked = Platform.SignIn(interactive);
                        if (await Task.WhenAny(asked, Task.Delay(interactive ? InteractiveTimeoutMs : SilentTimeoutMs)) != asked)
                        {
                            lastError = "Google Play Games did not answer";
                            Set(Status.NeedsSignIn);
                            return;
                        }
                        var p = await asked;
                        if (!p.Ok) { lastError = p.Error ?? ""; Set(Status.NeedsSignIn); return; }
                        PlatformName = p.DisplayName ?? "";
                        // every start uses a fresh Play Games code: a cached Unity session could belong to another Google account
                        if (auth.SessionTokenExists) { Social.Stop(); auth.SignOut(true); }
                        await auth.SignInWithGooglePlayGamesAsync(p.ServerCode);
                    }
                    else if (Application.isEditor)
                    {
                        await auth.SignInAnonymouslyAsync();   // editor test account only, never in a build
                    }
                    else
                    {
                        lastError = "Google Play Games is not available";
                        Set(Status.NeedsSignIn);
                        return;
                    }
                }

                string id = auth.PlayerId;
                if (PlayerPrefs.GetString(PlayerKey, "") != id)
                {
                    // another Google account than last time: the old totals are not this player's
                    PlayerPrefs.SetString(PlayerKey, id);
                    Progress.ResetServer();
                }
                lastError = "";
                await Server.Refresh();
                Set(string.IsNullOrEmpty(Name) ? Status.NeedsName : Status.Ready);
                await Server.SendPending();
                Social.Start();
            }
            catch (Exception e)
            {
                lastError = Short(e);
                Debug.LogWarning("[BAM.Online] sign-in failed: " + e);
                Set(Status.Offline);
            }
            finally
            {
                running = null;
            }
        }

        /// <summary>
        /// Deletes the account (Google Play requires this in the app when a game has accounts): the server frees the name, takes
        /// the player off the Top 100 and deletes the saved data, then Unity Authentication deletes the player. Online is turned
        /// off; turning it on again later starts a new, empty player.
        /// </summary>
        public static async Task<bool> Delete()
        {
            if (State != Status.Ready && State != Status.NeedsName) return false;
            Set(Status.Working);
            bool serverDone = false;
            try
            {
                Social.Stop();
                await Server.DeleteMe();
                serverDone = true;
                await AuthenticationService.Instance.DeleteAccountAsync();
                PlayerPrefs.DeleteKey(PlayerKey);
                Progress.ResetServer();
                Name = null;
                Enabled = false;
                Set(Status.Off);
                return true;
            }
            catch (Exception e)
            {
                lastError = Short(e);
                Debug.LogWarning("[BAM.Online] delete failed: " + e);
                if (serverDone)
                {
                    // the server half is done (name freed, data gone) but Unity still has the player: show it without a name,
                    // so Delete can be tried again and nothing pretends the old name is still ours
                    Name = null;
                    Progress.ResetServer();
                    Set(Status.NeedsName);
                }
                else
                {
                    Set(string.IsNullOrEmpty(Name) ? Status.NeedsName : Status.Ready);
                    Social.Start();
                }
                return false;
            }
        }

        internal static void NameChosen(string name)
        {
            Name = name;
            if (State == Status.NeedsName) Set(Status.Ready);
            else Changed?.Invoke();
        }

        internal static void Set(Status s)
        {
            State = s;
            Changed?.Invoke();
        }

        internal static string Short(Exception e)
        {
            string m = e.Message ?? "";
            if (m.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0)
                return "No connection";
            return m.Length > 80 ? m.Substring(0, 80) : m;
        }
    }

    /// <summary>The phone's own sign-in (Google Play Games on Android).</summary>
    public interface IPlatformSignIn
    {
        /// <summary>interactive = the player pressed a button, so a sign-in screen may show.</summary>
        Task<PlatformSignIn> SignIn(bool interactive);
    }

    public struct PlatformSignIn
    {
        public bool Ok;
        public string ServerCode, DisplayName, Error;
    }
}
