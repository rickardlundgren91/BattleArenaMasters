using System.Collections.Generic;
using System.IO;
using BAM.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Build
{
    /// <summary>
    /// BAM > Setup Project: puts every player, quality and scene setting the Google Play build needs in place.
    /// Checks before it changes anything and logs each change, so running it again is harmless.
    /// </summary>
    public static class BamProjectSetup
    {
        public const string Company = "Battle Arena Masters";
        public const string MainScene = "Assets/BAM/Scenes/Main.unity";
        const string BrandingFolder = "Assets/BAM/Branding";
        const string IconForeground = BrandingFolder + "/icon_foreground.png";
        const string IconBackground = BrandingFolder + "/icon_background.png";
        const string IconLegacy = BrandingFolder + "/icon_legacy.png";
        const string KeepSamplesPref = "BAM.Build.KeepTemplateSamples";

        // What the Universal 3D template brings that the game never uses. The URP global settings and the default
        // volume profile in Assets/Settings stay: URP needs them.
        static readonly string[] TemplateSamples =
        {
            "Assets/TutorialInfo",
            "Assets/Readme.asset",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Settings/SampleSceneProfile.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/Mobile_Renderer.asset",
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/PC_Renderer.asset",
        };

        /// <summary>Collects what changed so the run ends with one summary line.</summary>
        public sealed class Log
        {
            public int Changes;
            public int Warnings;

            public void Change(string what, object from, object to)
            {
                Changes++;
                Debug.Log("[BAM.Build] " + what + ": " + Show(from) + " -> " + Show(to));
            }

            public void Warn(string message)
            {
                Warnings++;
                Debug.LogWarning("[BAM.Build] " + message);
            }

            static string Show(object o)
            {
                string s = o == null ? "" : o.ToString();
                return s.Length == 0 ? "(empty)" : s;
            }
        }

        [MenuItem("BAM/Setup Project", priority = 1)]
        static void Menu()
        {
            var log = Run(true);
            string msg = log.Changes == 0
                ? "Allt var redan rätt inställt."
                : log.Changes + " inställningar ändrades (se Console).";
            if (log.Warnings > 0) msg += "\n\n" + log.Warnings + " varningar: titta i Console efter gula rader som börjar med [BAM.Build].";
            EditorUtility.DisplayDialog("BAM Setup Project", msg, "OK");
        }

        /// <summary>Applies everything. <paramref name="interactive"/> false skips questions (used by the build).</summary>
        public static Log Run(bool interactive)
        {
            var log = new Log();
            Debug.Log("[BAM.Build] Setup Project started");

            Identity(log);
            AndroidSettings(log);
            Orientation(log);
            BamVersion.Sync();
            BamQuality.Apply(log);
            Scene(log);
            Icons(log);
            PlayMode(log);

            AssetDatabase.SaveAssets();
            if (interactive) RemoveTemplateSamples(log);

            Debug.Log("[BAM.Build] Setup Project done: " + log.Changes + " changes, " + log.Warnings + " warnings");
            return log;
        }

        // The game keeps its state in static classes. Without a domain reload a second press on Play would reuse objects
        // destroyed by the first, so Play always reloads everything (the Unity default).
        static void PlayMode(Log log)
        {
            if (EditorSettings.enterPlayModeOptionsEnabled)
            {
                log.Change("Enter Play Mode options", "custom", "reload domain and scene");
                EditorSettings.enterPlayModeOptionsEnabled = false;
            }
        }

        static void Identity(Log log)
        {
            if (PlayerSettings.companyName != Company) { log.Change("Company name", PlayerSettings.companyName, Company); PlayerSettings.companyName = Company; }
            // The label under the icon on the phone.
            if (PlayerSettings.productName != BamInfo.ShortName) { log.Change("Product name", PlayerSettings.productName, BamInfo.ShortName); PlayerSettings.productName = BamInfo.ShortName; }
            if (PlayerSettings.bundleVersion != BamVersion.Name) { log.Change("Version", PlayerSettings.bundleVersion, BamVersion.Name); PlayerSettings.bundleVersion = BamVersion.Name; }

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (id != BamInfo.PackageName) { log.Change("Android package name", id, BamInfo.PackageName); PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BamInfo.PackageName); }
        }

        static void AndroidSettings(Log log)
        {
            var t = NamedBuildTarget.Android;
            var minSdk = AndroidSdkVersions.AndroidApiLevel26;
            var targetSdk = (AndroidSdkVersions)36;

            if (PlayerSettings.Android.minSdkVersion != minSdk) { log.Change("Android min API", PlayerSettings.Android.minSdkVersion, minSdk); PlayerSettings.Android.minSdkVersion = minSdk; }
            if (PlayerSettings.Android.targetSdkVersion != targetSdk) { log.Change("Android target API", PlayerSettings.Android.targetSdkVersion, targetSdk); PlayerSettings.Android.targetSdkVersion = targetSdk; }

            if (PlayerSettings.GetScriptingBackend(t) != ScriptingImplementation.IL2CPP) { log.Change("Scripting backend", PlayerSettings.GetScriptingBackend(t), ScriptingImplementation.IL2CPP); PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP); }
            if (PlayerSettings.GetIl2CppCompilerConfiguration(t) != Il2CppCompilerConfiguration.Release) { log.Change("IL2CPP configuration", PlayerSettings.GetIl2CppCompilerConfiguration(t), Il2CppCompilerConfiguration.Release); PlayerSettings.SetIl2CppCompilerConfiguration(t, Il2CppCompilerConfiguration.Release); }
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) { log.Change("Android architectures", PlayerSettings.Android.targetArchitectures, AndroidArchitecture.ARM64); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; }
            if (PlayerSettings.Android.buildApkPerCpuArchitecture) { log.Change("Split APK per architecture", true, false); PlayerSettings.Android.buildApkPerCpuArchitecture = false; }

            // Low keeps everything our code touches; link.xml in Assets/BAM/Settings guards the BAM assemblies anyway.
            if (PlayerSettings.GetManagedStrippingLevel(t) != ManagedStrippingLevel.Low) { log.Change("Managed stripping", PlayerSettings.GetManagedStrippingLevel(t), ManagedStrippingLevel.Low); PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Low); }
            if (!PlayerSettings.stripEngineCode) { log.Change("Strip engine code", false, true); PlayerSettings.stripEngineCode = true; }

            if (!EditorUserBuildSettings.buildAppBundle) { log.Change("Build App Bundle (AAB)", false, true); EditorUserBuildSettings.buildAppBundle = true; }
            if (EditorUserBuildSettings.exportAsGoogleAndroidProject) { log.Change("Export Android project", true, false); EditorUserBuildSettings.exportAsGoogleAndroidProject = false; }

            if (!PlayerSettings.Android.optimizedFramePacing) { log.Change("Optimized frame pacing", false, true); PlayerSettings.Android.optimizedFramePacing = true; }

            // Auto: Unity adds INTERNET only when the code or a package actually uses the network (analytics, later).
            if (PlayerSettings.Android.forceInternetPermission) { log.Change("Force Internet permission", true, false); PlayerSettings.Android.forceInternetPermission = false; }
            if (PlayerSettings.Android.forceSDCardPermission) { log.Change("Force SD card permission", true, false); PlayerSettings.Android.forceSDCardPermission = false; }

            var apis = new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 };
            var current = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || !Same(current, apis))
            {
                log.Change("Android graphics APIs", PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ? "Auto" : string.Join(", ", current), "Vulkan, OpenGLES3");
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis);
            }
        }

        static void Orientation(Log log)
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait) { log.Change("Orientation", PlayerSettings.defaultInterfaceOrientation, UIOrientation.Portrait); PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait; }
            // Only read when the orientation is AutoRotation, but kept consistent so nobody is surprised by a later switch.
            if (PlayerSettings.allowedAutorotateToPortraitUpsideDown) { log.Change("Autorotate upside down", true, false); PlayerSettings.allowedAutorotateToPortraitUpsideDown = false; }
            if (PlayerSettings.allowedAutorotateToLandscapeLeft) { log.Change("Autorotate landscape left", true, false); PlayerSettings.allowedAutorotateToLandscapeLeft = false; }
            if (PlayerSettings.allowedAutorotateToLandscapeRight) { log.Change("Autorotate landscape right", true, false); PlayerSettings.allowedAutorotateToLandscapeRight = false; }
        }

        static void Scene(Log log)
        {
            if (!File.Exists(Path.Combine(BamVersion.ProjectRoot, MainScene)))
            {
                BamQuality.EnsureFolder(Path.GetDirectoryName(MainScene).Replace('\\', '/'));
                // Unity cannot add an empty scene next to an unsaved one, so the open scene is replaced; ask first.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.Warn(MainScene + " not created: the open scene was not saved. Run Setup Project again.");
                    return;
                }
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                bool saved = EditorSceneManager.SaveScene(scene, MainScene);
                if (!saved) { log.Warn("Could not save " + MainScene); return; }
                AssetDatabase.ImportAsset(MainScene);
                log.Change("Created", "", MainScene);
            }

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length != 1 || scenes[0].path != MainScene || !scenes[0].enabled)
            {
                var names = new List<string>();
                foreach (var s in scenes) names.Add(s.path + (s.enabled ? "" : " (off)"));
                log.Change("Scenes in build", string.Join(", ", names), MainScene);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
            }
        }

        static void Icons(Log log)
        {
            var fg = LoadIcon(IconForeground, true);
            var bg = LoadIcon(IconBackground, false);
            var legacy = LoadIcon(IconLegacy, false);
            if (fg == null || bg == null || legacy == null)
            {
                log.Warn("App icon not set: " + BrandingFolder + " needs icon_foreground.png, icon_background.png and icon_legacy.png (the Play work makes them). The build gets Unity's default icon until then.");
                return;
            }

            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android);
            if (kinds == null || kinds.Length == 0)
            {
                log.Warn("Unity reports no Android icon types: is Android Build Support installed in Unity Hub?");
                return;
            }

            foreach (var kind in kinds)
            {
                string k = kind.ToString().ToLowerInvariant();
                bool adaptive = k.Contains("adaptive");
                if (!adaptive && !k.Contains("round") && !k.Contains("legacy")) continue;

                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool changed = false;
                foreach (var icon in icons)
                {
                    // Adaptive icons: layer 0 is the background, layer 1 the foreground.
                    if (adaptive && icon.maxLayerCount >= 2)
                    {
                        if (icon.GetTexture(0) != bg || icon.GetTexture(1) != fg) { icon.SetTextures(bg, fg); changed = true; }
                    }
                    else if (icon.GetTexture(0) != legacy)
                    {
                        icon.SetTexture(legacy, 0);
                        changed = true;
                    }
                }
                if (changed)
                {
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
                    log.Change("Android " + kind + " icons", "", adaptive ? "icon_background + icon_foreground" : "icon_legacy");
                }
            }

            var def = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (def == null || def.Length == 0 || def[0] != legacy)
            {
                log.Change("Default icon", "", "icon_legacy");
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
            }
        }

        // Icons need full quality and Unity's sizes are made from these, so no compression and no mip maps.
        static Texture2D LoadIcon(string path, bool transparent)
        {
            if (!File.Exists(Path.Combine(BamVersion.ProjectRoot, path))) return null;
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                bool dirty = false;
                if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (imp.alphaIsTransparency != transparent) { imp.alphaIsTransparency = transparent; dirty = true; }
                if (dirty)
                {
                    imp.SaveAndReimport();
                    Debug.Log("[BAM.Build] Import settings fixed for " + path);
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void RemoveTemplateSamples(Log log)
        {
            var present = new List<string>();
            foreach (var p in TemplateSamples)
                if (AssetDatabase.IsValidFolder(p) || File.Exists(Path.Combine(BamVersion.ProjectRoot, p))) present.Add(p);
            if (present.Count == 0) return;

            // Never remove something the game still uses.
            var keep = new HashSet<string>(AssetDatabase.GetDependencies(KeepRoots(), true));
            present.RemoveAll(p => keep.Contains(p));
            if (present.Count == 0) return;

            if (EditorPrefs.GetBool(KeepSamplesPref, false))
            {
                Debug.Log("[BAM.Build] Template sample content kept (you chose to keep it earlier): " + string.Join(", ", present));
                return;
            }

            bool remove = EditorUtility.DisplayDialog(
                "Ta bort mallens exempel?",
                "Unity-mallen har med exempel som spelet inte använder:\n\n" + string.Join("\n", present) +
                "\n\nVill du ta bort dem? De hamnar i papperskorgen, så du kan få tillbaka dem.",
                "Ta bort", "Behåll");
            if (!remove)
            {
                EditorPrefs.SetBool(KeepSamplesPref, true);
                Debug.Log("[BAM.Build] Template sample content kept; Setup Project will not ask again on this PC");
                return;
            }

            var failed = new List<string>();
            if (!AssetDatabase.MoveAssetsToTrash(present.ToArray(), failed))
                foreach (var f in failed) log.Warn("Could not remove " + f);
            foreach (var p in present)
                if (!failed.Contains(p)) log.Change("Removed (to the recycle bin)", p, "");

            // An empty Assets/Scenes folder is template leftover too.
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
            {
                AssetDatabase.MoveAssetToTrash("Assets/Scenes");
                log.Change("Removed (to the recycle bin)", "Assets/Scenes", "");
            }
        }

        static string[] KeepRoots()
        {
            var roots = new List<string> { MainScene, BamQuality.LowAsset, BamQuality.HighAsset };
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings"))
                roots.Add(AssetDatabase.GUIDToAssetPath(guid));
            roots.RemoveAll(p => !File.Exists(Path.Combine(BamVersion.ProjectRoot, p)));
            return roots.ToArray();
        }

        static bool Same(GraphicsDeviceType[] a, GraphicsDeviceType[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
