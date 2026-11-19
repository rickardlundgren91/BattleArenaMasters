using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace BAM.Online
{
    /// <summary>
    /// Gold, the day's first win and the three daily goals (Rickard 2026-11-15 "Ja, bygg nu" on the launch plan, numbers "okej"):
    /// the first win of the day gives 100 gold, each of the day's three goals 50, each new account level 100. A normal match
    /// gives none, so playing bots all day cannot farm it. Gold buys each hero's first skin (1500, about a week of daily play) and keeps a premium skin after a season in the top 10 (3000);
    /// gold is never sold for money and heroes are never bought. The SERVER counts it (ReportMatch.js, the same formulas as
    /// <see cref="Apply"/>) and keeps the gold and the bought skins in the protected "bam-progress" item; BuySkin.js spends it.
    /// The day is the UTC day (01:00 Swedish winter time), as the first win's XP already counts it.
    /// </summary>
    public static class Gold
    {
        // keep equal to Cloud/Scripts/ReportMatch.js and BuySkin.js
        public const int FirstWin = 100, Goal = 50, LevelUp = 100, SkinPrice = 1500;
        /// <summary>Levels 15, 20, 25 and 30 give MilestoneGold instead of LevelUp (Rickard 2026-11-16 on a card: "Ja, 500 guld på
        /// milstolparna"), so the long road from level 10 to 30 has goals too.</summary>
        public const int MilestoneGold = 500, MilestoneFrom = 15, MilestoneStep = 5;
        public static bool IsMilestone(int level) => level >= MilestoneFrom && (level - MilestoneFrom) % MilestoneStep == 0;
        /// <summary>The gold for reaching this level.</summary>
        public static int ForLevel(int level) => level <= 1 ? 0 : IsMilestone(level) ? MilestoneGold : LevelUp;
        /// <summary>The gold for every level from `from` (not counted) up to `to`.</summary>
        public static int ForLevels(int from, int to) { int g = 0; for (int l = from + 1; l <= to; l++) g += ForLevel(l); return g; }
        /// <summary>The skin gold buys: the hero's first skin (HeroSkins 1).</summary>
        public const int GoldSkin = 1;
        /// <summary>Premium skins (HeroSkins 2 and up; Rickard 2026-11-19): worn only while the player is in the Top 100's first TopN
        /// places this season, and then on every hero; out of the top 10, they are gone. A season finished in the top 10 gives one
        /// keep-pick: one premium skin of the player's choice kept for good, for KeepPrice gold (two gold skins: about two weeks of
        /// daily play, which a top-10 player earns easily). The server decides the place and the picks (ReportMatch/GetMe/BuySkin.js,
        /// EndSeason.js ends a season).</summary>
        public const int PremiumSkin = 2, TopN = 10, KeepPrice = 3000;

        public enum GoalKind { Play = 0, Win = 1, Kills = 2, Hero = 3 }
        static readonly int[] Target = { 3, 2, 10, 1 };
        static readonly List<string> pool = new List<string>(16);

        /// <summary>The heroes "play a match as ..." can ask for at this account level: only heroes the player has (Rickard 2026-11-16
        /// "questen bara ges för de heroes man har"), in HeroId order. ReportMatch.js HERO_UNLOCK must give the same list.</summary>
        public static List<string> GoalHeroes(int level)
        {
            pool.Clear();
            for (int id = 0; id < 256; id++)
                if (BAM.Sim.HeroCatalog.InGame(id) && Progress.HeroLevel((BAM.Sim.HeroId)id) <= level) pool.Add(((BAM.Sim.HeroId)id).ToString());
            if (pool.Count == 0) pool.Add("Tyra");
            return pool;
        }

        /// <summary>The day's three goals: all four kinds but one, and the hero the hero goal asks for, picked from the day number among
        /// the heroes open at the level the day's goals started at, so the phone and the server agree.</summary>
        public static void DayGoals(long day, int level, List<GoalKind> into, out string hero)
        {
            uint h = unchecked((uint)((ulong)day * 2654435761UL));
            int skip = (int)(h % 4);
            var heroes = GoalHeroes(level);
            hero = heroes[(int)((h >> 8) % (uint)heroes.Count)];
            into.Clear();
            for (int g = 0; g < 4; g++) if (g != skip) into.Add((GoalKind)g);
        }

        /// <summary>The level the day's goals started at; a day begun before gLv existed (ReportMatch v8) uses the level now.</summary>
        static int DayLevel(Totals t) => t.gLv > 0 ? t.gLv : t.level;

        public static int TargetOf(GoalKind g) => Target[(int)g];

        public static int ProgressOf(Totals t, GoalKind g) => g == GoalKind.Play ? t.gm : g == GoalKind.Win ? t.gw : g == GoalKind.Kills ? t.gk : t.gh;

        public static long Today => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;

        static readonly List<GoalKind> tmp = new List<GoalKind>(3);

        /// <summary>The gold one counted match gives, added to t (called by Progress.Apply after XP and level).</summary>
        internal static void Apply(Totals t, long day, bool won, int kills, string hero, bool firstWin, int levelBefore, int levelAfter)
        {
            if (levelAfter > levelBefore) t.gold += ForLevels(levelBefore, levelAfter);
            if (firstWin) t.gold += FirstWin;
            if (day > t.gDay) { t.gDay = day; t.gm = t.gw = t.gk = t.gh = t.gDone = 0; t.gLv = levelBefore; } // the day's hero comes from the level it started at
            if (day != t.gDay) return; // a late match from an earlier day: its goals are gone
            t.gm++;
            if (won) t.gw++;
            t.gk += Math.Min(60, Math.Max(0, kills));
            DayGoals(day, DayLevel(t), tmp, out string goalHero);
            if (hero == goalHero) t.gh = 1;
            foreach (var g in tmp)
            {
                int bit = 1 << (int)g;
                if ((t.gDone & bit) == 0 && ProgressOf(t, g) >= TargetOf(g)) { t.gDone |= bit; t.gold += Goal; }
            }
        }

        // ---------- what Home shows ----------

        /// <summary>One of today's goals for the list on Home: "Win 2 matches", 1 of 2, done or not.</summary>
        public struct GoalView { public string Text; public int Have, Need; public bool Done; }

        /// <summary>Today's three goals with the player's progress (zero when nothing was played today).</summary>
        public static List<GoalView> TodayGoals()
        {
            var t = Progress.Shown;
            long day = Today;
            bool now = t.gDay == day;
            DayGoals(day, now ? DayLevel(t) : t.level, tmp, out string hero);
            var list = new List<GoalView>(3);
            foreach (var g in tmp)
            {
                int need = TargetOf(g), have = now ? Math.Min(need, ProgressOf(t, g)) : 0;
                list.Add(new GoalView { Text = GoalText(g, hero), Have = have, Need = need, Done = now && (t.gDone & (1 << (int)g)) != 0 });
            }
            return list;
        }

        static string GoalText(GoalKind g, string hero)
        {
            switch (g)
            {
                case GoalKind.Play: return "Play 3 matches";
                case GoalKind.Win: return "Win 2 matches";
                case GoalKind.Kills: return "Get 10 kills";
                default: return "Play a match as " + (hero == "Nassla" ? "Nässla" : hero);
            }
        }

        /// <summary>The day's first win is still to come (its 100 gold).</summary>
        public static bool FirstWinLeft() => Progress.Shown.winDay != Today;

        // ---------- skins ----------

        public static bool OwnsGoldSkin(string hero)
        {
            var s = Progress.Shown.skins;
            return s != null && s.Contains(hero);
        }

        /// <summary>The hero's premium skin is the player's for good (kept with a season's keep-pick).</summary>
        public static bool KeepsPremium(string hero)
        {
            var p = Progress.Shown.prem;
            return p != null && p.Contains(hero);
        }

        /// <summary>The player is in this season's top 10 now (as the server last said), so every premium skin may be worn.</summary>
        public static bool InTop10 => Progress.Shown.top > 0 && Progress.Shown.top <= TopN;

        /// <summary>0 = the player may wear it; &gt; 0 = buy it for that much gold first (a gold skin, or a premium skin out of the top
        /// 10 with a keep-pick left); -1 = premium: top 10 only.</summary>
        public static int PriceOf(string hero, int skin)
        {
            if (skin <= 0) return 0;
            if (skin < PremiumSkin) return OwnsGoldSkin(hero) ? 0 : SkinPrice;
            if (KeepsPremium(hero) || InTop10) return 0;
            return Progress.Shown.picks > 0 ? KeepPrice : -1;
        }

        /// <summary>A premium skin the player may keep for good now (a keep-pick left, this hero's not kept yet): its price, else 0.</summary>
        public static int KeepPriceOf(string hero, int skin) => skin >= PremiumSkin && Progress.Shown.picks > 0 && !KeepsPremium(hero) ? KeepPrice : 0;

        /// <summary>A premium skin worn only thanks to the top 10 (gone when the player drops out).</summary>
        public static bool OnlyTop10(string hero, int skin) => skin >= PremiumSkin && !KeepsPremium(hero) && InTop10;

        static bool buying;

        /// <summary>Buys the hero's gold skin, or keeps its premium skin for good, on the server. Answers null when done, else why not
        /// (shown to the player).</summary>
        public static async Task<string> BuySkin(string hero, int skin)
        {
            if (buying) return "Wait a moment";
            bool premium = skin >= PremiumSkin;
            if (premium ? KeepsPremium(hero) : OwnsGoldSkin(hero)) return null;
            if (premium && Progress.Shown.picks <= 0) return "Finish a season in the top 10";
            if (Account.State != Account.Status.Ready) return "Go online to buy";
            buying = true;
            try
            {
                await Server.SendPending(); // the gold from matches not sent yet must reach the server first
                if (Progress.PendingCount > 0) return "Could not reach the server, try again";
                if (Progress.Shown.gold < (premium ? KeepPrice : SkinPrice)) return "Not enough gold";
                var a = await Server.BuySkin(hero, premium ? PremiumSkin : GoldSkin);
                if (a.totals != null) Progress.ServerAnswered(a.totals, null);
                return a.ok ? null : string.IsNullOrEmpty(a.reason) ? "Not bought" : a.reason;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] buy skin: " + e.Message);
                return "Could not reach the server, try again";
            }
            finally { buying = false; }
        }
    }

    [Serializable, Preserve]
    public class BuyAnswer
    {
        public bool ok;
        public string reason;
        public Totals totals;
    }
}
