// Cloud Code "ReportTrace": a match trace from the phone (Sim/MatchTrace.cs), so the bots can learn from real players
// (Rickard 2026-11-09 "bottarna borde kunna lära sig mycket ... för att spela mer som riktiga spelare och för att kunna spela
// bättre MED riktiga spelare"). A trace is the match's config (no names) and every seat's controls, gzipped, a few kB.
// Kept in PRIVATE custom Cloud Save data "bam-traces", one key per trace ("d" + day + random), WITHOUT the player's ID, so the
// traces are not tied to a person. A player can send MAX_PER_DAY a day (counted in its protected "bam-trace-day"). Traces older
// than KEEP_DAYS are deleted, a few per call, by anyone's call (the keys start with the day, so the oldest come first).
// Read them with ExportTraces.js (bot lab), never on a phone.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const TRACES = "bam-traces", MAX_PER_DAY = 30, MAX_B64 = 140000, KEEP_DAYS = 90, CLEAN_PER_CALL = 5;

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

  const key = "d" + String(today).padStart(6, "0") + "-" + Math.random().toString(36).slice(2, 12);
  await data.setPrivateCustomItem(projectId, TRACES, { key: key, value: { p: protocol, a: app, at: now, t: trace } });

  try {
    const first = await data.getPrivateCustomKeys(projectId, TRACES);
    let left = CLEAN_PER_CALL;
    for (const r of (first.data.results || [])) {
      if (left <= 0 || r.key[0] !== "d" || Number(r.key.slice(1, 7)) >= today - KEEP_DAYS) break;
      await data.deletePrivateCustomItem(r.key, projectId, TRACES); left--;
    }
  } catch (e) { }
  return { ok: true };
};

module.exports.params = { trace: { type: "STRING", required: true }, protocol: "NUMERIC", app: "STRING", name: "STRING" };
