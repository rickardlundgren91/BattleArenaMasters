# Brief: Android build and player settings ("Förbered Android-bygget för Google Play")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md` (Build section) and
`Docs/DECISIONS.md` in `/mnt/project-files/bam-unity/` first. Your folders: `Assets/BAM/Editor/Build/` (asmdef
BAM.Build.Editor, editor-only), `Assets/BAM/Settings/`, `Assets/BAM/Scenes/`, `Docs/guides/`, `Packages/manifest.json`.
ProjectSettings are only ever changed by your setup script inside Rickard's Unity, never copied as files. Effort: medium.

## Goal
Rickard opens his Unity project, clicks **BAM > Setup Project** once and **BAM > Build AAB (internal test)** whenever he
wants a build, and gets an AAB that Google Play accepts for internal testing. Change settings through editor code
(PlayerSettings, EditorUserBuildSettings, QualitySettings, AssetDatabase), not by hand-editing YAML, so Unity writes
valid files.

## Deliverables
1. `BamProjectSetup.cs`, menu **BAM/Setup Project** (safe to run again; logs every change):
   - company "Battle Arena Masters", product name "BAM" (the label under the icon), Android application id
     `com.battlearenamasters.game`, bundleVersion "0.1.0", Android version code kept in a small file you own and only
     ever increased.
   - Android: min SDK 26, target SDK 36 (cast the int if the enum has no name for it), IL2CPP, ARM64 only, App Bundle,
     portrait only (no autorotation), optimized frame pacing, sensible graphics APIs (Vulkan + OpenGLES3), managed
     stripping that IL2CPP builds survive (add a `link.xml` if needed), Internet permission only if a package needs it.
   - Quality: exactly two levels, index 0 "Low" and 1 "High", each with its own URP asset you create under
     `Assets/BAM/Settings/` (Low: render scale about 0.8, no shadows, no MSAA, no HDR; High: render scale 1, main-light
     shadows, MSAA 2x, no HDR). No post-processing anywhere. The game picks the level from `BamSettings.Graphics`
     (the main thread calls `QualitySettings.SetQualityLevel`); document the indices in your guide.
   - Scenes: create `Assets/BAM/Scenes/Main.unity` (truly empty: the game builds its own camera and light from code)
     and make it the only scene in the build.
   - App icon: if `Assets/BAM/Branding/` has the icon files (the Play thread makes them; agree names with it through the
     main thread), assign them, adaptive layers included; else warn.
   - Remove the template's sample content (TutorialInfo, Readme, SampleScene and its Settings that nothing uses) only
     after showing Rickard a confirm dialog.
2. `BamBuild.cs`, menu **BAM/Build AAB (internal test)**: checks the Android module is installed, runs the setup,
   raises the version code, asks for keystore and key passwords in a small window (never stored; the keystore path may
   be remembered in EditorPrefs), builds `Builds/BAM-<version>-<code>.aab` outside `Assets`, logs size and time and
   opens the folder. A development-build variant (APK, no keystore) for quick tests on his phone is welcome.
3. `Packages/manifest.json`: remove what the game does not use (for example Visual Scripting, Timeline, AI Navigation,
   Multiplayer Center, Collab), keep URP, Input System, uGUI, the IDE packages and the test framework. Explain each
   removal in your hand-over. `Packages/manifest.json` in the working copy is his current manifest; the main thread
   syncs your edited file to his PC. Read-only snapshots of his current ProjectSettings, packages-lock and the
   template's URP assets are in `/mnt/project-files/bam-ref/rickard-project/`.
4. `Docs/guides/keystore-sv.md` (Swedish, exact clicks): create the upload keystore in Unity's Keystore Manager, where to
   keep it (outside the project and outside any git folder), two backups, never on GitHub, what Play App Signing does at
   the first upload. `Docs/guides/bygga-aab-sv.md`: how to build and where the file ends up.

## Notes
- Compile with `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py` (it has UnityEditor and URP).
- Nobody can run Unity here: make the setup script defensive (check before changing, clear log lines, no exceptions on
  a second run) and tell the main thread exactly what Rickard should click and check.
- Do not touch other folders; the game code reads quality and settings itself.
