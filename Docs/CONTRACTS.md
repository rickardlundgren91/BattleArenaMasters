# Contracts between the modules

The stub files are the source of truth for signatures: `Assets/BAM/Art/ArtKit.cs`, `Audio/BamAudio.cs`,
`UI/Screens.cs`, `Analytics/BamAnalytics.cs`, `Core/*.cs`, `Sim/Ids.cs`, `Sim/HeroCatalog.cs`. This file says what the
calls mean, when they happen and what the caller expects. All calls happen on Unity's main thread.

## Shared facts

- **Teams:** 0 = the player's team (blue, bottom of the screen), 1 = the enemy (red, top). `BAM.Sim.Teams`.
- **World space:** 1 tile = 1 Unity unit, y up, map in the xz plane, 15 x 35 tiles. The player's tower is near small z,
  the enemy's near large z. Tile (tx, ty) with ty counted from the top row has its centre at
  `(tx + 0.5, 0, Height - ty - 0.5)` (`ArenaInfo.TileCenter`).
- **Camera:** portrait, looking down at about 55 degrees, following the player's hero, 11.5 tiles across the standing
  frame (`UiKit.PortraitFrame`, the old game's frame: the safe area, or a 9:19.5 picture in the middle of a wider
  window with black edges). Things must read clearly from there on a 6" phone: bold silhouettes, strong team colours.
- **Sizes (radius in tiles):** heroes 0.2-0.4 (Bram 0.3, Fenn 0.4, Tyra 0.25, Moa 0.215, Knut 0.4, Tikta 0.225,
  Blast 0.25), melee/ranged minions about 0.3, siege about 0.45, towers about 1.0, wolf about 0.3. Models may be a bit
  bigger than the radius; heroes stand about 0.9-1.1 units tall.
- **Quality:** `BamSettings.Graphics` Low or High; each module applies it itself when `BamSettings.Changed` fires.
- **Music, vibration, frame rate (2026-10-26, for the old Settings page):** `BamSettings.MusicOn` (BamAudio fades the music
  out and pauses it while off), `BamSettings.Vibration` (gates `BAM.Core.BamHaptics.Pulse(strength)`, strength 0..1 on the
  old Haptics.cs scale: 0.12 a button press, 0.3 a hit on the player, 0.6 its death, 0.8 a kill; 12-45 ms, at most one per
  0.08 s), `BamSettings.Fps` 30 or 60 (Boot applies `Application.targetFrameRate` on every change).

## Battle spells (BAM.Sim, 2026-10-26: old SimSpells.cs, plus Sprint)

- `SpellId` (Ids.cs): None, Blink, Recall, Shield, Haste, Sprint. `SpellDefs.All` (picker order), `SpellDefs.Name/Desc/
  Cooldown(id)`, `SpellDefs.Color(id)` (0xRRGGBB: Blink 8fd8ff, Recall 9fffd0, Shield ffe08a, Haste ff9a6a, Sprint c8ff6a),
  constants `SpellDefs.BlinkDist` 3.5 etc. Blink 25 s, Recall 2 s channel / 15 s (4 s after a cancel), Shield 20 % max HP
  for 3 s / 30 s, Haste +40 % attack speed 5 s / 30 s, Sprint +50 % move speed 4 s / 25 s.
- The pick: `BamSettings.Spell` (Core, PlayerPrefs `bam.spell`, Blink by default, one pick for every hero, the old hero
  page's BATTLE SPELL card). GameFlow passes it as `MatchConfig.PlayerSpell`; bots draw theirs with the seed and use them
  by the old rules. Off in the guided match (`Match.SpellsOn`).
- Cast: `PlayerInput.Spell = true` for one tick, `SpellAngle` (NaN = the way the hero faces; blink only) and `SpellK` 0..1
  (blink distance share, at least 1 tile). Blinks pass walls and water to the farthest free spot; `Match.BlinkPoint(unit,
  ang, k, out x, out y)` gives the landing spot for the aim preview.
- Reading: `unit.Spell`, `Match.SpellReady(unit)`, `Match.SpellLeft(unit)` (seconds), `SpellDefs.Cooldown(unit.Spell)`,
  `Match.SpellChanneling(unit)` + `unit.SpellChanStart/SpellChanEnd` (recall ring). Haste shows as `StatusFlags.Hasted`,
  the spell shield as a normal shield.
- Events: `SpellCast` (A, Kind = SpellId, X/Y = where A stood: a blink's start), `SpellCancel` (A's recall cut),
  `Recall` (A arrived at its spawn; X/Y = where it left).

## HERO TEST (2026-10-26: the old herotest.js, Rickard's training area)

- Start: `Flow.Game.StartMatch(new MatchSetup { Hero = h, HeroTest = true, Level = n })` (Level 1..12, 1 if 0).
  `RestartMatch()` = the bar's Reset (same hero and level); a new hero or level = StartMatch again; Exit =
  `Flow.Game.QuitToMenu()`. No analytics, no result: it never ends.
- `Match.HeroTest` (true in it), `Match.Config.HeroTestLevel`, `Match.HeroTestFastCd` (bool the bar toggles: the special is
  ready again 1 s after use), `Match.HtFoe` (the enemy dummy hero, Bram), `Match.HtAlly` (the friendly dummy, kept at
  40 % health). Practice minions are normal enemy minions. Spells are on. No intro banner, no top portraits, no clock
  (the old Hud hides them in hero test).

## Art (BAM.Art) - called by BAM.Game

| Call | When | Expectation |
|---|---|---|
| `ArtKit.Init()` | once at boot | Builds shared materials/meshes and pools. Must not hitch for more than a few hundred ms. |
| `ArtKit.ApplyQuality(q)` | boot and on change | Low: no shadows, fewer particles, cheaper materials. High: full look. |
| `ArtKit.BuildArena(info)` | match start | Returns one root with the static map: ground, walls (raised blocks), grass/bushes (low, see-through-able), rocks, void edge, tower bases. Few draw calls (combine meshes). Destroyed by the caller at match end. |
| `CreateHero/Minion/Tower/Wolf` | when a unit appears | Returns a pooled `UnitView`. The caller sets position/rotation every frame (feet at y = 0, forward = facing) and calls `Release` when the unit is gone for good (heroes stay all match and are `SetDead` instead). Fenn's clone is `CreateHero(Fenn, team)`; it must look identical to Fenn for the enemy. |
| `UnitView.PlayAttack(windup)` | auto attack starts | Swing/throw so the release lands `windup` s later. Always visible (ARCHITECTURE rule 5): the game calls it for every attack, minions and towers too, and plays a hit effect on every hit. |
| `UnitView.SetStatus(flags)` | when statuses change | Stun stars, slow, root, burn, poison, shield bubble, haste. `Illusion` is only set for the player's own team (a faint marker is fine). |
| `UnitView.SetVisibility(a)` | when it changes | 1 visible; about 0.5 for the player's team in grass; 0 = hidden from the player (hide the bar too). |
| `UnitView.SetHealth/SetLevel` | when they change | World-space bar above the unit, team-coloured; heroes show the level. Must stay readable and face the camera. |
| `CreateProjectile(kind, team)` + `Launch` | a shot is fired | The caller moves it every frame and releases it on impact. `ProjKind` lists what flies; melee kinds may show a short slash or nothing. Knut's anchor gets `SetTether(Knut)`. |
| `CreateZone(kind, team)` + `Show` | honey field, fire lane, bomb fuse | Circle when length = 0, else a strip; fades or ticks over `duration`. |
| `CreateAim()` | once per match | Line (with width) or circle while dragging the special, red when invalid; `ShowRange` for the attack range ring. |
| `PlayFx(kind, pos, size, team)` | events | Fire-and-forget, pooled. See `FxKind`. |

Hero looks (made in code from simple shapes, one clear prop each, team colour on a ring under the feet and on accents):
Bram the Anvil (stocky smith, big hammer he throws), Fenn the Pack Runner (light fighter with a sword; his wolf is
ghostly/spectral and runs through walls), Tyra the Longshot (archer, long bow), Moa the Honey Warden (bee guardian, honey
magic, shields), Knut the Anchor (big sailor tank with an anchor on a chain), Tikta the Needle Wing (small winged
insect-like assassin with needles), Blast the Pyromaniac (goblin bomber, bombs and fire). Minions: melee, ranged
(bow/sling), siege (a small cannon cart). Towers: one per team, sturdy, team-coloured.

## Audio (BAM.Audio) - called by BAM.Game and BAM.UI

`BamAudio.Init()` once at boot. `Play(id)` for UI and the player's own hero, `PlayAt(id, pos)` for the world (falls off
with distance from the point set by `SetListener`, which the game layer moves with the camera focus every frame).
Volume follows `BamSettings.SfxVolume`. Voice limits so that 20 minion hits in one frame do not stack into noise.
`StopAll()` on pause and when leaving a match. All clips synthesized in code at boot (short, punchy, mobile-speaker
friendly, nothing harsh above about 8 kHz, no clipping). See `SfxId` for the list; the game layer maps events to ids.
Music: `PlayMusic(MusicId.Match)` when a match starts and after a resume (it goes on where it was), `StopMusic()` at the
result screen so the victory or defeat jingle plays alone, then `PlayMusic(MusicId.Menu)` 2.5 s later; `Screens.ShowMainMenu`
plays `MusicId.Menu`; `StopAll()` also fades it out. Menu plays `Resources/BAM/Music/Menu` if it exists, else the match loop
(`Resources/BAM/Music/ByteStrikeLoop`), and the same clip goes on without a restart between menu and match. A recorded clip
at `Resources/BAM/Sfx/<SfxId name>` (.wav/.mp3/.ogg) replaces that synth sound, levelled at load.

## UI (BAM.UI) - called by BAM.Game; calls Core

- Boot order: `Screens.Init()`, then `Screens.ShowConsentIfNeeded(done)`, then `ShowMainMenu()` in `done`.
- Layout copied from the old game's Hub (Rickard 2026-10-25). Home: the chosen hero on a platform, a top bar, "<Hero>
  selected", MODES and a big orange PLAY, and a bottom bar HOME / HEROES / SETTINGS (only outside a match). PLAY calls
  `Flow.Game.StartMatch` at once with `BamSettings.LastHero/LastDifficulty`. The very first start goes into the guided
  match by itself (Boot -> `GameFlow.StartFirstTutorial`, a picture of Home zooms in and fades; Skip in the match), as the
  old MenuNewPlayer did. MODES: Easy/Normal/Hard (saves `LastDifficulty`) and Training (`StartMatch` with `Tutorial = true`,
  Easy). The game sets `BamSettings.TutorialDone` (finished or skipped) and, the first time, `BamSettings.TutorialCard`
  (Home's "Tutorial done · Next: your first match" card; a tap or the first finished bot match clears it) and picks
  Tyra vs Easy unless `BamSettings.PickedBefore`.
- Heroes: a card grid of the seven heroes from `HeroCatalog`; tapping a card saves `BamSettings.LastHero`; the chosen
  hero's texts and 1-3 bars sit under the grid. No Start button there.
- Settings: sound volume, graphics Low/High, left-handed controls, share anonymous usage data (on/off, writes
  `BamSettings.AnalyticsConsent`), privacy policy (opens `BamInfo.PrivacyPolicyUrl` when set), licenses
  (`Resources/BAM/Licenses`), version (`Application.version`).
- Pause (`ShowPause`/`HidePause`, called by the game layer): Resume -> `Flow.Game.Resume()`, Settings, Quit -> `Flow.Game.QuitToMenu()`.
- Result (`ShowResult(result, match)`, the old HudV3 card): VICTORY/DEFEAT, difficulty · length, then both teams with K/D/A,
  rating 0-10 and MVP (SimScore.cs); PLAY AGAIN -> `Flow.Game.RestartMatch()`, HOME -> `Flow.Game.QuitToMenu()`. The game calls
  `Screens.CountEvents(match)` after every `Match.Step` so the ratings count damage, damage taken, heals and shields.
  `ShowResult(result)` without the match (and the guided match) shows the old small card: portrait, name, K/D/A.
- Hint bubble (`ShowHint`/`HideHint`) for the guided match's island half; the arena half draws its own line, arrows and end
  screen in the HUD (Game/HudTutorial.cs) and shows no result card.
- `UiKit` is the shared look (Lilita One, colours, buttons, panels, safe area). The HUD in BAM.Game uses it.
- `Screens.IsBlockingInput` is true while any menu or dialog is open.
- Android back: outside a match the UI handles it; in a match the game calls `Screens.HandleBack()` and opens pause only
  when it returns false. `ShowResult` plays Victory/Defeat itself. Canvas order: menus 100, hint 60, so the HUD stays below 50.

## Analytics (BAM.Analytics) - called by BAM.Game

Nothing is sent without `BamSettings.AnalyticsConsent == Granted`; turning it off stops collection at once (and asks
the service to delete the data if the SDK supports it). Events: `MatchStarted` (hero, difficulty, tutorial),
`MatchEnded` (won/quit, seconds, kills, deaths, level), `TutorialStep` (index, name). No personal data, no ads, no
cross-app tracking. What is collected must match the Data safety answers in `Docs/play/`.

## Build (BAM.Build.Editor) - menu items in Rickard's Unity

- `BAM > Setup Project`: applies every player setting (see `Docs/DECISIONS.md`: package `com.battlearenamasters.game`,
  label under the icon "BAM", portrait only, min API 26, target API 36, IL2CPP, ARM64 only, AAB), the Low/High quality
  levels, the first scene, the app icon from `Assets/BAM/Branding/`. Safe to run again.
- `BAM > Build AAB (internal test)`: raises the version code, asks for the keystore passwords (never stored), builds
  `Builds/BAM-<version>-<code>.aab`, shows the file.
- The game itself sets `Application.targetFrameRate = 60`.
