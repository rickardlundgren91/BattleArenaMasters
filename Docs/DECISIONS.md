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
  ("like Popol and Kupa in Mobile Legends"), homes on an enemy hero in sight and pounces (bite + short root); if no enemy
  hero is near it bites minions. Walls stop it: it walks round them, a throw ends at the first wall and its bite needs a
  clear line like everyone's (Rickard 2026-10-25 14:50-14:51, after it won 90 % against bots going through walls; this
  replaced the first "spectral, through walls, 7 tiles" version). It still passes through bodies. To be tuned after playtests.
- **Fenn's clone (Phantom Step) works like the old ABC2 hero Omega's illusion:** a full copy with Fenn's health and level
  that runs to the nearest enemy within 5 tiles and fights with his damage for 6 s; every unit can target it; it takes
  double damage; it gives no special charge; it ends when its time is up or Fenn dies. Trigger as before: an enemy hero
  hits Fenn, at most once per 18 s. Open (2026-10-25 15:10): it makes Fenn win 83 % against Hard bots (48 % without it,
  old Fenn 61 %); Rickard is choosing how to weaken it (recommended: a decoy, 3 s, no damage, gone at the first hit = 62 %).

## Screens and feel
- Menus: Start, hero select, Settings. Short guided match as tutorial.
- HUD: only the essentials. On death the camera follows a teammate. Pause exists.
- Settings: sound, graphics Low/High, left-handed controls, analytics consent, privacy policy, licenses.
- English only. Font Lilita One (OFL).
- Graphics: effects, projectiles, zones and UI made in code; heroes, creeps, wolf and arena use Rickard's own old models,
  Mixamo animations and textures (changed 2026-10-25, see ARCHITECTURE rule 4). Every attack is always visible (rule 5).
  Sounds synthesized in code. No music for now. No vibration for now.
- Portrait, camera angled from above. 60 fps target with Low/High graphics.

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
