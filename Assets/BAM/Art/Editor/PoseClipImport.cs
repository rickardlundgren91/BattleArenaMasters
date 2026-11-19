using System.IO;
using UnityEditor;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Import settings for the end-of-match clips in Resources/BAMArt/Poses (BodyEndPose: Victory.fbx, Defeat.fbx, cut from
    /// Quaternius' Universal Animation Library 1 and 2, CC0). They are on Quaternius' own humanoid rig, not Mixamo's, so each file
    /// is humanoid with its own avatar (Quaternius' Unity setup: Humanoid, Create From This Model, Bake Axis Conversion) and plays
    /// on every hero through Unity's retargeting. One looping clip per file, in place; no mesh, materials, cameras or lights.
    /// </summary>
    sealed class PoseClipImport : AssetPostprocessor
    {
        const string PosesDir = "Assets/BAM/Art/Resources/BAMArt/Poses/";

        bool Ours => assetPath.StartsWith(PosesDir) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation c = clips[i];
                c.name = i == 0 ? name : name + i;
                c.loopTime = true;
                c.loopPose = true;
                // in place, as the heroes' own clips: the game places the unit
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
