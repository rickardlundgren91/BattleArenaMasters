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
        // A guest keeps a few frames in hand against the network's unevenness (a jitter buffer). It was a fixed 2 with a jump of up
        // to 4 extra ticks as soon as 4 were waiting: on Rickard's phones (2026-10-29) the guest ran dry ~1 frame in 7 and then leapt,
        // "a few steps, then it jumps and lags, over and over". Now the buffer grows by one each time it runs dry and shrinks again
        // after ~5 s with frames to spare; the game catches up one tick at a time (Excess). Costs a few ticks of extra delay.
        const int MinBuffer = 2, MaxBuffer = 10, ShrinkAfter = 300;
        int buffer = 3, calm, spare = int.MaxValue;
        long waitBuilt = -1; int waitDry; // while refilling: the frame count last seen and the ticks since it last grew
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
        readonly byte[] pickQ; // a guest's item pick waiting for the next frame (Sim/Items.cs)
        readonly byte[] useQ; // a guest's button item (Cloak, Divine Protection) waiting for the next frame
        readonly bool[] gone;
        int pendingMask;
        readonly long[] hashFrame = new long[128];
        readonly ulong[] hashVal = new ulong[128];

        // host: a guest's controls by the tick they are for (guest prediction): used at exactly that frame when they came in time
        const int FutCap = 128;
        readonly PlayerInput[] fut; readonly long[] futT, recvMax;

        // guest
        bool solo, waiting = true;
        // guest prediction (2026-10-31): the tick this phone's next controls are for (it runs ahead of the host by about the trip
        // there), its own controls by tick, and the host's word on how early they arrive (frames' lead, ticks)
        long g;
        readonly PlayerInput[] mine = new PlayerInput[Cap];
        const int NoLead = 127, LeadMin = 1, LeadTarget = 3, LeadMax = 7, AdjustEvery = 20;
        int lead = NoLead, leadSeen, leadUsed, adjWait;
        int shrink, shrinkTick;
        const int StartAhead = 12; // ticks ahead before the host's first word: about a trip there and back on mobile data
        /// <summary>Guest: the game shows a predicted match ahead of the frames (SimMirror), so frames are taken as soon as they come
        /// (no jitter buffer) and the controls carry the tick they are for.</summary>
        public bool Predicting;
        int soloMask;
        readonly Queue<PlayerInput> soloQ = new Queue<PlayerInput>(16);
        // what the shown match was run with, by tick, and its fingerprints: when the host's frames turn out to be exactly that, the
        // shown match already is what the confirmed one will be (same rules, same controls) and need not be copied and run again
        const int GuessCap = 256;
        readonly PlayerInput[] guess; readonly long[] guessAt = new long[GuessCap];
        readonly long[] guessHashAt = new long[16]; readonly ulong[] guessHash = new ulong[16];
        bool missed = true;
        /// <summary>Guest, for the log: frames that kept the shown match, frames that made it be copied again, fingerprints that differed.</summary>
        public int Held, Missed, HashMissed;

        public MpLockstep(bool host, int seats, int local)
        {
            Host = host; Seats = seats; Local = local;
            guess = new PlayerInput[GuessCap * seats];
            for (int i = 0; i < GuessCap; i++) guessAt[i] = -1;
            for (int i = 0; i < guessHashAt.Length; i++) guessHashAt[i] = -1;
            ring = new PlayerInput[Cap * seats];
            latest = new PlayerInput[seats];
            specQ = new Queue<PlayerInput>[seats]; spellQ = new Queue<PlayerInput>[seats]; sigQ = new TeamSignal[seats]; pickQ = new byte[seats]; useQ = new byte[seats];
            gone = new bool[seats];
            for (int s = 0; s < seats; s++) { latest[s] = PlayerInput.None; specQ[s] = new Queue<PlayerInput>(MaxPresses); spellQ[s] = new Queue<PlayerInput>(MaxPresses); }
            for (int i = 0; i < hashFrame.Length; i++) hashFrame[i] = -1;
            fut = new PlayerInput[FutCap * seats]; futT = new long[FutCap * seats]; recvMax = new long[seats];
            for (int i = 0; i < futT.Length; i++) futT[i] = -1;
            for (int s = 0; s < seats; s++) recvMax[s] = -1;
        }

        /// <summary>Frames known and not yet stepped.</summary>
        public int Backlog => (int)(built - stepped);
        /// <summary>Guest: frames beyond the buffer it keeps in hand (the game steps a little faster while this is above 1). Host: 0.</summary>
        public int Excess => Host || solo || waiting ? 0 : Math.Max(0, Backlog - buffer);
        /// <summary>Guest: frames it keeps in hand now.</summary>
        public int Buffer => buffer;
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
            var q = MpInput.RoundTrip(in own, scratch);
            mine[g % Cap] = q;
            if (solo) { if (soloQ.Count < 32) soloQ.Enqueue(q); g++; return; }
            if (outCount == 0) { outMsg.Begin(MpMsg.In); outMsg.U8(0); outMsg.I32((int)g); }
            MpInput.Write(outMsg, in own);
            outCount++; g++;
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
            for (int s = 0; s < Seats; s++) { latest[s] = PlayerInput.None; specQ[s].Clear(); spellQ[s].Clear(); sigQ[s] = TeamSignal.None; pickQ[s] = 0; useQ[s] = 0; }
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
                    // the controls the guest gave for exactly this tick, when they came in time (what its prediction showed)
                    int j = s * FutCap + (int)(f % FutCap);
                    if (futT[j] == f)
                    {
                        var x = fut[j]; futT[j] = -1; latest[s] = x;
                        if (x.Special) { i.Special = true; i.AimAngle = x.AimAngle; i.AimK = x.AimK; }
                        if (x.Spell) { i.Spell = true; i.SpellAngle = x.SpellAngle; i.SpellK = x.SpellK; }
                        i.Signal = x.Signal; i.ItemPick = x.ItemPick; i.ItemUse = x.ItemUse;
                    }
                    i.MoveX = latest[s].MoveX; i.MoveY = latest[s].MoveY;
                    // presses that came too late for their tick: at the next frame
                    if (!i.Special && specQ[s].Count > 0) { var p = specQ[s].Dequeue(); i.Special = true; i.AimAngle = p.AimAngle; i.AimK = p.AimK; }
                    if (!i.Spell && spellQ[s].Count > 0) { var p = spellQ[s].Dequeue(); i.Spell = true; i.SpellAngle = p.SpellAngle; i.SpellK = p.SpellK; }
                    if (i.Signal == TeamSignal.None) { i.Signal = sigQ[s]; sigQ[s] = TeamSignal.None; }
                    if (i.ItemPick == 0) { i.ItemPick = pickQ[s]; pickQ[s] = 0; }
                    if (i.ItemUse == 0) { i.ItemUse = useQ[s]; useQ[s] = 0; }
                }
                ring[b + s] = i;
            }
            masks[f % Cap] = (byte)pendingMask; pendingMask = 0;
            built++;
            if (outCount == 0)
            {
                outMsg.Begin(MpMsg.Frames); outMsg.I32((int)f); outMsg.U8(0); outFirst = f;
                // each seat's lead: how many ticks ahead of this frame its controls have come (the guest keeps it a little above 0)
                for (int s = 0; s < Seats; s++) outMsg.U8(s == Local || recvMax[s] < 0 ? NoLead : (byte)(sbyte)Math.Max(-126, Math.Min(126, recvMax[s] - f)));
            }
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
            for (int s = 0; s < Seats; s++) w.U8(NoLead);
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
            int n = r.U8(); long first = (uint)r.I32();
            for (int k = 0; k < n; k++)
            {
                var i = MpInput.Read(ref r); long t = first + k;
                if (t > recvMax[seat]) recvMax[seat] = t;
                // in time: kept for its own frame
                if (t >= built && t < built + FutCap) { int j = seat * FutCap + (int)(t % FutCap); fut[j] = i; futT[j] = t; continue; }
                latest[seat] = i;
                if (i.Special && specQ[seat].Count < MaxPresses) specQ[seat].Enqueue(i);
                if (i.Spell && spellQ[seat].Count < MaxPresses) spellQ[seat].Enqueue(i);
                if (i.Signal != TeamSignal.None) sigQ[seat] = i.Signal;
                if (i.ItemPick != 0) pickQ[seat] = i.ItemPick;
                if (i.ItemUse != 0) useQ[seat] = i.ItemUse;
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
            for (int s = 0; s < Seats; s++) { int l = r.U8(); if (s == Local && l != NoLead) { lead = (sbyte)l; leadSeen++; } }
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
                    // predicting: this phone's controls are kept by tick, so the confirmed match takes them in step with the shown one
                    // (a queue would hand tick `built` the controls given for the later tick g, the hero a few ticks late ever after)
                    // (one tick behind them: the shown match has always run a tick first, so it is kept, not copied every frame)
                    if (Predicting ? built >= g - 1 || g - built >= Cap : soloQ.Count == 0) return false;
                    int b0 = (int)(built % Cap) * Seats;
                    for (int s = 0; s < Seats; s++) ring[b0 + s] = PlayerInput.None;
                    ring[b0 + Local] = Predicting ? mine[built % Cap] : soloQ.Dequeue();
                    masks[built % Cap] = (byte)soloMask; soloMask = 0;
                    built++;
                }
                else if (!solo && !Predicting)
                {
                    long have = built - stepped;
                    // refilling the buffer: but when no frame has come for twice the buffer, the host has stopped (the match ended there,
                    // or it stalls): play what is in hand. Holding them kept the guest a few ticks short of the end for good (mptest
                    // "2 vs": seat 1 stepped 21595 of 21600; in the game the host stops at Over, so the guest's result never came).
                    if (waiting)
                    {
                        if (have < buffer)
                        {
                            if (built != waitBuilt) { waitBuilt = built; waitDry = 0; return false; }
                            if (have <= 0 || ++waitDry < 2 * buffer) return false;
                        }
                        waiting = false; calm = 0; spare = int.MaxValue; waitBuilt = -1;
                    }
                    if (have <= 0) { waiting = true; if (buffer < MaxBuffer) buffer++; return false; }
                    // a long calm with frames to spare all along: keep one less in hand (less delay)
                    if (have - 1 < spare) spare = (int)(have - 1);
                    if (++calm >= ShrinkAfter) { if (spare >= 2 && buffer > MinBuffer) buffer--; calm = 0; spare = int.MaxValue; }
                }
            }
            if (stepped >= built) return false;
            int idx = (int)(stepped % Cap), bb = idx * Seats, mask = masks[idx];
            if (mask != 0) for (int s = 0; s < Seats; s++) if ((mask & (1 << s)) != 0) m.SeatToBot(s);
            for (int s = 0; s < Seats && s < into.Length; s++) into[s] = ring[bb + s];
            if (Predicting && !Host)
            {
                // the shown match ran this frame with exactly these controls (and no seat became a bot)? else it is copied again
                int gi = (int)(stepped % GuessCap);
                bool same = mask == 0 && guessAt[gi] == stepped;
                for (int s = 0; same && s < Seats && s < into.Length; s++) same = SameInput(in into[s], in guess[gi * Seats + s]);
                if (!same) missed = true;
            }
            stepped++;
            return true;
        }

        /// <summary>Guest: the shown match must be copied from the confirmed one again (a guess was wrong since the last copy). Clears it.</summary>
        public bool TakeMiss()
        {
            bool m = missed; missed = false;
            if (m) Missed++; else Held++;
            return m;
        }

        static bool SameInput(in PlayerInput a, in PlayerInput b) =>
            Bits(a.MoveX) == Bits(b.MoveX) && Bits(a.MoveY) == Bits(b.MoveY) && a.Special == b.Special && Bits(a.AimAngle) == Bits(b.AimAngle)
            && Bits(a.AimK) == Bits(b.AimK) && a.Spell == b.Spell && Bits(a.SpellAngle) == Bits(b.SpellAngle) && Bits(a.SpellK) == Bits(b.SpellK)
            && a.Signal == b.Signal && a.ItemPick == b.ItemPick && a.ItemUse == b.ItemUse;
        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        // ---------------------------------------------------------------- guest prediction

        /// <summary>Guest: the tick this phone's next controls are for. The predicted match is shown up to here.</summary>
        public long Display => g;
        /// <summary>Guest: ticks the shown match is ahead of the confirmed one.</summary>
        public long Ahead => g - stepped;

        /// <summary>Guest: ticks to run more (+) or fewer (-) this frame, so this phone's controls reach the host a little before their
        /// frame. Changes at most once in AdjustEvery frames and only after a new word from the host.</summary>
        public int Adjust()
        {
            if (Host || solo || !Predicting) return 0;
            // no word from the host yet (it is still loading, or its first frames are on their way): run no further ahead than a
            // usual trip, instead of up to MaxAhead and then minutes of shrinking back (bug review NET-5, 2026-11-03)
            if (lead == NoLead) return g - stepped >= StartAhead ? -1000 : 0;
            // too far ahead: one tick less every other frame until the lead is back at the target (2/3 s for 20 ticks; it was one
            // tick per 20 frames, ~8 s of heavy reruns and blinking heroes after an uneven start). New words about the lead are
            // left alone meanwhile: they were sent before the shrinking showed at the host
            if (shrink > 0)
            {
                if ((++shrinkTick & 1) == 0) { if (--shrink == 0) { adjWait = AdjustEvery; leadUsed = leadSeen; } return -1; }
                return 0;
            }
            if (adjWait > 0) { adjWait--; return 0; }
            if (leadSeen == leadUsed) return 0;
            leadUsed = leadSeen;
            if (lead < LeadMin) { adjWait = AdjustEvery; return Math.Min(6, LeadTarget - lead); }
            if (lead > LeadMax) { shrink = lead - LeadTarget; shrinkTick = 0; return 0; }
            return 0;
        }

        /// <summary>Guest: the controls to predict frame f with: this phone's own as given, everyone else's last known movement
        /// (no presses: a special is not guessed).</summary>
        public void Predict(long f, PlayerInput[] into)
        {
            int b = stepped > 0 ? (int)((stepped - 1) % Cap) * Seats : -1;
            for (int s = 0; s < Seats && s < into.Length; s++)
            {
                if (s == Local) { into[s] = f < g && g - f < Cap ? mine[f % Cap] : PlayerInput.None; continue; }
                var i = PlayerInput.None;
                if (b >= 0) { i.MoveX = ring[b + s].MoveX; i.MoveY = ring[b + s].MoveY; }
                into[s] = i;
            }
            int gi = (int)(f % GuessCap);
            guessAt[gi] = f;
            for (int s = 0; s < Seats && s < into.Length; s++) guess[gi * Seats + s] = into[s];
        }

        /// <summary>Guest: the controls confirmed frame f was stepped with (false when f is not stepped yet, or long gone).</summary>
        public bool ConfirmedInputs(long f, PlayerInput[] into)
        {
            if (f < 0 || f >= stepped || stepped - f >= Cap) return false;
            int b = (int)(f % Cap) * Seats;
            for (int s = 0; s < Seats && s < into.Length; s++) into[s] = ring[b + s];
            return true;
        }

        /// <summary>Guest (tests): the controls the shown match last ran frame f with; false when not kept any more.</summary>
        public bool Guess(long f, PlayerInput[] into)
        {
            int gi = (int)(f % GuessCap); if (guessAt[gi] != f) return false;
            for (int s = 0; s < Seats && s < into.Length; s++) into[s] = guess[gi * Seats + s];
            return true;
        }

        /// <summary>Guest: after the shown match stepped frame f: its fingerprint is kept to check against the confirmed one's (a safety
        /// net: if a copy ever leaves something out, the shown match is copied again within half a second, not drifting on).</summary>
        public void PredictedStep(long f, Match shown)
        {
            if (f % HashEvery != 0) return;
            int i = (int)(f / HashEvery % guessHashAt.Length);
            guessHashAt[i] = f; guessHash[i] = shown.Checksum();
        }

        /// <summary>After each step: the fingerprint every HashEvery frames (the host keeps it, a guest sends it).</summary>
        public void AfterStep(Match m)
        {
            long f = stepped - 1;
            if (f % HashEvery != 0) return;
            ulong h = m.Checksum();
            if (Predicting && !Host)
            {
                int i = (int)(f / HashEvery % guessHashAt.Length);
                if (guessHashAt[i] == f && guessHash[i] != h && !missed) { missed = true; HashMissed++; Log?.Invoke("the shown match differed from the confirmed one at frame " + f); }
            }
            if (Host) { int slot = (int)(f / HashEvery % hashFrame.Length); hashFrame[slot] = f; hashVal[slot] = h; return; }
            if (solo) return;
            Flush();
            outMsg.Begin(MpMsg.Hash); outMsg.I32((int)f); outMsg.U64(h);
            Send?.Invoke(outMsg);
        }
    }
}
