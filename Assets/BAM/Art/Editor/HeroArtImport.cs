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

        bool Ours => assetPath.StartsWith(ModelsDir) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        bool IsClip => assetPath.Contains("/Anim/");

        // Rickard's arena pictures repeat across the floor and walls; 512 is plenty at the match camera's distance.
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/BAM/Art/Resources/BAMArt/Arena/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.sRGBTexture = true;
            ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        void OnPreprocessModel()
        {
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
                if (p.StartsWith(ModelsDir))
                {
                    EditorApplication.delayCall -= BuildAll;
                    EditorApplication.delayCall += BuildAll;
                    return;
                }
        }

        struct Spec
        {
            public string Hero, Model, Idle, Run, Attack, Cast, Hit, Death;
            public float Height, Release;
        }

        // Moa and Blast have no clips of their own in the project yet: they borrow other heroes' (humanoid clips retarget).
        static readonly Spec[] specs =
        {
            new Spec { Hero = "Bram", Model = "Bram/Bram", Idle = "Bram/Anim/Idle", Run = "Bram/Anim/Run", Attack = "Bram/Anim/Attack1", Cast = "Bram/Anim/Attack2", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Height = 0.95f, Release = 0.45f },
            new Spec { Hero = "Fenn", Model = "Fenn/Fenn", Idle = "Fenn/Anim/Idle", Run = "Fenn/Anim/Run", Attack = "Fenn/Anim/Attack1", Cast = "Fenn/Anim/Cast", Hit = "Fenn/Anim/Hit", Death = "Fenn/Anim/Dying", Height = 1.05f, Release = 0.45f },
            new Spec { Hero = "Tyra", Model = "Tyra/Tyra", Idle = "Tyra/Anim/IdleBow", Run = "Tyra/Anim/RunBow", Attack = "Tyra/Anim/Shoot", Cast = "Tyra/Anim/Cast", Hit = "Tyra/Anim/HitBow", Death = "Tyra/Anim/Dying", Height = 1.08f, Release = 0.55f },
            new Spec { Hero = "Moa", Model = "Moa/Moa", Idle = "Tikta/Anim/Idle", Run = "Tikta/Anim/Run", Attack = "Knut/Anim/Throw", Cast = "Fenn/Anim/Cast", Hit = "Tikta/Anim/Hit", Death = "Tikta/Anim/Dying", Height = 0.9f, Release = 0.4f },
            new Spec { Hero = "Knut", Model = "Knut/Knut", Idle = "Knut/Anim/Idle", Run = "Knut/Anim/Run", Attack = "Knut/Anim/Attack1", Cast = "Knut/Anim/Throw", Hit = "Knut/Anim/Hit", Death = "Knut/Anim/Dying", Height = 1.15f, Release = 0.45f },
            new Spec { Hero = "Tikta", Model = "Tikta/Tikta", Idle = "Tikta/Anim/Idle", Run = "Tikta/Anim/Run", Attack = "Tikta/Anim/Attack1", Cast = "Tikta/Anim/Cast", Hit = "Tikta/Anim/Hit", Death = "Tikta/Anim/Dying", Height = 0.95f, Release = 0.4f },
            new Spec { Hero = "Blast", Model = "Blast/Blast", Idle = "Bram/Anim/Idle", Run = "Bram/Anim/Run", Attack = "Knut/Anim/Throw", Cast = "Fenn/Anim/Cast", Hit = "Bram/Anim/Hit", Death = "Bram/Anim/Dying", Height = 0.9f, Release = 0.4f },
        };

        [MenuItem("BAM/Art/Rebuild Hero Models")]
        static void Rebuild()
        {
            foreach (Spec s in specs)
                foreach (string p in new[] { s.Model, s.Idle, s.Run, s.Attack, s.Cast, s.Hit, s.Death })
                    AssetDatabase.ImportAsset(ModelsDir + p + ".fbx", ImportAssetOptions.ForceUpdate);
            BuildAll();
        }

        internal static void BuildAll()
        {
            if (EditorApplication.isPlaying) return;
            if (!AssetDatabase.IsValidFolder(OutDir)) Directory.CreateDirectory(OutDir);
            int ok = 0;
            foreach (Spec s in specs)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + s.Model + ".fbx");
                if (model == null) { Debug.LogWarning("[BAM.Art] " + s.Hero + ": model " + s.Model + ".fbx not found, the code model is used"); continue; }
                string path = OutDir + "/" + s.Hero + ".asset";
                var art = AssetDatabase.LoadAssetAtPath<HeroArt>(path);
                bool isNew = art == null;
                if (isNew) art = ScriptableObject.CreateInstance<HeroArt>();
                art.Model = model;
                art.Idle = Clip(s.Idle); art.Run = Clip(s.Run); art.Attack = Clip(s.Attack);
                art.Cast = Clip(s.Cast); art.Hit = Clip(s.Hit); art.Death = Clip(s.Death);
                art.Height = s.Height;
                art.AttackRelease = s.Release;
                if (isNew) AssetDatabase.CreateAsset(art, path);
                else EditorUtility.SetDirty(art);
                ok++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BAM.Art] Hero models ready: " + ok + " of " + specs.Length);
        }

        static AnimationClip Clip(string p)
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelsDir + p + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            Debug.LogWarning("[BAM.Art] Clip missing: " + p + ".fbx");
            return null;
        }
    }
}
