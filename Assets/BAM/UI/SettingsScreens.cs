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
            var body = NewScreen("Settings", false, out settings);
            TopBar(body, "Settings");

            var panel = UiKit.NewRect("List", body);
            UiKit.Stretch(panel, 30, 170, 30, 30);
            var content = ScrollContent(panel, out _);
            VList(content, 18, new RectOffset(0, 0, 0, 20));

            var row = Row(content, "Sound", 140, out _);
            volume = SliderFactory.Create(row, v =>
            {
                BamSettings.SfxVolume = v;
                BamAudio.Play(SfxId.UiTap); // lets the player hear the new volume
            });
            UiKit.Place(volume, 1, 0.5f, -30, 0, 380, 100);

            row = Row(content, "Graphics", 140, out _);
            var seg = UiKit.NewRect("Quality", row);
            UiKit.Place(seg, 1, 0.5f, -24, 0, 380, 104);
            graphics = new Segmented(seg, new[] { "Low", "High" }, i => BamSettings.Graphics = (GraphicsQuality)i);

            row = Row(content, "Left-handed controls", 140, out _);
            leftHanded = new SwitchView(row, on => BamSettings.LeftHanded = on);
            UiKit.Place(leftHanded.Rect, 1, 0.5f, -30, 0, 220, 100);

            row = Row(content, ConsentText.SettingsLabel, 270, out var shareLabel);
            shareLabel.rectTransform.offsetMin = new Vector2(36, 150);
            shareLabel.rectTransform.offsetMax = new Vector2(-270, -14);
            var note = UiKit.Label(row, ConsentText.SettingsNote, 30, TextAnchor.UpperLeft, UiKit.TextDim);
            note.rectTransform.anchorMin = new Vector2(0, 0);
            note.rectTransform.anchorMax = new Vector2(1, 0);
            note.rectTransform.offsetMin = new Vector2(36, 14);
            note.rectTransform.offsetMax = new Vector2(-270, 150);
            shareData = new SwitchView(row, on => BamSettings.AnalyticsConsent = on ? Consent.Granted : Consent.Denied);
            UiKit.Place(shareData.Rect, 1, 0.5f, -30, 0, 220, 100);

            // The privacy policy button only appears once the Play thread has a public address for it.
            if (!string.IsNullOrEmpty(BamInfo.PrivacyPolicyUrl))
                LinkButton(content, "Privacy policy", () => Application.OpenURL(BamInfo.PrivacyPolicyUrl));
            LinkButton(content, "Licenses", () => Push(licenses));

            var version = UiKit.Label(content, BamInfo.AppName + "  v" + Application.version, 34, TextAnchor.MiddleCenter, UiKit.TextDim);
            version.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
        }

        static void LinkButton(RectTransform list, string text, Action onClick)
        {
            var b = UiKit.Button(list, text, onClick, UiKit.Muted, SfxId.UiTap, 48);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 130;
        }

        static void RefreshSettings()
        {
            volume.SetValueWithoutNotify(BamSettings.SfxVolume);
            graphics.Set((int)BamSettings.Graphics);
            leftHanded.Set(BamSettings.LeftHanded);
            shareData.Set(BamSettings.AnalyticsConsent == Consent.Granted);
        }

        static void BuildLicenses()
        {
            var body = NewScreen("Licenses", false, out licenses);
            TopBar(body, "Licenses");
            var panel = UiKit.Panel(body, UiKit.PanelColor);
            UiKit.Stretch(panel.rectTransform, 24, 170, 24, 30);
            licensesContent = ScrollContent(panel.rectTransform, out _);
            VList(licensesContent, 0, new RectOffset(30, 30, 30, 40));
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

                var box = UiKit.Panel(safe, new Color(0.1f, 0.14f, 0.34f, 0.98f)).rectTransform;
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
