// Cloud Code "GetMe": the signed-in player's own name and totals (called by Server.Refresh at every sign-in).
// The totals live in PROTECTED Cloud Save data: the phone can read them, only Cloud Code can write them.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

// Seasons (2026-11-19): the season number lives in the private custom item "bam-season" key "now" ({ s: 3 }), written by
// EndSeason.js (2 until the first EndSeason); "top<season>" holds the IDs of the players who finished that season in the top 10.
// A player's first call in a new season starts its Rating again from 0 and gives one keep-pick (Gold.KeepPrice gold buys one
// premium skin for good, BuySkin.js) for every finished season it ended in the top 10. Keep equal in ReportMatch, GetMe, BuySkin.
const SEASONS = "bam-season", FIRST_SEASON = 2, TOP_N = 10;
async function seasonNow(data, projectId) {
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, ["now"]); const v = g.data.results.length ? g.data.results[0].value : null; return v && Number.isInteger(v.s) ? v.s : FIRST_SEASON; }
  catch (e) { return FIRST_SEASON; }
}
async function seasonPicks(data, projectId, playerId, from, to) {
  if (!Number.isInteger(from) || from >= to) return 0;
  const keys = []; for (let s = Math.max(from, to - 8); s < to; s++) keys.push("top" + s);
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, keys); return g.data.results.filter(r => Array.isArray(r.value) && r.value.includes(playerId)).length; }
  catch (e) { return 0; }
}
/// The player's place on the board when it is in the top 10 of this season with some Rating, else 0 (sent, never saved).
async function topPlace(lb, projectId, playerId, t, season) {
  if (t.rs !== season || !(t.rating > 0)) return 0;
  try { const s = await lb.getLeaderboardPlayerScore(projectId, "rating", playerId); const r = s.data.rank + 1; return r <= TOP_N ? r : 0; }
  catch (e) { return 0; }   // not on the board yet
}

module.exports = async ({ context }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-name", "bam-progress"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const name = item("bam-name"), t = item("bam-progress") || null;
  const season = await seasonNow(data, projectId);
  // a new season: Rating 0 and the keep-picks it gives (ReportMatch saves both at the next match)
  if (t && t.rs !== season) { t.picks = (t.picks || 0) + await seasonPicks(data, projectId, playerId, t.rs, season); t.rating = 0; t.pend = 0; t.rs = season; }
  // gold (2026-11-15): an account from before gold has 100 for every level it already has; ReportMatch saves it at the next match
  if (t && t.gold === undefined) { let l = 1, xp = t.xp || 0; while (l < 30 && xp >= 100 + 50 * (l - 1)) { xp -= 100 + 50 * (l - 1); l++; } let g = 0; for (let k = 2; k <= l; k++) g += k >= 15 && (k - 15) % 5 === 0 ? 500 : 100; t.gold = g; }
  if (t) t.top = await topPlace(new LeaderboardsApi(context), projectId, playerId, t, season);   // premium skins: top 10 only (Gold.cs)
  return { name: name ? name.name : null, totals: t };
};
