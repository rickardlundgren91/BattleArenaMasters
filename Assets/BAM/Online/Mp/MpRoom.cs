using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Sim;
using UnityEngine;

// A room for playing with friends (2026-10-26, Rickard: "kunna spela mot eller med varandra"), the old game's code-match lobby
// (Speltest NetLobby.cs) for up to six phones: CREATE ROOM gives a code, the friends type it and JOIN, the host picks TOGETHER
// (everyone on one team against bots) or AGAINST EACH OTHER (two teams, bots fill the empty places) and presses START. Each player
// plays the hero picked in the room. After the match everyone is back in the room and the host can start again.
// 2026-10-27 (Rickard: "möjligheten att klicka på online match och sättas i en kö"): ONLINE MATCH, the old game's QUICK MATCH
// (Speltest NetLobby.Quick): look for an open quick lobby of this version and join the oldest, or open one and wait; a waiting
// player who finds an older open lobby joins that one instead, so two players who search at the same moment still meet. Two
// players, one on each team, bots fill the rest; it starts by itself. Invites from the online list are in MpSocial.cs.
// 2026-10-27 (Rickard: "1-3 spelare grupp 1 och 1-3 spelare grupp 2 ska kunna spela mot varandra"): a group searches together.
// The one who invited the others presses ONLINE MATCH in the room; the room's lobby then opens to the queue. If another group (or
// a single player) is already waiting, the whole group moves there instead (MpMsg.Move: every phone joins that lobby by id). The
// first group to join a waiting lobby claims it (it goes private again); one group per team, bots fill the empty places.
// No ranking or chat.
namespace BAM.Online.Mp
{
    public static class MpRoom
    {
        public enum Phase { Off, Connecting, Lobby, Match }
        public const int Together = 0, Versus = 1;
        public static readonly string[] ModeNames = { "Together", "Against each other" };
        const float LoadWait = 15f;        // host: seconds to wait for every phone's match before its seat goes to a bot
        const float PingEvery = 1f;

        public sealed class Member
        {
            public int Id, Conn = -1, Seat = -1;
            public string Name = "", PlayerId = "", Party = "";
            public int Side; // ONLINE MATCH: 0 the host's group, 1 the group that joined
            public HeroId Hero; public SpellId Spell = SpellId.Blink;
            public bool InMatch, Loaded;
            public float Ping = -1;
            public long Behind = -1; // host, in a match: first frame this guest has not been sent (its link was full), -1 = up to date
        }

        public static Phase State { get; private set; }
        public static bool IsHost { get; private set; }
        public static string Code => relay != null ? relay.Code : "";
        /// <summary>What is going on, or what went wrong, for the room page.</summary>
        public static string Status { get; private set; } = "";
        public static int Mode { get; private set; } = Together;
        public static readonly List<Member> Members = new List<Member>(MpRelay.MaxPlayers);
        public static int MyId { get; private set; }
        public static Member Me { get { foreach (var m in Members) if (m.Id == MyId) return m; return null; } }
        /// <summary>The room page should open when the menus are back (after a match).</summary>
        public static bool WantPage;
        /// <summary>Raised when anything on the room page changed.</summary>
        public static event Action Changed;

        static MpRelay relay;
        static MpMatchNet net;
        static readonly MpWriter w = new MpWriter();
        static int nextId = 1;
        static float pingT, loadT;

        public static int Capacity(int mode) => mode == Together ? 3 : MpRelay.MaxPlayers;
        /// <summary>ONLINE MATCH: two strangers against each other, bots fill; the room closes after the match.</summary>
        public static bool Quick { get; private set; }
        /// <summary>When the search began (Time.realtimeSinceStartup), for "Looking… 12 s".</summary>
        public static float SearchSince { get; private set; }
        /// <summary>A quick match is about to start (both players are in).</summary>
        public static bool QuickFound { get { if (!Quick || State != Phase.Lobby) return false; foreach (var m in Members) if (m.Side == 1) return true; return false; } }
        /// <summary>ONLINE MATCH: how many are searching together on this phone's side (1 alone).</summary>
        public static int PartySize => partySize;
        /// <summary>The side (0 or 1) this phone is on in an ONLINE MATCH lobby.</summary>
        public static int MySide { get { var me = Me; return me != null ? me.Side : 0; } }
        static int RoomCap => Quick ? MpRelay.MaxPlayers : Capacity(Mode);
        public const int MaxGroup = 3;
        const float QuickStartDelay = 2f, MergeEvery = 3f, PartyWait = 10f;
        static string myParty = "", otherParty; static int partySize = 1, otherSize; static float claimT;
        static int quickTok, quickTries; static float startT = -1, mergeT;
        static readonly HashSet<string> tried = new HashSet<string>();
        public static int TeamOf(int index, int mode) => mode == Together ? Teams.Player : index % 2;
        public static bool CanStart => IsHost && State == Phase.Lobby && Members.Count >= 2 && Members.Count <= RoomCap;
        public static string MyName => !string.IsNullOrEmpty(Account.Name) ? Account.Name : "Player";
        static HeroId MyHero => (HeroId)Mathf.Clamp(BamSettings.LastHero, 0, HeroCatalog.Count - 1);

        // ---------------------------------------------------------------- the buttons

        /// <summary>Keeps the room pump running (the online list ticks there too); safe to call every frame.</summary>
        public static void Ensure() => MpRunner.Ensure();

        public static void Host(int mode = -1)
        {
            Leave();
            if (mode == Together || mode == Versus) Mode = mode;
            IsHost = true; MyId = 0; nextId = 1; State = Phase.Connecting; Status = "Making a room…";
            Members.Add(new Member { Id = 0, Name = MyName, Hero = MyHero, Spell = BamSettings.Spell });
            relay = new MpRelay(true, null);
            Wire();
            relay.Start();
            MpRunner.Ensure();
            Changed?.Invoke();
        }

        public static void Join(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { Status = "Type your friend's room code first."; Changed?.Invoke(); return; }
            Leave();
            IsHost = false; State = Phase.Connecting; Status = "Finding the room…";
            relay = new MpRelay(false, code);
            Wire();
            relay.Start();
            MpRunner.Ensure();
            Changed?.Invoke();
        }

        public static void Leave()
        {
            CloseRelay();
            Quick = false; ++quickTok; startT = -1; myParty = ""; partySize = 1; otherParty = null;
            State = Phase.Off; Status = ""; WantPage = false;
            Changed?.Invoke();
        }

        static void CloseRelay()
        {
            if (relay != null)
            {
                w.Begin(MpMsg.Bye);
                if (IsHost) Broadcast(w, false); else relay.Send(w);
                relay.Close();
            }
            net?.Lockstep.GoSolo("You left the room, bots play on");
            relay = null; net = null; Members.Clear(); startT = -1;
        }

        // ---------------------------------------------------------------- ONLINE MATCH

        /// <summary>ONLINE MATCH: into the queue. Joins someone who is already waiting, or waits for the next one who searches.</summary>
        public static void QuickMatch()
        {
            if (Quick && State != Phase.Off) return; // already looking
            if (!IsHost && State == Phase.Lobby) { Status = "The one who invited you starts ONLINE MATCH for the group."; Changed?.Invoke(); return; }
            if (IsHost && State == Phase.Lobby && Members.Count > 1)
            {
                if (Members.Count > MaxGroup) { Status = "A group for ONLINE MATCH is at most " + MaxGroup + " players."; Changed?.Invoke(); return; }
                GroupMatch(); return;
            }
            Leave();
            Quick = true; Mode = Versus; quickTries = 0; tried.Clear(); SearchSince = Time.realtimeSinceStartup;
            myParty = MyPlayerId; partySize = 1;
            MpRunner.Ensure();
            QuickFind();
        }

        /// <summary>CANCEL while looking: a group searching from its own room goes back to that room; anyone else leaves.</summary>
        public static void CancelSearch()
        {
            if (IsHost && Quick && partySize > 1 && myParty == MyPlayerId && otherParty == null && relay != null && State == Phase.Lobby)
            {
                ++quickTok; Quick = false; myParty = ""; partySize = 1; startT = -1;
                relay.Quick = false; relay.SetOpen(false); Status = ""; SendRoom();
                return;
            }
            Leave();
        }

        // the host of a room with friends in it: the whole group looks for another group (or a single player)
        static async void GroupMatch()
        {
            Quick = true; Mode = Versus; quickTries = 0; tried.Clear(); SearchSince = Time.realtimeSinceStartup;
            myParty = MyPlayerId; partySize = Members.Count; otherParty = null; startT = -1;
            foreach (var m in Members) { m.Party = myParty; m.Side = 0; }
            Status = ""; SendRoom();
            int tok = ++quickTok; var mine = relay;
            try
            {
                var open = await MpRelay.FindOpen(mine?.LobbyId, partySize);
                if (tok != quickTok || relay != mine || relay == null || State != Phase.Lobby) return;
                open.RemoveAll(o => tried.Contains(o.Id));
                if (open.Count > 0) { MoveGroup(open[0].Id); return; }
                // nobody is waiting: our room is the one the next searcher finds
                relay.Quick = true; relay.SetOpen(true); mergeT = MergeEvery;
            }
            catch (Exception e)
            {
                if (tok != quickTok) return;
                Debug.LogWarning("[BAM.Online] queue: " + e);
                Quick = false; myParty = ""; partySize = 1; Status = MpRelay.Explain(e); SendRoom();
            }
        }

        // the group goes over to a waiting lobby: every phone of it joins that lobby by id
        static void MoveGroup(string lobbyId)
        {
            w.Begin(MpMsg.Move); w.Str(lobbyId); w.Str(myParty); w.U8(partySize);
            Broadcast(w, false);
            relay?.Update(); // out before the room closes
            ++quickTok; CloseRelay(); State = Phase.Connecting; Status = "Joining…";
            QuickJoin(lobbyId); Changed?.Invoke();
        }

        static int CountSide(int side) { int n = 0; foreach (var m in Members) if (m.Side == side) n++; return n; }

        // the group that claimed our lobby is gone before the match: open it to the queue again
        static void Reopen()
        {
            otherParty = null; startT = -1; mergeT = MergeEvery;
            relay?.SetOpen(true); SendRoom();
        }

        static async void QuickFind()
        {
            int tok = ++quickTok;
            CloseRelay();
            State = Phase.Connecting; Status = "Looking for an opponent…"; Changed?.Invoke();
            try
            {
                var open = await MpRelay.FindOpen(null, partySize);
                if (tok != quickTok) return;
                open.RemoveAll(o => tried.Contains(o.Id));
                if (open.Count > 0) QuickJoin(open[0].Id); else QuickHost();
            }
            catch (Exception e)
            {
                if (tok != quickTok) return;
                Debug.LogWarning("[BAM.Online] queue: " + e);
                var why = MpRelay.Explain(e); Leave(); Status = why; Changed?.Invoke();
            }
        }

        static void QuickHost()
        {
            IsHost = true; MyId = 0; nextId = 1; mergeT = MergeEvery; otherParty = null;
            Members.Add(new Member { Id = 0, Name = MyName, Hero = MyHero, Spell = BamSettings.Spell, Party = myParty });
            relay = new MpRelay(true, null) { Quick = true };
            Wire();
            relay.Start();
        }

        static void QuickJoin(string lobbyId)
        {
            tried.Add(lobbyId);
            IsHost = false;
            relay = new MpRelay(false, null) { JoinId = lobbyId };
            Wire();
            relay.Start();
        }

        // a waiting quick host: is there an older open lobby? then that one's player waits, and we join it
        static async void CheckMerge()
        {
            var mine = relay; if (mine == null || mine.LobbyId == null) return;
            int tok = quickTok;
            try
            {
                var open = await MpRelay.FindOpen(mine.LobbyId, partySize);
                if (tok != quickTok || relay != mine || otherParty != null || !Quick || State != Phase.Lobby) return;
                foreach (var o in open)
                    if (!tried.Contains(o.Id) && MpQueue.Older(o.Created, o.Id, mine.Created, mine.LobbyId))
                    {
                        if (partySize > 1) { MoveGroup(o.Id); return; }
                        ++quickTok; CloseRelay(); State = Phase.Connecting; QuickJoin(o.Id); Changed?.Invoke();
                        return;
                    }
            }
            catch (Exception) { } // a failed look is simply tried again
        }

        // a quick guest could not get in (the lobby filled up or closed): look again, a few times
        static bool QuickRetry(string why)
        {
            if (!Quick || State == Phase.Match || quickTries++ >= 3) return false;
            if (partySize > 1 && myParty != MyPlayerId) return false; // one of a group that moved: alone it would split the group
            Debug.Log("[BAM.Online] queue: " + why + ", looking again");
            QuickFind();
            return true;
        }

        public static void SetMode(int mode)
        {
            if (!IsHost || mode == Mode || Members.Count > Capacity(mode)) return;
            Mode = mode; SendRoom();
        }

        /// <summary>This player picked another hero (or battle spell) in the room.</summary>
        public static void Pick(HeroId hero)
        {
            BamSettings.LastHero = (int)hero;
            var me = Me; if (me == null) return;
            me.Hero = hero; me.Spell = BamSettings.Spell;
            if (IsHost) SendRoom();
            else { w.Begin(MpMsg.Pick); w.U8((int)hero); w.U8((int)me.Spell); relay?.Send(w); Changed?.Invoke(); }
        }

        /// <summary>Host: START.</summary>
        public static void StartMatch()
        {
            if (!CanStart) return;
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            var diff = BamSettings.LastDifficulty;
            var seats = new SeatConfig[Members.Count];
            for (int i = 0; i < Members.Count; i++)
            {
                var m = Members[i];
                m.Seat = i; m.InMatch = true; m.Loaded = m.Conn < 0; m.Behind = -1;
                seats[i] = new SeatConfig { Hero = m.Hero, Team = Quick ? TeamOf(m.Side, Versus) : TeamOf(i, Mode), Spell = m.Spell };
            }
            for (int i = 1; i < Members.Count; i++)
            {
                w.Begin(MpMsg.Start); w.I32(seed); w.U8((int)diff); w.U8(Mode); w.U8(seats.Length);
                for (int s = 0; s < seats.Length; s++) { w.U8((int)seats[s].Hero); w.U8(seats[s].Team); w.U8((int)seats[s].Spell); w.Str(Members[s].Name); }
                w.U8(i);
                relay.SendTo(Members[i].Conn, w);
            }
            loadT = 0;
            Begin(seed, diff, seats, 0);
        }

        static void Begin(int seed, Difficulty diff, SeatConfig[] seats, int mySeat)
        {
            var cfg = new MatchConfig { Seats = seats, LocalSeat = mySeat, Seed = seed, Difficulty = diff, PlayerHero = seats[mySeat].Hero, PlayerSpell = seats[mySeat].Spell };
            net = new MpMatchNet(IsHost, cfg, Mode == Versus && seats[mySeat].Team != Teams.Player);
            if (IsHost) net.Lockstep.Send = SendFrames;
            else net.Lockstep.Send = m => relay?.Send(m);
            State = Phase.Match; Status = ""; WantPage = false;
            Debug.Log("[BAM.Online] online match: " + seats.Length + " players, " + ModeNames[Mode] + ", seat " + mySeat + ", seed " + seed);
            Changed?.Invoke();
            Flow.Game?.StartMatch(new MatchSetup { Hero = seats[mySeat].Hero, Difficulty = diff, Seed = seed, Net = net });
            if (!IsHost) { w.Begin(MpMsg.Loaded); relay?.Send(w); }
        }

        /// <summary>The match is over on this phone (or this player left it).</summary>
        internal static void MatchEnded(MpMatchNet n, bool quit)
        {
            if (n != net) return;
            // a match with a player on the other team counts for Rating as a match against players; the game reports it right
            // after this (GameFlow.SendEnd), and Progress takes the seed as the match's name both sides send to the server
            var cfg = n.Config; int myTeam = cfg.Seats[cfg.LocalSeat].Team; bool rival = false;
            foreach (var s in cfg.Seats) if (s.Team != myTeam) rival = true;
            Progress.NextVs = rival ? "v" + cfg.Seed : null; Progress.NextTeam = myTeam;
            if (quit && relay != null)
            {
                w.Begin(IsHost ? MpMsg.MatchOver : MpMsg.LeftMatch);
                if (IsHost) Broadcast(w, true); else relay.Send(w);
            }
            net = null;
            foreach (var m in Members) { m.InMatch = false; m.Loaded = false; }
            if (Quick) { Leave(); return; } // a stranger: done, back to the menu (as the old QUICK MATCH)
            if (relay != null) { State = Phase.Lobby; WantPage = true; }
            else { State = Phase.Off; WantPage = true; Status = "The room has closed."; }
            if (IsHost) SendRoom();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- every frame (MpRunner)

        internal static void Tick(float dt)
        {
            if (relay == null) return;
            relay.Update();
            if (relay == null) return;
            pingT += dt;
            if (pingT >= PingEvery)
            {
                pingT = 0;
                w.Begin(MpMsg.Ping); w.F32(Time.realtimeSinceStartup);
                if (IsHost) Broadcast(w, false); else relay.Send(w);
            }
            if (Quick && IsHost && State == Phase.Lobby && relay.Quick)
            {
                if (otherParty != null)
                {
                    // the other group is in (or most of it after PartyWait): start
                    claimT += dt; int them = CountSide(1);
                    if (them == 0) { if (claimT > PartyWait) Reopen(); }
                    else if (them >= otherSize || claimT > PartyWait) { if (startT < 0) startT = QuickStartDelay; else if ((startT -= dt) <= 0) { startT = -1; StartMatch(); } }
                }
                else { startT = -1; if ((mergeT -= dt) <= 0) { mergeT = MergeEvery; CheckMerge(); } }
            }
            if (IsHost && net != null && !net.Lockstep.Started)
            {
                loadT += dt;
                bool all = true;
                foreach (var m in Members) if (m.InMatch && !m.Loaded) all = false;
                if (!all && loadT >= LoadWait)
                    foreach (var m in Members) if (m.InMatch && !m.Loaded) { m.InMatch = false; net.Lockstep.SeatLeft(m.Seat); all = true; }
                if (all) net.Lockstep.Start();
            }
        }

        internal static void Pause(bool p) => relay?.Pause(p);

        /// <summary>For the HUD: who the host is still waiting for.</summary>
        internal static string Waiting()
        {
            string s = null;
            foreach (var m in Members) if (m.InMatch && !m.Loaded) s = s == null ? m.Name : s + ", " + m.Name;
            return s;
        }

        // ---------------------------------------------------------------- messages

        static void Wire()
        {
            relay.OnStatus = s => { if (!string.IsNullOrEmpty(s)) Status = s; else if (IsHost) { State = Phase.Lobby; Status = ""; } Changed?.Invoke(); };
            relay.OnFail = s => { if (QuickRetry(s)) return; var keep = s; Leave(); Status = keep; Changed?.Invoke(); };
            relay.OnConnect = c =>
            {
                if (IsHost) return;
                w.Begin(MpMsg.Hello); w.I32(MpMsg.Protocol); w.Str(Application.version); w.Str(MyPlayerId); w.Str(MyName); w.U8((int)MyHero); w.U8((int)BamSettings.Spell);
                w.Str(Quick ? myParty : ""); w.U8(Quick ? partySize : 1);
                relay.Send(w);
            };
            relay.OnDisconnect = OnDisconnect;
            relay.OnData = OnData;
        }

        static string MyPlayerId { get { try { return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId ?? ""; } catch (Exception) { return ""; } } }

        static void OnDisconnect(int conn)
        {
            if (IsHost)
            {
                for (int i = 0; i < Members.Count; i++)
                {
                    var m = Members[i]; if (m.Conn != conn) continue;
                    if (m.InMatch && net != null) net.Lockstep.SeatLeft(m.Seat);
                    relay?.Unseat(m.PlayerId);
                    Members.RemoveAt(i);
                    if (Quick && otherParty != null && State == Phase.Lobby && CountSide(1) == 0) { Reopen(); return; }
                    SendRoom();
                    return;
                }
                return;
            }
            // the host is gone
            if (Quick && State != Phase.Match && net == null) { if (!QuickRetry("the other player left")) { Leave(); Status = partySize > 1 ? "The match fell through. Invite your friends again." : "The other player left."; Changed?.Invoke(); } return; }
            if (net != null) net.Lockstep.GoSolo("The host left, bots play on");
            relay?.Close(); relay = null;
            if (State != Phase.Match) { State = Phase.Off; Members.Clear(); Status = "The room has closed."; }
            Changed?.Invoke();
        }

        static Member ByConn(int conn) { foreach (var m in Members) if (m.Conn == conn) return m; return null; }

        static void OnData(int conn, byte[] data, int len)
        {
            var r = new MpReader(data, len);
            int type = r.U8();
            if (type == MpMsg.Ping) { float t = r.F32(); w.Begin(MpMsg.Pong); w.F32(t); if (IsHost) relay.SendTo(conn, w); else relay.Send(w); return; }
            if (IsHost) HostData(conn, type, ref r); else GuestData(type, ref r);
        }

        static void HostData(int conn, int type, ref MpReader r)
        {
            var m = ByConn(conn);
            switch (type)
            {
                case MpMsg.Hello:
                {
                    int proto = r.I32(); string version = r.Str(), pid = r.Str(), name = r.Str(); int hero = r.U8(), spell = r.U8();
                    string party = r.Str(); int psize = Math.Max(1, r.U8());
                    if (string.IsNullOrEmpty(party)) party = pid;
                    string no = proto != MpMsg.Protocol || version != Application.version ? "Your game and your friend's are different versions. Update both from Google Play."
                        : State == Phase.Match ? "A match is being played in that room. Try again when it is over."
                        : m == null && Members.Count >= RoomCap ? "That room is full." : null;
                    int side = 0;
                    if (no == null && Quick && m == null && party != myParty)
                    {
                        // ONLINE MATCH: the first group to come claims this lobby, later ones look elsewhere
                        if (otherParty == null)
                        {
                            if (psize > MaxGroup || CountSide(0) + psize > MpRelay.MaxPlayers) no = "That match is full.";
                            else { otherParty = party; otherSize = psize; claimT = 0; startT = -1; relay.SetOpen(false); }
                        }
                        else if (party != otherParty || CountSide(1) >= otherSize) no = "That match is full.";
                        side = 1;
                    }
                    if (no != null) { w.Begin(MpMsg.Busy); w.Str(no); relay.SendTo(conn, w); relay.Kick(conn); return; }
                    if (m == null) { m = new Member { Id = nextId++, Conn = conn, Party = party, Side = side }; Members.Add(m); }
                    m.PlayerId = pid; m.Name = MpPresence.CleanName(name); m.Hero = (HeroId)Mathf.Clamp(hero, 0, HeroCatalog.Count - 1); m.Spell = (SpellId)spell;
                    w.Begin(MpMsg.Welcome); w.U8(m.Id); relay.SendTo(conn, w);
                    SendRoom();
                    return;
                }
                case MpMsg.Pick:
                    if (m == null) return;
                    m.Hero = (HeroId)Mathf.Clamp(r.U8(), 0, HeroCatalog.Count - 1); m.Spell = (SpellId)r.U8();
                    SendRoom();
                    return;
                case MpMsg.Bye:
                    if (m != null) { relay.Kick(conn); OnDisconnect(conn); }
                    return;
                case MpMsg.Pong:
                    if (m != null) { m.Ping = (Time.realtimeSinceStartup - r.F32()) * 1000f; Changed?.Invoke(); }
                    return;
                case MpMsg.Loaded: if (m != null) m.Loaded = true; return;
                case MpMsg.In: if (m != null && net != null && m.InMatch) net.Lockstep.OnIn(m.Seat, ref r); return;
                case MpMsg.Hash: if (m != null && net != null) { long f = (uint)r.I32(); net.Lockstep.OnHash(m.Seat, f, r.U64()); } return;
                case MpMsg.LeftMatch:
                    if (m != null && m.InMatch) { m.InMatch = false; net?.Lockstep.SeatLeft(m.Seat); SendRoom(); }
                    return;
            }
        }

        static void GuestData(int type, ref MpReader r)
        {
            switch (type)
            {
                case MpMsg.Welcome: MyId = r.U8(); State = Phase.Lobby; Status = ""; quickTries = 0; Changed?.Invoke(); return;
                case MpMsg.Busy: { string s = r.Str(); if (QuickRetry(s)) return; Leave(); Status = s; Changed?.Invoke(); return; }
                case MpMsg.Room:
                {
                    Mode = Math.Min(r.U8(), Versus); int n = Math.Min(r.U8(), MpRelay.MaxPlayers);
                    Members.Clear();
                    for (int i = 0; i < n; i++)
                        Members.Add(new Member { Id = r.U8(), Name = r.Str(), Hero = (HeroId)Mathf.Clamp(r.U8(), 0, HeroCatalog.Count - 1), Spell = (SpellId)r.U8(), InMatch = r.U8() == 1, Ping = r.U16(), Side = r.U8() });
                    bool q = r.U8() == 1; int ps = r.U8();
                    if (q && !Quick) SearchSince = Time.realtimeSinceStartup; // our group's host started looking
                    Quick = q; if (q && ps > 0) partySize = Math.Max(partySize, ps); if (!q && partySize > 1) partySize = 1;
                    Changed?.Invoke();
                    return;
                }
                case MpMsg.Start:
                {
                    int seed = r.I32(); var diff = (Difficulty)r.U8(); Mode = Math.Min(r.U8(), Versus); int n = r.U8();
                    if (n > MpRelay.MaxPlayers) return;
                    var seats = new SeatConfig[n];
                    for (int s = 0; s < n; s++) { seats[s] = new SeatConfig { Hero = (HeroId)Mathf.Clamp(r.U8(), 0, HeroCatalog.Count - 1), Team = Math.Min(r.U8(), 1), Spell = (SpellId)r.U8() }; r.Str(); }
                    int mine = r.U8();
                    if (mine <= 0 || mine >= n) return;
                    Begin(seed, diff, seats, mine);
                    return;
                }
                case MpMsg.Frames: net?.Lockstep.OnFrames(ref r); return;
                case MpMsg.Desync: Debug.Log("[BAM.Online] lockstep: OUT OF SYNC (host found it at frame " + (uint)r.I32() + ")"); net?.Lockstep.GoSolo("The match got out of step, bots play on"); return;
                case MpMsg.MatchOver: net?.Lockstep.GoSolo("The host left the match, bots play on"); return;
                case MpMsg.Pong: { var me = Me; if (me != null) me.Ping = (Time.realtimeSinceStartup - r.F32()) * 1000f; return; }
                case MpMsg.Bye: OnDisconnect(0); return;
                case MpMsg.Move:
                {
                    // our group's host found a waiting lobby: follow it there
                    string id = r.Str(), party = r.Str(); int size = Math.Max(1, r.U8());
                    ++quickTok; CloseRelay();
                    Quick = true; Mode = Versus; myParty = party; partySize = size; quickTries = 0; tried.Clear();
                    State = Phase.Connecting; Status = "Joining…";
                    QuickJoin(id); Changed?.Invoke();
                    return;
                }
            }
        }

        static void SendRoom()
        {
            if (!IsHost) return;
            w.Begin(MpMsg.Room); w.U8(Mode); w.U8(Members.Count);
            foreach (var m in Members) { w.U8(m.Id); w.Str(m.Name); w.U8((int)m.Hero); w.U8((int)m.Spell); w.U8(m.InMatch ? 1 : 0); w.U16(Mathf.Clamp((int)m.Ping, 0, 65535)); w.U8(m.Side); }
            w.U8(Quick ? 1 : 0); w.U8(partySize);
            Broadcast(w, false);
            Changed?.Invoke();
        }

        // host, in a match: a guest whose frames can no longer go out (asleep, network gone: Relay's window is full) has lost one
        // for good and cannot follow any more; a bot plays it at once on every phone (it plays on alone when it notices)
        static readonly MpWriter refill = new MpWriter();
        const int MaxBehind = 8 * 60; // frames (8 s, as the disconnect timeout): a guest further behind becomes a bot

        static void SendFrames(MpWriter msg)
        {
            if (relay == null || net == null) return;
            var ls = net.Lockstep;
            bool frames = msg.Len > 0 && msg.Data[0] == MpMsg.Frames;
            foreach (var m in Members)
            {
                if (m.Conn < 0 || !m.InMatch) continue;
                if (!frames) { relay.SendTo(m.Conn, msg); continue; }
                // a full link (a guest on a slow network for a moment) loses no frame: it is sent again, in order, once there is room;
                // sending the newer frames first would make the guest give up ("Frames went missing")
                if (m.Behind < 0 && !relay.SendTo(m.Conn, msg)) m.Behind = ls.OutFirst;
                while (m.Behind >= 0)
                {
                    long to = ls.Refill(refill, m.Behind);
                    if (to < 0 || ls.Built - m.Behind > MaxBehind) { m.Behind = -1; m.InMatch = false; ls.SeatLeft(m.Seat); Debug.Log("[BAM.Online] " + m.Name + " fell too far behind, a bot plays on"); break; }
                    if (!relay.SendTo(m.Conn, refill)) break;
                    m.Behind = to >= ls.Built ? -1 : to;
                }
            }
        }

        static void Broadcast(MpWriter msg, bool inMatchOnly)
        {
            if (relay == null) return;
            foreach (var m in Members) if (m.Conn >= 0 && (!inMatchOnly || m.InMatch)) relay.SendTo(m.Conn, msg);
        }
    }

    /// <summary>One online match as the game's driver sees it (BAM.Core.IMatchNet).</summary>
    sealed class MpMatchNet : IMatchNet
    {
        internal readonly MpLockstep Lockstep;
        readonly MatchConfig cfg;
        readonly bool turned;
        bool ended;
        float dryT, problemT = -1;
        int stalls; float longestStall; // for the log at the end: how often this phone had to wait for the next frame
        string shownProblem;

        public MpMatchNet(bool host, MatchConfig config, bool turned)
        {
            cfg = config; this.turned = turned;
            Lockstep = new MpLockstep(host, config.Seats.Length, config.LocalSeat) { Log = s => Debug.Log("[BAM.Online] lockstep: " + s) };
        }

        public MatchConfig Config => cfg;
        public bool Turned => turned;
        public int Backlog => Lockstep.Backlog;
        public void Submit(in PlayerInput input) => Lockstep.Submit(in input);
        public bool Next(Match m, PlayerInput[] inputs)
        {
            bool ok = Lockstep.Next(m, inputs);
            if (ok) dryT = 0;
            else
            {
                if (dryT == 0 && Lockstep.Stepped > 0) stalls++;
                dryT += Rules.Tick;
                if (dryT > longestStall) longestStall = dryT;
            }
            return ok;
        }
        public void AfterStep(Match m) => Lockstep.AfterStep(m);
        public void Flush() => Lockstep.Flush();

        public string Status
        {
            get
            {
                var p = Lockstep.Problem;
                if (p != null)
                {
                    if (p != shownProblem) { shownProblem = p; problemT = Time.unscaledTime; }
                    return Time.unscaledTime - problemT < 5f ? p : null;
                }
                if (Lockstep.Host && !Lockstep.Started) { var who = MpRoom.Waiting(); return who != null ? "Waiting for " + who + "…" : null; }
                if (!Lockstep.Host && dryT > 0.75f) return Lockstep.Stepped == 0 ? "Waiting for the host…" : "Connection is slow…";
                return null;
            }
        }

        public void MatchEnded(bool quit)
        {
            if (ended) return;
            ended = true;
            // one line to compare between phones after a test: same version and seed, how far it got, waits, and why it stopped being shared
            Debug.Log("[BAM.Online] match over: version " + Application.version + ", seed " + cfg.Seed + ", seat " + cfg.LocalSeat + "/" + cfg.Seats.Length
                + ", ticks " + Lockstep.Stepped + ", waits " + stalls + " (longest " + Mathf.RoundToInt(longestStall * 1000) + " ms)"
                + ", net test " + MpNetTest.Current.name
                + (Lockstep.Problem != null ? ", problem: " + Lockstep.Problem : "") + (quit ? ", left" : ""));
            MpRoom.MatchEnded(this, quit);
        }
    }

    /// <summary>Pumps the room every frame, also between matches (one hidden object, made the first time a room opens).</summary>
    sealed class MpRunner : MonoBehaviour
    {
        static MpRunner inst;
        internal static void Ensure()
        {
            if (inst != null) return;
            var go = new GameObject("BAM.Online.Mp");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<MpRunner>();
        }
        void Update() { MpRoom.Tick(Time.unscaledDeltaTime); MpSocial.Tick(Time.unscaledDeltaTime); }
        void OnApplicationPause(bool p) => MpRoom.Pause(p);
        void OnApplicationQuit() { MpRoom.Leave(); MpPresence.Off(); }
    }
}
