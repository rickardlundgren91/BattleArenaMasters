// Cloud Code "GetProfile": another player's public profile, built by the server from its own protected data, so what others
// see is what the server counted (Speltest showed what the other phone said about itself). It answers with the public part of
// bam-progress only: level and XP, Rating, the totals and the three most played heroes (privacy policy section 4).
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
const SEASONS = "bam-season", FIRST_SEASON = 2;   // keep equal to ReportMatch.js
async function seasonNow(data, projectId) {
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, ["now"]); const v = g.data.results.length ? g.data.results[0].value : null; return v && Number.isInteger(v.s) ? v.s : FIRST_SEASON; }
  catch (e) { return FIRST_SEASON; }
}

module.exports = async ({ params, context }) => {
  const { projectId } = context;
  const id = String(params.playerId || "");
  if (!/^[A-Za-z0-9_-]{8,64}$/.test(id)) return { playerId: id, name: null, totals: null, rank: 0 };
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, id, ["bam-name", "bam-progress"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const t = item("bam-progress") || null;
  if (t) {
    delete t.winDay; delete t.pend;
    if (t.rs !== await seasonNow(data, projectId)) t.rating = 0;   // an old season's Rating shows as 0
    // only what the profile page shows and the privacy policy calls public (full review 2026-11-09 nr 95): no current streak, no
    // season number, no bot-level mistakes (mk), and of the per-hero lines only the three most played (the phone sorts them again)
    delete t.streak; delete t.rs; delete t.mk;
    // gold (2026-11-15) is the player's own: not the balance, the day's goals or the bought skins
    delete t.gold; delete t.gDay; delete t.gm; delete t.gw; delete t.gk; delete t.gh; delete t.gDone; delete t.skins;
    delete t.prem; delete t.picks; delete t.top;
    t.heroes = (t.heroes || []).sort((a, b) => b.m - a.m).slice(0, 3);
  }
  let rank = 0;
  try {
    const lb = new LeaderboardsApi(context);
    const s = await lb.getLeaderboardPlayerScore(projectId, "rating", id);
    rank = s.data.rank + 1;
  } catch (e) { }   // not on the board yet
  return { playerId: id, name: (item("bam-name") || {}).name || null, totals: t, rank: rank };
};

module.exports.params = { playerId: { type: "STRING", required: true } };
