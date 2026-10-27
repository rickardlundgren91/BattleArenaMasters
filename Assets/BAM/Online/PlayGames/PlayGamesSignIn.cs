using System.Threading.Tasks;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;

namespace BAM.Online
{
    /// <summary>
    /// Google Play Games sign-in (plugin v2, play-games-plugin-for-unity). Play Games v2 signs the player in by itself when
    /// the game starts; when that fails (cancelled, Play Games missing) the "Sign in" button calls ManuallyAuthenticate.
    /// After a success the plugin hands out a one-time server code, which Unity Authentication turns into the player's
    /// Unity account (Account.cs). Needs the Web client ID (Game server credential) set in Window > Google Play Games > Setup.
    /// </summary>
    public sealed class PlayGamesSignIn : IPlatformSignIn
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (Application.platform != RuntimePlatform.Android) return;   // the editor uses the test account (Account.cs)
            PlayGamesPlatform.Activate();
            Account.Platform = new PlayGamesSignIn();
        }

        public Task<PlatformSignIn> SignIn(bool interactive)
        {
            var done = new TaskCompletionSource<PlatformSignIn>();
            void Signed(SignInStatus status)
            {
                if (status != SignInStatus.Success)
                {
                    done.TrySetResult(new PlatformSignIn { Ok = false, Error = status == SignInStatus.Canceled ? "Sign-in was cancelled" : "Google Play Games sign-in failed" });
                    return;
                }
                // an exception in a plugin callback would leave the Task waiting forever (Account.cs then times out)
                try
                {
                    string display = PlayGamesPlatform.Instance.GetUserDisplayName();
                    PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
                    {
                        // no code also when Google refused the consent (403 access_denied: the account is not an OAuth test user)
                        if (string.IsNullOrEmpty(code)) done.TrySetResult(new PlatformSignIn { Ok = false, Error = "Google Play Games gave no server code" });
                        else done.TrySetResult(new PlatformSignIn { Ok = true, ServerCode = code, DisplayName = display });
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BAM.Online] Play Games server access: " + e);
                    done.TrySetResult(new PlatformSignIn { Ok = false, Error = "Google Play Games sign-in failed" });
                }
            }
            try
            {
                if (interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(Signed);
                else PlayGamesPlatform.Instance.Authenticate(Signed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BAM.Online] Play Games sign-in: " + e);
                done.TrySetResult(new PlatformSignIn { Ok = false, Error = "Google Play Games sign-in failed" });
            }
            return done.Task;
        }
    }
}
