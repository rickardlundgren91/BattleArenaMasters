// Cloud Code "SetName": gives the player a UNIQUE name (Rickard 2026-10-26: "a unique player and name for every player").
// Names live in PRIVATE custom Cloud Save data "bam-names" (only Cloud Code can read or write it): one key per name, lower
// case, holding the owner's player ID. A name can belong to one player only; the old name is freed when it changes.
// Rules: 3-16 letters, digits, _ - or . ; not a reserved or rude word; a new name at most every 7 days (the first is free).
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

const NAMES = "bam-names", CHANGE_EVERY = 7 * 86400;
const RESERVED = ["admin", "administrator", "bam", "bamgames", "battlearenamasters", "moderator", "mod", "support", "unity", "google", "system", "official", "staff", "player", "deleted"];
// a short starter list; matched against the name without digits and separators. Grow it from the reports.
const RUDE = ["fuck", "shit", "cunt", "nigg", "fagg", "bitch", "whore", "rape", "nazi", "hitler", "kuk", "fitta", "hora", "neger", "bög", "jävla"];

const keyOf = name => "n_" + Buffer.from(name.toLowerCase(), "utf8").toString("hex");
function problem(name) {
  if (typeof name !== "string") return "No name";
  if (name.length < 3) return "At least 3 letters";
  if (name.length > 16) return "At most 16 letters";
  if (!/^[\p{L}\p{N}_.\-]+$/u.test(name)) return "Letters, digits, _ - and . only";
  if (!/\p{L}/u.test(name)) return "Use at least one letter";
  const plain = name.toLowerCase().replace(/[^\p{L}]/gu, "");
  if (RESERVED.includes(plain) || RESERVED.includes(name.toLowerCase())) return "That name is reserved";
  if (RUDE.some(w => plain.includes(w))) return "Choose another name";
  return null;
}

module.exports = async ({ params, context }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const name = String(params.name || "").trim();
  const bad = problem(name);
  if (bad) return { ok: false, reason: bad };

  const mine = await data.getProtectedItems(projectId, playerId, ["bam-name", "bam-progress"]);
  const item = k => (mine.data.results.find(r => r.key === k) || {}).value;
  const cur = item("bam-name");
  const now = Math.floor(Date.now() / 1000);
  if (cur && cur.name === name) return { ok: true, name: name };
  const sameName = cur && cur.name.toLowerCase() === name.toLowerCase();   // only the capitals change: always allowed
  if (cur && !sameName && now - (cur.changedAt || 0) < CHANGE_EVERY) {
    const days = Math.ceil((CHANGE_EVERY - (now - cur.changedAt)) / 86400);
    return { ok: false, reason: "You can change your name again in " + days + (days === 1 ? " day" : " days") };
  }

  const key = keyOf(name);
  const taken = await data.getPrivateCustomItems(projectId, NAMES, [key]);
  const owner = taken.data.results.length ? taken.data.results[0].value.playerId : null;
  if (owner && owner !== playerId) return { ok: false, reason: "That name is taken" };
  await data.setPrivateCustomItem(projectId, NAMES, { key: key, value: { playerId: playerId, name: name, at: now } });
  // two players asking for the same name at the same moment: the one whose write is there afterwards gets it
  const check = await data.getPrivateCustomItems(projectId, NAMES, [key]);
  if (!check.data.results.length || check.data.results[0].value.playerId !== playerId) return { ok: false, reason: "That name is taken" };

  if (cur && !sameName) { try { await data.deletePrivateCustomItem(keyOf(cur.name), projectId, NAMES); } catch (e) { } }
  await data.setProtectedItem(projectId, playerId, { key: "bam-name", value: { name: name, changedAt: sameName ? cur.changedAt : (cur ? now : 0) } });

  // the Top 100 shows the new name at once (the score is the server's own total, unchanged)
  const t = item("bam-progress");
  if (t && t.matches > 0) {
    const lb = new LeaderboardsApi(context);
    await lb.addLeaderboardPlayerScore(projectId, "rating", playerId, { score: t.rating, metadata: { n: name, l: t.level } });
  }
  return { ok: true, name: name };
};

module.exports.params = { name: { type: "STRING", required: true } };
