using System.Collections.Generic;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Rickard's Meshy models from the old game (Resources/BAMArt/Props/&lt;name&gt;.bytes + &lt;name&gt;.jpg, copied as they were):
    /// the tower, the spectral wolf and props. The .bytes file is the old game's own mesh format (tools/meshy/convert.py):
    /// vertex count, submesh count, flags (1 = normals, 2 = uvs), positions, normals, uvs, then 16-bit indices per submesh.
    /// Each model stands with its bottom at y = 0 and its middle at x = z = 0. Read once, then cached.
    /// </summary>
    static class Props
    {
        const string Folder = "BAMArt/Props/";
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>The model's mesh, or null when the file is missing or broken (the caller keeps its code model).</summary>
        public static Mesh Mesh(string name)
        {
            if (meshes.TryGetValue(name, out Mesh m)) return m;
            m = null;
            var asset = Resources.Load<TextAsset>(Folder + name);
            if (asset != null)
            {
                try { m = Decode(name, asset.bytes); }
                catch (System.Exception e) { Debug.LogWarning("[BAM.Art] Could not read the model " + name + ": " + e.Message); }
                Resources.UnloadAsset(asset);
            }
            meshes[name] = m;
            return m;
        }

        public static Texture2D Texture(string name) => Resources.Load<Texture2D>(Folder + name);

        static readonly Dictionary<string, Mesh> sheets = new Dictionary<string, Mesh>();

        /// <summary>A cloak made as a thick shell (Tripo: an outer and an inner sheet about a quarter of its length apart, which showed
        /// as two cloaks, Rickard 2026-11-18: "fenns kappa är dubbel") pressed into one thin sheet just inside its outer face: each
        /// vertex's depth (z, out from the back) is measured against the outer and inner sheets near it (their faces' normals point
        /// +z and -z), and the gap between them kept at share keep. Its outline, its folds across and down, and its UVs stay. null
        /// when the mesh can't be read or has no two sheets.</summary>
        public static Mesh Sheet(string name, Mesh src, float keep)
        {
            string key = name + "|" + keep;
            if (sheets.TryGetValue(key, out Mesh done)) return done;
            Mesh m = src != null && src.isReadable ? MakeSheet(src, keep) : null;
            sheets[key] = m;
            return m;
        }

        static Mesh MakeSheet(Mesh src, float keep)
        {
            Vector3[] v = src.vertices, n = src.normals;
            if (v.Length == 0 || n == null || n.Length != v.Length) return null;
            float sig = 0.06f * Mathf.Max(src.bounds.size.x, src.bounds.size.y), inv2 = 1f / (2f * sig * sig), cut = 9f * sig * sig;
            var o = new Vector3[v.Length];
            int moved = 0;
            for (int i = 0; i < v.Length; i++)
            {
                float wo = 0f, zo = 0f, wi = 0f, zi = 0f;
                for (int j = 0; j < v.Length; j++)
                {
                    float dx = v[j].x - v[i].x, dy = v[j].y - v[i].y, d2 = dx * dx + dy * dy;
                    if (d2 > cut) continue;
                    float w = Mathf.Exp(-d2 * inv2);
                    if (n[j].z > 0.2f) { wo += w; zo += w * v[j].z; } else if (n[j].z < -0.2f) { wi += w; zi += w * v[j].z; }
                }
                o[i] = v[i];
                if (wo < 1e-3f || wi < 1e-3f) continue;
                zo /= wo; zi /= wi;
                float mid = (zo + zi) * 0.5f, at = zo - 0.5f * keep * (zo - zi);
                o[i].z = at + (v[i].z - mid) * keep;
                moved++;
            }
            if (moved < v.Length / 2) return null; // not a two-sheet shell
            var m = Object.Instantiate(src);
            m.name = src.name + " (one sheet)";
            m.vertices = o;
            m.RecalculateBounds();
            return m;
        }

        static readonly Dictionary<string, Mesh> shortHandles = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, float> shortGuards = new Dictionary<string, float>();

        /// <summary>A sword with a smaller handle (Rickard 2026-11-18, Fenn: "för stort handtag till svärdet"): the handle and pommel
        /// below the crossguard pushed together to share k of their length and thin times as thick about the handle's middle; the
        /// guard and blade as they were. The guard is found on the mesh (the widest slice in its lower half, down to where it is half
        /// as wide), so the skins' swords get the same. guard: where the guard began on src, as a share of src's height. null (and
        /// nothing changed) when no guard stands out, or the mesh can't be read.</summary>
        public static Mesh ShortHandle(string name, Mesh src, float k, float thin, out float guard)
        {
            string key = name + "|" + k + "|" + thin;
            guard = 0f;
            if (shortHandles.TryGetValue(key, out Mesh done)) { if (done != null) guard = shortGuards[key]; return done; }
            Mesh m = src != null && src.isReadable ? MakeShortHandle(src, k, thin, out guard) : null;
            shortHandles[key] = m;
            shortGuards[key] = guard;
            return m;
        }

        static Mesh MakeShortHandle(Mesh src, float k, float thin, out float guard)
        {
            guard = 0f;
            Vector3[] v = src.vertices;
            float y0 = src.bounds.min.y, H = src.bounds.size.y;
            if (v.Length == 0 || !(H > 1e-5f)) return null;
            const int Bins = 40; // over the lower half
            var lo = new Vector2[Bins]; var hi = new Vector2[Bins]; var any = new bool[Bins];
            for (int i = 0; i < v.Length; i++)
            {
                int b = Mathf.FloorToInt((v[i].y - y0) / (0.5f * H) * Bins);
                if (b < 0 || b >= Bins) continue;
                var p = new Vector2(v[i].x, v[i].z);
                if (!any[b]) { any[b] = true; lo[b] = hi[b] = p; } else { lo[b] = Vector2.Min(lo[b], p); hi[b] = Vector2.Max(hi[b], p); }
            }
            var w = new float[Bins];
            int top = -1;
            for (int b = 0; b < Bins; b++) { if (!any[b]) continue; Vector2 d = hi[b] - lo[b]; w[b] = Mathf.Max(d.x, d.y); if (top < 0 || w[b] > w[top]) top = b; }
            if (top <= 2) return null;
            var below = new List<float>();
            for (int b = 0; b < top; b++) if (any[b]) below.Add(w[b]);
            below.Sort();
            if (below.Count == 0 || w[top] < 2.2f * below[below.Count / 2]) return null; // no crossguard: leave it
            int g = top;
            while (g > 0 && (!any[g - 1] || w[g - 1] > 0.5f * w[top])) g--;
            float gy = y0 + g * 0.5f * H / Bins; // the guard's underside
            // the handle's own middle (its axis), from the part between pommel and guard
            Vector2 cLo = new Vector2(float.MaxValue, float.MaxValue), cHi = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < v.Length; i++)
            {
                float t = (v[i].y - y0) / (gy - y0);
                if (t < 0.4f || t > 0.9f) continue;
                var p = new Vector2(v[i].x, v[i].z); cLo = Vector2.Min(cLo, p); cHi = Vector2.Max(cHi, p);
            }
            if (cLo.x > cHi.x) return null;
            Vector2 c = (cLo + cHi) * 0.5f;
            float shift = (gy - y0) * (1f - k), fade = 0.04f * H;
            Vector3[] n = src.normals;
            bool hasN = n != null && n.Length == v.Length;
            var nv = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                if (p.y >= gy) { nv[i] = new Vector3(p.x, p.y - shift, p.z); continue; }
                float u = Mathf.Clamp01((gy - p.y) / fade); u = u * u * (3f - 2f * u); // thin below the guard, eased in
                float s = Mathf.Lerp(1f, thin, u);
                nv[i] = new Vector3(c.x + (p.x - c.x) * s, y0 + (p.y - y0) * k, c.y + (p.z - c.y) * s);
                if (hasN) n[i] = new Vector3(n[i].x / s, n[i].y / k, n[i].z / s).normalized; // normals by the inverse scale
            }
            var m = new Mesh { name = src.name + " (short handle)" };
            m.vertices = nv;
            if (hasN) m.normals = n;
            m.uv = src.uv;
            m.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) m.SetTriangles(src.GetTriangles(s), s);
            if (!hasN) m.RecalculateNormals();
            m.RecalculateBounds();
            guard = (gy - y0) / H;
            return m;
        }

        static Mesh Decode(string name, byte[] d)
        {
            int nv = System.BitConverter.ToInt32(d, 0), ns = System.BitConverter.ToInt32(d, 4), flags = System.BitConverter.ToInt32(d, 8);
            int o = 12;
            var v = new Vector3[nv];
            for (int i = 0; i < nv; i++, o += 12) v[i] = new Vector3(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4), System.BitConverter.ToSingle(d, o + 8));
            var mesh = new Mesh { name = name };
            mesh.vertices = v;
            if ((flags & 1) != 0)
            {
                var n = new Vector3[nv];
                for (int i = 0; i < nv; i++, o += 12) n[i] = new Vector3(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4), System.BitConverter.ToSingle(d, o + 8));
                mesh.normals = n;
            }
            if ((flags & 2) != 0)
            {
                var uv = new Vector2[nv];
                for (int i = 0; i < nv; i++, o += 8) uv[i] = new Vector2(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4));
                mesh.uv = uv;
            }
            // one submesh is enough for one texture: join them
            var all = new List<int>();
            for (int s = 0; s < ns; s++)
            {
                int count = System.BitConverter.ToInt32(d, o); o += 4;
                for (int i = 0; i < count; i++, o += 2) all.Add(d[o] | (d[o + 1] << 8));
            }
            mesh.SetTriangles(all, 0);
            if ((flags & 1) == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
