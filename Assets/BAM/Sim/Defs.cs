namespace BAM.Sim
{
    /// <summary>How the special's drag is aimed (the HUD draws this while the player drags).</summary>
    public enum AimShape : byte
    {
        None = 0,
        Line = 1,   // a line from the hero, DragReach long and DragWidth half wide
        Circle = 2, // a circle of radius DragWidth at a point up to DragReach away (at least 1.5 tiles out)
    }

    /// <summary>A hero's numbers: the old game's shipped values (move speeds already include its x1.25).</summary>
    public sealed class HeroDef
    {
        public HeroId Id;
        public float Radius, Speed, Hp, HpGrowth, Dmg, DmgGrowth, Cd, CdGrowth, Range;
        public int MaxCharge;
        public bool Tank;
        /// <summary>Auto attack: wind-up before the shot leaves, homing speed (0 = timed flight), flight time.</summary>
        public float SpawnDelay, ProjSpeed, ProjTime;
        public ProjKind Attack;
        /// <summary>Knut: his hits slow the target by OnHitSlow tiles/s for OnHitSlowTime s.</summary>
        public float OnHitSlow, OnHitSlowTime;
        /// <summary>The ring shown around the hero while the special is ready: how far a tap reaches (0 = no ring).</summary>
        public float TapReach;
        public AimShape Drag;
        public float DragReach, DragWidth;
    }

    /// <summary>Minions, towers and Fenn's wolf.</summary>
    public sealed class UnitDef
    {
        public UnitKind Kind;
        public MinionKind Minion;
        public float Radius, Speed, Hp, Dmg, Cd, Range, SpawnDelay, ProjSpeed, ProjTime;
        public ProjKind Attack;
    }

    /// <summary>A bot level (the old game's bot tiers). Easy plays like Bronze, Normal like Gold, Hard like Platinum.</summary>
    public sealed class BotTier
    {
        public string Name;
        public float LowHealth, NoMinionLowHealth, TargetedByTowerLowHealth, HighHealth;
        public float TtkDanger, TtkTowerDive, Roaming, Safety, ProjAvoid;
        public bool CanChaseLow;
        public float[] React;
        /// <summary>How much of a moving hero's motion the bot leads its aimed specials by, and how much it misses by.</summary>
        public float AimLead, AimNoise;
        /// <summary>V3 = the newer bot brain (every tier above Bronze in the old game).</summary>
        public bool V3;
    }

    public static class Defs
    {
        static HeroDef H(HeroId id, float r, float speed, int charges, bool tank, float hp, float hpG, float dmg, float dmgG, float cd, float cdG,
            float range, float spawnDelay, float projSpeed, float projTime, ProjKind attack, float tapReach, AimShape drag, float dragReach, float dragWidth)
            => new HeroDef
            {
                Id = id, Radius = r, Speed = speed, MaxCharge = charges, Tank = tank, Hp = hp, HpGrowth = hpG, Dmg = dmg, DmgGrowth = dmgG,
                Cd = cd, CdGrowth = cdG, Range = range, SpawnDelay = spawnDelay, ProjSpeed = projSpeed, ProjTime = projTime, Attack = attack,
                TapReach = tapReach, Drag = drag, DragReach = dragReach, DragWidth = dragWidth,
            };

        /// <summary>In HeroId order.</summary>
        public static readonly HeroDef[] Heroes =
        {
            H(HeroId.Bram, 0.3f, 2.56f, 8, false, 160, 30, 21, 4.5f, 1.4f, -0.018f, 3.55f, 0.195f, 0, 0.25f, ProjKind.BramHammer, BramS.Reach, AimShape.Circle, BramS.Reach, BramS.DropRadius), // drag: Anvil Drop (2026-10-28); damage 18 -> 21 (balance 2026-10-30)
            H(HeroId.Fenn, 0.4f, 2.5f, 6, false, 215, 35, 20, 4.5f, 1.45f, -0.036f, 3.0f, 0.2f, 0, 0.08f, ProjKind.FennSlash, 0, AimShape.Circle, FennS.ThrowReach, FennS.PounceRadius), // hp 200/33 -> 215/35 (balance 2026-10-28, Rickard OK)
            H(HeroId.Tyra, 0.25f, 2.19f, 6, false, 190, 26, 38, 6, 1.85f, -0.036f, 4.0f, 0.195f, 0, 0.25f, ProjKind.TyraArrow, 4.0f, AimShape.Line, TyraS.Length, TyraS.Hitbox), // tap: Arrow Volley within her range 4 (2026-10-28); attack 1.7 -> 1.85 s (balance 2026-10-28: 69 % vs Hard bots)
            H(HeroId.Moa, 0.215f, 2.5f, 6, false, 205, 30, 25, 4, 1.35f, -0.036f, 3.4f, 0.2f, 0, 0.25f, ProjKind.MoaBolt, MoaS.Range, AimShape.Circle, MoaS.Range, MoaS.AreaRadius), // hp 195/28 -> 205/30, damage 23 -> 25 (balance 2026-10-30)
            H(HeroId.Knut, 0.4f, 2.5f, 8, true, 290, 49, 26, 4.5f, 1.5f, -0.018f, 2.3f, 0.15f, 0, 0.075f, ProjKind.KnutHit, KnutS.SlamRadius, AimShape.Line, KnutS.HookDistance, KnutS.HookRadius), // hp 315/54 -> 300/51 (balance 2026-10-28, with Anchor Slam); hp 300/51 -> 290/49, charges 7 -> 8 (balance 2026-10-30)
            H(HeroId.Tikta, 0.225f, 2.66f, 10, false, 220, 37, 25, 4.5f, 1.3f, -0.036f, 2.35f, 0.15f, 0, 0.1f, ProjKind.TiktaNeedle, TiktaS.Reach, AimShape.Line, TiktaS.LineReach, 0.2f), // hp 195/33 -> 210/35 (balance 2026-10-28: 41-43 %); 210/35 -> 220/37 (balance 2026-10-30)
            H(HeroId.Blast, 0.25f, 2.25f, 10, false, 210, 29, 16.5f, 3.5f, 1.4f, -0.036f, 3.8f, 0.2f, 7, 0, ProjKind.BlastFire, BlastS.Reach, AimShape.Line, BlastS.LaneLength, BlastS.LaneHalfWidth), // hp 190/26 -> 210/29, damage 16 -> 16.5 (balance 2026-10-30)
            // Bonk (BAM's own, 2026-10-26): a melee bruiser between Fenn and Knut; balanced with bot matches (Hero3.cs)
            H(HeroId.Bonk, 0.4f, 2.5f, 3, false, 300, 50, 37, 9, 2.6f, -0.03f, 2.4f, 0.7f, 0, 0.075f, ProjKind.BonkClub, BonkS.SlamRadius, AimShape.Line, BonkS.BoulderReach, BonkS.BoulderHitbox), // damage 56 -> 40 when his club began to follow its target (2026-10-28); 40 -> 37 (balance 2026-10-30)
            // Hilde (XYZ main 2026-10-28, SimHeroes6.cs: 225/42 hp, 21/5 dmg, 1.5 s, range 3.2, speed 2.0 x HERO_SPEED 1.25, 7 charges;
            // her own radius and wind-up from HeroLooks.OWN_STATS, the shell's 0.2 s flight from the 2026-10-18 combat feel pass)
            H(HeroId.Hilde, 0.4f, 2.5f, 7, false, 235, 44, 21, 5, 1.5f, -0.036f, 3.2f, 0.175f, 0, 0.2f, ProjKind.HildeShell, HildeS.Reach, AimShape.Circle, HildeS.Reach, HildeS.KegRadius), // hp 225/42 -> 235/44 (balance 2026-10-30)
            // Oaken 3.0 (Speltest SimHeroes6.cs 2026-10-28: 330/56 hp, 34/5.5 dmg, 1.5 s, range 2.3, speed 1.85 x HERO_SPEED 1.25, 6 charges;
            // radius and the punch's timing from HeroLooks.OWN_STATS: 0.275, wind-up 0.15 s, 0.09 s to land)
            H(HeroId.Oaken, 0.275f, 2.3125f, 6, true, 340, 58, 34, 5.5f, 1.5f, -0.018f, 2.3f, 0.15f, 0, 0.09f, ProjKind.OakenFist, OakenS.SlamRadius, AimShape.Line, OakenS.QuakeRange, OakenS.QuakeHalfWidth), // hp 330/56 -> 340/58 (balance 2026-10-30)
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];

        static Defs()
        {
            var k = Heroes[(int)HeroId.Knut]; k.OnHitSlow = -1; k.OnHitSlowTime = 1; // Anchor Drag: -1 tile/s for 1 s on heroes and minions
        }

        static UnitDef U(UnitKind kind, MinionKind mk, float r, float speed, float hp, float dmg, float cd, float range, float spawnDelay, float projSpeed, float projTime, ProjKind attack)
            => new UnitDef { Kind = kind, Minion = mk, Radius = r, Speed = speed, Hp = hp, Dmg = dmg, Cd = cd, Range = range, SpawnDelay = spawnDelay, ProjSpeed = projSpeed, ProjTime = projTime, Attack = attack };

        /// <summary>In MinionKind order.</summary>
        public static readonly UnitDef[] Minions =
        {
            U(UnitKind.Minion, MinionKind.Melee, 0.145f, 1.6f, 135, 14, 1, 1, 0.175f, 0, 0.1666f, ProjKind.MeleeMinionHit),
            U(UnitKind.Minion, MinionKind.Ranged, 0.145f, 1.6f, 75, 14, 1, 4, 0, 5, 0, ProjKind.RangedMinionShot),
            U(UnitKind.Minion, MinionKind.Siege, 0.325f, 1.3f, 295, 15, 1, 1.5f, 0.025f, 0, 0.1666f, ProjKind.SiegeShell),
        };
        public static UnitDef Minion(MinionKind k) => Minions[(int)k];

        public static readonly UnitDef Tower = U(UnitKind.Tower, MinionKind.Melee, 0.55f, 0, 1400, 25, 0.75f, 4.25f, 0, 10, 0, ProjKind.TowerShot);
        /// <summary>Fenn's wolf: health and bite are set from Fenn at each summon.</summary>
        public static readonly UnitDef Wolf = U(UnitKind.Wolf, MinionKind.Melee, 0.2f, 2.5f, 100, 10, 1.2f, 1.2f, 0.175f, 0, 0.1666f, ProjKind.WolfBite);

        /// <summary>The team stars (1-3 after hero kills): +health, +damage, -attack cooldown.</summary>
        public static readonly float[] StarHp = { 0, 0.03f, 0.06f, 0.09f };
        public static readonly float[] StarDmg = { 0, 0.03f, 0.06f, 0.09f };
        public static readonly float[] StarCdr = { 0, -0.02f, -0.04f, -0.06f };
        public const int MaxStars = 3;

        static BotTier T(string name, float low, float noMinionLow, float towerLow, float high, float ttkDanger, float ttkDive, float roaming, bool chase,
            float[] react, float safety, float projAvoid, float lead, float noise)
            => new BotTier
            {
                Name = name, LowHealth = low, NoMinionLowHealth = noMinionLow, TargetedByTowerLowHealth = towerLow, HighHealth = high, TtkDanger = ttkDanger,
                TtkTowerDive = ttkDive, Roaming = roaming, CanChaseLow = chase, React = react, Safety = safety, ProjAvoid = projAvoid,
                AimLead = lead, AimNoise = noise, V3 = name != "Bronze",
            };

        public static readonly BotTier Bronze = T("Bronze", 0.35f, 0.4f, 0.5f, 0.7f, 0, 3, 3, false, new[] { 0.5f, 0.8f, 1.2f }, 1, 2, 0.2f, 0.7f);
        public static readonly BotTier Silver = T("Silver", 0.2f, 0.6f, 0.6f, 0.7f, 2, 3, 2, false, new[] { 0.5f, 0.7f, 1f }, 1, 4, 0.5f, 0.5f);
        public static readonly BotTier Gold = T("Gold", 0.4f, 0.6f, 0.6f, 0.7f, 2.5f, 3, 2, true, new[] { 0.4f, 0.6f, 0.9f }, 1, 5, 0.75f, 0.35f);
        public static readonly BotTier Platinum = T("Platinum", 0.5f, 0.6f, 0.69f, 0.8f, 3, 3, 2, true, new[] { 0.333f, 0.5f, 0.75f }, 1, 7, 0.9f, 0.25f);

        public static BotTier Tier(Difficulty d) => d == Difficulty.Easy ? Bronze : d == Difficulty.Normal ? Gold : Platinum;
    }

    /// <summary>The match rules' fixed numbers (the old game's shipped values).</summary>
    public static class Rules
    {
        public const float Tick = 1f / 60f;
        public const float Countdown = 5;          // the match clock runs from -5 to 0 before play
        public const float StartSpeedTime = 3, StartSpeedPct = 0.4f; // every hero runs +40 % for the first 3 s (was +100 % for 5 s; Rickard 2026-10-29: too much)
        public const float WaveFirst = 3, WavePeriod = 16, WaveStagger = 0.2f;
        public const float SecondsPerLevel = 16;
        public const int MaxLevel = 12;
        public const float RespawnBase = 8, RespawnPerDeath = 1, RespawnInvulnerable = 4;
        public const int RespawnLateLevel = 10;
        public const float RespawnLatePerLevel = 2, RespawnLatePerMinute = 4, RespawnLateMax = 20;
        public const float RegenAmount = 0.1f, RegenTick = 0.85f, HeroOutOfCombat = 3;
        public const float TowerOutOfCombat = 10, TowerRegenBelow = 0.25f, TowerRegenAmount = 0.01f, TowerRegenTick = 1;
        public const float GrassSight = 2.5f, RevealTime = 4;
        public const float AmbushBonus = 0.3f;
        public const float KnockCooldown = 2; // a hero (or minion) knocked back cannot be knocked again for 2 s from that knock's start (Rickard 2026-10-30)
        public const float AssistWindow = 5;
        public const float SpecialBuffer = 0.25f;
        public const float SiegeVsTower = 2;
        // towers: shots in a row at one hero hit harder, and heroes from level 10 take more from towers as the match goes on
        public const float TowerAggroTime = 2, TowerRamp = 0.2f, TowerRampMax = 2, TowerRampReset = 2.5f;
        public const int TowerLateLevel = 10;
        public const float TowerLateBase = 0.25f, TowerLatePerLevel = 0.1f, TowerLatePerMinute = 0.15f, TowerLateMax = 2;
        // the other side: from level 8 towers take more damage, a hero without its own minion at the tower gets half of that
        public const int TowerTakenLevel = 8;
        public const float TowerTakenPerMinute = 1, TowerTakenMax = 5;
        // minions
        public const float MinionVision = 3.4f, MinionMaxToLane = 4;
        public const float TutorialMinionHp = 209;
    }
}
