# Rights register

> Not legal advice. This lists what is in the game and why we may use it. Update it in the same change that adds a
> file, a package or an SDK. The text players see is `Assets/BAM/Resources/BAM/Licenses.txt` (Settings > Licenses).

Rules (Rickard, 2026-10-25): assets made in code by us, or under OFL/CC0 with the license recorded here. Later the same
day Rickard also allowed his own old hero models (made with Tripo or Meshy, rigged on Mixamo), Mixamo animations and his
own arena/tutorial textures; each such file gets a row under "Imported from the old game" with its origin before it ships.

## Ours

| What | Where | Made by | Rights |
|---|---|---|---|
| All game code | `Assets/BAM/**/*.cs` | The BAM project (Rickard, with Claude) | Ours. Ported rules and texts from the old game are Rickard's own code. |
| 3D look built in code (minions, towers, wolf, bushes, rocks, effects, fallback heroes) | `Assets/BAM/Art` | Code | Ours. Hero models and arena textures: see "Imported from the old game". |
| Sound effects | Synthesized at runtime by `Assets/BAM/Audio` | Code | Ours, no recorded samples. |
| Music loop "Byte Strike" | `Assets/BAM/Audio/Resources/BAM/Music/ByteStrikeLoop.wav` (one 14.3 s cycle cut from Rickard's file "Byte Strike (13s Loop).mp3", 2026-10-25) | Rickard (account "TerreTrigger") on **Google Flow Music** (flowmusic.app, Google's Lyria model), **free account** (screenshot 2026-10-26: `bam-work/audio/proof/byte-strike-flowmusic-account-2026-10-26.png`). Corrects the earlier note that it was flow-music.app. | **Rights unclear, kept by Rickard's decision (2026-10-26).** Google's terms do not claim ownership of generated content; commercial use is described for the Flow Music tiers that come with Google AI Plus/Pro/Ultra, not clearly for a free account. AI-only output is likely not protected by copyright (others could reuse it; does not stop us using it). Google faces a lawsuit over Lyria's training data (Kogan v. Google LLC), aimed at Google, not users. The output may carry a SynthID watermark. Re-check before the production release (Google AI Plus or higher when it ships, or a replacement track). Not legal advice. |
| BAM logo (source) | `Docs/play/branding/bam-logo.png` | Rickard, 2026-10-25 (AI image generator, see note) | Rickard's; tool to be confirmed (GPT Image / Nano Banana allow commercial use). |
| App icon layers, legacy icon, store icon, feature graphic | `Assets/BAM/Branding/icon_*.png`, `Docs/play/store-icon-512.png`, `Docs/play/feature-graphic-1024x500.png` | `Docs/play/branding/make_branding_logo.py` from the logo above | Rickard's logo. (The older code-drawn look, `make_branding.py`, used Lilita One and is no longer used.) |
| Store texts, privacy policy | `Docs/play/` | The BAM project | Ours. |
| Hero names and texts | `Assets/BAM/Sim/HeroCatalog.cs` | Rickard (from the old game) | Ours. |

## Third party

| What | Where | Author | License | Obligations |
|---|---|---|---|---|
| Lilita One font | `Assets/BAM/UI/Resources/BAM/Fonts/LilitaOne.ttf` (all UI text; the old code-drawn icons used it) | Juan Montoreano, 2011, Reserved Font Name "Lilita" | SIL Open Font License 1.1 (`Assets/BAM/Fonts/OFL-LilitaOne.txt`, from Google Fonts) | Ship the copyright notice and license with the game (done in `Licenses.txt`). Do not sell the font by itself. Do not modify the font file and keep the name "Lilita" (we use it unmodified). Using it in images such as the icon is allowed. |
| Unity engine and packages (URP, Input System, uGUI, TextMeshPro) | Engine and `Packages/manifest.json` | Unity Technologies | Unity Terms of Service / Unity Companion License | Unity Personal is free under USD 200k revenue and funding in the last 12 months. No in-game notice needed; the splash screen is optional in Unity 6. |
| Unity Analytics SDK (`com.unity.services.analytics`) | Package | Unity Technologies | Unity Terms of Service, Unity Gaming Services terms | Runs only after consent. Disclosed in the privacy policy and Data safety. |

## Imported from the old game

Filled in from the graphics thread's import list. Mixamo (Adobe) animations and auto-rigging are royalty free in games
and need no credit. A model from Tripo's free plan is CC BY 4.0 and needs a credit line in `Licenses.txt`; Meshy and
paid Tripo plans give the owner the rights under their terms (keep proof of the plan).

Source: graphics thread hand-over 2 (`bam-work/art/HANDOVER-2.md`), byte-identical copies from the old game repo
(xyz @ d231695). Paths under `Assets/BAM/Art/`.

| Files | What | Origin | Made by | Rights / proof | Credit |
|---|---|---|---|---|---|
| `Models/{Bram,Fenn,Tyra,Moa,Knut,Tikta,Blast}/<Hero>.fbx` (7) | Hero models, texture embedded | Rickard's own models made with Meshy AI or Tripo AI (old register says Meshy; Rickard says Tripo), auto-rigged on mixamo.com. Tyra = `Looks/TyraClay`, Knut = Meshy "walrus tank". | Rickard via Meshy/Tripo + Mixamo | Rickard showed (2026-10-25, screenshot) his Tripo account on the paid **Max** plan: outputs made on a paid plan are owned by him, no credit required. Rickard confirmed (2026-10-25) all seven were made on the paid plan, so they are his; the credit in `Licenses.txt` is a courtesy. Older models made in Meshy were also on a paid Meshy plan (also owned); the new Knut model is from Tripo. | Courtesy only |
| `Models/<Hero>/Anim/*.fbx` (29: Bram 6, Fenn 6, Knut 6, Tikta 6, Tyra 5) | Animation clips on Rickard's rigs | mixamo.com (Adobe) | Adobe | Mixamo terms: royalty free in games, commercial use OK; no redistribution of the raw files on their own. | Not required (given as courtesy) |
| `Models/Creeps/{Melee,Ranged,Siege}.fbx` (3) and `Models/Creeps/<Kind>_Tex.jpg` (3) | Lane minions (the goblin: one figure, three files) and their colour pictures | A Mixamo character that Rickard downloaded from mixamo.com himself (Rickard 2026-10-25 15:50, "ja" to the graphics thread's question; the old register `LICENSES/assets.csv` rows 642-644 had it as unconfirmed); byte copies of the old `Resources/Mixamo/Creeps/*.fbx`; the pictures are extracted unchanged from the FBX files | Adobe (Mixamo) | Mixamo terms: royalty free in games, commercial use OK; no redistribution of the raw files on their own. | Not required (given as courtesy) |
| `Resources/BAMArt/Arena/T_BAM_{Block,Kerb,Plinth,Water}.jpg`, `T_BAM_Ground_{Cobble,Grass,Moss,Road2}.png` (8) | Arena textures | Rickard's own textures made with GPT Image (OpenAI) and Nano Banana (Google Gemini), 2026-10-18 (Rickard, 2026-10-25) | Rickard | Rickard holds the rights. OpenAI's terms assign the output to the user; Google's generative AI terms do not claim ownership of generated content (Nano Banana images carry an invisible SynthID watermark). Both allow commercial use. Not legal advice; re-check the terms before a paid launch. | No |
| `Models/<Hero>/<Hero>_Tex.jpg` (7) | The hero models' own colour pictures | Extracted unchanged from the hero FBX files above (Unity did not pick up the embedded picture) | Rickard via Meshy/Tripo | Same as the hero models above. | Courtesy only |
| `Resources/BAMArt/Props/blackbruk_tower.{bytes,jpg}`, `blackbruk_tower_red.jpg`, `fenn_wolf.{bytes,jpg}` (5) | Tower model (blue and red picture) and Fenn's wolf model | Rickard's own Meshy models from his concept art (art/blackbruk), 2026-10-15; old register `LICENSES/assets.csv` rows 11-14 and 33-34; byte copies of the old `AbcModels` files; `blackbruk_tower.bytes` (10,212 to 3,973 triangles) and `fenn_wolf.bytes` (10,285 to 2,476) simplified by us for phones (2026-10-25, originals kept in `bam-work/art/props`) | Rickard via Meshy | Made on Rickard's paid Meshy plan (Rickard 2026-10-25: his Meshy models were made on a paid plan), so customer-owned, no credit required. | Courtesy only |
| `Resources/BAMArt/Arena/T_BAM_ArenaGround_v1_2.png` (1) | The arena floor of the BAM v1.2 map in one picture | Baked offline by the arena look thread (`bam-work/art/ground/bake.py`, a port of the old game's `View3DMapArena.ArenaGround11Paint`) from Rickard's Grass, Road2 and Moss tiles above and his Dirt tile (`T_BAM_Ground_Dirt.png`, old register `LICENSES/assets.csv` row 446, not in the build) | Rickard (tiles), baked by us | Same as the arena textures above. | No |
| `Resources/BAMArt/Props/wallkit_{pillar,straight}.{bytes,jpg}` (4) | Arena wall kit (pillar and straight wall) | Rickard's own Meshy models from his concept art (art/blackbruk), 2026-10-15; old register `LICENSES/assets.csv` rows 65-70; byte copies of the old `AbcModels` files | Rickard via Meshy | Made on Rickard's paid Meshy plan (Rickard 2026-10-25: his Meshy models were made on a paid plan), so customer-owned, no credit required. | Courtesy only |
| `Resources/BAMArt/Fx/tx_fx_{flash_burst,glow_soft,ground_crack,ground_swirl,light_pillar,particles,shards_burst,shockwave,streak,telegraph_ring}.png`, `tx_fx_nova.png` (11) | Effect pictures for attacks and specials (flash and shockwave flipbooks, glow, sparks, ground crack and swirl, light pillar, rays, nova ring) | Rickard's own effect textures from his Speltest project (`ABC2/Resources/AbcVisuals/Tex/`, 2026-10-25; `tx_fx_nova.png` is his `tx_wow_mage_nova2.png`), copied 2026-10-26 at his request ("ta dom texturerna jag gjort som är för attacker och specials") | Rickard | Rickard's own work; he holds the rights. | Not required |
| `Resources/BAMArt/Fx/T_BAM_FxAtlas.png` (1) | 16 white effect sprites (glow, star, streak, flame, smoke, shockwave, slash, drop, bubble, wisp, chunk, leaf, dust, crack, burst, dot), used where Rickard's pictures have no match | Painted by code for this project, offline (`bam-work/art/fx/make_atlas.py`, 2026-10-26) | Us (Claude for Rickard) | Ours. | Not required |
| `Resources/BAMArt/Props/bam_boundary_wall.{bytes,jpg}` (2) | The low wall round the arena's edge | Rickard's own Meshy models from his concept art (art/blackbruk), 2026-10-15; old register `LICENSES/assets.csv` rows 2-7; copied from Rickard's Speltest project (pixel and mesh identical to the old repo's `AbcModels` files); `bam_boundary_wall.bytes` (1,399 to 300) simplified by us for phones (2026-10-25, originals kept in `bam-work/art/props`); the Meshy bush (rows 2-4 of the old register) is no longer shipped: the arena uses the old game's code-built leaves, and its files moved to `bam-work/art/props/unused` (2026-10-25) | Rickard via Meshy | Made on Rickard's paid Meshy plan (Rickard 2026-10-25: his Meshy models were made on a paid plan), so customer-owned, no credit required. | Courtesy only |
| `Resources/BAMArt/Props/{fenn_sword,fenn_shield,bram_hammer_new,knut_anchor,moa_staff,moa_shield,blast_bomb,tikta_wing}.{bytes,jpg}` (16) | The heroes' held weapons and shields (Fenn sword and shield, Bram hammer, Knut anchor, Moa staff and crest, Blast skull bomb, Tikta Needle Wing) | Rickard's own Meshy/Tripo models, 2026-10-16 to 10-24 (old game `Resources/AbcModels`); the `.bytes` are the old game's own lighter `_lo` copies, the pictures scaled down to 512x512 by us for phones (2026-10-26) | Rickard via Meshy/Tripo | Made on Rickard's paid Meshy/Tripo plans (Rickard 2026-10-25), so customer-owned, no credit required. | Courtesy only |
| `Resources/BAMArt/Arena/T_BAM_Pillar_C.png` (1) | Painted mossy boulder (the arena's rocks) | Rickard's own picture made with an image generator, 2026-10-17/18; old register `LICENSES/assets.csv` row 459; copied from Rickard's Speltest project (pixel identical to the old repo's file) | Rickard | Same as the arena textures above. | No |
| `Resources/BAMArt/Island/tut_{disc,cliff_disc,cliff_lane}.png`, `tut_{stone,moss,bridge}.jpg` (6) | Training island textures (disc, cliffs, stone, moss, bridge) | Rickard's own Gemini textures, cropped and masked, 2026-10-18; old register `LICENSES/assets.csv` rows 95-100 ("tutorial-island"); decoded byte for byte from the old `AbcTutorial/*.bytes` | Rickard via Google Gemini | Rickard allowed his own tutorial textures (2026-10-25). Google's generative AI terms do not claim ownership of generated content and allow commercial use. Not legal advice; re-check the terms before a paid launch. | No |

Skipped by the graphics thread on purpose (unclear rights): the old Barbarian-mesh clips (`Mixamo/Anim/Idle|Walk|Run|Slash`),
Simon's TyraDemon, the Asset Store shrub, the original game's visuals and props.

## Not used
Asset Store assets, other people's models, textures, images or music (Rickard's own FlowMusic loop above is the one exception). Adding any needs a row here with proof first.

### Menu pictures (menus thread, 2026-10-25)

Copied byte-identical from Rickard's old project `Speltest/Assets/ABC2/Resources/AbcUI/` into `Assets/BAM/UI/Resources/BAM/Menu/`.

| Files | What | Origin | Made by | Rights / proof | Credit |
|---|---|---|---|---|---|
| `home_bg.jpg` (was `home_bg_soft.jpg`) | Home background painting | Old game Home | Rickard ("bilden är min egen", 2026-10-25) | Rickard's own | None |
| `portrait_<hero>.png` (7) | Hero portraits on the Heroes cards and the Home top bar | Old game `profile_<hero>.png` | Renders of Rickard's own hero models | Rickard's own (he confirmed 2026-10-25 15:07) | None |
| `menu_btn_orange.png` | The orange PLAY button face on Home | Old game `AbcUI/menu_btn_orange.png` (PlayButton) | Rickard's own UI art from the old project | Rickard's own | None |
| `special_<hero>.png`, `passive_<hero>.png` (14) | Ability icons on the Heroes page | Old game `ui_abilities_<hero>_*.png` | Rickard / the old project | Rickard's own (he confirmed 2026-10-25 15:14) | None |

### Match HUD pictures (match thread, 2026-10-26)

Copied byte-identical from Rickard's old project `Speltest/Assets/ABC2/Resources/AbcUI/` into `Assets/BAM/Game/Resources/BAM/Hud/`.

| Files | What | Origin | Made by | Rights / proof | Credit |
|---|---|---|---|---|---|
| `ui_ingameView_joystick.png`, `ui_ingameView_joystick_base.png`, `ui_ingameView_specialButton_fill.png` (3) | The joystick and the SPECIAL button's charge ring in a match | Old game `ui_ingameView_*.png` | Rickard / the old project | Rickard's own (he confirmed on a decision card 2026-10-26 13:05: "Ja, jag äger dem") | None |
