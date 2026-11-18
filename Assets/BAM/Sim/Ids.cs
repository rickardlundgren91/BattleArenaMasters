using System;

namespace BAM.Sim
{
    // The shared vocabulary of the game. Other assemblies (Art, Audio, UI, Game) key their visuals, sounds and screens on
    // these values, so entries are only ever added at the end; changing or removing one is a contract change (Docs/CONTRACTS.md).

    /// <summary>The heroes, in hero-select order (Bonk joined 2026-10-26; Hilde and Oaken, heroes 9 and 10, on 2026-10-28; Kugg,
    /// hero 11 after the old game's ROI17, on 2026-11-05; Ragn, Nassla, Skrall and Ravna, heroes 12 to 15 after tester Øli's four ideas,
    /// on 2026-11-09, Rickard: "vi bygger alla fyra"). Nassla is shown as "Nässla"; the code name stays plain ASCII.</summary>
    public enum HeroId : byte { Bram = 0, Fenn = 1, Tyra = 2, Moa = 3, Knut = 4, Tikta = 5, Blast = 6, Bonk = 7, Hilde = 8, Oaken = 9, Kugg = 10, Ragn = 11, Nassla = 12, Skrall = 13, Ravna = 14 }

    /// <summary>Team 0 is the player's team at the bottom of the map (blue), team 1 the enemy at the top (red).</summary>
    public static class Teams
    {
        public const int Player = 0, Enemy = 1;
        public static int Other(int team) => 1 - team;
    }

    public enum UnitKind : byte { Hero = 0, Minion = 1, Tower = 2, Wolf = 3 }

    public enum MinionKind : byte { Melee = 0, Ranged = 1, Siege = 2 }

    /// <summary>Everything that flies. Melee hits use the *Hit kinds: the art may show a swing instead of a projectile.</summary>
    public enum ProjKind : byte
    {
        BramHammer = 0,     // Bram's auto attack, a thrown hammer
        FennSlash = 1,      // Fenn's sword, near-instant
        TyraArrow = 2,
        MoaBolt = 3,        // honey magic
        KnutHit = 4,        // anchor swing, short range
        TiktaNeedle = 5,
        BlastFire = 6,      // fire bolt
        WolfBite = 7,       // Fenn's wolf, melee
        MeleeMinionHit = 8,
        RangedMinionShot = 9,
        SiegeShell = 10,
        TowerShot = 11,
        BramHammerSpecial = 12, // Hammer Throw, bounces between enemies
        TyraBolt = 13,          // Pinpoint Bolt / Long Shot
        KnutAnchor = 14,        // Anchor Throw, a hook on a chain (tethered to Knut)
        TiktaNeedleSpecial = 15,
        BlastBomb = 16,         // Supernova bomb, lobbed, explodes after a delay
        BlastChain = 17,        // Chain Reaction: Supernova's second blast where the burning hero stood
        BonkClub = 18,          // Bonk's auto attack, a stone club swing, near-instant
        BonkSlam = 19,          // Ground Slam: his club into the ground, a ring of hits round him
        BonkBoulder = 20,       // Boulder Toss: a boulder rolled along a line, knocks back the first enemy
        HildeShell = 21,        // Hilde's auto attack, a short cannon shell
        HildeKeg = 22,          // Powder Keg (drag): the big keg, a 1 s fuse, hits towers, pushes heroes the aimed way
        HildeBurst = 23,        // Powder Burst (tap): the small quick keg, a 0.4 s fuse, a small push away from its middle
        KnutSlam = 24,          // Knut's Anchor Slam (tap): the anchor into the ground, a ring of hits and slows round him
        OakenFist = 25,         // Oaken's auto attack, a stone punch, near-instant (also his Stored Impact's extra hit)
        OakenSlam = 26,         // Oaken's slam (tap): both fists into the ground, a ring of hits round him
        OakenQuake = 27,        // Oaken's quake (drag): a crack running along a line, knocks heroes along it
        TyraVolley = 28,        // view only: Tyra's Arrow Volley arrows (the rules fly them as TyraArrow in ProjMode.Volley)
        KuggShot = 29,          // Kugg's auto attack, a short rune cannon ball
        KuggShell = 30,         // Kugg's shot while Anchored (Rotfäste): long range, bigger
        KuggSpark = 31,         // Kugg's Spark Shot passive: a small shot at a random enemy, by itself
        RagnClaw = 32,          // Ragn's auto attack, a claw swipe, near-instant (melee)
        RagnLeap = 33,          // Ragn's Moon Leap: the landing hit, its ring and the shove
        NasslaThorn = 34,       // Nässla's auto attack, a thrown poison thorn
        NasslaBramble = 35,     // Nässla's Thorn Thicket: the seed pod and the bramble's poison ticks
        SkrallBolt = 36,        // Skrall's auto attack, a small bolt of lightning, near-instant (Trance's jump too)
        SkrallStrike = 37,      // Skrall's Thunder Judgement: one of three lightning strikes from the sky
        RavnaSlash = 38,        // Ravna's auto attack, two quick talon cuts, near-instant (melee)
        RavnaFlock = 39,        // Ravna's Crow Flock: the crows tearing at an enemy they fly through
        BramBolt = 40,          // Bram's Hammer Line as Silver (2026-11-11 a lightning beam; since 2026-11-12 his thrown hammer, flying straight)
    }

    /// <summary>Ground areas that last a while.</summary>
    public enum ZoneKind : byte
    {
        HoneyField = 0,
        FireLane = 1,
        BombFuse = 2,
        WarnLine = 3,   // a red warning line before a shot (a strip: centre, direction, length, half width)
        WarnCircle = 4, // a red warning circle
        FeatherTrail = 5, // Tyra's bolt's wind and feathers (a strip that slows enemies, TyraS.Trail*)
        Bramble = 6,      // Nässla's Thorn Thicket: a circle that slows and poisons enemies and heals allies (NasslaS)
    }

    [Flags]
    public enum StatusFlags : ushort
    {
        None = 0,
        Stunned = 1 << 0,
        Slowed = 1 << 1,
        Rooted = 1 << 2,
        Burning = 1 << 3,
        Poisoned = 1 << 4,
        Shielded = 1 << 5,
        Hasted = 1 << 6,
        InGrass = 1 << 7,
        Revealed = 1 << 8,
        Illusion = 1 << 9,     // Fenn's clone (only his own team is told)
        Marked = 1 << 10,      // Tikta's Deathmark is on this hero
    }

    /// <summary>Which arena a match is played on. Runes: the rune-road arena (BAM_OriginalArena_v1_2). Grove: the second map
    /// (2026-11-04, Rickard: own design in the spirit of the old game's walled garden map). Plateau: the third map (2026-11-18,
    /// Rickard: "bygg the plateau från abc2"; own layout after ABC2's The Plateau, Arena.PlateauTop).</summary>
    public enum MapId : byte { Runes = 0, Grove = 1, Plateau = 2 }

    // Ultra: above Hard (Rickard 2026-10-31). Beginner: below Easy (Rickard 2026-11-18 "det kanske är bättre med en egen beginner nivå");
    // it got the next free number so saved picks, traces and the online wire keep their meaning: walk the levels with DifficultyOrder.
    public enum Difficulty : byte { Easy = 0, Normal = 1, Hard = 2, Ultra = 3, Beginner = 4 }

    /// <summary>The bot levels from the easiest up, as the menus show them (Beginner is numbered after Ultra in the enum).</summary>
    public static class DifficultyOrder
    {
        public static readonly Difficulty[] Levels = { Difficulty.Beginner, Difficulty.Easy, Difficulty.Normal, Difficulty.Hard, Difficulty.Ultra };
        public static readonly string[] Names = { "Beginner", "Easy", "Normal", "Hard", "Ultra" };
        /// <summary>The level's place in Levels (0 = Beginner); an unknown number counts as Normal.</summary>
        public static int Pos(Difficulty d) { int i = System.Array.IndexOf(Levels, d); return i < 0 ? 2 : i; }
        public static string Name(Difficulty d) => Names[Pos(d)];
        /// <summary>A level number from a save or the wire: a known level, else Normal.</summary>
        public static Difficulty Clean(int v) => v >= 0 && v <= (int)Difficulty.Beginner ? (Difficulty)v : Difficulty.Normal;
    }

    /// <summary>The battle spell a hero takes into the match (old SimSpells.cs): one extra button beside the SPECIAL.</summary>
    public enum SpellId : byte { None = 0, Blink = 1, Recall = 2, Shield = 3, Haste = 4, Sprint = 5 }
    /// <summary>A team signal from the emote wheel (Signals.cs).</summary>
    public enum TeamSignal : byte { None = 0, Attack = 1, Defend = 2 }
}
