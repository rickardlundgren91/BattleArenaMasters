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
        float age, flight = 0.3f, spin, lob, sparkT, trailFxT;
        bool melee, launched, mirror, struck, arc, hit, honey;
        Color arcColor;
        static Texture2D arcTex;
        static bool arcLoaded;
        MeshFilter visF;
        Transform bombT;
        float bombTop = 0.27f;
        // every other swing comes from the other side (MLBB: the combo alternates left and right), counted per kind so minions
        // round Fenn do not break his rhythm
        static readonly int[] swings = new int[32];

        const float Height = 0.55f;

        public void Build(ProjKind k, int t)
        {
            kind = k;
            team = t;
            melee = k == ProjKind.FennSlash || k == ProjKind.KnutHit || k == ProjKind.WolfBite || k == ProjKind.MeleeMinionHit;
            var go = new GameObject("Look");
            vis = go.transform;
            vis.SetParent(transform, false);
            if (!arcLoaded) { arcLoaded = true; arcTex = RickardFx.Load("tx_fx_slash_arc"); }
            arc = melee && arcTex != null; // Rickard's own slash arc (2026-10-26), white on black: drawn additive
            bool painted = melee && !arc && FxAtlas.Texture != null; // else the painted sword arc from the effect atlas
            visF = go.AddComponent<MeshFilter>();
            // Blast throws his own skull bomb from the old game (Props/blast_bomb), not a fireball: the bomb sits on a child,
            // centred on the look so it spins round its middle
            Mesh bombMesh = k == ProjKind.BlastFire || k == ProjKind.BlastBomb ? Props.Mesh("blast_bomb") : null;
            if (bombMesh != null)
            {
                var bg = new GameObject("Bomb");
                bombT = bg.transform;
                bombT.SetParent(vis, false);
                float bs = (k == ProjKind.BlastBomb ? 0.62f : 0.42f) / Mathf.Max(0.01f, bombMesh.bounds.size.y); // tall in tiles
                bombT.localScale = new Vector3(bs, bs, bs);
                bombT.localPosition = new Vector3(0f, -bombMesh.bounds.center.y * bs, 0f);
                bombTop = bombMesh.bounds.extents.y * bs;
                bg.AddComponent<MeshFilter>().sharedMesh = bombMesh;
                var br = bg.AddComponent<MeshRenderer>();
                var bm = ArtMaterials.Instance(ArtMaterials.Lit);
                Texture2D btex = Props.Texture("blast_bomb");
                if (btex != null) { bm.SetTexture(ArtMaterials.BaseMap, btex); bm.mainTexture = btex; }
                br.sharedMaterial = bm;
                br.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Moa's honey shot: Rickard's drop picture (atlas cell 7) facing the camera, its tail trailing, a warm glow inside (no ball)
            honey = k == ProjKind.MoaBolt && FxAtlas.Texture != null;
            visF.sharedMesh = honey ? FxAtlas.Quad(FxAtlas.Drop) : arc ? FxAtlas.FullQuad() : painted ? FxAtlas.SlashQuad() : melee ? Shapes.Slash() : Shapes.Projectile(k, t);
            visR = go.AddComponent<MeshRenderer>();
            if (bombT != null) visR.enabled = false; // the code shape stays only as the fallback without the model
            bool glowy = k == ProjKind.BlastFire || k == ProjKind.TowerShot;
            if (melee)
            {
                visR.sharedMaterial = painted ? FxAtlas.Material(ArtMaterials.UnlitAdd) : ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                if (arc) { visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, arcTex); visR.sharedMaterial.mainTexture = arcTex; }
                // the heroes' own swing colours (MLBB analysis 8): Fenn silver-cyan, Knut steel blue; the wolf ghost blue, minions the team's
                Color c = k == ProjKind.WolfBite ? (Color)Fx.HeroMain(HeroId.Fenn) : k == ProjKind.KnutHit ? (Color)Fx.HeroMain(HeroId.Knut)
                    : k == ProjKind.FennSlash ? (Color)Fx.HeroMain(HeroId.Fenn) : (Color)Pal.Get(Pal.TeamLight(t)) * 0.6f; // minions' arcs dimmer
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, c);
                arcColor = c;
            }
            else if (honey)
            {
                visR.sharedMaterial = FxAtlas.Material(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, new Color(1f, 0.78f, 0.22f, 1f));
                visR.sharedMaterial.renderQueue = 3020;
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
                case ProjKind.BlastFire: c = new Color(1f, 0.55f, 0.2f, 0.45f); return true; // a faint fuse-smoke streak behind the bomb
                case ProjKind.TiktaNeedleSpecial: c = new Color(0.2f, 1f, 0.85f, 0.8f); return true;
                case ProjKind.BramHammerSpecial: c = Fx.HeroMain(HeroId.Bram); c.a = 0.8f; return true; // his forge orange, not the team's
                case ProjKind.TowerShot: c = Pal.Get(Pal.TeamLight(team)); c.a = 0.9f; return true;
                case ProjKind.TyraArrow: c = new Color(1f, 1f, 1f, 0.45f); return true;
                default: c = Color.clear; return false;
            }
        }

        public override void Launch(Vector3 a, Vector3 b, float flightTime)
        {
            from = a; to = b;
            flight = Mathf.Max(0.05f, flightTime);
            age = 0f; spin = 0f; sparkT = 0f; trailFxT = 0f;
            launched = true;
            hit = false;
            Vector3 d = b - a; d.y = 0f;
            dir = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
            float dist = d.magnitude;
            lob = kind == ProjKind.BlastBomb || kind == ProjKind.SiegeShell ? Mathf.Clamp(dist * 0.3f, 0.6f, 2.2f)
                : kind == ProjKind.BlastFire && bombT != null ? Mathf.Clamp(dist * 0.15f, 0.25f, 0.9f) : 0f; // a thrown bomb arcs a little
            lastPos = transform.position;
            vis.localScale = Vector3.one;
            Place(0f);
            if (trail != null) { trail.Clear(); trail.emitting = ArtKit.High; }
            if (melee)
            {
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, arc ? arcColor : WithAlpha(visR.sharedMaterial.GetColor(ArtMaterials.BaseColor), 1f));
                mirror = (++swings[(int)kind & 31] & 1) == 0;
                struck = false;
                if (!arc && FxAtlas.Texture != null) visF.sharedMesh = FxAtlas.SlashQuad(mirror);
            }
        }

        // the heroes' blades get the big arc; the wolf a smaller one, minions small (recipe: they must not fill the lane)
        static float ArcScale(ProjKind k) => k == ProjKind.FennSlash || k == ProjKind.KnutHit ? 1.6f : k == ProjKind.WolfBite ? 1.0f : 0.8f;

        // skillshots can miss: their big splash only plays when the game says they hit
        static bool CanMiss(ProjKind k) => k == ProjKind.TyraBolt || k == ProjKind.TiktaNeedleSpecial || k == ProjKind.BramHammerSpecial;

        public override void MarkHit() => hit = true;

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
                case ProjKind.BlastFire when bombT != null:
                case ProjKind.SiegeShell:
                    vis.rotation = look * Quaternion.Euler(spin * 0.5f, 0f, 0f);
                    break;
                case ProjKind.MoaBolt when honey:
                {
                    // face the camera, the drop's point (picture top) trailing behind the flight on screen
                    Quaternion cam = ArtDriver.CamRot;
                    Vector3 r = cam * Vector3.right, u = cam * Vector3.up;
                    float roll = Mathf.Atan2(Vector3.Dot(dir, u), Vector3.Dot(dir, r)) * Mathf.Rad2Deg + 90f;
                    vis.rotation = cam * Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(-90f, 0f, 0f);
                    vis.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    break;
                }
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
                float s = (0.7f + k * 0.5f) * (arc ? ArcScale(kind) : 1f);
                vis.localScale = new Vector3(s, 1f, s);
                vis.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, mirror ? Mathf.Lerp(40f, -40f, k) : Mathf.Lerp(-40f, 40f, k), 0f);
                if (!struck && age >= Mathf.Min(flight, 0.18f))
                {
                    // the hit sprays out behind the target in the swing's direction, with a small white core (MLBB analysis 7-8)
                    struck = true;
                    Vector3 at = to + new Vector3(0f, 0.5f, 0f);
                    Color32 sc = kind == ProjKind.KnutHit ? Fx.HeroAccent(HeroId.Knut) : kind == ProjKind.FennSlash || kind == ProjKind.WolfBite ? Fx.HeroMain(HeroId.Fenn) : new Color32(255, 220, 160, 255);
                    Fx.Flash(at, 0.1f, 0.4f, new Color(1f, 1f, 1f, 0.9f), 0.05f);
                    Fx.SparksAlong(at, to - from, kind == ProjKind.MeleeMinionHit ? 3 : 7, sc, 0.11f, 4.5f, 0.2f);
                    if (kind == ProjKind.KnutHit) Fx.Drops(at, 5, Fx.HeroAccent(HeroId.Knut), 0.12f, 2.2f, 0.4f, 1.2f, 0.1f);
                }
                Material m = visR.sharedMaterial;
                if (arc) m.SetColor(ArtMaterials.BaseColor, new Color(arcColor.r * (1f - k), arcColor.g * (1f - k), arcColor.b * (1f - k), 1f)); // additive on black: dim to fade
                else m.SetColor(ArtMaterials.BaseColor, WithAlpha(m.GetColor(ArtMaterials.BaseColor), 1f - k));
            }
            if (kind == ProjKind.BlastBomb || kind == ProjKind.BlastFire)
            {
                sparkT -= dt;
                if (sparkT <= 0f)
                {
                    sparkT = ArtKit.High ? 0.05f : 0.1f;
                    Fx.Sparks(vis.position + (kind == ProjKind.BlastBomb || bombT != null ? vis.up * bombTop : Vector3.zero), 1, Pal.Get(Pal.FireCore), 0.1f, 1f, 0.25f, 0.5f); // the lit fuse
                }
            }
            if (!melee)
            {
                trailFxT -= dt;
                if (trailFxT <= 0f)
                {
                    trailFxT = ArtKit.High ? 0.045f : 0.09f;
                    Fx.InFlight(kind, vis.position, dir, team);
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
            // the shot's own splash where it ended (bombs and the anchor have their own effects from the game layer)
            if (launched && !melee && age > 0.02f && kind != ProjKind.BlastBomb && kind != ProjKind.KnutAnchor)
            {
                if (CanMiss(kind) && !hit) Fx.Fizzle(kind, vis.position, team);
                else Fx.Impact(kind, vis.position, team);
            }
            launched = false;
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
        Material mat, innerMat, rimMat;
        Transform rim; // Rickard's telegraph ring round a circle warning or bomb fuse (tx_fx_telegraph_ring), when the picture exists
        static Texture2D rimTex;
        static bool rimLoaded;
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
                : k == ZoneKind.FireLane ? new Color(1f, 0.45f, 0.08f, 0.35f) // a faint heat floor: the cracks and flames carry the look
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
                if (!rimLoaded) { rimLoaded = true; rimTex = RickardFx.Load("tx_fx_telegraph_ring"); }
                if (k != ZoneKind.WarnLine && rimTex != null)
                {
                    var rg = new GameObject("Rim");
                    rim = rg.transform;
                    rim.SetParent(transform, false);
                    rim.localPosition = new Vector3(0f, 0.006f, 0f);
                    rg.AddComponent<MeshFilter>().sharedMesh = FxAtlas.FullQuad();
                    var rr = rg.AddComponent<MeshRenderer>();
                    rimMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                    rimMat.SetTexture(ArtMaterials.BaseMap, rimTex);
                    rimMat.mainTexture = rimTex;
                    rr.sharedMaterial = rimMat;
                    rr.shadowCastingMode = ShadowCastingMode.Off;
                    rr.receiveShadows = false;
                }
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
                if (rim != null) rim.localScale = new Vector3(radius * 2.255f, 1f, radius * 2.255f); // his ring peaks at 0.887 of the picture: edge exactly on the radius
            }
            // MLBB analysis 4: Blast's lane gets a glowing cracked floor, Moa's field a honey swirl and a ring as it opens
            if (kind == ZoneKind.FireLane && length > 0f)
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(length / 1.5f), 1, 6);
                for (int i = 0; i < n; i++) Fx.Crack(center + dir * (length * (i + 0.5f) / n), radius * 1.1f, duration, 0.55f);
            }
            else if (kind == ZoneKind.HoneyField)
            {
                Fx.Swirl(center, radius, new Color(1f, 0.75f, 0.2f, 1f), Mathf.Min(duration, 1.2f));
                Fx.Ring(center, radius * 0.3f, radius, new Color(1f, 0.85f, 0.35f, 0.9f), 0.45f);
                Fx.Drops(center + Vector3.up * 0.4f, 8, Pal.Get(Pal.Honey), 0.14f, 2f, 0.5f, 1.2f, radius * 0.4f);
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
            if (rimMat != null)
            {
                bool on = length <= 0f;
                rim.gameObject.SetActive(on);
                // additive: the colour's brightness is its strength
                if (on) rimMat.SetColor(ArtMaterials.BaseColor, new Color(baseColor.r * a, baseColor.g * a, baseColor.b * a, 1f));
            }
            if (warn) return;

            emitT -= dt;
            if (emitT > 0f || dt <= 0f) return;
            emitT = ArtKit.High ? (kind == ZoneKind.FireLane ? 0.045f : 0.07f) : 0.15f;
            Vector3 p = RandomPoint();
            if (kind == ZoneKind.FireLane)
            {
                Fx.Flames(p, 1, Pal.Get(Pal.Fire), 0.55f, 0.2f, 0.5f, 1.4f);
                Fx.Glow(p, 1, Pal.Get(Pal.FireCore), 0.25f, 0.2f, 0.3f, 1.2f);
                Fx.Sparks(p, 1, Fx.HeroAccent(HeroId.Blast), 0.05f, 0.5f, 0.6f, 1.6f, 0.1f); // embers
                if (Random.value < 0.15f) Fx.Smoke(p + Vector3.up * 0.6f, 1, new Color32(40, 32, 30, 90), 0.4f, 0.2f, 0.9f, 0.6f);
                if (length > 0f)
                {
                    // the old Inferno Lane's two burning edges
                    Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (radius * (Random.value < 0.5f ? 1f : -1f));
                    Fx.Flames(center + dir * (Random.value * length) + side + Vector3.up * 0.1f, 1, Pal.Get(Pal.FireCore), 0.45f, 0.15f, 0.45f, 1.2f);
                }
            }
            else if (kind == ZoneKind.HoneyField)
            {
                Fx.Glow(p, 1, Pal.Get(Pal.Honey), 0.18f, 0.1f, 0.9f, 0.5f);
                if (Random.value < 0.35f) Fx.Bubbles(p, 1, Pal.Get(Pal.Honey), 0.12f, 0.05f, 0.7f, 0.25f);
                if (Random.value < 0.15f) Fx.Drops(p + Vector3.up * 0.6f, 1, Pal.Get(Pal.Honey), 0.12f, 0.1f, 0.6f, -0.4f); // slow honey drips
            }
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
