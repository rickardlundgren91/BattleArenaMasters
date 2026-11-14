using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// A unit built from a <see cref="RigDef"/>: either one rigidly skinned mesh animated by code, or an imported hero model
    /// (<see cref="HeroArt"/>) driven by its Mixamo clips through a small playable mixer. Both get idle, walk, an attack timed
    /// by the windup, cast, hit flash and death, a ground decal with the team ring, a health bar and status markers.
    /// Everything is allocated in <see cref="Build"/>; the per-frame path only writes numbers.
    /// </summary>
    sealed partial class BodyView : UnitView
    {
        internal Stack<BodyView> Pool;
        internal bool InPool;
        internal int Team;
        /// <summary>Menu showcase: no ground ring, bar or status markers.</summary>
        internal bool Preview;
        /// <summary>A hero in a match (not a minion, tower, wolf or menu preview); set by ArtKit.Body.</summary>
        internal bool IsHero;
        /// <summary>HeroId of a hero view (also menu previews), else -1; set by ArtKit.Body.</summary>
        internal int HeroIndex = -1;
        /// <summary>Heroes in the match now, for the bushes (BushField). Kept by OnEnable/OnDisable, so pooled views drop out.</summary>
        internal static readonly List<BodyView> Heroes = new List<BodyView>(8);
        internal bool IsDead => dead;
        /// <summary>Animation Foundation 1.0: what this view's swings did (AnimDebugOverlay).</summary>
        internal readonly AnimProbe Probe = new AnimProbe();
        /// <summary>For the overlay: the swing's and the cast's time (below 0 = none), the windup, and the blend weights.</summary>
        internal float DbgAtkT => atkT;
        internal float DbgCastT => castT;
        internal float DbgWindup => windup;
        internal float DbgMoveW => moveW;
        internal float DbgAtkW => lastAtkW;
        internal float DbgCastW => lastCastW;
        internal bool DbgCut => atkCut >= 0f || castCut >= 0f;
        // what the player can actually see of it: an enemy hidden in a bush is still listed but must give nothing away
        internal bool Shown => renderersOn && vis > 0.01f && !dead;
        internal bool IsPlayer => marker == UnitMarker.Player;

        /// <summary>Every view with held weapons now, menu stages included (the weapon debug view).</summary>
        internal static readonly List<BodyView> Armed = new List<BodyView>(12);

        void OnEnable()
        {
            if (IsHero && !Heroes.Contains(this)) Heroes.Add(this);
            if (gear != null && !Armed.Contains(this)) Armed.Add(this);
        }
        void OnDisable() { Heroes.Remove(this); Armed.Remove(this); }

        RigDef def;
        Transform model;
        Transform[] bones;
        Renderer[] rends;
        Material[][] opaqueSets, fadeSets;
        Material[] mats;          // every material instance of this view, for the hit flash
        Material[] fadeMats;      // the fade instances, for the alpha
        Material opaque;          // code models: the single instance (the ghost wolf tints it)
        Material[] ghostMats = new Material[0]; // the ghost wolf's see-through materials (code glow, Meshy body)
        Color ghostTint = Color.white;

        // imported models
        Animator anim;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        float[] clipLen;
        float atkSpeed = 1f, atkTotal = 0.5f, castLen = 0.6f, hitA = -1f, deathFadeAt = 0.55f, deathFade = 0.45f;
        // old Speltest's HeroMixamo layers: standing, the attack and the cast move the whole body; on the move only the upper body
        // (this mask) swings while the legs keep running, so a hero hitting on the run no longer slides on frozen legs
        AnimationLayerMixerPlayable layers;
        AnimationPlayableOutput animOut;
        bool layered; // the layers are in the graph only while a swing runs on the move (Rickard 2026-10-27: Bram floated on Home)
        AnimationMixerPlayable upper;
        AnimationClipPlayable upAtk, upCast;
        // the attack in Speltest's four phases (HeroAttack.cs, HeroMixamo.PoseMx): the coil, the slash, the hold on the blow, the recovery
        float atkRel, atkSlash, atkHold, atkRec, atkStart;
        bool imported;
        HeldGear gear;            // weapons and shield in the hands (Weapons.cs), imported heroes only
        Vector3[][] gearVerts;    // their meshes' vertices, read once by GearTop (the menu stages)
        PreviewBreath breath;     // the menu hero's living idle on top of its clip (Preview only)
        ClothSway cloth;          // cloth and hair on spring bones (Fenn, Tyra, Bonk, Ragn, Nässla), made at the first frame
        bool clothTried;
        // the menu hero's feet kept on the stage (Rickard 2026-10-27: Bram and Fenn floated above the ring on Home): each idle stands
        // the model at its own height over the root, so the lowest foot bone is held where it was in the bind pose, standing on the ground
        Transform instT, footL, footR, toeL, toeR;
        float footRef = float.NaN, groundY;
        bool groundSet;
        float gearK = 1f; // the renderer bounds' height over the sole-to-crown one the model is fitted by (BuildImported)
#if UNITY_EDITOR
        float soleGap, instY0, groundLogT; // what the grounding found, logged once a second into a menu stage
        bool groundLogged;
#endif
        bool breathTried;
        float gearAct, gearAtk;   // how much the attack and cast clips play (and the attack alone), for the gear's rest pose
        int shadowRends;          // rends[0..shadowRends) may cast shadows; the gear after them never does
        // the rig and animation pass of 2026-10-28 (AnimFix): the flinch on the upper body on the move; where a new swing or cast
        // eases in from when the last one is still fading out; the played part of the attack and cast clips round their measured
        // blow; last frame's clip weights for holding a drifting clip in place; the legs turned to the travel; the lean
        AnimationClipPlayable upHit;
        float atkCarry, castCarry, lastAtkW, lastCastW;
        bool atkSeg, castSeg, clipsInfo;
        float atkFrom, atkBlow, atkTo, castFrom, castBlowAt, castTo;
        float atkHitAt = 0.3f; // swing seconds when the clip shows atkBlow: the windup, or for a melee hero windup + its blow's travel
        // the special's form (dragged or tapped), its clip's peak and the pace of the clip up to it (CastSegment)
        bool castDrag;
        float castPeak, castPre = CastRate;
        // (2026-10-28, after Rickard's test) the recovery's pace and fade-out, the special's fade-out, the time between the last two
        // swings, and how far Tyra turns so her arrow points at the target; her archer's stance between shots (a second copy of her
        // shot clip, see Stance) and how much it shows
        float recRate = RecoverRate, atkFade = 0.25f, castFade = 0.25f, atkGap = 1.4f, lastAtkAt = -10f, aimYaw;
        AnimationClipPlayable stance;
        bool bowHero;
        float stanceW;
        float drAtk, drCast, drHit, drAtkFrom, drCastFrom, drUpA, drUpC;
        // (2026-10-29, ANIM-11..13) where the idle stands the hips (hip heights), the spot a drifting clip is held over; the flinch's
        // size (big or small hit), its length and its clip's blow and settle
        Vector2 restHips;
        bool restSet, hitBig;
        float hitWin = 0.3f, hitBlow = -1f, hitTo = -1f;
        bool squared;
        Transform hipsT, spineT, chestT, thighL, thighR;
        Vector3 travelV;
        float legW, legYaw, leanP, leanR, lastYaw, lastSpd;
        int legDir = 1;
        const float RecoverRate = 1.2f, CastRate = 1.15f;
        const int CIdle = 0, CRun = 1, CAttack = 2, CCast = 3, CHit = 4, CDeath = 5, CWalk = 6, CStance = 7;
        // the walk and the run with the feet on the ground (old Speltest's HeroMixamoGait): each clip's own speed over the ground in
        // hip heights a second, measured once from its planted foot; the hips' height above the model's root; one stride phase for both
        float walkSpd = 2.2f, runSpd = 5f, hipsH, gaitPh, gSpd;
        Vector3 lastPos;
        bool hasLast, gaitMeasured;
        Transform decalT;
        MeshFilter decalF;
        MeshRenderer decalR;
        HealthBar bar;
        StatusFx status;

        bool moving;
        float speed01, moveW, phase, idleT, seed;
        float atkT = -1f, windup = 0.3f, castT = -1f, hitT = -1f, hitLen = 0.15f, recoil;
        long atkTick = -1; // the rules' tick the swing started on (ArtKit.SimTicks), -1 = timed by the frame clock
        float atkCut = -1f, castCut = -1f; // Interrupt: where the swing or the cast was stopped (it holds there and fades out)
        const float CutFade = 0.15f;
        float propBack = -1f;
        float hp = -1f, towerFxT, dustT;
        float channel = -1f, channelT, swirlT, sprintT;
        int sprintN; // the battle spells' looks (SpellFx): recall channel progress, sprint trail
        bool sprint;   // last health fraction (-1 = never set), the tower's smoke timer
        bool dead;
        float deathT = -1f, spawnT = -1f, vis = 1f, ghostPulse;
        int lastQuality = -1;
        bool fading, renderersOn = true;
        // a minion (ArtKit.Body calls MinionLook): its side's tint on the whole figure, and its bar only once it is hurt
        bool minion;
        Color tint = Color.white;
        Color flashColor = Color.white;
        float ownT = -1f, ownLen = 0.15f, ownPeak; // the player's own hit: a white flash over the red blink (PlayOwnHit)
        bool flashLit;                             // the emission is not black (a flash is showing or just ended)
        // Fenn the Spectral Knight (Rickard 2026-11-11 "fenn spectral knight, assassin", "gör effekter på hans modell"): his own
        // Clay-Fenn model in a soft pale-blue ghost light, ghost wisps rising off him, and a ghost tint when he is half seen (Veil)
        bool Spectral => HeroIndex == (int)HeroId.Fenn && !dead;
        static readonly Color SpectralGlow = new Color(0.09f, 0.2f, 0.3f), SpectralFade = new Color(0.7f, 0.9f, 1f);
        static readonly Color32 SpectralWisp = new Color32(170, 230, 255, 170);
        float wispT;
        UnitMarker marker = UnitMarker.None;

        public void Build(RigDef d, int team)
        {
            def = d;
            Team = team;
            seed = Random.value * 10f;
            var mg = new GameObject("Model");
            model = mg.transform;
            model.SetParent(transform, false);
            if (d.Art != null && d.Art.Model != null) BuildImported(d.Art);
            else BuildCode(d, mg);
            shadowRends = rends.Length - (gear != null ? gear.Rends.Length : 0);
            BuildCommon(d);
        }

        void BuildCode(RigDef d, GameObject mg)
        {
            bones = new Transform[d.Pivots.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var bg = new GameObject("B" + i);
                bones[i] = bg.transform;
                int p = d.Parents[i];
                bones[i].SetParent(p < 0 ? model : bones[p], false);
                bones[i].localPosition = d.Pivots[i] - (p < 0 ? Vector3.zero : d.Pivots[p]);
            }
            var smr = mg.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = d.Mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.quality = SkinQuality.Bone1;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, d.Height * 0.5f - d.Pivots[0].y, 0f), new Vector3(d.Radius * 4f + 1f, d.Height + 1f, d.Radius * 4f + 1f));
            Material fade;
            if (d.Ghost)
            {
                opaque = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                fade = opaque;
                smr.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                opaque = ArtMaterials.Instance(ArtMaterials.Lit);
                fade = ArtMaterials.Instance(ArtMaterials.LitFade);
            }
            smr.sharedMaterial = opaque;
            smr.receiveShadows = !d.Ghost;
            rends = new Renderer[] { smr };
            opaqueSets = new[] { new[] { opaque } };
            fadeSets = new[] { new[] { fade } };
            mats = d.Ghost ? new Material[0] : new[] { opaque, fade };
            fadeMats = new[] { fade };
            ghostMats = d.Ghost ? new[] { opaque } : new Material[0];
            if (d.PropMesh != null) AddProp(d);
        }

        // Rickard's Meshy model on one bone, with its own texture on our materials (flash, fade and ghost work as on the rest)
        void AddProp(RigDef d)
        {
            var pg = new GameObject("Prop");
            Transform pt = pg.transform;
            pt.SetParent(bones[d.PropBone], false);
            pt.localPosition = d.PropOffset - d.Pivots[d.PropBone];
            pt.localRotation = Quaternion.Euler(d.PropEuler);
            pt.localScale = new Vector3(d.PropScale, d.PropScale, d.PropScale);
            pg.AddComponent<MeshFilter>().sharedMesh = d.PropMesh;
            var r = pg.AddComponent<MeshRenderer>();
            Texture tex = d.PropTex != null ? (Texture)d.PropTex : Texture2D.whiteTexture;
            Material o, f;
            if (d.Ghost)
            {
                o = ArtMaterials.Instance(ArtMaterials.UnlitFade);
                o.SetTexture(ArtMaterials.BaseMap, tex); o.mainTexture = tex;
                f = o;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                ghostMats = new[] { opaque, o };
                ghostTint = new Color(0.62f, 1f, 0.96f, 1f);
            }
            else
            {
                o = ArtMaterials.Instance(ArtMaterials.Lit);
                f = ArtMaterials.Instance(ArtMaterials.LitFade);
                o.SetTexture(ArtMaterials.BaseMap, tex); o.mainTexture = tex;
                f.SetTexture(ArtMaterials.BaseMap, tex); f.mainTexture = tex;
                r.receiveShadows = true;
                mats = new[] { mats[0], mats[1], o, f };
                fadeMats = new[] { fadeMats[0], f };
            }
            r.sharedMaterial = o;
            rends = new[] { rends[0], r };
            opaqueSets = new[] { opaqueSets[0], new[] { o } };
            fadeSets = new[] { fadeSets[0], new[] { f } };
        }

        // PERF-03: a skinned body no longer works out its bounds from its bones every frame (updateWhenOffscreen), on or off screen.
        // It gets one fixed box instead, round its root bone (the box moves and turns with it), reaching 1.5 times as far as the
        // farthest point of the mesh in the pose it was made in, in every direction. Mixamo and Meshy rigs only turn their bones
        // (no stretch), and a T-pose already has arms and legs at full length, so no swing or cast can take a vertex outside the
        // box: the body is never culled while any part of it is on screen (every attack visible). Held weapons are their own
        // renderers with their own bounds. Animators stay AlwaysAnimate, so a body coming on screen is already in the right pose.
        static void FixedBounds(SkinnedMeshRenderer sk)
        {
            Mesh m = sk.sharedMesh;
            Transform space = sk.rootBone != null ? sk.rootBone : sk.transform;
            if (m == null) { sk.updateWhenOffscreen = true; return; }
            Bounds mb = m.bounds;
            float far = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f));
                far = Mathf.Max(far, space.InverseTransformPoint(sk.transform.TransformPoint(c)).magnitude);
            }
            if (!(far > 1e-4f) || float.IsInfinity(far)) { sk.updateWhenOffscreen = true; return; } // a mesh that makes no sense: as before
            sk.updateWhenOffscreen = false;
            sk.localBounds = new Bounds(Vector3.zero, Vector3.one * (2f * 1.5f * far));
        }

        // Rickard's model: scaled to the hero's height, feet on the ground, his texture on our lit material (so the hit flash
        // and the grass fade work the same as on everything else), clips mixed by weight every frame.
        void BuildImported(HeroArt art)
        {
            imported = true;
            bones = new Transform[0];
            GameObject inst = Instantiate(art.Model, model, false);
            inst.name = art.Model.name;
            Transform it = inst.transform;
            it.localPosition = Vector3.zero;
            it.localRotation = Quaternion.identity;
            rends = inst.GetComponentsInChildren<Renderer>(true); // true: the view may be built under the hidden pool root, where (false) finds nothing
            // the renderers' bounds, as before 2026-10-26 (a baked-vertex measure blew Knut up to many times his size in a phone test);
            // the menu stages frame with HeightOf, not these bounds
            Bounds bb = new Bounds();
            for (int i = 0; i < rends.Length; i++) { if (i == 0) bb = rends[i].bounds; else bb.Encapsulate(rends[i].bounds); }
            float hb = bb.size.y > 0.01f ? bb.size.y : 1f;
            // the soles go on the ground by the meshes' own bottom in the pose they were made in (BindBottom), not by these bounds:
            // a fresh skinned renderer's bounds may reach below the feet, and the model then stood that much over the ground
            // (Benny and Rickard 2026-10-28: the heroes floated about a tenth of their height over the ring on Home)
            float sole = BindBottom(rends, bb.min.y);
            if (Mathf.Abs(sole - bb.min.y) > 0.25f * hb) sole = bb.min.y; // a mesh whose stored bounds make no sense: as before
            // and the height is sole to crown in that pose too, as Speltest fitted its models (HeroMixamo: the meshes' own bounds).
            // Fitted to these bounds, every hero was drawn 7-14 % shorter than its Height (COORDS-03, Unity run 2026-10-29)
            float h = BindTop(rends, bb.max.y) - sole;
            if (h < 0.5f * hb || h > 1.02f * hb) h = hb;
            // the remakes (V2) are measured where their skin really draws them in the bind pose (each bone times its bind matrix),
            // not by the renderers' stored bounds or the mesh's own transform: on the Mixamo-rigged remakes those disagreed with the
            // drawn body, which came out a few centimetres tall or headless (Rickard 2026-11-14: Blast, Knut, Bram, then Kugg)
            if (art.V2 && SkinBindBox(rends, out Bounds sb) && sb.size.y > 1e-5f)
            {
                bb = sb; hb = sb.size.y; sole = sb.min.y; h = hb;
            }
            float k = art.Height / h;
            gearK = hb / h; // the weapons, the shoulder carry and the tuner's offsets grow with the body, as Rickard tuned them on it
#if UNITY_EDITOR
            soleGap = (sole - bb.min.y) * k;
#endif
            it.localScale = it.localScale * k;
            it.localPosition = new Vector3(0f, (transform.position.y - sole) * k, 0f);

            opaqueSets = new Material[rends.Length][];
            fadeSets = new Material[rends.Length][];
            var all = new List<Material>();
            var fades = new List<Material>();
            for (int i = 0; i < rends.Length; i++)
            {
                Material[] src = rends[i].sharedMaterials;
                opaqueSets[i] = new Material[src.Length];
                fadeSets[i] = new Material[src.Length];
                for (int j = 0; j < src.Length; j++)
                {
                    Texture tex = art.Texture != null ? art.Texture : src[j] != null ? src[j].mainTexture : null;
                    if (tex == null) tex = Texture2D.whiteTexture;
                    Material o = ArtMaterials.Instance(ArtMaterials.Lit), f = ArtMaterials.Instance(ArtMaterials.LitFade);
                    o.SetTexture(ArtMaterials.BaseMap, tex); o.mainTexture = tex;
                    f.SetTexture(ArtMaterials.BaseMap, tex); f.mainTexture = tex;
                    opaqueSets[i][j] = o; fadeSets[i][j] = f;
                    all.Add(o); all.Add(f); fades.Add(f);
                }
                rends[i].sharedMaterials = opaqueSets[i];
                rends[i].receiveShadows = true;
                if (rends[i] is SkinnedMeshRenderer sk) FixedBounds(sk); // attacks must always show, even at the screen edge
            }
            mats = all.ToArray();
            fadeMats = fades.ToArray();

            anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            instT = it;
#if UNITY_EDITOR
            instY0 = it.localPosition.y;
#endif
            if (anim.isHuman)
            {
                footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot); footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
                hipsT = anim.GetBoneTransform(HumanBodyBones.Hips); spineT = anim.GetBoneTransform(HumanBodyBones.Spine);
                chestT = anim.GetBoneTransform(HumanBodyBones.Chest);
                thighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); thighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                float f = LowestFoot();
                if (!float.IsNaN(f)) footRef = f - transform.position.y; // the bind pose, its mesh's sole on the ground
            }
            if (HeroIndex == (int)HeroId.Blast && art.V2) MeasureHeadHold(); else headHold = false;
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (def.Gear != null) AddGear(art);
            AnimationClip idle = art.Idle;
            if (idle == null) { Debug.LogWarning("[BAM.Art] " + art.name + " has no idle clip; the model stands still"); return; }
            var src2 = new[] { art.Idle, art.Run, art.Attack, art.Cast, art.Hit, art.Death, art.Walk != null ? art.Walk : art.Run };
            Probe.Missing = 0;
            for (int c = 0; c < src2.Length; c++) if (src2[c] == null) Probe.Missing |= 1 << c; // they play the idle (below)
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            hipsH = hips != null ? Mathf.Max(0.05f, anim.transform.InverseTransformPoint(hips.position).y) : 0f;
            MeasureGait(src2[CWalk], src2[CRun]);
            graph = PlayableGraph.Create("BAM " + art.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            bowHero = def.Gear != null && (def.Gear == Weapons.For(HeroId.Tyra) || def.Gear == Weapons.For(HeroId.Tyra, true));
            cFour = art.RunFour != null ? src2.Length + (bowHero ? 1 : 0) : -1;
            mixer = AnimationMixerPlayable.Create(graph, src2.Length + (bowHero ? 1 : 0) + (cFour >= 0 ? 1 : 0));
            animOut = output;
            output.SetSourcePlayable(mixer);
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            clips = new AnimationClipPlayable[src2.Length];
            clipLen = new float[src2.Length];
            for (int c = 0; c < src2.Length; c++)
            {
                AnimationClip clip = src2[c] != null ? src2[c] : idle;
                clips[c] = AnimationClipPlayable.Create(graph, clip);
                // the feet stay planted in idle, walk and run as in Speltest (its clips play with foot IK); the blows as they were
                clips[c].SetApplyFootIK(c == CIdle || c == CRun || c == CWalk);
                clipLen[c] = Mathf.Max(0.1f, clip.length);
                graph.Connect(clips[c], 0, mixer, c);
                mixer.SetInputWeight(c, c == CIdle ? 1f : 0f);
            }
            // the upper-body copies of the attack, the cast and the flinch, for swinging on the move
            upper = AnimationMixerPlayable.Create(graph, 3);
            upAtk = AnimationClipPlayable.Create(graph, src2[CAttack] != null ? src2[CAttack] : idle);
            upCast = AnimationClipPlayable.Create(graph, src2[CCast] != null ? src2[CCast] : idle);
            upHit = AnimationClipPlayable.Create(graph, src2[CHit] != null ? src2[CHit] : idle);
            graph.Connect(upAtk, 0, upper, 0);
            graph.Connect(upCast, 0, upper, 1);
            graph.Connect(upHit, 0, upper, 2);
            graph.Connect(upper, 0, layers, 1);
            layers.SetLayerMaskFromAvatarMask(1, UpperMask());
            layers.SetInputWeight(1, 0f);
            if (bowHero)
            {
                // Tyra's stance between shots: her shot clip once more, the whole body, posed by hand as the attack (Stance)
                stance = AnimationClipPlayable.Create(graph, src2[CAttack] != null ? src2[CAttack] : idle);
                stance.SetApplyFootIK(false);
                stance.Pause();
                graph.Connect(stance, 0, mixer, CStance);
                mixer.SetInputWeight(CStance, 0f);
            }
            if (cFour >= 0)
            {
                four = AnimationClipPlayable.Create(graph, art.RunFour);
                four.SetApplyFootIK(true);
                four.Pause();
                fourLen = Mathf.Max(0.1f, art.RunFour.length);
                graph.Connect(four, 0, mixer, cFour);
                mixer.SetInputWeight(cFour, 0f);
            }
            // walk and run are posed by hand each frame (AnimateImported): their time is the stride phase, not the clock; so are the
            // attack (its four phases) and the cast (so the upper-body copies stay on the same frame)
            clips[CWalk].Pause(); clips[CRun].Pause();
            clips[CAttack].Pause(); clips[CCast].Pause(); upAtk.Pause(); upCast.Pause();
            // one-shot clips must hold their last frame, not loop back
            clips[CDeath].SetDuration(clipLen[CDeath]);
            castLen = Mathf.Min(0.9f, clipLen[CCast] / 1.2f);
            deathFadeAt = Mathf.Clamp(clipLen[CDeath] * 0.9f, 0.6f, 2f);
            graph.Play();
        }

        /// <summary>The model's height in world units as it was fitted (the hero's height in tiles), for the menu stages.</summary>
        internal float ShownHeight => def != null ? def.Height * transform.lossyScale.y : 1.8f;

        /// <summary>The highest point of the held weapons now (world y), negative infinity without any, for the menu stages: their
        /// meshes' highest vertex, since a turned box reaches above the mesh (Bram's hammer: 1.40 of his height against 1.36).</summary>
        internal float GearTop()
        {
            float top = float.NegativeInfinity;
            if (gear == null) return top;
            if (gearVerts == null) gearVerts = new Vector3[gear.Rends.Length][];
            for (int i = 0; i < gear.Rends.Length; i++)
            {
                Renderer r = gear.Rends[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (gearVerts[i] == null)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    Mesh m = mf != null ? mf.sharedMesh : null;
                    gearVerts[i] = m != null && m.isReadable ? m.vertices : new Vector3[0];
                }
                Vector3[] v = gearVerts[i];
                if (v.Length == 0) { top = Mathf.Max(top, r.bounds.max.y); continue; }
                Matrix4x4 w = r.localToWorldMatrix;
                for (int k = 0; k < v.Length; k++) top = Mathf.Max(top, w.m10 * v[k].x + w.m11 * v[k].y + w.m12 * v[k].z + w.m13);
            }
            return top;
        }

        /// <summary>Where a hero's shot leaves its hand now (UnitView.Muzzle, SHOT-02): Hilde's cannon mouth; Bonk's boulder between his
        /// hands; Moa's casting hand; else the held weapon's head (HeldGear.ShotFrom: the bow's arrow, the bomb, the hammer, anchor or
        /// the needle in the throwing fist), else that fist. False for minions, towers, the wolf and code figures.</summary>
        public override bool Muzzle(out Vector3 at) => MuzzleAt(true, out at);

        /// <summary>The same point as <see cref="Muzzle"/> without counting a release (the weapon debug view asks every frame).</summary>
        internal bool MuzzlePoint(out Vector3 at) => MuzzleAt(false, out at);

        bool MuzzleAt(bool release, out Vector3 at)
        {
            if (FormMouth(out at)) return true; // Kugg's turret: its guns (BodyForm.cs)
            if (CannonMouth(out at)) return true;
            if (HeroIndex < 0 || anim == null || !anim.isHuman) return false;
            Transform l = anim.GetBoneTransform(HumanBodyBones.LeftHand), r = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (l == null || r == null) return false;
            var h = (HeroId)HeroIndex;
            // the rules' release as the screen sees it: the game asks this the frame the swing's shot is first drawn
            if (release && atkT >= 0f && atkCut < 0f && castT < 0f) Probe.OnRelease(windup, atkT, Time.deltaTime * ArtKit.Slow);
            if (h == HeroId.Bonk) { at = (l.position + r.position) * 0.5f; return true; }
            // Kugg's right forearm is his cannon: the shot leaves its mouth, beyond the wrist by 0.46 of the forearm (the barrel reaches
            // 0.2 m past the wrist on a 0.44 m forearm), never from the other hand (Rickard 2026-11-09: "kugg slänger sin attack
            // istället för att skjuta från kanonen i armen")
            if (h == HeroId.Kugg)
            {
                Transform f = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                at = f != null ? r.position + (r.position - f.position) * 0.46f : r.position;
                return true;
            }
            bool right = ThrowsRight(castT >= 0f && (atkT < 0f || castT < atkT) ? CCast : CAttack);
            if (h != HeroId.Moa && gear != null && gear.ShotFrom(bowHero, right, out at)) return true;
            at = right ? r.position : l.position;
            return true;
        }

        // the hand that throws in a clip: the faster one at its measured blow (Tikta's needles, Moa's honey); the right without a measure
        bool ThrowsRight(int slot)
        {
            ClipInfo ci = clips != null && AnimFix.BlowTiming ? Info(clips[slot].GetAnimationClip()) : null;
            return ci == null || ci.Right;
        }

        /// <summary>The held weapons (null for a body without any), for the weapon debug view.</summary>
        internal HeldGear Gear => gear;

        internal bool CannonMouth(out Vector3 at)
        {
            at = Vector3.zero;
            return gear != null && gear.Mouth(out at);
        }

        // the weapons go on in the bind pose, before the clips play (HeldGear), and join the body's renderers and materials
        void AddGear(HeroArt art)
        {
            gear = HeldGear.Build(def.Gear, anim, art.Height * transform.lossyScale.y * gearK);
            if (gear == null) return;
            gear.OnMenu = Preview; // Tyra's bow hangs from her fist on the menu stage (WeaponDef.Menu)
            if (isActiveAndEnabled && !Armed.Contains(this)) Armed.Add(this);
            int n0 = rends.Length, ng = gear.Rends.Length;
            var r2 = new Renderer[n0 + ng];
            var o2 = new Material[n0 + ng][];
            var f2 = new Material[n0 + ng][];
            for (int i = 0; i < n0; i++) { r2[i] = rends[i]; o2[i] = opaqueSets[i]; f2[i] = fadeSets[i]; }
            for (int i = 0; i < ng; i++) { r2[n0 + i] = gear.Rends[i]; o2[n0 + i] = new[] { gear.Opaque[i] }; f2[n0 + i] = new[] { gear.Fade[i] }; }
            rends = r2; opaqueSets = o2; fadeSets = f2;
            var m2 = new Material[mats.Length + ng * 2];
            var fm2 = new Material[fadeMats.Length + ng];
            mats.CopyTo(m2, 0);
            fadeMats.CopyTo(fm2, 0);
            for (int i = 0; i < ng; i++) { m2[mats.Length + i * 2] = gear.Opaque[i]; m2[mats.Length + i * 2 + 1] = gear.Fade[i]; fm2[fadeMats.Length + i] = gear.Fade[i]; }
            mats = m2; fadeMats = fm2;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        void BuildCommon(RigDef d)
        {

            var dg = new GameObject("Decal");
            decalT = dg.transform;
            decalT.SetParent(transform, false);
            decalT.localPosition = new Vector3(0f, 0.02f, 0f);
            float rr = d.Radius * 1.25f + 0.08f;
            decalT.localScale = new Vector3(rr, 1f, rr);
            decalF = dg.AddComponent<MeshFilter>();
            decalR = dg.AddComponent<MeshRenderer>();
            decalR.sharedMaterial = ArtMaterials.UnlitFade;
            decalR.shadowCastingMode = ShadowCastingMode.Off;
            decalR.receiveShadows = false;

            // no level badge by the hero bars (Rickard 2026-10-28 "Lvl siffran behövs inte bredvid namnet"); the HUD top still shows it
            bar = new HealthBar(transform, d.BarWidth, d.Height + 0.28f, d.BarThick);
            status = new StatusFx(transform, d.Radius, d.Height);
            ResetState();
        }

        /// <summary>Fresh state for a new life out of the pool.</summary>
        public void ResetState()
        {
            moving = false; speed01 = 0f; moveW = 0f; hasLast = false; gSpd = 0f; atkT = -1f; castT = -1f; hitT = -1f; ownT = -1f; hitA = -1f; recoil = 0f; propBack = -1f; hp = -1f; channel = -1f; sprint = false; fourOn = false; fourW = 0f;
            atkCarry = castCarry = lastAtkW = lastCastW = 0f; drAtk = drCast = drHit = drUpA = drUpC = 0f;
            recRate = RecoverRate; atkGap = 1.4f; lastAtkAt = -10f; aimYaw = 0f; stanceW = 0f;
            squared = false; travelV = Vector3.zero; legW = legYaw = leanP = leanR = 0f; legDir = 1; lastSpd = 0f; lastYaw = transform.eulerAngles.y;
            dead = false; deathT = -1f; spawnT = -1f; vis = 1f;
            Probe.Reset();
            FormReset();
            atkCut = castCut = -1f;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.identity;
            if (def.ThrowsProp) bones[Hb.Prop].localScale = Vector3.one;
            if (gear != null) gear.Reset();
            bar.Reset();
            bar.SetMaxHp(0f);
            bar.SetEnergy(-1f, false);
            status.Visible = false; // silently: a view parked while shielded must not pop its shield where it last stood (ApplyLook shows it again)
            status.Set(StatusFlags.None);
            if (marks != null) marks.Reset();
            if (honey != null) honey.Reset();
            SetMarker(Team == Teams.Player ? UnitMarker.Ally : UnitMarker.Enemy);
            SetFlash(Color.black);
            fading = true; // force ApplyLook to set the material
            ApplyLook();
        }

        // ---------------------------------------------------------------- UnitView

        public override void SetMoving(bool isMoving, float speed) { moving = isMoving; speed01 = Mathf.Clamp(speed, 0f, 2f); }

        public override void Interrupt(bool swing)
        {
            // a root or knockback leaves the auto attack alone (the rules let a rooted hero swing)
            bool atkOn = atkT >= 0f && atkCut < 0f, cutAtk = swing && atkOn, cutCast = castT >= 0f && castCut < 0f;
            if (!imported) { if (cutAtk) atkT = -1f; if (cutCast) castT = -1f; }
            else
            {
                if (cutAtk) { atkCut = atkT; atkTotal = atkT + CutFade; }
                if (cutCast) { castCut = castT; castLen = castT + CutFade; }
            }
            // the throw waiting in the hand stays there, unless a swing that goes on still throws it
            if (gear != null && (cutAtk || cutCast) && !(atkOn && !cutAtk && atkT < windup)) gear.Cancel();
        }

        public override void PlayAttack(float w)
        {
            Probe.OnSwing(atkT >= 0f && atkCut < 0f && atkT < windup);
            windup = Mathf.Max(0.05f, w);
            // on the rules' clock (ArtKit.SetSimClock) the swing counts from this tick and its blow is the release tick itself
            atkTick = IsHero && !Preview ? ArtKit.SimTicks : -1;
            if (atkTick >= 0 && w > 0f) windup = AnimTiming.ReleaseTick(w) * AnimTiming.Tick;
            float now = Time.time;
            if (now - lastAtkAt < 4f) atkGap = now - lastAtkAt; // a fight's swings: how long the recovery may take (BlowSegment)
            lastAtkAt = now;
            atkT = 0f; atkCut = -1f;
            atkCarry = lastAtkW;
            atkSeg = false;
            if (clips != null)
            {
                // the blow lands on the clip's release frame exactly when the rules' windup runs out; the coil goes quickly to 70 % of
                // the way and waits there (anticipation), the slash plays the last 30 %, the clip stands still on the blow for this kind
                // of hero's hit-stop (impact), then plays out (recovery), never dragging on
                float len = clipLen[CAttack];
                atkRel = def.Art.AttackRelease * len;
                atkStart = Mathf.Min(def.Art.AttackStart * len, atkRel * 0.8f);
                atkSpeed = Mathf.Clamp((atkRel - atkStart) / windup, 0.6f, 4f);
                atkSlash = Mathf.Min(0.12f, windup * 0.35f);
                atkHold = Phased(HeroIndex) ? AttackHold(HeroIndex) : 0f;
                atkRec = Mathf.Min((len - atkRel) / atkSpeed, 0.45f);
                atkTotal = Phased(HeroIndex) ? windup + atkHold + atkRec : Mathf.Min((len - atkStart) / atkSpeed, windup + 0.45f);
                if (AnimFix.BlowTiming && !Phased(HeroIndex)) BlowSegment(len);
            }
            // secondary action (Cajsa 2026-11-11): the coil tugs the cloth back a little, the blow throws it on past the body
            ClothKick(0f, -0.2f, 0f); ClothKick(atkSeg ? atkHitAt : windup, 0.6f, 0.15f);
            if (gear != null)
            {
                // a bomb leaves the hand as the shot leaves it; Tikta's needle leaves the fist she throws with for a moment (TIKTA-06)
                if (HeroIndex == (int)HeroId.Tikta) gear.Throw(windup, 0.1f, true, ThrowsRight(CAttack) ? 1 : -1);
                else gear.Throw(windup);
                gear.Fire(windup);
            }
        }

        // AnimFix.BlowTiming: the part of the clip round its measured blow (old Speltest's own attacks: from, hit, to). The windup
        // plays the last stretch before the blow, about 1.8 times its own pace instead of the whole clip at the 4x cap (at most 0.45 s
        // of it: Bonk's long windup plays his heavy swing slowly instead of starting on a frame with the club held out level), so the
        // blow lands exactly as the rules' windup runs out. Blast's throw (5.4 s into a 7.7 s clip) was never shown at all.
        // The recovery plays on until the body is back up (the clip's Settle), at 1.2x or as fast as it takes to be done before the
        // next swing (at most 2.5x), and fades out over its last part as Speltest's did (weight 1 to 60 % of it): cut half a second
        // after the blow, Bonk's slam (Bram's too) was still bent double and blended up to the idle in a quarter of a second, a hop
        // (Rickard 2026-10-28: "som ett hopp i slutet av attacken"). Tyra's arrow leaves at full draw, not at her hand's fastest
        // moment, which in her shot is the reach for the arrow (the bow rose only after the arrow had flown).
        void BlowSegment(float len)
        {
            ClipInfo ci = Info(clips[CAttack].GetAnimationClip());
            // the plan's maths and its formulas are in AnimTiming.PlanAttack (Animation Foundation 1.0, headless-tested)
            // a melee hero's weapon meets its target when the rules land the blow (the release plus the blow's travel, Bonk's slam at
            // the release), not at the hand's fastest moment: Rickard 2026-10-30 "damage nummer kommer innan träffen från vapnet nu"
            atkHitAt = windup;
            float anchor = ci != null ? ci.Blow : -1f;
            if (AnimFix.ContactTiming && ci != null && ci.Contact > ci.Blow && ci.Blow > 0f && MeleeHit(HeroIndex, out float travel))
            {
                anchor = ci.Contact;
                atkHitAt = windup + travel;
            }
            AnimTiming.AttackPlan p = AnimTiming.PlanAttack(atkHitAt, len, def.Art.AttackStart, def.Art.AttackRelease,
                anchor, ci != null ? ci.Settle : -1f, ci != null ? ci.Draw : -1f, Bow, atkGap);
            if (!p.Valid) return;
            atkFrom = p.From; atkBlow = p.Blow; atkTo = p.To; atkSpeed = p.Speed; recRate = p.RecRate;
            atkTotal = p.Total; atkFade = p.Fade;
            atkSeg = true;
        }

        // Tyra's bow (AnimFix.BlowTiming): her arrow leaves at full draw, she turns the few degrees her clip's arrow points aside
        // (aimYaw) and stays in her archer's stance between shots (Stance)
        bool Bow => IsHero && bowHero && AnimFix.BlowTiming;

        // the hero's own move pushes the body's cloth and the props' ribbons alike (secondary action)
        void ClothKick(float delay, float fwd, float up) { cloth?.Kick(delay, fwd, up); gear?.KickSway(delay, fwd, up); }

        public override void PlayCast() => PlayCast(false);

        public override void PlayCast(bool drag)
        {
            castT = 0f; castCut = -1f;
            castCarry = lastCastW;
            castDrag = drag;
            if (IsHero && !dead && vis > 0.01f) Fx.HeroCast(HeroIndex, transform.position); // never gives a hider away
            castSeg = false;
            if (clips != null)
            {
                if (AnimFix.BlowTiming) CastSegment();
                if (!castSeg) { castLen = Mathf.Min(0.9f, clipLen[CCast] / 1.2f); castBlowAt = castLen * 0.35f; }
                double c0 = castSeg ? castFrom : 0.0;
                clips[CCast].SetTime(c0); clips[CCast].SetTime(c0); upCast.SetTime(c0); upCast.SetTime(c0);
            }
            if (gear != null) ThrowOnCast(clips != null ? castBlowAt : castLen * 0.35f);
            ClothKick(clips != null ? castBlowAt : castLen * 0.35f, 0.8f, 0.3f); // a special's peak throws the cloth harder than a swing
        }

        // what leaves the hand at the cast's peak: Blast's bomb (Thrown), and Knut's anchor only on the hook (the drag), for about as
        // long as he casts: his fist kept a second anchor while the chained one flew. The tapped slam keeps it in his fist (KNUT-03).
        // Bram's hammer leaves his fist as the rules throw it, after the charge, and is away while it flies its line (hero review
        // 2026-11-13): it stayed in his fist while a second hammer flew; the flying one is the same model now (WorldViews)
        void ThrowOnCast(float at)
        {
            if (HeroIndex == (int)HeroId.Knut) { if (castDrag) gear.Throw(at, KnutS.CastTime, true); }
            else if (HeroIndex == (int)HeroId.Bram && BramS.Silver) gear.Throw(BramS.BeamCharge, BramS.LineReach / BramS.LineSpeed, true);
            else gear.Throw(at);
        }

        // seconds from the cast to its gesture's peak: the rules' wind-up for a special that lands later (the slams of Knut, Bonk and
        // Oaken, Tyra's arrows, Bonk's boulder, Oaken's quake, Bram's charged throw), so the blow never shows before the hit; a tenth
        // of a second for one that takes effect at once
        float CastLead()
        {
            switch ((HeroId)HeroIndex)
            {
                case HeroId.Tyra: return castDrag ? TyraS.Windup : TyraS.VolleyWindup;
                case HeroId.Knut: return castDrag ? 0.12f : KnutS.SlamWindup;
                case HeroId.Bonk: return castDrag ? BonkS.BoulderWindup : BonkS.SlamWindup;
                case HeroId.Oaken: return castDrag ? OakenS.QuakeWindup : OakenS.SlamWindup;
                case HeroId.Bram: return BramS.Silver ? BramS.BeamCharge : 0.12f; // the throw as the hammer leaves, after the charge (hero review 2026-11-13: 0.33 s early)
                default: return 0.12f;
            }
        }

        // AnimFix.BlowTiming: the clip starts before its own measured peak so the peak comes as the special takes effect (CastLead),
        // instead of at 0, where Moa's, Blast's and Tyra's casts were cut off before their gesture; a clip with too little before its
        // peak for a long wind-up plays that part slower (Oaken's slam waits 0.6 s). Tyra's cast is a held draw: her arrows leave at
        // full draw, as her shot's. At least a quarter of a second passes after the peak (the clip holding its end), so the fade-out
        // never starts before it
        void CastSegment()
        {
            ClipInfo ci = Info(clips[CCast].GetAnimationClip());
            // the plan's maths and its formulas are in AnimTiming.PlanCast (Animation Foundation 1.0, headless-tested)
            AnimTiming.CastPlan c = AnimTiming.PlanCast(clipLen[CCast], ci != null ? ci.Blow : -1f, ci != null ? ci.Settle : -1f,
                ci != null ? ci.Draw : -1f, Bow, CastLead());
            if (!c.Valid) return;
            castFrom = c.From; castTo = c.To; castPeak = c.Peak; castPre = c.Pre; castBlowAt = c.BlowAt; castLen = c.Len; castFade = c.Fade;
            castSeg = true;
        }

        public override void PlayHit(bool big)
        {
            hitT = 0f;
            hitA = 0f;
            status.Ripple();
            if (clips != null) { clips[CHit].SetTime(0.0); clips[CHit].SetTime(0.0); upHit.SetTime(0.0); upHit.SetTime(0.0); }
            // (ANIM-13) a big hit flinches harder and longer than a small one, and the flinch clip's own jolt (its measured blow: Moa's,
            // Blast's and Hilde's 0.6 s in, after the old 0.3 s window had closed) lands at the flinch's peak
            hitBig = big;
            hitWin = big ? 0.5f : 0.3f;
            hitBlow = hitTo = -1f;
            AnimationClip hcl = clips != null ? clips[CHit].GetAnimationClip() : null;
            ClipInfo hci = hcl != null && hcl != clips[CIdle].GetAnimationClip() && AnimFix.BlowTiming ? Info(hcl) : null;
            if (hci != null && hci.Blow > 0f)
            {
                hitBlow = hci.Blow;
                hitTo = Mathf.Min(hcl.length, hci.Settle > hci.Blow ? hci.Settle : hci.Blow * 2f);
            }
            hitLen = big ? 0.25f : 0.14f;
            recoil = big ? 1f : 0.6f;
            ClothKick(0f, big ? -0.8f : -0.35f, big ? 0.25f : 0.1f); // the flinch jolts the cloth back
            flashColor = big ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.45f, 0.4f); // a short red blink on every hit (MLBB analysis 7)
        }

        public override void PlayOwnHit(float strength, float len)
        {
            if (ownT >= 0f && ownPeak * (1f - ownT / ownLen) > strength) return; // a brighter one still showing wins
            ownT = 0f; ownLen = Mathf.Max(0.05f, len); ownPeak = Mathf.Clamp01(strength);
        }

        public override void SetDead(bool d)
        {
            if (d == dead) return;
            dead = d;
            if (d)
            {
                deathT = 0f; atkT = -1f; castT = -1f;
                if (def.Kind == RigKind.Target)
                {
                    // bursts into splinters at once
                    Vector3 p = transform.position + new Vector3(0f, 0.75f, 0f);
                    Fx.Shards(p, 16, Pal.Get(Pal.Wood), 0.12f, 4.5f, 0.9f, 2.5f, 0.3f);
                    Fx.Shards(p, 8, Pal.Get(Pal.Red), 0.1f, 4f, 0.8f, 2.5f, 0.3f);
                    Fx.Shards(p, 8, Pal.Get(Pal.Cloth), 0.1f, 4f, 0.8f, 2.5f, 0.3f);
                    Fx.Smoke(p, 6, Pal.Get(Pal.Cloth), 0.5f, 1.2f, 0.6f, 0.5f, 0.2f);
                    Fx.Ring(transform.position, 0.3f, 1.4f, new Color(1f, 0.9f, 0.6f, 0.9f), 0.35f);
                    deathFadeAt = 0f;
                }
                if (clips != null) { clips[CDeath].SetTime(0.0); clips[CDeath].SetTime(0.0); clips[CDeath].SetSpeed(1.0); }
                if (imported) { leanP = leanR = 0f; model.localRotation = Quaternion.identity; } // no lean left in the fall (Life)
                // ANIM-10: a hero's fall as Speltest's (HeroAnim.cs AnimState/PoseHero, HeroMixamo.cs PoseMx): the Dying clip from its
                // start at its own speed, the body sinking from 0.6 s and faded out from 0.8 to 1.3 s. Ours faded from 2.0 s, so the
                // clip's first stagger stood 1.3 s at full height before anything happened (Unity measuring run 1, 2026-10-29)
                if (imported && IsHero && !Preview) { deathFadeAt = HeroFadeAt; deathFade = HeroFade; }
                // ANIM-07: a minion's as Speltest's (View3DWorld minions: deadT 0.6 s): it tips over to its side, sinks half a tile and
                // fades over 0.6 s, a puff of dust where it lands. Ours stood through its Dying clip's first 1.3 s and faded from 2 s
                // (Unity measuring run 2, 2026-10-29)
                else if (MinionBody) { deathFadeAt = 0f; deathFade = MinionDie; minionLanded = false; }
            }
            else
            {
                deathT = -1f;
                spawnT = 0f;
                if (def.Kind == RigKind.Target) deathFadeAt = 0.55f;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                bar.Reset();
            }
            ApplyLook();
        }

        public override void SetHealth(float hp01, float shield01) { hp = hp01; bar.Set(hp01, shield01); bar.Visible = BarOn; }

        // The bar: a minion's only once it has taken damage, as in Speltest (Hud.drawMinion: if (hurt) hpBar), so a full wave is not
        // a row of full bars (review 2026-11-13, nr 5: 40 % of the bars on screen were full-health minions); none at all while the
        // match intro flies in (ArtKit.BarsHidden; nr 11: the bars sat between the heroes' name plates)
        bool BarOn => vis > 0.01f && !dead && !Preview && !ArtKit.BarsHidden && (!minion || (hp >= 0f && hp < 0.999f));

        // ABC2 View3DCreeps CREEP_TINT: the whole figure takes 40 % of its side's colour (ours blue, theirs red) "so lanes read at a
        // glance". The rebuild drew both sides' goblins alike, only the bar told them apart (review 2026-11-13, nr 4). The view's Team
        // is already the side as the player sees it (MatchViews.Side), so the online top team sees its own minions blue too.
        static readonly Color CreepAlly = new Color(0.35f, 0.8f, 1f), CreepEnemy = new Color(1f, 0.32f, 0.36f);
        const float CreepTint = 0.4f;

        internal void MinionLook(int team)
        {
            minion = true;
            tint = Color.Lerp(Color.white, team == Teams.Player ? CreepAlly : CreepEnemy, CreepTint);
            for (int i = 0; i < mats.Length; i++)
                if (System.Array.IndexOf(fadeMats, mats[i]) < 0) mats[i].SetColor(ArtMaterials.BaseColor, tint);
            fading = true; // ApplyLook sets the materials again, the fading ones with the tint
            ApplyLook();
        }
        public override void SetLevel(int level) { } // the level badge is the HUD's (HudOverhead), over the bar's left end
        public override void SetMaxHp(float maxHp) => bar.SetMaxHp(maxHp);
        public override void SetEnergy(float charge01, bool ready) => bar.SetEnergy(charge01, ready);
        public override void SetStatus(StatusFlags flags) => status.Set(flags);

        public override void SetVisibility(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha == vis) return;
            vis = alpha;
            ApplyLook();
        }

        public override void SetChannel(float progress01) => channel = progress01;
        public override void SetSprint(bool on) => sprint = on;
        public override void SetFourLegs(bool on)
        {
            fourOn = on;
#if UNITY_EDITOR
            Debug.Log("[BAM.Art] " + name + " Wolf Run " + (on ? "on" : "off") + (on && cFour < 0 ? ": no RunFour clip in his HeroArt (BAM > Art > Rebuild Hero Models)" : ""));
#endif
        }

        // Ragn's Wolf Run (Rickard 2026-11-10: "kan vi få ragn att springa som ett djur när hans passiva är igång?"): his all-fours run
        // takes the run's place, blended in and out over a quarter second, on the same stride phase as the run
        AnimationClipPlayable four;
        int cFour = -1;
        bool fourOn;
        float fourW, fourLen;
        public override void SetSquared(bool on) => squared = on;
        public override void SetDeathmark(float left) { if (left >= 0f || marks != null) Marks().SetDeathmark(left); }
        public override void SetChainSpark(bool on) { if (on || marks != null) Marks().SetChainSpark(on); }
        public override void SetChainCooldown(float frac) { if (frac >= 0f || marks != null) Marks().SetChainCooldown(frac); }

        public override void SetHoneyShields(float guard, float field, float queen)
        {
            if (honey == null && guard < 0f && field < 0f && queen < 0f) return;
            (honey ?? (honey = new HoneyMarks(transform, def.Height, def.Radius))).Set(guard, field, queen);
        }

        HoneyMarks honey; // Moa's shield rings, made the first time one shows
        HeroMarks marks; // Tikta's Deathmark and Blast's chain marks, made the first time one shows
        HeroMarks Marks() => marks ?? (marks = new HeroMarks(transform, def.Height, def.Radius));

        public override void SetMarker(UnitMarker m)
        {
            marker = m;
            decalF.sharedMesh = Shapes.Decal(m);
            byte fill = m == UnitMarker.Player ? Pal.BarPlayer : m == UnitMarker.Enemy ? Pal.BarEnemy : m == UnitMarker.Ally ? Pal.BarAlly : (Team == Teams.Player ? Pal.BarAlly : Pal.BarEnemy);
            bar.SetColors(fill);
        }

        void SpellLooks(float dt)
        {
            if (channel >= 0f)
            {
                // recall: mint motes rise round the feet faster as it fills, and a ring closes in every half second
                channelT -= dt;
                if (channelT <= 0f)
                {
                    channelT = ArtKit.High ? 0.12f : 0.24f;
                    Color c = SpellFx.Of(SpellId.Recall);
                    var c32 = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 230);
                    float a = Random.value * Mathf.PI * 2f, r = 0.5f + Random.value * 0.4f;
                    Fx.Glow(transform.position + new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r), 1, c32, 0.16f, 0.1f, 0.6f, 1.2f + channel * 1.5f);
                    if ((int)(channel * 8f) != (int)((channel - 0.0001f) * 8f) || Random.value < 0.12f) Fx.Ring(transform.position, 1.1f, 0.3f, new Color(c.r, c.g, c.b, 0.7f), 0.45f);
                    // MLBB's recall: a tall beam of light over the hero (Rickard's light pillar), widening and brightening as it fills,
                    // white at its heart, and a slow swirl on the ground
                    float k = Mathf.Clamp01(channel);
                    Fx.Pillar(transform.position, 0.7f + k * 0.9f, 5.5f, new Color(c.r, c.g, c.b, 0.55f + k * 0.4f), 0.3f);
                    Fx.Pillar(transform.position, 0.25f + k * 0.35f, 5.5f, new Color(1f, 1f, 1f, 0.5f + k * 0.4f), 0.25f);
                    Fx.Glow(transform.position + new Vector3(0f, 0.6f + Random.value * 2.5f, 0f), 1, c32, 0.2f, 0.1f, 0.5f, 2.5f, 0.35f);
                    swirlT -= channelT;
                    if (swirlT <= 0f) { swirlT = 0.45f; Fx.Swirl(transform.position, 1.3f, new Color(c.r, c.g, c.b, 0.8f), 0.6f); }
                }
            }
            if (sprint && moving)
            {
                sprintT -= dt;
                if (sprintT <= 0f)
                {
                    sprintT = ArtKit.High ? 0.05f : 0.1f;
                    SpellFx.SpeedTrail(transform.position, transform.forward, SpellFx.Of(SpellId.Sprint), def.Height, ++sprintN);
                }
            }
        }

        // ---------------------------------------------------------------- look

        float DeathAlpha => deathT < 0f ? 1f : Mathf.Clamp01(1f - (deathT - deathFadeAt) / deathFade);

        void ApplyLook()
        {
            float a = vis * (dead ? DeathAlpha : 1f);
            bool on = a > 0.01f;
            if (on != renderersOn)
            {
                renderersOn = on;
                for (int i = 0; i < rends.Length; i++) rends[i].enabled = on;
            }
            decalR.enabled = on && !dead && !Preview;
            if (towerGlow != null) towerGlow.gameObject.SetActive(on && !dead && !Preview);
            bar.Visible = BarOn;
            status.Visible = vis > 0.01f && !dead && !Preview;
            if (!on) return;
            if (def.Ghost)
            {
                float ga = a * (0.8f + ghostPulse * 0.15f); // a fuller spectral wolf (was 0.62-0.8; Rickard 2026-10-28)
                for (int i = 0; i < ghostMats.Length; i++)
                {
                    Color gc = i == 0 ? Color.white : ghostTint;
                    gc.a = ga;
                    ghostMats[i].SetColor(ArtMaterials.BaseColor, gc);
                }
                return;
            }
            bool wantFade = a < 0.99f;
            if (wantFade)
            {
                Color c = Spectral ? new Color(SpectralFade.r, SpectralFade.g, SpectralFade.b, a) : new Color(tint.r, tint.g, tint.b, a);
                for (int i = 0; i < fadeMats.Length; i++) fadeMats[i].SetColor(ArtMaterials.BaseColor, c);
                if (!fading)
                {
                    for (int i = 0; i < rends.Length; i++) { rends[i].sharedMaterials = fadeSets[i]; rends[i].shadowCastingMode = ShadowCastingMode.Off; }
                    fading = true;
                }
            }
            else if (fading)
            {
                for (int i = 0; i < rends.Length; i++) { rends[i].sharedMaterials = opaqueSets[i]; rends[i].shadowCastingMode = i < shadowRends && ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; }
                fading = false;
            }
        }

        void SetFlash(Color c)
        {
            flashLit = c.r > 0f || c.g > 0f || c.b > 0f;
            for (int i = 0; i < mats.Length; i++) mats[i].SetColor(ArtMaterials.EmissionColor, c);
        }

        // ---------------------------------------------------------------- animation

        void LateUpdate()
        {
            if (def == null) return; // a script reload in Play mode clears the view's state: stay still instead of throwing every frame
            float dt = Time.deltaTime * ArtKit.Slow; // in a hit-stop as slow as the rules' ticks, so the swing waits with its shot
            bar.Visible = BarOn; // ArtKit.BarsHidden turns over without a call to the view
            if (lastQuality != ArtKit.QualityVersion)
            {
                lastQuality = ArtKit.QualityVersion;
                if (!fading && !def.Ghost)
                    for (int i = 0; i < shadowRends; i++) rends[i].shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            idleT += dt;
            moveW = AnimTiming.Approach(moveW, moving && !dead ? 1f : 0f, 6f, dt); // idle <-> run in 1/6 s at any frame rate
            if (moving) phase += dt * (7f + 5f * speed01);

            if (def.Ghost)
            {
                ghostPulse = Mathf.Sin(idleT * 5f) * 0.5f + 0.5f;
                if (renderersOn && !dead) ApplyLook();
            }

            if (hitT >= 0f) { hitT += dt; if (hitT >= hitLen) hitT = -1f; }
            if (ownT >= 0f) { ownT += dt; if (ownT >= ownLen) ownT = -1f; }
            // the player's own hit flashes white and wins over the red blink every hit has (tester feedback, Ski 2026-11-07)
            if (ownT >= 0f) SetFlash(Color.white * (ownPeak * (1f - ownT / ownLen)));
            else if (hitT >= 0f) SetFlash(flashColor * ((1f - hitT / hitLen) * 0.9f));
            else if (Spectral) SetFlash(SpectralGlow * (0.75f + 0.25f * Mathf.Sin(idleT * 2.4f + seed))); // his ghost light breathes
            else if (flashLit) SetFlash(Color.black); // a tower's shot pulse (PoseTower, later this frame) lights it again
            if (Spectral && !Preview && renderersOn && vis > 0.3f && !dead && (wispT -= dt) <= 0f)
            {   // pale ghost wisps rise off him, more as he runs (none at all while the enemy cannot see him: renderersOn is off)
                wispT = moving ? 0.12f : 0.3f;
                Fx.Wisps(transform.position + new Vector3(0f, 0.25f + Random.value * 0.6f, 0f), 1, SpectralWisp, 0.35f, 0.4f, 0.7f, 0.6f, 0.2f);
            }
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 5f);

            if (imported) AnimateImported(dt);
            if (Preview && imported && clips != null && !dead)
            {
                if (!breathTried) { breathTried = true; breath = PreviewBreath.For(anim, HeroIndex); }
                breath?.Apply(Time.unscaledTime, 1f - Mathf.Clamp01(gearAct));
                GroundFeet(dt);
            }
            if (!Preview && imported && clips != null && !dead)
            {
                if (AnimFix.LegsFollow) { LegYaw(dt); LegTwist(); }
                if (IsHero)
                {
                    if (AnimFix.Life)
                    {
                        Lean(dt);
                        if (!breathTried) { breathTried = true; breath = PreviewBreath.For(anim, HeroIndex); }
                        breath?.ApplyMatch(Time.time, 0.6f * (1f - moveW) * (1f - Mathf.Clamp01(gearAct)));
                    }
                    else model.localRotation = Quaternion.Euler(0f, aimYaw, 0f);
                    // standing, the feet go on the ground as on the menu stages: Bram's idle held him a quarter tile over his ring and
                    // some blows sank feet into it (ANIM-02, Unity run 2026-10-29); the stride is left as it plays (a run's flight
                    // has both feet up)
                    GroundFeet(dt, 1f - moveW);
                }
            }
            // (IsHero is false on the menu stages, where HeroIndex still names the hero: these two go by HeroIndex alone so they show
            // there too; Kugg's fist stayed in his head on Home, Rickard 2026-11-11)
            if (imported && !dead && HeroIndex == (int)HeroId.Nassla) ArmsOut(NasslaArmOut);
            if (imported && !dead && HeroIndex == (int)HeroId.Kugg) KuggGuard();
            // Blast's remake (V2): his clips, made on his old body, throw the big new head back and round (Rickard 2026-11-14: "hans
            // huvud är vikt bakåt", still after a fixed 25-degree nod); the head is held on the chest as it sits in the bind pose,
            // mostly, after the Animator, in the menu and in a match
            if (imported && headHold) HoldHead(BlastV2HeadHold);
            if (!Preview && imported && IsHero && !dead && HeroIndex == (int)HeroId.Kugg) KuggAim();
            // dead, that lift settles away over the fall (AnimateDeath sinks the body below it) instead of dropping it at the blow
            if (!Preview && imported && IsHero && dead) GroundFeet(dt, 0f);
            if (gear != null && clips != null && gear.NeedsRest) MeasureRest();
            if (gear != null)
            {
                gear.Floor = Preview ? float.NegativeInfinity : transform.position.y; // ANIM-08/09: weapons stay above the ground
                if (bowHero && !Preview) BowOffLegs();
                gear.Tick(dt, moveW, gearAct, gearAtk, dead);
            }
            if (dead) AnimateDeath(dt);
            else
            {
                if (!imported)
                switch (def.Kind)
                {
                    case RigKind.Humanoid: PoseHumanoid(dt); break;
                    case RigKind.Cart: PoseCart(dt); break;
                    case RigKind.Target: PoseTarget(dt); break;
                    default: PoseTower(dt); break;
                }
                if (spawnT >= 0f)
                {
                    spawnT += dt;
                    float s = spawnT >= 0.3f ? 1f : Back(spawnT / 0.3f);
                    model.localScale = new Vector3(s, s, s);
                    if (spawnT >= 0.3f) spawnT = -1f;
                }
            }

            // cloth and hair last, after everything else has posed the body this frame
            if (imported && (IsHero || Preview) && clips != null && HeroIndex >= 0)
            {
                if (!clothTried) { clothTried = true; cloth = ClothSway.For(anim, rends, HeroIndex, def.Art != null && def.Art.V2); }
                cloth?.Tick(dt, AnimFix.Cloth);
                gear?.TickSway(dt, AnimFix.Cloth); // the cloth on the held props (Moa's shield and staff)
            }

            // the old game's dust puffs behind running heroes (View3DWorld.SyncAmbience); none while see-through or hidden, so it never gives a hider away
            if (IsHero && moving && !dead && renderersOn && vis >= 0.99f)
            {
                dustT -= dt;
                if (dustT <= 0f)
                {
                    dustT = ArtKit.High ? 0.13f : 0.26f;
                    Fx.Dust(transform.position - transform.forward * 0.25f + new Vector3(0f, 0.08f, 0f), 1, new Color32(196, 178, 140, 170), 0.32f, 0.35f, 0.55f, 0.25f, 0.12f);
                }
            }

            if (!dead && renderersOn && vis >= 0.99f) SpellLooks(dt);
            if (marks != null && marks.Any)
            {
                marks.Visible = renderersOn && vis > 0.01f && !dead;
                marks.Tick(dt, idleT, ArtDriver.CamRot);
            }
            if (honey != null && honey.Any)
            {
                honey.Visible = renderersOn && vis > 0.01f && !dead;
                honey.Tick(dt, idleT);
            }
            if (renderersOn)
            {
                bar.Tick(dt, ArtDriver.CamRot);
                status.Tick(dt, idleT);
            }
            FormLook(dt); // Kugg's turret while he is anchored (BodyForm.cs)
        }

        void AnimateImported(float dt)
        {
            if (clips == null) return;
            // the swing, the cast and the flinch are measured the first frame the view is out (BlowTiming), heroes, minions and the
            // menu's alike, on one graph, not at the first swing in the middle of a fight
            if (!clipsInfo && AnimFix.BlowTiming && anim.isActiveAndEnabled) MeasureClips();
            if (AnimFix.BlowTiming) InPlace();
            else model.localPosition = Vector3.zero; // switched off live (Animation Check): nothing left held over
            if (AnimFix.UpperSwing) UpperTurn();
            bool life = AnimFix.Life;
            float wAtk = 0f, wCast = 0f, wHit = 0f, wDeath = 0f;
            if (dead)
            {
                wDeath = deathT < 0f ? 1f : Mathf.Clamp01(deathT / 0.15f);
            }
            else
            {
                if (atkT >= 0f)
                {
                    float atkWas = atkT;
                    // the rules' clock when the game feeds it: never behind the frame clock's start, never back
                    if (atkTick >= 0 && ArtKit.SimTicks >= atkTick) atkT = Mathf.Max(atkT, AnimTiming.SimElapsed(atkTick, ArtKit.SimTicks, ArtKit.SimAlpha));
                    else atkT += dt;
                    if (atkCut < 0f && atkWas < windup && atkT >= windup) Probe.OnBlow();
                    // a swing that outlives its plan (a NaN total the plans never make) ends instead of holding the body forever
                    if (AnimTiming.Stuck(atkT, atkTotal)) { Probe.Locks++; atkTotal = atkT; }
                    if (life)
                    {
                        // eased in (from where a swing still fading out left it) and out over a quarter of a second, never before
                        // the blow; the linear 0.06-0.16 s in and the 0.18 s out of a clip cut short read as a snap. Tyra steps into
                        // her stance over the whole windup.
                        float blendIn = Bow ? windup : Mathf.Clamp(windup * 0.6f, 0.08f, 0.2f), fadeOut = atkCut >= 0f ? CutFade : atkSeg ? atkFade : Mathf.Clamp(atkTotal - windup, 0.05f, 0.25f);
                        // on the move the recovery hands over to the stride within its fade-out instead of playing out bent over
                        // the running legs
                        if (atkSeg && atkT > windup && moveW > 0.3f && atkTotal - atkT > fadeOut) atkTotal = atkT + fadeOut;
                        wAtk = Mathf.Min(Mathf.Lerp(atkCarry, 1f, Smooth(atkT / blendIn)), Smooth((atkTotal - atkT) / fadeOut));
                    }
                    else
                    {
                        // the swing blends in over up to 0.16 s, not 0.06 (Rickard 2026-10-27: Bram's hammer hung low on the walk and
                        // "teleporteras" up into the swing); still well inside the windup, so the blow lands on time
                        float blendIn = Mathf.Clamp(windup * 0.45f, 0.06f, 0.16f);
                        wAtk = Mathf.Min(Mathf.Clamp01(atkT / blendIn), Mathf.Clamp01((atkTotal - atkT) / (atkCut >= 0f ? CutFade : 0.18f)));
                    }
                    double at = AttackTime(atkCut >= 0f ? atkCut : atkT);
                    clips[CAttack].SetTime(at); upAtk.SetTime(at);
                    if (atkT >= atkTotal) { atkT = -1f; atkCut = -1f; }
                }
                if (castT >= 0f)
                {
                    castT += dt;
                    if (AnimTiming.Stuck(castT, castLen)) { Probe.Locks++; castLen = castT; }
                    float castOut = castCut >= 0f ? CutFade : castSeg ? castFade : Mathf.Clamp(castLen - castBlowAt, 0.05f, 0.25f);
                    wCast = life ? Mathf.Min(Mathf.Lerp(castCarry, 1f, Smooth(castT / 0.1f)), Smooth((castLen - castT) / castOut))
                        : Mathf.Min(Mathf.Clamp01(castT / 0.08f), Mathf.Clamp01((castLen - castT) / 0.15f));
                    float cs = castCut >= 0f ? castCut : castT; // cut: the cast holds where it was
                    double ct = !castSeg ? cs * 1.2f : cs < castBlowAt ? castFrom + cs * castPre
                        : Mathf.Min(castTo, castPeak + (cs - castBlowAt) * CastRate);
                    clips[CCast].SetTime(ct); upCast.SetTime(ct);
                    if (castT >= castLen) { castT = -1f; castCut = -1f; }
                }
                if (hitA >= 0f)
                {
                    hitA += dt;
                    if (AnimFix.BlowTiming)
                    {
                        // the clip runs to its blow over the first half of the flinch and on to where it is back up over the second
                        float f = Mathf.Clamp01(hitA / hitWin);
                        wHit = Mathf.Sin(f * Mathf.PI) * (hitBig ? 0.9f : 0.55f);
                        if (hitBlow > 0f)
                        {
                            double ht = f < 0.5f ? hitBlow * f * 2f : hitBlow + (hitTo - hitBlow) * (f * 2f - 1f);
                            clips[CHit].SetTime(ht); upHit.SetTime(ht);
                        }
                        if (hitA >= hitWin) hitA = -1f;
                    }
                    else
                    {
                        wHit = Mathf.Sin(Mathf.Clamp01(hitA / 0.3f) * Mathf.PI) * 0.6f;
                        if (hitA >= 0.3f) hitA = -1f;
                    }
                }
            }
            lastAtkW = wAtk; lastCastW = wCast;
            if (bowHero) Stance(dt);
            float wStance = stanceW * (1f - moveW); // about how much of the stance shows (the clips over it take their share below)
            // (Tyra_11) her cast is a shot too: on the move, with no stance under it, the bow stood in the fist's own grip, level over
            // her head; it turns upright with its belly to the target as in her shot
            gearAct = wAtk + wCast + wStance; gearAtk = wAtk + wStance + (Bow ? wCast : 0f);
            aimYaw = Bow ? -(AimOf(CAttack) * Mathf.Min(1f, wAtk + wStance) + AimOf(CCast) * wCast) : 0f;
            // on the move the swing (with UpperSwing also the cast and the flinch) goes to the upper-body layer and the legs keep their
            // stride, as Speltest's layers 1 and 2; standing, the whole body plays it
            bool up = AnimFix.UpperSwing || Phased(HeroIndex);
            float uHit = AnimFix.UpperSwing ? wHit : 0f;
            float uA = wAtk * moveW, uC = wCast * moveW, uH = uHit * moveW, uSum = uA + uC + uH;
            float upW = dead || !up ? 0f : Mathf.Min(1f, uSum);
            if (uSum > 1e-4f) { upper.SetInputWeight(0, uA / uSum); upper.SetInputWeight(1, uC / uSum); upper.SetInputWeight(2, uH / uSum); }
            SetLayered(upW > 1e-4f);
            if (layered) layers.SetInputWeight(1, upW);
            drUpA = upW > 1e-4f && uSum > 1e-4f ? upW * uA / uSum : 0f;
            drUpC = Bow && upW > 1e-4f && uSum > 1e-4f ? upW * uC / uSum : 0f;
            if (upW > 0f) { wAtk -= uA; wCast -= uC; wHit -= uH; }
            float over = wAtk + wCast + wHit + wDeath;
            if (over > 1f) { wAtk /= over; wCast /= over; wHit /= over; wDeath /= over; over = 1f; }
            // (ANIM-11/12) only the share played on the whole body walks it away: the upper-body layer leaves the hips to the stride
            // (Unity run 1: Bram swinging on the strafe, Bonk and Blast on the move, the body pushed off by the drift it never made)
            drAtk = wAtk; drCast = wCast; drHit = wHit; drAtkFrom = atkSeg ? atkFrom : atkStart; drCastFrom = castSeg ? castFrom : 0f;
            float rest = 1f - over;
            float runK = Stride(dt);
            mixer.SetInputWeight(CIdle, rest * (1f - moveW) * (1f - stanceW));
            if (bowHero) mixer.SetInputWeight(CStance, rest * (1f - moveW) * stanceW);
            fourW = cFour >= 0 ? Mathf.MoveTowards(fourW, fourOn && !dead ? 1f : 0f, dt * 4f) : 0f;
            mixer.SetInputWeight(CWalk, rest * moveW * (1f - runK) * (1f - fourW));
            mixer.SetInputWeight(CRun, rest * moveW * runK * (1f - fourW));
            // on all fours at any pace: the crawl takes the walk's share too (at a jog the walk carried most of the stride)
            if (cFour >= 0) { four.SetTime(gaitPh * fourLen); mixer.SetInputWeight(cFour, rest * moveW * fourW); }
            mixer.SetInputWeight(CAttack, wAtk);
            mixer.SetInputWeight(CCast, wCast);
            mixer.SetInputWeight(CHit, wHit);
            mixer.SetInputWeight(CDeath, wDeath);
            // idle always loops here, even when a clip was imported without Loop Time (it would play once and freeze)
            Wrap(CIdle);
        }

        // Tyra's archer stance (Bow): from her first shot until a shot is overdue she stays in her shot clip's stance, feet apart and
        // side on, instead of stepping into it and out again at every shot (Rickard 2026-10-28: one leg kicked out at each shot).
        // Between shots her arms lower the bow after the release (the clip's first frame) and, timed by the fight's pace, reach for
        // an arrow and nock it (the clip from its start) just in time for the next windup, which draws from the very frame she holds.
        // On the move, when the shots stop or with BlowTiming off she eases back to her idle.
        void Stance(float dt)
        {
            float now = Time.time, next = lastAtkAt + atkGap; // when the next windup is due at the fight's pace
            bool on = Bow && !dead && moveW < 0.3f && now < next + 0.6f;
            stanceW = Mathf.MoveTowards(stanceW, on ? 1f : 0f, dt / (on ? 0.2f : 0.45f));
            if (stanceW <= 0f) return;
            float ready = atkSeg ? atkFrom : 0f;
            // under a windup it holds the frame the draw starts from (a shot that came early blends from wherever the nock was)
            float t = atkT >= 0f && atkT <= windup ? ready : Mathf.Max(0f, ready - Mathf.Max(0f, next - now));
            stance.SetTime(t);
        }

        // AnimFix.BlowTiming: a clip that walks the body away (Fenn's attack lunges 1.2 hip heights, Bram's Attack2 and Blast's and
        // Moa's casts 0.5-0.8, Bram's flinch 0.4) is held over its spot: the hips' drift since its played part began, by how much it
        // plays, comes off the model, so the body no longer slides out and snaps back. With the times, starts and weights the pose on
        // screen was evaluated with (set last frame; a swing begun in this frame's Update has not been seen yet).
        void InPlace()
        {
            if (instT == null) return;
            Vector2 d = Vector2.zero;
            if (drAtk > 1e-3f) d += Drift(CAttack, drAtkFrom) * drAtk;
            if (drCast > 1e-3f) d += Drift(CCast, drCastFrom) * drCast;
            if (drHit > 1e-3f) d += Drift(CHit, 0f) * drHit;
            Vector3 k = instT.localScale;
            model.localPosition = Quaternion.Euler(0f, aimYaw, 0f) * new Vector3(-d.x * k.x, 0f, -d.y * k.z);
        }

        // the track is kept in hip heights, so a clip measured on one rig holds another in place by that rig's own size. (ANIM-11..13)
        // Held over where the idle stands the hips, not over the clip's own first played frame: Bram's, Bonk's and Blast's specials
        // start their played part with the hips already 0.2-0.6 m out (Blast crouched down-left), which the old start never took off.
        Vector2 Drift(int c, float from)
        {
            AnimationClip clip = clips[c].GetAnimationClip();
            if (clip == null || !clipInfo.TryGetValue(clip, out ClipInfo ci) || ci.Hips == null) return Vector2.zero;
            return (HipsAt(ci, (float)clips[c].GetTime()) - (restSet ? restHips : HipsAt(ci, from))) * (hipsH > 1e-4f ? hipsH : 1f);
        }

        // which way the arrow points at the full draw of clip c (degrees, left negative), 0 if not measured
        float AimOf(int c)
        {
            AnimationClip clip = clips[c].GetAnimationClip();
            return clip != null && clipInfo.TryGetValue(clip, out ClipInfo ci) ? ci.AimL : 0f;
        }

        // Tyra_02/05: on the move her bow keeps out of her legs (HeldGear.OffLegs), all of it once she runs
        Transform[] legBones;
        Animator legAnim;
        void BowOffLegs()
        {
            if (legAnim != anim) { legAnim = anim; legBones = null; } // a rebuilt model has new bones
            if (legBones == null && anim != null && anim.isHuman)
                legBones = new[]
                {
                    anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), footL,
                    anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), footR,
                };
            gear.Legs = legBones;
            gear.LegsW = dead ? 0f : moveW;
        }

        // AnimFix.UpperSwing (ANIM-05, Unity run 1 2026-10-29): the upper-body layer takes the swing's spine and arms but not its hips,
        // so a swing whose whole body turns into the blow (Bonk's overhead slam, Bram's too: the hips 135 degrees round at the blow)
        // swung its arms from hips still facing the target and the weapon landed beside or behind the hero on the move (Bram 17-62,
        // Bonk 9-51 degrees off the line to the target, Tyra's bow hand to the side). The hips' turn the clip has at the frame on
        // screen, by the swing's share of the upper layer, goes back on: some on the pelvis and the rest up the spine, so the blow
        // lands where it does standing. Same frame and weight the pose was evaluated with. (ANIM-20) The thighs are turned back about
        // their own joints, so the legs keep their stride: turned with the hips, a planted foot swung round with them (Unity run 3:
        // Bram's and Bonk's feet slid 0.4-0.5 of the way the body went in their swings on the move, 0.02-0.04 between swings).
        const float UpperTurnHips = 0.4f;
        // Tyra's cast as well (Tyra_11, Unity run 1): her volley is shot side on like her arrows, and on the run it went off square
        void UpperTurn()
        {
            if ((drUpA < 1e-3f && drUpC < 1e-3f) || hipsT == null || spineT == null) return;
            float deg = ClipYaw(upAtk, drUpA) + ClipYaw(upCast, drUpC);
            if (Mathf.Abs(deg) < 0.05f) return;
            Vector3 up = transform.up;
            Quaternion hips = Quaternion.AngleAxis(deg * UpperTurnHips, up), back = Quaternion.Inverse(hips);
            hipsT.rotation = hips * hipsT.rotation;
            if (thighL != null) thighL.rotation = back * thighL.rotation;
            if (thighR != null) thighR.rotation = back * thighR.rotation;
            float rest = deg * (1f - UpperTurnHips);
            if (chestT != null)
            {
                spineT.rotation = Quaternion.AngleAxis(rest * 0.6f, up) * spineT.rotation;
                chestT.rotation = Quaternion.AngleAxis(rest * 0.4f, up) * chestT.rotation;
            }
            else spineT.rotation = Quaternion.AngleAxis(rest, up) * spineT.rotation;
        }

        static float ClipYaw(AnimationClipPlayable p, float w)
        {
            if (w < 1e-3f) return 0f;
            AnimationClip clip = p.GetAnimationClip();
            return clip != null && clipInfo.TryGetValue(clip, out ClipInfo ci) && ci.Yaw != null ? YawAt(ci, (float)p.GetTime()) * w : 0f;
        }

        static float YawAt(ClipInfo ci, float t)
        {
            int n = ci.Yaw.Length;
            if (n < 2 || ci.Step <= 0f) return n > 0 ? ci.Yaw[0] : 0f;
            float f = Mathf.Clamp(t / ci.Step, 0f, n - 1);
            int i = Mathf.Min((int)f, n - 2);
            return Mathf.LerpAngle(ci.Yaw[i], ci.Yaw[i + 1], f - i);
        }

        static Vector2 HipsAt(ClipInfo ci, float t)
        {
            int n = ci.Hips.Length;
            if (n < 2 || ci.Step <= 0f) return n > 0 ? ci.Hips[0] : Vector2.zero;
            float f = Mathf.Clamp(t / ci.Step, 0f, n - 1);
            int i = Mathf.Min((int)f, n - 2);
            return Vector2.Lerp(ci.Hips[i], ci.Hips[i + 1], f - i);
        }

        // AnimFix.BlowTiming: a clip's blow (its fastest hand between 10 and 85 % of it, as the Blender measure of 2026-10-28,
        // bam-work/art/rigg-anim/matning: where the weapon meets a target standing in front; the hand stops 0.1 s later, Bonk's club
        // on the ground) and its hips' track over the ground in hip heights, 30 times a second; measured once per clip, on a manual
        // graph as GroundSpeed, the bones put back after. (2026-10-28, after Rickard's test) Also where the body is back up after the
        // blow (Settle: head, hands and feet round the hips and the hips' height within 0.3 hip heights of the first frame; Bonk's
        // slam 1.9 s, Knut's 2.1, Tikta's 1.5; -1 if never, Tyra's shot ends drawn), and for a bow the full draw (Draw: the bow hand
        // at its highest from 10 % on) with which way the arrow points there (AimL: degrees from straight ahead, left negative), and
        // which hand is the fast one at the blow (Right; the hand a shot leaves from, Muzzle)
        sealed class ClipInfo { public float Blow = -1f, Contact = -1f, Settle = -1f, Draw = -1f, AimL, Step = 1f / 30f; public bool Right = true; public Vector2[] Hips; public float[] Yaw; }
        static readonly Dictionary<AnimationClip, ClipInfo> clipInfo = new Dictionary<AnimationClip, ClipInfo>();
        static readonly List<AnimationClip> measureList = new List<AnimationClip>();

        // the swing, the cast and the flinch not measured yet, all on one graph (a clip standing in for a missing one, the idle, not)
        void MeasureClips()
        {
            clipsInfo = true;
            measureList.Clear();
            AnimationClip idle = clips[CIdle].GetAnimationClip();
            for (int c = CAttack; c <= CHit; c++)
            {
                AnimationClip cl = clips[c].GetAnimationClip();
                if (cl != null && cl != idle && !clipInfo.ContainsKey(cl) && !measureList.Contains(cl)) measureList.Add(cl);
            }
            // the idle too, for the spot its hips stand on (its mean over the clip, the sway averaged out)
            if (idle != null && !clipInfo.ContainsKey(idle)) measureList.Add(idle);
            Measure(anim, measureList, hipsH);
            if (idle != null && clipInfo.TryGetValue(idle, out ClipInfo ici) && ici.Hips != null && ici.Hips.Length > 0)
            {
                Vector2 sum = Vector2.zero;
                foreach (Vector2 h in ici.Hips) sum += h;
                restHips = sum / ici.Hips.Length;
                restSet = true;
            }
        }

        ClipInfo Info(AnimationClip c)
        {
            if (c == null) return null;
            if (clipInfo.TryGetValue(c, out ClipInfo known)) return known;
            measureList.Clear();
            measureList.Add(c);
            Measure(anim, measureList, hipsH);
            return clipInfo.TryGetValue(c, out known) ? known : null;
        }

        static void Measure(Animator an, List<AnimationClip> list, float hipsH)
        {
            if (list.Count == 0 || an == null || !an.isHuman || !an.isActiveAndEnabled) return;
            Transform root = an.transform, hips = an.GetBoneTransform(HumanBodyBones.Hips);
            Transform lh = an.GetBoneTransform(HumanBodyBones.LeftHand), rh = an.GetBoneTransform(HumanBodyBones.RightHand);
            if (hips == null || lh == null || rh == null) return;
            Transform head = an.GetBoneTransform(HumanBodyBones.Head);
            Transform lf = an.GetBoneTransform(HumanBodyBones.LeftFoot), rf = an.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform lul = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rul = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            float hs = hipsH > 1e-4f ? hipsH : 1f;
            Transform[] bones = SaveBones(root, out Vector3[] lp, out Quaternion[] lr);
            PlayableGraph g = PlayableGraph.Create("BAM clip info");
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var output = AnimationPlayableOutput.Create(g, "info", an);
                foreach (AnimationClip c in list)
                {
                    var ci = new ClipInfo();
                    clipInfo[c] = ci; // measured once, even if it fails: the clip then plays as before 2026-10-28
                    if (c.length < 1e-3f) continue; // a one-key pose: no blow, nothing to hold in place
                    var cp = AnimationClipPlayable.Create(g, c);
                    cp.Pause();
                    output.SetSourcePlayable(cp);
                    int n = Mathf.Max(2, Mathf.CeilToInt(c.length * 30f)) + 1;
                    float step = c.length / (n - 1);
                    var track = new Vector2[n];
                    var L = new Vector3[n];
                    var R = new Vector3[n];
                    var pose = new float[n * 16];
                    var yaw = lul != null && rul != null ? new float[n] : null;
                    for (int i = 0; i < n; i++)
                    {
                        cp.SetTime(step * i); g.Evaluate(0f);
                        Vector3 h = root.InverseTransformPoint(hips.position);
                        track[i] = new Vector2(h.x, h.z) / hs;
                        L[i] = root.InverseTransformPoint(lh.position); R[i] = root.InverseTransformPoint(rh.position);
                        int o = i * 16;
                        PoseOf(pose, o, L[i] - h, hs); PoseOf(pose, o + 3, R[i] - h, hs);
                        PoseOf(pose, o + 6, head != null ? root.InverseTransformPoint(head.position) - h : Vector3.zero, hs);
                        PoseOf(pose, o + 9, lf != null ? root.InverseTransformPoint(lf.position) - h : Vector3.zero, hs);
                        PoseOf(pose, o + 12, rf != null ? root.InverseTransformPoint(rf.position) - h : Vector3.zero, hs);
                        pose[o + 15] = h.y / hs;
                        if (yaw != null)
                        {
                            Vector3 a = root.InverseTransformPoint(rul.position) - root.InverseTransformPoint(lul.position);
                            a.y = 0f;
                            yaw[i] = a.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(Vector3.right, a, Vector3.up) : 0f;
                        }
                    }
                    ci.Step = step;
                    ci.Hips = track;
                    ci.Yaw = yaw;
                    float best = 0f;
                    for (int i = 1; i < n - 1; i++)
                    {
                        float f = (float)i / (n - 1);
                        if (f < 0.1f || f > 0.85f) continue;
                        float sl = (L[i + 1] - L[i - 1]).sqrMagnitude, sr = (R[i + 1] - R[i - 1]).sqrMagnitude, sp = Mathf.Max(sl, sr);
                        if (sp > best) { best = sp; ci.Blow = step * i; ci.Right = sr >= sl; }
                    }
                    // the contact: where the striking hand has braked to 40 % of its fastest after the blow (the weapon meets its
                    // target and stops; the fastest moment itself is still mid-swing), at most 0.35 s of clip after it
                    if (ci.Blow >= 0f)
                    {
                        Vector3[] H = ci.Right ? R : L;
                        int b0 = Mathf.RoundToInt(ci.Blow / step);
                        float peak = (H[Mathf.Min(n - 1, b0 + 1)] - H[Mathf.Max(0, b0 - 1)]).magnitude;
                        for (int i = b0 + 1; i < n - 1 && step * (i - b0) <= 0.35f; i++)
                            if ((H[i + 1] - H[i - 1]).magnitude <= 0.4f * peak) { ci.Contact = step * i; break; }
                    }
                    if (ci.Blow >= 0f)
                        for (int i = 0; i < n; i++)
                        {
                            if (step * i < ci.Blow + 0.1f) continue;
                            float d2 = 0f;
                            for (int k = 0; k < 16; k++) { float e = pose[i * 16 + k] - pose[k]; d2 += e * e; }
                            if (d2 <= 0.09f) { ci.Settle = step * i; break; }
                        }
                    int top = -1;
                    for (int i = 0; i < n; i++)
                        if ((float)i / (n - 1) >= 0.1f && (top < 0 || L[i].y > L[top].y)) top = i;
                    if (top >= 0)
                    {
                        ci.Draw = step * top;
                        Vector3 a = L[top] - R[top];
                        a.y = 0f;
                        ci.AimL = a.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(Vector3.forward, a, Vector3.up) : 0f;
                    }
                }
            }
            finally
            {
                g.Destroy();
                RestoreBones(bones, lp, lr);
            }
        }

        static void PoseOf(float[] p, int o, Vector3 v, float hs) { p[o] = v.x / hs; p[o + 1] = v.y / hs; p[o + 2] = v.z / hs; }

        static Transform[] SaveBones(Transform root, out Vector3[] lp, out Quaternion[] lr)
        {
            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            lp = new Vector3[bones.Length];
            lr = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { lp[i] = bones[i].localPosition; lr[i] = bones[i].localRotation; }
            return bones;
        }

        static void RestoreBones(Transform[] bones, Vector3[] lp, Quaternion[] lr)
        {
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) { bones[i].localPosition = lp[i]; bones[i].localRotation = lr[i]; }
        }

        // AnimFix.FixedGrip: the weapons' rest aim from three frames of the idle (HeldGear.SampleRest), on a manual graph as above
        void MeasureRest()
        {
            AnimationClip idle = clips[CIdle].GetAnimationClip();
            if (idle == null || anim == null || !anim.isHuman || !anim.isActiveAndEnabled) return;
            Transform[] bones = SaveBones(anim.transform, out Vector3[] lp, out Quaternion[] lr);
            PlayableGraph g = PlayableGraph.Create("BAM rest");
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var output = AnimationPlayableOutput.Create(g, "rest", anim);
                var cp = AnimationClipPlayable.Create(g, idle);
                cp.SetApplyFootIK(true);
                cp.Pause();
                output.SetSourcePlayable(cp);
                gear.BeginRest();
                for (int k = 0; k < 3; k++) { cp.SetTime(idle.length * (0.2f + 0.25f * k)); g.Evaluate(0f); gear.SampleRest(); }
                gear.EndRest();
            }
            finally
            {
                g.Destroy();
                RestoreBones(bones, lp, lr);
            }
        }

        // AnimFix.LegsFollow (old Speltest MxLegYaw and MxLegTwist, Simon 2026-10-21): while the body faces a target or an aim
        // (MatchViews, SetSquared) and moves, the hips and legs turn to the line of travel, the stride plays backwards when the travel
        // is behind, and the spine turns the chest back to the target, a quarter of the turn left in it; the clips only step forward,
        // so backing off the feet slid forward under a body going back
        void LegYaw(float dt)
        {
            bool on = squared && !dead && moveW > 0.01f && travelV.sqrMagnitude > 0.04f;
            legW += ((on ? 1f : 0f) - legW) * K60(12f, dt);
            if (legW < 0.01f && !on) { legW = 0f; legDir = 1; legYaw = 0f; return; }
            float want = 0f;
            if (on) want = LegYawFor(Vector3.SignedAngle(transform.forward, new Vector3(travelV.x, 0f, travelV.z), Vector3.up) * Mathf.Deg2Rad, ref legDir);
            // straight across the front, never round the back: both are angles off the body's front within 110 degrees, and the wrapped
            // way round, at a flip of the stride, twisted the hips through the back and the chest after them
            legYaw += (want - legYaw) * K60(14f, dt);
        }

        void LegTwist()
        {
            float deg = legYaw * legW * moveW * Mathf.Rad2Deg;
            if (Mathf.Abs(deg) < 0.05f || hipsT == null || spineT == null) return;
            Vector3 up = transform.up;
            hipsT.rotation = Quaternion.AngleAxis(deg, up) * hipsT.rotation;
            const float back = 1f - TorsoKeep;
            if (chestT != null)
            {
                spineT.rotation = Quaternion.AngleAxis(-deg * back * 0.6f, up) * spineT.rotation;
                chestT.rotation = Quaternion.AngleAxis(-deg * back * 0.4f, up) * chestT.rotation;
            }
            else spineT.rotation = Quaternion.AngleAxis(-deg * back, up) * spineT.rotation;
        }

        // Speltest CombatFeel.LegYaw: the legs follow the travel up to a little past the side; further back the stride turns round and
        // plays backwards, with some give either way so it does not flicker at the side
        const float LegHyst = 0.35f, LegMax = Mathf.PI / 2f + LegHyst, TorsoKeep = 0.25f;
        static float LegYawFor(float rel, ref int dir)
        {
            rel = WrapPi(rel);
            float side = Mathf.PI / 2f, a = Mathf.Abs(rel);
            if (dir >= 0 && a > side + LegHyst) dir = -1;
            else if (dir < 0 && a < side - LegHyst) dir = 1;
            else if (dir == 0) dir = 1;
            float y = dir > 0 ? rel : WrapPi(rel - Mathf.PI);
            return Mathf.Clamp(y, -LegMax, LegMax);
        }

        static float WrapPi(float a) => Mathf.Repeat(a + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
        // = Min(1, dt * k) at 60 fps, the same curve at any frame rate (Speltest HeroAnim.K60)
        static float K60(float k, float dt) => k >= 60f ? (dt > 0f ? 1f : 0f) : 1f - Mathf.Pow(1f - k / 60f, dt * 60f);

        // AnimFix.Life: a running hero leans into its turns and forward as it speeds up (back a little as it stops), a few degrees on
        // the model under the clips; Speltest kept a little of the bank into turns too (PoseMx)
        void Lean(float dt)
        {
            if (dt <= 1e-4f) return;
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt, acc = (gSpd - lastSpd) / dt;
            lastYaw = yaw; lastSpd = gSpd;
            float roll = Mathf.Clamp(-yawRate * 0.012f, -7f, 7f) * moveW, pitch = Mathf.Clamp(acc * 0.25f, -4f, 5f) * moveW;
            leanR += (roll - leanR) * K60(8f, dt);
            leanP += (pitch - leanP) * K60(6f, dt);
            model.localRotation = Quaternion.Euler(leanP, aimYaw, leanR);
        }

        // Kugg's right arm is his cannon (Rickard 2026-11-09, his picture: the arm held straight out at the target and the shot leaving its
        // mouth). Firing A Gun holds a rifle across the chest, so while his attack plays the upper arm and then the forearm are turned
        // until the forearm points level where he faces (his target in a fight); MuzzleAt then puts the shot at the barrel's mouth
        // Nässla in Moa's Idle (Rickard 2026-11-10, his picture: her right hand grey and lumpy): Moa's hands hang at her hips, and
        // Nässla's long hands went in through her tunic. At rest both arms turn out from her sides, as far as they hang (a raised arm
        // in her throw or cast is left alone), on the menu stage too
        const float NasslaArmOut = 14f;
        // and her palms face forward in Moa's Idle (Rickard 2026-11-10, his picture: "hennes händer är vända med handflaterna framåt"):
        // each hanging arm also turns about its own length, so the palms face her sides (checked on Unity's own pose of her idle)
        const float NasslaPalmIn = 60f;
        void ArmsOut(float deg)
        {
            if (anim == null || !anim.isHuman) return;
            float w = (1f - Mathf.Clamp01(gearAct)) * deg;
            if (w <= 0.01f) return;
            Vector3 fwd = anim.transform.forward, down = -anim.transform.up; // her own axes (the menu stage turns the model, not the view)
            void Out(HumanBodyBones u, HumanBodyBones l, float side)
            {
                Transform up = anim.GetBoneTransform(u), lo = anim.GetBoneTransform(l);
                if (up == null || lo == null) return;
                Vector3 d = lo.position - up.position;
                float m = d.magnitude, hang = m < 1e-6f ? 1f : Mathf.Clamp01(Vector3.Dot(d / m, down) / 0.7f);
                up.rotation = Quaternion.AngleAxis(side * w * hang, fwd) * up.rotation;
                if (m >= 1e-6f) up.rotation = Quaternion.AngleAxis(side * NasslaPalmIn * (w / deg) * hang, d / m) * up.rotation;
            }
            Out(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 1f);
            Out(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -1f);
        }

        // Kugg's free left fist in his mirrored idle (Kugg/Anim/IdleMirror, Rickard 2026-11-11, his pictures: "nu håller han andra handen
        // i huvudet", and after turning the arm out 50 degrees "det gör den visst"): the clip holds it up in front of his face, and on
        // his broad stone body it went into his head. Standing, a two-bone IK lowers it instead: the fist hangs beside his hip, a little
        // out and forward of the shoulder (KuggFist, in arm lengths: out, down, forward), the elbow out and back. Not on the move
        // (his walk and run swing as made) and not while he swings or casts.
        static readonly Vector3 KuggFist = new Vector3(0.55f, 0.6f, 0.3f); // 0.3 out still went into his body (Rickard 2026-11-11: "armen går in i hans kropp")
        void KuggGuard()
        {
            float w = (1f - Mathf.Clamp01(gearAct)) * (1f - Mathf.Clamp01(moveW));
            if (w <= 0.01f || anim == null || !anim.isHuman) return;
            Transform up = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), lo = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (up == null || lo == null || hand == null) return;
            // "out" is measured on the body itself: from his chest out through this shoulder, level (Rickard 2026-11-11, "handen i
            // magen": the fist went in front of his belly, so the side taken from the transform's axes was the wrong one here)
            Transform body = anim.transform, chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Spine);
            Vector3 fwd = body.forward, upW = body.up;
            Vector3 outW = chest != null ? Vector3.ProjectOnPlane(up.position - chest.position, upW) : -body.right;
            if (outW.sqrMagnitude < 1e-8f) return;
            outW.Normalize();
            fwd = Vector3.ProjectOnPlane(fwd, outW).normalized;
            Vector3 a = up.position, b = lo.position, c = hand.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude, arm = la + lb;
            if (la < 1e-5f || lb < 1e-5f) return;
            Vector3 goal = a + (outW * KuggFist.x - upW * KuggFist.y + fwd * KuggFist.z) * arm;
            Vector3 hint = a + (outW * 0.9f - upW * 0.2f - fwd * 0.2f) * arm;
            goal = Vector3.Lerp(c, goal, w);
            Vector3 at = goal - a;
            float d = Mathf.Clamp(at.magnitude, Mathf.Abs(la - lb) + 1e-4f, arm - 1e-4f);
            Vector3 dir = at.sqrMagnitude > 1e-10f ? at.normalized : -upW;
            float x = (la * la - lb * lb + d * d) / (2f * d), hg = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
            Vector3 pole = Vector3.ProjectOnPlane(hint - a, dir);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.ProjectOnPlane(outW, dir).normalized;
            Vector3 elbow = a + dir * x + pole * hg;
            up.rotation = Quaternion.FromToRotation(b - a, elbow - a) * up.rotation;
            Vector3 lp = lo.position;
            lo.rotation = Quaternion.FromToRotation(hand.position - lp, a + dir * d - lp) * lo.rotation;
        }

        void KuggAim()
        {
            float w = Mathf.Clamp01(gearAtk);
            if (w <= 0.01f || anim == null || !anim.isHuman) return;
            Transform up = anim.GetBoneTransform(HumanBodyBones.RightUpperArm), lo = anim.GetBoneTransform(HumanBodyBones.RightLowerArm),
                hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (up == null || lo == null || hand == null) return;
            Vector3 aim = transform.forward; aim.y = 0f;
            if (aim.sqrMagnitude < 1e-4f) return;
            aim.Normalize();
            up.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hand.position - up.position, aim), w) * up.rotation;
            lo.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hand.position - lo.position, aim), w) * lo.rotation;
        }

        // Old Speltest's stride (HeroMixamoGait.MxStride, Rickard 2026-10-26: Tikta and Tyra "hukar och går snabbt"): BAM played only
        // the run at 0.8-1.6x whatever the speed, so the feet slid and pattered. Now the speed over the ground, in hip heights a second,
        // picks how much walk and how much run play, and the stride runs at the rate that keeps the planted foot still on the ground.
        // Returns the run's share of the blend.
        float Stride(float dt)
        {
            if (!gaitMeasured) MeasureGait(clips[CWalk].GetAnimationClip(), clips[CRun].GetAnimationClip());
            Vector3 p = transform.position;
            float raw = 0f;
            Vector3 vel = Vector3.zero;
            if (hasLast && dt > 1e-4f)
            {
                Vector3 d = p - lastPos; d.y = 0f;
                if (d.magnitude < 1f) { raw = d.magnitude / dt; vel = d / dt; } // a respawn or a pull jumps: not a step
            }
            lastPos = p; hasLast = true;
            travelV += (vel - travelV) * Mathf.Min(1f, dt * 10f); // the way it goes, for LegsFollow
            gSpd += (raw - gSpd) * Mathf.Min(1f, dt * (raw < gSpd ? 22f : 10f)); // Speltest's CombatFeel.GroundSpeed: stops faster than it starts
            float hipsW = hipsH * anim.transform.lossyScale.y;
            float rel = hipsW > 1e-4f ? gSpd / hipsW : 0f;
            // the menu's preview walks on the spot: a steady jog for its speed
            if (moving && rel < 0.3f) rel = 3f * Mathf.Max(0.5f, speed01);
            float wLen = clipLen[CWalk], rLen = clipLen[CRun];
            float top = Mathf.Max(walkSpd + 0.5f, 0.6f * runSpd);
            float runK = Mathf.Clamp01((rel - walkSpd) / (top - walkSpd));
            float reach = !Preview && (uint)HeroIndex < (uint)RunReach.Length ? RunReach[HeroIndex] : 1f;
            float stride = Mathf.Lerp(walkSpd * wLen, runSpd * reach * rLen, runK), len = Mathf.Lerp(wLen, rLen, runK);
            // at slowest half the clip's own pace: Fenn's, Tyra's and Moa's runs need 0.52-0.57 of it at their match speed, and held at
            // 0.6 their planted feet slipped back 6-14 % (Unity run 2)
            float rate = Mathf.Clamp(rel / Mathf.Max(0.05f, stride), 0.5f / len, 1.8f / len);
            // backing off with its legs turned round (LegsFollow), the stride plays backwards
            float back = AnimFix.LegsFollow && legDir < 0 && legW > 0.5f ? -1f : 1f;
            if (moveW > 0.01f) gaitPh = Mathf.Repeat(gaitPh + dt * rate * back, 1f);
            clips[CWalk].SetTime(gaitPh * wLen);
            clips[CRun].SetTime(gaitPh * rLen);
            return runK;
        }

        // the run's stride in a match against GroundSpeed's measure of it, by HeroId (Unity run 2026-10-29 on Rickard's PC, ANIM-01):
        // played at the measured speed, the planted foot slipped back 7-31 % of the way the body went (the clip ran too fast for the
        // ground), Hilde's was carried 24 % forward. The same run clip lands differently on each rig with its foot IK, so it goes by
        // hero; measure again when a hero's model or run clip changes. Run 2 (2026-10-29): Fenn's is Original-Fenn's (Clay-Fenn's was
        // 1.24), Moa's and Hilde's from their slip at 1x and 2x
        static readonly float[] RunReach = { 1.106f, 1.11f, 1.313f, 1.28f, 1.073f, 1.184f, 1.218f, 1.319f, 0.84f, 1.147f };

        // a view built under the hidden pool root can't play its clips yet: measured the first frame it is out
        void MeasureGait(AnimationClip walk, AnimationClip run)
        {
            if (anim == null || !anim.isActiveAndEnabled) return;
            gaitMeasured = true;
            float w = GroundSpeed(anim, walk), r = GroundSpeed(anim, run);
            walkSpd = w > 0f ? w : 2.2f;
            runSpd = r > 0f ? r : 5f;
            if (runSpd < walkSpd + 0.5f) runSpd = walkSpd + 0.5f; // a hero whose walk is its run (no own walk)
        }

        // a clip's speed over the ground in hip heights a second, from how fast its planted foot passes back under the hips (Speltest's
        // MxGroundSpeed); the same on every humanoid rig, so measured once per clip
        static readonly Dictionary<AnimationClip, float> groundSpd = new Dictionary<AnimationClip, float>();
        static float GroundSpeed(Animator an, AnimationClip c)
        {
            if (c == null || an == null || !an.isHuman) return -1f;
            if (groundSpd.TryGetValue(c, out float known)) return known;
            Transform root = an.transform, hips = an.GetBoneTransform(HumanBodyBones.Hips);
            Transform lf = an.GetBoneTransform(HumanBodyBones.LeftFoot), rf = an.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || lf == null || rf == null) return -1f;
            float hipsY = root.InverseTransformPoint(hips.position).y;
            if (hipsY < 1e-4f) return -1f;
            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            var lp = new Vector3[bones.Length];
            var lr = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { lp[i] = bones[i].localPosition; lr[i] = bones[i].localRotation; }
            PlayableGraph g = PlayableGraph.Create("BAM gait");
            g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            float v = -1f;
            try
            {
                var output = AnimationPlayableOutput.Create(g, "gait", an);
                int n = Mathf.Max(8, Mathf.CeilToInt(c.length * 60f)) + 1;
                var H = new Vector3[n]; var L = new Vector3[n]; var R = new Vector3[n];
                var cp = AnimationClipPlayable.Create(g, c);
                cp.Pause();
                output.SetSourcePlayable(cp);
                for (int i = 0; i < n; i++)
                {
                    cp.SetTime(c.length * i / (n - 1)); g.Evaluate(0f);
                    H[i] = root.InverseTransformPoint(hips.position); L[i] = root.InverseTransformPoint(lf.position); R[i] = root.InverseTransformPoint(rf.position);
                }
                // a foot is planted while it is near its lowest; there it moves back under the hips at the speed of the ground
                var s = new List<float>();
                float step = c.length / (n - 1);
                foreach (Vector3[] F in new[] { L, R })
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (Vector3 q in F) { lo = Mathf.Min(lo, q.y); hi = Mathf.Max(hi, q.y); }
                    float cut = lo + 0.2f * (hi - lo);
                    for (int i = 1; i < n - 1; i++)
                    {
                        if (F[i].y > cut) continue;
                        Vector3 d = (F[i + 1] - H[i + 1]) - (F[i - 1] - H[i - 1]); d.y = 0f;
                        if (d.z < 0f) s.Add(d.magnitude / (2f * step) / hipsY); // backwards: the forward end of a step is still swinging
                    }
                }
                if (s.Count >= 3) { s.Sort(); v = Mathf.Clamp(s[s.Count / 2], 0.6f, 12f); }
            }
            finally
            {
                g.Destroy();
                for (int i = 0; i < bones.Length; i++) if (bones[i] != null) { bones[i].localPosition = lp[i]; bones[i].localRotation = lr[i]; }
            }
            if (v > 0f) groundSpd[c] = v;
            return v;
        }

        float LowestFoot()
        {
            float y = float.MaxValue;
            if (footL != null) y = Mathf.Min(y, footL.position.y);
            if (footR != null) y = Mathf.Min(y, footR.position.y);
            if (toeL != null) y = Mathf.Min(y, toeL.position.y);
            if (toeR != null) y = Mathf.Min(y, toeR.position.y);
            return y == float.MaxValue ? float.NaN : y;
        }

        // moves the model so its lowest foot bone stays at its bind-pose height over the stage (smoothed, so a step or a swing does
        // not make it bob); on the menu stages, and in a match by w (a hero standing, ANIM-02). The lift is worked out fresh each
        // frame from where the feet are with `model` level and goes on `model`, which only this view moves: on the Animator's own
        // transform (2026-10-27) it never took, and Rickard's Home still had Bonk a tenth of his height over the ring the next day
        void GroundFeet(float dt, float w = 1f)
        {
            if (float.IsNaN(footRef)) return;
            float low = LowestFoot();
            if (float.IsNaN(low)) return;
            float s = Mathf.Max(1e-4f, transform.lossyScale.y);
            Vector3 mp = model.localPosition;
            float want = (transform.position.y + footRef - (low - mp.y * s)) / s * w; // the lift that puts the foot on its mark
            // up at once and down eased, so a hop in a blow still leaves the ground: eased both ways, a foot under its mark stayed under
            // while the lift caught up (Unity run 2: Bram's blows out of his raised idle and Bonk's landings sank the feet 0.23-0.28
            // tiles). With w 0 (on the move, dead) it only eases, so a dead hero's lift settles over the fall
            groundY = !groundSet || (w > 0f && want > groundY) ? want : Mathf.Lerp(groundY, want, Mathf.Min(1f, Mathf.Max(dt, 1f / 60f) * 8f));
            groundSet = true;
            mp.y = groundY;
            model.localPosition = mp;
#if UNITY_EDITOR
            // one line per menu hero, so a hero that still floats tells why: bounds reaching under the soles (now ignored), the
            // foot mark, the lift the stage needed and whether anything moved the Animator's own transform
            if (Preview && !groundLogged && (groundLogT += dt) > 1f && instT != null)
            {
                groundLogged = true;
                float hh = Mathf.Max(0.01f, def.Height * s);
                Debug.Log("[BAM.Art] " + (def.Art != null ? def.Art.name : name) + " on its stage: renderer bounds under the soles " + (soleGap * s / hh).ToString("P1") + ", foot mark " +
                    (footRef / hh).ToString("P1") + ", lift " + (groundY * s / hh).ToString("P1") + ", Animator root moved " +
                    ((instT.localPosition.y - instY0) * s / hh).ToString("P1") + " of its height");
            }
#endif
        }

        // the lowest (BindBottom) or highest (BindTop) point of the meshes in the pose the model was made in: each mesh's stored
        // bounds through its own node (no CPU read, so fine on a phone), or a renderer's bounds where it has no mesh. All the heroes
        // and minions stand with their soles on their origin there (Blender 2026-10-28)
        /// <summary>The box of the skinned meshes as the skin draws them in the bind pose (a fresh instance's bones stand in it): the
        /// mesh's bounds through a bone's world matrix times its bind matrix. False without a skinned mesh.</summary>
        static bool SkinBindBox(Renderer[] rs, out Bounds box)
        {
            box = default;
            bool any = false;
            foreach (Renderer r in rs)
            {
                if (!(r is SkinnedMeshRenderer sk) || sk.sharedMesh == null) continue;
                Mesh m = sk.sharedMesh;
                Transform[] bs = sk.bones;
                Matrix4x4[] bp = m.bindposes;
                Matrix4x4 w = Matrix4x4.zero;
                bool found = false;
                for (int i = 0; i < bs.Length && i < bp.Length; i++) if (bs[i] != null) { w = bs[i].localToWorldMatrix * bp[i]; found = true; break; }
                if (!found) continue;
                Bounds b = m.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = w.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            return any;
        }

        /// <summary>How much of Blast's V2 head pose comes from the bind pose on his chest (the rest from his clips).</summary>
        const float BlastV2HeadHold = 0.85f;
        bool headHold;
        Transform holdHead, holdChest;
        Quaternion holdRel = Quaternion.identity;

        /// <summary>Notes the head's turn on the chest in the bind pose (a fresh instance stands in it), for <see cref="HoldHead"/>.</summary>
        void MeasureHeadHold()
        {
            headHold = false;
            if (anim == null || !anim.isHuman) return;
            holdHead = anim.GetBoneTransform(HumanBodyBones.Head);
            holdChest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            if (holdHead == null || holdChest == null) return;
            holdRel = Quaternion.Inverse(holdChest.rotation) * holdHead.rotation;
            headHold = true;
        }

        void HoldHead(float w)
        {
            if (holdHead == null || holdChest == null) return;
            holdHead.rotation = Quaternion.Slerp(holdHead.rotation, holdChest.rotation * holdRel, w);
        }

        static float BindBottom(Renderer[] rs, float fallback) => BindY(rs, fallback, false);
        static float BindTop(Renderer[] rs, float fallback) => BindY(rs, fallback, true);

        static float BindY(Renderer[] rs, float fallback, bool top)
        {
            float none = top ? float.MinValue : float.MaxValue, y = none;
            foreach (Renderer r in rs)
            {
                Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                if (m == null) { y = top ? Mathf.Max(y, r.bounds.max.y) : Mathf.Min(y, r.bounds.min.y); continue; }
                Bounds b = m.bounds;
                Matrix4x4 w = r.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    float py = w.MultiplyPoint3x4(p).y;
                    y = top ? Mathf.Max(y, py) : Mathf.Min(y, py);
                }
            }
            return y == none ? fallback : y;
        }

        // standing (and in the menu) the graph is the plain mixer as before the layers came; a swing on the move routes it through
        // the layer mixer, whose second layer carries the upper body
        void SetLayered(bool on)
        {
            if (on == layered) return;
            layered = on;
            if (on)
            {
                graph.Connect(mixer, 0, layers, 0);
                layers.SetInputWeight(0, 1f);
                animOut.SetSourcePlayable(layers);
            }
            else
            {
                animOut.SetSourcePlayable(mixer);
                graph.Disconnect(layers, 0);
            }
        }

        // where the attack clip stands atkT s into the swing (the four phases, see PlayAttack)
        double AttackTime(float t)
        {
            // as before 2026-10-27: the clip at its own pace, from AttackStart (Bram skips the hammer's dip to the side, so it rises
            // straight off his shoulder)
            if (atkSeg) return t < atkHitAt ? atkFrom + t * atkSpeed : Mathf.Min(atkTo, atkBlow + (t - atkHitAt) * recRate); // = AnimTiming.AttackClipTime
            if (!Phased(HeroIndex)) return Mathf.Min(clipLen[CAttack], atkStart + t * atkSpeed);
            float coil = windup - atkSlash;
            if (t < coil) { float w = t / Mathf.Max(1e-4f, coil); return atkRel * 0.7f * (1f - (1f - w) * (1f - w) * (1f - w)); }
            if (t < windup) { float w = (t - coil) / Mathf.Max(1e-4f, atkSlash); return atkRel * (0.7f + 0.3f * w * w); }
            if (t < windup + atkHold) return atkRel;
            return Mathf.Min(clipLen[CAttack], atkRel + (t - windup - atkHold) * atkSpeed);
        }

        // the four phases and the upper-body swing on the move are switched off for every hero (Rickard 2026-10-27, after trying
        // them: Tyra "nästan alltid har dragen båge och att armen ser ut att gå in i karaktären", Knut's swing "snabbspolar just vid
        // attacken"). The clips play at their own pace as before; the code stays for a later, gentler try.
        static bool Phased(int hero) => false;

        // the heroes whose auto is a blow in reach (MatchViews' melee arcs) and how long after the release the rules land it
        // (Combat.UpdateAuto: the shot's Travel, HeroDef.ProjTime; Bonk's club lands as its Smash, at the release)
        static bool MeleeHit(int hero, out float travel)
        {
            travel = 0f;
            switch ((HeroId)hero)
            {
                case HeroId.Bonk: return true;
                case HeroId.Fenn: case HeroId.Knut: case HeroId.Oaken: case HeroId.Ragn: case HeroId.Ravna:
                    var d = BAM.Sim.Defs.Hero((HeroId)hero);
                    travel = d.ProjTime > 0f ? d.ProjTime : 0.1f;
                    return true;
                default: return false;
            }
        }

        // the hit-stop on the blow by kind of hero (Speltest HeroAttack.cs ATK_STYLE hold): melee 0.06 s, ranged 0.035, magic 0.045,
        // tank 0.085 (Bram, Knut and Bonk; Speltest's ATK_CLASS has Bram and Knut as tanks)
        static float AttackHold(int hero)
        {
            switch ((HeroId)hero)
            {
                case HeroId.Bram: case HeroId.Knut: case HeroId.Bonk: return 0.085f;
                case HeroId.Tyra: return 0.035f;
                case HeroId.Moa: case HeroId.Blast: return 0.045f;
                default: return 0.06f;
            }
        }

        static AvatarMask upperMask;
        // Speltest's MxUpper: the spine, head, arms and fingers
        static AvatarMask UpperMask()
        {
            if (upperMask != null) return upperMask;
            var m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var p in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                m.SetHumanoidBodyPartActive(p, true);
            return upperMask = m;
        }

        void Wrap(int c)
        {
            double t = clips[c].GetTime(), len = clipLen[c];
            if (t >= len) clips[c].SetTime(t % len);
        }

        static float Back(float t) { t -= 1f; return 1f + t * t * (2.7f * t + 1.7f); }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        const float HeroSinkAt = 0.6f, HeroSink = 0.9f, HeroFadeAt = 0.8f, HeroFade = 0.5f, MinionDie = 0.6f;
        bool minionLanded;
        // the minions' imported bodies; the siege goblin keeps the old cart's RigKind (ANIM-07b: it missed the minions' death)
        bool MinionBody => imported && !IsHero && !Preview && (def.Kind == RigKind.Humanoid || def.Kind == RigKind.Cart);

        void AnimateDeath(float dt)
        {
            if (deathT < 0f) return;
            float before = deathT;
            deathT += dt;
            float t = Mathf.Clamp01(deathT / 0.45f);
            if (imported)
            {
                if (IsHero && !Preview)
                {
                    // Speltest's sink: 0.9 tiles a second from 0.6 s (below InPlace's height, which is 0, and the standing lift as it
                    // settles away, ANIM-02)
                    Vector3 lp = model.localPosition;
                    lp.y -= Mathf.Max(0f, deathT - HeroSinkAt) * HeroSink / Mathf.Max(1e-4f, transform.lossyScale.y);
                    model.localPosition = lp;
                }
                else if (MinionBody)
                {
                    // ANIM-07 (Speltest View3DWorld): over to its side by 85 degrees in the first 45 % of the 0.6 s, sinking 0.5 tiles in the rest
                    float k = Mathf.Clamp01(deathT / MinionDie), fall = Mathf.Clamp01(k / 0.45f);
                    model.localRotation = Quaternion.Euler(0f, 0f, -85f * fall * fall);
                    Vector3 lp = model.localPosition;
                    lp.y -= Mathf.Clamp01((k - 0.45f) / 0.55f) * 0.5f / Mathf.Max(1e-4f, transform.lossyScale.y);
                    model.localPosition = lp;
                    if (fall >= 1f && !minionLanded)
                    {
                        minionLanded = true;
                        if (renderersOn && vis >= 0.99f)
                            Fx.Dust(transform.position + transform.right * 0.35f + new Vector3(0f, 0.05f, 0f), 6, new Color32(217, 204, 184, 115), 0.42f, 1f, 0.4f, 0.25f, 0.1f);
                    }
                }
            }
            else if (def.Kind == RigKind.Target)
            {
                float s = 1f - Smooth(deathT / 0.12f);
                model.localScale = new Vector3(s, s, s);
            }
            else if (def.Kind == RigKind.Tower)
            {
                model.localPosition = new Vector3(Mathf.Sin(deathT * 40f) * 0.04f * (1f - t), -t * t * 2.2f, 0f);
                model.localRotation = Quaternion.Euler(t * 12f, 0f, t * 6f);
            }
            else if (def.Kind == RigKind.Humanoid)
            {
                // tip over backwards with a little bounce
                model.localRotation = Quaternion.Euler(-85f * Smooth(t), 0f, 0f);
                model.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.12f, -0.15f * t);
                bones[Hb.ArmL].localRotation = Quaternion.Euler(-150f * t, 0f, -30f * t);
                bones[Hb.ArmR].localRotation = Quaternion.Euler(-150f * t, 0f, 30f * t);
            }
            else
            {
                model.localRotation = Quaternion.Euler(0f, 0f, 85f * Smooth(t));
                model.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.1f, 0f);
            }
            if (deathT > deathFadeAt || before <= deathFadeAt) ApplyLook();
            if (deathT > deathFadeAt + 0.65f) deathT = deathFadeAt + 0.65f;
        }

        // Positive x rotation swings a hanging limb backward, negative forward (Unity's left-handed frame).
        void PoseHumanoid(float dt)
        {
            float swing = Mathf.Sin(phase) * 38f * Mathf.Clamp(0.6f + speed01 * 0.5f, 0.6f, 1.2f) * moveW;
            float idle = Mathf.Sin(idleT * 2.4f + seed);
            float legL = -swing, legR = swing;
            float armLx = swing * 0.8f, armRx = -swing * 0.8f, armLz = -8f, armRz = 8f;
            float chestX = moveW * 8f + idle * 1.5f * (1f - moveW) - recoil * 14f, chestY = 0f;
            float headX = -moveW * 4f;
            float bob = moveW * Mathf.Abs(Mathf.Cos(phase)) * 0.05f + (1f - moveW) * (idle * 0.5f + 0.5f) * 0.015f;
            float propX = 0f;

            if (atkT >= 0f)
            {
                atkT += dt;
                float rec = 0.28f;
                float u = Mathf.Clamp01(atkT / windup), r = Mathf.Clamp01((atkT - windup) / rec);
                bool released = atkT >= windup;
                float w = released ? 1f - Smooth((r - 0.55f) / 0.45f) : Smooth(u * 2.5f);
                float aRx = armRx, aLx = armLx, cY = 0f, cX = chestX;
                switch (def.Attack)
                {
                    case AttackStyle.Swing:
                        aRx = released ? Mathf.Lerp(-170f, 15f, Smooth(r * 2.2f)) : Mathf.Lerp(-20f, -170f, Smooth(u));
                        cY = released ? Mathf.Lerp(-25f, 20f, Smooth(r * 2.2f)) : -25f * Smooth(u);
                        cX = released ? 12f : -6f * u;
                        break;
                    case AttackStyle.Throw:
                        aRx = released ? Mathf.Lerp(130f, -80f, Smooth(r * 2.5f)) : Mathf.Lerp(0f, 130f, Smooth(u));
                        aLx = released ? -40f : Mathf.Lerp(0f, -60f, Smooth(u));
                        cY = released ? Mathf.Lerp(-30f, 15f, Smooth(r * 2.5f)) : -30f * Smooth(u);
                        cX = released ? 14f : -10f * u;
                        if (def.ThrowsProp && released && propBack < 0f) propBack = 0.45f;
                        break;
                    case AttackStyle.Bow:
                        aLx = -88f;
                        aRx = released ? Mathf.Lerp(-80f, -40f, Smooth(r * 3f)) : -88f;
                        cY = released ? Mathf.Lerp(-45f, -20f, Smooth(r * 3f)) : -45f * Smooth(u * 1.5f);
                        armRz = released ? 8f : Mathf.Lerp(8f, 50f, Smooth(u)); // the drawing hand pulls back toward the chest
                        break;
                    case AttackStyle.Cast:
                        aRx = released ? Mathf.Lerp(-120f, -70f, Smooth(r * 2f)) : Mathf.Lerp(0f, 40f, Smooth(u));
                        aLx = released ? Mathf.Lerp(-100f, -40f, Smooth(r * 2f)) : Mathf.Lerp(0f, 30f, Smooth(u));
                        cX = released ? 10f : -8f * u;
                        propX = released ? Mathf.Lerp(-30f, 0f, r) : 20f * u;
                        break;
                    case AttackStyle.Flick:
                        aRx = released ? Mathf.Lerp(-100f, -60f, Smooth(r * 2f)) : Mathf.Lerp(0f, 70f, Smooth(u));
                        aLx = released ? Mathf.Lerp(-100f, -60f, Smooth(r * 2f)) : Mathf.Lerp(0f, 70f, Smooth(u));
                        armLz = released ? -35f : -8f; armRz = released ? 35f : 8f;
                        cX = released ? 10f : -6f * u;
                        break;
                }
                armRx = Mathf.Lerp(armRx, aRx, w);
                armLx = Mathf.Lerp(armLx, aLx, w);
                chestY = Mathf.Lerp(0f, cY, w);
                chestX = Mathf.Lerp(chestX, cX, w);
                if (released && r >= 1f) atkT = -1f;
            }

            if (castT >= 0f)
            {
                castT += dt;
                const float len = 0.5f;
                float c = castT / len;
                float w = c < 0.2f ? Smooth(c / 0.2f) : 1f - Smooth((c - 0.6f) / 0.4f);
                float up = c < 0.35f ? -165f : Mathf.Lerp(-165f, -70f, Smooth((c - 0.35f) / 0.3f));
                armRx = Mathf.Lerp(armRx, up, w);
                armLx = Mathf.Lerp(armLx, up, w);
                armLz = Mathf.Lerp(armLz, -25f, w);
                armRz = Mathf.Lerp(armRz, 25f, w);
                bob += Mathf.Sin(Mathf.Clamp01(c / 0.5f) * Mathf.PI) * 0.12f * w;
                if (c >= 1f) castT = -1f;
            }

            if (propBack >= 0f)
            {
                // the thrown prop is gone from the hand until it "comes back"
                propBack -= dt;
                float s = propBack > 0.15f ? 0f : 1f - propBack / 0.15f;
                if (propBack <= 0f) { propBack = -1f; s = 1f; }
                bones[Hb.Prop].localScale = new Vector3(s, s, s);
            }

            bones[Hb.Hips].localPosition = def.Pivots[Hb.Hips] + new Vector3(0f, bob, 0f);
            bones[Hb.Chest].localRotation = Quaternion.Euler(chestX, chestY + Mathf.Sin(phase) * 6f * moveW, 0f);
            bones[Hb.Head].localRotation = Quaternion.Euler(headX - chestX * 0.4f, 0f, 0f);
            bones[Hb.ArmL].localRotation = Quaternion.Euler(armLx, 0f, armLz);
            bones[Hb.ArmR].localRotation = Quaternion.Euler(armRx, 0f, armRz);
            bones[Hb.LegL].localRotation = Quaternion.Euler(legL, 0f, 0f);
            bones[Hb.LegR].localRotation = Quaternion.Euler(legR, 0f, 0f);
            bones[Hb.Prop].localRotation = Quaternion.Euler(propX, 0f, 0f);
            bones[Hb.Extra].localRotation = Quaternion.Euler(moveW * 10f + Mathf.Sin(idleT * 18f) * 4f, 0f, 0f);
        }

        void PoseCart(float dt)
        {
            float spin = phase * 60f;
            float kick = 0f, pitch = 0f;
            if (atkT >= 0f)
            {
                atkT += dt;
                float u = Mathf.Clamp01(atkT / windup), r = Mathf.Clamp01((atkT - windup) / 0.35f);
                pitch = atkT < windup ? -12f * Smooth(u) : Mathf.Lerp(-22f, 0f, Smooth(r));
                kick = atkT < windup ? 0f : Mathf.Lerp(-0.12f, 0f, Smooth(r));
                if (r >= 1f) atkT = -1f;
            }
            bones[Models.CartBody].localPosition = def.Pivots[Models.CartBody] + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 2f)) * 0.015f * moveW, kick * 0.4f);
            bones[Models.CartBody].localRotation = Quaternion.Euler(-recoil * 6f, 0f, Mathf.Sin(phase * 2f) * 2f * moveW);
            bones[Models.CartBarrel].localPosition = def.Pivots[Models.CartBarrel] - def.Pivots[Models.CartBody] + new Vector3(0f, 0f, kick);
            bones[Models.CartBarrel].localRotation = Quaternion.Euler(pitch, 0f, 0f);
            bones[Models.CartWheelL].localRotation = Quaternion.Euler(spin, 0f, 0f);
            bones[Models.CartWheelR].localRotation = Quaternion.Euler(spin, 0f, 0f);
        }

        // stands still; a hit knocks the board back on its post and it wobbles out
        void PoseTarget(float dt)
        {
            atkT = -1f; castT = -1f;
            float wob = recoil * Mathf.Sin(idleT * 30f) * 14f + recoil * 10f;
            bones[Models.TargetBoard].localRotation = Quaternion.Euler(wob, 0f, Mathf.Sin(idleT * 1.3f) * 1.5f);
        }

        // a soft halo round the Tripo tower's crystal in the team colour (Rickard 2026-11-02: the model's crystal looked flat without
        // the painted glow of his concept): it breathes slowly and flares while the tower fires. Only for the prop tower, whose
        // crystal is in the model; the code-built tower has its own crystals on the TowerCrystal bone.
        Transform towerGlow;
        Material towerGlowMat;

        void TowerGlow(float pulse)
        {
            if (def.PropMesh == null || def.Kind != RigKind.Tower) return;
            bool show = !dead && renderersOn && !Preview;
            if (towerGlow == null)
            {
                if (!show) return;
                Texture2D t = RickardFx.Load("tx_fx_glow_soft");
                if (t == null) return;
                towerGlowMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                towerGlowMat.SetTexture(ArtMaterials.BaseMap, t);
                towerGlowMat.mainTexture = t;
                towerGlowMat.renderQueue = 3060;
                var go = new GameObject("CrystalGlow");
                towerGlow = go.transform;
                towerGlow.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = FxAtlas.FullQuad();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = towerGlowMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            if (towerGlow.gameObject.activeSelf != show) towerGlow.gameObject.SetActive(show);
            if (!show) return;
            // the crystal's middle, a little under the bone (the bone sits near its tip)
            towerGlow.position = transform.position + new Vector3(0f, def.Pivots[Models.TowerCrystal].y - 0.1f, 0f);
            towerGlow.rotation = ArtDriver.CamRot * Quaternion.Euler(-90f, 0f, 0f); // the flat quad turned to face the camera
            float sz = 1.3f + pulse * 0.8f, a = 0.42f + 0.1f * Mathf.Sin(idleT * 2.2f) + pulse * 0.9f;
            towerGlow.localScale = new Vector3(sz, 1f, sz);
            Color c = Pal.Get(Pal.TeamLight(Team));
            towerGlowMat.SetColor(ArtMaterials.BaseColor, new Color(c.r * a, c.g * a, c.b * a, 1f));
        }

        void PoseTower(float dt)
        {
            float pulse = 0f;
            if (atkT >= 0f)
            {
                atkT += dt;
                float u = Mathf.Clamp01(atkT / windup), r = Mathf.Clamp01((atkT - windup) / 0.3f);
                pulse = atkT < windup ? Smooth(u) * 0.35f : (1f - r) * 0.35f;
                if (hitT < 0f && ownT < 0f) SetFlash(Pal.Get(Pal.TeamLight(Team)) * (pulse * 1.6f));
                if (r >= 1f) { atkT = -1f; if (hitT < 0f && ownT < 0f) SetFlash(Color.black); }
            }
            bones[Models.TowerCrystal].localPosition = def.Pivots[Models.TowerCrystal] + new Vector3(0f, Mathf.Sin(idleT * 2f) * 0.08f, 0f);
            bones[Models.TowerCrystal].localRotation = Quaternion.Euler(0f, idleT * 45f, 0f);
            float s = 1f + pulse;
            bones[Models.TowerCrystal].localScale = new Vector3(s, s, s);
            TowerGlow(pulse);
            model.localPosition = new Vector3(Mathf.Sin(idleT * 60f) * 0.03f * recoil, 0f, 0f);
            // the old game's tower (View3DWorld.SyncTower): it rocks when hit, smokes under half health and also sparks under a quarter
            model.localRotation = Quaternion.Euler(Mathf.Sin(idleT * 18f) * 3f * recoil, 0f, Mathf.Cos(idleT * 14f) * 2f * recoil);
            if (def.Kind != RigKind.Tower || hp < 0f || hp >= 0.5f || !renderersOn) return;
            towerFxT -= dt;
            if (towerFxT > 0f) return;
            towerFxT = ArtKit.High ? 0.16f : 0.32f;
            var top = transform.position + new Vector3(0f, def.Height * 0.75f, 0f);
            Fx.Smoke(top, 1, new Color32(58, 54, 54, 150), 0.55f, 0.3f, 1.5f, 0.9f, 0.35f);
            if (hp < 0.25f) Fx.Sparks(top, 2, new Color32(255, 180, 80, 255), 0.08f, 2.5f, 0.35f, 1.2f, 0.3f);
        }
    }
}
