using System;
using System.Text;
using BAM.Sim;

// The bytes of an online match (no UnityEngine here, so the lockstep can be tested headless with the rules in .NET).
namespace BAM.Online.Mp
{
    static class MpMsg
    {
        // room
        public const byte Hello = 1, Welcome = 2, Busy = 3, Room = 4, Pick = 5, Start = 6, Bye = 7, Ping = 8, Pong = 9, Move = 10;
        // match
        public const byte Loaded = 20, In = 21, Frames = 22, Hash = 23, Desync = 24, LeftMatch = 25, MatchOver = 26;
        /// <summary>Phones with another protocol never meet (bump it whenever a message or the rules' input changes).</summary>
        public const int Protocol = 20; // 2: groups in ONLINE MATCH (party in Hello, side in Room, Move); 3: Simon's reworks change the rules (8d996f0), so a 2 build would desync; 4: balance (Tyra, Tikta, Knut numbers); 5: Hilde and Oaken (heroes 9 and 10); 6: backdoor protection (half tower damage without a wave); 7: smaller start speed boost, Attack/Defend signals; 8: Fenn 2.0 (Pack Hunt, Shadow Escape) and the thrown wolf's 6-tile cap, plus the Precision & Polish rule fixes (Tyra, Bram, Knut, Bonk, chain map); 9: Precision round 2 (aim, Blast chain walls, Bram bounce, Knut hook reach, stun cuts wind-ups, hidden Tikta warning, checksum fields); 10: a blink or recall makes shots on their way miss (SHOT-08); 11: bodies glide along walls and round the middle boulders instead of sticking (Movement.Slide), and Bonk's boulder bowls on into more enemies (BonkRoll, pins knock their neighbours); 12: smarter bots (teammates fall back only after a real fight, Normal/Hard count the enemy's ready special and never Recall), which run on both phones; 13: knockback cooldown (Rules.KnockCooldown, no new knockback for 2 s after one) and Bonk's bowling capped (3 enemies per throw, the boulder 2); 14: hero balance 2026-10-30 (Defs numbers for eight heroes, Knut's slows no longer stack between Knuts); 15: Blast's special swapped (tap = fire strip at a random enemy, drag = the bomb where it is aimed); 16: the bots' level in the Room message (the host picks it in the room); 17: exact wall test for sight lines and shots (Arena.LineClear), so a shot never slips a wall corner the aim stops at; 18: a hero's regen is stopped only by fights with heroes and towers, never by minions (Unit.LastFight); 19: Tyra's drag bolt flies until it hits an enemy, a tower or a wall, or leaves the arena (TyraS.Flight); 20: a hero's regen starts 2 s after the last hero or tower fight instead of 3 s (Rules.HeroRegenDelay), and a hit reveals in grass for 2 s instead of 4 (Rules.RevealTime)
    }

    static class MpQueue
    {
        /// <summary>ONLINE MATCH: is lobby a older than lobby b? The younger one's player leaves it and joins the older (same time: the smaller id wins).</summary>
        public static bool Older(DateTime aCreated, string aId, DateTime bCreated, string bId) =>
            aCreated < bCreated || aCreated == bCreated && string.CompareOrdinal(aId, bId) < 0;
    }

    /// <summary>A growing byte buffer that is reused, so sending allocates nothing.</summary>
    sealed class MpWriter
    {
        public byte[] Data = new byte[1024];
        public int Len;
        public MpWriter Begin(byte type) { Len = 0; U8(type); return this; }
        void Room(int n) { if (Len + n > Data.Length) Array.Resize(ref Data, Math.Max(Data.Length * 2, Len + n)); }
        public void U8(int v) { Room(1); Data[Len++] = (byte)v; }
        public void U16(int v) { Room(2); Data[Len++] = (byte)v; Data[Len++] = (byte)(v >> 8); }
        public void I32(int v) { Room(4); for (int i = 0; i < 4; i++) Data[Len++] = (byte)(v >> (8 * i)); }
        public void U64(ulong v) { Room(8); for (int i = 0; i < 8; i++) Data[Len++] = (byte)(v >> (8 * i)); }
        public void F32(float v) => I32(BitConverter.SingleToInt32Bits(v));
        public void Str(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            int n = Math.Min(b.Length, 255); U8(n); Room(n); Buffer.BlockCopy(b, 0, Data, Len, n); Len += n;
        }
    }

    /// <summary>Reads one message. Reading past the end gives zeros (a short message from an older phone never throws).</summary>
    struct MpReader
    {
        readonly byte[] d; int p; readonly int end;
        public MpReader(byte[] data, int len) { d = data; p = 0; end = len; }
        public bool More => p < end;
        public int U8() => p < end ? d[p++] : 0;
        public int U16() { int v = U8(); return v | (U8() << 8); }
        public int I32() { int v = 0; for (int i = 0; i < 4; i++) v |= U8() << (8 * i); return v; }
        public ulong U64() { ulong v = 0; for (int i = 0; i < 8; i++) v |= (ulong)U8() << (8 * i); return v; }
        public float F32() => BitConverter.Int32BitsToSingle(I32());
        public string Str() { int n = U8(); if (p + n > end) n = Math.Max(0, end - p); var s = Encoding.UTF8.GetString(d, p, n); p += n; return s; }
    }

    /// <summary>
    /// The controls of one tick, packed into 9 bytes. Every phone steps with the unpacked values, the host its own too, so all
    /// of them feed the rules exactly the same floats.
    /// </summary>
    static class MpInput
    {
        public const int Bytes = 9;
        const int AngSteps = 65534, NoAngle = 65535;
        const double AngScale = 2 * Math.PI / AngSteps;

        public static void Write(MpWriter w, in PlayerInput i)
        {
            w.U8(Q(i.MoveX)); w.U8(Q(i.MoveY));
            w.U8((i.Special ? 1 : 0) | (i.Spell ? 2 : 0) | ((int)i.Signal & 3) << 2); // bits 2-3: a team signal
            w.U16(i.Special ? Ang(i.AimAngle) : NoAngle); w.U8(i.Special ? K(i.AimK) : 0);
            w.U16(i.Spell ? Ang(i.SpellAngle) : NoAngle); w.U8(i.Spell ? K(i.SpellK) : 255);
        }

        public static PlayerInput Read(ref MpReader r)
        {
            var i = PlayerInput.None;
            i.MoveX = (sbyte)r.U8() / 127f; i.MoveY = (sbyte)r.U8() / 127f;
            int f = r.U8();
            i.Special = (f & 1) != 0; i.Spell = (f & 2) != 0; i.Signal = (TeamSignal)((f >> 2) & 3);
            int a = r.U16(); int k = r.U8();
            if (i.Special) { i.AimAngle = UnAng(a); i.AimK = k / 255f; }
            a = r.U16(); k = r.U8();
            if (i.Spell) { i.SpellAngle = UnAng(a); i.SpellK = k / 255f; }
            return i;
        }

        /// <summary>What the other phones will get for i (the host steps with this, never with the raw value).</summary>
        public static PlayerInput RoundTrip(in PlayerInput i, MpWriter scratch)
        {
            scratch.Len = 0; Write(scratch, in i);
            var r = new MpReader(scratch.Data, scratch.Len);
            return Read(ref r);
        }

        static int Q(float v) => (byte)(sbyte)Math.Round(Math.Max(-1f, Math.Min(1f, float.IsNaN(v) ? 0f : v)) * 127f);
        static int K(float v) => (int)Math.Round(Math.Max(0f, Math.Min(1f, float.IsNaN(v) ? 1f : v)) * 255f);
        static int Ang(float a)
        {
            if (float.IsNaN(a) || float.IsInfinity(a)) return NoAngle;
            double x = a + Math.PI; x -= Math.Floor(x / (2 * Math.PI)) * 2 * Math.PI; // 0..2pi
            int q = (int)Math.Round(x / AngScale);
            return q >= AngSteps ? 0 : q;
        }
        static float UnAng(int q) => q == NoAngle ? float.NaN : (float)(q * AngScale - Math.PI);
    }
}
