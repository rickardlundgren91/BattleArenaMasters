using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Sim;
using UnityEngine;

// The online list and invites as the game uses them (2026-10-27, Rickard: "online lista, möjligheten att bjuda in en vän och köra
// mot bottar"). The old game's NetLobby.InviteTo / AcceptInvite (Speltest ABC2/Game/NetLobby.cs): INVITE makes a room if we are
// not hosting one (TOGETHER, so the friends play side by side against bots), then writes the invite into our sign (MpPresence);
// the friend's ACCEPT joins the room with its code. The host picks the mode and presses START as in any room; bots fill.
namespace BAM.Online.Mp
{
    public static class MpSocial
    {
        const string BlockedKey = "bam.online.blocked", ShowKey = "bam.online.showme";
        static bool started;
        static readonly List<string> pending = new List<string>(); // invited before the room had its code
        static readonly Dictionary<string, string> names = new Dictionary<string, string>();

        /// <summary>Short lines for a toast ("Anna said no thanks").</summary>
        public static event Action<string> Say;

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
        }

        internal static void Tick(float dt)
        {
            if (!started) Start();
            var me = MpPresence.Me;
            me.Name = MpRoom.MyName;
            me.Hero = Mathf.Clamp(BamSettings.LastHero, 0, HeroCatalog.Count - 1);
            bool playing = Flow.Game != null && Flow.Game.InMatch;
            me.State = playing ? PresenceState.Match
                : MpRoom.Quick && MpRoom.State != MpRoom.Phase.Off ? PresenceState.Queue
                : MpRoom.State != MpRoom.Phase.Off ? PresenceState.Room
                : PresenceState.Menu;

            // our invites: the room is gone or playing, or the cards on the other phones have run out
            if (me.InvCode.Length > 0 && (!MpRoom.IsHost || MpRoom.Quick || MpRoom.Code != me.InvCode || MpRoom.State == MpRoom.Phase.Match || MpPresence.InvitesOld))
                MpPresence.ClearInvites();
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

        /// <summary>Is this player invited to our room right now?</summary>
        public static bool IsInvited(string playerId) => MpPresence.Invited.Contains(playerId) || pending.Contains(playerId);

        /// <summary>INVITE: into our room (made now if we have none). Answers null when sent, else why not.</summary>
        public static string Invite(string playerId, string name)
        {
            if (!SignedIn) return "Sign in to play online first.";
            if (!ShowMe) return "Turn on \"Show me as online\" to invite.";
            var r = MpPresence.Find(playerId);
            if (r == null) return (name ?? "They") + " does not have the game open right now.";
            if (!r.Free) return r.Name + " is busy (" + PresenceState.Text(r.State).ToLowerInvariant() + ").";
            names[playerId] = r.Name;
            bool hosting = MpRoom.IsHost && !MpRoom.Quick && (MpRoom.State == MpRoom.Phase.Lobby || MpRoom.State == MpRoom.Phase.Connecting);
            if (!hosting) MpRoom.Host(MpRoom.Together);
            else if (MpRoom.Members.Count + MpPresence.Invited.Count + pending.Count >= MpRelay.MaxPlayers) return "Your room is full.";
            if (MpRoom.State == MpRoom.Phase.Lobby && MpRoom.Code.Length > 0) MpPresence.AddInvite(playerId, MpRoom.Code, MpRoom.Mode);
            else if (!pending.Contains(playerId)) pending.Add(playerId);
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
        public static void Decline(PresenceRow r) => MpPresence.Decline(r, true);

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
