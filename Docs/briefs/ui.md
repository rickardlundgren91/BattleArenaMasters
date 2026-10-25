# Brief: menus, settings and the consent dialog ("Gör menyerna och inställningarna")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md` (UI section) and
`Docs/DECISIONS.md` in `/mnt/project-files/bam-unity/` first. Your folders: `Assets/BAM/UI/` (BAM.UI) and
`Assets/BAM/Fonts/`. Effort: medium.

## Goal
Implement `Assets/BAM/UI/Screens.cs` and `UiKit` (keep every public signature of the stub): every screen outside the
match, built in code with uGUI, in portrait, in English, with the Lilita One font, looking like a bright mobile game.

## Deliverables
1. **UiKit**: the font (copy `/mnt/project-files/bam-ref/xyz/ABC2/Resources/Fonts/LilitaOne.ttf` and
   `bam-ref/xyz/LICENSES/OFL-LilitaOne.txt` into the project so the font is in the build, for example
   `Assets/BAM/UI/Resources/BAM/Fonts/`), colours, buttons with press feedback and `BamAudio.Play(SfxId.UiTap)`,
   panels, labels, a canvas factory (CanvasScaler 1080x1920, match 0.5) and a safe-area helper. The HUD will use it.
   The project uses the Input System only: create an `EventSystem` with `InputSystemUIInputModule` (the old
   StandaloneInputModule does not work here).
2. **Main menu**: title "Battle Arena Masters", Play, Tutorial, Settings. Before the first finished tutorial, Play
   offers the tutorial first (skippable).
3. **Hero select**: the seven heroes from `BAM.Sim.HeroCatalog` (name, title, role, special and passive with their texts,
   the 1-3 bars for health, damage, speed, difficulty), a simple code-drawn badge per hero for now, Easy/Normal/Hard,
   Start. Opens on `BamSettings.LastHero`/`LastDifficulty`; Start saves them and calls `Flow.Game.StartMatch(...)`.
4. **Settings**: sound volume, graphics Low/High, left-handed controls, "Share anonymous usage data" switch (writes
   `BamSettings.AnalyticsConsent`), privacy policy link (`BamInfo.PrivacyPolicyUrl`, hidden while empty), licenses
   screen (scrollable text from `Resources.Load<TextAsset>(BamInfo.LicensesResource)`), app version.
5. **Consent dialog** at first start (`ShowConsentIfNeeded`): the Play thread writes the final text in
   `Docs/play/consent-text.md`; start with a clear placeholder and switch to it when it exists. Yes and No are equally
   easy to press (Play policy).
6. **Pause**, **Result** and **Hint bubble** as in CONTRACTS.md. Android back button: closes the top dialog or goes back
   one screen in the menus (the game layer handles back during a match).
7. `Screens.IsBlockingInput`, `HideAll`.

## Notes
- No per-frame allocations; build screens once and show/hide them.
- Compile with `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py --only BAM.UI`, then the full check.
- You cannot run Unity: keep layouts anchor-based and simple, and tell the main thread what Rickard should check
  (sizes on a phone, the safe area, every button). Ask Rickard directly in your thread about matters of taste.
