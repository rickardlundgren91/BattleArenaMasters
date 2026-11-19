using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using BAM.Core;
using UnityEngine;
using UnityEngine.Scripting;

// The answer classes are filled by the Cloud Code / JSON deserializer, not by our code.
#pragma warning disable 0649

namespace BAM.Online
{
    /// <summary>
    /// Calls to the game's Cloud Code scripts (Cloud/Scripts/*.js). The server decides everything that others can see or that
    /// counts: the unique name, level, Rating, stats and the Top 100. The phone only asks and shows the answer.
    /// </summary>
    public static class Server
    {
        const int SendBatch = 20;
        static bool sending;

        /// <summary>The player's name and totals as the server has them.</summary>
        internal static async Task Refresh()
        {
            var me = await Call<MeAnswer>("GetMe", null, "totals");
            Account.Name = string.IsNullOrEmpty(me.name) ? null : me.name;
            Progress.ServerAnswered(me.totals, null);
        }

        /// <summary>Sends finished matches the server has not counted yet. Safe to call often; one send runs at a time.</summary>
        public static async Task SendPending()
        {
            if (sending || Account.State != Account.Status.Ready) return;
            var list = Progress.Pending;
            if (list.Count == 0 && Progress.Waiting == 0) return;
            sending = true;
            if (list.Count > 0) askTries = 0;
            try
            {
                do
                {
                    // an empty list only asks about matches against players that wait for the other side's report
                    int n = Math.Min(SendBatch, list.Count), before = list.Count;
                    var batch = list.GetRange(0, n);
                    var ans = await Call<ReportAnswer>("ReportMatch", new Dictionary<string, object> { { "matches", batch } }, "done");
                    var done = new HashSet<string>(ans.done ?? new List<string>());
                    Progress.ServerAnswered(ans.totals, done, ans.waiting);
                    if (done.Count == 0 || list.Count >= before) break;   // nothing taken (or only ids we no longer have): try again later instead of looping
                }
                while (list.Count > 0 && Account.State == Account.Status.Ready);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] match report not sent, kept for later: " + e.Message);
            }
            finally
            {
                sending = false;
            }
            if (Progress.Waiting > 0) _ = AskAgain(); else askTries = 0;
            _ = SendTraces();
        }

        // ---------- match traces (Core/MatchTraces.cs, Sim/MatchTrace.cs): for the bots to learn from real players ----------
        const int TracesPerSend = 3;
        static bool tracing, traceScriptMissing;

        /// <summary>Sends up to three waiting match traces, one per call (ReportTrace.js), after the match reports. A trace the
        /// server took is deleted; on any error the rest wait for the next time. Without the script on the server it stops for this
        /// session.</summary>
        static async Task SendTraces()
        {
            if (tracing || traceScriptMissing || !MatchTraces.Share || Account.State != Account.Status.Ready) return;
            tracing = true;
            try
            {
                var names = MatchTraces.Pending();
                for (int i = 0; i < names.Count && i < TracesPerSend; i++)
                {
                    var data = MatchTraces.Read(names[i]);
                    if (data == null || data.Length > MatchTraces.MaxBytes) { MatchTraces.Remove(names[i]); continue; }
                    var a = await Call<OkAnswer>("ReportTrace", new Dictionary<string, object>
                        { { "name", names[i] }, { "protocol", MatchTraces.Protocol }, { "app", Application.version }, { "trace", Convert.ToBase64String(data) } }, "ok");
                    if (!a.ok) break;
                    MatchTraces.Remove(names[i]);
                }
            }
            catch (Exception e)
            {
                if (e.Message.IndexOf("not BAM's", StringComparison.Ordinal) >= 0 || e.Message.IndexOf("404", StringComparison.Ordinal) >= 0 || e.Message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) traceScriptMissing = true;
                Debug.LogWarning("[BAM.Online] match traces kept for later: " + e.Message);
            }
            finally { tracing = false; }
        }

        // the other side of a match against players usually reports within seconds of this phone: ask again after 20 s, 1 and
        // 3 minutes, then only with the next match or sign-in (the server counts it as a bot match after 30 minutes)
        static bool asking;
        static int askTries;
        static readonly int[] AskAfter = { 20000, 60000, 180000 };
        static async Task AskAgain()
        {
            if (asking || askTries >= AskAfter.Length) return;
            asking = true;
            try { await Task.Delay(AskAfter[askTries++]); }
            finally { asking = false; }
            if (Progress.Waiting > 0) await SendPending();
        }

        /// <summary>Asks the server to give this player the name. Answers null when it is taken or not allowed (reason says why).</summary>
        public static async Task<NameAnswer> SetName(string wanted)
        {
            string local = NameRules.Check(wanted);
            if (local != null) return new NameAnswer { ok = false, reason = local };
            try
            {
                var a = await Call<NameAnswer>("SetName", new Dictionary<string, object> { { "name", NameRules.Clean(wanted) } }, "ok");
                if (a.ok)
                {
                    Account.NameChosen(a.name);
                    Social.NameChanged(a.name);
                    Social.Start();   // a new player signs in as NeedsName, so friends could not start then
                    _ = SendPending();
                }
                return a;
            }
            catch (Exception e)
            {
                return new NameAnswer { ok = false, reason = Account.Short(e) };
            }
        }

        /// <summary>Another player's public profile, or null.</summary>
        public static async Task<PublicProfile> GetProfile(string playerId)
        {
            try { return await Call<PublicProfile>("GetProfile", new Dictionary<string, object> { { "playerId", playerId } }, "playerId"); }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] profile: " + e.Message); return null; }
        }

        /// <summary>The player ID behind a name (names are unique), or null when nobody has it.</summary>
        public static async Task<string> FindPlayer(string name)
        {
            try
            {
                var a = await Call<FindAnswer>("FindPlayer", new Dictionary<string, object> { { "name", NameRules.Clean(name) } }, "playerId");
                return string.IsNullOrEmpty(a.playerId) ? null : a.playerId;
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] find: " + e.Message); return null; }
        }

        /// <summary>Report a player. The report is kept on the server for review in the Unity Dashboard.</summary>
        public static async Task<bool> ReportPlayer(string playerId, string reason)
        {
            try
            {
                var a = await Call<OkAnswer>("ReportPlayer", new Dictionary<string, object> { { "target", playerId }, { "reason", reason ?? "" } }, "ok");
                return a.ok;
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] report: " + e.Message); return false; }
        }

        internal static Task DeleteMe() => Call<OkAnswer>("DeleteMe", null, "ok");

        /// <summary>Buys the hero's gold skin (skin 1, Gold.SkinPrice) or keeps its premium skin for good (skin 2, Gold.KeepPrice, one
        /// keep-pick from a season finished in the top 10) on the server (BuySkin.js), which takes the gold it keeps.</summary>
        internal static Task<BuyAnswer> BuySkin(string hero, int skin) => Call<BuyAnswer>("BuySkin", new Dictionary<string, object> { { "hero", hero }, { "skin", skin } }, "ok");

#if UNITY_EDITOR
        // BAM > Online > End season: ends the Rating season (EndSeason.js, premium skins 2026-11-19). Needs Play mode (signed in) and
        // this editor's player ID in "bam-admins" (as the traces). First it shows the season and its top 10 now; OK saves them (each
        // gets a keep-pick) and starts the next season. Then reset the leaderboard "rating" on the Unity Dashboard.
        [Serializable, Preserve] class SeasonRow { public string playerId, name; public int score; }
        [Serializable, Preserve] class SeasonAnswer { public bool ok; public string you; public int season; public List<SeasonRow> top; }

        [UnityEditor.MenuItem("BAM/Online/End season (in Play)")]
        static async void EndSeason()
        {
            if (!Application.isPlaying || Account.State != Account.Status.Ready) { Debug.LogWarning("[BAM.Online] end season: enter Play and wait for the sign-in first"); return; }
            try
            {
                var a = await Call<SeasonAnswer>("EndSeason", null, "season");
                if (a.season == 0) { Debug.LogWarning("[BAM.Online] end season: this editor may not. Add \"" + a.you + "\" to Cloud Save > Custom data > bam-admins > ids on the Unity Dashboard"); return; }
                var who = new System.Text.StringBuilder();
                int n = 0;
                foreach (var r in a.top ?? new List<SeasonRow>()) who.Append(++n).Append(". ").Append(r.name).Append("  ").Append(r.score).Append('\n');
                if (n == 0) who.Append("(nobody has Rating yet)\n");
                if (!UnityEditor.EditorUtility.DisplayDialog("End season " + a.season + "?",
                    "Top 10 now (each gets one premium skin to keep for " + Gold.KeepPrice + " gold):\n\n" + who +
                    "\nSeason " + (a.season + 1) + " starts: everyone's Rating goes back to 0 at their next match.\nAfterwards reset the leaderboard \"rating\" on the Unity Dashboard.",
                    "End season " + a.season, "Cancel")) return;
                var b = await Call<SeasonAnswer>("EndSeason", new Dictionary<string, object> { { "confirm", a.season } }, "season");
                if (b.ok) Debug.Log("[BAM.Online] season " + b.season + " ended, " + n + " in its top 10. Season " + (b.season + 1) + " runs now: reset the leaderboard \"rating\" on the Unity Dashboard.");
                else Debug.LogWarning("[BAM.Online] end season: not ended (season " + b.season + " runs; someone else ended it meanwhile?)");
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] end season: " + e.Message); }
        }

        // BAM > Bots > Download match traces: the bot lab's copy of every trace on the server (ExportTraces.js), as .bmt files in
        // Downloads/BAM-Traces (a file already there is skipped). Needs Play mode (signed in) and this editor's player ID in the
        // Dashboard's Cloud Save custom item "bam-admins" key "ids"; refused, the Console says which ID to add.
        [Serializable, Preserve] class TraceRow { public string key; public int p; public string a; public long at; public string t; }
        [Serializable, Preserve] class ExportAnswer { public bool ok; public string you; public List<TraceRow> traces; public string next; }

        [UnityEditor.MenuItem("BAM/Bots/Download match traces (in Play)")]
        static async void DownloadTraces()
        {
            if (!Application.isPlaying || Account.State != Account.Status.Ready) { Debug.LogWarning("[BAM.Online] traces: enter Play and wait for the sign-in first"); return; }
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "BAM-Traces");
            System.IO.Directory.CreateDirectory(dir);
            int got = 0, had = 0; string after = "";
            try
            {
                for (int page = 0; page < 1000; page++)
                {
                    var a = await Call<ExportAnswer>("ExportTraces", new Dictionary<string, object> { { "after", after } }, "ok");
                    if (!a.ok) { Debug.LogWarning("[BAM.Online] traces: this editor may not read them. Add \"" + a.you + "\" to Cloud Save > Custom data > bam-admins > ids on the Unity Dashboard"); return; }
                    foreach (var r in a.traces ?? new List<TraceRow>())
                    {
                        string f = System.IO.Path.Combine(dir, r.key + "-p" + r.p + ".bmt");
                        if (System.IO.File.Exists(f)) { had++; continue; }
                        System.IO.File.WriteAllBytes(f, Convert.FromBase64String(r.t)); got++;
                    }
                    if (string.IsNullOrEmpty(a.next)) break;
                    after = a.next;
                }
                Debug.Log("[BAM.Online] traces: " + got + " new, " + had + " already there, in " + dir);
                UnityEditor.EditorUtility.RevealInFinder(dir);
            }
            catch (Exception e) { Debug.LogWarning("[BAM.Online] traces: " + e.Message); }
        }
#endif

        // The answer is read with JsonUtility, which skips fields it does not know. Cloud Code's own reader stops at any unknown field,
        // so a script on the Dashboard that is not BAM's (the "roll" dice example Unity fills a new script with) failed with
        // "Could not find member 'roll' on object of type 'MeAnswer'" (Rickard's phone, 2026-10-27). Such a script is now named
        // in the error instead: the answer must hold the field we need (need).
        static async Task<T> Call<T>(string script, Dictionary<string, object> args, string need)
        {
            string json = await CloudCodeService.Instance.CallEndpointAsync(script, args ?? new Dictionary<string, object>());
            json = json ?? "";
            if (need != null && json.IndexOf("\"" + need + "\"", StringComparison.Ordinal) < 0)
            {
                Debug.LogWarning("[BAM.Online] Cloud Code " + script + " answered " + json);
                throw new Exception("the server script " + script + " is not BAM's. Paste Assets/BAM/Online/Cloud/Scripts/" + script + ".js on the Unity Dashboard");
            }
            return JsonUtility.FromJson<T>(json);
        }

        [Serializable, Preserve] class MeAnswer { public string name; public Totals totals; }
        [Serializable, Preserve] class ReportAnswer { public Totals totals; public List<string> done; public int waiting = -1; }
        [Serializable, Preserve] class FindAnswer { public string playerId; }
        [Serializable, Preserve] class OkAnswer { public bool ok; }
    }

    [Serializable, Preserve]
    public class NameAnswer
    {
        public bool ok;
        public string name, reason;
    }

    /// <summary>What anyone can see about a player (GetProfile.js). Built by the server from its own totals.</summary>
    [Serializable, Preserve]
    public class PublicProfile
    {
        public string playerId, name;
        public Totals totals;
        public int rank;    // Top 100 place, 0 when not on the board
    }

    /// <summary>Name rules checked on the phone for a quick answer; SetName.js checks the same again (and that it is free).</summary>
    public static class NameRules
    {
        public const int Min = 3, Max = 16;

        public static string Clean(string s) => (s ?? "").Trim();

        /// <summary>Null when the name may be sent, else why not (in English, shown in the game).</summary>
        public static string Check(string s)
        {
            s = Clean(s);
            if (s.Length < Min) return "At least " + Min + " letters";
            if (s.Length > Max) return "At most " + Max + " letters";
            bool letter = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return "Letters, digits, _ - and . only";
                if (char.IsLetter(c)) letter = true;
            }
            return letter ? null : "Use at least one letter";
        }
    }
}
