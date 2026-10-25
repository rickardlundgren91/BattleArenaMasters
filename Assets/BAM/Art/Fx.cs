using System.Collections.Generic;
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

    /// <summary>Particles (four shared systems, so all sparks of the match cost four draw calls) and pooled rings/flashes.</summary>
    static class Fx
    {
        static ParticleSystem glow, spark, smoke, shard;
        static ParticleSystem.EmitParams ep;
        public static float Amount = 1f; // 1 on High, about half on Low

        sealed class Blob
        {
            public Transform T;
            public MeshRenderer R;
            public MeshFilter F;
            public Material Mat;
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
            glow = MakeSystem("BAM Glow", ArtMaterials.ParticleAdd, 0f, 1f, 0.2f, 800);
            spark = MakeSystem("BAM Sparks", ArtMaterials.ParticleAdd, 1.6f, 1f, 0.4f, 600);
            smoke = MakeSystem("BAM Smoke", ArtMaterials.ParticleAlpha, -0.05f, 0.6f, 1.8f, 300);
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

        static ParticleSystem MakeSystem(string name, Material mat, float gravity, float sizeStart, float sizeEnd, int max)
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
                ps.Emit(ep, 1);
            }
            ep.rotation3D = Vector3.zero;
        }

        public static void Glow(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(glow, pos, n, c, size, speed, life, up, jitter);
        public static void Sparks(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(spark, pos, n, c, size, speed, life, up, jitter);
        public static void Smoke(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(smoke, pos, n, c, size, speed, life, up, jitter);
        public static void Shards(Vector3 pos, int n, Color32 c, float size, float speed, float life, float up = 0f, float jitter = 0f) => Burst(shard, pos, n, c, size, speed, life, up, jitter);

        // ---------------------------------------------------------------- rings and flashes

        static Blob NewBurst()
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            go.SetActive(false);
            var bu = new Blob { T = go.transform, F = go.AddComponent<MeshFilter>(), R = go.AddComponent<MeshRenderer>() };
            bu.Mat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
            bu.R.sharedMaterial = bu.Mat;
            bu.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bu.R.receiveShadows = false;
            return bu;
        }

        /// <summary>An expanding ring on the ground (flat) or a swelling ball (flash). Radii in units.</summary>
        public static void Ring(Vector3 pos, float r0, float r1, Color c, float life, float thickness = 0.8f) => Spawn(Shapes.RingMesh(thickness, Pal.White), pos, r0, r1, c, life, true);

        public static void Flash(Vector3 pos, float r0, float r1, Color c, float life) => Spawn(Shapes.Ball(), pos, r0, r1, c, life, false);

        static void Spawn(Mesh mesh, Vector3 pos, float r0, float r1, Color c, float life, bool flat)
        {
            Blob bu = free.Count > 0 ? free.Pop() : NewBurst();
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
                    Sparks(mid, 6, C(Pal.FireCore), 0.12f * size, 3f, 0.25f, 1f);
                    Flash(mid, 0.1f * size, 0.35f * size, new Color(1f, 0.95f, 0.8f, 0.8f), 0.12f);
                    break;
                case FxKind.HitBig:
                    Sparks(mid, 12, C(Pal.Fire), 0.16f * size, 4.5f, 0.35f, 1.5f);
                    Glow(mid, 4, C(Pal.FireCore), 0.5f * size, 0.5f, 0.2f);
                    Flash(mid, 0.2f * size, 0.6f * size, new Color(1f, 0.85f, 0.5f, 0.9f), 0.16f);
                    Ring(p, 0.2f * size, 0.9f * size, new Color(1f, 0.8f, 0.4f, 0.8f), 0.25f);
                    break;
                case FxKind.Death:
                    Smoke(mid, 10, C(Pal.Cloth, 200), 0.45f * size, 1.2f, 0.8f, 0.6f, 0.2f);
                    Shards(mid, 10, teamC, 0.1f * size, 3f, 0.8f, 2.5f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 1f, 1f, 0.7f), 0.4f);
                    break;
                case FxKind.Respawn:
                    Glow(p + Vector3.up * 0.1f, 18, teamL, 0.22f * size, 0.6f, 0.8f, 2.4f, 0.35f);
                    Ring(p, 0.2f * size, 1.2f * size, (Color)teamL, 0.5f);
                    Flash(mid, 0.3f * size, 0.8f * size, new Color(1f, 1f, 1f, 0.6f), 0.25f);
                    break;
                case FxKind.LevelUp:
                    Glow(p + Vector3.up * 0.2f, 20, C(Pal.Gold), 0.2f * size, 0.8f, 0.9f, 2.2f, 0.4f);
                    Ring(p, 0.3f * size, 1.3f * size, new Color(1f, 0.85f, 0.25f, 0.9f), 0.5f);
                    Ring(p, 0.2f * size, 0.8f * size, new Color(1f, 1f, 0.7f, 0.9f), 0.35f, 0.6f);
                    break;
                case FxKind.Explosion:
                    Flash(mid, 0.4f * size, 1.4f * size, new Color(1f, 0.6f, 0.2f, 1f), 0.25f);
                    Glow(mid, 16, C(Pal.Fire), 0.6f * size, 2.5f * size, 0.45f, 1f, 0.3f * size);
                    Sparks(mid, 14, C(Pal.FireCore), 0.14f, 6f * size, 0.5f, 2f);
                    Smoke(mid, 10, C(Pal.Ink, 170), 0.8f * size, 1.4f * size, 1.0f, 0.8f, 0.4f * size);
                    Shards(mid, 8, C(Pal.Bomb), 0.09f, 5f, 0.7f, 3f);
                    Ring(p, 0.3f * size, 1.8f * size, new Color(1f, 0.7f, 0.3f, 0.9f), 0.35f);
                    break;
                case FxKind.TowerDestroyed:
                    Flash(p + Vector3.up * 1.5f, 0.5f * size, 2.4f * size, new Color(1f, 0.9f, 0.7f, 1f), 0.4f);
                    Shards(p + Vector3.up * 1.4f, 30, C(Pal.Stone), 0.22f * size, 5f, 1.4f, 4f, 0.8f);
                    Shards(p + Vector3.up * 1.8f, 10, teamC, 0.16f * size, 5f, 1.4f, 5f, 0.5f);
                    Smoke(p + Vector3.up * 0.8f, 20, C(Pal.Cloth, 220), 1.2f * size, 2f, 1.8f, 1f, 1f);
                    Ring(p, 0.8f * size, 4f * size, new Color(1f, 1f, 1f, 0.8f), 0.6f);
                    Ring(p, 0.5f * size, 2.6f * size, (Color)teamL, 0.45f, 0.6f);
                    break;
                case FxKind.ShieldPop:
                    Shards(mid, 10, C(Pal.Honey), 0.09f * size, 3.5f, 0.5f, 1.5f, 0.3f);
                    Flash(mid, 0.5f * size, 0.9f * size, new Color(1f, 0.85f, 0.35f, 0.6f), 0.18f);
                    Ring(p, 0.4f * size, 1.1f * size, new Color(1f, 0.85f, 0.3f, 0.8f), 0.3f);
                    break;
                case FxKind.Cast:
                    Ring(p, 0.3f * size, 1.1f * size, (Color)teamL, 0.35f);
                    Glow(p + Vector3.up * 0.1f, 10, teamL, 0.18f * size, 1.2f, 0.5f, 1.6f, 0.3f);
                    break;
                case FxKind.Bounce:
                    Sparks(mid, 8, C(Pal.Steel), 0.12f, 3.5f, 0.3f, 1.2f);
                    Ring(p, 0.2f * size, 0.8f * size, new Color(0.9f, 0.95f, 1f, 0.8f), 0.22f);
                    break;
                case FxKind.WolfSummon:
                    Glow(p + Vector3.up * 0.3f, 18, C(Pal.Ghost), 0.25f * size, 1.6f, 0.7f, 0.8f, 0.3f);
                    Smoke(p + Vector3.up * 0.2f, 6, C(Pal.GhostDark, 140), 0.6f * size, 0.8f, 0.7f, 0.4f, 0.3f);
                    Ring(p, 0.2f * size, 1.2f * size, new Color(0.6f, 0.9f, 1f, 0.9f), 0.45f);
                    break;
                case FxKind.CloneSpawn:
                case FxKind.CloneEnd:
                    Smoke(mid, 10, C(Pal.Violet, 170), 0.5f * size, 1.2f, 0.6f, 0.5f, 0.25f);
                    Glow(mid, 10, C(Pal.GhostDark), 0.18f * size, 2f, 0.4f, 0.5f, 0.3f);
                    if (kind == FxKind.CloneSpawn) Ring(p, 0.2f * size, 0.9f * size, new Color(0.6f, 0.45f, 1f, 0.8f), 0.3f);
                    break;
                case FxKind.MinionSpawn:
                    Smoke(p + Vector3.up * 0.15f, 6, C(Pal.Cloth, 180), 0.35f * size, 1f, 0.5f, 0.2f, 0.2f);
                    Ring(p, 0.2f * size, 0.7f * size, (Color)teamL, 0.3f);
                    break;
                case FxKind.TowerShotFlash:
                    Flash(p, 0.15f * size, 0.6f * size, (Color)teamL, 0.15f);
                    Glow(p, 6, teamL, 0.2f * size, 2f, 0.25f);
                    break;
                case FxKind.AnchorHit:
                    Sparks(mid, 12, C(Pal.Steel), 0.13f, 4f, 0.35f, 1.4f);
                    Smoke(p + Vector3.up * 0.1f, 5, C(Pal.Ground, 200), 0.4f * size, 1.2f, 0.5f, 0.3f);
                    Ring(p, 0.3f * size, 1.0f * size, new Color(0.75f, 0.8f, 0.9f, 0.9f), 0.3f);
                    break;
            }
        }
    }
}
