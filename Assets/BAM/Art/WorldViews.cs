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
        bool melee, launched, mirror, struck, arc, hit, honey, boulder;
        float roll; // Bonk's boulder: degrees rolled so far, on screen
        Color arcColor;
        static Texture2D arcTex, boulderTex, dustTrailTex;
        Transform dustT; // Bonk's boulder: Rickard's dust trail picture (tx_fx_bonk_trail) streaming out behind it
        Material dustMat;
        static bool arcLoaded, boulderLoaded, arcAlpha;
        static Material texTrail; // Rickard's comet trail picture (tx_fx_trail), shared by every shot that leaves one
        static bool texTrailLoaded;
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
            melee = k == ProjKind.FennSlash || k == ProjKind.KnutHit || k == ProjKind.WolfBite || k == ProjKind.MeleeMinionHit || k == ProjKind.OakenFist;
            var go = new GameObject("Look");
            vis = go.transform;
            vis.SetParent(transform, false);
            if (!arcLoaded)
            {
                arcLoaded = true;
                // his wide hot slash (2026-10-27, white + alpha) over the thin tx_fx_slash_arc (2026-10-26, white on black)
                arcTex = RickardFx.Load("tx_fx_slash_hot");
                arcAlpha = arcTex != null;
                if (arcTex == null) arcTex = RickardFx.Load("tx_fx_slash_arc");
            }
            arc = melee && arcTex != null; // Rickard's own slash arc: drawn additive
            bool painted = melee && !arc && FxAtlas.Texture != null; // else the painted sword arc from the effect atlas
            visF = go.AddComponent<MeshFilter>();
            // Blast throws his own skull bomb from the old game (Props/blast_bomb), not a fireball: the bomb sits on a child,
            // centred on the look so it spins round its middle
            // Hilde's powder keg is Speltest's own (AbcModels hilde_bomb, Props/hilde_keg), the big one for the drag, a small one for the tap
            bool keg = k == ProjKind.HildeKeg || k == ProjKind.HildeBurst;
            string bombName = keg ? "hilde_keg" : "blast_bomb";
            Mesh bombMesh = k == ProjKind.BlastFire || k == ProjKind.BlastBomb || keg ? Props.Mesh(bombName) : null;
            if (bombMesh != null)
            {
                var bg = new GameObject("Bomb");
                bombT = bg.transform;
                bombT.SetParent(vis, false);
                float bs = (k == ProjKind.BlastBomb || k == ProjKind.HildeKeg ? 0.62f : k == ProjKind.HildeBurst ? 0.45f : 0.42f) / Mathf.Max(0.01f, bombMesh.bounds.size.y); // tall in tiles
                bombT.localScale = new Vector3(bs, bs, bs);
                bombT.localPosition = new Vector3(0f, -bombMesh.bounds.center.y * bs, 0f);
                bombTop = bombMesh.bounds.extents.y * bs;
                bg.AddComponent<MeshFilter>().sharedMesh = bombMesh;
                var br = bg.AddComponent<MeshRenderer>();
                var bm = ArtMaterials.Instance(ArtMaterials.Lit);
                Texture2D btex = Props.Texture(bombName);
                if (btex != null) { bm.SetTexture(ArtMaterials.BaseMap, btex); bm.mainTexture = btex; }
                br.sharedMaterial = bm;
                br.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Moa's honey shot: Rickard's drop picture (atlas cell 7) facing the camera, its tail trailing, a warm glow inside (no ball)
            honey = k == ProjKind.MoaBolt && FxAtlas.Texture != null;
            // Bonk's boulder: Rickard's own picture (tx_fx_bonk_boulder) facing the camera and rolling, big and heavy
            if (k == ProjKind.BonkBoulder && !boulderLoaded) { boulderLoaded = true; boulderTex = RickardFx.Load("tx_fx_bonk_boulder"); dustTrailTex = RickardFx.Load("tx_fx_bonk_trail"); }
            boulder = k == ProjKind.BonkBoulder && boulderTex != null;
            visF.sharedMesh = boulder ? FxAtlas.FullQuad() : honey ? FxAtlas.Quad(FxAtlas.Drop) : arc ? FxAtlas.FullQuad() : painted ? FxAtlas.SlashQuad() : melee ? Shapes.Slash() : Shapes.Projectile(k, t);
            visR = go.AddComponent<MeshRenderer>();
            if (bombT != null) visR.enabled = false; // the code shape stays only as the fallback without the model
            bool glowy = k == ProjKind.BlastFire || k == ProjKind.TowerShot || k == ProjKind.TyraVolley;
            if (melee)
            {
                visR.sharedMaterial = painted ? FxAtlas.Material(ArtMaterials.UnlitAdd) : ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                if (arc)
                {
                    visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, arcTex);
                    visR.sharedMaterial.mainTexture = arcTex;
                    if (arcAlpha) RickardFx.WeighByAlpha(visR.sharedMaterial);
                }
                // the heroes' own swing colours (MLBB analysis 8): Fenn silver-cyan, Knut steel blue; the wolf ghost blue, minions the team's
                Color c = k == ProjKind.WolfBite ? (Color)Fx.HeroMain(HeroId.Fenn) : k == ProjKind.KnutHit ? (Color)Fx.HeroMain(HeroId.Knut)
                    : k == ProjKind.FennSlash ? (Color)Fx.HeroMain(HeroId.Fenn) : k == ProjKind.OakenFist ? (Color)Fx.HeroAccent(HeroId.Oaken) : (Color)Pal.Get(Pal.TeamLight(t)) * 0.6f; // minions' arcs dimmer
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, c);
                arcColor = c;
            }
            else if (honey)
            {
                visR.sharedMaterial = FxAtlas.Material(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, new Color(1f, 0.78f, 0.22f, 1f));
                visR.sharedMaterial.renderQueue = 3020;
            }
            else if (boulder)
            {
                visR.sharedMaterial = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, boulderTex);
                visR.sharedMaterial.mainTexture = boulderTex;
                visR.sharedMaterial.renderQueue = 3020;
                if (dustTrailTex != null)
                {
                    var dg = new GameObject("Dust trail");
                    dustT = dg.transform;
                    dustT.SetParent(transform, false);
                    dg.AddComponent<MeshFilter>().sharedMesh = FxAtlas.FullQuad();
                    var dr = dg.AddComponent<MeshRenderer>();
                    dustMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                    dustMat.SetTexture(ArtMaterials.BaseMap, dustTrailTex);
                    dustMat.mainTexture = dustTrailTex;
                    dustMat.renderQueue = 3010; // behind the boulder
                    dr.sharedMaterial = dustMat;
                    dr.shadowCastingMode = ShadowCastingMode.Off;
                    dr.receiveShadows = false;
                }
            }
            else visR.sharedMaterial = glowy ? ArtMaterials.Unlit : ArtMaterials.Lit;
            visR.shadowCastingMode = ShadowCastingMode.Off;

            Color trailColor;
            if (TrailColor(k, t, out trailColor))
            {
                trail = go.AddComponent<TrailRenderer>();
                Material tm = TexTrail();
                trail.sharedMaterial = tm ?? ArtKit.TrailMaterial;
                trail.time = k == ProjKind.TowerShot || k == ProjKind.TyraBolt || k == ProjKind.TyraVolley ? 0.22f : 0.16f;
                trail.minVertexDistance = 0.08f;
                trail.widthMultiplier = k == ProjKind.TyraBolt || k == ProjKind.BramHammerSpecial || k == ProjKind.TowerShot ? 0.22f : 0.14f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                if (tm != null)
                {
                    // the picture is the comet itself (head, soft streaks, fading tail) stretched once along the trail, so the width
                    // stays and the picture's own glow is the edge
                    trail.textureMode = LineTextureMode.Stretch;
                    trail.widthMultiplier *= 2.2f;
                    trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
                }
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

        static Material TexTrail()
        {
            if (texTrailLoaded) return texTrail;
            texTrailLoaded = true;
            var t = RickardFx.Load("tx_fx_trail"); // turned so its head is on the left: a trail's u = 0 is its newest point
            if (t == null) return null;
            t.wrapMode = TextureWrapMode.Clamp;
            texTrail = RickardFx.WeighByAlpha(ArtMaterials.Instance(ArtMaterials.ParticleAdd));
            texTrail.SetTexture(ArtMaterials.BaseMap, t);
            texTrail.mainTexture = t;
            return texTrail;
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
                case ProjKind.TyraVolley: c = new Color(1f, 0.92f, 0.5f, 0.95f); return true; // the volley's warm gold (Speltest tyravolley)
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
            lob = kind == ProjKind.BlastBomb || kind == ProjKind.SiegeShell || kind == ProjKind.HildeKeg || kind == ProjKind.HildeBurst || kind == ProjKind.HildeShell ? Mathf.Clamp(dist * 0.3f, 0.6f, 2.2f)
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
        static bool CanMiss(ProjKind k) => k == ProjKind.TyraBolt || k == ProjKind.TyraVolley || k == ProjKind.TiktaNeedleSpecial || k == ProjKind.BramHammerSpecial;

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
                case ProjKind.HildeShell:
                case ProjKind.HildeKeg:
                case ProjKind.HildeBurst:
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
                case ProjKind.BonkBoulder when boulder:
                {
                    // face the camera and roll the way it moves across the screen; the stone fills 0.92 of the picture, so a
                    // 1.2 quad is a boulder about 1.1 tiles across standing on the ground
                    Quaternion cam = ArtDriver.CamRot;
                    vis.rotation = cam * Quaternion.Euler(0f, 0f, roll) * Quaternion.Euler(-90f, 0f, 0f);
                    vis.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                    if (dustT != null)
                    {
                        // the cloud's head (right of the picture) at the boulder, its tail streaming back the way it came on
                        // screen; it swells in over the first moments of the roll
                        Vector3 r = cam * Vector3.right, u = cam * Vector3.up;
                        float ang = Mathf.Atan2(Vector3.Dot(dir, u), Vector3.Dot(dir, r)) * Mathf.Rad2Deg;
                        float grow = Mathf.Clamp01(age / 0.25f), len = 2.8f * (0.5f + 0.5f * grow);
                        dustT.rotation = cam * Quaternion.Euler(0f, 0f, ang) * Quaternion.Euler(-90f, 0f, 0f);
                        dustT.localScale = new Vector3(len, len, len);
                        dustT.position = transform.position + new Vector3(0f, 0.35f, 0f) - dustT.right * (len * 0.35f);
                        dustMat.SetColor(ArtMaterials.BaseColor, new Color(1f, 1f, 1f, 0.9f * grow));
                    }
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
            float dt = ArtKit.Dt;
            age += dt;
            Vector3 p = transform.position;
            Vector3 d = p - lastPos; d.y = 0f;
            if (d.sqrMagnitude > 1e-6f) dir = d.normalized;
            if (boulder) roll -= Vector3.Dot(d, ArtDriver.CamRot * Vector3.right) / 0.55f * Mathf.Rad2Deg; // rolls without slipping
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
                    // Rickard's spikes fly out behind a hero's blow; the wolf leaves his three claw marks
                    if (kind != ProjKind.MeleeMinionHit) Fx.Spikes(at, to - from, (Color)sc, kind == ProjKind.WolfBite ? 0.8f : 1.1f, 0.16f);
                    if (kind == ProjKind.WolfBite) Fx.PicUpright("tx_fx_fenn_claw", at, (Color)Fx.HeroMain(HeroId.Fenn), 1.1f, 0.25f);
                    if (kind == ProjKind.KnutHit) Fx.Drops(at, 5, Fx.HeroAccent(HeroId.Knut), 0.12f, 2.2f, 0.4f, 1.2f, 0.1f);
                }
                Material m = visR.sharedMaterial;
                if (arc) m.SetColor(ArtMaterials.BaseColor, new Color(arcColor.r * (1f - k), arcColor.g * (1f - k), arcColor.b * (1f - k), 1f)); // additive on black: dim to fade
                else m.SetColor(ArtMaterials.BaseColor, WithAlpha(m.GetColor(ArtMaterials.BaseColor), 1f - k));
            }
            if (kind == ProjKind.BlastBomb || kind == ProjKind.BlastFire || kind == ProjKind.HildeKeg || kind == ProjKind.HildeBurst)
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
            // the shot's own splash where it ended (bombs and the anchor have their own effects from the game layer); also when
            // the shot came and went inside one hitch frame and was never drawn, so every attack still shows its hit
            if (launched && !melee && kind != ProjKind.BlastBomb && kind != ProjKind.HildeKeg && kind != ProjKind.HildeBurst && kind != ProjKind.KnutAnchor)
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
        // Blast's Inferno Lane as the old game drew it (View3DStudioBlast.cs "Ässans vrede"): from the first burn two burning strips
        // along the lane's edges and a glow down its middle, segment by segment so each piece flickers on its own, and a row of flame
        // tongues inside it that leap on every burn, which also throws embers (FireLook)
        const int LaneSegs = 10, LaneTongues = 10;
        MeshRenderer[] laneFloor, laneTongue, laneCore;
        GameObject laneFire;
        int laneTicks, laneLit;
        // Rickard 2026-10-27 ("blasts drag skill känns lite för lite och enkel"): the lane catches fire from Blast to its end in a
        // quick run of his fireballs (tx_fx_blast_explosion), leaves his burn marks (tx_fx_blast_scorch) and throws his embers
        const float LaneIgniteStep = 0.035f, LaneIgniteEvery = 0.6f;

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
                laneTicks = 0;
                if (laneFire == null && FireLook.Ready) MakeLaneFire();
                if (laneFire != null) laneFire.SetActive(false); // lights with the first burn, as the old game
                int n = Mathf.Clamp(Mathf.RoundToInt(length / 1.5f), 1, 6);
                for (int i = 0; i < n; i++) Fx.Crack(center + dir * (length * (i + 0.5f) / n), radius * 1.1f, duration, 0.55f);
                laneLit = 0;
                int sc = Mathf.Clamp(Mathf.RoundToInt(length / 0.9f), 1, 8);
                for (int i = 0; i < sc; i++)
                    Fx.Pic("tx_fx_blast_scorch", center + dir * (length * (i + 0.5f) / sc), new Color(1f, 1f, 1f, 0.75f), radius * 3f, duration + 0.6f);
            }
            else if (kind == ZoneKind.HoneyField)
            {
                Fx.Swirl(center, radius, new Color(1f, 0.75f, 0.2f, 1f), Mathf.Min(duration, 1.2f));
                Fx.Ring(center, radius * 0.3f, radius, new Color(1f, 0.85f, 0.35f, 0.9f), 0.45f);
                Fx.Drops(center + Vector3.up * 0.4f, 8, Pal.Get(Pal.Honey), 0.14f, 2f, 0.5f, 1.2f, radius * 0.4f);
            }
            Tick(0f);
        }

        void LateUpdate() => Tick(ArtKit.Dt);

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
            if (kind == ZoneKind.FireLane && length > 0f) LaneIgnite();
            if (kind == ZoneKind.FireLane && length > 0f && laneFire != null) LaneFire();

            emitT -= dt;
            if (emitT > 0f || dt <= 0f) return;
            emitT = ArtKit.High ? (kind == ZoneKind.FireLane ? 0.045f : 0.07f) : 0.15f;
            Vector3 p = RandomPoint();
            if (kind == ZoneKind.FireLane && laneFire != null)
            {
                if (Random.value < 0.06f) Fx.Smoke(p + Vector3.up * 0.6f, 1, new Color32(40, 32, 30, 90), 0.4f, 0.2f, 0.9f, 0.6f); // the tongues and burns carry the rest
            }
            else if (kind == ZoneKind.FireLane)
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

        // the fire runs down the lane: one fireball per step, an ember spray and a thump of light under it
        void LaneIgnite()
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / LaneIgniteEvery), 1, 12);
            int want = Mathf.Min(steps, Mathf.FloorToInt(age / LaneIgniteStep) + 1);
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            Color32 accent = Fx.HeroAccent(HeroId.Blast), main = Fx.HeroMain(HeroId.Blast);
            for (; laneLit < want; laneLit++)
            {
                Vector3 p = center + dir * (length * (laneLit + 0.5f) / steps) + side * ((Random.value * 2f - 1f) * radius * 0.3f);
                bool last = laneLit == steps - 1;
                Fx.Pic("tx_fx_blast_explosion", p + Vector3.up * 0.4f, Color.white, (last ? 1.9f : 1.4f) * Mathf.Max(1f, radius * 2f), last ? 0.45f : 0.35f);
                Fx.Embers(p + Vector3.up * 0.2f, last ? 6 : 3, accent, 0.11f, 1.8f, 0.8f, 1.8f, radius * 0.4f);
                Fx.Flames(p + Vector3.up * 0.1f, 2, main, 0.5f, 0.6f, 0.45f, 1.5f, radius * 0.3f);
                if (laneLit == 0 || last) Fx.ImpactRing(p, radius * (last ? 2.2f : 1.4f), new Color(1f, 0.55f, 0.15f, 0.9f), 0.3f);
            }
        }

        void MakeLaneFire()
        {
            laneFire = new GameObject("Inferno");
            laneFire.transform.SetParent(transform, false);
            laneFloor = new MeshRenderer[LaneSegs * 3];
            for (int i = 0; i < laneFloor.Length; i++) laneFloor[i] = FireLook.Piece(laneFire.transform, true);
            laneTongue = new MeshRenderer[LaneTongues];
            laneCore = new MeshRenderer[LaneTongues];
            for (int i = 0; i < LaneTongues; i++) { laneTongue[i] = FireLook.Piece(laneFire.transform, false); laneCore[i] = FireLook.Piece(laneFire.transform, false); }
        }

        // the old BlastInfernoStrips / BlastLaneBody / BlastLaneTick, on the zone's own clock (the sim burns every LaneTick s)
        void LaneFire()
        {
            const float tick = BlastS.LaneTick;
            int ticks = Mathf.Min(Mathf.RoundToInt(duration / tick), Mathf.FloorToInt((age + 1e-4f) / tick));
            if (ticks > laneTicks)
            {
                laneTicks = ticks;
                // one burn: embers thrown up all along the strip, inside it
                int n = Mathf.Max(4, Mathf.RoundToInt(length * 1.6f));
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                for (int i = 0; i < n; i++)
                    Fx.Embers(center + dir * ((i + Random.value) / n * length) + side * ((Random.value * 2f - 1f) * radius * 0.8f) + Vector3.up * 0.15f,
                        1, new Color32(255, 217, 115, 255), 0.15f, 0.3f, 0.6f, 1.85f);
                // and a few of his fireballs flare up along it on every burn
                for (int i = 0; i < 2; i++)
                    Fx.Pic("tx_fx_blast_explosion", center + dir * (Random.Range(0.15f, 0.95f) * length) + side * ((Random.value * 2f - 1f) * radius * 0.5f) + Vector3.up * 0.35f,
                        new Color(1f, 1f, 1f, 0.9f), 1.0f, 0.3f);
            }
            if (age < tick) { if (laneFire.activeSelf) laneFire.SetActive(false); return; }
            if (!laneFire.activeSelf) laneFire.SetActive(true);
            float now = age, since = age - laneTicks * tick, pulse = Mathf.Clamp01(1f - since / 0.2f); pulse *= pulse;
            float fin = Mathf.Clamp01((duration - age) / 0.15f) * 0.6f + 0.4f, intro = Mathf.Clamp01((age - tick) / 0.12f);
            float a = fin * Mathf.Max(intro, pulse);
            Vector3 nrm = new Vector3(-dir.z, 0f, dir.x);
            float hw = radius, edge = Mathf.Min(0.2f, hw * 0.45f);
            int segs = Mathf.Clamp(Mathf.CeilToInt(length / 0.6f), 1, LaneSegs);
            float seg = length / segs, lx = center.x * 3f;
            Color ec = team == Teams.Player ? FireLook.Forge : FireLook.ForgeFoe;
            int k = 0;
            for (int i = 0; i < LaneSegs; i++)
            {
                bool on = i < segs;
                for (int j = 0; j < 3; j++, k++)
                {
                    var fr = laneFloor[k];
                    if (fr.enabled != on) fr.enabled = on;
                    if (!on) continue;
                    float fl = 0.7f + 0.3f * Mathf.Sin(now * 21f + i * 1.9f + lx);
                    Vector3 mid = center + dir * ((i + 0.5f) * seg) + new Vector3(0f, 0.03f, 0f);
                    if (j < 2)
                    {
                        float o = (j == 0 ? -1f : 1f) * (hw - edge * 0.5f);
                        FireLook.Floor(fr, mid + nrm * o, dir, edge, seg, Color.Lerp(ec, FireLook.ForgeHot, pulse * 0.7f), (0.6f + 0.4f * pulse) * fl * a, 1f);
                    }
                    else FireLook.Floor(fr, mid - new Vector3(0f, 0.005f, 0f), dir, hw * 1.6f, seg, FireLook.Forge, (0.22f + 0.3f * pulse) * fl * a, 1f);
                }
            }
            int m = Mathf.Clamp(Mathf.RoundToInt(length / 0.5f), 2, LaneTongues);
            for (int i = 0; i < LaneTongues; i++)
            {
                bool on = i < m;
                if (laneTongue[i].enabled != on) { laneTongue[i].enabled = on; laneCore[i].enabled = on; }
                if (!on) continue;
                float dd = (i + 0.5f) * length / m, o = ((i & 1) == 0 ? -0.35f : 0.35f) * hw, fl = 0.8f + 0.2f * Mathf.Sin(now * 17f + i * 2.3f);
                float hgt = (0.45f + 0.25f * ((i * 7) % 3) / 2f + 0.65f * pulse) * fl, w = 0.38f + 0.1f * pulse;
                Vector3 foot = center + dir * dd + nrm * o + new Vector3(0f, -0.04f, 0f);
                FireLook.Tongue(laneTongue[i], foot, hgt, w, 0f, Color.Lerp(FireLook.Forge, FireLook.ForgeHot, 0.25f + 0.5f * pulse), 0.75f * a, 1f);
                FireLook.Tongue(laneCore[i], foot, hgt * 0.6f, w * 0.45f, 0f, FireLook.ForgeHot, 0.5f * a, 1f);
            }
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

        // aims are not pooled (ArtKit.Release destroys them), and their two private materials would outlive the GameObject
        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (rangeMat != null) Destroy(rangeMat);
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
