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
    /// Rickard's own pictures (2026-10-27, Resources/BAMArt/Fx/tx_aim_*: white + alpha) take over where they exist: the reach ring
    /// (tx_aim_range), the arrow (tx_aim_arrow: its head keeps its shape, the shaft stretches to the length) and the area's disc with
    /// its rim, four ticks and middle dot (tx_aim_area, its fill turned down to a whisper). The code-drawn shapes stay as the fallback.
    /// </summary>
    sealed class AimIndicator
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap");
        static Texture2D strokeTex, guideTex, fillTex, bandTex, rangeTex, areaTex, dotTex;
        static Texture2D ownRange, ownArrow, ownArea; // Rickard's pictures, null when missing
        // where his pictures draw: the reach ring at 0.735 of tx_aim_range's half width, the area's rim at 0.79 of tx_aim_area's;
        // tx_aim_arrow (508 x 1024, v up): tail 0.04, head's base 0.707, tip 0.941, picture's top 0.97; head 0.414 of the width
        // (Precision AIM-07/02: the ring's peak measured at 0.729; the shaft spans u 0.4075-0.5905 = 0.183 of the width, up to v 0.70)
        const float RangeAt = 0.729f, AreaAt = 0.79f, ArrowTail = 0.04f, ArrowBase = 0.707f, ArrowTip = 0.941f, ArrowTop = 0.97f, ArrowHeadU = 0.414f;
        const float ArrowShaftU = 0.183f, ArrowShaftTop = 0.70f;
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
            rangeFill = MakePart("RangeFill", quadC, ownRange ?? rangeTex, src, 3004);
            rangeRim = MakeRibbon("RangeRim", strokeTex, src, 3005, 64);
            guide = MakePart("Guide", quadZ, guideTex, src, 3006);
            areaFill = MakePart("AreaFill", quadC, ownArea ?? areaTex, src, 3007);
            arrowFill = MakeRibbon("ArrowFill", ownArrow ?? fillTex, src, 3007, 8);
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
            if (ownRange != null)
            {
                // his thin glowing ring is the whole reach
                float d = reach * 2f / RangeAt;
                Place(rangeFill, hero, Quaternion.identity, new Vector3(d, 1f, d), 0.04f);
                rangeFill.SetColor(Edge(ready, cancel, 0.75f));
                return;
            }
            Place(rangeFill, hero, Quaternion.identity, new Vector3(reach * 2f, 1f, reach * 2f), 0.04f);
            rangeFill.SetColor(Fill(ready, cancel, 0.11f));
            CirclePath(reach, 64);
            rangeRim.Rib.Build(pts, 64, true, EdgeOut * 0.8f, EdgeIn * 0.6f);
            Place(rangeRim, hero, Quaternion.identity, Vector3.one, 0.042f);
            rangeRim.SetColor(Edge(ready, cancel, 0.6f));
        }

        /// <summary>An arrow from the hero over what the rules' line covers (Sim AimInfo, Precision program AIM-01/02/06): its middle
        /// travels length (already cut at the first wall), a body is hit within half of that path, the danger goes tip further at the
        /// far end and back behind the hero. The shaft is exactly 2 x half wide from -back to length, the head's point is at
        /// length + tip and its sides pass the shaft's end corners, so the outline is the hit area (the round end as its chord).
        /// alpha dims the whole arrow (Fenn's wolf path, Hilde's push).</summary>
        public void Line(Vector3 from, Vector3 dir, float length, float half, float tip, float back, bool ready, bool cancel, float dt, float alpha = 1f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            half = Mathf.Max(0.02f, half);
            length = Mathf.Max(0.05f, length);
            float z0 = -Mathf.Max(0f, back), end = length + Mathf.Max(0f, tip), wing = Mathf.Max(half + 0.18f, half * 1.4f);
            // the head's sides go through (half, length): head length = wing x tip / half; a flat end (tip 0) keeps a short head inside
            float headLen = tip > 0.01f ? wing * tip / half : wing;
            headLen = Mathf.Min(headLen, (end - z0) * 0.45f);
            float headBase = end - headLen;

            // the outline in the arrow's own frame (x across to the right, y along), counter-clockwise seen from above
            pts[0] = new Vector2(half, z0);
            pts[1] = new Vector2(half, headBase);
            pts[2] = new Vector2(wing, headBase);
            pts[3] = new Vector2(0f, end);
            pts[4] = new Vector2(-wing, headBase);
            pts[5] = new Vector2(-half, headBase);
            pts[6] = new Vector2(-half, z0);
            var q = Quaternion.LookRotation(dir);

            if (ownArrow != null)
            {
                // his arrow: the shaft picture scaled so its shaft is the hit width, stretched to the path; the head on top of its end
                arrowFill.Rib.PictureArrow(2f * half / ArrowShaftU, 2f * wing / ArrowHeadU, z0, length, headBase, headLen);
                Place(arrowFill, from, q, Vector3.one, 0.06f);
                arrowFill.SetColor(Edge(ready, cancel, 0.95f * alpha));
                arrowRim.R.enabled = false;
            }
            else
            {
                arrowFill.Rib.Arrow(half, wing, z0, length, headBase, end);
                Place(arrowFill, from, q, Vector3.one, 0.06f);
                arrowFill.SetColor(Fill(ready, cancel, 0.5f * alpha));

                arrowRim.Rib.Build(pts, 7, false, EdgeOut, EdgeIn);
                Place(arrowRim, from, q, Vector3.one, 0.062f);
                arrowRim.SetColor(Edge(ready, cancel, 0.95f * alpha));
            }

            // a soft light runs from the hero to the head every 0.9 s (old HudAim: "a slow light running along it")
            float bodyLen = length;
            sweepT = cancel ? 0f : (sweepT + dt / 0.9f) % 1f;
            float band = Mathf.Min(1.1f, bodyLen * 0.4f);
            float at = Mathf.Lerp(-band * 0.5f, bodyLen - band * 0.5f, sweepT);
            float a0 = Mathf.Max(0f, at), a1 = Mathf.Min(bodyLen, at + band);
            Place(sweep, from + dir * a0, q, new Vector3(Mathf.Max(0.05f, 2f * half * 0.8f), 1f, Mathf.Max(0.02f, a1 - a0)), 0.061f);
            sweep.SetColor(Fill(ready, cancel, cancel ? 0f : 0.35f * alpha * Mathf.Sin(sweepT * Mathf.PI)));
            sweep.R.enabled = !cancel && a1 > a0 + 0.02f;
        }

        /// <summary>The spot an area special lands on: a soft disc with a crisp rim, a quiet inner pulse, a dot in the middle and a
        /// thin guide from the hero (old HudAim GROUND).</summary>
        public void Circle(Vector3 hero, Vector3 center, float radius, bool ready, bool cancel, float dt)
        {
            radius = Mathf.Max(0.05f, radius);
            if (ownArea != null)
            {
                // his disc carries the fill, rim, ticks and the middle dot itself
                float across = radius * 2f / AreaAt;
                Place(areaFill, center, Quaternion.identity, new Vector3(across, 1f, across), 0.06f);
                areaFill.SetColor(Edge(ready, cancel, 0.95f));
                areaRim.R.enabled = false;
                dot.R.enabled = false;
            }
            else
            {
                Place(areaFill, center, Quaternion.identity, new Vector3(radius * 2f, 1f, radius * 2f), 0.06f);
                areaFill.SetColor(Fill(ready, cancel, 0.34f));
                CirclePath(radius, 64);
                areaRim.Rib.Build(pts, 64, true, EdgeOut, EdgeIn);
                Place(areaRim, center, Quaternion.identity, Vector3.one, 0.063f);
                areaRim.SetColor(Edge(ready, cancel, 0.95f));
                Place(dot, center, Quaternion.identity, new Vector3(0.2f, 1f, 0.2f), 0.064f);
                dot.SetColor(Edge(ready, cancel, 0.9f));
            }

            sweepT = cancel ? 0f : (sweepT + dt / 1.1f) % 1f;
            float pr = radius * (0.3f + 0.62f * sweepT);
            CirclePath(pr, 48);
            pulse.Rib.Build(pts, 48, true, EdgeOut * 0.6f, EdgeIn * 0.5f);
            Place(pulse, center, Quaternion.identity, Vector3.one, 0.062f);
            pulse.SetColor(Edge(ready, cancel, 0.45f * (1f - sweepT)));
            pulse.R.enabled = !cancel;

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

            /// <summary>The arrow's fill: the body from z0 to bodyEnd, the head from headBase to its point at end; u across, v along.</summary>
            public void Arrow(float half, float wing, float z0, float bodyEnd, float headBase, float end)
            {
                nv = nt = 0;
                float span = Mathf.Max(0.01f, end - z0), vb = (bodyEnd - z0) / span, vh = (headBase - z0) / span;
                Put(half, z0, 1f, 0f); Put(half, bodyEnd, 1f, vb); Put(-half, bodyEnd, 0f, vb); Put(-half, z0, 0f, 0f);
                Tri(0, 1, 2); Tri(0, 2, 3);
                Put(wing, headBase, 1f, vh); Put(0f, end, 0.5f, 1f); Put(-wing, headBase, 0f, vh);
                Tri(4, 5, 6);
                Apply();
            }

            /// <summary>Rickard's arrow picture on two quads: the shaft (v tail to just under the head) shaftW wide from z0 to bodyEnd,
            /// and the head (v base to the picture's top) headW wide from headBase, kept in shape so its point is headLen further.</summary>
            public void PictureArrow(float shaftW, float headW, float z0, float bodyEnd, float headBase, float headLen)
            {
                nv = nt = 0;
                float h = shaftW * 0.5f, hh = headW * 0.5f, top = headBase + headLen * (ArrowTop - ArrowBase) / (ArrowTip - ArrowBase);
                Put(-h, z0, 0f, ArrowTail); Put(h, z0, 1f, ArrowTail); Put(h, bodyEnd, 1f, ArrowShaftTop); Put(-h, bodyEnd, 0f, ArrowShaftTop);
                Tri(0, 1, 2); Tri(0, 2, 3);
                Put(-hh, headBase, 0f, ArrowBase); Put(hh, headBase, 1f, ArrowBase); Put(hh, top, 1f, ArrowTop); Put(-hh, top, 0f, ArrowTop);
                Tri(4, 5, 6); Tri(4, 6, 7);
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
            ownRange = Own("tx_aim_range");
            ownArrow = Own("tx_aim_arrow");
            ownArea = Own("tx_aim_area");
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

        static Texture2D Own(string name)
        {
            var t = Resources.Load<Texture2D>("BAMArt/Fx/" + name);
            if (t != null) t.wrapMode = TextureWrapMode.Clamp;
            return t;
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
