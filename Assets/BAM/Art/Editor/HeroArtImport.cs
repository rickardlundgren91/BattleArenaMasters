using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Import settings for the hero models and Mixamo clips under Assets/BAM/Art/Models (humanoid rigs, clips in place,
    /// idle/run looping, no cameras or lights), and the HeroArt assets that tell the game which model and clips each hero uses.
    /// Runs by itself whenever those files are imported; BAM > Art > Rebuild Hero Models does it again by hand.
    /// </summary>
    sealed class HeroArtImport : AssetPostprocessor
    {
        const string ModelsDir = "Assets/BAM/Art/Models/";
        /// <summary>The remade heroes of 2026-11-13 (one shared art style): ModelsV2/&lt;Hero&gt;/&lt;Hero&gt;.fbx + &lt;Hero&gt;_Tex.jpg beside the
        /// old Models/&lt;Hero&gt;, which stay untouched. A hero uses its V2 body when that file is there and BAM &gt; Art &gt; Models V2 has not
        /// turned it off (Rickard 2026-11-13: "jag måste ju kunna spela dom så lägg in dom"); its clips stay the ones in specs below.</summary>
        const string ModelsV2Dir = "Assets/BAM/Art/ModelsV2/";
        const string OutDir = "Assets/BAM/Art/Resources/BAMArt/Heroes";
        static string OutOf(Spec s) => s.Out == null ? OutDir : "Assets/BAM/Art/Resources/BAMArt/" + s.Out;

        bool Ours => (assetPath.StartsWith(ModelsDir) || assetPath.StartsWith(ModelsV2Dir)) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        bool IsClip => assetPath.Contains("/Anim/");
        /// <summary>The bodies whose cloth or hair swings (ClothSway): they are imported with Read/Write on so the game can find the
        /// cloth's vertices and add its spring bones once. Fenn's Original body (cape, tabard), Tyra (ponytail), Bonk (loincloth,
        /// hair), Ragn (tail), Nässla (tunic), Tikta (front panel, scarf ends), Moa (front panel).</summary>
        static bool ClothModel(string path) => path == ModelsDir + "FennOriginal/Fenn.fbx" || path == ModelsDir + "Tyra/Tyra.fbx"
            || path == ModelsDir + "Bonk/Bonk.fbx" || path == ModelsDir + "Ragn/Ragn.fbx"
            || path == ModelsDir + "Nassla/Nassla.fbx"
            || path == ModelsDir + "Tikta/Tikta.fbx" || path == ModelsDir + "Moa/Moa.fbx"
            // the remakes whose swinging cloth or hair is part of the body shell (ClothSway's V2 specs): Tyra's ponytail, Ragn's tail,
            // Nässla's hair; capes, scarves and pelts are parts of their own (Weapons.cs, WornDef.Sway)
            || path == ModelsV2Dir + "Tyra/Tyra.fbx" || path == ModelsV2Dir + "Ragn/Ragn.fbx" || path == ModelsV2Dir + "Nassla/Nassla.fbx"
            // Moa's ponytail and apron (2026-11-18); her skins (Druid, Queen) are not measured, so they stay still
            || path == ModelsV2Dir + "Moa/Moa.fbx"
            // and their skins (ModelsV2/<Hero>/Skins/<Skin>/*.fbx), which sway the same way
            || path.StartsWith(ModelsV2Dir + "Tyra/Skins/") || path.StartsWith(ModelsV2Dir + "Ragn/Skins/") || path.StartsWith(ModelsV2Dir + "Nassla/Skins/")
            // Fenn's helmet plume (Rickard 2026-11-18 "animera tofsen"), on the knight and his skins
            || path == ModelsV2Dir + "Fenn/Fenn.fbx" || path.StartsWith(ModelsV2Dir + "Fenn/Skins/");

        // Rickard's arena pictures repeat across the floor and walls; 512 is plenty at the match camera's distance.
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(ModelsDir) || assetPath.StartsWith(ModelsV2Dir) || assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Props/")
                || assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Forms/"))
            {
                // the heroes', props' and forms' colour pictures: 1024 is plenty on a phone, compressed (Kugg's turret picture came
                // in at 2048, four times a hero body's cap; full review 2026-11-09 nr 84)
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
                return;
            }
            if (assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Fx/"))
            {
                // the effect sprites: white shapes in alpha, one 4x4 grid; clamp so cells never bleed across the edge
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = assetPath.EndsWith("T_BAM_HexShield.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; // the honeycomb tiles round the shield
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.Compressed;
                return;
            }
            if (!assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Arena/")) return;
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = assetPath.Contains("T_BAM_ArenaGround") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            // the boulder picture is cut out by its alpha: no dark fringe from the transparent pixels' colour
            if (assetPath.Contains("T_BAM_Pillar")) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; }
            if (assetPath.Contains("tx_arena_cliff")) ti.alphaIsTransparency = true; // the grass tips along its top are cut out (repeats left-right)
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.sRGBTexture = true;
            // the baked arena ground is one 480x1120 picture of the whole floor: keep its full size
            ti.maxTextureSize = assetPath.Contains("T_BAM_ArenaGround") ? 2048 : assetPath.Contains("tx_arena_cliff") ? 1024 : 512; // the cliff spans a third of the screen
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // a clip whose first frame is far from a standing pose (Ragn's Running Crawl, on all fours) gives Unity no usable avatar of
            // its own, and then no clip at all (Rickard 2026-11-10: "no clip in Ragn/Anim/RunFour.fbx"): it borrows the avatar of its
            // hero's body (Models/<Hero>/<Hero>.fbx), built from the standing bind pose. Since 2026-11-15 the avatar of the hero's own
            // Run.fbx beside it comes first (same Mixamo rig, standing, and its clip is known to import): Ragn's body was replaced on
            // 2026-11-12 and the clip came out empty again with that body's avatar (HeroArt RunFour none, Rickard's console 2026-11-15)
            if (IsClip && Path.GetFileNameWithoutExtension(assetPath).StartsWith("RunFour"))
            {
                string animDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                string heroDir = Path.GetDirectoryName(animDir).Replace('\\', '/');
                // 2026-11-19: borrowing Run.fbx's avatar (CopyFromOther) still gave no clip on Rickard's PC ("no clip in
                // Ragn/Anim/RunFour.fbx" at every reload). RunFour's rest skeleton is the same as Run.fbx's (checked bone by bone),
                // so build RunFour's OWN avatar from Run.fbx's human description: its skeleton is the standing rest pose, so the
                // crawl's first frame no longer matters, and the clip is no longer tied to another file's avatar
                var run = AssetImporter.GetAtPath(animDir + "/Run.fbx") as ModelImporter;
                if (run != null && run.humanDescription.human != null && run.humanDescription.human.Length > 0)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    mi.humanDescription = run.humanDescription;
                }
                else
                {
                    Avatar body = AssetDatabase.LoadAssetAtPath<Avatar>(heroDir + "/" + Path.GetFileName(heroDir) + ".fbx");
                    if (body != null) { mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = body; }
                    else Debug.LogWarning("[BAM.Art] " + assetPath + ": no avatar in " + heroDir + " to borrow, its own is used");
                }
            }
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.isReadable = ClothModel(assetPath);
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importAnimation = IsClip;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            if (IsClip) mi.materialImportMode = ModelImporterMaterialImportMode.None;
            else
            {
                // the colour texture is embedded in the FBX; keep it inside the model so it comes along by itself
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!Ours || !IsClip) return;
            var mi = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = name.StartsWith("Idle") || name.StartsWith("Run") || name.StartsWith("Walk");
            // a clip whose file name ends in Mirror plays left for right (Kugg's IdleMirror: Oaken's idle with the raised fist on the
            // right, the cannon arm; Rickard 2026-11-11)
            bool mirror = Mirrored(name);
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation c = clips[i];
                c.name = i == 0 ? name : name + i;
                c.loopTime = loop;
                c.loopPose = loop;
                // in place: the game layer moves the unit, the clip must not walk it away
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                c.mirror = mirror;
            }
            mi.clipAnimations = clips;
        }

        static bool Mirrored(string clipName) => clipName.EndsWith("Mirror");

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string p in imported)
                if (p.StartsWith(ModelsDir) || p.StartsWith(ModelsV2Dir) || p.StartsWith("Assets/BAM/Art/Editor/"))
                {
                    EditorApplication.delayCall -= BuildAll;
                    EditorApplication.delayCall += BuildAll;
                    return;
                }
        }

        struct Spec
        {
            public string Out; // null = Heroes
            public string Hero, Model, Idle, Run, Attack, Cast, Hit, Death, Walk, RunFour;
            public float Height, Release, Start;
        }

        // The shared animation pool (Rickard 2026-10-25): every Mixamo clip retargets onto every humanoid model, so a hero or
        // minion without a clip of its own for a slot borrows the pool's. A slot left null below, or whose file is missing,
        // uses the pool. Order: Idle, Run, Attack, Cast, Hit, Death, Walk.
        // Walk and Run are each hero's clips in current Speltest (Resources/Mixamo/Anim/<hero>/Walk|Run, Rickard 2026-10-26: Tikta and
        // Tyra "hukar och går snabbt": BAM played only the run, Tyra's crouched longbow RunBow, at a fixed rate). Bonk walks with Knut's walk.
        // Bram's Idle, Hit and Dying are Speltest's current ones too (2026-10-26); his Attack1 and Attack2 stay the older clips their timing
        // (Release) was set for, and Bonk's slam borrows that Attack2.
        static readonly string[] pool = { "Tikta/Anim/Idle", "Tikta/Anim/Run", "Fenn/Anim/Attack1", "Fenn/Anim/Cast", "Tikta/Anim/Hit", "Tikta/Anim/Dying", "Tikta/Anim/Walk" };
        // Height: a hero's sole-to-crown height in tiles as old Speltest draws it in a match (HeroScale.cs: its measured visible
        // height x 1.8 tiles x GlobalMul 0.9; Fenn 1.35, Tikta 1.20, Blast 1.16, Knut 1.14, StdVis 1.10 for Tyra, Moa 1.07, Bram 0.98).
        // Rickard 2026-10-26: the heroes looked smaller than in Speltest (they were 1.25-1.55)
        // Moa and Blast have their own Mixamo clips from Speltest (Resources/Mixamo/Anim/<hero>, HeroMixamoOwn.cs), since 2026-10-26
        static readonly Spec[] specs =
        {
            // Rickard 2026-10-27: his own Attack1 is a spin (his back to the target at 20-30 %) and the hammer hung from a straight arm
            // at rest, so the swing went "upp över huvudet, runt till andra sidan och sen svinga". Now the hammer rests on his shoulder
            // (Weapons.cs) and he strikes straight down from overhead with Bonk's Standing Melee Attack Downward, from 12 % (before
            // that the hammer swings out to the side first)
            // Bram stands in Bonk's Orc Idle 1 like Knut (Rickard 2026-11-12, lobby picture: "du ser hur bram står helt konstigt"): his
            // Speltest idle leant him back with the hips pushed forward; the orc stance is upright, feet apart, the hammer arm free
            // His Hammer Line throws the hammer straight ahead after a charge, so he casts with Knut's Throw (a right-hand overhand
            // throw) instead of his Anvil Strike, a blow straight down that peaked a third of a second before the hammer left (hero
            // review 2026-11-13, BodyView.CastLead)
            // His auto attack throws the hammer too since 2026-11-16 (hero review, Rickard 2026-11-18 "låter bra"): Bonk's overhead
            // chop read as a melee blow while a hammer flew 3.55 tiles, so he attacks with the same overhand Throw (the ranged minions'
            // attack, Release 0.4 as theirs) until he has a throw clip of his own
            new Spec { Hero = "Bram", Model = "Bram/Bram", Idle = "Bonk/Anim/IdleOrc1", Run = "Bram/Anim/Run", Attack = "Knut/Anim/Throw", Cast = "Knut/Anim/Throw", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Walk = "Bram/Anim/Walk", Height = 1.59f, Release = 0.4f },
            // Rickard 2026-10-28: Fenn "vänder sig bakåt och sparkar ut i luften för varje attack": his own Attack1 (Speltest's Power
            // Slash) is a jump with a whole turn (the hips turn 386 degrees and rise half a hip height). Knut's level swing after it was
            // made for an anchor hanging from the fist: Fenn's sword stood 35-60 degrees up at every blow (ANIM-21, Unity run 2), so he
            // chops down with Bonk's one-handed overhead strike from 12 % as Bram does (the hammer meets the target 15 degrees down),
            // his shield staying in the left hand (Rickard's pick 2026-10-29)
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): his own Mixamo Sword And Shield Slash (Downward Slash, blow at 39 %), not Bonk's club chop; it starts in his idle's stance (hips -54 degrees, as his idle)
            new Spec { Hero = "Fenn", Model = "FennOriginal/Fenn", Idle = "Fenn/Anim/Idle", Run = "Fenn/Anim/Run", Attack = "Fenn/Anim/Slash", Cast = "Fenn/Anim/Cast", Hit = "Fenn/Anim/Hit", Death = "Fenn/Anim/Dying", Walk = "Fenn/Anim/Walk", Height = 2.19f, Release = 0.39f },
            // Tyra stands relaxed with the bow down (Moa's idle) in the menu and the match and raises it only to shoot (Rickard
            // 2026-10-27: her drawn-bow IdleBow in the menu "precis samma som när hon är i spelet"; old Speltest's Home had her in a
            // relaxed idle too). Since 2026-10-30 her own relaxed idle: Rickard's Mixamo Breathing Idle (9.9 s), no longer Moa's;
            // back to Moa's the same day: Breathing Idle's open hand at the hip left the bow hanging loose beside it (Rickard: "håller
            // inte bågen bra"), Moa's closed fist holds it as he approved
            new Spec { Hero = "Tyra", Model = "Tyra/Tyra", Idle = "Moa/Anim/Idle", Run = "Tyra/Anim/Run", Attack = "Tyra/Anim/Shoot", Cast = "Tyra/Anim/Cast", Hit = "Tyra/Anim/HitBow", Death = "Tyra/Anim/Dying", Walk = "Tyra/Anim/Walk", Height = 1.78f, Release = 0.55f },
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Mixamo Magic Heal (one hand forward, blow at 29 %) instead of the Throw she shared with Tikta, Bram and the minions
            new Spec { Hero = "Moa", Model = "Moa/Moa", Idle = "Moa/Anim/Idle", Run = "Moa/Anim/Run", Attack = "Moa/Anim/Heal", Cast = "Moa/Anim/Cast", Hit = "Moa/Anim/Hit", Death = "Moa/Anim/Dying", Walk = "Moa/Anim/Walk", Height = 1.73f, Release = 0.29f },
            // Knut stands in Bonk's Orc Idle 1 too (Rickard 2026-10-30: "knut behöver också orc pose", "som dom andra två")
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Sword And Shield Slash (Cross Slash, blow at 48 %) instead of Standing Melee Attack Horizontal, whose chest swung 226 degrees round in the played part (now 142)
            new Spec { Hero = "Knut", Model = "Knut/Knut", Idle = "Bonk/Anim/IdleOrc1", Run = "Knut/Anim/Run", Attack = "Knut/Anim/Slash", Cast = "Knut/Anim/Throw", Hit = "Knut/Anim/Hit", Death = "Knut/Anim/Dying", Walk = "Knut/Anim/Walk", Height = 1.85f, Release = 0.48f },
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): a quick left jab with the needle (Mixamo Punching, Jab Punch, blow at 45 %) instead of the Throw that Moa, Bram and the minions shared; the melee minions keep it
            new Spec { Hero = "Tikta", Model = "Tikta/Tikta", Idle = "Tikta/Anim/Idle", Run = "Tikta/Anim/Run", Attack = "Tikta/Anim/Jab", Cast = "Tikta/Anim/Cast", Hit = "Tikta/Anim/Hit", Death = "Tikta/Anim/Dying", Walk = "Tikta/Anim/Walk", Height = 1.94f, Release = 0.45f },
            // the lane minions: the old game's Mixamo goblin creeps (one figure, three paint jobs), moving with the heroes' clips
            new Spec { Out = "Minions", Hero = "Melee", Model = "Creeps/Melee", Attack = "Tikta/Anim/Attack1", Height = 0.8f, Release = 0.4f },
            new Spec { Out = "Minions", Hero = "Ranged", Model = "Creeps/Ranged", Attack = "Knut/Anim/Throw", Height = 0.78f, Release = 0.4f },
            new Spec { Out = "Minions", Hero = "Siege", Model = "Creeps/Siege", Idle = "Bram/Anim/Idle", Run = "Bram/Anim/Run", Attack = "Bram/Anim/Attack1", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Walk = "Bram/Anim/Walk", Height = 0.9f, Release = 0.45f },
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Mixamo Throwing (blow at 29 % of 5.6 s): the chest turns 30 degrees in the played part, against 174 in Casting Spell, his old clip
            new Spec { Hero = "Blast", Model = "Blast/Blast", Idle = "Blast/Anim/Idle", Run = "Blast/Anim/Run", Attack = "Blast/Anim/Throw", Cast = "Blast/Anim/Cast", Hit = "Blast/Anim/Hit", Death = "Blast/Anim/Dying", Walk = "Blast/Anim/Walk", Height = 1.88f, Release = 0.29f },
            // Bonk (hero 8, Rickard's Tripo body rigged in Mixamo 2026-10-26; his wolf pelt reweighted to the chest so it no longer
            // stretches with his left arm and head). His own Mixamo clips (Orc Idle, Mutant Run, Standing Melee Attack Downward with
            // the club landing at 46 %, Hit Reaction, Dying); the slam borrows Bram's Anvil Strike, since Mixamo's jump attack is
            // airborne for longer than a cast lasts. Idle is Knut's: on Bonk the Orc Idle bent his feet and lifted him off the ground
            // (Rickard 2026-10-26: "fötterna böjer sig"). Since 2026-10-30 his own again: Rickard's Orc Idle 1 (7.4 s, downloaded on
            // Bonk's rig), whose knees bend 13-22 degrees against the old Orc Idle's 23-34 (Rickard's pick: "bonk behöver ny idle")
            // Rickard 2026-11-19 ("bonk snurrar fortfarande vid autoattack", "stora rätt långsamma slag över huvudet med bägge händer"): his
            // auto attack is Oaken's two-armed overhead smash (both fists over his head at 0.9-1.1 s, on the ground at 1.33 s of 3.0; blow 42 %),
            // its windup's turn mostly held off (BodyView.TwistHold); his one-armed axe chop (Bonk/Anim/Attack1) swung round him
            new Spec { Hero = "Bonk", Model = "Bonk/Bonk", Idle = "Bonk/Anim/IdleOrc1", Run = "Bonk/Anim/Run", Attack = "Oaken/Anim/Cast", Cast = "Bram/Anim/Attack2", Hit = "Bonk/Anim/Hit", Death = "Bonk/Anim/Dying", Walk = "Knut/Anim/Walk", Height = 2.0f, Release = 0.42f },
            // Hilde (hero 9) and Oaken (hero 10), 2026-10-28: Speltest's current bodies (Mixamo/Looks/Hilde with Hilde2.jpg, Looks/Oaken
            // with OaknN.jpg) and clips (Resources/Mixamo/Anim/Hilde|Oaken). Hilde's shot is a 0.27 s rifle clip for the shoulder cannon,
            // her cast the keg throw. Oaken walks and runs with the heavy Mutant clips; his attack is a hook punch (the fist is furthest
            // out at 0.49 of the clip) and his cast a two-arm overhead smash (fists on the ground at 0.44). Heights: Speltest HeroScale
            // visible ratios (Hilde 1.01, Oaken 1.10) x 1.62 as the others.
            // Hilde's spec is out while she waits in HeroCatalog.Coming (full review 2026-11-09 nr 85): everything under Resources ships,
            // and her HeroArt pulled her body, clips and picture (3 MB) into every build. Put the line back when she returns:
            // new Spec { Hero = "Hilde", Model = "Hilde/Hilde", Idle = "Hilde/Anim/Idle", Run = "Hilde/Anim/Run", Attack = "Hilde/Anim/Attack1", Cast = "Hilde/Anim/Cast", Hit = "Hilde/Anim/Hit", Death = "Hilde/Anim/Dying", Walk = "Hilde/Anim/Walk", Height = 1.64f, Release = 0.3f },
            // Oaken stays: Kugg borrows his Hit, Dying and Walk clips below.
            new Spec { Hero = "Oaken", Model = "Oaken/Oaken", Idle = "Oaken/Anim/Idle", Run = "Oaken/Anim/Run", Attack = "Oaken/Anim/Attack1", Cast = "Oaken/Anim/Cast", Hit = "Oaken/Anim/Hit", Death = "Oaken/Anim/Dying", Walk = "Oaken/Anim/Walk", Height = 1.78f, Release = 0.49f },
            // Kugg (hero 10, 2026-11-09): Rickard's Tripo rune golem rigged in Mixamo (the cannon is his right arm), his own clips:
            // Male Weighted Run, Firing A Gun (the kick at 0.30-0.33 s of 1.17, so the shot leaves at 27 %) and Standing To Kneeling
            // Down as he anchors (the turret takes over 0.3 s later, BodyForm.cs). Idle, hit, death and walk are
            // Oaken's heavy golem clips: Mutant Breathing Idle held the cannon arm up at his face, and Bonk's Orc Idle 1 (tried 2026-11-10)
            // pressed his arms in against his wide body and tilted his face (Rickard: "hans armar är lite nära och han är lite skev"). Height as Oaken's, whose body he wore until now (the turret form is sized from it).
            // Since 2026-11-11 Oaken's idle plays mirrored (Kugg/Anim/IdleMirror, a copy of Oaken/Anim/Idle): its raised fist was his
            // left, and Rickard wanted the cannon in that raised hand ("bättre med kugg kanonen i andra handen", his menu pictures)
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Firing A Gun fired with his LEFT hand and stood 78 degrees off his mirrored idle, so he turned round at every shot while the right (cannon) arm was pulled to the target (KuggAim). Attack1Mirror is a byte copy played mirrored: the right hand fires, the stance matches the idle
            new Spec { Hero = "Kugg", Model = "Kugg/Kugg", Idle = "Kugg/Anim/IdleMirror", Run = "Kugg/Anim/Run", Attack = "Kugg/Anim/Attack1Mirror", Cast = "Kugg/Anim/Cast", Hit = "Oaken/Anim/Hit", Death = "Oaken/Anim/Dying", Walk = "Oaken/Anim/Walk", Height = 1.78f, Release = 0.27f },
            // Ragn (hero 11, preview, 2026-11-10): Rickard's Tripo werewolf blacksmith rigged in Mixamo, his own clips: Mutant Breathing
            // Idle, Mutant Run, Mutant Swiping (both arms fastest at 0.44-0.45 of the 2.7 s clip) and the long Mutant Jumping (crouch, leap
            // with arms up, land) for Månsprång. Idle is Bonk's Orc Idle 1 since 2026-11-10: Mutant Breathing Idle held his right hand up
            // at his face (Rickard: "samma problem som med kugg"). Hit and death are Bonk's, walk Knut's, as Bonk's. A little shorter than Bonk, whose body he wore.
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Mixamo Right Hook (blow at 24 %) instead of Mutant Swiping, whose chest swung 185 degrees in the played part (now 121)
            new Spec { Hero = "Ragn", Model = "Ragn/Ragn", Idle = "Bonk/Anim/IdleOrc1", Run = "Ragn/Anim/Run", Attack = "Ragn/Anim/Hook", Cast = "Ragn/Anim/Cast", Hit = "Bonk/Anim/Hit", Death = "Bonk/Anim/Dying", Walk = "Knut/Anim/Walk", RunFour = "Ragn/Anim/RunFour", Height = 1.95f, Release = 0.24f },
            // Nässla (hero 12, preview, 2026-11-10): Rickard's second Tripo swamp witch (short tunic, no staff: the first one with a long
            // ragged cloak and a root staff failed Mixamo's auto-rigger) rigged in Mixamo, his own clips: Old Man Idle, Running, Standing
            // 1H Magic Attack 01 (the throwing forearm fastest at 0.32-0.35) and Standing 2H Magic Area Attack 02 for Taggsnår (arms wide,
            // facing forward). Idle is Moa's since 2026-11-10 (Old Man Idle bowed her head so the hat hid her face); her hat and basket
            // are skinned wholly to Head and Spine2 in Nassla.fbx (Mixamo had tied the basket to the shoulders and arms, so the hat
            // and basket broke apart when the arms came down: Rickard "hatten och korgen buggat ur helt"). Hit, death and walk are Moa's, whose body she wore. A small hunched goblin, shorter than Moa.
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): Standing 2H Magic Attack 01 (two hands forward, blow at 41 %) instead of 1H Magic Attack 01, whose chest swung 180 degrees in the played part (now 47)
            new Spec { Hero = "Nassla", Model = "Nassla/Nassla", Idle = "Moa/Anim/Idle", Run = "Nassla/Anim/Run", Attack = "Nassla/Anim/Attack2H", Cast = "Nassla/Anim/Cast", Hit = "Moa/Anim/Hit", Death = "Moa/Anim/Dying", Walk = "Moa/Anim/Walk", Height = 1.62f, Release = 0.41f },
            // Ravna (hero 14, in the game for everyone since 2026-11-12, Tikta's body until now): Rickard's Tripo crow druid rigged in
            // Mixamo (33 bones, index finger only, like Tyra), his clips downloaded 2026-11-12: Standing Idle, Running With Intention
            // (in place), Stabbing (the right fist furthest forward at 0.38 of the 2.7 s clip) and Standing 2H Cast Spell 01 for Night
            // Flight. Hit, death and walk are Tikta's, whose body she wore. The feather cloak and crow come later as their own parts.
            // Her idle is Moa's since 2026-11-12 (Rickard: "annan idle på ravna", "benet går igenom manteln"): her own Standing Idle stood
            // wide-legged, so a knee came through the long mantle.
            // Rickard 2026-11-18 ("dom snurrar runt och slår", "dumt ut om många har det"): a lead-hand hook with the dagger (Mixamo Punching, Hook With The Lead Hand, blow at 43 %) instead of Stabbing (chest 109 degrees, now 84)
            new Spec { Hero = "Ravna", Model = "Ravna/Ravna", Idle = "Moa/Anim/Idle", Run = "Ravna/Anim/Run", Attack = "Ravna/Anim/Hook", Cast = "Ravna/Anim/Cast", Hit = "Tikta/Anim/Hit", Death = "Tikta/Anim/Dying", Walk = "Tikta/Anim/Walk", Height = 1.8f, Release = 0.43f },
        };

        // Fenn has one body, the Original (Models/FennOriginal, his Meshy body rigged in Mixamo, the right ankle and foot weights
        // mirrored from the left since Mixamo had put that ankle on the toes). The Clay body and the BAM > Art > Fenn menu were removed
        // 2026-11-13 (Rickard: "ta bort clay"; his Unity had Original picked). His clips stay in Models/Fenn/Anim.
        static string ModelOf(Spec s) => s.Model;

        // ---- the V2 switch (BAM > Art > Models V2) ----
        // Kept per computer in EditorPrefs, so a sync from the repository never flips it; on by default. The HeroArt asset is rebuilt
        // with the chosen body, so a phone build gets whatever is ticked here.
        static string V2Key(string hero) => "BAM.Art.ModelsV2.Off." + hero;
        static bool V2On(string hero) => !EditorPrefs.GetBool(V2Key(hero), false);
        static string V2Body(Spec s) => ModelsV2Dir + s.Hero + "/" + s.Hero + ".fbx";
        static bool UsesV2(Spec s) => s.Out == null && V2On(s.Hero) && File.Exists(V2Body(s));
        /// <summary>The body file the hero uses now: its V2 remake, or the old model.</summary>
        static string ModelPath(Spec s) => UsesV2(s) ? V2Body(s) : ModelsDir + ModelOf(s) + ".fbx";
        static string TexPath(Spec s) => UsesV2(s) ? ModelsV2Dir + s.Hero + "/" + s.Hero + "_Tex.jpg" : ModelsDir + ModelOf(s) + "_Tex.jpg";

        // ---- the skins (ModelsV2/<Hero>/Skins/<Skin>/: a body FBX and its <Body>_Tex.jpg; Parts/ under it are not used yet) ----
        // Every skin of a V2 hero goes into its HeroArt (and so into the build); the player picks one on Home (HeroSkins).

        struct SkinFiles { public string Name, Body, Tex; }

        static SkinFiles[] SkinsOf(Spec s)
        {
            var list = new System.Collections.Generic.List<SkinFiles>();
            string root = ModelsV2Dir + s.Hero + "/Skins";
            if (s.Out != null || !Directory.Exists(root)) return list.ToArray();
            string[] dirs = Directory.GetDirectories(root);
            System.Array.Sort(dirs, System.StringComparer.Ordinal);
            foreach (string d in dirs)
            {
                string dir = d.Replace('\\', '/');
                string[] bodies = Directory.GetFiles(dir, "*.fbx");
                if (bodies.Length == 0) continue; // a skin of parts only (Moa's Druid and Queen so far): waits for the weapon step
                System.Array.Sort(bodies, System.StringComparer.Ordinal);
                string body = bodies[0].Replace('\\', '/');
                string tex = dir + "/" + Path.GetFileNameWithoutExtension(body) + "_Tex.jpg";
                if (!File.Exists(tex))
                {
                    tex = null;
                    foreach (string t in Directory.GetFiles(dir, "*_Tex.jpg")) { tex = t.Replace('\\', '/'); break; }
                }
                list.Add(new SkinFiles { Name = Path.GetFileName(dir), Body = body, Tex = tex });
            }
            return list.ToArray();
        }

        /// <summary>The picture's main colour, bright enough to read as text on the dark menu (its dark and grey pixels left out).</summary>
        static Color MainColour(Texture2D tex)
        {
            if (tex == null) return Color.white;
            const int N = 32;
            var rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var was = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var small = new Texture2D(N, N, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, N, N), 0, 0);
            small.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(rt);
            Color sum = Color.black; float w = 0;
            foreach (Color c in small.GetPixels())
            {
                Color.RGBToHSV(c, out float h, out float sat, out float v);
                if (v < 0.15f) continue;
                float k = sat * v; // colourful, lit pixels count most
                sum += c * k; w += k;
            }
            Object.DestroyImmediate(small);
            if (w <= 1e-4f) return Color.white;
            Color.RGBToHSV(sum / w, out float hh, out float ss, out float vv);
            return Color.HSVToRGB(hh, Mathf.Clamp(ss * 1.3f, 0.45f, 0.85f), Mathf.Max(vv, 0.95f));
        }

        static void FillSkins(HeroArt art, Spec s)
        {
            SkinFiles[] files = art.V2 ? SkinsOf(s) : new SkinFiles[0];
            // the skins' weapons and worn parts into Props (SkinGear reads them)
            foreach (SkinFiles f in files) SkinParts.Convert(s.Hero, Path.GetDirectoryName(f.Body).Replace('\\', '/'));
            var names = new System.Collections.Generic.List<string>();
            var models = new System.Collections.Generic.List<GameObject>();
            var texs = new System.Collections.Generic.List<Texture2D>();
            var cols = new System.Collections.Generic.List<Color>();
            foreach (SkinFiles f in files)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(f.Body);
                if (m == null) { Debug.LogWarning("[BAM.Art] " + s.Hero + " skin " + f.Name + ": " + f.Body + " not imported yet"); continue; }
                var t = f.Tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(f.Tex) : null;
                names.Add(f.Name); models.Add(m); texs.Add(t); cols.Add(MainColour(t));
            }
            art.SkinNames = names.ToArray();
            art.SkinModels = models.ToArray();
            art.SkinTextures = texs.ToArray();
            art.SkinColors = cols.ToArray();
        }

        static bool SkinsStale(HeroArt art, Spec s)
        {
            if (!art.V2) return art.SkinCount != 0;
            SkinFiles[] files = SkinsOf(s);
            foreach (SkinFiles f in files) if (SkinParts.Pending(s.Hero, Path.GetDirectoryName(f.Body).Replace('\\', '/'))) return true; // a part to convert
            if (art.SkinModels == null || art.SkinNames == null || art.SkinModels.Length != files.Length) return true;
            for (int i = 0; i < files.Length; i++)
                if (art.SkinModels[i] == null || art.SkinNames[i] != files[i].Name) return true;
            return false;
        }

        static void SetV2(string hero, bool on)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[BAM.Art] Leave Play first, then switch Models V2 (the heroes are rebuilt outside Play)"); return; }
            if (on) EditorPrefs.DeleteKey(V2Key(hero)); else EditorPrefs.SetBool(V2Key(hero), true);
            BuildAll();
            Debug.Log("[BAM.Art] " + hero + ": " + (on ? "new model (V2)" : "old model"));
        }

        static void SetAllV2(bool on)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[BAM.Art] Leave Play first, then switch Models V2 (the heroes are rebuilt outside Play)"); return; }
            foreach (Spec s in specs) if (s.Out == null) { if (on) EditorPrefs.DeleteKey(V2Key(s.Hero)); else EditorPrefs.SetBool(V2Key(s.Hero), true); }
            BuildAll();
            Debug.Log("[BAM.Art] Every hero: " + (on ? "new models (V2)" : "old models"));
        }

        const string V2Menu = "BAM/Art/Models V2/";
        [MenuItem(V2Menu + "All heroes: new models (V2)", false, 0)] static void AllV2() => SetAllV2(true);
        [MenuItem(V2Menu + "All heroes: old models", false, 1)] static void AllOld() => SetAllV2(false);
        static bool Tick(string hero) { Menu.SetChecked(V2Menu + hero, V2On(hero)); return !EditorApplication.isPlaying; }
        [MenuItem(V2Menu + "Blast", false, 20)] static void TBlast() => SetV2("Blast", !V2On("Blast"));
        [MenuItem(V2Menu + "Blast", true)] static bool VBlast() => Tick("Blast");
        [MenuItem(V2Menu + "Bonk", false, 21)] static void TBonk() => SetV2("Bonk", !V2On("Bonk"));
        [MenuItem(V2Menu + "Bonk", true)] static bool VBonk() => Tick("Bonk");
        [MenuItem(V2Menu + "Bram", false, 22)] static void TBram() => SetV2("Bram", !V2On("Bram"));
        [MenuItem(V2Menu + "Bram", true)] static bool VBram() => Tick("Bram");
        [MenuItem(V2Menu + "Fenn", false, 23)] static void TFenn() => SetV2("Fenn", !V2On("Fenn"));
        [MenuItem(V2Menu + "Fenn", true)] static bool VFenn() => Tick("Fenn");
        [MenuItem(V2Menu + "Knut", false, 24)] static void TKnut() => SetV2("Knut", !V2On("Knut"));
        [MenuItem(V2Menu + "Knut", true)] static bool VKnut() => Tick("Knut");
        [MenuItem(V2Menu + "Kugg", false, 25)] static void TKugg() => SetV2("Kugg", !V2On("Kugg"));
        [MenuItem(V2Menu + "Kugg", true)] static bool VKugg() => Tick("Kugg");
        [MenuItem(V2Menu + "Moa", false, 26)] static void TMoa() => SetV2("Moa", !V2On("Moa"));
        [MenuItem(V2Menu + "Moa", true)] static bool VMoa() => Tick("Moa");
        [MenuItem(V2Menu + "Nassla", false, 27)] static void TNassla() => SetV2("Nassla", !V2On("Nassla"));
        [MenuItem(V2Menu + "Nassla", true)] static bool VNassla() => Tick("Nassla");
        [MenuItem(V2Menu + "Ragn", false, 28)] static void TRagn() => SetV2("Ragn", !V2On("Ragn"));
        [MenuItem(V2Menu + "Ragn", true)] static bool VRagn() => Tick("Ragn");
        [MenuItem(V2Menu + "Ravna", false, 29)] static void TRavna() => SetV2("Ravna", !V2On("Ravna"));
        [MenuItem(V2Menu + "Ravna", true)] static bool VRavna() => Tick("Ravna");
        [MenuItem(V2Menu + "Tikta", false, 30)] static void TTikta() => SetV2("Tikta", !V2On("Tikta"));
        [MenuItem(V2Menu + "Tikta", true)] static bool VTikta() => Tick("Tikta");
        [MenuItem(V2Menu + "Tyra", false, 31)] static void TTyra() => SetV2("Tyra", !V2On("Tyra"));
        [MenuItem(V2Menu + "Tyra", true)] static bool VTyra() => Tick("Tyra");

        // Also when Unity opens or the scripts reload: the game must never fall back to the code figures just because
        // the hero list was not built yet on this computer.
        [InitializeOnLoadMethod]
        static void EnsureBuilt()
        {
            EditorApplication.delayCall += () =>
            {
                // the scripts reload on the way into Play too: nothing is rebuilt in Play, so do not import or warn then (Rickard
                // 2026-11-18 saw "not rebuilt in Play mode" at every Play start); the next reload outside Play checks again
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                // a model or clip imported before these import rules compiled (first open of a fresh copy) keeps Unity's defaults:
                // not humanoid, clips not looping, so the heroes stand frozen. Import those files again with the rules.
                if (ReimportStale()) { BuildAll(); return; }
                foreach (Spec s in specs)
                {
                    var art = AssetDatabase.LoadAssetAtPath<HeroArt>(OutOf(s) + "/" + s.Hero + ".asset");
                    if (art == null || art.Model == null || art.Model != AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(s)) || art.V2 != UsesV2(s) || art.Texture == null || !Mathf.Approximately(art.Height, s.Height)
                        || art.Idle == null || art.Walk == null || art.Run == null || art.Attack == null || art.Cast == null || art.Hit == null || art.Death == null
                        || (s.RunFour != null && art.RunFour == null)) { BuildAll(); return; } // Ragn's all-fours run came 2026-11-10: a HeroArt built before it lacks it
                    if (art.Cast != Clip(s.Cast, 3) || art.Attack != Clip(s.Attack, 2)) { BuildAll(); return; } // an attack clip changed too (Bram's throw 2026-11-18)
                    if (SkinsStale(art, s)) { BuildAll(); return; } // a skin folder came or went // a cast clip changed in specs (Bram's throw 2026-11-13): no Rebuild by hand needed
                }
            };
        }

        internal static void ReimportStaleForBuild() => ReimportStale();

        static bool ReimportStale()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (Spec s in specs)
            {
                paths.Add(ModelPath(s));
                if (UsesV2(s)) foreach (SkinFiles f in SkinsOf(s)) paths.Add(f.Body);
                foreach (string p in new[] { s.Idle, s.Run, s.Attack, s.Cast, s.Hit, s.Death, s.Walk, s.RunFour })
                    if (p != null) paths.Add(ModelsDir + p + ".fbx");
            }
            foreach (string p in pool) paths.Add(ModelsDir + p + ".fbx");
            bool any = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    bool stale = mi.animationType != ModelImporterAnimationType.Human || mi.isReadable != ClothModel(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    // a clip is judged by what was imported, not by the importer's saved clipAnimations: OnPreprocessAnimation sets
                    // those for the import only, so a clip whose .meta never held them (Bonk's, Kugg's, Ragn's, Nässla's, Ravna's)
                    // looked stale at every script reload and was imported again, with a BuildAll that warned in Play (2026-11-18)
                    if (!stale && path.StartsWith(ModelsDir) && path.Contains("/Anim/"))
                    {
                        AnimationClip clip = Load(path.Substring(ModelsDir.Length, path.Length - ModelsDir.Length - 4));
                        if (clip == null) stale = true;
                        else
                        {
                            AnimationClipSettings cs = AnimationUtility.GetAnimationClipSettings(clip);
                            bool loop = name.StartsWith("Idle") || name.StartsWith("Run") || name.StartsWith("Walk");
                            stale = (loop && !cs.loopTime) || cs.mirror != Mirrored(name);
                        }
                    }
                    if (!stale) continue;
                    Debug.Log("[BAM.Art] Importing " + path + " again with the hero rules (humanoid, looping idle and run)");
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    any = true;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return any;
        }

        [MenuItem("BAM/Art/Rebuild Hero Models")]
        static void Rebuild()
        {
            foreach (Spec s in specs)
            {
                AssetDatabase.ImportAsset(ModelPath(s), ImportAssetOptions.ForceUpdate);
                foreach (string p in new[] { s.Idle, s.Run, s.Attack, s.Cast, s.Hit, s.Death, s.Walk, s.RunFour })
                    if (p != null) AssetDatabase.ImportAsset(ModelsDir + p + ".fbx", ImportAssetOptions.ForceUpdate);
            }
            BuildAll();
        }

        internal static void BuildAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[BAM.Art] Hero models not rebuilt in Play mode: leave Play and run BAM > Art > Rebuild Hero Models"); return; }
            int ok = 0;
            foreach (Spec s in specs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(s));
                if (model == null) { Debug.LogWarning("[BAM.Art] " + s.Hero + ": model " + ModelPath(s) + " not found, the code model is used"); continue; }
                if (!AssetDatabase.IsValidFolder(OutOf(s))) { Directory.CreateDirectory(OutOf(s)); AssetDatabase.Refresh(); }
                string path = OutOf(s) + "/" + s.Hero + ".asset";
                var art = AssetDatabase.LoadAssetAtPath<HeroArt>(path);
                bool isNew = art == null;
                if (isNew) art = ScriptableObject.CreateInstance<HeroArt>();
                art.Model = model;
                art.Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath(s));
                art.V2 = UsesV2(s);
                FillSkins(art, s);
                art.Idle = Clip(s.Idle, 0); art.Run = Clip(s.Run, 1); art.Attack = Clip(s.Attack, 2);
                art.Cast = Clip(s.Cast, 3); art.Hit = Clip(s.Hit, 4); art.Death = Clip(s.Death, 5); art.Walk = Clip(s.Walk, 6);
                art.RunFour = s.RunFour != null ? Load(s.RunFour) : null;
                if (s.RunFour != null && art.RunFour == null) Debug.LogWarning("[BAM.Art] " + s.Hero + ": no clip in " + s.RunFour + ".fbx" + WhatIsIn(s.RunFour));
                art.Height = s.Height;
                art.AttackRelease = s.Release;
                art.AttackStart = s.Start;
                if (isNew) AssetDatabase.CreateAsset(art, path);
                else EditorUtility.SetDirty(art);
                ok++;
            }
            // a hero taken out of specs (Hilde, full review 2026-11-09 nr 85) leaves its HeroArt behind on every PC that once built it,
            // and everything under Resources ships: drop the assets no spec names, so the body and clips stay out of the build
            if (AssetDatabase.IsValidFolder(OutDir))
                foreach (string guid in AssetDatabase.FindAssets("t:HeroArt", new[] { OutDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    bool named = false;
                    foreach (Spec s in specs) if (OutOf(s) + "/" + s.Hero + ".asset" == path) { named = true; break; }
                    if (!named && AssetDatabase.DeleteAsset(path)) Debug.Log("[BAM.Art] Dropped " + path + ": no hero in the list uses it");
                }
            AssetDatabase.SaveAssets();
            Debug.Log("[BAM.Art] Hero models ready: " + ok + " of " + specs.Length);
        }

        // the clip of slot from the given file, else from the pool
        static AnimationClip Clip(string p, int slot)
        {
            AnimationClip c = p != null ? Load(p) : null;
            if (c == null) c = Load(pool[slot]);
            if (c == null) Debug.LogWarning("[BAM.Art] Clip missing: " + (p ?? pool[slot]) + ".fbx");
            return c;
        }

        // what an FBX that gave no clip holds after import, so the console shows why (Ragn's RunFour, 2026-11-19)
        static string WhatIsIn(string p)
        {
            string path = ModelsDir + p + ".fbx";
            var sb = new System.Text.StringBuilder(" (holds:");
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                sb.Append(' ').Append(o.GetType().Name).Append(" '").Append(o.name).Append('\'');
                if (o is Avatar a) sb.Append(a.isValid ? " valid" : " INVALID").Append(a.isHuman ? " human" : " not human");
            }
            if (AssetImporter.GetAtPath(path) is ModelImporter mi)
                sb.Append("; importer: ").Append(mi.animationType).Append(", avatar ").Append(mi.avatarSetup)
                  .Append(", importAnimation ").Append(mi.importAnimation).Append(", takes ").Append(mi.importedTakeInfos.Length);
            return sb.Append(')').ToString();
        }

        static AnimationClip Load(string p)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelsDir + p + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }
    }
}
