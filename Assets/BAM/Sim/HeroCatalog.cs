namespace BAM.Sim
{
    /// <summary>What the menus show about a hero. Gameplay numbers live in the rules, not here.</summary>
    public sealed class HeroInfo
    {
        public HeroId Id;
        public string Name, Title, Role;
        public string SpecialName, SpecialDesc, PassiveName, PassiveDesc;
        /// <summary>The one-tap special's name and text (Rules.OneTapSpecial, 2026-11-01); null = SpecialName / SpecialDesc.
        /// SpecialName and SpecialDesc keep the old tap + drag texts, for taking the change back.</summary>
        public string OneTapName, OneTapDesc;
        /// <summary>What the menus and the hold card show as the special's name and text.</summary>
        public string SpecialTitle => Rules.OneTapSpecial && OneTapName != null ? OneTapName : SpecialName;
        public string SpecialText => Rules.OneTapSpecial && OneTapDesc != null ? OneTapDesc : SpecialDesc;
        public int Health, Damage, Speed, Difficulty; // 1..3, for the bars on the hero-select screen
        public bool Ranged;
    }

    public static class HeroCatalog
    {
        /// <summary>The heroes in the game now (All). Bonk joined 2026-10-26 once his model and views were in; Hilde and Oaken
        /// (heroes 9 and 10) wait in Coming for theirs.</summary>
        public const int Count = 8;

        /// <summary>The special's icon in Resources/BAM/Menu (each hero's one picture shows the form the one-tap special keeps).</summary>
        public static string SpecialIcon(HeroId id)
        {
            string lower = Get(id).Name.ToLowerInvariant();
            return "special_" + lower;
        }

        public static HeroInfo Get(HeroId id) => (int)id < All.Length ? All[(int)id] : Coming[(int)id - All.Length];

        /// <summary>Heroes in the rules that are not in the game yet, in HeroId order after All (a new hero waits here for its art).</summary>
        public static readonly HeroInfo[] Coming =
        {
            // Hilde (Simon's Hilde 2.0 from XYZ, Rickard 2026-10-28: the rules now, hidden until her model and views are in)
            new HeroInfo { Id = HeroId.Hilde, Name = "Hilde", Title = "Siege Support", Role = "Support", Ranged = true,
                SpecialName = "Powder Keg", SpecialDesc = "Tap: tosses a small, quick keg at a random enemy that knocks it back a little. Drag: places the big keg that hits towers hardest and knocks every hero it hits the way you aimed, into walls if you like.",
                PassiveName = "Wall Crash", PassiveDesc = "When her Powder Keg knocks an enemy hero into a wall, that hero is stunned for 0.4 s. One hero per keg.",
                Health = 2, Damage = 2, Speed = 1, Difficulty = 1 },
            // Oaken 3.0 (Speltest, Simon's Worldbreaker; Rickard 2026-10-28: hero 10), hidden until his model and views are in
            new HeroInfo { Id = HeroId.Oaken, Name = "Oaken", Title = "The Worldbreaker", Role = "Tank", Ranged = false,
                SpecialName = "Earth Judgement", SpecialDesc = "Tap: after a short moment he slams the ground, hurting every enemy around him. Drag: a quake runs along a line, hurting every enemy in it and knocking enemy heroes back. Walls stop it.",
                PassiveName = "Stored Impact", PassiveDesc = "After 4 s without losing health, the next hit from an enemy hero is cut by 30%, and part of it is added to his next attack. Every 16 s.",
                Health = 3, Damage = 2, Speed = 1, Difficulty = 2 },
        };

        // Texts follow the old game's shipped heroes plus Rickard's 2026-10-25 changes (Fenn's thrown wolf and decoy clone).
        public static readonly HeroInfo[] All =
        {
            new HeroInfo { Id = HeroId.Bram, Name = "Bram", Title = "The Anvil", Role = "Marksman", Ranged = true,
                SpecialName = "Hammer Throw", SpecialDesc = "Tap: hurls his hammer at a random enemy, then it bounces on to up to two more enemies close by. Drag: Anvil Drop, the hammer lands where you aim after a short moment and stuns every enemy it hits for 1.5 s.",
                OneTapName = "Anvil Drop", OneTapDesc = "Tap: his hammer comes down on a random enemy in reach after a short moment and stuns every enemy it hits for 1.5 s.",
                PassiveName = "Backlash", PassiveDesc = "Every 4th attack sends an echo of the hammer back towards Bram that hits the first other enemy in its way.",
                Health = 2, Damage = 3, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Fenn, Name = "Fenn", Title = "Pack Runner", Role = "Fighter", Ranged = false,
                SpecialName = "Call of the Pack", SpecialDesc = "Calls a wolf that picks the nearest enemy and bites it for 4 s, then guards Fenn. Drag to throw it up to 6 tiles: it hunts an enemy hero, or bites minions when no hero is near. Walls stop it.",
                OneTapDesc = "Tap: throws his wolf at a random enemy in his range. It hunts enemy heroes, or bites minions when no hero is near. Walls stop it.",
                PassiveName = "Phantom Step", PassiveDesc = "When an enemy hero hits Fenn, a decoy of him appears for 3 s. It looks like it fights but deals no damage and vanishes at the first hit; if an enemy hero's attack hits it, Fenn runs 20% faster for 1 s. Every 18 s.",
                Health = 2, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Tyra, Name = "Tyra", Title = "The Longshot", Role = "Marksman", Ranged = true,
                SpecialName = "Pinpoint Bolt", SpecialDesc = "Tap: Arrow Volley, five arrows in a cone at a random enemy in her range; each enemy in it is hit once. Drag: a heavy bolt after a short draw that flies until it hits something: the first enemy in its path, a tower or a wall.",
                OneTapDesc = "Tap: after a short draw, a heavy bolt at a random enemy in her range, minions too. It flies past minions until it hits something: the first enemy hero in its path, a tower or a wall.", // any enemy in reach since 2026-11-03 (OneTap.TryTapSpecial, no hero first; bug review round 3)
                PassiveName = "Featherstep", PassiveDesc = "When her attack hurts an enemy hero she runs 12% faster for 1.5 s, once every 10 s.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Moa, Name = "Moa", Title = "Honey Warden", Role = "Support", Ranged = true,
                SpecialName = "Honey Rush", SpecialDesc = "Tap: shields the ally hero with the lowest health. Drag: a honey field that gives each ally hero in it a smaller shield. Shielded heroes run faster for a moment, and the shield blocks stuns and slows.",
                OneTapDesc = "Tap: a honey field on a random ally hero near her, or on herself when no ally is close. Each ally hero in it gets a shield and runs faster for a moment; the shield blocks stuns and slows.",
                PassiveName = "Queen's Instinct", PassiveDesc = "When an enemy would drop an ally near her below 25% health, she shields that ally. Every 22 s.",
                Health = 2, Damage = 1, Speed = 2, Difficulty = 1 },
            new HeroInfo { Id = HeroId.Knut, Name = "Knut", Title = "The Anchor", Role = "Tank", Ranged = false,
                SpecialName = "Anchor Throw", SpecialDesc = "Tap: slams his anchor into the ground, hurting and slowing every enemy around him. Drag: throws the anchor where you aim; it flies past minions and pulls the first enemy hero it hits to him and stuns it.",
                OneTapDesc = "Tap: throws his anchor at a random enemy in reach, minions too. It flies past minions, pulls the first enemy hero it hits to him and stuns it.", // any enemy in reach since 2026-11-03 (OneTap.TryTapSpecial, no hero first; bug review round 3)
                PassiveName = "Anchor Drag", PassiveDesc = "His attacks slow the target.",
                Health = 3, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Tikta, Name = "Tikta", Title = "The Needle Wing", Role = "Assassin", Ranged = false,
                SpecialName = "Needle Storm", SpecialDesc = "Fires four poison needles at random nearby enemies, minions too. Drag to fire them all in one straight line.",
                OneTapDesc = "Tap: four poison needles in a straight line at a random enemy in reach; each needle stops in the first enemy it hits.",
                PassiveName = "Deathmark", PassiveDesc = "A normal attack that hurts an enemy hero marks it for 3 s (one mark at a time). The first Needle Storm needle that hits the marked hero deals 50% of her attack damage more.",
                Health = 1, Damage = 3, Speed = 3, Difficulty = 3 },
            new HeroInfo { Id = HeroId.Blast, Name = "Blast", Title = "The Pyromaniac", Role = "Mage", Ranged = true,
                SpecialName = "Supernova", SpecialDesc = "Tap: a strip of fire toward a random enemy that burns enemies standing in it for 2 s. Drag: throws a bomb where you aim that explodes after 0.5 s.",
                OneTapDesc = "Tap: throws a bomb at a random enemy in reach that explodes after 0.5 s.",
                PassiveName = "Ignite", PassiveDesc = "Hits set the target on fire: 15% more over 2 s. Chain Reaction (9 s cooldown): Supernova on a hero already burning from Blast blasts again where that hero stood 0.25 s later, hitting only the other enemies within 1 tile for 50% of his attack.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Bonk, Name = "Bonk", Title = "The Caveman", Role = "Fighter", Ranged = false,
                SpecialName = "Ground Slam", SpecialDesc = "Slams his club into the ground: enemies around him take damage and are stunned for a moment. Drag to roll a boulder along a line instead: it hits the first enemy and knocks it back. Walls stop it.",
                OneTapDesc = "Tap: slams his club into the ground with all his might: every enemy in a big circle around him takes heavy damage and is stunned for 0.8 s. Only with an enemy in the circle.",
                PassiveName = "Heavy Club", PassiveDesc = "Every 4th attack is a power hit that deals 60% more damage.",
                Health = 3, Damage = 2, Speed = 2, Difficulty = 1 },
        };
    }
}
