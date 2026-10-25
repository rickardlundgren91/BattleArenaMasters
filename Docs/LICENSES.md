# Rights register

> Not legal advice. This lists what is in the game and why we may use it. Update it in the same change that adds a
> file, a package or an SDK. The text players see is `Assets/BAM/Resources/BAM/Licenses.txt` (Settings > Licenses).

Rule (Rickard, 2026-10-25): only assets made in code by us, or under OFL/CC0 with the license recorded here. Nothing
binary is copied from the old game (no models, Mixamo animations, textures, images or music).

## Ours

| What | Where | Made by | Rights |
|---|---|---|---|
| All game code | `Assets/BAM/**/*.cs` | The BAM project (Rickard, with Claude) | Ours. Ported rules and texts from the old game are Rickard's own code. |
| 3D look (heroes, arena, minions, towers, effects) | Built at runtime by `Assets/BAM/Art` | Code | Ours, no external models or textures. |
| Sound effects | Synthesized at runtime by `Assets/BAM/Audio` | Code | Ours, no recorded samples. |
| App icon layers, legacy icon | `Assets/BAM/Branding/icon_*.png` | `Docs/play/branding/make_branding.py` | Ours; uses Lilita One (OFL, see below). |
| Store icon, feature graphic | `Docs/play/store-icon-512.png`, `Docs/play/feature-graphic-1024x500.png` | Same script | Ours; uses Lilita One. |
| Store texts, privacy policy | `Docs/play/` | The BAM project | Ours. |
| Hero names and texts | `Assets/BAM/Sim/HeroCatalog.cs` | Rickard (from the old game) | Ours. |

## Third party

| What | Where | Author | License | Obligations |
|---|---|---|---|---|
| Lilita One font | `Assets/BAM/Fonts/LilitaOne.ttf` (UI) and baked into the icon images | Juan Montoreano, 2011, Reserved Font Name "Lilita" | SIL Open Font License 1.1 (`Assets/BAM/Fonts/OFL-LilitaOne.txt`, from Google Fonts) | Ship the copyright notice and license with the game (done in `Licenses.txt`). Do not sell the font by itself. Do not modify the font file and keep the name "Lilita" (we use it unmodified). Using it in images such as the icon is allowed. |
| Unity engine and packages (URP, Input System, uGUI, TextMeshPro) | Engine and `Packages/manifest.json` | Unity Technologies | Unity Terms of Service / Unity Companion License | Unity Personal is free under USD 200k revenue and funding in the last 12 months. No in-game notice needed; the splash screen is optional in Unity 6. |
| Unity Analytics SDK (`com.unity.services.analytics`) | Package | Unity Technologies | Unity Terms of Service, Unity Gaming Services terms | Runs only after consent. Disclosed in the privacy policy and Data safety. |

## Not used (on purpose)
Mixamo, Meshy or AI-generated models and textures, Asset Store assets, the old game's images, music and textures.
Adding any of them later needs a row here with proof of the right to use it.
