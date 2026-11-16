using System;

namespace BAM.Sim
{
    /// <summary>What a bot is doing right now (the old game's ai.mode strings). For tests and debugging only.</summary>
    public enum BotMode : byte { None, Retreat, Danger, LeaveTower, Soak, Hero, Harass, BushChase, Tower, Lane, Group, Support, Defend, Hold, Regen, Follow }

    /// <summary>
    /// The old bots' per-tier tables and tuning knobs (ABC2 SimAI.cs), with every old switch at its default. Bronze is the V2
    /// brain, every other tier V2 + V3 (the old game turned V3 off for Bronze only). Read-only after start-up.
    /// </summary>
    static class BotTables
    {
        // tier index: Bronze 0, Silver 1, Gold 2, Platinum 3, Ultra 4 (BotTier.Name)
        internal static int Index(BotTier t) => t.Name == "Ultra" ? 4 : t.Name == "Platinum" ? 3 : t.Name == "Gold" ? 2 : t.Name == "Silver" ? 1 : 0;

        internal static readonly float[] DodgeChance = { 0.15f, 0.45f, 0.6f, 0.75f, 0.95f };
        internal const float DodgeChanceMax = 0.8f;
        internal static readonly float[] Mistake = { 0.35f, 0.15f, 0.06f, 0.02f, 0 };
        internal static readonly float[] PanicSpecial = { 0.35f, 0.2f, 0, 0, 0 };
        internal static readonly float[] Suspicion = { 0.1f, 0.45f, 0.65f, 0.75f, 0.8f };
        internal static readonly float[] Soak = { 0, 0.5f, 0.8f, 1, 1 };
        internal static readonly float[] DodgeReact = { 0.4f, 0.3f, 0.22f, 0.16f, 0.08f };
        // human reflexes: a dodge never starts sooner than a person reacts (old HUMAN_REFLEX, on by default)
        internal static readonly float[] DodgeReactMin = { 0.45f, 0.35f, 0.3f, 0.25f, 0.12f };
        internal static readonly float[] DodgeStep = { 0.15f, 0.25f, 0.4f, 9, 9 };

        // V2 knobs (old TK_V2) and the V3 offsets added to some of them (TK3_OF -> TK3_DEF)
        static readonly float[] TkV2 = { 0, 0, 1.25f, 1, -0.2f, 1.5f, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0.8f, 1.25f, 1, 0.25f, 2.5f, 0, 1.5f, 0, 0, 1 };
        static readonly int[] Tk3Of = { 10, 14, 11, 27, 13, 12, 26, -1, -1, 18, -1, 19, 28, 16, 29, 17, 11, -1, 40, 39, 38 };
        /// <summary>The old TK3_DEF (V3 knobs, trained 2026-10-11). The bots read the ones they use through Tk3(i).</summary>
        internal static readonly float[] Tk3Def =
            { 0.6f, 0.6f, 1f, 2f, 1.25f, 1.2f, 0.25f, 2f, 1.5f, 1.5f, -0.1f, -0.45f, -0.25f, 0f, 1f, 0, -0.5f, 0.1f, -0.5f, -0.2f, 1.5f, 0f, 2.5f, 1.5f, 1f, 0.7f, 2f, 0.9f, -0.1f, -0.3f, 1f, 1f, 0f, 0.5f, -1f, 1f, 0f, 2.5f, -0.1f, -1f, -0.25f, 0.15f, -1f, 1f };

        /// <summary>The knob values a bot of this kind uses (old TK(h, i)).</summary>
        internal static float[] Knobs(bool v3)
        {
            var k = (float[])TkV2.Clone();
            if (v3) for (int i = 0; i < Tk3Of.Length; i++) if (Tk3Of[i] >= 0) k[i] += Tk3Def[Tk3Of[i]];
            return k;
        }

        /// <summary>Old HERO_ROLE for the seven heroes: Tank, Fighter, Assassin, Marksman, Mage, Support (Hilde: Support).</summary>
        internal static bool Ranged(HeroId id) => id == HeroId.Bram || id == HeroId.Tyra || id == HeroId.Moa || id == HeroId.Blast || id == HeroId.Hilde || id == HeroId.Nassla || id == HeroId.Skrall; // Marksman, Mage, Support; not Kugg: his cannon reaches 2.5, he fights like a melee hero (nr 69)
        /// <summary>Stocks a Kugg bot waits for before it anchors (he may from 2, Hero7.cs KuggS.MinShots): with 2 he stood rooted ~3.4 s for
        /// two shots, 16 times a match, and won 15 % (full review 2026-11-09 nr 69).</summary>
        internal const int KuggAnchorStocks = 4;
        /// <summary>Kugg's bot never anchors with an enemy hero this close (any side, through walls too): rooted with one already on him,
        /// 52 % of his deaths came while anchored and 27 % within 3.5 s after; with this rule Hard 28 -> 38 %, deaths 2.55 -> 1.77 a match,
        /// his numbers untouched (hero review 2026-11-13, Rickard "fixa alla punkter").</summary>
        internal const float KuggAnchorClear = 3.5f;
        internal static bool Tank(HeroId id) => id == HeroId.Knut || id == HeroId.Oaken; // Oaken: Tank in the old HERO_ROLE

        // the old bot constants
        internal const float SoakMinHp = 0.7f, SoakAllyHp = 0.45f;
        internal const float MemoryT = 4;
        internal const float AfkMax = 3, AfkExp = 5, ReactionOffset = 0.1f;
        internal const float TowerBotLeaveS = 4;
        internal const float ChaseLookT = 1, ChaseCatchT = 2, ChaseLeash = 6, ChaseDropT = 3;
        // hunting a hero that slipped into the grass (BotTarget.cs WantsBushChase, Rickard 2026-11-16 "Våra bottar, speciellt de svårare,
        // ska jaga mer i gräset när man är synlig och en stund efter om dom misstänker att dom har en chans"): seconds after the team last
        // saw it, tiles from the bot to that spot, how far past it the bot first looks (the way it went), and how clearly the bot and its
        // teammates there must win the duel when it is not low. Easy (Bronze) keeps the old chase: 3 s, 7 tiles, low heroes, the spot itself.
        internal static readonly float[] HuntT = { 3, 4, 5, 6, 6 };
        internal static readonly float[] HuntR = { 7, 8, 9, 10, 10 };
        internal static readonly float[] HuntAhead = { 0, 2, 2.5f, 3, 3 };
        internal static readonly float[] HuntEdge = { 0, 1.6f, 1.45f, 1.3f, 1.3f };
        // item cards (Items.cs BotCard; real players' 0.4.1 picks and 12 000 bot matches, 2026-11-16): the levels that take the card worth
        // most to bots (ItemS.BotWorth) instead of a random one. Easy and Normal pick at random, like a beginner.
        internal static readonly bool[] BestCard = { false, false, true, true, true };
        // team play with the human (old SimTeamplay.cs)
        internal const float TeamJoinR = 12, TeamFightT = 2, TeamReach = 6, TeamStayHp = 0.5f, TeamJoinHp = 0.45f, TeamLowHp = 0.3f,
            TeamFollowR = 5, TeamFollowStop = 2.5f, TeamIntentR = 8, TeamIntentKeep = 1.5f, TeamLeash = 10, TeamBackR = 8,
            TeamBackFightT = 3; // the human walking away counts as pulling out of a fight only this soon after trading hits with an enemy hero
        internal const float HumanReact = 1.3f, SpHoldMax = 6;
        // fair bots (old SimAiFair.cs): a hero out of view this long must be noticed again; aim lead builds up over LeadSeenT
        internal const float NoticeGrace = 0.5f, LeadSeenT = 0.3f;
        // human-like movement (old SimHumanMove.cs): walking turns at half rate, dodges turn at 16 rad/s, brake over the last 0.35 tiles
        internal const float HmTurn = 0.5f, HmDodgeTurn = 16, HmBrake = 0.35f;

        // human mistakes and bot smarts by level (BotHuman.cs, Rickard 2026-10-31); same for every bot of a tier on both teams
        internal static readonly float[] TiltChance = { 0.7f, 0.5f, 0.3f, 0.1f, 0 }; // A: angry after a death
        internal static readonly float[] TiltBold = { 0.6f, 0.45f, 0.3f, 0.15f, 0 };
        internal static readonly float[] CarefulChance = { 0, 0.2f, 0.5f, 0.8f, 1 }; // G: careful after a death instead
        internal const float CarefulBold = 0.3f, MoodT = 20;
        internal static readonly float[] GreedChance = { 0.5f, 0.35f, 0.2f, 0.05f, 0 }; // chases too far right after a kill
        internal const float GreedT = 4;
        internal static readonly float[] Tunnel = { 1.2f, 0.6f, 0.4f, 0.15f, 0.1f }; // B: s later to notice a second enemy hero
        internal static readonly float[] Fumble = { 0.35f, 0.2f, 0.12f, 0.05f, 0.02f }; // C: an aimed special without lead
        internal static readonly float[] FocusMul = { 0.6f, 0.8f, 1.2f, 1.5f, 2.5f }; // H: weight of "a teammate is on it"
        internal static readonly float[] OwnTowerFight = { 0, 0.5f, 1, 1.5f, 2 }; // I: a target in our tower's reach
        internal static readonly float[] KiteWait = { 0, 0.5f, 1, 1.5f, 2 }; // J: an outranging hero with its special spent
        internal const float SaveCcR = 7; // K: an enemy hero this close keeps the stun slam off the minions
    }
}
