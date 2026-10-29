# Battle Arena Masters (BAM): read this first

BAM is Rickard's portrait mobile MOBA (3v3 against bots or other players online, eight playable heroes; Hilde and Oaken wait as heroes 9 and 10), rebuilt in Unity 6.6 (6000.6.5f1, URP 17.6)
for Google Play. **This repository is the player version.** The rule book is `Docs/ARCHITECTURE.md` (hard rules,
assemblies, owners, hand-overs); scope decisions are in `Docs/DECISIONS.md`, interfaces in `Docs/CONTRACTS.md`, work
orders per area in `Docs/briefs/`, asset rights in `Docs/LICENSES.md`. Read ARCHITECTURE.md before changing anything.

## Where things are

| Place | What |
|---|---|
| `rickardlundgren91/Battle-Arena-Masters` (this repo, private) | Source of truth and history of the game. |
| `C:\Users\Rickard\BattleArenaMasters` | Rickard's Unity project, where the game runs and is built. Only the sync thread ("Synka filerna till din dator") writes there, and only committed states. |
| `rickardlundgren91/XYZ` and `C:\Users\Rickard\Speltest` | The old game (ABC2): Rickard's test bed since 2026-10-26 and the reference for rules, numbers and assets. Never change it from BAM work. |
| `rickardlundgren91/BattleArenaMasters` (public) | The website battlearenamasters.com. Never put game files there. |
| Unity Cloud project "BattleArenaMasters" | The player version's analytics (sent only after the player says yes). The old game uses "Speltest (1)"; keep them apart. |
| `/mnt/project-files/bam-unity/`, `bam-tools/`, `bam-ref/` | Cloud working copy, tools (compile check, metagen) and read-only references, see ARCHITECTURE.md. |

## Rules that are easy to break

1. Never commit a keystore, a password or any other secret. The upload key lives outside the project on Rickard's PC.
2. Rickard tests every build on his own phone before anything is uploaded anywhere (Google Play, the website).
3. Talk to Rickard in Swedish, short and simple, with exact clicks. Ask him instead of doing something halfway.
4. Every change passes `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py` ("all assemblies compile")
   before it is committed. New or removed files under `Assets/BAM` need `python3 /mnt/project-files/bam-tools/metagen.py`.
5. ProjectSettings change only through BAM's setup menu in Rickard's Unity, never by hand from a thread.
6. Game rules that Rickard set: every attack is always visible; Fenn's wolf is thrown 6 tiles and never walks or bites
   through walls; the camera stays as wide as the old game's (11.5 tiles), no further out; the menus keep the old look.
