using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    // Ravna's Night Flight (Rickard 2026-11-16: "Ravna ska bli kråkor vid ult"; ABC2's Ron dissolved into a flock the same way): while
    // the rules fly her (Hero8.cs RavnaFlying) her body and gear are hidden and a flock of her own crows (Rickard's Tripo crow, the one
    // on her shoulder: Props v2_ravna_crow, ravna_crow if that is missing) swirls along her path instead. The crows burst out of her in
    // FlockGrow s, circle round her spot as it flies (so they turn with her when she steers), beat their wings (the model is a perched
    // crow, so a beat is a quick squeeze across the wings and a bob) and fly in a dark violet magic mist with wisps, amber glints and
    // falling feathers (Rickard 2026-11-16 "dimmigt, mystiskt, fåglar, magiskt"). When she lands they fly back into
    // her as her body returns (the puff and caw are MatchViews' FlockEnd). Nothing changes in the rules: this is only the look.
    sealed partial class BodyView
    {
        const int FlockMax = 22, FlockLow = 14; // Rickard 2026-11-17 "tätare och större flock" (was 8 / 6)
        const float FlockGrow = 0.14f, FlockShrink = 0.18f;
        const float CrowSize = 0.27f;  // a crow's longest side over her height (0.3 with 8 crows; a little smaller now there are many)
        const float CrowPitch = -45f;  // the perched model stands about 55 degrees up from tail to head: this lays it nearly flat, beak first and a touch down

        Transform[] crowT, crowM;      // each crow's place (turns with the flight) and its mesh under it (the wing beat)
        Material crowOpaque, crowFade;
        MeshRenderer[] crowR;
        float[] crowA, crowRad, crowH, crowSpin, crowPh;
        int crowN;
        bool flockOn, flockShown, flockFading;
        float flockK, flockT, featherT, mistT, wispT2, crowScale;
        static readonly Color32 MistDark = new Color32(34, 26, 52, 150), MistViolet = new Color32(96, 64, 150, 90),
            WispViolet = new Color32(150, 100, 235, 200), EyeAmber = new Color32(255, 180, 70, 255);
        Vector3 crowCentre;

        bool FlockHidesBody => (flockOn || flockK > 0.5f) && !dead;

        void SetFlock(bool on)
        {
            if (on && crowT == null) BuildFlock();
            if (crowT == null) on = false; // the crow model is missing: she keeps her own body
            if (on && !flockOn) { flockT = 0f; FlockBurst(); }
            if (!on && flockOn && renderersOn && vis >= 0.99f && !dead) FlockLand(transform.position, ShownHeight);
            flockOn = on;
        }

        void BuildFlock()
        {
            string model = "v2_ravna_crow";
            Mesh mesh = Props.Mesh(model);
            if (mesh == null) { model = "ravna_crow"; mesh = Props.Mesh(model); }
            if (mesh == null || def == null) return;
            Texture tex = Props.Texture(model);
            if (tex == null) tex = Texture2D.whiteTexture;
            crowOpaque = ArtMaterials.Instance(ArtMaterials.Lit);
            crowOpaque.SetTexture(ArtMaterials.BaseMap, tex); crowOpaque.mainTexture = tex;
            crowFade = ArtMaterials.Instance(ArtMaterials.LitFade);
            crowFade.SetTexture(ArtMaterials.BaseMap, tex); crowFade.mainTexture = tex;

            Vector3 sz = mesh.bounds.size;
            crowScale = CrowSize * def.Height / Mathf.Max(0.01f, Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)));
            crowCentre = mesh.bounds.center;
            Quaternion lay = Quaternion.Euler(CrowPitch, 180f, 0f);

            crowN = ArtKit.High ? FlockMax : FlockLow;
            crowT = new Transform[crowN]; crowM = new Transform[crowN]; crowR = new MeshRenderer[crowN];
            crowA = new float[crowN]; crowRad = new float[crowN]; crowH = new float[crowN]; crowSpin = new float[crowN]; crowPh = new float[crowN];
            for (int i = 0; i < crowN; i++)
            {
                var go = new GameObject("Ravna crow " + i);
                crowT[i] = go.transform;
                crowT[i].SetParent(transform, false);
                var mg = new GameObject("mesh");
                crowM[i] = mg.transform;
                crowM[i].SetParent(crowT[i], false);
                crowM[i].localRotation = lay;
                crowM[i].localPosition = -(lay * crowCentre) * crowScale; // turns about its own middle, not its feet
                mg.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = mg.AddComponent<MeshRenderer>();
                r.sharedMaterial = crowOpaque;
                r.shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                crowR[i] = r;
                // a loose cloud: evenly round her, each at its own distance, height, speed and wing beat
                crowA[i] = i * Mathf.PI * 2f / crowN + Random.value * 0.5f;
                crowRad[i] = def.Height * (0.12f + 0.6f * Mathf.Sqrt(Random.value)); // a filled cloud up to 0.72 of her height out, not a ring
                crowH[i] = def.Height * (0.15f + 0.95f * ((i * 0.618f) % 1f));
                crowSpin[i] = (i % 2 == 0 ? 1f : -1f) * (1.6f + 0.9f * Random.value);
                crowPh[i] = Random.value * 10f;
                go.SetActive(false);
            }
        }

        // every frame (from FormLook): the flock grows out of her, swirls along and flies back in as she lands
        void FlockLook(float dt)
        {
            if (crowT == null) return;
            flockK = Mathf.MoveTowards(flockK, flockOn && !dead ? 1f : 0f, dt / (flockOn ? FlockGrow : FlockShrink));
            bool show = flockK > 0f && renderersOn && !dead;
            if (show != flockShown)
            {
                flockShown = show;
                for (int i = 0; i < crowN; i++) crowT[i].gameObject.SetActive(show);
            }
            if (!show) return;
            flockT += dt;

            // see-through like her body when only her own team sees her (in grass), so the crows never give her away more than she would
            bool fade = vis < 0.99f;
            if (fade != flockFading)
            {
                flockFading = fade;
                for (int i = 0; i < crowN; i++)
                {
                    crowR[i].sharedMaterial = fade ? crowFade : crowOpaque;
                    crowR[i].shadowCastingMode = !fade && ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
            }
            if (fade) crowFade.SetColor(ArtMaterials.BaseColor, new Color(1f, 1f, 1f, vis));

            float spread = BackOut(flockK); // a little overshoot as they burst out, then they settle
            for (int i = 0; i < crowN; i++)
            {
                float t = flockT, ph = crowPh[i];
                float a = crowA[i] + t * crowSpin[i];
                float rad = crowRad[i] * (1f + 0.25f * Mathf.Sin(t * 2.3f + ph)) * spread;
                float y = Mathf.Lerp(def.Height * 0.5f, crowH[i] + def.Height * 0.07f * Mathf.Sin(t * 6f + ph), flockK);
                // stretched along the flight (z), so it reads as a stream of birds and not a ring
                crowT[i].localPosition = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad * 1.5f);
                // beak along the flight, leaning into its circle, nose bobbing with the beat
                float beat = Mathf.Sin(t * 20f + ph);
                crowT[i].localRotation = Quaternion.Euler(6f * beat, 14f * Mathf.Sin(t * 3f + ph), -24f * Mathf.Sign(crowSpin[i]) * Mathf.Cos(a));
                // the wing beat: the folded wings squeeze and widen across the body (mesh x)
                crowM[i].localScale = new Vector3(crowScale * (1f + 0.3f * beat), crowScale * (1f - 0.08f * beat), crowScale);
            }

            // the magic round the flock (Rickard 2026-11-16: "mer effekter, rök eller dimma ... dimmigt, mystiskt, fåglar, magiskt"): a
            // dark violet mist rolls off it and trails behind, pale violet wisps curl up out of it, amber motes (her eyes' colour) glint
            // in it, and dark feathers fall behind (none of it while see-through: only her own team sees her then)
            if (!fade && flockOn)
            {
                Vector3 mid = transform.position + new Vector3(0f, ShownHeight * 0.45f, 0f);
                Vector3 back = -transform.forward;
                if ((mistT -= dt) <= 0f)
                {
                    mistT = ArtKit.High ? 0.035f : 0.07f;
                    // the mist: big soft puffs round the flock and a little behind it, so it leaves a trail along the path
                    Vector3 at = mid + back * (ShownHeight * 0.25f) + new Vector3((Random.value - 0.5f) * ShownHeight * 1.1f, (Random.value - 0.5f) * ShownHeight * 0.6f, (Random.value - 0.5f) * ShownHeight * 1.3f);
                    Fx.Smoke(at, 1, MistDark, ShownHeight * (0.7f + 0.45f * Random.value), 0.15f, 0.9f, 0.08f, 0.1f);
                    if (Random.value < 0.5f) Fx.Smoke(mid + back * (ShownHeight * 0.1f), 1, MistViolet, ShownHeight * 0.45f, 0.25f, 0.6f, 0.05f, ShownHeight * 0.2f);
                }
                if ((wispT2 -= dt) <= 0f)
                {
                    wispT2 = ArtKit.High ? 0.08f : 0.16f;
                    var c = crowT[(int)(Random.value * crowN) % crowN];
                    Fx.Wisps(c.position, 1, WispViolet, ShownHeight * 0.28f, 0.2f, 0.7f, 0.5f, 0.08f);
                    if (Random.value < 0.45f) Fx.Glow(c.position + back * 0.1f, 1, EyeAmber, 0.07f, 0.15f, 0.5f, 0.15f, 0.12f);
                }
                if ((featherT -= dt) <= 0f)
                {
                    featherT = ArtKit.High ? 0.07f : 0.14f;
                    var c = crowT[(int)(Random.value * crowN) % crowN];
                    Fx.Leaves(c.position, 1, new Color32(28, 24, 34, 230), 0.14f, 0.25f, 0.7f, 0.05f, 0.1f);
                }
            }
        }

        // the moment she bursts into crows: a cloud of dark violet mist and feathers on her spot, wisps curling up out of it and a
        // dark swirl on the ground (FlockLand when she lands; the puff and caw there are MatchViews' FlockEnd)
        void FlockBurst()
        {
            if (!renderersOn || vis < 0.99f || dead) return;
            Vector3 mid = transform.position + new Vector3(0f, ShownHeight * 0.5f, 0f);
            Fx.Smoke(mid, ArtKit.High ? 10 : 5, MistDark, ShownHeight * 0.7f, 1.1f, 0.9f, 0.2f, ShownHeight * 0.2f);
            Fx.Wisps(mid, ArtKit.High ? 8 : 4, WispViolet, ShownHeight * 0.35f, 0.9f, 0.8f, 0.6f, 0.15f);
            Fx.Leaves(mid, ArtKit.High ? 14 : 8, new Color32(28, 24, 34, 235), 0.18f, 1.2f, 0.6f, 0.3f, 0.15f);
            Fx.Swirl(transform.position, ShownHeight * 0.7f, new Color(0.45f, 0.3f, 0.75f, 0.7f), 0.7f);
        }

        // she lands out of her crows: the mist and wisps gather where she stands again
        void FlockLand(Vector3 feet, float height)
        {
            Vector3 mid = feet + new Vector3(0f, height * 0.5f, 0f);
            Fx.Smoke(mid, ArtKit.High ? 8 : 4, MistDark, height * 0.6f, 0.7f, 0.8f, 0.1f, height * 0.2f);
            Fx.Wisps(mid, ArtKit.High ? 6 : 3, WispViolet, height * 0.3f, 0.5f, 0.7f, 0.7f, 0.12f);
            Fx.Swirl(feet, height * 0.6f, new Color(0.45f, 0.3f, 0.75f, 0.6f), 0.6f);
        }

        void FlockReset()
        {
            flockOn = false; flockK = 0f; flockT = 0f; mistT = wispT2 = featherT = 0f;
            if (crowT != null && flockShown) for (int i = 0; i < crowN; i++) crowT[i].gameObject.SetActive(false);
            flockShown = false;
        }
    }
}
