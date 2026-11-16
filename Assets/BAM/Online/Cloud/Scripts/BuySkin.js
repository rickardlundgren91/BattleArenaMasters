// Cloud Code "BuySkin": buys one hero's gold skin (Rickard 2026-11-15, Gold.cs). The phone names the hero; the server takes
// SKIN_PRICE from the gold it keeps in the protected "bam-progress" item (the phone can read it, only Cloud Code writes it)
// and adds the hero to "skins". Gold is earned only in ReportMatch.js and never sold for money. The item is written with its
// write lock, so a ReportMatch at the same moment is never lost: one of the two fails and is tried again.
const { DataApi } = require("@unity-services/cloud-save-1.4");

// keep equal to Gold.cs and ReportMatch.js
const SKIN_PRICE = 1500, GOLD_LEVEL = 100;
const HEROES = ["Bram", "Fenn", "Tyra", "Moa", "Knut", "Tikta", "Blast", "Bonk", "Kugg", "Hilde", "Oaken", "Ragn", "Nassla", "Ravna"];
const xpToNext = l => 100 + 50 * (l - 1);
function levelOf(xp) { let l = 1; while (l < 30 && xp >= xpToNext(l)) { xp -= xpToNext(l); l++; } return l; }

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const hero = typeof params.hero === "string" ? params.hero : "";
  if (!HEROES.includes(hero)) return { ok: false, reason: "Unknown hero" };
  const got = await data.getProtectedItems(projectId, playerId, ["bam-progress"]);
  const row = got.data.results.find(r => r.key === "bam-progress");
  if (!row || !row.value) return { ok: false, reason: "Not enough gold" };
  const t = row.value;
  if (t.gold === undefined) { let g = 0; for (let l = 2; l <= levelOf(t.xp || 0); l++) g += l >= 15 && (l - 15) % 5 === 0 ? 500 : GOLD_LEVEL; t.gold = g; }   // an account from before gold (levels 15/20/25/30: 500)
  if (!Array.isArray(t.skins)) t.skins = [];
  if (t.skins.includes(hero)) return { ok: true, totals: t };
  if (t.gold < SKIN_PRICE) return { ok: false, reason: "Not enough gold", totals: t };
  t.gold -= SKIN_PRICE;
  t.skins.push(hero);
  try {
    await data.setProtectedItem(projectId, playerId, Object.assign({ key: "bam-progress", value: t }, row.writeLock ? { writeLock: row.writeLock } : {}));
  } catch (e) {
    logger.info("BuySkin " + playerId + " " + hero + " not saved: " + e.message);
    return { ok: false, reason: "Try again" };
  }
  logger.info("BuySkin " + playerId + " " + hero);
  return { ok: true, totals: t };
};

module.exports.params = { hero: { type: "String", required: true } };
