using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The special-ready cue on the hero itself (Rickard 2026-11-16, after Tikta's glowing wings: "Tänk om varje hero hade något
    /// speciellt likt tikta som aktiveras när ens special är redo"; his cards: every hero, seen on enemies too, always on the menu
    /// stages). One look per hero, on a part it already wears or holds, in its own colours, made of small glows and motes only (no
    /// smoke, no fog, no text):
    /// - Tikta: her glowing wings beat faster and wider and shine brighter (HeldGear.ReadyBoost), teal and violet motes off them.
    /// - Fenn: a pale ghost light that breathes on his back, under the cape, with a few rising specks.
    /// - Ravna: a violet light on her back between the shoulders.
    /// - Knut: a sea-green shine on his anchor's head that drips.
    /// - Tyra: gold sparkles over the arrow tips in her quiver.
    /// - Bram: small blue sparks crackling on his hammer head.
    /// - Blast: the fuse of the bomb in his hand burns and sparks.
    /// - Moa: the honey head of her staff glows warm gold and drips honey.
    /// - Bonk: the sabre-tooth skull on his back gets ember eyes (his skins, without the skull: embers on the club head).
    /// - Kugg: his back cog turns four times as fast and glows in his look's colour; the chimney smokes twice as often.
    /// - Ragn: a pale moonlight round both fists (Moon Leap).
    /// - Nässla: her mushroom cap glows green and lets go of a few spores.
    /// Tikta, Fenn and Ravna have it behind them (Rickard 2026-11-16: "sneaky heroes har det bakom sig vilket gör att motståndaren
    /// inte vet om deras special är redo så länge de anfaller"; his card: these three): on an enemy it shows only while the hero
    /// turns its back to the camera. Display only: the rules' ready is MatchViews' (Unit.SpecialReady).
    /// </summary>
    sealed partial class BodyView
    {
        bool readyCue;
        float cueW, cueT;

        public override void SetReadyCue(bool on) => readyCue = on;

        static bool CueBehind(HeroId h) => h == HeroId.Tikta || h == HeroId.Fenn || h == HeroId.Ravna;

        void ReadyCueTick(float dt)
        {
            if (HeroIndex < 0 || anim == null || !anim.isHuman) return;
            var h = (HeroId)HeroIndex;
            // on the menu stages always, but not Tyra: her quiver's sparkles blinked on Home (Rickard 2026-11-16 "tyras pilar blinkar i
            // huvudmenyn, bort med det"), and not Fenn: his ghost light and specks looked bad up close on Home (Rickard 2026-11-16 "bort
            // med partiklar från fenn phantom från menyn som med tyra"); in a match both keep it
            bool on = (readyCue || Preview && h != HeroId.Tyra && h != HeroId.Fenn) && !dead;
            if (on && !Preview && marker == UnitMarker.Enemy && CueBehind(h))
            {   // behind them: the enemy's cue shows only while its back is turned to the camera
                Vector3 cf = ArtDriver.CamRot * Vector3.forward, f = transform.forward;
                cf.y = 0f; f.y = 0f;
                on = cf.sqrMagnitude > 1e-6f && f.sqrMagnitude > 1e-6f && Vector3.Dot(f.normalized, cf.normalized) > 0.25f;
            }
            cueW = Mathf.MoveTowards(cueW, on ? 1f : 0f, dt * 4f);
            if (gear != null) gear.ReadyBoost = cueW;
            if (cueW <= 0.05f || !renderersOn || vis < 0.3f) return;
            if ((cueT -= dt) > 0f) return;
            cueT = ArtKit.High ? 0.09f : 0.16f;
            CueEmit(h, cueW);
        }

        // a point a share of the hero's height from a bone, in the hero's own axes (x right, y up, z forward)
        bool CueBone(HumanBodyBones b, Vector3 off, out Vector3 at)
        {
            Transform t = anim.GetBoneTransform(b);
            if (t == null && b == HumanBodyBones.UpperChest) t = anim.GetBoneTransform(HumanBodyBones.Chest);
            at = t != null ? t.position + anim.transform.rotation * off * ShownHeight : Vector3.zero;
            return t != null;
        }

        bool CueWeaponHead(out Vector3 at)
        {
            at = Vector3.zero;
            if (gear == null || !gear.WeaponShown(0)) return false;
            gear.Ends(0, out _, out at);
            return true;
        }

        static Color32 Fade(Color32 c, float w) => new Color32(c.r, c.g, c.b, (byte)(c.a * Mathf.Clamp01(w)));

        static readonly Color32 FennLight = new Color32(160, 225, 255, 150), FennSpeck = new Color32(225, 245, 255, 220),
            TiktaTeal = new Color32(97, 255, 224, 210), TiktaViolet = new Color32(175, 105, 255, 210),
            RavnaLight = new Color32(150, 90, 255, 150), RavnaSpeck = new Color32(205, 170, 255, 210),
            KnutShine = new Color32(70, 230, 190, 160), KnutDrop = new Color32(130, 240, 220, 210),
            TyraGold = new Color32(255, 212, 95, 230), TyraHalo = new Color32(255, 190, 80, 120),
            BramSpark = new Color32(130, 195, 255, 255), BramHalo = new Color32(90, 160, 255, 130),
            BlastSpark = new Color32(255, 175, 60, 255), BlastFlame = new Color32(255, 215, 110, 220),
            MoaHoney = new Color32(255, 190, 60, 160), MoaDrop = new Color32(255, 165, 30, 230),
            BonkEmber = new Color32(255, 95, 40, 235),
            RagnMoon = new Color32(210, 225, 255, 190),
            NasslaGlow = new Color32(140, 255, 120, 150), NasslaSpore = new Color32(195, 255, 150, 220);

        void CueEmit(HeroId h, float w)
        {
            Vector3 p;
            switch (h)
            {
                case HeroId.Tikta:
                    if (CueBone(HumanBodyBones.UpperChest, new Vector3(0f, -0.02f, -0.12f), out p))
                        Fx.Glow(p, 1, Fade(Random.value < 0.5f ? TiktaTeal : TiktaViolet, w), 0.1f, 0.15f, 0.55f, 0.2f, 0.28f);
                    break;
                case HeroId.Fenn:
                    if (CueBone(HumanBodyBones.UpperChest, new Vector3(0f, -0.04f, -0.11f), out p))
                    {
                        float breath = 0.75f + 0.25f * Mathf.Sin(idleT * 2.4f);
                        Fx.Glow(p, 1, Fade(FennLight, w * breath), 0.34f, 0.03f, 0.3f, 0f, 0.03f);
                        if (Random.value < 0.35f) Fx.Stars(p, 1, Fade(FennSpeck, w), 0.07f, 0.15f, 0.7f, 0.5f, 0.12f);
                    }
                    break;
                case HeroId.Ravna:
                    if (CueBone(HumanBodyBones.UpperChest, new Vector3(0f, -0.03f, -0.11f), out p))
                    {
                        Fx.Glow(p, 1, Fade(RavnaLight, w), 0.32f, 0.03f, 0.3f, 0f, 0.03f);
                        if (Random.value < 0.3f) Fx.Glow(p, 1, Fade(RavnaSpeck, w), 0.07f, 0.2f, 0.6f, 0.4f, 0.15f);
                    }
                    break;
                case HeroId.Knut:
                    if (CueWeaponHead(out p))
                    {
                        Fx.Glow(p, 1, Fade(KnutShine, w), 0.3f, 0.03f, 0.3f, 0f, 0.04f);
                        if (Random.value < 0.25f) Fx.Drops(p, 1, Fade(KnutDrop, w), 0.06f, 0.1f, 0.5f, -1.2f, 0.06f);
                    }
                    break;
                case HeroId.Tyra:
                    if (gear != null && gear.WornPoint(gear.FindWorn("quiver"), new Vector3(0f, 1f, 0f), out p)
                        || CueBone(HumanBodyBones.UpperChest, new Vector3(0.03f, 0.08f, -0.08f), out p))
                    {
                        Fx.Glow(p, 1, Fade(TyraHalo, w), 0.2f, 0.03f, 0.3f, 0f, 0.03f);
                        if (Random.value < 0.6f) Fx.Stars(p, 1, Fade(TyraGold, w), 0.07f, 0.1f, 0.45f, 0.2f, 0.07f);
                    }
                    break;
                case HeroId.Bram:
                    if (CueWeaponHead(out p))
                    {
                        Fx.Glow(p, 1, Fade(BramHalo, w), 0.28f, 0.03f, 0.3f, 0f, 0.04f);
                        if (Random.value < 0.7f) Fx.Sparks(p, 2, Fade(BramSpark, w), 0.05f, 1.1f, 0.22f, 0f, 0.08f);
                    }
                    break;
                case HeroId.Blast:
                    if (CueWeaponHead(out p))
                    {
                        Fx.Glow(p, 1, Fade(BlastFlame, w), 0.13f, 0.05f, 0.2f, 0.2f, 0.02f);
                        Fx.Sparks(p, 1, Fade(BlastSpark, w), 0.045f, 0.9f, 0.3f, 1.2f, 0.03f);
                    }
                    break;
                case HeroId.Moa:
                    if (CueWeaponHead(out p))
                    {
                        Fx.Glow(p, 1, Fade(MoaHoney, w), 0.34f, 0.03f, 0.3f, 0f, 0.04f);
                        if (Random.value < 0.2f) Fx.Drops(p, 1, Fade(MoaDrop, w), 0.07f, 0.05f, 0.6f, -1.5f, 0.07f);
                    }
                    break;
                case HeroId.Bonk:
                {
                    int sk = gear != null ? gear.FindWorn("skull") : -1;
                    // the skull is made facing +z (turned round onto his back): its eyes on that face, a little over its middle
                    if (sk >= 0 && gear.WornPoint(sk, new Vector3(-0.3f, 0.25f, 0.9f), out Vector3 eL) && gear.WornPoint(sk, new Vector3(0.3f, 0.25f, 0.9f), out Vector3 eR))
                    {
                        Fx.Glow(eL, 1, Fade(BonkEmber, w), 0.1f, 0.01f, 0.25f, 0f, 0.005f);
                        Fx.Glow(eR, 1, Fade(BonkEmber, w), 0.1f, 0.01f, 0.25f, 0f, 0.005f);
                    }
                    else if (CueWeaponHead(out p))
                    {
                        Fx.Glow(p, 1, Fade(BonkEmber, w * 0.7f), 0.3f, 0.03f, 0.3f, 0f, 0.05f);
                        if (Random.value < 0.4f) Fx.Sparks(p, 1, Fade(BonkEmber, w), 0.05f, 0.5f, 0.4f, 0.8f, 0.1f);
                    }
                    break;
                }
                case HeroId.Kugg:
                    if (gear != null && gear.WornPoint(gear.FindWorn("gear"), Vector3.zero, out p))
                    {
                        Color32 c = KuggSmokeColor[Mathf.Clamp(Skin, 0, KuggSmokeColor.Length - 1)];
                        Fx.Glow(p, 1, Fade(new Color32(c.r, c.g, c.b, 150), w), 0.42f, 0.03f, 0.3f, 0f, 0.04f);
                    }
                    break;
                case HeroId.Ragn:
                    if (CueBone(HumanBodyBones.LeftHand, Vector3.zero, out p)) Fx.Glow(p, 1, Fade(RagnMoon, w), 0.17f, 0.05f, 0.3f, 0.1f, 0.03f);
                    if (CueBone(HumanBodyBones.RightHand, Vector3.zero, out p)) Fx.Glow(p, 1, Fade(RagnMoon, w), 0.17f, 0.05f, 0.3f, 0.1f, 0.03f);
                    break;
                case HeroId.Nassla:
                    if (gear != null && gear.WornPoint(gear.FindWorn("cap"), new Vector3(0f, 0.5f, 0f), out p)
                        || CueBone(HumanBodyBones.Head, new Vector3(0f, 0.08f, 0f), out p))
                    {
                        Fx.Glow(p, 1, Fade(NasslaGlow, w), 0.34f, 0.03f, 0.3f, 0f, 0.04f);
                        if (Random.value < 0.25f) Fx.Stars(p, 1, Fade(NasslaSpore, w), 0.05f, 0.12f, 0.9f, 0.35f, 0.15f);
                    }
                    break;
            }
        }
    }
}
