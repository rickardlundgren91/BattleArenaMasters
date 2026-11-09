// Cloud Code "ExportTraces": the bot lab reads the match traces (ReportTrace.js) through the Unity editor's
// BAM > Bots > Download match traces. Only players whose ID is in the private custom item "bam-admins" key "ids" (a list,
// set by hand on the Unity Dashboard: Cloud Save > Custom data) may call it; anyone else gets { ok: false, you: <their ID> }
// so the editor can show which ID to add. Pages of up to PAGE (10) traces after the key "after".
const { DataApi } = require("@unity-services/cloud-save-1.4");
const TRACES = "bam-traces", PAGE = 10;

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId } = context;
  const data = new DataApi(context);
  const adm = await data.getPrivateCustomItems(projectId, "bam-admins", ["ids"]);
  const ids = adm.data.results.length ? adm.data.results[0].value : [];
  if (!Array.isArray(ids) || !ids.includes(playerId)) return { ok: false, you: playerId, traces: [] };
  const after = String(params.after || "");
  const page = after ? await data.getPrivateCustomKeys(projectId, TRACES, after) : await data.getPrivateCustomKeys(projectId, TRACES);
  const keys = (page.data.results || []).map(r => r.key).slice(0, PAGE);
  if (!keys.length) return { ok: true, traces: [], next: "" };
  const got = await data.getPrivateCustomItems(projectId, TRACES, keys);
  const traces = got.data.results.map(r => ({ key: r.key, p: r.value.p, a: r.value.a, at: r.value.at, t: r.value.t }));
  return { ok: true, traces: traces, next: keys.length >= PAGE ? keys[keys.length - 1] : "" };
};

module.exports.params = { after: "STRING" };
