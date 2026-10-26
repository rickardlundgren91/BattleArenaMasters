using BAM.Sim;

namespace BAM.Core
{
    /// <summary>What the menus ask for when a match starts.</summary>
    public struct MatchSetup
    {
        public HeroId Hero;
        public Difficulty Difficulty;
        public bool Tutorial;   // the short guided match
        public int Seed;        // 0 = pick one at random
        public bool HeroTest;   // HERO TEST: Hero against dummies, never ends (Reset/Exit on its bar)
        public int Level;       // HERO TEST: the fixed level (1 if 0)
    }

    /// <summary>What the result screen and analytics get when a match ends.</summary>
    public struct MatchResult
    {
        public bool Won;
        public bool Quit;       // the player left from the pause menu
        public float Seconds;
        public int Kills, Deaths;
        public int Level;       // the player's hero level at the end
        public HeroId Hero;
        public Difficulty Difficulty;
        public bool Tutorial;
    }

    /// <summary>
    /// The game's flow, implemented by BAM.Game and set in <see cref="Flow.Game"/> at boot. Menus and the pause screen call it;
    /// they never reach into BAM.Game directly (it references them, not the other way round).
    /// </summary>
    public interface IGameFlow
    {
        void StartMatch(MatchSetup setup);
        void RestartMatch();
        void Pause();
        void Resume();
        void QuitToMenu();
        bool InMatch { get; }
        bool Paused { get; }
    }

    public static class Flow
    {
        public static IGameFlow Game;
    }
}
