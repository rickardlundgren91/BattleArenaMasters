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
            OnlineHooks.Back = OnlineUi.Back;
            OnlineHooks.HomeChip = OnlineUi.HomeChip;
            OnlineHooks.OpenFriends = OnlineUi.OpenFriends;
            OnlineHooks.FriendsOnline = OnlineUi.FriendsOnline;
            OnlineHooks.OpenAccount = OnlineUi.OpenAccount;
            OnlineHooks.OpenPlayFriends = OnlineUi.OpenPlayFriends;
            OnlineHooks.OnlineMatch = OnlineUi.OnlineMatch;
            OnlineHooks.OpenInvite = OnlineUi.OpenInvite;
            OnlineHooks.Searching = OnlineUi.Searching;
            OnlineHooks.AskToGoOnline = OnlineUi.AskToGoOnline;
            OnlineHooks.LastGain = LastGain;
            OnlineHooks.HeroLockLevel = id => Progress.HeroOpen(id) ? 0 : Progress.HeroLevel(id);
            OnlineHooks.Level = () => Progress.Shown.level;
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
                FirstWin = Progress.LastFirstWin,
                LevelProgress = max || need <= 0 ? 1f : (float)have / need,
                LevelText = max ? "Max level" : have + " / " + need + " XP",
                Unlocked = Progress.LastXp > 0 ? Progress.UnlockedBetween(Progress.LastLevelBefore, t.level) : null,
            };
        }
    }
}
