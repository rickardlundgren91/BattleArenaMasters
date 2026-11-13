using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;

namespace BAM.Online
{
    /// <summary>
    /// Friends with Unity Friends (ported from Speltest UgsFriends.cs, 2026-10-26). A friendship belongs to the Unity player ID,
    /// which is the Google Play Games account, so friends follow the player to any phone.
    /// What changed from Speltest: friends are added by the game's unique name (FindPlayer.js turns it into a player ID), not
    /// by "Name#1234"; there is no lobby "here I am" sign, demo friends or party (online matches come in a later version);
    /// presence is only Online / Playing / Offline from the Friends service itself.
    /// </summary>
    public static class Social
    {
        /// <summary>
        /// The game version friends compare: the app version and the online protocol, e.g. "0.2.0 (10)". Every build since the closed
        /// test says 0.2.0, so the protocol (MpWire, set by OnlineHookup) is what tells two builds apart.
        /// </summary>
        public static string GameVersion => Application.version + (Protocol > 0 ? " (" + Protocol + ")" : "");
        public static int Protocol;

        public enum Relation { None, Friends, Incoming, Outgoing, Blocked }
        public enum Presence { Offline, Online, Playing }

        public struct Person
        {
            public string PlayerId, Name;
            public Presence Presence;
            /// <summary>The game version on their phone (from their Friends presence), "" when unknown (older builds send none).</summary>
            public string Version;
        }

        // what we put in our Friends presence: the game version, so a friend's phone can tell why we are missing from its
        // "Online now" list (only phones with the same version can play each other; Rickard 2026-10-30)
        [Serializable, UnityEngine.Scripting.Preserve] public class Activity { public string v; }

        /// <summary>A short note between friends through the Friends service (invites that do not need the online list).</summary>
        [Serializable, UnityEngine.Scripting.Preserve] public class Note { public string t, code, name, v; public int mode; public long at; }
        /// <summary>A note from a friend arrived (their player id, the note).</summary>
        public static event Action<string, Note> NoteIn;

        /// <summary>Sends a note to a friend. False when it could not be sent.</summary>
        public static async Task<bool> SendNote(string playerId, Note n)
        {
            if (!active || string.IsNullOrEmpty(playerId)) return false;
            try { await FriendsService.Instance.MessageAsync(playerId, n); return true; }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] note to friend: " + e.Message); return false; }
        }

        static void OnMessage(IMessageReceivedEvent e)
        {
            try { var n = e.GetAs<Note>(); if (n != null && e.UserId != null && RelationTo(e.UserId) != Relation.Blocked) NoteIn?.Invoke(e.UserId, n); }
            catch (Exception ex) { Debug.LogWarning("[BAM.Online] note from friend: " + ex.Message); }
        }

        /// <summary>This friend, or null.</summary>
        public static Person? FriendById(string playerId)
        {
            foreach (var f in friends) if (f.PlayerId == playerId) return f;
            return null;
        }

        static readonly List<Person> friends = new List<Person>(), incoming = new List<Person>(), outgoing = new List<Person>(), blocked = new List<Person>();
        static readonly Dictionary<string, Relation> relation = new Dictionary<string, Relation>();
        static bool active, starting, dirty, inMatch, sentInMatch;
        static int seenIncoming = -1;

        public static bool Active => active;
        public static IReadOnlyList<Person> Friends => friends;
        public static IReadOnlyList<Person> Incoming => incoming;
        public static IReadOnlyList<Person> Outgoing => outgoing;
        public static IReadOnlyList<Person> Blocked => blocked;
        /// <summary>Raised on the main thread when any list changed.</summary>
        public static event Action Changed;
        /// <summary>Short lines for a toast ("Anna sent you a friend request").</summary>
        public static event Action<string> Say;

        public static Relation RelationTo(string playerId) => playerId != null && relation.TryGetValue(playerId, out var r) ? r : Relation.None;

        internal static void Start() { _ = StartAsync(); }

        // Answers true when Friends is running afterwards. A start already under way is waited for, not doubled.
        static async Task<bool> StartAsync()
        {
            while (starting) await Task.Delay(50);
            if (active) return true;
            if (Account.State != Account.Status.Ready) return false;
            starting = true;
            try
            {
                await FriendsService.Instance.InitializeAsync();
                if (Account.State != Account.Status.Ready) return false;   // signed out or deleted while the service started
                var f = FriendsService.Instance;
                f.RelationshipAdded += OnChange;
                f.RelationshipDeleted += OnChange;
                f.PresenceUpdated += OnChange;
                f.MessageReceived += OnMessage;
                active = true;
                await SyncName();
                await SendPresence();
                presenceAt = Time.realtimeSinceStartup + PresenceEvery; refreshAt = Time.realtimeSinceStartup + RefreshEvery;
                Rebuild();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] friends off: " + e.Message);
                return false;
            }
            finally
            {
                starting = false;
            }
        }

        // Unity Friends shuts itself down when Authentication signs out. Account signs out and in again after the session
        // expired in the background (and before every Play Games sign-in), so Friends could be off while we still said active:
        // every call then failed with "Unable to call FriendsService.AddFriendAsync because the Friends..." (Rickard's phone,
        // 2026-11-02). Account now stops us before it signs out, and a call that still meets a stopped service restarts it once.
        internal static void Stop()
        {
            if (!active) return;
            try
            {
                var f = FriendsService.Instance;
                f.RelationshipAdded -= OnChange;
                f.RelationshipDeleted -= OnChange;
                f.PresenceUpdated -= OnChange;
                f.MessageReceived -= OnMessage;
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] friends stop: " + e.Message); }
            active = false;
            seenIncoming = -1;   // a new account must not toast the old one's requests
            friends.Clear(); incoming.Clear(); outgoing.Clear(); blocked.Clear(); relation.Clear();
            Changed?.Invoke();
        }

        static void OnChange<T>(T e) { dirty = true; }

        /// <summary>Once per frame from the menu (cheap): rebuilds the lists after the service told us something changed.</summary>
        public static void Tick()
        {
            if (!active) return;
            // Bug 2026-11-13 (Rickard and a friend, both with the game open: each saw the other Offline, yet INVITE worked through the
            // online list). The presence was sent once at sign-in, and the friends' presence only changed on a PresenceUpdated event,
            // so a lapsed presence on the service or a missed event (the event link drops in the background) stayed wrong for good.
            // Now our presence is sent again every minute and after the app comes back, and the friends list is fetched again every
            // 30 s while a menu is open.
            float now = Time.realtimeSinceStartup;
            if (now >= presenceAt) { presenceAt = now + PresenceEvery; _ = SendPresence(); }
            if (now >= refreshAt && !refreshing) { refreshAt = now + RefreshEvery; _ = Refresh(); }
            if (dirty) Rebuild();
        }
        const float PresenceEvery = 60, RefreshEvery = 30;
        static float presenceAt, refreshAt;
        static bool refreshing;

        static async Task Refresh()
        {
            refreshing = true;
            try { await FriendsService.Instance.ForceRelationshipsRefreshAsync(); dirty = true; }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] friends refresh: " + e.Message); }
            finally { refreshing = false; }
        }

        /// <summary>The app came back from the background: send our presence and fetch the friends' again at the next Tick.</summary>
        internal static void Resumed() { presenceAt = 0; refreshAt = 0; }

        /// <summary>The game tells friends whether the player is in a match (shown as Playing).</summary>
        public static void SetInMatch(bool playing)
        {
            inMatch = playing;
            if (active && inMatch != sentInMatch) _ = SendPresence();
        }

        // ---------- actions ----------

        /// <summary>Send a friend request to a name. Answers null when sent, else why not.</summary>
        public static async Task<string> Add(string name)
        {
            if (!active && !await StartAsync()) return Offline;
            name = NameRules.Clean(name);
            if (name.Length == 0) return "Type a player name";
            if (string.Equals(name, Account.Name, StringComparison.OrdinalIgnoreCase)) return "That is you";
            string id = await Server.FindPlayer(name);
            if (id == null) return "No player is called " + name;
            if (id == PlayerIdNow) return "That is you";
            return await Run(() => FriendsService.Instance.AddFriendAsync(id), "Friend request sent", "Could not send the request", id);
        }

        public static Task<string> AddById(string playerId) => Run(() => FriendsService.Instance.AddFriendAsync(playerId), "Friend request sent", "Could not send the request", playerId);
        public static Task<string> Accept(string playerId) => Run(() => FriendsService.Instance.AddFriendAsync(playerId), "Friend added", "Could not add friend", playerId);
        public static Task<string> Decline(string playerId) => Run(() => FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId), null, "Could not decline");
        public static Task<string> Cancel(string playerId) => Run(() => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(playerId), null, "Could not cancel");
        public static Task<string> Remove(string playerId) => Run(() => FriendsService.Instance.DeleteFriendAsync(playerId), null, "Could not remove friend");
        public static Task<string> Block(string playerId) => Run(() => FriendsService.Instance.AddBlockAsync(playerId), "Blocked", "Could not block");
        // tells MpSocial too, so the phone's own list of blocked invites (MpPresence.Blocked) lets the player in again; before,
        // an unblocked player's invites and online sign stayed hidden on this phone for good (bug review 2026-11-02, nr 24)
        public static async Task<string> Unblock(string playerId)
        {
            string err = await Run(() => FriendsService.Instance.DeleteBlockAsync(playerId), null, "Could not unblock");
            if (err == null) Unblocked?.Invoke(playerId);
            return err;
        }
        /// <summary>A player was unblocked on the Friends server (the id).</summary>
        public static event Action<string> Unblocked;

        static string PlayerIdNow { get { try { return AuthenticationService.Instance.PlayerId; } catch (Exception) { return null; } } }

        // Friends knows a request or friendship with this player (its own lists, kept up to date by its events)
        static bool Linked(string playerId)
        {
            var s = FriendsService.Instance;
            foreach (var r in s.OutgoingFriendRequests) if (r.Member != null && r.Member.Id == playerId) return true;
            foreach (var r in s.Friends) if (r.Member != null && r.Member.Id == playerId) return true;
            return false;
        }

        // the call is made here, after the check: FriendsService.Instance throws before the service has started.
        // target: an add or accept. The request can reach the other player while the answer to us fails (Rickard 2026-10-29:
        // "Could not send the request", yet FUL got it), so after an error we look whether Friends now knows the link before saying so.
        static async Task<string> Run(Func<Task> call, string ok, string fail, string target = null)
        {
            if (!active && !await StartAsync()) return Offline;
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        await call();
                        if (ok != null) Say?.Invoke(ok);
                        return null;
                    }
                    catch (Exception e) when (attempt == 0 && ServiceStopped(e))
                    {
                        // Friends was shut down under us (a sign-out and in again): start it again and try once more
                        Debug.LogWarning("[BAM.Online] friends restart: " + e.Message);
                        Stop();
                        if (!await StartAsync()) return Offline;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] friends: " + e);
                if (target != null)
                    for (int i = 0; i < 6; i++)
                    {
                        try { if (Linked(target)) { if (ok != null) Say?.Invoke(ok); return null; } } catch (Exception) { }
                        await Task.Delay(250);
                    }
                return Friendly(e, fail);
            }
            finally
            {
                dirty = true;
            }
        }

        const string Offline = "Friends is not available right now. Try again in a moment";

        // the SDK says "Unable to call FriendsService.X because the Friends service is not initialized" (or similar) when it
        // was shut down; the exact words vary between versions, so look for the parts that stay
        static bool ServiceStopped(Exception e)
        {
            string m = e.Message ?? "";
            return m.IndexOf("Unable to call", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("not initialized", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("not been initialized", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("not signed in", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // a short line a player understands, never the raw exception (that goes to the log above)
        static string Friendly(Exception e, string fail)
        {
            string m = (e.Message ?? "").ToLowerInvariant();
            if (ServiceStopped(e)) return Offline;
            if (m.Contains("network") || m.Contains("timeout") || m.Contains("timed out") || m.Contains("connection") || m.Contains("resolve host"))
                return fail + ". Check your internet and try again";
            if (m.Contains("block")) return fail + ". This player is blocked";
            if (m.Contains("limit") || m.Contains("too many") || m.Contains("429")) return fail + ". Too many requests, wait a moment";
            if (m.Contains("already") || m.Contains("exist")) return fail + ". You already have a request or friendship with this player";
            return fail + ". Try again in a moment";
        }

        // ---------- keeping the service up to date ----------

        /// <summary>The Unity player name follows the game name, so friend lists and the leaderboard show it.</summary>
        internal static void NameChanged(string name) { if (active) _ = SyncName(); }

        static async Task SyncName()
        {
            string want = Account.Name;
            if (string.IsNullOrEmpty(want)) return;
            try
            {
                string now = await AuthenticationService.Instance.GetPlayerNameAsync();
                if (Leaderboard.StripTag(now) != want) await AuthenticationService.Instance.UpdatePlayerNameAsync(want);
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] player name: " + e.Message); }
        }

        static async Task SendPresence()
        {
            bool want = inMatch;
            try
            {
                await FriendsService.Instance.SetPresenceAsync(want ? Availability.Busy : Availability.Online, new Activity { v = GameVersion });
                sentInMatch = want;
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] presence: " + e.Message); }
        }

        static void Rebuild()
        {
            dirty = false;
            var s = FriendsService.Instance;
            friends.Clear(); incoming.Clear(); outgoing.Clear(); blocked.Clear(); relation.Clear();
            foreach (var r in s.Friends) Add(friends, r, Relation.Friends);
            foreach (var r in s.IncomingFriendRequests) Add(incoming, r, Relation.Incoming);
            foreach (var r in s.OutgoingFriendRequests) Add(outgoing, r, Relation.Outgoing);
            foreach (var r in s.Blocks) Add(blocked, r, Relation.Blocked);
            friends.Sort((a, b) => a.Presence != b.Presence ? b.Presence.CompareTo(a.Presence) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            if (seenIncoming >= 0 && incoming.Count > seenIncoming) Say?.Invoke(incoming[incoming.Count - 1].Name + " sent you a friend request");
            seenIncoming = incoming.Count;
            Changed?.Invoke();
        }

        static void Add(List<Person> into, Relationship r, Relation kind)
        {
            var m = r.Member;
            if (m == null) return;
            var av = m.Presence != null ? m.Presence.Availability : Availability.Offline;
            string ver = "";
            try { var a = m.Presence != null ? m.Presence.GetActivity<Activity>() : null; if (a != null && a.v != null) ver = a.v; } catch (Exception) { }
            into.Add(new Person
            {
                PlayerId = m.Id,
                Name = Leaderboard.StripTag(m.Profile != null ? m.Profile.Name : ""),
                Presence = av == Availability.Online || av == Availability.Away ? Presence.Online : av == Availability.Busy ? Presence.Playing : Presence.Offline,
                Version = ver,
            });
            relation[m.Id] = kind;
        }
    }
}
