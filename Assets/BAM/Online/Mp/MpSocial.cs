using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Sim;
using UnityEngine;

// The online list and invites as the game uses them (2026-10-27, Rickard: "online lista, möjligheten att bjuda in en vän och köra
// mot bottar"). The old game's NetLobby.InviteTo / AcceptInvite (Speltest ABC2/Game/NetLobby.cs): INVITE makes a room if we are
// not hosting one (in the mode picked on the page: TOGETHER or AGAINST EACH OTHER), then writes the invite into our sign (MpPresence);
// the friend's ACCEPT joins the room with its code. The host picks the mode and presses START as in any room; bots fill.
namespace BAM.Online.Mp
{
    public static class MpSocial
    {
        const string BlockedKey = "bam.online.blocked", ShowKey = "bam.online.showme";
        static bool started;
        static readonly List<string> pending = new List<string>(); // invited before the room had its code
        static readonly Dictionary<string, string> names = new Dictionary<string, string>();
        // friends are also invited with a Friends note (Rickard 2026-10-30: FUL online in Friends but missing from Online now, so
        // no INVITE): the note carries the room code, so it works whatever the online list sees. id -> the room code it was sent for
        static readonly Dictionary<string, string> noted = new Dictionary<string, string>();
        static readonly List<string> notePending = new List<string>();

        /// <summary>Short lines for a toast ("Anna said no thanks").</summary>
        public static event Action<string> Say;
        /// <summary>The lockstep protocol (MpWire), for Social.GameVersion.</summary>
        public static int Protocol => MpMsg.Protocol;

        /// <summary>"Show me as online": others see this phone in their list and can invite it.</summary>
        public static bool ShowMe
        {
            get => PlayerPrefs.GetInt(ShowKey, 1) != 0;
            set { PlayerPrefs.SetInt(ShowKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static bool SignedIn => Account.State == Account.Status.Ready;

        static void Start()
        {
            started = true;
            MpPresence.Backend = new MpPresenceLobby();
            foreach (var id in (PlayerPrefs.GetString(BlockedKey, "") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) MpPresence.Blocked.Add(id);
            MpPresence.IsBlocked = id => Social.RelationTo(id) == Social.Relation.Blocked;
            MpPresence.SaveBlocked = () => { PlayerPrefs.SetString(BlockedKey, string.Join(",", MpPresence.Blocked)); PlayerPrefs.Save(); };
            Social.Unblocked += MpPresence.Unblock; // bug review 2026-11-02, nr 24
            Social.NoteIn += OnNote;
        }

        static void OnNote(string from, Social.Note n)
        {
            if (n.t == "inv" && !string.IsNullOrEmpty(n.code))
            {
                string who = MpPresence.CleanName(n.name);
                if (!string.IsNullOrEmpty(n.v) && n.v != Social.GameVersion) { Say?.Invoke(who + " invited you, but has game version " + n.v + " (you have " + Social.GameVersion + ")"); return; }
                var me = MpPresence.Backend != null ? MpPresence.Backend.Me : "";
                MpPresence.Offer(new PresenceRow { Id = "note", Owner = from, Name = who, InvCode = n.code, InvMode = n.mode, InvAt = n.at, InvTo = me });
            }
            else if (n.t == "no" && n.code == MpRoom.Code && noted.TryGetValue(from, out var c) && c == n.code)
            {
                noted.Remove(from);
                Say?.Invoke((names.TryGetValue(from, out var nm) ? nm : "Your friend") + " said no thanks");
            }
        }

        static void SendInviteNote(string id)
        {
            noted[id] = MpRoom.Code;
            _ = Social.SendNote(id, new Social.Note { t = "inv", code = MpRoom.Code, mode = MpRoom.Mode, name = MpRoom.MyName, v = Social.GameVersion, at = (long)MpPresence.Clock() });
        }

        internal static void Tick(float dt)
        {
            if (!started) Start();
            var me = MpPresence.Me;
            me.Name = MpRoom.MyName;
            me.Hero = (int)HeroCatalog.Clean(BamSettings.LastHero);
            bool playing = Flow.Game != null && Flow.Game.InMatch;
            me.State = playing ? PresenceState.Match
                : MpRoom.Quick && MpRoom.State != MpRoom.Phase.Off ? PresenceState.Queue
                : MpRoom.State != MpRoom.Phase.Off ? PresenceState.Room
                : PresenceState.Menu;

            // our invites: the room is gone or playing, or the cards on the other phones have run out
            if (me.InvCode.Length > 0 && (!MpRoom.IsHost || MpRoom.Quick || MpRoom.Code != me.InvCode || MpRoom.State == MpRoom.Phase.Match || MpPresence.InvitesOld))
                MpPresence.ClearInvites();
            if (MpRoom.State == MpRoom.Phase.Off || MpRoom.Quick || !MpRoom.IsHost) { notePending.Clear(); noted.Clear(); }
            else if (notePending.Count > 0 && MpRoom.State == MpRoom.Phase.Lobby && MpRoom.Code.Length > 0)
            {
                foreach (var id in notePending) SendInviteNote(id);
                notePending.Clear();
            }
            if (pending.Count > 0)
            {
                if (MpRoom.State == MpRoom.Phase.Off || MpRoom.Quick) pending.Clear();
                else if (MpRoom.IsHost && MpRoom.State == MpRoom.Phase.Lobby && MpRoom.Code.Length > 0)
                {
                    foreach (var id in pending) MpPresence.AddInvite(id, MpRoom.Code, MpRoom.Mode);
                    pending.Clear();
                }
            }
            for (int i = MpPresence.Invited.Count - 1; i >= 0; i--)
            {
                var id = MpPresence.Invited[i];
                if (!MpPresence.SaidNo(id)) continue;
                MpPresence.Invited.RemoveAt(i);
                me.InvTo = string.Join(",", MpPresence.Invited);
                Say?.Invoke((names.TryGetValue(id, out var n) ? n : "Your friend") + " said no thanks");
            }

            MpPresence.Tick(dt, SignedIn && ShowMe);
        }

        /// <summary>Has this player the game open (same version), and what are they doing? null when not.</summary>
        public static PresenceRow Find(string playerId) => MpPresence.Find(playerId);

        /// <summary>An invite to our room is still waiting for an answer (Home's PLAY waits for it).</summary>
        public static bool Inviting => MpPresence.Invited.Count > 0 || pending.Count > 0 || notePending.Count > 0;

        /// <summary>Is this player invited to our room right now?</summary>
        public static bool IsInvited(string playerId) => MpPresence.Invited.Contains(playerId) || pending.Contains(playerId)
            || notePending.Contains(playerId) || noted.TryGetValue(playerId, out var c) && c == MpRoom.Code;

        /// <summary>Can INVITE be offered for this player: free in the online list, or a friend the Friends service shows online.</summary>
        public static bool CanInvite(string playerId)
        {
            var r = MpPresence.Find(playerId);
            if (r != null) return r.Free;
            var f = Social.FriendById(playerId);
            return f != null && f.Value.Presence == Social.Presence.Online;
        }

        /// <summary>INVITE: into our room (made now if we have none). Answers null when sent, else why not.</summary>
        public static string Invite(string playerId, string name, int mode = MpRoom.Together)
        {
            if (!SignedIn) return "Sign in to play online first.";
            var r = MpPresence.Find(playerId);
            var friend = Social.FriendById(playerId);
            if (r != null && !r.Free) return r.Name + " is busy (" + PresenceState.Text(r.State).ToLowerInvariant() + ").";
            if (friend != null)
            {
                var f = friend.Value;
                if (r == null && f.Presence == Social.Presence.Playing) return f.Name + " is in a match.";
                if (r == null && f.Presence == Social.Presence.Offline) return f.Name + " does not have the game open right now.";
                if (!string.IsNullOrEmpty(f.Version) && f.Version != Social.GameVersion) return f.Name + " has game version " + f.Version + ", you have " + Social.GameVersion + ". Both need the same version from Google Play.";
            }
            else
            {
                if (r == null) return (name ?? "They") + " does not have the game open right now.";
                if (!ShowMe) return "Turn on \"Show me as online\" to invite.";
            }
            if (MpRoom.Quick && MpRoom.State != MpRoom.Phase.Off) return "You are looking for a match. Cancel first to invite.";
            // a guest in someone's party: Host() below would leave that party without a word (bug review 2026-11-02, nr 28);
            // the same words as the invite panel shows a guest
            if (MpRoom.State != MpRoom.Phase.Off && !MpRoom.IsHost)
                return "Only the host can invite. Ask " + (MpRoom.Members.Count > 0 ? MpRoom.Members[0].Name : "the host") + " to invite more.";
            names[playerId] = r != null ? r.Name : friend.Value.Name;
            bool hosting = MpRoom.IsHost && !MpRoom.Quick && (MpRoom.State == MpRoom.Phase.Lobby || MpRoom.State == MpRoom.Phase.Connecting);
            if (!hosting) MpRoom.Host(mode);
            else if (MpRoom.Members.Count + MpPresence.Invited.Count + pending.Count >= MpRelay.MaxPlayers) return "Your room is full.";
            bool ready = MpRoom.State == MpRoom.Phase.Lobby && MpRoom.Code.Length > 0;
            if (r != null && ShowMe) { if (ready) MpPresence.AddInvite(playerId, MpRoom.Code, MpRoom.Mode); else if (!pending.Contains(playerId)) pending.Add(playerId); }
            if (friend != null) { if (ready) SendInviteNote(playerId); else if (!notePending.Contains(playerId)) notePending.Add(playerId); }
            return null;
        }

        /// <summary>ACCEPT on the invite card: into the inviter's room.</summary>
        public static void Accept(PresenceRow r)
        {
            if (r == null) return;
            MpPresence.Decline(r, false);
            MpRoom.Join(r.InvCode);
        }

        /// <summary>NO THANKS: the inviter is told at its next look.</summary>
        public static void Decline(PresenceRow r)
        {
            if (r == null) return;
            MpPresence.Decline(r, true);
            if (Social.FriendById(r.Owner) != null) _ = Social.SendNote(r.Owner, new Social.Note { t = "no", code = r.InvCode });
        }

        /// <summary>Never see this player in the list or get invites from them again (on this phone).</summary>
        public static void Block(PresenceRow r)
        {
            if (r == null) return;
            MpPresence.Decline(r, true);
            MpPresence.Block(r.Owner);
            _ = Social.Block(r.Owner); // on the Friends server too, so it follows the account to another phone
        }
    }
}
