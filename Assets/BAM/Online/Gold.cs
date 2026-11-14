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
    /// gives none, so playing bots all day cannot farm it. Gold buys each hero's first skin (1500, about a week of daily play);
    /// gold is never sold for money and heroes are never bought. The SERVER counts it (ReportMatch.js, the same formulas as
    /// <see cref="Apply"/>) and keeps the gold and the bought skins in the protected "bam-progress" item; BuySkin.js spends it.
    /// The day is the UTC day (01:00 Swedish winter time), as the first win's XP already counts it.
    /// </summary>
    public static class Gold
    {
        // keep equal to Cloud/Scripts/ReportMatch.js and BuySkin.js
        public const int FirstWin = 100, Goal = 50, LevelUp = 100, SkinPrice = 1500;
        /// <summary>The skin gold buys: the hero's first skin (HeroSkins 1). The others wait for real-money purchases.</summary>
        public const int GoldSkin = 1;
        /// <summary>Skins after the gold one show "COMING SOON" and cannot be worn until purchases exist.</summary>
        public static bool MoneySkinsLocked = true;

        public enum GoalKind { Play = 0, Win = 1, Kills = 2, Hero = 3 }
        static readonly int[] Target = { 3, 2, 10, 1 };
        /// <summary>The heroes "play a match with ..." can ask for: open to every player from the start.</summary>
        public static readonly string[] GoalHeroes = { "Tyra", "Kugg", "Bram", "Fenn", "Ragn", "Nassla" };

        /// <summary>The day's three goals: all four kinds but one, and the hero the hero goal asks for, both picked from the day number
        /// alone, so the phone and the server agree without storing anything.</summary>
        public static void DayGoals(long day, List<GoalKind> into, out string hero)
        {
            uint h = unchecked((uint)((ulong)day * 2654435761UL));
            int skip = (int)(h % 4);
            hero = GoalHeroes[(int)((h >> 8) % (uint)GoalHeroes.Length)];
            into.Clear();
            for (int g = 0; g < 4; g++) if (g != skip) into.Add((GoalKind)g);
        }

        public static int TargetOf(GoalKind g) => Target[(int)g];

        public static int ProgressOf(Totals t, GoalKind g) => g == GoalKind.Play ? t.gm : g == GoalKind.Win ? t.gw : g == GoalKind.Kills ? t.gk : t.gh;

        public static long Today => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;

        static readonly List<GoalKind> tmp = new List<GoalKind>(3);

        /// <summary>The gold one counted match gives, added to t (called by Progress.Apply after XP and level).</summary>
        internal static void Apply(Totals t, long day, bool won, int kills, string hero, bool firstWin, int levelBefore, int levelAfter)
        {
            if (levelAfter > levelBefore) t.gold += LevelUp * (levelAfter - levelBefore);
            if (firstWin) t.gold += FirstWin;
            if (day > t.gDay) { t.gDay = day; t.gm = t.gw = t.gk = t.gh = t.gDone = 0; }
            if (day != t.gDay) return; // a late match from an earlier day: its goals are gone
            t.gm++;
            if (won) t.gw++;
            t.gk += Math.Min(60, Math.Max(0, kills));
            DayGoals(day, tmp, out string goalHero);
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
            DayGoals(day, tmp, out string hero);
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

        /// <summary>0 = the player may wear it; SkinPrice = buy it for gold first; -1 = coming soon (sold for money later).</summary>
        public static int PriceOf(string hero, int skin)
        {
            if (skin <= 0) return 0;
            if (skin == GoldSkin) return OwnsGoldSkin(hero) ? 0 : SkinPrice;
            return MoneySkinsLocked ? -1 : 0;
        }

        static bool buying;

        /// <summary>Buys the hero's gold skin on the server. Answers null when bought, else why not (shown to the player).</summary>
        public static async Task<string> BuySkin(string hero)
        {
            if (buying) return "Wait a moment";
            if (OwnsGoldSkin(hero)) return null;
            if (Account.State != Account.Status.Ready) return "Go online to buy";
            buying = true;
            try
            {
                await Server.SendPending(); // the gold from matches not sent yet must reach the server first
                if (Progress.PendingCount > 0) return "Could not reach the server, try again";
                if (Progress.Shown.gold < SkinPrice) return "Not enough gold";
                var a = await Server.BuySkin(hero);
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
