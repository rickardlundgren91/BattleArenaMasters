using BAM.Core;
using BAM.UI;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// The game's flow behind <see cref="Flow.Game"/>: menus to match and back. The match itself is not built yet, so for
    /// now Start shows a short note and the menus stay up.
    /// </summary>
    sealed class GameFlow : MonoBehaviour, IGameFlow
    {
        public Camera Camera;
        float hintUntil;

        public bool InMatch => false;
        public bool Paused => false;

        public void StartMatch(MatchSetup setup)
        {
            Debug.Log("[BAM.Game] Start pressed: " + setup.Hero + ", " + setup.Difficulty + (setup.Tutorial ? ", guided match" : "") + ". The match is not built yet.");
            Screens.ShowMainMenu();
            Screens.ShowHint("The match is coming soon!");
            hintUntil = Time.unscaledTime + 2.5f;
        }

        public void RestartMatch() { }
        public void Pause() { }
        public void Resume() { }
        public void QuitToMenu() => Screens.ShowMainMenu();

        void Update()
        {
            if (hintUntil > 0f && Time.unscaledTime > hintUntil)
            {
                hintUntil = 0f;
                Screens.HideHint();
            }
        }
    }
}
