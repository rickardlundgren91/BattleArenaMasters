# Brief: Google Play requirements ("Gör spelet klart för Google Play: integritet, analytics och butik")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md` (Analytics) and
`Docs/DECISIONS.md` in `/mnt/project-files/bam-unity/` first. Your folders: `Assets/BAM/Analytics/` (BAM.Analytics),
`Assets/BAM/Branding/`, `Assets/BAM/Resources/BAM/Licenses.txt`, `Docs/play/`, `Docs/LICENSES.md`. Effort: medium.

## Goal
Everything Google Play needs for an internal test besides the AAB itself, and analytics that only runs after consent.
The old game already had drafts: `/mnt/project-files/bam-ref/xyz/docs/google-play/` (privacy policy EN/SV, Data
safety worksheet, content rating, store listing, checklist), `LICENSES/`, and its consent/analytics code
(`ABC2/Game/MenuConsent.cs`, `AbcAnalytics.cs`). Reuse what fits; the new game is much smaller (offline, no accounts,
no online, no purchases, no ads, no chat).

## Deliverables
1. **Analytics** in `Assets/BAM/Analytics/BamAnalytics.cs` (keep the stub's public API). Use Unity Gaming Services
   Analytics (`com.unity.services.analytics`, check the version that fits Unity 6.6 and its current consent API).
   Nothing starts before `BamSettings.AnalyticsConsent == Granted`; switching it off stops collection and requests
   deletion if the SDK supports it. Guard the SDK code with a `versionDefines` symbol in `BAM.Analytics.asmdef` so the
   project compiles before the package is installed. Send the manifest line you need to the main thread (the build
   thread owns `Packages/manifest.json`). Ask Rickard, in Swedish with exact clicks, to link the project to Unity Cloud
   when the code is ready.
2. **Consent text**: `Docs/play/consent-text.md` with the short dialog text (title, one or two sentences, Yes/No) and
   the Settings switch label. The menus thread builds the dialog with your text.
3. **Privacy policy** `Docs/play/privacy-policy.md` (English) that matches exactly what is collected. It needs a public
   web address: ask Rickard where to host it (for example Google Sites with his Google account; GitHub Pages only if
   he allows GitHub later). Put the final address in `Docs/play/README.md`; the main thread puts it in `BamInfo`.
4. **Data safety** `Docs/play/data-safety.md`: the exact answers for the Play Console form, matching the analytics.
5. **Content rating and audience** `Docs/play/content-rating.md` (IARC answers) and a target-audience recommendation
   (13+ keeps the game out of the Families policy; explain the choice to Rickard).
6. **Store listing** `Docs/play/store-listing.md`: name "Battle Arena Masters", short and full description, internal
   test release notes. The label under the icon is "BAM".
7. **Branding made in code**: a script (yours, outside the Unity project or in an editor-only asmdef) that draws the
   app icon (Android adaptive foreground and background layers plus a legacy icon), the 512x512 store icon and the
   1024x500 feature graphic. Simple and bold: "BAM" in Lilita One (`bam-ref/xyz/ABC2/Resources/Fonts/LilitaOne.ttf`,
   OFL), strong colours. App icon files into `Assets/BAM/Branding/`, store images into `Docs/play/`. Tell the main
   thread the file names (the build thread assigns them).
8. **Licenses**: `Docs/LICENSES.md` (rights register: what is ours, Lilita One under OFL, anything else) and
   `Assets/BAM/Resources/BAM/Licenses.txt` (the text the Settings screen shows, OFL notice included).
9. `Docs/play/play-console-sv.md` (Swedish, exact clicks for Rickard): create the app, internal testing track and
   testers, upload the AAB, fill in App content (privacy policy, ads, app access, content rating, target audience, data
   safety), Play App Signing.

## Notes
- Compile with `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py`. Not legal advice: say so in the docs.
- Ask Rickard directly in your thread for anything only he can do or decide (hosting, Unity Cloud link, his developer
  account details). Never put personal data or keys in files.
