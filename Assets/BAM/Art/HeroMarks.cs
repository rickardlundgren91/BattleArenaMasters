using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// The Tikta 2.0 and Blast 2.0 marks on one unit (XYZ df94538, View3DTiktaMark.cs and View3DBlastChain.cs), built the first time
    /// one is shown:
    /// - Deathmark: a sigil over the head (a soft violet glow, a pink heart, four teal needle points that breathe in and out round it)
    ///   and a thin violet ring at the feet, turning slowly. It blinks in its last 0.75 s.
    /// - Chain spark: a small gold and violet spark crown over a burning enemy while that Blast's Chain Reaction is ready.
    /// - Chain cooldown: a thin violet arc at your own Blast's feet that fills while the chain cools down.
    /// No smoke and no fog: thin lines and dots that read on a phone and keep the hero visible. Display only.
    /// </summary>
    sealed class HeroMarks
    {
        static readonly Color Teal = new Color(0.38f, 1f, 0.88f), Pink = new Color(1f, 0.36f, 0.78f), Violet = new Color(0.66f, 0.4f, 1f),
            Hot = new Color(1f, 0.86f, 0.97f), Gold = new Color(1f, 0.82f, 0.3f), ChainViolet = new Color(0.7f, 0.42f, 1f);
        const float Blink = 0.75f;
        const int ArcSeg = 24;
        static Texture2D glowTex;
        static bool glowLoaded;
        static readonly Mesh[] arcs = new Mesh[ArcSeg + 1];

        readonly Transform parent;
        readonly float height, radius;
        Transform markRoot, sigil, glow, heart, core, feetRing, crown, arcT;
        readonly Transform[] needles = new Transform[4], stars = new Transform[3];
        Material glowMat, heartMat, coreMat, needleMat, ringMat, crownGlowMat, arcMat;
        readonly Material[] starMats = new Material[3];
        MeshFilter arcF;
        float markLeft = -1f, cooldown = -1f, markAge;
        bool spark, visible = true;
        int arcN = -1;

        public HeroMarks(Transform parent, float height, float radius)
        {
            this.parent = parent;
            this.height = height;
            this.radius = radius;
        }

        public bool Any => markLeft >= 0f || spark || cooldown >= 0f;

        public bool Visible
        {
            set { if (visible == value) return; visible = value; Apply(); }
        }

        public void SetDeathmark(float left)
        {
            if (left >= 0f && markLeft < 0f) markAge = 0f;
            markLeft = left;
            if (left >= 0f && markRoot == null) BuildMark();
            Apply();
        }

        public void SetChainSpark(bool on)
        {
            spark = on;
            if (on && crown == null) BuildCrown();
            Apply();
        }

        public void SetChainCooldown(float frac)
        {
            cooldown = frac;
            if (frac >= 0f && arcT == null) BuildArc();
            Apply();
        }

        public void Reset()
        {
            markLeft = -1f; spark = false; cooldown = -1f; visible = true;
            Apply();
        }

        void Apply()
        {
            if (markRoot != null) markRoot.gameObject.SetActive(visible && markLeft >= 0f);
            if (crown != null) crown.gameObject.SetActive(visible && spark);
            if (arcT != null) arcT.gameObject.SetActive(visible && cooldown >= 0f);
        }

        /// <summary>After the animation, every frame while <see cref="Any"/>: billboards face the camera, things turn and blink.</summary>
        public void Tick(float dt, float time, Quaternion cam)
        {
            if (!visible) return;
            Quaternion face = cam * Quaternion.Euler(-90f, 0f, 0f); // the flat quads (picture top toward +z) turned to face the camera
            if (markLeft >= 0f && markRoot != null)
            {
                markAge += dt;
                float fade = Mathf.Clamp01(markAge / 0.12f);
                if (markLeft < Blink && Mathf.Sin(time * 28f) < 0f) fade *= 0.35f;
                float spin = time * 1.6f, close = 0.5f + 0.5f * Mathf.Sin(time * 9f), sz = Mathf.Max(0.8f, height / 1.8f);
                sigil.position = parent.position + new Vector3(0f, height + 0.35f * sz, 0f);
                sigil.rotation = cam;
                glow.localRotation = heart.localRotation = core.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                Add(glowMat, Violet, 0.45f * fade);
                Add(heartMat, Pink, 0.95f * fade);
                Add(coreMat, Hot, fade);
                for (int i = 0; i < 4; i++)
                {
                    float a = spin + i * Mathf.PI * 0.5f, r0 = (0.42f - 0.08f * close) * sz, r1 = 0.17f * sz, rm = (r0 + r1) * 0.5f;
                    Transform n = needles[i];
                    n.localPosition = new Vector3(Mathf.Cos(a) * rm, Mathf.Sin(a) * rm, -0.01f);
                    // the needle's picture runs along its z: lay it in the screen plane, pointing at the heart
                    n.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f) * Quaternion.Euler(-90f, 0f, 0f);
                    n.localScale = new Vector3(0.11f * sz, 1f, (r0 - r1) * 1.6f);
                }
                Add(needleMat, Teal, 0.95f * fade);
                feetRing.position = parent.position + new Vector3(0f, 0.05f, 0f);
                feetRing.rotation = Quaternion.Euler(0f, -spin * 0.5f * Mathf.Rad2Deg, 0f);
                Add(ringMat, Violet, 0.55f * fade);
            }
            if (spark && crown != null)
            {
                float spin = time * 2.2f;
                crown.position = parent.position + new Vector3(0f, height + 0.3f, 0f);
                crown.rotation = cam;
                for (int i = 0; i < 3; i++)
                {
                    float a = spin + i * 2.094f;
                    stars[i].localPosition = new Vector3(Mathf.Cos(a) * 0.22f, Mathf.Sin(a) * 0.07f, 0f);
                    stars[i].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg) * Quaternion.Euler(-90f, 0f, 0f);
                    Add(starMats[i], i == 0 ? Gold : ChainViolet, 0.9f);
                }
                Add(crownGlowMat, Gold, 0.35f + 0.15f * Mathf.Sin(time * 9f));
            }
            if (cooldown >= 0f && arcT != null)
            {
                arcT.position = parent.position + new Vector3(0f, 0.045f, 0f);
                arcT.rotation = Quaternion.identity;
                int n = Mathf.Clamp(Mathf.RoundToInt(ArcSeg * Mathf.Clamp01(cooldown)), 1, ArcSeg);
                if (n != arcN) { arcN = n; arcF.sharedMesh = Arc(n); }
            }
        }

        // additive pictures fade by their brightness
        static void Add(Material m, Color c, float a) => m.SetColor(ArtMaterials.BaseColor, new Color(c.r * a, c.g * a, c.b * a, 1f));

        // ---------------------------------------------------------------- building

        static Texture2D GlowTex()
        {
            if (!glowLoaded) { glowLoaded = true; glowTex = RickardFx.Load("tx_fx_glow_soft"); }
            return glowTex;
        }

        Transform Part(string name, Transform under, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(under, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        static Material GlowMat()
        {
            Texture2D t = GlowTex();
            if (t == null) return FxAtlas.Texture != null ? FxAtlas.Material(ArtMaterials.UnlitAdd) : ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            var m = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            m.SetTexture(ArtMaterials.BaseMap, t);
            m.mainTexture = t;
            m.renderQueue = 3060;
            return m;
        }

        static Mesh GlowMesh() => GlowTex() != null ? FxAtlas.FullQuad() : FxAtlas.Quad(FxAtlas.Glow);

        void BuildMark()
        {
            var root = new GameObject("Deathmark");
            markRoot = root.transform;
            markRoot.SetParent(parent, false);
            var sg = new GameObject("Sigil");
            sigil = sg.transform;
            sigil.SetParent(markRoot, false);
            float sz = Mathf.Max(0.8f, height / 1.8f);
            glow = Part("Glow", sigil, GlowMesh(), glowMat = GlowMat());
            glow.localScale = new Vector3(0.75f * sz, 1f, 0.75f * sz);
            heart = Part("Heart", sigil, GlowMesh(), heartMat = GlowMat());
            heart.localScale = new Vector3(0.24f * sz, 1f, 0.24f * sz);
            heart.localPosition = new Vector3(0f, 0f, -0.01f);
            core = Part("Core", sigil, GlowMesh(), coreMat = GlowMat());
            core.localScale = new Vector3(0.1f * sz, 1f, 0.1f * sz);
            core.localPosition = new Vector3(0f, 0f, -0.02f);
            needleMat = FxAtlas.Texture != null ? FxAtlas.Material(ArtMaterials.UnlitAdd) : ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            needleMat.renderQueue = 3061;
            Mesh nm = FxAtlas.Texture != null ? FxAtlas.Quad(FxAtlas.Streak) : Shapes.RingMesh(0.9f, Pal.White);
            for (int i = 0; i < 4; i++) needles[i] = Part("Needle", sigil, nm, needleMat);
            ringMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            feetRing = Part("FeetRing", markRoot, Shapes.RingMesh(0.88f, Pal.White, 48), ringMat);
            float rr = Mathf.Max(radius, 0.3f) * 1.45f;
            feetRing.localScale = new Vector3(rr, 1f, rr);
        }

        void BuildCrown()
        {
            var go = new GameObject("ChainCrown");
            crown = go.transform;
            crown.SetParent(parent, false);
            Mesh star = FxAtlas.Texture != null ? FxAtlas.Quad(FxAtlas.Star) : FxAtlas.FullQuad();
            for (int i = 0; i < 3; i++)
            {
                starMats[i] = FxAtlas.Texture != null ? FxAtlas.Material(ArtMaterials.UnlitAdd) : ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                starMats[i].renderQueue = 3061;
                stars[i] = Part("Star", crown, star, starMats[i]);
                stars[i].localScale = new Vector3(0.24f, 1f, 0.24f);
            }
            Transform g = Part("Glow", crown, GlowMesh(), crownGlowMat = GlowMat());
            g.localScale = new Vector3(0.5f, 1f, 0.5f);
            g.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        void BuildArc()
        {
            arcMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            arcMat.SetColor(ArtMaterials.BaseColor, new Color(ChainViolet.r * 0.8f, ChainViolet.g * 0.8f, ChainViolet.b * 0.8f, 1f));
            arcT = Part("ChainArc", parent, Arc(1), arcMat);
            arcF = arcT.GetComponent<MeshFilter>();
            float r = Mathf.Max(radius, 0.3f) + 0.25f;
            arcT.localScale = new Vector3(r, 1f, r);
        }

        // n of 24 ring segments, clockwise from the top (as seen from above, z up the screen), thin; painted with the palette's white
        static Mesh Arc(int n)
        {
            if (arcs[n] != null) return arcs[n];
            var v = new Vector3[(n + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[n * 12]; // both windings: seen from above whichever way the ground plane faces
            Vector2 w = Pal.UV(Pal.White);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / ArcSeg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = new Vector3(c * 0.9f, 0f, s * 0.9f);
                v[i * 2 + 1] = new Vector3(c, 0f, s);
                uv[i * 2] = w; uv[i * 2 + 1] = w;
            }
            for (int i = 0; i < n; i++)
            {
                int a0 = i * 2, q = i * 6;
                tri[q] = a0; tri[q + 1] = a0 + 1; tri[q + 2] = a0 + 3; tri[q + 3] = a0; tri[q + 4] = a0 + 3; tri[q + 5] = a0 + 2;
                int r = n * 6 + q;
                tri[r] = a0; tri[r + 1] = a0 + 3; tri[r + 2] = a0 + 1; tri[r + 3] = a0; tri[r + 4] = a0 + 2; tri[r + 5] = a0 + 3;
            }
            var m = new Mesh { name = "Chain arc " + n, vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            arcs[n] = m;
            return m;
        }
    }
}
