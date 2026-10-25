using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// Anything that flies. The game layer moves the transform on the ground plane (y = 0); the look is drawn at hand
    /// height above it, faces the way it moves, spins (hammers, bombs) or arcs (bombs, siege shells) over its flight time.
    /// </summary>
    sealed class ProjView : ProjectileView
    {
        internal Stack<ProjView> Pool;
        internal bool InPool;
        ProjKind kind;
        int team;
        Transform vis, chainT;
        MeshRenderer visR, chainR;
        TrailRenderer trail;
        Transform tether;
        Vector3 lastPos, dir = Vector3.forward, from, to;
        float age, flight = 0.3f, spin, lob, sparkT;
        bool melee;

        const float Height = 0.55f;

        public void Build(ProjKind k, int t)
        {
            kind = k;
            team = t;
            melee = k == ProjKind.FennSlash || k == ProjKind.KnutHit || k == ProjKind.WolfBite || k == ProjKind.MeleeMinionHit;
            var go = new GameObject("Look");
            vis = go.transform;
            vis.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = melee ? Shapes.Slash() : Shapes.Projectile(k, t);
            visR = go.AddComponent<MeshRenderer>();
            bool glowy = k == ProjKind.BlastFire || k == ProjKind.TowerShot;
            if (melee)
            {
                visR.sharedMaterial = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                Color c = k == ProjKind.WolfBite ? new Color(0.6f, 0.9f, 1f, 1f) : k == ProjKind.KnutHit ? new Color(0.85f, 0.9f, 1f, 1f) : (Color)Pal.Get(Pal.TeamLight(t));
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, c);
            }
            else visR.sharedMaterial = glowy ? ArtMaterials.Unlit : ArtMaterials.Lit;
            visR.shadowCastingMode = ShadowCastingMode.Off;

            Color trailColor;
            if (TrailColor(k, t, out trailColor))
            {
                trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = ArtKit.TrailMaterial;
                trail.time = k == ProjKind.TowerShot || k == ProjKind.TyraBolt ? 0.22f : 0.16f;
                trail.minVertexDistance = 0.08f;
                trail.widthMultiplier = k == ProjKind.TyraBolt || k == ProjKind.BramHammerSpecial || k == ProjKind.TowerShot ? 0.22f : 0.14f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                var g = new Gradient();
                Color end = trailColor; end.a = 0f;
                g.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
                    new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.generateLightingData = false;
            }

            if (k == ProjKind.KnutAnchor)
            {
                var cg = new GameObject("Chain");
                chainT = cg.transform;
                chainT.SetParent(transform, false);
                cg.AddComponent<MeshFilter>().sharedMesh = Shapes.Chain();
                chainR = cg.AddComponent<MeshRenderer>();
                chainR.sharedMaterial = ArtMaterials.Lit;
                chainR.shadowCastingMode = ShadowCastingMode.Off;
                chainR.enabled = false;
            }
        }

        static bool TrailColor(ProjKind k, int team, out Color c)
        {
            switch (k)
            {
                case ProjKind.TyraBolt: c = new Color(0.7f, 0.95f, 1f, 0.9f); return true;
                case ProjKind.MoaBolt: c = new Color(1f, 0.8f, 0.25f, 0.8f); return true;
                case ProjKind.BlastFire: c = new Color(1f, 0.5f, 0.1f, 0.9f); return true;
                case ProjKind.TiktaNeedleSpecial: c = new Color(0.2f, 1f, 0.85f, 0.8f); return true;
                case ProjKind.BramHammerSpecial: c = Pal.Get(Pal.TeamLight(team)); c.a = 0.8f; return true;
                case ProjKind.TowerShot: c = Pal.Get(Pal.TeamLight(team)); c.a = 0.9f; return true;
                case ProjKind.TyraArrow: c = new Color(1f, 1f, 1f, 0.45f); return true;
                default: c = Color.clear; return false;
            }
        }

        public override void Launch(Vector3 a, Vector3 b, float flightTime)
        {
            from = a; to = b;
            flight = Mathf.Max(0.05f, flightTime);
            age = 0f; spin = 0f; sparkT = 0f;
            Vector3 d = b - a; d.y = 0f;
            dir = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
            float dist = d.magnitude;
            lob = kind == ProjKind.BlastBomb || kind == ProjKind.SiegeShell ? Mathf.Clamp(dist * 0.3f, 0.6f, 2.2f) : 0f;
            lastPos = transform.position;
            vis.localScale = Vector3.one;
            Place(0f);
            if (trail != null) { trail.Clear(); trail.emitting = ArtKit.High; }
            if (melee) visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, WithAlpha(visR.sharedMaterial.GetColor(ArtMaterials.BaseColor), 1f));
        }

        public override void SetTether(Transform anchor)
        {
            tether = anchor;
            if (chainR != null) chainR.enabled = anchor != null;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void Place(float t)
        {
            float h = melee ? 0.45f : Height + lob * 4f * t * (1f - t);
            vis.localPosition = new Vector3(0f, h, 0f);
            Quaternion look = Quaternion.LookRotation(dir);
            switch (kind)
            {
                case ProjKind.BramHammer:
                case ProjKind.BramHammerSpecial:
                    vis.rotation = look * Quaternion.Euler(spin, 0f, 0f);
                    break;
                case ProjKind.BlastBomb:
                case ProjKind.SiegeShell:
                    vis.rotation = look * Quaternion.Euler(spin * 0.5f, 0f, 0f);
                    break;
                default:
                    if (lob > 0f) vis.rotation = look * Quaternion.Euler(Mathf.Lerp(-35f, 35f, t), 0f, 0f);
                    else vis.rotation = look;
                    break;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            age += dt;
            Vector3 p = transform.position;
            Vector3 d = p - lastPos; d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            lastPos = p;
            spin += dt * 900f;
            float t = Mathf.Clamp01(age / flight);
            Place(t);
            if (melee)
            {
                // the swing arc sweeps and fades over a short moment, however long the hit takes
                float life = Mathf.Min(flight, 0.18f) + 0.12f;
                float k = Mathf.Clamp01(age / life);
                float s = 0.7f + k * 0.5f;
                vis.localScale = new Vector3(s, 1f, s);
                vis.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, Mathf.Lerp(-40f, 40f, k), 0f);
                Material m = visR.sharedMaterial;
                m.SetColor(ArtMaterials.BaseColor, WithAlpha(m.GetColor(ArtMaterials.BaseColor), 1f - k));
            }
            if (kind == ProjKind.BlastBomb || kind == ProjKind.BlastFire)
            {
                sparkT -= dt;
                if (sparkT <= 0f)
                {
                    sparkT = ArtKit.High ? 0.05f : 0.1f;
                    Fx.Sparks(vis.position + (kind == ProjKind.BlastBomb ? vis.up * 0.27f : Vector3.zero), 1, Pal.Get(Pal.FireCore), 0.1f, 1f, 0.25f, 0.5f);
                }
            }
            if (chainT != null && tether != null)
            {
                Vector3 a = tether.position + new Vector3(0f, Height, 0f), b = vis.position;
                Vector3 ab = b - a;
                float len = ab.magnitude;
                if (len > 0.01f)
                {
                    chainT.position = a;
                    chainT.rotation = Quaternion.LookRotation(ab / len);
                    chainT.localScale = new Vector3(1f, 1f, len);
                }
            }
        }

        public void OnRelease()
        {
            tether = null;
            if (chainR != null) chainR.enabled = false;
            if (trail != null) trail.Clear();
        }
    }

    /// <summary>Honey field, fire lane, bomb fuse: a tinted circle or strip on the ground that fades or counts down.</summary>
    sealed class ZoneLook : ZoneView
    {
        internal Stack<ZoneLook> Pool;
        internal bool InPool;
        ZoneKind kind;
        int team;
        Transform shape, inner;
        MeshFilter shapeF;
        MeshRenderer shapeR, innerR;
        Material mat, innerMat;
        Vector3 center, dir;
        float length, radius, duration, age, emitT;
        Color baseColor;
        bool warn;

        public void Build(ZoneKind k, int t)
        {
            kind = k;
            team = t;
            var go = new GameObject("Shape");
            shape = go.transform;
            shape.SetParent(transform, false);
            shapeF = go.AddComponent<MeshFilter>();
            shapeR = go.AddComponent<MeshRenderer>();
            mat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            mat.renderQueue = 2990; // under unit rings and bars
            shapeR.sharedMaterial = mat;
            shapeR.shadowCastingMode = ShadowCastingMode.Off;
            shapeR.receiveShadows = false;
            warn = k == ZoneKind.WarnLine || k == ZoneKind.WarnCircle;
            // warnings: the enemy's in strong red (that is what players dodge), our own team's in a quiet white
            baseColor = k == ZoneKind.HoneyField ? new Color(1f, 0.78f, 0.2f, 1f)
                : k == ZoneKind.FireLane ? new Color(1f, 0.45f, 0.08f, 1f)
                : warn ? (t == Teams.Player ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.12f, 0.1f, 1f))
                : (t == Teams.Player ? new Color(1f, 0.6f, 0.15f, 1f) : new Color(1f, 0.15f, 0.12f, 1f));
            if (warn) mat.renderQueue = 2995; // over honey and fire, under unit rings
            if (k == ZoneKind.BombFuse || warn)
            {
                // the inner fill grows to the rim (or the end of the line) as the time runs out
                var ig = new GameObject("Fuse");
                inner = ig.transform;
                inner.SetParent(transform, false);
                ig.AddComponent<MeshFilter>().sharedMesh = k == ZoneKind.WarnLine ? Shapes.AimBody() : Shapes.Disc(Pal.WhiteMid);
                innerR = ig.AddComponent<MeshRenderer>();
                innerMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                innerMat.renderQueue = warn ? 2996 : 2991;
                innerMat.SetColor(ArtMaterials.BaseColor, baseColor);
                innerR.sharedMaterial = innerMat;
                innerR.shadowCastingMode = ShadowCastingMode.Off;
                innerR.receiveShadows = false;
            }
        }

        public override void Show(Vector3 c, Vector3 d, float len, float r, float dur)
        {
            center = c; length = Mathf.Max(0f, len); radius = Mathf.Max(0.05f, r); duration = Mathf.Max(0.05f, dur);
            d.y = 0f;
            dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            age = 0f; emitT = 0f;
            transform.position = new Vector3(c.x, warn ? 0.04f : 0.03f, c.z);
            transform.rotation = Quaternion.LookRotation(dir);
            if (length > 0f)
            {
                shapeF.sharedMesh = Shapes.ZoneStrip();
                shape.localScale = new Vector3(radius, 1f, length);
            }
            else
            {
                shapeF.sharedMesh = Shapes.ZoneCircle();
                shape.localScale = new Vector3(radius, 1f, radius);
            }
            Tick(0f);
        }

        void LateUpdate() => Tick(Time.deltaTime);

        void Tick(float dt)
        {
            age += dt;
            float t = Mathf.Clamp01(age / duration);
            float a;
            if (warn)
            {
                // fades in fast, then the fill races to the end: when it arrives the shot leaves
                float fadeIn = Mathf.Clamp01(age / 0.1f);
                a = fadeIn * (0.85f + Mathf.Sin(age * 18f) * 0.15f);
                if (length > 0f)
                {
                    inner.localScale = new Vector3(radius * 2f * 0.84f, 1f, Mathf.Max(0.01f, length * t));
                    inner.localPosition = new Vector3(0f, 0.004f, 0f);
                }
                else
                {
                    float s = radius * Mathf.Lerp(0.05f, 0.94f, t);
                    inner.localScale = new Vector3(s, 1f, s);
                    inner.localPosition = new Vector3(0f, 0.004f, 0f);
                }
                Color ic = baseColor; ic.a = baseColor.a * fadeIn * 0.8f;
                innerMat.SetColor(ArtMaterials.BaseColor, ic);
            }
            else if (kind == ZoneKind.BombFuse)
            {
                a = 0.75f + Mathf.Sin(age * (10f + t * 25f)) * 0.25f; // blinks faster as it is about to blow
                float s = radius * Mathf.Lerp(0.1f, 0.95f, t);
                inner.localScale = new Vector3(s, 1f, s);
                inner.localPosition = new Vector3(0f, 0.004f, 0f);
            }
            else
            {
                float fadeIn = Mathf.Clamp01(age / 0.15f), fadeOut = Mathf.Clamp01((duration - age) / 0.35f);
                a = fadeIn * fadeOut * (0.85f + Mathf.Sin(age * 4f) * 0.1f);
            }
            Color c = baseColor; c.a = baseColor.a * a;
            mat.SetColor(ArtMaterials.BaseColor, c);
            if (warn) return;

            emitT -= dt;
            if (emitT > 0f || dt <= 0f) return;
            emitT = ArtKit.High ? 0.07f : 0.15f;
            Vector3 p = RandomPoint();
            if (kind == ZoneKind.FireLane)
            {
                Fx.Glow(p, 1, Pal.Get(Pal.Fire), 0.45f, 0.2f, 0.45f, 1.6f);
                Fx.Glow(p, 1, Pal.Get(Pal.FireCore), 0.25f, 0.2f, 0.3f, 1.2f);
            }
            else if (kind == ZoneKind.HoneyField) Fx.Glow(p, 1, Pal.Get(Pal.Honey), 0.18f, 0.1f, 0.9f, 0.5f);
            else Fx.Sparks(center + Vector3.up * 0.1f, 1, Pal.Get(Pal.FireCore), 0.08f, 1.5f, 0.2f, 1f);
        }

        Vector3 RandomPoint()
        {
            if (length > 0f)
            {
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                return center + dir * Random.Range(0f, length) + side * Random.Range(-radius, radius) * 0.85f + Vector3.up * 0.05f;
            }
            Vector2 q = Random.insideUnitCircle * radius * 0.9f;
            return center + new Vector3(q.x, 0.05f, q.y);
        }
    }

    /// <summary>The special's aim (line with arrow head or circle, red when invalid) and the attack range ring.</summary>
    sealed class AimLook : AimView
    {
        Transform line, head, circle, range;
        MeshRenderer lineR, headR, circleR, rangeR;
        Material mat, rangeMat;
        static readonly Color ok = new Color(1f, 1f, 1f, 0.85f), bad = new Color(1f, 0.2f, 0.2f, 0.85f), rangeCol = new Color(1f, 1f, 1f, 0.35f);

        public void Build()
        {
            mat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            mat.renderQueue = 3010;
            rangeMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            rangeMat.renderQueue = 3005;
            rangeMat.SetColor(ArtMaterials.BaseColor, rangeCol);
            line = Part("Line", Shapes.AimBody(), mat, out lineR);
            head = Part("Head", Shapes.AimHead(), mat, out headR);
            circle = Part("Circle", Shapes.ZoneCircle(), mat, out circleR);
            range = Part("Range", Shapes.RingMesh(0.96f, Pal.White, 64), rangeMat, out rangeR);
            Hide();
        }

        Transform Part(string name, Mesh m, Material material, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return go.transform;
        }

        public override void ShowLine(Vector3 from, Vector3 d, float length, float width, bool valid)
        {
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            d.Normalize();
            width = Mathf.Max(0.15f, width);
            float headLen = Mathf.Min(length * 0.35f, Mathf.Max(0.35f, width * 1.2f));
            Quaternion q = Quaternion.LookRotation(d);
            line.SetPositionAndRotation(new Vector3(from.x, 0.06f, from.z), q);
            line.localScale = new Vector3(width, 1f, Mathf.Max(0.01f, length - headLen));
            head.SetPositionAndRotation(new Vector3(from.x, 0.061f, from.z) + d * (length - headLen), q);
            head.localScale = new Vector3(width * 1.7f, 1f, headLen);
            mat.SetColor(ArtMaterials.BaseColor, valid ? ok : bad);
            lineR.enabled = true; headR.enabled = true; circleR.enabled = false;
        }

        public override void ShowCircle(Vector3 center, float radius, bool valid)
        {
            circle.SetPositionAndRotation(new Vector3(center.x, 0.06f, center.z), Quaternion.identity);
            circle.localScale = new Vector3(radius, 1f, radius);
            mat.SetColor(ArtMaterials.BaseColor, valid ? ok : bad);
            circleR.enabled = true; lineR.enabled = false; headR.enabled = false;
        }

        public override void ShowRange(Vector3 center, float radius)
        {
            range.SetPositionAndRotation(new Vector3(center.x, 0.05f, center.z), Quaternion.identity);
            range.localScale = new Vector3(radius, 1f, radius);
            rangeR.enabled = radius > 0.01f;
        }

        public override void Hide()
        {
            if (lineR == null) return;
            lineR.enabled = false; headR.enabled = false; circleR.enabled = false; rangeR.enabled = false;
        }
    }
}
