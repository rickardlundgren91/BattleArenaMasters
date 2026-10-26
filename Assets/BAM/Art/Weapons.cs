using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>One weapon held in a fist (ported from the old game's HeroMixamoWeapon.cs MX_WEAPON).</summary>
    sealed class WeaponDef
    {
        /// <summary>Props model (Resources/BAMArt/Props/&lt;Model&gt;.bytes + .jpg); it stands with its handle along +y from y = 0.</summary>
        public string Model;
        /// <summary>Length as a share of the figure's height, and where the fist holds it as a share of the length from the handle's end.</summary>
        public float Len, Grip;
        public bool Left;
        /// <summary>A staff held along the forearm in the bind pose, so it stands up when the arm hangs (Moa; old MX_ALONG).</summary>
        public bool Along;
        /// <summary>Degrees above level the weapon is turned to about its grip while the hero is not swinging (Bram; old MX_HOLD_UP); below level when negative (Tikta); 0 = left as the fist holds it.</summary>
        public float HoldUp;
        /// <summary>Degrees the held weapon also turns from straight ahead towards its own hand's side (Bram 30: beside the face; past 90 it leans back, Bonk 160: over the shoulder).</summary>
        public float HoldOut;
        /// <summary>Held up through the fist with its face to the front instead of its head along the swing (Moa's staff).</summary>
        public bool Upright;
        /// <summary>Leaves the hand when an attack or cast releases and comes back a moment later (Blast's bomb).</summary>
        public bool Thrown;
        /// <summary>Reverse grip: the weapon leaves the fist on the little-finger side instead of the thumb side.</summary>
        public bool Reverse;
        /// <summary>Degrees the weapon is turned about its own handle after it is placed (a flat fan turned to lie along the knuckles).</summary>
        public float Twist;
    }

    /// <summary>An off-hand shield in front of the left hand (old MX_SHIELD).</summary>
    sealed class ShieldDef
    {
        public string Model;
        /// <summary>Diameter as a share of the figure's height.</summary>
        public float Dia;
        /// <summary>Always carried at the side on the hand (Moa); otherwise held up in front while standing and at the side on the move.</summary>
        public bool AlwaysSide;
        /// <summary>AlwaysSide: degrees its face is turned from straight out towards the front, and how far its back sits off the hand (share of its height).</summary>
        public float Turn, Off;
    }

    sealed class GearDef
    {
        public WeaponDef[] Weapons;
        public ShieldDef Shield;
        /// <summary>Degrees both upper arms are turned out from the sides after the clips, so a broad body's arms do not cut through it (old MX_ARM_SPREAD).</summary>
        public float ArmSpread;
    }

    /// <summary>
    /// Rickard's Meshy/Tripo weapons in the imported heroes' hands, placed as in the old game (HeroMixamoWeapon.cs,
    /// View3DOwnHeroFx.cs MxNeedles). Numbers are the old game's, which Rickard tuned on screen (2026-10-16 .. 10-24).
    /// Only imported heroes (HeroArt) use these; the code-built bodies keep their RigDef props.
    /// </summary>
    static class Weapons
    {
        static readonly GearDef[] byHero = Make();

        public static GearDef For(HeroId hero) => (int)hero < byHero.Length ? byHero[(int)hero] : null;

        static GearDef[] Make()
        {
            var g = new GearDef[8];
            g[(int)HeroId.Fenn] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "fenn_sword", Len = 1.0f, Grip = 0.2f } },
                Shield = new ShieldDef { Model = "fenn_shield", Dia = 0.5f },
            };
            // arms out as in the old game (MX_ARM_SPREAD 22) and the hammer leaning up and back over his right shoulder, so it never
            // covers his face (Rickard 2026-10-26: "bram täcker sig själv"); leaning out only 30 it still stood in front of his face
            // from the menu stage's angle (turned 20 degrees, swaying 14)
            g[(int)HeroId.Bram] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "bram_hammer_new", Len = 0.8f, Grip = 0.3f, HoldUp = 50f, HoldOut = 130f } },
                ArmSpread = 22f,
            };
            // held by the shank just under the ring, so it hangs from the fist with the flukes down (Rickard 2026-10-16/19)
            g[(int)HeroId.Knut] = new GearDef { Weapons = new[] { new WeaponDef { Model = "knut_anchor", Len = 0.55f, Grip = 0.85f } } };
            // with her own clips the staff along the forearm hung beside the fist and the crest, face out at the side, read as a
            // torch from the front (Rickard 2026-10-26: "moa håller inte i sina vapen"): the staff now stands up through her right
            // fist, face to the front, and the crest sits on her left hand turned half to the front
            g[(int)HeroId.Moa] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "moa_staff", Len = 0.95f, Grip = 0.4f, Along = true, HoldUp = 82f, HoldOut = 8f, Upright = true } },
                Shield = new ShieldDef { Model = "moa_shield", Dia = 0.45f, AlwaysSide = true, Turn = 45f, Off = 0.12f },
            };
            // her ornate bow (Rickard's Tripo model 2026-10-24, old fig_ornateBow; Rickard 2026-10-26: "ja", put it in her hand),
            // gripped at the middle in the left fist like any weapon: the limbs leave the fist on the thumb side, so in her bow
            // clips (arm out, thumb up) it stands upright with the belly away from her. Along the forearm (as Moa's staff) it lay
            // flat along her outstretched arm (Rickard 2026-10-26: "håller konstigt i vapen"; checked in a Blender render of IdleBow)
            g[(int)HeroId.Tyra] = new GearDef { Weapons = new[] { new WeaponDef { Model = "tyra_bow", Len = 0.75f, Grip = 0.5f, Left = true } } };
            // her old needle daggers, one in each fist out of the thumb side (old View3DOwnHeroFx.cs MxNeedles before the Needle
            // Wing: NeedleMesh/NeedleTex as Props tikta_needle, NEEDLE_LEN 0.36, NEEDLE_GRIP 0.14; Rickard 2026-10-26: "tikta ska
            // ha sina gamla vapen, typ två daggers"); at rest the blades point down along her forearms, since her guard idle holds
            // a fist at her mouth and out of the thumb side the needle stuck into her face
            g[(int)HeroId.Tikta] = new GearDef
            {
                Weapons = new[]
                {
                    new WeaponDef { Model = "tikta_needle", Len = 0.36f, Grip = 0.14f, Left = true, HoldUp = -70f, HoldOut = 20f },
                    new WeaponDef { Model = "tikta_needle", Len = 0.36f, Grip = 0.14f, HoldUp = -70f, HoldOut = 20f },
                },
            };
            // the skull bomb he throws (the old game only flew it; held here so the throw reads)
            g[(int)HeroId.Blast] = new GearDef { Weapons = new[] { new WeaponDef { Model = "blast_bomb", Len = 0.22f, Grip = 0.3f, Thrown = true } } };
            // Bonk's wooden club with bone spikes (Rickard's Tripo model 2026-10-26), held low on the handle, far too big for him
            // (Rickard 2026-10-26: "klubban ska vara för stor för honom"); at rest it stands up and out to his right, a little back,
            // a clear silhouette beside him: up in front it covered his face, leaned far back (172) it went behind him when the menu
            // stage turned him, and hanging forward-down (-35) it melted into his pelt at the knees ("vridas framåt", "syns tydligt")
            g[(int)HeroId.Bonk] = new GearDef { Weapons = new[] { new WeaponDef { Model = "bonk_club", Len = 0.85f, Grip = 0.12f, HoldUp = 55f, HoldOut = 115f } } };
            return g;
        }

        // x and z of a shaft's centre line at height y (model units), from the vertices in a band round it; once per mesh (build time)
        static readonly Dictionary<Mesh, Vector2> shaftAxis = new Dictionary<Mesh, Vector2>();
        public static Vector3 ShaftAxis(Mesh m, float y)
        {
            if (!shaftAxis.TryGetValue(m, out Vector2 c))
            {
                Vector3[] v = m.vertices;
                float band = m.bounds.size.y * 0.06f, sx = 0f, sz = 0f;
                int n = 0;
                for (int i = 0; i < v.Length; i++) if (Mathf.Abs(v[i].y - y) < band) { sx += v[i].x; sz += v[i].z; n++; }
                c = n > 0 ? new Vector2(sx / n, sz / n) : Vector2.zero;
                shaftAxis[m] = c;
            }
            return new Vector3(c.x, 0f, c.y);
        }
    }

    /// <summary>
    /// One view's held weapons and shield. Built once in the bind pose and parented to the hand bones, so they follow every
    /// clip; <see cref="Tick"/> runs after the animation each frame (shield facing, Bram's hold angle, Blast's thrown bomb)
    /// and only writes transforms. The renderers and materials are handed to BodyView, so fades, hit flash and death go
    /// with the body. Weapons cast no shadows (as in the old game).
    /// </summary>
    sealed class HeldGear
    {
        public Renderer[] Rends;
        public Material[] Opaque, Fade;

        Transform root;
        // weapons
        WeaponDef[] defs;
        Transform[] wT, hand, index;
        Vector3[] bindP, gripL;
        Quaternion[] bindR;
        Vector3[] bindS;
        // shield
        ShieldDef sDef;
        Transform sAnchor, sModel, sHand;
        float sMeshH, sMeshD;
        // arms turned out after the clips
        float spread;
        Transform armR, armL;
        // thrown
        float hideIn = -1f, backIn = -1f;
        const float AwayFor = 0.45f, PopIn = 0.15f;

        public static HeldGear Build(GearDef g, Animator an, float figH)
        {
            if (g == null || an == null || an.avatar == null || !an.isHuman) return null;
            var h = new HeldGear { root = an.transform };
            var rends = new List<Renderer>();
            var op = new List<Material>();
            var fd = new List<Material>();
            int nw = g.Weapons != null ? g.Weapons.Length : 0;
            h.defs = new WeaponDef[nw];
            h.wT = new Transform[nw]; h.hand = new Transform[nw]; h.index = new Transform[nw];
            h.bindP = new Vector3[nw]; h.gripL = new Vector3[nw]; h.bindR = new Quaternion[nw]; h.bindS = new Vector3[nw];
            if (g.ArmSpread > 0f)
            {
                h.spread = g.ArmSpread;
                h.armR = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
                h.armL = an.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            }
            for (int i = 0; i < nw; i++) h.AddWeapon(i, g.Weapons[i], an, figH, rends, op, fd);
            if (g.Shield != null) h.AddShield(g.Shield, an, figH, rends, op, fd);
            if (rends.Count == 0) return null;
            h.Rends = rends.ToArray();
            h.Opaque = op.ToArray();
            h.Fade = fd.ToArray();
            return h;
        }

        static Renderer MakeRenderer(GameObject go, Mesh mesh, string model, List<Renderer> rends, List<Material> op, List<Material> fd)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            Texture tex = Props.Texture(model);
            if (tex == null) tex = Texture2D.whiteTexture;
            Material o = ArtMaterials.Instance(ArtMaterials.Lit), f = ArtMaterials.Instance(ArtMaterials.LitFade);
            o.SetTexture(ArtMaterials.BaseMap, tex); o.mainTexture = tex;
            f.SetTexture(ArtMaterials.BaseMap, tex); f.mainTexture = tex;
            r.sharedMaterial = o;
            rends.Add(r); op.Add(o); fd.Add(f);
            return r;
        }

        // as MxOwnWeapon: the fingers point along the hand, the handle leaves the fist on the thumb side, the head forward of the knuckles
        void AddWeapon(int i, WeaponDef W, Animator an, float H, List<Renderer> rends, List<Material> op, List<Material> fd)
        {
            defs[i] = W;
            Mesh mesh = Props.Mesh(W.Model);
            Transform hd = an.GetBoneTransform(W.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (mesh == null || hd == null) return;
            Transform ix = an.GetBoneTransform(W.Left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform th = an.GetBoneTransform(W.Left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Vector3 fingers = ix != null ? ix.position - hd.position : hd.TransformDirection(Vector3.up) * 0.05f;
            Vector3 fn = fingers.normalized;
            Vector3 up = th != null ? th.position - hd.position : root.up;
            Vector3 handle = up - fn * Vector3.Dot(up, fn);
            handle = handle.sqrMagnitude < 1e-10f ? root.up : handle.normalized;
            Vector3 palm = hd.position + fingers * 0.6f;
            if (W.Along) { handle = -fn; palm = hd.position + root.forward * (0.03f * H); fingers = root.forward; }
            if (W.Reverse) handle = -handle;
            float L = W.Len * H, mh = Mathf.Max(1e-4f, mesh.bounds.size.y);
            var go = new GameObject("Weapon " + W.Model);
            MakeRenderer(go, mesh, W.Model, rends, op, fd);
            Transform t = go.transform;
            t.SetParent(hd, false);
            t.rotation = Quaternion.LookRotation(fingers.normalized, handle);
            if (W.Twist != 0f) t.rotation *= Quaternion.AngleAxis(W.Twist, Vector3.up);
            t.position = palm - handle * (W.Grip * L);
            float hs = Mathf.Max(1e-6f, hd.lossyScale.y), k = L / mh / hs;
            t.localScale = new Vector3(k, k, k);
            wT[i] = t; hand[i] = hd; index[i] = ix;
            bindP[i] = t.localPosition; bindR[i] = t.localRotation; bindS[i] = t.localScale;
            float gy = W.Grip * mh;
            Vector3 ax = W.HoldUp != 0f ? Weapons.ShaftAxis(mesh, gy) : Vector3.zero; // a hammer's box is not centred on its shaft
            gripL[i] = new Vector3(ax.x, gy, ax.z);
        }

        // as MxOwnShield: on an anchor under the model, placed from the hand every frame (Tick), so it never lies flat with the wrist
        void AddShield(ShieldDef S, Animator an, float H, List<Renderer> rends, List<Material> op, List<Material> fd)
        {
            Mesh mesh = Props.Mesh(S.Model);
            Transform hd = an.GetBoneTransform(HumanBodyBones.LeftHand);
            if (mesh == null || hd == null) return;
            sDef = S; sHand = hd;
            var ao = new GameObject("ShieldAnchor");
            sAnchor = ao.transform;
            sAnchor.SetParent(root, false);
            var go = new GameObject("Shield " + S.Model);
            MakeRenderer(go, mesh, S.Model, rends, op, fd);
            sModel = go.transform;
            sModel.SetParent(sAnchor, false);
            sMeshH = Mathf.Max(1e-4f, mesh.bounds.size.y);
            sMeshD = mesh.bounds.size.z;
            float k = S.Dia * H / sMeshH / Mathf.Max(1e-6f, sAnchor.lossyScale.y);
            sModel.localScale = new Vector3(k, k, k);
            sModel.localPosition = -mesh.bounds.center * k; // the model's bottom is at y = 0: centre it on the anchor
        }

        /// <summary>Back in the hand, for a fresh life out of the pool.</summary>
        public void Reset()
        {
            hideIn = -1f; backIn = -1f;
            for (int i = 0; i < wT.Length; i++) if (wT[i] != null) wT[i].localScale = bindS[i];
        }

        /// <summary>A thrown weapon leaves the hand after <paramref name="delay"/> seconds (the release).</summary>
        public void Throw(float delay)
        {
            hideIn = Mathf.Max(0f, delay);
            backIn = -1f;
        }

        /// <param name="moveW">0 standing .. 1 on the move.</param>
        /// <param name="actW">How much an attack or cast clip is playing (0..1).</param>
        public void Tick(float dt, float moveW, float actW, bool dead)
        {
            float show = 1f;
            if (hideIn >= 0f)
            {
                hideIn -= dt;
                if (hideIn < 0f) { hideIn = -1f; backIn = AwayFor; }
            }
            if (backIn >= 0f)
            {
                backIn -= dt;
                show = backIn > PopIn ? 0f : 1f - Mathf.Max(0f, backIn) / PopIn;
                if (backIn < 0f) { backIn = -1f; show = 1f; }
            }
            if (spread > 0f) ArmSpread();
            for (int i = 0; i < wT.Length; i++)
            {
                Transform t = wT[i];
                if (t == null) continue;
                WeaponDef W = defs[i];
                if (W.Thrown) t.localScale = bindS[i] * show;
                if (W.HoldUp != 0f) HoldAngle(i, W, dead ? 0f : 1f - Mathf.Clamp01(actW));
            }
            if (sAnchor != null) ShieldFace(moveW);
        }

        // MxArmSpread: about the hero's forward axis, so a hanging arm swings out from the side (before the weapons read the hands)
        void ArmSpread()
        {
            Vector3 fwd = root.forward;
            if (armR != null) armR.rotation = Quaternion.AngleAxis(spread, fwd) * armR.rotation;
            if (armL != null) armL.rotation = Quaternion.AngleAxis(-spread, fwd) * armL.rotation;
        }

        // MxHoldAngle: the grip is set in the T-pose, so with the arm hanging a hammer would point straight ahead, flat; turned
        // about its grip to point forward and up, its head's long axis along the swing, and slid across the shaft into the palm
        void HoldAngle(int i, WeaponDef W, float w)
        {
            Transform t = wT[i];
            t.localPosition = bindP[i]; t.localRotation = bindR[i];
            if (w <= 0.001f) return;
            Vector3 gl = gripL[i];
            Vector3 fwd = root.forward, upW = root.up;
            Transform hd = hand[i], ix = index[i];
            Vector3 palm = hd.position + (ix != null ? (ix.position - hd.position) * 0.6f : Vector3.zero);
            Vector3 g0 = t.TransformPoint(gl);
            float a = W.HoldUp * Mathf.Deg2Rad, o = W.HoldOut * Mathf.Deg2Rad;
            Vector3 flat = fwd * Mathf.Cos(o) + (W.Left ? -root.right : root.right) * Mathf.Sin(o);
            Vector3 want = flat * Mathf.Cos(a) + upW * Mathf.Sin(a);
            Quaternion q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(t.up, want), w);
            t.SetPositionAndRotation(g0 + q * (t.position - g0), q * t.rotation);
            Vector3 sh = t.up.normalized, lax = W.Upright ? Vector3.zero : Vector3.Cross(root.right, sh);
            if (W.Upright)
            {
                // turned about its shaft so its face (the model's +z) looks the way the hero faces
                Vector3 tgt = Vector3.ProjectOnPlane(fwd, sh), fz = Vector3.ProjectOnPlane(t.forward, sh);
                if (tgt.sqrMagnitude > 1e-6f && fz.sqrMagnitude > 1e-6f)
                {
                    float ang = Vector3.SignedAngle(fz, tgt, sh) * w;
                    Vector3 gg = t.TransformPoint(gl);
                    Quaternion tw = Quaternion.AngleAxis(ang, sh);
                    t.SetPositionAndRotation(gg + tw * (t.position - gg), tw * t.rotation);
                }
            }
            if (lax.sqrMagnitude > 1e-6f)
            {
                lax.Normalize();
                if (Vector3.Dot(lax, t.right) < 0f) lax = -lax;
                float ang = Vector3.SignedAngle(t.right, lax, sh) * w;
                Vector3 gg = t.TransformPoint(gl);
                Quaternion tw = Quaternion.AngleAxis(ang, sh);
                t.SetPositionAndRotation(gg + tw * (t.position - gg), tw * t.rotation);
            }
            Vector3 d = palm - t.TransformPoint(gl);
            d -= sh * Vector3.Dot(d, sh); // slide across the shaft only, never along it
            t.position += d * w;
        }

        // MxShieldFace: standing, held up in front close to the arm; on the move, carried at the left side, face turned outwards
        // (an AlwaysSide shield sits on the hand instead)
        void ShieldFace(float moveW)
        {
            Transform fw = root;
            float D = sModel.lossyScale.y * sMeshH;
            Vector3 side = Vector3.ProjectOnPlane(sHand.position - fw.position, fw.up);
            side = side.sqrMagnitude > 1e-8f ? side.normalized : -fw.right;
            if (sDef.AlwaysSide)
            {
                // on the hand, its back against the fist, face turned part way from straight out to the front
                float a = sDef.Turn * Mathf.Deg2Rad;
                Vector3 dir = (side * Mathf.Cos(a) + fw.forward * Mathf.Sin(a)).normalized;
                float th = sModel.lossyScale.z * sMeshD;
                sAnchor.SetPositionAndRotation(sHand.position + dir * (th * 0.5f + D * sDef.Off), Quaternion.LookRotation(dir, fw.up));
                return;
            }
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveW));
            Quaternion stand = Quaternion.AngleAxis(-20f, fw.up) * Quaternion.LookRotation(fw.forward, fw.up);
            Quaternion walk = Quaternion.LookRotation(side, fw.up);
            Vector3 pStand = sHand.position + fw.forward * (D * 0.15f) + side * (D * 0.05f);
            Vector3 pWalk = sHand.position + side * (D * 0.3f);
            sAnchor.SetPositionAndRotation(Vector3.Lerp(pStand, pWalk, w), Quaternion.Slerp(stand, walk, w));
        }
    }
}
