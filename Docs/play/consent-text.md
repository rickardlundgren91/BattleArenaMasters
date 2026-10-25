# Consent text (for the menus thread)

Shown once at first start, before the main menu (`Screens.ShowConsentIfNeeded`). Both buttons look equally prominent
(same size, neither pre-selected, no "dark pattern" colours); Google and the GDPR want a free choice. The dialog cannot
be dismissed without choosing. Text in English, as everything in the game.

## Dialog

- **Title:** Help us improve BAM?
- **Body:** Can we collect data about how you play, like which heroes you pick and how matches end? It is sent
  to Unity Analytics and never includes your name. You can change this any time in Settings.
- **Link under the body (small):** Privacy policy  -> opens `BamInfo.PrivacyPolicyUrl`
- **Buttons:** `No thanks` (sets `BamSettings.AnalyticsConsent = Consent.Denied`) and `Yes, share` (sets `Consent.Granted`).
  Put `No thanks` on the left and `Yes, share` on the right, same style.

## Settings

- **Switch label:** Share usage data
- **Small text under it:** Helps us balance heroes and fix problems. Turning it off stops sharing and asks Unity to delete
  what was already sent.
- On = `Consent.Granted`, off = `Consent.Denied`. If the player never answered (`Unknown`), show it as off.
- **Row:** Privacy policy (opens `BamInfo.PrivacyPolicyUrl`).
