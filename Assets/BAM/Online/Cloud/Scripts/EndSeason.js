// Cloud Code "EndSeason": ends the Rating season (Rickard 2026-11-19, premium skins). Called from the Unity editor's
// BAM > Online > End season (in Play), only by players whose ID is in the private custom item "bam-admins" key "ids" (as
// ExportTraces.js). Without "confirm" it only answers which season runs and who is in its top 10 now; with confirm equal to that
// season it saves those players' IDs as "top<season>" in the private custom item "bam-season" and starts the next season
// ("now" = { s: season + 1 }). Each player's next call (ReportMatch, GetMe, BuySkin) then starts its Rating again from 0 and
// gives a top-10 player one keep-pick: one premium skin of its choice for good, for Gold.KeepPrice gold.
// The leaderboard "rating" itself is reset by hand on the Unity Dashboard right after (Leaderboards > Rating > Reset), as for
// season 2: until then the old season's rows still stand on the board.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
const SEASONS = "bam-season", FIRST_SEASON = 2, TOP_N = 10;   // keep equal to ReportMatch.js
const status = e => (e && e.response && e.response.status) || 0;

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context), lb = new LeaderboardsApi(context);
  let ids = [];
  try { const adm = await data.getPrivateCustomItems(projectId, "bam-admins", ["ids"]); if (adm.data.results.length) ids = adm.data.results[0].value; }
  catch (e) { if (status(e) !== 404) throw e; }
  if (!Array.isArray(ids) || !ids.includes(playerId)) return { ok: false, you: playerId, season: 0, top: [] };

  let season = FIRST_SEASON;
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, ["now"]); const v = g.data.results.length ? g.data.results[0].value : null; if (v && Number.isInteger(v.s)) season = v.s; }
  catch (e) { if (status(e) !== 404) throw e; }

  const page = await lb.getLeaderboardScores(projectId, "rating", 0, TOP_N);
  const rows = ((page.data && page.data.results) || []).filter(r => r.score > 0);
  const top = rows.map(r => ({ playerId: r.playerId, name: (r.playerName || "").replace(/#\d+$/, ""), score: Math.round(r.score) }));
  if (Number(params.confirm) !== season) return { ok: false, season: season, top: top };

  await data.setPrivateCustomItem(projectId, SEASONS, { key: "top" + season, value: rows.map(r => r.playerId) });
  await data.setPrivateCustomItem(projectId, SEASONS, { key: "now", value: { s: season + 1, at: Math.floor(Date.now() / 1000) } });
  logger.info("EndSeason " + season + " by " + playerId + ", top " + rows.length);
  return { ok: true, season: season, top: top };
};

module.exports.params = { confirm: { type: "Numeric", required: false } };
