using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Cloth and hair that swing after the body (tester idea 2026-11-06, thread "Add cloth and hair simulation to heroes"):
    /// Fenn's cape and tabard, Tyra's ponytail and front locks. The Tripo/Meshy bodies are one shell on a plain Mixamo skeleton, so there are
    /// no cape or hair bones to hang a cloth package on, and re-exporting the FBX in Blender breaks the clips. Instead, once
    /// per model, the cloth's vertices are found from the bind pose (below the hips and clear of the legs; behind the skull),
    /// a few spring bones are added under the hips or the head, and a share of those vertices' weights moves onto them. Each
    /// spring bone is a damped pendulum: it lags when the body turns or starts, overshoots a little and settles, flares out
    /// when the hero spins and trails while running. View only: the rules and online never read it. A handful of bones per
    /// hero and no physics engine, so it costs next to nothing on a phone. AnimFix.Cloth turns it off (BAM > Art > Animation Check).
    /// The model must be imported with Read/Write on (HeroArtImport.ClothModel); without it the hero simply has no sway.
    /// </summary>
    sealed class ClothSway
    {
        enum Kind { Skirt, Tail, HipTail, BackFlaps, Fringe, Plume }

        /// <summary>One cloth part of one hero. Distances are fractions of the model's height in its bind pose.</summary>
        sealed class Spec
        {
            public Kind Kind;
            public int Sectors = 6;          // Skirt: spring bones round the hips
            public float Clear0, Clear1;     // Skirt: distance from the leg bones where the sway starts and is full
            public float Drop = 0.12f;       // Skirt: how far below the hips the sway reaches full
            public float MaxBelow;           // Skirt: only cloth at most this far below the hips (0 = no limit); keeps the calves still
                                             // under a short tunic
            public float MaxSide;            // Skirt: only cloth this close to the body's middle line, to the sides (0 = all round);
                                             // for loincloth panels front and back on a hero whose thighs are thick
            public float Back0, Back1;       // Tail: distance behind the head bone where the sway starts and is full
            public float PivotUp;            // Tail: the tie point, this far above the head bone (on the Back0 plane)
            public float Above = 0.04f;      // HipTail: only what is at most this far above the hips (not the back)
            public float Front = 0.10f;      // Fringe: only hair this far in front of the head bone (the ears sit behind it)
            public float Side0 = 0.040f, Side1 = 0.052f; // Fringe: out to the side of the head's middle line where the locks start and
                                             // are full (the face and temples are inside Side0)
            public float Top = 0.11f, TopDrop = 0.03f; // Fringe: the locks hang free from Top above the head bone, full TopDrop below it
            public float PivotFwd = 0.13f, PivotSide = 0.04f; // Fringe: the tie point of each lock (with PivotUp), at the hairline
            public float Hz = 2.4f;          // spring frequency
            public float Damping = 0.4f;     // share of critical damping (lower = more overshoot)
            public float MaxAngle = 28f;     // degrees the part may swing from its rest
            public float Drag = 0.05f;       // seconds of the hips' or head's velocity it trails by
            public float Flare;              // Skirt: outward swing at a full spin (share of its length)
            public float Inward = 0.12f;     // Skirt: how far a part may swing in towards the legs (share of its length)
            public float Top0, Top1;         // BackFlaps: height above the hips (share of height) where the flaps hang free (Top1 = full)
            public float Low;                // BackFlaps: and above this (the seat below stays put)
            public float Front0, Front1;     // Skirt: when set, the sway share comes from how far in front of the hips the cloth is
                                             // (a panel lying over the thighs), not from its distance to the legs
            public float Flutter;            // a flap on the run: it waves to the sides and out with the stride (share of its length at
                                             // full run), so a panel that can't swing back into the legs still moves while running
            public float SideFade;           // Skirt: a soft edge from MaxSide - SideFade to MaxSide instead of a hard cut
            public bool Single;              // BackFlaps, Skirt: one spring for the whole flap (long hair, one piece), not one per side
            public bool FrontOnly;           // Skirt: only cloth in front of the hips (a single front panel; the seat behind stays put)
            public float Kick = 1f;          // how hard the hero's own moves push it (secondary action, Kick); 0 = never
            public bool WithSpine;           // HipTail: the Spine bone counts as the hips too (V2 Ragn: Mixamo put his tail ~2/3 on Hips,
                                             // ~1/4 on Spine; with Hips alone only 35 of ~126 tail vertices passed the 0.8 share test)
            public bool WithHead;            // BackFlaps: the neck and head count as the torso too (V2 Nässla: her hanging hair is
                                             // weighted to the shoulders, Spine2, Neck and Head together)
            public float Up0, Up1;           // Plume: height above the head bone where the sway starts and is full (what rises off the
                                             // helmet's crown); Back0/Back1 do the same behind the head (what trails down the back)
            public float Ahead = 0.03f;      // Plume: nothing more than this far in front of the head bone sways (a hood's peak, a crown)
            public float PivotBack;          // Plume: the tie point, this far behind the head bone (with PivotUp above it)
            public float Wind;               // a slow wave from side to side all the time, at rest too (share of its length), so it
                                             // moves on its own (Fenn's plume, Rickard 2026-11-18: "animera tofsen")
        }

        /// <summary>The 2026-11-13 remakes (HeroArt.V2, Art/ModelsV2): only cloth and hair that are part of the body shell; capes,
        /// scarves and pelts are parts of their own there (Weapons.MakeV2, WornDef.Sway). Measured on the V2 FBX files (weights and
        /// heights as shares of the height, bam-work/art/remake/&lt;hero&gt;/notes.md).</summary>
        static Spec[] SpecsForV2(int hero)
        {
            switch (hero)
            {
                case 2: // Tyra: the ponytail behind her head, as on her old body (its lower part also carries some right-arm weight in the
                        // V2 rig, so only what sits on the head and neck swings)
                    return new[] { new Spec { Kind = Kind.Tail, Back0 = 0.04f, Back1 = 0.085f, PivotUp = 0.12f, Hz = 2.6f, Damping = 0.3f, MaxAngle = 30f, Drag = 0.06f } };
                case 3: // Moa (Rickard 2026-11-18 on Home: "gör håret rörligt ... gärna hennes kläder om det är lätt"), measured on
                        // ModelsV2/Moa/Moa.fbx in its bind pose (hips 0.46 of her height, bam-work/hero-review-2026-11-16/moa-sway):
                        // her white ponytail, from the back of her head down to her shoulder blades, 0.09-0.115 behind, one spring that
                        // lifts it out from her back and never in (as Nässla's); about 90 vertices
                    return new[]
                    {
                        new Spec { Kind = Kind.BackFlaps, Single = true, WithHead = true, Top0 = 0.50f, Top1 = 0.40f, Low = 0.10f, Back0 = 0.09f, Back1 = 0.115f, MaxSide = 0.09f, Hz = 2.2f, Damping = 0.4f, MaxAngle = 16f, Drag = 0.05f, Flare = 0.15f, Inward = 0f, Kick = 0.8f },
                        // the lower half of the white apron in front of her thighs (0.04-0.06 in front of the hips, 0.08 to the sides with a
                        // soft edge): ONE spring tied at the belt, a small swing that never goes back into her legs. Her old body's apron was
                        // redone many times (12-13 Nov), so this is its own line: delete it alone if it looks wrong
                        new Spec { Kind = Kind.Skirt, Single = true, FrontOnly = true, Front0 = 0.04f, Front1 = 0.06f, MaxSide = 0.08f, SideFade = 0.02f, Drop = 0.10f, MaxBelow = 0.30f, Hz = 2.4f, Damping = 0.5f, MaxAngle = 10f, Drag = 0.04f, Flare = 0.08f, Inward = 0f, Kick = 0.6f },
                    };
                case 11: // Ragn: the wolf tail behind the hips, 0.36-0.50 of his height (the hips at about 0.42, so it reaches 0.08 above
                         // them), out to 0.30 behind; on Hips and Spine together
                    return new[] { new Spec { Kind = Kind.HipTail, Back0 = 0.12f, Back1 = 0.20f, Above = 0.09f, WithSpine = true, Hz = 2.2f, Damping = 0.38f, MaxAngle = 30f, Drag = 0.06f, Flare = 0.3f } };
                case 12: // Nässla: her white hair hanging behind, 0.72-0.82 of her height (the hips at about 0.55), 0.10-0.13 behind,
                         // lying close on her back: one spring that lifts it out and never in (testers 2026-11-13: the hair must swing)
                    return new[] { new Spec { Kind = Kind.BackFlaps, Single = true, WithHead = true, Top0 = 0.27f, Top1 = 0.23f, Low = 0.15f, Back0 = 0.085f, Back1 = 0.115f, MaxSide = 0.11f, Hz = 2.3f, Damping = 0.4f, MaxAngle = 16f, Drag = 0.05f, Flare = 0.15f, Inward = 0f, Kick = 0.8f } };
                case 1: // Fenn: the plume on his helmet (Rickard 2026-11-18: "animera tofsen"), on the knight and all three skins
                        // (Revenant's ghost flame, Sunforged's white crest, Phantom's on the hood). Measured on the V2 FBX files
                        // (Blender, bam-work/fenn-detaljer-2026-11-18/): the helmet's crown ends 0.085 of his height above the head
                        // bone and its back 0.07 behind it; the plume rises to 0.19-0.20 above and arcs back to 0.17-0.21 behind,
                        // its tip hanging down to the head bone's height. Everything on Head (weight 1.0), so the Head share picks it.
                        // Tied on at the crown's top; the sun crown's rays and the hood's peak sit in front (Ahead) and stay put
                    return new[] { new Spec { Kind = Kind.Plume, Up0 = 0.085f, Up1 = 0.125f, Back0 = 0.08f, Back1 = 0.12f, Ahead = 0.03f, PivotUp = 0.085f, PivotBack = 0.0f, Hz = 2.0f, Damping = 0.28f, MaxAngle = 30f, Drag = 0.07f, Kick = 1.0f, Wind = 0.15f } };
                default: return null;
            }
        }

        static Spec[] SpecsFor(int hero, Mesh body)
        {
            switch (hero)
            {
                case 1 when !body.name.StartsWith("unamed"):
                    // Fenn's Clay body, the one in the game (Rickard 2026-11-12: "lägg in den där textil och hår animation på alla som det
                    // ser bra ut på"): the cloth panels in front and behind, between his legs (only the middle: the side tassets and the
                    // knees are part of the leg shell and stay put; measured on Fenn/Fenn.fbx), and the helmet's plume behind his head
                    return new[]
                    {
                        new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.04f, Clear1 = 0.08f, Drop = 0.10f, MaxBelow = 0.2f, MaxSide = 0.07f, Hz = 2.4f, Damping = 0.5f, MaxAngle = 18f, Drag = 0.04f, Flare = 0.12f },
                        new Spec { Kind = Kind.Tail, Back0 = 0.06f, Back1 = 0.11f, PivotUp = 0.12f, Hz = 2.4f, Damping = 0.35f, MaxAngle = 25f, Drag = 0.05f },
                    };
                case 1: // Fenn (the Original body, mesh "unamed"): the cape behind and the tabard in front, both hanging free of the legs
                    return new[] { new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.055f, Clear1 = 0.11f, Drop = 0.12f, Hz = 2.2f, Damping = 0.45f, MaxAngle = 26f, Drag = 0.05f, Flare = 0.22f } };
                case 2: // Tyra: the ponytail, and the two front locks beside her face (Rickard 2026-11-12: "tyras lugg skulle må bra av
                        // liite rörelse som det andra håret här bak"): a little, shorter and stiffer than the ponytail; measured on
                        // Tyra.fbx (Blender renders 2026-11-12: the ears sit 0.08-0.10 of her height behind the locks' Front plane)
                    return new[]
                    {
                        new Spec { Kind = Kind.Tail, Back0 = 0.04f, Back1 = 0.085f, PivotUp = 0.12f, Hz = 2.6f, Damping = 0.3f, MaxAngle = 35f, Drag = 0.06f },
                        // Rickard 2026-11-12 after testing 12 degrees: "för lite på tyras lugg": about twice the swing, softer and slower
                        new Spec { Kind = Kind.Fringe, PivotUp = 0.12f, Hz = 2.4f, Damping = 0.28f, MaxAngle = 24f, Drag = 0.07f, Inward = 0.05f, Kick = 1.2f },
                    };
                case 3: // Moa: only the narrow embroidered panel hanging in front between her legs (Rickard 2026-11-12: "moas kläder och hår
                        // kanske kunde använda den nya funktionen"). Front only and narrow: the 9 Nov try (removed, "det buggar") took the
                        // back skirt too, which is the same shell as her seat and the backs of her thighs (measured again on Moa.fbx)
                    // + the lower end of her long hair (Rickard 2026-11-13: "och hennes hår"): only its bottom, from shoulder-blade height
                    // down, behind her back and in from its edges, on one spring that lifts it out from her back and never in (rendered on
                    // Moa.fbx at 12 degrees out: bam-work/cloth/round7/moa-har.png); a small swing, since its top is her back's surface
                    return new[]
                    {
                        // (Rickard 2026-11-13 after testing: "fladdrar bara mitten av tyget och buggar när det drar sig utanför kanterna som
                        // egentligen också ska röra sig"): the whole width of the panel (to 0.05 out, its gold edges too) on ONE spring, picked
                        // by lying in front of her thighs (0.045-0.053 in front of the hips) instead of by distance to the leg bones, which
                        // left its edges on the thighs and split the middle over three springs (measured on Moa.fbx)
                        // REVERTED 2026-11-13 (Rickard: "moas mitt tyg är buggat nu mer än innan ... ha som det va för ett tag sen"): back to the 12 Nov
                        // panel (round 6); the whole-width try above (Single, Front0/1, Flutter) is unused for now
                        new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.04f, Clear1 = 0.07f, Drop = 0.12f, MaxBelow = 0.4f, MaxSide = 0.04f, FrontOnly = true, Hz = 2.3f, Damping = 0.42f, MaxAngle = 22f, Drag = 0.05f, Flare = 0.15f },
                        new Spec { Kind = Kind.BackFlaps, Single = true, Top0 = 0.254f, Top1 = 0.194f, Low = 0.054f, Back0 = 0.117f, Back1 = 0.147f, MaxSide = 0.075f, Hz = 2.2f, Damping = 0.45f, MaxAngle = 12f, Drag = 0.04f, Flare = 0.12f, Inward = 0f, Kick = 0.6f },
                    };
                case 7: // Bonk: the fur loincloth flaps in front and behind, and the long hair behind his head
                    return new[]
                    {
                        // Rickard 2026-11-09 after testing: "bonk har för mycket rörelse i kläder": about half the swing, stiffer and better damped
                        new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.06f, Clear1 = 0.10f, Drop = 0.10f, MaxSide = 0.07f, Hz = 2.8f, Damping = 0.62f, MaxAngle = 10f, Drag = 0.025f, Flare = 0.06f, Kick = 0.5f },
                        new Spec { Kind = Kind.Tail, Back0 = 0.04f, Back1 = 0.085f, PivotUp = 0.08f, Hz = 2.8f, Damping = 0.55f, MaxAngle = 13f, Drag = 0.025f, Kick = 0.5f },
                    };
                case 11: // Ragn (preview): his wolf tail behind the hips (Rickard 2026-11-10: "ragns svans kanske kan få röra sig som vi
                         // gjort med annat innan?", "när man snurrar gubben osv"); it trails when he runs and swings out when he is spun
                    return new[] { new Spec { Kind = Kind.HipTail, Back0 = 0.11f, Back1 = 0.17f, Above = 0.04f, Hz = 2.2f, Damping = 0.38f, MaxAngle = 30f, Drag = 0.06f, Flare = 0.3f } };
                case 12: // Nässla: her short tunic, free of her legs from the belt down to the hem (Cajsa 2026-11-11, "mer c/h simulering",
                         // Rickard "1 och 2"); measured on Nassla.fbx: the hem ends 0.15 of her height below the hips, the calves stay put
                    return new[] { new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.04f, Clear1 = 0.08f, Drop = 0.10f, MaxBelow = 0.165f, Hz = 2.4f, Damping = 0.42f, MaxAngle = 22f, Drag = 0.05f, Flare = 0.2f } };
                // Moa (again 2026-11-11): her long hair lies on her back as part of the same shell (Moa.fbx: the hair IS the back's
                // surface from the neck to the waist, weighted to Spine1/Spine2), so swinging it would bend her back. No sway.
                case 5: // Tikta: the narrow purple cloth panel hanging in front between her legs (Rickard 2026-11-12, "på alla som det ser
                        // bra ut på"); front only and narrow, so her seat and thighs stay put (measured on Tikta.fbx)
                    // + the two ends of her scarf hanging down her back (Rickard 2026-11-12: "tiktas halsduk skulle kunna behöva lite, iaf
                    // ändarna som är bakom henne"): free of the back from 0.09 above the hips down to their tips (measured on Tikta.fbx)
                    return new[]
                    {
                        new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.03f, Clear1 = 0.06f, Drop = 0.12f, MaxBelow = 0.25f, MaxSide = 0.04f, FrontOnly = true, Hz = 2.6f, Damping = 0.4f, MaxAngle = 22f, Drag = 0.05f, Flare = 0.15f },
                        new Spec { Kind = Kind.BackFlaps, Top0 = 0.125f, Top1 = 0.09f, Low = 0.015f, Back0 = 0.05f, Back1 = 0.065f, Hz = 2.3f, Damping = 0.35f, MaxAngle = 30f, Drag = 0.06f, Flare = 0.25f, Inward = 0.05f },
                    };
                // Knut, Bram, Blast, Kugg, Oaken: nothing hangs free of the body (Knut's vest and mane are the top of his back, Blast's
                // scarf is a collar, Kugg is stone and brass). Checked again on the models 2026-11-12. Hilde (not in the
                // game yet): her long braid reaches her hips and needs a chain of bones, not one; when she comes in.
                default: return null;
            }
        }

        /// <summary>A spring bone as baked into the mesh: its parent (index into the renderer's bones), its pivot in the parent's
        /// space and its rest vector (pivot to the cloth's middle) in the parent's space.</summary>
        struct BoneDef
        {
            public int Parent;
            public Vector3 Pivot, Rest;
            public Spec Spec;
            public Vector3 OutLocal; // Skirt: outward from the body, in the parent's space (zero for a tail)
        }

        sealed class Baked { public Mesh Mesh; public BoneDef[] Bones; }

        static readonly Dictionary<Mesh, Baked> baked = new Dictionary<Mesh, Baked>(); // one copy per model, shared by all its views
#if UNITY_EDITOR
        static bool warned;
#endif

        sealed class Spring
        {
            public Transform T, Parent;
            public BoneDef Def;
            public Vector3 Tip, Vel, LastPivot, LastTarget;
            public bool Live;
        }

        readonly Spring[] springs;
        readonly Transform body;   // the model root: its yaw speed drives the flare
        Vector3 lastFwd;
        float clock; // seconds, for Flutter
        bool wasOn = true;

        // secondary action (Kick): pushes waiting to land, in the body's own frame (forward, up), and the run that a hard stop ends
        struct Push { public float At, Fwd, Up; }
        readonly Push[] pushes = new Push[4];
        int pushN;
        Vector3 lastBody, runDir;
        float runSpeed, runPeak;
        bool bodyLive;

        ClothSway(Spring[] s, Transform body) { springs = s; this.body = body; lastFwd = body.forward; }

        /// <summary>null when this hero has no cloth parts, or its model can't be read.</summary>
        internal static ClothSway For(Animator an, Renderer[] rends, int heroIndex, bool v2 = false)
        {
            Spec[] specs = null;
            if (an == null || !an.isHuman || rends == null) return null;
            SkinnedMeshRenderer smr = null;
            for (int i = 0; i < rends.Length && smr == null; i++) smr = rends[i] as SkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null) return null;
            specs = v2 ? SpecsForV2(heroIndex) : SpecsFor(heroIndex, smr.sharedMesh);
            if (specs == null) return null;
            Mesh src = smr.sharedMesh;
            if (!baked.TryGetValue(src, out Baked b))
            {
                b = Bake(src, smr.bones, an, specs);
                baked[src] = b; // null too: don't try again for every view
            }
            if (b == null) return null;

            Transform[] old = smr.bones;
            var bones = new Transform[old.Length + b.Bones.Length];
            System.Array.Copy(old, bones, old.Length);
            var springs = new Spring[b.Bones.Length];
            for (int i = 0; i < b.Bones.Length; i++)
            {
                BoneDef d = b.Bones[i];
                var g = new GameObject("BAM_Cloth" + i);
                Transform t = g.transform;
                t.SetParent(old[d.Parent], false);
                t.localPosition = d.Pivot;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                bones[old.Length + i] = t;
                springs[i] = new Spring { T = t, Parent = old[d.Parent], Def = d };
            }
            smr.bones = bones;
            smr.sharedMesh = b.Mesh;
            return new ClothSway(springs, an.transform);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static Baked Bake(Mesh src, Transform[] bones, Animator an, Spec[] specs)
        {
            // only the bodies HeroArtImport.ClothModel imports with Read/Write on (Clay-Fenn since 2026-11-12: only his middle
            // panels and plume, as his side tassets are part of the leg shell and would bend his thighs)
            if (!src.isReadable)
            {
#if UNITY_EDITOR
                if (!warned) { warned = true; Debug.Log("[BAM.Art] " + src.name + " is not readable, so it has no cloth sway (BAM > Art > Rebuild Hero Models imports the cloth heroes with Read/Write on)."); }
#endif
                return null;
            }
            Matrix4x4[] bind = src.bindposes;
            if (bind.Length != bones.Length) return null;
            int Idx(HumanBodyBones h)
            {
                Transform t = an.GetBoneTransform(h);
                return t == null ? -1 : System.Array.IndexOf(bones, t);
            }
            Vector3 At(int i) => bind[i].inverse.MultiplyPoint3x4(Vector3.zero); // a bone's bind position in mesh space
            int hips = Idx(HumanBodyBones.Hips), head = Idx(HumanBodyBones.Head);
            int lUp = Idx(HumanBodyBones.LeftUpperLeg), lLo = Idx(HumanBodyBones.LeftLowerLeg), lFoot = Idx(HumanBodyBones.LeftFoot), lToe = Idx(HumanBodyBones.LeftToes);
            int rUp = Idx(HumanBodyBones.RightUpperLeg), rLo = Idx(HumanBodyBones.RightLowerLeg), rFoot = Idx(HumanBodyBones.RightFoot), rToe = Idx(HumanBodyBones.RightToes);
            if (hips < 0 || head < 0 || lUp < 0 || lLo < 0 || lFoot < 0 || rUp < 0 || rLo < 0 || rFoot < 0) return null;

            // the body's up and forward in mesh space, from its own bones (no guessing about the FBX's axes)
            Vector3 up = (At(head) - At(hips)).normalized;
            Vector3 fwd = Vector3.zero;
            if (lToe >= 0) fwd += At(lToe) - At(lFoot);
            if (rToe >= 0) fwd += At(rToe) - At(rFoot);
            fwd -= up * Vector3.Dot(fwd, up);
            if (fwd.sqrMagnitude < 1e-10f) return null;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(up, fwd);

            Vector3[] v = src.vertices;
            int n = v.Length;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < n; i++) { float y = Vector3.Dot(v[i], up); if (y < lo) lo = y; if (y > hi) hi = y; }
            float H = hi - lo;
            if (!(H > 1e-5f)) return null;

            // the model's weights, per vertex
            NativeArray<byte> bpvN = src.GetBonesPerVertex();
            NativeArray<BoneWeight1> bwN = src.GetAllBoneWeights();
            if (bpvN.Length != n) return null;
            var start = new int[n];
            var count = new int[n];
            var bw = bwN.ToArray();
            for (int i = 0, s = 0; i < n; i++) { start[i] = s; count[i] = bpvN[i]; s += count[i]; }

            float Share(int vi, int[] set)
            {
                float w = 0f;
                for (int k = 0; k < count[vi]; k++)
                {
                    BoneWeight1 x = bw[start[vi] + k];
                    for (int j = 0; j < set.Length; j++) if (set[j] == x.boneIndex) { w += x.weight; break; }
                }
                return w;
            }

            var defs = new List<BoneDef>();
            var extra = new List<(int bone, float w)>[n];
            void Add(int vi, int bone, float w)
            {
                if (w <= 0.005f) return;
                (extra[vi] ?? (extra[vi] = new List<(int, float)>())).Add((bone, w));
            }

            foreach (Spec sp in specs)
            {
                if (sp.Kind == Kind.Skirt)
                {
                    Vector3 hp = At(hips);
                    Vector3 aL = At(lUp), bL = At(lLo), cL = At(lFoot), aR = At(rUp), bR = At(rLo), cR = At(rFoot);
                    int spine = Idx(HumanBodyBones.Spine);
                    var set = new List<int> { hips, lUp, lLo, rUp, rLo };
                    if (spine >= 0) set.Add(spine);
                    int[] setA = set.ToArray();
                    int N = Mathf.Max(3, sp.Sectors);
                    float side = sp.MaxSide > 0f ? sp.MaxSide * H : float.MaxValue;
                    var sw = new float[n];
                    var sa = new float[n]; // the vertex's angle round the hips, in sectors
                    var sum = new Vector3[N];
                    var wsum = new float[N];
                    for (int i = 0; i < n; i++)
                    {
                        float below = -Vector3.Dot(v[i] - hp, up);
                        if (below <= 0f || Share(i, setA) < 0.8f) continue; // above the hips, or an arm hanging past them
                        float sideD = Mathf.Abs(Vector3.Dot(v[i] - hp, right));
                        if (sideD > side) continue;
                        if (sp.MaxBelow > 0f && below > sp.MaxBelow * H) continue;
                        if (sp.FrontOnly && Vector3.Dot(v[i] - hp, fwd) < 0f) continue;
                        float s;
                        if (sp.Front1 > sp.Front0)
                            s = Smooth((Vector3.Dot(v[i] - hp, fwd) / H - sp.Front0) / (sp.Front1 - sp.Front0)); // in front of the thighs
                        else
                        {
                            float d = Mathf.Min(Mathf.Min(SegDist(v[i], aL, bL), SegDist(v[i], bL, cL)), Mathf.Min(SegDist(v[i], aR, bR), SegDist(v[i], bR, cR))) / H;
                            s = Smooth((d - sp.Clear0) / Mathf.Max(1e-4f, sp.Clear1 - sp.Clear0));
                        }
                        s *= Smooth(below / (sp.Drop * H));
                        if (sp.SideFade > 0f) s *= Smooth((side - sideD) / (sp.SideFade * H));
                        if (s <= 0.005f) continue;
                        Vector3 h = v[i] - hp; h -= up * Vector3.Dot(h, up);
                        float ang = Mathf.Atan2(Vector3.Dot(h, right), Vector3.Dot(h, fwd)); // 0 = straight ahead
                        float a = sp.Single ? 0f : Mathf.Repeat(ang / (2f * Mathf.PI) * N, N); // Single: all on one spring, no seam
                        sw[i] = s; sa[i] = a;
                        int k0 = Mathf.FloorToInt(a) % N, k1 = (k0 + 1) % N;
                        float f = a - Mathf.Floor(a);
                        sum[k0] += (v[i] - hp) * (s * (1f - f)); wsum[k0] += s * (1f - f);
                        sum[k1] += (v[i] - hp) * (s * f); wsum[k1] += s * f;
                    }
                    // one spring bone per sector that has cloth in it, pivot at the hips, resting towards its cloth's middle
                    var boneOf = new int[N];
                    Vector3 pivotL = bind[hips].MultiplyPoint3x4(hp);
                    for (int k = 0; k < N; k++)
                    {
                        boneOf[k] = -1;
                        if (wsum[k] < 1f) continue; // a few stray vertices: they stay on the body
                        Vector3 rest = sum[k] / wsum[k];
                        if (rest.sqrMagnitude < 1e-8f) continue;
                        Vector3 outM = rest - up * Vector3.Dot(rest, up);
                        Vector3 pv = pivotL;
                        if (sp.Single)
                        {
                            // one panel: tied on at the belt straight above its middle, so it swings like a flag from its top edge
                            // (about the hips' centre the whole panel only tilted a little: Rickard 2026-11-13 "fladdrar inte alls nu")
                            pv = bind[hips].MultiplyPoint3x4(hp + outM);
                            rest -= outM;
                        }
                        boneOf[k] = bones.Length + defs.Count;
                        defs.Add(new BoneDef
                        {
                            Parent = hips, Pivot = pv, Rest = bind[hips].MultiplyVector(rest), Spec = sp,
                            OutLocal = outM.sqrMagnitude > 1e-8f ? bind[hips].MultiplyVector(outM.normalized).normalized : Vector3.zero,
                        });
                    }
                    for (int i = 0; i < n; i++)
                    {
                        if (sw[i] <= 0f) continue;
                        int k0 = Mathf.FloorToInt(sa[i]) % N, k1 = (k0 + 1) % N;
                        float f = sa[i] - Mathf.Floor(sa[i]);
                        // a sector without its own bone hands its share to the neighbour, so no vertex is left behind with a seam
                        if (boneOf[k0] < 0 && boneOf[k1] < 0) continue;
                        if (boneOf[k0] < 0) { k0 = k1; f = 0f; }
                        if (boneOf[k1] < 0) { k1 = k0; f = 0f; }
                        Add(i, boneOf[k0], sw[i] * (1f - f));
                        if (k1 != k0) Add(i, boneOf[k1], sw[i] * f);
                    }
                }
                else if (sp.Kind == Kind.BackFlaps)
                {
                    // flaps hanging down the back clear of it (Tikta's scarf ends): what is behind the hips by Back0..Back1, between Low
                    // and Top0 above them, on the torso's bones; one spring bone per side, tied on at the flap's top, on the chest
                    Vector3 hp = At(hips), back = -fwd;
                    int s1 = Idx(HumanBodyBones.Spine), s2 = Idx(HumanBodyBones.Chest), s3 = Idx(HumanBodyBones.UpperChest);
                    int lSh = Idx(HumanBodyBones.LeftShoulder), rSh = Idx(HumanBodyBones.RightShoulder);
                    var set = new List<int> { hips };
                    foreach (int b in new[] { s1, s2, s3, lSh, rSh }) if (b >= 0) set.Add(b);
                    if (sp.WithHead) foreach (int b in new[] { Idx(HumanBodyBones.Neck), head }) if (b >= 0) set.Add(b);
                    int[] setA = set.ToArray();
                    int parent = s3 >= 0 ? s3 : s2 >= 0 ? s2 : s1 >= 0 ? s1 : hips;
                    var sw = new float[n];
                    var side = new int[n];
                    var sum = new Vector3[2]; var ws = new float[2]; var top = new float[2];
                    for (int i = 0; i < n; i++)
                    {
                        float ab = Vector3.Dot(v[i] - hp, up) / H;
                        if (ab <= sp.Low || ab >= sp.Top0 || Share(i, setA) < 0.8f) continue;
                        float bk = Vector3.Dot(v[i] - hp, back) / H;
                        float s = Smooth((sp.Top0 - ab) / Mathf.Max(1e-4f, sp.Top0 - sp.Top1)) * Smooth((bk - sp.Back0) / Mathf.Max(1e-4f, sp.Back1 - sp.Back0));
                        float sd = Vector3.Dot(v[i] - hp, right);
                        if (sp.MaxSide > 0f) s *= Smooth((sp.MaxSide * H - Mathf.Abs(sd)) / (0.33f * sp.MaxSide * H)); // its edges stay on the body
                        if (s <= 0.005f) continue;
                        int k = sp.Single || sd >= 0f ? 1 : 0;
                        sw[i] = s; side[i] = k;
                        sum[k] += v[i] * s; ws[k] += s;
                    }
                    var boneOf = new[] { -1, -1 };
                    for (int k = 0; k < 2; k++)
                    {
                        if (ws[k] < 1f) continue;
                        Vector3 mid = sum[k] / ws[k];
                        // the tie point: above the flap's middle, where it leaves the back
                        Vector3 pivot = mid + up * (Vector3.Dot(hp - mid, up) + sp.Top0 * H);
                        boneOf[k] = bones.Length + defs.Count;
                        defs.Add(new BoneDef
                        {
                            Parent = parent, Pivot = bind[parent].MultiplyPoint3x4(pivot), Rest = bind[parent].MultiplyVector(mid - pivot), Spec = sp,
                            OutLocal = bind[parent].MultiplyVector(back).normalized, // a spin swings them out behind, never into her back
                        });
                    }
                    for (int i = 0; i < n; i++) if (sw[i] > 0f && boneOf[side[i]] >= 0) Add(i, boneOf[side[i]], sw[i]);
                }
                else if (sp.Kind == Kind.HipTail)
                {
                    // a tail behind the hips: what sits on the hips, behind them and not up the back, hangs on one spring bone tied on
                    // at the rump (Ragn.fbx has the tail wholly on Hips)
                    Vector3 hp = At(hips), back = -fwd;
                    int spn = Idx(HumanBodyBones.Spine);
                    int[] setA = sp.WithSpine && spn >= 0 ? new[] { hips, spn } : new[] { hips };
                    Vector3 pivot = hp + back * (sp.Back0 * H) + up * (sp.PivotUp * H);
                    var sw = new float[n];
                    Vector3 sum = Vector3.zero; float ws = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        if (Vector3.Dot(v[i] - hp, up) > sp.Above * H || Share(i, setA) < 0.8f) continue;
                        float bk = Vector3.Dot(v[i] - hp, back) / H;
                        float s = Smooth((bk - sp.Back0) / Mathf.Max(1e-4f, sp.Back1 - sp.Back0));
                        if (s <= 0.005f) continue;
                        sw[i] = s;
                        sum += (v[i] - pivot) * (s * s); ws += s * s;
                    }
                    if (ws < 1f) continue;
                    int bi = bones.Length + defs.Count;
                    defs.Add(new BoneDef
                    {
                        Parent = hips, Pivot = bind[hips].MultiplyPoint3x4(pivot), Rest = bind[hips].MultiplyVector(sum / ws), Spec = sp,
                        OutLocal = bind[hips].MultiplyVector(back).normalized, // a spin swings it out behind, never in through the legs
                    });
                    for (int i = 0; i < n; i++) if (sw[i] > 0f) Add(i, bi, sw[i]);
                }
                else if (sp.Kind == Kind.Fringe)
                {
                    // the two front locks that hang beside the face (Tyra), one spring bone each, tied on at the hairline above the
                    // temple; what is inside Side0 (the face), behind Front (the ears) or above Top (the scalp) stays on the head
                    Vector3 hp = At(head);
                    int neck = Idx(HumanBodyBones.Neck);
                    int[] setA = neck >= 0 ? new[] { head, neck } : new[] { head };
                    for (int sideSign = -1; sideSign <= 1; sideSign += 2)
                    {
                        Vector3 pivot = hp + fwd * (sp.PivotFwd * H) + up * (sp.PivotUp * H) + right * (sideSign * sp.PivotSide * H);
                        var sw = new float[n];
                        Vector3 sum = Vector3.zero; float ws = 0f;
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 d = (v[i] - hp) / H;
                            float x = Vector3.Dot(d, right) * sideSign;
                            if (x <= 0f || Share(i, setA) < 0.8f) continue;
                            float s = Smooth((x - sp.Side0) / Mathf.Max(1e-4f, sp.Side1 - sp.Side0))
                                * Smooth((sp.Top - Vector3.Dot(d, up)) / Mathf.Max(1e-4f, sp.TopDrop))
                                * Smooth((Vector3.Dot(d, fwd) - sp.Front) / 0.012f);
                            if (s <= 0.005f) continue;
                            sw[i] = s;
                            sum += (v[i] - pivot) * (s * s); ws += s * s;
                        }
                        if (ws < 1f) continue;
                        int bi = bones.Length + defs.Count;
                        defs.Add(new BoneDef
                        {
                            Parent = head, Pivot = bind[head].MultiplyPoint3x4(pivot), Rest = bind[head].MultiplyVector(sum / ws), Spec = sp,
                            OutLocal = bind[head].MultiplyVector(right * sideSign).normalized, // never in across the face
                        });
                        for (int i = 0; i < n; i++) if (sw[i] > 0f) Add(i, bi, sw[i]);
                    }
                }
                else if (sp.Kind == Kind.Plume)
                {
                    // a helmet's plume (Fenn V2): what rises above the crown (Up0..Up1) or trails behind the head (Back0..Back1),
                    // whichever is more, never what is in front of the head bone (Ahead); one spring bone tied on at the crown
                    Vector3 hp = At(head), back = -fwd;
                    int neck = Idx(HumanBodyBones.Neck);
                    int[] setA = neck >= 0 ? new[] { head, neck } : new[] { head };
                    Vector3 pivot = hp + back * (sp.PivotBack * H) + up * (sp.PivotUp * H);
                    var sw = new float[n];
                    Vector3 sum = Vector3.zero; float ws = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        if (Share(i, setA) < 0.8f) continue;
                        Vector3 d = (v[i] - hp) / H;
                        float bk = Vector3.Dot(d, back), ab = Vector3.Dot(d, up);
                        float s = Mathf.Max(Smooth((ab - sp.Up0) / Mathf.Max(1e-4f, sp.Up1 - sp.Up0)), Smooth((bk - sp.Back0) / Mathf.Max(1e-4f, sp.Back1 - sp.Back0)));
                        s *= Smooth((bk + sp.Ahead) / Mathf.Max(1e-4f, sp.Ahead)); // in front of the head: stays on the helmet
                        if (s <= 0.005f) continue;
                        sw[i] = s;
                        sum += (v[i] - pivot) * (s * s); ws += s * s;
                    }
                    if (ws < 1f) continue;
                    int bi = bones.Length + defs.Count;
                    defs.Add(new BoneDef { Parent = head, Pivot = bind[head].MultiplyPoint3x4(pivot), Rest = bind[head].MultiplyVector(sum / ws), Spec = sp });
                    for (int i = 0; i < n; i++) if (sw[i] > 0f) Add(i, bi, sw[i]);
                }
                else
                {
                    Vector3 hp = At(head), back = -fwd;
                    int neck = Idx(HumanBodyBones.Neck);
                    int[] setA = neck >= 0 ? new[] { head, neck } : new[] { head };
                    Vector3 pivot = hp + back * (sp.Back0 * H) + up * (sp.PivotUp * H);
                    var sw = new float[n];
                    Vector3 sum = Vector3.zero; float ws = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        if (Share(i, setA) < 0.8f) continue;
                        float bk = Vector3.Dot(v[i] - hp, back) / H;
                        float s = Smooth((bk - sp.Back0) / Mathf.Max(1e-4f, sp.Back1 - sp.Back0));
                        if (s <= 0.005f) continue;
                        sw[i] = s;
                        sum += (v[i] - pivot) * (s * s); ws += s * s;
                    }
                    if (ws < 1f) continue;
                    int bi = bones.Length + defs.Count;
                    defs.Add(new BoneDef { Parent = head, Pivot = bind[head].MultiplyPoint3x4(pivot), Rest = bind[head].MultiplyVector(sum / ws), Spec = sp });
                    for (int i = 0; i < n; i++) if (sw[i] > 0f) Add(i, bi, sw[i]);
                }
            }
            if (defs.Count == 0) return null;

            // the new weights: the body's own scaled down by the cloth's share, the cloth bones added, at most 4, heaviest first
            var outCount = new byte[n];
            var outW = new List<BoneWeight1>(bw.Length + n);
            var tmp = new List<BoneWeight1>(8);
            for (int i = 0; i < n; i++)
            {
                tmp.Clear();
                float ex = 0f;
                if (extra[i] != null) foreach (var e in extra[i]) ex += e.w;
                float keep = Mathf.Clamp01(1f - ex);
                for (int k = 0; k < count[i]; k++) { BoneWeight1 x = bw[start[i] + k]; x.weight *= keep; tmp.Add(x); }
                if (extra[i] != null) foreach (var e in extra[i]) tmp.Add(new BoneWeight1 { boneIndex = e.bone, weight = e.w });
                tmp.Sort((p, q) => q.weight.CompareTo(p.weight));
                int c = Mathf.Min(4, tmp.Count);
                float tot = 0f;
                for (int k = 0; k < c; k++) tot += tmp[k].weight;
                if (tot <= 1e-6f) tot = 1f;
                for (int k = 0; k < c; k++) { BoneWeight1 x = tmp[k]; x.weight /= tot; outW.Add(x); }
                outCount[i] = (byte)c;
            }

            Mesh m = Object.Instantiate(src);
            m.name = src.name + " (cloth)";
            var newBind = new Matrix4x4[bind.Length + defs.Count];
            System.Array.Copy(bind, newBind, bind.Length);
            for (int i = 0; i < defs.Count; i++)
                newBind[bind.Length + i] = Matrix4x4.Translate(-defs[i].Pivot) * bind[defs[i].Parent]; // at rest it skins exactly as its parent
            m.bindposes = newBind;
            using (var c = new NativeArray<byte>(outCount, Allocator.Temp))
            using (var w = new NativeArray<BoneWeight1>(outW.ToArray(), Allocator.Temp))
                m.SetBoneWeights(c, w);
            return new Baked { Mesh = m, Bones = defs.ToArray() };
        }

        /// <summary>Secondary action (Cajsa 2026-11-11): the hero's own move pushes the cloth and hair, so they swing past the body and
        /// settle after it instead of only trailing it. delay: seconds until the push lands (the blow, a special's peak); fwd and up:
        /// its size along the hero's facing and up (1 swings a part about 20 degrees before MaxAngle caps it).</summary>
        internal void Kick(float delay, float fwd, float up)
        {
            if (!AnimFix.ClothKicks) return;
            if (pushN == pushes.Length) { for (int i = 1; i < pushN; i++) pushes[i - 1] = pushes[i]; pushN--; } // the oldest goes
            pushes[pushN++] = new Push { At = Mathf.Max(0f, delay), Fwd = fwd, Up = up };
        }

        void PushAll(Vector3 dirWorld)
        {
            foreach (Spring s in springs)
            {
                if (!s.Live) continue;
                Spec sp = s.Def.Spec;
                float L = s.Parent.TransformVector(s.Def.Rest).magnitude;
                s.Vel += dirWorld * (sp.Kick * L * 2f * Mathf.PI * sp.Hz * 0.6f);
            }
        }

        /// <summary>After everything else has posed the body this frame (BodyView.LateUpdate). dt: the view's frame time.</summary>
        internal void Tick(float dt, bool on)
        {
            if (!on)
            {
                if (wasOn) { wasOn = false; foreach (Spring s in springs) { s.T.localRotation = Quaternion.identity; s.Live = false; } }
                pushN = 0; bodyLive = false;
                return;
            }
            wasOn = true;
            if (dt <= 0f) return;
            Vector3 fwd = body.forward;
            Vector3 jump = Vector3.zero; // a Blink or a dash too fast for the springs: the cloth starts at rest, then trails the jump
            {
                Vector3 bp = body.position;
                Vector3 d = bp - lastBody; d.y = 0f;
                if (!bodyLive) { bodyLive = true; d = Vector3.zero; }
                lastBody = bp;
                float spd = d.magnitude / dt;
                if (spd > 12f) { if (d.magnitude > 1f && d.magnitude < 6f) jump = d; runPeak = 0f; } // not a respawn's jump across the map, nor an online snap
                else
                {
                    if (spd > 0.5f) runDir = d / (spd * dt);
                    runSpeed = Mathf.Lerp(runSpeed, spd, 1f - Mathf.Exp(-dt * 20f));
                    runPeak = Mathf.Max(runPeak * Mathf.Exp(-dt * 4f), runSpeed);
                    // a hard stop after a run: the cloth swings on forward past the body and settles
                    if (AnimFix.ClothKicks && runSpeed < 0.3f && runPeak > 1.5f)
                    {
                        PushAll(runDir * (0.45f * Mathf.Clamp01(runPeak / 3f)));
                        runPeak = 0f;
                    }
                }
            }
            for (int i = 0; i < pushN;)
            {
                pushes[i].At -= dt;
                if (pushes[i].At > 0f) { i++; continue; }
                PushAll(fwd * pushes[i].Fwd + Vector3.up * pushes[i].Up);
                pushes[i] = pushes[--pushN];
            }
            clock += dt;
            float yawRate = Mathf.Abs(Vector3.SignedAngle(lastFwd, fwd, Vector3.up)) / dt; // degrees per second
            lastFwd = fwd;
            float spin = Mathf.Clamp01((yawRate - 90f) / 540f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 90f), 1, 6);
            float h = dt / steps;
            foreach (Spring s in springs)
            {
                Spec sp = s.Def.Spec;
                Transform p = s.Parent;
                Vector3 pivot = s.T.position;
                Vector3 rest = p.TransformVector(s.Def.Rest);
                float L = rest.magnitude;
                if (!(L > 1e-5f)) continue;
                Vector3 restN = rest / L;
                Vector3 outW = s.Def.OutLocal.sqrMagnitude > 0f ? p.TransformDirection(s.Def.OutLocal) : Vector3.zero;
                Vector3 vel = (pivot - s.LastPivot) / dt;
                Vector3 target = pivot + rest;
                if (!s.Live || (pivot - s.LastPivot).sqrMagnitude > 4f * L * L) // first frame, a respawn or a Blink: start at rest
                {
                    s.Live = true; s.Tip = target; s.Vel = Vector3.zero; s.LastPivot = pivot; s.LastTarget = target;
                    s.T.localRotation = Quaternion.identity;
                    // after a Blink the cloth swings on in the jump's direction, as if it had flown with the body
                    if (AnimFix.ClothKicks && jump.sqrMagnitude > 0f) s.Vel = jump.normalized * (0.5f * sp.Kick * L * 2f * Mathf.PI * sp.Hz * 0.6f);
                    continue;
                }
                // air: the part trails the body's motion and flares out in a spin
                Vector3 trail = -vel * sp.Drag;
                if (trail.sqrMagnitude > 0.16f * L * L) trail = trail.normalized * (0.4f * L);
                target += trail + outW * (sp.Flare * spin * L);
                if (sp.Wind > 0f) // its own slow wave, standing still too: side to side, and a little up and down out of step
                    target += (body.right * Mathf.Sin(clock * 2.1f) + Vector3.up * (0.35f * Mathf.Sin(clock * 3.4f + 1f))) * (sp.Wind * L);
                if (sp.Flutter > 0f)
                {
                    float run = Mathf.Clamp01(new Vector2(vel.x, vel.z).magnitude / 3f);
                    target += (body.right * Mathf.Sin(clock * 8.5f) + fwd * (0.6f * Mathf.Abs(Mathf.Sin(clock * 8.5f + 1f)))) * (sp.Flutter * L * run);
                }
                float w = 2f * Mathf.PI * sp.Hz, k = w * w, c = 2f * sp.Damping * w;
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 tg = Vector3.Lerp(s.LastTarget, target, (float)i / steps); // the target moves through the frame, not in one jump
                    Vector3 acc = (tg - s.Tip) * k - s.Vel * c;
                    s.Vel += acc * h;
                    s.Tip += s.Vel * h;
                }
                s.LastTarget = target;
                s.LastPivot = pivot;
                Vector3 dir = s.Tip - pivot;
                if (dir.sqrMagnitude < 1e-10f) dir = restN; else dir.Normalize();
                if (outW != Vector3.zero)
                {
                    float into = Vector3.Dot(dir - restN, outW) + sp.Inward; // not in through the legs
                    if (into < 0f) dir = (dir - outW * into).normalized;
                }
                float ang = Vector3.Angle(restN, dir);
                if (ang > sp.MaxAngle) dir = Vector3.Slerp(restN, dir, sp.MaxAngle / ang);
                s.Tip = pivot + dir * L;
                s.T.rotation = Quaternion.FromToRotation(restN, dir) * p.rotation;
            }
        }
    }

    /// <summary>
    /// The cloth ribbons and charms hanging from a held prop (Moa's shield and staff; Rickard 2026-11-13: "moa kan vi animera tyg på
    /// sköld och vapen också?"). The props are plain meshes (Props, the old game's format) on a MeshRenderer, so the ribbons are bent
    /// on the CPU: each hanging group is one damped pendulum as in ClothSway, and its vertices turn about the group's tie point by
    /// their share. A few hundred vertices a frame on a copy of the mesh per view; the prop's own placement (Weapons, WeaponTune.json)
    /// is untouched. The groups are measured on the meshes (bam-work/cloth/round7/), in the mesh's own space: bottom at y = 0,
    /// fractions of its height.
    /// </summary>
    sealed class PropSway
    {
        readonly Transform t, body;
        readonly Mesh mesh;
        readonly Vector3[] baseV, outV;
        readonly int[] grp;
        readonly float[] wt;
        readonly Vector3[] pivotL, restL;
        readonly Vector3[] tip, vel, lastPivot, lastTarget;
        readonly bool[] live;
        readonly float hz, damping, maxAngle, drag;
        float kickGain = 1f;
        Vector3 lastBody, runDir, lastFwd;
        float runSpeed, runPeak, bodyYaw;
        bool bodyLive;
        bool wasOn = true;
        struct Push { public float At, Fwd, Up; }
        readonly Push[] pushes = new Push[4];
        int pushN;

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>null when this prop has no hanging cloth, or its mesh can't be read.</summary>
        internal static PropSway For(GameObject go, string model, Transform body)
        {
            var mf = go.GetComponent<MeshFilter>();
            Mesh src = mf != null ? mf.sharedMesh : null;
            if (src == null || !src.isReadable) return null;
            // Moa's staff: none (Rickard 2026-11-13 after testing: "ta bort textil animation på staven det blev buggat"); its branch below stays
            // for reference but is never reached
            bool v2 = model == "v2_moa_shield";
            if (model != "moa_shield" && !v2) return null;
            Vector3[] v = src.vertices;
            float H = Mathf.Max(1e-4f, src.bounds.size.y);
            int n = v.Length;
            var w = new float[n];
            var g = new int[n];
            int groups;
            // the V2 shield's two cream ribbons (Rickard 2026-11-18: "gör tygbitarna på skölden lätt rörliga"), hanging from the rim's
            // lower left and right (the rim's centre 0.574 up, radius 0.426; the ribbons 0.16-0.30 out to the sides, from 0.27 down to its
            // bottom, measured on v2_moa_shield.bytes): what lies beyond 0.97 of the rim, below 0.32 and out past the bottom stud
            Vector2 c2 = new Vector2(0f, 0.574f * H);
            float r2 = 0.426f * H;
            if (v2)
            {
                groups = 2;
                for (int i = 0; i < n; i++)
                {
                    if (v[i].y >= 0.32f * H || Mathf.Abs(v[i].x) < 0.12f * H) continue;
                    float d = Vector2.Distance(new Vector2(v[i].x, v[i].y), c2);
                    w[i] = Smooth((d - 0.97f * r2) / (0.10f * H)) * Smooth((0.32f * H - v[i].y) / (0.04f * H));
                    g[i] = v[i].x < 0f ? 0 : 1;
                }
            }
            else if (model == "moa_shield")
            {
                // three ribbons (and the charms on them) below the round shield (centre 0.63 up, radius 0.33): what lies beyond 0.92 of
                // the rim and below the centre, full 0.12 further out; left, middle and right each on its own spring
                groups = 3;
                var c = new Vector2(0f, 0.63f * H);
                for (int i = 0; i < n; i++)
                {
                    if (v[i].y >= c.y) continue;
                    float d = Vector2.Distance(new Vector2(v[i].x, v[i].y), c);
                    w[i] = Smooth((d - 0.92f * 0.33f * H) / (0.12f * H));
                    g[i] = v[i].x < -0.13f * H ? 0 : v[i].x > 0.13f * H ? 2 : 1;
                }
            }
            else
            {
                // the ribbon bundle tied below the crest on the shaft's right side (x > 0), from 0.71 of its height down to 0.2
                groups = 1;
                for (int i = 0; i < n; i++)
                {
                    float y = v[i].y / H;
                    if (y >= 0.72f) continue;
                    w[i] = Smooth((v[i].x / H + 0.01f) / 0.02f) * Smooth((0.71f - y) / 0.06f) * Smooth((y - 0.2f) / 0.03f);
                }
            }
            var sum = new Vector3[groups]; var ws = new float[groups];
            for (int i = 0; i < n; i++) if (w[i] > 0.005f) { sum[g[i]] += v[i] * w[i]; ws[g[i]] += w[i]; }
            var pivot = new Vector3[groups]; var rest = new Vector3[groups];
            int any = 0;
            for (int k = 0; k < groups; k++)
            {
                if (ws[k] < 1f) { ws[k] = 0f; continue; }
                any++;
                Vector3 mid = sum[k] / ws[k];
                if (v2)
                {
                    Vector2 dir = (new Vector2(mid.x, mid.y) - c2).normalized, at = c2 + dir * (0.97f * r2);
                    pivot[k] = new Vector3(at.x, at.y, mid.z);
                }
                else if (model == "moa_shield")
                {
                    // tied on at the rim, towards the group's middle
                    Vector2 c = new Vector2(0f, 0.63f * H), dir = (new Vector2(mid.x, mid.y) - c).normalized;
                    Vector2 at = c + dir * (0.92f * 0.33f * H);
                    pivot[k] = new Vector3(at.x, at.y, mid.z);
                }
                else pivot[k] = new Vector3(mid.x, 0.71f * H, mid.z);
                rest[k] = mid - pivot[k];
            }
            if (any == 0) return null;
            for (int i = 0; i < n; i++) if (w[i] > 0.005f && ws[g[i]] == 0f) w[i] = 0f;
            Mesh m = Object.Instantiate(src);
            m.name = src.name + " (cloth)";
            m.MarkDynamic();
            mf.sharedMesh = m;
            // the shield's ribbons (Rickard 2026-11-13 after testing: "moas sköld gör nästan ingenting nu"): softer, less damped and trailing
            // further than the body's cloth, pushed twice as hard, so they swing clearly when she runs, stops, turns and strikes
            // the V2 ribbons "lätt rörliga" (lightly): a little stiffer and a smaller swing than the old shield's
            if (v2) return new PropSway(go.transform, body, m, v, g, w, pivot, rest, 1.9f, 0.3f, 30f, 0.10f) { kickGain = 1.5f };
            return new PropSway(go.transform, body, m, v, g, w, pivot, rest, 1.7f, 0.22f, 45f, 0.14f) { kickGain = 2f };
        }

        PropSway(Transform t, Transform body, Mesh m, Vector3[] v, int[] g, float[] w, Vector3[] pivot, Vector3[] rest, float hz, float damping, float maxAngle, float drag)
        {
            this.t = t; this.body = body; mesh = m; baseV = v; outV = (Vector3[])v.Clone(); grp = g; wt = w;
            pivotL = pivot; restL = rest; this.hz = hz; this.damping = damping; this.maxAngle = maxAngle; this.drag = drag;
            int k = pivot.Length;
            tip = new Vector3[k]; vel = new Vector3[k]; lastPivot = new Vector3[k]; lastTarget = new Vector3[k]; live = new bool[k];
        }

        /// <summary>As ClothSway.Kick: the hero's own move pushes the ribbons.</summary>
        internal void Kick(float delay, float fwd, float up)
        {
            if (!AnimFix.ClothKicks) return;
            if (pushN == pushes.Length) { for (int i = 1; i < pushN; i++) pushes[i - 1] = pushes[i]; pushN--; }
            pushes[pushN++] = new Push { At = Mathf.Max(0f, delay), Fwd = fwd, Up = up };
        }

        internal void Tick(float dt, bool on)
        {
            if (t == null) return;
            if (!on)
            {
                if (wasOn) { wasOn = false; mesh.vertices = baseV; for (int k = 0; k < live.Length; k++) live[k] = false; }
                pushN = 0; bodyLive = false;
                return;
            }
            wasOn = true;
            if (dt <= 0f) return;
            Vector3 push = Vector3.zero;
            {
                // as ClothSway: a hard stop after a run throws the ribbons on past the shield; a fast turn swings them out
                Vector3 bp = body.position, d = bp - lastBody; d.y = 0f;
                if (!bodyLive) { bodyLive = true; d = Vector3.zero; lastFwd = body.forward; }
                lastBody = bp;
                float spd = d.magnitude / dt;
                if (spd < 12f)
                {
                    if (spd > 0.5f) runDir = d / (spd * dt);
                    runSpeed = Mathf.Lerp(runSpeed, spd, 1f - Mathf.Exp(-dt * 20f));
                    runPeak = Mathf.Max(runPeak * Mathf.Exp(-dt * 4f), runSpeed);
                    if (AnimFix.ClothKicks && runSpeed < 0.3f && runPeak > 1.5f) { push += runDir * (0.5f * Mathf.Clamp01(runPeak / 3f)); runPeak = 0f; }
                }
                else runPeak = 0f;
                float yaw = Vector3.SignedAngle(lastFwd, body.forward, Vector3.up);
                lastFwd = body.forward;
                if (AnimFix.ClothKicks && Mathf.Abs(yaw) / dt > 240f) push += body.right * (-Mathf.Sign(yaw) * 0.25f * Mathf.Clamp01(Mathf.Abs(yaw) / dt / 720f));
            }
            for (int i = 0; i < pushN;)
            {
                pushes[i].At -= dt;
                if (pushes[i].At > 0f) { i++; continue; }
                push += body.forward * pushes[i].Fwd + Vector3.up * pushes[i].Up;
                pushes[i] = pushes[--pushN];
            }
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 90f), 1, 6);
            float h = dt / steps, wq = 2f * Mathf.PI * hz, kq = wq * wq, cq = 2f * damping * wq;
            Quaternion inv = Quaternion.Inverse(t.rotation);
            var turn = new Quaternion[pivotL.Length];
            for (int k = 0; k < pivotL.Length; k++)
            {
                turn[k] = Quaternion.identity;
                Vector3 rest = t.TransformVector(restL[k]);
                float L = rest.magnitude;
                if (!(L > 1e-5f)) continue;
                Vector3 restN = rest / L;
                Vector3 pivot = t.TransformPoint(pivotL[k]);
                Vector3 target = pivot + rest;
                if (!live[k] || (pivot - lastPivot[k]).sqrMagnitude > 4f * L * L) // first frame, a respawn or a Blink: at rest
                {
                    live[k] = true; tip[k] = target; vel[k] = Vector3.zero; lastPivot[k] = pivot; lastTarget[k] = target;
                    continue;
                }
                Vector3 pv = (pivot - lastPivot[k]) / dt;
                Vector3 trail = -pv * drag;
                if (trail.sqrMagnitude > 0.16f * L * L) trail = trail.normalized * (0.4f * L);
                target += trail;
                vel[k] += push * (kickGain * L * wq * 0.6f);
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 tg = Vector3.Lerp(lastTarget[k], target, (float)i / steps);
                    vel[k] += ((tg - tip[k]) * kq - vel[k] * cq) * h;
                    tip[k] += vel[k] * h;
                }
                lastTarget[k] = target; lastPivot[k] = pivot;
                Vector3 dir = tip[k] - pivot;
                dir = dir.sqrMagnitude < 1e-10f ? restN : dir.normalized;
                float ang = Vector3.Angle(restN, dir);
                if (ang > maxAngle) dir = Vector3.Slerp(restN, dir, maxAngle / ang);
                tip[k] = pivot + dir * L;
                turn[k] = inv * Quaternion.FromToRotation(restN, dir) * t.rotation; // the same turn in the mesh's own space
            }
            for (int i = 0; i < baseV.Length; i++)
            {
                float w = wt[i];
                if (w <= 0f) continue;
                int k = grp[i];
                Vector3 b = baseV[i], p = pivotL[k];
                Vector3 moved = p + turn[k] * (b - p);
                outV[i] = b + (moved - b) * w;
            }
            mesh.vertices = outV;
        }
    }
}
