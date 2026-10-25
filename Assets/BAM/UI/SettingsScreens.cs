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
        static Slider volume;
        static Segmented graphics;
        static SwitchView leftHanded, shareData;
        static bool licensesLoaded;
        static RectTransform licensesContent;

        static void BuildSettings()
        {
            var content = NewPage("Settings", out settings, out var panel);
            tabPanels[2] = panel;
            VList(content, 14, new RectOffset(8, 8, 0, 30));

            SectionHeader(content, "AUDIO");
            var row = PageRow(content, "Sound volume", 124, out _);
            volume = SliderFactory.Create(row, v =>
            {
                BamSettings.SfxVolume = v;
                BamAudio.Play(SfxId.UiTap); // lets the player hear the new volume
            });
            UiKit.Place(volume, 1, 0.5f, -28, 0, 360, 90);

            SectionHeader(content, "GRAPHICS");
            row = PageRow(content, "Quality", 210, out var qualityLabel);
            qualityLabel.rectTransform.offsetMin = new Vector2(36, 110);
            var seg = UiKit.NewRect("Quality", row);
            UiKit.Band(seg, false, 22, 92, 26);
            // Shown High first, like the old game.
            graphics = new Segmented(seg, new[] { "High", "Low" }, i => BamSettings.Graphics = i == 0 ? GraphicsQuality.High : GraphicsQuality.Low);

            SectionHeader(content, "CONTROLS");
            row = PageRow(content, "Left-handed controls", 124, out _);
            leftHanded = new SwitchView(row, on => BamSettings.LeftHanded = on);
            UiKit.Place(leftHanded.Rect, 1, 0.5f, -28, 0, 136, 72);

            SectionHeader(content, "PRIVACY");
            row = PageRow(content, ConsentText.SettingsLabel, 236, out var shareLabel);
            shareLabel.rectTransform.offsetMin = new Vector2(36, 130);
            var note = UiKit.Label(row, ConsentText.SettingsNote, 30, TextAnchor.UpperLeft, UiKit.TextDim);
            note.font = UiKit.BodyFont;
            note.rectTransform.anchorMin = new Vector2(0, 0);
            note.rectTransform.anchorMax = new Vector2(1, 0);
            note.rectTransform.offsetMin = new Vector2(36, 16);
            note.rectTransform.offsetMax = new Vector2(-36, 130);
            shareData = new SwitchView(row, on => BamSettings.AnalyticsConsent = on ? Consent.Granted : Consent.Denied);
            UiKit.Place(shareData.Rect, 1, 1, -28, -30, 136, 72);
            // The privacy policy row only appears once the Play thread has a public address for it.
            if (!string.IsNullOrEmpty(BamInfo.PrivacyPolicyUrl))
                LinkRow(content, "Privacy policy", () => Application.OpenURL(BamInfo.PrivacyPolicyUrl));

            SectionHeader(content, "ABOUT");
            LinkRow(content, "Licenses", () => Push(licenses));
            row = PageRow(content, "Version", 124, out _);
            var version = UiKit.Label(row, Application.version, 36, TextAnchor.MiddleRight, Lavender);
            version.font = UiKit.BodyFont;
            UiKit.Stretch(version.rectTransform, 300, 0, 36, 0);
        }

        /// <summary>A row that opens something, with a "&gt;" on the right.</summary>
        static void LinkRow(RectTransform list, string text, Action onClick)
        {
            var row = PageRow(list, text, 124, out _);
            var bg = row.GetComponent<Image>();
            bg.raycastTarget = true;
            var b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); onClick?.Invoke(); });
            row.gameObject.AddComponent<ButtonFx>();
            var arrow = UiKit.Label(row, ">", 48, TextAnchor.MiddleRight, Lavender);
            UiKit.Stretch(arrow.rectTransform, 300, 0, 36, 0);
        }

        static void RefreshSettings()
        {
            volume.SetValueWithoutNotify(BamSettings.SfxVolume);
            graphics.Set(BamSettings.Graphics == GraphicsQuality.High ? 0 : 1);
            leftHanded.Set(BamSettings.LeftHanded);
            shareData.Set(BamSettings.AnalyticsConsent == Consent.Granted);
        }

        static void BuildLicenses()
        {
            licensesContent = NewPage("Licenses", out licenses, out _);
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

                var box = UiKit.Panel(safe, new Color(0.086f, 0.114f, 0.141f, 0.97f)).rectTransform;
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
