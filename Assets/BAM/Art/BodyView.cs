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
    sealed class BodyView : UnitView
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
        // what the player can actually see of it: an enemy hidden in a bush is still listed but must give nothing away
        internal bool Shown => renderersOn && vis > 0.01f && !dead;
        internal bool IsPlayer => marker == UnitMarker.Player;

        void OnEnable() { if (IsHero && !Heroes.Contains(this)) Heroes.Add(this); }
        void OnDisable() { Heroes.Remove(this); }

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
        float atkSpeed = 1f, atkTotal = 0.5f, castLen = 0.6f, hitA = -1f, deathFadeAt = 0.55f;
        // old Speltest's HeroMixamo layers: standing, the attack and the cast move the whole body; on the move only the upper body
        // (this mask) swings while the legs keep running, so a hero hitting on the run no longer slides on frozen legs
        AnimationLayerMixerPlayable layers;
        AnimationPlayableOutput animOut;
        bool layered; // the layers are in the graph only while a swing runs on the move (Rickard 2026-10-27: Bram floated on Home)
        AnimationMixerPlayable upper;
        AnimationClipPlayable upAtk, upCast;
        // the attack in Speltest's four phases (HeroAttack.cs, HeroMixamo.PoseMx): the coil, the slash, the hold on the blow, the recovery
        float atkRel, atkSlash, atkHold, atkRec;
        bool imported;
        HeldGear gear;            // weapons and shield in the hands (Weapons.cs), imported heroes only
        PreviewBreath breath;     // the menu hero's living idle on top of its clip (Preview only)
        // the menu hero's feet kept on the stage (Rickard 2026-10-27: Bram and Fenn floated above the ring on Home): each idle stands
        // the model at its own height over the root, so the lowest foot bone is held where it was in the bind pose, standing on the ground
        Transform instT, footL, footR, toeL, toeR;
        float footRef = float.NaN, groundOff;
        bool breathTried;
        float gearAct;            // how much the attack and cast clips play, for the gear's rest pose
        int shadowRends;          // rends[0..shadowRends) may cast shadows; the gear after them never does
        const int CIdle = 0, CRun = 1, CAttack = 2, CCast = 3, CHit = 4, CDeath = 5, CWalk = 6;
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
        float propBack = -1f;
        float hp = -1f, towerFxT, dustT;
        float channel = -1f, channelT, swirlT, sprintT;
        int sprintN; // the battle spells' looks (SpellFx): recall channel progress, sprint trail
        bool sprint;   // last health fraction (-1 = never set), the tower's smoke timer
        bool dead;
        float deathT = -1f, spawnT = -1f, vis = 1f, ghostPulse;
        int lastQuality = -1;
        bool fading, renderersOn = true;
        Color flashColor = Color.white;
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
            if (d.RigModel != null) AddWolfRig(d);
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

        AnimationClipPlayable wolfWalk;

        // Fenn's wolf as old Speltest draws it (View3DWolf.cs): Rickard's rigged Meshy wolf on the body bone, turned so hips to head
        // points forward, about 2 tiles nose to tail, see-through turquoise like the rest of the ghost, its walk played as it runs
        void AddWolfRig(RigDef d)
        {
            Transform holder = bones[Models.WolfBody];
            var go = Instantiate(d.RigModel, holder, false);
            go.name = "SpectralWolf";
            Transform t = go.transform;
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            Transform hips = null, head = null;
            foreach (Transform tr in go.GetComponentsInChildren<Transform>(true)) { if (tr.name == "Hips") hips = tr; else if (tr.name == "head") head = tr; }
            if (hips != null && head != null)
            {
                Vector3 dd = t.InverseTransformPoint(head.position) - t.InverseTransformPoint(hips.position);
                if (dd.x * dd.x + dd.z * dd.z > 1e-8f) t.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(dd.x, dd.z) * Mathf.Rad2Deg, 0f);
            }
            // size and ground from the bones (a skinned mesh's own bounds are not reliable before it has drawn)
            Bounds kb = default; bool any = false;
            foreach (Transform tr in go.GetComponentsInChildren<Transform>(true))
                if (tr != t && tr.GetComponent<Renderer>() == null) { Vector3 p = holder.InverseTransformPoint(tr.position); if (!any) { kb = new Bounds(p, Vector3.zero); any = true; } else kb.Encapsulate(p); }
            if (any && kb.size.z > 1e-4f)
            {
                float s = 2.0f / (kb.size.z * 1.12f);
                t.localScale = new Vector3(s, s, s);
                t.localPosition = new Vector3(-kb.center.x * s, -d.Pivots[Models.WolfBody].y - kb.min.y * s + 0.03f, -kb.center.z * s);
            }
            Texture2D tex = Resources.Load<Texture2D>("BAMArt/Wolf/Wolf_Tex");
            Material o = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            if (tex != null) { o.SetTexture(ArtMaterials.BaseMap, tex); o.mainTexture = tex; }
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            var r2 = new Renderer[rends.Length + rs.Length];
            var o2 = new Material[rends.Length + rs.Length][];
            var f2 = new Material[rends.Length + rs.Length][];
            for (int i = 0; i < rends.Length; i++) { r2[i] = rends[i]; o2[i] = opaqueSets[i]; f2[i] = fadeSets[i]; }
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                var ms = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int j = 0; j < ms.Length; j++) ms[j] = o;
                r.sharedMaterials = ms;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
                r2[rends.Length + i] = r; o2[rends.Length + i] = ms; f2[rends.Length + i] = ms;
            }
            rends = r2; opaqueSets = o2; fadeSets = f2;
            ghostMats = new[] { ghostMats.Length > 0 ? ghostMats[0] : opaque, o };
            ghostTint = new Color(0.55f, 1f, 0.95f, 1f);

            AnimationClip clip = null;
            foreach (AnimationClip c in Resources.LoadAll<AnimationClip>("BAMArt/Wolf/Wolf")) if (!c.name.StartsWith("__preview__")) { clip = c; break; }
            if (clip == null) return;
            Animator an = go.GetComponent<Animator>();
            if (an == null) an = go.AddComponent<Animator>();
            an.runtimeAnimatorController = null;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("BAM spectral wolf");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", an);
            wolfWalk = AnimationClipPlayable.Create(graph, clip);
            wolfWalk.SetSpeed(0.0);
            output.SetSourcePlayable(wolfWalk);
            graph.Play();
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
            float h = bb.size.y > 0.01f ? bb.size.y : 1f;
            float k = art.Height / h;
            it.localScale = it.localScale * k;
            it.localPosition = new Vector3(0f, (transform.position.y - bb.min.y) * k, 0f);

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
                if (rends[i] is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true; // attacks must always show, even at the screen edge
            }
            mats = all.ToArray();
            fadeMats = fades.ToArray();

            anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            instT = it;
            if (anim.isHuman)
            {
                footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot); footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                toeL = anim.GetBoneTransform(HumanBodyBones.LeftToes); toeR = anim.GetBoneTransform(HumanBodyBones.RightToes);
                float f = LowestFoot();
                if (!float.IsNaN(f)) footRef = f - transform.position.y; // the bind pose, its mesh's sole on the ground
            }
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (def.Gear != null) AddGear(art);
            AnimationClip idle = art.Idle;
            if (idle == null) { Debug.LogWarning("[BAM.Art] " + art.name + " has no idle clip; the model stands still"); return; }
            var src2 = new[] { art.Idle, art.Run, art.Attack, art.Cast, art.Hit, art.Death, art.Walk != null ? art.Walk : art.Run };
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            hipsH = hips != null ? Mathf.Max(0.05f, anim.transform.InverseTransformPoint(hips.position).y) : 0f;
            MeasureGait(src2[CWalk], src2[CRun]);
            graph = PlayableGraph.Create("BAM " + art.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, src2.Length);
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
            // the upper-body copies of the attack and the cast, for swinging on the move
            upper = AnimationMixerPlayable.Create(graph, 2);
            upAtk = AnimationClipPlayable.Create(graph, src2[CAttack] != null ? src2[CAttack] : idle);
            upCast = AnimationClipPlayable.Create(graph, src2[CCast] != null ? src2[CCast] : idle);
            graph.Connect(upAtk, 0, upper, 0);
            graph.Connect(upCast, 0, upper, 1);
            graph.Connect(upper, 0, layers, 1);
            layers.SetLayerMaskFromAvatarMask(1, UpperMask());
            layers.SetInputWeight(1, 0f);
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

        // the weapons go on in the bind pose, before the clips play (HeldGear), and join the body's renderers and materials
        void AddGear(HeroArt art)
        {
            gear = HeldGear.Build(def.Gear, anim, art.Height * transform.lossyScale.y);
            if (gear == null) return;
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

            bar = new HealthBar(transform, d.BarWidth, d.Height + 0.28f, d.Kind == RigKind.Humanoid && d.BarWidth > 0.9f);
            status = new StatusFx(transform, d.Radius, d.Height);
            ResetState();
        }

        /// <summary>Fresh state for a new life out of the pool.</summary>
        public void ResetState()
        {
            moving = false; speed01 = 0f; moveW = 0f; hasLast = false; gSpd = 0f; atkT = -1f; castT = -1f; hitT = -1f; hitA = -1f; recoil = 0f; propBack = -1f; hp = -1f; channel = -1f; sprint = false;
            dead = false; deathT = -1f; spawnT = -1f; vis = 1f;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.identity;
            if (def.ThrowsProp) bones[Hb.Prop].localScale = Vector3.one;
            if (gear != null) gear.Reset();
            bar.Reset();
            bar.SetLevel(1);
            status.Visible = false; // silently: a view parked while shielded must not pop its shield where it last stood (ApplyLook shows it again)
            status.Set(StatusFlags.None);
            if (marks != null) marks.Reset();
            SetMarker(Team == Teams.Player ? UnitMarker.Ally : UnitMarker.Enemy);
            SetFlash(Color.black);
            fading = true; // force ApplyLook to set the material
            ApplyLook();
        }

        // ---------------------------------------------------------------- UnitView

        public override void SetMoving(bool isMoving, float speed) { moving = isMoving; speed01 = Mathf.Clamp(speed, 0f, 2f); }

        public override void PlayAttack(float w)
        {
            windup = Mathf.Max(0.05f, w);
            atkT = 0f;
            if (clips != null)
            {
                // the blow lands on the clip's release frame exactly when the rules' windup runs out; the coil goes quickly to 70 % of
                // the way and waits there (anticipation), the slash plays the last 30 %, the clip stands still on the blow for this kind
                // of hero's hit-stop (impact), then plays out (recovery), never dragging on
                float len = clipLen[CAttack];
                atkRel = def.Art.AttackRelease * len;
                atkSpeed = Mathf.Clamp(atkRel / windup, 0.6f, 4f);
                atkSlash = Mathf.Min(0.12f, windup * 0.35f);
                atkHold = Phased(HeroIndex) ? AttackHold(HeroIndex) : 0f;
                atkRec = Mathf.Min((len - atkRel) / atkSpeed, 0.45f);
                atkTotal = Phased(HeroIndex) ? windup + atkHold + atkRec : Mathf.Min(len / atkSpeed, windup + 0.45f);
            }
            if (gear != null) gear.Throw(windup);
        }

        public override void PlayCast()
        {
            castT = 0f;
            if (IsHero && !dead && vis > 0.01f) Fx.HeroCast(HeroIndex, transform.position); // never gives a hider away
            if (clips != null) { clips[CCast].SetTime(0.0); clips[CCast].SetTime(0.0); upCast.SetTime(0.0); upCast.SetTime(0.0); }
            if (gear != null) gear.Throw(castLen * 0.35f);
        }

        public override void PlayHit(bool big)
        {
            hitT = 0f;
            hitA = 0f;
            status.Ripple();
            if (clips != null) { clips[CHit].SetTime(0.0); clips[CHit].SetTime(0.0); }
            hitLen = big ? 0.25f : 0.14f;
            recoil = big ? 1f : 0.6f;
            flashColor = big ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.45f, 0.4f); // a short red blink on every hit (MLBB analysis 7)
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

        public override void SetHealth(float hp01, float shield01) { hp = hp01; bar.Set(hp01, shield01); }
        public override void SetLevel(int level) => bar.SetLevel(level);
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
        public override void SetDeathmark(float left) { if (left >= 0f || marks != null) Marks().SetDeathmark(left); }
        public override void SetChainSpark(bool on) { if (on || marks != null) Marks().SetChainSpark(on); }
        public override void SetChainCooldown(float frac) { if (frac >= 0f || marks != null) Marks().SetChainCooldown(frac); }

        HeroMarks marks; // Tikta's Deathmark and Blast's chain marks, made the first time one shows
        HeroMarks Marks() => marks ?? (marks = new HeroMarks(transform, def.Height, def.Radius));

        public override void SetMarker(UnitMarker m)
        {
            marker = m;
            decalF.sharedMesh = Shapes.Decal(m);
            byte fill = m == UnitMarker.Player ? Pal.BarPlayer : m == UnitMarker.Enemy ? Pal.BarEnemy : m == UnitMarker.Ally ? Pal.BarAlly : (Team == Teams.Player ? Pal.BarAlly : Pal.BarEnemy);
            bar.SetColors(fill, Pal.TeamDark(Team));
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

        float DeathAlpha => deathT < 0f ? 1f : Mathf.Clamp01(1f - (deathT - deathFadeAt) / 0.45f);

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
            bar.Visible = vis > 0.01f && !dead && !Preview;
            status.Visible = vis > 0.01f && !dead && !Preview;
            if (!on) return;
            if (def.Ghost)
            {
                float ga = a * (0.62f + ghostPulse * 0.18f);
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
                Color c = new Color(1f, 1f, 1f, a);
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
            for (int i = 0; i < mats.Length; i++) mats[i].SetColor(ArtMaterials.EmissionColor, c);
        }

        // ---------------------------------------------------------------- animation

        void LateUpdate()
        {
            if (def == null) return; // a script reload in Play mode clears the view's state: stay still instead of throwing every frame
            float dt = Time.deltaTime;
            if (lastQuality != ArtKit.QualityVersion)
            {
                lastQuality = ArtKit.QualityVersion;
                if (!fading && !def.Ghost)
                    for (int i = 0; i < shadowRends; i++) rends[i].shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            idleT += dt;
            moveW = Mathf.MoveTowards(moveW, moving && !dead ? 1f : 0f, dt * 6f);
            if (moving) phase += dt * (7f + 5f * speed01);

            if (def.Ghost)
            {
                ghostPulse = Mathf.Sin(idleT * 5f) * 0.5f + 0.5f;
                if (renderersOn && !dead) ApplyLook();
            }

            if (hitT >= 0f)
            {
                hitT += dt;
                float k = 1f - hitT / hitLen;
                if (k <= 0f) { hitT = -1f; SetFlash(Color.black); }
                else SetFlash(flashColor * (k * 0.9f));
            }
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 5f);

            if (imported) AnimateImported(dt);
            if (Preview && imported && clips != null && !dead)
            {
                if (!breathTried) { breathTried = true; breath = PreviewBreath.For(anim, HeroIndex); }
                breath?.Apply(Time.unscaledTime, 1f - Mathf.Clamp01(gearAct));
                GroundFeet(dt);
            }
            if (gear != null) gear.Tick(dt, moveW, gearAct, dead);
            if (dead) AnimateDeath(dt);
            else
            {
                if (!imported)
                switch (def.Kind)
                {
                    case RigKind.Humanoid: PoseHumanoid(dt); break;
                    case RigKind.Wolf: PoseWolf(dt); break;
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
            // Fenn's wolf leaves a ghostly trail of wisps as it runs (MLBB analysis 4, Fenn)
            // as old Speltest's SpectralWolf: turquoise wisps burn off its back and trail behind (more on the run), white-turquoise sparks rise
            if (def.Kind == RigKind.Wolf && !dead && renderersOn && vis >= 0.99f)
            {
                dustT -= dt;
                if (dustT <= 0f)
                {
                    float rate = 22f + 30f * moveW;
                    dustT = (ArtKit.High ? 1f : 2f) / rate;
                    Vector3 f = transform.forward, p = transform.position + f * Random.Range(-0.5f, 0.3f) + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.35f, 1.1f), Random.Range(-0.2f, 0.2f));
                    Fx.Wisps(p, 1, new Color32(89, 255, 235, 200), Random.Range(0.3f, 0.5f), 0.3f, Random.Range(0.45f, 0.9f), 0.7f, 0.05f);
                    if (Random.value < 10f / rate) Fx.Sparks(transform.position + new Vector3(0f, Random.Range(0.2f, 0.9f), 0f), 2, new Color32(220, 255, 250, 255), 0.07f, 0.5f, 0.7f, 1.3f, 0.35f);
                }
            }

            if (marks != null && marks.Any)
            {
                marks.Visible = renderersOn && vis > 0.01f && !dead;
                marks.Tick(dt, idleT, ArtDriver.CamRot);
            }
            if (renderersOn)
            {
                bar.Tick(dt, ArtDriver.CamRot);
                status.Tick(dt, idleT);
            }
        }

        void AnimateImported(float dt)
        {
            if (clips == null) return;
            float wAtk = 0f, wCast = 0f, wHit = 0f, wDeath = 0f;
            if (dead)
            {
                wDeath = deathT < 0f ? 1f : Mathf.Clamp01(deathT / 0.15f);
            }
            else
            {
                if (atkT >= 0f)
                {
                    atkT += dt;
                    wAtk = Mathf.Min(Mathf.Clamp01(atkT / 0.06f), Mathf.Clamp01((atkTotal - atkT) / 0.15f));
                    double at = AttackTime(atkT);
                    clips[CAttack].SetTime(at); upAtk.SetTime(at);
                    if (atkT >= atkTotal) atkT = -1f;
                }
                if (castT >= 0f)
                {
                    castT += dt;
                    wCast = Mathf.Min(Mathf.Clamp01(castT / 0.08f), Mathf.Clamp01((castLen - castT) / 0.15f));
                    double ct = castT * 1.2f;
                    clips[CCast].SetTime(ct); upCast.SetTime(ct);
                    if (castT >= castLen) castT = -1f;
                }
                if (hitA >= 0f)
                {
                    hitA += dt;
                    wHit = Mathf.Sin(Mathf.Clamp01(hitA / 0.3f) * Mathf.PI) * 0.6f;
                    if (hitA >= 0.3f) hitA = -1f;
                }
            }
            gearAct = wAtk + wCast;
            // on the move the swing goes to the upper-body layer and the legs keep their stride
            float act = Mathf.Min(1f, wAtk + wCast), upW = dead || !Phased(HeroIndex) ? 0f : act * moveW;
            if (act > 1e-4f) { upper.SetInputWeight(0, wAtk / (wAtk + wCast)); upper.SetInputWeight(1, wCast / (wAtk + wCast)); }
            SetLayered(upW > 1e-4f);
            if (layered) layers.SetInputWeight(1, upW);
            if (upW > 0f) { wAtk *= 1f - moveW; wCast *= 1f - moveW; }
            float over = wAtk + wCast + wHit + wDeath;
            if (over > 1f) { wAtk /= over; wCast /= over; wHit /= over; wDeath /= over; over = 1f; }
            float rest = 1f - over;
            float runK = Stride(dt);
            mixer.SetInputWeight(CIdle, rest * (1f - moveW));
            mixer.SetInputWeight(CWalk, rest * moveW * (1f - runK));
            mixer.SetInputWeight(CRun, rest * moveW * runK);
            mixer.SetInputWeight(CAttack, wAtk);
            mixer.SetInputWeight(CCast, wCast);
            mixer.SetInputWeight(CHit, wHit);
            mixer.SetInputWeight(CDeath, wDeath);
            // idle always loops here, even when a clip was imported without Loop Time (it would play once and freeze)
            Wrap(CIdle);
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
            if (hasLast && dt > 1e-4f)
            {
                Vector3 d = p - lastPos; d.y = 0f;
                if (d.magnitude < 1f) raw = d.magnitude / dt; // a respawn or a pull jumps: not a step
            }
            lastPos = p; hasLast = true;
            gSpd += (raw - gSpd) * Mathf.Min(1f, dt * (raw < gSpd ? 22f : 10f)); // Speltest's CombatFeel.GroundSpeed: stops faster than it starts
            float hipsW = hipsH * anim.transform.lossyScale.y;
            float rel = hipsW > 1e-4f ? gSpd / hipsW : 0f;
            // the menu's preview walks on the spot: a steady jog for its speed
            if (moving && rel < 0.3f) rel = 3f * Mathf.Max(0.5f, speed01);
            float wLen = clipLen[CWalk], rLen = clipLen[CRun];
            float top = Mathf.Max(walkSpd + 0.5f, 0.6f * runSpd);
            float runK = Mathf.Clamp01((rel - walkSpd) / (top - walkSpd));
            float stride = Mathf.Lerp(walkSpd * wLen, runSpd * rLen, runK), len = Mathf.Lerp(wLen, rLen, runK);
            float rate = Mathf.Clamp(rel / Mathf.Max(0.05f, stride), 0.6f / len, 1.8f / len);
            if (moveW > 0.01f) gaitPh = Mathf.Repeat(gaitPh + dt * rate, 1f);
            clips[CWalk].SetTime(gaitPh * wLen);
            clips[CRun].SetTime(gaitPh * rLen);
            return runK;
        }

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
        // not make it bob); only on the menu stages
        void GroundFeet(float dt)
        {
            if (float.IsNaN(footRef) || instT == null) return;
            float low = LowestFoot();
            if (float.IsNaN(low)) return;
            float err = low - transform.position.y - footRef; // world units above where the foot should be
            float step = -err * Mathf.Min(1f, Mathf.Max(dt, 1f / 60f) * 8f);
            groundOff += step;
            Transform par = instT.parent;
            float k = par != null ? Mathf.Max(1e-4f, par.lossyScale.y) : 1f;
            instT.localPosition += new Vector3(0f, step / k, 0f);
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
            if (!Phased(HeroIndex)) return Mathf.Min(clipLen[CAttack], t * atkSpeed); // as before 2026-10-27: the clip at its own pace
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

        void AnimateDeath(float dt)
        {
            if (deathT < 0f) return;
            float before = deathT;
            deathT += dt;
            float t = Mathf.Clamp01(deathT / 0.45f);
            if (imported) { }
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

        void PoseWolf(float dt)
        {
            if (wolfWalk.IsValid()) wolfWalk.SetSpeed(moveW * (1.1f + speed01 * 0.3f)); // the walk runs as it moves and holds when it stands
            float s = Mathf.Sin(phase * 1.2f) * 40f * moveW;
            float idle = Mathf.Sin(idleT * 3f + seed);
            float lunge = 0f, headX = idle * 3f * (1f - moveW);
            if (atkT >= 0f)
            {
                atkT += dt;
                float u = Mathf.Clamp01(atkT / windup), r = Mathf.Clamp01((atkT - windup) / 0.25f);
                lunge = atkT < windup ? -0.08f * Smooth(u) : Mathf.Lerp(0.18f, 0f, Smooth(r));
                headX = atkT < windup ? -20f * u : Mathf.Lerp(25f, 0f, r);
                if (r >= 1f) atkT = -1f;
            }
            if (castT >= 0f) { castT += dt; headX = -35f * Mathf.Sin(Mathf.Clamp01(castT / 0.5f) * Mathf.PI); if (castT > 0.5f) castT = -1f; }
            bones[Models.WolfBody].localPosition = def.Pivots[Models.WolfBody] + new Vector3(0f, Mathf.Abs(Mathf.Cos(phase * 1.2f)) * 0.04f * moveW + 0.04f, lunge);
            bones[Models.WolfBody].localRotation = Quaternion.Euler(Mathf.Sin(phase * 1.2f) * 4f * moveW - recoil * 10f, 0f, 0f);
            bones[Models.WolfHead].localRotation = Quaternion.Euler(headX, 0f, 0f);
            bones[Models.WolfFL].localRotation = Quaternion.Euler(-s, 0f, 0f);
            bones[Models.WolfBR].localRotation = Quaternion.Euler(-s, 0f, 0f);
            bones[Models.WolfFR].localRotation = Quaternion.Euler(s, 0f, 0f);
            bones[Models.WolfBL].localRotation = Quaternion.Euler(s, 0f, 0f);
            bones[Models.WolfTail].localRotation = Quaternion.Euler(0f, Mathf.Sin(idleT * 9f) * 25f, 0f);
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

        void PoseTower(float dt)
        {
            float pulse = 0f;
            if (atkT >= 0f)
            {
                atkT += dt;
                float u = Mathf.Clamp01(atkT / windup), r = Mathf.Clamp01((atkT - windup) / 0.3f);
                pulse = atkT < windup ? Smooth(u) * 0.35f : (1f - r) * 0.35f;
                if (hitT < 0f) SetFlash(Pal.Get(Pal.TeamLight(Team)) * (pulse * 1.6f));
                if (r >= 1f) { atkT = -1f; if (hitT < 0f) SetFlash(Color.black); }
            }
            bones[Models.TowerCrystal].localPosition = def.Pivots[Models.TowerCrystal] + new Vector3(0f, Mathf.Sin(idleT * 2f) * 0.08f, 0f);
            bones[Models.TowerCrystal].localRotation = Quaternion.Euler(0f, idleT * 45f, 0f);
            float s = 1f + pulse;
            bones[Models.TowerCrystal].localScale = new Vector3(s, s, s);
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
