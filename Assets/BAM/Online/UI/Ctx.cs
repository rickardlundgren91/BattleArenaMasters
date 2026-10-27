using System;
using System.Collections.Generic;
using UnityEngine;

// Copied from Speltest ABC2/Game/Ctx.cs (2026-10-26) so the old online pages (MenuHome/MenuSocial) can be ported line by line and
// look exactly the same. Changes: namespace, Img loads from Resources/BAM/Menu (no IconHalves/ImgSizes), fonts from BAM/Fonts.
// A small HTML canvas 2D context drawn with GL immediate mode, so the HUD and menu drawing in game.js can be ported almost line by line.
// Coordinates are CSS pixels of the game frame (W x H, like the original); Begin() maps them onto the screen.
// Supports paths (lines, arcs, ellipses, curves, rounded rects), fill/stroke with soft edges, linear gradients, convex clips
// (circles and rounded rects), images and text (Lilita One or the system's Trebuchet MS), and save/restore with transforms.
namespace BAM.Online.UI
{
    public class Grad
    {
        public bool radial; public float x0, y0, x1, y1, r0, r1;
        public readonly List<float> offs = new List<float>(); public readonly List<Color> cols = new List<Color>();
        public Grad addColorStop(float o, object c)
        {
            Color col = c is Color cc ? cc : Ctx.ParseColor(c as string);
            int i = 0; while (i < offs.Count && offs[i] <= o) i++;
            offs.Insert(i, o); cols.Insert(i, col); return this;
        }
        public Color At(float t)
        {
            if (offs.Count == 0) return Color.clear;
            if (t <= offs[0]) return cols[0];
            for (int i = 1; i < offs.Count; i++) if (t <= offs[i]) { float k = (t - offs[i - 1]) / Math.Max(1e-6f, offs[i] - offs[i - 1]); return Color.Lerp(cols[i - 1], cols[i], k); }
            return cols[cols.Count - 1];
        }
    }

    // an image with the size it has in the original files (Unity may have scaled the texture when importing it)
    public class Img
    {
        public Texture tex; public float w, h;
        public bool ok => tex != null;
        static readonly Dictionary<string, Img> cache = new Dictionary<string, Img>();
        public static Img Get(string name)
        {
            if (cache.TryGetValue(name, out var im)) return im;
            var t = Resources.Load<Texture2D>("BAM/Menu/" + name);
            im = new Img { tex = t };
            if (t != null) { im.w = t.width; im.h = t.height; }
            return cache[name] = im;
        }
        public static Img Of(Texture t) => new Img { tex = t, w = t != null ? t.width : 1, h = t != null ? t.height : 1 };
    }

    public class Ctx
    {
        // ---------- state ----------
        struct Paint { public Color c; public Grad g; }
        class State
        {
            public float a = 1, b, c, d = 1, e, f; public float alpha = 1;
            public Paint fill = new Paint { c = Color.black }, stroke = new Paint { c = Color.black };
            public float lineWidth = 1; public string font = "10px sans-serif", align = "start", baseline = "alphabetic", cap = "butt", join = "miter";
            public float[] dash; public List<Vector2> clip; public bool add, erase;
            public void CopyFrom(State o)
            {
                a = o.a; b = o.b; c = o.c; d = o.d; e = o.e; f = o.f; alpha = o.alpha; fill = o.fill; stroke = o.stroke; lineWidth = o.lineWidth;
                font = o.font; align = o.align; baseline = o.baseline; cap = o.cap; join = o.join; dash = o.dash; clip = o.clip; add = o.add; erase = o.erase;
            }
        }
        State st = new State();
        readonly Stack<State> stack = new Stack<State>();
        // saved states are reused (save/restore runs hundreds of times a frame), so no garbage per call
        readonly Stack<State> statePool = new Stack<State>();
        static readonly State defaultState = new State();
        State NewState(State from) { var s = statePool.Count > 0 ? statePool.Pop() : new State(); s.CopyFrom(from); return s; }

        public float globalAlpha { get => st.alpha; set => st.alpha = value; }
        public float lineWidth { get => st.lineWidth; set => st.lineWidth = value; }
        public string font { get => st.font; set => st.font = value; }
        public string textAlign { get => st.align; set => st.align = value; }
        public string textBaseline { get => st.baseline; set => st.baseline = value; }
        public string lineCap { get => st.cap; set => st.cap = value; }
        public string lineJoin { get => st.join; set => st.join = value; }
        public string globalCompositeOperation { get => st.erase ? "destination-out" : st.add ? "lighter" : "source-over"; set { st.add = value == "lighter"; st.erase = value == "destination-out"; } }
        public object fillStyle { get => st.fill.g != null ? (object)st.fill.g : st.fill.c; set => st.fill = ToPaint(value); }
        public object strokeStyle { get => st.stroke.g != null ? (object)st.stroke.g : st.stroke.c; set => st.stroke = ToPaint(value); }
        public void setLineDash(float[] d) { st.dash = d != null && d.Length > 0 ? d : null; }

        static Paint ToPaint(object v)
        {
            if (v is Grad g) return new Paint { g = g };
            if (v is Color c) return new Paint { c = c };
            return new Paint { c = ParseColor(v as string) };
        }

        // ---------- colours ----------
        static readonly Dictionary<string, Color> colorCache = new Dictionary<string, Color>();
        public static Color RGBA(float r, float g, float b, float a = 1) => new Color(r / 255f, g / 255f, b / 255f, a);
        public static Color ParseColor(string s)
        {
            if (string.IsNullOrEmpty(s)) return Color.black;
            if (colorCache.TryGetValue(s, out var c)) return c;
            c = Color.black; string t = s.Trim();
            try
            {
                if (t[0] == '#')
                {
                    string h = t.Substring(1);
                    if (h.Length == 3 || h.Length == 4) h = string.Concat(Array.ConvertAll(h.ToCharArray(), ch => new string(ch, 2)));
                    uint n = Convert.ToUInt32(h, 16);
                    if (h.Length == 8) c = new Color((n >> 24 & 255) / 255f, (n >> 16 & 255) / 255f, (n >> 8 & 255) / 255f, (n & 255) / 255f);
                    else c = new Color((n >> 16 & 255) / 255f, (n >> 8 & 255) / 255f, (n & 255) / 255f, 1);
                }
                else if (t.StartsWith("rgb"))
                {
                    int i0 = t.IndexOf('('), i1 = t.IndexOf(')'); var p = t.Substring(i0 + 1, i1 - i0 - 1).Split(',');
                    Func<int, float> F = i => float.Parse(p[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    c = new Color(F(0) / 255f, F(1) / 255f, F(2) / 255f, p.Length > 3 ? F(3) : 1);
                }
                else if (t == "white") c = Color.white;
                else if (t == "transparent") c = Color.clear;
            }
            catch (Exception) { c = Color.magenta; }
            if (colorCache.Count > 4000) colorCache.Clear();
            colorCache[s] = c; return c;
        }

        // ---------- transform ----------
        float baseA = 1, baseE, baseF;
        public void save() { stack.Push(NewState(st)); }
        public void restore() { if (stack.Count > 0) { statePool.Push(st); st = stack.Pop(); } }
        void ResetState() { while (stack.Count > 0) statePool.Push(stack.Pop()); st.CopyFrom(defaultState); }
        public void translate(float x, float y) { st.e += st.a * x + st.c * y; st.f += st.b * x + st.d * y; }
        public void scale(float x, float y) { st.a *= x; st.b *= x; st.c *= y; st.d *= y; }
        public void rotate(float r)
        {
            float cs = (float)Math.Cos(r), sn = (float)Math.Sin(r);
            float a = st.a * cs + st.c * sn, b = st.b * cs + st.d * sn, c = st.a * -sn + st.c * cs, d = st.b * -sn + st.d * cs;
            st.a = a; st.b = b; st.c = c; st.d = d;
        }
        public void resetTransform() { st.a = baseA; st.b = 0; st.c = 0; st.d = baseA; st.e = baseE; st.f = baseF; }
        Vector2 T(float x, float y) => new Vector2(st.a * x + st.c * y + st.e, st.b * x + st.d * y + st.f);
        float Scale => (float)Math.Sqrt(Math.Abs(st.a * st.d - st.b * st.c));

        // ---------- frame ----------
        Material mat, fontMat, addMat, eraseMat; Texture2D white; bool linear;
        Material BlendMat() => st.erase ? eraseMat : st.add ? addMat : mat;
        public float W, H, DPR = 1, OX, OY;
        public Ctx(Material uiMaterial)
        {
            white = Texture2D.whiteTexture;
            mat = uiMaterial != null ? new Material(uiMaterial) : new Material(Shader.Find("UI/Default"));
            fontMat = new Material(mat); addMat = new Material(mat);
            // _TextureSampleAdd is not in UI/Default's Properties, so HasProperty says no and it was never set. Then the glyphs took
            // whatever UGUI last left in the global of that name: (0,0,0,0) after an image, so text came out black on BAM's
            // menu (2026-10-27). Set it on every material: (1,1,1,0) makes the font's alpha-only texture white, 0 keeps images as they are.
            fontMat.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
            mat.SetVector("_TextureSampleAdd", Vector4.zero); addMat.SetVector("_TextureSampleAdd", Vector4.zero);
            if (addMat.HasProperty("_DstBlend")) addMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            eraseMat = new Material(mat); eraseMat.SetVector("_TextureSampleAdd", Vector4.zero);
            if (eraseMat.HasProperty("_SrcBlend")) eraseMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            LoadFonts();
        }
        // ox, oy: top-left of the game frame in screen pixels (top-down); w, h: frame size in CSS pixels; dpr: screen pixels per CSS pixel
        public void Begin(float ox, float oy, float w, float h, float dpr)
        {
            W = w; H = h; DPR = dpr; OX = ox; OY = oy; baseA = dpr; baseE = ox; baseF = oy; toRT = false; meshUsed = 0;
            ResetState(); resetTransform();
            GL.PushMatrix(); GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            curTex = null; curMat = null; verts.Clear();
        }
        public void End() { Flush(); GL.PopMatrix(); }
        // draw into a texture instead of the screen (the 3D view's particle sprites); size in texture pixels
        public void Begin(RenderTexture rt)
        {
            W = rt.width; H = rt.height; DPR = 1; OX = OY = 0; baseA = 1; baseE = 0; baseF = 0; toRT = true; meshUsed = 0;
            ResetState(); resetTransform();
            RenderTexture.active = rt; GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix(); GL.LoadPixelMatrix(0, rt.width, rt.height, 0);
            curTex = null; curMat = null; verts.Clear();
        }

        // ---------- batching ----------
        struct V { public float x, y, u, v; public Color c; }
        readonly List<V> verts = new List<V>(4096);
        Texture curTex; Material curMat;
        void Use(Texture t, Material m) { if (t == curTex && m == curMat) return; Flush(); curTex = t; curMat = m; }
        // Each batch goes to the GPU as one mesh (Graphics.DrawMeshNow, which uses the same GL matrices) instead of three GL calls per vertex.
        // If the HUD or menu ever comes out wrong on a device, set meshDraw = false to get the old GL.Begin path back.
        public static bool meshDraw = true;
        readonly List<Mesh> meshPool = new List<Mesh>(); int meshUsed;
        readonly List<Vector3> mPos = new List<Vector3>(4096); readonly List<Color> mCol = new List<Color>(4096); readonly List<Vector2> mUv = new List<Vector2>(4096);
        static int[] seqIdx = new int[0];
        void Flush()
        {
            if (verts.Count == 0 || curMat == null) { verts.Clear(); return; }
            curMat.mainTexture = curTex; curMat.SetPass(0);
            int n = verts.Count;
            if (meshDraw)
            {
                if (seqIdx.Length < n) { seqIdx = new int[Math.Max(n, seqIdx.Length * 2)]; for (int i = 0; i < seqIdx.Length; i++) seqIdx[i] = i; }
                mPos.Clear(); mCol.Clear(); mUv.Clear();
                for (int i = 0; i < n; i++) { var v = verts[i]; mPos.Add(new Vector3(v.x, v.y, 0)); mCol.Add(v.c); mUv.Add(new Vector2(v.u, v.v)); }
                // a mesh per batch in this frame (a mesh is not changed again until the next frame)
                if (meshUsed == meshPool.Count) { var nm = new Mesh { name = "ctx" }; nm.MarkDynamic(); meshPool.Add(nm); }
                var m = meshPool[meshUsed++];
                m.Clear(); m.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                m.SetVertices(mPos); m.SetColors(mCol); m.SetUVs(0, mUv);
                m.SetIndices(seqIdx, 0, n, MeshTopology.Triangles, 0, false);
                Graphics.DrawMeshNow(m, Matrix4x4.identity);
            }
            else
            {
                GL.Begin(GL.TRIANGLES);
                for (int i = 0; i < n; i++) { var v = verts[i]; GL.Color(v.c); GL.TexCoord2(v.u, v.v); GL.Vertex3(v.x, v.y, 0); }
                GL.End();
            }
            verts.Clear();
        }
        // most vertices in a row share one colour, so remember the last conversion (three pow() calls per vertex otherwise)
        float linR = -1, linG, linB; Color linOut;
        Color Lin(Color c)
        {
            if (!linear) return c;
            if (c.r != linR || c.g != linG || c.b != linB) { linR = c.r; linG = c.g; linB = c.b; linOut = new Color(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b)); }
            linOut.a = c.a; return linOut;
        }

        // a vertex before colouring: position (screen px), uv, coverage (soft edge)
        struct PV { public float x, y, u, v, k; public PV(float x, float y, float u, float v, float k) { this.x = x; this.y = y; this.u = u; this.v = v; this.k = k; } }
        Paint curPaint; float gx, gy, gdx, gdy, gl2; bool imagePaint;
        void SetPaint(Paint p, bool image = false)
        {
            curPaint = p; imagePaint = image;
            if (p.g != null && !p.g.radial)
            {
                var a = T(p.g.x0, p.g.y0); var b = T(p.g.x1, p.g.y1);
                gx = a.x; gy = a.y; gdx = b.x - a.x; gdy = b.y - a.y; gl2 = Math.Max(1e-6f, gdx * gdx + gdy * gdy);
            }
            else if (p.g != null) { var a = T(p.g.x0, p.g.y0); gx = a.x; gy = a.y; gl2 = Math.Max(1e-6f, p.g.r1 * Scale); }
        }
        float GradT(float x, float y) => curPaint.g.radial ? ((float)Math.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy)) / gl2 - curPaint.g.r0 / Math.Max(1e-6f, curPaint.g.r1)) / Math.Max(1e-6f, 1 - curPaint.g.r0 / Math.Max(1e-6f, curPaint.g.r1)) : ((x - gx) * gdx + (y - gy) * gdy) / gl2;
        Color PaintAt(float x, float y)
        {
            if (imagePaint) return Color.white;
            return curPaint.g != null ? curPaint.g.At(GradT(x, y)) : curPaint.c;
        }
        void Push(PV p)
        {
            var c = PaintAt(p.x, p.y); c.a *= p.k * st.alpha;
            verts.Add(new V { x = p.x, y = p.y, u = p.u, v = p.v, c = Lin(c) });
        }
        readonly List<PV> polyA = new List<PV>(32), polyB = new List<PV>(32);
        void Tri(PV a, PV b, PV c)
        {
            bool grad = curPaint.g != null && !curPaint.g.radial && !imagePaint;
            if (!grad && st.clip == null) { Push(a); Push(b); Push(c); return; }
            if (!grad) { polyA.Clear(); polyA.Add(a); polyA.Add(b); polyA.Add(c); ClipAndEmit(polyA); return; }
            // linear gradient: cut the triangle into slabs between colour stops so the colours come out right
            var g = curPaint.g; float ta = GradT(a.x, a.y), tb = GradT(b.x, b.y), tc = GradT(c.x, c.y);
            float lo = Math.Min(ta, Math.Min(tb, tc)), hi = Math.Max(ta, Math.Max(tb, tc));
            var cuts = cutBuf; cuts.Clear(); cuts.Add(float.NegativeInfinity);
            foreach (var o in g.offs) if (o > lo && o < hi) cuts.Add(o);
            cuts.Add(float.PositiveInfinity);
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                polyA.Clear(); polyA.Add(a); polyA.Add(b); polyA.Add(c);
                if (!float.IsNegativeInfinity(cuts[i])) { SlabClip(polyA, polyB, cuts[i], 1); Swap(); }
                if (!float.IsPositiveInfinity(cuts[i + 1])) { SlabClip(polyA, polyB, cuts[i + 1], -1); Swap(); }
                if (polyA.Count >= 3) ClipAndEmit(polyA);
            }
        }
        readonly List<float> cutBuf = new List<float>(8);
        readonly List<PV> clipA = new List<PV>(16), clipB = new List<PV>(16);
        void Swap() { polyA.Clear(); polyA.AddRange(polyB); }
        // keep the part where sign * (t - cut) >= 0
        void SlabClip(List<PV> inp, List<PV> outp, float cut, int sign)
        {
            outp.Clear(); int n = inp.Count; if (n == 0) return;
            for (int i = 0; i < n; i++)
            {
                var p = inp[i]; var q = inp[(i + 1) % n];
                float dp = sign * (GradT(p.x, p.y) - cut), dq = sign * (GradT(q.x, q.y) - cut);
                if (dp >= 0) outp.Add(p);
                if ((dp >= 0) != (dq >= 0)) outp.Add(Lerp(p, q, dp / (dp - dq)));
            }
        }
        static PV Lerp(PV p, PV q, float t) => new PV(p.x + (q.x - p.x) * t, p.y + (q.y - p.y) * t, p.u + (q.u - p.u) * t, p.v + (q.v - p.v) * t, p.k + (q.k - p.k) * t);
        void ClipAndEmit(List<PV> poly)
        {
            if (st.clip != null)
            {
                // an empty clip (a clip() fully outside the clip already there, e.g. a round portrait scrolled above a page's
                // top) hides everything; it used to have no edges and so drew the shape unclipped over the menu's top
                if (st.clip.Count < 3) return;
                var cl = st.clip; var cur = clipA; var nxt = clipB; cur.Clear(); cur.AddRange(poly);
                for (int e = 0; e < cl.Count && cur.Count > 0; e++)
                {
                    Vector2 a = cl[e], b = cl[(e + 1) % cl.Count]; nxt.Clear();
                    for (int i = 0; i < cur.Count; i++)
                    {
                        var p = cur[i]; var q = cur[(i + 1) % cur.Count];
                        float dp = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x), dq = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
                        if (dp >= 0) nxt.Add(p);
                        if ((dp >= 0) != (dq >= 0)) nxt.Add(Lerp(p, q, dp / (dp - dq)));
                    }
                    var t = cur; cur = nxt; nxt = t;
                }
                poly = cur;
            }
            for (int i = 1; i + 1 < poly.Count; i++) { Push(poly[0]); Push(poly[i]); Push(poly[i + 1]); }
        }

        // ---------- paths ----------
        class Sub { public List<Vector2> p = new List<Vector2>(); public bool closed; }
        readonly List<Sub> path = new List<Sub>();
        Sub cur;
        // subpaths are reused between paths instead of allocated per moveTo
        readonly List<Sub> subPool = new List<Sub>(); int subUsed;
        Sub NewSub()
        {
            if (subUsed == subPool.Count) subPool.Add(new Sub());
            var s = subPool[subUsed++]; s.p.Clear(); s.closed = false; path.Add(s); return s;
        }
        public void beginPath() { path.Clear(); cur = null; subUsed = 0; }
        public void moveTo(float x, float y) { cur = NewSub(); cur.p.Add(T(x, y)); }
        void MoveDev(Vector2 v) { cur = NewSub(); cur.p.Add(v); }
        void LineDev(Vector2 v) { if (cur == null) MoveDev(v); else cur.p.Add(v); }
        public void lineTo(float x, float y) => LineDev(T(x, y));
        public void closePath() { if (cur == null || cur.p.Count == 0) return; cur.closed = true; var first = cur.p[0]; cur = NewSub(); cur.p.Add(first); }
        Vector2 LastUser()
        {
            // inverse transform of the last point (curves need it as a start point)
            var v = cur != null && cur.p.Count > 0 ? cur.p[cur.p.Count - 1] : Vector2.zero; float det = st.a * st.d - st.b * st.c; if (Math.Abs(det) < 1e-9f) return v;
            float x = v.x - st.e, y = v.y - st.f; return new Vector2((st.d * x - st.c * y) / det, (-st.b * x + st.a * y) / det);
        }
        public void quadraticCurveTo(float cx, float cy, float x, float y)
        {
            var p0 = LastUser(); int n = Math.Max(6, (int)((Math.Abs(cx - p0.x) + Math.Abs(cy - p0.y) + Math.Abs(x - cx) + Math.Abs(y - cy)) * Scale / 6));
            n = Math.Min(n, 40);
            for (int i = 1; i <= n; i++) { float t = i / (float)n, u = 1 - t; lineTo(u * u * p0.x + 2 * u * t * cx + t * t * x, u * u * p0.y + 2 * u * t * cy + t * t * y); }
        }
        public void bezierCurveTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            var p0 = LastUser(); int n = Math.Max(8, (int)((Math.Abs(c1x - p0.x) + Math.Abs(c1y - p0.y) + Math.Abs(c2x - c1x) + Math.Abs(c2y - c1y) + Math.Abs(x - c2x) + Math.Abs(y - c2y)) * Scale / 6));
            n = Math.Min(n, 48);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                lineTo(u * u * u * p0.x + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * x, u * u * u * p0.y + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * y);
            }
        }
        public void arc(float x, float y, float r, float a0, float a1, bool ccw = false) => ellipse(x, y, r, r, 0, a0, a1, ccw);
        public void ellipse(float x, float y, float rx, float ry, float rot, float a0, float a1, bool ccw = false)
        {
            const float TAU = (float)(Math.PI * 2);
            float sweep = a1 - a0;
            if (!ccw) { if (sweep >= TAU) sweep = TAU; else { sweep %= TAU; if (sweep < 0) sweep += TAU; } }
            else { if (sweep <= -TAU) sweep = -TAU; else { sweep %= TAU; if (sweep > 0) sweep -= TAU; } }
            float rd = Math.Max(rx, ry) * Scale;
            int n = Math.Max(4, Math.Min(160, (int)Math.Ceiling(Math.Abs(sweep) * Math.Max(2.5, Math.Sqrt(Math.Max(1, rd)) * 1.6))));
            float cr = (float)Math.Cos(rot), sr = (float)Math.Sin(rot);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + sweep * i / n, px = (float)Math.Cos(a) * rx, py = (float)Math.Sin(a) * ry;
                LineDev(T(x + px * cr - py * sr, y + px * sr + py * cr));
            }
            if (Math.Abs(sweep) >= TAU - 1e-4f && cur != null && cur.p.Count > 2) { cur.p.RemoveAt(cur.p.Count - 1); cur.closed = true; var first = cur.p[0]; cur = NewSub(); cur.p.Add(first); }
        }
        public void rect(float x, float y, float w, float h) { moveTo(x, y); lineTo(x + w, y); lineTo(x + w, y + h); lineTo(x, y + h); closePath(); }
        public void roundRect(float x, float y, float w, float h, float r) => RoundRect(x, y, w, h, r, r, r, r);
        public void roundRect(float x, float y, float w, float h, float[] r)
        {
            float tl, tr, br, bl;
            if (r.Length == 1) tl = tr = br = bl = r[0]; else if (r.Length == 2) { tl = br = r[0]; tr = bl = r[1]; } else if (r.Length == 3) { tl = r[0]; tr = bl = r[1]; br = r[2]; } else { tl = r[0]; tr = r[1]; br = r[2]; bl = r[3]; }
            RoundRect(x, y, w, h, tl, tr, br, bl);
        }
        void RoundRect(float x, float y, float w, float h, float tl, float tr, float br, float bl)
        {
            if (w < 0) { x += w; w = -w; }
            if (h < 0) { y += h; h = -h; }
            float m = Math.Min(w, h) / 2; tl = Math.Min(tl, m); tr = Math.Min(tr, m); br = Math.Min(br, m); bl = Math.Min(bl, m);
            const float P = (float)Math.PI;
            moveTo(x + tl, y);
            lineTo(x + w - tr, y); if (tr > 0) ellipse(x + w - tr, y + tr, tr, tr, 0, -P / 2, 0);
            lineTo(x + w, y + h - br); if (br > 0) ellipse(x + w - br, y + h - br, br, br, 0, 0, P / 2);
            lineTo(x + bl, y + h); if (bl > 0) ellipse(x + bl, y + h - bl, bl, bl, 0, P / 2, P);
            lineTo(x, y + tl); if (tl > 0) ellipse(x + tl, y + tl, tl, tl, 0, P, P * 1.5f);
            closePath();
        }

        // the result is a shared scratch list: use it before the next Clean, and copy it to keep it
        readonly List<Vector2> cleanBuf = new List<Vector2>(64);
        List<Vector2> Clean(List<Vector2> p)
        {
            var o = cleanBuf; o.Clear();
            foreach (var v in p) if (o.Count == 0 || (o[o.Count - 1] - v).sqrMagnitude > 1e-4f) o.Add(v);
            if (o.Count > 2 && (o[0] - o[o.Count - 1]).sqrMagnitude < 1e-4f) o.RemoveAt(o.Count - 1);
            return o;
        }
        static float Area(List<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { var q = p[i]; var r = p[(i + 1) % p.Count]; a += q.x * r.y - r.x * q.y; } return a / 2; }

        // ---------- fill ----------
        public void fill() { Use(white, BlendMat()); SetPaint(st.fill); foreach (var s in path) FillPoly(Clean(s.p)); }
        void FillPoly(List<Vector2> p)
        {
            if (p.Count < 3) return;
            float area = Area(p); if (Math.Abs(area) < 0.01f) return;
            float sgn = area > 0 ? 1 : -1;
            // interior
            var idx = Triangulate(p, sgn);
            for (int i = 0; i + 2 < idx.Count; i += 3) Tri(PVof(p[idx[i]], 1), PVof(p[idx[i + 1]], 1), PVof(p[idx[i + 2]], 1));
            // soft 1px edge outside the shape
            int n = p.Count; var nrm = Scratch(ref nrmBuf, n);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = p[(i + n - 1) % n], b = p[i], c = p[(i + 1) % n];
                Vector2 e1 = (b - a).normalized, e2 = (c - b).normalized;
                Vector2 n1 = new Vector2(e1.y, -e1.x) * sgn, n2 = new Vector2(e2.y, -e2.x) * sgn;
                Vector2 m = n1 + n2; float ml = m.magnitude; if (ml < 1e-4f) m = n1; else m /= ml;
                float d = Vector2.Dot(m, n1); nrm[i] = m / Math.Max(0.35f, d);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; Vector2 a = p[i], b = p[j], ao = a + nrm[i], bo = b + nrm[j];
                Tri(PVof(a, 1), PVof(b, 1), PVof(bo, 0)); Tri(PVof(a, 1), PVof(bo, 0), PVof(ao, 0));
            }
        }
        Vector2[] nrmBuf = new Vector2[64], miterBuf = new Vector2[64];
        static Vector2[] Scratch(ref Vector2[] a, int n) { if (a.Length < n) a = new Vector2[Math.Max(n, a.Length * 2)]; return a; }
        static PV PVof(Vector2 v, float k) => new PV(v.x, v.y, 0.5f, 0.5f, k);
        static List<int> Triangulate(List<Vector2> p, float sgn)
        {
            int n = p.Count; var res = triRes; res.Clear();
            // convex: a fan
            bool convex = true;
            for (int i = 0; i < n && convex; i++) { Vector2 a = p[i], b = p[(i + 1) % n], c = p[(i + 2) % n]; if (((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x)) * sgn < -1e-3f) convex = false; }
            if (convex) { for (int i = 1; i + 1 < n; i++) { res.Add(0); res.Add(i); res.Add(i + 1); } return res; }
            // ear clipping
            var V = triV; V.Clear(); for (int i = 0; i < n; i++) V.Add(i);
            int guard = n * n + 10;
            while (V.Count > 3 && guard-- > 0)
            {
                bool cut = false;
                for (int i = 0; i < V.Count; i++)
                {
                    int i0 = V[(i + V.Count - 1) % V.Count], i1 = V[i], i2 = V[(i + 1) % V.Count];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    float cr = ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * sgn; if (cr <= 1e-6f) continue;
                    bool inside = false;
                    for (int k = 0; k < V.Count && !inside; k++)
                    {
                        int ik = V[k]; if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (InTri(p[ik], a, b, c, sgn)) inside = true;
                    }
                    if (inside) continue;
                    res.Add(i0); res.Add(i1); res.Add(i2); V.RemoveAt(i); cut = true; break;
                }
                if (!cut) break;
            }
            if (V.Count == 3) { res.Add(V[0]); res.Add(V[1]); res.Add(V[2]); }
            else for (int i = 1; i + 1 < V.Count; i++) { res.Add(V[0]); res.Add(V[i]); res.Add(V[i + 1]); }
            return res;
        }
        static readonly List<int> triRes = new List<int>(192), triV = new List<int>(64);
        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float sgn)
        {
            float d1 = ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x)) * sgn, d2 = ((c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x)) * sgn, d3 = ((a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x)) * sgn;
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }

        // ---------- stroke ----------
        public void stroke()
        {
            Use(white, BlendMat()); SetPaint(st.stroke);
            float hw = st.lineWidth * Scale / 2, am = 1; if (hw < 0.5f) { am = hw * 2; hw = 0.5f; }
            float sa = st.alpha; st.alpha *= am;
            foreach (var s in path)
            {
                var p = Clean(s.p); if (p.Count < 2) continue;
                bool closed = s.closed && p.Count > 2;
                if (st.dash != null) foreach (var seg in Dashes(p, closed)) StrokeLine(seg, false, hw);
                else StrokeLine(p, closed, hw);
            }
            st.alpha = sa;
        }
        List<List<Vector2>> Dashes(List<Vector2> p, bool closed)
        {
            var outp = new List<List<Vector2>>(); var pts = new List<Vector2>(p); if (closed) pts.Add(p[0]);
            float sc = Scale; var d = st.dash; int di = 0; float left = d[0] * sc; bool on = true; var curS = new List<Vector2> { pts[0] };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1]; float L = (b - a).magnitude, pos = 0;
                while (L - pos > left)
                {
                    pos += left; var q = a + (b - a) * (pos / L);
                    if (on) { curS.Add(q); if (curS.Count > 1) outp.Add(curS); }
                    curS = new List<Vector2> { q }; on = !on; di = (di + 1) % d.Length; left = Math.Max(0.5f, d[di] * sc);
                }
                left -= L - pos; if (on) curS.Add(b);
            }
            if (on && curS.Count > 1) outp.Add(curS);
            return outp;
        }
        void StrokeLine(List<Vector2> p, bool closed, float hw)
        {
            int n = p.Count;
            if (!closed && st.cap == "square")
            {
                capBuf.Clear(); capBuf.AddRange(p); p = capBuf;
                p[0] -= (p[1] - p[0]).normalized * hw; p[n - 1] += (p[n - 1] - p[n - 2]).normalized * hw;
            }
            var m = Scratch(ref miterBuf, n);
            for (int i = 0; i < n; i++)
            {
                bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
                Vector2 n1 = Vector2.zero, n2 = Vector2.zero;
                if (hasPrev) { var e = (p[i] - p[(i + n - 1) % n]).normalized; n1 = new Vector2(-e.y, e.x); }
                if (hasNext) { var e = (p[(i + 1) % n] - p[i]).normalized; n2 = new Vector2(-e.y, e.x); }
                if (!hasPrev) n1 = n2; if (!hasNext) n2 = n1;
                Vector2 mm = n1 + n2; float ml = mm.magnitude; if (ml < 1e-4f) mm = n1; else mm /= ml;
                float d = Vector2.Dot(mm, n1); m[i] = mm / Math.Max(0.3f, d);
            }
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n; Vector2 a = p[i], b = p[j];
                Vector2 ac = m[i] * hw, bc = m[j] * hw, af = m[i] * (hw + 1), bf = m[j] * (hw + 1);
                Tri(PVof(a + af, 0), PVof(b + bf, 0), PVof(b + bc, 1)); Tri(PVof(a + af, 0), PVof(b + bc, 1), PVof(a + ac, 1));
                Tri(PVof(a + ac, 1), PVof(b + bc, 1), PVof(b - bc, 1)); Tri(PVof(a + ac, 1), PVof(b - bc, 1), PVof(a - ac, 1));
                Tri(PVof(a - ac, 1), PVof(b - bc, 1), PVof(b - bf, 0)); Tri(PVof(a - ac, 1), PVof(b - bf, 0), PVof(a - af, 0));
            }
            if (!closed && st.cap == "round") { Cap(p[0], (p[0] - p[1]).normalized, hw); Cap(p[n - 1], (p[n - 1] - p[n - 2]).normalized, hw); }
        }
        readonly List<Vector2> capBuf = new List<Vector2>(64);
        void Cap(Vector2 c, Vector2 dir, float hw)
        {
            float a0 = Mathf.Atan2(dir.y, dir.x) - Mathf.PI / 2; int k = Math.Max(6, (int)(hw * 1.5f)); k = Math.Min(k, 32);
            for (int i = 0; i < k; i++)
            {
                float t0 = a0 + Mathf.PI * i / k, t1 = a0 + Mathf.PI * (i + 1) / k;
                Vector2 d0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)), d1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
                Tri(PVof(c, 1), PVof(c + d0 * hw, 1), PVof(c + d1 * hw, 1));
                Tri(PVof(c + d0 * hw, 1), PVof(c + d0 * (hw + 1), 0), PVof(c + d1 * (hw + 1), 0)); Tri(PVof(c + d0 * hw, 1), PVof(c + d1 * (hw + 1), 0), PVof(c + d1 * hw, 1));
            }
        }

        // ---------- shortcuts ----------
        public void fillRect(float x, float y, float w, float h)
        {
            if (w == 0 || h == 0) return;
            Use(white, BlendMat()); SetPaint(st.fill);
            Vector2 a = T(x, y), b = T(x + w, y), c = T(x + w, y + h), d = T(x, y + h);
            bool axis = Math.Abs(st.b) < 1e-5f && Math.Abs(st.c) < 1e-5f;
            if (axis) { Tri(PVof(a, 1), PVof(b, 1), PVof(c, 1)); Tri(PVof(a, 1), PVof(c, 1), PVof(d, 1)); return; } // rects on the pixel grid stay crisp
            quadBuf.Clear(); quadBuf.Add(a); quadBuf.Add(b); quadBuf.Add(c); quadBuf.Add(d); FillPoly(quadBuf);
        }
        readonly List<Vector2> quadBuf = new List<Vector2>(4); readonly List<Sub> keepBuf = new List<Sub>();
        // strokes a rect without touching the current path (its subpaths stay reserved in the pool)
        public void strokeRect(float x, float y, float w, float h)
        {
            keepBuf.Clear(); keepBuf.AddRange(path); var kc = cur; int ku = subUsed;
            path.Clear(); cur = null; rect(x, y, w, h); stroke();
            path.Clear(); path.AddRange(keepBuf); cur = kc; subUsed = ku;
        }
        public void clearRect(float x, float y, float w, float h) { }
        public void clip()
        {
            Sub s = null; foreach (var q in path) if (q.p.Count >= 3) { s = q; break; }
            if (s == null) return;
            var p = Clean(s.p); if (p.Count < 3) return;
            p = new List<Vector2>(p); // Clean's list is scratch; a clip is kept in the state
            if (Area(p) < 0) p.Reverse(); // keep the inside on the left of each edge
            if (st.clip == null) { st.clip = p; return; }
            // intersect with the clip that is already there
            var res = new List<Vector2>(); var cl = st.clip; var curP = new List<Vector2>(p);
            for (int e = 0; e < cl.Count && curP.Count > 0; e++)
            {
                Vector2 a = cl[e], b = cl[(e + 1) % cl.Count]; res = new List<Vector2>();
                for (int i = 0; i < curP.Count; i++)
                {
                    var q = curP[i]; var r = curP[(i + 1) % curP.Count];
                    float dq = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x), dr = (b.x - a.x) * (r.y - a.y) - (b.y - a.y) * (r.x - a.x);
                    if (dq >= 0) res.Add(q);
                    if ((dq >= 0) != (dr >= 0)) res.Add(q + (r - q) * (dq / (dq - dr)));
                }
                curP = res;
            }
            st.clip = curP;
        }

        // ---------- images ----------
        public void drawImage(Img im, float dx, float dy, float dw, float dh) { if (im != null && im.ok) drawImage(im, 0, 0, im.w, im.h, dx, dy, dw, dh); }
        public void drawImage(Img im, float sx, float sy, float sw, float sh, float dx, float dy, float dw, float dh)
        {
            if (im == null || !im.ok || dw == 0 || dh == 0) return;
            Use(im.tex, BlendMat()); SetPaint(default, true);
            float u0 = sx / im.w, u1 = (sx + sw) / im.w, v0 = 1 - sy / im.h, v1 = 1 - (sy + sh) / im.h;
            Vector2 a = T(dx, dy), b = T(dx + dw, dy), c = T(dx + dw, dy + dh), d = T(dx, dy + dh);
            var A = new PV(a.x, a.y, u0, v0, 1); var B = new PV(b.x, b.y, u1, v0, 1); var C = new PV(c.x, c.y, u1, v1, 1); var D = new PV(d.x, d.y, u0, v1, 1);
            Tri(A, B, C); Tri(A, C, D);
        }

        // ---------- text ----------
        public static Font lilita, sans;
        static void LoadFonts()
        {
            if (lilita == null) lilita = Resources.Load<Font>("BAM/Fonts/LilitaOne");
            if (sans == null)
            {
                try { sans = Font.CreateDynamicFontFromOSFont(new[] { "Trebuchet MS", "Segoe UI", "Arial", "Roboto", "Helvetica" }, 32); } catch (Exception) { sans = null; }
                if (sans == null) sans = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            if (lilita == null) lilita = sans;
        }
        class FontSpec { public Font font; public float px; public FontStyle style; }
        static readonly Dictionary<string, FontSpec> fontCache = new Dictionary<string, FontSpec>();
        static FontSpec Spec(string f)
        {
            if (fontCache.TryGetValue(f, out var s) && s.font != null) return s;
            if (sans == null || lilita == null) { fontCache.Clear(); LoadFonts(); } // a font made in an earlier Play session is gone
            s = new FontSpec { px = 10, font = sans };
            bool bold = false, italic = false; var parts = f.Split(' ');
            int i = 0;
            for (; i < parts.Length; i++)
            {
                string t = parts[i];
                if (t == "bold" || t == "700" || t == "800" || t == "900") bold = true;
                else if (t == "italic") italic = true;
                else if (t.EndsWith("px")) { float.TryParse(t.Substring(0, t.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s.px); i++; break; }
            }
            string fam = string.Join(" ", parts, Math.Min(i, parts.Length), parts.Length - Math.Min(i, parts.Length));
            bool lil = fam.TrimStart('"', '\'').StartsWith("Lilita");
            s.font = lil ? lilita : sans;
            s.style = lil ? (italic ? FontStyle.Italic : FontStyle.Normal) : bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
            if (fontCache.Count > 2000) fontCache.Clear();
            return fontCache[f] = s;
        }
        // glyph size in device pixels, in steps above 20 px: a zooming HUD would otherwise ask for a new size every frame
        // and keep refilling (and rebuilding) the font texture; the glyph quads are scaled to the exact size anyway
        int DevSize(FontSpec s)
        {
            int z = Math.Max(4, Math.Min(400, (int)Math.Round(s.px * Scale)));
            int step = z <= 20 ? 1 : z <= 48 ? 2 : z <= 96 ? 4 : 8;
            return (z + step / 2) / step * step;
        }
        // make sure the glyphs are in the font texture; a request can rebuild that texture, so text already queued
        // with it is drawn first (only then, so labels in a row share one draw call)
        void Need(FontSpec s, string text, int size)
        {
            foreach (char ch in text)
                if (!s.font.GetCharacterInfo(ch, out _, size, s.style))
                {
                    if (verts.Count > 0 && curTex == s.font.material.mainTexture) Flush();
                    s.font.RequestCharactersInTexture(text, size, s.style); return;
                }
        }
        public float measureText(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var s = Spec(st.font); int size = DevSize(s); Need(s, text, size);
            float w = 0; foreach (char ch in text) if (s.font.GetCharacterInfo(ch, out var ci, size, s.style)) w += ci.advance;
            return w * s.px / size;
        }
        public void fillText(string text, float x, float y) { DrawText(text, x, y, st.fill); }
        // the outline is the text drawn round the point in a ring of copies (canvas strokes the glyph outlines)
        public void strokeText(string text, float x, float y)
        {
            float r = st.lineWidth / 2; int n = st.lineWidth * Scale > 5 ? 16 : 8;
            DrawText(text, x, y, st.stroke, r, n); // the glyphs are looked up once and emitted n times
        }
        // Glyphs are drawn with the font's own shader (GUI/Text Shader: vertex colour, texture alpha). UI/Default needs
        // _TextureSampleAdd = (1,1,1,0) to make the alpha-only font texture white, and on BAM's Unity 6.6 setting it on the material
        // did not take (all online text came out black, 2026-10-27). The font's shader is in every build, as the font uses it.
        Material textMat;
        Material FontMat(Font f)
        {
            if (textMat == null && f != null && f.material != null && f.material.shader != null && f.material.shader.isSupported)
                textMat = new Material(f.material) { name = "ctx_text" };
            return textMat != null ? textMat : fontMat;
        }
        CharacterInfo[] glyphs = new CharacterInfo[64]; bool[] hasGlyph = new bool[64];
        // ringN > 0: the text is drawn ringN times round (x, y) at distance ringR (strokeText)
        void DrawText(string text, float x, float y, Paint paint, float ringR = 0, int ringN = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            var s = Spec(st.font); int size = DevSize(s); float k = s.px / size; // device glyph pixels -> CSS pixels
            Need(s, text, size);
            int len = text.Length; if (glyphs.Length < len) { glyphs = new CharacterInfo[len * 2]; hasGlyph = new bool[len * 2]; }
            float w = 0;
            for (int i = 0; i < len; i++) { hasGlyph[i] = s.font.GetCharacterInfo(text[i], out glyphs[i], size, s.style); if (hasGlyph[i]) w += glyphs[i].advance; }
            w *= k;
            string al = st.align; float ox = al == "center" ? -w / 2 : al == "right" || al == "end" ? -w : 0;
            string bl = st.baseline; float oy = bl == "middle" ? s.px * 0.35f : bl == "top" || bl == "hanging" ? s.px * 0.8f : bl == "bottom" || bl == "ideographic" ? -s.px * 0.2f : 0;
            Use(s.font.material.mainTexture, FontMat(s.font)); SetPaint(paint);
            float bx = x, by = y;
            for (int r = 0; r < Math.Max(1, ringN); r++)
            {
                if (ringN > 0) { float a = r * Mathf.PI * 2 / ringN; x = bx + Mathf.Cos(a) * ringR; y = by + Mathf.Sin(a) * ringR; }
                float pen = 0;
                for (int gi = 0; gi < len; gi++)
                {
                    if (!hasGlyph[gi]) continue; var ci = glyphs[gi];
                    float x0 = x + ox + (pen + ci.minX) * k, x1 = x + ox + (pen + ci.maxX) * k, y0 = y + oy - ci.maxY * k, y1 = y + oy - ci.minY * k;
                    pen += ci.advance;
                    if (ci.maxX <= ci.minX) continue;
                    Vector2 a = T(x0, y0), b = T(x1, y0), c = T(x1, y1), d = T(x0, y1);
                    var A = new PV(a.x, a.y, ci.uvTopLeft.x, ci.uvTopLeft.y, 1); var B = new PV(b.x, b.y, ci.uvTopRight.x, ci.uvTopRight.y, 1);
                    var C = new PV(c.x, c.y, ci.uvBottomRight.x, ci.uvBottomRight.y, 1); var D = new PV(d.x, d.y, ci.uvBottomLeft.x, ci.uvBottomLeft.y, 1);
                    Tri(A, B, C); Tri(A, C, D);
                }
            }
        }

        // ---------- gradients ----------
        public Grad createLinearGradient(float x0, float y0, float x1, float y1) => new Grad { x0 = x0, y0 = y0, x1 = x1, y1 = y1 };
        public Grad createRadialGradient(float x0, float y0, float r0, float x1, float y1, float r1) => new Grad { radial = true, x0 = x0, y0 = y0, r0 = r0, x1 = x1, y1 = y1, r1 = r1 };
        // radial gradient over an ellipse (CSS radial-gradient), drawn as rings so the colours come out right
        // On screen a radial gradient is a small cached texture stretched over the ellipse (one quad instead of rings x 48 segments,
        // ~9 000 vertices per frame for the menu background). Into a render texture (the 3D view's sprites) it is still drawn as rings.
        bool toRT;
        readonly Dictionary<long, Img> radialTex = new Dictionary<long, Img>();
        Img RadialTex(Grad g)
        {
            long key = 17; for (int i = 0; i < g.offs.Count; i++) { var c = g.cols[i]; key = key * 31 + g.offs[i].GetHashCode(); key = key * 31 + c.GetHashCode(); }
            if (radialTex.TryGetValue(key, out var im)) return im;
            if (radialTex.Count > 32) { foreach (var o in radialTex.Values) UnityEngine.Object.Destroy(o.tex); radialTex.Clear(); }
            const int S = 128; float half = S / 2f; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half, t = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = g.At(Math.Min(t, 1)); c.a *= Mathf.Clamp01((1 - t) * half + 0.5f); // soft 1px rim, colour kept outside so the edge does not darken
                    px[y * S + x] = c;
                }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ctx_radial" };
            tex.SetPixels32(px); tex.Apply(false, true);
            return radialTex[key] = Img.Of(tex);
        }
        public void fillRadial(float cx, float cy, float rx, float ry, Grad g, int rings = 24)
        {
            if (!toRT) { drawImage(RadialTex(g), cx - rx, cy - ry, 2 * rx, 2 * ry); return; }
            Use(white, BlendMat()); SetPaint(default, true);
            int seg = 48; var save = st.fill;
            for (int r = 0; r < rings; r++)
            {
                float t0 = r / (float)rings, t1 = (r + 1) / (float)rings; Color c0 = Lin(g.At(t0)), c1 = Lin(g.At(t1)); c0.a *= st.alpha; c1.a *= st.alpha;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector2 p00 = T(cx + Mathf.Cos(a0) * rx * t0, cy + Mathf.Sin(a0) * ry * t0), p01 = T(cx + Mathf.Cos(a1) * rx * t0, cy + Mathf.Sin(a1) * ry * t0);
                    Vector2 p10 = T(cx + Mathf.Cos(a0) * rx * t1, cy + Mathf.Sin(a0) * ry * t1), p11 = T(cx + Mathf.Cos(a1) * rx * t1, cy + Mathf.Sin(a1) * ry * t1);
                    Raw(p00, c0); Raw(p10, c1); Raw(p11, c1); Raw(p00, c0); Raw(p11, c1); Raw(p01, c0);
                }
            }
            st.fill = save;
        }
        void Raw(Vector2 p, Color c) { verts.Add(new V { x = p.x, y = p.y, u = 0.5f, v = 0.5f, c = c }); }
    }
}
