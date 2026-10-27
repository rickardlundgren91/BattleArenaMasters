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
        // tier index: Bronze 0, Silver 1, Gold 2, Platinum 3 (BotTier.Name)
        internal static int Index(BotTier t) => t.Name == "Platinum" ? 3 : t.Name == "Gold" ? 2 : t.Name == "Silver" ? 1 : 0;

        internal static readonly float[] DodgeChance = { 0.25f, 0.45f, 0.6f, 0.75f };
        internal const float DodgeChanceMax = 0.8f;
        internal static readonly float[] Mistake = { 0.3f, 0.15f, 0.06f, 0.02f };
        internal static readonly float[] PanicSpecial = { 0.35f, 0.2f, 0, 0 };
        internal static readonly float[] Suspicion = { 0.1f, 0.45f, 0.65f, 0.75f };
        internal static readonly float[] Soak = { 0, 0.5f, 0.8f, 1 };
        internal static readonly float[] DodgeReact = { 0.4f, 0.3f, 0.22f, 0.16f };
        // human reflexes: a dodge never starts sooner than a person reacts (old HUMAN_REFLEX, on by default)
        internal static readonly float[] DodgeReactMin = { 0.45f, 0.35f, 0.3f, 0.25f };
        internal static readonly float[] DodgeStep = { 0.15f, 0.25f, 0.4f, 9 };

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

        /// <summary>Old HERO_ROLE for the seven heroes: Tank, Fighter, Assassin, Marksman, Mage, Support.</summary>
        internal static bool Ranged(HeroId id) => id == HeroId.Bram || id == HeroId.Tyra || id == HeroId.Moa || id == HeroId.Blast; // Marksman, Mage, Support
        internal static bool Tank(HeroId id) => id == HeroId.Knut;

        // the old bot constants
        internal const float SoakMinHp = 0.7f, SoakAllyHp = 0.45f;
        internal const float MemoryT = 4;
        internal const float AfkMax = 3, AfkExp = 5, ReactionOffset = 0.1f;
        internal const float TowerBotLeaveS = 4;
        internal const float ChaseLookT = 1, ChaseCatchT = 2, ChaseLeash = 6, ChaseDropT = 3;
        // team play with the human (old SimTeamplay.cs)
        internal const float TeamJoinR = 12, TeamFightT = 2, TeamReach = 6, TeamStayHp = 0.5f, TeamJoinHp = 0.45f, TeamLowHp = 0.3f,
            TeamFollowR = 5, TeamFollowStop = 2.5f, TeamIntentR = 8, TeamIntentKeep = 1.5f, TeamLeash = 10, TeamBackR = 8;
        internal const float HumanReact = 1.3f, SpHoldMax = 6;
        // fair bots (old SimAiFair.cs): a hero out of view this long must be noticed again; aim lead builds up over LeadSeenT
        internal const float NoticeGrace = 0.5f, LeadSeenT = 0.3f;
        // human-like movement (old SimHumanMove.cs): walking turns at half rate, dodges turn at 16 rad/s, brake over the last 0.35 tiles
        internal const float HmTurn = 0.5f, HmDodgeTurn = 16, HmBrake = 0.35f;
    }
}
