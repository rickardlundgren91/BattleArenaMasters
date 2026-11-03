using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAM.Build
{
    /// <summary>
    /// BAM > Build AAB (internal test): the signed App Bundle for Google Play, and BAM > Build test APK for a quick
    /// install on Rickard's own phone. Passwords live only in memory for the length of one build.
    /// </summary>
    public static class BamBuild
    {
        public const string OutputFolder = "Builds";

        [MenuItem("BAM/Build AAB (internal test)", priority = 20)]
        static void BuildAabMenu()
        {
            if (!Prepare() || !OnlineOk()) return;
            BamKeystoreWindow.Open();
        }

        // Online needs the upload key: Google Play Games only signs in apps whose key it knows (upload and app signing), so the
        // debug-signed test APK cannot sign in. This one is a release APK signed with the upload key, installed over USB.
        [MenuItem("BAM/Build signed APK for online test and run on phone (USB)", priority = 23)]
        static void BuildSignedApkMenu()
        {
            if (!Prepare() || !OnlineOk()) return;
            BamKeystoreWindow.Open(true);
        }

        [MenuItem("BAM/Build test APK (own phone)", priority = 21)]
        static void BuildApkMenu()
        {
            if (!Prepare()) return;
            BuildApk(false);
        }

        [MenuItem("BAM/Build test APK and run on phone (USB)", priority = 22)]
        static void BuildApkRunMenu()
        {
            if (!Prepare()) return;
            BuildApk(true);
        }

        // PERF-04: a test APK that records every shader state one match draws and sends the list to this editor at the match's
        // end (ShaderWarm.cs). Play one full match on Low and one on High, phone connected, until the Console says each arrived.
        [MenuItem("BAM/Build shader recording APK and run on phone (USB)", priority = 24)]
        static void BuildTraceApkMenu()
        {
            if (!Prepare()) return;
            BuildApk(true, new[] { "BAM_SHADER_TRACE" });
        }

        [MenuItem("BAM/Open Builds folder", priority = 40)]
        static void OpenFolderMenu()
        {
            string dir = OutputDir();
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir + Path.DirectorySeparatorChar);
        }

        // Everything a build needs before it starts: the Android module, Android as the active platform, and the setup.
        static bool Prepare()
        {
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("BAM", "Unity är upptagen (bygger, kompilerar eller spelar). Vänta och försök igen.", "OK");
                return false;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[BAM.Build] Android Build Support is not installed");
                EditorUtility.DisplayDialog("Android saknas",
                    "Unity har inte stöd för Android installerat.\n\nÖppna Unity Hub > Installs > kugghjulet vid 6000.6.5f1 > Add modules > " +
                    "kryssa i Android Build Support (med OpenJDK och Android SDK & NDK Tools) > Install. Starta sedan om Unity.", "OK");
                return false;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUtility.DisplayDialog("Byt till Android",
                        "Projektet är inställt på en annan plattform. Unity behöver byta till Android först (kan ta några minuter första gången).",
                        "Byt till Android", "Avbryt"))
                    return false;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[BAM.Build] Could not switch to Android");
                    EditorUtility.DisplayDialog("BAM", "Kunde inte byta till Android. Se Console.", "OK");
                    return false;
                }
            }
            var log = BamProjectSetup.Run(false);
            if (log.Warnings > 0) Debug.LogWarning("[BAM.Build] Setup finished with " + log.Warnings + " warnings (see above); building anyway");
            return true;
        }

        // BAM_ONLINE is set by hand in Player Settings on Rickard's PC (27 Oct), not in the checked-in ProjectSettings, so a build
        // from a fresh copy of the project came out without online play and said nothing (bug review 2026-11-02, nr 45). The two
        // builds that need online (the AAB for Google Play and the signed online test APK) ask first.
        static bool OnlineOk()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            string defs = PlayerSettings.GetScriptingDefineSymbols(target) ?? "";
            foreach (var d in defs.Split(';')) if (d.Trim() == "BAM_ONLINE") return true;
            int pick = EditorUtility.DisplayDialogComplex("Onlinespel saknas",
                "Symbolen BAM_ONLINE är inte satt för Android, så appen byggs utan onlinespel (inloggning, vänner, onlinematcher och topplista).\n\n" +
                "Lägg till: Unity kompilerar om en stund. Välj sedan samma bygge i BAM-menyn igen.",
                "Lägg till", "Avbryt", "Bygg utan online");
            if (pick == 0)
            {
                PlayerSettings.SetScriptingDefineSymbols(target, defs.Trim().Length > 0 ? defs.Trim() + ";BAM_ONLINE" : "BAM_ONLINE");
                Debug.Log("[BAM.Build] BAM_ONLINE added to Android's scripting define symbols. Choose the build again after the recompile.");
                return false;
            }
            return pick == 2;
        }

        /// <summary>Signed release AAB with a raised version code. Called by the keystore window.</summary>
        public static void BuildAab(string keystorePath, string keystorePass, string alias, string keyPass)
        {
            int before = BamVersion.Current;
            int code = BamVersion.Raise();
            string file = Path.Combine(OutputDir(), "BAM-" + BamVersion.Name + "-" + code + ".aab");

            bool ok = false;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
                SymbolTableInBundle();

                ok = Run(file, BuildOptions.None, "AAB " + BamVersion.Name + " (" + code + ")");
            }
            finally
            {
                // The keystore stays out of ProjectSettings: nothing about it is left behind for git to pick up.
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                if (!ok) BamVersion.Restore(before);
                AssetDatabase.SaveAssets();
            }

            if (ok)
                EditorUtility.DisplayDialog("AAB klar",
                    "Filen finns här:\n" + file + "\n\nLadda upp den i Play Console > Testa och släpp > Intern testning > Skapa ny version.", "OK");
            else
                EditorUtility.DisplayDialog("Bygget misslyckades",
                    "Titta i Console efter röda rader. Fel lösenord eller alias ger ofta ett fel som nämner 'keystore' eller 'signing'.", "OK");
        }

        /// <summary>Release APK signed with the upload key and run on the phone over USB; the version code is not raised.</summary>
        public static void BuildSignedApk(string keystorePath, string keystorePass, string alias, string keyPass)
        {
            string file = Path.Combine(OutputDir(), "BAM-" + BamVersion.Name + "-signed.apk");
            bool ok = false;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.development = false;
                ok = Run(file, BuildOptions.AutoRunPlayer, "signed APK " + BamVersion.Name);
            }
            finally
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = true;
                AssetDatabase.SaveAssets();
            }
            if (!ok)
                EditorUtility.DisplayDialog("Bygget misslyckades",
                    "Titta i Console efter röda rader. Är telefonen inkopplad med USB-felsökning på? Fel lösenord ger ett fel som nämner 'keystore' eller 'signing'.", "OK");
        }

        // Development APK signed with Unity's debug key: installs on a phone directly, can never go to Play.
        static void BuildApk(bool run, string[] defines = null)
        {
            string file = Path.Combine(OutputDir(), "BAM-" + BamVersion.Name + "-test.apk");
            bool ok;
            try
            {
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                var options = BuildOptions.Development;
                if (run) options |= BuildOptions.AutoRunPlayer;
                ok = Run(file, options, defines != null ? "shader recording APK" : "test APK", defines);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
            }
            if (!ok)
                EditorUtility.DisplayDialog("Bygget misslyckades", "Titta i Console efter röda rader.", "OK");
            else if (!run)
                EditorUtility.DisplayDialog("APK klar", "Filen finns här:\n" + file, "OK");
        }

        /// <summary>
        /// Native debug symbols (symbol table) go inside the AAB, so Play Console can read a native crash (version code 15 had none,
        /// 2026-11-01). Unity 6: UserBuildSettings.DebugSymbols.level / .format in UnityEditor.Android, set by reflection because that
        /// assembly only exists with Android Build Support (and our cloud compile check has no copy of it). Logs what it set; if the
        /// API is missing it says so and the build still runs (then set Build Profiles > Android > Debug Symbols by hand once).
        /// </summary>
        static void SymbolTableInBundle()
        {
            try
            {
                Type ubs = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    ubs = asm.GetType("UnityEditor.Android.UserBuildSettings", false);
                    if (ubs != null) break;
                }
                var ds = ubs?.GetNestedType("DebugSymbols", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var level = ds?.GetProperty("level", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var format = ds?.GetProperty("format", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (level == null || format == null || !level.CanWrite || !format.CanWrite)
                {
                    Debug.LogWarning("[BAM.Build] Debug symbols: UserBuildSettings.DebugSymbols not found; set Build Profiles > Android > Debug Symbols = Symbol Table, Include in bundle by hand");
                    return;
                }
                level.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
                format.SetValue(null, Enum.Parse(format.PropertyType, "IncludeInBundle"));
                Debug.Log("[BAM.Build] Debug symbols: level " + level.GetValue(null) + ", format " + format.GetValue(null));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BAM.Build] Debug symbols could not be set (" + e.Message + "); set Build Profiles > Android > Debug Symbols = Symbol Table, Include in bundle by hand");
            }
        }

        static bool Run(string file, BuildOptions options, string what, string[] defines = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var bpo = new BuildPlayerOptions
            {
                scenes = new[] { BamProjectSetup.MainScene },
                locationPathName = file,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options,
                extraScriptingDefines = defines,
            };

            Debug.Log("[BAM.Build] Building " + what + " -> " + file);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(bpo);
            }
            catch (Exception e)
            {
                Debug.LogError("[BAM.Build] Build threw: " + e);
                return false;
            }

            var s = report.summary;
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError("[BAM.Build] Build " + s.result + " with " + s.totalErrors + " errors after " + Minutes(s.totalTime));
                return false;
            }

            long bytes = File.Exists(file) ? new FileInfo(file).Length : (long)s.totalSize;
            Debug.Log("[BAM.Build] Built " + what + ": " + (bytes / (1024f * 1024f)).ToString("0.0") + " MB in " + Minutes(s.totalTime) + " -> " + file);
            EditorUtility.RevealInFinder(file);
            return true;
        }

        static string Minutes(TimeSpan t) => (int)t.TotalMinutes + " min " + t.Seconds + " s";

        // Outside Assets so Unity never imports the build, inside the project so it is easy to find.
        static string OutputDir() => Path.Combine(BamVersion.ProjectRoot, OutputFolder);
    }

    /// <summary>Asks for the upload key. The path and alias are remembered on this PC; the passwords never are.</summary>
    sealed class BamKeystoreWindow : EditorWindow
    {
        const string PathPref = "BAM.Build.KeystorePath";
        const string AliasPref = "BAM.Build.KeyAlias";

        string keystorePath, alias, keystorePass = "", keyPass = "";
        bool apk; // the signed APK for the own phone instead of the AAB

        public static void Open(bool apk = false)
        {
            var w = CreateInstance<BamKeystoreWindow>();
            w.apk = apk;
            w.titleContent = new GUIContent(apk ? "BAM: bygg signerad APK" : "BAM: bygg AAB");
            w.keystorePath = EditorPrefs.GetString(PathPref, "");
            w.alias = EditorPrefs.GetString(AliasPref, "bam-upload");
            w.minSize = w.maxSize = new Vector2(480, 220);
            w.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(apk ? "Version " + BamVersion.Name + ", APK till telefonen via USB" : "Version " + BamVersion.Name + ", versionskod " + (BamVersion.Current + 1), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            keystorePath = EditorGUILayout.TextField("Keystore-fil", keystorePath);
            if (GUILayout.Button("Välj...", GUILayout.Width(60)))
            {
                string dir = string.IsNullOrEmpty(keystorePath) ? "" : Path.GetDirectoryName(keystorePath);
                string picked = EditorUtility.OpenFilePanel("Välj din upload-keystore", dir, "keystore,jks");
                if (!string.IsNullOrEmpty(picked)) keystorePath = picked;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            keystorePass = EditorGUILayout.PasswordField("Keystore-lösenord", keystorePass);
            alias = EditorGUILayout.TextField("Nyckelns alias", alias);
            keyPass = EditorGUILayout.PasswordField("Nyckelns lösenord", keyPass);
            EditorGUILayout.HelpBox("Lösenorden sparas inte. Är nyckelns lösenord samma som keystorens, skriv samma i båda.", MessageType.None);

            string problem = Problem();
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Avbryt")) Close();
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button(apk ? "Bygg APK och kör" : "Bygg AAB"))
                {
                    EditorPrefs.SetString(PathPref, keystorePath);
                    EditorPrefs.SetString(AliasPref, alias);
                    string p = keystorePath, a = alias, kp = keystorePass, ap = keyPass;
                    keystorePass = keyPass = "";
                    Close();
                    // A build inside OnGUI breaks the GUI layout, so it starts once the window is gone.
                    bool asApk = apk;
                    EditorApplication.delayCall += () => { if (asApk) BamBuild.BuildSignedApk(p, kp, a, ap); else BamBuild.BuildAab(p, kp, a, ap); };
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        string Problem()
        {
            if (string.IsNullOrEmpty(keystorePath)) return "Välj keystore-filen.";
            if (!File.Exists(keystorePath)) return "Hittar ingen fil på den sökvägen.";
            string full = Path.GetFullPath(keystorePath);
            if (full.StartsWith(Path.GetFullPath(BamVersion.ProjectRoot), StringComparison.OrdinalIgnoreCase))
                return "Keystoren ligger i projektmappen. Flytta den utanför (se guiden keystore-sv.md) så den aldrig hamnar på GitHub.";
            if (string.IsNullOrEmpty(keystorePass)) return "Skriv keystore-lösenordet.";
            if (string.IsNullOrEmpty(alias)) return "Skriv nyckelns alias.";
            if (string.IsNullOrEmpty(keyPass)) return "Skriv nyckelns lösenord.";
            return null;
        }
    }
}
