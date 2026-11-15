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
        // guest, in a match: the host is the clock, so a host that went quiet stops the match for everyone. Its app in the background
        // or swiped away (Rickard 2026-11-02: "när man som host lämnar spelet tillfälligt och stänger ner det så fryser det för dom
        // andra") sends nothing, and Relay may not report it gone at all. The host pings every second and sends 60 frames a second:
        // this long without a byte from it and the guest plays on with bots (PAUSE RULE: a match with other people never stands still).
        // 6 s, not 3: the host's own app going to the background is told at once (Pause), so this only catches a killed app or a
        // phone without network, and a guest's own short dead spot (lift, cell handover) must not split the match (review 2026-11-02).
        // This phone without any network: it waits (the host makes it a bot after 8 s anyway) up to HostQuietOffline, then plays on.
        const float HostQuiet = 6f, HostQuietOffline = 15f, HostQuietLoading = 25f; // before the first frame the host may still be loading (LoadWait 15 s)
        static float hostHeardT, framesHeardT; // guest, in a match: the last byte and the last Frames message from the host

        public sealed class Member
        {
            public int Id, Conn = -1, Seat = -1;
            public string Name = "", PlayerId = "", Party = "";
            public int Side; // ONLINE MATCH: 0 the host's group, 1 the group that joined
            public HeroId Hero; public SpellId Spell = SpellId.Blink;
            /// <summary>The look worn on Hero (HeroSkins: 0 plain), sent by the member's own phone, which only sends a look its player
            /// may wear; the others draw the hero in it (Rickard 2026-11-15). Cosmetic: the rules never read it.</summary>
            public int Skin;
            public bool InMatch, Loaded;
            public float Ping = -1;
            public long Behind = -1; // host, in a match: first frame this guest has not been sent (its link was full), -1 = up to date
            public bool DropPending; // host: its seat went to a bot but Dropped could not go out yet (its link was full); Tick tries again
        }

        public static Phase State { get; private set; }
        public static bool IsHost { get; private set; }
        public static string Code => relay != null ? relay.Code : "";
        /// <summary>What is going on, or what went wrong, for the room page.</summary>
        public static string Status { get; private set; } = "";
        public static int Mode { get; private set; } = Together;
        /// <summary>The bots' level in this room: the host picks it in the room and everyone sees it (Rickard 2026-10-30).</summary>
        public static Difficulty Diff { get; private set; } = Difficulty.Normal;
        /// <summary>Heroes per team in this room: 3 (3v3) or 2 (2v2, Rickard 2026-11-15: "gör även så att man kan köra 2v2 med vänner
        /// och mot andra"). The leader picks it for a party; ONLINE MATCH looks only for lobbies of the same size (MpRelay.QuickTag).</summary>
        public static int Size { get; private set; } = 3;
        /// <summary>The arena of this room's matches: the leader's MAP pick for a party. ONLINE MATCH: each side brings its own pick
        /// (quickMap, sent in Hello); the same pick plays it, two different picks 50/50 by the match's seed (Rickard 2026-11-15: "om man
        /// har valt olika kartor är det 50/50. Inte vara runes"). The host decides and sends it in Start, so every phone plays the same.</summary>
        public static MapId Map { get; private set; } = MapId.Runes;
        static MapId quickMap = MapId.Runes, otherMap = MapId.Runes; // ONLINE MATCH: this side's pick, and the side that joined the host's
        public static readonly List<Member> Members = new List<Member>(MpRelay.MaxPlayers);
        public static int MyId { get; private set; }
        public static Member Me { get { foreach (var m in Members) if (m.Id == MyId) return m; return null; } }
        /// <summary>The room page should open when the menus are back (after a match).</summary>
        public static bool WantPage;
        /// <summary>Raised when anything on the room page changed.</summary>
        public static event Action Changed;

        static MpRelay relay;
        static MpMatchNet net;
        /// <summary>A match of this room is running on this phone.</summary>
        internal static bool Playing => net != null;
        static readonly MpWriter w = new MpWriter();
        static int nextId = 1;
        static float pingT, loadT;

        public static int Capacity(int mode) => mode == Together ? Size : 2 * Size;
        /// <summary>ONLINE MATCH: two strangers against each other, bots fill; the room closes after the match.</summary>
        public static bool Quick { get; private set; }
        /// <summary>When the search began (Time.realtimeSinceStartup), for "Looking… 12 s".</summary>
        public static float SearchSince { get; private set; }
        /// <summary>A quick match is about to start (both players are in).</summary>
        public static bool QuickFound { get { if (!Quick || State != Phase.Lobby) return false; foreach (var m in Members) if (m.Side == 1) return true; return false; } }
        /// <summary>ONLINE MATCH: how many are searching together on this phone's side (1 alone).</summary>
        public static int PartySize => partySize;
        /// <summary>ONLINE MATCH: seconds until the host starts the match that was found, -1 when it is not counting down.</summary>
        public static float StartsIn => startT;
        /// <summary>ONLINE MATCH: a lobby was found and this phone is getting into it (State is Connecting).</summary>
        public static bool QuickJoining { get; private set; }
        /// <summary>Why the last ONLINE MATCH search stopped by itself (no server, the lobby closed…), for the error card;
        /// null when it did not, or after a new search, a room, or ClearQuickError.</summary>
        public static string QuickError { get; private set; }
        public static void ClearQuickError() { if (QuickError == null) return; QuickError = null; Changed?.Invoke(); }
        /// <summary>The side (0 or 1) this phone is on in an ONLINE MATCH lobby.</summary>
        public static int MySide { get { var me = Me; return me != null ? me.Side : 0; } }
        static int RoomCap => Quick ? 2 * Size : Capacity(Mode);
        public static int MaxGroup => Size;
        const float QuickStartDelay = 2f, MergeEvery = 3f, PartyWait = 10f;
        /// <summary>ONLINE MATCH: seconds of searching before bots take the empty places (Rickard 2026-11-15, launch plan: a match
        /// within 10-15 s even when nobody else is online). Before this the queue waited for ever, with PLAY BOTS INSTEAD after 30 s.</summary>
        public const float BotFillAfter = 12f;
        static string myParty = "", otherParty; static int partySize = 1, otherSize; static float claimT;
        static int quickTok, quickTries; static float startT = -1, mergeT;
        static readonly HashSet<string> tried = new HashSet<string>();
        public static int TeamOf(int index, int mode) => mode == Together ? Teams.Player : index % 2;
        public static bool CanStart => IsHost && State == Phase.Lobby && Members.Count >= 2 && Members.Count <= RoomCap;
        public static string MyName => !string.IsNullOrEmpty(Account.Name) ? Account.Name : "Player";
        static HeroId MyHero => HeroCatalog.Clean(BamSettings.LastHero);
        static int MySkin(HeroId hero) => BamSettings.SkinOf != null ? Mathf.Clamp(BamSettings.SkinOf(hero), 0, 255) : 0;

        static MpRoom() { BamSettings.SkinPicked += SkinPicked; }

        // a skin picked on Home while in a room: the others see it at once (and in the next match)
        static void SkinPicked()
        {
            var me = Me;
            if (me == null || State != Phase.Lobby) return;
            int skin = MySkin(me.Hero);
            if (skin == me.Skin) return;
            Pick(me.Hero);
        }

        // ---------------------------------------------------------------- the buttons

        /// <summary>Keeps the room pump running (the online list ticks there too); safe to call every frame.</summary>
        public static void Ensure() => MpRunner.Ensure();

        public static void Host(int mode = -1)
        {
            Leave(); QuickError = null;
            if (mode == Together || mode == Versus) Mode = mode;
            IsHost = true; MyId = 0; nextId = 1; State = Phase.Connecting; Status = "Making a room…"; Diff = BamSettings.LastDifficulty;
            Size = BamSettings.TeamSize; Map = BamSettings.LastMap;
            Members.Add(new Member { Id = 0, Name = MyName, Hero = MyHero, Skin = MySkin(MyHero), Spell = BamSettings.Spell });
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
            Leave(); QuickError = null;
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
            Quick = false; QuickJoining = false; ++quickTok; startT = -1; myParty = ""; partySize = 1; otherParty = null;
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
            Leave(); QuickError = null;
            Quick = true; Mode = Versus; quickTries = 0; tried.Clear(); SearchSince = Time.realtimeSinceStartup;
            Size = BamSettings.TeamSize; Map = quickMap = BamSettings.LastMap; // a stranger's lobby of the same size (MpRelay.QuickTag), with this player's map pick
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
            Quick = true; QuickError = null; Mode = Versus; quickTries = 0; tried.Clear(); SearchSince = Time.realtimeSinceStartup;
            quickMap = Map; // the room's team size and map stay: the group looks for a lobby of that size and brings its map pick
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
            CloseRelay(); QuickJoining = false;
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
                var why = MpRelay.Explain(e); Leave(); Status = why; QuickError = why; Changed?.Invoke();
            }
        }

        static void QuickHost()
        {
            IsHost = true; MyId = 0; nextId = 1; mergeT = MergeEvery; otherParty = null; Diff = BamSettings.LastDifficulty;
            Members.Add(new Member { Id = 0, Name = MyName, Hero = MyHero, Skin = MySkin(MyHero), Spell = BamSettings.Spell, Party = myParty });
            relay = new MpRelay(true, null) { Quick = true };
            Wire();
            relay.Start();
        }

        static void QuickJoin(string lobbyId)
        {
            tried.Add(lobbyId);
            IsHost = false; QuickJoining = true;
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

        /// <summary>ONLINE MATCH: seconds until bots take the empty places, -1 when this phone does not decide that (a guest in
        /// someone's lobby, one of a group whose leader searches, a match already found, or not searching).</summary>
        public static float BotFillIn
        {
            get
            {
                if (!Quick || State == Phase.Match || QuickJoining || otherParty != null) return -1;
                if (relay != null && !IsHost) return -1;                       // in a stranger's lobby: its host starts
                if (partySize > 1 && !(IsHost && myParty == MyPlayerId)) return -1; // a group: only its leader
                return Mathf.Max(0, BotFillAfter - (Time.realtimeSinceStartup - SearchSince));
            }
        }

        /// <summary>A group's leader, nobody found in BotFillAfter: the lobby closes to the queue and the group plays against bots
        /// (the same room match as with strangers, the other side all bots). Alone, the menu starts a bot match instead
        /// (OnlineMenu.SearchTick), since a room with one phone gains nothing from Relay.</summary>
        public static void FillWithBots()
        {
            if (!Quick || !IsHost || State != Phase.Lobby || otherParty != null || relay == null || Members.Count < 2) return;
            Debug.Log("[BAM.Online] queue: nobody found in " + BotFillAfter + " s, the group plays bots");
            ++quickTok; startT = -1;
            relay.Quick = false; relay.SetOpen(false);
            StartMatch();
        }

        /// <summary>Host: the bots' level for the next match.</summary>
        public static void SetDiff(Difficulty d)
        {
            if (!IsHost || d == Diff) return;
            Diff = d; BamSettings.LastDifficulty = d; SendRoom();
        }

        /// <summary>Host: 3v3 or 2v2 for the next match (refused while the room holds more players than it fits).</summary>
        public static bool SetSize(int size)
        {
            size = size == 2 ? 2 : 3;
            if (!IsHost || Quick) return false;
            if (size == Size) return true;
            int was = Size; Size = size;
            if (Members.Count > Capacity(Mode)) { Size = was; return false; }
            BamSettings.TeamSize = size; SendRoom();
            return true;
        }

        /// <summary>Host: the arena for the next match.</summary>
        public static void SetMap(MapId map)
        {
            if (!IsHost || Quick || map == Map) return;
            Map = map; BamSettings.LastMap = map; SendRoom();
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
            me.Hero = hero; me.Spell = BamSettings.Spell; me.Skin = MySkin(hero);
            if (IsHost) SendRoom();
            else { w.Begin(MpMsg.Pick); w.U8((int)hero); w.U8((int)me.Spell); w.U8(me.Skin); relay?.Send(w); Changed?.Invoke(); }
        }

        /// <summary>Host: START.</summary>
        public static void StartMatch()
        {
            if (!CanStart) return;
            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            var diff = Diff;
            // ONLINE MATCH with two different map picks: 50/50 by the seed (every phone gets the host's choice in Start)
            var map = Quick && otherParty != null && otherMap != quickMap ? ((seed & 1) == 0 ? quickMap : otherMap) : Map;
            var seats = new SeatConfig[Members.Count];
            var host = Me; if (host != null) host.Skin = MySkin(host.Hero); // the host's own look as it is now
            for (int i = 0; i < Members.Count; i++)
            {
                var m = Members[i];
                m.Seat = i; m.InMatch = true; m.Loaded = m.Conn < 0; m.Behind = -1; m.DropPending = false;
                seats[i] = new SeatConfig { Hero = m.Hero, Team = Quick ? TeamOf(m.Side, Versus) : TeamOf(i, Mode), Spell = m.Spell, Name = m.Name, Skin = m.Skin };
            }
            for (int i = 1; i < Members.Count; i++)
            {
                w.Begin(MpMsg.Start); w.I32(seed); w.U8((int)diff); w.U8(Mode); w.U8(seats.Length);
                for (int s = 0; s < seats.Length; s++) { w.U8((int)seats[s].Hero); w.U8(seats[s].Team); w.U8((int)seats[s].Spell); w.Str(Members[s].Name); }
                w.U8(i); w.U8(Size); w.U8((int)map);
                for (int s = 0; s < seats.Length; s++) w.U8(seats[s].Skin); // last, after the 2v2 fields: every seat's look
                relay.SendTo(Members[i].Conn, w);
            }
            relay.Flush(); // out now, not with the next frame's sends after this phone has built its scene (bug review NET-6)
            loadT = 0;
            Begin(seed, diff, seats, 0, Size, map);
        }

        static void Begin(int seed, Difficulty diff, SeatConfig[] seats, int mySeat, int size, MapId map)
        {
            var cfg = new MatchConfig { Seats = seats, LocalSeat = mySeat, Seed = seed, Difficulty = diff, PlayerHero = seats[mySeat].Hero, PlayerSpell = seats[mySeat].Spell, TeamSize = size, Map = map };
            net = new MpMatchNet(IsHost, cfg, Mode == Versus && seats[mySeat].Team != Teams.Player);
            if (IsHost) net.Lockstep.Send = SendFrames;
            else net.Lockstep.Send = m => relay?.Send(m);
            State = Phase.Match; Status = ""; WantPage = false; QuickJoining = false; hostHeardT = framesHeardT = Time.realtimeSinceStartup;
            Debug.Log("[BAM.Online] online match: " + seats.Length + " players, " + ModeNames[Mode] + ", " + size + "v" + size + ", " + map + ", seat " + mySeat + ", seed " + seed);
            Changed?.Invoke();
            Flow.Game?.StartMatch(new MatchSetup { Hero = seats[mySeat].Hero, Difficulty = diff, Seed = seed, Net = net, TeamSize = size, Map = map });
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
            foreach (var m in Members) { m.InMatch = false; m.Loaded = false; m.DropPending = false; }
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
            if (!IsHost && State == Phase.Match && net != null && !net.Lockstep.Solo
                && Time.realtimeSinceStartup - hostHeardT > (net.Lockstep.Stepped == 0 ? HostQuietLoading
                    : Application.internetReachability == NetworkReachability.NotReachable ? HostQuietOffline : HostQuiet))
            {
                Debug.Log("[BAM.Online] nothing from the host for " + Mathf.RoundToInt(Time.realtimeSinceStartup - hostHeardT) + " s: it is gone");
                OnDisconnect(0);
                return;
            }
            // the host still pings but sends no frames: it gave this seat to a bot (too far behind) and its Dropped has not reached us
            // (its link to us was full then). After the first frame, this long without one and this phone plays on with bots too; the
            // host's pings alone must not keep a dead match waiting (full review 2026-11-09 nr 71)
            if (!IsHost && State == Phase.Match && net != null && !net.Lockstep.Solo && net.Lockstep.Stepped > 0
                && Time.realtimeSinceStartup - framesHeardT > HostQuiet)
            {
                Debug.Log("[BAM.Online] no frames from the host for " + Mathf.RoundToInt(Time.realtimeSinceStartup - framesHeardT) + " s while its pings came: our seat is a bot there");
                net.Lockstep.GoSolo("You fell behind, bots play on");
            }
            pingT += dt;
            if (pingT >= PingEvery)
            {
                pingT = 0;
                w.Begin(MpMsg.Ping); w.F32(Time.realtimeSinceStartup);
                if (IsHost) Broadcast(w, false); else relay.Send(w);
            }
            if (IsHost) foreach (var m in Members) if (m.DropPending && (m.Conn < 0 || SendDropped(m))) m.DropPending = false; // Drop's message, once its link has room
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
                    foreach (var m in Members) if (m.InMatch && !m.Loaded) { Drop(m, "did not load in time"); all = true; }
                if (all) net.Lockstep.Start();
            }
        }

        internal static void Pause(bool p)
        {
            if (p && IsHost && State == Phase.Match && net != null && relay != null)
            {
                // the host's app goes to the background: its match stops, so the others must not wait for it. Tell them now (they
                // play on with bots at once) and make their seats bots here, for when this phone comes back
                w.Begin(MpMsg.MatchOver); Broadcast(w, true); relay.Flush();
                foreach (var m in Members) if (m.InMatch) { m.InMatch = false; m.Behind = -1; net.Lockstep.SeatLeft(m.Seat); }
                Debug.Log("[BAM.Online] the host's app went to the background: the others play on with bots");
            }
            if (!p) hostHeardT = framesHeardT = Time.realtimeSinceStartup; // this phone slept itself: the host's silence was ours
            relay?.Pause(p);
        }

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
            relay.OnFail = s => { if (QuickRetry(s)) return; var keep = s; bool wasQuick = Quick && State != Phase.Match; Leave(); Status = keep; if (wasQuick) QuickError = keep; Changed?.Invoke(); };
            relay.OnConnect = c =>
            {
                if (IsHost) return;
                w.Begin(MpMsg.Hello); w.I32(MpMsg.Protocol); w.Str(Application.version); w.Str(MyPlayerId); w.Str(MyName); w.U8((int)MyHero); w.U8((int)BamSettings.Spell);
                w.Str(Quick ? myParty : ""); w.U8(Quick ? partySize : 1); w.U8(MySkin(MyHero)); w.U8((int)(Quick ? quickMap : Map)); // last: the ONLINE MATCH map pick
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
            if (!IsHost) hostHeardT = Time.realtimeSinceStartup;
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
                    string party = r.Str(); int psize = Math.Max(1, r.U8()); int skin = r.U8(); var hmap = r.U8() == (int)MapId.Grove ? MapId.Grove : MapId.Runes;
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
                            if (psize > MaxGroup || CountSide(0) + psize > 2 * Size) no = "That match is full.";
                            else { otherParty = party; otherSize = psize; otherMap = hmap; claimT = 0; startT = -1; relay.SetOpen(false); }
                        }
                        else if (party != otherParty || CountSide(1) >= otherSize) no = "That match is full.";
                        side = 1;
                    }
                    if (no != null) { w.Begin(MpMsg.Busy); w.Str(no); relay.SendTo(conn, w); relay.Kick(conn); return; }
                    if (m == null) { m = new Member { Id = nextId++, Conn = conn, Party = party, Side = side }; Members.Add(m); }
                    m.PlayerId = pid; m.Name = MpPresence.CleanName(name); m.Hero = HeroCatalog.Clean(hero); m.Spell = (SpellId)spell; m.Skin = skin;
                    w.Begin(MpMsg.Welcome); w.U8(m.Id); relay.SendTo(conn, w);
                    SendRoom();
                    return;
                }
                case MpMsg.Pick:
                    if (m == null) return;
                    m.Hero = HeroCatalog.Clean(r.U8()); m.Spell = (SpellId)r.U8(); m.Skin = r.U8();
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
                case MpMsg.Welcome: MyId = r.U8(); State = Phase.Lobby; Status = ""; quickTries = 0; QuickJoining = false; Changed?.Invoke(); return;
                case MpMsg.Busy: { string s = r.Str(); if (QuickRetry(s)) return; Leave(); Status = s; Changed?.Invoke(); return; }
                case MpMsg.Room:
                {
                    Mode = Math.Min(r.U8(), Versus); int n = Math.Min(r.U8(), MpRelay.MaxPlayers);
                    Members.Clear();
                    for (int i = 0; i < n; i++)
                        Members.Add(new Member { Id = r.U8(), Name = r.Str(), Hero = HeroCatalog.Clean(r.U8()), Spell = (SpellId)r.U8(), InMatch = r.U8() == 1, Ping = r.U16(), Side = r.U8() });
                    bool q = r.U8() == 1; int ps = r.U8(); Diff = (Difficulty)Math.Min(r.U8(), (int)Difficulty.Ultra);
                    Size = r.U8() == 2 ? 2 : 3; Map = r.U8() == (int)MapId.Grove ? MapId.Grove : MapId.Runes;
                    foreach (var mm in Members) mm.Skin = r.U8(); // last: every member's look, in the same order
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
                    for (int s = 0; s < n; s++) { seats[s] = new SeatConfig { Hero = HeroCatalog.Clean(r.U8()), Team = Math.Min(r.U8(), 1), Spell = (SpellId)r.U8() }; seats[s].Name = r.Str(); }
                    int mine = r.U8();
                    int size = r.U8() == 2 ? 2 : 3; var map = r.U8() == (int)MapId.Grove ? MapId.Grove : MapId.Runes;
                    for (int s = 0; s < n; s++) seats[s].Skin = r.U8();
                    if (mine <= 0 || mine >= n) return;
                    // still playing on with bots after the host left a match (its app in the background, EXIT GAME): the host came
                    // back and started a new one. Do not tear this phone's match down (it would count as a quit); our seat is a bot there
                    if (net != null) { w.Begin(MpMsg.LeftMatch); relay?.Send(w); return; }
                    Begin(seed, diff, seats, mine, size, map);
                    return;
                }
                case MpMsg.Frames: framesHeardT = Time.realtimeSinceStartup; net?.Lockstep.OnFrames(ref r); return;
                case MpMsg.Desync: Debug.Log("[BAM.Online] lockstep: OUT OF SYNC (host found it at frame " + (uint)r.I32() + ")"); net?.Lockstep.GoSolo("The match got out of step, bots play on"); return;
                case MpMsg.MatchOver: net?.Lockstep.GoSolo("The host left the match, bots play on"); return;
                case MpMsg.Dropped: net?.Lockstep.GoSolo("You fell behind, bots play on"); return; // the host gave our seat to a bot (nr 71); null net: that match is over here already
                case MpMsg.Pong: { var me = Me; if (me != null) me.Ping = (Time.realtimeSinceStartup - r.F32()) * 1000f; return; }
                case MpMsg.Bye: OnDisconnect(0); return;
                case MpMsg.Move:
                {
                    // our group's host found a waiting lobby: follow it there
                    string id = r.Str(), party = r.Str(); int size = Math.Max(1, r.U8());
                    ++quickTok; CloseRelay();
                    Quick = true; Mode = Versus; myParty = party; partySize = size; quickTries = 0; tried.Clear(); quickMap = Map; // the leader's map pick
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
            w.U8(Quick ? 1 : 0); w.U8(partySize); w.U8((int)Diff); w.U8(Size); w.U8((int)Map);
            foreach (var m in Members) w.U8(m.Skin); // last: every member's look (Rickard 2026-11-15)
            Broadcast(w, false);
            Changed?.Invoke();
        }

        // host, in a match: a guest whose frames can no longer go out (asleep, network gone: Relay's window is full) has lost one
        // for good and cannot follow any more; a bot plays it at once on every phone (it plays on alone when it notices)
        static readonly MpWriter refill = new MpWriter();
        const int MaxBehind = 8 * 60; // frames (8 s, as the disconnect timeout): a guest further behind becomes a bot

        // host, in a match: a guest's seat goes to a bot (too far behind, or too slow to load) and the guest is told, so it plays on
        // with bots at once instead of waiting for frames that never come (its pings still flowed, so no watchdog fired; full review
        // 2026-11-09 nr 71). The link that made it fall behind is full, so the message may not go out now: Tick tries again until it
        // does (the guest's own frames-quiet check in Tick covers the wait). Not a quit: nothing is booked for the guest here or there
        static readonly MpWriter drop = new MpWriter();
        static void Drop(Member m, string why)
        {
            m.InMatch = false; m.Behind = -1;
            net?.Lockstep.SeatLeft(m.Seat);
            m.DropPending = m.Conn >= 0 && !SendDropped(m);
            Debug.Log("[BAM.Online] " + m.Name + " " + why + ", a bot plays on" + (m.DropPending ? " (telling it when its link has room)" : ""));
        }
        static bool SendDropped(Member m) { drop.Begin(MpMsg.Dropped); return relay != null && relay.SendTo(m.Conn, drop); }

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
                    if (to < 0 || ls.Built - m.Behind > MaxBehind) { Drop(m, "fell too far behind"); break; }
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
            Lockstep.Predicting = !host && SimMirror.Available;
            if (!host && !SimMirror.Available) Debug.LogWarning("[BAM.Online] SimCopy.g.cs is out of date: no prediction, the guest plays with the delay (BAM > Online > Regenerate SimCopy)");
        }

        public bool Predicts => Lockstep.Predicting;
        public long Confirmed => Lockstep.Stepped;
        public long Display => Lockstep.Display;
        public int Adjust() => Lockstep.Adjust();
        public void Predict(long f, PlayerInput[] inputs) => Lockstep.Predict(f, inputs);
        public void PredictedStep(long f, Match shown) => Lockstep.PredictedStep(f, shown);
        public bool TakeMiss() => Lockstep.TakeMiss();
        public bool ConfirmedInputs(long f, PlayerInput[] inputs) => Lockstep.ConfirmedInputs(f, inputs);

        public MatchConfig Config => cfg;
        public bool Turned => turned;
        public int Backlog => Lockstep.Excess;
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
                + ", ticks " + Lockstep.Stepped + (Lockstep.Host ? "" : ", buffer " + Lockstep.Buffer) + ", waits " + stalls + " (longest " + Mathf.RoundToInt(longestStall * 1000) + " ms)"
                + ", net test " + MpNetTest.Current.name
                + (Lockstep.Predicting ? ", prediction: kept " + Lockstep.Held + ", copied again " + Lockstep.Missed + ", fingerprint off " + Lockstep.HashMissed
                    + ", ahead " + Lockstep.Ahead : "")
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
