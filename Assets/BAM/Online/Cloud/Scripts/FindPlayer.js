// Cloud Code "FindPlayer": the player ID behind a name (names are unique, SetName.js). Used by Add friend.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const keyOf = name => "n_" + Buffer.from(name.toLowerCase(), "utf8").toString("hex");

module.exports = async ({ params, context }) => {
  const name = String(params.name || "").trim();
  if (name.length < 3 || name.length > 16) return { playerId: null };
  const data = new DataApi(context);
  const got = await data.getPrivateCustomItems(context.projectId, "bam-names", [keyOf(name)]);
  return { playerId: got.data.results.length ? got.data.results[0].value.playerId : null };
};

module.exports.params = { name: { type: "STRING", required: true } };
