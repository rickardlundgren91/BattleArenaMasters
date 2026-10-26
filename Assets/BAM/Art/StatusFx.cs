using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>Status markers on one unit. Each marker is a child renderer made the first time it is needed and then only toggled.</summary>
    sealed class StatusFx
    {
        const int Stun = 0, Slow = 1, Root = 2, Burn = 3, Poison = 4, Shield = 5, Haste = 6, Illusion = 7, StunRing = 8, PoisonRing = 9, BurnRing = 10, Count = 11;
        // MLBB analysis rule 6: a ring in the status colour under every unit hit by a stun, poison or burn, as long as it lasts

        readonly Transform parent;
        readonly float radius, height;
        readonly Transform[] t = new Transform[Count];
        readonly MeshRenderer[] r = new MeshRenderer[Count];
        StatusFlags flags;
        bool visible = true;
        float emitTimer;
        float shieldT = -1f, ripple; // time since the shield came (for its pop-in), and a hit's wobble

        // Moa's honey shield (old View3DShields.cs): a slowly turning globe of honeycomb cells, additive honey over the hero
        static Mesh hexSphere;
        static Material hexMat;

        public StatusFx(Transform parent, float radius, float height)
        {
            this.parent = parent;
            this.radius = radius;
            this.height = height;
        }

        public bool Visible
        {
            set
            {
                if (visible == value) return;
                visible = value;
                Refresh();
            }
        }

        public void Set(StatusFlags f)
        {
            if (f == flags) return;
            // gain and loss come from the flags, not the renderer (off while hidden): stepping out of a bush is no new shield
            bool had = Has(flags, StatusFlags.Shielded), has = Has(f, StatusFlags.Shielded);
            flags = f;
            Refresh();
            if (!visible || had == has) return;
            if (has)
            {
                shieldT = 0f;
                Vector3 c = parent.position + new Vector3(0f, height * 0.5f, 0f);
                Fx.Flash(c, 0.3f, 1.1f, new Color(1f, 0.85f, 0.35f, 0.8f), 0.12f);
                Fx.Glow(c, 8, Pal.Get(Pal.Honey), 0.2f, 1.6f, 0.35f, 0.4f, 0.3f);
            }
            else Fx.Play(FxKind.ShieldPop, parent.position, 1f, 0);
        }

        static bool Has(StatusFlags f, StatusFlags bit) => (f & bit) != 0;

        void Refresh()
        {
            Show(Stun, Has(flags, StatusFlags.Stunned));
            Show(Slow, Has(flags, StatusFlags.Slowed));
            Show(Root, Has(flags, StatusFlags.Rooted));
            Show(Burn, Has(flags, StatusFlags.Burning));
            Show(Poison, Has(flags, StatusFlags.Poisoned));
            Show(Shield, Has(flags, StatusFlags.Shielded));
            Show(Haste, Has(flags, StatusFlags.Hasted));
            Show(Illusion, Has(flags, StatusFlags.Illusion));
            Show(StunRing, Has(flags, StatusFlags.Stunned));
            Show(PoisonRing, Has(flags, StatusFlags.Poisoned));
            Show(BurnRing, Has(flags, StatusFlags.Burning) && !Has(flags, StatusFlags.Poisoned));
        }

        void Show(int i, bool on)
        {
            on &= visible;
            if (r[i] == null)
            {
                if (!on) return;
                Make(i);
            }
            if (r[i].enabled != on) r[i].enabled = on;
        }

        void Make(int i)
        {
            Mesh mesh; Material mat; Vector3 pos; float s;
            float rr = Mathf.Max(radius, 0.25f) * 1.3f;
            switch (i)
            {
                case Stun: mesh = Shapes.StunStars(); mat = ArtMaterials.Unlit; pos = new Vector3(0f, height + 0.12f, 0f); s = Mathf.Max(1f, radius * 2.5f); break;
                case Slow: mesh = Shapes.SlowRing(); mat = ArtMaterials.UnlitFade; pos = new Vector3(0f, 0.03f, 0f); s = rr; break;
                case Root: mesh = Shapes.RootVines(); mat = ArtMaterials.Lit; pos = Vector3.zero; s = rr; break;
                case Burn: mesh = Shapes.Flames(); mat = ArtMaterials.UnlitAdd; pos = Vector3.zero; s = rr * 0.8f; break;
                case Poison: mesh = Shapes.PoisonBubbles(); mat = ArtMaterials.Unlit; pos = new Vector3(0.18f, height + 0.05f, 0f); s = 1f; break;
                case Shield: mesh = HexSphere(); mat = HexMat(); pos = new Vector3(0f, height * 0.5f, 0f); s = Mathf.Max(radius * 1.6f, height * 0.62f); break;
                case Haste: mesh = Shapes.HasteLines(); mat = ArtMaterials.UnlitAdd; pos = Vector3.zero; s = Mathf.Max(1f, radius * 2.2f); break;
                case StunRing: mesh = Shapes.RingMesh(0.82f, Pal.Stun); mat = ArtMaterials.UnlitAdd; pos = new Vector3(0f, 0.035f, 0f); s = rr * 1.05f; break;
                case PoisonRing: mesh = Shapes.RingMesh(0.82f, Pal.Poison); mat = ArtMaterials.UnlitAdd; pos = new Vector3(0f, 0.035f, 0f); s = rr * 1.05f; break;
                case BurnRing: mesh = Shapes.RingMesh(0.82f, Pal.Burn); mat = ArtMaterials.UnlitAdd; pos = new Vector3(0f, 0.035f, 0f); s = rr * 1.05f; break;
                default: mesh = Shapes.RingMesh(0.85f, Pal.Violet); mat = ArtMaterials.UnlitAdd; pos = new Vector3(0f, 0.04f, 0f); s = rr * 1.15f; break;
            }
            var go = new GameObject("Status" + i);
            t[i] = go.transform;
            t[i].SetParent(parent, false);
            t[i].localPosition = pos;
            t[i].localScale = new Vector3(s, s, s);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r[i] = go.AddComponent<MeshRenderer>();
            r[i].sharedMaterial = mat;
            r[i].shadowCastingMode = ShadowCastingMode.Off;
            r[i].receiveShadows = false;
        }

        /// <summary>A hit on the shield makes it wobble for a moment.</summary>
        public void Ripple() { if (r[Shield] != null && r[Shield].enabled) ripple = 1f; }

        static Mesh HexSphere()
        {
            if (hexSphere != null) return hexSphere;
            const int Lon = 24, Lat = 12;
            var v = new Vector3[(Lon + 1) * (Lat + 1)];
            var uv = new Vector2[v.Length];
            var n = new Vector3[v.Length];
            for (int j = 0; j <= Lat; j++)
                for (int i = 0; i <= Lon; i++)
                {
                    float a = i * Mathf.PI * 2f / Lon, e = -Mathf.PI * 0.5f + j * Mathf.PI / Lat;
                    var d = new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a));
                    int k = j * (Lon + 1) + i;
                    v[k] = d; n[k] = d;
                    uv[k] = new Vector2(i * 4f / Lon, j * 2.3f / Lat); // Rickard's picture holds ~6 cells across; about square round the middle
                }
            var tri = new int[Lon * Lat * 6];
            int q = 0;
            for (int j = 0; j < Lat; j++)
                for (int i = 0; i < Lon; i++)
                {
                    int a0 = j * (Lon + 1) + i, a1 = a0 + 1, b0 = a0 + Lon + 1, b1 = b0 + 1;
                    tri[q++] = a0; tri[q++] = b0; tri[q++] = b1; tri[q++] = a0; tri[q++] = b1; tri[q++] = a1;
                }
            hexSphere = new Mesh { name = "Hex shield", vertices = v, normals = n, uv = uv, triangles = tri };
            hexSphere.RecalculateBounds();
            hexSphere.UploadMeshData(true);
            return hexSphere;
        }

        static Material HexMat()
        {
            if (hexMat != null) return hexMat;
            var tex = Resources.Load<Texture2D>("BAMArt/Fx/T_BAM_HexShield");
            if (tex == null) return hexMat = ArtMaterials.UnlitFade; // no picture: the old plain bubble colour
            tex.wrapMode = TextureWrapMode.Repeat;
            hexMat = FxAtlas.Material(ArtMaterials.UnlitAdd); // additive, weighed by the picture's alpha
            hexMat.name = "BAM hex shield";
            hexMat.SetTexture(ArtMaterials.BaseMap, tex);
            hexMat.mainTexture = tex;
            hexMat.SetColor(ArtMaterials.BaseColor, new Color(1f, 0.72f, 0.2f, 0.75f));
            return hexMat;
        }

        public void Tick(float dt, float time)
        {
            if (flags == StatusFlags.None || !visible) return;
            if (r[Stun] != null && r[Stun].enabled) t[Stun].localRotation = Quaternion.Euler(0f, time * 240f, 0f);
            if (r[Burn] != null && r[Burn].enabled)
            {
                float f = 0.85f + Mathf.Sin(time * 23f) * 0.1f + Mathf.Sin(time * 37f) * 0.06f;
                Vector3 s = t[Burn].localScale;
                t[Burn].localScale = new Vector3(s.x, s.x * f, s.x);
                t[Burn].localRotation = Quaternion.Euler(0f, time * 90f, 0f);
            }
            if (r[Poison] != null && r[Poison].enabled) t[Poison].localPosition = new Vector3(0.18f, height + 0.05f + Mathf.Sin(time * 4f) * 0.05f, 0f);
            if (r[Shield] != null && r[Shield].enabled)
            {
                float k = 1f + Mathf.Sin(time * 5f) * 0.03f + Mathf.Sin(time * 40f) * 0.06f * ripple;
                if (shieldT >= 0f)
                {
                    // pops in with a little overshoot
                    shieldT += dt;
                    float u = Mathf.Clamp01(shieldT / 0.3f);
                    k *= Mathf.Lerp(0.6f, 1f, u) + 0.15f * Mathf.Sin(u * Mathf.PI);
                    if (u >= 1f) shieldT = -1f;
                }
                ripple = Mathf.MoveTowards(ripple, 0f, dt * 4f);
                float b = Mathf.Max(radius * 1.6f, height * 0.62f) * k;
                t[Shield].localScale = new Vector3(b, b, b);
                t[Shield].localRotation = Quaternion.Euler(0f, time * 20f, 0f);
            }
            float ringK = Mathf.Max(radius, 0.25f) * 1.3f * 1.05f * (1f + Mathf.Sin(time * 6f) * 0.06f);
            for (int i = StunRing; i <= BurnRing; i++)
                if (r[i] != null && r[i].enabled) t[i].localScale = new Vector3(ringK, 1f, ringK);
            if (r[Illusion] != null && r[Illusion].enabled) t[Illusion].localRotation = Quaternion.Euler(0f, time * 60f, 0f);
            if (r[Root] != null && r[Root].enabled) t[Root].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 3f) * 6f, 0f);

            // a few world particles for the statuses that should feel alive
            emitTimer -= dt;
            if (emitTimer > 0f) return;
            emitTimer = Fx.Amount >= 1f ? 0.12f : 0.25f;
            Vector3 p = parent.position;
            if (Has(flags, StatusFlags.Burning)) Fx.Flames(p + new Vector3(0f, height * 0.4f, 0f), 2, Pal.Get(Pal.Fire), 0.3f, 0.3f, 0.5f, 1.2f, radius * 0.6f);
            if (Has(flags, StatusFlags.Poisoned)) Fx.Bubbles(p + new Vector3(0f, height * 0.6f, 0f), 1, Pal.Get(Pal.Poison), 0.14f, 0.2f, 0.6f, 0.6f, radius * 0.5f);
            if (Has(flags, StatusFlags.Hasted)) Fx.Glow(p + new Vector3(0f, 0.2f, 0f), 1, Pal.Get(Pal.White), 0.16f, 0.1f, 0.35f, 0.2f, radius * 0.6f);
            if (Has(flags, StatusFlags.Slowed)) Fx.Glow(p + new Vector3(0f, 0.3f, 0f), 1, Pal.Get(Pal.Slow), 0.12f, 0.2f, 0.5f, -0.2f, radius * 0.8f);
        }
    }
}
