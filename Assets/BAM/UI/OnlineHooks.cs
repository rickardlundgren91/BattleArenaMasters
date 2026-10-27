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
        /// <summary>Once, after the first match after the tutorial: asks to go online. True when the question was shown.</summary>
        public static Func<bool> AskToGoOnline;
    }
}
