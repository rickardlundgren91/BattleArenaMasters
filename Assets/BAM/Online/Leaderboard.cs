using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using UnityEngine;

// The answer classes are filled by the Cloud Code / JSON deserializer, not by our code.
#pragma warning disable 0649

namespace BAM.Online
{
    /// <summary>
    /// TOP 100 and MY RANK (Speltest MenuSocial "rank" page). The leaderboard "rating" is written only by Cloud Code
    /// (ReportMatch.js, SetName.js); the phone only reads it. Player writes are also denied in Unity's access control
    /// (Cloud/project-access-policy.json), so an edited app cannot put its own score on the board (Speltest could).
    /// </summary>
    public static class Leaderboard
    {
        public const string Id = "rating";
        public const int Top = 100;

        public struct Row
        {
            public int Rank;        // 1-based
            public string PlayerId, Name;
            public int Rating, Level;
            public bool Me;
        }

        static readonly List<Row> rows = new List<Row>();
        static Row me;
        static bool hasMe;
        static float loadedAt = -999f;

        public static IReadOnlyList<Row> Rows => rows;
        /// <summary>The player's own row, also when it is below the Top 100.</summary>
        public static bool TryGetMe(out Row row) { row = me; return hasMe; }
        public static string Error { get; private set; } = "";
        public static bool Loading { get; private set; }
        public static event Action Changed;

        /// <summary>Loads the board; at most every 30 s unless forced (pull to refresh).</summary>
        public static async Task Load(bool force = false)
        {
            if (Loading || Account.State != Account.Status.Ready) return;
            if (!force && Time.realtimeSinceStartup - loadedAt < 30f) return;
            Loading = true;
            Changed?.Invoke();
            try
            {
                var page = await LeaderboardsService.Instance.GetScoresAsync(Id, new GetScoresOptions { Limit = Top, IncludeMetadata = true });
                rows.Clear();
                string myId = Account.PlayerId;
                foreach (var e in page.Results) rows.Add(ToRow(e.Rank, e.PlayerId, e.PlayerName, e.Score, e.Metadata, myId));
                try
                {
                    var mine = await LeaderboardsService.Instance.GetPlayerScoreAsync(Id, new GetPlayerScoreOptions { IncludeMetadata = true });
                    me = ToRow(mine.Rank, mine.PlayerId, mine.PlayerName, mine.Score, mine.Metadata, myId);
                    hasMe = true;
                }
                catch (Exception) { hasMe = false; }   // not on the board yet (no counted match)
                Error = "";
                loadedAt = Time.realtimeSinceStartup;
            }
            catch (Exception e)
            {
                Error = Account.Short(e);
                Debug.LogWarning("[BAM.Online] leaderboard: " + e.Message);
            }
            finally
            {
                Loading = false;
                Changed?.Invoke();
            }
        }

        static Row ToRow(int rank0, string id, string unityName, double score, string metadata, string myId)
        {
            var m = string.IsNullOrEmpty(metadata) ? null : JsonUtility.FromJson<Meta>(metadata);
            return new Row
            {
                Rank = rank0 + 1,
                PlayerId = id,
                Name = m != null && !string.IsNullOrEmpty(m.n) ? m.n : StripTag(unityName),
                Rating = (int)score,
                Level = m != null ? m.l : 0,
                Me = id == myId,
            };
        }

        /// <summary>Unity player names end in "#1234"; the game's own names are unique, so the tag is never shown.</summary>
        public static string StripTag(string n)
        {
            if (string.IsNullOrEmpty(n)) return "Player";
            int i = n.LastIndexOf('#');
            return i > 0 ? n.Substring(0, i) : n;
        }

        [Serializable] class Meta { public string n; public int l; }
    }
}
