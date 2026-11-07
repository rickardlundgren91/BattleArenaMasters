using System;
using BAM.Audio;
using BAM.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    /// <summary>
    /// The consent wording, from the Play thread's Docs/play/consent-text.md. It must match the privacy policy and the
    /// Data safety answers, so change it there first.
    /// </summary>
    static class ConsentText
    {
        public const string Title = "Help us improve BAM?";
        public const string Body =
            "Can we collect data about how you play, like which heroes you pick and how matches end? " +
            "It is sent to Unity Analytics and never includes your name. You can change this any time in Settings.";
        public const string Yes = "Yes, share";
        public const string No = "No thanks";
        public const string SettingsLabel = "Share usage data";
        public const string SettingsNote =
            "Helps us balance heroes and fix problems. Turning it off stops sharing and asks Unity to delete what was already sent.";
    }

    // Settings, licenses and the shared dialog.
    public static partial class Screens
    {
        static GameObject settings, licenses;
        static HubSwitch soundSwitch, musicSwitch, vibrationSwitch, hitVibrationSwitch, leftSwitch, shareSwitch;
        static HubChoice quality, frameRate;
        static GameObject tutorialRow; // hidden in a match: starting the tutorial there would drop the match unasked
        static GameObject settingsBack; // the Back pill: only when Settings is opened from the pause menu, where no tab bar shows
        static bool licensesLoaded;
        static RectTransform licensesContent;

        // The old Settings page (MenuHome.PageHubSet with the v3 rows of MenuV3.cs): green section heads, glass rows with a
        // switch, a choice or a value pill: Sound, Music, Vibration; Quality, Frame rate, Active. Player name comes when the game
        // has one. Left-handed controls and the privacy rows are BAM's own.
        static void BuildSettings()
        {
            var content = NewPage("Settings", out settings, out var panel, back: false); // a tab: HOME is in the bar under it
            tabPanels[2] = panel;
            // from the pause menu there is no tab bar, so only Android's back led out: a Back pill there, with the sheet moved
            // down under it as on the other pages (bug review 2026-11-02, nr 22; RefreshSettings switches it)
            settingsBack = BackPill((RectTransform)panel.parent).gameObject;
            settingsBack.SetActive(false);
            VList(content, 2 * Cw, new RectOffset(0, 0, 0, Mathf.RoundToInt(6 * Cw)));

            SetHead(content, "AUDIO");
            soundSwitch = SwitchRow(content, "Sound", on =>
            {
                BamSettings.SfxVolume = on ? 0.8f : 0f;
                if (on) BamAudio.Play(SfxId.UiTap);
            });
            musicSwitch = SwitchRow(content, "Music", on => BamSettings.MusicOn = on);
            vibrationSwitch = SwitchRow(content, "Vibration", on =>
            {
                BamSettings.Vibration = on;
                if (on) BamHaptics.Pulse(0.3f);
            });
            // tester feedback (Ski 2026-11-07): a light buzz on each of your own hits, off unless the player turns it on
            hitVibrationSwitch = SwitchRow(content, "Vibrate on my hits", on =>
            {
                BamSettings.HitVibration = on;
                if (on) BamHaptics.Pulse(BamHaptics.LightHit);
            });

            SetHead(content, "GRAPHICS");
            quality = ChoiceRow(content, "Quality", new[] { "High", "Low" }, i => BamSettings.Graphics = i == 0 ? GraphicsQuality.High : GraphicsQuality.Low);
            frameRate = ChoiceRow(content, "Frame rate", new[] { "30", "60" }, i => BamSettings.Fps = i == 0 ? 30 : 60);
            // the old "Active: High · 60 fps" row is gone (2026-10-31): it only repeated the two choices above it

            SetHead(content, "CONTROLS");
            leftSwitch = SwitchRow(content, "Left-handed controls", on => BamSettings.LeftHanded = on);

            SetHead(content, "PRIVACY");
            shareSwitch = SwitchRow(content, ConsentText.SettingsLabel, on => BamSettings.AnalyticsConsent = on ? Consent.Granted : Consent.Denied);
            var note = Treb(content, ConsentText.SettingsNote, 2.9f, UiKit.TextDim);
            note.fontStyle = FontStyle.Normal;
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.alignment = TextAnchor.UpperLeft;
            // The privacy policy row only appears once the Play thread has a public address for it.
            if (!string.IsNullOrEmpty(BamInfo.PrivacyPolicyUrl))
                LinkRow(content, "Privacy policy", "Open", () => Application.OpenURL(BamInfo.PrivacyPolicyUrl));

            if (OnlineHooks.OpenAccount != null) LinkRow(content, "Account", "Open", () => OnlineHooks.OpenAccount());

            SetHead(content, "HELP");
            tutorialRow = LinkRow(content, "How to play", "Tutorial", () => StartTutorial());
            // for the testers (2026-10-31): straight to the Discord, next to the version they should quote
            if (!string.IsNullOrEmpty(BamInfo.DiscordUrl)) LinkRow(content, "Report a bug", "Discord", () => Application.OpenURL(BamInfo.DiscordUrl));
            LinkRow(content, "Licenses", "Open", () => Push(licenses));
            PlainRow(content, "Version", Application.version);
        }

        // HubSetHead: a small mint heading, cw(1) in from the rows
        static void SetHead(RectTransform list, string text)
        {
            var head = UiKit.NewRect(text, list);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 4.5f * Cw;
            var t = Treb(head, text, 2.8f, UiKit.Primary);
            t.alignment = TextAnchor.LowerLeft;
            Stretched(t.rectTransform, Cw, 0, 0, 4.5f * Cw);
        }

        static RectTransform SetRow(RectTransform list, string label, float h)
        {
            var row = UiKit.NewRect(label, list);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            CardBack(row, 3 * Cw);
            var t = Treb(row, label, 3.4f, UiKit.TextColor);
            Stretched(t.rectTransform, 4 * Cw, 0, 30 * Cw, h);
            return row;
        }

        // On/Off: a switch; the whole row toggles it
        static HubSwitch SwitchRow(RectTransform list, string label, Action<bool> changed)
        {
            var row = SetRow(list, label, 12.5f * Cw);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var sw = row.gameObject.AddComponent<HubSwitch>();
            sw.Build(row, changed);
            return sw;
        }

        // two short choices side by side on the right (HubSegments)
        static HubChoice ChoiceRow(RectTransform list, string label, string[] names, Action<int> chosen)
        {
            var row = SetRow(list, label, 14 * Cw);
            return new HubChoice(row, names, chosen);
        }

        // a row that opens something: the value in a mint pill with a small arrow
        static GameObject LinkRow(RectTransform list, string label, string value, Action onClick)
        {
            float rh = 11 * Cw, vh = rh - 3.6f * Cw;
            var row = SetRow(list, label, rh);
            var pill = Pic(row, "Value", UiKit.Rounded, new Color(UiKit.Primary.r, UiKit.Primary.g, UiKit.Primary.b, 0.16f));
            Radius(pill, vh / 2);
            var text = UiKit.Label(pill.transform, value, Mathf.RoundToInt(3.6f * Cw), TextAnchor.MiddleLeft, UiKit.Primary);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float vw = text.preferredWidth + 9 * Cw;
            Mid(pill.rectTransform, 1, 0.5f, -2.5f * Cw - vw / 2, 0, vw, vh);
            Stretched(text.rectTransform, 3.5f * Cw, 0, 0, vh);
            var arrow = Pic(pill.rectTransform, "Arrow", HomeArt.Roof, UiKit.Primary);
            Mid(arrow.rectTransform, 1, 0.5f, -3 * Cw, 0, 2.6f * Cw, 1.6f * Cw);
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); onClick?.Invoke(); });
            row.gameObject.AddComponent<ButtonFx>().PressScale = false;
            return row.gameObject;
        }

        // a row that only shows a value, dim on the right
        static Text PlainRow(RectTransform list, string label, string value)
        {
            var row = SetRow(list, label, 11 * Cw);
            var t = UiKit.Label(row, value, Mathf.RoundToInt(3 * Cw), TextAnchor.MiddleRight, UiKit.TextDim);
            t.font = UiKit.BodyFont;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretched(t.rectTransform, 30 * Cw, 0, 4 * Cw, 11 * Cw);
            return t;
        }

        static void RefreshSettings()
        {
            soundSwitch.Set(BamSettings.SfxVolume > 0.001f, true);
            musicSwitch.Set(BamSettings.MusicOn, true);
            vibrationSwitch.Set(BamSettings.Vibration, true);
            hitVibrationSwitch.Set(BamSettings.HitVibration, true);
            quality.Set(BamSettings.Graphics == GraphicsQuality.High ? 0 : 1);
            frameRate.Set(BamSettings.Fps <= 30 ? 0 : 1);
            leftSwitch.Set(BamSettings.LeftHanded, true);
            shareSwitch.Set(BamSettings.AnalyticsConsent == Consent.Granted, true);
            bool inMatch = Flow.Game != null && Flow.Game.InMatch;
            tutorialRow.SetActive(!inMatch);
            settingsBack.SetActive(inMatch); // nr 22
            tabPanels[2].offsetMax = new Vector2(tabPanels[2].offsetMax.x, -(inMatch ? PageTop : 4 * Cw));
        }

        static void BuildLicenses()
        {
            licensesContent = NewPage("Licenses", out licenses, out var panel);
            panel.offsetMin = new Vector2(panel.offsetMin.x, 24f); // never under the tab bar (as RefreshNav does for tab pages)
            VList(licensesContent, 0, new RectOffset(20, 20, 0, 40));
            licenses.AddComponent<OnEnableHook>().Enabled = LoadLicenses;
        }

        // Loaded on first open, not at boot. Plain built-in font: legal text needs characters a display font may lack.
        static void LoadLicenses()
        {
            if (licensesLoaded) return;
            licensesLoaded = true;
            var asset = Resources.Load<TextAsset>(BamInfo.LicensesResource);
            string text = asset != null ? asset.text : "The licenses file is missing from this build.";
            if (asset == null) Debug.LogError("[BAM.UI] Resources/" + BamInfo.LicensesResource + " not found");
            var plain = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // One uGUI Text holds at most ~16k characters (65k vertices), so long texts are split at line breaks.
            const int Chunk = 6000;
            int pos = 0;
            while (pos < text.Length)
            {
                int end = Mathf.Min(text.Length, pos + Chunk);
                if (end < text.Length)
                {
                    int nl = text.LastIndexOf('\n', end - 1, end - pos);
                    if (nl > pos) end = nl + 1;
                }
                var t = UiKit.Label(licensesContent, text.Substring(pos, end - pos).TrimEnd('\n', '\r'), 30, TextAnchor.UpperLeft, UiKit.TextColor);
                if (plain != null) t.font = plain;
                pos = end;
            }
        }

        /// <summary>One modal box: title, text, two buttons side by side. Built once and refilled for each question.</summary>
        sealed class Dialog
        {
            readonly GameObject go;
            readonly Text title, body;
            readonly Button left, right, link;
            Action onLeft, onRight, onLink;
            public Action Back { get; private set; }
            public bool Open => go.activeSelf;

            public Dialog(RectTransform parent)
            {
                var rt = UiKit.NewRect("Dialog", parent);
                UiKit.Stretch(rt);
                go = rt.gameObject;
                UiKit.Fill(rt, UiKit.Dim).raycastTarget = true;
                var safe = UiKit.SafeArea(rt);

                var box = UiKit.Panel(safe, new Color(0.055f, 0.07f, 0.19f, 0.97f)).rectTransform;
                UiKit.Frame(box);
                box.anchorMin = new Vector2(0, 0.5f);
                box.anchorMax = new Vector2(1, 0.5f);
                box.pivot = new Vector2(0.5f, 0.5f);
                box.offsetMin = new Vector2(50, 0);
                box.offsetMax = new Vector2(-50, 0);
                VList(box, 30, new RectOffset(50, 50, 50, 50));
                box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                title = UiKit.Label(box, "", 64, TextAnchor.MiddleCenter, UiKit.Gold);
                UiKit.AddOutline(title, 3);
                body = UiKit.Label(box, "", 40, TextAnchor.UpperCenter, UiKit.TextColor);

                // A small text link (the privacy policy under the consent question); hidden when not used.
                var linkLabel = UiKit.Label(box, "", 38, TextAnchor.MiddleCenter, UiKit.Gold);
                linkLabel.raycastTarget = true;
                linkLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                link = linkLabel.gameObject.AddComponent<Button>();
                link.targetGraphic = linkLabel;
                link.navigation = new Navigation { mode = Navigation.Mode.None };
                link.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); onLink?.Invoke(); });

                var buttons = UiKit.NewRect("Buttons", box);
                buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
                left = UiKit.Button(buttons, "", () => Answer(onLeft), UiKit.Muted, SfxId.UiTap, 52);
                right = UiKit.Button(buttons, "", () => Answer(onRight), UiKit.Primary, SfxId.UiTap, 52);
                Half((RectTransform)left.transform, 0);
                Half((RectTransform)right.transform, 1);
                go.SetActive(false);
            }

            static void Half(RectTransform rt, int side)
            {
                rt.anchorMin = new Vector2(side * 0.5f, 0);
                rt.anchorMax = new Vector2(side * 0.5f + 0.5f, 1);
                rt.offsetMin = new Vector2(side == 0 ? 0 : 15, 0);
                rt.offsetMax = new Vector2(side == 0 ? -15 : 0, 0);
            }

            /// <summary>back = what the Android back button does (null = it does nothing). The link is optional.</summary>
            public void Show(string titleText, string bodyText, string leftText, Action leftAction, string rightText,
                Action rightAction, Color leftColor, Color rightColor, Action back, string linkText = null, Action linkAction = null)
            {
                bool hasLink = !string.IsNullOrEmpty(linkText) && linkAction != null;
                link.gameObject.SetActive(hasLink);
                UiKit.ButtonText(link).text = hasLink ? linkText : "";
                onLink = linkAction;
                title.text = titleText;
                body.text = bodyText;
                UiKit.ButtonText(left).text = leftText;
                UiKit.ButtonText(right).text = rightText;
                UiKit.Tint(left, leftColor);
                UiKit.Tint(right, rightColor);
                onLeft = leftAction;
                onRight = rightAction;
                Back = back;
                go.transform.SetAsLastSibling();
                go.SetActive(true);
            }

            public void Hide()
            {
                go.SetActive(false);
                onLeft = onRight = onLink = Back = null;
            }

            void Answer(Action action)
            {
                // Hide first: the action may open another dialog (or this one again).
                Hide();
                action?.Invoke();
            }
        }
    }

    /// <summary>Runs a callback whenever its GameObject is shown (lazy loading).</summary>
    sealed class OnEnableHook : MonoBehaviour
    {
        public Action Enabled;
        void OnEnable() => Enabled?.Invoke();
    }
}
