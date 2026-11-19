// Cloud Code "BuySkin": buys one hero's gold skin (skin 1, Rickard 2026-11-15, Gold.cs), or keeps its premium skin for good
// (skin 2, Rickard 2026-11-19: a season finished in the top 10 gives one keep-pick, "prem" lists the heroes kept, "picks" what is
// left; KEEP_PRICE gold). An app from before premium sends no skin: the gold skin. The phone names the hero; the server takes
// SKIN_PRICE from the gold it keeps in the protected "bam-progress" item (the phone can read it, only Cloud Code writes it)
// and adds the hero to "skins". Gold is earned only in ReportMatch.js and never sold for money. The item is written with its
// write lock, so a ReportMatch at the same moment is never lost: one of the two fails and is tried again.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

// keep equal to Gold.cs and ReportMatch.js
const SKIN_PRICE = 1500, KEEP_PRICE = 3000, GOLD_LEVEL = 100;
const HEROES = ["Bram", "Fenn", "Tyra", "Moa", "Knut", "Tikta", "Blast", "Bonk", "Kugg", "Hilde", "Oaken", "Ragn", "Nassla", "Ravna"];
const xpToNext = l => 100 + 50 * (l - 1);
// Seasons (2026-11-19): the season number lives in the private custom item "bam-season" key "now" ({ s: 3 }), written by
// EndSeason.js (2 until the first EndSeason); "top<season>" holds the IDs of the players who finished that season in the top 10.
// A player's first call in a new season starts its Rating again from 0 and gives one keep-pick (Gold.KeepPrice gold buys one
// premium skin for good, BuySkin.js) for every finished season it ended in the top 10. Keep equal in ReportMatch, GetMe, BuySkin.
const SEASONS = "bam-season", FIRST_SEASON = 2, TOP_N = 10;
async function seasonNow(data, projectId) {
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, ["now"]); const v = g.data.results.length ? g.data.results[0].value : null; return v && Number.isInteger(v.s) ? v.s : FIRST_SEASON; }
  catch (e) { return FIRST_SEASON; }
}
async function seasonPicks(data, projectId, playerId, from, to) {
  if (!Number.isInteger(from) || from >= to) return 0;
  const keys = []; for (let s = Math.max(from, to - 8); s < to; s++) keys.push("top" + s);
  try { const g = await data.getPrivateCustomItems(projectId, SEASONS, keys); return g.data.results.filter(r => Array.isArray(r.value) && r.value.includes(playerId)).length; }
  catch (e) { return 0; }
}
/// The player's place on the board when it is in the top 10 of this season with some Rating, else 0 (sent, never saved).
async function topPlace(lb, projectId, playerId, t, season) {
  if (t.rs !== season || !(t.rating > 0)) return 0;
  try { const s = await lb.getLeaderboardPlayerScore(projectId, "rating", playerId); const r = s.data.rank + 1; return r <= TOP_N ? r : 0; }
  catch (e) { return 0; }   // not on the board yet
}
function levelOf(xp) { let l = 1; while (l < 30 && xp >= xpToNext(l)) { xp -= xpToNext(l); l++; } return l; }

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const hero = typeof params.hero === "string" ? params.hero : "";
  if (!HEROES.includes(hero)) return { ok: false, reason: "Unknown hero" };
  const premium = Number(params.skin) >= 2;
  const got = await data.getProtectedItems(projectId, playerId, ["bam-progress"]);
  const row = got.data.results.find(r => r.key === "bam-progress");
  if (!row || !row.value) return { ok: false, reason: "Not enough gold" };
  const t = row.value;
  if (t.gold === undefined) { let g = 0; for (let l = 2; l <= levelOf(t.xp || 0); l++) g += l >= 15 && (l - 15) % 5 === 0 ? 500 : GOLD_LEVEL; t.gold = g; }   // an account from before gold (levels 15/20/25/30: 500)
  if (!Array.isArray(t.skins)) t.skins = [];
  if (!Array.isArray(t.prem)) t.prem = [];
  const season = await seasonNow(data, projectId);
  // a new season not counted yet (ReportMatch does the same): Rating 0 and its keep-picks
  if (t.rs !== season) { t.picks = (t.picks || 0) + await seasonPicks(data, projectId, playerId, t.rs, season); t.rating = 0; t.pend = 0; t.rs = season; }
  delete t.top;
  const answer = async (ok, reason) => { t.top = await topPlace(new LeaderboardsApi(context), projectId, playerId, t, season); return Object.assign({ ok: ok, totals: t }, reason ? { reason: reason } : {}); };
  const owned = premium ? t.prem : t.skins, price = premium ? KEEP_PRICE : SKIN_PRICE;
  if (owned.includes(hero)) return answer(true);
  if (premium && !(t.picks > 0)) return answer(false, "Finish a season in the top 10");
  if (t.gold < price) return answer(false, "Not enough gold");
  t.gold -= price;
  owned.push(hero);
  if (premium) t.picks--;
  try {
    await data.setProtectedItem(projectId, playerId, Object.assign({ key: "bam-progress", value: t }, row.writeLock ? { writeLock: row.writeLock } : {}));
  } catch (e) {
    logger.info("BuySkin " + playerId + " " + hero + " not saved: " + e.message);
    return { ok: false, reason: "Try again" };
  }
  logger.info("BuySkin " + playerId + " " + hero + (premium ? " premium" : ""));
  return answer(true);
};

module.exports.params = { hero: { type: "String", required: true }, skin: { type: "Numeric", required: false } };
