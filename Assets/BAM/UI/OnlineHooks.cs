using System;
using UnityEngine;

namespace BAM.UI
{
    /// <summary>
    /// Where the online pages (BAM.Online.UI, only in builds with BAM_ONLINE) plug into the menus. BAM.Online.UI references BAM.UI,
    /// so BAM.UI cannot call it directly: the online side fills these before the game boots (OnlineHookup), and the menus show
    /// an online entry only when its hook is set. Offline every hook is null and the menus look as before.
    /// </summary>
    public static class OnlineHooks
    {
        /// <summary>Android Back: true when an online page or invite was open and closed.</summary>
        public static Func<bool> Back;
        /// <summary>Home's top-left profile chip (the old one, cw(40) x cw(12)), built in the given parent; a tap opens the profile.</summary>
        public static Func<RectTransform, RectTransform> HomeChip;
        public static Action OpenFriends, OpenAccount, OpenPlayFriends, OnlineMatch;
        /// <summary>How many friends are online now (the badge on Home's FRIENDS button).</summary>
        public static Func<int> FriendsOnline;
        /// <summary>Once, after the first match after the tutorial: asks to go online. True when the question was shown.</summary>
        public static Func<bool> AskToGoOnline;
        /// <summary>What the match that just ended gave the account, for the result card (Xp 0 = show nothing).</summary>
        public static Func<MatchGain> LastGain;
        /// <summary>The account level that opens a hero, 0 when it is open (offline builds: null, every hero open).</summary>
        public static Func<BAM.Sim.HeroId, int> HeroLockLevel;
        /// <summary>The account level now.</summary>
        public static Func<int> Level;
    }

    /// <summary>The account XP line on the result card (Speltest HudV3 drawResult): "+160 XP (first win today)", the level bar.</summary>
    public struct MatchGain
    {
        public int Xp, Rating, Level, LevelBefore;
        public bool FirstWin;
        /// <summary>0..1 into the current level, and its text: "40 / 150 XP" or "Max level".</summary>
        public float LevelProgress;
        public string LevelText;
        /// <summary>Heroes the level-up opened, e.g. "Moa"; null for none.</summary>
        public string Unlocked;
    }
}
