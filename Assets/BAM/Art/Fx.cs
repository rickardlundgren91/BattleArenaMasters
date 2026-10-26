using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Runs once per frame before the views: caches the camera, ticks the one-shot effects. Lives on the hidden
    /// "BAM.Art" object that also parks pooled views.
    /// </summary>
    sealed class ArtDriver : MonoBehaviour
    {
        public static Transform CamT;
        public static Quaternion CamRot = Quaternion.Euler(57f, 0f, 0f);

        void Update()
        {
            Camera c = Camera.main;
            if (c != null) { CamT = c.transform; CamRot = CamT.rotation; }
            Fx.Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// BAM's own effect pictures (Resources/BAMArt/Fx/T_BAM_FxAtlas, painted by bam-work/art/fx/make_atlas.py): 16 white sprites in a
    /// 4x4 grid that the particles and ground decals tint. Rickard can repaint any cell; the cell order is fixed (Cell below).
    /// Without the picture every effect falls back to the round dot and the mesh rings.
    /// </summary>
    static class FxAtlas
    {
        public const int Glow = 0, Star = 1, Streak = 2, Flame = 3, Smoke = 4, Shockwave = 5, Slash = 6, Drop = 7,
            Bubble = 8, Wisp = 9, Chunk = 10, Leaf = 11, Dust = 12, Crack = 13, Burst = 14, Dot = 15;
        static bool loaded;
        static Texture2D tex;
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend"), DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly Mesh[] quads = new Mesh[16];
        static Mesh slash;

        public static Texture2D Texture
        {
            get
            {
                if (!loaded) { loaded = true; tex = Resources.Load<Texture2D>("BAMArt/Fx/T_BAM_FxAtlas"); }
                return tex;
            }
        }

        static Vector2 Uv(int cell, float u, float v) => new Vector2(((cell & 3) + u) * 0.25f, (3 - (cell >> 2) + v) * 0.25f);

        /// <summary>A flat unit quad on the ground (x, z from -0.5 to 0.5) showing one cell, picture top toward +z.</summary>
        public static Mesh Quad(int cell)
        {
            if (quads[cell] != null) return quads[cell];
            quads[cell] = Flat("Fx " + cell, cell, -0.5f, 0.5f, -0.5f, 0.5f);
            return quads[cell];
        }

        static Mesh full;

        /// <summary>A flat unit quad on the ground showing a whole picture (uv 0..1).</summary>
        public static Mesh FullQuad()
        {
            if (full != null) return full;
            full = new Mesh { name = "Fx full quad" };
            full.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            full.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            full.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            full.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            full.RecalculateBounds();
            full.UploadMeshData(true);
            return full;
        }

        /// <summary>The sword arc: the crescent cell sized so its blade runs at radius 0.5 in front of the origin, as Shapes.Slash.</summary>
        public static Mesh SlashQuad(bool mirror = false)
        {
            if (mirror) { if (slashM == null) slashM = Flat("Fx slash (mirrored)", Slash, -0.714f, 0.714f, -0.714f, 0.714f, true); return slashM; }
            if (slash == null) slash = Flat("Fx slash", Slash, -0.714f, 0.714f, -0.714f, 0.714f);
            return slash;
        }

        static Mesh slashM;

        static Mesh Flat(string name, int cell, float x0, float x1, float z0, float z1, bool mirrorU = false)
        {
            var m = new Mesh { name = name };
            float u0 = mirrorU ? 0.996f : 0.004f, u1 = mirrorU ? 0.004f : 0.996f; // mirrored picture, same winding (still faces up)
            m.vertices = new[] { new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0) };
            m.uv = new[] { Uv(cell, u0, 0.004f), Uv(cell, u0, 0.996f), Uv(cell, u1, 0.996f), Uv(cell, u1, 0.004f) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }

        /// <summary>A private copy of mat drawing the atlas.</summary>
        public static Material Material(Material mat)
        {
            var m = ArtMaterials.Instance(mat);
            m.SetTexture(ArtMaterials.BaseMap, Texture);
            m.mainTexture = Texture;
            // the sprites are white with an alpha shape: additive must weigh by alpha, or each cell shows as a square
            if (m.HasProperty(DstBlend) && Mathf.Approximately(m.GetFloat(DstBlend), (float)UnityEngine.Rendering.BlendMode.One))
                m.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            return m;
        }
    }

    /// <summary>
    /// Rickard's own effect pictures from his Speltest project (Resources/BAMArt/Fx/tx_fx_*: white on black, drawn additive). They
    /// win over the painted atlas wherever one exists: glow, star, streak, the flash and shockwave flipbooks (4x4 frames played
    /// over each particle's life), ground crack and swirl, light pillar, shard rays and the nova ring.
    /// </summary>
    static class RickardFx
    {
        public static Texture2D Load(string name) => Resources.Load<Texture2D>("BAMArt/Fx/" + name);

        /// <summary>A private additive particle material drawing tex.</summary>
        public static Material Additive(Texture2D tex)
        {
            var m = ArtMaterials.Instance(ArtMaterials.ParticleAdd);
            m.SetTexture(ArtMaterials.BaseMap, tex);
            m.mainTexture = tex;
            return m;
        }
    }

    /// <summary>Particles (a few shared systems, so all sparks of the match cost four draw calls) and pooled rings/flashes.</summary>
    static class Fx
    {
        static ParticleSystem glow, spark, smoke, shard;
        // the atlas sprites (null without the picture: their calls then fall back to glow / smoke)
        static ParticleSystem star, flame, burst, wisp, dust, drop, bubble, leaf, chunk;
        // Rickard's pictures: ground-flat rings, cracks and swirls, the light pillar, shard rays and the nova (null when missing)
        static ParticleSystem shock, crackPs, swirl, pillar, rays, nova;
        static ParticleSystem.EmitParams ep;
        public static float Amount = 1f; // 1 on High, about half on Low

        sealed class Blob
        {
            public Transform T;
            public MeshRenderer R;
            public MeshFilter F;
            public Material Mat, PalMat, AddMat, FadeMat;
            public float Age, Life, S0, S1, A0;
            public Color Col;
            public bool Flat;
        }

        static readonly List<Blob> live = new List<Blob>(32);
        static readonly Stack<Blob> free = new Stack<Blob>(32);
        static Transform root;

        public static void Init(Transform parent)
        {
            root = parent;
            bool atlas = FxAtlas.Texture != null;
            glow = MakeSystem("BAM Glow", ArtMaterials.ParticleAdd, 0f, 1f, 0.2f, 800, atlas ? FxAtlas.Glow : -1);
            spark = MakeSystem("BAM Sparks", ArtMaterials.ParticleAdd, 1.6f, 1f, 0.4f, 600, atlas ? FxAtlas.Streak : -1);
            smoke = MakeSystem("BAM Smoke", ArtMaterials.ParticleAlpha, -0.05f, 0.6f, 1.8f, 300, atlas ? FxAtlas.Smoke : -1);
            if (atlas)
            {
                // sparks fly as streaks along their motion
                var sr0 = spark.GetComponent<ParticleSystemRenderer>();
                sr0.renderMode = ParticleSystemRenderMode.Stretch;
                sr0.velocityScale = 0.06f;
                sr0.lengthScale = 2.2f;
                star = MakeSystem("BAM Stars", ArtMaterials.ParticleAdd, 0f, 0.4f, 1f, 200, FxAtlas.Star);
                flame = MakeSystem("BAM Flames", ArtMaterials.ParticleAdd, -0.25f, 0.8f, 0.3f, 400, FxAtlas.Flame);
                burst = MakeSystem("BAM Bursts", ArtMaterials.ParticleAdd, 0f, 0.5f, 1.15f, 120, FxAtlas.Burst);
                wisp = MakeSystem("BAM Wisps", ArtMaterials.ParticleAdd, -0.2f, 0.6f, 1.2f, 200, FxAtlas.Wisp);
                dust = MakeSystem("BAM Dust", ArtMaterials.ParticleAlpha, -0.02f, 0.5f, 1.6f, 200, FxAtlas.Dust);
                drop = MakeSystem("BAM Drops", ArtMaterials.ParticleAlpha, 1.4f, 1f, 0.7f, 300, FxAtlas.Drop);
                bubble = MakeSystem("BAM Bubbles", ArtMaterials.ParticleAlpha, -0.15f, 0.5f, 1.2f, 300, FxAtlas.Bubble);
                leaf = MakeSystem("BAM Leaves", ArtMaterials.ParticleAlpha, 0.35f, 1f, 0.8f, 150, FxAtlas.Leaf);
                chunk = MakeSystem("BAM Chunks", ArtMaterials.ParticleAlpha, 2.4f, 1f, 0.8f, 200, FxAtlas.Chunk);
                ParticleSystem[] spinning = { star, flame, burst, wisp, dust, leaf, chunk, smoke };
                for (int i = 0; i < spinning.Length; i++)
                {
                    var spinRot = spinning[i].rotationOverLifetime;
                    spinRot.enabled = true;
                    spinRot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
                }
            }
            RickardSystems();
            var shardMat = ArtMaterials.Instance(ArtMaterials.ParticleAlpha);
            shardMat.SetTexture(ArtMaterials.BaseMap, Texture2D.whiteTexture);
            shardMat.mainTexture = Texture2D.whiteTexture;
            shard = MakeSystem("BAM Shards", shardMat, 2.6f, 1f, 0.6f, 300);
            var sr = shard.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Mesh;
            sr.mesh = Shapes.Cube();
            var main = shard.main;
            main.startRotation3D = true;
            var rot = shard.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            for (int i = 0; i < 24; i++) free.Push(NewBurst());
        }

        static void RickardSystems()
        {
            Texture2D t;
            if ((t = RickardFx.Load("tx_fx_glow_soft")) != null) Retexture(glow, t, 1, 1, 0, false);
            if ((t = RickardFx.Load("tx_fx_streak")) != null)
            {
                Retexture(spark, t, 1, 1, 0, false);
                var r = spark.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 2.2f;
            }
            if ((t = RickardFx.Load("tx_fx_particles")) != null)
            {
                if (star == null) star = MakeSystem("BAM Stars", ArtMaterials.ParticleAdd, 0f, 0.4f, 1f, 200);
                Retexture(star, t, 2, 2, 1, false); // his 2x2 sheet: dot, star, diamond, spark; the star
            }
            if ((t = RickardFx.Load("tx_fx_flash_burst")) != null)
            {
                if (burst == null) burst = MakeSystem("BAM Bursts", ArtMaterials.ParticleAdd, 0f, 1f, 1f, 120);
                Retexture(burst, t, 4, 4, 0, true);
                SizeCurve(burst, 0.9f, 1.15f);
            }
            if ((t = RickardFx.Load("tx_fx_shockwave")) != null)
            {
                // the flipbook grows the ring itself: the particle keeps one size, the frames do the rest
                shock = Flat(MakeSystem("BAM Shockwaves", ArtMaterials.ParticleAdd, 0f, 1f, 1f, 80));
                Retexture(shock, t, 4, 4, 0, true);
            }
            if ((t = RickardFx.Load("tx_fx_ground_crack")) != null)
            {
                crackPs = Flat(MakeSystem("BAM Cracks", ArtMaterials.ParticleAdd, 0f, 0.9f, 1f, 40));
                Retexture(crackPs, t, 1, 1, 0, false);
            }
            if ((t = RickardFx.Load("tx_fx_ground_swirl")) != null)
            {
                swirl = Flat(MakeSystem("BAM Swirls", ArtMaterials.ParticleAdd, 0f, 0.6f, 1.1f, 40));
                Retexture(swirl, t, 1, 1, 0, false);
                var rot = swirl.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(4f);
            }
            if ((t = RickardFx.Load("tx_fx_light_pillar")) != null)
            {
                pillar = MakeSystem("BAM Pillars", ArtMaterials.ParticleAdd, 0f, 1f, 1f, 40);
                Retexture(pillar, t, 1, 1, 0, false);
                var r = pillar.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                var main = pillar.main;
                main.startSize3D = true;
            }
            if ((t = RickardFx.Load("tx_fx_shards_burst")) != null)
            {
                rays = MakeSystem("BAM Rays", ArtMaterials.ParticleAdd, 0f, 0.6f, 1.2f, 60);
                Retexture(rays, t, 1, 1, 0, false);
            }
            if ((t = RickardFx.Load("tx_fx_nova")) != null)
            {
                nova = Flat(MakeSystem("BAM Novas", ArtMaterials.ParticleAdd, 0f, 0.35f, 1.1f, 30));
                Retexture(nova, t, 1, 1, 0, false);
            }
        }

        static ParticleSystem Flat(ParticleSystem ps)
        {
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return ps;
        }

        static void SizeCurve(ParticleSystem ps, float a, float b)
        {
            var size = ps.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, a, 1f, b));
        }

        // one picture (or a grid of tiles: one fixed tile, or every tile in turn over the particle's life)
        static void Retexture(ParticleSystem ps, Texture2D tex, int tx, int ty, int cell, bool animate)
        {
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = RickardFx.Additive(tex);
            var tsa = ps.textureSheetAnimation;
            if (tx * ty <= 1) { tsa.enabled = false; return; }
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = tx;
            tsa.numTilesY = ty;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f);
            tsa.cycleCount = 1;
            tsa.frameOverTime = animate ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 0.999f))
                : new ParticleSystem.MinMaxCurve((cell + 0.5f) / (tx * ty));
        }

        static ParticleSystem MakeSystem(string name, Material mat, float gravity, float sizeStart, float sizeEnd, int max, int cell = -1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.startLifetime = 0.6f;
            main.startSize = 0.2f;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, sizeStart, 1f, sizeEnd));
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (cell >= 0)
            {
                // one fixed cell of the atlas
                mat = FxAtlas.Material(mat);
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Grid;
                tsa.numTilesX = 4;
                tsa.numTilesY = 4;
                tsa.animation = ParticleSystemAnimationType.WholeSheet;
                // a constant frame: the curve is the share of the whole sheet (cell 0 is the top left, rows run down)
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve((cell + 0.5f) / 16f);
                tsa.startFrame = new ParticleSystem.MinMaxCurve(0f);
                tsa.cycleCount = 1;
            }
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0f;
            ps.Play();
            return ps;
        }

        static int Count(int n) => Mathf.Max(1, Mathf.RoundToInt(n * Amount));

        /// <summary>Particles thrown out from pos: speed in units/s, up adds an upward push, spread 0..1 flattens to the ground plane.</summary>
        public static void Burst(ParticleSystem ps, Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f)
        {
            if (ps == null) return;
            n = Count(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Random.insideUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.6f;
                ep.position = pos + Random.insideUnitSphere * jitter;
                ep.velocity = d * speed + Vector3.up * up;
                ep.startColor = c;
                ep.startSize = size * Random.Range(0.7f, 1.3f);
                ep.startLifetime = life * Random.Range(0.75f, 1.25f);
                if (ps == shard) ep.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                else ep.rotation = ps == spark || ps == drop || ps == bubble ? 0f : Random.value * 360f;
                ps.Emit(ep, 1);
            }
            ep.rotation3D = Vector3.zero;
            ep.rotation = 0f;
        }

        /// <summary>Streak sparks sprayed forward along dir within +-spread degrees: a melee hit bursts out behind its target.</summary>
        public static void SparksAlong(Vector3 pos, Vector3 dir, int n, Color32 c, float size, float speed, float life, float spread = 35f)
        {
            if (spark == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            n = Count(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = Quaternion.Euler(Random.Range(-10f, 25f), Random.Range(-spread, spread), 0f) * Vector3.forward;
                d = Quaternion.LookRotation(dir) * new Vector3(d.x, -d.y, d.z); // a little up, never into the ground
                ep.position = pos;
                ep.velocity = d * speed * Random.Range(0.7f, 1.2f);
                ep.startColor = c;
                ep.startSize = size * Random.Range(0.7f, 1.3f);
                ep.startLifetime = life * Random.Range(0.75f, 1.25f);
                ep.rotation = 0f;
                spark.Emit(ep, 1);
            }
        }

        public static void Glow(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(glow, pos, n, c, size, speed, life, up, jitter);
        public static void Sparks(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(spark, pos, n, c, size, speed, life, up, jitter);
        public static void Smoke(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(smoke, pos, n, c, size, speed, life, up, jitter);
        public static void Shards(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(shard, pos, n, c, size, speed, life, up, jitter);
        public static void Stars(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(star ?? glow, pos, n, c, size, speed, life, up, jitter);
        public static void Flames(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(flame ?? glow, pos, n, c, size, speed, life, up, jitter);
        /// <summary>A comic impact star at pos (one or two are plenty).</summary>
        public static void Pow(Vector3 pos, int n, Color32 c, float size, float life) => Burst(burst ?? glow, pos, n, c, size, 0f, life);
        public static void Wisps(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(wisp ?? smoke, pos, n, c, size, speed, life, up, jitter);
        public static void Dust(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(dust ?? smoke, pos, n, c, size, speed, life, up, jitter);
        public static void Drops(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(drop ?? glow, pos, n, c, size, speed, life, up, jitter);
        public static void Bubbles(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(bubble ?? glow, pos, n, c, size, speed, life, up, jitter);
        public static void Leaves(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(leaf ?? smoke, pos, n, c, size, speed, life, up, jitter);
        public static void Chunks(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(chunk ?? shard, pos, n, c, size, speed, life, up, jitter);

        // ---------------------------------------------------------------- rings and flashes

        static Blob NewBurst()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            go.SetActive(false);
            var bu = new Blob { T = go.transform, F = go.AddComponent<MeshFilter>(), R = go.AddComponent<MeshRenderer>() };
            bu.PalMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            if (FxAtlas.Texture != null) { bu.AddMat = FxAtlas.Material(ArtMaterials.UnlitAdd); bu.FadeMat = FxAtlas.Material(ArtMaterials.UnlitFade); }
            bu.Mat = bu.PalMat;
            bu.R.sharedMaterial = bu.Mat;
            bu.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bu.R.receiveShadows = false;
            return bu;
        }

        /// <summary>An expanding ring on the ground (flat) or a swelling ball (flash). Radii in units.</summary>
        public static void Ring(Vector3 pos, float r0, float r1, Color c, float life, float thickness = 0.8f)
        {
            if (shock != null)
            {
                // his flipbook's last ring reaches about 0.9 of the frame: one particle sized so it ends at r1
                Emit1(shock, new Vector3(pos.x, 0.06f, pos.z), c, r1 * 2.2f, life * 1.3f);
                return;
            }
            // the painted shockwave (its rim sits at 0.82 of the cell's half width: sized so the rim lands on the radius)
            if (FxAtlas.Texture != null) Spawn(FxAtlas.Quad(FxAtlas.Shockwave), pos, r0 * 2.44f, r1 * 2.44f, c, life, true, 1);
            else Spawn(Shapes.RingMesh(thickness, Pal.White), pos, r0, r1, c, life, true, 0);
        }

        public static void Flash(Vector3 pos, float r0, float r1, Color c, float life) => Spawn(Shapes.Ball(), pos, r0, r1, c, life, false, 0);

        /// <summary>Cracks in the ground that fade (heavy landings, explosions). Radius in units.</summary>
        public static void Crack(Vector3 pos, float radius, float life, float alpha = 0.7f)
        {
            if (crackPs != null)
            {
                Emit1(crackPs, new Vector3(pos.x, 0.03f, pos.z), new Color(1f, 0.75f, 0.45f, alpha), radius * 2.2f, life, Random.value * 360f);
                return;
            }
            if (FxAtlas.Texture == null) return;
            Spawn(FxAtlas.Quad(FxAtlas.Crack), pos, radius * 2f, radius * 2.1f, new Color(1f, 1f, 1f, alpha), life, true, 2);
            live[live.Count - 1].T.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        }

        /// <summary>A magic swirl turning on the ground (casts). Radius in units.</summary>
        public static void Swirl(Vector3 pos, float radius, Color c, float life)
        {
            if (swirl != null) Emit1(swirl, new Vector3(pos.x, 0.05f, pos.z), c, radius * 2f, life, Random.value * 360f);
            else Ring(pos, radius * 0.4f, radius, c, life);
        }

        /// <summary>A column of light rising from pos (respawn, level up, summons).</summary>
        public static void Pillar(Vector3 pos, float width, float height, Color c, float life)
        {
            if (pillar == null) { Glow(pos + Vector3.up * 0.2f, 8, c, width * 0.4f, 0.4f, life, height * 2f, width * 0.3f); return; }
            ep.position = pos + Vector3.up * (height * 0.5f);
            ep.velocity = Vector3.zero;
            ep.startColor = c;
            ep.startSize3D = new Vector3(width, height, 1f);
            ep.startLifetime = life;
            ep.rotation = 0f;
            pillar.Emit(ep, 1);
            ep.ResetStartSize();
        }

        /// <summary>Sharp light rays bursting out (big hits).</summary>
        public static void Rays(Vector3 pos, Color c, float size, float life)
        {
            if (rays != null) Emit1(rays, pos, c, size, life, Random.value * 360f);
            else Pow(pos, 1, c, size, life);
        }

        /// <summary>A flat spiky nova ring on the ground (explosions).</summary>
        public static void Nova(Vector3 pos, Color c, float radius, float life)
        {
            if (nova != null) Emit1(nova, new Vector3(pos.x, 0.07f, pos.z), c, radius * 2f, life, Random.value * 360f);
            else Ring(pos, radius * 0.3f, radius, c, life);
        }

        static void Emit1(ParticleSystem ps, Vector3 pos, Color c, float size, float life, float rotation = 0f)
        {
            ep.position = pos;
            ep.velocity = Vector3.zero;
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = life;
            ep.rotation = rotation;
            ps.Emit(ep, 1);
            ep.rotation = 0f;
        }

        // matKind: 0 palette (meshes), 1 atlas additive, 2 atlas alpha
        static void Spawn(Mesh mesh, Vector3 pos, float r0, float r1, Color c, float life, bool flat, int matKind)
        {
            Blob bu = free.Count > 0 ? free.Pop() : NewBurst();
            bu.Mat = matKind == 1 && bu.AddMat != null ? bu.AddMat : matKind == 2 && bu.FadeMat != null ? bu.FadeMat : bu.PalMat;
            bu.R.sharedMaterial = bu.Mat;
            bu.T.rotation = Quaternion.identity;
            bu.F.sharedMesh = mesh;
            bu.T.position = flat ? new Vector3(pos.x, 0.05f, pos.z) : pos;
            bu.Age = 0f; bu.Life = Mathf.Max(0.05f, life); bu.S0 = r0; bu.S1 = r1; bu.Col = c; bu.A0 = c.a; bu.Flat = flat;
            Apply(bu, 0f);
            bu.T.gameObject.SetActive(true);
            live.Add(bu);
        }

        static void Apply(Blob bu, float t)
        {
            float e = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(bu.S0, bu.S1, e);
            bu.T.localScale = bu.Flat ? new Vector3(s, 1f, s) : new Vector3(s, s, s);
            Color c = bu.Col;
            c.a = bu.A0 * (1f - t);
            bu.Mat.SetColor(ArtMaterials.BaseColor, c);
        }

        public static void Tick(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Blob bu = live[i];
                bu.Age += dt;
                if (bu.Age >= bu.Life)
                {
                    bu.T.gameObject.SetActive(false);
                    live[i] = live[live.Count - 1];
                    live.RemoveAt(live.Count - 1);
                    free.Push(bu);
                    continue;
                }
                Apply(bu, bu.Age / bu.Life);
            }
        }

        /// <summary>Removes every running effect (match end).</summary>
        public static void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--) { live[i].T.gameObject.SetActive(false); free.Push(live[i]); }
            live.Clear();
            if (glow != null) { glow.Clear(); spark.Clear(); smoke.Clear(); shard.Clear(); }
            ParticleSystem[] extra = { star, flame, burst, wisp, dust, drop, bubble, leaf, chunk, shock, crackPs, swirl, pillar, rays, nova };
            for (int i = 0; i < extra.Length; i++) if (extra[i] != null) extra[i].Clear();
        }

        // ---------------------------------------------------------------- the FxKind catalogue

        static Color32 C(byte pal) => Pal.Get(pal);
        static Color32 C(byte pal, byte a) { Color32 c = Pal.Get(pal); c.a = a; return c; }

        public static void Play(FxKind kind, Vector3 p, float size, int team)
        {
            Color32 teamC = C(Pal.Team(team)), teamL = C(Pal.TeamLight(team));
            Vector3 mid = p + Vector3.up * 0.5f;
            switch (kind)
            {
                case FxKind.Hit:
                    Pow(mid, 1, C(Pal.Cream, 230), 0.55f * size, 0.14f);
                    Sparks(mid, 6, C(Pal.FireCore), 0.12f * size, 3f, 0.25f, 1f);
                    Stars(mid, 1, C(Pal.White), 0.35f * size, 0.5f, 0.2f);
                    break;
                case FxKind.HitBig:
                    Pow(mid, 1, C(Pal.FireCore), 0.9f * size, 0.18f);
                    Rays(mid, new Color(1f, 0.9f, 0.6f, 1f), 1.3f * size, 0.2f);
                    Sparks(mid, 12, C(Pal.Fire), 0.16f * size, 4.5f, 0.35f, 1.5f);
                    Stars(mid, 2, C(Pal.White), 0.45f * size, 1.5f, 0.25f, 1f);
                    Chunks(p + Vector3.up * 0.2f, 3, C(Pal.Rock), 0.14f * size, 2.5f, 0.6f, 2f);
                    Ring(p, 0.2f * size, 0.9f * size, new Color(1f, 0.8f, 0.4f, 0.8f), 0.25f);
                    break;
                case FxKind.Death:
                    Pow(mid, 1, C(Pal.White, 200), 0.8f * size, 0.16f);
                    Smoke(mid, 10, C(Pal.Cloth, 200), 0.45f * size, 1.2f, 0.8f, 0.6f, 0.2f);
                    Wisps(mid, 3, teamL, 0.6f * size, 0.4f, 0.9f, 1.4f, 0.2f);
                    Shards(mid, 10, teamC, 0.1f * size, 3f, 0.8f, 2.5f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 1f, 1f, 0.7f), 0.4f);
                    break;
                case FxKind.Respawn:
                    Pillar(p, 1.2f * size, 3f * size, (Color)teamL, 0.7f);
                    Glow(p + Vector3.up * 0.1f, 18, teamL, 0.22f * size, 0.6f, 0.8f, 2.4f, 0.35f);
                    Stars(p + Vector3.up * 0.6f, 6, C(Pal.White), 0.3f * size, 0.8f, 0.6f, 1.6f, 0.4f);
                    Ring(p, 0.2f * size, 1.2f * size, (Color)teamL, 0.5f);
                    Flash(mid, 0.3f * size, 0.8f * size, new Color(1f, 1f, 1f, 0.6f), 0.25f);
                    break;
                case FxKind.LevelUp:
                    Pillar(p, 1.1f * size, 2.6f * size, new Color(1f, 0.85f, 0.3f, 1f), 0.8f);
                    Glow(p + Vector3.up * 0.2f, 16, C(Pal.Gold), 0.2f * size, 0.8f, 0.9f, 2.2f, 0.4f);
                    Stars(p + Vector3.up * 0.8f, 8, C(Pal.Gold), 0.35f * size, 1.2f, 0.8f, 1.8f, 0.4f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 0.85f, 0.25f, 0.9f), 0.5f);
                    Ring(p, 0.2f * size, 0.8f * size, new Color(1f, 1f, 0.7f, 0.9f), 0.35f, 0.6f);
                    break;
                case FxKind.Explosion:
                    Flash(mid, 0.5f * size, 1.9f * size, new Color(1f, 1f, 1f, 0.95f), 0.08f); // the white core first
                    Ring(p, 0.3f * size, 2.1f * size, (Color)heroAccent[(int)HeroId.Blast], 0.4f, 0.6f);
                    Pow(mid, 1, C(Pal.FireCore), 1.8f * size, 0.22f);
                    Flash(mid, 0.4f * size, 1.4f * size, new Color(1f, 0.6f, 0.2f, 1f), 0.25f);
                    Flames(mid, 14, C(Pal.Fire), 0.8f * size, 2.5f * size, 0.5f, 1.2f, 0.3f * size);
                    Sparks(mid, 14, C(Pal.FireCore), 0.14f, 6f * size, 0.5f, 2f);
                    Smoke(mid, 10, C(Pal.Ink, 170), 0.8f * size, 1.4f * size, 1.0f, 0.8f, 0.4f * size);
                    Chunks(mid, 8, C(Pal.Bomb), 0.16f * size, 5f, 0.7f, 3f);
                    Crack(p, 1.0f * size, 1.2f);
                    Nova(p, new Color(1f, 0.6f, 0.25f, 1f), 1.6f * size, 0.45f);
                    Ring(p, 0.3f * size, 1.8f * size, new Color(1f, 0.7f, 0.3f, 0.9f), 0.35f);
                    break;
                case FxKind.TowerDestroyed:
                    Pow(p + Vector3.up * 1.5f, 1, C(Pal.Cream), 3f * size, 0.3f);
                    Flash(p + Vector3.up * 1.5f, 0.5f * size, 2.4f * size, new Color(1f, 0.9f, 0.7f, 1f), 0.4f);
                    Shards(p + Vector3.up * 1.4f, 20, C(Pal.Stone), 0.22f * size, 5f, 1.4f, 4f, 0.8f);
                    Chunks(p + Vector3.up * 1.4f, 14, C(Pal.Stone), 0.35f * size, 5f, 1.3f, 4f, 0.8f);
                    Shards(p + Vector3.up * 1.8f, 10, teamC, 0.16f * size, 5f, 1.4f, 5f, 0.5f);
                    Smoke(p + Vector3.up * 0.8f, 20, C(Pal.Cloth, 220), 1.2f * size, 2f, 1.8f, 1f, 1f);
                    Dust(p + Vector3.up * 0.2f, 10, C(Pal.Ground, 220), 1.4f * size, 2.5f, 1.4f, 0.2f, 0.8f);
                    Crack(p, 2.2f * size, 2f, 0.8f);
                    Nova(p, new Color(1f, 0.95f, 0.8f, 1f), 3.2f * size, 0.6f);
                    Pillar(p, 2f * size, 5f * size, (Color)teamL, 0.6f);
                    Ring(p, 0.8f * size, 4f * size, new Color(1f, 1f, 1f, 0.8f), 0.6f);
                    Ring(p, 0.5f * size, 2.6f * size, (Color)teamL, 0.45f, 0.6f);
                    break;
                case FxKind.ShieldPop:
                    Pow(mid, 1, C(Pal.Honey), 0.9f * size, 0.16f);
                    Drops(mid, 10, C(Pal.Honey), 0.16f * size, 3f, 0.6f, 1.8f, 0.3f);
                    Shards(mid, 6, C(Pal.Honey), 0.09f * size, 3.5f, 0.5f, 1.5f, 0.3f);
                    Ring(p, 0.4f * size, 1.1f * size, new Color(1f, 0.85f, 0.3f, 0.8f), 0.3f);
                    break;
                case FxKind.Cast:
                    Swirl(p, 1.1f * size, (Color)teamL, 0.5f);
                    Ring(p, 0.3f * size, 1.1f * size, (Color)teamL, 0.35f);
                    Glow(p + Vector3.up * 0.1f, 8, teamL, 0.18f * size, 1.2f, 0.5f, 1.6f, 0.3f);
                    Stars(p + Vector3.up * 0.5f, 3, teamL, 0.3f * size, 1f, 0.45f, 1.2f, 0.3f);
                    break;
                case FxKind.Bounce:
                    Pow(mid, 1, C(Pal.Steel), 0.6f * size, 0.14f);
                    Sparks(mid, 8, C(Pal.Steel), 0.12f, 3.5f, 0.3f, 1.2f);
                    Ring(p, 0.2f * size, 0.8f * size, new Color(0.9f, 0.95f, 1f, 0.8f), 0.22f);
                    break;
                case FxKind.WolfSummon:
                    Pillar(p, 1.2f * size, 2.4f * size, new Color(0.6f, 0.9f, 1f, 1f), 0.6f);
                    Swirl(p, 1.2f * size, new Color(0.6f, 0.9f, 1f, 1f), 0.6f);
                    Wisps(p + Vector3.up * 0.3f, 6, C(Pal.Ghost), 0.7f * size, 1.2f, 0.8f, 1f, 0.3f);
                    Glow(p + Vector3.up * 0.3f, 12, C(Pal.Ghost), 0.25f * size, 1.6f, 0.7f, 0.8f, 0.3f);
                    Stars(p + Vector3.up * 0.6f, 4, C(Pal.GhostEye), 0.3f * size, 1.2f, 0.6f, 1f, 0.4f);
                    Smoke(p + Vector3.up * 0.2f, 6, C(Pal.GhostDark, 140), 0.6f * size, 0.8f, 0.7f, 0.4f, 0.3f);
                    Ring(p, 0.2f * size, 1.2f * size, new Color(0.6f, 0.9f, 1f, 0.9f), 0.45f);
                    break;
                case FxKind.CloneSpawn:
                case FxKind.CloneEnd:
                    Smoke(mid, 10, C(Pal.Violet, 170), 0.5f * size, 1.2f, 0.6f, 0.5f, 0.25f);
                    Wisps(mid, 3, C(Pal.GhostDark), 0.5f * size, 0.8f, 0.6f, 0.8f, 0.2f);
                    Glow(mid, 8, C(Pal.GhostDark), 0.18f * size, 2f, 0.4f, 0.5f, 0.3f);
                    if (kind == FxKind.CloneSpawn) Ring(p, 0.2f * size, 0.9f * size, new Color(0.6f, 0.45f, 1f, 0.8f), 0.3f);
                    break;
                case FxKind.MinionSpawn:
                    Dust(p + Vector3.up * 0.15f, 5, C(Pal.Ground, 200), 0.5f * size, 1f, 0.5f, 0.2f, 0.2f);
                    Ring(p, 0.2f * size, 0.7f * size, (Color)teamL, 0.3f);
                    break;
                case FxKind.TowerShotFlash:
                    Pow(p, 1, teamL, 0.6f * size, 0.12f);
                    Flash(p, 0.15f * size, 0.6f * size, (Color)teamL, 0.15f);
                    Glow(p, 6, teamL, 0.2f * size, 2f, 0.25f);
                    break;
                case FxKind.AnchorHit:
                    SpecialHit((int)HeroId.Knut, mid, 1.1f * size);
                    Drops(mid, 10, heroAccent[(int)HeroId.Knut], 0.15f * size, 3f, 0.5f, 1.5f, 0.2f);
                    Pow(mid, 1, C(Pal.Steel), 0.9f * size, 0.16f);
                    Sparks(mid, 12, C(Pal.Steel), 0.13f, 4f, 0.35f, 1.4f);
                    Dust(p + Vector3.up * 0.1f, 6, C(Pal.Ground, 210), 0.6f * size, 1.4f, 0.6f, 0.3f, 0.2f);
                    Chunks(p + Vector3.up * 0.15f, 4, C(Pal.Rock), 0.13f * size, 2.5f, 0.6f, 2.2f);
                    Crack(p, 0.8f * size, 1f);
                    Ring(p, 0.3f * size, 1.0f * size, new Color(0.75f, 0.8f, 0.9f, 0.9f), 0.3f);
                    break;
            }
        }

        // Each hero's two special colours (main, accent) from the MLBB clip analysis (bam-work/art/mlbb-miya/skill-fx-analys.md),
        // index = HeroId: Bram, Fenn, Tyra, Moa, Knut, Tikta, Blast. Tyra keeps the cyan her bolt already has.
        internal static Color32 HeroMain(HeroId h) => heroMain[(int)h];
        internal static Color32 HeroAccent(HeroId h) => heroAccent[(int)h];

        static readonly Color32[] heroMain =
        {
            new Color32(255, 138, 42, 255), new Color32(191, 239, 255, 255), new Color32(180, 240, 255, 255), new Color32(255, 179, 0, 255),
            new Color32(47, 140, 255, 255), new Color32(124, 255, 79, 255), new Color32(255, 90, 31, 255),
        };
        static readonly Color32[] heroAccent =
        {
            new Color32(255, 227, 107, 255), new Color32(140, 123, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 246, 214, 255),
            new Color32(127, 255, 224, 255), new Color32(176, 76, 255, 255), new Color32(255, 242, 160, 255),
        };

        /// <summary>The caster's own "release" when a hero fires the special, in that hero's colours, so you see who cast it even
        /// when the hit lands far away: a short white flash, a thin pillar, rising glows and a few accent stars.</summary>
        public static void HeroCast(int hero, Vector3 p)
        {
            if (hero < 0 || hero >= heroMain.Length) return;
            Color32 m = heroMain[hero], a = heroAccent[hero];
            Flash(p + Vector3.up * 0.6f, 0.2f, 0.7f, new Color(1f, 1f, 1f, 0.7f), 0.1f);
            Pillar(p, 0.6f, 2.2f, (Color)m, 0.35f);
            Glow(p + Vector3.up * 0.2f, 4, m, 0.22f, 0.8f, 0.3f, 1.4f, 0.25f);
            Stars(p + Vector3.up * 0.7f, 2, a, 0.28f, 0.9f, 0.45f, 1f, 0.3f);
            switch ((HeroId)hero)
            {
                case HeroId.Knut: Drops(p + Vector3.up * 0.5f, 6, m, 0.14f, 2f, 0.5f, 1.2f, 0.2f); break;
                case HeroId.Blast: Flames(p + Vector3.up * 0.3f, 4, m, 0.4f, 1f, 0.4f, 1f, 0.2f); break;
                case HeroId.Moa: Bubbles(p + Vector3.up * 0.4f, 5, m, 0.16f, 0.8f, 0.6f, 1f, 0.3f); break;
                case HeroId.Tikta: Bubbles(p + Vector3.up * 0.4f, 3, a, 0.12f, 0.8f, 0.5f, 1f, 0.2f); break;
                case HeroId.Fenn: Wisps(p + Vector3.up * 0.4f, 3, a, 0.5f, 0.6f, 0.6f, 0.8f, 0.3f); break;
                case HeroId.Bram: Sparks(p + Vector3.up * 0.5f, 6, a, 0.1f, 3f, 0.3f, 1.2f); break;
            }
        }

        /// <summary>A special's hit, MLBB order: a white core first (under 0.1 s), then the main colour's ring and nova and the
        /// accent's sparks going outward.</summary>
        static void SpecialHit(int hero, Vector3 p, float r)
        {
            Color32 m = heroMain[hero], a = heroAccent[hero];
            Vector3 g = new Vector3(p.x, 0f, p.z);
            Flash(p, 0.3f * r, 1.1f * r, new Color(1f, 1f, 1f, 0.95f), 0.08f);
            Ring(g, 0.3f * r, r, (Color)m, 0.35f);
            Ring(g, 0.2f * r, 0.7f * r, (Color)a, 0.3f, 0.6f);
            Nova(g, (Color)m, r, 0.4f);
            Sparks(p, 10, a, 0.12f, 4.5f * r, 0.3f, 0.8f);
        }

        /// <summary>Where a projectile ends: each hero's own splash (honey, poison, fire, sparks), so you see whose shot landed.</summary>
        public static void Impact(ProjKind k, Vector3 p, int team)
        {
            Color32 teamL = C(Pal.TeamLight(team));
            switch (k)
            {
                case ProjKind.BramHammer:
                    Pow(p, 1, C(Pal.Steel), 0.5f, 0.12f);
                    Sparks(p, 5, C(Pal.FireCore), 0.1f, 3f, 0.25f, 1f);
                    break;
                case ProjKind.BramHammerSpecial:
                    SpecialHit((int)HeroId.Bram, p, 1f);
                    Crack(new Vector3(p.x, 0f, p.z), 0.6f, 0.9f);
                    Pow(p, 1, teamL, 0.8f, 0.15f);
                    Sparks(p, 8, C(Pal.Steel), 0.12f, 3.5f, 0.3f, 1.2f);
                    Stars(p, 2, teamL, 0.35f, 1.2f, 0.3f, 0.8f);
                    break;
                case ProjKind.TyraArrow:
                    Stars(p, 1, C(Pal.White), 0.3f, 0f, 0.15f);
                    Sparks(p, 3, C(Pal.Cream), 0.08f, 2.5f, 0.2f, 0.6f);
                    break;
                case ProjKind.TyraBolt:
                    SpecialHit((int)HeroId.Tyra, p, 1f);
                    Pow(p, 1, new Color32(180, 240, 255, 255), 0.9f, 0.16f);
                    Rays(p, new Color(0.7f, 0.95f, 1f, 1f), 1.1f, 0.18f);
                    Stars(p, 3, new Color32(200, 245, 255, 255), 0.45f, 1.5f, 0.3f, 0.6f);
                    Sparks(p, 8, new Color32(180, 240, 255, 255), 0.12f, 4f, 0.3f, 0.8f);
                    break;
                case ProjKind.MoaBolt:
                    Drops(p, 6, C(Pal.Honey), 0.14f, 2.2f, 0.5f, 1.4f, 0.1f);
                    Glow(p, 2, C(Pal.Honey), 0.4f, 0.4f, 0.25f);
                    break;
                case ProjKind.TiktaNeedle:
                    Bubbles(p, 3, C(Pal.Poison), 0.14f, 0.6f, 0.6f, 0.8f, 0.1f);
                    break;
                case ProjKind.TiktaNeedleSpecial:
                    SpecialHit((int)HeroId.Tikta, p, 0.6f);
                    Bubbles(p, 7, C(Pal.Poison), 0.18f, 0.9f, 0.8f, 1f, 0.2f);
                    Wisps(p, 2, C(Pal.Poison, 200), 0.5f, 0.4f, 0.7f, 0.8f, 0.1f);
                    break;
                case ProjKind.BlastFire:
                    Pow(p, 1, C(Pal.FireCore), 0.6f, 0.14f);
                    Flames(p, 5, C(Pal.Fire), 0.45f, 1.4f, 0.4f, 1f, 0.1f);
                    break;
                case ProjKind.RangedMinionShot:
                    Pow(p, 1, C(Pal.Cream, 200), 0.35f, 0.1f);
                    break;
                case ProjKind.SiegeShell:
                    Pow(p, 1, C(Pal.FireCore), 0.8f, 0.15f);
                    Dust(p, 4, C(Pal.Ground, 210), 0.6f, 1.5f, 0.5f, 0.3f, 0.2f);
                    Chunks(p, 3, C(Pal.Rock), 0.12f, 2.5f, 0.5f, 2f);
                    break;
                case ProjKind.TowerShot:
                    Pow(p, 1, teamL, 0.7f, 0.14f);
                    Sparks(p, 6, teamL, 0.12f, 3.5f, 0.3f, 1f);
                    break;
            }
        }

        /// <summary>What a projectile leaves behind in flight; called every few hundredths of a second.</summary>
        public static void InFlight(ProjKind k, Vector3 p, Vector3 dir, int team)
        {
            switch (k)
            {
                case ProjKind.MoaBolt: Drops(p, 1, C(Pal.Honey), 0.1f, 0.3f, 0.45f, 0.2f, 0.05f); break;
                case ProjKind.TiktaNeedle: Bubbles(p, 1, C(Pal.Poison, 200), 0.08f, 0.1f, 0.35f, 0.3f); break;
                case ProjKind.TiktaNeedleSpecial: Bubbles(p, 1, C(Pal.Poison), 0.12f, 0.2f, 0.45f, 0.4f, 0.05f); break;
                case ProjKind.BlastFire: Flames(p, 1, C(Pal.Fire), 0.35f, 0.3f, 0.3f, 0.6f, 0.05f); break;
                case ProjKind.TyraBolt: Stars(p, 1, new Color32(200, 245, 255, 255), 0.25f, 0.2f, 0.25f, 0f, 0.05f); break;
                case ProjKind.BramHammerSpecial: Glow(p, 1, C(Pal.TeamLight(team)), 0.3f, 0.2f, 0.2f); break;
                case ProjKind.TowerShot: Glow(p, 1, C(Pal.TeamLight(team)), 0.35f, 0.1f, 0.2f); break;
            }
        }
    }
}
