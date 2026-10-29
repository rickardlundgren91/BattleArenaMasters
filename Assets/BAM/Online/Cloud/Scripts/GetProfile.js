// Cloud Code "GetProfile": another player's public profile, built by the server from its own protected data, so what others
// see is what the server counted (Speltest showed what the other phone said about itself).
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
const SEASON = 2;   // keep equal to ReportMatch.js

module.exports = async ({ params, context }) => {
  const { projectId } = context;
  const id = String(params.playerId || "");
  if (!/^[A-Za-z0-9_-]{8,64}$/.test(id)) return { playerId: id, name: null, totals: null, rank: 0 };
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, id, ["bam-name", "bam-progress"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const t = item("bam-progress") || null;
  if (t) { delete t.winDay; delete t.pend; if (t.rs !== SEASON) t.rating = 0; }   // an old season's Rating shows as 0
  let rank = 0;
  try {
    const lb = new LeaderboardsApi(context);
    const s = await lb.getLeaderboardPlayerScore(projectId, "rating", id);
    rank = s.data.rank + 1;
  } catch (e) { }   // not on the board yet
  return { playerId: id, name: (item("bam-name") || {}).name || null, totals: t, rank: rank };
};

module.exports.params = { playerId: { type: "STRING", required: true } };
