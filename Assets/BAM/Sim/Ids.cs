using System;

namespace BAM.Sim
{
    // The shared vocabulary of the game. Other assemblies (Art, Audio, UI, Game) key their visuals, sounds and screens on
    // these values, so entries are only ever added at the end; changing or removing one is a contract change (Docs/CONTRACTS.md).

    /// <summary>The heroes, in hero-select order (Bonk joined 2026-10-26; Hilde and Oaken, heroes 9 and 10, on 2026-10-28).</summary>
    public enum HeroId : byte { Bram = 0, Fenn = 1, Tyra = 2, Moa = 3, Knut = 4, Tikta = 5, Blast = 6, Bonk = 7, Hilde = 8, Oaken = 9 }

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
    }

    /// <summary>Ground areas that last a while.</summary>
    public enum ZoneKind : byte
    {
        HoneyField = 0,
        FireLane = 1,
        BombFuse = 2,
        WarnLine = 3,   // a red warning line before a shot (a strip: centre, direction, length, half width)
        WarnCircle = 4, // a red warning circle
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

    public enum Difficulty : byte { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>The battle spell a hero takes into the match (old SimSpells.cs): one extra button beside the SPECIAL.</summary>
    public enum SpellId : byte { None = 0, Blink = 1, Recall = 2, Shield = 3, Haste = 4, Sprint = 5 }
}
