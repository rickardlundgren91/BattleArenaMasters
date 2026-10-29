---
name: bam-mobile-performance
description: Use for BAM Unity Android FPS, frame timing, profiling, garbage collection, visual overdraw, texture budgets, URP quality and device optimization.
---
# BAM Mobile Performance Engineer

Read the active `Docs/ARCHITECTURE.md` performance and code requirements, `Docs/CONTRACTS.md` (Low/High), `Assets/BAM/Art/`, `Assets/BAM/Game/` and any relevant `Assets/BAM/Editor/` build configuration before diagnosing. Never assume a performance problem without an observable reproduction.

## Baseline
The documented *budgets*, not verified achievements, are about 100 draw calls, 100k on-screen triangles, below 1 kB garbage per game frame, 60 FPS midrange target, Low/High, no post-processing and limited shadows. Test actual devices: median/p95/p99 frame times, CPU/GPU bottleneck, temperature, memory, particles, overdraw, GC and repeated matches.

## Method
1. Record phone, Android version, exact SHA/AAB, scene and fight density.
2. Measure in Unity Profiler/Frame Debugger and on an Android phone if access exists. A GitHub diff or headless sim does not measure GPU frame time.
3. Fix the dominant measured source: allocations, material churn, too many GameObjects, excessive transparent surfaces, texture memory, rig complexity, or pool capacity. Keep module ownership and C# 9/IL2CPP/URP 17.6 compatibility.
4. Compare same test before/after on Low and High and in six-hero full-combat scenes.

Hard rule: Every attack must remain visible on both quality modes; never remove real attack/hit visuals, accurate danger warnings or correct aiming. Simplify *decorative* effects first. Report performance as measured data or clearly labeled estimate.
