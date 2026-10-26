using BAM.Analytics;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = BAM.Sim.PlayerInput;

namespace BAM.Game
{
    /// <summary>
    /// The game's flow behind <see cref="Flow.Game"/>: menus to match and back. In a match it steps the rules at a fixed
    /// 60 Hz (at most 4 ticks per frame) and draws between ticks; views, camera and HUD only read the rules.
    /// </summary>
    sealed class GameFlow : MonoBehaviour, IGameFlow
    {
        public Camera Camera;

        // a match that falls further behind than this drops time instead of fast-forwarding (a slow phone plays slower, not jumpier)
        const int MaxTicksPerFrame = 4;
        // the last moment stays on screen this long before the result
        const float ResultDelay = 2f;
        // the result's jingle plays alone this long, then the menu music comes back on the result screen
        const float ResultMusicDelay = 2.5f;

        MatchSetup setup;
        Match match;
        MatchViews views;
        MatchCamera cam;
        Hud hud;
        bool inMatch, paused, over, resultShown, resultMusic, endSent;
        float acc, overAt;
        // the guided match is two matches: Fenn's training island, then Tyra's first battle in the arena
        float islandSeconds;

        public bool InMatch => inMatch;
        public bool Paused => paused;

        public void StartMatch(MatchSetup s)
        {
            Teardown();
            setup = s;
            Begin(setup.Tutorial);
        }

        public void RestartMatch()
        {
            if (match == null) return;
            SendEnd(true);
            Teardown();
            Begin(setup.Tutorial);
        }

        public void Pause()
        {
            if (!inMatch || paused || resultShown) return;
            paused = true;
            hud.CancelTouches();
            BamAudio.StopAll();
            Screens.ShowPause();
        }

        public void Resume()
        {
            if (!inMatch || !paused) return;
            paused = false;
            acc = 0f;
            Screens.HidePause();
            BamAudio.PlayMusic(MusicId.Match); // Pause silenced it; it goes on where it was
        }

        public void QuitToMenu()
        {
            SendEnd(true);
            Teardown();
            Screens.ShowMainMenu();
        }

        void Begin(bool island)
        {
            if (island) islandSeconds = 0f;
            int seed = setup.Seed != 0 ? setup.Seed : Random.Range(1, int.MaxValue);
            var cfg = new MatchConfig
            {
                PlayerHero = setup.Tutorial ? HeroId.Tyra : setup.Hero,
                Difficulty = setup.Difficulty,
                Seed = seed,
                Tutorial = setup.Tutorial,
                Island = island,
            };
            match = Match.Create(cfg);
            Screens.HideAll();
            views = new MatchViews(match);
            if (cam == null) cam = new MatchCamera(Camera);
            cam.Begin(match);
            if (hud == null) hud = new Hud(this);
            hud.Begin(match, views, cam);
            inMatch = true; paused = false; over = false; resultShown = false; resultMusic = false; endSent = false;
            acc = 0f;
            // Rickard's loop plays through the match (also across the guided match's two halves); the result screen and
            // leaving the match fade it out
            BamAudio.PlayMusic(MusicId.Match);
            if (!setup.Tutorial || island) BamAnalytics.MatchStarted(setup);
            Debug.Log("[BAM.Game] match started: " + cfg.PlayerHero + ", " + cfg.Difficulty + (cfg.Tutorial ? (island ? ", guided match (island)" : ", guided match (arena)") : "") + ", seed " + seed);
        }

        void Teardown()
        {
            if (match == null) return;
            inMatch = false; paused = false;
            hud.End();
            views.Dispose();
            views = null;
            cam.End();
            match = null;
            BamAudio.StopAll();
            Screens.HideHint();
        }

        void Update()
        {
            if (!inMatch) return;
            var kb = Keyboard.current;
            // Android's back button arrives as Escape: it closes our menus first, else it pauses
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !Screens.HandleBack()) Pause();
            if (!inMatch) return; // the back press may have left the match

            float dt = Time.deltaTime;
            if (!paused && !over)
            {
                acc += dt;
                int n = 0;
                while (acc >= Rules.Tick && n < MaxTicksPerFrame)
                {
                    PlayerInput input = hud.TakeInput();
                    match.Step(in input);
                    views.AfterStep();
                    hud.AfterStep();
                    acc -= Rules.Tick;
                    n++;
                    if (IslandDone()) { SecondHalf(); return; }
                    if (IsOver()) { over = true; overAt = Time.unscaledTime; acc = 0f; break; }
                }
                if (acc >= Rules.Tick) acc = 0f;
            }
            float alpha = paused || over ? 1f : Mathf.Clamp01(acc / Rules.Tick);
            views.Frame(alpha, dt);
            cam.Frame(alpha, dt, over);
            BamAudio.SetListener(cam.Focus);
            hud.Frame(dt, paused || resultShown);

            if (over && !resultShown && Time.unscaledTime - overAt >= ResultDelay) ShowResult();
            else if (resultShown && !resultMusic && Time.unscaledTime - overAt >= ResultDelay + ResultMusicDelay)
            {
                resultMusic = true;
                BamAudio.PlayMusic(MusicId.Menu);
            }
        }

        bool IslandDone() => match.Tut != null && match.Tut.Island && match.Tut.Done;

        // the island's pad threw Fenn and the screen is black: the arena half starts behind it and fades in
        void SecondHalf()
        {
            islandSeconds = match.Time;
            Teardown();
            Begin(false);
            hud.FadeIn();
        }

        bool IsOver() => match.State == MatchState.Over || (match.Tut != null && match.Tut.Done);

        MatchResult Result(bool quit)
        {
            var p = match.Player;
            bool won = match.State == MatchState.Over ? match.Winner == Teams.Player : match.Tut != null && match.Tut.Done;
            return new MatchResult
            {
                Won = !quit && won,
                Quit = quit,
                Seconds = Mathf.Max(0f, match.Time) + (match.Tut != null && !match.Tut.Island ? islandSeconds : 0f),
                Kills = p.Kills,
                Deaths = p.Deaths,
                Level = p.Level,
                Hero = p.Hero,
                Difficulty = setup.Difficulty,
                Tutorial = setup.Tutorial,
            };
        }

        void ShowResult()
        {
            resultShown = true;
            var r = Result(false);
            if (r.Tutorial && r.Won) BamSettings.TutorialDone = true;
            SendEnd(false);
            hud.CancelTouches();
            BamAudio.StopMusic(); // the victory or defeat jingle first; Update brings the music back after it
            Screens.ShowResult(r);
        }

        // Exactly one MatchEnded per match: at the result, or as a quit when the player leaves or restarts before it.
        void SendEnd(bool quit)
        {
            if (match == null || endSent) return;
            endSent = true;
            BamAnalytics.MatchEnded(setup, Result(quit && !over));
        }

        void OnApplicationPause(bool pausedByOs)
        {
            if (pausedByOs) Pause();
        }
    }
}
