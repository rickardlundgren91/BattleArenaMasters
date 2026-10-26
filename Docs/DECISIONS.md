# Decisions (Rickard, 2026-10-25)

Rickard answered 36 decision cards; this is the scope of the first Google Play internal test. Ask the main thread
before going beyond it.

## Game
- 3v3 against bots, offline. One map: BAM_OriginalArena_v1_2 (15 x 35 tiles, mirrored, one tower per team, middle rocks).
- Seven heroes: Bram, Fenn, Tyra, Moa, Knut, Tikta, Blast. Numbers as in the old game; balance later.
- Special: tap = automatic target, drag = aim. All passives in.
- Auto attacks: D1 (random valid target among all reachable visible enemies), line of sight (no shots over walls).
- Minions, towers, match rules and grass as in the old game. No buff/core spots, no items.
- Bots: Easy, Normal, Hard.
- Everything else from the old game is out: online, friends, voice, progression, shop, talents, WoW modes, RL.

## Rule changes against the old game
- **Fenn's wolf can be thrown:** tap = it follows Fenn and bites enemies near him; drag = it is thrown up to 6 tiles
  ("like Popol and Kupa in Mobile Legends"), homes on an enemy hero in sight within 1 tile of its flight and pounces (bite +
  short root); a red strip 2 tiles wide warns over the whole flight and the landing; if no enemy hero is near it bites minions. Walls stop it: it walks round them, a throw ends at the first wall and its bite needs a
  clear line like everyone's (Rickard 2026-10-25 14:50-14:51, after it won 90 % against bots going through walls; this
  replaced the first "spectral, through walls, 7 tiles" version). It still passes through bodies. To be tuned after playtests:
  in 1000 bot matches per level (2026-10-25) Fenn wins 58/57/39 % on Hard/Normal/Easy (old 55/54/40); the thrown, homing
  wolf hurts Tikta most (she wins 38 % on Hard, old 44 %; her own rules and bot match the old game, see
  `bam-work/simtest/tikta/REPORT.md`).
- **Fenn's clone is a decoy:** when an enemy hero hits Fenn (at most once per 18 s) a copy with his look, health and level
  appears for 3 s, runs at the nearest enemy within 5 tiles and swings at it, but deals no damage and is gone at the first
  hit it takes; every unit can target it, it gives no special charge, it ends early when Fenn dies (Rickard 2026-10-25,
  card "Klonen blir ett lockbete"). The first version, the old ABC2 hero Omega's illusion (6 s, full damage, double damage
  taken), made Fenn win 83 % against Hard bots; the decoy 58 % Hard, 59 % Normal, 39 % Easy (old Fenn 61, 52, 43 %).

## Screens and feel
- Menus: Start, hero select, Settings. Short guided match as tutorial.
- HUD: only the essentials. On death the camera follows a teammate. Pause exists.
- Settings: sound, graphics Low/High, left-handed controls, analytics consent, privacy policy, licenses.
- English only. Font Lilita One (OFL).
- Graphics: effects, projectiles, zones and UI made in code; heroes, creeps, wolf and arena use Rickard's own old models,
  Mixamo animations and textures (changed 2026-10-25, see ARCHITECTURE rule 4). Every attack is always visible (rule 5).
  Sounds synthesized in code. Music: Rickard's "Byte Strike" loop plays in matches (2026-10-25). On 2026-10-26 Rickard
  decided to keep it in the build although its rights are unclear (made on Google Flow Music, free tier, see LICENSES);
  re-check before the production release. No vibration for now.
- Portrait, camera angled from above. 60 fps target with Low/High graphics.
- Camera zoom exactly as the old game (Rickard 2026-10-25: "rätt zoom på kamera är det viktigaste", "inte mer av
  kamer på map"): 11.5 tiles across the old game's standing frame, the focus 52% down it. The HUD and menus lay out in
  the same frame, so a landscape Game view in the editor looks like the phone with black edges beside it.

## Data
- Analytics WITH consent: a consent dialog at first start, a switch in Settings, a privacy policy, and Data safety
  answers that match what is collected. Nothing is sent before the player says yes.

## Android and Play
- Package name `com.battlearenamasters.game` (permanent after the first upload). Label under the icon "BAM"; store and
  menu name "Battle Arena Masters".
- Min Android 8 (API 26), target API 36 (required for new apps since 2026-08-31). AAB, IL2CPP, ARM64.
- App icon: simple, generated in code.
- Upload key: Rickard creates it himself in Unity's Keystore Manager following our steps; it never goes to GitHub and is
  backed up in two places. AAB built from a menu item in Unity.
- Version history: git in the working copy only. Headless bot matches in the cloud: allowed.
