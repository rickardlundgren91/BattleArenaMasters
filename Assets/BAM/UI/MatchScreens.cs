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
        static Text resultTitle, resultSub, resultTime, resultKills, resultDeaths, resultLevel;
        static Image resultBadgeColor;
        static Text resultBadgeInitial;

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

        // ---- result ----

        static void BuildResult()
        {
            var body = NewScreen("Result", true, out resultScreen);
            resultTitle = UiKit.Label(body, "", 150, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Band(resultTitle, true, 180, 200, 20);
            UiKit.AddOutline(resultTitle, 6);
            resultSub = UiKit.Label(body, "", 48, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Band(resultSub, true, 380, 70, 40);

            var box = UiKit.Panel(body, UiKit.PanelColor).rectTransform;
            UiKit.Place(box, 0.5f, 0.5f, 0, 60, 820, 560);
            UiKit.Frame(box);
            var badge = UiKit.HeroBadge(box, HeroId.Bram);
            UiKit.Place(badge, 0.5f, 1, 0, -40, 170, 170);
            resultBadgeColor = badge.GetChild(0).GetComponent<Image>();
            resultBadgeInitial = badge.GetComponentInChildren<Text>();
            resultTime = StatCell(box, "Time", 0);
            resultKills = StatCell(box, "Kills", 1);
            resultDeaths = StatCell(box, "Deaths", 2);
            resultLevel = StatCell(box, "Level", 3);

            var again = UiKit.Button(body, "Play again", RestartMatch, UiKit.Orange, SfxId.UiConfirm, 64);
            UiKit.Place(again, 0.5f, 0, 0, 250, 620, 160);
            var menu = UiKit.Button(body, "Menu", GoToMenu, UiKit.Muted);
            UiKit.Place(menu, 0.5f, 0, 0, 70, 620, 140);
        }

        // Four columns under the badge: the number big, its name small.
        static Text StatCell(RectTransform box, string name, int column)
        {
            var cell = UiKit.NewRect(name, box);
            cell.anchorMin = new Vector2(column / 4f, 0);
            cell.anchorMax = new Vector2((column + 1) / 4f, 0);
            cell.pivot = new Vector2(0.5f, 0);
            cell.offsetMin = new Vector2(0, 50);
            cell.offsetMax = new Vector2(0, 290);
            var value = UiKit.Label(cell, "", 80, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Band(value, true, 20, 120, 0);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.AddOutline(value, 3);
            var label = UiKit.Label(cell, name, 36, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Band(label, false, 30, 60, 0);
            return value;
        }

        static void FillResult(MatchResult r)
        {
            if (r.Quit) { resultTitle.text = "Match left"; resultTitle.color = UiKit.TextDim; }
            else if (r.Won) { resultTitle.text = "VICTORY!"; resultTitle.color = UiKit.Gold; }
            else { resultTitle.text = "DEFEAT"; resultTitle.color = UiKit.Bad; }

            var info = HeroCatalog.Get(r.Hero);
            if (r.Tutorial) resultSub.text = r.Won ? "Tutorial complete!" : "Tutorial";
            else resultSub.text = info.Name + "  /  " + DifficultyNames[Mathf.Clamp((int)r.Difficulty, 0, 2)] + " bots";

            resultBadgeColor.color = UiKit.HeroColor(r.Hero);
            resultBadgeInitial.text = info.Name.Substring(0, 1);
            int seconds = Mathf.Max(0, Mathf.RoundToInt(r.Seconds));
            resultTime.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            resultKills.text = r.Kills.ToString();
            resultDeaths.text = r.Deaths.ToString();
            resultLevel.text = r.Level.ToString();
        }

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
