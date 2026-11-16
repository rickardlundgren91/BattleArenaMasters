using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Scripting;

namespace BAM.Online
{
    /// <summary>
    /// Account level, XP, Rating and lifetime stats (ported from Speltest Progression.cs + PlayerStats, 2026-10-26).
    /// What changed from Speltest: the SERVER is the truth. Cloud Code ReportMatch.js counts every match with the same formula
    /// as <see cref="Apply"/> and keeps the totals in protected Cloud Save data, so an edited phone cannot raise its own level,
    /// Rating or stats. This class only shows them: the last numbers the server sent (<see cref="Server"/>), plus the matches
    /// that are finished but not reported yet (offline, or no network), counted here the same way so the profile moves at once.
    /// A match that waits is sent with the time it ended, so a batch sent later is judged by when each match was played.
    /// </summary>
    public static class Progress
    {
        // Keep these equal to Cloud/Scripts/ReportMatch.js
        public const int MaxLevel = 30;
        public const int XpWin = 100, XpLoss = 60, XpKill = 5, XpFightMax = 60, XpFirstWin = 50;
        // Rating by opponent (Rickard 2026-10-29, 20 Easy wins to Rift): a win gives Easy +15, Normal +35, Hard +75, real players
        // +150; a loss takes 0, 0, 25 and 50. Easy and Normal stop giving Rating at 300 and 700 (Rift, Vanguard). Never under 0.
        // Ultra bots (2026-10-31): +100, a loss -30.
        public const string Players = "Players";
        static readonly string[] RatingKinds = { "Easy", "Normal", "Hard", "Ultra", Players };
        static readonly int[] RatingWin = { 15, 35, 75, 100, 150 }, RatingLoss = { 0, 0, -25, -30, -50 }, RatingCap = { 300, 700, 0, 0, 0 };

        /// <summary>What a match changes the Rating by, the same way as ReportMatch.js ratingChange.</summary>
        public static int RatingChange(int rating, string kind, bool won)
        {
            int k = Math.Max(0, Array.IndexOf(RatingKinds, kind));
            if (!won) return Math.Max(RatingLoss[k], -Math.Max(0, rating));
            return RatingCap[k] > 0 ? Math.Max(0, Math.Min(RatingWin[k], RatingCap[k] - rating)) : RatingWin[k];
        }
        public static int RatingWinOf(string kind) => RatingWin[Math.Max(0, Array.IndexOf(RatingKinds, kind))];
        public static int RatingLossOf(string kind) => -RatingLoss[Math.Max(0, Array.IndexOf(RatingKinds, kind))];
        public static int RatingCapOf(string kind) => RatingCap[Math.Max(0, Array.IndexOf(RatingKinds, kind))];
        static string KindOf(MatchReport m) => string.IsNullOrEmpty(m.vs) ? m.difficulty : Players;
        public const int MinSeconds = 45;
        // a player who never signs in has only these: 50 dropped the oldest from the 51st match on, so the level stalled at ~13 and
        // the XP bar could go back (bug review 2026-11-02, nr 6). 1000 (~230 KB) covers level 30 (~300 matches); the server takes
        // them in batches of 20 (ReportMatch.js MAX_BATCH) once the player signs in
        const int MaxPending = 1000;

        const string ServerKey = "bam.online.state", PendingKey = "bam.online.pending";

        static bool loaded;
        static Totals server = new Totals();
        static readonly List<MatchReport> pending = new List<MatchReport>();
        static Totals shown;

        /// <summary>Raised on the main thread when the numbers on screen change (a match, or an answer from the server).</summary>
        public static event Action Changed;

        /// <summary>The numbers to show: the server's last totals plus the matches still waiting to be sent.</summary>
        public static Totals Shown { get { Load(); return shown; } }
        public static Totals Server { get { Load(); return server; } }
        public static int PendingCount { get { Load(); return pending.Count; } }

        /// <summary>What the last counted match gave, for the result card (Speltest Progression.lastXp etc.). Zero XP = nothing.</summary>
        public static int LastXp, LastRating, LastLevelBefore, LastGold;
        public static bool LastFirstWin, LastWon;
        /// <summary>What the last match counted as for Rating: "Easy", "Normal", "Hard" or <see cref="Players"/>.</summary>
        public static string LastRatingKind;

        /// <summary>Set by the online room when a match against players of the other team starts: the match's seed and this
        /// player's team, so the server can match both sides' reports. Taken (and cleared) by <see cref="MatchEnded"/>.</summary>
        public static string NextVs;
        public static int NextTeam;

        /// <summary>Matches against players the server is still waiting to hear from the other side about.</summary>
        internal static int Waiting;

        // XP to go from level l to l + 1: 100, 150, 200 ... (level 5 after ~9 matches, 10 after ~35, 30 after ~300)
        public static int XpToNext(int l) => 100 + 50 * (l - 1);
        public static int LevelOf(int xp)
        {
            int l = 1;
            while (l < MaxLevel && xp >= XpToNext(l)) { xp -= XpToNext(l); l++; }
            return l;
        }
        /// <summary>XP into the current level and what the level needs, for the bar. At max level (1, 1).</summary>
        public static void IntoLevel(int xp, out int have, out int need)
        {
            int l = 1;
            while (l < MaxLevel && xp >= XpToNext(l)) { xp -= XpToNext(l); l++; }
            if (l >= MaxLevel) { have = 1; need = 1; } else { have = xp; need = XpToNext(l); }
        }

        // ---------- hero unlocks (Speltest Progression.cs, Rickard 2026-10-27: "Hjältar låses upp med nivå") ----------
        // The first FreeHeroes of the hero list (HeroCatalog.All: Tyra, Knut, Fenn since 2026-11-16; Bram before) are open from the start, then one more every
        // HeroLevelStep levels: Moa 2, Bram 4 (Knut until 2026-11-16), Tikta 6, Blast 8, Bonk 10 (~35 matches), Hilde 12, Oaken 14. Only the menu is locked: HERO TEST (TRY)
        // and the bots still use every hero, so a player meets the heroes they unlock later. The level is the server's, so the
        // locks follow the account to a new phone.
        public const int FreeHeroes = 3, HeroLevelStep = 2;

        /// <summary>The account level that opens this hero (1 = open from the start).</summary>
        public static int HeroLevel(HeroId id)
        {
            if (AlwaysOpen(id)) return 1;
            // the place in the list without the always-open heroes, so Kugg going in second moved nobody's level
            int i = 0;
            foreach (var h in HeroCatalog.All)
            {
                if (h.Id == id) break;
                if (!AlwaysOpen(h.Id)) i++;
            }
            return i < FreeHeroes ? 1 : HeroLevelStep * (i - FreeHeroes + 1);
        }

        /// <summary>Heroes open for every player from the start, whatever their place in the list: Kugg (Rickard 2026-11-09 "lägg in
        /// honom som nr 2 i spelet och upplåst för alla"). They stand with the free heroes at the top of HeroCatalog.All, so the open
        /// heroes are always the first cards (Rickard 2026-11-13).</summary>
        static bool AlwaysOpen(HeroId id) => id == HeroId.Kugg || id == HeroId.Ragn || id == HeroId.Nassla || id == HeroId.Ravna; // Ragn, Nässla: 0.4.0, "tillgängliga för alla" (Rickard 2026-11-10); Ravna 2026-11-14 ("lägg in henne")

        // in the Unity Editor every hero is always open (Rickard 2026-10-29: "när jag kör på datorn måste jag alltid få ha alla heroes
        // upplåsta"); on a phone the level decides
        public static bool HeroOpen(HeroId id) => Application.isEditor || Shown.level >= HeroLevel(id);

        /// <summary>The heroes that open when the level goes from `from` to `to`, e.g. "Moa" or "Knut, Tikta"; null for none.</summary>
        public static string UnlockedBetween(int from, int to)
        {
            string r = null;
            foreach (var h in HeroCatalog.All)
            {
                int l = HeroLevel(h.Id);
                if (l > from && l <= to) r = r == null ? h.Name : r + ", " + h.Name;
            }
            return r;
        }

        /// <summary>The next hero still locked, e.g. "Level 4: Knut", or null when every hero is open.</summary>
        public static string NextUnlock()
        {
            int lv = Shown.level;
            for (int l = lv + 1; l <= MaxLevel; l++) { var u = UnlockedBetween(l - 1, l); if (u != null) return "Level " + l + ": " + u; }
            return null;
        }

        /// <summary>The XP today's first win still gives: XpFirstWin, or 0 once today (UTC day, as MatchEnded counts it) has a win.</summary>
        public static int FirstWinXpLeft() => Shown.winDay != DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400 ? XpFirstWin : 0;
        public static int XpFor(bool won, int kills, bool firstWinToday)
            => (won ? XpWin : XpLoss) + Math.Min(XpFightMax, Math.Max(0, kills) * XpKill) + (won && firstWinToday ? XpFirstWin : 0);

        /// <summary>The tutorial, matches left from the pause menu and very short matches give nothing (as in Speltest).</summary>
        public static bool Counts(MatchResult r) => !r.Tutorial && !r.Quit && r.Seconds >= MinSeconds && HeroCatalog.InGame((int)r.Hero); // never a preview hero (Ragn, Nässla: editor only) to ReportMatch

        /// <summary>Called by the game once per finished match. Queues it for the server and updates the shown numbers.</summary>
        public static void MatchEnded(MatchResult r)
        {
            Load();
            LastXp = LastRating = LastGold = 0;
            string vs = NextVs; int team = NextTeam;
            NextVs = null;
            // leaving a match against players still counts it as lost for Rating (no XP), so the other side gets its win
            bool quitVs = vs != null && r.Quit && !r.Tutorial;
            if (!Counts(r) && !quitVs) return;
            var m = new MatchReport
            {
                id = Guid.NewGuid().ToString("N"),
                at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                won = r.Won && !quitVs,
                seconds = (int)r.Seconds,
                kills = r.Kills,
                deaths = r.Deaths,
                towerDeaths = r.TowerDeaths,
                readyDeaths = r.ReadyDeaths,
                hero = r.Hero.ToString(),
                difficulty = r.Difficulty.ToString(),
                vs = vs ?? "",
                team = team,
                quit = quitVs,
            };
            var before = shown;
            long day = m.at / 86400;
            LastFirstWin = !m.quit && m.won && before.winDay != day;
            LastXp = m.quit ? 0 : XpFor(m.won, m.kills, LastFirstWin);
            LastRatingKind = KindOf(m);
            LastWon = m.won;
            LastRating = RatingChange(before.rating, LastRatingKind, m.won);
            LastLevelBefore = before.level;
            pending.Add(m);
            if (pending.Count > MaxPending) pending.RemoveAt(0);
            SavePending();
            Recount();
            LastGold = Math.Max(0, shown.gold - before.gold);
        }

        // ---------- what the server says ----------

        internal static List<MatchReport> Pending { get { Load(); return pending; } }

        /// <summary>The server counted (or refused) these matches: drop them and take its totals.</summary>
        internal static void ServerAnswered(Totals totals, ICollection<string> doneIds, int waiting = -1)
        {
            Load();
            if (totals != null) server = totals;
            if (waiting >= 0) Waiting = waiting;
            if (doneIds != null && doneIds.Count > 0)
                for (int i = pending.Count - 1; i >= 0; i--)
                    if (doneIds.Contains(pending[i].id)) pending.RemoveAt(i);
            PlayerPrefs.SetString(ServerKey, JsonUtility.ToJson(server));
            SavePending();
            Recount();
        }

        /// <summary>Another account signed in (or the account was deleted): forget the old totals, keep unsent matches.</summary>
        internal static void ResetServer()
        {
            Load();
            server = new Totals();
            PlayerPrefs.DeleteKey(ServerKey);
            Recount();
        }

        // ---------- counting ----------

        /// <summary>One match added to totals, the same way as ReportMatch.js. Used for matches the server has not seen yet.</summary>
        public static void Apply(Totals t, MatchReport m)
        {
            t.rating += RatingChange(t.rating, KindOf(m), m.won && !m.quit);
            if (m.quit) return;   // a match against players that was left: only its Rating
            long day = m.at / 86400;
            bool firstWin = m.won && t.winDay != day;
            int levelBefore = LevelOf(t.xp);
            t.xp += XpFor(m.won, m.kills, firstWin);
            Gold.Apply(t, day, m.won, m.kills, m.hero, firstWin, levelBefore, LevelOf(t.xp));
            if (firstWin) t.winDay = day;
            t.matches++;
            if (m.won) { t.wins++; t.streak++; if (t.streak > t.bestStreak) t.bestStreak = t.streak; }
            else { t.losses++; t.streak = 0; }
            t.kills += Math.Max(0, m.kills);
            t.deaths += Math.Max(0, m.deaths);
            t.seconds += Math.Max(0, m.seconds);
            t.level = LevelOf(t.xp);
            var h = t.Hero(m.hero);
            h.m++;
            if (m.won) h.w++;
        }

        static void Recount()
        {
            shown = server.Copy();
            shown.rating = Math.Max(0, shown.rating + shown.pend);   // matches against players the server waits to confirm
            for (int i = 0; i < pending.Count; i++) Apply(shown, pending[i]);
            shown.level = LevelOf(shown.xp);
            Changed?.Invoke();
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                var s = PlayerPrefs.GetString(ServerKey, "");
                if (s.Length > 0) server = JsonUtility.FromJson<Totals>(s) ?? new Totals();
                var p = PlayerPrefs.GetString(PendingKey, "");
                if (p.Length > 0)
                {
                    var list = JsonUtility.FromJson<PendingList>(p);
                    if (list != null && list.items != null) pending.AddRange(list.items);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] saved progress unreadable, starting over: " + e.Message);
                server = new Totals();
                pending.Clear();
            }
            shown = server.Copy();
            shown.rating = Math.Max(0, shown.rating + shown.pend);
            for (int i = 0; i < pending.Count; i++) Apply(shown, pending[i]);
        }

        static void SavePending()
        {
            PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(new PendingList { items = pending.ToArray() }));
            PlayerPrefs.Save();
        }

        [Serializable] class PendingList { public MatchReport[] items; }
    }

    /// <summary>One finished match as sent to Cloud Code ReportMatch. Field names are the JSON keys the script reads.</summary>
    [Serializable, Preserve]
    public class MatchReport
    {
        public string id;          // so a match sent twice (lost answer) is counted once
        public long at;            // unix seconds when it ended
        public bool won;
        public int seconds, kills, deaths;
        public int towerDeaths, readyDeaths; // mistakes (2026-10-31): deaths in the enemy tower's reach, with the special ready
        public string hero, difficulty;
        public string vs;          // against players: "v" + the match's seed, the same on every phone in it; "" against bots
        public int team;           // with vs: this player's team (0 or 1)
        public bool quit;          // with vs: the player left before the end (counts only as a Rating loss)
    }

    /// <summary>A player's totals. Same shape as the "bam-progress" protected Cloud Save item and the public profile.</summary>
    [Serializable, Preserve]
    public class Totals
    {
        public int xp, level = 1, rating, matches, wins, losses, kills, deaths, streak, bestStreak;
        public int rs, pend;   // Rating season; Rating from matches against players that wait for the other side's report
        public long seconds, winDay = -1;
        public List<HeroTotals> heroes = new List<HeroTotals>();
        // gold (Gold.cs, 2026-11-15): the balance; today's goals (gDay, matches, wins, kills, hero goal 0/1, done bits); the heroes
        // whose gold skin is bought
        public int gold, gm, gw, gk, gh, gDone, gLv; // gLv: the level today's goals started at (picks the hero goal's hero)
        public long gDay = -1;
        public List<string> skins = new List<string>();

        public HeroTotals Hero(string id)
        {
            for (int i = 0; i < heroes.Count; i++) if (heroes[i].id == id) return heroes[i];
            var h = new HeroTotals { id = id };
            heroes.Add(h);
            return h;
        }

        /// <summary>The most played heroes first.</summary>
        public void TopHeroes(List<HeroTotals> into, int max)
        {
            into.Clear();
            into.AddRange(heroes);
            into.Sort((a, b) => b.m.CompareTo(a.m));
            if (into.Count > max) into.RemoveRange(max, into.Count - max);
        }

        public Totals Copy()
        {
            var c = (Totals)MemberwiseClone();
            c.heroes = new List<HeroTotals>(heroes.Count);
            for (int i = 0; i < heroes.Count; i++) c.heroes.Add(new HeroTotals { id = heroes[i].id, m = heroes[i].m, w = heroes[i].w });
            c.skins = skins != null ? new List<string>(skins) : new List<string>();
            return c;
        }
    }

    [Serializable, Preserve]
    public class HeroTotals
    {
        public string id;
        public int m, w;   // matches, wins
    }
}
