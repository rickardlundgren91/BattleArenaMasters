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
        public string SpecialText => Rules.OneTapSpecial && OneTapDesc != null
            ? (Rules.DragAimSpecial && Match.CanDragAim(Id) ? OneTapDesc + " " + DragLine : OneTapDesc) : SpecialDesc;
        /// <summary>The line every aimable hero's special text ends with (Rules.DragAimSpecial, tester Ski 2026-11-07).</summary>
        public const string DragLine = "Drag to aim it yourself.";
        public int Health, Damage, Speed, Difficulty; // 1..3, for the bars on the hero-select screen
        public bool Ranged;
        /// <summary>A hero not in the game yet that PRACTICE > HERO TEST already lists, so Rickard can try its rules with a stand-in body.</summary>
        public bool Preview;
    }

    public static class HeroCatalog
    {
        /// <summary>The heroes in the game now (All). Bonk joined 2026-10-26 once his model and views were in, Kugg 2026-11-09; Hilde and
        /// Oaken (HeroId 8 and 9) wait in Coming for theirs.</summary>
        public const int Count = 9;

        /// <summary>Is this HeroId one of the heroes in the game (All)? HeroIds are not contiguous since Kugg (10) went in before Hilde (8)
        /// and Oaken (9), so a number below Count is not enough.</summary>
        public static bool InGame(int id) => id >= 0 && id < 256 && IndexOf((HeroId)id) >= 0;

        /// <summary>A hero number from settings or the network made safe: itself when it is in the game, else Tyra.</summary>
        public static HeroId Clean(int id) => InGame(id) ? (HeroId)id : HeroId.Tyra;

        /// <summary>The heroes in the game in HeroId order (bot line-ups draw from these, in the order they always had).</summary>
        public static HeroId[] GameIds => gameIds ??= MakeGameIds();
        static HeroId[] gameIds;
        static HeroId[] MakeGameIds()
        {
            var l = new System.Collections.Generic.List<HeroId>();
            for (int i = 0; i < 256; i++) if (IndexOf((HeroId)i) >= 0) l.Add((HeroId)i);
            return l.ToArray();
        }

        /// <summary>The special's icon in Resources/BAM/Menu (each hero's one picture shows the form the one-tap special keeps).</summary>
        public static string SpecialIcon(HeroId id)
        {
            string lower = id.ToString().ToLowerInvariant(); // the code name: plain ASCII file names (Nässla's is "nassla")
            return "special_" + lower;
        }

        /// <summary>A hero's menu info by id (All is in list order, not HeroId order, since Tyra went first 2026-11-04).</summary>
        public static HeroInfo Get(HeroId id)
        {
            foreach (var h in All) if (h.Id == id) return h;
            foreach (var h in Coming) if (h.Id == id) return h;
            return All[0];
        }

        /// <summary>Where a hero stands in the list (All), or -1 for a hero not in the game yet.</summary>
        public static int IndexOf(HeroId id) => System.Array.FindIndex(All, h => h.Id == id);

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
            // Tester Øli's four ideas as BAM's own heroes (Rickard 2026-11-09 "vi bygger alla fyra", kit approved on a card; Hero8.cs). In
            // HERO TEST only (Preview), with stand-in bodies until Rickard's own models are in (Models.cs)
            new HeroInfo { Id = HeroId.Ragn, Name = "Ragn", Title = "The Werewolf", Role = "Fighter", Ranged = false, Preview = true,
                SpecialName = "Moon Leap", SpecialDesc = "Leaps up to 5 tiles onto a random enemy in reach. Landing, the enemy nearest him is hit hard, stunned for 0.8 s and shoved 2 tiles back, and Ragn gets a shield of 15% of his health for 3 s.",
                PassiveName = "Wolf Run", PassiveDesc = "After 3 s out of combat he runs 15% faster, until he hits or is hit.",
                Health = 3, Damage = 2, Speed = 2, Difficulty = 1 },
            new HeroInfo { Id = HeroId.Nassla, Name = "Nässla", Title = "The Swamp Witch", Role = "Mage", Ranged = true, Preview = true,
                SpecialName = "Thorn Thicket", SpecialDesc = "Throws a seed pod at a random enemy in her range. A thicket grows there for 3 s: enemies in it are slowed 35% and poisoned.",
                PassiveName = "Healing Sap", PassiveDesc = "Allied heroes standing in her thicket get 2% of their health back every second.",
                Health = 1, Damage = 2, Speed = 2, Difficulty = 2 },
            new HeroInfo { Id = HeroId.Skrall, Name = "Skrall", Title = "The Storm Monk", Role = "Mage", Ranged = true, Preview = true,
                SpecialName = "Thunder Judgement", SpecialDesc = "Three lightning strikes fall on random enemies in his range, each after a short red warning. Every enemy under one is hurt; heroes are stunned for 0.3 s.",
                PassiveName = "Trance", PassiveDesc = "Every second he stands still with no enemy in reach he gathers a step, up to 3. His next attack deals 40% more per step; with all 3 it also jumps to another enemy close by.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 3 },
            new HeroInfo { Id = HeroId.Ravna, Name = "Ravna", Title = "The Crow Druid", Role = "Assassin", Ranged = false, Preview = true,
                SpecialName = "Crow Flock", SpecialDesc = "Turns into a flock of crows and flies up to 5.5 tiles straight at a random enemy in reach. As a flock she cannot be hit, and every enemy she flies through is torn at once.",
                PassiveName = "Crow's Eye", PassiveDesc = "An enemy hero she hurts is seen by everyone for 3 s, even in grass.",
                Health = 1, Damage = 2, Speed = 3, Difficulty = 2 },
        };

        /// <summary>Whether HERO TEST also lists the Preview heroes waiting in Coming (Ragn, Nässla, Skrall, Ravna). Off for 0.3.9
        /// (Rickard 2026-11-09: "dom nya hjältarna ska inte in nu, bara kugg"); set true to try them in the editor again.</summary>
        public const bool PreviewsInHeroTest = false;

        /// <summary>What PRACTICE > HERO TEST lists: every hero in the game, then (with PreviewsInHeroTest) the Preview heroes in Coming.</summary>
        public static HeroInfo[] HeroTestList => heroTestList ??= MakeHeroTestList(); // lazy: All is declared below
        static HeroInfo[] heroTestList;
        static HeroInfo[] MakeHeroTestList()
        {
            var l = new System.Collections.Generic.List<HeroInfo>(All);
            if (PreviewsInHeroTest) foreach (var h in Coming) if (h.Preview) l.Add(h);
            return l.ToArray();
        }

        // In list order (hero select, unlocks): Tyra first, the beginner hero you start with and play the tutorial with (Rickard
        // 2026-11-04 after a tester's note that new players quit when they do not feel in control). HeroId keeps its numbers.
        // Texts follow the old game's shipped heroes plus Rickard's 2026-10-25 changes (Fenn's thrown wolf and decoy clone).
        public static readonly HeroInfo[] All =
        {
            new HeroInfo { Id = HeroId.Tyra, Name = "Tyra", Title = "The Longshot", Role = "Marksman", Ranged = true,
                SpecialName = "Pinpoint Bolt", SpecialDesc = "Tap: Arrow Volley, five arrows in a cone at a random enemy in her range; each enemy in it is hit once. Drag: a heavy bolt after a short draw that flies until it hits something: the first enemy in its path, a tower or a wall.",
                OneTapDesc = "Tap: a quick bolt at a random enemy in her range, minions too, aimed where a running hero is heading. It flies past minions until it hits something: the first enemy hero in its path, a tower or a wall. Its path leaves wind and feathers that slow enemies 25 % for 1.5 s.", // any enemy in reach since 2026-11-03 (OneTap.TryTapSpecial, no hero first; bug review round 3)
                PassiveName = "Featherstep", PassiveDesc = "When her attack hurts an enemy hero she runs 12% faster for 1.5 s, once every 10 s.",
                Health = 1, Damage = 3, Speed = 1, Difficulty = 2 },
            // Kugg (Rickard 2026-11-05: ROI17's principle from the old game, his own concept; Hero7.cs). In the game as hero 2 in the
            // list and open for everyone (Rickard 2026-11-09 "lägg in honom som nr 2 i spelet och upplåst för alla"); Oaken's body is his
            // stand-in until Rickard's own model is rigged (Models.cs)
            new HeroInfo { Id = HeroId.Kugg, Name = "Kugg", Title = "The Rune Golem", Role = "Marksman", Ranged = true,
                SpecialName = "Anchor", SpecialDesc = "Tap: drives his heel spikes into the ground and becomes a cannon. He cannot walk, but his reach grows to 8 tiles, he fires faster and sees into grass up to 6 tiles away. After 6 shots (or 10 s) he pulls the spikes up again.",
                PassiveName = "Spark Shot", PassiveDesc = "Every 7.5 to 15 s his cannon fires by itself at a random enemy within 8 tiles for 35% of his attack.",
                Health = 2, Damage = 3, Speed = 1, Difficulty = 3 },
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
