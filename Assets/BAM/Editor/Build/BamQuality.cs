using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BAM.Build
{
    /// <summary>
    /// The two quality levels the game switches between (index = (int)BAM.Core.GraphicsQuality: 0 Low, 1 High), each with
    /// its own URP asset under Assets/BAM/Settings. No post-processing anywhere, no HDR, one directional light.
    /// </summary>
    static class BamQuality
    {
        public const string Folder = "Assets/BAM/Settings";
        public const string LowAsset = Folder + "/BAM_URP_Low.asset";
        public const string HighAsset = Folder + "/BAM_URP_High.asset";
        const string LowRenderer = Folder + "/BAM_URP_Low_Renderer.asset";
        const string HighRenderer = Folder + "/BAM_URP_High_Renderer.asset";
        const string QualityPath = "ProjectSettings/QualitySettings.asset";

        public static readonly string[] LevelNames = { "Low", "High" };

        public static void Apply(BamProjectSetup.Log log)
        {
            EnsureFolder(Folder);
            var low = EnsureAsset(LowAsset, LowRenderer, log);
            var high = EnsureAsset(HighAsset, HighRenderer, log);
            if (low == null || high == null)
            {
                Debug.LogError("[BAM.Build] Could not create the URP assets; quality levels left as they were");
                return;
            }

            ConfigureLow(low, log);
            ConfigureHigh(high, log);
            ConfigureRenderer(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LowRenderer), log);
            ConfigureRenderer(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(HighRenderer), log);
            EditorUtility.SetDirty(low);
            EditorUtility.SetDirty(high);

            // Fallback for anything that reads the default pipeline rather than the quality level's.
            if (GraphicsSettings.defaultRenderPipeline != high)
            {
                log.Change("Graphics default render pipeline", Name(GraphicsSettings.defaultRenderPipeline), "BAM_URP_High");
                GraphicsSettings.defaultRenderPipeline = high;
            }

            ApplyLevels(new RenderPipelineAsset[] { low, high }, log);
        }

        static UniversalRenderPipelineAsset EnsureAsset(string assetPath, string rendererPath, BamProjectSetup.Log log)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = null;
                AssetDatabase.CreateAsset(renderer, rendererPath);
                log.Change("Created", "", rendererPath);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
                log.Change("Created", "", assetPath);
            }

            // The asset must render with its own renderer (index 0), whatever happened to it since.
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list != null && list.isArray)
            {
                if (list.arraySize != 1 || list.GetArrayElementAtIndex(0).objectReferenceValue != renderer)
                {
                    list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                    var def = so.FindProperty("m_DefaultRendererIndex");
                    if (def != null) def.intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.Change(assetPath + " renderer", "", rendererPath);
                }
            }
            else
            {
                Debug.LogWarning("[BAM.Build] " + assetPath + ": renderer list not found; check it uses " + rendererPath);
            }
            return asset;
        }

        static void ConfigureLow(UniversalRenderPipelineAsset a, BamProjectSetup.Log log)
        {
            string n = "Low URP ";
            if (!Mathf.Approximately(a.renderScale, 0.8f)) { log.Change(n + "render scale", a.renderScale, 0.8f); a.renderScale = 0.8f; }
            if (a.msaaSampleCount != 1) { log.Change(n + "MSAA", a.msaaSampleCount, 1); a.msaaSampleCount = 1; }
            SetSerialized(a, "m_MainLightShadowsSupported", 0, n + "main light shadows", log);
            Common(a, n, log);
        }

        static void ConfigureHigh(UniversalRenderPipelineAsset a, BamProjectSetup.Log log)
        {
            string n = "High URP ";
            if (!Mathf.Approximately(a.renderScale, 1f)) { log.Change(n + "render scale", a.renderScale, 1f); a.renderScale = 1f; }
            if (a.msaaSampleCount != 2) { log.Change(n + "MSAA", a.msaaSampleCount, 2); a.msaaSampleCount = 2; }
            SetSerialized(a, "m_MainLightShadowsSupported", 1, n + "main light shadows", log);
            if (a.mainLightShadowmapResolution != 1024) { log.Change(n + "shadow map", a.mainLightShadowmapResolution, 1024); a.mainLightShadowmapResolution = 1024; }
            // The whole arena (15 x 35 tiles) seen from above fits in about 30 m from the camera.
            if (!Mathf.Approximately(a.shadowDistance, 30f)) { log.Change(n + "shadow distance", a.shadowDistance, 30f); a.shadowDistance = 30f; }
            if (a.shadowCascadeCount != 1) { log.Change(n + "shadow cascades", a.shadowCascadeCount, 1); a.shadowCascadeCount = 1; }
            SetSerialized(a, "m_SoftShadowsSupported", 0, n + "soft shadows", log);
            Common(a, n, log);
        }

        // What both levels share: the cheapest pipeline that still looks right for flat-shaded colour.
        static void Common(UniversalRenderPipelineAsset a, string n, BamProjectSetup.Log log)
        {
            if (a.supportsHDR) { log.Change(n + "HDR", true, false); a.supportsHDR = false; }
            if (a.supportsCameraDepthTexture) { log.Change(n + "depth texture", true, false); a.supportsCameraDepthTexture = false; }
            if (a.supportsCameraOpaqueTexture) { log.Change(n + "opaque texture", true, false); a.supportsCameraOpaqueTexture = false; }
            // These have no public setter in URP 17.6; the asset's own fields are what its inspector edits.
            SetSerialized(a, "m_MainLightRenderingMode", (int)LightRenderingMode.PerPixel, n + "main light", log);
            SetSerialized(a, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.Disabled, n + "additional lights", log);
            SetSerialized(a, "m_AdditionalLightShadowsSupported", 0, n + "additional light shadows", log);
            if (!a.useSRPBatcher) { log.Change(n + "SRP batcher", false, true); a.useSRPBatcher = true; }
            if (a.colorGradingMode != ColorGradingMode.LowDynamicRange) { log.Change(n + "color grading", a.colorGradingMode, ColorGradingMode.LowDynamicRange); a.colorGradingMode = ColorGradingMode.LowDynamicRange; }
            // GPUResidentDrawerMode lives in Unity.RenderPipelines.GPUDriven.Runtime in Unity 6.6, so this goes through the field (0 = Disabled).
            SetSerialized(a, "m_GPUResidentDrawerMode", 0, n + "GPU resident drawer", log);
            if (a.volumeProfile != null) { log.Change(n + "volume profile", a.volumeProfile.name, "none"); a.volumeProfile = null; }
            if (a.upscalingFilter != UpscalingFilterSelection.Linear) { log.Change(n + "upscaling filter", a.upscalingFilter, UpscalingFilterSelection.Linear); a.upscalingFilter = UpscalingFilterSelection.Linear; }
        }

        static void SetSerialized(Object o, string field, int value, string label, BamProjectSetup.Log log)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(field);
            if (p == null) { log.Warn(o.name + ": field " + field + " not found, check '" + label + "' by hand"); return; }
            int current = p.propertyType == SerializedPropertyType.Boolean ? (p.boolValue ? 1 : 0) : p.intValue;
            if (current == value) return;
            log.Change(label, current, value);
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureRenderer(UniversalRendererData r, BamProjectSetup.Log log)
        {
            if (r == null) return;
            string n = r.name + " ";
            bool dirty = false;
            if (r.postProcessData != null) { log.Change(n + "post-processing", "on", "off"); r.postProcessData = null; dirty = true; }
            if (r.renderingMode != RenderingMode.Forward) { log.Change(n + "rendering path", r.renderingMode, RenderingMode.Forward); r.renderingMode = RenderingMode.Forward; dirty = true; }
            if (r.depthPrimingMode != DepthPrimingMode.Disabled) { log.Change(n + "depth priming", r.depthPrimingMode, DepthPrimingMode.Disabled); r.depthPrimingMode = DepthPrimingMode.Disabled; dirty = true; }
            if (dirty) EditorUtility.SetDirty(r);
        }

        // Unity has no public API to add, remove or rename quality levels, so this edits the settings object the way the
        // Quality window does (through SerializedObject, so Unity still writes the file).
        static void ApplyLevels(RenderPipelineAsset[] assets, BamProjectSetup.Log log)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(QualityPath);
            if (objs == null || objs.Length == 0 || objs[0] == null)
            {
                Debug.LogError("[BAM.Build] Could not open " + QualityPath + "; set the quality levels by hand");
                return;
            }
            var so = new SerializedObject(objs[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                Debug.LogError("[BAM.Build] " + QualityPath + " has no m_QualitySettings list; set the quality levels by hand");
                return;
            }

            if (levels.arraySize != LevelNames.Length)
            {
                log.Change("Quality level count", levels.arraySize, LevelNames.Length);
                // Growing duplicates the last level, which is then overwritten below.
                levels.arraySize = LevelNames.Length;
            }

            for (int i = 0; i < LevelNames.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                SetString(level, "name", LevelNames[i], "Quality " + i + " name", log);
                SetObject(level, "customRenderPipeline", assets[i], "Quality " + LevelNames[i] + " URP asset", log);
                // The game caps the frame rate itself; vsync would fight it on some phones.
                SetInt(level, "vSyncCount", 0, "Quality " + LevelNames[i] + " vsync", log);
                SetInt(level, "antiAliasing", 0, "Quality " + LevelNames[i] + " quality-level MSAA (URP asset decides)", log);
                SetInt(level, "realtimeReflectionProbes", 0, "Quality " + LevelNames[i] + " realtime reflection probes", log);
                SetInt(level, "softParticles", 0, "Quality " + LevelNames[i] + " soft particles", log);
            }

            SetInt(so.FindProperty("m_CurrentQuality"), 1, "Quality level used in the editor", log);

            // Per-platform defaults pointing past the new list would leave a platform without a level.
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var entry = perPlatform.GetArrayElementAtIndex(i);
                    var platform = entry.FindPropertyRelative("first");
                    var index = entry.FindPropertyRelative("second");
                    if (platform == null || index == null) continue;
                    int want = platform.stringValue == "Android" ? 1 : Mathf.Clamp(index.intValue, 0, LevelNames.Length - 1);
                    if (index.intValue != want)
                    {
                        log.Change("Default quality for " + platform.stringValue, index.intValue, want);
                        index.intValue = want;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(SerializedProperty parent, string rel, string value, string label, BamProjectSetup.Log log)
        {
            var p = parent.FindPropertyRelative(rel);
            if (p == null) { Debug.LogWarning("[BAM.Build] Quality setting '" + rel + "' not found"); return; }
            if (p.stringValue == value) return;
            log.Change(label, p.stringValue, value);
            p.stringValue = value;
        }

        static void SetObject(SerializedProperty parent, string rel, Object value, string label, BamProjectSetup.Log log)
        {
            var p = parent.FindPropertyRelative(rel);
            if (p == null) { Debug.LogWarning("[BAM.Build] Quality setting '" + rel + "' not found"); return; }
            if (p.objectReferenceValue == value) return;
            log.Change(label, Name(p.objectReferenceValue), Name(value));
            p.objectReferenceValue = value;
        }

        static void SetInt(SerializedProperty parent, string rel, int value, string label, BamProjectSetup.Log log)
        {
            if (parent == null) return;
            SetInt(parent.FindPropertyRelative(rel), value, label, log);
        }

        static void SetInt(SerializedProperty p, int value, string label, BamProjectSetup.Log log)
        {
            if (p == null) { Debug.LogWarning("[BAM.Build] Quality setting for '" + label + "' not found"); return; }
            if (p.intValue == value) return;
            log.Change(label, p.intValue, value);
            p.intValue = value;
        }

        static string Name(Object o) => o == null ? "none" : o.name;

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
