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
- **Fenn's wolf is spectral:** it moves through walls and units. When thrown (drag) it homes on an enemy hero in range
  and follows it; if no enemy hero is near it bites minions. Throw range about 7 tiles ("fairly long, like Popol and
  Kupa in Mobile Legends"), to be tuned after playtests.
- **Fenn's clone (Phantom Step) works like the old ABC2 hero Omega's illusion:** a full copy with Fenn's health and level
  that runs to the nearest enemy within 5 tiles and fights with his damage for 6 s; every unit can target it; it takes
  double damage; it gives no special charge; it ends when its time is up or Fenn dies. Trigger as before: an enemy hero
  hits Fenn, at most once per 18 s.

## Screens and feel
- Menus: Start, hero select, Settings. Short guided match as tutorial.
- HUD: only the essentials. On death the camera follows a teammate. Pause exists.
- Settings: sound, graphics Low/High, left-handed controls, analytics consent, privacy policy, licenses.
- English only. Font Lilita One (OFL).
- Graphics made in code now (3D models later). Sounds synthesized in code. No music for now. No vibration for now.
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
