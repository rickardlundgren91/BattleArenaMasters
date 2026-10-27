using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
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
        public enum Relation { None, Friends, Incoming, Outgoing, Blocked }
        public enum Presence { Offline, Online, Playing }

        public struct Person
        {
            public string PlayerId, Name;
            public Presence Presence;
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

        internal static async void Start()
        {
            if (active || starting || Account.State != Account.Status.Ready) return;
            starting = true;
            try
            {
                await FriendsService.Instance.InitializeAsync();
                if (Account.State != Account.Status.Ready) return;   // signed out or deleted while the service started
                var f = FriendsService.Instance;
                f.RelationshipAdded += OnChange;
                f.RelationshipDeleted += OnChange;
                f.PresenceUpdated += OnChange;
                active = true;
                await SyncName();
                await SendPresence();
                Rebuild();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] friends off: " + e.Message);
            }
            finally
            {
                starting = false;
            }
        }

        internal static void Stop()
        {
            if (!active) return;
            var f = FriendsService.Instance;
            f.RelationshipAdded -= OnChange;
            f.RelationshipDeleted -= OnChange;
            f.PresenceUpdated -= OnChange;
            active = false;
            seenIncoming = -1;   // a new account must not toast the old one's requests
            friends.Clear(); incoming.Clear(); outgoing.Clear(); blocked.Clear(); relation.Clear();
            Changed?.Invoke();
        }

        static void OnChange<T>(T e) { dirty = true; }

        /// <summary>Once per frame from the menu (cheap): rebuilds the lists after the service told us something changed.</summary>
        public static void Tick()
        {
            if (active && dirty) Rebuild();
        }

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
            if (!active) return "Friends is not available right now";
            name = NameRules.Clean(name);
            if (name.Length == 0) return "Type a player name";
            if (string.Equals(name, Account.Name, StringComparison.OrdinalIgnoreCase)) return "That is you";
            string id = await Server.FindPlayer(name);
            if (id == null) return "No player is called " + name;
            return await Run(() => FriendsService.Instance.AddFriendAsync(id), "Friend request sent", "Could not send the request");
        }

        public static Task<string> AddById(string playerId) => Run(() => FriendsService.Instance.AddFriendAsync(playerId), "Friend request sent", "Could not send the request");
        public static Task<string> Accept(string playerId) => Run(() => FriendsService.Instance.AddFriendAsync(playerId), "Friend added", "Could not add friend");
        public static Task<string> Decline(string playerId) => Run(() => FriendsService.Instance.DeleteIncomingFriendRequestAsync(playerId), null, "Could not decline");
        public static Task<string> Cancel(string playerId) => Run(() => FriendsService.Instance.DeleteOutgoingFriendRequestAsync(playerId), null, "Could not cancel");
        public static Task<string> Remove(string playerId) => Run(() => FriendsService.Instance.DeleteFriendAsync(playerId), null, "Could not remove friend");
        public static Task<string> Block(string playerId) => Run(() => FriendsService.Instance.AddBlockAsync(playerId), "Blocked", "Could not block");
        public static Task<string> Unblock(string playerId) => Run(() => FriendsService.Instance.DeleteBlockAsync(playerId), null, "Could not unblock");

        // the call is made here, after the check: FriendsService.Instance throws before the service has started
        static async Task<string> Run(Func<Task> call, string ok, string fail)
        {
            if (!active) return "Friends is not available right now";
            try
            {
                await call();
                if (ok != null) Say?.Invoke(ok);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] friends: " + e.Message);
                return fail;
            }
            finally
            {
                dirty = true;
            }
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
                await FriendsService.Instance.SetPresenceAvailabilityAsync(want ? Availability.Busy : Availability.Online);
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
            into.Add(new Person
            {
                PlayerId = m.Id,
                Name = Leaderboard.StripTag(m.Profile != null ? m.Profile.Name : ""),
                Presence = av == Availability.Online || av == Availability.Away ? Presence.Online : av == Availability.Busy ? Presence.Playing : Presence.Offline,
            });
            relation[m.Id] = kind;
        }
    }
}
