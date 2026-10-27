// Cloud Code "GetMe": the signed-in player's own name and totals (called by Server.Refresh at every sign-in).
// The totals live in PROTECTED Cloud Save data: the phone can read them, only Cloud Code can write them.
const { DataApi } = require("@unity-services/cloud-save-1.4");

module.exports = async ({ context }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-name", "bam-progress"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const name = item("bam-name");
  return { name: name ? name.name : null, totals: item("bam-progress") || null };
};
