// Cloud Code "DeleteMe": the first half of Delete account (Account.Delete). Frees the player's name and deletes the game's
// protected data. The phone then deletes the Unity player itself (AuthenticationService.DeleteAccountAsync), which removes the
// rest of the player's data in Unity's services, the leaderboard row and friends included.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const keyOf = name => "n_" + Buffer.from(name.toLowerCase(), "utf8").toString("hex");
const PVP = "bam-pvp";

/// Takes the player out of one "bam-pvp" entry, deleting the entry when nobody is left in it (a copy of ReportMatch.js's).
async function leavePvp(data, projectId, playerId, key) {
  try {
    const g = await data.getPrivateCustomItems(projectId, PVP, [key]);
    if (!g.data.results.length) return;
    const side = g.data.results[0].value;
    if (side.p) delete side.p[playerId];
    if (!side.p || Object.keys(side.p).length === 0) await data.deletePrivateCustomItem(key, projectId, PVP);
    else await data.setPrivateCustomItem(projectId, PVP, { key: key, value: side });
  } catch (e) { }
}

module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-name", "bam-matches"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const cur = item("bam-name") || null;
  // the player's ID out of the online-match results it reported (bug review 2026-11-02 nr 50)
  const seen = item("bam-matches") || {};
  for (const e of Array.isArray(seen.pvpKeys) ? seen.pvpKeys : []) await leavePvp(data, projectId, playerId, e.k);
  if (cur && cur.name) {
    const key = keyOf(cur.name);
    const owner = await data.getPrivateCustomItems(projectId, "bam-names", [key]);
    if (owner.data.results.length && owner.data.results[0].value.playerId === playerId) await data.deletePrivateCustomItem(key, projectId, "bam-names");
  }
  await data.deleteProtectedItems(projectId, playerId);
  await data.deleteItems(projectId, playerId);
  logger.info("DeleteMe " + playerId);
  return { ok: true };
};
