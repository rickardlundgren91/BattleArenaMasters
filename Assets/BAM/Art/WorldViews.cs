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
        MeshFilter chainF;
        // Rickard's chain picture (O-052, 2026-11-04, iron links on alpha, 4:1, seamless left-right): a camera-facing strip with the
        // picture repeated along it, so the hook no longer reads as a grey band from afar. Without it the code links stay.
        LineRenderer chainLine;
        static Texture2D chainTex;
        static Material chainMat;
        static bool chainLoaded;
        const float ChainWidth = 0.18f; // tiles; one picture (two links) every 4 widths
        int chainLinks = 16;
        const float ChainLink = 0.17f; // tiles per chain link, whatever the chain's length
        TrailRenderer trail;
        Transform tether;
        Vector3 lastPos, dir = Vector3.forward, from, to;
        float age, flight = 0.3f, spin, lob, sparkT, trailFxT;
        bool melee, launched, mirror, struck, arc, hit, honey, boulder, boltPic, towerPic, kuggPic;
        bool spent;                  // it hurt someone earlier and flew on: ends without a splash or a fizzle
        float prog = -1f;            // the rules' share of the flight done (SetProgress), -1 = by the clock
        Vector3 launchOff;           // the hand's offset from the rules' path at the release, eased out over LaunchEase
        bool launchEase;
        const float LaunchEase = 0.08f;
        float roll; // Bonk's boulder: degrees rolled so far, on screen
        Color arcColor;
        static Texture2D arcTex, boulderTex, dustTrailTex;
        Transform dustT; // Bonk's boulder: Rickard's dust trail picture (tx_fx_bonk_trail) streaming out behind it
        Material dustMat;
        static bool arcLoaded, boulderLoaded, arcAlpha;
        // Tyra's bolt: Rickard's own arrow picture (tx_fx_tyra_bolt, head to the right) when it exists, else the code-made bolt
        static Texture2D boltTex;
        static bool boltLoaded;
        // the tower's shot: Rickard's own bolt picture (tx_fx_tower_bolt, 2026-11-02, white + alpha, head to the right) in the team's colour
        static Texture2D towerBoltTex;
        static bool towerBoltLoaded;
        // Kugg's shots (Rickard's own O-063 cannon ball and O-064 spark, 2026-11-09, white + alpha, the ball flying right): tinted his turquoise
        static Texture2D kuggShotTex, kuggSparkTex;
        static bool kuggLoaded;
        static Material texTrail; // Rickard's comet trail picture (tx_fx_trail), shared by every shot that leaves one
        static bool texTrailLoaded;
        static Material tyraTrail; // Tyra's own bolt streak (O-056, 2026-11-04): pointed head, thin fading tail
        static bool tyraTrailLoaded;
        MeshFilter visF;
        Transform bombT;
        float bombTop = 0.27f;
        // every other swing comes from the other side (MLBB: the combo alternates left and right), counted per kind so minions
        // round Fenn do not break his rhythm
        static readonly int[] swings = new int[32];

        const float Height = 0.55f;
        const float AnchorLen = 0.8f; // Knut's thrown anchor, ring to flukes, in tiles (the code-made one was about 0.5)
        const float HammerLen = 1f; // Bram's thrown hammer, handle end to head, in tiles: about as long as in his fist (Weapons.cs Len 0.7 of his 1.59-tile figure)
        const float HammerLenAuto = 0.8f; // his auto attack's: a little smaller, so the Hammer Line stays the big throw
        Transform hammerT; MeshFilter hammerF; Material hammerMat; string hammerModel; float hammerLen, heat;

        public void Build(ProjKind k, int t)
        {
            kind = k;
            team = t;
            melee = k == ProjKind.FennSlash || k == ProjKind.KnutHit || k == ProjKind.WolfBite || k == ProjKind.MeleeMinionHit || k == ProjKind.OakenFist || k == ProjKind.RagnClaw || k == ProjKind.RavnaSlash;
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
            if (k == ProjKind.TyraBolt && !boltLoaded) { boltLoaded = true; boltTex = RickardFx.Load("tx_fx_tyra_bolt"); }
            boltPic = k == ProjKind.TyraBolt && boltTex != null;
            if (k == ProjKind.TowerShot && (!towerBoltLoaded || towerBoltTex == null)) { towerBoltLoaded = true; towerBoltTex = RickardFx.Load("tx_fx_tower_bolt"); }
            towerPic = k == ProjKind.TowerShot && towerBoltTex != null;
            bool kuggBall = k == ProjKind.KuggShot || k == ProjKind.KuggShell;
            if ((kuggBall || k == ProjKind.KuggSpark) && !kuggLoaded) { kuggLoaded = true; kuggShotTex = RickardFx.Load("tx_fx_kugg_shot"); kuggSparkTex = RickardFx.Load("tx_fx_kugg_spark"); }
            kuggPic = kuggBall ? kuggShotTex != null : k == ProjKind.KuggSpark && kuggSparkTex != null;
            // Knut's thrown anchor: the same model he holds (Props/knut_anchor), not the code-made one (Rickard 2026-10-30 "knuts
            // ankare måste göras om"): flukes first, the ring (where the chain hangs) back toward Knut, the flat side up to the camera
            Mesh anchorMesh = k == ProjKind.KnutAnchor ? Props.Mesh("knut_anchor") : null;
            if (anchorMesh != null)
            {
                var ag = new GameObject("Anchor");
                var at = ag.transform;
                at.SetParent(vis, false);
                float s = AnchorLen / Mathf.Max(0.01f, anchorMesh.bounds.size.y); // long in tiles
                at.localScale = new Vector3(s, s, s);
                at.localRotation = Quaternion.Euler(-90f, 0f, 0f); // the model's +y (the ring) to the back
                at.localPosition = -(at.localRotation * (anchorMesh.bounds.center * s));
                ag.AddComponent<MeshFilter>().sharedMesh = anchorMesh;
                var ar = ag.AddComponent<MeshRenderer>();
                var am = ArtMaterials.Instance(ArtMaterials.Lit);
                Texture2D atex = Props.Texture("knut_anchor");
                if (atex != null) { am.SetTexture(ArtMaterials.BaseMap, atex); am.mainTexture = atex; }
                ar.sharedMaterial = am;
                ar.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Bram's thrown hammer, his Hammer Line and since 2026-11-16 every auto attack too: the hammer that leaves his fist
            // (BodyView.ThrowOnCast and PlayAttack), not a code-made box hammer beside the one still in his hand (hero reviews
            // 2026-11-13 and 2026-11-16; Rickard's rule: no code graphics where there is an asset). It starts as the V2 hammer (the old
            // body's without it) and SetWeapon swaps in the one in his hand at each throw, his skin's own too. The handle along the
            // look's up and the head along the flight, so it tumbles end over end as the box did
            bool bramHammer = k == ProjKind.BramBolt || k == ProjKind.BramHammer;
            Mesh hammerMesh = null;
            if (bramHammer)
            {
                hammerLen = k == ProjKind.BramBolt ? HammerLen : HammerLenAuto;
                hammerModel = Props.Mesh("v2_bram_hammer") != null ? "v2_bram_hammer" : "bram_hammer_new";
                hammerMesh = Props.Mesh(hammerModel);
            }
            if (hammerMesh != null)
            {
                var hg = new GameObject("Hammer");
                hammerT = hg.transform;
                hammerT.SetParent(vis, false);
                hammerF = hg.AddComponent<MeshFilter>();
                var hr = hg.AddComponent<MeshRenderer>();
                hammerMat = ArtMaterials.Instance(ArtMaterials.Lit);
                hr.sharedMaterial = hammerMat;
                hr.shadowCastingMode = ShadowCastingMode.Off;
                PlaceHammer(hammerMesh, hammerModel);
            }
            else hammerModel = null;
            visF.sharedMesh = boulder || boltPic || towerPic || kuggPic ? FxAtlas.FullQuad() : honey ? FxAtlas.Quad(FxAtlas.Drop) : arc ? FxAtlas.FullQuad() : painted ? FxAtlas.SlashQuad() : melee ? Shapes.Slash() : Shapes.Projectile(k, t);
            visR = go.AddComponent<MeshRenderer>();
            if (bombT != null) visR.enabled = false; // the code shape stays only as the fallback without the model
            if (anchorMesh != null || hammerMesh != null) visR.enabled = false;
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
                    : k == ProjKind.FennSlash ? (Color)Fx.HeroMain(HeroId.Fenn) : k == ProjKind.OakenFist ? (Color)Fx.HeroAccent(HeroId.Oaken)
                    : k == ProjKind.RagnClaw ? (Color)Fx.HeroMain(HeroId.Ragn) : k == ProjKind.RavnaSlash ? (Color)Fx.HeroMain(HeroId.Ravna) : (Color)Pal.Get(Pal.TeamLight(t)) * 0.6f; // minions' arcs dimmer
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, c);
                arcColor = c;
            }
            else if (honey)
            {
                visR.sharedMaterial = FxAtlas.Material(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, new Color(1f, 0.78f, 0.22f, 1f));
                visR.sharedMaterial.renderQueue = 3020;
            }
            else if (boltPic)
            {
                visR.sharedMaterial = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, boltTex);
                visR.sharedMaterial.mainTexture = boltTex;
                visR.sharedMaterial.renderQueue = 3020;
            }
            else if (towerPic)
            {
                visR.sharedMaterial = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, towerBoltTex);
                visR.sharedMaterial.mainTexture = towerBoltTex;
                Color tc = Pal.Get(Pal.TeamLight(t)); tc.a = 1f;
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, tc); // white picture tinted: the red team shoots red
                visR.sharedMaterial.renderQueue = 3020;
            }
            else if (kuggPic)
            {
                var kt = k == ProjKind.KuggSpark ? kuggSparkTex : kuggShotTex;
                visR.sharedMaterial = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                visR.sharedMaterial.SetTexture(ArtMaterials.BaseMap, kt);
                visR.sharedMaterial.mainTexture = kt;
                Color kc = Fx.HeroMain(HeroId.Kugg); kc.a = 1f;
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, kc); // the white picture in his turquoise (his look's glow: SetSkin)
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
                Material tm = k == ProjKind.TyraBolt ? TyraTrail() ?? TexTrail() : TexTrail();
                bool tyraPic = k == ProjKind.TyraBolt && tm != null && tm == tyraTrail;
                trail.sharedMaterial = tm ?? ArtKit.TrailMaterial;
                // the volley's five arrows (Skill Visibility 2.0): at 18 tiles/s a 0.22 s trail is 4 tiles, her whole range, so every
                // trail reached back to the bow and the five ran together into one gold wedge; 0.09 s (1.6 tiles) and a thinner line
                // keep five arrows apart from a tile out (they fly 10 degrees apart: 0.17 tiles apart a tile out, 0.7 at the end)
                trail.time = k == ProjKind.BramBolt ? 0.22f : k == ProjKind.TyraBolt ? 0.3f : k == ProjKind.TyraVolley ? 0.09f : k == ProjKind.TowerShot ? 0.22f : 0.16f;
                trail.minVertexDistance = 0.08f;
                trail.widthMultiplier = k == ProjKind.BramBolt ? 0.32f : k == ProjKind.TyraBolt ? 0.44f : k == ProjKind.BramHammerSpecial || k == ProjKind.TowerShot ? 0.22f : k == ProjKind.TyraVolley ? 0.08f : 0.14f; // Tyra's bolt: twice as wide (2026-10-28)
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                if (tm != null)
                {
                    // the picture is the comet itself (head, soft streaks, fading tail) stretched once along the trail, so the width
                    // stays and the picture's own glow is the edge
                    trail.textureMode = LineTextureMode.Stretch;
                    trail.widthMultiplier *= 2.2f;
                    trail.widthCurve = tyraPic ? AnimationCurve.Constant(0f, 1f, 1f) : AnimationCurve.Linear(0f, 1f, 1f, 0.6f); // her picture tapers itself
                }
                var g = new Gradient();
                Color end = trailColor; end.a = 0f;
                g.SetKeys(new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
                    new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                autoTrailW = trail.widthMultiplier;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.generateLightingData = false;
            }

            if (k == ProjKind.KnutAnchor && ChainMat() != null)
            {
                var cg = new GameObject("Chain");
                cg.transform.SetParent(transform, false);
                chainLine = cg.AddComponent<LineRenderer>();
                chainLine.sharedMaterial = chainMat;
                chainLine.useWorldSpace = true;
                chainLine.positionCount = 2;
                chainLine.widthMultiplier = ChainWidth;
                chainLine.textureMode = LineTextureMode.Tile;
                chainLine.alignment = LineAlignment.View;
                chainLine.numCapVertices = 0;
                chainLine.shadowCastingMode = ShadowCastingMode.Off;
                chainLine.receiveShadows = false;
                chainLine.generateLightingData = false;
                chainLine.enabled = false;
            }
            else if (k == ProjKind.KnutAnchor)
            {
                var cg = new GameObject("Chain");
                chainT = cg.transform;
                chainT.SetParent(transform, false);
                chainF = cg.AddComponent<MeshFilter>();
                chainF.sharedMesh = Shapes.Chain(chainLinks = 16);
                chainR = cg.AddComponent<MeshRenderer>();
                chainR.sharedMaterial = ArtMaterials.Lit;
                chainR.shadowCastingMode = ShadowCastingMode.Off;
                chainR.enabled = false;
            }
        }

        static Material ChainMat()
        {
            if (chainLoaded) return chainMat;
            chainLoaded = true;
            chainTex = RickardFx.Load("tx_fx_knut_chain");
            if (chainTex == null) return null;
            chainTex.wrapMode = TextureWrapMode.Repeat;
            chainMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            chainMat.name = "BAM Knut chain";
            chainMat.SetTexture(ArtMaterials.BaseMap, chainTex);
            chainMat.mainTexture = chainTex;
            chainMat.SetColor(ArtMaterials.BaseColor, Color.white);
            // Tile mode puts one u per world unit along the line: one picture every 4 chain widths keeps the links round
            var sc = new Vector2(1f / (ChainWidth * 4f), 1f);
            chainMat.SetTextureScale(ArtMaterials.BaseMap, sc);
            chainMat.mainTextureScale = sc;
            return chainMat;
        }

        static Material TyraTrail()
        {
            if (tyraTrailLoaded) return tyraTrail;
            tyraTrailLoaded = true;
            var t = RickardFx.Load("tx_fx_tyra_bolt_trail"); // head on the left like tx_fx_trail: a trail's u = 0 is its newest point
            if (t == null) return null;
            t.wrapMode = TextureWrapMode.Clamp;
            tyraTrail = RickardFx.WeighByAlpha(ArtMaterials.Instance(ArtMaterials.ParticleAdd));
            tyraTrail.name = "BAM Tyra bolt trail";
            tyraTrail.SetTexture(ArtMaterials.BaseMap, t);
            tyraTrail.mainTexture = t;
            return tyraTrail;
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

        // the hammer model on the tumbling look: long hammerLen tiles, spinning round its middle, its texture
        void PlaceHammer(Mesh mesh, string model)
        {
            float s = hammerLen / Mathf.Max(0.01f, mesh.bounds.size.y); // long in tiles
            hammerT.localScale = new Vector3(s, s, s);
            hammerT.localRotation = Quaternion.Euler(0f, 90f, 0f); // the model's head (along its x) along the flight
            hammerT.localPosition = -(hammerT.localRotation * (mesh.bounds.center * s)); // it spins round its middle
            hammerF.sharedMesh = mesh;
            Texture2D tex = Props.Texture(model);
            Texture use = tex != null ? (Texture)tex : Texture2D.whiteTexture;
            hammerMat.SetTexture(ArtMaterials.BaseMap, use); hammerMat.mainTexture = use;
        }

        public override void SetWeapon(string model)
        {
            if (hammerT == null || string.IsNullOrEmpty(model) || model == hammerModel) return;
            Mesh mesh = Props.Mesh(model);
            if (mesh == null) return; // a model this build lacks: the plain hammer flies
            hammerModel = model;
            PlaceHammer(mesh, model);
        }

        // Bram's Focus on the hammer he throws (Rickard 2026-11-18 "låter bra"): from a plain hammer with a faint forge streak at no
        // step to an orange-hot one with a long white-gold streak at the last (BramS.FocusMax), so the player sees it pay to stay
        public override void SetHeat(float h)
        {
            heat = Mathf.Clamp01(h);
            if (hammerMat != null) hammerMat.SetColor(ArtMaterials.BaseColor, Color.Lerp(Color.white, HotIron, heat));
            if (trail == null || kind != ProjKind.BramHammer) return;
            Color c = Color.Lerp(Fx.HeroMain(HeroId.Bram), WhiteHot, heat);
            float a = heat > 0f ? 0.35f + 0.6f * heat : 0f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) }, new[] { new GradientAlphaKey(a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.widthMultiplier = autoTrailW * (1f + heat);
            trail.time = 0.1f + 0.15f * heat;
        }
        static readonly Color HotIron = new Color(1f, 0.6f, 0.35f, 1f), WhiteHot = new Color(1f, 0.93f, 0.62f, 1f);
        float autoTrailW;

        static bool TrailColor(ProjKind k, int team, out Color c)
        {
            switch (k)
            {
                case ProjKind.TyraBolt: c = new Color(0.7f, 0.95f, 1f, 0.9f); return true;
                case ProjKind.MoaBolt: c = new Color(1f, 0.8f, 0.25f, 0.8f); return true;
                case ProjKind.BlastFire: c = new Color(1f, 0.55f, 0.2f, 0.45f); return true; // a faint fuse-smoke streak behind the bomb
                case ProjKind.TiktaNeedleSpecial: c = new Color(0.2f, 1f, 0.85f, 0.8f); return true;
                case ProjKind.BramHammerSpecial: c = Fx.HeroMain(HeroId.Bram); c.a = 0.8f; return true; // his forge orange, not the team's
                case ProjKind.BramBolt: c = Fx.HeroMain(HeroId.Bram); c.a = 0.85f; return true; // his thrown hammer's forge-orange streak (a beam until 2026-11-12)
                case ProjKind.BramHammer: c = Fx.HeroMain(HeroId.Bram); c.a = 0f; return true; // his auto hammer's Focus streak: SetHeat shows it
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
            hit = false; spent = false; prog = -1f; launchEase = false;
            Vector3 d = b - a; d.y = 0f;
            dir = d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward;
            float dist = d.magnitude;
            lob = kind == ProjKind.BlastBomb || kind == ProjKind.SiegeShell || kind == ProjKind.HildeKeg || kind == ProjKind.HildeBurst || kind == ProjKind.HildeShell || kind == ProjKind.KuggShell ? Mathf.Clamp(dist * 0.3f, 0.6f, 2.2f)
                : kind == ProjKind.BlastFire && bombT != null ? Mathf.Clamp(dist * 0.15f, 0.25f, 0.9f) : 0f; // a thrown bomb arcs a little
            lastPos = transform.position;
            vis.localScale = Vector3.one;
            Place(0f);
            if (trail != null) { trail.Clear(); trail.emitting = true; } // VIS-06: on Low too, the thin shots need their trail to be seen
            if (melee)
            {
                visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, arc ? arcColor : WithAlpha(visR.sharedMaterial.GetColor(ArtMaterials.BaseColor), 1f));
                mirror = (++swings[(int)kind & 31] & 1) == 0;
                struck = false;
                if (!arc && FxAtlas.Texture != null) visF.sharedMesh = FxAtlas.SlashQuad(mirror);
            }
        }

        // the heroes' blades get the big arc; the wolf a smaller one, minions small (recipe: they must not fill the lane)
        // Knut's a little bigger and heavier (Rickard 2026-11-03: his blow read as a shot)
        static float ArcScale(ProjKind k) => k == ProjKind.KnutHit ? 1.9f : k == ProjKind.FennSlash ? 1.6f : k == ProjKind.WolfBite ? 1.0f : 0.8f;

        // every shot can miss (a wall, a target that died, a volley arrow past everyone): its splash plays only when the game says
        // this very shot hit; one that hit earlier and flew on ends quietly (precision SHOT-01/03, VIS-04, TYRA-07)
        public override void MarkHit() => hit = true;
        public override void MarkSpent() => spent = true;
        public override void SetProgress(float k) => prog = Mathf.Clamp01(k);
        public override void SetLob(float height) => lob = Mathf.Max(0f, height);

        // Kugg's balls and sparks in his look's glow (BodyForm.KuggGlow; Rickard 2026-11-18 "kugg fixa"). Called on every launch, so a
        // pooled shot never keeps another Kugg's colour.
        int shotSkin; // the shooter's look, for the splash (Fx.Impact)
        public override void SetSkin(int skin)
        {
            shotSkin = skin;
            if (!kuggPic || visR == null || visR.sharedMaterial == null) return;
            Color kc = BodyView.KuggGlow(skin); kc.a = 1f;
            visR.sharedMaterial.SetColor(ArtMaterials.BaseColor, kc);
        }

        public override void LaunchFrom(Vector3 muzzle)
        {
            if (melee) return;
            if (WeaponDebug.On) WeaponDebug.Shot(muzzle, vis.position, dir);
            launchOff = muzzle - vis.position;
            launchEase = true;
            vis.position = muzzle;
        }

        public override void SetTether(Transform anchor)
        {
            tether = anchor;
            if (chainR != null) chainR.enabled = anchor != null;
            if (chainLine != null) chainLine.enabled = anchor != null;
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
                case ProjKind.BramBolt:
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
                case ProjKind.TyraBolt when boltPic:
                {
                    // Rickard 2026-10-29 "en mycket bättre pil, större och mer unik": his picture facing the camera, its head (the
                    // picture's right) pointing the way the bolt flies on screen, about 1.8 tiles across
                    Quaternion cam = ArtDriver.CamRot;
                    Vector3 r = cam * Vector3.right, u = cam * Vector3.up;
                    float ang = Mathf.Atan2(Vector3.Dot(dir, u), Vector3.Dot(dir, r)) * Mathf.Rad2Deg;
                    vis.rotation = cam * Quaternion.Euler(0f, 0f, ang) * Quaternion.Euler(-90f, 0f, 0f);
                    vis.localScale = new Vector3(1.8f, 1.8f, 1.8f);
                    break;
                }
                case ProjKind.TowerShot when towerPic:
                {
                    // the same as Tyra's bolt: facing the camera, its head (the picture's right) the way it flies on screen
                    Quaternion cam = ArtDriver.CamRot;
                    Vector3 r = cam * Vector3.right, u = cam * Vector3.up;
                    float ang = Mathf.Atan2(Vector3.Dot(dir, u), Vector3.Dot(dir, r)) * Mathf.Rad2Deg;
                    vis.rotation = cam * Quaternion.Euler(0f, 0f, ang) * Quaternion.Euler(-90f, 0f, 0f);
                    vis.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                    break;
                }
                case ProjKind.KuggShot when kuggPic:
                case ProjKind.KuggShell when kuggPic:
                case ProjKind.KuggSpark when kuggPic:
                {
                    // like the tower's bolt: facing the camera, the ball (the picture's right) leading the way it flies on screen, its
                    // tail behind; the anchored shell bigger, the spark small but seen
                    Quaternion cam = ArtDriver.CamRot;
                    Vector3 r = cam * Vector3.right, u = cam * Vector3.up;
                    float ang = Mathf.Atan2(Vector3.Dot(dir, u), Vector3.Dot(dir, r)) * Mathf.Rad2Deg;
                    vis.rotation = cam * Quaternion.Euler(0f, 0f, kind == ProjKind.KuggSpark ? 0f : ang) * Quaternion.Euler(-90f, 0f, 0f);
                    float ks = kind == ProjKind.KuggShell ? 1.5f : kind == ProjKind.KuggShot ? 1.1f : 0.6f;
                    vis.localScale = new Vector3(ks, ks, ks);
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
                case ProjKind.KnutAnchor when tether != null:
                {
                    // always flukes out from Knut, also while the chain hauls it back (by its flight it would turn round on the way in)
                    Vector3 away = vis.position - tether.position; away.y = 0f;
                    vis.rotation = away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away.normalized) : look;
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
            float t = prog >= 0f ? prog : Mathf.Clamp01(age / flight);
            Place(t);
            if (launchEase) { float w = 1f - Mathf.Clamp01(age / LaunchEase); vis.position += launchOff * w; launchEase = w > 0f; }
            if (melee)
            {
                // the swing arc sweeps and fades over a short moment, however long the hit takes
                float life = Mathf.Min(flight, 0.18f) + 0.12f;
                float k = Mathf.Clamp01(age / life);
                float s = (0.7f + k * 0.5f) * (arc ? ArcScale(kind) : 1f);
                vis.localScale = new Vector3(s, 1f, s);
                vis.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, mirror ? Mathf.Lerp(40f, -40f, k) : Mathf.Lerp(-40f, 40f, k), 0f);
                // Knut's anchor blow lands on the spot: his arc sits where the anchor strikes and never slides from him toward the
                // target, which read as a projectile ("knut skjuter bara en projektil", Rickard 2026-11-03); Fenn's slash sweeps as before
                if (kind == ProjKind.KnutHit) vis.position = to + new Vector3(0f, 0.45f, 0f);
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
            if (chainLine != null && tether != null)
            {
                chainLine.SetPosition(0, tether.position + new Vector3(0f, Height, 0f));
                chainLine.SetPosition(1, vis.position);
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
                    int n = Mathf.Clamp(Mathf.CeilToInt(len / ChainLink), 2, 64);
                    if (n != chainLinks && chainF != null) chainF.sharedMesh = Shapes.Chain(chainLinks = n);
                }
            }
        }

        public void OnRelease()
        {
            // the shot's own splash where it ended (bombs and the anchor have their own effects from the game layer); also when
            // the shot came and went inside one hitch frame and was never drawn, so every attack still shows its hit
            if (launched && !melee && kind != ProjKind.BlastBomb && kind != ProjKind.HildeKeg && kind != ProjKind.HildeBurst && kind != ProjKind.KnutAnchor)
            {
                if (hit) Fx.Impact(kind, vis.position, team, shotSkin);
                else if (!spent) Fx.Fizzle(kind, vis.position, team);
                // Tyra's bolt (Rickard 2026-10-28, tester Benny: "pilen borde ha mer ljus och effekter"): his spikes fly on the way
                // it went, and a short light pillar stands where it struck
                if (kind == ProjKind.TyraBolt && hit)
                {
                    Fx.Spikes(vis.position, dir, new Color(0.75f, 0.95f, 1f, 1f), 1.4f, 0.2f);
                    Fx.Pillar(new Vector3(vis.position.x, 0f, vis.position.z), 0.7f, 3f, new Color(0.7f, 0.95f, 1f, 0.8f), 0.3f);
                }
            }
            launched = false;
            tether = null;
            if (chainR != null) chainR.enabled = false;
            if (chainLine != null) chainLine.enabled = false;
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
        // Moa 2.0's honey field (Simon, XYZ #610): a faint honeycomb on the floor (Rickard's hex picture) and a sharper edge in
        // three layers, a bright thin line outside two softer ones
        Transform comb;
        Material combMat;
        readonly Transform[] edge = new Transform[3];
        readonly Material[] edgeMat = new Material[3];
        static readonly float[] EdgeInner = { 0.955f, 0.9f, 0.78f }, EdgeOuter = { 1f, 0.94f, 0.88f }, EdgeStrength = { 1f, 0.55f, 0.28f };
        static Mesh combMesh;
        static Texture2D rimTex;
        static bool rimLoaded;
        // Rickard's warning lane (O-051, 2026-11-04): edges and chevrons pointing the way the shot flies, repeated along a warning
        // line (Tyra's bolt, Knut's hook, Tikta's needles); white + alpha, tinted like the plain strip it replaces
        static Texture2D laneTex;
        static bool laneLoaded;
        // Rickard's danger circle (O-054, 2026-11-04): a bold ring with ticks and an almost empty inside, white + alpha, on every round
        // warning (Bonk's slam and the other big red discs, which hid who stood where). The fill that runs to the rim stays, faint.
        static Texture2D warnRingTex;
        static bool warnRingLoaded;
        const float WarnRingPeak = 0.89f; // the ring's bright band sits at 0.89 of the picture's half width
        Transform ringPic;
        Material ringPicMat;
        Transform lanePic;
        Material laneMat;
        Vector3 center, dir;
        float length, radius, duration, age, emitT;
        int brambleTicks; // Nässla's thicket: the poison ticks already shown (-1 = not seen yet, a late-shown one bursts no old ticks)
        Color baseColor;
        bool warn;
        // Blast's Inferno Lane as the old game drew it (View3DStudioBlast.cs "Ässans vrede"): from the first burn two burning strips
        // along the lane's edges and a glow down its middle, segment by segment so each piece flickers on its own, and a row of flame
        // tongues inside it that leap on every burn, which also throws embers (FireLook)
        const int LaneSegs = 10, LaneTongues = 10;
        MeshRenderer[] laneFloor, laneTongue, laneCore;
        Transform pushBody, pushHead; // Hilde's keg: the way it pushes (HILDE-02)
        Transform capA, capB; // the lane's round ends: it burns everyone within its half width of the line, behind Blast too (BLAST-02)
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
                : k == ZoneKind.Bramble ? new Color(0.55f, 0.85f, 0.2f, 0.45f) // Nässla's thicket: a lime-green floor (her own picture comes later)
                : k == ZoneKind.FireLane ? new Color(1f, 0.45f, 0.08f, 0.35f) // a faint heat floor: the cracks and flames carry the look
                : k == ZoneKind.FeatherTrail ? new Color(0.78f, 1f, 0.9f, 0.3f) // Tyra's wind: a pale mint floor, the feathers carry the look
                : warn ? (t == Teams.Player ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.12f, 0.1f, 1f))
                : (t == Teams.Player ? new Color(1f, 0.6f, 0.15f, 1f) : new Color(1f, 0.15f, 0.12f, 1f));
            if (warn) mat.renderQueue = 2995; // over honey and fire, under unit rings
            if (k == ZoneKind.WarnLine)
            {
                if (!laneLoaded) { laneLoaded = true; laneTex = RickardFx.Load("tx_warn_lane"); if (laneTex != null) laneTex.wrapMode = TextureWrapMode.Repeat; }
                if (laneTex != null)
                {
                    laneMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                    laneMat.renderQueue = 2995;
                    laneMat.SetTexture(ArtMaterials.BaseMap, laneTex);
                    laneMat.mainTexture = laneTex;
                    lanePic = Child("LanePic", LaneMesh(), laneMat, 0.003f);
                    shapeR.enabled = false; // the picture has its own edges
                }
            }
            if (k == ZoneKind.WarnCircle)
            {
                if (!warnRingLoaded) { warnRingLoaded = true; warnRingTex = RickardFx.Load("tx_warn_ring"); }
                if (warnRingTex != null)
                {
                    ringPicMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                    ringPicMat.renderQueue = 2995;
                    ringPicMat.SetTexture(ArtMaterials.BaseMap, warnRingTex);
                    ringPicMat.mainTexture = warnRingTex;
                    ringPic = Child("RingPic", FxAtlas.FullQuad(), ringPicMat, 0.003f);
                }
            }
            if (k == ZoneKind.HoneyField) MakeComb();
            if (k == ZoneKind.Bramble) MakeBrambleMarks();
            if (k == ZoneKind.FireLane || k == ZoneKind.FeatherTrail) { capA = Child("CapStart", Shapes.ZoneCap(), mat, 0f); capB = Child("CapEnd", Shapes.ZoneCap(), mat, 0f); }
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

        // Bomb and keg fuses (BLAST-05): their blast stops at walls, so the fuse is drawn only as far as it reaches each way
        Mesh reachShape, reachFill;
        bool cut;

        /// <summary>Cuts a circle zone to how far its blast reaches (world offsets from the centre, one per direction, all of them at
        /// the radius on open ground), after Show. Show makes it a full circle again.</summary>
        public override void CutTo(Vector3[] reach)
        {
            Uncut();
            if (reach == null || length > 0f) return;
            var pts = new Vector3[reach.Length];
            for (int i = 0; i < reach.Length; i++) { Vector3 l = transform.InverseTransformVector(reach[i]); l.y = 0f; pts[i] = l / radius; }
            reachShape = Shapes.ReachFan(pts, true);
            shapeF.sharedMesh = reachShape;
            if (inner != null)
            {
                reachFill = Shapes.ReachFan((Vector3[])pts.Clone(), false);
                inner.GetComponent<MeshFilter>().sharedMesh = reachFill;
            }
            cut = true;
            if (rim != null) rim.gameObject.SetActive(false); // the round telegraph picture can't be cut
        }

        void Uncut()
        {
            if (!cut) return;
            cut = false;
            shapeF.sharedMesh = Shapes.ZoneCircle();
            if (inner != null) inner.GetComponent<MeshFilter>().sharedMesh = Shapes.Disc(Pal.WhiteMid);
            Destroy(reachShape); Destroy(reachFill);
            reachShape = reachFill = null;
        }

        // a warning strip whose fill follows its shot (Tyra's bolt, Rickard 2026-10-29): how far the fill reaches, -1 = by the clock
        float front = -1f;
        public override void SetFront(float along) => front = along;

        public override void Show(Vector3 c, Vector3 d, float len, float r, float dur)
        {
            Uncut();
            front = -1f;
            center = c; length = Mathf.Max(0f, len); radius = Mathf.Max(0.05f, r); duration = Mathf.Max(0.05f, dur);
            d.y = 0f;
            dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            age = 0f; emitT = 0f; brambleTicks = -1;
            transform.position = new Vector3(c.x, warn ? 0.04f : 0.03f, c.z);
            transform.rotation = Quaternion.LookRotation(dir);
            if (pushBody != null) { pushBody.gameObject.SetActive(false); pushHead.gameObject.SetActive(false); }
            if (length > 0f)
            {
                shapeF.sharedMesh = Shapes.ZoneStrip();
                shape.localScale = new Vector3(radius, 1f, length);
                if (lanePic != null)
                {
                    // the picture is 4:1: as many whole repeats as fit the line at its width, so the chevrons keep their shape
                    lanePic.localScale = new Vector3(radius, 1f, length);
                    float reps = Mathf.Max(1f, Mathf.Round(length / (radius * 2f * 4f)));
                    laneMat.SetTextureScale(ArtMaterials.BaseMap, new Vector2(reps, 1f));
                    laneMat.mainTextureScale = new Vector2(reps, 1f);
                }
                if (capA != null)
                {
                    capA.localRotation = Quaternion.Euler(0f, 180f, 0f); capA.localScale = new Vector3(radius, 1f, radius);
                    capB.localPosition = new Vector3(0f, 0f, length); capB.localScale = new Vector3(radius, 1f, radius);
                }
            }
            else
            {
                shapeF.sharedMesh = Shapes.ZoneCircle();
                shape.localScale = new Vector3(radius, 1f, radius);
                if (rim != null) rim.localScale = new Vector3(radius * 2.255f, 1f, radius * 2.255f); // his ring peaks at 0.887 of the picture: edge exactly on the radius
                if (ringPic != null) ringPic.localScale = new Vector3(radius * 2f / WarnRingPeak, 1f, radius * 2f / WarnRingPeak);
                if (comb != null) comb.localScale = new Vector3(radius, 1f, radius);
                for (int i = 0; i < 3; i++) if (edge[i] != null) edge[i].localScale = new Vector3(radius, 1f, radius);
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
                    Fx.Pic("tx_fx_blast_scorch", center + dir * (length * (i + 0.5f) / sc), new Color(1f, 1f, 1f, 0.75f), radius * 2.5f, duration + 0.6f); // his mark reaches 0.8 of the picture: edge on the hit half width (BLAST-05)
            }
            else if (kind == ZoneKind.FeatherTrail && length > 0f)
            {
                // Tyra's feather trail (Rickard's card 2026-11-04): Rickard's feather picture strewn along the bolt's path, lying on
                // the ground and fading with the trail (none until tx_fx_tyra_feather is in, O-058); the wind streaks below run always
                int n = Mathf.Clamp(Mathf.RoundToInt(length / 0.7f), 1, 14);
                for (int i = 0; i < n; i++)
                {
                    Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (radius * (Random.value * 1.4f - 0.7f));
                    Fx.Pic("tx_fx_tyra_feather", center + dir * (length * (i + Random.value) / n) + side, new Color(0.85f, 1f, 0.92f, 0.9f), radius * 1.4f, duration);
                }
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
                    float fill = front >= 0f ? Mathf.Min(front, length) : length * t;
                    inner.localScale = new Vector3(radius * 2f * 0.84f, 1f, Mathf.Max(0.01f, fill));
                    inner.localPosition = new Vector3(0f, 0.004f, 0f);
                }
                else
                {
                    float s = radius * Mathf.Lerp(0.05f, 0.94f, t);
                    inner.localScale = new Vector3(s, 1f, s);
                    inner.localPosition = new Vector3(0f, 0.004f, 0f);
                }
                bool ringOn = ringPic != null && length <= 0f && !cut;
                Color ic = baseColor; ic.a = baseColor.a * fadeIn * (ringOn ? 0.3f : 0.8f); // with the ring picture only a faint fill
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
            if (laneMat != null) laneMat.SetColor(ArtMaterials.BaseColor, c);
            if (ringPic != null)
            {
                bool on = length <= 0f && !cut;
                if (ringPic.gameObject.activeSelf != on) ringPic.gameObject.SetActive(on);
                shapeR.enabled = !on; // the picture is the edge; a cut (walls) fuse keeps the code shape
                if (on) ringPicMat.SetColor(ArtMaterials.BaseColor, c);
            }
            if (rimMat != null)
            {
                bool on = length <= 0f && !cut && ringPic == null;
                rim.gameObject.SetActive(on);
                // additive: the colour's brightness is its strength
                if (on) rimMat.SetColor(ArtMaterials.BaseColor, new Color(baseColor.r * a, baseColor.g * a, baseColor.b * a, 1f));
            }
            if (comb != null)
            {
                // additive: the colour's brightness is its strength; the comb drifts slowly so the floor looks alive
                float cs = 0.32f * a;
                combMat.SetColor(ArtMaterials.BaseColor, new Color(baseColor.r * cs, baseColor.g * cs, baseColor.b * cs, 1f));
                comb.localRotation = Quaternion.Euler(0f, age * 6f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    float es = EdgeStrength[i] * a * (i == 0 ? 0.9f + 0.1f * Mathf.Sin(age * 7f) : 1f);
                    edgeMat[i].SetColor(ArtMaterials.BaseColor, new Color(1f * es, 0.82f * es, 0.35f * es, 1f));
                }
            }
            if (kind == ZoneKind.Bramble && edge[0] != null)
            {
                Color tc = Pal.Get(Pal.TeamLight(team));
                for (int i = 0; i < 3; i++)
                {
                    float es = EdgeStrength[i] * a * (i == 0 ? 0.9f + 0.1f * Mathf.Sin(age * 7f) : 1f);
                    edgeMat[i].SetColor(ArtMaterials.BaseColor, new Color(tc.r * es, tc.g * es, tc.b * es, 1f));
                }
                // seconds since it rooted, also when it is shown late with the time it has left (a hidden Nässla's, then seen)
                float since = NasslaS.Duration - duration + age, ft = since / Mathf.Max(0.05f, NasslaS.Warn);
                bool filling = ft < 1f;
                if (innerR.enabled != filling) innerR.enabled = filling;
                if (filling)
                {
                    float fs = radius * Mathf.Lerp(0.05f, 0.94f, Mathf.Clamp01(ft));
                    inner.localScale = new Vector3(fs, 1f, fs);
                    inner.localPosition = new Vector3(0f, 0.004f, 0f);
                    Color fc = tc; fc.a = 0.35f * a;
                    innerMat.SetColor(ArtMaterials.BaseColor, fc);
                }
                // each poison tick (NasslaS.Warn, then every Tick s) shows on the ground: a green ring runs out to the edge and leaves
                // and thorny bits fly up round it, so the thicket stings where you see it (Rickard 2026-11-18 "nässla fixa")
                int due = since + 1e-4f < NasslaS.Warn ? 0 : Mathf.Min(NasslaS.PoisonTicks, Mathf.FloorToInt((since - NasslaS.Warn + 1e-4f) / NasslaS.Tick) + 1);
                if (brambleTicks < 0) brambleTicks = due;
                else if (due > brambleTicks) { brambleTicks = due; BrambleSting(); }
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
            else if (kind == ZoneKind.FeatherTrail)
            {
                // the wind: pale streaks blowing along the bolt's way, so a player sees where the slow lies
                Fx.SparksAlong(p + Vector3.up * 0.15f, dir, 1, new Color(0.85f, 1f, 0.95f, 0.8f), 0.05f, 2.2f, 0.35f, 10f);
                if (Random.value < 0.3f) Fx.Glow(p, 1, new Color(0.7f, 1f, 0.85f, 0.6f), 0.2f, 0.1f, 0.4f, 0.6f);
            }
            else if (kind == ZoneKind.HoneyField)
            {
                Fx.Glow(p, 1, Pal.Get(Pal.Honey), 0.18f, 0.1f, 0.9f, 0.5f);
                if (Random.value < 0.35f) Fx.Bubbles(p, 1, Pal.Get(Pal.Honey), 0.12f, 0.05f, 0.7f, 0.25f);
                if (Random.value < 0.15f) Fx.Drops(p + Vector3.up * 0.6f, 1, Pal.Get(Pal.Honey), 0.12f, 0.1f, 0.6f, -0.4f); // slow honey drips
            }
            else if (kind == ZoneKind.Bramble)
            {
                // Nässla's thicket (design review 2026-11-16, Rickard 2026-11-18 "nässla fixa"): it fell to the line below and threw
                // orange fire sparks from its middle, as if it burned. Now poison: lime spores drifting up, swamp bubbles and a leaf now and then
                Fx.Glow(p, 1, BrambleSpore, 0.16f, 0.05f, 0.9f, 0.5f, 0.05f);
                if (Random.value < 0.35f) Fx.Bubbles(p, 1, BramblePoison, 0.13f, 0.05f, 0.7f, 0.25f);
                if (Random.value < 0.12f) Fx.Leaves(p + Vector3.up * 0.1f, 1, BrambleLeaf, 0.13f, 0.3f, 0.9f, 0.4f, 0.05f);
            }
            else Fx.Sparks(center + Vector3.up * 0.1f, 1, Pal.Get(Pal.FireCore), 0.08f, 1.5f, 0.2f, 1f);
        }

        // Nässla's poison: lime spores (her colour, Fx heroMain), a darker swamp green for the bubbles, and leaf green
        static readonly Color32 BrambleSpore = new Color32(170, 240, 80, 170), BramblePoison = new Color32(90, 150, 40, 200), BrambleLeaf = new Color32(70, 120, 35, 230);

        /// <summary>A poison tick of Nässla's thicket: a ring runs out to its edge, and leaves and thorny bits jump up round the rim.</summary>
        void BrambleSting()
        {
            Fx.Ring(center + Vector3.up * 0.02f, radius * 0.25f, radius, new Color(0.6f, 0.95f, 0.25f, 0.75f), 0.35f, 0.5f);
            int n = ArtKit.High ? 8 : 4;
            for (int i = 0; i < n; i++)
            {
                float ang = (i + Random.value * 0.6f) * Mathf.PI * 2f / n;
                Vector3 rimP = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (radius * 0.85f) + Vector3.up * 0.08f;
                Fx.Leaves(rimP, 1, BrambleLeaf, 0.15f, 0.9f, 0.6f, 1.4f, 0.1f);
                if ((i & 1) == 0) Fx.Shards(rimP, 1, Fx.HeroAccent(HeroId.Nassla), 0.07f, 1.2f, 0.45f, 1.6f, 0.1f); // thorny root bits
            }
            Fx.Bubbles(center + Vector3.up * 0.05f, ArtKit.High ? 4 : 2, BramblePoison, 0.16f, 0.4f, 0.6f, 0.6f, radius * 0.4f);
        }

        // a strip x -1..1, z 0..1 whose u runs along it (the lane picture's chevrons point along +u), v across
        static Mesh laneMesh;
        static Mesh LaneMesh()
        {
            if (laneMesh != null) return laneMesh;
            laneMesh = new Mesh { name = "Warn lane" };
            laneMesh.vertices = new[] { new Vector3(-1f, 0f, 0f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f) };
            laneMesh.uv = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f) };
            laneMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            laneMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            laneMesh.RecalculateBounds();
            return laneMesh;
        }

        // Nässla's thicket (hero review 2026-11-13): the same lime floor for both teams did not say whose it was, poison or heal, and
        // nothing showed when it first stings. Now the edge is the team's colour (the enemy's red, ours blue, as the shots) and a fill
        // grows to the edge until the first tick (NasslaS.Warn), as a bomb's fuse; her own picture comes later (orders)
        void MakeBrambleMarks()
        {
            for (int i = 0; i < 3; i++)
            {
                edgeMat[i] = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                edgeMat[i].renderQueue = 2992 + i;
                edge[i] = Child("Edge" + i, EdgeMesh(i), edgeMat[i], 0.007f + i * 0.001f);
            }
            var ig = new GameObject("Fuse");
            inner = ig.transform;
            inner.SetParent(transform, false);
            ig.AddComponent<MeshFilter>().sharedMesh = Shapes.Disc(Pal.WhiteMid);
            innerR = ig.AddComponent<MeshRenderer>();
            innerMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            innerMat.renderQueue = 2991;
            innerR.sharedMaterial = innerMat;
            innerR.shadowCastingMode = ShadowCastingMode.Off;
            innerR.receiveShadows = false;
        }

        void MakeComb()
        {
            var tex = Resources.Load<Texture2D>("BAMArt/Fx/T_BAM_HexShield");
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                combMat = FxAtlas.Material(ArtMaterials.UnlitAdd); // additive, weighed by the picture's alpha
                combMat.name = "BAM honey comb";
                combMat.SetTexture(ArtMaterials.BaseMap, tex);
                combMat.mainTexture = tex;
                combMat.renderQueue = 2991;
                comb = Child("Comb", CombMesh(), combMat, 0.005f);
            }
            for (int i = 0; i < 3; i++)
            {
                edgeMat[i] = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                edgeMat[i].renderQueue = 2992 + i;
                edge[i] = Child("Edge" + i, EdgeMesh(i), edgeMat[i], 0.007f + i * 0.001f);
            }
        }

        public override void SetPush(float len)
        {
            if (len <= 0f) return;
            if (pushBody == null)
            {
                var pm = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                pm.renderQueue = 2992; // over the keg's growing fuse fill
                pm.SetColor(ArtMaterials.BaseColor, new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f));
                pushBody = Child("PushBody", Shapes.AimBody(), pm, 0.008f); pushHead = Child("PushHead", Shapes.AimHead(), pm, 0.008f);
            }
            // as the drag aim's push arrow (Hud): 0.12 half wide, the head 0.3 wide and 0.3 long, its point len from the centre
            float head = Mathf.Min(0.3f, len * 0.45f);
            pushBody.localScale = new Vector3(0.24f, 1f, len - head);
            pushHead.localPosition = new Vector3(0f, 0.008f, len - head);
            pushHead.localScale = new Vector3(0.6f, 1f, head);
            pushBody.gameObject.SetActive(true); pushHead.gameObject.SetActive(true);
        }

        Transform Child(string name, Mesh mesh, Material m, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        static readonly Mesh[] edgeMeshes = new Mesh[3];

        // a thin flat ring from EdgeInner to EdgeOuter of radius 1, both windings, palette white (the material tints it)
        static Mesh EdgeMesh(int i)
        {
            if (edgeMeshes[i] != null) return edgeMeshes[i];
            const int Seg = 64;
            var v = new Vector3[(Seg + 1) * 2];
            var uv = new Vector2[v.Length];
            var tri = new int[Seg * 12];
            Vector2 w = Pal.UV(Pal.White);
            for (int k = 0; k <= Seg; k++)
            {
                float a = k * Mathf.PI * 2f / Seg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[k * 2] = new Vector3(c * EdgeInner[i], 0f, s * EdgeInner[i]);
                v[k * 2 + 1] = new Vector3(c * EdgeOuter[i], 0f, s * EdgeOuter[i]);
                uv[k * 2] = w; uv[k * 2 + 1] = w;
            }
            for (int k = 0; k < Seg; k++)
            {
                int a0 = k * 2, q = k * 6, r = Seg * 6 + q;
                tri[q] = a0; tri[q + 1] = a0 + 1; tri[q + 2] = a0 + 3; tri[q + 3] = a0; tri[q + 4] = a0 + 3; tri[q + 5] = a0 + 2;
                tri[r] = a0; tri[r + 1] = a0 + 3; tri[r + 2] = a0 + 1; tri[r + 3] = a0; tri[r + 4] = a0 + 2; tri[r + 5] = a0 + 3;
            }
            var m = new Mesh { name = "Honey edge " + i, vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return edgeMeshes[i] = m;
        }

        // a disc of radius 0.95 with the hex picture laid flat across it: about 6 cells over the 1.8-tile field's radius
        static Mesh CombMesh()
        {
            if (combMesh != null) return combMesh;
            const int Seg = 48;
            const float R = 0.95f, Tile = 0.85f;
            var v = new Vector3[Seg + 1];
            var uv = new Vector2[v.Length];
            var tri = new int[Seg * 6];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int k = 0; k < Seg; k++)
            {
                float a = k * Mathf.PI * 2f / Seg, c = Mathf.Cos(a) * R, s = Mathf.Sin(a) * R;
                v[k + 1] = new Vector3(c, 0f, s);
                uv[k + 1] = new Vector2(0.5f + c * Tile, 0.5f + s * Tile);
            }
            for (int k = 0; k < Seg; k++)
            {
                int a = k + 1, b = (k + 1) % Seg + 1, q = k * 6;
                tri[q] = 0; tri[q + 1] = a; tri[q + 2] = b; tri[q + 3] = 0; tri[q + 4] = b; tri[q + 5] = a;
            }
            combMesh = new Mesh { name = "Honey comb", vertices = v, uv = uv, triangles = tri };
            combMesh.RecalculateNormals();
            combMesh.RecalculateBounds();
            combMesh.UploadMeshData(true);
            return combMesh;
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
