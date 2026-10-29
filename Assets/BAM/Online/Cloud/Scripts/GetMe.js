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
  return { name: name ? name.name : null, totals: t };
};
