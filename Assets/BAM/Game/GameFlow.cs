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
        bool inMatch, paused, over, resultShown, resultMusic, endSent, endLogged, booked, botsMoved;
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
        // guest prediction (2026-10-31, Rickard: "som gäst var det långsamt ... som värd är det jättebra respons"): the confirmed match
        // steps with the host's frames as before; what is shown (match) is a copy of it run ahead with this phone's own controls at
        // once (SimMirror), copied anew whenever frames come. Events show once, the first time a tick is run.
        Match confirmed;
        BAM.Sim.SimMirror mirror;
        long pFrame, shownFrame;
        // at most this far ahead of the host (its frames stopped coming): the shown match waits instead of guessing on
        const int MaxAhead = 45;
        readonly System.Diagnostics.Stopwatch predWatch = new System.Diagnostics.Stopwatch();
        float predMaxMs; int predFrom; // (the first second left out: loading)
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

        /// <summary>Only a match alone against bots may pause (Rickard 2026-10-31): with another real player (friends party or
        /// online) the match never pauses and never shows the pause screen, whatever asks (back button, focus loss, background).</summary>
        public bool CanPause => net == null;

        /// <summary>Editor only (menu BAM > Test > Immortal in matches, Rickard 2026-11-03 "kunna vara odödlig i match för att kunna
        /// testa"): the player is topped up to full health before and after every tick of a match against bots, so hits, rings and
        /// damage numbers still show but he never dies. Never online, never in a phone build; the rules (Sim) are untouched.</summary>
        void EditorImmortal()
        {
#if UNITY_EDITOR
            var p = match.Player;
            if (net == null && p != null && p.Alive && p.Hp < p.MaxHp && UnityEditor.EditorPrefs.GetBool(ImmortalKey, false)) p.Hp = p.MaxHp;
#endif
        }

        /// <summary>The EditorPrefs key the BAM > Test > Immortal in matches menu sets (Editor/Build/BamImmortal.cs).</summary>
        public const string ImmortalKey = "BAM.ImmortalInMatch";

        public void Pause()
        {
            if (!inMatch || paused || resultShown || !CanPause) return;
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
                Map = setup.Tutorial || setup.HeroTest ? MapId.Runes : setup.Map,
            };
            if (net != null) { seed = cfg.Seed; netInputs = new PlayerInput[Match.MaxSeats]; netStatus = null; }
            Coords.Turned = net != null && net.Turned;
            match = Match.Create(cfg);
            confirmed = null; mirror = null;
            // the match trace for the bots to learn from (Sim/MatchTrace.cs, Core/MatchTraces.cs): every real match, not the guided
            // one or HERO TEST; the editor's are marked (its Immortal switch changes the match)
            trace = null; traceSaved = false;
            if (!cfg.Tutorial && !cfg.HeroTest && MatchTraces.Share)
                trace = MatchTrace.Begin(cfg, net != null && cfg.Seats != null ? cfg.Seats.Length : 1, MatchTraces.Protocol, Application.version,
                    System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), net != null, Application.isEditor);
            if (net != null && net.Predicts)
            {
                confirmed = Match.Create(cfg);
                mirror = new BAM.Sim.SimMirror(confirmed, match);
                predMaxMs = 0; predFrom = Time.frameCount + 60; mirror.Sync(); // before the views: they keep the shown match's objects
                pFrame = shownFrame = 0;
            }
            Screens.HideAll();
            views = new MatchViews(match);
            if (cam == null) cam = new MatchCamera(Camera);
            cam.Begin(match);
            juice.Begin(match);
            intro.Begin(match, views);
            if (hud == null) hud = new Hud(this);
            hud.Intro = intro;
            hud.Begin(match, views, cam);
            inMatch = true; paused = false; over = false; resultShown = false; resultMusic = false; endSent = false; endLogged = false;
            booked = false; botsMoved = false;
            acc = 0f; netDraw = 0f;
            // Rickard's loop plays through the match (also across the guided match's two halves); the result screen and
            // leaving the match fade it out
            BamAudio.PlayMusic(MusicId.Match);
            if ((!setup.Tutorial || island) && !setup.HeroTest && !Practice)
            {
                BamAnalytics.MatchStarted(setup); // HERO TEST is practice, not a match (old: no stats)
                Flow.MatchStarted?.Invoke();      // online: "in a match" for friends
            }
            Debug.Log("[BAM.Game] match started: " + (net != null ? "online, seat " + cfg.LocalSeat + " of " + cfg.Seats.Length + ", " : "") + cfg.PlayerHero + ", " + cfg.Difficulty + (cfg.Tutorial ? (island ? ", guided match (island)" : ", guided match (arena)") : "") + (cfg.HeroTest ? ", HERO TEST level " + cfg.HeroTestLevel : "") + (cfg.Map != MapId.Runes ? ", map " + cfg.Map : "") + ", seed " + seed);
        }

        MatchTrace trace;
        bool traceSaved;

        // once per match, at its end or when it is left: a left match is kept too (where people quit is worth learning from)
        void SaveTrace()
        {
            if (trace == null || traceSaved) return;
            traceSaved = true;
            try
            {
                var m = confirmed ?? match;
                if (m != null && m.State == MatchState.Over) trace.End(m);
                if (trace.Steps > 0) MatchTraces.Save(trace.ToBytes());
            }
            catch (System.Exception e) { Debug.LogWarning("[BAM.Game] match trace: " + e.Message); }
            trace = null;
        }

        void Teardown()
        {
            if (match == null) return;
            SaveTrace();
            inMatch = false; paused = false;
            BAM.Art.ArtKit.SetSimClock(-1, 0f);
            hud.End();
            intro.End(cam.Camera);
            views.Dispose();
            views = null;
            BAM.Art.ArtKit.ClearEffects(); // every effect of the match goes (Bonk's too), and the art pause is lifted
            cam.End();
            match = null; confirmed = null; mirror = null;
            BamAudio.StopAll();
            Screens.HideHint();
        }

        void Update()
        {
            if (!inMatch) return;
            var kb = Keyboard.current;
            // Android's back button arrives as Escape: it closes our menus first, else it pauses (with other players: it opens the
            // score sheet instead, whose EXIT GAME leaves; the match plays on)
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !Screens.HandleBack() && (paused || !hud.HandleBack()))
            {
                if (CanPause) Pause(); else hud.OpenMenu();
            }
            if (!inMatch) return; // the back press may have left the match

            float dt = Time.deltaTime;
            // offline the open score sheet (the in-match menu) holds the match like Pause, without the pause screen (Rickard
            // 2026-10-30: against bots only, the menu pauses); online the others play on, so it never holds there
            bool frozen = paused || (net == null && !over && hud.MenuOpen);
            BAM.Art.ArtKit.SetPaused(frozen); // effects, zones and shots wait with the match (no-op when unchanged)
            // the bodies and effects slow with the hit-stop's ticks below, so a swing never lands before its shot (offline only)
            BAM.Art.ArtKit.SetSlow(net == null && !frozen && !over && juice.Stopping ? 0.1f : 1f);
            if (net != null && !over) { if (mirror != null) NetTicksPredicted(dt); else NetTicks(dt); }
            else if (!frozen && !over)
            {
                acc += juice.Stopping ? dt * 0.1f : dt; // the old hit-stop: ticks come slower for a moment
                int n = 0;
                while (acc >= Rules.Tick && n < MaxTicksPerFrame)
                {
                    // the online wire's rounding offline too (1/127 of the stick), so the match trace replays exactly
                    PlayerInput input = MatchTrace.Quantize(hud.TakeInput());
                    trace?.Step(match, in input);
                    EditorImmortal();
                    match.Step(in input);
                    EditorImmortal();
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
                        LogEnd(false);
                        Bookkeep();
                        if (TutorialEnd) TutorialFinished(); // done as the tower falls: leaving its end screen by Pause or a kill still counts
                        break;
                    }
                }
                if (acc >= Rules.Tick) acc = 0f;
            }
            float alpha = frozen || over ? 1f : Mathf.Clamp01((net != null && mirror == null ? netDraw : acc) / Rules.Tick);
            BAM.Art.ArtKit.SetSimClock(match.Ticks, alpha); // the swings' clock (Animation Foundation): blows land on their shots' frame
            views.Frame(alpha, dt, frozen);
            cam.Frame(alpha, dt, over);
            juice.Frame(frozen ? 0f : dt);
            if (intro.Active) intro.Frame(cam.Camera, frozen);
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

        // Online: this phone's controls go to the link 60 times a second (never paused online: the others play on), and the rules step with every tick the link knows. Hit-stop stays off, so no phone slows the host.
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

        // A guest with prediction: this phone's controls go out tagged with the tick they are for, and the shown match runs to that
        // tick at once with them. The confirmed match steps with every frame the host sent; when any came, the shown match is copied
        // from it again and run ahead anew (the guesses about the others corrected).
        void NetTicksPredicted(float dt)
        {
            acc += dt;
            int n = 0;
            while (acc >= Rules.Tick && n < MaxTicksPerFrame) { acc -= Rules.Tick; n++; }
            if (acc >= Rules.Tick) acc = 0f;
            bool moved = false;
            while (net.Next(confirmed, netInputs))
            {
                trace?.Step(confirmed, netInputs);
                confirmed.Step(netInputs);
                net.AfterStep(confirmed);
                moved = true;
                if (confirmed.State == MatchState.Over) break;
            }
            bool end = confirmed.State == MatchState.Over;

            // (after the frames: "never behind what is confirmed" counts the ones that just came)
            n = Mathf.Max(0, n + net.Adjust()); // keep this phone's controls arriving just before their frame at the host
            if (net.Display + n < net.Confirmed) n = (int)(net.Confirmed - net.Display);
            long room = MaxAhead - (net.Display - net.Confirmed);
            if (n > room) n = (int)Mathf.Max(0, room);
            for (int i = 0; i < n; i++) { PlayerInput input = paused ? PlayerInput.None : hud.TakeInput(); net.Submit(in input); }

            predWatch.Restart();
            // confirmed past what was ever shown (this phone hitched, or the frames stalled and came in a heap): the shown match runs
            // those ticks itself with the confirmed controls, each shown (swings, hits, numbers, sounds, the kill feed), and only then
            // is copied. Copying straight to the confirmed frame skipped them unseen (bug review NET-1, 2026-11-03: every attack visible)
            if (moved && pFrame < net.Confirmed)
            {
                while (pFrame < net.Confirmed && net.ConfirmedInputs(pFrame, netInputs))
                {
                    match.Step(netInputs);
                    pFrame++;
                    if (pFrame > shownFrame) { shownFrame = pFrame; ShownStep(); }
                }
            }
            // copied again only when a guess was wrong: most frames the host's frames are just what the shown match already ran with
            // (this phone's controls in time, the others holding their stick), and then the shown match is kept as it is
            bool sync = moved && (net.TakeMiss() || end || pFrame < net.Confirmed);
            long was = pFrame; // the frame shown so far: where the corrections are measured
            if (sync) { views.BeginCorrect(); mirror.Sync(); pFrame = net.Confirmed; }
            long to = end ? pFrame : net.Display;
            if (sync && pFrame == was) views.EndCorrect(); // (past what was shown: nothing to compare)
            while (pFrame < to)
            {
                net.Predict(pFrame, netInputs);
                match.Step(netInputs);
                net.PredictedStep(pFrame, match);
                pFrame++;
                if (sync && pFrame == was) views.EndCorrect();
                if (pFrame > shownFrame) { shownFrame = pFrame; ShownStep(); }
            }
            if (sync) views.Rebind();
            float ms = (float)predWatch.Elapsed.TotalMilliseconds;
            if (ms > predMaxMs && Time.frameCount > predFrom) predMaxMs = ms;
            if (end) Debug.Log("[BAM.Online] prediction: longest frame of copying and rerunning " + Mathf.RoundToInt(predMaxMs) + " ms");
            BAM.Art.ArtKit.SetSimClock(match.Ticks, 0f);
            net.Flush();
            if (end) { over = true; overAt = Time.unscaledTime; acc = 0f; LogEnd(false); Bookkeep(); }
            string st = net.Status;
            if (st != netStatus)
            {
                netStatus = st;
                if (st != null) Screens.ShowHint(st); else Screens.HideHint();
            }
        }

        // a tick of the shown match run for the first time: its events go to the views, sounds and the result card's counts
        void ShownStep()
        {
            BAM.Art.ArtKit.SetSimClock(match.Ticks, 0f);
            Screens.CountEvents(match);
            views.AfterStep();
            juice.AfterStep();
            hud.AfterStep();
        }

        // one online tick; true when the match ended with it
        bool NetStep()
        {
            trace?.Step(match, netInputs);
            match.Step(netInputs);
            BAM.Art.ArtKit.SetSimClock(match.Ticks, 0f); // a swing started by this tick's events counts from this tick
            netDraw = Mathf.Max(0f, netDraw - Rules.Tick);
            net.AfterStep(match);
            Screens.CountEvents(match);
            views.AfterStep();
            juice.AfterStep();
            hud.AfterStep();
            if (IsOver()) { over = true; overAt = Time.unscaledTime; acc = 0f; LogEnd(false); Bookkeep(); return true; }
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
                TowerDeaths = p.TowerDeaths,
                ReadyDeaths = p.ReadyDeaths,
                Level = p.Level,
                Hero = p.Hero,
                Difficulty = setup.Difficulty,
                Tutorial = setup.Tutorial,
                Practice = Practice,
            };
        }

        void ShowResult()
        {
            resultShown = true;
            var r = Result(false);
            Bookkeep(); // done already as the match was decided; kept here in case a path missed it
            r.BotsMoved = botsMoved;
            SendEnd(false);
            hud.CancelTouches();
            BamAudio.StopMusic(); // the victory or defeat jingle first; Update brings the music back after it
            Screens.ShowResult(r, match); // with the match: both teams' K/D/A, ratings and MVP (the old HudV3 result card)
        }

        // The phone's own counts, once per match as it is decided (the tower falls), not with the result card ResultDelay later:
        // a player who paused and went Home in those 2 s lost the win for the Hard and Ultra locks and the bot level's move, while
        // the server still gave the XP (bug review 2026-11-02 nr 18, again 2026-11-04). A match left before it is decided counts
        // nothing, as before. PRACTICE > NEW MAP counts toward neither lock (bug review 2026-11-04 nr 52).
        void Bookkeep()
        {
            if (match == null || booked) return;
            booked = true;
            SaveTrace();
            var r = Result(false);
            if (!r.Tutorial) BamSettings.TutorialCard = false; // the first finished match takes the "Tutorial done" card away
            bool counts = !r.Tutorial && !setup.HeroTest && !Practice && setup.Net == null;
            if (counts) botsMoved = BamSettings.AfterBotMatch(r.Difficulty, r.Won); // Easy and Normal follow the player
            if (counts && r.Won && r.Difficulty == Difficulty.Hard) BamSettings.AddHardWin(); // 30 open Ultra
            if (counts && r.Won && r.Difficulty == Difficulty.Normal) BamSettings.AddNormalWin(); // 3 open Hard
        }

        // Exactly one MatchEnded per match: at the result, or as a quit when the player leaves or restarts before it.
        // PRACTICE > NEW MAP (the second map, Grove): practice like HERO TEST, so no analytics, no bot-level move, no online XP
        bool Practice => setup.Map != MapId.Runes && setup.Net == null;

        void SendEnd(bool quit)
        {
            if (match == null || endSent) return;
            endSent = true;
            if (net != null) net.MatchEnded(quit && !over);
            if (setup.HeroTest || Practice) return;
            var r = Result(quit && !over);
            LogEnd(quit);
            Flow.MatchEnded?.Invoke(r); // online: level, XP and rating are counted on the server
        }

        // The analytics' matchEnd, once per match: already as the match is decided (the tower falls), not with the result card
        // 2 s later; a match left before that sends it from SendEnd. So a phone closed on the result card, or one that hangs as
        // the match ends, still reports the match (bot analysis 2026-11-01: 5 of 41 matches on 0.3.1 sent no matchEnd, three of
        // them just when matches end). The values are the same as at the card: the match stops stepping once it is over.
        void LogEnd(bool quit)
        {
            if (match == null || endLogged || setup.HeroTest || Practice) return;
            endLogged = true;
            BamAnalytics.MatchEnded(setup, Result(quit && !over));
        }

        void OnApplicationPause(bool pausedByOs)
        {
            if (pausedByOs && net == null) Pause(); // online the match goes on (the link notices a phone that stays away)
            if (!pausedByOs) Flow.Resumed?.Invoke();
        }

        // Android's notification shade or a system dialog only takes focus (no OnApplicationPause): pause there too
        void OnApplicationFocus(bool focus)
        {
            if (!focus) Pause(); // no-op with other players (CanPause)
        }
    }
}
