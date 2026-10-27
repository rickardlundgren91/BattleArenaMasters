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
            Flow.Booted = () => { BAM.Online.Online.Boot(); OnlineUi.Init(); };
            Flow.MatchStarted = BAM.Online.Online.MatchStarted;
            Flow.MatchEnded = BAM.Online.Online.MatchEnded;
            Flow.Resumed = BAM.Online.Online.Resumed;
            OnlineHooks.Back = OnlineUi.Back;
            OnlineHooks.HomeChip = OnlineUi.HomeChip;
            OnlineHooks.OpenFriends = OnlineUi.OpenFriends;
            OnlineHooks.FriendsOnline = OnlineUi.FriendsOnline;
            OnlineHooks.OpenAccount = OnlineUi.OpenAccount;
            OnlineHooks.OpenPlayFriends = OnlineUi.OpenPlayFriends;
            OnlineHooks.OnlineMatch = OnlineUi.OnlineMatch;
            OnlineHooks.AskToGoOnline = OnlineUi.AskToGoOnline;
        }
    }
}
