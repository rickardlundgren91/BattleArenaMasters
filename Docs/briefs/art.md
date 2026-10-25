# Brief: the game's graphics, made in code ("Gör spelets grafik i kod")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md` (Art section) and
`Docs/DECISIONS.md` in `/mnt/project-files/bam-unity/` first. Your folders: `Assets/BAM/Art/` (BAM.Art) and
`Assets/BAM/Art/Editor/` (make an editor-only asmdef BAM.Art.Editor if you need editor code). Effort: medium.

## Goal
Implement `Assets/BAM/Art/ArtKit.cs` (keep every public signature of the stub) so the game looks good, bright and
readable on a phone in portrait, with no imported models or textures: every mesh is built in code from simple shapes
(boxes, cylinders, spheres, capsules, cones, extrusions), with flat or vertex colours and team colours. 3D models come
later, so keep the views swappable behind the same API.

## Deliverables
1. Arena from `ArenaInfo`: ground, raised walls, grass/bushes (units inside are hidden, so bushes must read as cover),
   the middle rocks, a void edge, tower bases; combined into few meshes.
2. Seven heroes with distinct silhouettes and one signature prop each (see CONTRACTS.md and
   `/mnt/project-files/bam-ref/xyz/design/BAM_HERO_IDENTITY_BIBLE_v1.0.md` for who they are), simple procedural
   animation (idle bob, walk cycle, attack swing/throw timed by `windup`, cast, hit flash, death), team ring.
   Fenn's wolf is spectral (ghostly, see-through glow). Fenn's clone is a normal Fenn.
3. Minions (melee, ranged, siege), towers, every `ProjKind`, `ZoneKind` and `FxKind`, the aim view, health bars with
   level, status visuals (stun, slow, root, burn, poison, shield, haste).
4. Materials that survive an Android build: no `Shader.Find` at runtime unless the shader is certain to be in the build.
   A good way: an editor script that runs by itself in Rickard's Unity (`[InitializeOnLoad]`) and creates the few
   material assets you need under `Assets/BAM/Art/Resources/...`, loaded at runtime with `Resources.Load`. URP Lit or
   Simple Lit; a custom shader only if you are sure it compiles for URP 17 on mobile.
5. Low/High quality (`ApplyQuality`): Low = no shadows, fewer particles, simpler effects.
6. Pools for everything that comes and goes; zero garbage per frame; `Release` returns views to their pool.
7. **BAM > Art Preview** editor menu that builds a scene with every hero (both teams), minion, tower, projectile, zone
   and effect lined up on a piece of arena, so Rickard can look at the graphics before the game layer is ready.

## Notes
- Performance budget in ARCHITECTURE.md (about 100 draw calls, 100k triangles, mid-range Android, 60 fps).
- Compile with `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py --only BAM.Art`, then the full check.
- You cannot run Unity: build meshes in code you can reason about (unit sizes in CONTRACTS.md), and describe to the
  main thread what Rickard should check in the preview. Ask Rickard directly in your thread if a look is a matter of
  taste (show him a few options with the preview).
