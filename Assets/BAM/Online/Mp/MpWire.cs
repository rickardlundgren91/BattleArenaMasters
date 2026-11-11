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
        public const byte Loaded = 20, In = 21, Frames = 22, Hash = 23, Desync = 24, LeftMatch = 25, MatchOver = 26, Dropped = 27;
        // Dropped (full review 2026-11-09 nr 71): host to the one guest whose seat it gave to a bot (too far behind, too slow to load),
        // so that guest plays on with bots at once. A new message id: it goes out with the protocol bump of this fix round (59)
        /// <summary>Phones with another protocol never meet (bump it whenever a message or the rules' input changes).</summary>
        public const int Protocol = 60; // 2: groups in ONLINE MATCH (party in Hello, side in Room, Move); 3: Simon's reworks change the rules (8d996f0), so a 2 build would desync; 4: balance (Tyra, Tikta, Knut numbers); 5: Hilde and Oaken (heroes 9 and 10); 6: backdoor protection (half tower damage without a wave); 7: smaller start speed boost, Attack/Defend signals; 8: Fenn 2.0 (Pack Hunt, Shadow Escape) and the thrown wolf's 6-tile cap, plus the Precision & Polish rule fixes (Tyra, Bram, Knut, Bonk, chain map); 9: Precision round 2 (aim, Blast chain walls, Bram bounce, Knut hook reach, stun cuts wind-ups, hidden Tikta warning, checksum fields); 10: a blink or recall makes shots on their way miss (SHOT-08); 11: bodies glide along walls and round the middle boulders instead of sticking (Movement.Slide), and Bonk's boulder bowls on into more enemies (BonkRoll, pins knock their neighbours); 12: smarter bots (teammates fall back only after a real fight, Normal/Hard count the enemy's ready special and never Recall), which run on both phones; 13: knockback cooldown (Rules.KnockCooldown, no new knockback for 2 s after one) and Bonk's bowling capped (3 enemies per throw, the boulder 2); 14: hero balance 2026-10-30 (Defs numbers for eight heroes, Knut's slows no longer stack between Knuts); 15: Blast's special swapped (tap = fire strip at a random enemy, drag = the bomb where it is aimed); 16: the bots' level in the Room message (the host picks it in the room); 17: exact wall test for sight lines and shots (Arena.LineClear), so a shot never slips a wall corner the aim stops at; 18: a hero's regen is stopped only by fights with heroes and towers, never by minions (Unit.LastFight); 19: Tyra's drag bolt flies until it hits an enemy, a tower or a wall, or leaves the arena (TyraS.Flight); 20: a hero's regen starts 2 s after the last hero or tower fight instead of 3 s (Rules.HeroRegenDelay), and a hit reveals in grass for 2 s instead of 4 (Rules.RevealTime); 21: respawn wait and tower shots on heroes grow in one straight line from 1:30 to 6:00 (Combat.LateRamp) instead of level steps from 2:24; 22: Normal bots play like Silver instead of Gold (Defs.Tier), and the bots run on both phones; 23: human-like bots by level (Bots/BotHuman.cs) and the Ultra level (Difficulty.Ultra in the Room message); 24: guest prediction (a guest's controls carry the tick they are for, frames carry each seat's lead); 25: late tower damage gentler (Rules.TowerTakenLevel 10, +50 %/min, max x3); 26: one special per hero, tapped at a random target in reach (Rules.OneTapSpecial, Sim/OneTap.cs); 27: Moa's one-tap field shield lasts 5 s (MoaS.FieldShieldTimeOneTap); 28: one-tap Bonk's special is a big Ground Slam (3 tiles, 1.6 x, 0.8 s stun, 6 charges, 240/38 hp); 29: Moa's one-tap field shield 10 % instead of 12 % (MoaS.FieldShieldOneTap); 30: Tikta's needles hit for 1.5 x instead of 1.2 x (TiktaS.DamageCoeff); 31: Knut's hook flies past minions and the wolf, catches only heroes, and his tap aims only at heroes (KnutS.HookPassesMinions); 32: Knut's hook flies out at a steady 15 tiles/s and hauls its catch with a short pause and a speeding-up reel (KnutS.Reeled); 33: Knut's hook reaches as far as his auto attack range, 2.3 tiles instead of 6.5 (KnutS.HookDistance); 34: Knut's tap fires with only minions in reach too (a hero first, else through a random minion), and every tapped special looks for its target within the hero's auto attack range (OneTap.TapReach), Tikta's needles stop there; 35: the enemy base (tower) stops every hero's straight special and Fenn's thrown wolf as a wall, not only Tyra's bolt, and a tap never picks a target behind it (Match.TowerReach); 36: the own tower stops its team's straight specials and wolf too (both bases are walls for shots); 37: Tyra's bolt flies past minions and hits only heroes, her tap a hero first (TyraS.BoltPassesMinions); 38: Ultra bots much stronger (see every hero, full lead, near-perfect dodges, faster reactions); 39: tower aggro holds: once a tower shoots at a hero it stays on that hero until the hero leaves its reach or dies (Match.TowerLocked); 40: a hero's ready auto attack waits 0.3 s for a target in reach before the swing runs out (Rules.AutoHold); 41: a tapped special warns first: aim locked at the tap, a red warning of its shape and a flash on the hero for 0.3 s, then the cast; nothing homes after the tap (Rules.SpecialTelegraph, Sim/Telegraph.cs); 42: tower shots on heroes x1.15 (Rules.TowerVsHero), minions and the tutorial tower unchanged; 43: the Shield battle spell 25 % max health for 4 s, cooldown 25 s (was 20 % 3 s 30 s; SpellDefs); 44: one-tap bots no longer hold their special to save CC when it is in their tap reach (BotHuman.SaveCc); 45: Knut's long hook as Franco's (flies a steady 12 tiles/s up to 7 tiles, hook 0.5, a steady 10 tiles/s drag back, the tap picks a hero up to 6 tiles away, KnutS.TapReach); 46: Tyra's bolt leaves 0.25 s after the warning instead of 0.35 s (TyraS.Windup); 47: Knut's tap picks its target inside his white ring (2.3) again, not up to 6 tiles; the hook still flies 7 (OneTap.TapReach); 48: the Shield battle spell lasts 5 s instead of 4 (SpellDefs.ShieldDur); 49: Bonk swings every 2.0 s for 28.5 (was 2.6 s for 37) with a 0.5 s wind-up (was 0.7), same damage per second (Defs); 50: Knut's and Tyra's taps roll among all enemies in reach, minions too, no hero first (OneTap.TryTapSpecial, focus); 51: tester feedback round 2 (any fight incl. minions stops regen, Moa one-tap shield 8 %, Fenn wolf x0.5 vs towers, Tyra speed 2.5) and hero balance (Defs numbers for Tyra, Moa, Blast, Tikta, Fenn, Knut, Bram); 52: a Knut who dies mid-haul frees his catch with Unstick, as at the normal end (bug review 12), and a special still in its warning when a Recall completes is cut with its charge kept (bug review 16); 53: bots spread out, push the tower on a lead, step out to regenerate under 40 % (Hard, Ultra), Tyra's bot bolt picks a random target (Bots/BotSquad.cs); 54: mirror-fair sides (units update in turn order that flips every tick and per seed, paths and the tower push mirrored, bug review 11), bots never aim for a spot inside a tower body (34), bots' special reach is the one-tap reach (35), Ultra reactions 0.25/0.36/0.55 s; 55: a dead hero waits at most 15 s (Rules.RespawnMax) and late damage to towers stops at x2 (Rules.TowerTakenMax, was x3; video review 2026-11-03); 56: Tyra the beginner hero: attack 0.95 s for 17, bolt draw 0.1 s and hit box 0.6, the tap leads a running hero, the bolt leaves a feather trail that slows 25 % for 1.5 s (TyraS), plus Bonk's club lands on its target anywhere in his reach (or another enemy in reach, BonkS.ClubReach) and a hero's ready swing waits for a target until 0.3 s after its charge ring is full (cd + wind-up + AutoHold); 0.3.7 is unreleased, so no new number; 57: bodies are kept out of the tower's wall (Rules.TowerBody 1.1 instead of the hit body 0.55), paths go round the tower's tile and its four neighbours (tester Cajsa 2026-11-04), plus being hit in grass no longer reveals you, only your own hits and an enemy within 2.5 tiles (Combat.DealDamage; tester MysOxen 2026-11-04); 0.3.8 is unreleased, so no new number; 58: respawn starts at 10 s instead of 8 and caps at 20 s instead of 15 (Rules.RespawnBase, Rules.RespawnMax), plus the preview hero Kugg (HERO TEST only; new Unit fields in the checksum copy); 0.3.9 is unreleased, so no new number, plus a drag on SPECIAL aims the one special by hand on top of the random tap (Rules.DragAimSpecial, Match.TryAimSpecial; a placed form lands at most at the white ring, Match.DragShare; tester Ski 2026-11-07), plus a tower turns from its target only to an enemy hero who attacks one of its heroes from inside its reach, never to a hero who was only hit by a defender or hit one from outside (Match.TowerAggro; tester Øli 2026-11-06), plus Tikta can walk during her special's 0.3 s tap warning and her special's damage factor is 1.4 instead of 1.5 (TiktaS.WalksInWarning, TiktaS.DamageCoeff; testers TechincallyAlex and Øli 2026-11-09), plus Tyra walks 2.3 instead of 2.5 and her special bolt's damage factor is 2.6 instead of 1.15 (Defs Tyra speed, TyraS.DamageCoeff; testers Alex and Øli 2026-11-09), plus the preview heroes Ragn, Nässla, Skrall and Ravna (HERO TEST only; new Unit fields in the checksum copy), plus Kugg in the game as hero 2 for everyone (hero picks accept id 10, HeroCatalog.Clean), plus poison and burn ticks show the hero they tick on in grass (Combat.DealDamage; review 0.3.9 nr 57) and no battle spell during a special's warning, Tikta's too (Spells.CastSpell; nr 59), plus Kugg anchors from 2 stocks and fires as many shots as he has (KuggS.MinShots; Rickard 2026-11-09); 59: the 0.4.0 fix round (full review 2026-11-09): a killing blow puts no slow on the corpse and a revive starts without statuses or reveal (nr 72), a dead attacker's late hit, burn or poison tick reveals him no more (76, 66), a tower shot re-aimed in flight takes a minion before a hero (77), no auto attack or battle spell in a jump pad's flight (78), Kugg's stock is spent as the shell leaves so a stun in the wind-up costs none (63), no battle spell while Tikta's dragged needles leave her hand (64), a 2 s burn is exactly its 8 ticks (67), Knut's catch stun shrinks to 0.4 s when he dies mid-haul (101), his tap's red line goes with the catch or the turn back (102), a catch that dies ends the haul at once (103), no SpecialDenied for a press in a special's warning (106), straight shots and their lines stop at the arena's edge, the hook turns back there (107); Fenn's drag preview marks nobody along a warned throw (100, preview only); bots: Kugg's bot anchors from 4 stocks, never in Retreat or Danger and never in panic, and is not Ranged (69), bots keep off jump-pad tiles in spots, straight walks, dodges and kites (87), a tower blocks its eight neighbour tiles for paths and retreat spots keep TowerBody + R + 0.6 off it (88), Kugg's special reach for the bots is 8 only while anchored (89); and a Dropped message (27), host to the guest whose seat became a bot (71), which an old guest would not know; balance (nr 75, Rickard 2026-11-09): Tyra damage 17/3.1 -> 15.5/2.9, Knut hp 305/49 -> 320/52; an anchored Kugg with nobody in reach fires his shells anyway, straight ahead and harmless, so his stocks run out (Match.KuggBlank, Rickard 2026-11-09); the preview heroes Ragn and Nässla play as ABC2's Bronson (Bulldoze charge that follows its target) and Ginette (Bramble Blossom), HERO TEST only (Hero8.cs; new Unit field RagnHome in the checksum copy); Nässla's range and special reach 3.6 -> 3.8 (Rickard 2026-11-10); balance (Rickard 2026-11-10): Blast damage 16.5/3.5 -> 18.15/3.85, Moa's field shield 8 % 5 s -> 7 % 8 s and Queen's Instinct 8 % 2 s -> 7 % 5 s, Bram's Backlash echo on every attack for 25 % (was every 4th for 65 %), Fenn hp 208/33 -> 240/38 and damage 19/4.5 -> 16/3.8; Ragn's Moon Leap shields him only when it hits (Match.RagnLand, Rickard 2026-11-10); Moon Leap and Nässla's bramble can be dodged again (no homing; Ragn damage 21/5, LandCoeff 2.6), and Ragn and Nässla join the game for everyone (hero picks accept ids 11 and 12, HeroCatalog.All); Ragn damage 21/5 -> 19.5/4.6 and Nässla 23/5 -> 20.5/4.5 (Rickard 2026-11-10); 60: three melee minions in every wave instead of two, so a hero has more creeps to push with (Match.WaveCount; Rickard 2026-11-11), plus Kugg's Anchor no longer sees 6 tiles into grass (Match grass sight; Rickard 2026-11-11), plus items: a choice of three at match levels 6 and 12 (six of Simon's Items V2), picked through PlayerInput.ItemPick (bits 4-5 of the flags byte), new Unit and Status fields in the checksum copy (Sim/Items.cs; Rickard 2026-11-11), plus four plain items in the same choice: 10 % lifesteal, +15 % damage to minions, +10 % to towers, +5 % to heroes (ItemS; Rickard 2026-11-11), plus Fenn's special is Shadow Veil (a clone, invisible and faster, a stealth strike that slows; no wolf), Ravna's Night Flight (a steered flock through walls that slows) and Restless (charged by running), and Bram's Hammer Line (straight through every enemy) and Focus (more damage per hit in a row on one target; new Unit field FocusTgt in the checksum copy) (Rickard 2026-11-11)
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
            w.U8((i.Special ? 1 : 0) | (i.Spell ? 2 : 0) | ((int)i.Signal & 3) << 2 | (i.ItemPick & 3) << 4); // bits 2-3: a team signal, 4-5: an item pick
            w.U16(i.Special ? Ang(i.AimAngle) : NoAngle); w.U8(i.Special ? K(i.AimK) : 0);
            w.U16(i.Spell ? Ang(i.SpellAngle) : NoAngle); w.U8(i.Spell ? K(i.SpellK) : 255);
        }

        public static PlayerInput Read(ref MpReader r)
        {
            var i = PlayerInput.None;
            i.MoveX = (sbyte)r.U8() / 127f; i.MoveY = (sbyte)r.U8() / 127f;
            int f = r.U8();
            i.Special = (f & 1) != 0; i.Spell = (f & 2) != 0; i.Signal = (TeamSignal)((f >> 2) & 3); i.ItemPick = (byte)((f >> 4) & 3);
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
