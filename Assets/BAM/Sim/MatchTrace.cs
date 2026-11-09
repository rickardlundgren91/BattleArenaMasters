using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BAM.Sim
{
    /// <summary>
    /// A match trace: the match's config and every seat's controls, so the whole match can be run again (Match is deterministic:
    /// the same config and controls give the same match on every machine). Made for the bots to learn from real players (Rickard
    /// 2026-11-09 "bottarna borde kunna lära sig mycket ... för att spela mer som riktiga spelare och för att kunna spela bättre
    /// MED riktiga spelare"). No names and no place: the seat names are left out. Only a controls change is written, gzipped:
    /// a few kB a match.
    ///
    /// Record: Begin before the first Step, Step with the very inputs the match steps with (before that Step), End after the last.
    /// Offline the controls go through Quantize first (the online wire's 9-byte rounding), so the trace holds exactly what the
    /// rules got. Replay runs a trace again and says whether it ended the same (MatchEnd checksum).
    /// </summary>
    public sealed class MatchTrace
    {
        public const int Format = 1;
        const uint Magic = 0x31544D42; // "BMT1"

        // ---------- header ----------
        public int Protocol;
        public string App = "";
        /// <summary>Unix seconds when the match began (the phone's clock).</summary>
        public long At;
        public bool Online, Editor;
        public MatchConfig Config;
        public int Seats;

        // ---------- the end ----------
        public int Steps, Winner = -1;
        public long Ticks;
        public float Time;
        public ulong EndChecksum;
        public bool Ended;

        readonly MemoryStream body = new MemoryStream(4096);
        byte[] last; // the packed controls per seat as last written
        int lastStep, botMask;

        // ================================================================ recording

        public static MatchTrace Begin(MatchConfig cfg, int seats, int protocol, string app, long at, bool online, bool editor)
        {
            var t = new MatchTrace { Config = cfg, Seats = Math.Max(1, Math.Min(8, seats)), Protocol = protocol, App = app ?? "", At = at, Online = online, Editor = editor };
            t.last = new byte[t.Seats * Pack.Bytes];
            for (int s = 0; s < t.Seats; s++) Pack.Write(t.last, s * Pack.Bytes, PlayerInput.None);
            return t;
        }

        /// <summary>Before m.Step(inputs): the seats that became bots since the last step and the controls that changed.</summary>
        public void Step(Match m, PlayerInput[] inputs)
        {
            if (Ended) return;
            int mask = BotMask(m);
            if (mask != botMask) { Head(1, 0); body.WriteByte((byte)mask); botMask = mask; }
            var buf = scratch;
            for (int s = 0; s < Seats; s++)
            {
                var i = inputs != null && s < inputs.Length ? inputs[s] : PlayerInput.None;
                Pack.Write(buf, 0, i);
                bool same = true;
                for (int k = 0; k < Pack.Bytes; k++) if (buf[k] != last[s * Pack.Bytes + k]) { same = false; break; }
                if (same) continue;
                Head(0, s);
                body.Write(buf, 0, Pack.Bytes);
                Buffer.BlockCopy(buf, 0, last, s * Pack.Bytes, Pack.Bytes);
            }
            Steps++;
        }
        readonly byte[] scratch = new byte[Pack.Bytes];
        readonly PlayerInput[] one = new PlayerInput[1];

        /// <summary>One seat (offline).</summary>
        public void Step(Match m, in PlayerInput input) { one[0] = input; Step(m, one); }

        public void End(Match m)
        {
            if (Ended) return;
            Ended = true; Winner = m.Winner; Ticks = m.Ticks; Time = m.Time; EndChecksum = m.Checksum();
        }

        // a record's head: the steps since the last record, then the kind (0 controls, 1 bot seats) and seat
        void Head(int kind, int seat)
        {
            Var(body, (uint)(Steps - lastStep)); lastStep = Steps;
            body.WriteByte((byte)(kind << 4 | seat));
        }

        static int BotMask(Match m)
        {
            int mask = 0;
            for (int s = 0; s < m.Humans.Count && s < 8; s++) { var h = m.Humans[s]; if (h != null && h.Bot != null) mask |= 1 << s; }
            return mask;
        }

        // ================================================================ bytes

        public byte[] ToBytes()
        {
            var raw = new MemoryStream(body.Length > 0 ? (int)body.Length + 256 : 256);
            var w = new BinaryWriter(raw);
            w.Write(Magic); w.Write((byte)Format); w.Write(Protocol); w.Write(App ?? ""); w.Write(At);
            w.Write((byte)((Online ? 1 : 0) | (Editor ? 2 : 0) | (Ended ? 4 : 0)));
            WriteConfig(w, Config ?? new MatchConfig());
            w.Write((byte)Seats);
            w.Write(Steps); w.Write((sbyte)Winner); w.Write(Ticks); w.Write(Time); w.Write(EndChecksum);
            w.Write((int)body.Length); w.Write(body.GetBuffer(), 0, (int)body.Length);
            w.Flush();
            var zipped = new MemoryStream();
            using (var z = new GZipStream(zipped, CompressionLevel.Optimal, true)) z.Write(raw.GetBuffer(), 0, (int)raw.Length);
            return zipped.ToArray();
        }

        public static MatchTrace FromBytes(byte[] data)
        {
            var raw = new MemoryStream();
            using (var z = new GZipStream(new MemoryStream(data), CompressionMode.Decompress)) z.CopyTo(raw);
            raw.Position = 0;
            var r = new BinaryReader(raw);
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("not a BAM match trace");
            int fmt = r.ReadByte(); if (fmt != Format) throw new InvalidDataException("trace format " + fmt);
            var t = new MatchTrace { Protocol = r.ReadInt32(), App = r.ReadString(), At = r.ReadInt64() };
            int f = r.ReadByte(); t.Online = (f & 1) != 0; t.Editor = (f & 2) != 0; t.Ended = (f & 4) != 0;
            t.Config = ReadConfig(r);
            t.Seats = r.ReadByte();
            t.Steps = r.ReadInt32(); t.Winner = r.ReadSByte(); t.Ticks = r.ReadInt64(); t.Time = r.ReadSingle(); t.EndChecksum = r.ReadUInt64();
            int n = r.ReadInt32(); var b = r.ReadBytes(n); t.body.Write(b, 0, b.Length);
            return t;
        }

        static void WriteConfig(BinaryWriter w, MatchConfig c)
        {
            w.Write(c.Seed); w.Write((byte)c.PlayerHero); w.Write((byte)c.Difficulty); w.Write(c.AllyDifficulty.HasValue ? (byte)c.AllyDifficulty.Value : (byte)255);
            w.Write((byte)((c.Tutorial ? 1 : 0) | (c.Island ? 2 : 0) | (c.PlayerIsBot ? 4 : 0) | (c.BotIsHuman ? 8 : 0) | (c.SlotOrder ? 16 : 0) | (c.HeroTest ? 32 : 0)));
            Ids(w, c.Allies); Ids(w, c.Enemies);
            w.Write((byte)c.PlayerSpell); w.Write(c.HeroTestLevel);
            if (c.Seats == null) w.Write((byte)255);
            else { w.Write((byte)c.Seats.Length); foreach (var s in c.Seats) { w.Write((byte)s.Hero); w.Write((byte)s.Team); w.Write((byte)s.Spell); } } // no names
            w.Write(c.LocalSeat); w.Write((byte)c.Map);
        }
        static MatchConfig ReadConfig(BinaryReader r)
        {
            var c = new MatchConfig { Seed = r.ReadInt32(), PlayerHero = (HeroId)r.ReadByte(), Difficulty = (Difficulty)r.ReadByte() };
            int ad = r.ReadByte(); c.AllyDifficulty = ad == 255 ? (Difficulty?)null : (Difficulty)ad;
            int f = r.ReadByte();
            c.Tutorial = (f & 1) != 0; c.Island = (f & 2) != 0; c.PlayerIsBot = (f & 4) != 0; c.BotIsHuman = (f & 8) != 0; c.SlotOrder = (f & 16) != 0; c.HeroTest = (f & 32) != 0;
            c.Allies = ReadIds(r); c.Enemies = ReadIds(r);
            c.PlayerSpell = (SpellId)r.ReadByte(); c.HeroTestLevel = r.ReadInt32();
            int n = r.ReadByte();
            if (n != 255)
            {
                c.Seats = new SeatConfig[n];
                for (int i = 0; i < n; i++) c.Seats[i] = new SeatConfig { Hero = (HeroId)r.ReadByte(), Team = r.ReadByte(), Spell = (SpellId)r.ReadByte(), Name = "P" + (i + 1) };
            }
            c.LocalSeat = r.ReadInt32(); c.Map = (MapId)r.ReadByte();
            return c;
        }
        static void Ids(BinaryWriter w, HeroId[] a) { if (a == null) { w.Write((byte)255); return; } w.Write((byte)a.Length); foreach (var h in a) w.Write((byte)h); }
        static HeroId[] ReadIds(BinaryReader r) { int n = r.ReadByte(); if (n == 255) return null; var a = new HeroId[n]; for (int i = 0; i < n; i++) a[i] = (HeroId)r.ReadByte(); return a; }

        static void Var(Stream s, uint v) { while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; } s.WriteByte((byte)v); }
        static uint ReadVar(byte[] b, ref int p) { uint v = 0; int sh = 0; while (true) { byte x = b[p++]; v |= (uint)(x & 0x7f) << sh; if (x < 0x80) return v; sh += 7; } }

        // ================================================================ running it again

        /// <summary>Runs the trace again from its config. afterStep (may be null) sees the match after every step, with the
        /// controls of that step. Returns the match at its end; same = it ended with the recorded checksum.</summary>
        public Match Replay(Action<Match, PlayerInput[]> afterStep, out bool same)
        {
            var m = Match.Create(Config);
            var inputs = new PlayerInput[Seats];
            for (int s = 0; s < Seats; s++) inputs[s] = PlayerInput.None;
            var b = body.GetBuffer(); int len = (int)body.Length, p = 0;
            int due = p < len ? (int)ReadVar(b, ref p) : int.MaxValue; // the step the next record is for
            for (int step = 0; step < Steps; step++)
            {
                while (due == step) // every record written before this step
                {
                    int head = b[p++], kind = head >> 4, seat = head & 15;
                    if (kind == 1) { int mask = b[p++]; for (int s = 0; s < 8; s++) if ((mask & 1 << s) != 0) m.SeatToBot(s); }
                    else { if (seat < Seats) inputs[seat] = Pack.Read(b, p); p += Pack.Bytes; }
                    due = p < len ? due + (int)ReadVar(b, ref p) : int.MaxValue;
                }
                m.Step(inputs);
                afterStep?.Invoke(m, inputs);
            }
            same = Ended && m.Checksum() == EndChecksum && m.Winner == Winner;
            return m;
        }

        // ================================================================ the controls in 9 bytes (as the online wire, MpInput)

        [ThreadStatic] static byte[] qbuf;

        /// <summary>The controls rounded as the online wire rounds them: offline matches step with this, so a trace replays exactly.</summary>
        public static PlayerInput Quantize(in PlayerInput i)
        {
            var b = qbuf ?? (qbuf = new byte[Pack.Bytes]); // scratch only (no garbage each tick), never match state
            Pack.Write(b, 0, i);
            return Pack.Read(b, 0);
        }

        /// <summary>Byte for byte the same packing as BAM.Online's MpInput (9 bytes), so online controls pass unchanged.</summary>
        public static class Pack
        {
            public const int Bytes = 9;
            const int AngSteps = 65534, NoAngle = 65535;
            const double AngScale = 2 * Math.PI / AngSteps;

            public static void Write(byte[] d, int o, in PlayerInput i)
            {
                d[o] = (byte)Q(i.MoveX); d[o + 1] = (byte)Q(i.MoveY);
                d[o + 2] = (byte)((i.Special ? 1 : 0) | (i.Spell ? 2 : 0) | ((int)i.Signal & 3) << 2);
                int a = i.Special ? Ang(i.AimAngle) : NoAngle; d[o + 3] = (byte)a; d[o + 4] = (byte)(a >> 8); d[o + 5] = (byte)(i.Special ? K(i.AimK) : 0);
                a = i.Spell ? Ang(i.SpellAngle) : NoAngle; d[o + 6] = (byte)a; d[o + 7] = (byte)(a >> 8); d[o + 8] = (byte)(i.Spell ? K(i.SpellK) : 255);
            }

            public static PlayerInput Read(byte[] d, int o)
            {
                var i = PlayerInput.None;
                i.MoveX = (sbyte)d[o] / 127f; i.MoveY = (sbyte)d[o + 1] / 127f;
                int f = d[o + 2];
                i.Special = (f & 1) != 0; i.Spell = (f & 2) != 0; i.Signal = (TeamSignal)((f >> 2) & 3);
                int a = d[o + 3] | d[o + 4] << 8, k = d[o + 5];
                if (i.Special) { i.AimAngle = UnAng(a); i.AimK = k / 255f; }
                a = d[o + 6] | d[o + 7] << 8; k = d[o + 8];
                if (i.Spell) { i.SpellAngle = UnAng(a); i.SpellK = k / 255f; }
                return i;
            }

            static int Q(float v) => (byte)(sbyte)Math.Round(Math.Max(-1f, Math.Min(1f, float.IsNaN(v) ? 0f : v)) * 127f);
            static int K(float v) => (int)Math.Round(Math.Max(0f, Math.Min(1f, float.IsNaN(v) ? 1f : v)) * 255f);
            static int Ang(float a)
            {
                if (float.IsNaN(a) || float.IsInfinity(a)) return NoAngle;
                double x = a + Math.PI; x -= Math.Floor(x / (2 * Math.PI)) * 2 * Math.PI;
                int q = (int)Math.Round(x / AngScale);
                return q >= AngSteps ? 0 : q;
            }
            static float UnAng(int q) => q == NoAngle ? float.NaN : (float)(q * AngScale - Math.PI);
        }
    }
}
