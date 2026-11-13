// Cloud Code "ExportTraces": the bot lab downloads the match traces (ReportTrace.js) through the Unity editor's
// BAM > Bots > Download match traces. Only players whose ID is in the private custom item "bam-admins" key "ids" (a list,
// set by hand on the Unity Dashboard: Cloud Save > Custom data) may call it; anyone else gets { ok: false, you: <their ID> }
// so the editor can show which ID to add. Pages of up to PAGE (10) traces; "after" is the cursor the last page gave as "next"
// ("" = from the start, "" back = no more). It walks the old single item "bam-traces" first (traces sent before the change of
// full review 2026-11-13 nr 109), then every day still kept, bucket by bucket, oldest first.
// A download deletes what it fetched (Rickard 2026-11-13: "vi måste inte spara dom i 90 dagar, planen är väl att analysera
// dom kanske omgående"), but only once the editor has saved it: the cursor "<n>|<key>,<key>,..." names the item (n = -1 for
// the old one, else day * BUCKETS + bucket) and the keys of the page just sent, and the editor asks for the next page only
// after writing every file of that one. So the next call deletes those keys, and a page whose files were not written stays
// (the next download sends it again). That is why a page is never the last one: the call after it confirms it and answers
// with no traces. Anything not downloaded goes after KEEP_DAYS (ReportTrace.js). KEEP_DAYS, BUCKETS and dayItem are the same there.
const { DataApi } = require("@unity-services/cloud-save-1.4");
const OLD = "bam-traces", PAGE = 10, KEEP_DAYS = 14, BUCKETS = 4, LOOKS_PER_CALL = 24;
const dayItem = (day, b) => OLD + "-" + day + "-" + b;
const itemOf = n => n < 0 ? OLD : dayItem(Math.floor(n / BUCKETS), n % BUCKETS);
const status = e => (e && e.response && e.response.status) || 0;

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  let ids = [];
  try { const adm = await data.getPrivateCustomItems(projectId, "bam-admins", ["ids"]); if (adm.data.results.length) ids = adm.data.results[0].value; }
  catch (e) { if (status(e) !== 404) throw e; } // no "bam-admins" yet: the editor is told which ID to add, as for any other
  if (!Array.isArray(ids) || !ids.includes(playerId)) return { ok: false, you: playerId, traces: [] };

  const today = Math.floor(Date.now() / 86400000);
  // a day or two before the kept ones too: a day ReportTrace has not got round to deleting yet is still read
  const first = (today - KEEP_DAYS - 2) * BUCKETS, last = (today + 1) * BUCKETS - 1;
  const cur = String(params.after || ""), bar = cur.indexOf("|");
  let n = -1, saved = [];
  if (bar > 0 && Number.isInteger(Number(cur.slice(0, bar)))) {
    n = Number(cur.slice(0, bar));
    saved = cur.slice(bar + 1).split(",").filter(k => /^d\d{6}-[a-z0-9]+$/.test(k)).slice(0, PAGE);
  }
  // the page before is on the editor's disk now: off the server with it
  for (const k of saved) {
    try { await data.deletePrivateCustomItem(k, projectId, itemOf(n)); } catch (e) { if (status(e) !== 404) throw e; }
  }
  let after = saved.length ? saved[saved.length - 1] : "";
  if (n >= 0 && n < first) { n = first; after = ""; } // that day went while the editor was reading

  for (let looks = 0; looks < LOOKS_PER_CALL && n <= last; looks++) {
    const id = itemOf(n);
    let keys = [];
    try {
      const page = after ? await data.getPrivateCustomKeys(projectId, id, after) : await data.getPrivateCustomKeys(projectId, id);
      keys = (page.data.results || []).map(r => r.key).slice(0, PAGE);
    } catch (e) { if (status(e) !== 404) throw e; }
    if (keys.length) {
      const got = await data.getPrivateCustomItems(projectId, id, keys);
      const traces = got.data.results.map(r => ({ key: r.key, p: r.value.p, a: r.value.a, at: r.value.at, t: r.value.t }));
      return { ok: true, traces: traces, next: n + "|" + keys.join(",") };
    }
    n = n < 0 ? first : n + 1; after = "";
  }
  return { ok: true, traces: [], next: n <= last ? n + "|" : "" };
};

module.exports.params = { after: "STRING" };
