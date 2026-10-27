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
            public string Name = "", PlayerId = "";
            public HeroId Hero; public SpellId Spell = SpellId.Blink;
            public bool InMatch, Loaded;
            public float Ping = -1;
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
        public static bool QuickFound => Quick && State == Phase.Lobby && Members.Count >= 2;
        static int RoomCap => Quick ? 2 : Capacity(Mode);
        const float QuickStartDelay = 2f, MergeEvery = 3f;
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
            Quick = false; ++quickTok; startT = -1;
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
            relay = null; net = null; Members.Clear(); startT = -1;
        }

        // ---------------------------------------------------------------- ONLINE MATCH

        /// <summary>ONLINE MATCH: into the queue. Joins someone who is already waiting, or waits for the next one who searches.</summary>
        public static void QuickMatch()
        {
            Leave();
            Quick = true; Mode = Versus; quickTries = 0; tried.Clear(); SearchSince = Time.realtimeSinceStartup;
            MpRunner.Ensure();
            QuickFind();
        }

        static async void QuickFind()
        {
            int tok = ++quickTok;
            CloseRelay();
            State = Phase.Connecting; Status = "Looking for an opponent…"; Changed?.Invoke();
            try
            {
                var open = await MpRelay.FindOpen(null);
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
            IsHost = true; MyId = 0; nextId = 1; mergeT = MergeEvery;
            Members.Add(new Member { Id = 0, Name = MyName, Hero = MyHero, Spell = BamSettings.Spell });
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
                var open = await MpRelay.FindOpen(mine.LobbyId);
                if (tok != quickTok || relay != mine || Members.Count != 1 || State != Phase.Lobby) return;
                foreach (var o in open)
                    if (!tried.Contains(o.Id) && MpQueue.Older(o.Created, o.Id, mine.Created, mine.LobbyId))
                    {
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
                m.Seat = i; m.InMatch = true; m.Loaded = m.Conn < 0;
                seats[i] = new SeatConfig { Hero = m.Hero, Team = TeamOf(i, Mode), Spell = m.Spell };
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
            if (IsHost) net.Lockstep.Send = m => Broadcast(m, true);
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
            if (Quick && IsHost && State == Phase.Lobby)
            {
                if (Members.Count >= 2) { if (startT < 0) startT = QuickStartDelay; else if ((startT -= dt) <= 0) { startT = -1; StartMatch(); } }
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
                    SendRoom();
                    return;
                }
                return;
            }
            // the host is gone
            if (Quick && State != Phase.Match && net == null) { if (!QuickRetry("the other player left")) { Leave(); Status = "The other player left."; Changed?.Invoke(); } return; }
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
                    string no = proto != MpMsg.Protocol || version != Application.version ? "Your game and your friend's are different versions. Update both from Google Play."
                        : State == Phase.Match ? "A match is being played in that room. Try again when it is over."
                        : (m == null && Members.Count >= RoomCap) || Quick && QuickFound && m == null ? "That room is full." : null;
                    if (no != null) { w.Begin(MpMsg.Busy); w.Str(no); relay.SendTo(conn, w); relay.Kick(conn); return; }
                    if (m == null) { m = new Member { Id = nextId++, Conn = conn }; Members.Add(m); }
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
                    Mode = r.U8(); int n = r.U8();
                    Members.Clear();
                    for (int i = 0; i < n; i++)
                        Members.Add(new Member { Id = r.U8(), Name = r.Str(), Hero = (HeroId)r.U8(), Spell = (SpellId)r.U8(), InMatch = r.U8() == 1, Ping = r.U16() });
                    Changed?.Invoke();
                    return;
                }
                case MpMsg.Start:
                {
                    int seed = r.I32(); var diff = (Difficulty)r.U8(); Mode = r.U8(); int n = r.U8();
                    var seats = new SeatConfig[n];
                    for (int s = 0; s < n; s++) { seats[s] = new SeatConfig { Hero = (HeroId)r.U8(), Team = r.U8(), Spell = (SpellId)r.U8() }; r.Str(); }
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
            }
        }

        static void SendRoom()
        {
            if (!IsHost) return;
            w.Begin(MpMsg.Room); w.U8(Mode); w.U8(Members.Count);
            foreach (var m in Members) { w.U8(m.Id); w.Str(m.Name); w.U8((int)m.Hero); w.U8((int)m.Spell); w.U8(m.InMatch ? 1 : 0); w.U16(Mathf.Clamp((int)m.Ping, 0, 65535)); }
            Broadcast(w, false);
            Changed?.Invoke();
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
    }
}
