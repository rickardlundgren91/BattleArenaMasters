using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Cloth and hair that swing after the body (tester idea 2026-11-06, thread "Add cloth and hair simulation to heroes"):
    /// Fenn's cape and tabard, Tyra's ponytail. The Tripo/Meshy bodies are one shell on a plain Mixamo skeleton, so there are
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
        enum Kind { Skirt, Tail }

        /// <summary>One cloth part of one hero. Distances are fractions of the model's height in its bind pose.</summary>
        sealed class Spec
        {
            public Kind Kind;
            public int Sectors = 6;          // Skirt: spring bones round the hips
            public float Clear0, Clear1;     // Skirt: distance from the leg bones where the sway starts and is full
            public float Drop = 0.12f;       // Skirt: how far below the hips the sway reaches full
            public float Back0, Back1;       // Tail: distance behind the head bone where the sway starts and is full
            public float PivotUp;            // Tail: the tie point, this far above the head bone (on the Back0 plane)
            public float Hz = 2.4f;          // spring frequency
            public float Damping = 0.4f;     // share of critical damping (lower = more overshoot)
            public float MaxAngle = 28f;     // degrees the part may swing from its rest
            public float Drag = 0.05f;       // seconds of the hips' or head's velocity it trails by
            public float Flare;              // Skirt: outward swing at a full spin (share of its length)
            public float Inward = 0.12f;     // Skirt: how far a part may swing in towards the legs (share of its length)
        }

        static Spec[] SpecsFor(int hero)
        {
            switch (hero)
            {
                case 1: // Fenn (the Original body): the cape behind and the tabard in front, both hanging free of the legs
                    return new[] { new Spec { Kind = Kind.Skirt, Sectors = 6, Clear0 = 0.055f, Clear1 = 0.11f, Drop = 0.12f, Hz = 2.2f, Damping = 0.45f, MaxAngle = 26f, Drag = 0.05f, Flare = 0.22f } };
                case 2: // Tyra: the ponytail
                    return new[] { new Spec { Kind = Kind.Tail, Back0 = 0.04f, Back1 = 0.085f, PivotUp = 0.12f, Hz = 2.6f, Damping = 0.3f, MaxAngle = 35f, Drag = 0.06f } };
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
        bool wasOn = true;

        ClothSway(Spring[] s, Transform body) { springs = s; this.body = body; lastFwd = body.forward; }

        /// <summary>null when this hero has no cloth parts, or its model can't be read.</summary>
        internal static ClothSway For(Animator an, Renderer[] rends, int heroIndex)
        {
            Spec[] specs = SpecsFor(heroIndex);
            if (specs == null || an == null || !an.isHuman || rends == null) return null;
            SkinnedMeshRenderer smr = null;
            for (int i = 0; i < rends.Length && smr == null; i++) smr = rends[i] as SkinnedMeshRenderer;
            if (smr == null || smr.sharedMesh == null) return null;
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
            // only the bodies HeroArtImport.ClothModel imports with Read/Write on (Fenn's Clay body is not one: its cloth is part
            // of the leg shell, so it would bend his thighs)
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
                    var sw = new float[n];
                    var sa = new float[n]; // the vertex's angle round the hips, in sectors
                    var sum = new Vector3[N];
                    var wsum = new float[N];
                    for (int i = 0; i < n; i++)
                    {
                        float below = -Vector3.Dot(v[i] - hp, up);
                        if (below <= 0f || Share(i, setA) < 0.8f) continue; // above the hips, or an arm hanging past them
                        float d = Mathf.Min(Mathf.Min(SegDist(v[i], aL, bL), SegDist(v[i], bL, cL)), Mathf.Min(SegDist(v[i], aR, bR), SegDist(v[i], bR, cR))) / H;
                        float s = Smooth((d - sp.Clear0) / Mathf.Max(1e-4f, sp.Clear1 - sp.Clear0)) * Smooth(below / (sp.Drop * H));
                        if (s <= 0.005f) continue;
                        Vector3 h = v[i] - hp; h -= up * Vector3.Dot(h, up);
                        float ang = Mathf.Atan2(Vector3.Dot(h, right), Vector3.Dot(h, fwd)); // 0 = straight ahead
                        float a = Mathf.Repeat(ang / (2f * Mathf.PI) * N, N);
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
                        boneOf[k] = bones.Length + defs.Count;
                        defs.Add(new BoneDef
                        {
                            Parent = hips, Pivot = pivotL, Rest = bind[hips].MultiplyVector(rest), Spec = sp,
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

        /// <summary>After everything else has posed the body this frame (BodyView.LateUpdate). dt: the view's frame time.</summary>
        internal void Tick(float dt, bool on)
        {
            if (!on)
            {
                if (wasOn) { wasOn = false; foreach (Spring s in springs) { s.T.localRotation = Quaternion.identity; s.Live = false; } }
                return;
            }
            wasOn = true;
            if (dt <= 0f) return;
            Vector3 fwd = body.forward;
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
                    continue;
                }
                // air: the part trails the body's motion and flares out in a spin
                Vector3 trail = -vel * sp.Drag;
                if (trail.sqrMagnitude > 0.16f * L * L) trail = trail.normalized * (0.4f * L);
                target += trail + outW * (sp.Flare * spin * L);
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
}
