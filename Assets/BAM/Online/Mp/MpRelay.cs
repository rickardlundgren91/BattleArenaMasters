using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// The bytes between the phones, copied from the old game (Speltest ABC2/Game/NetRelay.cs, read live 2026-10-26) and opened up for
// up to six phones: Unity Relay (a server every phone connects out to, so no router setup) over Unity Transport, found through a
// private Unity Lobby whose code is what the friends type. The lobby only remembers the host's current Relay join code: a phone
// that sleeps for more than ~10 s (switching app to send the code) loses its Relay seat, and the host then makes a new one and puts
// its code in the lobby, so the code the friends have keeps working.
// One reliable, ordered pipeline for everything: lockstep needs every frame in order, and a frame is only a few bytes.
// Needs the player signed in to Unity (BAM.Online.Account, the Google Play Games account).
namespace BAM.Online.Mp
{
    /// <summary>Network test (development builds): a bad mobile network on purpose for the next room (Unity Transport's simulator).</summary>
    public static class MpNetTest
    {
        /// <summary>0 off, else an index in Profiles.</summary>
        public static int Pick;
        public static readonly (string name, int delay, int jitter, int drop)[] Profiles =
        {
            ("Off", 0, 0, 0), ("Good", 20, 5, 0), ("Medium", 50, 20, 1), ("Bad", 100, 50, 3), ("Extreme", 200, 100, 5),
        };
        public static (string name, int delay, int jitter, int drop) Current => Profiles[Math.Max(0, Math.Min(Profiles.Length - 1, Pick))];
    }

    sealed class MpRelay
    {
        const string RelayKey = "relay", TagKey = "v";
        public const int MaxPlayers = 6;
        /// <summary>Only phones with the same messages and the same rules meet (a balance change between versions would desync).</summary>
        internal static string Tag(string kind) => "bam" + kind + "-" + MpMsg.Protocol + "-" + Application.version;
        internal static string QuickTag => Tag("q");

        readonly bool host;
        NetworkDriver driver; NetworkPipeline rel; bool bound;
        readonly List<KeyValuePair<int, NetworkConnection>> conns = new List<KeyValuePair<int, NetworkConnection>>();
        int nextConn = 1;
        string lobbyId, relayCode; float heartbeatT; bool busy, closed, paused;
        NetworkReachability reach;
        byte[] inBuf = new byte[4096];

        public Action<int> OnConnect, OnDisconnect;
        public Action<int, byte[], int> OnData;   // connection, bytes (reused buffer), length
        public Action<string> OnFail, OnStatus;
        /// <summary>The lobby code the friends type (host, once made).</summary>
        public string Code = "";
        public bool IsHost => host;
        public bool Busy => busy;
        /// <summary>ONLINE MATCH: the host's lobby is public, tagged, for two players (MpRoom.QuickMatch); a quick guest joins by id.</summary>
        public bool Quick; public string JoinId; public DateTime Created; public string LobbyId => lobbyId;

        public MpRelay(bool host, string code) { this.host = host; Code = host ? "" : (code ?? "").Trim().ToUpperInvariant(); reach = Application.internetReachability; }

        static bool SignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        public static string Explain(Exception e)
        {
            var m = e.Message ?? "";
            if (e is LobbyServiceException le && le.Reason == LobbyExceptionReason.LobbyFull) return "That room is full.";
            if (e is LobbyServiceException le2 && le2.Reason == LobbyExceptionReason.LobbyNotFound) return "No room with that code. Is the code right, and is your friend's game open?";
            if (e is LobbyServiceException && m.ToLowerInvariant().Contains("not found")) return "No room with that code. Is the code right, and is your friend's game open?";
            if (m.Contains("project") || m.Contains("Project")) return "Online play is not set up for this game yet (Unity Relay and Lobby).";
            return "Could not connect (" + (m.Length > 80 ? m.Substring(0, 80) + "…" : m) + ").";
        }

        public async void Start()
        {
            busy = true;
            try
            {
                if (!SignedIn) { OnFail?.Invoke("Sign in to play online first."); return; }
                if (host)
                {
                    OnStatus?.Invoke("Making a room…");
                    await NewAllocation(); if (closed) return;
                    var data = new Dictionary<string, DataObject> { { RelayKey, new DataObject(DataObject.VisibilityOptions.Member, relayCode) } };
                    if (Quick) data[TagKey] = new DataObject(DataObject.VisibilityOptions.Public, QuickTag, DataObject.IndexOptions.S1);
                    var lobby = await LobbyService.Instance.CreateLobbyAsync(Quick ? "bam-quick" : "bam-room", Quick ? 2 : MaxPlayers, new CreateLobbyOptions { IsPrivate = !Quick, Data = data });
                    lobbyId = lobby.Id; Code = lobby.LobbyCode ?? ""; Created = lobby.Created; if (closed) { LeaveLobby(); return; }
                    OnStatus?.Invoke("");
                }
                else
                {
                    OnStatus?.Invoke("Finding the room…");
                    var lobby = JoinId != null ? await LobbyService.Instance.JoinLobbyByIdAsync(JoinId) : await LobbyService.Instance.JoinLobbyByCodeAsync(Code);
                    lobbyId = lobby.Id; if (closed) { LeaveLobby(); return; }
                    string rc = RelayCode(lobby); if (rc == null) throw new Exception("the room has no Relay code");
                    await Connect(rc);
                }
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] room: " + e); if (!closed) OnFail?.Invoke(Explain(e)); }
            finally { busy = false; }
        }

        /// <summary>Open ONLINE MATCH lobbies of this version, oldest first (not the one with id mine).</summary>
        public static async Task<List<Lobby>> FindOpen(string mine)
        {
            var filters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                new QueryFilter(QueryFilter.FieldOptions.S1, QuickTag, QueryFilter.OpOptions.EQ),
            };
            var r = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 10, Filters = filters, Order = new List<QueryOrder> { new QueryOrder(true, QueryOrder.FieldOptions.Created) } });
            var res = new List<Lobby>();
            if (r != null && r.Results != null) foreach (var l in r.Results) if (l.Id != mine) res.Add(l);
            return res;
        }

        /// <summary>Host: a guest left, so its lobby place is free again (a quick lobby is found by its free places).</summary>
        public void Unseat(string playerId)
        {
            if (!host || lobbyId == null || string.IsNullOrEmpty(playerId)) return;
            try { _ = LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId).ContinueWith(t => { }); } catch (Exception) { }
        }

        static string RelayCode(Lobby l) => l.Data != null && l.Data.TryGetValue(RelayKey, out var d) ? d.Value : null;

        async Task NewAllocation()
        {
            var alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers); // the guests, +1 so one coming back is never refused while its old seat times out
            if (closed) return;
            var ep = Endpoint(alloc.ServerEndpoints);
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, alloc.AllocationIdBytes, alloc.ConnectionData, alloc.ConnectionData, alloc.Key, ep.Secure, false);
            MakeDriver(ref data);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || driver.Listen() != 0) throw new Exception("could not bind to Relay");
            bound = true;
            relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            if (closed) DisposeDriver();
        }

        async Task Connect(string joinCode)
        {
            OnStatus?.Invoke("Connecting…");
            var j = await RelayService.Instance.JoinAllocationAsync(joinCode); if (closed) return;
            var ep = Endpoint(j.ServerEndpoints);
            var data = new RelayServerData(ep.Host, (ushort)ep.Port, j.AllocationIdBytes, j.ConnectionData, j.HostConnectionData, j.Key, ep.Secure, false);
            MakeDriver(ref data);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("could not bind to Relay");
            bound = true; relayCode = joinCode;
            var c = driver.Connect(data.Endpoint); conns.Clear(); conns.Add(new KeyValuePair<int, NetworkConnection>(nextConn++, c));
        }

        static RelayServerEndpoint Endpoint(List<RelayServerEndpoint> eps)
        {
            foreach (var e in eps) if (e.ConnectionType == "dtls") return e; // encrypted
            foreach (var e in eps) if (e.ConnectionType == "udp") return e;
            throw new Exception("no Relay endpoint");
        }

        void MakeDriver(ref RelayServerData data)
        {
            DisposeDriver();
            var s = new NetworkSettings();
            s.WithRelayParameters(ref data);
            s.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 10, disconnectTimeoutMS: 8000); // a phone that is gone: Disconnect after ~8 s
            s.WithFragmentationStageParameters(payloadCapacity: 16 * 1024);
            s.WithReliableStageParameters(windowSize: 64);
            var sim = MpNetTest.Current;
            if (sim.delay > 0 || sim.drop > 0)
            {
                // test builds only: a bad mobile network on purpose (each way, so the round trip gets twice the delay)
                s.WithSimulatorStageParameters(maxPacketCount: 1000, mode: ApplyMode.AllPackets, packetDelayMs: sim.delay, packetJitterMs: sim.jitter, packetDropPercentage: sim.drop);
                driver = NetworkDriver.Create(s);
                rel = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
                Debug.Log("[BAM.Online] network test: " + sim.name);
                return;
            }
            driver = NetworkDriver.Create(s);
            rel = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        }

        void DisposeDriver() { if (driver.IsCreated) driver.Dispose(); bound = false; conns.Clear(); }

        // ---------- every frame ----------
        public void Update()
        {
            // wifi <-> mobile data: the host's Relay seat is probably gone, take a new one at once
            var nr = Application.internetReachability;
            if (nr != reach)
            {
                reach = nr;
                if (nr != NetworkReachability.NotReachable && host && !closed && !paused && !busy && bound && lobbyId != null) Reallocate();
            }
            if (closed || paused || !driver.IsCreated || !bound) { Heartbeat(); return; }
            driver.ScheduleUpdate().Complete();
            if (host && driver.GetRelayConnectionStatus() == RelayConnectionStatus.AllocationInvalid && !busy) { Reallocate(); return; }
            if (host) { NetworkConnection c; while ((c = driver.Accept()) != default(NetworkConnection)) conns.Add(new KeyValuePair<int, NetworkConnection>(nextConn++, c)); }
            for (int i = 0; i < conns.Count; i++)
            {
                var kv = conns[i]; int id = kv.Key; var c = kv.Value; NetworkEvent.Type ev; DataStreamReader reader;
                while ((ev = driver.PopEventForConnection(c, out reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Connect) OnConnect?.Invoke(id);
                    else if (ev == NetworkEvent.Type.Data)
                    {
                        int n = reader.Length;
                        if (n > inBuf.Length) inBuf = new byte[Math.Max(n, inBuf.Length * 2)];
                        var na = new NativeArray<byte>(n, Allocator.Temp); reader.ReadBytes(na); NativeArray<byte>.Copy(na, 0, inBuf, 0, n); na.Dispose();
                        OnData?.Invoke(id, inBuf, n);
                        if (closed || !driver.IsCreated) return;
                    }
                    else if (ev == NetworkEvent.Type.Disconnect)
                    {
                        conns.RemoveAt(i--); OnDisconnect?.Invoke(id);
                        if (closed || !driver.IsCreated) return;
                        break;
                    }
                }
            }
            Heartbeat();
        }

        void Heartbeat()
        {
            if (!host || lobbyId == null || closed || paused) return;
            heartbeatT -= Time.unscaledDeltaTime; if (heartbeatT > 0) return; heartbeatT = 15;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(lobbyId).ContinueWith(t => { });
        }

        // the host slept past Relay's timeout: a new Relay seat, its code into the lobby (a friend joining later finds it there)
        async void Reallocate()
        {
            busy = true;
            try
            {
                var old = conns.ToArray();
                foreach (var kv in old) OnDisconnect?.Invoke(kv.Key);
                await NewAllocation(); if (closed) return;
                await LobbyService.Instance.UpdateLobbyAsync(lobbyId, new UpdateLobbyOptions { Data = new Dictionary<string, DataObject> { { RelayKey, new DataObject(DataObject.VisibilityOptions.Member, relayCode) } } });
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] room: " + e); }
            finally { busy = false; }
        }

        public void Pause(bool p)
        {
            paused = p;
            if (!p && host && lobbyId != null) heartbeatT = 0;
        }

        // ---------- sending ----------
        bool SendOn(NetworkConnection c, MpWriter w)
        {
            if (closed || !driver.IsCreated || c == default(NetworkConnection)) return false;
            if (driver.BeginSend(rel, c, out var dw, w.Len) != 0) return false;
            var na = new NativeArray<byte>(w.Len, Allocator.Temp); NativeArray<byte>.Copy(w.Data, 0, na, 0, w.Len); dw.WriteBytes(na); na.Dispose();
            return driver.EndSend(dw) >= 0;
        }

        /// <summary>Host: to one guest.</summary>
        public bool SendTo(int conn, MpWriter w)
        {
            for (int i = 0; i < conns.Count; i++) if (conns[i].Key == conn) return SendOn(conns[i].Value, w);
            return false;
        }

        /// <summary>Guest: to the host.</summary>
        public bool Send(MpWriter w) => conns.Count > 0 && SendOn(conns[0].Value, w);

        public void Kick(int conn)
        {
            for (int i = 0; i < conns.Count; i++)
                if (conns[i].Key == conn) { driver.ScheduleUpdate().Complete(); driver.Disconnect(conns[i].Value); conns.RemoveAt(i); break; }
        }

        public void Close()
        {
            if (closed) return; closed = true;
            if (driver.IsCreated) { driver.ScheduleUpdate().Complete(); foreach (var kv in conns) driver.Disconnect(kv.Value); driver.ScheduleUpdate().Complete(); }
            DisposeDriver(); LeaveLobby();
        }

        void LeaveLobby()
        {
            if (lobbyId == null) return; var id = lobbyId; lobbyId = null;
            try
            {
                if (host) _ = LobbyService.Instance.DeleteLobbyAsync(id).ContinueWith(t => { });
                else _ = LobbyService.Instance.RemovePlayerAsync(id, AuthenticationService.Instance.PlayerId).ContinueWith(t => { });
            }
            catch (Exception) { }
        }
    }

    /// <summary>The "here I am" signs in Unity Lobby (copy of Speltest NetRelayPresence): one small public lobby per phone.</summary>
    sealed class MpPresenceLobby : IPresenceBackend
    {
        static string Tag => MpRelay.Tag("o");
        static bool SignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
        public string Me { get { try { return SignedIn ? AuthenticationService.Instance.PlayerId : ""; } catch (Exception) { return ""; } } }

        static Dictionary<string, DataObject> Obj(Dictionary<string, string> d)
        {
            var o = new Dictionary<string, DataObject> { { "v", new DataObject(DataObject.VisibilityOptions.Public, Tag, DataObject.IndexOptions.S1) } };
            foreach (var kv in d) o[kv.Key] = new DataObject(DataObject.VisibilityOptions.Public, kv.Value ?? "");
            return o;
        }

        public async Task<string> Create(Dictionary<string, string> data)
        {
            var l = await LobbyService.Instance.CreateLobbyAsync("bam-online", 2, new CreateLobbyOptions { IsPrivate = false, Data = Obj(data) });
            return l.Id;
        }

        public Task Update(string id, Dictionary<string, string> data) => LobbyService.Instance.UpdateLobbyAsync(id, new UpdateLobbyOptions { Data = Obj(data) });
        public Task Heartbeat(string id) => LobbyService.Instance.SendHeartbeatPingAsync(id);

        public async Task<List<PresenceRow>> Query()
        {
            var filters = new List<QueryFilter> { new QueryFilter(QueryFilter.FieldOptions.S1, Tag, QueryFilter.OpOptions.EQ) };
            var r = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 50, Filters = filters, Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.LastUpdated) } });
            var res = new List<PresenceRow>();
            if (r != null && r.Results != null)
                foreach (var l in r.Results)
                {
                    string D(string k) => l.Data != null && l.Data.TryGetValue(k, out var o) ? o.Value ?? "" : "";
                    int.TryParse(D("h"), out int h); int.TryParse(D("im"), out int im); double.TryParse(D("ia"), out double ia);
                    res.Add(new PresenceRow { Id = l.Id, Owner = l.HostId ?? "", Name = D("n"), Hero = h, State = D("s"), InvTo = D("it"), InvCode = D("ic"), InvMode = im, InvAt = ia, Declined = D("dc") });
                }
            return res;
        }

        public void Delete(string id) { try { _ = LobbyService.Instance.DeleteLobbyAsync(id).ContinueWith(t => { }); } catch (Exception) { } }
    }
}
