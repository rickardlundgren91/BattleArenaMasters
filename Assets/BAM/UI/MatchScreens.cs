using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // Pause, result and the guided match's hint bubble.
    public static partial class Screens
    {
        static GameObject pause, resultScreen;

        // ---- pause ----

        static void BuildPause()
        {
            var body = NewScreen("Pause", true, out pause);
            var box = UiKit.Panel(body, UiKit.PanelColor).rectTransform;
            UiKit.Place(box, 0.5f, 0.5f, 0, 0, 760, 780);
            UiKit.Frame(box);
            var title = UiKit.Label(box, "Paused", 96, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Band(title, true, 50, 130, 20);
            UiKit.AddOutline(title, 4);
            var resume = UiKit.Button(box, "Resume", ResumeMatch, UiKit.Good, SfxId.UiConfirm, 64);
            UiKit.Place(resume, 0.5f, 1, 0, -230, 600, 150);
            var settingsButton = UiKit.Button(box, "Settings", () => Push(settings), UiKit.Muted);
            UiKit.Place(settingsButton, 0.5f, 1, 0, -410, 600, 140);
            var quit = UiKit.Button(box, "Quit match", ConfirmQuit, UiKit.Bad);
            UiKit.Place(quit, 0.5f, 1, 0, -580, 600, 140);
        }

        static void ConfirmQuit()
        {
            dialog.Show("Leave the match?", "This match will end.",
                "Stay", null,
                "Leave", GoToMenu,
                UiKit.Good, UiKit.Bad, () => dialog.Hide());
        }

        // ---- result: ResultCard.cs ----

        // ---- hint bubble ----

        static Canvas hintCanvas;
        static RectTransform hintBubble, hintArrow;
        static Text hintText;
        static string hintShown;
        static float hintBaseY, hintBobSign;

        static void BuildHint()
        {
            // Its own canvas without a raycaster: the bubble never steals a touch from the HUD below it.
            hintCanvas = UiKit.CreateCanvas("BAM.Hint", HintOrder);
            Object.Destroy(hintCanvas.GetComponent<GraphicRaycaster>());
            var bubble = UiKit.Panel(hintCanvas.transform, new Color(1f, 0.97f, 0.88f, 0.97f));
            bubble.raycastTarget = false;
            hintBubble = bubble.rectTransform;
            hintBubble.anchorMin = hintBubble.anchorMax = hintBubble.pivot = new Vector2(0.5f, 0.5f);
            var arrow = UiKit.Fill(hintBubble, bubble.color);
            hintArrow = arrow.rectTransform;
            hintArrow.anchorMin = hintArrow.anchorMax = new Vector2(0.5f, 0);
            hintArrow.sizeDelta = new Vector2(44, 44);
            hintArrow.localRotation = Quaternion.Euler(0, 0, 45);
            hintArrow.SetAsFirstSibling();
            hintText = UiKit.Label(hintBubble, "", 44, TextAnchor.MiddleCenter, UiKit.Ink);
            UiKit.Stretch(hintText.rectTransform, 36, 24, 36, 24);
            // The canvas stays active (so its size is always current); only the bubble is shown and hidden.
            hintBubble.gameObject.SetActive(false);
        }

        /// <summary>Guided match: a hint bubble with one short sentence. anchorScreenPos = what it points at (null = centred).</summary>
        public static void ShowHint(string text, Vector2? anchorScreenPos = null)
        {
            Init();
            var canvasRt = (RectTransform)hintCanvas.transform;
            hintBubble.gameObject.SetActive(true);
            Vector2 size = canvasRt.rect.size;
            float width = Mathf.Min(760f, size.x - 60f);
            if (!ReferenceEquals(text, hintShown))
            {
                // Only measure when the text changes, so following a moving target every frame costs nothing.
                hintShown = text;
                hintText.text = text;
                hintBubble.sizeDelta = new Vector2(width, 0);
                float h = hintText.cachedTextGeneratorForLayout.GetPreferredHeight(text,
                    hintText.GetGenerationSettings(new Vector2(width - 72f, 0))) / hintText.pixelsPerUnit;
                hintBubble.sizeDelta = new Vector2(width, Mathf.Max(110f, h + 48f));
            }

            float bubbleH = hintBubble.sizeDelta.y;
            Vector2 pos;
            bool arrowVisible = anchorScreenPos.HasValue;
            bool below = false;
            if (anchorScreenPos.HasValue
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, anchorScreenPos.Value, null, out var local))
            {
                // Above the target when there is room, else below it; always fully on screen.
                float gap = 50f + bubbleH * 0.5f;
                below = local.y + gap + bubbleH * 0.5f > size.y * 0.5f - 40f;
                pos = new Vector2(local.x, below ? local.y - gap : local.y + gap);
                float maxX = Mathf.Max(0f, (size.x - width) * 0.5f - 20f);
                pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
                hintArrow.gameObject.SetActive(true);
                hintArrow.anchorMin = hintArrow.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
                float arrowX = Mathf.Clamp(local.x - pos.x, -width * 0.5f + 50f, width * 0.5f - 50f);
                hintArrow.anchoredPosition = new Vector2(arrowX, 0);
            }
            else
            {
                arrowVisible = false;
                pos = new Vector2(0, size.y * 0.15f);
            }
            hintArrow.gameObject.SetActive(arrowVisible);
            hintBobSign = below ? -1f : 1f;
            hintBaseY = pos.y;
            hintBubble.anchoredPosition = pos;
        }

        public static void HideHint()
        {
            if (hintBubble != null) hintBubble.gameObject.SetActive(false);
            hintShown = null;
        }

        // A gentle bob towards the target so the bubble catches the eye.
        static void TickHint()
        {
            if (hintBubble == null || !hintBubble.gameObject.activeSelf) return;
            float bob = Mathf.Sin(Time.unscaledTime * 5f) * 8f;
            var p = hintBubble.anchoredPosition;
            p.y = hintBaseY - hintBobSign * (bob + 8f);
            hintBubble.anchoredPosition = p;
        }
    }
}
