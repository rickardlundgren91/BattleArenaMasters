using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Who else has the game open, and invites (2026-10-27, Rickard: "online lista, möjligheten att bjuda in en vän och köra mot
// bottar"). A copy of the old game's NetPresence.cs (Speltest ABC2/Game, read live 2026-10-27): every signed-in phone keeps a
// small public Unity Lobby of its own as a "here I am" sign (name, hero, what it is doing), tagged with the game version so only
// phones that can play each other are listed. Every few seconds the phone lists the others' signs. An invite is written into the
// inviter's own sign: who it is for (up to five player ids) and the code of the room the inviter just made (MpRoom.Host). The
// invited phone sees it at its next look and shows an ACCEPT card, which joins with that code. NO THANKS writes the code into the
// decliner's sign ("dc"), so the inviter learns at its next look. A sign whose app is closed or asleep stops its heartbeat, and
// Unity Lobby stops listing it within ~30 s.
// What differs from Speltest: invites can go to several people at once (a room holds up to six), party invites are left out.
// No UnityEngine here, so the invite logic is tested headless with a pretend backend (tools/mptest); MpPresenceLobby is the real one.
namespace BAM.Online.Mp
{
    public static class PresenceState
    {
        public const string Menu = "menu", Room = "room", Queue = "queue", Match = "match";
        public static string Text(string s) => s == Menu ? "Online" : s == Room ? "In a room" : s == Queue ? "Looking for a match" : s == Match ? "In a match" : "Online";
    }

    public sealed class PresenceRow
    {
        public string Id = "", Owner = "", Name = "", State = PresenceState.Menu, InvTo = "", InvCode = "", Declined = "";
        public int Hero, InvMode; public double InvAt;
        public bool Free => State == PresenceState.Menu;
        public bool InvitesMe(string me) => !string.IsNullOrEmpty(me) && InvCode.Length > 0 && Array.IndexOf(InvTo.Split(','), me) >= 0;
    }

    /// <summary>What our own sign says.</summary>
    public sealed class PresenceInfo
    {
        public string Name = "", State = PresenceState.Menu, InvTo = "", InvCode = "", Declined = "";
        public int Hero, InvMode; public double InvAt;
        public Dictionary<string, string> Data() => new Dictionary<string, string>
        {
            { "n", Name }, { "h", Hero.ToString() }, { "s", State }, { "it", InvTo }, { "ic", InvCode }, { "im", InvMode.ToString() },
            { "ia", ((long)InvAt).ToString() }, { "dc", Declined },
        };
        public string Key => Name + "|" + Hero + "|" + State + "|" + InvTo + "|" + InvCode + "|" + InvMode + "|" + (long)InvAt + "|" + Declined;
        public void CopyFrom(PresenceInfo o) { Name = o.Name; Hero = o.Hero; State = o.State; InvTo = o.InvTo; InvCode = o.InvCode; InvMode = o.InvMode; InvAt = o.InvAt; Declined = o.Declined; }
    }

    public interface IPresenceBackend
    {
        string Me { get; }                                    // this player's id ("" until signed in)
        Task<string> Create(Dictionary<string, string> data); // our sign; returns its id
        Task Update(string id, Dictionary<string, string> data);
        Task<List<PresenceRow>> Query();                      // the live signs of this version, ours too
        Task Heartbeat(string id);
        void Delete(string id);
    }

    public static class MpPresence
    {
        public static IPresenceBackend Backend;
        /// <summary>The others, free ones first.</summary>
        public static readonly List<PresenceRow> Online = new List<PresenceRow>();
        /// <summary>An invite to us that is still open, or null.</summary>
        public static PresenceRow Invite;
        /// <summary>Why the list is empty ("" = fine).</summary>
        public static string Status = "";
        /// <summary>The list has been fetched at least once.</summary>
        public static bool Looked;
        public const float LookEvery = 4, LookIdle = 6, HeartbeatEvery = 15, InviteTtl = 60;
        /// <summary>The list or the friends are on screen: look every LookEvery s, else every LookIdle s (fewer Lobby queries; was 15 s, Rickard 2026-10-27: an invite took very long to show).</summary>
        public static bool Watching;
        /// <summary>Also blocked (the Friends service's block list), asked by the game.</summary>
        public static Func<string, bool> IsBlocked;
        public const double DeclineQuiet = 120; // after NO THANKS the same sender cannot invite again for this long
        public const int MaxInvites = 5;
        /// <summary>The clock (seconds), replaced in the tests.</summary>
        public static Func<double> Clock = () => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

        // our sign; the game fills it every frame (MpSocial)
        public static readonly PresenceInfo Me = new PresenceInfo();
        static string myLobby, sent; static bool busy; static float lookT, beatT, waitT;
        static readonly HashSet<string> declined = new HashSet<string>();
        static readonly Dictionary<string, double> firstSeen = new Dictionary<string, double>(); // invite code -> when this phone first saw it
        static readonly Dictionary<string, double> declinedAt = new Dictionary<string, double>(); // owner -> when we said no
        /// <summary>The code of the invite we said no to, for our sign (the inviter stops waiting).</summary>
        public static string LastDeclined = "";

        /// <summary>Blocked player ids, kept on this phone (loaded and saved by the game).</summary>
        public static readonly HashSet<string> Blocked = new HashSet<string>();
        public static Action SaveBlocked;
        public static void Block(string owner)
        {
            if (string.IsNullOrEmpty(owner) || !Blocked.Add(owner)) return;
            SaveBlocked?.Invoke();
            Online.RemoveAll(r => r.Owner == owner);
            if (Invite != null && Invite.Owner == owner) Invite = null;
        }

        /// <summary>NO THANKS (tell: the inviter is told) or ACCEPT (tell false: the card just goes away).</summary>
        public static void Decline(PresenceRow r, bool tell = true)
        {
            if (r == null) return;
            if (r.InvCode.Length > 0) declined.Add(r.InvCode);
            if (tell) { LastDeclined = r.InvCode; declinedAt[r.Owner] = Clock(); }
            if (Invite == r) Invite = null;
            if (noted == r) noted = null;
        }

        /// <summary>Seconds left on the invite card.</summary>
        public static int InviteLeft(PresenceRow r) => r == null || !firstSeen.TryGetValue(r.InvCode, out var t0) ? 0 : Math.Max(0, (int)Math.Ceiling(InviteTtl - (Clock() - t0)));

        public static PresenceRow Find(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return null;
            foreach (var r in Online) if (r.Owner == owner) return r;
            return null;
        }

        /// <summary>Every frame: on = signed in and "Show me as online". Keeps our sign up to date and looks at the others' now and then.</summary>
        public static void Tick(float dt, bool on)
        {
            if (Backend == null) return;
            if (!on) { Off(); return; }
            lookT -= dt; beatT -= dt; waitT -= dt;
            Me.Declined = LastDeclined;
            if (Invite != null && InviteLeft(Invite) <= 0) { declined.Add(Invite.InvCode); Invite = null; } // ran out between two looks
            if (busy || waitT > 0) return;
            if (myLobby == null) { var d = Me.Data(); var k = Me.Key; Run(async () => { myLobby = await Backend.Create(d); sent = k; beatT = HeartbeatEvery; lookT = 0; }); return; }
            if (Me.Key != sent) { string id = myLobby, k = Me.Key; var d = Me.Data(); Run(async () => { await Backend.Update(id, d); sent = k; beatT = HeartbeatEvery; }); return; }
            if (beatT <= 0) { beatT = HeartbeatEvery; string id = myLobby; Run(() => Backend.Heartbeat(id)); return; }
            if (lookT <= 0) { lookT = Watching ? LookEvery : LookIdle; Run(async () => Take(await Backend.Query())); }
            else if (Watching && lookT > LookEvery) lookT = LookEvery; // the list was just opened
        }

        /// <summary>Look again at once (the online list was opened).</summary>
        public static void LookSoon() { lookT = 0; }

        public static void Off()
        {
            if (myLobby != null && Backend != null) { try { Backend.Delete(myLobby); } catch (Exception) { } }
            myLobby = null; sent = null; Online.Clear(); Looked = false; Status = "";
            // a friend's note invite does not need our sign: it stays (with the time it was first seen)
            double t0 = noted != null && firstSeen.TryGetValue(noted.InvCode, out var x) ? x : 0;
            firstSeen.Clear();
            if (noted != null && t0 > 0) firstSeen[noted.InvCode] = t0;
            Invite = noted != null && t0 > 0 && !declined.Contains(noted.InvCode) && InviteLeft(noted) > 0 ? noted : null;
            if (Invite == null) noted = null;
        }

        static async void Run(Func<Task> f)
        {
            busy = true;
            try { await f(); Status = ""; }
            catch (Exception e)
            {
                Status = "Could not reach the online list (" + Short(e.Message) + ").";
                waitT = 10;
                if (myLobby != null && e.Message != null && e.Message.ToLowerInvariant().Contains("not found")) { myLobby = null; sent = null; } // our sign expired: make a new one
            }
            finally { busy = false; }
        }

        static string Short(string m) { m = m ?? ""; return m.Length > 60 ? m.Substring(0, 60) + "…" : m; }

        /// <summary>A name from someone else's sign, made safe to show (a changed client can send anything).</summary>
        public static string CleanName(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (!char.IsControl(c) && b.Length < 16) b.Append(c);
            var t = b.ToString().Trim();
            return t.Length == 0 ? "Player" : t;
        }

        // an invite that came as a Friends note (MpSocial), not through the others' signs: kept across looks
        static PresenceRow noted;
        /// <summary>An invite from a friend's note: shown like one seen in their sign (same quiet time, same 60 s card).</summary>
        public static void Offer(PresenceRow r)
        {
            if (r == null || string.IsNullOrEmpty(r.InvCode) || Blocked.Contains(r.Owner) || IsBlocked != null && IsBlocked(r.Owner) || declined.Contains(r.InvCode)) return;
            double now = Clock();
            if (declinedAt.TryGetValue(r.Owner, out var dAt) && now - dAt < DeclineQuiet) return;
            if (!firstSeen.ContainsKey(r.InvCode)) firstSeen[r.InvCode] = now;
            noted = r;
            if (Invite == null && InviteLeft(r) > 0) Invite = r;
        }

        internal static void Take(List<PresenceRow> rows)
        {
            Looked = true; Online.Clear(); Invite = null; string me = Backend.Me; double now = Clock();
            foreach (var r in rows)
            {
                if (r.Owner == me || r.Id == myLobby || Blocked.Contains(r.Owner) || IsBlocked != null && IsBlocked(r.Owner)) continue;
                r.Name = CleanName(r.Name);
                Online.Add(r);
                if (!r.InvitesMe(me) || declined.Contains(r.InvCode)) continue;
                if (declinedAt.TryGetValue(r.Owner, out var dAt) && now - dAt < DeclineQuiet) continue; // said no a moment ago: the same sender cannot ring again at once
                if (Math.Abs(now - r.InvAt) > 600) continue; // a leftover from long ago (the other phone's clock may differ a little, not by minutes)
                if (!firstSeen.ContainsKey(r.InvCode)) firstSeen[r.InvCode] = now;
                if (Invite == null && InviteLeft(r) > 0) Invite = r;
            }
            if (Invite == null && noted != null)
            {
                if (declined.Contains(noted.InvCode) || InviteLeft(noted) <= 0) noted = null;
                else Invite = noted;
            }
            Online.Sort((a, b) => a.Free != b.Free ? (a.Free ? -1 : 1) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        // ---------------------------------------------------------------- our invites

        /// <summary>Whom our sign invites now (player ids), and since when.</summary>
        public static readonly List<string> Invited = new List<string>();

        /// <summary>Invite someone into our room (code). A new code clears the old list, so an older invite cannot join.</summary>
        public static string AddInvite(string owner, string code, int mode)
        {
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(code)) return "Make a room first.";
            if (Me.InvCode != code) Invited.Clear();
            Invited.Remove(owner);
            Invited.Add(owner);
            while (Invited.Count > MaxInvites) Invited.RemoveAt(0);
            Me.InvTo = string.Join(",", Invited); Me.InvCode = code; Me.InvMode = mode; Me.InvAt = Clock();
            return null;
        }

        /// <summary>The room is gone, or the invites ran out: our sign invites no one.</summary>
        public static void ClearInvites() { Invited.Clear(); Me.InvTo = ""; Me.InvCode = ""; Me.InvAt = 0; }

        /// <summary>Did this person say NO THANKS to our current invite (seen at our last look)?</summary>
        public static bool SaidNo(string owner)
        {
            var r = Find(owner);
            return r != null && Me.InvCode.Length > 0 && r.Declined == Me.InvCode;
        }

        /// <summary>Our invites are older than the card on the other phone lasts.</summary>
        public static bool InvitesOld => Me.InvCode.Length > 0 && Clock() - Me.InvAt > InviteTtl + 5;
    }
}
