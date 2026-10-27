// Cloud Code "ReportPlayer" (ported from Speltest): a report about a player (rude name, cheating ...). Reports are kept in
// PRIVATE custom Cloud Save data "bam-reports", one key per reported player, so the Unity Dashboard shows every report about
// someone in one place (Cloud Save > Custom data > bam-reports). A player can send 10 reports a day and 3 about the same player.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const MAX_PER_DAY = 10, MAX_PER_TARGET = 3, KEEP = 50;
const REASONS = ["name", "cheating", "other"];

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const target = String(params.target || "");
  const reason = REASONS.includes(params.reason) ? params.reason : "other";
  if (!/^[A-Za-z0-9_-]{8,64}$/.test(target) || target === playerId) return { ok: false };
  const now = Math.floor(Date.now() / 1000);

  const mine = await data.getProtectedItems(projectId, playerId, ["bam-sent-reports"]);
  let sent = mine.data.results.length ? mine.data.results[0].value : [];
  const day = sent.filter(r => now - r.t < 86400);
  if (day.length >= MAX_PER_DAY || sent.filter(r => r.target === target).length >= MAX_PER_TARGET) return { ok: false };

  const about = await data.getPrivateCustomItems(projectId, "bam-reports", [target]);
  let list = about.data.results.length ? about.data.results[0].value : [];
  list.push({ from: playerId, reason: reason, t: now });
  if (list.length > KEEP) list = list.slice(list.length - KEEP);
  await data.setPrivateCustomItem(projectId, "bam-reports", { key: target, value: list });

  sent.push({ target: target, t: now });
  if (sent.length > KEEP) sent = sent.slice(sent.length - KEEP);
  await data.setProtectedItem(projectId, playerId, { key: "bam-sent-reports", value: sent });
  logger.warning("report " + playerId + " -> " + target + ": " + reason + " (" + list.length + " reports about this player)");
  return { ok: true };
};

module.exports.params = { target: { type: "STRING", required: true }, reason: "STRING" };
