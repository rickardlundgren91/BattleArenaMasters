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

    /// <summary>A bot level (the old game's bot tiers). Easy plays like Bronze, Normal like Silver, Hard like Platinum.</summary>
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
            H(HeroId.Bram, 0.3f, 2.56f, 8, false, 155, 31.5f, 20.5f, 4.5f, 1.4f, -0.018f, 3.55f, 0.195f, 0, 0.25f, ProjKind.BramHammer, BramS.Reach, AimShape.Circle, BramS.Reach, BramS.DropRadius), // drag: Anvil Drop (2026-10-28); damage 18 -> 21 (balance 2026-10-30); hp 160/30 -> 155/31.5, damage 21 -> 20.5 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %)
            H(HeroId.Fenn, 0.4f, 2.5f, 6, false, 240, 38, 16f, 3.8f, 1.45f, -0.036f, 3.0f, 0.2f, 0, 0.08f, ProjKind.FennSlash, 0, AimShape.Circle, FennS.ThrowReach, FennS.PounceRadius), // hp 200/33 -> 215/35 (balance 2026-10-28, Rickard OK); hp 215/35 -> 208/33, damage 20 -> 19 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %)
            H(HeroId.Tyra, 0.25f, 2.3f, 6, false, 190, 26, 15.5f, 2.9f, 0.95f, -0.018f, 4.0f, 0.195f, 0, 0.25f, ProjKind.TyraArrow, 4.0f, AimShape.Line, TyraS.Length, TyraS.Hitbox), // tap: Arrow Volley within her range 4 (2026-10-28); attack 1.7 -> 1.85 s (balance 2026-10-28: 69 % vs Hard bots); speed 2.19 -> 2.5 (tester, Rickard ok), damage 38 -> 33 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %); the beginner hero (Rickard 2026-11-04 "snabba skott, special ofta, balanserad därefter"): attack every 0.95 s for 17/3.1 instead of 1.85 s for 33/6 (same damage per second, a missed target costs half the wait), so her 6 hits charge the special twice as often; bots 3200 matches with the bolt and trail changes (TyraS): Normal 49.0 -> 48.8 %, Hard 47.7 -> 47.3 %; speed 2.5 -> 2.3 (testers Alex and Øli 2026-11-09 "too fast", it felt right before the 2026-11-03 raise; bots 3200 matches with the stronger bolt: Normal 51.0 -> 49.8 %, Hard 49.2 -> 49.7 %, Easy 1600 50.0 -> 51.1 %); damage 17/3.1 -> 15.5/2.9 (balance 2026-11-09, full review nr 75 with the runner's hero attribution fixed, nr 70: she sat at 61-65 % on every tier and both maps, and 7 hits to charge changed nothing; bots same seeds 1000 Hard 60.9 -> 54.4 %, 600 Normal 64.1 -> 56.3 %; pace and the 6-hit special kept)
            H(HeroId.Moa, 0.215f, 2.5f, 6, false, 195, 32, 19.125f, 3.4f, 1.35f, -0.036f, 3.4f, 0.2f, 0, 0.25f, ProjKind.MoaBolt, MoaS.Range, AimShape.Circle, MoaS.Range, MoaS.AreaRadius), // hp 195/28 -> 205/30, damage 23 -> 25 (balance 2026-10-30); hp 205/30 -> 195/32, damage 25 -> 22.5 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %); damage 22.5/4 -> 19.125/3.4 (-15 %) as her field's shield went to 25 % of her own max health (Rickard 2026-11-13 "sköld 25% av moas maxhp", "15%- dmg")
            H(HeroId.Knut, 0.4f, 2.5f, 8, true, 320, 52, 27.5f, 4.5f, 1.5f, -0.018f, 2.3f, 0.15f, 0, 0.075f, ProjKind.KnutHit, KnutS.SlamRadius, AimShape.Line, KnutS.HookDistance, KnutS.HookRadius), // hp 315/54 -> 300/51 (balance 2026-10-28, with Anchor Slam); hp 300/51 -> 290/49, charges 7 -> 8 (balance 2026-10-30); hp 290 -> 305, damage 26 -> 27.5 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %); hp 305/49 -> 320/52 (balance 2026-11-09, nr 75: 42-47 % with the fixed attribution, worst on Normal, and 7 hits to charge changed nothing since bots never hook; bots same seeds 1000 Hard 47.9 -> 51.7 %, 600 Normal 42.3 -> 48.7 %)
            H(HeroId.Tikta, 0.225f, 2.66f, 10, false, 245, 37, 27, 4.5f, 1.3f, -0.036f, 2.35f, 0.15f, 0, 0.1f, ProjKind.TiktaNeedle, TiktaS.Reach, AimShape.Line, TiktaS.LineReach, 0.2f), // hp 195/33 -> 210/35 (balance 2026-10-28: 41-43 %); 210/35 -> 220/37 (balance 2026-10-30); hp 220 -> 245, damage 25 -> 27 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %)
            H(HeroId.Blast, 0.25f, 2.25f, 9, false, 230, 31, 17f, 3.85f, 1.4f, -0.036f, 3.8f, 0.2f, 7, 0, ProjKind.BlastFire, BlastS.LaneLength, AimShape.Circle, BlastS.Reach, BlastS.Radius), // hp 190/26 -> 210/29, damage 16 -> 16.5 (balance 2026-10-30); tap = fire strip, drag = bomb (Rickard 2026-10-30); hp 210/29 -> 230/31, special charges in 9 hits instead of 10 (balance 2026-11-03 after tester feedback, Rickard ok: bots 3200 matches Hard 47-54 %, Normal 43-55 %); damage 18.15 -> 17 (Rickard 2026-11-12 "Ja, alla fyra")
            // Bonk (BAM's own, 2026-10-26): a melee bruiser between Fenn and Knut; balanced with bot matches (Hero3.cs)
            H(HeroId.Bonk, 0.4f, 2.5f, 3, false, 300, 50, 28.5f, 6.9f, 2.0f, -0.03f, 2.4f, 0.5f, 0, 0.075f, ProjKind.BonkClub, BonkS.SlamRadius, AimShape.Line, BonkS.BoulderReach, BonkS.BoulderHitbox), // damage 56 -> 40 when his club began to follow its target (2026-10-28); 40 -> 37 (balance 2026-10-30)
            // faster swing, same damage per second (tester via Discord 2026-11-03, Rickard "bonk slå lite snabbare"): every 2.0 s instead of 2.6,
            // 28.5/6.9 instead of 37/9, wind-up 0.5 s instead of 0.7 so the club lands before the chance is gone; the special charges in
            // 14 s instead of 18. Hard bots 3200 matches (with round 2): 41.5 -> 52.7 %, Normal 38.7 -> 49.7 %
            // Hilde (XYZ main 2026-10-28, SimHeroes6.cs: 225/42 hp, 21/5 dmg, 1.5 s, range 3.2, speed 2.0 x HERO_SPEED 1.25, 7 charges;
            // her own radius and wind-up from HeroLooks.OWN_STATS, the shell's 0.2 s flight from the 2026-10-18 combat feel pass)
            H(HeroId.Hilde, 0.4f, 2.5f, 7, false, 235, 44, 21, 5, 1.5f, -0.036f, 3.2f, 0.175f, 0, 0.2f, ProjKind.HildeShell, HildeS.Reach, AimShape.Circle, HildeS.Reach, HildeS.KegRadius), // hp 225/42 -> 235/44 (balance 2026-10-30)
            // Oaken 3.0 (Speltest SimHeroes6.cs 2026-10-28: 330/56 hp, 34/5.5 dmg, 1.5 s, range 2.3, speed 1.85 x HERO_SPEED 1.25, 6 charges;
            // radius and the punch's timing from HeroLooks.OWN_STATS: 0.275, wind-up 0.15 s, 0.09 s to land)
            H(HeroId.Oaken, 0.275f, 2.3125f, 6, true, 340, 58, 34, 5.5f, 1.5f, -0.018f, 2.3f, 0.15f, 0, 0.09f, ProjKind.OakenFist, OakenS.SlamRadius, AimShape.Line, OakenS.QuakeRange, OakenS.QuakeHalfWidth), // hp 330/56 -> 340/58 (balance 2026-10-30)
            // Kugg (2026-11-05, Rickard: ROI17's principle, his own concept; Hero7.cs). ROI17 in the old game: 210/28 hp (237 in BAM's old
            // sim), 28/7 dmg, 1.5 s, range 2.25, speed 2.08 x 1.25, radius 0.3, 6 stocks; his ball flies 0.47 s. Kugg: 237/30 hp, 26/6.5
            // dmg, 1.5 s, range 2.5, speed 2.5, charges in 6 hits; a first guess for bot matches to tune
            H(HeroId.Kugg, 0.3f, 2.5f, 6, false, 237, 30, 26, 6.5f, 1.5f, -0.036f, 2.5f, 0.2f, 0, 0.3f, ProjKind.KuggShot, KuggS.TapReach, AimShape.None, 0, 0),
            // Øli's four (2026-11-09, Hero8.cs; doc "Nya egna hjältar – förslag", Rickard's card "Ja, bygg alla fyra"): first guesses beside
            // Bonk and Blast, to tune with bot matches. Ragn: melee bruiser like Bonk; Nässla: a frail ranged caster; Skrall: slow, heavy
            // bolts; Ravna: quick double cuts (one hit in the rules), frail
            // Ragn and Nässla as ABC2's Bronson and Ginette (Rickard 2026-11-10 "kan du göra så att dom känns lite mer som i abc2"):
            // their charges (6, 7) and attack pace (1.3 s, 2.0 s) from abc2_gamedata.json, Nässla slower than Ragn as the Witch is than
            // the Brawler (1.92 vs 2.16); damage per hit set with bots (Hard 3200 matches, BAM_HEROES=15): at BAM's damage per second
            // the stronger specials made them 62.5 and 61.6 %, at 17.5/4 and 23/5 they are 50.8 and 53.2 % (before: 53.3 and 51.7 %); a Moon Leap that can be dodged (2026-11-10) put Ragn at 41.8 %, so 21/5 and LandCoeff 2.6: 51.3 %; in the 11-hero game (0.4.0, no Skrall/Ravna) they were 54.3 and 59.6 %, so 19.5/4.6 and 20.5/4.5: 50.1 and 49.0 % (Rickard 2026-11-10 "nässla lite för bra, ragn också")
            H(HeroId.Ragn, 0.4f, 2.4f, 6, false, 270, 45, 18f, 4.6f, 1.4f, -0.03f, 2.4f, 0.15f, 0, 0.075f, ProjKind.RagnClaw, RagnS.LeapReach, AimShape.Circle, RagnS.LeapReach, RagnS.LandRadius), // damage 19.5 -> 18, speed 2.5 -> 2.4 (tester feedback "han va för stark", Rickard 2026-11-12; Hard 5000 matches 51.3 -> 46.7 %)
            H(HeroId.Nassla, 0.25f, 2.4f, 7, false, 205, 26, 14.9f, 4.1f, 1.45f, -0.036f, 3.8f, 0.2f, 0, 0.25f, ProjKind.NasslaThorn, NasslaS.Reach, AimShape.Circle, NasslaS.Reach, NasslaS.Radius), // hp 190/24 -> 205/26, attack 1.6 -> 1.45 s at the same damage per second (16.4/4.5 -> 14.9/4.1; tester feedback, Rickard 2026-11-12 "Ja, alla fyra"); hp 205/26 -> 197/25 (Rickard 2026-11-12 "Nässla ner lite": Hard 5000 matches 54.9 -> 50.9 %), back to 205/26 with her thicket's poison 1.5 -> 1.25 instead (Rickard 2026-11-12 "ge nässla mindre value på ulten istället för mindre hp")
            H(HeroId.Skrall, 0.3f, 2.3f, 9, false, 185, 29, 24, 5.5f, 1.7f, -0.036f, 3.8f, 0.25f, 0, 0.08f, ProjKind.SkrallBolt, 3.8f, AimShape.None, 0, 0),
            // Ravna as ABC2's Ron (Night Flight + Restless, 2026-11-11): Ron's slower, heavier swing (BAM's own numbers near abc2's 26 dmg /
            // 1.5 s, 6 charges) instead of the quick 15 / 0.9 cuts; tuned with bots (Hard, BAM_HEROES=15)
            H(HeroId.Ravna, 0.25f, 2.6f, 7, false, 238, 36, 25, 5.3f, 1.45f, -0.036f, 2.3f, 0.12f, 0, 0.08f, ProjKind.RavnaSlash, RavnaS.FlockReach, AimShape.Line, RavnaS.FlockReach, RavnaS.HitRadius), // damage 26 -> 25 (Rickard 2026-11-12)
        };

        public static HeroDef Hero(HeroId id) => Heroes[(int)id];

        static Defs()
        {
            var k = Heroes[(int)HeroId.Knut]; k.OnHitSlow = -1; k.OnHitSlowTime = 1; // Anchor Drag: -1 tile/s for 1 s on heroes and minions
            if (Rules.OneTapSpecial)
            {   // one-tap Bonk: his big Ground Slam (Hero3.cs) hits hard and stuns long, so it should feel mighty (Rickard 2026-11-02
                // "man ska känna sig mäktig"); paid for with 6 hits to charge instead of 3 and 240/38 hp instead of 300/50 (Hard bots
                // 2026-11-02: 58.5 % wins, 54 % before the one-tap change; the old line above stays for taking it back)
                var b = Heroes[(int)HeroId.Bonk]; b.MaxCharge = 6; b.Hp = 228; b.HpGrowth = 36; // 240/38 -> 228/36 (Rickard 2026-11-12 "Ja, alla fyra")
                if (Rules.SpecialTelegraph && !Rules.TelegraphTopUp)
                {   // the warning before every tapped special (2026-11-02) moved the balance without touching any effect: Bram's anvil
                    // and Tikta's needles are dodged more, Bonk is caught less (Hard bots 1600 matches: Bonk 52.6 -> 62.8 %, Bram 55.2 -> 40.2,
                    // Tikta 49.7 -> 43.7). Paid back in how often, never in how strong (Rickard's rule): Bonk 7 hits to charge instead of
                    // 6, Bram 6 instead of 8, Tikta 8 instead of 10 (Hard then: Bonk 55.3, Bram 45.4, Tikta 47.1; Normal top Moa 62.3, was 60.6)
                    b.MaxCharge = 7; Heroes[(int)HeroId.Bram].MaxCharge = 6; Heroes[(int)HeroId.Tikta].MaxCharge = 8;
                    // Fenn's thrown wolf no longer turns onto a hero (it lands on the warned spot): his special comes one hit sooner
                    // instead, 5 instead of 6, so the bite and root stay as strong (Rickard 2026-11-02, his rule: never weaken the effect)
                    Heroes[(int)HeroId.Fenn].MaxCharge = FennS.VeilCharge; // the Veil since 2026-11-11 (the wolf, gone 2026-11-13, took 5)
                }
            }
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

        public static readonly BotTier Bronze = T("Bronze", 0.35f, 0.4f, 0.5f, 0.7f, 0, 3, 3, false, new[] { 0.6f, 0.9f, 1.3f }, 1, 2, 0.2f, 0.9f); // Easy a bit easier (Rickard 2026-10-31): reactions 0.5/0.8/1.2 -> 0.6/0.9/1.3 s, aim noise 0.7 -> 0.9
        public static readonly BotTier Silver = T("Silver", 0.2f, 0.6f, 0.6f, 0.7f, 2, 3, 2, false, new[] { 0.5f, 0.7f, 1f }, 1, 4, 0.5f, 0.5f);
        public static readonly BotTier Gold = T("Gold", 0.4f, 0.6f, 0.6f, 0.7f, 2.5f, 3, 2, true, new[] { 0.4f, 0.6f, 0.9f }, 1, 5, 0.75f, 0.35f);
        public static readonly BotTier Platinum = T("Platinum", 0.5f, 0.6f, 0.69f, 0.8f, 3, 3, 2, true, new[] { 0.333f, 0.5f, 0.75f }, 1, 7, 0.9f, 0.25f);

        // Ultra (Rickard 2026-10-31 "ett botläge som heter Ultra"): Platinum with faster reactions and sharper aim, and the
        // smartest choices with no mistakes in BotTables; every bot in the match, both teams, plays it
        // 2026-11-02 (Rickard "gör ultrabottarna mycket svårare, dom ska vinna 70-80% mot hard"): reactions 0.15/0.22/0.35 s, aim noise 0.06
        // 2026-11-04 (bug review nr 11): the mirror fix took away the bottom team's handicap that Ultra had always played with
        // (Ultra vs Platinum 79 -> 86 %), so reactions 0.25/0.36/0.55 s bring it back into 70-80 %
        public static readonly BotTier Ultra = T("Ultra", 0.5f, 0.6f, 0.69f, 0.8f, 3, 3, 2, true, new[] { 0.25f, 0.36f, 0.55f }, 1, 8, 1f, 0.06f);

        // Normal is Silver since 2026-10-31: with Gold, Easy -> Normal cost a mid player 25 points and Normal -> Hard only 5
        // (real matches 30-31 Oct: 82 % wins on Easy, 27 % on Normal, 57 % on Hard); Silver splits the gap (58/42/28 %).
        public static BotTier Tier(Difficulty d) => d == Difficulty.Easy ? Bronze : d == Difficulty.Normal ? Silver : d == Difficulty.Ultra ? Ultra : Platinum;
    }

    /// <summary>The match rules' fixed numbers (the old game's shipped values).</summary>
    public static class Rules
    {
        public const float Tick = 1f / 60f;
        /// <summary>How long a hero's ready auto attack waits for a target in reach before the swing runs out and the
        /// cooldown starts over (Rickard's tester 2026-11-02: "i ABC fanns ett litet fönster där man kunde få in ett skott lite
        /// senare"). 0 = the old rule: a swing with nobody in reach runs out at once, so an enemy at the ring's edge waited a whole
        /// cooldown. Same for players and bots (Combat.UpdateAttack).</summary>
        public const float AutoHold = 0.3f;
        /// <summary>One special per hero, cast with a tap at a random target in reach (Rickard 2026-11-01, Match.TryTapSpecial in
        /// OneTap.cs). false = the old tap + drag specials, HUD split, aim ring, cancel X and tutorial drag step, all kept for at
        /// least two versions so the change can be taken back. Not const, so both code paths stay compiled.</summary>
        public static readonly bool OneTapSpecial = true;
        /// <summary>On top of the one-tap special, a drag on the SPECIAL button aims it by hand (tester Ski 2026-11-07: "More control on
        /// specials would be fantabulous"; Rickard picked it). A tap still rolls a random target in reach (OneTap.cs); a drag casts the same
        /// single special the way the finger points, with the same warning, damage and reach: a placed form lands at most at the white
        /// ring, as a tap can (Match.DragShare). Bonk's slam and Kugg's turret have nothing to aim and stay tap only (Match.CanDragAim). Bots
        /// keep tapping (BotSpecial), the guided match keeps the tap only. false = protocol 58: every release is a tap.</summary>
        public static readonly bool DragAimSpecial = true;
        /// <summary>A tapped special warns before it goes (Rickard's tester 2026-11-02: "the reaction window is very short once an
        /// ultimate is activated ... some ultimates seem to be guaranteed hits as long as the target is within range"). The tap locks
        /// the aim at once (the line's direction, the placed form's spot), the hero stands and flashes, and a red warning of the real
        /// shape shows for TelegraphTime before the special's own cast starts (Match.StartTelegraph in Telegraph.cs). Nothing homes
        /// after the tap: Fenn's thrown wolf lands on the locked spot. Moa's shield on a friend waits as long (flash, no red warning). Only with
        /// OneTapSpecial; false = the special goes at the tap, as protocol 40. Same for players and bots.</summary>
        public static readonly bool SpecialTelegraph = true;
        public const float TelegraphTime = 0.3f;
        /// <summary>Blast's bomb lands on the target's feet with a 1.5-tile blast and a 0.5 s fuse: with 0.3 s it still always hit, so it
        /// warns 0.5 s and a hero that runs at the flash gets out (Rickard 2026-11-02).</summary>
        public const float TelegraphTimeBlast = 0.5f;
        /// <summary>true = the warning only where a special had none (Knut's hook, Tikta's needles, Fenn's wolf, Moa): Tyra's draw,
        /// Bram's anvil, Blast's fuse and Bonk's wind-up already warn with their aim fixed, so they keep their own time and only get
        /// the flash. false = every special waits TelegraphTime more.</summary>
        public static readonly bool TelegraphTopUp = false;
        public const float Countdown = 5;          // the match clock runs from -5 to 0 before play
        public const float StartSpeedTime = 3, StartSpeedPct = 0.4f; // every hero runs +40 % for the first 3 s (was +100 % for 5 s; Rickard 2026-10-29: too much)
        public const float WaveFirst = 3, WavePeriod = 16, WaveStagger = 0.2f;
        public const float SecondsPerLevel = 16;
        public const int MaxLevel = 12;
        public const float RespawnBase = 10, RespawnPerDeath = 1, RespawnInvulnerable = 4; // base 8 -> 10 (Cajsa's testers 2026-11-04: "motståndaren respawnar för fort"; Rickard card)
        // late game (Rickard 2026-10-31: no sudden jumps): from 1:30 to 6:00 on the match clock a dead hero's wait grows in one
        // straight line to +20 s and tower shots on heroes to x2 (until protocol 21 both stepped up with the levels from 2:24)
        public const float LateStart = 90, LateFull = 360;
        public const float RespawnLateMax = 20;
        // video review 2026-11-03: 21 s at 3:56 and 28 s at 5:20 (then 8 s + 1 per death + the late ramp; base 10 s, max 20 s since 2026-11-04), so the last minute and a half
        // was watched, not played. The wait never passes RespawnMax; Min keeps it smooth (it rises, then stays flat, no jump)
        public const float RespawnMax = 20; // 15 -> 20 (Rickard 2026-11-04 "Ta max 20s")
        public const float RegenAmount = 0.1f, RegenTick = 0.85f, HeroOutOfCombat = 3, HeroRegenDelay = 2; // HeroRegenDelay: seconds after the last hero/tower fight before regen starts (was HeroOutOfCombat, 3 s, until protocol 20)
        public const float TowerOutOfCombat = 10, TowerRegenBelow = 0.25f, TowerRegenAmount = 0.01f, TowerRegenTick = 1;
        public const float GrassSight = 2.5f, RevealTime = 2; // RevealTime: seconds a hit keeps the attacker visible in grass, and a target hit outside grass (4 s until protocol 20; a target hit inside grass is not revealed since 0.3.8, except by poison and burn ticks since 0.3.9)
        public const float AmbushBonus = 0.3f;
        public const float KnockCooldown = 2; // a hero (or minion) knocked back cannot be knocked again for 2 s from that knock's start (Rickard 2026-10-30)
        public const float AssistWindow = 5;
        public const float SpecialBuffer = 0.25f;
        public const float SiegeVsTower = 2;
        // towers: shots in a row at one hero hit harder, and heroes take more from towers as the match goes on (LateStart..LateFull)
        public const float TowerAggroTime = 2, TowerRamp = 0.2f, TowerRampMax = 2, TowerRampReset = 2.5f;
        public const float TowerLateMax = 2;
        // Rickard 2026-11-02 "torn ska skada lite mer": every tower shot on a hero x1.15 (25 -> 28.75 before streak and late bonus);
        // shots on minions and the tutorial tower are unchanged (protocol 42)
        public const float TowerVsHero = 1.15f;
        // Rickard's Tripo tower stands 3 tiles wide: a round wall of radius ~1.05 on a flat stone foot to 1.5. Its body (Tower.Radius
        // 0.55) let heroes walk into the wall (tester Cajsa 2026-11-04 "jag kan gå rätt igenom dem"), so bodies are kept out to the
        // wall's edge; heroes may still stand on the low foot. Only walking uses it: hits, reach and shots still use Tower.Radius
        public const float TowerBody = 1.1f;
        // the other side: from level 10 towers take more damage, a hero without its own minion at the tower gets half of that.
        // Was level 8, +100 %/min, max x5: in Rickard's recording of 2026-11-01 the tower went 60 -> 0 % in four seconds at 2:52
        // (x2 by then, three level-11 heroes and Blast's keg), a cliff nobody could react to. Now x1 at 2:24, x1.5 at 3:24, x2 at 4:24,
        // and no more: max x3 still let a tower sit at 58 % for 1:45 and then go 58 -> 0 in one second at 6:14 (x2.9; video
        // review 2026-11-03), so the cap is x2 since protocol 55
        public const int TowerTakenLevel = 10;
        public const float TowerTakenPerMinute = 0.5f, TowerTakenMax = 2;
        // minions
        public const float MinionVision = 3.4f, MinionMaxToLane = 4;
        public const float TutorialMinionHp = 209;
    }
}
