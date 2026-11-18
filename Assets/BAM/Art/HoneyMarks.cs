using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// Moa's shields on one hero (Simon's Moa 2.0, XYZ #610 f4dbfea, the picture part only), built the first time one is shown:
    /// - Honey Guard (her tap): a ring of 12 small hex beads round the feet in honey yellow, just outside the orange "special
    ///   ready" ring (MOA-03); a cell goes dark for each twelfth of the
    ///   shield that is used up, so everyone can read how much is left.
    /// - The honey field's shield: the same ring, smaller and paler.
    /// - Queen's Instinct: a crown of 6 hex cells over the head and an orange ring at the feet, beating a double heartbeat.
    /// The cells are cut from Rickard's hex picture (T_BAM_HexShield, the one on Moa's shield globe). Display only.
    /// </summary>
    sealed class HoneyMarks
    {
        const int Ring = 12, Crown = 6;
        // the guard in the palette's honey yellow (Pal.Honey ffc93c), not amber: the orange "special ready" ring stays unique (MOA-03)
        static readonly Color GuardLit = new Color(1f, 0.79f, 0.235f), GuardDim = new Color(0.3f, 0.24f, 0.08f),
            FieldLit = new Color(1f, 0.9f, 0.55f), FieldDim = new Color(0.2f, 0.16f, 0.08f),
            QueenLit = new Color(1f, 0.5f, 0.12f), QueenDim = new Color(0.3f, 0.12f, 0.04f), QueenRing = new Color(1f, 0.45f, 0.08f);
        // one hex cell of the picture (512 px, cells point up): its middle and half size, in pixels from the top left
        const float CellX = 127.5f, CellY = 61.5f, CellHalf = 42f, TexSize = 512f;
        static Texture2D hexTex;
        static bool texLoaded;
        static readonly Mesh[] ringMeshes = new Mesh[Ring + 1], guardMeshes = new Mesh[Ring + 1], crownMeshes = new Mesh[Crown + 1];
        // the guard's cells are small, apart beads (0.6 of a full cell): a broken ring, unlike the solid ready ring (MOA-03)
        const float GuardBead = 0.6f;

        readonly Transform parent;
        readonly float height, radius;
        Transform guard, field, crown, queenRing;
        MeshFilter guardF, fieldF, crownF;
        Material guardLit, guardDim, fieldLit, fieldDim, crownLit, crownDim, queenRingMat;
        float g = -1f, f = -1f, q = -1f, gAge, fAge, qAge;
        int gN = -1, fN = -1, qN = -1;
        bool visible = true;
        // the cell that cracks off when the shield stops a stun (Crack): its piece, how old it is (-1 = none), which way it flies and
        // the ring's size it broke from; the ring jolts while it flies
        Transform shard;
        Material shardMat;
        float crackAge = -1f, crackR = 1f, guardS = -1f, fieldS = -1f;
        Vector3 crackDir;
        static Mesh cellOnly;
        const float CrackTime = 0.45f;

        public HoneyMarks(Transform parent, float height, float radius)
        {
            this.parent = parent;
            this.height = height;
            this.radius = radius;
        }

        public bool Any => g >= 0f || f >= 0f || q >= 0f || crackAge >= 0f;

        public bool Visible
        {
            set { if (visible == value) return; visible = value; Apply(); }
        }

        public void Set(float guardLeft, float fieldLeft, float queenLeft)
        {
            if (HexTex() == null) return; // no picture: Moa's shield globe (StatusFx) still shows the shield
            if (guardLeft >= 0f && g < 0f) gAge = 0f;
            if (fieldLeft >= 0f && f < 0f) fAge = 0f;
            if (queenLeft >= 0f && q < 0f) qAge = 0f;
            g = guardLeft; f = fieldLeft; q = queenLeft;
            if (g >= 0f && guard == null) BuildGuard();
            if (f >= 0f && field == null) BuildField();
            if (q >= 0f && crown == null) BuildQueen();
            Cells(guardF, ref gN, g, Ring, guardMeshes, guard, GuardBead);
            Cells(fieldF, ref fN, f, Ring, ringMeshes, field);
            Cells(crownF, ref qN, q, Crown, crownMeshes, crown);
            Apply();
        }

        public void Reset()
        {
            g = f = q = -1f; gN = fN = qN = -1; visible = true;
            crackAge = -1f; guardS = fieldS = -1f;
            Apply();
        }

        void Apply()
        {
            if (guard != null) guard.gameObject.SetActive(visible && g >= 0f);
            if (field != null) field.gameObject.SetActive(visible && f >= 0f);
            if (crown != null) crown.gameObject.SetActive(visible && q >= 0f);
            if (queenRing != null) queenRing.gameObject.SetActive(visible && q >= 0f);
            if (shard != null) shard.gameObject.SetActive(visible && crackAge >= 0f);
        }

        /// <summary>Moa's shield just stopped a stun, root, pull or knock on this hero (EventType.CcBlocked; Rickard 2026-11-18): one
        /// lit cell breaks off the ring, flies out and up and fades, and the ring jolts. It still plays when the same blow used the
        /// shield up (the ring is gone then), from the ring's last size. Queen's Instinct blocks nothing, so only the guard and the
        /// field ring crack.</summary>
        public void Crack()
        {
            if (HexTex() == null || !visible) return;
            float s = g >= 0f && guardS > 0f ? guardS : fieldS > 0f ? fieldS : guardS;
            if (s <= 0f) return; // no ring was ever drawn on this hero
            if (shard == null)
            {
                shardMat = HexMat(3063);
                shard = Part("HoneyCrack", CellOnly(), shardMat);
            }
            float a = Random.value * Mathf.PI * 2f;
            crackDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            crackR = s;
            crackAge = 0f;
            Apply();
        }

        // a cell lights for each started twelfth (sixth on the crown) still left; one going dark gives a small honey spark there
        void Cells(MeshFilter mf, ref int shown, float left, int count, Mesh[] cache, Transform at, float scale = 1f)
        {
            if (mf == null || left < 0f) { shown = -1; return; }
            int n = Mathf.Clamp(Mathf.CeilToInt(left * count - 0.001f), 0, count);
            if (n == shown) return;
            if (shown > n && visible && at != null)
                Fx.Glow(at.position + new Vector3(0f, 0.15f, 0f), 2, Pal.Get(Pal.Honey), 0.14f, 0.6f, 0.3f, 0.6f, at.lossyScale.x * 0.8f);
            shown = n;
            mf.sharedMesh = CellMesh(count, n, cache, scale);
        }

        /// <summary>After the animation, every frame while <see cref="Any"/>: the rings turn, pop in and the crown beats.</summary>
        public void Tick(float dt, float time)
        {
            if (!visible) return;
            Vector3 p = parent.position;
            float baseR = Mathf.Max(radius, 0.3f);
            // the ring jolts outward for a moment when a cell cracks off (Crack)
            float jolt = 1f;
            if (crackAge >= 0f)
            {
                crackAge += dt;
                float u = crackAge / CrackTime;
                if (u >= 1f) { crackAge = -1f; Apply(); }
                else
                {
                    float w = 1f - u;
                    jolt = 1f + 0.18f * w * w;
                    // the piece: from the ring's edge out by half the ring and up in a small arc, a little bigger, its light fading
                    shard.position = p + crackDir * (crackR * 0.6f * u) + new Vector3(0f, 0.06f + 0.9f * u - 0.7f * u * u, 0f);
                    shard.rotation = Quaternion.LookRotation(crackDir) * Quaternion.Euler(-u * 20f, 0f, 0f);
                    shard.localScale = Local(crackR * (1f + 0.3f * u));
                    Add(shardMat, GuardLit, 1.6f * w * w);
                }
            }
            if (g >= 0f && guard != null)
            {
                gAge += dt;
                // its beads sit just outside the orange "special ready" ring (MatchRings: max(0.55, R + 0.35) + 0.12, about 0.1
                // wide when it breathes), so the two never overlap on the player's own hero (MOA-03)
                float bead = Mathf.Sin(Mathf.PI / Ring) * 0.92f * GuardBead;
                float s = Mathf.Max(baseR * 1.75f, (Mathf.Max(0.55f, radius + 0.35f) + 0.12f + 0.14f) / (1f - bead)) * Pop(gAge);
                guardS = s;
                s *= jolt;
                guard.position = p + new Vector3(0f, 0.05f, 0f);
                guard.rotation = Quaternion.Euler(0f, time * 25f, 0f);
                guard.localScale = Local(s);
                Add(guardLit, GuardLit, 0.95f + 0.05f * Mathf.Sin(time * 5f));
                Add(guardDim, GuardDim, 0.8f);
            }
            if (f >= 0f && field != null)
            {
                fAge += dt;
                float s = baseR * 1.3f * Pop(fAge);
                fieldS = s;
                s *= jolt;
                field.position = p + new Vector3(0f, 0.045f, 0f);
                field.rotation = Quaternion.Euler(0f, -time * 20f, 0f);
                field.localScale = Local(s);
                Add(fieldLit, FieldLit, 0.55f);
                Add(fieldDim, FieldDim, 0.6f);
            }
            if (q >= 0f && crown != null)
            {
                qAge += dt;
                float beat = Heartbeat(time), sz = Mathf.Max(0.8f, height / 1.8f);
                crown.position = p + new Vector3(0f, height + 0.25f * sz, 0f);
                crown.rotation = Quaternion.Euler(0f, time * 40f, 0f);
                crown.localScale = Local(0.3f * sz * (1f + 0.12f * beat) * Pop(qAge));
                queenRing.position = p + new Vector3(0f, 0.055f, 0f);
                queenRing.rotation = Quaternion.identity;
                queenRing.localScale = Local(baseR * 1.5f * (1f + 0.08f * beat));
                Add(crownLit, QueenLit, 0.8f + 0.2f * beat);
                Add(crownDim, QueenDim, 0.8f);
                Add(queenRingMat, QueenRing, 0.55f + 0.4f * beat);
            }
        }

        // the parts hang under the hero, whose model may be scaled: set the world size through the parent's scale
        Vector3 Local(float s)
        {
            float k = 1f / Mathf.Max(0.01f, parent.lossyScale.x);
            return new Vector3(s * k, 1f, s * k);
        }

        // grows in with a small overshoot over 0.25 s
        static float Pop(float age)
        {
            float u = Mathf.Clamp01(age / 0.25f);
            return Mathf.Lerp(0.55f, 1f, u) + 0.15f * Mathf.Sin(u * Mathf.PI);
        }

        // two quick beats, then a rest, about 70 a minute: 0..1
        static float Heartbeat(float time)
        {
            float t = Mathf.Repeat(time, 0.85f);
            float a = Mathf.Exp(-Sq((t - 0.06f) / 0.05f)), b = 0.7f * Mathf.Exp(-Sq((t - 0.25f) / 0.05f));
            return Mathf.Max(a, b);
        }

        static float Sq(float x) => x * x;

        // additive pictures fade by their brightness
        static void Add(Material m, Color c, float a) => m.SetColor(ArtMaterials.BaseColor, new Color(c.r * a, c.g * a, c.b * a, 1f));

        // ---------------------------------------------------------------- building

        static Texture2D HexTex()
        {
            if (!texLoaded) { texLoaded = true; hexTex = Resources.Load<Texture2D>("BAMArt/Fx/T_BAM_HexShield"); }
            return hexTex;
        }

        static Material HexMat(int queue)
        {
            var m = FxAtlas.Material(ArtMaterials.UnlitAdd); // additive, weighed by the picture's alpha
            m.name = "BAM honey cells";
            m.SetTexture(ArtMaterials.BaseMap, HexTex());
            m.mainTexture = HexTex();
            m.renderQueue = queue;
            return m;
        }

        Transform Part(string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void BuildGuard()
        {
            guard = Part("HoneyGuard", CellMesh(Ring, Ring, guardMeshes, GuardBead), guardLit = HexMat(3052), guardDim = HexMat(3051));
            guardF = guard.GetComponent<MeshFilter>();
        }

        void BuildField()
        {
            field = Part("HoneyFieldShield", CellMesh(Ring, Ring, ringMeshes), fieldLit = HexMat(3052), fieldDim = HexMat(3051));
            fieldF = field.GetComponent<MeshFilter>();
        }

        void BuildQueen()
        {
            crown = Part("QueenCrown", CellMesh(Crown, Crown, crownMeshes), crownLit = HexMat(3062), crownDim = HexMat(3061));
            crownF = crown.GetComponent<MeshFilter>();
            queenRingMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            queenRing = Part("QueenRing", Shapes.RingMesh(0.9f, Pal.White, 48), queenRingMat);
        }

        /// <summary>count hex cells in a ring of radius 1 on the ground, the first lit of them (clockwise from the top, as seen
        /// from above) in submesh 0 and the rest in submesh 1; each cell is cut from the picture and points out from the middle.</summary>
        static Mesh CellMesh(int count, int lit, Mesh[] cache, float scale = 1f)
        {
            if (cache[lit] != null) return cache[lit];
            // a cell's corners in its own units: x along the ring, y out from the middle (the picture's cells point up)
            var hx = new[] { 0f, 1f, 1f, 0f, -1f, -1f };
            var hy = new[] { 1f, 0.5f, -0.5f, -1f, -0.5f, 0.5f };
            float size = Mathf.Sin(Mathf.PI / count) * 0.92f * scale; // at scale 1 the cells nearly touch
            var v = new Vector3[count * 7];
            var uv = new Vector2[v.Length];
            float cu = CellX / TexSize, cv = 1f - CellY / TexSize, hu = CellHalf / TexSize;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / count;
                Vector3 r = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), t = new Vector3(-r.z, 0f, r.x);
                int k = i * 7;
                v[k] = r; uv[k] = new Vector2(cu, cv);
                for (int c = 0; c < 6; c++)
                {
                    v[k + 1 + c] = r + (t * hx[c] + r * hy[c]) * size;
                    uv[k + 1 + c] = new Vector2(cu + hx[c] * hu, cv + hy[c] * hu);
                }
            }
            var m = new Mesh { name = "Honey cells " + count + "/" + lit, vertices = v, uv = uv, subMeshCount = 2 };
            m.SetTriangles(Fans(0, lit), 0);
            m.SetTriangles(Fans(lit, count), 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return cache[lit] = m;
        }

        /// <summary>One hex cell as CellMesh draws a ring's first (at radius 1 along +z, pointing out), alone: the piece that cracks off.</summary>
        static Mesh CellOnly()
        {
            if (cellOnly != null) return cellOnly;
            // as CellMesh's first cell (a = 90 degrees: r = +z, t = -x), built apart since CellMesh's meshes are not readable
            var hx = new[] { 0f, 1f, 1f, 0f, -1f, -1f };
            var hy = new[] { 1f, 0.5f, -0.5f, -1f, -0.5f, 0.5f };
            float size = Mathf.Sin(Mathf.PI / Ring) * 0.92f;
            Vector3 r = new Vector3(0f, 0f, 1f), t = new Vector3(-1f, 0f, 0f);
            float cu = CellX / TexSize, cv = 1f - CellY / TexSize, hu = CellHalf / TexSize;
            var v = new Vector3[7]; var uv = new Vector2[7];
            v[0] = r; uv[0] = new Vector2(cu, cv);
            for (int c = 0; c < 6; c++)
            {
                v[1 + c] = r + (t * hx[c] + r * hy[c]) * size;
                uv[1 + c] = new Vector2(cu + hx[c] * hu, cv + hy[c] * hu);
            }
            var m = new Mesh { name = "Honey cell crack", vertices = v, uv = uv };
            m.SetTriangles(Fans(0, 1), 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return cellOnly = m;
        }

        // the cells from..to as triangle fans, both windings: seen from above whichever way the ground plane faces
        static int[] Fans(int from, int to)
        {
            var tri = new int[(to - from) * 36];
            int q = 0;
            for (int i = from; i < to; i++)
            {
                int k = i * 7;
                for (int c = 0; c < 6; c++)
                {
                    int a = k + 1 + c, b = k + 1 + (c + 1) % 6;
                    tri[q++] = k; tri[q++] = a; tri[q++] = b;
                    tri[q++] = k; tri[q++] = b; tri[q++] = a;
                }
            }
            return tri;
        }
    }
}
