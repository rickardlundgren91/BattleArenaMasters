using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>A rest hold as <see cref="WeaponDef.HoldUp"/>, <see cref="WeaponDef.HoldOut"/> and <see cref="WeaponDef.RestTurn"/>
    /// (AnimFix.FixedGrip); off while Up is 0.</summary>
    struct Hold
    {
        public float Up, Out;
        public Vector3 Turn;
        public bool On => Up != 0f;
    }

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
        /// <summary>With a hold angle (AnimFix.FixedGrip): how much of the hand's own turn away from its idle the weapon follows at
        /// rest; 0 stands it still in the hero's space whatever the hand does, 1 fixes it in the fist (both looked wrong in the
        /// run renders of 2026-10-28: stiff, or tipped flat by the pumping fist).</summary>
        public float Follow = 0.25f;
        /// <summary>AnimFix.FixedGrip: through a swing or cast the weapon keeps its rest aim, following <see cref="SwingFollow"/> of the
        /// hand, instead of going over to the fist's own grip. For weapons the borrowed clips were not made for: in the fist's grip
        /// Moa's staff flipped up to 170 degrees and Tikta's needles 100-150 at every swing (Blender renders 2026-10-28, bam-work/art/
        /// rigg-anim/sving); a fist-made slash (Fenn, Bram, Bonk) keeps the fist's grip.</summary>
        public bool SwingAim;
        public float SwingFollow = 0.5f;
        /// <summary>AnimFix.FixedGrip: degrees turned about the grip in the weapon's own x/y/z while an attack plays, with its weight
        /// (Tyra's bow stands up for the shot; in the fist's own grip it lay flat). Not in a cast: Tyra's cast is a held draw, made for
        /// a bow, upright in the fist's own grip.</summary>
        public Vector3 SwingTurn;
        /// <summary>AnimFix.FixedGrip: degrees turned about the grip in the weapon's own x/y/z at rest, with the rest weight, before
        /// Rickard's Turn (Tyra's bow lies level at her hip with the string up, as his pictures of 2026-10-28).</summary>
        public Vector3 RestTurn;
        /// <summary>AnimFix.FixedGrip, code only (not in WeaponTune.json): the hold eased in with the move weight while the hero walks
        /// or runs (Tyra's bow upright at her side, Rickard 2026-10-29).</summary>
        public Hold Run;
        /// <summary>Code only: on the menu stage the weapon is held this way instead of the rest hold, its flat side to
        /// <see cref="MenuSign"/> (Tyra's bow hanging from her fist, Rickard's pick 2026-10-29). Aimed in the hero's own space: left
        /// in the fist's own grip, Unity's retargeted idle turned her hand so the bow lay level across her legs (ANIM-22).</summary>
        public Hold Menu;
        public int MenuSign;
        /// <summary>Which way the flat side of the run hold goes: 0 = as the idle votes (BAM's own pick), 1 or -1 fixed (Weapon Tuner
        /// "Walk: flip", Rickard 2026-10-30: "tyra i game norr och vänster gå blir pilbågen bakofram vänd").</summary>
        public int RunSign;
        /// <summary>Rickard's own touch-up (Weapon Tuner): a shift in the hero's right/up/forward as a share of the figure's height,
        /// and degrees turned about the grip in the weapon's own x/y/z, on top of everything else, every frame.</summary>
        public Vector3 Offset, Turn;
        /// <summary>Two-handed weapons (Weapon Precision 2.0): where the other hand holds the weapon, as a share of the length from
        /// the handle's end like <see cref="Grip"/>; negative = one hand only (every weapon today). With a support grip the other
        /// arm is bent by a two-bone IK after everything else so its palm sits on that point, by <see cref="SupportW"/>.</summary>
        public float Support = -1f, SupportW = 1f;
        /// <summary>AnimFix.WristTurn (Weapon Precision 2.0, Rickard's yes 2026-10-30): the wrist turns up to this many degrees so the
        /// weapon sits in the fist as it does in the shot (its fist grip with <see cref="SwingTurn"/>), instead of the weapon turning
        /// inside the fist; the weapon's own aim is not changed, so the silhouette stays. 0 = off (only Tyra: her bow turned 86-180
        /// degrees in her fist between rest and shot, Simon's audit on issue #5).</summary>
        public float WristMax;
        /// <summary>Weapon Tuner "Close hand" (Rickard 2026-10-30: "kunna göra så hon stänger händerna"): 0 = the fingers as the clip
        /// has them, 1 = curled round the weapon's shaft (each finger joint bends a share of <see cref="HeldGear.FistBend"/> degrees on
        /// top of the clip, towards the shaft; the thumb half as much).</summary>
        public float Fist;
        /// <summary>Tyra pilot (issue #5, Simon's yes 2026-10-31; AnimFix.PalmSocket): the palm socket, where the fist holds the
        /// weapon's grip, in the hand's own frame f, t, n (f from the wrist to the index knuckle, t towards the thumb's root across f,
        /// n = f x t), in lengths of wrist to index knuckle; zero = the 0.6 rule (palmL). Calibrated offline from the curled fingers of
        /// the clip that was made for this weapon (Tyra: Shoot at its draw, the centres of the four fingers' curl circles; the same
        /// fist in Run within 0.53 % of the height), so a rig's own hand decides it, not a fixed share. Code only.</summary>
        public Vector3 Socket;
        /// <summary>Tyra pilot (AnimFix.AnatomicalWrist, AnimFix.GripLockFull): the weapon's own +y (along the handle) and +z as the
        /// fist holds it, in the same f, t, n frame (Tyra's bow: along the knuckles, its back to the arrow's line at the draw); zero
        /// = none. Code only.</summary>
        public Vector3 GripY, GripZ;
        /// <summary>For a model without finger bones (Knut: his clips have them, Knut.fbx has none, so the socket's frame could not be
        /// found and the grip sat in the wrist; Fenn.fbx has no right thumb): where the index and thumb roots are in the hand bone's
        /// own space, from the clips' rig (Knut read in Unity via MCP, 2026-11-01; Fenn from Blender, x negated as Knut's showed).
        /// In lengths of the hand bone's offset from the forearm, or, for the thumb of a model that has the index, of the index
        /// root's offset. Zero = from the bones.</summary>
        public Vector3 NoFingerIndex, NoFingerThumb;
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
        /// <summary>Not AlwaysSide: on the move, how far its centre sits out from the hand (share of its height; the old game's 0.3 threw
        /// Fenn's out far from his arm in the run, Unity run 1 2026-10-29).</summary>
        public float WalkOut = 0.3f;
        /// <summary>Weapon Tuner (Rickard 2026-10-30: "gör också så jag kan ändra fenns sköld", "likadant med moas"): turned about
        /// its centre in its own axes, then shifted in the hero's axes (share of the hero's height), on top of the code's placing.</summary>
        public Vector3 Twist, Shift;
    }

    /// <summary>A cannon riding the right shoulder like a bazooka, barrel forward, the right fist on its rear grip (old
    /// View3DHilde.cs MxCannon, MxCannonArms).</summary>
    sealed class CannonDef
    {
        public string Model;
        /// <summary>Length (the model's x) as a share of the figure's height.</summary>
        public float Len;
        /// <summary>Points in the model's own units (y = 0 at the bottom, the mouth at +x): on the barrel's axis above the rear
        /// grip (it rests on the shoulder there), the rear grip, the mouth.</summary>
        public Vector3 Seat, Grip, Mouth;
        /// <summary>Where the seat sits from the right shoulder joint, as shares of the figure's height (up, out, forward).</summary>
        public Vector3 At;
        /// <summary>The recoil of a shot: how far back (share of the figure's height) and how long it takes to come back.</summary>
        public float Kick = 0.05f, KickTime = 0.25f;
        /// <summary>Rickard's own touch-up (Weapon Tuner, HILDE-04): a shift in the hero's right/up/forward as a share of the figure's
        /// height, and degrees turned about the cannon's own x/y/z, on top of the placement, every frame. Len is live too.</summary>
        public Vector3 Offset, Turn;
    }

    sealed class GearDef
    {
        public WeaponDef[] Weapons;
        public ShieldDef Shield;
        public CannonDef Cannon;
        /// <summary>Degrees both upper arms are turned out from the sides after the clips, so a broad body's arms do not cut through it (old MX_ARM_SPREAD).</summary>
        public float ArmSpread;
        /// <summary>Weapon Tuner (Rickard 2026-10-30: "måste kunna ta ut hennes högerhand och arm från ansiktet", Tikta): each arm
        /// turned at rest on top of the clip, in degrees: x = the upper arm out from the side, y = the upper arm forward, z = the
        /// elbow bent more (negative = straighter). Eased out while a swing or cast plays, so those play as made.</summary>
        public Vector3 RightArm, LeftArm;
        /// <summary>Degrees added to <see cref="ArmSpread"/> in code only (the Weapon Tuner and WeaponTune.json leave it alone), so
        /// hands that hang close to the legs stay off the thighs as the idle sways (Tyra, ANIM-24).</summary>
        public float HandsOff;
        /// <summary>Every finger of both hands is held in the model's own pose each frame, before the fist closes, so no clip moves
        /// them (Tyra: her Avatar maps only the index, so her idle and run moved that one finger alone while the rest stood still,
        /// Cajsa 2026-11-11: "Tyras fingrar är så klåfingriga"). Code only.</summary>
        public bool StillFingers;
        /// <summary>At rest the right arm bends so the fist sits in front of the shoulder and the weapon lies back over it (Bonk);
        /// attack and cast clips play untouched.</summary>
        public bool Shoulder;
        /// <summary>Shoulder: where the wrist goes from the upper arm (forward, down, in) and where the elbow points (down, forward,
        /// out), as shares of the figure's height.</summary>
        public Vector3 CarryAt = new Vector3(0.13f, 0.12f, 0.02f), Elbow = new Vector3(0.2f, 0.12f, 0.12f);
        /// <summary>Shoulder: how much further out to the side than <see cref="CarryAt"/> the wrist goes (share of the figure's
        /// height), in code only (the Weapon Tuner and WeaponTune.json leave it alone), so the weapon over the shoulder and the
        /// fist stay out of a broad body (Bram, Bonk, ANIM-25).</summary>
        public float CarryOut;
        /// <summary>Parts worn on the body (Ravna's feather cloak and crow, Rickard 2026-11-12: "ge henne fjäderkappa kråka"), each
        /// Rickard's own Tripo model as a Props mesh, fixed to a bone in the bind pose; a cloak swings back as the hero runs.</summary>
        public WornDef[] Worn;
    }

    /// <summary>A part worn on the body (<see cref="GearDef.Worn"/>).</summary>
    sealed class WornDef
    {
        public string Model;
        /// <summary>The bone it rides on (falls back to the chest, then the hips).</summary>
        public HumanBodyBones Bone;
        /// <summary>Its height as a share of the figure's height.</summary>
        public float Height;
        /// <summary>Where the mesh's origin goes in the bind pose, from the hero's feet, in shares of the figure's height: x to her
        /// right, y up, z forward. A cloak's origin is its top edge, anything else its bottom.</summary>
        public Vector3 At;
        /// <summary>When set (not LastBone): At is measured from this bone's joint (or the point halfway to <see cref="To"/>) instead
        /// of the feet, so the part sits on the body whatever pose the model is built in (Rickard 2026-11-12 "inte bra": the crow
        /// floated by her head, the cloak hung like two narrow wings).</summary>
        public HumanBodyBones From = HumanBodyBones.LastBone, To = HumanBodyBones.LastBone;
        /// <summary>When above 0: the part's width is this many times the distance from <see cref="From"/> to <see cref="To"/>
        /// (a cloak as wide as the shoulders), and Height is ignored.</summary>
        public float Wide;
        /// <summary>Degrees turned about the hero's up axis.</summary>
        public float Yaw;
        /// <summary>Depth scale (a thick cloak pressed flatter against the back); 1 = as made.</summary>
        public float Flat = 1f;
        /// <summary>Degrees the part swings back about its top edge at full run (0 = rigid), with a soft spring.</summary>
        public float Sway;
    }

    /// <summary>
    /// Rickard's Meshy/Tripo weapons in the imported heroes' hands, placed as in the old game (HeroMixamoWeapon.cs,
    /// View3DOwnHeroFx.cs MxNeedles). Numbers are the old game's, which Rickard tuned on screen (2026-10-16 .. 10-24).
    /// Only imported heroes (HeroArt) use these; the code-built bodies keep their RigDef props.
    /// </summary>
    static class Weapons
    {
        static readonly GearDef[] byHero = WeaponTuning.Apply(Make());

        public static GearDef For(HeroId hero) => (int)hero < byHero.Length ? byHero[(int)hero] : null;

        /// <summary>The live table every view reads each frame (the Weapon Tuner changes it in place).</summary>
        internal static GearDef[] All => byHero;

        /// <summary>A fresh copy of the numbers in this file, without WeaponTune.json (the tuner's Reset).</summary>
        internal static GearDef[] CodeDefaults() => Make();

        static GearDef[] Make()
        {
            var g = new GearDef[(int)HeroId.Ravna + 1];
            // at rest the sword raised across beside his head (Rickard's own Weapon Tuner hold 2026-10-28 20:49 CET, after a tester
            // found the upright hold beside the helmet odd on the menu stage and the lowered pick B looked worse to him in Unity), so
            // Reset Fenn gives this hold and not the bind grip across the shield. On the move the sword trails back at his side, a little
            // below level, and the shield rides close to his arm (Unity run 1 2026-10-29: in the run his torso turns to the front and the
            // rest hold pointed the blade straight ahead like a lance, the shield thrown far out; coordinator's go on the recommended
            // pick A of bam-work/art/rigg-anim/spring/fenn-spring-val.png for Rickard, 2026-10-29; Clay-Fenn, redo if Original-Fenn stays)
            // Run hold since 2026-11-04 (Rickard: "fenn springer med sitt svärd neråt istället för uppåt som när han står still"): raised
            // almost straight up at his shoulder, Rickard's pick B (Up 70, Out 100) of bam-work/fenn-run/fenn-run-up.png
            g[(int)HeroId.Fenn] = new GearDef
            {
                // (2026-11-01, after Tyra and Knut) the sword in the palm socket: the centre of his four fingers' curl in his own Idle
                // and Run (curled 109 degrees; the same within 0.002), Socket only, so his Tuner hold and swing stay as tuned
                Weapons = new[] { new WeaponDef { Model = "fenn_sword", Len = 1.0f, Grip = 0.2f, HoldUp = 45f, HoldOut = 62f, Turn = new Vector3(-33f, 55f, 0f), Run = new Hold { Up = 70f, Out = 100f },
                    Socket = new Vector3(0.7521f, -0.0457f, 0.2897f), NoFingerThumb = new Vector3(-0.2402f, 0.2411f, 0.0900f) } },
                Shield = new ShieldDef { Model = "fenn_shield", Dia = 0.5f, WalkOut = 0.08f },
            };
            // arms out as in the old game (MX_ARM_SPREAD 22) and, at rest, the hammer on his right shoulder as Bonk carries his club
            // (Rickard 2026-10-27: hanging from a straight arm "inte så naturlig pose", and it flipped round into the swing); his
            // overhead strike (HeroArtImport) lifts it straight off the shoulder. Held at the end of the shaft, standing steep behind
            // his head (Rickard's pick 2026-10-28, option C of bam-work/art/rigg-anim/vila/bram-hammare-val.png: held a third of the
            // way up, the shaft's end stuck down between his legs, "heltokig"). The fist sits a little outside the shoulder and the head
            // leans out to his right, so facing the camera the hammer no longer hides his face (Unity run 1 2026-10-29; pick B of
            // bam-work/art/rigg-anim/spring/bram-spring-val.png; WeaponTune.json beats this until Rickard presses Reset Bram).
            // (ANIM-25, Rickard 2026-10-29: "Bram vapen kropp") with his tuned hold the fist sat 6 % of his height inside his belly
            // and the shaft went as deep through his chest, right shoulder and head standing and running (Unity's own poses
            // 2026-10-29); the fist 0.12 further out to his right clears both (bam-work/precision/anim/hold-bram-bonk-knut). That
            // goes on top of CarryAt, so pick B's own step out (in -0.04) is left to it: after Reset Bram the fist is as far out as
            // with his tuned numbers, and pick B's aim clears too
            g[(int)HeroId.Bram] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "bram_hammer_new", Len = 0.7f, Grip = 0.08f, HoldUp = 60f, HoldOut = 145f, Follow = 0.1f } },
                ArmSpread = 22f,
                Shoulder = true,
                CarryAt = new Vector3(0.11f, 0.12f, 0.02f),
                CarryOut = 0.12f,
            };
            // held by the shank just under the ring, so it hangs from the fist with the flukes down (Rickard 2026-10-16/19); hung straight
            // down at his right side, the flukes out, since in the fist's own grip it lay slanted in front of his thighs (Unity run 1
            // 2026-10-29; pick A of bam-work/art/rigg-anim/spring/knut-spring-val.png; WeaponTune.json beats this until Reset Knut)
            // (ANIM-25) his right fist sank up to 5 % of his height into his hip and thigh standing and running (Unity's own poses
            // 2026-10-29): both arms 16 degrees out keep it off; with pick A's hold that also keeps the flukes off his right foot
            // (2026-11-01, Rickard's pick after the Tyra pilot) his anchor in the palm socket: the centre of his index finger's curl in
            // Attack1 (curled 131 degrees through the whole clip; his rig has only thumb and index), measured offline in Blender as
            // Tyra's was (bam-work/precision/grip-knut); his Tuner Offset is skipped while the grip is locked in the socket
            g[(int)HeroId.Knut] = new GearDef
            {
                HandsOff = 16f,
                Weapons = new[] { new WeaponDef { Model = "knut_anchor", Len = 0.55f, Grip = 0.85f, HoldUp = 80f, HoldOut = -90f, Socket = new Vector3(0.9936f, 0.7866f, 0.9747f),
                    NoFingerIndex = new Vector3(0f, 0.2811f, -0.0272f), NoFingerThumb = new Vector3(-0.1263f, 0.3211f, 0f) } },
            };
            // with her own clips the staff along the forearm hung beside the fist and the crest, face out at the side, read as a
            // torch from the front (Rickard 2026-10-26: "moa håller inte i sina vapen"): the staff now stands up through her right
            // fist, face to the front, and the crest sits on her left hand turned half to the front; (2026-10-28) it stays up through
            // her swing and cast, leaning a third of the way with her hand, instead of flipping over in her fist
            // (Cajsa 2026-11-11: "Moas arm/stav går in i hennes kropp", "andra handen går in i Moas ben") her idle hangs both hands
            // against her hips, so the staff's fist and the staff went into her tunic and the left hand into her thigh; both arms 14
            // degrees out, as Nässla in the same idle (Blender renders bam-work/feedback-cajsa-modeller/moa_fore_efter.png)
            g[(int)HeroId.Moa] = new GearDef
            {
                HandsOff = 14f,
                Weapons = new[] { new WeaponDef { Model = "moa_staff", Len = 0.95f, Grip = 0.4f, Along = true, HoldUp = 82f, HoldOut = 8f, Upright = true, SwingAim = true, SwingFollow = 0.35f } },
                Shield = new ShieldDef { Model = "moa_shield", Dia = 0.45f, AlwaysSide = true, Turn = 45f, Off = 0.12f },
            };
            // her ornate bow (Rickard's Tripo model 2026-10-24, old fig_ornateBow; Rickard 2026-10-26: "ja", put it in her hand),
            // gripped at the middle in the left fist like any weapon: the limbs leave the fist on the thumb side, so in her bow
            // clips (arm out, thumb up) it stands upright with the belly away from her. Along the forearm (as Moa's staff) it lay
            // flat along her outstretched arm (Rickard 2026-10-26: "håller konstigt i vapen"; checked in a Blender render of IdleBow)
            // at rest (her relaxed idle and walk) it lies level at her left hip, gripped at the middle, one limb forward and one
            // back, the string up (Rickard's three pictures 2026-10-28, after hanging it beside her leg cut into it and none of four
            // other holds looked right; render bam-work/art/rigg-anim/vila/tyra-bage-ny.png); her shot plays the fist's grip
            // (2026-10-28) for the shot the bow is turned a quarter about the fingers, so it stands up with its belly to the target;
            // (Rickard 2026-10-29) on the menu stage it hangs from her left fist down and forward past her knee in her relaxed idle,
            // as his pick 1 of bam-work/art/grundposer/forslag-bram-tyra.png (rendered in the fist's own grip: there its lower limb
            // pointed 48 degrees down and 15 out to her side, the string side to her front; HoldUp -60 HoldOut -30 put the upper limb
            // up past her shoulder; in Unity the fist's own grip lay level across her legs, his screenshot 22:52 CET, ANIM-22), and on
            // the move she carries it upright at her side, its lower limb out past her leg (run renders
            // bam-work/art/rigg-anim/vila/tyra-bage-sidan.png)
            // (ANIM-24, Rickard 2026-10-29: "tyra hennes högra hand lår när anim går") her relaxed idle swings the right hand up to
            // 4 % of her height into the right thigh and the left up to 3 % into the left (Unity run of 2026-10-29); both arms 12
            // degrees out clear every idle frame, the run and the end of her shot (bam-work/precision/anim/handclip-tyra)
            // (2026-10-30) no run hold of her own any more: on the move she keeps the idle's hold with Rickard's Turn, as in her idle
            // and shot (Rickard: "funkar så bra med tyras båge vid attacker och idle men springande är kaosdåligt")
            g[(int)HeroId.Tyra] = new GearDef
            {
                HandsOff = 12f,
                StillFingers = true, // her idle and run moved only the index (Cajsa 2026-11-11: "klåfingriga")
                Weapons = new[]
                {
                    new WeaponDef
                    {
                        Model = "tyra_bow", Len = 0.75f, Grip = 0.5f, Left = true, HoldUp = -5f, HoldOut = 10f, Follow = 0.15f, RestTurn = new Vector3(0f, -90f, 0f), WristMax = 60f,
                        SwingTurn = new Vector3(0f, 0f, -90f), Menu = new Hold { Up = -48f, Out = 15f }, MenuSign = -1,
                        // Tyra pilot (issue #5): her fist from Shoot's draw, measured offline (Blender, 2026-10-30); the grip 2.78 % of
                        // her height from the 0.6 rule, the bow 9.5 degrees from her knuckle line
                        Socket = new Vector3(0.8148f, -0.0447f, -0.3928f),
                        GripY = new Vector3(0.3956f, 0.6415f, 0.6573f), GripZ = new Vector3(0.8995f, -0.4151f, -0.1363f),
                    },
                },
            };
            // her old needle daggers, one in each fist out of the thumb side (old View3DOwnHeroFx.cs MxNeedles before the Needle
            // Wing: NeedleMesh/NeedleTex as Props tikta_needle, NEEDLE_LEN 0.36, NEEDLE_GRIP 0.14; Rickard 2026-10-26: "tikta ska
            // ha sina gamla vapen, typ två daggers"); at rest the blades point down along her forearms, since her guard idle holds
            // a fist at her mouth and out of the thumb side the needle stuck into her face; (2026-10-28) they keep that aim through her
            // swing, following half of her hand, instead of flipping 100-150 degrees into the fist's own grip (pointing at her face at
            // the wind-up, across her body after the blow) at every swing; on the move they point back along her forearms, away from her
            // thighs (Unity run 1 2026-10-29: the right one ran down through her thigh; pick A of bam-work/art/rigg-anim/spring/tikta-spring-val.png)
            g[(int)HeroId.Tikta] = new GearDef
            {
                Weapons = new[]
                {
                    new WeaponDef { Model = "tikta_needle", Len = 0.36f, Grip = 0.14f, Left = true, HoldUp = -70f, HoldOut = 20f, Follow = 0.4f, SwingAim = true, Run = new Hold { Up = -25f, Out = 160f } },
                    new WeaponDef { Model = "tikta_needle", Len = 0.36f, Grip = 0.14f, HoldUp = -70f, HoldOut = 20f, Follow = 0.4f, SwingAim = true, Run = new Hold { Up = -25f, Out = 160f } },
                },
            };
            // Ravna's own body (2026-11-12) with Rickard's Tripo talon daggers, feather cloak and crow (Rickard: "ge henne fjäderkappa
            // kråka och dolk"); the daggers held as Tikta's needles were, tune them with the Weapon Tuner
            g[(int)HeroId.Ravna] = new GearDef { Weapons = new[] { new WeaponDef { Model = "ravna_dagger", Len = 0.3f, Grip = 0.06f, Left = true, HoldUp = -70f, HoldOut = 20f, Follow = 0.4f, SwingAim = true, Run = new Hold { Up = -25f, Out = 160f } }, new WeaponDef { Model = "ravna_dagger", Len = 0.3f, Grip = 0.06f, HoldUp = -70f, HoldOut = 20f, Follow = 0.4f, SwingAim = true, Run = new Hold { Up = -25f, Out = 160f } } },
                Worn = new[]
                {
                    // her feather cloak: its shoulder plates on her shoulders, 1.25 times as wide as they are, hanging down her back to the knees (checked in Blender on her idle, 2026-11-12)
                    new WornDef { Model = "ravna_cloak", Bone = HumanBodyBones.UpperChest, From = HumanBodyBones.LeftUpperArm, To = HumanBodyBones.RightUpperArm, Wide = 1.25f, At = new Vector3(0f, 0.035f, -0.06f), Flat = 0.8f, Sway = 28f },
                    // the crow sits on her left shoulder, looking ahead
                    new WornDef { Model = "ravna_crow", Bone = HumanBodyBones.LeftShoulder, From = HumanBodyBones.LeftUpperArm, Height = 0.1f, At = new Vector3(0.012f, 0.012f, -0.01f) },
                } }; // own copies, so tuning her never moves Tikta's
            // Nässla's root staff with its glowing pod and her green cloak (Rickard's Tripo parts 2026-11-12: "samma med nässla vill jag ha
            // items", "hennes gröna kappa också"): the staff held upright as Moa's, the cloak from her shoulders under the basket
            g[(int)HeroId.Nassla] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "nassla_staff", Len = 0.8f, Grip = 0.4f, Along = true, HoldUp = 82f, HoldOut = 8f, Upright = true, SwingAim = true, SwingFollow = 0.35f } },
                Worn = new[] { new WornDef { Model = "nassla_cloak", Bone = HumanBodyBones.UpperChest, From = HumanBodyBones.LeftUpperArm, To = HumanBodyBones.RightUpperArm, Wide = 1.3f, At = new Vector3(0f, 0.03f, -0.06f), Flat = 0.8f, Sway = 22f } },
            };
            // the skull bomb he throws (the old game only flew it; held here so the throw reads)
            g[(int)HeroId.Blast] = new GearDef { Weapons = new[] { new WeaponDef { Model = "blast_bomb", Len = 0.22f, Grip = 0.3f, Thrown = true } } };
            // Bonk's wooden club with bone spikes (Rickard's Tripo model 2026-10-26), held low on the handle, far too big for him
            // (Rickard 2026-10-26: "klubban ska vara för stor för honom"), carried on his right shoulder at rest: the arm bends, the
            // fist in front of the shoulder, the club leaning back over it (Rickard 2026-10-26: "ha vapen naturligt på axeln", as in
            // the "Bonk has arrived" picture); only turning the club, it stood in front of his face, behind his back or in his pelt. On the
            // move it stands up over the shoulder, leaning out a little (Unity run 1 2026-10-29: lying back nearly level it read as a pole
            // from the match camera; pick B of bam-work/art/rigg-anim/spring/bonk-spring-val.png). (ANIM-25, Rickard 2026-10-29: "bonk
            // vapen i axel") standing, the club lay up to 5 % of his height deep in his right shoulder, upper arm and chest (Unity's
            // own poses 2026-10-29); the fist 0.1 further out to his right lays it over the shoulder instead. With his own Orc Idle 1
            // (2026-10-30) he leans forward and the club lying straight back pressed against his head and pelt (Rickard: "bonk blev bra
            // förutom hur vapen ligger"): it now leans 35 degrees out from straight back and the fist sits 0.04 further out
            g[(int)HeroId.Bonk] = new GearDef
            {
                Weapons = new[] { new WeaponDef { Model = "bonk_club", Len = 0.85f, Grip = 0.12f, HoldUp = 36f, HoldOut = 145f, Follow = 0.1f, Run = new Hold { Up = 75f, Out = 140f } } },
                Shoulder = true,
                CarryOut = 0.14f,
            };
            // her cannon (Rickard's Meshy model) on her right shoulder like a bazooka, barrel forward, her right fist on its rear grip,
            // kicking back at every shot: old View3DHilde.cs with its numbers for her current body (stocky, short neck: the barrel
            // rides out on the shoulder beside her head). Her left arm plays the clip: her short arms could not reach the front grip
            // across her chest, the stretched arm twisted round behind her back (Rickard 2026-10-24: "hilde är armarna inte bra")
            g[(int)HeroId.Hilde] = new GearDef
            {
                Cannon = new CannonDef
                {
                    Model = "hilde_cannon", Len = 0.42f, At = new Vector3(0.045f, 0.10f, 0.08f),
                    Seat = new Vector3(-0.47f, 0.59f, 0f), Grip = new Vector3(-0.47f, 0.12f, 0f), Mouth = new Vector3(0.95f, 0.59f, 0f),
                },
            };
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
    /// Rickard's own numbers from the Weapon Tuner (BAM > Art > Weapon Tuner), saved as Resources/BAMArt/WeaponTune.json. Laid
    /// over the numbers in <see cref="Weapons"/> once at start, per hero by name and per weapon by its place; without the file the
    /// code's numbers stand.
    /// </summary>
    static class WeaponTuning
    {
        public const string ResourcePath = "BAMArt/WeaponTune";

        [System.Serializable]
        internal sealed class File { public Hero[] Heroes; }

        [System.Serializable]
        internal sealed class Hero
        {
            public string Name;
            public Weapon[] Weapons;
            public float ArmSpread;
            public Vector3 CarryAt, Elbow;
            public Vector3 RightArm, LeftArm; // 0 (an older file) = the clip's arms
            public Shield Shield; // Fenn's and Moa's; Dia 0 (an older file) leaves the code's numbers
            public Cannon Cannon; // Hilde's; Len 0 (an older file, or a hero without one) leaves the code's numbers
        }

        [System.Serializable]
        internal sealed class Shield
        {
            public float Dia;
            public Vector3 Twist, Shift;
        }

        [System.Serializable]
        internal sealed class Cannon
        {
            public float Len;
            public Vector3 Offset, Turn;
        }

        [System.Serializable]
        internal sealed class Weapon
        {
            public float Len, Grip, HoldUp, HoldOut;
            public Vector3 Offset, Turn;
            public float Fist;
            public int RunSign; // 0 (an older file) = the idle's vote
            public float MenuUp, MenuOut; // the menu's own hold (Tyra's bow on Home); MenuUp 0 (an older file) leaves the code's
        }

        public static GearDef[] Apply(GearDef[] g)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return g;
            try
            {
                var f = JsonUtility.FromJson<File>(asset.text);
                if (f != null && f.Heroes != null)
                    foreach (Hero h in f.Heroes)
                        for (int i = 0; i < g.Length; i++)
                            if (g[i] != null && ((HeroId)i).ToString() == h.Name) Copy(h, g[i]);
            }
            catch (System.Exception e) { Debug.LogWarning("[BAM.Art] Could not read WeaponTune.json: " + e.Message); }
            return g;
        }

        /// <summary>One hero's numbers onto its gear, in place (views keep reading the same objects).</summary>
        internal static void Copy(Hero h, GearDef d)
        {
            d.ArmSpread = h.ArmSpread;
            d.RightArm = h.RightArm; d.LeftArm = h.LeftArm;
            d.CarryAt = h.CarryAt; d.Elbow = h.Elbow;
            if (d.Shield != null && h.Shield != null && h.Shield.Dia > 0f) { d.Shield.Dia = h.Shield.Dia; d.Shield.Twist = h.Shield.Twist; d.Shield.Shift = h.Shield.Shift; }
            if (d.Cannon != null && h.Cannon != null && h.Cannon.Len > 0f) { d.Cannon.Len = h.Cannon.Len; d.Cannon.Offset = h.Cannon.Offset; d.Cannon.Turn = h.Cannon.Turn; }
            if (h.Weapons == null || d.Weapons == null) return;
            for (int w = 0; w < h.Weapons.Length && w < d.Weapons.Length; w++)
            {
                Weapon s = h.Weapons[w]; WeaponDef t = d.Weapons[w];
                t.Len = s.Len; t.Grip = s.Grip; t.HoldUp = s.HoldUp; t.HoldOut = s.HoldOut; t.Offset = s.Offset; t.Turn = s.Turn; t.Fist = s.Fist; t.RunSign = s.RunSign;
                if (s.MenuUp != 0f && t.Menu.On) t.Menu = new Hold { Up = s.MenuUp, Out = s.MenuOut, Turn = t.Menu.Turn };
            }
        }

        internal static Hero From(string name, GearDef d)
        {
            var h = new Hero { Name = name, ArmSpread = d.ArmSpread, CarryAt = d.CarryAt, Elbow = d.Elbow, RightArm = d.RightArm, LeftArm = d.LeftArm };
            if (d.Shield != null) h.Shield = new Shield { Dia = d.Shield.Dia, Twist = d.Shield.Twist, Shift = d.Shield.Shift };
            if (d.Cannon != null) h.Cannon = new Cannon { Len = d.Cannon.Len, Offset = d.Cannon.Offset, Turn = d.Cannon.Turn };
            int n = d.Weapons != null ? d.Weapons.Length : 0;
            h.Weapons = new Weapon[n];
            for (int w = 0; w < n; w++)
            {
                WeaponDef s = d.Weapons[w];
                h.Weapons[w] = new Weapon { Len = s.Len, Grip = s.Grip, HoldUp = s.HoldUp, HoldOut = s.HoldOut, Offset = s.Offset, Turn = s.Turn, MenuUp = s.Menu.Up, MenuOut = s.Menu.Out, Fist = s.Fist, RunSign = s.RunSign };
            }
            return h;
        }

        /// <summary>Every hero's current numbers as the file's text.</summary>
        internal static string ToJson(GearDef[] g)
        {
            var list = new List<Hero>();
            for (int i = 0; i < g.Length; i++) if (g[i] != null) list.Add(From(((HeroId)i).ToString(), g[i]));
            return JsonUtility.ToJson(new File { Heroes = list.ToArray() }, true);
        }
    }

    /// <summary>
    /// One view's held weapons, shield and cannon. Built once in the bind pose and parented to the hand bones (the cannon to the
    /// chest), so they follow every clip; <see cref="Tick"/> runs after the animation each frame (shield facing, Bram's hold
    /// angle, Blast's thrown bomb, Hilde's fist on her cannon) and only writes transforms. The renderers and materials are handed
    /// to BodyView, so fades, hit flash and death go with the body. Weapons cast no shadows (as in the old game).
    /// </summary>
    sealed class HeldGear
    {
        public Renderer[] Rends;
        public Material[] Opaque, Fade;

        Transform root;
        // weapons
        WeaponDef[] defs;
        Transform[] wT, hand, index;
        Transform[][] fing; // per weapon: its hand's finger joints, 3 per finger, thumb first (null where the rig has none)
        Vector3[] bindP, gripL;
        Quaternion[] bindR;
        Vector3[] bindS;
        // the bind-pose placement in the hand's own space, so a tuned length or grip can be placed again live
        Vector3[] palmL, handleV, shaftC;
        float[] meshH, handS, placedLen, placedGrip;
        // shield
        ShieldDef sDef;
        Transform sAnchor, sModel, sHand;
        float sMeshH, sMeshD, sPlacedDia;
        // arms turned out after the clips
        GearDef gdef;
        Transform armR, armL, elbR, elbL, wriR, wriL;
        // the right arm bent to carry the weapon on the shoulder
        Transform carryUp, carryLo, carryHand;
        // the cannon on the chest, where it sits and which way it kicks (chest space), the right arm that holds it
        CannonDef cDef;
        Transform cT, cUp, cLo, cHand, cIndex, cOther;
        Vector3 cAt, cBack;
        Quaternion cRot;           // the cannon's placed turn on the chest, before Rickard's Turn
        Vector3 cScale; float cLen; // its placed size and the Len it was placed at, so a tuned Len scales it live
        float kickIn = -1f, kickT = -1f, deadT;
        float liveW = 1f; // 1 alive, down to 0 over DeathLetGo after a death (the rest hold lets go)
        const float DeathLetGo = 0.25f;
        float figH;
        // worn parts (GearDef.Worn): their transforms, rest turns and swing springs
        WornDef[] worn; Transform[] wornT; Quaternion[] wornRest; float[] swayA, swayV; float swayClock;
        // thrown: when the weapon leaves the hand, how long it stays away, and whether a weapon that is not Thrown goes too (Throw)
        float hideIn = -1f, backIn = -1f, awayFor = AwayFor;
        bool hideHeld;
        int hideSide;
        const float AwayFor = 0.45f, PopIn = 0.15f;
        // AnimFix.FixedGrip, measured in the idle (MeasureRest): which way each weapon's flat side faced, kept so the rest aim never
        // flips half a turn as the fist turns (Tikta's needles turned up to 178 degrees in her fists every step), and where each
        // hand turned in the hero's own space; again when the tuner changes a number the idle aim depends on
        int[] laxSign, votes;
        int[] runSign, runVotes; // the same for WeaponDef.Run
        Quaternion[] handRef;
        Vector4[] refSum;
        Vector3[] restGrip, gripSum; // an Along weapon's grip in its hand's own space in the idle (HoldAngle holds it there)
        int restN;
        bool rested, sampling;
        int restKey;
        /// <summary>A menu stage figure (BodyView.Preview): weapons with a <see cref="WeaponDef.Menu"/> hold are held that way.</summary>
        public bool OnMenu;
        /// <summary>The ground's height under the hero (world y): no weapon's end goes below it (ANIM-08/09). Off (negative
        /// infinity) on the menu stage.</summary>
        public float Floor = float.NegativeInfinity;
        /// <summary>The legs a carried weapon keeps out of (hip, knee and ankle, left then right; null for none) and by how much
        /// (0..1, the hero's share on the move): Tyra's bow at her side (Tyra_02/05).</summary>
        public Transform[] Legs;
        public float LegsW;
        // AboveGround: the way across the ground each weapon's end rested last frame (zero while it is above it)
        Vector3[] lowDir;
        // Weapon Precision 2.0 (AnimFix.GripLock): how far each weapon's grip was from its palm before the lock this frame and at
        // most since the last ResetStats (share of the figure's height), and its largest turn in one frame in the hero's own space
        // (degrees; a flip or a spin shows here), for the debug view (WeaponDebug)
        internal float[] GripErr, GripErrMax, JumpMax;
        /// <summary>How far each weapon is turned in its fist from the shot's grip, now and at most (degrees; WristTurn takes up to
        /// WeaponDef.WristMax of it), for the debug view.</summary>
        internal float[] FistTwist, FistTwistMax;
        Quaternion[] lastLocal;
        // Tyra pilot (WeaponDef.Socket, GripY/GripZ): the palm socket in the hand's own space, and the anatomical grip as a turn in
        // the hand's own space (the weapon's localRotation when the fist holds it as the clip made it)
        Vector3[] sockL;
        Quaternion[] anatR;
        bool[] hasSock, hasAnat;
        // read by the Grip Validator (BAM.Art.Editor) after this frame's Tick, besides WeaponT/HandT/Def/GripPoints below
        internal float FigH => figH;
        internal Transform[] FingersAt(int i) => fing[i];
        internal Vector3 PalmRuleWorld(int i) => hand[i].TransformPoint(palmL[i]);
        internal bool SocketWorld(int i, out Vector3 at) { at = hasSock[i] ? hand[i].TransformPoint(sockL[i]) : Vector3.zero; return hasSock[i]; }
        internal bool AnatomicalGrip(int i, out Quaternion local) { local = hasAnat[i] ? anatR[i] : Quaternion.identity; return hasAnat[i]; }
        // the other arm for a two-handed weapon (WeaponDef.Support): upper arm, forearm, hand, index finger
        Transform[] supUp, supLo, supHand, supIndex;

        public static HeldGear Build(GearDef g, Animator an, float figH)
        {
            if (g == null || an == null || an.avatar == null || !an.isHuman) return null;
            var h = new HeldGear { root = an.transform, figH = figH, gdef = g };
            var rends = new List<Renderer>();
            var op = new List<Material>();
            var fd = new List<Material>();
            int nw = g.Weapons != null ? g.Weapons.Length : 0;
            h.defs = new WeaponDef[nw];
            h.wT = new Transform[nw]; h.hand = new Transform[nw]; h.index = new Transform[nw]; h.fing = new Transform[nw][];
            h.bindP = new Vector3[nw]; h.gripL = new Vector3[nw]; h.bindR = new Quaternion[nw]; h.bindS = new Vector3[nw];
            h.palmL = new Vector3[nw]; h.handleV = new Vector3[nw]; h.shaftC = new Vector3[nw];
            h.meshH = new float[nw]; h.handS = new float[nw]; h.placedLen = new float[nw]; h.placedGrip = new float[nw];
            h.laxSign = new int[nw]; h.votes = new int[nw]; h.handRef = new Quaternion[nw]; h.refSum = new Vector4[nw];
            h.restGrip = new Vector3[nw]; h.gripSum = new Vector3[nw];
            h.runSign = new int[nw]; h.runVotes = new int[nw];
            h.FistTwist = new float[nw]; h.FistTwistMax = new float[nw];
            h.GripErr = new float[nw]; h.GripErrMax = new float[nw]; h.JumpMax = new float[nw]; h.lastLocal = new Quaternion[nw];
            h.supUp = new Transform[nw]; h.supLo = new Transform[nw]; h.supHand = new Transform[nw]; h.supIndex = new Transform[nw];
            h.sockL = new Vector3[nw]; h.anatR = new Quaternion[nw]; h.hasSock = new bool[nw]; h.hasAnat = new bool[nw];
            h.armR = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            h.armL = an.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            h.elbR = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
            h.elbL = an.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            h.wriR = an.GetBoneTransform(HumanBodyBones.RightHand);
            h.wriL = an.GetBoneTransform(HumanBodyBones.LeftHand);
            if (g.Shoulder)
            {
                h.carryUp = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
                h.carryLo = an.GetBoneTransform(HumanBodyBones.RightLowerArm);
                h.carryHand = an.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (g.StillFingers) h.KeepFingers(an, g);
            for (int i = 0; i < nw; i++) h.AddWeapon(i, g.Weapons[i], an, figH, rends, op, fd);
            if (g.Shield != null) h.AddShield(g.Shield, an, figH, rends, op, fd);
            if (g.Cannon != null) h.AddCannon(g.Cannon, an, figH, rends, op, fd);
            if (g.Worn != null) h.AddWorn(g.Worn, an, figH, rends, op, fd);
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

        void AddWorn(WornDef[] ws, Animator an, float H, List<Renderer> rends, List<Material> op, List<Material> fd)
        {
            int n = ws.Length;
            worn = ws; wornT = new Transform[n]; wornRest = new Quaternion[n]; swayA = new float[n]; swayV = new float[n];
            for (int i = 0; i < n; i++)
            {
                WornDef w = ws[i];
                Mesh mesh = Props.Mesh(w.Model);
                Transform bone = an.GetBoneTransform(w.Bone);
                if (bone == null) bone = an.GetBoneTransform(HumanBodyBones.Chest);
                if (bone == null) bone = an.GetBoneTransform(HumanBodyBones.Hips);
                if (mesh == null || bone == null) continue;
                var go = new GameObject("Worn " + w.Model);
                MakeRenderer(go, mesh, w.Model, rends, op, fd);
                Transform t = go.transform;
                t.SetParent(bone, false);
                Transform fa = w.From != HumanBodyBones.LastBone ? an.GetBoneTransform(w.From) : null;
                Transform fb = fa != null && w.To != HumanBodyBones.LastBone ? an.GetBoneTransform(w.To) : null;
                Vector3 from = fa == null ? root.position : fb == null ? fa.position : (fa.position + fb.position) * 0.5f;
                t.position = from + (root.right * w.At.x + root.up * w.At.y + root.forward * w.At.z) * H;
                t.rotation = root.rotation * Quaternion.Euler(0f, w.Yaw, 0f);
                float k = w.Wide > 0f && fb != null
                    ? w.Wide * Vector3.Distance(fa.position, fb.position) / Mathf.Max(1e-4f, mesh.bounds.size.x)
                    : w.Height * H / Mathf.Max(1e-4f, mesh.bounds.size.y);
                float ps = Mathf.Max(1e-6f, bone.lossyScale.x);
                t.localScale = new Vector3(k, k, k * w.Flat) / ps;
                wornT[i] = t; wornRest[i] = t.localRotation;
            }
        }

        // a cloak's swing: a soft spring toward a lean that grows with running and a little with the swing of an attack, plus a
        // slow breath at rest, about its top edge (the mesh's origin)
        void TickWorn(float dt, float moveW, float atkW, bool dead)
        {
            if (wornT == null || dt <= 0f) return;
            swayClock += dt;
            for (int i = 0; i < wornT.Length; i++)
            {
                Transform t = wornT[i];
                if (t == null) continue;
                float max = worn[i].Sway;
                if (max <= 0f) continue;
                float target = dead ? 0f : max * Mathf.Clamp01(moveW) + max * 0.25f * Mathf.Clamp01(atkW) + 1.5f * Mathf.Sin(swayClock * 1.4f + i);
                float acc = 40f * (target - swayA[i]) - 7f * swayV[i];
                swayV[i] += acc * Mathf.Min(dt, 0.05f);
                swayA[i] = Mathf.Clamp(swayA[i] + swayV[i] * Mathf.Min(dt, 0.05f), -max * 0.3f, max * 1.3f);
                t.localRotation = wornRest[i] * Quaternion.AngleAxis(swayA[i], Vector3.right);
            }
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
            float mh = Mathf.Max(1e-4f, mesh.bounds.size.y);
            var go = new GameObject("Weapon " + W.Model);
            MakeRenderer(go, mesh, W.Model, rends, op, fd);
            Transform t = go.transform;
            t.SetParent(hd, false);
            t.rotation = Quaternion.LookRotation(fingers.normalized, handle);
            if (W.Twist != 0f) t.rotation *= Quaternion.AngleAxis(W.Twist, Vector3.up);
            wT[i] = t; hand[i] = hd; index[i] = ix;
            int f0 = (int)(W.Left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            fing[i] = new Transform[15];
            for (int k = 0; k < 15; k++) fing[i][k] = an.GetBoneTransform((HumanBodyBones)(f0 + k));
            // an Avatar that maps only some fingers (Tyra's maps only the index, Rickard 2026-11-01: "bara ett finger som greppet
            // styr"): the rest are taken by name from the FBX, as the socket's frame does, so Close hand curls the whole fist
            for (int k = 0; k < 15; k++)
                if (fing[i][k] == null) fing[i][k] = HandBone(hd, W.Left, FingerNames[k / 3] + (k % 3 + 1));
            if (W.Support >= 0f)
            {
                // the other side's arm (a left-hand weapon is supported by the right hand)
                supUp[i] = an.GetBoneTransform(W.Left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                supLo[i] = an.GetBoneTransform(W.Left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                supHand[i] = an.GetBoneTransform(W.Left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                supIndex[i] = an.GetBoneTransform(W.Left ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            }
            bindR[i] = t.localRotation;
            palmL[i] = hd.InverseTransformPoint(palm);
            handleV[i] = hd.InverseTransformVector(handle);
            meshH[i] = mh;
            handS[i] = Mathf.Max(1e-6f, hd.lossyScale.y);
            shaftC[i] = Weapons.ShaftAxis(mesh, W.Grip * mh); // a hammer's box is not centred on its shaft
            Place(i);
            // Tyra pilot: her Avatar (Tyra.fbx.meta, made 25 Oct from a rig without them) maps only the index finger, so the
            // Humanoid lookup finds no thumb; the FBX has the bone, so the socket's frame takes it by name (only here, so the
            // fist and every other hero stay as they are)
            Transform ixB = ix != null ? ix : HandBone(hd, W.Left, "Index1"), thB = th != null ? th : HandBone(hd, W.Left, "Thumb1");
            // a model without those bones takes them from WeaponDef.NoFingerIndex/NoFingerThumb (Knut has no fingers, Fenn no thumb)
            float fl = hd.localPosition.magnitude * hd.parent.lossyScale.y / Mathf.Max(1e-6f, hd.lossyScale.y);
            bool hasIx = ixB != null || W.NoFingerIndex != Vector3.zero, hasTh = thB != null || W.NoFingerThumb != Vector3.zero;
            if (hasIx && hasTh)
            {
                Vector3 ixP = ixB != null ? ixB.position : hd.TransformPoint(W.NoFingerIndex * fl);
                Vector3 thP = thB != null ? thB.position
                    : hd.TransformPoint(W.NoFingerThumb * (ixB != null ? hd.InverseTransformPoint(ixB.position).magnitude : fl));
                Anatomy(i, W, hd, ixP, thP);
            }
        }

        // GearDef.StillFingers: every finger bone of both hands (by the Avatar, else by its Mixamo name) and the pose it is held in:
        // the bind pose for a hand that holds a weapon (Close hand curls it after), a relaxed curl for a free hand, so it is not a
        // flat mannequin hand (the bind pose's fingers are straight). stillM: the Avatar drives it, so a swing or cast plays its
        // own fingers (Tyra's draw in Shoot); a bone the Avatar does not drive is set whole every frame (nothing else resets it).
        Transform[] stillB;
        Quaternion[] stillR;
        bool[] stillM;
        /// <summary>Degrees a free hand's finger joints bend in its relaxed pose (knuckle, middle, tip; the thumb half).</summary>
        static readonly float[] RelaxBend = { 20f, 20f, 15f };

        void KeepFingers(Animator an, GearDef g)
        {
            var bs = new List<Transform>();
            var ms = new List<bool>();
            var rel = new List<Quaternion>();
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 1;
                Transform hd = an.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                if (hd == null) continue;
                bool held = false;
                if (g.Weapons != null) foreach (WeaponDef w in g.Weapons) if (w != null && w.Left == left) held = true;
                int f0 = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
                var f = new Transform[15];
                var fm = new bool[15];
                for (int k = 0; k < 15; k++)
                {
                    Transform b = an.GetBoneTransform((HumanBodyBones)(f0 + k));
                    fm[k] = b != null;
                    if (b == null) b = HandBone(hd, left, FingerNames[k / 3] + (k % 3 + 1));
                    f[k] = b;
                }
                var bind = new Quaternion[15];
                for (int k = 0; k < 15; k++) if (f[k] != null) bind[k] = f[k].localRotation;
                if (!held) Relax(f);
                for (int k = 0; k < 15; k++)
                {
                    if (f[k] == null || bs.Contains(f[k])) continue;
                    bs.Add(f[k]); ms.Add(fm[k]); rel.Add(f[k].localRotation);
                    f[k].localRotation = bind[k]; // the weapons go on in the bind pose
                }
            }
            stillB = bs.ToArray();
            stillR = rel.ToArray();
            stillM = ms.ToArray();
        }

        // a free hand's relaxed curl, from the bind pose: the four fingers bend about the knuckle line towards the palm (the side
        // the thumb's tip is on), the thumb a little in towards the index's root
        static void Relax(Transform[] f)
        {
            if (f[3] == null || f[4] == null || f[12] == null) return;
            Vector3 kn = f[12].position - f[3].position, dir = f[4].position - f[3].position;
            Transform tip = f[2] != null ? f[2] : f[1] != null ? f[1] : f[0];
            if (tip == null || kn.sqrMagnitude < 1e-12f || dir.sqrMagnitude < 1e-12f) return;
            Vector3 palm = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(tip.position - f[3].position, dir.normalized), kn.normalized);
            if (palm.sqrMagnitude < 1e-12f) return;
            Vector3 axis = Vector3.Cross(dir, palm).normalized;
            axis = Vector3.Dot(axis, kn) < 0f ? -kn.normalized : kn.normalized;
            Vector3 thAxis = Vector3.zero;
            if (f[0] != null && f[1] != null)
            {
                Vector3 td = f[1].position - f[0].position, to = Vector3.ProjectOnPlane(f[3].position - f[0].position, td.normalized);
                if (td.sqrMagnitude > 1e-12f && to.sqrMagnitude > 1e-12f) thAxis = Vector3.Cross(td, to).normalized;
            }
            for (int n = 0; n < 5; n++)
            {
                Vector3 ax = n == 0 ? thAxis : axis;
                if (ax == Vector3.zero) continue;
                for (int j = 0; j < 3; j++)
                {
                    Transform b = f[n * 3 + j];
                    if (b == null) break;
                    b.rotation = Quaternion.AngleAxis(RelaxBend[j] * (n == 0 ? 0.5f : 1f), ax) * b.rotation;
                }
            }
        }

        /// <summary>A finger's root bone under the hand by its Mixamo name (LeftHandThumb1 and so on), for rigs whose Avatar does not
        /// map it; null when the rig has none.</summary>
        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        internal static Transform HandBone(Transform hand, bool left, string finger)
        {
            if (hand == null) return null;
            string want = (left ? "LeftHand" : "RightHand") + finger;
            foreach (Transform c in hand.GetComponentsInChildren<Transform>(true))
            {
                string n = c.name;
                int colon = n.LastIndexOf(':');
                if ((colon >= 0 ? n.Substring(colon + 1) : n) == want) return c;
            }
            return null;
        }

        // Tyra pilot: the palm socket and the anatomical grip from the hand's own frame (wrist, index knuckle, thumb root: all fixed in
        // the hand bone, so whatever the fingers do the frame follows the hand exactly); measured in the bind pose, kept in hand space
        void Anatomy(int i, WeaponDef W, Transform hd, Vector3 ixP, Vector3 thP)
        {
            if (W.Socket == Vector3.zero) return;
            Vector3 fa = ixP - hd.position;
            float hl = fa.magnitude;
            if (hl < 1e-6f) return;
            Vector3 f = fa / hl;
            Vector3 tt = Vector3.ProjectOnPlane(thP - hd.position, f);
            if (tt.sqrMagnitude < 1e-12f) return;
            tt.Normalize();
            Vector3 n = Vector3.Cross(f, tt);
            sockL[i] = hd.InverseTransformPoint(hd.position + (f * W.Socket.x + tt * W.Socket.y + n * W.Socket.z) * hl);
            hasSock[i] = true;
            if (W.GripY == Vector3.zero || W.GripZ == Vector3.zero) return;
            Vector3 y = (f * W.GripY.x + tt * W.GripY.y + n * W.GripY.z).normalized;
            Vector3 z = f * W.GripZ.x + tt * W.GripZ.y + n * W.GripZ.z;
            z = Vector3.ProjectOnPlane(z, y).normalized;
            if (z.sqrMagnitude < 0.5f) return;
            anatR[i] = Quaternion.Inverse(hd.rotation) * Quaternion.LookRotation(z, y);
            hasAnat[i] = true;
        }

        // length and grip into the bind placement (again when the tuner changes them)
        void Place(int i)
        {
            WeaponDef W = defs[i];
            float L = W.Len * figH, k = L / meshH[i] / handS[i];
            Transform t = wT[i];
            bindP[i] = palmL[i] - handleV[i] * (W.Grip * L);
            bindS[i] = new Vector3(k, k, k);
            t.localPosition = bindP[i]; t.localRotation = bindR[i]; t.localScale = bindS[i];
            gripL[i] = new Vector3(shaftC[i].x, W.Grip * meshH[i], shaftC[i].z);
            placedLen[i] = W.Len; placedGrip[i] = W.Grip;
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
            sCenter = mesh.bounds.center; sPlacedDia = S.Dia;
        }
        Vector3 sCenter;

        // as MxCannon: on the chest bone, laid along the chest's own axes in the bind pose (up from the hips to the head, right
        // across the shoulders, forward from those), its seat that far from the right shoulder joint; then slid back towards the
        // shoulder until the rear grip is within 0.9 of the arm's length (a short-armed body could not get its fist to it)
        void AddCannon(CannonDef C, Animator an, float H, List<Renderer> rends, List<Material> op, List<Material> fd)
        {
            Mesh mesh = Props.Mesh(C.Model);
            Transform chest = an.GetBoneTransform(HumanBodyBones.UpperChest) ?? an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.Spine),
                sh = an.GetBoneTransform(HumanBodyBones.RightUpperArm), lsh = an.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                hips = an.GetBoneTransform(HumanBodyBones.Hips), head = an.GetBoneTransform(HumanBodyBones.Head),
                ra = an.GetBoneTransform(HumanBodyBones.RightLowerArm), rh = an.GetBoneTransform(HumanBodyBones.RightHand);
            if (mesh == null || chest == null || sh == null || lsh == null || hips == null || head == null || ra == null || rh == null) return;
            Vector3 up = (head.position - hips.position).normalized, right = sh.position - lsh.position;
            right = (right - up * Vector3.Dot(right, up)).normalized;
            Vector3 fwd = Vector3.Cross(right, up).normalized;
            float k = C.Len * H / Mathf.Max(1e-4f, mesh.bounds.size.x);
            var go = new GameObject("Cannon " + C.Model);
            MakeRenderer(go, mesh, C.Model, rends, op, fd);
            Transform t = go.transform;
            t.rotation = Quaternion.LookRotation(Vector3.Cross(fwd, up), up); // the model's +x (the mouth) forward, +y up
            t.localScale = new Vector3(k, k, k);
            Vector3 seat = sh.position + up * (C.At.x * H) + right * (C.At.y * H) + fwd * (C.At.z * H);
            t.position = seat - t.rotation * (C.Seat * k);
            float reach = 0.9f * ((ra.position - sh.position).magnitude + (rh.position - ra.position).magnitude);
            for (int i = 0; i < 16; i++)
            {
                Vector3 rg = t.TransformPoint(C.Grip);
                float over = (rg - sh.position).magnitude - reach;
                if (over <= 0f) break;
                t.position += (sh.position - rg).normalized * Mathf.Min(over, 0.04f * H);
            }
            t.SetParent(chest, true);
            cDef = C; cT = t;
            cAt = t.localPosition;
            cRot = t.localRotation; cScale = t.localScale; cLen = C.Len;
            cBack = t.localRotation * Vector3.left * (C.Kick * H / Mathf.Max(1e-6f, chest.lossyScale.y));
            cUp = sh; cLo = ra; cHand = rh; cOther = lsh;
            cIndex = an.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        }

        /// <summary>Back in the hand, for a fresh life out of the pool.</summary>
        public void Reset()
        {
            hideIn = -1f; backIn = -1f; hideHeld = false; hideSide = 0;
            for (int i = 0; i < wT.Length; i++) if (wT[i] != null) wT[i].localScale = bindS[i];
            kickIn = -1f; kickT = -1f; deadT = 0f; liveW = 1f;
            if (cT != null) cT.localPosition = cAt;
        }

        /// <summary>A thrown weapon leaves the hand after <paramref name="delay"/> seconds (the release) and is back in it about
        /// <paramref name="away"/> seconds later. <paramref name="held"/>: a weapon that is not Thrown goes too (Knut's anchor on the
        /// hook, Tikta's needle); the next throw without it (a swing) puts it back in the fist at once. <paramref name="side"/>: only
        /// the right hand's (1) or the left's (-1), 0 both.</summary>
        public void Throw(float delay, float away = AwayFor, bool held = false, int side = 0)
        {
            hideIn = Mathf.Max(0f, delay);
            backIn = -1f;
            awayFor = Mathf.Max(PopIn, away);
            hideHeld = held;
            hideSide = side;
        }

        /// <summary>Where a shot leaves this gear now (world space): the cannon's mouth; with a <paramref name="bow"/> the arrow at its
        /// grip; a thrown bomb in the palm; else the head of the weapon, its end away from the fist (the hammer's, the anchor's flukes, a
        /// needle's point), in the <paramref name="right"/> hand if there is one in each. False without a weapon.</summary>
        public bool ShotFrom(bool bow, bool right, out Vector3 at)
        {
            if (Mouth(out at)) return true;
            int i = -1;
            for (int k = 0; k < wT.Length; k++)
                if (wT[k] != null && (i < 0 || defs[k].Left != right)) i = k;
            if (i < 0) return false;
            WeaponDef W = defs[i];
            Transform t = wT[i];
            if (W.Thrown) { at = hand[i].TransformPoint(palmL[i]); return true; } // the bomb in the palm, also once it has left
            float y = bow ? gripL[i].y : W.Grip >= 0.5f ? meshH[i] * 0.1f : meshH[i] * 0.9f;
            // in the weapon's own space at its placed size, so a needle already flashed away (scaled to 0) still gives its point
            at = t.parent.TransformPoint(t.localPosition + t.localRotation * Vector3.Scale(bindS[i], new Vector3(shaftC[i].x, y, shaftC[i].z)));
            return true;
        }

        /// <summary>The swing or cast was cut before its release (EventType.Interrupt): a weapon waiting to leave the hand stays, the
        /// cannon does not kick; one already away comes back as usual.</summary>
        public void Cancel() { hideIn = -1f; kickIn = -1f; }

        /// <summary>A cannon kicks back as its shot leaves, after <paramref name="delay"/> seconds (the blow).</summary>
        public void Fire(float delay)
        {
            if (cT != null) kickIn = Mathf.Max(0f, delay);
        }

        /// <summary>Where the cannon's mouth is now, for a shot to leave from; false without a cannon.</summary>
        public bool Mouth(out Vector3 at)
        {
            at = cT != null ? cT.TransformPoint(cDef.Mouth) : Vector3.zero;
            return cT != null;
        }

        /// <param name="moveW">0 standing .. 1 on the move.</param>
        /// <param name="actW">How much an attack or cast clip is playing (0..1).</param>
        public void Tick(float dt, float moveW, float actW, float atkW, bool dead)
        {
            TickWorn(dt, moveW, atkW, dead);
            float show = 1f;
            if (hideIn >= 0f)
            {
                hideIn -= dt;
                if (hideIn < 0f) { hideIn = -1f; backIn = awayFor; }
            }
            if (backIn >= 0f)
            {
                backIn -= dt;
                show = backIn > PopIn ? 0f : 1f - Mathf.Max(0f, backIn) / PopIn;
                if (backIn < 0f) { backIn = -1f; show = 1f; }
            }
            // GearDef.StillFingers: the fingers back in their held pose before anything below bends them (Close hand); a finger the
            // Avatar drives plays its clip in a swing or cast (Tyra's draw), one it does not is always set whole
            if (stillB != null)
            {
                float sw = Mathf.Clamp01(actW);
                for (int i = 0; i < stillB.Length; i++)
                    stillB[i].localRotation = stillM[i] && sw > 0.001f ? Quaternion.Slerp(stillR[i], stillB[i].localRotation, sw) : stillR[i];
            }
            if (gdef.ArmSpread + gdef.HandsOff > 0f) ArmSpread(gdef.ArmSpread + gdef.HandsOff);
            ArmTune(dead ? 0f : 1f - Mathf.Clamp01(actW));
            if (carryHand != null && carryLo != null && carryUp != null) CarryOnShoulder(dead ? 0f : 1f - Mathf.Clamp01(actW));
            // a death lets go of the rest hold over a quarter of a second instead of in one frame: at the blow that killed him Bram's
            // hammer jumped about 155 degrees into the fist's own grip in one frame (WeaponDebug, Rickard's Unity 2026-10-30); a
            // respawn comes back at once (he appears at his spawn)
            liveW = dead ? Mathf.MoveTowards(liveW, 0f, dt / DeathLetGo) : 1f;
            bool gone = dead && liveW <= 0.001f;
            for (int i = 0; i < wT.Length; i++)
            {
                Transform t = wT[i];
                if (t == null) continue;
                WeaponDef W = defs[i];
                if (W.Len != placedLen[i] || W.Grip != placedGrip[i]) Place(i);
                if ((W.Thrown || hideHeld) && (hideSide == 0 || hideSide > 0 != W.Left)) t.localScale = bindS[i] * show;
                else if (t.localScale != bindS[i]) t.localScale = bindS[i]; // a held weapon put back by a swing during its throw
                float rest = (1f - Mathf.Clamp01(actW)) * liveW;
                float rw = W.Run.On ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveW)) * liveW : 0f;
                if (W.SwingAim) rw *= rest; // a SwingAim weapon swings from its rest aim, not its run hold (Tikta's needles point back in the run)
                if (W.HoldUp == 0f || gone) { t.localPosition = bindP[i]; t.localRotation = bindR[i]; }
                else if (OnMenu && W.Menu.On) HoldAngle(i, W, rest, W.MenuSign, W.Menu.Up, W.Menu.Out); // a tapped swing plays the fist's grip
                else if (AnimFix.FixedGrip)
                {
                    // the rest hold, and the run's eased in as the hero moves (Tyra's bow at her side)
                    RestHold(i, W, rest, new Hold { Up = W.HoldUp, Out = W.HoldOut, Turn = W.RestTurn }, laxSign[i]);
                    if (rw > 0.001f)
                    {
                        Quaternion ra = t.rotation;
                        Vector3 ga = t.TransformPoint(gripL[i]);
                        RestHold(i, W, rest, W.Run, W.RunSign != 0 ? W.RunSign : runSign[i]);
                        Vector3 gb = t.TransformPoint(gripL[i]);
                        t.rotation = Quaternion.Slerp(ra, t.rotation, rw);
                        t.position += Vector3.Lerp(ga, gb, rw) - t.TransformPoint(gripL[i]);
                    }
                }
                else HoldAngle(i, W, rest, 0);
                // Rickard's Turn is tuned on the rest hold, so with FixedGrip it goes as the rest aim goes: through a swing the weapon
                // is in the fist's own grip (Fenn's -33/55 left his blade end-on to the match camera at the blow, 2026-10-28); a
                // SwingAim weapon keeps its rest aim and its Turn, a weapon without a rest aim and a dead hero's keep the Turn whole;
                // a run hold is set without it, so it goes out as the run hold comes in (on Fenn's run hold his -33/55 tipped the blade
                // forward like a lance again, Blender renders 2026-10-29 checked against Unity run 1)
                if (W.Turn != Vector3.zero)
                {
                    float tw = !AnimFix.FixedGrip || W.HoldUp == 0f || gone || (W.SwingAim && rested) ? 1f : rest;
                    if (AnimFix.FixedGrip && !gone && W.HoldUp != 0f) tw *= 1f - rw;
                    if (dead && !gone) tw = Mathf.Lerp(1f, tw, liveW); // towards the dead hero's whole Turn as the hold lets go
                    if (tw > 0.001f) TurnAbout(i, Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(W.Turn), tw));
                }
                float sw = Mathf.Clamp01(atkW) * liveW;
                if (AnimFix.FixedGrip && W.SwingTurn != Vector3.zero && sw > 0.001f)
                    TurnAbout(i, Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(W.SwingTurn), sw));
                // every turn above is about the grip, so the aim is final here: the grip goes into the palm (a shift, never a turn, so
                // the silhouette Rickard picked stays), before his Offset, which is his own grip touch-up and so stays on top of it
                // Tyra pilot, AnimFix.GripLockFull (on by default since Rickard 2026-10-31): the weapon is held only as the
                // fist holds it (the anatomical grip), whatever the hold above aimed; the wrist has nothing to take, the bow's look
                // then follows the clip's hand
                bool full = AnimFix.GripLockFull && hasAnat[i] && !gone && t.localScale.y > 1e-4f;
                if (full) { t.rotation = hand[i].rotation * anatR[i]; FistTwist[i] = 0f; } // nothing left for the wrist to take
                if (W.WristMax > 0f && !gone && !full) Wrist(i, W, liveW);
                LockGrip(i, full);
                // Rickard's Offset was tuned on the 0.6 palm; with the calibrated socket the grip is already in the fist (Tyra's -0.03
                // down would move it 3 % of her height off it again), so it is left out while the grip is locked to the socket
                // (2026-11-01) only for a weapon with the anatomical grip (Tyra): Knut's and Fenn's socket is a grip point only, and
                // Rickard moves their weapon on top of it with his Offset ("då antar jag att jag bara flyttar den")
                bool inSocket = AnimFix.PalmSocket && hasSock[i] && hasAnat[i] && (AnimFix.GripLock || full);
                if (W.Offset != Vector3.zero && !inSocket) t.position += root.TransformDirection(W.Offset) * figH;
                if (W.Fist > 0.001f && !gone) CloseHand(i, W.Fist * liveW);
                if (Legs != null && LegsW > 0.001f && !dead) OffLegs(i);
                if (Floor > float.NegativeInfinity) AboveGround(i, dt);
                if (supHand[i] != null && !dead && W.SupportW > 0.001f && t.localScale.y > 1e-4f) SupportHand(i, W);
                if (WeaponDebug.On && !gone) Jumps(i);
            }
            if (sAnchor != null) ShieldFace(moveW);
            if (cT != null) CannonTick(dt, dead);
        }

        /// <summary>Weapon Precision 2.0: the weapon's grip point (gripL, the shaft's own centre at <see cref="WeaponDef.Grip"/>, in the
        /// weapon's space) and the palm (palmL: 0.6 of the way from the wrist to the index knuckle, in the hand's space; an Along staff:
        /// where the idle held it, restGrip). The bind placement lays the mesh's axis, not the shaft's centre, in the fist, and HoldAngle
        /// slides the grip across the shaft only, so without the lock the grip sat up to 3 % of the hero's height off the palm and slid
        /// along the shaft as the hand turned (Simon's audit on issue #5). Measured before the lock (GripErr), then removed by a shift.</summary>
        /// <summary>Degrees each finger joint bends at <see cref="WeaponDef.Fist"/> 1.</summary>
        internal const float FistBend = 70f;
        Vector3[] curlAx, thumbAx; // Close hand's bend axes per weapon hand, in the hand bone's own space, fixed at the first use
        // Close hand's finger poses before and after its bend, per weapon hand and finger bone (see the reset in CloseHand)
        Quaternion[][] curlBase, curlSet;

        // the fingers curled towards the weapon's shaft, on top of the clip (each frame from the clip's own pose, so nothing adds up)
        void CloseHand(int i, float k)
        {
            Transform t = wT[i];
            Transform[] f = fing[i];
            if (f == null || t.localScale.y < 1e-4f) return;
            Vector3 g = t.TransformPoint(gripL[i]), ax = t.up;
            // One shared bend axis for the four fingers: the knuckle line (index root to pinky root), turned so the index curls
            // towards the shaft. Each finger bending round its own axis made them swing sideways into each other and read as
            // one giant finger (Rickard 2026-11-01, Tyra); a shared axis keeps them side by side, like a real fist.
            // The axes are fixed in the hand's own space the first time (Cajsa and Rickard 2026-11-04: "Tyras fingrar fladdrar"):
            // chosen anew every frame from the shaft, the side flipped whenever the shaft crossed the knuckles (a bow through the
            // fist), and the fingers curled one way and back again frame by frame. With a calibrated palm socket the side is
            // towards the socket, the palm's own centre, which no pose can flip.
            Transform hb = f[3] != null ? f[3].parent : (f[0] != null ? f[0].parent : null);
            if (hb == null) return;
            // Start every frame from the clip's finger pose. A finger the Avatar does not map (Tyra's maps only the index; the
            // others are found by name) is never written by the Animator, so the bend was added on top of last frame's bend and
            // the fingers spun round and round ("fingrar buggar fortfarande", Rickard 2026-11-04 after Cajsa's "fladdrar").
            // A bone still exactly as this bend left it was not touched by the clip: it goes back to its pose before the bend.
            if (curlBase == null || curlBase.Length != fing.Length) { curlBase = new Quaternion[fing.Length][]; curlSet = new Quaternion[fing.Length][]; }
            if (curlBase[i] == null) { curlBase[i] = new Quaternion[15]; curlSet[i] = new Quaternion[15]; }
            for (int q = 0; q < 15; q++)
            {
                if (f[q] == null) continue;
                Quaternion now = f[q].localRotation, set = curlSet[i][q];
                bool untouched = set.w != 0f || set.x != 0f || set.y != 0f || set.z != 0f;
                untouched = untouched && now.x == set.x && now.y == set.y && now.z == set.z && now.w == set.w;
                if (untouched) f[q].localRotation = curlBase[i][q]; else curlBase[i][q] = now;
            }
            if (curlAx == null || curlAx.Length != fing.Length) { curlAx = new Vector3[fing.Length]; thumbAx = new Vector3[fing.Length]; }
            if (curlAx[i] == Vector3.zero && f[3] != null && f[4] != null && f[12] != null)
            {
                Vector3 kn = (f[12].position - f[3].position).normalized, dir = f[4].position - f[3].position;
                Vector3 side = hasSock[i] ? Vector3.Cross(dir, hand[i].TransformPoint(sockL[i]) - f[3].position).normalized
                    : BendAxis(f[3].position, dir, g, ax);
                if (side != Vector3.zero && kn != Vector3.zero)
                    curlAx[i] = hb.InverseTransformDirection(Vector3.Dot(side, kn) < 0f ? -kn : kn);
            }
            if (thumbAx[i] == Vector3.zero && f[0] != null && f[1] != null)
            {
                Vector3 dir = f[1].position - f[0].position;
                Vector3 th = hasSock[i] ? Vector3.Cross(dir, hand[i].TransformPoint(sockL[i]) - f[0].position).normalized
                    : BendAxis(f[0].position, dir, g, ax);
                if (th != Vector3.zero) thumbAx[i] = hb.InverseTransformDirection(th);
            }
            Vector3 knuckle = curlAx[i] != Vector3.zero ? hb.TransformDirection(curlAx[i]) : Vector3.zero;
            Vector3 thumb = thumbAx[i] != Vector3.zero ? hb.TransformDirection(thumbAx[i]) : Vector3.zero;
            for (int n = 0; n < 5; n++)
            {
                Transform a = f[n * 3], b = f[n * 3 + 1];
                if (a == null || b == null) continue;
                Vector3 axis = n == 0 ? thumb : knuckle;
                if (axis == Vector3.zero) continue;
                // every joint bends a share of FistBend on top of its parent, so the finger curls instead of pivoting stiffly at
                // the knuckle; world rotations stack, so joint j ends up bent by its own share relative to joint j-1
                for (int j = 0; j < 3; j++)
                {
                    Transform bone = f[n * 3 + j];
                    if (bone == null) break;
                    bone.rotation = Quaternion.AngleAxis(FistBend * k * CurlShare[j] * (n == 0 ? 0.5f : 1f), axis) * bone.rotation;
                }
            }
            for (int q = 0; q < 15; q++) if (f[q] != null) curlSet[i][q] = f[q].localRotation;
        }

        /// <summary>Share of <see cref="FistBend"/> per finger joint (knuckle, middle, tip) for Close hand.</summary>
        static readonly float[] CurlShare = { 0.5f, 0.6f, 0.4f };

        /// <summary>The axis that turns a finger (root at a, pointing dir) across itself towards the nearest point of the shaft
        /// (through g along ax); zero when undefined.</summary>
        static Vector3 BendAxis(Vector3 a, Vector3 dir, Vector3 g, Vector3 ax)
        {
            if (dir.sqrMagnitude < 1e-10f) return Vector3.zero;
            Vector3 onShaft = g + ax * Vector3.Dot(a - g, ax);
            Vector3 toShaft = Vector3.ProjectOnPlane(onShaft - a, dir);
            if (toShaft.sqrMagnitude < 1e-10f) return Vector3.zero;
            return Vector3.Cross(dir, toShaft).normalized;
        }

        void LockGrip(int i, bool force = false)
        {
            Transform t = wT[i];
            if (t.localScale.y < 1e-4f) return; // away (thrown)
            WeaponDef W = defs[i];
            // Tyra pilot (AnimFix.PalmSocket): the calibrated socket instead of the 0.6 rule
            Vector3 palm = AnimFix.PalmSocket && hasSock[i] ? hand[i].TransformPoint(sockL[i])
                : W.Along && rested && W.HoldUp != 0f && !sampling ? hand[i].TransformPoint(restGrip[i]) : hand[i].TransformPoint(palmL[i]);
            Vector3 d = palm - t.TransformPoint(gripL[i]);
            float e = d.magnitude / Mathf.Max(1e-4f, figH);
            GripErr[i] = e;
            if (e > GripErrMax[i]) GripErrMax[i] = e;
            if (AnimFix.GripLock || force) t.position += d;
        }

        /// <summary>Weapon Precision 2.0 (AnimFix.WristTurn): the weapon's aim is final; the turn it has in the fist away from the shot's
        /// grip (bindR, with the swing turn) is taken by the wrist instead, up to WristMax degrees, and the weapon set back to the same
        /// aim in the world. The hand turns about the wrist joint, so the palm moves a little; LockGrip runs after this.</summary>
        void Wrist(int i, WeaponDef W, float weight)
        {
            Transform t = wT[i], hd = hand[i];
            if (t.localScale.y < 1e-4f) return;
            Quaternion aim = t.rotation;
            // Tyra pilot (AnimFix.AnatomicalWrist): the grip the wrist works towards is the one her fist makes in the clip made for
            // the bow (anatR), not the bind placement, which lay 53 degrees off her knuckle line
            Quaternion shotGrip = AnimFix.AnatomicalWrist && hasAnat[i] ? anatR[i]
                : W.SwingTurn != Vector3.zero ? bindR[i] * Quaternion.Euler(W.SwingTurn) : bindR[i];
            // the hand that would hold this aim in the shot's grip: aim = hand' * shotGrip
            Quaternion want = aim * Quaternion.Inverse(shotGrip);
            Quaternion d = want * Quaternion.Inverse(hd.rotation);
            if (d.w < 0f) d = new Quaternion(-d.x, -d.y, -d.z, -d.w);
            float ang = 2f * Mathf.Acos(Mathf.Min(1f, d.w)) * Mathf.Rad2Deg;
            // what is left in the fist after the wrist's share (all of it with the switch off)
            float left = AnimFix.WristTurn ? Mathf.Max(0f, ang - W.WristMax) : ang;
            FistTwist[i] = left;
            if (left > FistTwistMax[i]) FistTwistMax[i] = left;
            if (!AnimFix.WristTurn || ang < 0.01f) return;
            float k = Mathf.Min(1f, W.WristMax / ang) * weight;
            Vector3 g = t.TransformPoint(gripL[i]);
            hd.rotation = Quaternion.Slerp(Quaternion.identity, d, k) * hd.rotation;
            // same aim, the grip back where it was (LockGrip then moves it to the turned palm)
            t.rotation = aim;
            t.position += g - t.TransformPoint(gripL[i]);
        }

        /// <summary>The other arm bent so its palm holds the weapon at <see cref="WeaponDef.Support"/> (two-bone IK, elbow out and down).</summary>
        void SupportHand(int i, WeaponDef W)
        {
            Transform t = wT[i], up = supUp[i], lo = supLo[i], hd = supHand[i];
            if (up == null || lo == null) return;
            Vector3 at = t.TransformPoint(new Vector3(shaftC[i].x, W.Support * meshH[i], shaftC[i].z));
            Vector3 toPalm = supIndex[i] != null ? (supIndex[i].position - hd.position) * 0.6f : Vector3.zero;
            Vector3 side = W.Left ? root.right : -root.right;
            Vector3 hint = lo.position + side * (0.1f * figH) - root.up * (0.05f * figH);
            float w = Mathf.Clamp01(W.SupportW);
            TwoBone(up, lo, hd, Vector3.Lerp(hd.position, at - toPalm, w), hint);
        }

        // the weapon's largest turn in one frame in the hero's own space (debug view only)
        void Jumps(int i)
        {
            Quaternion q = Quaternion.Inverse(root.rotation) * wT[i].rotation;
            if (lastLocal[i].w != 0f || lastLocal[i].x != 0f || lastLocal[i].y != 0f || lastLocal[i].z != 0f)
            {
                float a = Quaternion.Angle(lastLocal[i], q);
                if (a > JumpMax[i]) JumpMax[i] = a;
            }
            lastLocal[i] = q;
        }

        /// <summary>The debug view's Reset: the worst grip error and turn start over.</summary>
        internal void ResetStats()
        {
            for (int i = 0; i < wT.Length; i++) { GripErrMax[i] = 0f; JumpMax[i] = 0f; FistTwistMax[i] = 0f; lastLocal[i] = default; }
        }

        internal int WeaponCount => wT.Length;
        internal Transform WeaponT(int i) => wT[i];
        internal Transform HandT(int i) => hand[i];
        internal WeaponDef Def(int i) => defs[i];
        /// <summary>The run hold's flat side the idle voted for (the tuner's flip turns the other way from it).</summary>
        internal int RunVote(int i) => runSign[i];
        /// <summary>The weapon's grip point and the palm it is locked to, now (world space).</summary>
        internal void GripPoints(int i, out Vector3 grip, out Vector3 palm)
        {
            grip = wT[i].TransformPoint(gripL[i]);
            palm = AnimFix.PalmSocket && hasSock[i] ? hand[i].TransformPoint(sockL[i]) : hand[i].TransformPoint(palmL[i]);
        }
        /// <summary>A two-handed weapon's support point now; false without one.</summary>
        internal bool SupportPoint(int i, out Vector3 at)
        {
            at = Vector3.zero;
            if (defs[i] == null || defs[i].Support < 0f || wT[i] == null) return false;
            at = wT[i].TransformPoint(new Vector3(shaftC[i].x, defs[i].Support * meshH[i], shaftC[i].z));
            return true;
        }
        /// <summary>The weapon's two ends (handle, head) now.</summary>
        internal void Ends(int i, out Vector3 handleEnd, out Vector3 head)
        {
            handleEnd = wT[i].TransformPoint(new Vector3(shaftC[i].x, 0f, shaftC[i].z));
            head = wT[i].TransformPoint(new Vector3(shaftC[i].x, meshH[i], shaftC[i].z));
        }

        // as MxCannonArms: the recoil (back fast, forward slowly), then the right fist on the rear grip; in death the fist lets go
        // over a quarter of a second
        void CannonTick(float dt, bool dead)
        {
            if (kickIn >= 0f)
            {
                kickIn -= dt;
                if (kickIn < 0f) { kickIn = -1f; kickT = 0f; }
            }
            float kick = 0f;
            if (kickT >= 0f)
            {
                float kt = kickT / cDef.KickTime;
                kick = kt < 0.15f ? kt / 0.15f : 1f - (kt - 0.15f) / 0.85f;
                kickT += dt;
                if (kickT >= cDef.KickTime) kickT = -1f;
            }
            cT.localPosition = cAt + cBack * kick;
            // the Weapon Tuner's numbers, live (HILDE-04): size, turn about its own axes, shift in the hero's right/up/forward
            cT.localScale = cScale * (cLen > 0f ? cDef.Len / cLen : 1f);
            cT.localRotation = cDef.Turn != Vector3.zero ? cRot * Quaternion.Euler(cDef.Turn) : cRot;
            if (cDef.Offset != Vector3.zero) cT.position += root.TransformDirection(cDef.Offset) * figH;
            deadT = dead ? deadT + dt : 0f;
            float w = dead ? Mathf.Clamp01(1f - deadT / 0.25f) : 1f;
            if (w > 0.001f) CannonFist(w);
        }

        // as CannonFist: the palm, not the wrist, on the grip; then the arm swung about the shoulder-to-fist line until the elbow
        // points out from the body and a little down, not into the chest (Rickard 2026-10-19: her arm cut through her body)
        void CannonFist(float w)
        {
            Vector3 toPalm = cIndex != null ? (cIndex.position - cHand.position) * 0.6f : Vector3.zero;
            Vector3 goal = cT.TransformPoint(cDef.Grip) - toPalm;
            TwoBone(cUp, cLo, cHand, Vector3.Lerp(cHand.position, goal, w), cLo.position);
            Vector3 axis = cHand.position - cUp.position;
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();
            Vector3 pole = (cUp.position - cOther.position).normalized - root.up * 0.35f;
            Vector3 elbow = Vector3.ProjectOnPlane(cLo.position - cUp.position, axis), want = Vector3.ProjectOnPlane(pole, axis);
            if (elbow.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) return;
            cUp.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, want, axis) * w, axis) * cUp.rotation;
        }

        // ANIM-08/09 (Unity measuring run 1, 2026-10-29): the borrowed swings and the fall put a long weapon's end through the ground
        // (Fenn's sword tip 0.8-0.96 below it at every blow, Tyra's bow and Knut's anchor 0.5 as they fall, Bonk's club 0.28). The
        // end below the ground is turned up about the grip, keeping its way across the ground, until it rests on it: the blade
        // sweeps along the ground instead of into it. Nearly straight down, it goes the way the hero faces; and its way across the
        // ground turns at most 1800 degrees a second (about as fast as the swing itself), so a blade swung through straight down
        // (Fenn's, from behind him to the front in one frame) sweeps round along the ground instead of jumping.
        void AboveGround(int i, float dt)
        {
            Transform t = wT[i];
            if (lowDir == null || lowDir.Length != wT.Length) lowDir = new Vector3[wT.Length];
            if (t.localScale.y < 1e-4f) { lowDir[i] = Vector3.zero; return; } // away (thrown)
            float floor = Floor + 0.01f;
            Vector3 g = t.TransformPoint(gripL[i]);
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 a = t.TransformPoint(new Vector3(shaftC[i].x, 0f, shaftC[i].z));
                Vector3 b = t.TransformPoint(new Vector3(shaftC[i].x, meshH[i], shaftC[i].z));
                Vector3 v = (a.y < b.y ? a : b) - g;
                if (g.y + v.y >= floor) { if (pass == 0) lowDir[i] = Vector3.zero; return; }
                float L = v.magnitude;
                if (L < 1e-5f) return;
                Vector3 h = new Vector3(v.x, 0f, v.z), fw = new Vector3(root.forward.x, 0f, root.forward.z);
                if (fw.sqrMagnitude > 1e-8f) h += fw.normalized * Mathf.Max(0f, 0.35f * L - h.magnitude);
                if (h.sqrMagnitude < 1e-10f) return;
                if (pass == 0)
                {
                    if (lowDir[i] != Vector3.zero) h = Vector3.RotateTowards(lowDir[i], h.normalized, 1800f * Mathf.Deg2Rad * dt, 0f);
                    lowDir[i] = h.normalized;
                }
                float y = Mathf.Clamp(floor - g.y, -L, L);
                Vector3 want = h.normalized * Mathf.Sqrt(L * L - y * y) + Vector3.up * y;
                Quaternion q = Quaternion.FromToRotation(v, want);
                t.SetPositionAndRotation(g + q * (t.position - g), q * t.rotation);
            }
        }

        // Tyra_02/05 (Unity measuring run 1, 2026-10-29): at twice her speed, and strafing with her hips turned the way she runs, her
        // legs swung out through the bow she carries upright at her side (its lower limb through her thigh). An end of the weapon
        // within a leg's thickness of a leg (hip to knee to ankle) is turned away about the grip, straight out from that leg across
        // the ground, by LegsW; the length and the grip stay, so the other limb leans in a little the other way
        const float LegClear = 0.06f;
        void OffLegs(int i)
        {
            Transform t = wT[i];
            if (t.localScale.y < 1e-4f || Legs.Length < 6) return;
            float r = LegClear * figH;
            Vector3 g = t.TransformPoint(gripL[i]);
            for (int e = 0; e < 2; e++)
            {
                Vector3 end = t.TransformPoint(new Vector3(shaftC[i].x, e == 0 ? 0f : meshH[i], shaftC[i].z));
                float d = r;
                Vector3 near = end;
                for (int k = 0; k < 5; k++)
                {
                    if (k == 2 || Legs[k] == null || Legs[k + 1] == null) continue; // hip-knee and knee-ankle of each leg
                    Vector3 a = Legs[k].position, ab = Legs[k + 1].position - a;
                    Vector3 c = a + ab * Mathf.Clamp01(ab.sqrMagnitude > 1e-8f ? Vector3.Dot(end - a, ab) / ab.sqrMagnitude : 0f);
                    float dk = (end - c).magnitude;
                    if (dk < d) { d = dk; near = c; }
                }
                if (d >= r) continue;
                Vector3 away = end - near;
                away.y = 0f;
                if (away.sqrMagnitude < 1e-8f) away = root.right * (defs[i].Left ? -1f : 1f);
                Vector3 v = end - g, want = end + away.normalized * ((r - d) * Mathf.Clamp01(LegsW)) - g;
                if (v.sqrMagnitude < 1e-8f || want.sqrMagnitude < 1e-8f) continue;
                Quaternion q = Quaternion.FromToRotation(v, want);
                t.SetPositionAndRotation(g + q * (t.position - g), q * t.rotation);
            }
        }

        // turned by a rotation in the weapon's own axes, about its grip
        void TurnAbout(int i, Quaternion local)
        {
            Transform t = wT[i];
            Vector3 gg = t.TransformPoint(gripL[i]);
            Quaternion q = t.rotation * local * Quaternion.Inverse(t.rotation);
            t.SetPositionAndRotation(gg + q * (t.position - gg), q * t.rotation);
        }

        // MxArmSpread: about the hero's forward axis, so a hanging arm swings out from the side (before the weapons read the hands)
        void ArmSpread(float spread)
        {
            Vector3 fwd = root.forward;
            if (armR != null) armR.rotation = Quaternion.AngleAxis(spread * Hang(armR, elbR), fwd) * armR.rotation;
            if (armL != null) armL.rotation = Quaternion.AngleAxis(-spread * Hang(armL, elbL), fwd) * armL.rotation;
        }

        // the tuner's RightArm/LeftArm at rest (w), in the hero's own axes: out from the side, forward, then the elbow's bend
        void ArmTune(float w)
        {
            if (w <= 0.001f) return;
            if (gdef.RightArm != Vector3.zero) ArmTune(armR, elbR, wriR, gdef.RightArm * w, 1f);
            if (gdef.LeftArm != Vector3.zero) ArmTune(armL, elbL, wriL, gdef.LeftArm * w, -1f);
        }

        void ArmTune(Transform up, Transform lo, Transform hd, Vector3 a, float side)
        {
            if (up == null || lo == null) return;
            up.rotation = Quaternion.AngleAxis(-a.y, root.right) * Quaternion.AngleAxis(a.x * side, root.forward) * up.rotation;
            if (a.z == 0f || hd == null) return;
            Vector3 u = lo.position - up.position, l = hd.position - lo.position;
            Vector3 axis = Vector3.Cross(u, l);
            if (axis.sqrMagnitude < 1e-10f) axis = side * root.right; // a straight arm bends forward
            lo.rotation = Quaternion.AngleAxis(-a.z, axis.normalized) * lo.rotation;
        }

        // AnimFix.ArmRoom: 1 for an upper arm hanging down (up to 45 degrees off, as in the run), less as it rises, 0 from level up
        float Hang(Transform up, Transform lo)
        {
            if (!AnimFix.ArmRoom || lo == null) return 1f;
            Vector3 d = lo.position - up.position;
            float m = d.magnitude;
            return m < 1e-6f ? 1f : Mathf.Clamp01(Vector3.Dot(d / m, -root.up) / 0.7f);
        }

        // The right arm bent by a two-bone IK after the clips (and the menu's breath): the wrist goes a little in front of and
        // below the shoulder, the elbow down and out in front. w: 1 at rest, 0 while a swing or cast plays, so those play as made.
        void CarryOnShoulder(float w)
        {
            if (w <= 0.001f) return;
            Vector3 fwd = root.forward, upW = root.up, rt = root.right;
            Vector3 a = carryUp.position;
            float H = figH;
            Vector3 ca = gdef.CarryAt - new Vector3(0f, 0f, gdef.CarryOut), el = gdef.Elbow;
            Vector3 goal = a + fwd * (ca.x * H) - upW * (ca.y * H) - rt * (ca.z * H);
            Vector3 hint = a - upW * (el.x * H) + fwd * (el.y * H) + rt * (el.z * H);
            TwoBone(carryUp, carryLo, carryHand, Vector3.Lerp(carryHand.position, goal, w), Vector3.Lerp(carryLo.position, hint, w));
        }

        // two-bone IK: the upper arm and forearm turned so the wrist reaches the goal (as near as the arm's length allows), the
        // elbow bent towards the hint
        void TwoBone(Transform upper, Transform lower, Transform hd, Vector3 goal, Vector3 hint)
        {
            Vector3 a = upper.position, b = lower.position, c = hd.position;
            float la = (b - a).magnitude, lb = (c - b).magnitude;
            if (la < 1e-5f || lb < 1e-5f) return;
            Vector3 at = goal - a;
            float d = Mathf.Clamp(at.magnitude, Mathf.Abs(la - lb) + 1e-4f, la + lb - 1e-4f);
            Vector3 dir = at.sqrMagnitude > 1e-10f ? at.normalized : root.forward;
            float x = (la * la - lb * lb + d * d) / (2f * d), hg = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
            Vector3 pole = Vector3.ProjectOnPlane(hint - a, dir);
            pole = pole.sqrMagnitude > 1e-10f ? pole.normalized : Vector3.ProjectOnPlane(-root.up, dir).normalized;
            Vector3 elbow = a + dir * x + pole * hg;
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            Vector3 lo = lower.position;
            lower.rotation = Quaternion.FromToRotation(hd.position - lo, a + dir * d - lo) * lower.rotation;
        }

        /// <summary>AnimFix.FixedGrip wants the idle measured: the first time, or after the tuner changed a number the rest aim
        /// depends on (BodyView then calls <see cref="BeginRest"/>, <see cref="SampleRest"/> per idle frame, <see cref="EndRest"/>).</summary>
        public bool NeedsRest
        {
            get
            {
                if (!AnimFix.FixedGrip) return false;
                bool aims = false;
                for (int i = 0; i < defs.Length; i++) if (defs[i] != null && defs[i].HoldUp != 0f) aims = true;
                return aims && (!rested || restKey != RestKey());
            }
        }

        public void BeginRest()
        {
            for (int i = 0; i < wT.Length; i++) { votes[i] = runVotes[i] = 0; refSum[i] = Vector4.zero; gripSum[i] = Vector3.zero; }
            restN = 0;
            sampling = true;
        }

        /// <summary>The rig stands in one idle frame straight from the clip: the arms go as Tick puts them, then each hand's turn in
        /// the hero's space and the way HoldAngle turns each weapon's flat side are noted. Writes bones (BodyView puts them back).</summary>
        public void SampleRest()
        {
            if (gdef.ArmSpread + gdef.HandsOff > 0f) ArmSpread(gdef.ArmSpread + gdef.HandsOff);
            ArmTune(1f);
            if (carryHand != null && carryLo != null && carryUp != null) CarryOnShoulder(1f);
            for (int i = 0; i < wT.Length; i++)
            {
                Transform t = wT[i];
                if (t == null) continue;
                WeaponDef W = defs[i];
                if (W.Len != placedLen[i] || W.Grip != placedGrip[i]) Place(i);
                Quaternion q = Quaternion.Inverse(root.rotation) * hand[i].rotation;
                var v = new Vector4(q.x, q.y, q.z, q.w);
                if (Vector4.Dot(v, refSum[i]) < 0f) v = -v; // an idle's frames are close together: summed on one side they average
                refSum[i] += v;
                if (W.HoldUp != 0f) votes[i] += HoldAngle(i, W, 1f, 0);
                if (W.HoldUp != 0f && W.Along) gripSum[i] += hand[i].InverseTransformPoint(t.TransformPoint(gripL[i]));
                if (W.Run.On) runVotes[i] += HoldAngle(i, W, 1f, 0, W.Run.Up, W.Run.Out);
                t.localPosition = bindP[i]; t.localRotation = bindR[i];
            }
            restN++;
        }

        public void EndRest()
        {
            for (int i = 0; i < wT.Length; i++)
            {
                Vector4 s = refSum[i];
                float m = s.magnitude;
                handRef[i] = m > 1e-6f ? new Quaternion(s.x / m, s.y / m, s.z / m, s.w / m) : Quaternion.identity;
                laxSign[i] = votes[i] > 0 ? 1 : votes[i] < 0 ? -1 : 0;
                runSign[i] = runVotes[i] > 0 ? 1 : runVotes[i] < 0 ? -1 : 0;
                restGrip[i] = restN > 0 ? gripSum[i] / restN : Vector3.zero;
            }
            sampling = false;
            rested = true;
            restKey = RestKey();
        }

        // the numbers the idle aim depends on (the tuner changes them in place)
        int RestKey()
        {
            unchecked
            {
                int k = (((gdef.ArmSpread + gdef.HandsOff).GetHashCode() * 31 + gdef.CarryAt.GetHashCode()) * 31 + gdef.Elbow.GetHashCode()) * 31 + gdef.CarryOut.GetHashCode();
                for (int i = 0; i < defs.Length; i++)
                {
                    WeaponDef W = defs[i];
                    if (W == null) continue;
                    k = k * 31 + W.HoldUp.GetHashCode();
                    k = k * 31 + W.HoldOut.GetHashCode();
                    k = k * 31 + W.Len.GetHashCode();
                    k = k * 31 + W.Grip.GetHashCode();
                    k = k * 31 + W.Run.Up.GetHashCode() + W.Run.Out.GetHashCode() * 7;
                }
                return k;
            }
        }

        // AnimFix.FixedGrip: the rest aim is HoldAngle's (what Rickard tuned on the idle), with its flat side always the way the idle
        // had it, so it never flips half a turn; it follows a share of the hand's own turn (W.Follow) instead of standing still while
        // the fist turns round it; and the swing's grip is reached in one turn about the palm while the swing eases in (rest from
        // BodyView), not let go of in a tenth of a second at every swing and taken back after it
        // (a SwingAim weapon keeps the rest aim through the swing, following SwingFollow of the hand)
        void RestHold(int i, WeaponDef W, float rest, Hold h, int sign)
        {
            Steady(i, W, rest, h, sign);
            if (h.Turn != Vector3.zero && rest > 0.001f) TurnAbout(i, Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(h.Turn), rest));
        }

        void Steady(int i, WeaponDef W, float rest, Hold h, int sign)
        {
            Transform t = wT[i];
            t.localPosition = bindP[i]; t.localRotation = bindR[i];
            bool keep = W.SwingAim && rested;
            if (rest <= 0.001f && !keep) return;
            Vector3 gl = gripL[i];
            Quaternion rB = t.rotation;
            Vector3 gB = t.TransformPoint(gl);
            HoldAngle(i, W, 1f, rested ? sign : 0, h.Up, h.Out);
            Quaternion rR = t.rotation;
            float follow = keep ? Mathf.Lerp(W.SwingFollow, W.Follow, rest) : W.Follow;
            if (rested) rR = Share(hand[i].rotation * Quaternion.Inverse(root.rotation * handRef[i]), follow) * rR;
            float w = keep ? 1f : rest;
            Vector3 g = Vector3.Lerp(gB, t.TransformPoint(gl), w);
            t.rotation = Quaternion.Slerp(rB, rR, w);
            t.position += g - t.TransformPoint(gl);
        }

        // a share of the hand's turn from its rest, the shortest way, all of it up to a quarter turn and fading out towards half a
        // turn, where the shortest way goes over to the other side. Followed along the way it went (until 2026-10-28) the turn could
        // wind past half a circle and stay there: Tikta's fists turn up to 175 degrees from her guard in her run and swing, and her
        // needles swung over and pointed at her face (Rickard's video 2026-10-28, "buggade vapen som snurrar")
        static Quaternion Share(Quaternion q, float f)
        {
            if (f <= 0f) return Quaternion.identity;
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            float w = Mathf.Min(q.w, 1f), s = Mathf.Sqrt(1f - w * w);
            if (s < 1e-5f) return Quaternion.identity;
            float deg = 2f * Mathf.Acos(w) * Mathf.Rad2Deg, k = deg <= 90f ? 1f : Mathf.SmoothStep(0f, 1f, (180f - deg) / 90f);
            return Quaternion.AngleAxis(deg * f * k, new Vector3(q.x / s, q.y / s, q.z / s));
        }

        // MxHoldAngle: the grip is set in the T-pose, so with the arm hanging a hammer would point straight ahead, flat; turned
        // about its grip to point forward and up, its head's long axis along the swing, and slid across the shaft into the palm.
        // sign: which way the flat side goes (0 = the one nearer the fist's, which flips as the fist turns); returns the one used
        int HoldAngle(int i, WeaponDef W, float w, int sign) => HoldAngle(i, W, w, sign, W.HoldUp, W.HoldOut);

        int HoldAngle(int i, WeaponDef W, float w, int sign, float up, float outDeg)
        {
            Transform t = wT[i];
            t.localPosition = bindP[i]; t.localRotation = bindR[i];
            if (w <= 0.001f) return sign;
            Vector3 gl = gripL[i];
            Vector3 fwd = root.forward, upW = root.up;
            Transform hd = hand[i], ix = index[i];
            Vector3 palm = hd.position + (ix != null ? (ix.position - hd.position) * 0.6f : Vector3.zero);
            Vector3 g0 = t.TransformPoint(gl);
            float a = up * Mathf.Deg2Rad, o = outDeg * Mathf.Deg2Rad;
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
                if (sign == 0) sign = Vector3.Dot(lax, t.right) < 0f ? -1 : 1;
                lax *= sign;
                float ang = Vector3.SignedAngle(t.right, lax, sh) * w;
                Vector3 gg = t.TransformPoint(gl);
                Quaternion tw = Quaternion.AngleAxis(ang, sh);
                t.SetPositionAndRotation(gg + tw * (t.position - gg), tw * t.rotation);
            }
            Vector3 d = palm - t.TransformPoint(gl);
            // an Along weapon (Moa's staff) stays where the idle holds it in the fist: slid across its shaft only, the staff stood
            // still while her fist ran up and down it with the arm's swing, up to 0.3 tiles (ANIM-03, Unity run 2026-10-29)
            if (W.Along && rested && !sampling) d = hd.TransformPoint(restGrip[i]) - t.TransformPoint(gl);
            else d -= sh * Vector3.Dot(d, sh); // slide across the shaft only, never along it
            t.position += d * w;
            return sign;
        }

        // MxShieldFace: standing, held up in front close to the arm; on the move, carried at the left side, face turned outwards
        // (an AlwaysSide shield sits on the hand instead)
        void ShieldFace(float moveW)
        {
            if (sDef.Dia != sPlacedDia && sDef.Dia > 0f)
            {
                // the tuner's Size, live
                float k = sModel.localScale.y * sDef.Dia / Mathf.Max(1e-4f, sPlacedDia);
                sModel.localScale = new Vector3(k, k, k);
                sModel.localPosition = -sCenter * k;
                sPlacedDia = sDef.Dia;
            }
            PlaceShield(moveW);
            if (sDef.Twist != Vector3.zero) sAnchor.rotation *= Quaternion.Euler(sDef.Twist);
            if (sDef.Shift != Vector3.zero) sAnchor.position += root.TransformDirection(sDef.Shift) * figH;
        }

        void PlaceShield(float moveW)
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
            Vector3 pWalk = sHand.position + side * (D * sDef.WalkOut);
            sAnchor.SetPositionAndRotation(Vector3.Lerp(pStand, pWalk, w), Quaternion.Slerp(stand, walk, w));
        }
    }
}
