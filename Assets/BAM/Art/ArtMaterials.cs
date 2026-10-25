using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The few materials the whole game draws with. The assets live in Resources/BAMArt and are made by the editor script
    /// BAM.Art.Editor.ArtMaterialSetup (runs by itself in Unity), so their URP shaders are always in the Android build.
    /// </summary>
    static class ArtMaterials
    {
        public const string Folder = "BAMArt/";
        // Asset names, shared with the editor script that creates them.
        public const string LitName = "BAM_Lit", LitFadeName = "BAM_LitFade", UnlitName = "BAM_Unlit", UnlitFadeName = "BAM_UnlitFade",
            UnlitAddName = "BAM_UnlitAdd", LitClipName = "BAM_LitClip", ParticleAddName = "BAM_ParticleAdd", ParticleAlphaName = "BAM_ParticleAlpha";

        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>Opaque, lit, palette-coloured: arena and every unit.</summary>
        public static Material Lit;
        /// <summary>Same look, alpha blended: units in grass, dying units.</summary>
        public static Material LitFade;
        /// <summary>Lit with alpha clipping (cutoff 0.5): painted pictures with holes, e.g. the island's cliffs and stalactites.</summary>
        public static Material LitClip;
        /// <summary>Opaque unlit palette: health bars, team rings.</summary>
        public static Material Unlit;
        /// <summary>Alpha-blended unlit palette: shadows, rings, zones, aim, the ghost wolf.</summary>
        public static Material UnlitFade;
        /// <summary>Additive unlit palette: glows, flashes, slashes.</summary>
        public static Material UnlitAdd;
        public static Material ParticleAdd, ParticleAlpha;

        static bool ready;

        public static void Init()
        {
            if (ready) return;
            ready = true;
            Lit = Make(LitName, "Universal Render Pipeline/Simple Lit", false, false, Pal.Texture);
            LitFade = Make(LitFadeName, "Universal Render Pipeline/Simple Lit", true, false, Pal.Texture);
            LitClip = Make(LitClipName, "Universal Render Pipeline/Simple Lit", false, false, Pal.Texture, true);
            Unlit = Make(UnlitName, "Universal Render Pipeline/Unlit", false, false, Pal.Texture);
            UnlitFade = Make(UnlitFadeName, "Universal Render Pipeline/Unlit", true, false, Pal.Texture);
            UnlitAdd = Make(UnlitAddName, "Universal Render Pipeline/Unlit", true, true, Pal.Texture);
            ParticleAdd = Make(ParticleAddName, "Universal Render Pipeline/Particles/Unlit", true, true, Pal.Dot);
            ParticleAlpha = Make(ParticleAlphaName, "Universal Render Pipeline/Particles/Unlit", true, false, Pal.Dot);
            // Overlays draw after the world so rings and bars are never hidden by a decal or zone.
            UnlitFade.renderQueue = 3000;
            UnlitAdd.renderQueue = 3050;
        }

        static Material Make(string asset, string shader, bool transparent, bool additive, Texture tex, bool clip = false)
        {
            Material src = Resources.Load<Material>(Folder + asset);
            Material m;
            if (src != null) m = new Material(src);
            else
            {
                // Only happens before the editor script has run (or if the assets were deleted). In the editor the shader
                // is always there; in a player build this would give pink, so say so loudly.
                Debug.LogError("[BAM.Art] Material Resources/" + Folder + asset + " is missing. Open Unity once (BAM > Art > Create Materials) before building.");
                Shader s = Shader.Find(shader);
                if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                m = new Material(s);
                SetupFallback(m, transparent, additive);
                if (clip) SetupClip(m);
            }
            m.name = asset + " (BAM)";
            m.hideFlags = HideFlags.DontSave;
            m.SetTexture(BaseMap, tex);
            m.mainTexture = tex;
            m.SetColor(BaseColor, Color.white);
            if (m.HasProperty(EmissionColor)) m.SetColor(EmissionColor, Color.black);
            return m;
        }

        // The same switches URP's material inspector flips. The editor script sets them on the real assets too.
        public static void SetupFallback(Material m, bool transparent, bool additive)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", transparent ? 1f : 0f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
            if (transparent)
            {
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = 3000;
            }
            else
            {
                m.SetOverrideTag("RenderType", "Opaque");
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = 2000;
            }
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
        }

        /// <summary>Alpha clipping on an opaque URP material (what the inspector's Alpha Clipping box does).</summary>
        public static void SetupClip(Material m)
        {
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        /// <summary>A private copy for one view (hit flash, fade, tint). Made when the view is created, reused by its pool.</summary>
        public static Material Instance(Material m)
        {
            var c = new Material(m) { hideFlags = HideFlags.DontSave };
            return c;
        }
    }
}
