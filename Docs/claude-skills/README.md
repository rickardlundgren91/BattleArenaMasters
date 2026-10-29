# BAM Claude Skills v1 — shared from XYZ, adapted for the separate Unity rebuild

**For Rickard / Claude.** Prepared for review on 2026-10-29. Based on the six Skills and Master Skill originally proposed in the old XYZ repo, Draft PR [#614](https://github.com/rickardlundgren91/XYZ/pull/614). This is **not** a byte-for-byte copy: instructions now refer to the *new* BAM Unity code, architecture, constraints and approvals.

## Correct repositories
- **New player game:** `rickardlundgren91/Battle-Arena-Masters` (this private repo). This is the real intended installation target.
- **Old game/testing reference:** `rickardlundgren91/XYZ`. It is not the production BAM rebuild.
- **Website:** `rickardlundgren91/BattleArenaMasters` (public, no game code). This package is intentionally not placed there.

## Package
Six Claude Code project skills under `.claude/skills/*/SKILL.md`:
- `bam-project-architect`
- `bam-vfx-art-director`
- `bam-animation-hero-pipeline`
- `bam-mobile-performance`
- `bam-competitive-qa`
- `bam-highlight-studio`

The combined Master Skill content is in `Docs/claude-skills/BAM_MASTER_SKILL_CLAUDE_AI.md`. It can be packaged as `BAM-Master-Skill-Claude-ai/SKILL.md` for Claude.ai if Rickard's account supports uploading custom Skills. The original binary ZIPs are *not* committed here.

## How Rickard evaluates this
1. Read `CLAUDE.md`, `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md` and the package; confirm any newer instructions.
2. In a **separate checkout of this Draft PR branch** start Claude Code and confirm project Skills are recognized. Do not replace the live Unity project from this branch.
3. Ask Claude to analyze a single art issue (for example, a Tyra special effect) with `/bam-vfx-art-director`. First request **design and evidence only**, not code changes.
4. Test any approved code work via the project's main-thread ownership process and `compilecheck`/`metagen` when applicable. Rickard must verify real Unity and Android visuals before release.
5. Optional future tool: [CoplayDev MCP for Unity](https://github.com/CoplayDev/unity-mcp). It's a separate third-party integration, **not** part of this patch and **not installed**.

## Constraints and current scope
This is the rebuilt Unity project, *not* the old XYZ game. The skills deliberately do not hardcode heroes or scope; they read `HeroCatalog.cs`, `Assets/BAM/Online/` and the current code, because docs can lag. Maintain seeded D1 and LOS, real Tap/Drag mechanics, the approved Fenn behaviors and Rickard's rule that all attacks must remain visible. Follow art, performance, team ownership, licensing and build safety constraints in `Docs/ARCHITECTURE.md`.

This change includes **only text documentation and Skill instructions**. It does not alter C#, assets, scenes, animation, ProjectSettings or the published website. No compilecheck, Unity test, Android test or Skill-run was performed as part of this sharing task. Draft PR is for Rickard and Simon to approve; **do not merge without new explicit authorization**.
