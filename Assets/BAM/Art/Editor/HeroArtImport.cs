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
        const string OutDir = "Assets/BAM/Art/Resources/BAMArt/Heroes";
        static string OutOf(Spec s) => s.Out == null ? OutDir : "Assets/BAM/Art/Resources/BAMArt/" + s.Out;

        bool Ours => assetPath.StartsWith(ModelsDir) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        bool IsClip => assetPath.Contains("/Anim/");

        // Rickard's arena pictures repeat across the floor and walls; 512 is plenty at the match camera's distance.
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(ModelsDir) || assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Props/") || assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Wolf/"))
            {
                // the heroes' and props' colour pictures: 1024 is plenty on a phone, compressed
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
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.sRGBTexture = true;
            // the baked arena ground is one 480x1120 picture of the whole floor: keep its full size
            ti.maxTextureSize = assetPath.Contains("T_BAM_ArenaGround") ? 2048 : 512;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        // Fenn's wolf (Resources/BAMArt/Wolf): Rickard's Meshy beast on four legs can't be Humanoid, so Generic with its own
        // skeleton, no materials (the ghost material goes on at runtime) and Meshy's one clip, the walk, looping in place (old AbcKovaFormsImport)
        bool IsWolf => assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Wolf/") && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (IsWolf)
            {
                var wm = (ModelImporter)assetImporter;
                wm.animationType = ModelImporterAnimationType.Generic;
                wm.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                wm.materialImportMode = ModelImporterMaterialImportMode.None;
                wm.importAnimation = true;
                wm.importBlendShapes = false; wm.importCameras = false; wm.importLights = false;
                wm.isReadable = false;
                return;
            }
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.isReadable = false;
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
            if (IsWolf)
            {
                var wm = (ModelImporter)assetImporter;
                ModelImporterClipAnimation[] wc = wm.defaultClipAnimations;
                if (wc == null) return;
                foreach (ModelImporterClipAnimation c in wc)
                {
                    c.name = "Walk"; c.loopTime = true;
                    c.lockRootRotation = true; c.keepOriginalOrientation = true;
                    c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                }
                wm.clipAnimations = wc;
                return;
            }
            if (!Ours || !IsClip) return;
            var mi = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = name.StartsWith("Idle") || name.StartsWith("Run") || name.StartsWith("Walk");
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
            }
            mi.clipAnimations = clips;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string p in imported)
                if (p.StartsWith(ModelsDir) || p.StartsWith("Assets/BAM/Art/Editor/"))
                {
                    EditorApplication.delayCall -= BuildAll;
                    EditorApplication.delayCall += BuildAll;
                    return;
                }
        }

        struct Spec
        {
            public string Out; // null = Heroes
            public string Hero, Model, Idle, Run, Attack, Cast, Hit, Death, Walk;
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
            new Spec { Hero = "Bram", Model = "Bram/Bram", Idle = "Bram/Anim/Idle", Run = "Bram/Anim/Run", Attack = "Bonk/Anim/Attack1", Cast = "Bram/Anim/Attack2", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Walk = "Bram/Anim/Walk", Height = 1.59f, Release = 0.46f, Start = 0.12f },
            // Rickard 2026-10-28: Fenn "vänder sig bakåt och sparkar ut i luften för varje attack": his own Attack1 (Speltest's Power
            // Slash) is a jump with a whole turn (the hips turn 386 degrees and rise half a hip height). Knut's level swing after it was
            // made for an anchor hanging from the fist: Fenn's sword stood 35-60 degrees up at every blow (ANIM-21, Unity run 2), so he
            // chops down with Bonk's one-handed overhead strike from 12 % as Bram does (the hammer meets the target 15 degrees down),
            // his shield staying in the left hand (Rickard's pick 2026-10-29)
            new Spec { Hero = "Fenn", Model = "Fenn/Fenn", Idle = "Fenn/Anim/Idle", Run = "Fenn/Anim/Run", Attack = "Bonk/Anim/Attack1", Cast = "Fenn/Anim/Cast", Hit = "Fenn/Anim/Hit", Death = "Fenn/Anim/Dying", Walk = "Fenn/Anim/Walk", Height = 2.19f, Release = 0.46f, Start = 0.12f },
            // Tyra stands relaxed with the bow down (Moa's idle) in the menu and the match and raises it only to shoot (Rickard
            // 2026-10-27: her drawn-bow IdleBow in the menu "precis samma som när hon är i spelet"; old Speltest's Home had her in a
            // relaxed idle too)
            new Spec { Hero = "Tyra", Model = "Tyra/Tyra", Idle = "Moa/Anim/Idle", Run = "Tyra/Anim/Run", Attack = "Tyra/Anim/Shoot", Cast = "Tyra/Anim/Cast", Hit = "Tyra/Anim/HitBow", Death = "Tyra/Anim/Dying", Walk = "Tyra/Anim/Walk", Height = 1.78f, Release = 0.55f },
            new Spec { Hero = "Moa", Model = "Moa/Moa", Idle = "Moa/Anim/Idle", Run = "Moa/Anim/Run", Attack = "Moa/Anim/Attack1", Cast = "Moa/Anim/Cast", Hit = "Moa/Anim/Hit", Death = "Moa/Anim/Dying", Walk = "Moa/Anim/Walk", Height = 1.73f, Release = 0.4f },
            new Spec { Hero = "Knut", Model = "Knut/Knut", Idle = "Knut/Anim/Idle", Run = "Knut/Anim/Run", Attack = "Knut/Anim/Attack1", Cast = "Knut/Anim/Throw", Hit = "Knut/Anim/Hit", Death = "Knut/Anim/Dying", Walk = "Knut/Anim/Walk", Height = 1.85f, Release = 0.45f },
            new Spec { Hero = "Tikta", Model = "Tikta/Tikta", Idle = "Tikta/Anim/Idle", Run = "Tikta/Anim/Run", Attack = "Tikta/Anim/Attack1", Cast = "Tikta/Anim/Cast", Hit = "Tikta/Anim/Hit", Death = "Tikta/Anim/Dying", Walk = "Tikta/Anim/Walk", Height = 1.94f, Release = 0.4f },
            // the lane minions: the old game's Mixamo goblin creeps (one figure, three paint jobs), moving with the heroes' clips
            new Spec { Out = "Minions", Hero = "Melee", Model = "Creeps/Melee", Attack = "Tikta/Anim/Attack1", Height = 0.8f, Release = 0.4f },
            new Spec { Out = "Minions", Hero = "Ranged", Model = "Creeps/Ranged", Attack = "Knut/Anim/Throw", Height = 0.78f, Release = 0.4f },
            new Spec { Out = "Minions", Hero = "Siege", Model = "Creeps/Siege", Idle = "Bram/Anim/Idle", Run = "Bram/Anim/Run", Attack = "Bram/Anim/Attack1", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Walk = "Bram/Anim/Walk", Height = 0.9f, Release = 0.45f },
            new Spec { Hero = "Blast", Model = "Blast/Blast", Idle = "Blast/Anim/Idle", Run = "Blast/Anim/Run", Attack = "Blast/Anim/Attack1", Cast = "Blast/Anim/Cast", Hit = "Blast/Anim/Hit", Death = "Blast/Anim/Dying", Walk = "Blast/Anim/Walk", Height = 1.88f, Release = 0.4f },
            // Bonk (hero 8, Rickard's Tripo body rigged in Mixamo 2026-10-26; his wolf pelt reweighted to the chest so it no longer
            // stretches with his left arm and head). His own Mixamo clips (Orc Idle, Mutant Run, Standing Melee Attack Downward with
            // the club landing at 46 %, Hit Reaction, Dying); the slam borrows Bram's Anvil Strike, since Mixamo's jump attack is
            // airborne for longer than a cast lasts. Idle is Knut's: on Bonk the Orc Idle bent his feet and lifted him off the ground
            // (Rickard 2026-10-26: "fötterna böjer sig")
            new Spec { Hero = "Bonk", Model = "Bonk/Bonk", Idle = "Knut/Anim/Idle", Run = "Bonk/Anim/Run", Attack = "Bonk/Anim/Attack1", Cast = "Bram/Anim/Attack2", Hit = "Bonk/Anim/Hit", Death = "Bonk/Anim/Dying", Walk = "Knut/Anim/Walk", Height = 2.0f, Release = 0.46f },
            // Hilde (hero 9) and Oaken (hero 10), 2026-10-28: Speltest's current bodies (Mixamo/Looks/Hilde with Hilde2.jpg, Looks/Oaken
            // with OaknN.jpg) and clips (Resources/Mixamo/Anim/Hilde|Oaken). Hilde's shot is a 0.27 s rifle clip for the shoulder cannon,
            // her cast the keg throw. Oaken walks and runs with the heavy Mutant clips; his attack is a hook punch (the fist is furthest
            // out at 0.49 of the clip) and his cast a two-arm overhead smash (fists on the ground at 0.44). Heights: Speltest HeroScale
            // visible ratios (Hilde 1.01, Oaken 1.10) x 1.62 as the others.
            new Spec { Hero = "Hilde", Model = "Hilde/Hilde", Idle = "Hilde/Anim/Idle", Run = "Hilde/Anim/Run", Attack = "Hilde/Anim/Attack1", Cast = "Hilde/Anim/Cast", Hit = "Hilde/Anim/Hit", Death = "Hilde/Anim/Dying", Walk = "Hilde/Anim/Walk", Height = 1.64f, Release = 0.3f },
            new Spec { Hero = "Oaken", Model = "Oaken/Oaken", Idle = "Oaken/Anim/Idle", Run = "Oaken/Anim/Run", Attack = "Oaken/Anim/Attack1", Cast = "Oaken/Anim/Cast", Hit = "Oaken/Anim/Hit", Death = "Oaken/Anim/Dying", Walk = "Oaken/Anim/Walk", Height = 1.78f, Release = 0.49f },
        };

        // Fenn's two bodies (Rickard 2026-10-29: "testa att ta in den bättre modellen av fenn som bugga innan"): the Original
        // (Models/FennOriginal, his Meshy body rigged in Mixamo, with the right leg's ankle and foot weights mirrored from the left
        // since Mixamo had put that ankle on the toes) or the Clay body (Models/Fenn). Original unless BAM > Art > Fenn: Clay is
        // picked on this computer; the choice is remembered per computer and the hero list is built again at once.
        const string FennClayPref = "BAM.Art.FennClay";
        static string ModelOf(Spec s) => s.Hero == "Fenn" && !EditorPrefs.GetBool(FennClayPref, false) ? "FennOriginal/Fenn" : s.Model;

        [MenuItem("BAM/Art/Fenn: Original")]
        static void FennOriginal() { EditorPrefs.SetBool(FennClayPref, false); Rebuild(); }
        [MenuItem("BAM/Art/Fenn: Original", true)]
        static bool FennOriginalCheck() { Menu.SetChecked("BAM/Art/Fenn: Original", !EditorPrefs.GetBool(FennClayPref, false)); return true; }
        [MenuItem("BAM/Art/Fenn: Clay")]
        static void FennClay() { EditorPrefs.SetBool(FennClayPref, true); Rebuild(); }
        [MenuItem("BAM/Art/Fenn: Clay", true)]
        static bool FennClayCheck() { Menu.SetChecked("BAM/Art/Fenn: Clay", EditorPrefs.GetBool(FennClayPref, false)); return true; }

        // Also when Unity opens or the scripts reload: the game must never fall back to the code figures just because
        // the hero list was not built yet on this computer.
        [InitializeOnLoadMethod]
        static void EnsureBuilt()
        {
            EditorApplication.delayCall += () =>
            {
                // a model or clip imported before these import rules compiled (first open of a fresh copy) keeps Unity's defaults:
                // not humanoid, clips not looping, so the heroes stand frozen. Import those files again with the rules.
                if (ReimportStale()) { BuildAll(); return; }
                foreach (Spec s in specs)
                {
                    var art = AssetDatabase.LoadAssetAtPath<HeroArt>(OutOf(s) + "/" + s.Hero + ".asset");
                    if (art == null || art.Model == null || art.Model != AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + ModelOf(s) + ".fbx") || art.Texture == null || !Mathf.Approximately(art.Height, s.Height)
                        || art.Idle == null || art.Walk == null || art.Run == null || art.Attack == null || art.Cast == null || art.Hit == null || art.Death == null) { BuildAll(); return; }
                }
            };
        }

        internal static void ReimportStaleForBuild() => ReimportStale();

        static bool ReimportStale()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (Spec s in specs)
                foreach (string p in new[] { ModelOf(s), s.Idle, s.Run, s.Attack, s.Cast, s.Hit, s.Death, s.Walk })
                    if (p != null) paths.Add(ModelsDir + p + ".fbx");
            foreach (string p in pool) paths.Add(ModelsDir + p + ".fbx");
            bool any = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    bool stale = mi.animationType != ModelImporterAnimationType.Human;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!stale && path.Contains("/Anim/") && (name.StartsWith("Idle") || name.StartsWith("Run") || name.StartsWith("Walk")))
                    {
                        ModelImporterClipAnimation[] c = mi.clipAnimations;
                        stale = c == null || c.Length == 0 || !c[0].loopTime;
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
                foreach (string p in new[] { ModelOf(s), s.Idle, s.Run, s.Attack, s.Cast, s.Hit, s.Death, s.Walk })
                    if (p != null) AssetDatabase.ImportAsset(ModelsDir + p + ".fbx", ImportAssetOptions.ForceUpdate);
            BuildAll();
        }

        internal static void BuildAll()
        {
            if (EditorApplication.isPlaying) return;
            int ok = 0;
            foreach (Spec s in specs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + ModelOf(s) + ".fbx");
                if (model == null) { Debug.LogWarning("[BAM.Art] " + s.Hero + ": model " + ModelOf(s) + ".fbx not found, the code model is used"); continue; }
                if (!AssetDatabase.IsValidFolder(OutOf(s))) { Directory.CreateDirectory(OutOf(s)); AssetDatabase.Refresh(); }
                string path = OutOf(s) + "/" + s.Hero + ".asset";
                var art = AssetDatabase.LoadAssetAtPath<HeroArt>(path);
                bool isNew = art == null;
                if (isNew) art = ScriptableObject.CreateInstance<HeroArt>();
                art.Model = model;
                art.Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelsDir + ModelOf(s) + "_Tex.jpg");
                art.Idle = Clip(s.Idle, 0); art.Run = Clip(s.Run, 1); art.Attack = Clip(s.Attack, 2);
                art.Cast = Clip(s.Cast, 3); art.Hit = Clip(s.Hit, 4); art.Death = Clip(s.Death, 5); art.Walk = Clip(s.Walk, 6);
                art.Height = s.Height;
                art.AttackRelease = s.Release;
                art.AttackStart = s.Start;
                if (isNew) AssetDatabase.CreateAsset(art, path);
                else EditorUtility.SetDirty(art);
                ok++;
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

        static AnimationClip Load(string p)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelsDir + p + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }
    }
}
