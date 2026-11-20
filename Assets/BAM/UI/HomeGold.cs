using System;
using BAM.Art;
using BAM.Audio;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // Gold on Home (Rickard 2026-11-15 "Ja, bygg nu", numbers "okej"; BAM.Online Gold.cs counts it): a pill beside the skin
    // dropdown, "GOLD 1250 · DAILY 1/3 ▾", whose tap opens the day's first win and three goals with their progress and when new
    // ones come. The skin list shows "1500 GOLD" on a gold skin not bought yet (the first tap previews it on the hero and asks, the second buys; HomeScreen SkinPreview) and "TOP 10 ONLY" on a premium
    // skin (Rickard 2026-11-19: worn only while in the top 10, then on every hero; "TOP 10" on one worn that way). After a season
    // finished in the top 10 a premium skin shows "KEEP 3000 GOLD": two taps (on the worn one) keep it for good. Offline builds have no gold hooks: no pill, every skin open, as before.
    public static partial class Screens
    {
        static GameObject homeGoldPill;
        static Text homeGoldText;
        static RectTransform homeGoalList;
        static int skinBuyAsk = -1; // hero * 100 + skin whose "BUY FOR 1500 GOLD?" is showing: the next tap on it buys
        static bool skinBuying;

        static readonly Color GoldText = new Color32(0xff, 0xd1, 0x66, 0xff);
        const string GoldHex = "ffd166";

        /// <summary>The skin row's text: the tick on the worn look, the name, and what it costs when it cannot be worn yet.</summary>
        static string SkinRowText(HeroId id, int skin, int cur, int price, string note)
        {
            string name = (skin == cur ? "✓  " : "    ") + HeroSkins.Name(id, skin).ToUpperInvariant();
            if (note != null) return name + "   <color=#" + GoldHex + ">" + note + "</color>";
            if (price < 0) return name + "   <color=#8a90b8>TOP 10 ONLY</color>";
            if (price > 0) return name + "   <color=#" + GoldHex + ">" + (OnlineHooks.SkinKeep != null && OnlineHooks.SkinKeep(id, skin) > 0 ? "KEEP " : "") + price + " GOLD</color>";
            int keep = OnlineHooks.SkinKeep != null ? OnlineHooks.SkinKeep(id, skin) : 0;
            if (keep > 0) return name + "   <color=#" + GoldHex + ">KEEP " + keep + " GOLD</color>";
            if (OnlineHooks.SkinTop10 != null && OnlineHooks.SkinTop10(id, skin)) return name + "   <color=#7fe3ff>TOP 10</color>";
            return name;
        }

        /// <summary>A tap on a skin that cannot be worn yet: the first tap asks, the second buys (or says how much gold is missing).</summary>
        static void SkinBuyTap(HeroId id, int skin, int price, Text row)
        {
            int cur = HeroSkins.Chosen(id);
            if (price < 0 || OnlineHooks.BuySkin == null) return;
            if (skinBuying) return;
            int have = OnlineHooks.Gold != null ? OnlineHooks.Gold() : 0;
            if (have < price) { row.text = SkinRowText(id, skin, cur, price, "NEED " + (price - have) + " MORE GOLD"); return; }
            int key = (int)id * 100 + skin;
            bool keep = OnlineHooks.SkinKeep != null && OnlineHooks.SkinKeep(id, skin) > 0;
            if (skinBuyAsk != key) { skinBuyAsk = key; row.text = SkinRowText(id, skin, cur, price, "TAP AGAIN: " + (keep ? "KEEP FOR " : "BUY ") + price + " GOLD"); return; }
            skinBuyAsk = -1;
            skinBuying = true;
            row.text = SkinRowText(id, skin, cur, price, "BUYING...");
            OnlineHooks.BuySkin(id, skin, why =>
            {
                skinBuying = false;
                if (why == null)
                {
                    BamAudio.Play(SfxId.UiTap);
                    HeroSkins.Choose(id, skin);
                    SkinListOpen(false);
                    RefreshHome(); // the hero puts the new look on at once
                }
                else if (row != null) row.text = SkinRowText(id, skin, HeroSkins.Chosen(id), price, why.ToUpperInvariant());
            });
        }

        static void RefreshGoldPill()
        {
            if (homeGoldText == null || OnlineHooks.Gold == null) return;
            int done = 0, all = 0;
            var goals = OnlineHooks.DailyGoals != null ? OnlineHooks.DailyGoals() : null;
            if (goals != null) foreach (var g in goals) { all++; if (g.Done) done++; }
            homeGoldText.text = "<color=#" + GoldHex + ">GOLD " + OnlineHooks.Gold() + "</color>  ·  DAILY " + done + "/" + all + "  ▾";
        }

        static void GoalListOpen(bool open)
        {
            if (homeGoalList == null) return;
            for (int i = homeGoalList.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(homeGoalList.GetChild(i).gameObject);
            homeGoalList.gameObject.SetActive(open);
            if (!open) return;
            SkinListOpen(false);
            homeGoalList.SetAsLastSibling();
            const float rowH = 5.6f * Cw, gap = 0.6f * Cw, w = 54 * Cw;
            int y = 0;
            void Row(string text, Color back)
            {
                var row = UiKit.Fill(homeGoalList, back, UiKit.Rounded);
                row.name = "Goal";
                row.raycastTarget = true; // a tap on the list closes it
                row.pixelsPerUnitMultiplier = 28f / (1.6f * Cw);
                var rr = row.rectTransform;
                rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0, 1);
                rr.anchoredPosition = new Vector2(0, -y * (rowH + gap));
                rr.sizeDelta = new Vector2(w, rowH);
                var t = UiKit.Label(rr, text, Mathf.RoundToInt(2.7f * Cw), TextAnchor.MiddleLeft, new Color32(0xcd, 0xd3, 0xfa, 0xff));
                t.supportRichText = true;
                t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                var tr = t.rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(2 * Cw, 0); tr.offsetMax = Vector2.zero;
                t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
                var b = row.gameObject.AddComponent<Button>();
                b.targetGraphic = row;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => GoalListOpen(false));
                y++;
            }
            var dark = new Color(10 / 255f, 13 / 255f, 36 / 255f, 0.95f);
            var doneBack = new Color(16 / 255f, 44 / 255f, 34 / 255f, 0.95f);
            bool winLeft = OnlineHooks.FirstWinGold != null && OnlineHooks.FirstWinGold() > 0;
            Row((winLeft ? "    " : "✓  ") + "First win today   <color=#" + GoldHex + ">+100</color>", winLeft ? dark : doneBack);
            var goals = OnlineHooks.DailyGoals != null ? OnlineHooks.DailyGoals() : null;
            if (goals != null)
                foreach (var g in goals)
                    Row((g.Done ? "✓  " : "    ") + g.Text + "   " + g.Have + "/" + g.Need + "   <color=#" + GoldHex + ">+50</color>", g.Done ? doneBack : dark);
            var left = TimeSpan.FromSeconds(86400 - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 86400);
            // only the countdown: the level-reward text ran past the screen's right edge (Rickard 2026-11-20)
            Row("<color=#8a90b8>New goals in " + (int)left.TotalHours + "h " + left.Minutes + "m</color>", dark);
            homeGoalList.sizeDelta = new Vector2(w, y * (rowH + gap));
        }

        /// <summary>Keeps the pill's numbers fresh while Home is open: gold and goals change when the server answers.</summary>
        sealed class GoldTicker : MonoBehaviour
        {
            float next;
            void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 0.5f;
                RefreshGoldPill();
            }
        }

        sealed partial class HomeFx
        {
            // the gold pill, right of the skin dropdown and level with it
            void BuildGoldPill()
            {
                if (OnlineHooks.Gold == null) return;
                float y = SkinRowY, h = 6.4f * Cw, x = 41.4f * Cw, w = 38 * Cw;
                var pill = Img(top, "GoldPill", UiKit.Rounded, new Color(10 / 255f, 13 / 255f, 36 / 255f, 0.85f));
                Round(pill, 1.6f * Cw);
                pill.raycastTarget = true;
                AtTop(pill.rectTransform, x, y, w, h);
                var edge = Img(pill.rectTransform, "Edge", HomeArt.RoundRing(0.3f * Cw / (1.6f * Cw)), Fade(HubGold, 0.7f));
                RoundRing(edge, 1.6f * Cw);
                UiKit.Stretch(edge.rectTransform);
                edge.raycastTarget = false;
                var label = Lil(pill.rectTransform, "", 2.8f, HubSoft);
                label.supportRichText = true;
                label.alignment = TextAnchor.MiddleLeft;
                label.font = UiKit.BodyFont;
                label.fontStyle = FontStyle.Bold;
                var lr = label.rectTransform;
                lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(2.2f * Cw, 0); lr.offsetMax = Vector2.zero;
                Edge(label, new Color(6 / 255f, 8 / 255f, 26 / 255f, 0.9f), 0.2f * Cw, new Color(0, 0, 0, 0.85f), 0.3f * Cw);
                var b = pill.gameObject.AddComponent<Button>();
                b.targetGraphic = pill;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); GoalListOpen(!homeGoalList.gameObject.activeSelf); });
                pill.gameObject.AddComponent<GoldTicker>();
                homeGoldPill = pill.gameObject;
                homeGoldText = label;

                homeGoalList = UiKit.NewRect("GoalList", top);
                AtTop(homeGoalList, x, y + h + 0.8f * Cw, 54 * Cw, 0);
                homeGoalList.gameObject.SetActive(false);
                RefreshGoldPill();
            }
        }
    }
}
