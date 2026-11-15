using System;
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
        public IMatchNet Net;   // an online match (BAM.Online); null = offline
        public MapId Map;       // the arena: CLASSIC's MAP pick (BamSettings.LastMap), Runes in the tutorial and HERO TEST; online the room's (Net.Config)
        public int TeamSize;    // heroes per team: 2 (2v2) or 3 (3v3); 0 = 3. Online the room's (Net.Config)
    }

    /// <summary>What the result screen and analytics get when a match ends.</summary>
    public struct MatchResult
    {
        public bool Won;
        public bool Quit;       // the player left from the pause menu
        public float Seconds;
        public int Kills, Deaths;
        public int TowerDeaths, ReadyDeaths; // mistakes: deaths in the enemy tower's reach, deaths with the special ready
        public int Level;       // the player's hero level at the end
        public HeroId Hero;
        public Difficulty Difficulty;
        public bool Tutorial;
        public bool BotsMoved;  // the bot level the player follows moved after this match (BamSettings.AfterBotMatch): the result says so
        public bool Online;     // an online match (MatchSetup.Net): the card says so instead of the bots' level and hides PLAY AGAIN (full review 2026-11-09 nr 61)
        public bool Versus;     // online with a person on the other team ("vs players"); false when everyone played together against bots
        public int Humans;      // the people in the match: 1 offline, one per seat online (analytics, full review 2026-11-13 nr 110)
        public SpellId Spell;   // the player's battle spell (analytics)
        public ItemId Item1, Item2; // the items the player picked, in pick order (None: not picked); analytics' matchEnd
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

        // The online parts (BAM.Online, only in builds with BAM_ONLINE) hook in here from their own assemblies before the game
        // boots; the game never references them, and offline nobody listens.
        /// <summary>The menus are built and Flow.Game is set.</summary>
        public static Action Booted;
        /// <summary>A real match started (not HERO TEST; the guided match once).</summary>
        public static Action MatchStarted;
        /// <summary>That match ended or was left (exactly once, like the analytics' matchEnd).</summary>
        public static Action<MatchResult> MatchEnded;
        /// <summary>The app came back from the background.</summary>
        public static Action Resumed;
    }
}
