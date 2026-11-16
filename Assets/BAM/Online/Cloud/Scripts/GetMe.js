// Cloud Code "GetMe": the signed-in player's own name and totals (called by Server.Refresh at every sign-in).
// The totals live in PROTECTED Cloud Save data: the phone can read them, only Cloud Code can write them.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const SEASON = 2;   // keep equal to ReportMatch.js

module.exports = async ({ context }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-name", "bam-progress"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const name = item("bam-name"), t = item("bam-progress") || null;
  if (t && t.rs !== SEASON) { t.rating = 0; t.pend = 0; }   // Rating season 2 (2026-10-29): ReportMatch saves the 0 at the next match
  // gold (2026-11-15): an account from before gold has 100 for every level it already has; ReportMatch saves it at the next match
  if (t && t.gold === undefined) { let l = 1, xp = t.xp || 0; while (l < 30 && xp >= 100 + 50 * (l - 1)) { xp -= 100 + 50 * (l - 1); l++; } let g = 0; for (let k = 2; k <= l; k++) g += k >= 15 && (k - 15) % 5 === 0 ? 500 : 100; t.gold = g; }
  return { name: name ? name.name : null, totals: t };
};
