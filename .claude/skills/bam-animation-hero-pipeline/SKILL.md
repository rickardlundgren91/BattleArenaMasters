---
name: bam-animation-hero-pipeline
description: Use for BAM Tripo/Meshy/Mixamo models, FBX import, characters, rigs, attachments, locomotion and combat animation in Unity.
---
# BAM Hero, Models & Animation Pipeline

Read latest `CLAUDE.md`, `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, `Docs/LICENSES.md`, Art contracts and current `Assets/BAM/Art/` including `Art/Models/`, `Art/Editor/HeroArtImport.cs`, `Art/Editor/AnimationCheck.cs`, `Art/Editor/HeroArtBuildCheck.cs`, `BodyView.cs`, and `AnimFix.cs` as present.

## Pipeline
- Verify source license and individual asset provenance; check the old XYZ registry before copying older assets. Do not assume Tripo or other AI-generated assets are royalty-free or attribution-free.
- Validate a consistent scale at the real 11.5-tile portrait camera, silhouette at phone size, pivot/feet at ground y=0, clean rig, separate hard weapon accessories, correct A-pose and safe import settings for Unity 6.6.
- Check idle, start/stop, movement, strafe, backpedal, facing changes, every attack release, windup and hit, special pose, damage reaction, death. Prevent foot slide, inverted torso/legs, underground characters and weapon separation.
- Keep original simulation attack timing and events authoritative; the animation follows gameplay rather than changing gameplay to fit an animation.
- Distinguish the seven currently approved heroes from characters still only present in XYZ. Do not add heroes or systems to the new rebuild without approval.

## Verification
Produce a specific test matrix for Art Preview, gameplay and Android: hero silhouette, moving while attacking, hit/block/miss, 3v3 visual density, Low and High, FPS, import license and `.meta` integrity. Record exact SHA and mark everything not physically tested as NOT TESTED. Rickard reviews visual gameplay on his own phone before any store upload.
