using System.Collections;
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
        readonly MatchJuice juice = new MatchJuice();
        readonly MatchIntro intro = new MatchIntro();
        bool inMatch, paused, over, resultShown, resultMusic, endSent;
        float acc, overAt;
        // online: how far the drawing is past the last stepped tick. It counts on the local clock but is used up only by ticks
        // really stepped, so a frame without a new frame from the host holds on the newest tick instead of sliding back a tick
        // (precision CHAINMAP-08: a guest stepped 0 ticks in ~4 % of frames and its alpha restarted)
        float netDraw;
        // an online match (BAM.Online lockstep): the rules step only with the ticks the link knows, never on their own
        IMatchNet net;
        PlayerInput[] netInputs;
        string netStatus;
        // a guest that fell behind steps up to this many extra ticks a frame to catch up
        const int NetCatchUp = 4, NetFarBehind = 12;
        // the guided match is two matches with one hero: the training island, then the first battle in the arena (Rickard 2026-10-26: one hero all through, Tyra)
        float islandSeconds;
        // the first start shows Home this long before the guided match takes over (its picture zooms in and fades, Hud.Dissolve)
        const float FirstTutorialDelay = 0.6f;

        public bool InMatch => inMatch;
        public bool Paused => paused;
        /// <summary>Seconds since the match ended (the tower fell), -1 before.</summary>
        public float OverSeconds => over ? Time.unscaledTime - overAt : -1f;
        // the guided match's arena half ended with the tower down: its own end screen (Hud.TutorialEnd), no result card
        bool TutorialEnd => setup.Tutorial && match != null && match.Tut != null && match.Tut.Done && !match.Tut.Island;

        /// <summary>
        /// The very first start (the old MenuNewPlayer.NewPlayerTick, Tutorial 2.0): no question, the guided match starts by itself
        /// a moment after Home comes up, under a picture of Home that zooms in and fades into the island.
        /// </summary>
        public void StartFirstTutorial() => StartCoroutine(FirstTutorial());

        IEnumerator FirstTutorial()
        {
            yield return new WaitForSecondsRealtime(FirstTutorialDelay);
            if (inMatch || BamSettings.TutorialDone) yield break; // the player was quicker
            yield return new WaitForEndOfFrame();
            var home = ScreenCapture.CaptureScreenshotAsTexture();
            StartMatch(new MatchSetup { Hero = HeroId.Tyra, Difficulty = Difficulty.Easy, Tutorial = true });
            hud.Dissolve(home);
        }

        /// <summary>The guided match's Skip button: the tutorial counts as done, then Home (the old skipTutorial).</summary>
        public void SkipTutorial()
        {
            if (!inMatch || !setup.Tutorial) return;
            TutorialFinished();
            SendEnd(true); // in the analytics a skip is a quit
            Teardown();
            Screens.ShowMainMenu();
        }

        /// <summary>A tap on the guided match's end screen (TOWER DESTROYED!): Home, with the "Tutorial done" card.</summary>
        public void FinishTutorial()
        {
            if (!inMatch || !TutorialEnd) return;
            TutorialFinished();
            SendEnd(false);
            Teardown();
            Screens.ShowMainMenu();
        }

        // The first time only (the old NewPlayer.FinishTutorial and FirstPicks): the card on Home, and the first match is Tyra
        // against Easy bots unless the player has already picked something.
        static void TutorialFinished()
        {
            if (BamSettings.TutorialDone) return;
            BamSettings.TutorialDone = true;
            if (!BamSettings.PickedBefore)
            {
                BamSettings.LastHero = (int)HeroId.Tyra;
                BamSettings.LastDifficulty = Difficulty.Easy;
            }
            BamSettings.TutorialCard = true;
        }

        public void StartMatch(MatchSetup s)
        {
            SendEnd(true); // a match left for another one (Settings > How to play from Pause) still gets its MatchEnded
            Teardown();
            setup = s;
            Begin(setup.Tutorial);
        }

        public void RestartMatch()
        {
            if (match == null) return;
            if (net != null) { QuitToMenu(); return; } // an online match cannot be restarted alone: back to the room
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
            BAM.Art.ArtKit.SetPaused(true); // effects, zones and shots wait with the match
            Screens.ShowPause();
        }

        public void Resume()
        {
            if (!inMatch || !paused) return;
            paused = false;
            acc = 0f;
            BAM.Art.ArtKit.SetPaused(false);
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
            net = setup.Net;
            var cfg = net != null ? net.Config : new MatchConfig
            {
                PlayerHero = setup.Tutorial ? HeroId.Tyra : setup.Hero,
                Difficulty = setup.Difficulty,
                Seed = seed,
                Tutorial = setup.Tutorial,
                Island = island,
                PlayerSpell = BamSettings.Spell, // the battle spell picked on the hero page (off in the guided match)
                HeroTest = setup.HeroTest && !setup.Tutorial,
                HeroTestLevel = setup.Level > 0 ? setup.Level : 1,
            };
            if (net != null) { seed = cfg.Seed; netInputs = new PlayerInput[Match.MaxSeats]; netStatus = null; }
            Coords.Turned = net != null && net.Turned;
            match = Match.Create(cfg);
            Screens.HideAll();
            views = new MatchViews(match);
            if (cam == null) cam = new MatchCamera(Camera);
            cam.Begin(match);
            juice.Begin(match);
            intro.Begin(match, views);
            if (hud == null) hud = new Hud(this);
            hud.Intro = intro;
            hud.Begin(match, views, cam);
            inMatch = true; paused = false; over = false; resultShown = false; resultMusic = false; endSent = false;
            acc = 0f; netDraw = 0f;
            // Rickard's loop plays through the match (also across the guided match's two halves); the result screen and
            // leaving the match fade it out
            BamAudio.PlayMusic(MusicId.Match);
            if ((!setup.Tutorial || island) && !setup.HeroTest)
            {
                BamAnalytics.MatchStarted(setup); // HERO TEST is practice, not a match (old: no stats)
                Flow.MatchStarted?.Invoke();      // online: "in a match" for friends
            }
            Debug.Log("[BAM.Game] match started: " + (net != null ? "online, seat " + cfg.LocalSeat + " of " + cfg.Seats.Length + ", " : "") + cfg.PlayerHero + ", " + cfg.Difficulty + (cfg.Tutorial ? (island ? ", guided match (island)" : ", guided match (arena)") : "") + (cfg.HeroTest ? ", HERO TEST level " + cfg.HeroTestLevel : "") + ", seed " + seed);
        }

        void Teardown()
        {
            if (match == null) return;
            inMatch = false; paused = false;
            BAM.Art.ArtKit.SetSimClock(-1, 0f);
            hud.End();
            intro.End(cam.Camera);
            views.Dispose();
            views = null;
            BAM.Art.ArtKit.ClearEffects(); // every effect of the match goes (Bonk's too), and the art pause is lifted
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
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !Screens.HandleBack() && (paused || !hud.HandleBack())) Pause();
            if (!inMatch) return; // the back press may have left the match

            float dt = Time.deltaTime;
            // the bodies and effects slow with the hit-stop's ticks below, so a swing never lands before its shot (offline only)
            BAM.Art.ArtKit.SetSlow(net == null && !paused && !over && juice.Stopping ? 0.1f : 1f);
            if (net != null && !over) NetTicks(dt);
            else if (!paused && !over)
            {
                acc += juice.Stopping ? dt * 0.1f : dt; // the old hit-stop: ticks come slower for a moment
                int n = 0;
                while (acc >= Rules.Tick && n < MaxTicksPerFrame)
                {
                    PlayerInput input = hud.TakeInput();
                    match.Step(in input);
                    BAM.Art.ArtKit.SetSimClock(match.Ticks, 0f); // a swing started by this tick's events counts from this tick
                    Screens.CountEvents(match); // the result card's ratings (damage, healing, shields per hero)
                    views.AfterStep();
                    juice.AfterStep();
                    hud.AfterStep();
                    acc -= Rules.Tick;
                    n++;
                    if (IslandDone()) { SecondHalf(); return; }
                    if (IsOver())
                    {
                        over = true; overAt = Time.unscaledTime; acc = 0f;
                        if (TutorialEnd) TutorialFinished(); // done as the tower falls: leaving its end screen by Pause or a kill still counts
                        break;
                    }
                }
                if (acc >= Rules.Tick) acc = 0f;
            }
            float alpha = paused || over ? 1f : Mathf.Clamp01((net != null ? netDraw : acc) / Rules.Tick);
            BAM.Art.ArtKit.SetSimClock(match.Ticks, alpha); // the swings' clock (Animation Foundation): blows land on their shots' frame
            views.Frame(alpha, dt, paused);
            cam.Frame(alpha, dt, over);
            juice.Frame(paused ? 0f : dt);
            if (intro.Active) intro.Frame(cam.Camera, paused);
            else juice.Apply(cam.Camera);
            BamAudio.SetListener(cam.Focus);
            hud.Frame(dt, paused || resultShown);

            // not while paused (the tower fell, then Pause or the notification shade within ResultDelay): the result would clear the
            // pause screen and leave the match paused under it with no way to resume; it comes up once the player resumes
            if (over && !resultShown && !paused && !TutorialEnd && Time.unscaledTime - overAt >= ResultDelay) ShowResult();
            else if (resultShown && !resultMusic && Time.unscaledTime - overAt >= ResultDelay + ResultMusicDelay)
            {
                resultMusic = true;
                BamAudio.PlayMusic(MusicId.Menu);
            }
        }

        // Online: this phone's controls go to the link 60 times a second (also while the pause screen is up, then as no input:
        // the others play on), and the rules step with every tick the link knows. Hit-stop stays off, so no phone slows the host.
        void NetTicks(float dt)
        {
            acc += dt; netDraw += dt;
            int n = 0, extra = 0;
            while (acc >= Rules.Tick && n < MaxTicksPerFrame)
            {
                acc -= Rules.Tick; n++;
                PlayerInput input = paused ? PlayerInput.None : hud.TakeInput();
                net.Submit(in input);
                if (net.Next(match, netInputs) && NetStep()) break;
                if (over) break;
            }
            // behind the host: one extra tick a frame (barely seen), several only when far behind (after a long hiccup)
            int allow = net.Backlog > NetFarBehind ? NetCatchUp : net.Backlog > 1 ? 1 : 0;
            while (!over && extra < allow && net.Next(match, netInputs)) { extra++; if (NetStep()) break; }
            if (acc >= Rules.Tick) acc = 0f;
            netDraw = Mathf.Min(netDraw, Rules.Tick);
            net.Flush();
            string st = net.Status;
            if (st != netStatus)
            {
                netStatus = st;
                if (st != null) Screens.ShowHint(st); else Screens.HideHint();
            }
        }

        // one online tick; true when the match ended with it
        bool NetStep()
        {
            match.Step(netInputs);
            BAM.Art.ArtKit.SetSimClock(match.Ticks, 0f); // a swing started by this tick's events counts from this tick
            netDraw = Mathf.Max(0f, netDraw - Rules.Tick);
            net.AfterStep(match);
            Screens.CountEvents(match);
            views.AfterStep();
            juice.AfterStep();
            hud.AfterStep();
            if (IsOver()) { over = true; overAt = Time.unscaledTime; acc = 0f; return true; }
            return false;
        }

        bool IslandDone() => match.Tut != null && match.Tut.Island && match.Tut.Done;

        // the island's pad threw the hero and the screen is black: the arena half starts behind it and fades in
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
            bool won = match.State == MatchState.Over ? match.Winner == p.Team : match.Tut != null && match.Tut.Done;
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
            if (!r.Tutorial) BamSettings.TutorialCard = false; // the first finished match takes the "Tutorial done" card away
            SendEnd(false);
            hud.CancelTouches();
            BamAudio.StopMusic(); // the victory or defeat jingle first; Update brings the music back after it
            Screens.ShowResult(r, match); // with the match: both teams' K/D/A, ratings and MVP (the old HudV3 result card)
        }

        // Exactly one MatchEnded per match: at the result, or as a quit when the player leaves or restarts before it.
        void SendEnd(bool quit)
        {
            if (match == null || endSent) return;
            endSent = true;
            if (net != null) net.MatchEnded(quit && !over);
            if (setup.HeroTest) return;
            var r = Result(quit && !over);
            BamAnalytics.MatchEnded(setup, r);
            Flow.MatchEnded?.Invoke(r); // online: level, XP and rating are counted on the server
        }

        void OnApplicationPause(bool pausedByOs)
        {
            if (pausedByOs && net == null) Pause(); // online the match goes on (the link notices a phone that stays away)
            if (!pausedByOs) Flow.Resumed?.Invoke();
        }

        // Android's notification shade or a system dialog only takes focus (no OnApplicationPause): pause there too
        void OnApplicationFocus(bool focus)
        {
            if (!focus) Pause();
        }
    }
}
