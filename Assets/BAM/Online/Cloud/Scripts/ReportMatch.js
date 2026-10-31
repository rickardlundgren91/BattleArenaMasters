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
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

// keep equal to Progress.cs
const MAX_LEVEL = 30, XP_WIN = 100, XP_LOSS = 60, XP_KILL = 5, XP_FIGHT_MAX = 60, XP_FIRST_WIN = 50;
const RATING = { Easy: { win: 15, loss: 0, cap: 300 }, Normal: { win: 35, loss: 0, cap: 700 }, Hard: { win: 75, loss: -25, cap: 0 }, Ultra: { win: 100, loss: -30, cap: 0 }, Players: { win: 150, loss: -50, cap: 0 } };
const SEASON = 2;
const MIN_SECONDS = 45, MAX_SECONDS = 1800, MAX_PER_DAY = 60, MAX_AGE = 7 * 86400, MAX_BATCH = 20, KEEP_IDS = 300;
const WAIT_SECONDS = 1800, MAX_WAIT = 20, PVP = "bam-pvp";
const HEROES = ["Bram", "Fenn", "Tyra", "Moa", "Knut", "Tikta", "Blast", "Bonk", "Hilde", "Oaken"], LEVELS = ["Easy", "Normal", "Hard", "Ultra"];

const xpToNext = l => 100 + 50 * (l - 1);
function levelOf(xp) { let l = 1; while (l < MAX_LEVEL && xp >= xpToNext(l)) { xp -= xpToNext(l); l++; } return l; }
const fresh = () => ({ xp: 0, level: 1, rating: 0, rs: SEASON, pend: 0, matches: 0, wins: 0, losses: 0, kills: 0, deaths: 0, streak: 0, bestStreak: 0, seconds: 0, winDay: -1, heroes: [], mk: {} });
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
  const seen = Object.assign({ ids: [], lastAt: 0, days: {}, wait: [] }, item("bam-matches") || {});
  const name = (item("bam-name") || {}).name || "";
  const now = Math.floor(Date.now() / 1000), today = Math.floor(now / 86400);
  let changed = false;
  if (old && old.rs !== SEASON) { t.rating = 0; t.rs = SEASON; t.pend = 0; seen.wait = []; changed = true; }

  const pvpKey = (vs, team) => vs + "t" + team;
  const otherSide = async (vs, team) => {
    const g = await data.getPrivateCustomItems(projectId, PVP, [pvpKey(vs, 1 - team)]);
    return g.data.results.length ? g.data.results[0].value : null;
  };
  // a player of the other team reported this match with the opposite result
  const confirmed = (side, won) => !!side && Object.keys(side.p || {}).some(id => id !== playerId && side.p[id] === !won);
  const rate = (kind, won) => { t.rating += ratingChange(t.rating, kind, won); };

  // earlier matches against players that waited for the other side
  const still = [];
  for (const w of seen.wait) {
    if (confirmed(await otherSide(w.vs, w.team), w.won)) { rate("Players", w.won); changed = true; }
    else if (now - w.at > WAIT_SECONDS) { if (LEVELS.includes(w.diff)) rate(w.diff, w.won); changed = true; }
    else still.push(w);
  }
  seen.wait = still;

  const list = Array.isArray(params.matches) ? params.matches.slice(0, MAX_BATCH) : [];
  list.sort((a, b) => (a.at || 0) - (b.at || 0));
  const done = [], refused = [];
  for (const m of list) {
    const id = typeof m.id === "string" ? m.id.slice(0, 40) : "";
    if (!id) continue;
    done.push(id);
    const vs = typeof m.vs === "string" && /^v\d{1,10}$/.test(m.vs) ? m.vs : "", quit = m.quit === true;
    let why = null;
    if (seen.ids.includes(id)) why = "already counted";
    else if (!int(m.at, now - MAX_AGE, now + 60)) why = "too old or in the future";
    else if (quit ? !vs || !int(m.seconds, 0, MAX_SECONDS) : !int(m.seconds, MIN_SECONDS, MAX_SECONDS)) why = "length";
    else if (!int(m.kills, 0, 60) || !int(m.deaths, 0, 60)) why = "numbers";
    else if (!HEROES.includes(m.hero) || !LEVELS.includes(m.difficulty)) why = "hero or bot level";
    else if (vs && !int(m.team, 0, 1)) why = "team";
    else if (m.at - m.seconds < seen.lastAt - 5) why = "overlaps the previous match";
    else if ((seen.days[Math.floor(m.at / 86400)] || 0) >= MAX_PER_DAY) why = "daily cap";
    if (why) { refused.push(id + ": " + why); continue; }

    const day = Math.floor(m.at / 86400), won = m.won === true && !quit;
    if (!quit) {
      const firstWin = won && t.winDay !== day;
      t.xp += (won ? XP_WIN : XP_LOSS) + Math.min(XP_FIGHT_MAX, m.kills * XP_KILL) + (firstWin ? XP_FIRST_WIN : 0);
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
      const key = pvpKey(vs, m.team);
      const mine = await data.getPrivateCustomItems(projectId, PVP, [key]);
      const side = mine.data.results.length ? mine.data.results[0].value : { at: m.at, p: {} };
      side.p[playerId] = won;
      await data.setPrivateCustomItem(projectId, PVP, { key: key, value: side });
      if (quit || confirmed(await otherSide(vs, m.team), won)) rate("Players", won);
      else if (seen.wait.length < MAX_WAIT) seen.wait.push({ vs: vs, team: m.team, won: won, diff: m.difficulty, at: now });
      else rate(m.difficulty, won);
    } else rate(m.difficulty, won);

    seen.ids.push(id);
    seen.lastAt = Math.max(seen.lastAt, m.at);
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
    for (const d of Object.keys(seen.days)) if (Number(d) < today - 8) delete seen.days[d];
    await data.setProtectedItemBatch(projectId, playerId, { data: [{ key: "bam-progress", value: t }, { key: "bam-matches", value: seen }] });
    await lb.addLeaderboardPlayerScore(projectId, "rating", playerId, { score: t.rating, metadata: { n: name, l: t.level } });
  }
  return { totals: t, done: done, waiting: seen.wait.length };
};

module.exports.params = { matches: { type: "JSON", required: true } };
