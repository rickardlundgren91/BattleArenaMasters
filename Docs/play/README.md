# Google Play material for BAM

> Not legal advice. Everything here describes the build as of 2026-10-25 and must be checked again whenever the game
> starts collecting something new, adds an SDK, or goes online.

| Thing | Value / file |
|---|---|
| Privacy policy URL | **https://battlearenamasters.com/privacy** (Cloudflare Pages, site files in `/mnt/project-files/bam-site/`) |
| Contact email | support@battlearenamasters.com (forwards to Rickard) |
| Website | https://battlearenamasters.com |
| Developer name | BAM Games |
| Privacy policy text | `privacy-policy.md` |
| Consent dialog and Settings texts | `consent-text.md` |
| Data safety answers | `data-safety.md` |
| Content rating + target audience | `content-rating.md` (13+) |
| Store texts + release notes | `store-listing.md` |
| Store icon / feature graphic | `store-icon-512.png`, `feature-graphic-1024x500.png` |
| App icon layers | `Assets/BAM/Branding/icon_foreground.png`, `icon_background.png`, `icon_legacy.png` |
| Regenerate all images | `python3 Docs/play/branding/make_branding_logo.py` (from Rickard's logo `branding/bam-logo.png`) |
| Play Console steps (Swedish) | `play-console-sv.md` |
| Unity Cloud + Analytics steps (Swedish) | `unity-analytics-sv.md` |
| Rights register | `Docs/LICENSES.md`; in-game text `Assets/BAM/Resources/BAM/Licenses.txt` |

What is collected (keep these four in sync: `BamAnalytics.cs`, `privacy-policy.md`, `data-safety.md`, Play Console):
only with consent, Unity Analytics with three custom events (`matchStart`, `matchEnd`, `tutorialStep`) plus Unity's
automatic events (session start/stop, first start, device info), the Unity installation ID, and approximate location
derived from the IP address. No ads, no advertising ID, no accounts, no personal info.
