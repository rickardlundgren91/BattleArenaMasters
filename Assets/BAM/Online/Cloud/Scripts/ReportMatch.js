// Cloud Code "ReportMatch": the SERVER counts finished matches (Speltest ClaimRating.js + PlayerStats, 2026-10-26).
// The phone sends what happened in each match (won, length, kills, deaths, hero, bot level, when it ended); the server checks
// it, adds XP, Rating and stats with the same formula as Progress.cs, keeps the totals in protected Cloud Save data and puts
// the Rating on the leaderboard "rating". The phone can never send a level, a Rating or a total.
// Matches played offline arrive later in a batch: each is judged by the time it ended, not by when it arrived, so a batch is
// not refused by a cooldown (Speltest's 60 s cooldown refused every queued match but the first).
// Every match id is answered in "done", also the refused ones, so the phone stops sending them.
//
// Rating by opponent (Rickard 2026-10-29, "20 matcher till Rift"): Easy +15, Normal +35, Hard +75 / -25, Ultra +100 / -30 (2026-10-31), real players +150 / -50.
// Easy and Normal never take Rating away, and stop giving any at 300 and 700 (Rift and Vanguard reached), so grinding easy bots
// cannot reach the top.
// XP and level count every match as before. Rating never goes under 0. Season 2 started everyone's Rating again from 0.
// A match against real players ("vs", the online match's seed, and the player's team) gives the player Rating only once a
// player of the OTHER team has reported the same match with the opposite result: each side writes what it reported in private
// custom data "bam-pvp" (one key per match and team, so the two sides never overwrite each other), and reads the other side.
// The side that reports first waits ("wait") and gets its Rating on its next call, which the phone makes by itself a little
// later (an empty "matches" list is fine). Unconfirmed after WAIT_SECONDS, the match counts as a match against its bots.
// Leaving an online match against players ("quit") gives no XP or stats, only the players' Rating loss, and tells the
// other side the match was lost.
// Gold (Rickard 2026-11-15, Gold.cs): the day's first win 100, each of the day's three goals 50, each new level 100; a normal
// match none. The gold and the bought skins ("skins", hero ids) live in "bam-progress" too; BuySkin.js spends the gold. An
// account from before gold gets 100 for every level it already has, once. "bam-progress" is written with its write lock, so
// a BuySkin running at the same moment is never overwritten (this call then fails and the phone sends again later).
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

// keep equal to Progress.cs
const MAX_LEVEL = 30, XP_WIN = 100, XP_LOSS = 60, XP_KILL = 5, XP_FIGHT_MAX = 60, XP_FIRST_WIN = 50;
const RATING = { Easy: { win: 15, loss: 0, cap: 300 }, Normal: { win: 35, loss: 0, cap: 700 }, Hard: { win: 75, loss: -25, cap: 0 }, Ultra: { win: 100, loss: -30, cap: 0 }, Players: { win: 150, loss: -50, cap: 0 } };
const SEASON = 2;
// MAX_AGE 7 -> 30 days (bug review 2026-11-02 nr 15): the phone counts offline matches at once, and a player offline over a week
// lost level at the first sync when the server refused the older ones
const MIN_SECONDS = 45, MAX_SECONDS = 1800, MAX_PER_DAY = 60, MAX_AGE = 30 * 86400, MAX_BATCH = 20, KEEP_IDS = 300;
const WAIT_SECONDS = 1800, MAX_WAIT = 20, PVP = "bam-pvp";
// Rating from real players: at most this many wins a day against the same opponent (bug review 2026-11-02 nr 39: a second
// account kept at 0 Rating loses nothing and could feed +150 again and again)
const PVP_WINS_PER_OPP_DAY = 10, KEEP_SPANS = 120; // 10, not 3: few players to meet yet (Rickard 2026-11-04 "3 om dagen ... låter snålt")
// "bam-pvp" entries are deleted when they are over a day old (bug review 2026-11-02 nr 50): they are only needed while the other
// side may still confirm (WAIT_SECONDS), and they hold player IDs. The key starts with the match's day ("d020761v123t0"), so the
// oldest come first in the key list and any player's ReportMatch sweeps a few, also those of players who stopped playing. Keys
// from before the day prefix ("v123t0") are swept by their "at". DeleteMe takes the player out of its own entries at once.
const PVP_KEEP = 86400, PVP_CLEAN_PER_CALL = 10;
const pvpKey = (vs, team, day) => "d" + String(day).padStart(6, "0") + vs + "t" + team;

/// Deletes up to PVP_CLEAN_PER_CALL "bam-pvp" entries over a day old: day-prefixed keys from the front of the list, then the
/// old keys without a day. Never fails the report.
async function sweepPvp(data, projectId, now) {
  try {
    const today = Math.floor(now / 86400);
    let left = PVP_CLEAN_PER_CALL;
    const first = await data.getPrivateCustomKeys(projectId, PVP);
    for (const r of (first.data.results || [])) {
      if (left <= 0 || r.key[0] !== "d") break;
      if (Number(r.key.slice(1, 7)) >= today - 1) break;   // sorted: everything after is newer
      await data.deletePrivateCustomItem(r.key, projectId, PVP); left--;
    }
    if (left <= 0) return;
    const legacy = await data.getPrivateCustomKeys(projectId, PVP, "u");   // keys after "u": the old "v..." ones
    const keys = (legacy.data.results || []).map(r => r.key).filter(k => k[0] === "v").slice(0, 20);
    if (!keys.length) return;
    const got = await data.getPrivateCustomItems(projectId, PVP, keys);
    for (const r of got.data.results) {
      if (left <= 0) break;
      if (!(r.value && r.value.at > now - PVP_KEEP)) { await data.deletePrivateCustomItem(r.key, projectId, PVP); left--; }
    }
  } catch (e) { }
}
// Kugg joined the game as hero 2 for everyone (0.3.9, main 5d8275e), Ragn and Nässla (code name Nassla) in 0.4.0 (Ravna back 2026-11-14: publish this script again before a build with her goes to testers); a hero missing
// here makes every match with him refused
// ("hero or bot level") and the phone drops it for good (full review 2026-11-09 nr 68). Keep this list equal to HeroCatalog.All.
const HEROES = ["Bram", "Fenn", "Tyra", "Moa", "Knut", "Tikta", "Blast", "Bonk", "Kugg", "Hilde", "Oaken", "Ragn", "Nassla", "Ravna"], LEVELS = ["Easy", "Normal", "Hard", "Ultra"];

// keep equal to Gold.cs (and BuySkin.js)
const GOLD_FIRST_WIN = 100, GOLD_GOAL = 50, GOLD_LEVEL = 100;
const GOAL_TARGET = [3, 2, 10, 1], GOAL_HEROES = ["Tyra", "Kugg", "Bram", "Fenn", "Ragn", "Nassla"];
/// The day's three goals (all four kinds but one: 0 play, 1 win, 2 kills, 3 a match with one hero) and that hero, from the day alone.
function dayGoals(day) {
  const h = (day * 2654435761) % 4294967296, skip = h % 4;
  return { goals: [0, 1, 2, 3].filter(g => g !== skip), hero: GOAL_HEROES[Math.floor(h / 256) % GOAL_HEROES.length] };
}
function applyGold(t, day, won, kills, hero, firstWin, levelBefore, levelAfter) {
  if (levelAfter > levelBefore) t.gold += GOLD_LEVEL * (levelAfter - levelBefore);
  if (firstWin) t.gold += GOLD_FIRST_WIN;
  if (day > t.gDay) { t.gDay = day; t.gm = t.gw = t.gk = t.gh = t.gDone = 0; }
  if (day !== t.gDay) return;
  t.gm++; if (won) t.gw++; t.gk += Math.min(60, Math.max(0, kills));
  const d = dayGoals(day);
  if (hero === d.hero) t.gh = 1;
  const have = [t.gm, t.gw, t.gk, t.gh];
  for (const g of d.goals) if (!(t.gDone & (1 << g)) && have[g] >= GOAL_TARGET[g]) { t.gDone |= 1 << g; t.gold += GOLD_GOAL; }
}

const xpToNext = l => 100 + 50 * (l - 1);
function levelOf(xp) { let l = 1; while (l < MAX_LEVEL && xp >= xpToNext(l)) { xp -= xpToNext(l); l++; } return l; }
const fresh = () => ({ xp: 0, level: 1, rating: 0, rs: SEASON, pend: 0, matches: 0, wins: 0, losses: 0, kills: 0, deaths: 0, streak: 0, bestStreak: 0, seconds: 0, winDay: -1, heroes: [], mk: {}, gold: 0, gm: 0, gw: 0, gk: 0, gh: 0, gDone: 0, gDay: -1, skins: [] });
const int = (v, lo, hi) => Number.isInteger(v) && v >= lo && v <= hi;

/// The Rating one match changes: a win up to the cap of its kind (0 = none), a loss never under 0.
function ratingChange(rating, kind, won) {
  const r = RATING[kind];
  if (!won) return Math.max(r.loss, -rating);
  return r.cap > 0 ? Math.max(0, Math.min(r.win, r.cap - rating)) : r.win;
}

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context), lb = new LeaderboardsApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-progress", "bam-matches", "bam-name"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const old = item("bam-progress"), t = Object.assign(fresh(), old || {});
  const lock = (got.data.results.find(r => r.key === "bam-progress") || {}).writeLock;
  const seen = Object.assign({ ids: [], lastAt: 0, days: {}, wait: [] }, item("bam-matches") || {});
  const name = (item("bam-name") || {}).name || "";
  const now = Math.floor(Date.now() / 1000), today = Math.floor(now / 86400);
  let changed = false;
  if (old && old.rs !== SEASON) { t.rating = 0; t.rs = SEASON; t.pend = 0; seen.wait = []; changed = true; }
  if (old && old.gold === undefined) { t.gold = GOLD_LEVEL * (levelOf(t.xp) - 1); changed = true; }   // levels from before gold

  // the other team's entry: its players may date the match a day either side of ours (midnight, clocks), so all three are read
  const otherSide = async (vs, team, day) => {
    const g = await data.getPrivateCustomItems(projectId, PVP, [day - 1, day, day + 1].map(d => pvpKey(vs, 1 - team, d)));
    if (!g.data.results.length) return null;
    const side = { p: {} };
    for (const r of g.data.results) Object.assign(side.p, (r.value && r.value.p) || {});
    return side;
  };
  // a player of the other team reported this match with the opposite result
  const opponents = (side, won) => side ? Object.keys(side.p || {}).filter(id => id !== playerId && side.p[id] === !won) : [];
  const rate = (kind, won) => { t.rating += ratingChange(t.rating, kind, won); };
  // a confirmed match against players: a win counts only while no opponent in it has given this player
  // PVP_WINS_PER_OPP_DAY wins today (nr 39); a loss always counts
  const pvpDay = seen.pvpDay === today && seen.pvpWins ? seen.pvpWins : {};
  seen.pvpDay = today; seen.pvpWins = pvpDay;
  const ratePlayers = (won, opps) => {
    if (won) {
      if (opps.some(o => (pvpDay[o] || 0) >= PVP_WINS_PER_OPP_DAY)) return;
      for (const o of opps) pvpDay[o] = (pvpDay[o] || 0) + 1;
    }
    rate("Players", won);
  };

  // earlier matches against players that waited for the other side
  const still = [];
  for (const w of seen.wait) {
    const opps = opponents(await otherSide(w.vs, w.team, w.day || Math.floor(w.at / 86400)), w.won);
    if (opps.length) { ratePlayers(w.won, opps); changed = true; }
    else if (now - w.at > WAIT_SECONDS) { if (LEVELS.includes(w.diff)) rate(w.diff, w.won); changed = true; }
    else still.push(w);
  }
  seen.wait = still;

  await sweepPvp(data, projectId, now);
  // the entries this player wrote, for DeleteMe; the sweep deletes them after a day, so older ones are forgotten here too
  const mineKeys = (Array.isArray(seen.pvpKeys) ? seen.pvpKeys : []).filter(e => now - e.at <= 2 * PVP_KEEP);
  if (mineKeys.length !== (seen.pvpKeys || []).length) changed = true;
  seen.pvpKeys = mineKeys;

  const list = Array.isArray(params.matches) ? params.matches.slice(0, MAX_BATCH) : [];
  list.sort((a, b) => (a.at || 0) - (b.at || 0));
  // a phone whose clock runs fast: the batch is moved back so its newest match ended now, keeping the gaps between them
  // (bug review 2026-11-02 nr 17: a clock over 60 s fast had every match refused as "in the future" and deleted)
  const newest = list.reduce((mx, m) => Number.isInteger(m.at) && m.at > mx ? m.at : mx, 0);
  const skew = newest > now + 60 ? now - newest : 0;
  // the matches already counted, as [start, end]: a new one is refused only if it overlaps one of them, so an older match
  // arriving after a newer one (two phones, a clock jump) still counts (nr 37); before, only the newest end was kept
  const spans = Array.isArray(seen.spans) ? seen.spans : [];
  const overlaps = (a, b) => spans.some(sp => a < sp[1] - 5 && b > sp[0] + 5);
  const done = [], refused = [];
  for (const m of list) {
    const id = typeof m.id === "string" ? m.id.slice(0, 40) : "";
    if (!id) continue;
    done.push(id);
    const vs = typeof m.vs === "string" && /^v\d{1,10}$/.test(m.vs) ? m.vs : "", quit = m.quit === true;
    const at = Number.isInteger(m.at) ? m.at + skew : m.at;
    let why = null;
    if (seen.ids.includes(id)) why = "already counted";
    else if (!int(at, now - MAX_AGE, now + 60)) why = "too old or in the future";
    else if (quit ? !vs || !int(m.seconds, 0, MAX_SECONDS) : !int(m.seconds, MIN_SECONDS, MAX_SECONDS)) why = "length";
    else if (!int(m.kills, 0, 60) || !int(m.deaths, 0, 60)) why = "numbers";
    else if (!HEROES.includes(m.hero) || !LEVELS.includes(m.difficulty)) why = "hero or bot level";
    else if (vs && !int(m.team, 0, 1)) why = "team";
    else if (overlaps(at - m.seconds, at)) why = "overlaps another match";
    else if ((seen.days[Math.floor(at / 86400)] || 0) >= MAX_PER_DAY) why = "daily cap";
    if (why) { refused.push(id + ": " + why); continue; }

    const day = Math.floor(at / 86400), won = m.won === true && !quit;
    if (!quit) {
      const firstWin = won && t.winDay !== day, levelBefore = levelOf(t.xp);
      t.xp += (won ? XP_WIN : XP_LOSS) + Math.min(XP_FIGHT_MAX, m.kills * XP_KILL) + (firstWin ? XP_FIRST_WIN : 0);
      applyGold(t, day, won, m.kills, m.hero, firstWin, levelBefore, levelOf(t.xp));
      if (firstWin) t.winDay = day;
      t.matches++;
      if (won) { t.wins++; t.streak++; t.bestStreak = Math.max(t.bestStreak, t.streak); } else { t.losses++; t.streak = 0; }
      t.kills += m.kills; t.deaths += m.deaths; t.seconds += m.seconds;
      // the player's mistakes per bot level, so the bots can copy them (2026-10-31); older apps send none
      const mk = t.mk[m.difficulty] = Object.assign({ m: 0, d: 0, tower: 0, ready: 0 }, t.mk[m.difficulty] || {});
      mk.m++; mk.d += m.deaths;
      if (int(m.towerDeaths, 0, m.deaths)) mk.tower += m.towerDeaths;
      if (int(m.readyDeaths, 0, m.deaths)) mk.ready += m.readyDeaths;
      t.level = levelOf(t.xp);
      let h = t.heroes.find(x => x.id === m.hero);
      if (!h) { h = { id: m.hero, m: 0, w: 0 }; t.heroes.push(h); }
      h.m++; if (won) h.w++;
    }

    if (vs) {
      // tell the other side what this player reported, then see if it has reported too
      const key = pvpKey(vs, m.team, day);
      const mine = await data.getPrivateCustomItems(projectId, PVP, [key]);
      const side = mine.data.results.length ? mine.data.results[0].value : { at: at, p: {} };
      side.p[playerId] = won;
      await data.setPrivateCustomItem(projectId, PVP, { key: key, value: side });
      if (!mineKeys.some(e => e.k === key)) mineKeys.push({ k: key, at: now });
      const opps = opponents(await otherSide(vs, m.team, day), won);
      if (quit) rate("Players", won);
      else if (opps.length) ratePlayers(won, opps);
      else if (seen.wait.length < MAX_WAIT) seen.wait.push({ vs: vs, team: m.team, won: won, diff: m.difficulty, at: now, day: day });
      else rate(m.difficulty, won);
    } else rate(m.difficulty, won);

    seen.ids.push(id);
    seen.lastAt = Math.max(seen.lastAt, at);
    spans.push([at - m.seconds, at]);
    seen.days[day] = (seen.days[day] || 0) + 1;
    changed = true;
  }
  if (refused.length) logger.info("ReportMatch " + playerId + " refused " + refused.join("; "));

  // what the waiting matches would give if confirmed, so the phone can show it meanwhile
  let pend = 0;
  for (const w of seen.wait) pend += ratingChange(t.rating + pend, "Players", w.won);
  if (pend !== t.pend) { t.pend = pend; changed = true; }

  if (changed) {
    if (seen.ids.length > KEEP_IDS) seen.ids = seen.ids.slice(seen.ids.length - KEEP_IDS);
    spans.sort((a, b) => a[1] - b[1]);
    seen.spans = spans.filter(sp => sp[1] >= now - MAX_AGE - 3600).slice(-KEEP_SPANS);
    for (const d of Object.keys(seen.days)) if (Number(d) < today - 31) delete seen.days[d];
    await data.setProtectedItemBatch(projectId, playerId, { data: [Object.assign({ key: "bam-progress", value: t }, lock ? { writeLock: lock } : {}), { key: "bam-matches", value: seen }] });
    await lb.addLeaderboardPlayerScore(projectId, "rating", playerId, { score: t.rating, metadata: { n: name, l: t.level } });
  }
  return { totals: t, done: done, waiting: seen.wait.length };
};

module.exports.params = { matches: { type: "JSON", required: true } };

