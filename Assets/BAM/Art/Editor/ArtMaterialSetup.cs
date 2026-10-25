using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Creates the few material assets the game draws with (Assets/BAM/Art/Resources/BAMArt). Runs by itself after every
    /// script reload when one is missing, so a fresh checkout works without a click; BAM > Art > Create Materials redoes them.
    /// Being assets in Resources is what pulls their URP shaders into the Android build.
    /// </summary>
    [InitializeOnLoad]
    static class ArtMaterialSetup
    {
        const string Dir = "Assets/BAM/Art/Resources/BAMArt";

        static ArtMaterialSetup()
        {
            EditorApplication.delayCall += () => Ensure(false);
        }

        [MenuItem("BAM/Art/Create Materials")]
        static void Recreate() => Ensure(true);

        static void Ensure(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool made = false;
            made |= Make(ArtMaterials.LitName, "Universal Render Pipeline/Simple Lit", false, false, force);
            made |= Make(ArtMaterials.LitFadeName, "Universal Render Pipeline/Simple Lit", true, false, force);
            made |= Make(ArtMaterials.UnlitName, "Universal Render Pipeline/Unlit", false, false, force);
            made |= Make(ArtMaterials.UnlitFadeName, "Universal Render Pipeline/Unlit", true, false, force);
            made |= Make(ArtMaterials.UnlitAddName, "Universal Render Pipeline/Unlit", true, true, force);
            made |= Make(ArtMaterials.ParticleAddName, "Universal Render Pipeline/Particles/Unlit", true, true, force);
            made |= Make(ArtMaterials.ParticleAlphaName, "Universal Render Pipeline/Particles/Unlit", true, false, force);
            if (made)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BAM.Art] Materials ready in " + Dir);
            }
        }

        static bool Make(string name, string shaderName, bool transparent, bool additive, bool force)
        {
            string path = Dir + "/" + name + ".mat";
            if (!force && AssetDatabase.LoadAssetAtPath<Material>(path) != null) return false;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[BAM.Art] Shader not found: " + shaderName + ". Is URP installed?");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(Dir)) Directory.CreateDirectory(Dir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(shader);
            else m.shader = shader;
            ArtMaterials.SetupFallback(m, transparent, additive);
            // let URP's own inspector code finish the job (keywords, passes, queue) exactly as it would by hand
            BaseShaderGUI.SetupMaterialBlendMode(m);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (m.HasProperty("_ReceiveShadows")) m.SetFloat("_ReceiveShadows", transparent ? 0f : 1f);
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return true;
        }
    }
}
