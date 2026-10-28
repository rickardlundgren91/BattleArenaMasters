namespace BAM.Sim
{
    /// <summary>What the menus show about a hero. Gameplay numbers live in the rules, not here.</summary>
    public sealed class HeroInfo
    {
        public HeroId Id;
        public string Name, Title, Role;
        public string SpecialName, SpecialDesc, PassiveName, PassiveDesc;
        public int Health, Damage, Speed, Difficulty; // 1..3, for the bars on the hero-select screen
        public bool Ranged;
    }

    public static class HeroCatalog
    {
        /// <summary>The heroes in the game now (All). Bonk joined 2026-10-26 once his model and views were in.</summary>
        public const int Count = 8;

        public static HeroInfo Get(HeroId id) => (int)id < All.Length ? All[(int)id] : Coming[(int)id - All.Length];

        /// <summary>Heroes in the rules that are not in the game yet, in HeroId order after All (a new hero waits here for its art).</summary>
        public static readonly HeroInfo[] Coming =
        {
            // Hilde (Simon's Hilde 2.0 from XYZ, Rickard 2026-10-28: the rules now, hidden until her model and views are in)
            new HeroInfo { Id = HeroId.Hilde, Name = "Hilde", Title = "Siege Support", Role = "Support", Ranged = true,
                SpecialName = "Powder Keg", SpecialDesc = "Tap: tosses a small, quick keg at a random enemy that knocks it back a little. Drag: places the big keg that hits towers hardest and knocks every hero it hits the way you aimed, into walls if you like.",
                PassiveName = "Wall Crash", PassiveDesc = "When her Powder Keg knocks an enemy hero into a wall, that hero is stunned for 0.4 s. One hero per keg.",
                Health = 2, Damage = 2, Speed = 1, Difficulty = 1 },
        };

        // Texts follow the old game's shipped heroes plus Rickard's 2026-10-25 changes (Fenn's thrown wolf and decoy clone).
        public static readonly HeroInfo[] All =
        {
            new HeroInfo { Id = HeroId.Bram, Name = "Bram", Title = "The Anvil", Role = "Marksman", Ranged = true,
                SpecialName = "Hammer Throw", SpecialDesc = "Tap: hurls his hammer at a random enemy, then it bounces on to up to two more enemies close by. Drag: Anvil Drop, the hammer lands where you aim after a short moment and stuns every enemy it hits for 1.5 s.",
                PassiveName = "Backlash", PassiveDesc = "Every 4th attack sends an echo of the hammer back towards Bram that hits the first other enemy in its way.",
                Health = 2, Damage = 3, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Fenn, Name = "Fenn", Title = "Pack Runner", Role = "Fighter", Ranged = false,
                SpecialName = "Call of the Pack", SpecialDesc = "Calls a wolf that follows Fenn and bites enemies for 4 s. Drag to throw it up to 6 tiles: it hunts an enemy hero, or bites minions when no hero is near. Walls stop it.",
                PassiveName = "Phantom Step", PassiveDesc = "When an enemy hero hits Fenn, a decoy of him appears for 3 s. It looks like it fights but deals no damage and vanishes at the first hit. Every 18 s.",
                Health = 2, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Tyra, Name = "Tyra", Title = "The Longshot", Role = "Marksman", Ranged = true,
                SpecialName = "Pinpoint Bolt", SpecialDesc = "Tap: Arrow Volley, five arrows in a cone at a random enemy in her range; each enemy in it is hit once. Drag: a heavy bolt after a short draw that hits the first enemy in its path.",
                PassiveName = "Featherstep", PassiveDesc = "When her attack hits an enemy hero she runs 12% faster for 1.5 s, once every 10 s.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Moa, Name = "Moa", Title = "Honey Warden", Role = "Support", Ranged = true,
                SpecialName = "Honey Rush", SpecialDesc = "Tap: shields the ally hero with the lowest health. Drag: a honey field that gives each ally hero in it a smaller shield. Shielded heroes run faster for a moment, and the shield blocks stuns and slows.",
                PassiveName = "Queen's Instinct", PassiveDesc = "When an enemy would drop an ally near her below 25% health, she shields that ally. Every 22 s.",
                Health = 2, Damage = 1, Speed = 2, Difficulty = 1 },
            new HeroInfo { Id = HeroId.Knut, Name = "Knut", Title = "The Anchor", Role = "Tank", Ranged = false,
                SpecialName = "Anchor Throw", SpecialDesc = "Tap: slams his anchor into the ground, hurting and slowing every enemy around him. Drag: throws the anchor where you aim; it pulls the first enemy it hits to him and stuns it.",
                PassiveName = "Anchor Drag", PassiveDesc = "His attacks slow the target.",
                Health = 3, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Tikta, Name = "Tikta", Title = "The Needle Wing", Role = "Assassin", Ranged = false,
                SpecialName = "Needle Storm", SpecialDesc = "Fires four poison needles at random nearby enemies, minions too. Drag to fire them all in one straight line.",
                PassiveName = "Deathmark", PassiveDesc = "A normal attack that hurts an enemy hero marks it for 3 s (one mark at a time). The first Needle Storm needle that hits the marked hero deals 50% of her attack damage more.",
                Health = 1, Damage = 3, Speed = 3, Difficulty = 3 },
            new HeroInfo { Id = HeroId.Blast, Name = "Blast", Title = "The Pyromaniac", Role = "Mage", Ranged = true,
                SpecialName = "Supernova", SpecialDesc = "Tap: throws a bomb at a random enemy that explodes after 0.5 s. Drag: a strip of fire that burns enemies standing in it for 2 s.",
                PassiveName = "Ignite", PassiveDesc = "Hits set the target on fire: 15% more over 2 s. Chain Reaction (9 s cooldown): Supernova on a hero already burning from Blast blasts again where that hero stood 0.25 s later, hitting only the other enemies within 1 tile for 50% of his attack.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Bonk, Name = "Bonk", Title = "The Caveman", Role = "Fighter", Ranged = false,
                SpecialName = "Ground Slam", SpecialDesc = "Slams his club into the ground: enemies around him take damage and are stunned for a moment. Drag to roll a boulder along a line instead: it hits the first enemy and knocks it back. Walls stop it.",
                PassiveName = "Heavy Club", PassiveDesc = "Every 4th attack is a power hit that deals 60% more damage.",
                Health = 3, Damage = 2, Speed = 2, Difficulty = 1 },
        };
    }
}
