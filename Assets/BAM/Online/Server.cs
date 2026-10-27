using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
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
            if (list.Count == 0) return;
            sending = true;
            try
            {
                while (list.Count > 0 && Account.State == Account.Status.Ready)
                {
                    int n = Math.Min(SendBatch, list.Count), before = list.Count;
                    var batch = list.GetRange(0, n);
                    var ans = await Call<ReportAnswer>("ReportMatch", new Dictionary<string, object> { { "matches", batch } }, "done");
                    var done = new HashSet<string>(ans.done ?? new List<string>());
                    Progress.ServerAnswered(ans.totals, done);
                    if (done.Count == 0 || list.Count >= before) break;   // nothing taken (or only ids we no longer have): try again later instead of looping
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Online] match report not sent, kept for later: " + e.Message);
            }
            finally
            {
                sending = false;
            }
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
        [Serializable, Preserve] class ReportAnswer { public Totals totals; public List<string> done; }
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
