# BAM Unity rebuild: architecture and working rules

Battle Arena Masters (BAM) is Rickard's portrait mobile MOBA: 3v3 against bots, one map, seven heroes. This is a clean
rebuild in Unity 6.6 of the old game (the ABC2 Unity port in `rickardlundgren91/XYZ`), with only what the first
Google Play internal test needs. Every scope decision is in `Docs/DECISIONS.md`.

## Hard rules (Rickard, 2026-10-25)

1. **Nothing goes to GitHub** (no push, no repo, no PR, no gist) until Rickard says so. The game lives on his PC and here.
2. **Ask Rickard** for anything you need from him (a decision, an account, a click in Unity) instead of doing it halfway.
   Write to him in Swedish, short and simple. He is not a programmer; give exact clicks.
3. **Safe for Google Play:** only assets with clear rights (made in code by us, or OFL/CC0 with the license recorded in
   `Docs/LICENSES.md`); no data leaves the phone unless the player said yes (analytics with consent); offline game.
4. Old game assets (Rickard chose "Ja, använd dem" on 2026-10-25): his own hero models (made with Tripo or Meshy, rigged
   on Mixamo), Mixamo animations, and his arena and tutorial-island textures may be used. Check each file in the old
   register (`bam-ref/xyz/LICENSES/ASSETS.md`, `assets.csv`) first: take only files whose origin is Rickard's own (Tripo,
   Meshy, Gemini or another image generator), Mixamo, or made in code; leave out anything marked unknown ("Oklart") and
   Asset Store packs. Every used file gets a line in `Docs/LICENSES.md` (Tripo free-plan models are CC BY 4.0 and need a
   credit line). The old binaries are in `bam-ref/xyz-art/` (from GitHub, read-only). Its code and texts may be ported.
5. **Every attack is always visible** (Rickard, 2026-10-25): hero, minion, tower, wolf and clone attacks, on any target
   (creeps too), on Low and High. The attacker shows its swing or throw, a ranged attack shows its projectile, a melee
   attack shows a slash, and every hit shows a hit effect on the target. Never skip, cull or pool away one of these.

## Where things are

| Place | What |
|---|---|
| `/mnt/project-files/bam-unity/` | The working copy: the Unity project's versioned files. `Assets/BAM/` is all ours. Git history is kept by the main thread only. |
| `/mnt/project-files/bam-tools/` | `setup.sh`, `compilecheck/compilecheck.py`, `metagen.py`, Unity reference DLLs. See its README. |
| `/mnt/project-files/bam-tools/simrunner/` | The headless bot-match runner for the rules (test thread "Testa reglerna med botmatcher"). |
| `/mnt/project-files/bam-ref/oldsim/` | The old game's headless runner, its data dump and baseline results (read-only). |
| `/mnt/project-files/bam-ref/xyz/` | Read-only snapshot of the old game: rules, old sound synth, old consent/analytics, Play documents, licenses, Lilita One. |
| `/mnt/project-files/bam-ref/rickard-project/` | Read-only snapshot of Rickard's new project as created from the template (ProjectSettings, packages, URP assets). |
| `C:\Users\Rickard\BattleArenaMasters` | Rickard's Unity project (Unity 6000.6.5f1, Universal 3D template, URP 17.6, Input System only, uGUI 2.6). **Only the main thread writes there**, after compiling and committing. |

## Assemblies and owners

| Folder (`Assets/BAM/...`) | Assembly | Owner | What |
|---|---|---|---|
| `Sim/` | BAM.Sim (no UnityEngine) | main thread; `Sim/Bots/`: bots thread ("Porta bottarna") | The rules: map, heroes, minions, towers, line of sight, bots. Deterministic, fixed 60 Hz step, seeded RNG, runs headless in .NET 8. Also the shared ids (`Ids.cs`) and hero texts (`HeroCatalog.cs`). |
| `Core/` | BAM.Core | main thread | Settings (`BamSettings`), `MatchSetup`/`MatchResult`, `IGameFlow` + `Flow.Game`, `BamInfo`. Tiny and stable. |
| `Game/` | BAM.Game | match thread ("Bygg matchskärmen"); `Boot.cs` stays as the main thread wrote it | Boot, match driver, input (joystick, special tap/drag), camera, view sync, HUD, guided match. References everything below. |
| `Art/` (+ `Art/Editor/`) | BAM.Art (+ BAM.Art.Editor) | graphics thread | Everything visible in the world, built in code: arena, heroes, wolf, minions, towers, projectiles, zones, effects, aim, health bars, materials. |
| `Audio/` | BAM.Audio | sound thread | Every sound effect, synthesized in code. No music for now. |
| `UI/`, `Fonts/` | BAM.UI | menus thread | Main menu, hero select, Settings, pause, result, consent dialog, licenses screen, hint bubble, and the shared UI kit the HUD uses. |
| `Analytics/`, `Branding/`, `Resources/BAM/Licenses.txt`, `Docs/play/`, `Docs/LICENSES.md` | BAM.Analytics | Play thread | Analytics behind consent, privacy policy, Data safety, content rating, store texts, app icon and feature graphic, licenses. |
| `Editor/Build/`, `Settings/`, `Scenes/`, `Docs/guides/`, `Packages/manifest.json` | BAM.Build.Editor | build thread | Android player settings, quality levels Low/High (URP assets), the Build AAB menu, version codes, keystore guide, package clean-up, the first scene. ProjectSettings change only through its setup script in Rickard's Unity. |
| `Docs/ARCHITECTURE.md`, `CONTRACTS.md`, `DECISIONS.md` | | main thread | Ask the main thread to change them. |

Dependencies point one way: `Sim <- Core <- {Audio, Art, Analytics} <- UI <- Game` (UI uses Audio; Game uses all).
Nothing references BAM.Game. Editor assemblies are editor-only (`includePlatforms: ["Editor"]`).

## How a frame works

`BAM.Game` boots from `[RuntimeInitializeOnLoadMethod]` (the scene is empty), calls `ArtKit.Init`, `BamAudio.Init`,
`Screens.Init`, `BamAnalytics.Init`, sets `Flow.Game`, then shows the consent question (first start) and the main menu.
In a match it steps the Sim at a fixed 60 Hz, reads the Sim's state and events, and drives views (ArtKit), sounds
(BamAudio), the HUD and analytics from them. Art, Audio and UI never read or change the rules.

## Code rules

- C# 9 (Unity's limit), `namespace BAM.<Module>`, English names and comments. Comments say why, not what.
- No per-frame allocations in anything that runs every frame (no LINQ, no `new` lists, no string building, no boxing,
  no closures); pool what comes and goes. No `GameObject.Find`, `SendMessage` or reflection.
- IL2CPP and Android safe: no `System.Reflection.Emit`, no dynamic code, no threads touching Unity objects.
- No `Shader.Find` in a player build unless the shader is guaranteed in the build; prefer material assets loaded from
  `Resources` or created by an editor script that runs by itself in Rickard's Unity.
- Runtime code never uses `UnityEditor` (the player pass of compilecheck fails if it does); editor code goes in an
  editor-only asmdef or behind `#if UNITY_EDITOR`.
- Portrait only. Respect `Screen.safeArea`. Touch first; mouse works in the editor through the Input System.
- Text in the game is English. Talk to Rickard in Swedish.

## Performance budget (mid-range Android, 60 fps)

At most about 100 draw calls and 100k triangles on screen, one directional light, shadows only on High, no
post-processing, no realtime GI. Fewer than 1 kB of garbage per frame in a match. Textures small (the look is
flat-shaded colour, not texture-heavy). Every pool sized for a full 3v3 match (6 heroes, about 30 minions, 2 towers,
wolves, clones, about 40 projectiles).

## Working as a helper thread

1. `bash /mnt/project-files/bam-tools/setup.sh` once.
2. Read this file, `Docs/CONTRACTS.md` and the stub files of your module. The stubs are the contract: keep every public
   signature; add new public API only after telling the main thread.
3. Write only inside your own folders (table above). Re-read a shared file just before you change it (other threads write
   to `/mnt/project-files` too). Scratch work goes outside the project, for example `/mnt/project-files/bam-work/<module>/`.
4. Before you hand over: `python3 /mnt/project-files/bam-tools/metagen.py` then
   `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py` must end with `all assemblies compile`.
5. Hand over with `message_thread` to the main thread ("Rebuild BAM in Unity for Play testing"): what is done, which
   files, what is untested, and anything Rickard must do in Unity. The main thread commits, syncs to Rickard's PC and
   asks him to test. Do not message Rickard about syncing; do ask him anything only he can answer.
6. Nobody can run Unity here. Write code that is easy to verify by reading, log clearly with `Debug.Log("[BAM.<Module>] ...")`
   on errors, and say exactly what Rickard should look at when he tests.
