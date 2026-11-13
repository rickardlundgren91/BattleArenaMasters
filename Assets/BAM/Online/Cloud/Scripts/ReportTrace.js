// Cloud Code "ReportTrace": a match trace from the phone (Sim/MatchTrace.cs), so the bots can learn from real players
// (Rickard 2026-11-09 "bottarna borde kunna lära sig mycket ... för att spela mer som riktiga spelare och för att kunna spela
// bättre MED riktiga spelare"). A trace is the match's config (no names) and every seat's controls, gzipped, a few kB.
// Kept in PRIVATE custom Cloud Save data WITHOUT the player's ID, so the traces are not tied to a person: one custom item per
// day and bucket, "bam-traces-<day>-<bucket>" (dayItem), one key per trace ("d" + day + random). Cloud Save takes at most
// 5 MB and 2000 keys per custom item, so the old single item "bam-traces" filled within weeks and then refused every new
// trace, before its clean-up could run (full review 2026-11-13 nr 109). A day's BUCKETS hold 20 MB; a full bucket tries the
// next, and with all full the trace is dropped. A player can send MAX_PER_DAY a day (counted in its protected "bam-trace-day").
// Traces are kept KEEP_DAYS days (Rickard 2026-11-13: "vi måste inte spara dom i 90 dagar, planen är väl att analysera dom
// kanske omgående"): every call FIRST deletes what is older (clean), so a full store can never stop it.
// Read them with ExportTraces.js (bot lab), never on a phone. KEEP_DAYS, BUCKETS and dayItem are the same there.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const OLD = "bam-traces", META = "bam-trace-meta", MAX_PER_DAY = 30, MAX_B64 = 140000, KEEP_DAYS = 14, BUCKETS = 4;
const OLD_PER_CALL = 6, WIPES_PER_CALL = 8;
const dayItem = (day, b) => OLD + "-" + day + "-" + b;
const status = e => (e && e.response && e.response.status) || 0;

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const trace = String(params.trace || ""), protocol = Number(params.protocol) | 0, app = String(params.app || "").slice(0, 16);
  if (trace.length < 40 || trace.length > MAX_B64 || !/^[A-Za-z0-9+/=]+$/.test(trace)) return { ok: true }; // taken (dropped): the phone stops sending it
  const now = Math.floor(Date.now() / 1000), today = Math.floor(now / 86400);

  const mine = await data.getProtectedItems(projectId, playerId, ["bam-trace-day"]);
  const c = mine.data.results.length ? mine.data.results[0].value : { day: today, n: 0 };
  if (c.day !== today) { c.day = today; c.n = 0; }
  if (c.n >= MAX_PER_DAY) return { ok: true }; // over the day's limit: dropped, not kept on the phone
  c.n++;
  await data.setProtectedItem(projectId, playerId, { key: "bam-trace-day", value: c });

  try { await clean(data, projectId, today); } catch (e) { logger.warning("ReportTrace clean: " + e.message); }

  const key = "d" + String(today).padStart(6, "0") + "-" + Math.random().toString(36).slice(2, 12);
  const value = { p: protocol, a: app, at: now, t: trace };
  const b0 = Math.floor(Math.random() * BUCKETS);
  for (let i = 0; i < BUCKETS; i++) {
    try { await data.setPrivateCustomItem(projectId, dayItem(today, (b0 + i) % BUCKETS), { key: key, value: value }); return { ok: true }; }
    catch (e) { if (i === BUCKETS - 1) logger.warning("ReportTrace: every bucket of day " + today + " refused the trace: " + e.message); }
  }
  return { ok: true }; // dropped: a trace is a sample, not a record
};

// Deletes what is older than KEEP_DAYS. The private custom item META, key "clean", holds { day, old }: every day before day is
// gone, and old = the old single item is empty. A day goes whole (deletePrivateCustomItems on each bucket, at most
// WIPES_PER_CALL a call; "not found" counts as gone, any other failure leaves the day for the next call). The old item's keys
// start with the day, so its oldest come first; up to OLD_PER_CALL of them go a call.
async function clean(data, projectId, today) {
  const keepFrom = today - KEEP_DAYS;
  let was = null;
  try { const got = await data.getPrivateCustomItems(projectId, META, ["clean"]); if (got.data.results.length) was = got.data.results[0].value; }
  catch (e) { if (status(e) !== 404) throw e; }
  const m = { day: was && Number.isFinite(was.day) ? Math.min(was.day, keepFrom) : keepFrom, old: !!(was && was.old) };

  if (!m.old) {
    try {
      const r = await data.getPrivateCustomKeys(projectId, OLD);
      const keys = (r.data.results || []).map(x => x.key);
      if (!keys.length) m.old = true;
      let left = OLD_PER_CALL;
      for (const k of keys) {
        if (left-- <= 0 || (k[0] === "d" && Number(k.slice(1, 7)) >= keepFrom)) break;
        try { await data.deletePrivateCustomItem(k, projectId, OLD); } catch (e) { if (status(e) !== 404) throw e; }
      }
    } catch (e) { if (status(e) === 404) m.old = true; }
  }

  let wipes = WIPES_PER_CALL;
  while (m.day < keepFrom && wipes >= BUCKETS) {
    let gone = true;
    for (let b = 0; b < BUCKETS; b++, wipes--) {
      try { await data.deletePrivateCustomItems(projectId, dayItem(m.day, b)); } catch (e) { if (status(e) !== 404) gone = false; }
    }
    if (!gone) break;
    m.day++;
  }
  if (!was || was.day !== m.day || !!was.old !== m.old) await data.setPrivateCustomItem(projectId, META, { key: "clean", value: m });
}

module.exports.params = { trace: { type: "STRING", required: true }, protocol: "NUMERIC", app: "STRING", name: "STRING" };
