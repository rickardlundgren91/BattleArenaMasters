// Cloud Code "DeleteMe": the first half of Delete account (Account.Delete). Frees the player's name and deletes the game's
// protected data. The phone then deletes the Unity player itself (AuthenticationService.DeleteAccountAsync), which removes the
// rest of the player's data in Unity's services, the leaderboard row and friends included.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const keyOf = name => "n_" + Buffer.from(name.toLowerCase(), "utf8").toString("hex");

module.exports = async ({ context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-name"]);
  const cur = got.data.results.length ? got.data.results[0].value : null;
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
