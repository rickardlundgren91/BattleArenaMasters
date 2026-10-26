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
        public const int Count = 7;

        public static HeroInfo Get(HeroId id) => All[(int)id];

        // Texts follow the old game's shipped heroes plus Rickard's 2026-10-25 changes (Fenn's thrown wolf and decoy clone).
        public static readonly HeroInfo[] All =
        {
            new HeroInfo { Id = HeroId.Bram, Name = "Bram", Title = "The Anvil", Role = "Marksman", Ranged = true,
                SpecialName = "Hammer Throw", SpecialDesc = "Hurls his hammer at a random enemy hero; it bounces on to up to two more enemies close by. Drag to send it through every enemy in a line.",
                PassiveName = "Anvil Strike", PassiveDesc = "Every 4th attack also hits enemies next to the target.",
                Health = 2, Damage = 3, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Fenn, Name = "Fenn", Title = "Pack Runner", Role = "Fighter", Ranged = false,
                SpecialName = "Call of the Pack", SpecialDesc = "Calls a wolf that follows Fenn and bites enemies for 4 s. Drag to throw it up to 6 tiles: it hunts an enemy hero, or bites minions when no hero is near. Walls stop it.",
                PassiveName = "Phantom Step", PassiveDesc = "When an enemy hero hits Fenn, a decoy of him appears for 3 s. It looks like it fights but deals no damage and vanishes at the first hit. Every 18 s.",
                Health = 2, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Tyra, Name = "Tyra", Title = "The Longshot", Role = "Marksman", Ranged = true,
                SpecialName = "Pinpoint Bolt", SpecialDesc = "Fires a heavy bolt after a short draw that hits the first enemy in its path. Drag to aim it.",
                PassiveName = "Steady Hand", PassiveDesc = "Attacks hit harder after she stands still for 1 s.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Moa, Name = "Moa", Title = "Honey Warden", Role = "Support", Ranged = true,
                SpecialName = "Honey Rush", SpecialDesc = "Tap: shields the ally hero with the lowest health. Drag: a honey field that gives each ally hero in it a smaller shield. Shielded heroes run faster for a moment, and the shield blocks stuns and slows.",
                PassiveName = "Queen's Instinct", PassiveDesc = "When an enemy would drop an ally near her below 25% health, she shields that ally. Every 22 s.",
                Health = 2, Damage = 1, Speed = 2, Difficulty = 1 },
            new HeroInfo { Id = HeroId.Knut, Name = "Knut", Title = "The Anchor", Role = "Tank", Ranged = false,
                SpecialName = "Anchor Throw", SpecialDesc = "Throws an anchor that pulls the first enemy hero it hits to him and stuns it. Drag to aim.",
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
        };
    }
}
