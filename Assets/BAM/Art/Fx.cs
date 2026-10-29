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
            Fx.Tick(ArtKit.Dt);
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

        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");

        /// <summary>Additive weighed by alpha, for his white + alpha pictures (2026-10-27 Gemini set): without it each one shows as a
        /// white square. Works on any additive material (DstBlend One).</summary>
        public static Material WeighByAlpha(Material m)
        {
            if (m.HasProperty(SrcBlend)) m.SetFloat(SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
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
        static bool ownWisp;
        // Bonk's own pictures (Rickard 2026-10-26, coloured with alpha, drawn alpha-blended): cracks and a dust ring flat on the
        // ground, dust puffs and a 4x4 sheet of stone chips (null when missing: GroundSmash then uses the shared helpers)
        static ParticleSystem bCrack, bShock, bDust, bRocks;
        static ParticleSystem.EmitParams ep;
        static readonly List<ParticleSystem> systems = new List<ParticleSystem>(32); // every one made, for ClearAll and SetPaused
        public static float Amount = 1f; // 1 on High, about half on Low

        sealed class Blob
        {
            public Transform T;
            public MeshRenderer R;
            public MeshFilter F;
            public Material Mat, PalMat, AddMat, FadeMat;
            public float Age, Life, S0, S1, A0;
            public Color Col;
            public bool Flat, Linear; // Linear: grows at an even pace (a fuse), not easing out
            public Material TeleMat, SpikeMat;
            public float Aspect = 1f; // width over height of a standing picture (his 2:1 hit spikes)
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
            BonkSystems();
            pics.Clear();
            for (int i = 0; i < WowPics.Length; i++) PicSystem(WowPics[i], WowFlat[i]);
            OwnSystems();
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
            for (int i = 0; i < 48; i++) free.Push(NewBurst()); // headroom so a six-hero fight does not build new ones (and their materials) mid-fight (PERF-06)
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
            // his newer spark star sits in the atlas (cell 1); the old 2x2 sheet only stands in without the atlas
            if (star == null && (t = RickardFx.Load("tx_fx_particles")) != null)
            {
                star = MakeSystem("BAM Stars", ArtMaterials.ParticleAdd, 0f, 0.4f, 1f, 200);
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
                RickardFx.WeighByAlpha(crackPs.GetComponent<ParticleSystemRenderer>().sharedMaterial); // redone 2026-10-27 as white + alpha
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
                RickardFx.WeighByAlpha(pillar.GetComponent<ParticleSystemRenderer>().sharedMaterial); // redone 2026-10-27 as white + alpha
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
            if ((t = RickardFx.Load("tx_fx_wisp")) != null)
            {
                // his will-o'-wisp (2026-10-27) over atlas cell 9: its tail trails up behind it as it rises, so it does not spin
                if (wisp == null) wisp = MakeSystem("BAM Wisps", ArtMaterials.ParticleAdd, -0.2f, 0.6f, 1.2f, 200);
                Retexture(wisp, t, 1, 1, 0, false);
                RickardFx.WeighByAlpha(wisp.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                var rot = wisp.rotationOverLifetime;
                rot.enabled = false;
                ownWisp = true;
            }
            if ((t = RickardFx.Load("tx_fx_nova")) != null)
            {
                nova = Flat(MakeSystem("BAM Novas", ArtMaterials.ParticleAdd, 0f, 0.35f, 1.1f, 30));
                Retexture(nova, t, 1, 1, 0, false);
            }
        }

        // Rickard's WoW-mode effect pictures from Speltest (tx_wow_*, white on black, approved for BAM 2026-10-27: "effektbilderna
        // från wowläget kan jag använda"): one additive system per picture, made at Init so the first use never hitches
        static readonly string[] WowPics =
        {
            "tx_wow_warrior_impact", "tx_wow_sword_slash", "tx_wow_mage_blink", "tx_wow_priest_shield", "tx_wow_mage_shatter",
            "tx_wow_warrior_whirl", "tx_wow_magic_circle", "tx_wow_priest_runes",
        };
        static readonly bool[] WowFlat = { false, false, false, false, false, true, true, true }; // the last three lie on the ground
        static readonly Dictionary<string, ParticleSystem> pics = new Dictionary<string, ParticleSystem>(32);

        // Rickard's own Gemini set (2026-10-27, from the list "Texturer att göra för BAM"): white + alpha, so drawn additive weighed
        // by alpha; the Blast explosion is in colour and drawn see-through. They win over the WoW-mode pictures where both fit.
        static readonly string[] OwnPics =
        {
            "tx_fx_hit_star_a", "tx_fx_hit_star_b", "tx_fx_hit_star_c", "tx_fx_impact_ring", "tx_fx_fenn_claw", "tx_fx_bram_anvil",
            "tx_fx_tyra_release", "tx_fx_tyra_charge", "tx_fx_blast_explosion", "tx_fx_tikta_poison", "tx_fx_knut_splash", "tx_fx_moa_honey",
            "tx_fx_blast_scorch",
        };

        // mode: 0 white on black (additive), 1 white + alpha (additive weighed by alpha), 2 a coloured picture (alpha blended)
        static ParticleSystem PicSystem(string name, bool flat, int mode = 0, float s0 = 0.75f, float s1 = 1.1f)
        {
            if (pics.TryGetValue(name, out var ps)) return ps;
            var t = RickardFx.Load(name);
            if (t != null)
            {
                ps = MakeSystem("BAM " + name, mode == 2 ? ArtMaterials.ParticleAlpha : ArtMaterials.ParticleAdd, 0f, s0, s1, 40);
                if (mode == 2) RetextureAlpha(ps, t, false);
                else Retexture(ps, t, 1, 1, 0, false);
                if (mode == 1) RickardFx.WeighByAlpha(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                if (flat) Flat(ps);
            }
            pics[name] = ps;
            return ps;
        }

        static void OwnSystems()
        {
            for (int i = 0; i < OwnPics.Length; i++)
            {
                string n = OwnPics[i];
                if (n == "tx_fx_impact_ring") PicSystem(n, true, 1, 0.3f, 1f); // a sharp ring that races out, lying on the ground
                else if (n == "tx_fx_blast_explosion") PicSystem(n, false, 2, 0.45f, 1.15f); // the fireball swells
                else if (n == "tx_fx_hit_star_a" || n == "tx_fx_hit_star_b" || n == "tx_fx_hit_star_c") PicSystem(n, false, 1, 0.8f, 1.1f);
                else if (n == "tx_fx_tyra_charge") PicSystem(n, false, 1, 1.3f, 0.25f); // the spiral pulls in to the bow
                else if (n == "tx_fx_tikta_poison") PicSystem(n, false, 2, 0.6f, 1.15f); // a poison puff that swells
                else if (n == "tx_fx_knut_splash") PicSystem(n, true, 2, 0.45f, 1.1f); // water thrown out on the ground
                else if (n == "tx_fx_moa_honey") PicSystem(n, true, 2, 0.6f, 1f); // a honey puddle spreading
                else if (n == "tx_fx_blast_scorch") PicSystem(n, true, 2, 0.95f, 1f); // a burn mark that stays a while
                else PicSystem(n, false, 1);
            }
            var t = RickardFx.Load("tx_fx_ember");
            if (t != null)
            {
                // his glowing flake: embers that drift up off fire and burns, turning as they go
                ember = MakeSystem("BAM Embers", ArtMaterials.ParticleAdd, -0.25f, 1f, 0.35f, 200);
                Retexture(ember, t, 1, 1, 0, false);
                RickardFx.WeighByAlpha(ember.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                var spin = ember.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            }
        }

        static ParticleSystem ember;

        /// <summary>Rickard's ember flakes (tx_fx_ember) drifting up; plain sparks without the picture.</summary>
        public static void Embers(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f)
            => Burst(ember ?? spark, pos, n, c, ember != null ? size * 1.6f : size, speed, life, up, jitter);

        /// <summary>The hit star in the middle of the target (his tx_fx_hit_star_c, now and then one of the two others); false when
        /// it is missing.</summary>
        public static bool HitStar(Vector3 pos, Color c, float size, float life)
        {
            float r = Random.value;
            string n = r < 0.6f ? "tx_fx_hit_star_c" : r < 0.8f ? "tx_fx_hit_star_a" : "tx_fx_hit_star_b";
            return Pic(n, pos, c, size, life) || Pic("tx_fx_hit_star_c", pos, c, size, life);
        }

        /// <summary>One of his pictures standing upright at pos, unturned (the anvil's spark fan rises from its foot).</summary>
        public static bool PicUpright(string name, Vector3 pos, Color c, float size, float life)
        {
            if (!pics.TryGetValue(name, out var ps) || ps == null) return false;
            Emit1(ps, pos, c, size, life, 0f);
            return true;
        }

        /// <summary>One of Rickard's WoW-mode pictures at pos, tinted c, size in units, turned at random; false when it is missing.</summary>
        public static bool Pic(string name, Vector3 pos, Color c, float size, float life)
        {
            if (!pics.TryGetValue(name, out var ps) || ps == null) return false;
            bool flat = ps.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.HorizontalBillboard;
            Emit1(ps, flat ? new Vector3(pos.x, 0.06f, pos.z) : pos, c, size, life, Random.value * 360f);
            return true;
        }

        static void BonkSystems()
        {
            Texture2D t;
            if ((t = RickardFx.Load("tx_fx_bonk_crack")) != null)
                RetextureAlpha(bCrack = Flat(MakeSystem("BAM Bonk Cracks", ArtMaterials.ParticleAlpha, 0f, 1f, 1f, 24)), t, false);
            if ((t = RickardFx.Load("tx_fx_bonk_shockwave")) != null)
            {
                bShock = Flat(MakeSystem("BAM Bonk Dust Rings", ArtMaterials.ParticleAlpha, 0f, 1f, 1f, 24));
                RetextureAlpha(bShock, t, false);
                // rolls out fast, then slows to its edge
                var size = bShock.sizeOverLifetime;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 2.6f, 2.6f), new Keyframe(1f, 1f, 0f, 0f)));
            }
            if ((t = RickardFx.Load("tx_fx_bonk_dust")) != null)
            {
                bDust = MakeSystem("BAM Bonk Dust", ArtMaterials.ParticleAlpha, -0.03f, 0.6f, 1.7f, 160);
                RetextureAlpha(bDust, t, false);
                var spin = bDust.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            }
            if ((t = RickardFx.Load("tx_fx_bonk_rocks")) != null)
            {
                bRocks = MakeSystem("BAM Bonk Rocks", ArtMaterials.ParticleAlpha, 2.4f, 1f, 0.9f, 160);
                RetextureAlpha(bRocks, t, true);
                var spin = bRocks.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            }
        }

        // a coloured picture with its own alpha; sheet = his 4x4 stone chips, one chip picked per particle
        static void RetextureAlpha(ParticleSystem ps, Texture2D tex, bool sheet)
        {
            var m = ArtMaterials.Instance(ArtMaterials.ParticleAlpha);
            m.SetTexture(ArtMaterials.BaseMap, tex);
            m.mainTexture = tex;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = sheet;
            if (!sheet) return;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = 4;
            tsa.numTilesY = 4;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.startFrame = new ParticleSystem.MinMaxCurve(0f);
            tsa.cycleCount = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f); // two constants: a random chip that stays the same
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
            systems.Add(ps);
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
                else ep.rotation = ps == spark || ps == drop || ps == bubble || (ps == wisp && ownWisp) ? 0f : Random.value * 360f;
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
            if (shock != null && r1 >= r0)
            {
                // his flipbook's last ring reaches about 0.9 of the frame: one particle sized so it ends at r1
                Emit1(shock, new Vector3(pos.x, 0.06f, pos.z), c, r1 * 2.2f, life * 1.3f);
                return;
            }
            // the painted shockwave (its rim sits at 0.82 of the cell's half width: sized so the rim lands on the radius)
            // (a closing ring, Recall's or a cancel's, uses this path too: the flipbook only grows)
            if (FxAtlas.Texture != null) Spawn(FxAtlas.Quad(FxAtlas.Shockwave), pos, r0 * 2.44f, r1 * 2.44f, c, life, true, 1);
            else Spawn(Shapes.RingMesh(thickness, Pal.White), pos, r0, r1, c, life, true, 0);
        }

        /// <summary>Rickard's sharp impact ring (tx_fx_impact_ring) racing out to radius r on the ground; his shockwave flipbook
        /// (Ring) when it is missing. The ring sits at 0.70 of the picture's half width.</summary>
        public static void ImpactRing(Vector3 pos, float r, Color c, float life)
        {
            if (!Pic("tx_fx_impact_ring", pos, c, r * 2f / 0.7f, life)) Ring(pos, 0.2f * r, r, c, life);
        }

        static Texture2D teleTex;
        static bool teleLoaded;

        /// <summary>Blast's chain warning (ArtKit.PlayChainWarn): the edge on the radius for the whole fuse, a gold ring racing out to it.</summary>
        public static void ChainWarn(Vector3 at, float radius, float fuse)
        {
            if (!teleLoaded) { teleLoaded = true; teleTex = RickardFx.Load("tx_fx_telegraph_ring"); }
            Color violet = new Color(0.7f, 0.42f, 1f, 1f), gold = new Color(1f, 0.82f, 0.3f, 1f);
            if (teleTex != null)
            {
                // his telegraph ring peaks at 0.887 of the picture: sized so its edge lands on the radius
                SpawnTele(at, radius * 2.255f, radius * 2.255f, violet, fuse);
                SpawnTele(at, 0.05f, radius * 2.255f, gold, fuse, true);
            }
            else
            {
                Spawn(Shapes.RingMesh(0.9f, Pal.White), at, radius, radius, violet, fuse, true, 0);
                Spawn(Shapes.RingMesh(0.85f, Pal.White), at, 0.05f, radius, gold, fuse, true, 0);
                live[live.Count - 1].Linear = true;
            }
            Flash(at + Vector3.up * 0.5f, 0.1f, 0.45f, new Color(1f, 0.96f, 0.82f, 0.9f), 0.12f);
            Sparks(at + Vector3.up * 0.6f, 8, new Color32(255, 245, 209, 255), 0.1f, 2.5f, 0.2f, 1.2f);
        }

        static void SpawnTele(Vector3 at, float s0, float s1, Color c, float life, bool linear = false)
        {
            Spawn(FxAtlas.FullQuad(), at, s0, s1, c, life, true, 3);
            live[live.Count - 1].Linear = linear;
        }

        public static void Flash(Vector3 pos, float r0, float r1, Color c, float life) => Spawn(Shapes.Ball(), pos, r0, r1, c, life, false, 0);

        static Texture2D spikesTex;
        static bool spikesLoaded;

        /// <summary>Rickard's hit spikes (tx_fx_hit_spikes, 2026-10-27): a fan of light shooting out behind the target the way the
        /// blow went, facing the camera. The fan's root (the picture's left edge) sits on the target; dir is the blow's direction.</summary>
        public static void Spikes(Vector3 at, Vector3 dir, Color c, float size, float life)
        {
            if (!spikesLoaded) { spikesLoaded = true; spikesTex = RickardFx.Load("tx_fx_hit_spikes"); }
            if (spikesTex == null) return;
            Spawn(FxAtlas.FullQuad(), at, size * 0.85f, size * 1.1f, c, life, false, 4);
            Blob bu = live[live.Count - 1];
            bu.Aspect = 2f; // 1024 x 508
            Apply(bu, 0f);
            Quaternion cam = ArtDriver.CamRot;
            float ang = Mathf.Atan2(Vector3.Dot(dir, cam * Vector3.up), Vector3.Dot(dir, cam * Vector3.right)) * Mathf.Rad2Deg;
            bu.T.rotation = cam * Quaternion.Euler(0f, 0f, ang) * Quaternion.Euler(-90f, 0f, 0f); // as the boulder's dust trail
            bu.T.position = at + bu.T.right * (size * 0.85f);
        }

        /// <summary>Cracks in the ground that fade (heavy landings, explosions). Radius in units.</summary>
        public static void Crack(Vector3 pos, float radius, float life, float alpha = 0.7f) => Crack(pos, radius, life, new Color(1f, 0.75f, 0.45f, alpha));

        /// <summary>Cracks in a colour of their own (glowing lava orange by default; Knut's water anchor a dim blue-grey).</summary>
        public static void Crack(Vector3 pos, float radius, float life, Color c)
        {
            if (crackPs != null)
            {
                Emit1(crackPs, new Vector3(pos.x, 0.03f, pos.z), c, radius * 2.2f, life, Random.value * 360f);
                return;
            }
            if (FxAtlas.Texture == null) return;
            Spawn(FxAtlas.Quad(FxAtlas.Crack), pos, radius * 2f, radius * 2.1f, new Color(1f, 1f, 1f, c.a), life, true, 2);
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
            if (matKind == 3 && bu.TeleMat == null && teleTex != null)
            {
                bu.TeleMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                bu.TeleMat.SetTexture(ArtMaterials.BaseMap, teleTex);
                bu.TeleMat.mainTexture = teleTex;
            }
            if (matKind == 4 && bu.SpikeMat == null && spikesTex != null)
            {
                bu.SpikeMat = RickardFx.WeighByAlpha(ArtMaterials.Instance(ArtMaterials.UnlitAdd));
                bu.SpikeMat.SetTexture(ArtMaterials.BaseMap, spikesTex);
                bu.SpikeMat.mainTexture = spikesTex;
            }
            bu.Mat = matKind == 1 && bu.AddMat != null ? bu.AddMat : matKind == 2 && bu.FadeMat != null ? bu.FadeMat : matKind == 3 && bu.TeleMat != null ? bu.TeleMat
                : matKind == 4 && bu.SpikeMat != null ? bu.SpikeMat : bu.PalMat;
            bu.Linear = false;
            bu.Aspect = 1f;
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
            float e = bu.Linear ? t : 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(bu.S0, bu.S1, e);
            bu.T.localScale = bu.Flat ? new Vector3(s, 1f, s) : new Vector3(s * bu.Aspect, s, s);
            Color c = bu.Col;
            c.a = bu.A0 * (1f - t);
            if (bu.Mat == bu.TeleMat) { float k = bu.Linear ? 0.55f + 0.45f * t : 0.8f + 0.2f * Mathf.Sin(bu.Age * 40f); c = new Color(bu.Col.r * k, bu.Col.g * k, bu.Col.b * k, 1f); } // additive: brightness is strength
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
            for (int i = 0; i < systems.Count; i++) if (systems[i] != null) systems[i].Clear();
        }

        /// <summary>Freezes (or lets go of) every particle system; the rings and flashes stop with ArtKit.Dt.</summary>
        public static void SetPaused(bool on)
        {
            for (int i = 0; i < systems.Count; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                if (on) ps.Pause(true); else ps.Play(true); // emission is off, so Play only lets the live particles run on
            }
        }

        // ---------------------------------------------------------------- the FxKind catalogue

        static Color32 C(byte pal) => Pal.Get(pal);
        static Color32 C(byte pal, byte a) { Color32 c = Pal.Get(pal); c.a = a; return c; }

        /// <summary>hero (a HeroId, or -1): whose attack or cast it is, so hits and casts wear that hero's two colours (MLBB rule:
        /// two colours per skill); -1 (minions, towers) keeps the neutral look.</summary>
        public static void Play(FxKind kind, Vector3 p, float size, int team, int hero = -1)
        {
            Color32 teamC = C(Pal.Team(team)), teamL = C(Pal.TeamLight(team));
            Vector3 mid = p + Vector3.up * 0.5f;
            if (hero >= heroMain.Length) hero = -1;
            bool heavy = hero == (int)HeroId.Bram || hero == (int)HeroId.Knut || hero == (int)HeroId.Blast;
            switch (kind)
            {
                case FxKind.Hit when hero >= 0:
                    if (!HitStar(mid, Color.white, 0.8f * size, 0.14f)) Pow(mid, 1, C(Pal.Cream, 230), 0.55f * size, 0.14f);
                    if (hero == (int)HeroId.Fenn) Pic("tx_wow_sword_slash", mid, (Color)heroMain[hero], 1.2f * size, 0.14f); // his sword cuts
                    Sparks(mid, 6, heroMain[hero], 0.12f * size, 3f, 0.25f, 1f);
                    Stars(mid, 1, heroAccent[hero], 0.35f * size, 0.5f, 0.2f);
                    break;
                case FxKind.HitBig when hero >= 0:
                    Pow(mid, 1, C(Pal.Cream), 0.9f * size, 0.18f);
                    HitStar(mid, Color.white, 1.3f * size, 0.18f);
                    if (heavy || hero == (int)HeroId.Bonk) Pic("tx_wow_warrior_impact", mid, (Color)heroAccent[hero], 1.8f * size, 0.22f);
                    Rays(mid, (Color)heroMain[hero], 1.3f * size, 0.2f);
                    Sparks(mid, 12, heroMain[hero], 0.16f * size, 4.5f, 0.35f, 1.5f);
                    Stars(mid, 2, heroAccent[hero], 0.45f * size, 1.5f, 0.25f, 1f);
                    if (heavy)
                    {
                        Chunks(p + Vector3.up * 0.2f, 3, C(Pal.Rock), 0.14f * size, 2.5f, 0.6f, 2f);
                        ImpactRing(p, 0.9f * size, (Color)heroMain[hero], 0.25f);
                    }
                    break;
                case FxKind.Hit:
                    if (!HitStar(mid, Color.white, 0.8f * size, 0.14f)) Pow(mid, 1, C(Pal.Cream, 230), 0.55f * size, 0.14f);
                    Sparks(mid, 6, C(Pal.FireCore), 0.12f * size, 3f, 0.25f, 1f);
                    Stars(mid, 1, C(Pal.White), 0.35f * size, 0.5f, 0.2f);
                    break;
                case FxKind.HitBig:
                    Pow(mid, 1, C(Pal.FireCore), 0.9f * size, 0.18f);
                    if (!HitStar(mid, new Color(1f, 0.92f, 0.75f, 1f), 1.3f * size, 0.18f))
                        Pic("tx_wow_warrior_impact", mid, new Color(1f, 0.85f, 0.55f, 1f), 1.6f * size, 0.22f);
                    Rays(mid, new Color(1f, 0.9f, 0.6f, 1f), 1.3f * size, 0.2f);
                    Sparks(mid, 12, C(Pal.Fire), 0.16f * size, 4.5f, 0.35f, 1.5f);
                    Stars(mid, 2, C(Pal.White), 0.45f * size, 1.5f, 0.25f, 1f);
                    Chunks(p + Vector3.up * 0.2f, 3, C(Pal.Rock), 0.14f * size, 2.5f, 0.6f, 2f);
                    ImpactRing(p, 0.9f * size, new Color(1f, 0.8f, 0.4f, 0.8f), 0.25f);
                    break;
                case FxKind.Death:
                    Pow(mid, 1, C(Pal.White, 200), 0.8f * size, 0.16f);
                    Pic("tx_wow_mage_shatter", mid, (Color)teamL, 1.8f * size, 0.3f);
                    Smoke(mid, 10, C(Pal.Cloth, 200), 0.45f * size, 1.2f, 0.8f, 0.6f, 0.2f);
                    Wisps(mid, 3, teamL, 0.6f * size, 0.4f, 0.9f, 1.4f, 0.2f);
                    Shards(mid, 10, teamC, 0.1f * size, 3f, 0.8f, 2.5f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 1f, 1f, 0.7f), 0.4f);
                    break;
                case FxKind.Respawn:
                    Pillar(p, 1.2f * size, 3f * size, (Color)teamL, 0.7f);
                    Pic("tx_wow_magic_circle", p, (Color)teamL, 2.2f * size, 0.9f);
                    Glow(p + Vector3.up * 0.1f, 18, teamL, 0.22f * size, 0.6f, 0.8f, 2.4f, 0.35f);
                    Stars(p + Vector3.up * 0.6f, 6, C(Pal.White), 0.3f * size, 0.8f, 0.6f, 1.6f, 0.4f);
                    Ring(p, 0.2f * size, 1.2f * size, (Color)teamL, 0.5f);
                    Flash(mid, 0.3f * size, 0.8f * size, new Color(1f, 1f, 1f, 0.6f), 0.25f);
                    break;
                case FxKind.LevelUp:
                    Pillar(p, 1.1f * size, 2.6f * size, new Color(1f, 0.85f, 0.3f, 1f), 0.8f);
                    Pic("tx_wow_priest_runes", p, new Color(1f, 0.85f, 0.35f, 1f), 2.2f * size, 0.9f);
                    Glow(p + Vector3.up * 0.2f, 16, C(Pal.Gold), 0.2f * size, 0.8f, 0.9f, 2.2f, 0.4f);
                    Stars(p + Vector3.up * 0.8f, 8, C(Pal.Gold), 0.35f * size, 1.2f, 0.8f, 1.8f, 0.4f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 0.85f, 0.25f, 0.9f), 0.5f);
                    Ring(p, 0.2f * size, 0.8f * size, new Color(1f, 1f, 0.7f, 0.9f), 0.35f, 0.6f);
                    break;
                case FxKind.Explosion:
                    Flash(mid, 0.4f * size, 1.2f * size, new Color(1f, 1f, 1f, 0.95f), 0.08f); // the white core first; rings end on the real radius
                    if (!Pic("tx_fx_blast_explosion", mid, Color.white, 2.6f * size, 0.45f)) // his own fireball (2026-10-27)
                        Pic("tx_wow_warrior_impact", mid, new Color(1f, 0.6f, 0.25f, 1f), 2.2f * size, 0.25f);
                    Ring(p, 0.3f * size, 1.0f * size, (Color)heroAccent[(int)HeroId.Blast], 0.4f, 0.6f);
                    Pow(mid, 1, C(Pal.FireCore), 1.8f * size, 0.22f);
                    Flash(mid, 0.4f * size, 1.0f * size, new Color(1f, 0.6f, 0.2f, 1f), 0.25f);
                    Flames(mid, 14, C(Pal.Fire), 0.8f * size, 2.5f * size, 0.5f, 1.2f, 0.3f * size);
                    Sparks(mid, 14, C(Pal.FireCore), 0.14f, 6f * size, 0.5f, 2f);
                    Smoke(mid, 6, C(Pal.Ink, 170), 0.8f * size, 1.4f * size, 1.0f, 0.8f, 0.4f * size);
                    Chunks(mid, 8, C(Pal.Bomb), 0.16f * size, 5f, 0.7f, 3f);
                    Crack(p, 1.0f * size, 1.2f);
                    Pic("tx_fx_blast_scorch", p, new Color(1f, 1f, 1f, 0.9f), 2.4f * size, 2f); // his burn mark stays a moment
                    Embers(mid, 8, C(Pal.FireCore), 0.12f, 2.5f * size, 0.9f, 1.5f, 0.3f * size);
                    Nova(p, new Color(1f, 0.6f, 0.25f, 1f), 1.0f * size, 0.45f);
                    Ring(p, 0.3f * size, 0.8f * size, new Color(1f, 0.7f, 0.3f, 0.9f), 0.35f);
                    break;
                case FxKind.AnvilStrike:
                {
                    // Bram's passive (every 4th hammer): the recipe's heavy anvil blow in his forge colours, ring on the real radius
                    Color32 m = heroMain[(int)HeroId.Bram], a = heroAccent[(int)HeroId.Bram];
                    Flash(mid, 0.25f * size, 0.8f * size, new Color(1f, 1f, 1f, 0.9f), 0.07f);
                    PicUpright("tx_fx_bram_anvil", p + Vector3.up * (0.15f + 0.6f * size), (Color)a, 1.8f * size, 0.3f); // his spark fan, rising from the blow
                    ImpactRing(p, size, (Color)m, 0.3f);
                    Sparks(mid, 10, a, 0.12f, 4f, 0.3f, 1.2f);
                    Chunks(p + Vector3.up * 0.2f, 5, C(Pal.Rock), 0.13f, 3f, 0.6f, 2.2f);
                    Dust(p + Vector3.up * 0.1f, 4, C(Pal.Ground, 200), 0.5f, 1.2f, 0.5f, 0.3f, 0.2f);
                    Crack(p, 0.5f * size, 0.8f);
                    break;
                }
                case FxKind.WolfLand:
                    // Fenn's wolf lands on nothing: a soft ring and dust, no bite
                    Ring(p, 0.2f * size, 1.2f * size, (Color)heroMain[(int)HeroId.Fenn], 0.35f);
                    Dust(p + Vector3.up * 0.1f, 4, C(Pal.Ground, 200), 0.5f * size, 1.2f, 0.5f, 0.3f, 0.2f);
                    Wisps(p + Vector3.up * 0.3f, 3, heroAccent[(int)HeroId.Fenn], 0.5f * size, 0.6f, 0.6f, 0.6f, 0.2f);
                    break;
                case FxKind.DeathmarkTaken:
                {
                    // a needle takes Tikta's mark: a short pink flash and four teal sparks thrown out, no smoke
                    Vector3 h = p + Vector3.up * 1.6f;
                    Flash(h, 0.15f, 0.6f, new Color(1f, 0.86f, 0.97f, 0.95f), 0.08f);
                    Pow(h, 1, new Color32(255, 92, 199, 255), 0.7f, 0.16f);
                    Ring(new Vector3(p.x, 0f, p.z), 0.2f, 0.7f, new Color(1f, 0.36f, 0.78f, 0.9f), 0.22f);
                    Sparks(p + Vector3.up * 0.7f, 4, new Color32(97, 255, 224, 255), 0.1f, 3.5f, 0.22f, 0.6f);
                    break;
                }
                case FxKind.GroundSmash:
                {
                    // Bonk (Rickard 2026-10-26: "slå sin enorma klubba kraftigt slag som gör aoe i marken och sprickor och damm"), in
                    // his own pictures: the ground cracks open (held, then fading over ~2 s), his dust ring rolls out to the true
                    // radius with a slower second one behind it, dust puffs kicked up and stone chips thrown. size = the hit radius
                    Flash(p + Vector3.up * 0.25f, 0.2f * size, 0.7f * size, new Color(1f, 0.95f, 0.85f, 0.85f), 0.08f);
                    Ring(p, 0.3f * size, 1.15f * size, new Color(1f, 1f, 1f, 0.6f), 0.18f, 0.6f); // a fast white punch past the edge
                    if (bCrack != null) Emit1(bCrack, new Vector3(p.x, 0.03f, p.z), Color.white, 2.6f * 0.95f * size, 2.2f, Random.value * 360f); // rim at 0.76 of the picture
                    else Crack(p, 0.85f * size, 2f, new Color(0.35f, 0.27f, 0.2f, 0.85f));
                    if (bShock != null)
                    {
                        // the dust ring's middle sits at 0.64 of the picture's half width: sized so it lands on the radius
                        Emit1(bShock, new Vector3(p.x, 0.07f, p.z), Color.white, 3.1f * size, 0.6f, Random.value * 360f);
                        Emit1(bShock, new Vector3(p.x, 0.06f, p.z), new Color(1f, 1f, 1f, 0.55f), 3.1f * 1.3f * size, 1.1f, Random.value * 360f);
                    }
                    else Ring(p, 0.25f * size, size, new Color(0.92f, 0.82f, 0.62f, 0.9f), 0.32f, 1.1f);
                    if (bDust != null) Burst(bDust, p + Vector3.up * 0.2f, 6 + Mathf.RoundToInt(3f * size), new Color32(255, 250, 240, 235), 0.75f * size, 2.4f * size, 1f, 0.5f, 0.45f * size);
                    else Dust(p + Vector3.up * 0.15f, 10, C(Pal.Ground, 220), 0.55f * size, 2.2f * size, 0.9f, 0.6f, 0.5f * size);
                    if (bRocks != null) Burst(bRocks, p + Vector3.up * 0.25f, 7 + Mathf.RoundToInt(3f * size), new Color32(255, 255, 255, 255), 0.26f + 0.05f * size, 4f + size, 0.8f, 3.8f, 0.35f * size);
                    else Chunks(p + Vector3.up * 0.2f, 7 + Mathf.RoundToInt(3f * size), C(Pal.Rock), 0.13f + 0.03f * size, 4f + size, 0.7f, 3.5f, 0.4f * size);
                    break;
                }
                case FxKind.ChainBoom:
                {
                    // Blast's chain goes off: a hot gold-violet flash, a violet shock ring ending on the true radius, gold sparks thrown up
                    // inside it and a short light. No smoke, so heroes, bars and other warnings stay readable. size = radius
                    Color violet = new Color(0.7f, 0.42f, 1f, 1f);
                    Flash(mid, 0.3f * size, 1.1f * size, new Color(1f, 0.96f, 0.82f, 0.95f), 0.12f);
                    Ring(p, 0.2f * size, size, violet, 0.3f);
                    Sparks(p + Vector3.up * 0.3f, 10, new Color32(255, 209, 77, 255), 0.13f, 3.6f, 0.4f, 3f, 0.5f * size);
                    Sparks(mid, 6, new Color32(255, 245, 209, 255), 0.1f, 3.5f, 0.18f, 0.5f);
                    Glow(p + Vector3.up * 0.8f, 2, new Color32(255, 209, 77, 200), 1.3f * size, 0.1f, 0.35f);
                    Glow(p + Vector3.up * 0.8f, 1, new Color32(179, 107, 255, 160), 1.6f * size, 0.1f, 0.45f);
                    break;
                }
                case FxKind.TowerDestroyed:
                    Pow(p + Vector3.up * 1.5f, 1, C(Pal.Cream), 3f * size, 0.3f);
                    Pic("tx_wow_warrior_impact", p + Vector3.up * 1.5f, new Color(1f, 0.9f, 0.7f, 1f), 4.5f * size, 0.4f);
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
                    Pic("tx_wow_priest_shield", mid, (Color)C(Pal.Honey), 1.7f * size, 0.2f); // the bubble bursts
                    Drops(mid, 10, C(Pal.Honey), 0.16f * size, 3f, 0.6f, 1.8f, 0.3f);
                    Shards(mid, 6, C(Pal.Honey), 0.09f * size, 3.5f, 0.5f, 1.5f, 0.3f);
                    Ring(p, 0.4f * size, 1.1f * size, new Color(1f, 0.85f, 0.3f, 0.8f), 0.3f);
                    break;
                case FxKind.KegBlast:
                {
                    // Hilde's keg (Speltest View3DStudioHilde.cs KegVerdictFx, Simon's "powder verdict"): a small quick white-hot core,
                    // dark stave and stone chips, a few hot splinters and fast sparks, a little powder smoke inside the edge, a shock
                    // front that stops dead on the true radius and a crisp amber band lying on it; the scorch stays. size = the radius
                    Color amber = new Color(1f, 0.757f, 0.435f, 1f), hot = new Color(1f, 0.96f, 0.82f, 1f);
                    Flash(mid, 0.15f * size, 0.6f * size, new Color(1f, 0.96f, 0.82f, 0.95f), 0.1f);
                    Pow(mid, 1, (Color32)hot, 1.1f * size, 0.16f);
                    Glow(p + Vector3.up * 0.07f, 1, new Color32(255, 193, 111, 200), 1.6f * size, 0f, 0.16f); // the ground lit inside the edge
                    Chunks(p + Vector3.up * 0.3f, 12, new Color32(41, 28, 20, 255), 0.14f, 4.5f, 0.55f, 3.5f, 0.15f); // stave and stone chips
                    Shards(p + Vector3.up * 0.35f, 8, (Color32)hot, 0.1f, 4.2f, 0.4f, 2f); // hot splinters
                    Sparks(p + Vector3.up * 0.35f, 16, (Color32)hot, 0.1f, 6f, 0.25f, 1.5f);
                    Smoke(p + Vector3.up * 0.3f, 5, new Color32(107, 92, 82, 115), 0.6f * size, 1f, 0.65f, 0.6f, 0.35f * size);
                    ImpactRing(p, size, hot, 0.22f); // the shock front, out to the edge
                    Spawn(Shapes.RingMesh(0.92f, Pal.White), p, size, size, amber, 0.4f, true, 0); // the finite edge, on the radius
                    Pic("tx_fx_blast_scorch", p, new Color(1f, 1f, 1f, 0.7f), 1.7f * size, 2.2f);
                    break;
                }
                case FxKind.WallCrash:
                {
                    // a hero slid into a wall by Hilde's keg (Speltest View3DSpecialFx.WallCrashFx: its own crack, flash and sound):
                    // a short white flash at the wall, a brass crack on the ground there, stone thrown off the wall and a little dust
                    Color32 m = heroMain[(int)HeroId.Hilde], a = heroAccent[(int)HeroId.Hilde];
                    Flash(p + Vector3.up * 0.6f, 0.1f, 0.55f, new Color(1f, 0.96f, 0.85f, 0.95f), 0.09f);
                    HitStar(p + Vector3.up * 0.6f, (Color)a, 1.0f, 0.16f);
                    Crack(p, 0.6f, 1.4f, new Color(1f, 0.8f, 0.5f, 0.8f));
                    ImpactRing(p, 0.6f, (Color)m, 0.2f);
                    Chunks(p + Vector3.up * 0.5f, 6, C(Pal.Stone), 0.13f, 3f, 0.6f, 2.2f, 0.2f);
                    Sparks(p + Vector3.up * 0.6f, 8, a, 0.1f, 3.5f, 0.25f, 1.2f);
                    Dust(p + Vector3.up * 0.2f, 4, C(Pal.Ground, 200), 0.45f, 1.2f, 0.5f, 0.3f, 0.2f);
                    break;
                }
                case FxKind.OakenSlam:
                {
                    // Oaken's two fists into the ground (no Speltest look exists for Oaken 3.0): the ground splits with his gold glowing
                    // in the cracks, a stone ring rolls out to the true radius, grey stone chunks and dust, a short gold light. Not
                    // Bonk's brown wood-and-dust look. size = the hit radius
                    Color gold = (Color)heroAccent[(int)HeroId.Oaken];
                    Flash(p + Vector3.up * 0.3f, 0.2f * size, 0.7f * size, new Color(1f, 0.95f, 0.8f, 0.85f), 0.08f);
                    Crack(p, 0.8f * size, 2.2f, new Color(gold.r, gold.g, gold.b, 0.9f));
                    Crack(p, 0.45f * size, 1.6f, new Color(1f, 0.95f, 0.75f, 0.8f));
                    ImpactRing(p, size, gold, 0.3f);
                    Ring(p, 0.25f * size, size, new Color(0.75f, 0.72f, 0.66f, 0.8f), 0.35f, 1.1f);
                    Rays(p + Vector3.up * 0.4f, gold, 1.1f * size, 0.18f);
                    Chunks(p + Vector3.up * 0.25f, 8 + Mathf.RoundToInt(3f * size), C(Pal.Stone), 0.14f + 0.03f * size, 4f + size, 0.75f, 3.6f, 0.4f * size);
                    Dust(p + Vector3.up * 0.15f, 8, new Color32(170, 162, 150, 210), 0.55f * size, 2f * size, 0.8f, 0.5f, 0.5f * size);
                    Glow(p + Vector3.up * 0.2f, 6, heroAccent[(int)HeroId.Oaken], 0.25f, 1.2f, 0.5f, 1.5f, 0.5f * size);
                    break;
                }
                case FxKind.OakenGuard:
                {
                    // Stored Impact takes part of a hit: his stone skin flashes gold and chips fly off it (no longer Moa's honey bubble)
                    Color32 a = heroAccent[(int)HeroId.Oaken];
                    Pow(mid, 1, a, 0.9f * size, 0.16f);
                    Flash(mid, 0.3f * size, 0.75f * size, new Color(1f, 0.85f, 0.4f, 0.6f), 0.14f);
                    Shards(mid, 6, C(Pal.Stone), 0.1f * size, 3f, 0.45f, 1.5f, 0.3f);
                    Sparks(mid, 6, a, 0.1f, 3f, 0.25f, 1f);
                    Ring(p, 0.3f * size, 0.9f * size, new Color(1f, 0.8f, 0.3f, 0.8f), 0.3f);
                    break;
                }
                case FxKind.OakenStored:
                    // the damage he holds for his next punch: gold glowing at both fists, a spark now and then (p = his feet)
                    Glow(p + Vector3.up * 0.75f, 2, new Color32(255, 200, 70, 200), 0.3f * size, 0.25f, 0.25f, 0.4f, 0.35f);
                    Embers(p + Vector3.up * 0.7f, 1, heroAccent[(int)HeroId.Oaken], 0.08f, 0.5f, 0.45f, 0.8f, 0.35f);
                    break;
                case FxKind.Cast when hero >= 0:
                    // HeroCast already gives the glow and stars: only the ground swirl and ring, in the hero's two colours
                    Swirl(p, 1.1f * size, (Color)heroMain[hero], 0.5f);
                    Ring(p, 0.3f * size, 1.1f * size, (Color)heroAccent[hero], 0.35f);
                    break;
                case FxKind.Cast:
                    Swirl(p, 1.1f * size, (Color)teamL, 0.5f);
                    Ring(p, 0.3f * size, 1.1f * size, (Color)teamL, 0.35f);
                    Glow(p + Vector3.up * 0.1f, 8, teamL, 0.18f * size, 1.2f, 0.5f, 1.6f, 0.3f);
                    Stars(p + Vector3.up * 0.5f, 3, teamL, 0.3f * size, 1f, 0.45f, 1.2f, 0.3f);
                    break;
                case FxKind.Bounce:
                    // Bram's hammer bounces from target to target: a thin gold line of glints flashes between them (MLBB analysis 4, Bram)
                    if (Time.time - lastBounceT < 0.7f)
                    {
                        Vector3 a0 = lastBounce, d0 = mid - a0;
                        int steps = Mathf.Clamp(Mathf.RoundToInt(d0.magnitude * 3f), 2, 12);
                        for (int i = 1; i < steps; i++) Glow(a0 + d0 * (i / (float)steps), 1, heroAccent[(int)HeroId.Bram], 0.16f, 0.05f, 0.15f);
                    }
                    lastBounce = mid; lastBounceT = Time.time;
                    // each bounce an anvil clink in Bram's forge colours
                    Flash(mid, 0.15f, 0.55f, new Color(1f, 1f, 1f, 0.9f), 0.06f);
                    Pow(mid, 1, heroAccent[(int)HeroId.Bram], 0.6f * size, 0.14f);
                    PicUpright("tx_fx_bram_anvil", mid + Vector3.up * 0.3f * size, (Color)heroAccent[(int)HeroId.Bram], 1.1f * size, 0.22f);
                    Sparks(mid, 8, heroMain[(int)HeroId.Bram], 0.12f, 3.5f, 0.3f, 1.2f);
                    Crack(p, 0.4f, 0.7f);
                    Ring(p, 0.2f * size, 0.8f * size, (Color)heroMain[(int)HeroId.Bram], 0.22f);
                    break;
                case FxKind.WolfSummon:
                {
                    // Fenn's own two colours (silver-cyan, purple) and the recipe's white flash as the wolf appears
                    Color32 m = heroMain[(int)HeroId.Fenn], a = heroAccent[(int)HeroId.Fenn];
                    Flash(mid, 0.2f * size, 0.8f * size, new Color(1f, 1f, 1f, 0.9f), 0.08f);
                    Pillar(p, 1.2f * size, 2.4f * size, (Color)m, 0.6f);
                    Swirl(p, 1.2f * size, (Color)m, 0.6f);
                    Pic("tx_wow_warrior_whirl", p, (Color)a, 2.6f * size, 0.6f);
                    Wisps(p + Vector3.up * 0.3f, 6, a, 0.7f * size, 1.2f, 0.8f, 1f, 0.3f);
                    Glow(p + Vector3.up * 0.3f, 12, m, 0.25f * size, 1.6f, 0.7f, 0.8f, 0.3f);
                    Stars(p + Vector3.up * 0.6f, 4, a, 0.3f * size, 1.2f, 0.6f, 1f, 0.4f);
                    Ring(p, 0.2f * size, 1.2f * size, (Color)m, 0.45f);
                    break;
                }
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
                    Pic("tx_fx_knut_splash", p, Color.white, 2.4f * size, 0.5f); // his water splash on the ground
                    Drops(mid, 10, heroAccent[(int)HeroId.Knut], 0.15f * size, 3f, 0.5f, 1.5f, 0.2f);
                    Pow(mid, 1, C(Pal.Steel), 0.9f * size, 0.16f);
                    Sparks(mid, 12, C(Pal.Steel), 0.13f, 4f, 0.35f, 1.4f);
                    Dust(p + Vector3.up * 0.1f, 6, C(Pal.Ground, 210), 0.6f * size, 1.4f, 0.6f, 0.3f, 0.2f);
                    Chunks(p + Vector3.up * 0.15f, 4, C(Pal.Rock), 0.13f * size, 2.5f, 0.6f, 2.2f);
                    Crack(p, 0.8f * size, 1f, new Color(0.45f, 0.55f, 0.7f, 0.6f)); // water, not lava
                    Ring(p, 0.3f * size, 1.0f * size, new Color(0.75f, 0.8f, 0.9f, 0.9f), 0.3f);
                    break;
            }
        }

        // Each hero's two special colours (main, accent) from the MLBB clip analysis (bam-work/art/mlbb-miya/skill-fx-analys.md),
        // index = HeroId: Bram, Fenn, Tyra, Moa, Knut, Tikta, Blast, Bonk, Hilde, Oaken. Tyra keeps the cyan her bolt already has.
        static Vector3 lastBounce;
        static float lastBounceT = -10f;

        internal static Color32 HeroMain(HeroId h) => heroMain[(int)h];
        internal static Color32 HeroAccent(HeroId h) => heroAccent[(int)h];

        static readonly Color32[] heroMain =
        {
            new Color32(255, 138, 42, 255), new Color32(191, 239, 255, 255), new Color32(180, 240, 255, 255), new Color32(255, 179, 0, 255),
            new Color32(47, 140, 255, 255), new Color32(124, 255, 79, 255), new Color32(255, 90, 31, 255),
            new Color32(196, 140, 72, 255), // Bonk: club wood and dust
            new Color32(255, 150, 50, 255), // Hilde: powder flash
            new Color32(150, 140, 125, 255), // Oaken: stone
        };
        static readonly Color32[] heroAccent =
        {
            new Color32(255, 227, 107, 255), new Color32(140, 123, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 246, 214, 255),
            new Color32(127, 255, 224, 255), new Color32(176, 76, 255, 255), new Color32(255, 242, 160, 255),
            new Color32(240, 230, 205, 255), // Bonk: bone spikes
            new Color32(200, 160, 90, 255), // Hilde: brass
            new Color32(255, 200, 70, 255), // Oaken: the gold glow in his cracks (Speltest recoloured it from cyan)
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
                case HeroId.Tyra:
                    // his light spiral pulls in to the bow, then his star flashes as she lets go
                    Pic("tx_fx_tyra_charge", p + Vector3.up * 0.9f, (Color)m, 1.8f, 0.22f);
                    Pic("tx_fx_tyra_release", p + Vector3.up * 0.8f, (Color)m, 1.7f, 0.2f);
                    break;
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
                    break;
                case ProjKind.BonkBoulder:
                {
                    // the boulder stops (on a hero or at its range): a dull thump of dust and chips, a small crack
                    Vector3 g = new Vector3(p.x, 0f, p.z);
                    if (bDust != null) Burst(bDust, g + Vector3.up * 0.3f, 5, new Color32(255, 250, 240, 230), 0.7f, 1.6f, 0.9f, 0.4f, 0.3f);
                    else Dust(g + Vector3.up * 0.3f, 6, C(Pal.Ground, 220), 0.5f, 1.5f, 0.8f, 0.4f, 0.3f);
                    if (bRocks != null) Burst(bRocks, g + Vector3.up * 0.4f, 6, new Color32(255, 255, 255, 255), 0.22f, 3.2f, 0.7f, 3f, 0.2f);
                    if (bCrack != null) Emit1(bCrack, new Vector3(p.x, 0.03f, p.z), new Color(1f, 1f, 1f, 0.8f), 1.3f, 1.4f, Random.value * 360f);
                    break;
                }
                case ProjKind.TyraVolley:
                {
                    // a volley arrow lands: Speltest's tyravolley gold, his hit star and a small ring (#fff6a8)
                    Color gold = new Color(1f, 0.965f, 0.66f, 1f);
                    HitStar(p, gold, 0.9f, 0.15f);
                    ImpactRing(new Vector3(p.x, 0f, p.z), 0.45f, gold, 0.22f);
                    Sparks(p, 5, new Color32(255, 214, 110, 255), 0.09f, 3f, 0.25f, 0.7f);
                    break;
                }
                case ProjKind.TyraArrow:
                    Stars(p, 1, C(Pal.White), 0.3f, 0f, 0.15f);
                    Sparks(p, 3, C(Pal.Cream), 0.08f, 2.5f, 0.2f, 0.6f);
                    break;
                case ProjKind.TyraBolt:
                    SpecialHit((int)HeroId.Tyra, p, 1f);
                    ImpactRing(new Vector3(p.x, 0f, p.z), 1.1f, new Color(0.75f, 0.95f, 1f, 1f), 0.3f);
                    Pow(p, 1, new Color32(180, 240, 255, 255), 0.9f, 0.16f);
                    Rays(p, new Color(0.7f, 0.95f, 1f, 1f), 1.1f, 0.18f);
                    HitStar(p, Color.white, 1.3f, 0.18f);
                    Stars(p, 3, new Color32(200, 245, 255, 255), 0.45f, 1.5f, 0.3f, 0.6f);
                    Sparks(p, 8, new Color32(180, 240, 255, 255), 0.12f, 4f, 0.3f, 0.8f);
                    break;
                case ProjKind.MoaBolt:
                    // a soft honey splat: warm-white pop, drops flung out, a small honey ring (support colours, no streaks)
                    Flash(p, 0.08f, 0.35f, new Color(1f, 0.97f, 0.85f, 0.85f), 0.06f);
                    Drops(p, 7, heroMain[(int)HeroId.Moa], 0.13f, 2.2f, 0.5f, 1.4f, 0.1f);
                    Glow(p, 2, heroAccent[(int)HeroId.Moa], 0.4f, 0.4f, 0.25f);
                    Bubbles(p, 2, heroAccent[(int)HeroId.Moa], 0.1f, 0.6f, 0.5f, 0.6f, 0.1f);
                    Ring(new Vector3(p.x, 0f, p.z), 0.1f, 0.45f, (Color)heroMain[(int)HeroId.Moa], 0.25f);
                    Pic("tx_fx_moa_honey", p, new Color(1f, 1f, 1f, 0.85f), 0.8f, 0.7f); // his honey puddle
                    break;
                case ProjKind.TiktaNeedle:
                    Bubbles(p, 3, C(Pal.Poison), 0.14f, 0.6f, 0.6f, 0.8f, 0.1f);
                    break;
                case ProjKind.TiktaNeedleSpecial:
                {
                    // four needles land together: each one small (recipe: tiny white flash, 4 green sparks, 2-3 purple bubbles)
                    Color32 m = heroMain[(int)HeroId.Tikta], a = heroAccent[(int)HeroId.Tikta];
                    Flash(p, 0.08f, 0.3f, new Color(1f, 1f, 1f, 0.9f), 0.05f);
                    Sparks(p, 4, m, 0.1f, 3f, 0.2f, 0.6f);
                    Bubbles(p, 3, a, 0.14f, 0.8f, 0.6f, 0.8f, 0.1f);
                    Pic("tx_fx_tikta_poison", p + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.9f), 0.9f, 0.5f); // his poison puff
                    Ring(new Vector3(p.x, 0f, p.z), 0.1f, 0.5f, (Color)m, 0.25f);
                    break;
                }
                case ProjKind.BlastFire:
                {
                    // the bomb pops and sets the spot alight (MLBB recipe: white core, then fire rising, embers, heat glow, smoke last)
                    Color32 m = heroMain[(int)HeroId.Blast], a = heroAccent[(int)HeroId.Blast];
                    Vector3 g = new Vector3(p.x, 0f, p.z);
                    Flash(p, 0.12f, 0.5f, new Color(1f, 1f, 1f, 0.9f), 0.06f);
                    if (!Pic("tx_fx_blast_explosion", p, Color.white, 1.2f, 0.35f)) Pow(p, 1, a, 0.7f, 0.14f);
                    Glow(p, 2, m, 0.7f, 0.2f, 0.3f);
                    Flames(g + Vector3.up * 0.15f, 7, m, 0.5f, 0.8f, 0.55f, 1.4f, 0.25f);
                    Sparks(p, 8, a, 0.09f, 3.5f, 0.4f, 1.6f);
                    Smoke(p + Vector3.up * 0.3f, 2, C(Pal.Ink, 120), 0.5f, 0.5f, 0.8f, 0.6f, 0.15f);
                    Pic("tx_fx_blast_scorch", g, new Color(1f, 1f, 1f, 0.8f), 1.1f, 1.4f);
                    Embers(p, 4, a, 0.1f, 1.2f, 0.7f, 1.2f, 0.15f);
                    Ring(g, 0.1f, 0.6f, (Color)m, 0.25f);
                    break;
                }
                case ProjKind.RangedMinionShot:
                    Pow(p, 1, C(Pal.Cream, 200), 0.35f, 0.1f);
                    break;
                case ProjKind.HildeShell:
                {
                    // her cannon shell (Speltest View3DHilde.cs: a blast, a flash and a burst in powder orange): her powder flash and
                    // brass sparks over the siege shell's dust and chips
                    Color32 m = heroMain[(int)HeroId.Hilde], a = heroAccent[(int)HeroId.Hilde];
                    Flash(p + Vector3.up * 0.3f, 0.1f, 0.45f, new Color(1f, 0.9f, 0.6f, 0.9f), 0.08f);
                    Pow(p, 1, m, 0.85f, 0.16f);
                    Sparks(p + Vector3.up * 0.3f, 6, a, 0.1f, 3.5f, 0.25f, 1.2f);
                    Dust(p, 4, C(Pal.Ground, 210), 0.6f, 1.5f, 0.5f, 0.3f, 0.2f);
                    Chunks(p, 3, C(Pal.Rock), 0.12f, 2.5f, 0.5f, 2f);
                    break;
                }
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

        /// <summary>Oaken's quake (ArtKit.PlayOakenQuake): the ground splits from a to b, his gold in the crack, grey stone and dust
        /// thrown up along it and a few gold sparks running the way it goes; nothing reaches past the line's half width.</summary>
        public static void OakenQuake(Vector3 a, Vector3 b, float halfWidth)
        {
            a.y = 0f; b.y = 0f;
            Vector3 d = b - a;
            float len = d.magnitude;
            Vector3 dir = len > 1e-4f ? d / len : Vector3.forward;
            Color gold = (Color)heroAccent[(int)HeroId.Oaken];
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.6f));
            for (int i = 0; i <= n; i++)
            {
                Vector3 q = Vector3.Lerp(a, b, i / (float)n);
                Crack(q, 1.3f * halfWidth, 2f, new Color(gold.r, gold.g, gold.b, 0.85f));
                Chunks(q + Vector3.up * 0.2f, 3, C(Pal.Stone), 0.13f, 2.5f, 0.6f, 3f, 0.6f * halfWidth);
                Dust(q + Vector3.up * 0.1f, 2, new Color32(170, 162, 150, 200), 0.5f, 1f, 0.7f, 0.4f, 0.6f * halfWidth);
                if ((i & 1) == 0) SparksAlong(q + Vector3.up * 0.2f, dir, 3, heroAccent[(int)HeroId.Oaken], 0.1f, 4f, 0.3f, 20f);
            }
            ImpactRing(b, 2f * halfWidth, gold, 0.25f);
            Flash(a + Vector3.up * 0.3f, 0.15f, 0.6f, new Color(1f, 0.95f, 0.8f, 0.8f), 0.08f);
        }

        /// <summary>A skillshot that hit nothing: a small puff in its colour, no flash, so a miss never reads as a hit.</summary>
        public static void Fizzle(ProjKind k, Vector3 p, int team)
        {
            Color32 c = k == ProjKind.TyraVolley ? new Color32(255, 230, 140, 255) : k == ProjKind.TyraBolt ? heroMain[(int)HeroId.Tyra] : k == ProjKind.TiktaNeedleSpecial ? heroMain[(int)HeroId.Tikta]
                : k == ProjKind.BramHammerSpecial ? heroMain[(int)HeroId.Bram] : C(Pal.TeamLight(team));
            Glow(p, 3, c, 0.2f, 0.6f, 0.25f, 0.3f);
            Dust(p, 2, C(Pal.Ground, 180), 0.35f, 0.6f, 0.4f, 0.2f, 0.1f);
        }

        /// <summary>What a projectile leaves behind in flight; called every few hundredths of a second.</summary>
        public static void InFlight(ProjKind k, Vector3 p, Vector3 dir, int team)
        {
            switch (k)
            {
                case ProjKind.MoaBolt: Glow(p, 1, heroAccent[(int)HeroId.Moa], 0.32f, 0.05f, 0.12f); Drops(p, 1, heroMain[(int)HeroId.Moa], 0.09f, 0.3f, 0.45f, 0.2f, 0.05f); if (Random.value < 0.3f) Bubbles(p, 1, heroAccent[(int)HeroId.Moa], 0.07f, 0.2f, 0.4f, 0.3f, 0.05f); break; // warm core, honey drips, a bubble now and then
                case ProjKind.TiktaNeedle: Bubbles(p, 1, C(Pal.Poison, 200), 0.08f, 0.1f, 0.35f, 0.3f); break;
                case ProjKind.TiktaNeedleSpecial: Bubbles(p, 1, C(Pal.Poison), 0.12f, 0.2f, 0.45f, 0.4f, 0.05f); break;
                case ProjKind.BlastFire: Smoke(p, 1, C(Pal.Ink, 90), 0.18f, 0.1f, 0.35f, 0.3f, 0.03f); break; // fuse smoke behind the thrown bomb
                case ProjKind.TyraBolt:
                    // a bright ball of light at the head, thin streaks and wisps behind, and a faint line of light left lying along
                    // the path for half a second (Rickard 2026-10-28)
                    Glow(p, 1, new Color32(200, 245, 255, 255), 0.75f, 0f, 0.08f);
                    Stars(p, 1, new Color32(200, 245, 255, 255), 0.3f, 0.2f, 0.25f, 0f, 0.05f);
                    SparksAlong(p, -dir, 1, new Color32(180, 240, 255, 255), 0.09f, 3f, 0.2f, 12f);
                    if (Random.value < 0.5f) Wisps(p, 1, new Color32(170, 235, 255, 200), 0.35f, 0.3f, 0.4f, 0.4f, 0.05f);
                    Glow(new Vector3(p.x, 0.15f, p.z), 1, new Color32(150, 225, 255, 110), 0.45f, 0f, 0.5f);
                    break;
                case ProjKind.TyraVolley: Glow(p, 1, new Color32(255, 230, 140, 220), 0.3f, 0.05f, 0.15f); if (Random.value < 0.35f) Embers(p, 1, new Color32(255, 214, 110, 255), 0.07f, 0.4f, 0.35f, 0.3f); break;
                case ProjKind.BramHammerSpecial: Sparks(p, 1, heroAccent[(int)HeroId.Bram], 0.1f, 1.5f, 0.25f, 0.3f); break; // a forge tail
                case ProjKind.TowerShot: Glow(p, 1, C(Pal.TeamLight(team)), 0.35f, 0.1f, 0.2f); break;
                case ProjKind.BonkBoulder:
                {
                    // dust thrown up behind the rolling boulder where it touches the ground, a chip now and then
                    Vector3 g = new Vector3(p.x, 0.15f, p.z) - dir * 0.35f;
                    if (bDust != null) Burst(bDust, g, 1, new Color32(255, 248, 235, 200), 0.55f, 0.5f, 0.7f, 0.35f, 0.15f);
                    else Dust(g, 1, C(Pal.Ground, 200), 0.4f, 0.5f, 0.6f, 0.3f, 0.1f);
                    if (bRocks != null && Random.value < 0.25f) Burst(bRocks, g, 1, new Color32(255, 255, 255, 255), 0.14f, 1.5f, 0.5f, 2f, 0.1f);
                    break;
                }
            }
        }
    }
}
