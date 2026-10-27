using BAM.Core;
using UnityEngine;

namespace BAM.Online
{
    /// <summary>
    /// The three calls the rest of the game makes (only in builds with BAM_ONLINE; see README-sv.md for the hook lines):
    ///   Boot:        BAM.Online.Online.Boot();
    ///   Match start: BAM.Online.Online.MatchStarted();
    ///   Match end:   BAM.Online.Online.MatchEnded(result);
    /// </summary>
    public static class Online
    {
        static bool booted;

        public static void Boot()
        {
            if (booted) return;
            booted = true;
            Account.Start();
        }

        public static void MatchStarted() => Social.SetInMatch(true);

        public static void MatchEnded(MatchResult result)
        {
            Social.SetInMatch(false);
            Progress.MatchEnded(result);
            if (Account.State == Account.Status.Ready) _ = Server.SendPending();
            else if (Account.State == Account.Status.Offline) Account.Start();
        }

        /// <summary>The app came back from the background: retry a failed sign-in and send waiting matches.</summary>
        public static void Resumed()
        {
            if (Account.State == Account.Status.Offline) Account.Start();
            else if (Account.SessionLost) Account.Start();   // the Unity session expired in the background: sign in again
            else if (Account.State == Account.Status.Ready) _ = Server.SendPending();
        }
    }
}
