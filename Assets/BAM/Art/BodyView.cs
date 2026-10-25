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

        RigDef def;
        Transform model;
        Transform[] bones;
        Renderer[] rends;
        Material[][] opaqueSets, fadeSets;
        Material[] mats;          // every material instance of this view, for the hit flash
        Material[] fadeMats;      // the fade instances, for the alpha
        Material opaque;          // code models: the single instance (the ghost wolf tints it)

        // imported models
        Animator anim;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        float[] clipLen;
        float atkSpeed = 1f, atkTotal = 0.5f, castLen = 0.6f, hitA = -1f, deathFadeAt = 0.55f;
        bool imported;
        const int CIdle = 0, CRun = 1, CAttack = 2, CCast = 3, CHit = 4, CDeath = 5;
        Transform decalT;
        MeshFilter decalF;
        MeshRenderer decalR;
        HealthBar bar;
        StatusFx status;

        bool moving;
        float speed01, moveW, phase, idleT, seed;
        float atkT = -1f, windup = 0.3f, castT = -1f, hitT = -1f, hitLen = 0.15f, recoil;
        float propBack = -1f;
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
            rends = inst.GetComponentsInChildren<Renderer>(true);
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
                    Texture tex = src[j] != null ? src[j].mainTexture : null;
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
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AnimationClip idle = art.Idle;
            if (idle == null) { Debug.LogWarning("[BAM.Art] " + art.name + " has no idle clip; the model stands still"); return; }
            var src2 = new[] { art.Idle, art.Run, art.Attack, art.Cast, art.Hit, art.Death };
            graph = PlayableGraph.Create("BAM " + art.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Anim", anim);
            mixer = AnimationMixerPlayable.Create(graph, src2.Length);
            output.SetSourcePlayable(mixer);
            clips = new AnimationClipPlayable[src2.Length];
            clipLen = new float[src2.Length];
            for (int c = 0; c < src2.Length; c++)
            {
                AnimationClip clip = src2[c] != null ? src2[c] : idle;
                clips[c] = AnimationClipPlayable.Create(graph, clip);
                clips[c].SetApplyFootIK(false);
                clipLen[c] = Mathf.Max(0.1f, clip.length);
                graph.Connect(clips[c], 0, mixer, c);
                mixer.SetInputWeight(c, c == CIdle ? 1f : 0f);
            }
            // one-shot clips must hold their last frame, not loop back
            clips[CDeath].SetDuration(clipLen[CDeath]);
            castLen = Mathf.Min(0.9f, clipLen[CCast] / 1.2f);
            deathFadeAt = Mathf.Clamp(clipLen[CDeath] * 0.9f, 0.6f, 2f);
            graph.Play();
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
            moving = false; speed01 = 0f; moveW = 0f; atkT = -1f; castT = -1f; hitT = -1f; hitA = -1f; recoil = 0f; propBack = -1f;
            dead = false; deathT = -1f; spawnT = -1f; vis = 1f;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.identity;
            if (def.ThrowsProp) bones[Hb.Prop].localScale = Vector3.one;
            bar.Reset();
            bar.SetLevel(1);
            status.Set(StatusFlags.None);
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
                // line the clip's release up with the windup, and never let the follow-through drag on
                float len = clipLen[CAttack];
                atkSpeed = Mathf.Clamp(def.Art.AttackRelease * len / windup, 0.6f, 4f);
                atkTotal = Mathf.Min(len / atkSpeed, windup + 0.45f);
                clips[CAttack].SetTime(0.0);
                clips[CAttack].SetTime(0.0);
                clips[CAttack].SetSpeed(atkSpeed);
            }
        }

        public override void PlayCast()
        {
            castT = 0f;
            if (clips != null) { clips[CCast].SetTime(0.0); clips[CCast].SetTime(0.0); clips[CCast].SetSpeed(1.2f); }
        }

        public override void PlayHit(bool big)
        {
            hitT = 0f;
            hitA = 0f;
            if (clips != null) { clips[CHit].SetTime(0.0); clips[CHit].SetTime(0.0); }
            hitLen = big ? 0.25f : 0.14f;
            recoil = big ? 1f : 0.6f;
            flashColor = big ? new Color(1f, 0.55f, 0.35f) : Color.white;
        }

        public override void SetDead(bool d)
        {
            if (d == dead) return;
            dead = d;
            if (d)
            {
                deathT = 0f; atkT = -1f; castT = -1f;
                if (clips != null) { clips[CDeath].SetTime(0.0); clips[CDeath].SetTime(0.0); clips[CDeath].SetSpeed(1.0); }
            }
            else
            {
                deathT = -1f;
                spawnT = 0f;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                bar.Reset();
            }
            ApplyLook();
        }

        public override void SetHealth(float hp01, float shield01) => bar.Set(hp01, shield01);
        public override void SetLevel(int level) => bar.SetLevel(level);
        public override void SetStatus(StatusFlags flags) => status.Set(flags);

        public override void SetVisibility(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (alpha == vis) return;
            vis = alpha;
            ApplyLook();
        }

        public override void SetMarker(UnitMarker m)
        {
            marker = m;
            decalF.sharedMesh = Shapes.Decal(m);
            byte fill = m == UnitMarker.Player ? Pal.BarPlayer : m == UnitMarker.Enemy ? Pal.BarEnemy : m == UnitMarker.Ally ? Pal.BarAlly : (Team == Teams.Player ? Pal.BarAlly : Pal.BarEnemy);
            bar.SetColors(fill, Pal.TeamDark(Team));
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
            decalR.enabled = on && !dead;
            bar.Visible = vis > 0.01f && !dead;
            status.Visible = vis > 0.01f && !dead;
            if (!on) return;
            if (def.Ghost)
            {
                opaque.SetColor(ArtMaterials.BaseColor, new Color(1f, 1f, 1f, a * (0.62f + ghostPulse * 0.18f)));
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
                for (int i = 0; i < rends.Length; i++) { rends[i].sharedMaterials = opaqueSets[i]; rends[i].shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; }
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
            float dt = Time.deltaTime;
            if (lastQuality != ArtKit.QualityVersion)
            {
                lastQuality = ArtKit.QualityVersion;
                if (!fading && !def.Ghost)
                    for (int i = 0; i < rends.Length; i++) rends[i].shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
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
            if (dead) AnimateDeath(dt);
            else
            {
                if (!imported)
                switch (def.Kind)
                {
                    case RigKind.Humanoid: PoseHumanoid(dt); break;
                    case RigKind.Wolf: PoseWolf(dt); break;
                    case RigKind.Cart: PoseCart(dt); break;
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
                    if (atkT >= atkTotal) atkT = -1f;
                }
                if (castT >= 0f)
                {
                    castT += dt;
                    wCast = Mathf.Min(Mathf.Clamp01(castT / 0.08f), Mathf.Clamp01((castLen - castT) / 0.15f));
                    if (castT >= castLen) castT = -1f;
                }
                if (hitA >= 0f)
                {
                    hitA += dt;
                    wHit = Mathf.Sin(Mathf.Clamp01(hitA / 0.3f) * Mathf.PI) * 0.6f;
                    if (hitA >= 0.3f) hitA = -1f;
                }
            }
            float over = wAtk + wCast + wHit + wDeath;
            if (over > 1f) { wAtk /= over; wCast /= over; wHit /= over; wDeath /= over; over = 1f; }
            float rest = 1f - over;
            mixer.SetInputWeight(CIdle, rest * (1f - moveW));
            mixer.SetInputWeight(CRun, rest * moveW);
            mixer.SetInputWeight(CAttack, wAtk);
            mixer.SetInputWeight(CCast, wCast);
            mixer.SetInputWeight(CHit, wHit);
            mixer.SetInputWeight(CDeath, wDeath);
            clips[CRun].SetSpeed(0.8f + 0.4f * speed01);
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
        }
    }
}
