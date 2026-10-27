using System;
using BAM.Audio;
using BAM.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The online entries in the menus (2026-10-27, only in BAM_ONLINE builds, when BAM.Online.UI has filled OnlineHooks): the old
    // Home's profile chip in the top bar (Speltest MenuHome3.HubTopBar3, drawn by BAM.Online.UI), its round FRIENDS button above
    // the bottom row (MenuHome.HubPills, ONE_PLAY look: calm until used, a badge with the friends online), and in the mode sheet
    // two rows that act at once like the old SERVER row: ONLINE MATCH (the old "1v1" sign and line) and PLAY WITH FRIENDS.
    // After the first finished match that is not the tutorial, Home asks once to go online (OnlineHooks.AskToGoOnline).
    // Offline every hook is null and none of this is built.
    public static partial class Screens
    {
        static bool askOnline;

        // ---------------------------------------------------------------- Home

        /// <summary>The top bar's profile: the online chip instead of the hero's picture and name. False offline.</summary>
        static bool OnlineChip(RectTransform top, float cy)
        {
            if (OnlineHooks.HomeChip == null) return false;
            var slot = UiKit.NewRect("Profile", top);
            TopLeft(slot, 3 * Cw, cy - 6 * Cw, 40 * Cw, 12 * Cw); // the old chip: cw(3) in, cw(40) x cw(12), on the bar's middle
            OnlineHooks.HomeChip(slot).gameObject.AddComponent<ButtonFx>().PressScale = false;
            return true;
        }

        /// <summary>HubPill "hubFriendsTab": the round button with two people, its right edge cw(4) from the side, cw(3) above the bottom row.</summary>
        static void OnlineFriendsPill(RectTransform low, float rowTop)
        {
            if (OnlineHooks.OpenFriends == null) return;
            float h = 11 * Cw, r = h / 2, cx = 1080 - 4 * Cw - r, cy = rowTop + 3 * Cw + r;
            var rt = UiKit.NewRect("Friends", low);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.sizeDelta = new Vector2(h + 2 * Cw, h + 2 * Cw); // the 52 px touch area
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var calm = UiKit.NewRect("Body", rt);
            UiKit.Stretch(calm);
            var group = calm.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.4f; // ONE_PLAY: calmer until used, so the platform reads clean
            group.blocksRaycasts = false;
            Mid(Pic(calm, "Shadow", UiKit.Circle, new Color(0, 0, 0, 0.3f)).rectTransform, 0.5f, 0.5f, 0, -0.6f * Cw, h, h);
            Mid(Pic(calm, "Face", UiKit.Circle, new Color32(0x15, 0x1a, 0x42, 0xff)).rectTransform, 0.5f, 0.5f, 0, 0, h, h);
            Mid(Pic(calm, "Tint", UiKit.Circle, new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.05f)).rectTransform, 0.5f, 0.5f, 0, 0, h, h);
            float lw = 0.4f * Cw;
            Mid(Pic(calm, "Edge", HomeArt.CircleRing(lw / (r + lw / 2)), new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.5f)).rectTransform, 0.5f, 0.5f, 0, 0, h + lw, h + lw);
            var icon = UiKit.NewRect("People", calm);
            Mid(icon, 0.5f, 0.5f, 0, 0.4f * Cw, 0, 0);
            People(icon, 2.6f * Cw, UiKit.Primary);

            // the badge: friends online, on the top-left corner (the button is on the right side), fully visible
            var badge = UiKit.NewRect("Badge", rt);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0, 1);
            var ring = Pic(badge, "Ring", UiKit.Rounded, new Color32(0x0e, 0x12, 0x30, 0xff));
            var fill = Pic(badge, "Fill", UiKit.Rounded, UiKit.Primary);
            var num = Treb(badge, "", 2.4f, new Color32(0x0e, 0x12, 0x30, 0xff));
            num.alignment = TextAnchor.MiddleCenter;
            var count = rt.gameObject.AddComponent<FriendsBadge>();
            count.Set(badge, ring, fill, num, -r, r + 1.2f * Cw);

            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); OnlineHooks.OpenFriends?.Invoke(); });
            rt.gameObject.AddComponent<ButtonFx>();
        }

        /// <summary>The badge follows the number of friends online (asked twice a second).</summary>
        sealed class FriendsBadge : MonoBehaviour
        {
            RectTransform box; Image ring, fill; Text num; float left, top, t; int shown = -1;

            public void Set(RectTransform box, Image ring, Image fill, Text num, float left, float top)
            {
                this.box = box; this.ring = ring; this.fill = fill; this.num = num; this.left = left; this.top = top;
                box.gameObject.SetActive(false);
            }

            void Update()
            {
                if ((t -= Time.unscaledDeltaTime) > 0) return;
                t = 0.5f;
                int n = OnlineHooks.FriendsOnline != null ? OnlineHooks.FriendsOnline() : 0;
                if (n == shown) return;
                shown = n;
                box.gameObject.SetActive(n > 0);
                if (n <= 0) return;
                num.text = n > 9 ? "9+" : n.ToString();
                float w = Mathf.Max(4.6f * Cw, num.preferredWidth + 2.4f * Cw), h = 4.6f * Cw;
                box.anchoredPosition = new Vector2(left - 0.5f * Cw, top);
                box.sizeDelta = new Vector2(w, h);
                Stretch(ring.rectTransform, -0.4f * Cw); Radius(ring, 2.7f * Cw);
                Stretch(fill.rectTransform, 0); Radius(fill, 2.3f * Cw);
                Stretch(num.rectTransform, 0);
            }

            static void Stretch(RectTransform r, float out_) { UiKit.Stretch(r); r.offsetMin = new Vector2(-out_, -out_); r.offsetMax = new Vector2(out_, out_); }
        }

        /// <summary>After a finished match (not the tutorial) the next Home asks once to go online.</summary>
        static void NoteMatchForOnline(MatchResult r) { if (!r.Tutorial && !r.Quit) askOnline = true; }

        static void AskOnlineOnce()
        {
            if (!askOnline || OnlineHooks.AskToGoOnline == null) return;
            askOnline = false;
            OnlineHooks.AskToGoOnline(); // shows the question the first time only
        }

        // ---------------------------------------------------------------- the mode sheet

        static RectTransform[] onlineRows = new RectTransform[0];

        static int OnlineRowCount => onlineRows.Length;

        static void BuildOnlineRows()
        {
            var list = new System.Collections.Generic.List<RectTransform>();
            if (OnlineHooks.OnlineMatch != null) list.Add(OnlineRow("ONLINE MATCH", "Quick match against another player", true, OnlineHooks.OnlineMatch));
            if (OnlineHooks.OpenPlayFriends != null) list.Add(OnlineRow("PLAY WITH FRIENDS", "Invite friends: together or against each other", false, OnlineHooks.OpenPlayFriends));
            onlineRows = list.ToArray();
        }

        /// <summary>The cards under the modes, from y down; returns the y under them.</summary>
        static float LayoutOnlineRows(float y)
        {
            foreach (var row in onlineRows) { Stretched(row, SheetX, y, SheetX, OptH); y += OptH + OptGap; }
            return y;
        }

        // the old SERVER row: a card like a mode not chosen (quiet gold line, dim icon, chevron) that acts at once
        static RectTransform OnlineRow(string title, string sub, bool swords, Action act)
        {
            var rt = UiKit.NewRect(title, modeSheet);
            var fill = Pic(rt, "Fill", UiKit.Rounded, new Color(1, 1, 1, 0.04f));
            UiKit.Stretch(fill.rectTransform);
            Radius(fill, 3.4f * Cw);
            fill.raycastTarget = true;
            var off = Pic(rt, "Quiet", HomeArt.RoundRing(0.3f / 3.4f), new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.35f));
            UiKit.Stretch(off.rectTransform);
            RingRadius(off, 3.4f * Cw);
            var icon = UiKit.NewRect("Icon", rt);
            Mid(icon, 0, 0.5f, 3 * Cw + 5 * Cw, 0, 10 * Cw, 10 * Cw);
            var ig = icon.gameObject.AddComponent<CanvasGroup>();
            ig.alpha = 0.45f; // dim like the other modes not chosen
            ig.blocksRaycasts = false;
            OnlineIcon(icon, swords, 10 * Cw);
            float tx = 3 * Cw + 5 * Cw + 8 * Cw;
            var name = UiKit.Label(rt, title, Mathf.RoundToInt(4.4f * Cw), TextAnchor.MiddleLeft, HubText);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Mid(name.rectTransform, 0, 0.5f, tx + 30 * Cw, 2.3f * Cw, 60 * Cw, 6 * Cw);
            var line = Treb(rt, sub, 2.9f, new Color32(0xc6, 0xca, 0xe6, 0xff));
            Mid(line.rectTransform, 0, 0.5f, tx + 30 * Cw, -2.9f * Cw, 60 * Cw, 5 * Cw);
            var chev = UiKit.NewRect("Chevron", rt);
            Mid(chev, 1, 0.5f, -6 * Cw, 0, 3 * Cw, 3 * Cw);
            ModeChevron(chev, 1.4f * Cw, UiKit.TextDim);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); CloseModes(); act(); });
            rt.gameObject.AddComponent<ButtonFx>().PressScale = false;
            return rt;
        }

        // HubModeIcon("1v1"): two crossed swords on the dark square; the friends row has the two people of the FRIENDS button
        static void OnlineIcon(RectTransform box, bool swords, float s)
        {
            var back = Pic(box, "Back", UiKit.Rounded, new Color32(0x22, 0x2a, 0x62, 0xff));
            Radius(back, s * 0.26f);
            Mid(back.rectTransform, 0.5f, 0.5f, 0, 0, s, s);
            float k = s * 0.3f;
            if (!swords) { var c = UiKit.NewRect("People", box); Mid(c, 0.5f, 0.5f, 0, 0.4f * k, 0, 0); People(c, 0.8f * k, UiKit.Primary); return; }
            for (int d = -1; d <= 1; d += 2)
            {
                // the old canvas y goes down; here up
                Line(box, -d * k, k, d * k * 0.9f, -k * 0.9f, k * 0.28f, UiKit.Primary);
                Line(box, d * k * 0.45f, -k * 0.95f, d * k * 1.05f, -k * 0.35f, k * 0.28f, UiKit.Primary);
            }
        }

        static void Line(RectTransform box, float x0, float y0, float x1, float y1, float w, Color c)
        {
            float dx = x1 - x0, dy = y1 - y0;
            var img = Pic(box, "Line", null, c);
            Mid(img.rectTransform, 0.5f, 0.5f, (x0 + x1) / 2, (y0 + y1) / 2, Mathf.Sqrt(dx * dx + dy * dy), w);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        // HubPeople: a friend behind (half see-through) and one in front, each a head and the top half of an oval
        static void People(RectTransform at, float s, Color col)
        {
            var dim = new Color(col.r, col.g, col.b, col.a * 0.55f);
            Blob(at, s * 0.9f, s * 0.35f, s * 0.6f, s * 0.6f, dim, false);
            Blob(at, s * 0.9f, -s * 1.05f, s * 0.95f, s * 0.6f, dim, true);
            Blob(at, -s * 0.2f, s * 0.2f, s * 0.75f, s * 0.75f, col, false);
            Blob(at, -s * 0.2f, -s * 1.35f, s * 1.15f, s * 0.75f, col, true);
        }

        static void Blob(RectTransform at, float x, float y, float rx, float ry, Color c, bool topHalf)
        {
            var img = Pic(at, topHalf ? "Body" : "Head", UiKit.Circle, c);
            Mid(img.rectTransform, 0.5f, 0.5f, x, y, 2 * rx, 2 * ry);
            if (!topHalf) return;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Vertical;
            img.fillOrigin = (int)Image.OriginVertical.Top;
            img.fillAmount = 0.5f;
        }
    }
}
