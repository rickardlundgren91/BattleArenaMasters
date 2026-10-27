using BAM.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Game
{
    /// <summary>
    /// The special's aim on the ground while it is dragged, in the MLBB skill-indicator style. Rickard 2026-10-27 ("inte så snyggt
    /// som mlbb när man ska sikta"): the first version filled the whole reach with a pale dome and drew a flat white arrow. Now, as
    /// MLBB and the old game's HudAim.PaintAim: the reach is a thin crisp ring with only a whisper of fill; a line special is one
    /// arrow shape (body and head) with a translucent fill that grows towards the tip, a crisp light outline with a thin dark edge
    /// outside (so it reads on stone and grass alike) and a soft glow inside, and a light that runs along it; an area special is a
    /// soft disc with the same crisp rim, a quiet inner pulse, a dot in the middle and a thin guide from the hero.
    /// The outlines are ribbons of a fixed width on the ground (rebuilt each frame), so they stay sharp whatever the size.
    /// Cancelling dims it all to a quiet grey (old HudAim AIM_CANCEL); a special that is not ready shows red.
    /// The material is a copy of the art's own aim material, so its transparent shader variant is the one already in the build.
    /// </summary>
    sealed class AimIndicator
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap");
        static Texture2D strokeTex, guideTex, fillTex, bandTex, rangeTex, areaTex, dotTex;
        static Mesh quadZ, quadC;

        // the outline ribbons, in world units: the crisp edge and its dark rim reach EdgeOut outside the shape, the glow EdgeIn inside
        const float EdgeOut = 0.035f, EdgeIn = 0.15f;

        readonly GameObject root;
        readonly Part rangeFill, rangeRim, arrowFill, sweep, arrowRim, areaFill, pulse, areaRim, dot, guide;
        readonly Part[] all;
        float sweepT;

        // MLBB's own indicator is a light cyan whatever the hero; the fill a deeper blue under the white-cyan edges
        static readonly Color Ok = new Color(0.72f, 0.94f, 1f, 1f), OkFill = new Color(0.45f, 0.8f, 1f, 1f),
            Bad = new Color(1f, 0.45f, 0.42f, 1f), Grey = new Color(0.78f, 0.78f, 0.82f, 1f);

        sealed class Part
        {
            public Transform T;
            public MeshRenderer R;
            public Material M;
            public Ribbon Rib; // the parts drawn as ribbons build their own mesh every frame
            public Color Shown = new Color(-1f, 0f, 0f, 0f);

            public void SetColor(Color c)
            {
                if (c == Shown) return;
                Shown = c;
                M.SetColor(BaseColor, c);
            }
        }

        public AimIndicator(AimView source)
        {
            MakeAssets();
            Material src = null;
            var rs = source.GetComponentsInChildren<MeshRenderer>(true);
            if (rs.Length > 0) src = rs[0].sharedMaterial;
            root = new GameObject("BAM.AimIndicator");
            rangeFill = MakePart("RangeFill", quadC, rangeTex, src, 3004);
            rangeRim = MakeRibbon("RangeRim", strokeTex, src, 3005, 64);
            guide = MakePart("Guide", quadZ, guideTex, src, 3006);
            areaFill = MakePart("AreaFill", quadC, areaTex, src, 3007);
            arrowFill = MakeRibbon("ArrowFill", fillTex, src, 3007, 8);
            sweep = MakePart("Sweep", quadZ, bandTex, src, 3008);
            pulse = MakeRibbon("Pulse", strokeTex, src, 3008, 48);
            arrowRim = MakeRibbon("ArrowRim", strokeTex, src, 3009, 8);
            areaRim = MakeRibbon("AreaRim", strokeTex, src, 3009, 64);
            dot = MakePart("Dot", quadC, dotTex, src, 3010);
            all = new[] { rangeFill, rangeRim, guide, areaFill, arrowFill, sweep, pulse, arrowRim, areaRim, dot };
            Hide();
        }

        Part MakePart(string name, Mesh mesh, Texture2D tex, Material src, int queue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture(BaseMap, tex);
            mat.mainTexture = tex;
            mat.renderQueue = queue;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return new Part { T = go.transform, R = r, M = mat };
        }

        Part MakeRibbon(string name, Texture2D tex, Material src, int queue, int maxPoints)
        {
            var rib = new Ribbon(name, maxPoints);
            var p = MakePart(name, rib.Mesh, tex, src, queue);
            p.Rib = rib;
            return p;
        }

        public void Destroy()
        {
            if (root == null) return;
            foreach (var p in all)
            {
                Object.Destroy(p.M);
                if (p.Rib != null) Object.Destroy(p.Rib.Mesh);
            }
            Object.Destroy(root);
        }

        public void Hide()
        {
            foreach (var p in all) p.R.enabled = false;
        }

        static Color Edge(bool ready, bool cancel, float a)
        {
            Color c = cancel ? Grey : ready ? Ok : Bad;
            return new Color(c.r, c.g, c.b, cancel ? a * 0.55f : a);
        }

        static Color Fill(bool ready, bool cancel, float a)
        {
            Color c = cancel ? Grey : ready ? OkFill : Bad;
            return new Color(c.r, c.g, c.b, cancel ? a * 0.4f : a);
        }

        /// <summary>The reach of the special: a thin crisp ring round the hero with only a faint fill (MLBB, old HudAim AIM_RANGE).</summary>
        public void Range(Vector3 hero, float reach, bool ready, bool cancel)
        {
            Place(rangeFill, hero, Quaternion.identity, new Vector3(reach * 2f, 1f, reach * 2f), 0.04f);
            rangeFill.SetColor(Fill(ready, cancel, 0.11f));
            CirclePath(reach, 64);
            rangeRim.Rib.Build(pts, 64, true, EdgeOut * 0.8f, EdgeIn * 0.6f);
            Place(rangeRim, hero, Quaternion.identity, Vector3.one, 0.042f);
            rangeRim.SetColor(Edge(ready, cancel, 0.6f));
        }

        /// <summary>An arrow from the hero, length long and width wide (the hit width): one outline round body and head, a fill that
        /// grows towards the tip, and a light that runs along it.</summary>
        public void Line(Vector3 from, Vector3 dir, float length, float width, bool ready, bool cancel, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            width = Mathf.Max(0.3f, width);
            length = Mathf.Max(0.6f, length);
            float headLen = Mathf.Min(length * 0.28f, Mathf.Max(0.5f, width * 0.85f));
            float bodyLen = length - headLen, half = width * 0.5f, wing = Mathf.Max(half + 0.18f, half * 1.6f);

            // the outline in the arrow's own frame (x across to the right, y along), counter-clockwise seen from above
            pts[0] = new Vector2(half, 0f);
            pts[1] = new Vector2(half, bodyLen);
            pts[2] = new Vector2(wing, bodyLen);
            pts[3] = new Vector2(0f, length);
            pts[4] = new Vector2(-wing, bodyLen);
            pts[5] = new Vector2(-half, bodyLen);
            pts[6] = new Vector2(-half, 0f);
            var q = Quaternion.LookRotation(dir);

            arrowFill.Rib.Arrow(half, wing, bodyLen, length);
            Place(arrowFill, from, q, Vector3.one, 0.06f);
            arrowFill.SetColor(Fill(ready, cancel, 0.5f));

            arrowRim.Rib.Build(pts, 7, false, EdgeOut, EdgeIn);
            Place(arrowRim, from, q, Vector3.one, 0.062f);
            arrowRim.SetColor(Edge(ready, cancel, 0.95f));

            // a soft light runs from the hero to the head every 0.9 s (old HudAim: "a slow light running along it")
            sweepT = cancel ? 0f : (sweepT + dt / 0.9f) % 1f;
            float band = Mathf.Min(1.1f, bodyLen * 0.4f);
            float at = Mathf.Lerp(-band * 0.5f, bodyLen - band * 0.5f, sweepT);
            float a0 = Mathf.Max(0f, at), a1 = Mathf.Min(bodyLen, at + band);
            Place(sweep, from + dir * a0, q, new Vector3(Mathf.Max(0.05f, width - 0.06f), 1f, Mathf.Max(0.02f, a1 - a0)), 0.061f);
            sweep.SetColor(Fill(ready, cancel, cancel ? 0f : 0.35f * Mathf.Sin(sweepT * Mathf.PI)));
            sweep.R.enabled = !cancel && a1 > a0 + 0.02f;
        }

        /// <summary>The spot an area special lands on: a soft disc with a crisp rim, a quiet inner pulse, a dot in the middle and a
        /// thin guide from the hero (old HudAim GROUND).</summary>
        public void Circle(Vector3 hero, Vector3 center, float radius, bool ready, bool cancel, float dt)
        {
            radius = Mathf.Max(0.3f, radius);
            Place(areaFill, center, Quaternion.identity, new Vector3(radius * 2f, 1f, radius * 2f), 0.06f);
            areaFill.SetColor(Fill(ready, cancel, 0.34f));

            CirclePath(radius, 64);
            areaRim.Rib.Build(pts, 64, true, EdgeOut, EdgeIn);
            Place(areaRim, center, Quaternion.identity, Vector3.one, 0.063f);
            areaRim.SetColor(Edge(ready, cancel, 0.95f));

            sweepT = cancel ? 0f : (sweepT + dt / 1.1f) % 1f;
            float pr = radius * (0.3f + 0.62f * sweepT);
            CirclePath(pr, 48);
            pulse.Rib.Build(pts, 48, true, EdgeOut * 0.6f, EdgeIn * 0.5f);
            Place(pulse, center, Quaternion.identity, Vector3.one, 0.062f);
            pulse.SetColor(Edge(ready, cancel, 0.45f * (1f - sweepT)));
            pulse.R.enabled = !cancel;

            Place(dot, center, Quaternion.identity, new Vector3(0.2f, 1f, 0.2f), 0.064f);
            dot.SetColor(Edge(ready, cancel, 0.9f));

            // the guide: from the hero's feet to the rim, faint
            Vector3 d = center - hero;
            d.y = 0f;
            float len = d.magnitude - radius;
            if (len > 0.3f)
            {
                Place(guide, hero, Quaternion.LookRotation(d), new Vector3(0.09f, 1f, len), 0.05f);
                guide.SetColor(Edge(ready, cancel, 0.5f));
            }
        }

        static void Place(Part p, Vector3 at, Quaternion q, Vector3 scale, float y)
        {
            p.T.SetPositionAndRotation(new Vector3(at.x, y, at.z), q);
            p.T.localScale = scale;
            p.R.enabled = true;
        }

        static readonly Vector2[] pts = new Vector2[64];

        // a circle of n points, counter-clockwise seen from above (x right, y = world z)
        static void CirclePath(float r, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
        }

        // ---------------------------------------------------------------- a ribbon of fixed width along a path, or the arrow's fill

        sealed class Ribbon
        {
            public readonly Mesh Mesh;
            readonly Vector3[] v;
            readonly Vector2[] uv;
            readonly int[] tri;
            int nv, nt;

            public Ribbon(string name, int maxPoints)
            {
                Mesh = new Mesh { name = "BAM.Aim." + name, hideFlags = HideFlags.DontSave };
                Mesh.MarkDynamic();
                v = new Vector3[(maxPoints + 1) * 2];
                uv = new Vector2[v.Length];
                tri = new int[(maxPoints + 1) * 6];
            }

            /// <summary>A band along the n points (counter-clockwise seen from above): outW wide outside the path, inW inside it;
            /// u runs 0 at the outer edge to 1 at the inner, so one texture draws the dark rim, the crisp edge and the glow.</summary>
            public void Build(Vector2[] p, int n, bool closed, float outW, float inW)
            {
                nv = nt = 0;
                int count = closed ? n + 1 : n;
                for (int k = 0; k < count; k++)
                {
                    int i = k % n;
                    Vector2 here = p[i];
                    bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
                    Vector2 nPrev = hasPrev ? Out(p[(i - 1 + n) % n], here) : Vector2.zero;
                    Vector2 nNext = hasNext ? Out(here, p[(i + 1) % n]) : Vector2.zero;
                    Vector2 nm = (nPrev + nNext).normalized;
                    // a mitred corner, limited so the arrow's sharp tip does not shoot far out
                    float m = 1f / Mathf.Max(0.4f, Vector2.Dot(nm, hasNext ? nNext : nPrev));
                    Vector2 o = here + nm * outW * m, inn = here - nm * inW * m;
                    v[nv] = new Vector3(o.x, 0f, o.y); uv[nv++] = new Vector2(0f, 0.5f);
                    v[nv] = new Vector3(inn.x, 0f, inn.y); uv[nv++] = new Vector2(1f, 0.5f);
                    if (k > 0)
                    {
                        int a = nv - 4; // the previous pair: a outer, a + 1 inner; this pair: a + 2, a + 3
                        Tri(a, a + 2, a + 3);
                        Tri(a, a + 3, a + 1);
                    }
                }
                Apply();
            }

            /// <summary>The arrow's fill: body and head, u across, v along from the hero (0) to the tip (1).</summary>
            public void Arrow(float half, float wing, float bodyLen, float length)
            {
                nv = nt = 0;
                float vb = bodyLen / length;
                Put(half, 0f, 1f, 0f); Put(half, bodyLen, 1f, vb); Put(-half, bodyLen, 0f, vb); Put(-half, 0f, 0f, 0f);
                Tri(0, 1, 2); Tri(0, 2, 3);
                Put(wing, bodyLen, 1f, vb); Put(0f, length, 0.5f, 1f); Put(-wing, bodyLen, 0f, vb);
                Tri(4, 5, 6);
                Apply();
            }

            void Put(float x, float z, float u, float vv)
            {
                v[nv] = new Vector3(x, 0f, z);
                uv[nv++] = new Vector2(u, vv);
            }

            // the outward normal of the segment a -> b on a counter-clockwise path
            static Vector2 Out(Vector2 a, Vector2 b)
            {
                Vector2 d = (b - a).normalized;
                return new Vector2(d.y, -d.x);
            }

            // facing up whichever way the three were given (Unity: clockwise seen from above)
            void Tri(int a, int b, int c)
            {
                Vector3 ab = v[b] - v[a], ac = v[c] - v[a];
                if (ab.z * ac.x - ab.x * ac.z < 0f) { int t = b; b = c; c = t; }
                tri[nt++] = a; tri[nt++] = b; tri[nt++] = c;
            }

            void Apply()
            {
                Mesh.Clear();
                Mesh.SetVertices(v, 0, nv);
                Mesh.SetUVs(0, uv, 0, nv);
                Mesh.SetTriangles(tri, 0, nt, 0);
                Mesh.RecalculateBounds();
            }
        }

        // ---------------------------------------------------------------- the textures and meshes, made once

        static void MakeAssets()
        {
            if (strokeTex != null) return;
            const float total = EdgeOut + EdgeIn;
            // the outline, across from outside (u = 0) to inside (u = 1): a thin dark rim, a crisp bright edge, then a soft glow that
            // fades into the shape. The rim's colour is in the texture, so tinting the material keeps it dark.
            strokeTex = Tex1D("BAM.Aim.Stroke", 64, u =>
            {
                float w = u * total; // world units in from the outer edge
                const float dark = 0.022f, coreEnd = 0.058f;
                if (w < dark) return new Color(0.05f, 0.07f, 0.12f, 0.55f * Mathf.SmoothStep(0f, 1f, w / 0.008f));
                if (w < coreEnd) return Color.white;
                float g = 1f - (w - coreEnd) / (total - coreEnd);
                return new Color(1f, 1f, 1f, 0.42f * g * g);
            });

            // the guide to an area: a light core with a faint dark edge each side
            guideTex = Tex1D("BAM.Aim.Guide", 32, u =>
            {
                float d = Mathf.Abs(u * 2f - 1f);
                if (d > 0.62f) return new Color(0.05f, 0.07f, 0.12f, 0.45f * (1f - Mathf.SmoothStep(0.85f, 1f, d)));
                return Color.white;
            });

            // the arrow's fill: faint at the hero's feet, growing towards the tip, a touch brighter along its sides
            fillTex = NewTex("BAM.Aim.Fill", 32, 64);
            var px = new Color32[32 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 32; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / 16f - 1f), vv = (y + 0.5f) / 64f;
                    float a = (0.28f + 0.5f * vv + 0.22f * u * u * u) * Mathf.SmoothStep(0f, 1f, vv / 0.1f);
                    px[y * 32 + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            fillTex.SetPixels32(px); fillTex.Apply(false, true);

            // the running light: soft at both ends and both sides
            bandTex = NewTex("BAM.Aim.Band", 16, 32);
            px = new Color32[16 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 16; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / 8f - 1f), vv = (y + 0.5f) / 32f, s = Mathf.Sin(vv * Mathf.PI);
                    float a = s * s * (1f - Mathf.SmoothStep(0.7f, 1f, u));
                    px[y * 16 + x] = new Color32(255, 255, 255, (byte)(255f * a));
                }
            bandTex.SetPixels32(px); bandTex.Apply(false, true);

            // the reach: an almost clear disc that only lifts a little near the rim (no dome: Rickard 2026-10-27)
            rangeTex = Disc("BAM.Aim.Range", 128, d => 0.35f + 0.65f * Mathf.Pow(Mathf.SmoothStep(0.7f, 1f, d), 2f));
            // an area's ground: an even soft fill, brighter towards the rim
            areaTex = Disc("BAM.Aim.Area", 128, d => 0.45f + 0.55f * Mathf.SmoothStep(0.4f, 1f, d));
            dotTex = Disc("BAM.Aim.Dot", 32, d => 1f - Mathf.SmoothStep(0.6f, 1f, d));

            quadZ = Quad("BAM.Aim.QuadZ", -0.5f, 0.5f, 0f, 1f);
            quadC = Quad("BAM.Aim.QuadC", -0.5f, 0.5f, -0.5f, 0.5f);
        }

        // a strip w wide and 2 high whose colour runs across u
        static Texture2D Tex1D(string name, int w, System.Func<float, Color> f)
        {
            var t = NewTex(name, w, 2);
            var px = new Color[w * 2];
            for (int x = 0; x < w; x++) px[x] = px[w + x] = f((x + 0.5f) / w);
            t.SetPixels(px); t.Apply(false, true);
            return t;
        }

        // a round texture whose alpha is f(distance from the middle, 0..1), cut softly at the rim
        static Texture2D Disc(string name, int n, System.Func<float, float> f)
        {
            var t = NewTex(name, n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / (n * 0.5f) - 1f, dy = (y + 0.5f) / (n * 0.5f) - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d > 1f ? 0f : f(d) * (1f - Mathf.SmoothStep(0.97f, 1f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        static Texture2D NewTex(string name, int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        static Mesh Quad(string name, float x0, float x1, float z0, float z1)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            m.vertices = new[] { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; // clockwise seen from above
            m.RecalculateBounds();
            return m;
        }
    }
}
