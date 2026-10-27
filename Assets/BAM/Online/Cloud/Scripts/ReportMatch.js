// Cloud Code "ReportMatch": the SERVER counts finished matches (Speltest ClaimRating.js + PlayerStats, 2026-10-26).
// The phone sends what happened in each match (won, length, kills, deaths, hero, bot level, when it ended); the server checks
// it, adds XP, Rating and stats with the same formula as Progress.cs, keeps the totals in protected Cloud Save data and puts
// the Rating on the leaderboard "rating". The phone can never send a level, a Rating or a total.
// Matches played offline arrive later in a batch: each is judged by the time it ended, not by when it arrived, so a batch is
// not refused by a cooldown (Speltest's 60 s cooldown refused every queued match but the first).
// Every match id is answered in "done", also the refused ones, so the phone stops sending them.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");

// keep equal to Progress.cs
const MAX_LEVEL = 30, XP_WIN = 100, XP_LOSS = 60, XP_KILL = 5, XP_FIGHT_MAX = 60, XP_FIRST_WIN = 50, RATING_WIN = 20, RATING_LOSS = 8;
const MIN_SECONDS = 45, MAX_SECONDS = 1800, MAX_PER_DAY = 60, MAX_AGE = 7 * 86400, MAX_BATCH = 20, KEEP_IDS = 300;
const HEROES = ["Bram", "Fenn", "Tyra", "Moa", "Knut", "Tikta", "Blast"], LEVELS = ["Easy", "Normal", "Hard"];

const xpToNext = l => 100 + 50 * (l - 1);
function levelOf(xp) { let l = 1; while (l < MAX_LEVEL && xp >= xpToNext(l)) { xp -= xpToNext(l); l++; } return l; }
const fresh = () => ({ xp: 0, level: 1, rating: 0, matches: 0, wins: 0, losses: 0, kills: 0, deaths: 0, streak: 0, bestStreak: 0, seconds: 0, winDay: -1, heroes: [] });
const int = (v, lo, hi) => Number.isInteger(v) && v >= lo && v <= hi;

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context), lb = new LeaderboardsApi(context);
  const got = await data.getProtectedItems(projectId, playerId, ["bam-progress", "bam-matches", "bam-name"]);
  const item = k => (got.data.results.find(r => r.key === k) || {}).value;
  const t = Object.assign(fresh(), item("bam-progress") || {});
  const seen = Object.assign({ ids: [], lastAt: 0, days: {} }, item("bam-matches") || {});
  const name = (item("bam-name") || {}).name || "";
  const now = Math.floor(Date.now() / 1000), today = Math.floor(now / 86400);

  const list = Array.isArray(params.matches) ? params.matches.slice(0, MAX_BATCH) : [];
  list.sort((a, b) => (a.at || 0) - (b.at || 0));
  const done = [], refused = [];
  let counted = 0;
  for (const m of list) {
    const id = typeof m.id === "string" ? m.id.slice(0, 40) : "";
    if (!id) continue;
    done.push(id);
    let why = null;
    if (seen.ids.includes(id)) why = "already counted";
    else if (!int(m.at, now - MAX_AGE, now + 60)) why = "too old or in the future";
    else if (!int(m.seconds, MIN_SECONDS, MAX_SECONDS)) why = "length";
    else if (!int(m.kills, 0, 60) || !int(m.deaths, 0, 60)) why = "numbers";
    else if (!HEROES.includes(m.hero) || !LEVELS.includes(m.difficulty)) why = "hero or bot level";
    else if (m.at - m.seconds < seen.lastAt - 5) why = "overlaps the previous match";
    else if ((seen.days[Math.floor(m.at / 86400)] || 0) >= MAX_PER_DAY) why = "daily cap";
    if (why) { refused.push(id + ": " + why); continue; }

    const day = Math.floor(m.at / 86400), won = m.won === true;
    const firstWin = won && t.winDay !== day;
    t.xp += (won ? XP_WIN : XP_LOSS) + Math.min(XP_FIGHT_MAX, m.kills * XP_KILL) + (firstWin ? XP_FIRST_WIN : 0);
    if (firstWin) t.winDay = day;
    t.rating += won ? RATING_WIN : RATING_LOSS;
    t.matches++;
    if (won) { t.wins++; t.streak++; t.bestStreak = Math.max(t.bestStreak, t.streak); } else { t.losses++; t.streak = 0; }
    t.kills += m.kills; t.deaths += m.deaths; t.seconds += m.seconds;
    t.level = levelOf(t.xp);
    let h = t.heroes.find(x => x.id === m.hero);
    if (!h) { h = { id: m.hero, m: 0, w: 0 }; t.heroes.push(h); }
    h.m++; if (won) h.w++;

    seen.ids.push(id);
    seen.lastAt = Math.max(seen.lastAt, m.at);
    seen.days[day] = (seen.days[day] || 0) + 1;
    counted++;
  }
  if (refused.length) logger.info("ReportMatch " + playerId + " refused " + refused.join("; "));

  if (counted > 0) {
    if (seen.ids.length > KEEP_IDS) seen.ids = seen.ids.slice(seen.ids.length - KEEP_IDS);
    for (const d of Object.keys(seen.days)) if (Number(d) < today - 8) delete seen.days[d];
    await data.setProtectedItemBatch(projectId, playerId, { data: [{ key: "bam-progress", value: t }, { key: "bam-matches", value: seen }] });
    await lb.addLeaderboardPlayerScore(projectId, "rating", playerId, { score: t.rating, metadata: { n: name, l: t.level } });
  }
  return { totals: t, done: done };
};

module.exports.params = { matches: { type: "JSON", required: true } };
