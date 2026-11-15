using BAM.Core;
using BAM.UI;
using UnityEngine;

namespace BAM.Online.UI
{
    /// <summary>
    /// Plugs the online parts into the game (BAM.Core.Flow) and the menus (BAM.UI.OnlineHooks) before the first scene loads, so
    /// the game itself never references BAM.Online. This is README-sv Del 4 except the menu buttons, which read OnlineHooks.
    /// </summary>
    static class OnlineHookup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            // the game calls these in the middle of its own flow (boot, the result card, OnApplicationPause): an online error must
            // never break the offline game, so it is only logged
            Flow.Booted = () => Safe("boot", () => { BAM.Online.Online.Boot(); OnlineUi.Init(); });
            Flow.MatchStarted = () => Safe("match start", BAM.Online.Online.MatchStarted);
            Flow.MatchEnded = r => Safe("match end", () => BAM.Online.Online.MatchEnded(r));
            Flow.Resumed = () => Safe("resume", BAM.Online.Online.Resumed);
            MatchTraces.Protocol = BAM.Online.Mp.MpSocial.Protocol; // a match trace replays only with the rules of its protocol
            Social.Protocol = BAM.Online.Mp.MpSocial.Protocol; // friends see "0.2.0 (10)": the app version alone is the same in every build
            OnlineHooks.Back = OnlineUi.Back;
            OnlineHooks.HomeChip = OnlineUi.HomeChip;
            OnlineHooks.OpenFriends = OnlineUi.OpenFriends;
            OnlineHooks.FriendsOnline = OnlineUi.FriendsOnline;
            OnlineHooks.FriendRequests = () => Social.Incoming.Count;
            OnlineHooks.OpenAccount = OnlineUi.OpenAccount;
            OnlineHooks.OpenPlayFriends = OnlineUi.OpenPlayFriends;
            OnlineHooks.OnlineMatch = OnlineUi.OnlineMatch;
            OnlineHooks.OpenInvite = OnlineUi.OpenInvite;
            OnlineHooks.Searching = OnlineUi.Searching;
            OnlineHooks.AskToGoOnline = OnlineUi.AskToGoOnline;
            OnlineHooks.LastGain = LastGain;
            OnlineHooks.HeroLockLevel = id => Progress.HeroOpen(id) ? 0 : Progress.HeroLevel(id);
            OnlineHooks.Level = () => Progress.Shown.level;
            OnlineHooks.Matches = () => Progress.Shown.matches;
            OnlineHooks.FirstWinXp = () => Account.State == Account.Status.Ready || Account.State == Account.Status.NeedsName
                || Account.State == Account.Status.Offline && Account.Enabled ? Progress.FirstWinXpLeft() : 0;
            OnlineHooks.Gold = () => Progress.Shown.gold;
            // not tied to the sign-in like FirstWinXp: the gold list showed "✓ First win today" (done, green) to a player who was
            // not signed in and had not won; an unsent match still gets its gold once it reaches the server (2026-11-15)
            OnlineHooks.FirstWinGold = () => Gold.FirstWinLeft() ? Gold.FirstWin : 0;
            OnlineHooks.DailyGoals = () =>
            {
                var list = new System.Collections.Generic.List<DailyGoal>(3);
                foreach (var g in Gold.TodayGoals()) list.Add(new DailyGoal { Text = g.Text, Have = g.Have, Need = g.Need, Done = g.Done });
                return list;
            };
            OnlineHooks.SkinPrice = (id, skin) => Gold.PriceOf(id.ToString(), skin);
            OnlineHooks.BuySkin = async (id, done) => { string why = await Gold.BuySkin(id.ToString()); done?.Invoke(why); };
            OnlineHooks.Party = OnlineMenu.Party;
            OnlineHooks.PartyPlay = OnlineMenu.PartyPlay;
            OnlineHooks.LeaveParty = OnlineMenu.LeaveParty;
            OnlineHooks.PartyBots = OnlineMenu.PartyBots;
            OnlineHooks.PartyMode = OnlineMenu.PartyMode;
            OnlineHooks.PartySize = OnlineMenu.PartySize;
            OnlineHooks.PartyMap = OnlineMenu.PartyMap;
            OnlineHooks.Paint = OnlineUi.Paint;
        }

        static void Safe(string what, System.Action a)
        {
            try { a(); }
            catch (System.Exception e) { Debug.LogWarning("[BAM.Online] " + what + ": " + e); }
        }

        // "+25 Rating", "-10 Rating", "+0 Rating"; a win against Easy or Normal bots over their cap: "Max Rating from Easy bots"
        static string RatingText()
        {
            int r = Progress.LastRating;
            string kind = Progress.LastRatingKind;
            if (r == 0 && Progress.LastWon && kind != Progress.Players && Progress.RatingCapOf(kind) > 0 && Progress.Shown.rating >= Progress.RatingCapOf(kind))
                return "Max Rating from " + kind + " bots";
            return (r < 0 ? "-" + (-r) : "+" + r) + " Rating";
        }

        static MatchGain LastGain()
        {
            var t = Progress.Shown;
            Progress.IntoLevel(t.xp, out int have, out int need);
            bool max = t.level >= Progress.MaxLevel;
            return new MatchGain
            {
                Xp = Progress.LastXp, Rating = Progress.LastRating, Level = t.level, LevelBefore = Progress.LastLevelBefore,
                RatingText = RatingText(),
                Gold = Progress.LastGold,
                FirstWin = Progress.LastFirstWin,
                LevelProgress = max || need <= 0 ? 1f : (float)have / need,
                LevelText = max ? "Max level" : have + " / " + need + " XP",
                Unlocked = Progress.LastXp > 0 ? Progress.UnlockedBetween(Progress.LastLevelBefore, t.level) : null,
            };
        }
    }
}
