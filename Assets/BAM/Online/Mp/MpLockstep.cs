using System;
using System.Collections.Generic;
using BAM.Sim;

// Lockstep for online matches (2026-10-26, Rickard: "spela med eller mot varandra"). The rules are deterministic (fixed 60 Hz,
// seeded random numbers, our own sine and friends in SimMath), so the phones do not send the match to each other, only the taps:
// every phone runs the very same match from the very same controls. The host is the clock: 60 times a second it puts its own
// controls and the latest of every guest's into a "frame" and sends the frames to the guests; everyone (the host at once, a guest
// when the frame arrives) steps the rules with frame after frame. A guest's own taps therefore show after one trip to the host and
// back (Relay, usually 60-150 ms); a frame is ~10 bytes a player, so a match uses a few kB a second.
// Safety nets: every phone sends a fingerprint of the match (Match.Checksum) every half second and the host compares; a match that
// drifted apart, or a host that is gone, ends the link and each phone plays on with bots in the other people's places (SeatToBot).
// A person who leaves becomes a bot on every phone, at the same tick, through the frame's bot mask.
// No UnityEngine here: the headless test (tools/mptest) runs two of these against each other with the real rules.
namespace BAM.Online.Mp
{
    sealed class MpLockstep
    {
        public const int HashEvery = 30;      // frames between fingerprints (half a second)
        const int Prebuffer = 2;              // a guest that ran dry waits for this many frames, so a late packet costs one pause, not many
        const int Cap = 2048;                 // frames kept: a guest further behind than this (34 s) has lost the match anyway
        const int MaxPresses = 4;

        public readonly bool Host;
        public readonly int Seats, Local;
        /// <summary>Guest: bytes to the host. Host: bytes to every guest.</summary>
        public Action<MpWriter> Send;
        public Action<string> Log;
        /// <summary>Why the link is no longer used (out of sync, host gone); null while it is fine.</summary>
        public string Problem { get; private set; }
        /// <summary>Host: frames are being made (every guest has its match ready).</summary>
        public bool Started { get; private set; }
        public bool Solo => solo;

        readonly PlayerInput[] ring;
        readonly byte[] masks = new byte[Cap];
        long built, stepped;
        readonly MpWriter scratch = new MpWriter(), outMsg = new MpWriter();
        int outCount; long outFirst;

        // host
        readonly PlayerInput[] latest;
        readonly Queue<PlayerInput>[] specQ, spellQ;
        readonly TeamSignal[] sigQ; // a guest's team signal waiting for the next frame
        readonly bool[] gone;
        int pendingMask;
        readonly long[] hashFrame = new long[128];
        readonly ulong[] hashVal = new ulong[128];

        // guest
        bool solo, waiting = true;
        int soloMask;
        readonly Queue<PlayerInput> soloQ = new Queue<PlayerInput>(16);

        public MpLockstep(bool host, int seats, int local)
        {
            Host = host; Seats = seats; Local = local;
            ring = new PlayerInput[Cap * seats];
            latest = new PlayerInput[seats];
            specQ = new Queue<PlayerInput>[seats]; spellQ = new Queue<PlayerInput>[seats]; sigQ = new TeamSignal[seats];
            gone = new bool[seats];
            for (int s = 0; s < seats; s++) { latest[s] = PlayerInput.None; specQ[s] = new Queue<PlayerInput>(MaxPresses); spellQ[s] = new Queue<PlayerInput>(MaxPresses); }
            for (int i = 0; i < hashFrame.Length; i++) hashFrame[i] = -1;
        }

        /// <summary>Frames known and not yet stepped.</summary>
        public int Backlog => (int)(built - stepped);
        public long Stepped => stepped;
        /// <summary>Host: frames made so far, and the first frame of the message Send is sending now.</summary>
        public long Built => built;
        public long OutFirst => outFirst;

        // ---------------------------------------------------------------- this phone's controls, 60 a second

        public void Submit(in PlayerInput own)
        {
            if (Host)
            {
                if (!Started) return;
                Build(MpInput.RoundTrip(in own, scratch));
                return;
            }
            if (solo) { if (soloQ.Count < 32) soloQ.Enqueue(MpInput.RoundTrip(in own, scratch)); return; }
            if (outCount == 0) { outMsg.Begin(MpMsg.In); outMsg.U8(0); }
            MpInput.Write(outMsg, in own);
            outCount++;
            if (outCount >= 60) Flush(); // never more than a second in one message
        }

        /// <summary>Sends what Submit gathered (once a frame, after the ticks).</summary>
        public void Flush()
        {
            if (outCount == 0) return;
            if (Host) outMsg.Data[5] = (byte)outCount; else outMsg.Data[1] = (byte)outCount;
            if (Problem == null) Send?.Invoke(outMsg);
            outCount = 0;
        }

        // ---------------------------------------------------------------- host

        /// <summary>Host: every guest has its match ready, frames start now.</summary>
        public void Start()
        {
            if (!Host || Started) return;
            Started = true;
            for (int s = 0; s < Seats; s++) { latest[s] = PlayerInput.None; specQ[s].Clear(); spellQ[s].Clear(); sigQ[s] = TeamSignal.None; }
        }

        void Build(in PlayerInput own)
        {
            if (built - stepped >= Cap - 1) return; // the host itself fell behind (cannot happen: it steps at once)
            long f = built; int b = (int)(f % Cap) * Seats;
            for (int s = 0; s < Seats; s++)
            {
                if (s == Local) { ring[b + s] = own; continue; }
                var i = PlayerInput.None;
                if (!gone[s])
                {
                    i.MoveX = latest[s].MoveX; i.MoveY = latest[s].MoveY;
                    if (specQ[s].Count > 0) { var p = specQ[s].Dequeue(); i.Special = true; i.AimAngle = p.AimAngle; i.AimK = p.AimK; }
                    if (spellQ[s].Count > 0) { var p = spellQ[s].Dequeue(); i.Spell = true; i.SpellAngle = p.SpellAngle; i.SpellK = p.SpellK; }
                    i.Signal = sigQ[s]; sigQ[s] = TeamSignal.None;
                }
                ring[b + s] = i;
            }
            masks[f % Cap] = (byte)pendingMask; pendingMask = 0;
            built++;
            if (outCount == 0) { outMsg.Begin(MpMsg.Frames); outMsg.I32((int)f); outMsg.U8(0); outFirst = f; }
            outMsg.U8(masks[f % Cap]);
            for (int s = 0; s < Seats; s++) MpInput.Write(outMsg, in ring[b + s]);
            outCount++;
            if (outCount >= 60) Flush();
        }

        /// <summary>Host: writes frames from..(at most 60 on) again for a guest whose link was full, as one Frames message.
        /// Returns the frame after the last one written, or -1 when frame from is no longer kept.</summary>
        public long Refill(MpWriter w, long from)
        {
            if (from < 0 || from >= built || built - from >= Cap - 1) return -1;
            long to = Math.Min(built, from + 60);
            w.Begin(MpMsg.Frames); w.I32((int)from); w.U8((int)(to - from));
            for (long f = from; f < to; f++)
            {
                w.U8(masks[f % Cap]);
                int b = (int)(f % Cap) * Seats;
                for (int s = 0; s < Seats; s++) MpInput.Write(w, in ring[b + s]);
            }
            return to;
        }

        /// <summary>Host: a guest's controls arrived.</summary>
        public void OnIn(int seat, ref MpReader r)
        {
            if (!Host || seat < 0 || seat >= Seats || gone[seat]) return;
            int n = r.U8();
            for (int k = 0; k < n; k++)
            {
                var i = MpInput.Read(ref r);
                latest[seat] = i;
                if (i.Special && specQ[seat].Count < MaxPresses) specQ[seat].Enqueue(i);
                if (i.Spell && spellQ[seat].Count < MaxPresses) spellQ[seat].Enqueue(i);
                if (i.Signal != TeamSignal.None) sigQ[seat] = i.Signal;
            }
        }

        /// <summary>Host: a person left the match; a bot plays their hero from the next frame on, on every phone.</summary>
        public void SeatLeft(int seat)
        {
            if (seat < 0 || seat >= Seats || seat == Local || gone[seat]) return;
            gone[seat] = true; pendingMask |= 1 << seat;
            Log?.Invoke("seat " + seat + " left, a bot plays it from frame " + built);
        }

        /// <summary>Host: a guest's fingerprint of frame f.</summary>
        public void OnHash(int seat, long f, ulong h)
        {
            int slot = (int)(f / HashEvery % hashFrame.Length);
            if (hashFrame[slot] != f || hashVal[slot] == h || Problem != null) return;
            Log?.Invoke("OUT OF SYNC: seat " + seat + " frame " + f + " " + h.ToString("x16") + " vs host " + hashVal[slot].ToString("x16"));
            outMsg.Begin(MpMsg.Desync); outMsg.I32((int)f); outCount = 0;
            Send?.Invoke(outMsg);
            for (int s = 0; s < Seats; s++) if (s != Local) SeatLeft(s);
            Problem = "The match got out of step, bots play on";
        }

        // ---------------------------------------------------------------- guest

        /// <summary>Guest: frames from the host.</summary>
        public void OnFrames(ref MpReader r)
        {
            if (Host || solo) return;
            long first = (uint)r.I32(); int n = r.U8();
            for (int k = 0; k < n; k++)
            {
                long f = first + k;
                int mask = r.U8();
                if (f != built) { for (int s = 0; s < Seats; s++) MpInput.Read(ref r); if (f > built) GoSolo("Frames went missing"); continue; }
                if (built - stepped >= Cap - 1) { GoSolo("Too far behind the host"); return; }
                int b = (int)(f % Cap) * Seats;
                for (int s = 0; s < Seats; s++) ring[b + s] = MpInput.Read(ref r);
                masks[f % Cap] = (byte)mask;
                built++;
            }
        }

        /// <summary>Guest: the host is gone or the match drifted apart. This phone plays on alone, bots in the others' places.</summary>
        public void GoSolo(string why)
        {
            if (Host || solo) return;
            solo = true; Problem = why; outCount = 0;
            outMsg.Begin(MpMsg.LeftMatch); Send?.Invoke(outMsg); // a host still there makes our seat a bot (else our hero runs on with our last input)
            for (int s = 0; s < Seats; s++) if (s != Local) soloMask |= 1 << s;
            Log?.Invoke("playing on with bots: " + why);
        }

        // ---------------------------------------------------------------- stepping

        /// <summary>The next frame's controls into `into` (seat order), and its seats-to-bots done on m. False = not known yet.</summary>
        public bool Next(Match m, PlayerInput[] into)
        {
            if (!Host)
            {
                if (solo && stepped >= built)
                {
                    if (soloQ.Count == 0) return false;
                    int b0 = (int)(built % Cap) * Seats;
                    for (int s = 0; s < Seats; s++) ring[b0 + s] = PlayerInput.None;
                    ring[b0 + Local] = soloQ.Dequeue();
                    masks[built % Cap] = (byte)soloMask; soloMask = 0;
                    built++;
                }
                else if (!solo)
                {
                    long have = built - stepped;
                    if (waiting) { if (have < Prebuffer) return false; waiting = false; }
                    if (have <= 0) { waiting = true; return false; }
                }
            }
            if (stepped >= built) return false;
            int idx = (int)(stepped % Cap), bb = idx * Seats, mask = masks[idx];
            if (mask != 0) for (int s = 0; s < Seats; s++) if ((mask & (1 << s)) != 0) m.SeatToBot(s);
            for (int s = 0; s < Seats && s < into.Length; s++) into[s] = ring[bb + s];
            stepped++;
            return true;
        }

        /// <summary>After each step: the fingerprint every HashEvery frames (the host keeps it, a guest sends it).</summary>
        public void AfterStep(Match m)
        {
            long f = stepped - 1;
            if (f % HashEvery != 0) return;
            ulong h = m.Checksum();
            if (Host) { int slot = (int)(f / HashEvery % hashFrame.Length); hashFrame[slot] = f; hashVal[slot] = h; return; }
            if (solo) return;
            Flush();
            outMsg.Begin(MpMsg.Hash); outMsg.I32((int)f); outMsg.U64(h);
            Send?.Invoke(outMsg);
        }
    }
}
