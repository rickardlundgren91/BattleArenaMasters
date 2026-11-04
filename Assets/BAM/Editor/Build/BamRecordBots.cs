using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using BAM.Core;
using BAM.Sim;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace BAM.Build
{
    /// <summary>
    /// BAM > Record bot match (Rickard 2026-10-30: "spela in 30 sekunder bottar spel mot varandra"): enters Play, starts a Hard
    /// match with the hero last picked in the menu, hands the player's hero to a bot so bots play both teams, and writes the
    /// Game view to an mp4 (no sound) in Downloads\BAM-Recordings, with a .txt of the kills' match times so a 30 s cut can be
    /// picked. Frames are taken at a fixed 30 fps game time (Time.captureFramerate), so a slow PC gives a smooth film.
    /// Leaves Play when done. Editor only; nothing in the game changes.
    /// </summary>
    [InitializeOnLoad]
    static class BamRecordBots
    {
        const string PendingKey = "BAM.RecordBots.Pending", SecondsKey = "BAM.RecordBots.Seconds";
        const int Fps = 30;

        static BamRecordBots()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("BAM/Record bot match (30 s)", priority = 60)]
        static void Record30() => Begin(30);

        [MenuItem("BAM/Record bot match (90 s, cut later)", priority = 61)]
        static void Record90() => Begin(90);

        [MenuItem("BAM/Record bot match (30 s)", true)]
        [MenuItem("BAM/Record bot match (90 s, cut later)", true)]
        static bool CanRecord() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static void Begin(int seconds)
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(SecondsKey, seconds);
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            var go = new GameObject("BAM Recorder");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<BotMatchRecorder>().Seconds = SessionState.GetInt(SecondsKey, 30);
        }
    }

    sealed class BotMatchRecorder : MonoBehaviour
    {
        public int Seconds = 30;
        const int Fps = 30;

        IEnumerator Start()
        {
            // the menus boot first (Flow.Game), and a first start's guided match is replaced by ours
            while (Flow.Game == null) yield return null;
            yield return new WaitForSecondsRealtime(1f);

            var setup = new MatchSetup { Hero = (HeroId)Mathf.Clamp(BamSettings.LastHero, 0, HeroCatalog.Count - 1), Difficulty = Difficulty.Hard };
            Flow.Game.StartMatch(setup);
            var field = Flow.Game.GetType().GetField("match", BindingFlags.Instance | BindingFlags.NonPublic);
            var match = field?.GetValue(Flow.Game) as Match;
            if (match == null) { Debug.LogError("[BAM.Record] could not reach the match"); EditorApplication.ExitPlaymode(); yield break; }
            match.SeatToBot(0);
            match.Config.PlayerIsBot = true; // the bots play as in the headless tests, not around a human (BotTeam.HumanInMatch)

            Time.captureFramerate = Fps;
            yield return new WaitForEndOfFrame();
            int w = Screen.width & ~1, h = Screen.height & ~1;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "BAM-Recordings");
            Directory.CreateDirectory(dir);
            string name = "bots-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string mp4 = Path.Combine(dir, name + ".mp4");
            var log = new StringBuilder();
            log.AppendLine("hero " + match.Player.Hero + ", Hard, " + w + "x" + h + " @ " + Fps + " fps, " + Seconds + " s");
            foreach (var u in match.Humans) if (u != null) log.AppendLine("team " + u.Team + ": " + u.Hero);

            var attrs = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = (uint)w, height = (uint)h, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
            var frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            int lastKills = 0;
            float recStart = Time.time;
            using (var enc = new MediaEncoder(mp4, attrs))
            {
                for (int f = 0; f < Seconds * Fps; f++)
                {
                    yield return new WaitForEndOfFrame();
                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                    var src = shot.GetPixels32();
                    int sw = shot.width;
                    for (int y = 0; y < h && y < shot.height; y++) Array.Copy(src, y * sw, px, y * w, Mathf.Min(w, sw));
                    Destroy(shot);
                    frame.SetPixels32(px);
                    frame.Apply(false);
                    enc.AddFrame(frame);

                    int kills = 0;
                    foreach (var u in match.Humans) if (u != null) kills += u.Kills;
                    if (kills > lastKills) { log.AppendLine("kill at video " + (f / (float)Fps).ToString("0.0") + " s (match " + match.Time.ToString("0.0") + " s)"); lastKills = kills; }
                    if (!Flow.Game.InMatch) break;
                }
            }
            Destroy(frame);
            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(dir, name + ".txt"), log.ToString());
            Debug.Log("[BAM.Record] saved " + mp4);
            EditorUtility.RevealInFinder(mp4);
            EditorApplication.ExitPlaymode();
        }
    }
}
