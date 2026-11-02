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
            // the old chip was cw(40) x cw(12); 1.35 times bigger since 2026-11-02 (Rickard: "gör spelaren lite större"), its middle
            // cw(2) lower so the bigger chip keeps clear of the screen's top; the chip draws itself to fit (OnlineMenu.DrawChip)
            const float k = 1.35f;
            TopLeft(slot, 3 * Cw, cy + 2 * Cw - 6 * Cw * k, 40 * Cw * k, 12 * Cw * k);
            OnlineHooks.HomeChip(slot).gameObject.AddComponent<ButtonFx>().PressScale = false;
            return true;
        }

        /// <summary>
        /// HubPill "hubFriendsTab": the round button with two people. Since 2026-10-31 (Rickard's screenshot review) it sits in the
        /// top bar's empty right corner, level with the profile chip, with "FRIENDS" under it: next to the mode chip nobody knew
        /// what the unmarked circle did, and the mode chip now has the whole row. The badge with the friends online stays.
        /// </summary>
        static void OnlineFriendsPill(RectTransform top, float cy)
        {
            if (OnlineHooks.OpenFriends == null) return;
            float h = 10 * Cw, r = h / 2, cx = 1080 - 4 * Cw - r;
            var rt = UiKit.NewRect("Friends", top);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(h + 2 * Cw, h + 5 * Cw); // the touch area takes the caption too
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var calm = UiKit.NewRect("Body", rt);
            UiKit.Stretch(calm);
            var group = calm.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.85f; // a little calmer than the profile chip, no longer the 0.4 it had next to the hero's platform
            group.blocksRaycasts = false;
            Mid(Pic(calm, "Shadow", UiKit.Circle, new Color(0, 0, 0, 0.3f)).rectTransform, 0.5f, 0.5f, 0, -0.6f * Cw, h, h);
            Mid(Pic(calm, "Face", UiKit.Circle, new Color32(0x15, 0x1a, 0x42, 0xff)).rectTransform, 0.5f, 0.5f, 0, 0, h, h);
            Mid(Pic(calm, "Tint", UiKit.Circle, new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.05f)).rectTransform, 0.5f, 0.5f, 0, 0, h, h);
            float lw = 0.4f * Cw;
            Mid(Pic(calm, "Edge", HomeArt.CircleRing(lw / (r + lw / 2)), new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.5f)).rectTransform, 0.5f, 0.5f, 0, 0, h + lw, h + lw);
            var icon = UiKit.NewRect("People", calm);
            Mid(icon, 0.5f, 0.5f, 0, 0.4f * Cw, 0, 0);
            People(icon, 2.4f * Cw, UiKit.Primary);
            var cap = Treb(calm, "FRIENDS", 2.2f, new Color32(0xcd, 0xd3, 0xfa, 0xff)); // HUB_SOFT
            cap.alignment = TextAnchor.MiddleCenter;
            Mid(cap.rectTransform, 0.5f, 0.5f, 0, -r - 1.7f * Cw, 20 * Cw, 3.5f * Cw);

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

        /// <summary>
        /// Speltest MenuHome3.HubTeam3 / HubInvitePill3 (ONE_PLAY): an empty party place on each side of the hero is a small glassy
        /// "+ INVITE" pill, cw(34) out from the middle, at 0.42 of a side hero's height over its feet (cw(10) behind the player's).
        /// A tap opens the invite panel: friends first, then everyone online (Rickard 2026-10-30). In a party HomeParty.cs moves
        /// them with the zoom and hides the one whose place a party member has taken. Null offline.
        /// </summary>
        static RectTransform[] OnlineInvitePills(RectTransform low, float feetY, float heroH)
        {
            if (OnlineHooks.OpenInvite == null) return null;
            // HubGhost3: the pill at 0.42 of the empty figure's height (0.86 of a side hero's) over its feet
            float sideH = heroH * 0.7f, cy = feetY + 10 * Cw + sideH * 0.86f * 0.42f, h = 7 * Cw;
            var pills = new RectTransform[2];
            for (int side = -1; side <= 1; side += 2)
            {
                var rt = UiKit.NewRect(side < 0 ? "Invite Left" : "Invite Right", low);
                pills[side < 0 ? 0 : 1] = rt;
                var label = Treb(rt, "INVITE", 2.6f, new Color(243 / 255f, 244 / 255f, 1f, 0.92f));
                float w = label.preferredWidth + h + 3 * Cw;
                Mid(rt, 0, 0, 540 + side * 34 * Cw, cy, w + 4 * Cw, h + 6 * Cw); // a touch area a little bigger than the pill
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var pill = UiKit.NewRect("Pill", rt);
                Mid(pill, 0.5f, 0.5f, 0, 0, w, h);
                var group = pill.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.7f; // see-through, so the eye stays on the player's hero
                group.blocksRaycasts = false;
                var fill = Pic(pill, "Fill", UiKit.Rounded, new Color(14 / 255f, 18 / 255f, 48 / 255f, 0.55f));
                UiKit.Stretch(fill.rectTransform);
                Radius(fill, h / 2);
                var edge = Pic(pill, "Edge", HomeArt.RoundRing(0.3f / 3.5f), UiKit.Primary);
                UiKit.Stretch(edge.rectTransform);
                RingRadius(edge, h / 2);
                float px = h / 2 + 0.5f * Cw, s = 1.4f * Cw; // HubPlus: two bars, 2s long and 0.4s thick
                Mid(Pic(pill, "PlusH", null, UiKit.Primary).rectTransform, 0, 0.5f, px, 0, 2 * s, 0.4f * s);
                Mid(Pic(pill, "PlusV", null, UiKit.Primary).rectTransform, 0, 0.5f, px, 0, 0.4f * s, 2 * s);
                label.transform.SetParent(pill, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f);
                label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(h + 0.6f * Cw, 0);
                label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + Cw, h);
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); OnlineHooks.OpenInvite?.Invoke(); });
                rt.gameObject.AddComponent<ButtonFx>();
            }
            return pills;
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
            // PLAY WITH FRIENDS is gone from the sheet (Rickard 2026-10-31): Home's + INVITE beside the hero does the same
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
            if (swords && OnlineHooks.Searching != null) SearchLight(rt);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); CloseModes(); act(); });
            rt.gameObject.AddComponent<ButtonFx>().PressScale = false;
            return rt;
        }

        // Rickard 2026-10-30: "visa en lampa ... när någon spelare söker match? Så man vet och kan köa in i dom": a green light
        // that breathes and "N SEARCHING" on the ONLINE MATCH row while other players are in the queue; pressing the row joins them
        static void SearchLight(RectTransform row)
        {
            var box = UiKit.NewRect("Searching", row);
            box.anchorMin = box.anchorMax = new Vector2(1, 0.5f);
            box.pivot = new Vector2(1, 0.5f);
            box.anchoredPosition = new Vector2(-10 * Cw, 2.3f * Cw); // on the title line: the line under it is long
            var back = Pic(box, "Back", UiKit.Rounded, new Color(0.18f, 0.68f, 0.36f, 0.22f));
            UiKit.Stretch(back.rectTransform);
            Radius(back, 2.6f * Cw);
            var halo = Pic(box, "Halo", UiKit.Circle, new Color(0.37f, 0.89f, 0.6f, 0.35f));
            var dot = Pic(box, "Light", UiKit.Circle, new Color32(0x5f, 0xe3, 0x9a, 0xff));
            var text = Treb(box, "", 2.5f, new Color32(0x8f, 0xff, 0xb0, 0xff));
            var g = box.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            box.gameObject.AddComponent<Searching>().Set(box, halo, dot, text, g);
        }

        /// <summary>The light follows the number of players searching (asked twice a second) and breathes while it is on.</summary>
        sealed class Searching : MonoBehaviour
        {
            RectTransform box; Image halo, dot; Text text; CanvasGroup group; float t; int shown = -1;

            public void Set(RectTransform box, Image halo, Image dot, Text text, CanvasGroup group)
            {
                this.box = box; this.halo = halo; this.dot = dot; this.text = text; this.group = group;
                group.alpha = 0;
            }

            void Update()
            {
                if (shown > 0)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / 1.4f);
                    float d = 1.6f * Cw * (1.6f + 0.9f * k);
                    halo.rectTransform.sizeDelta = new Vector2(d, d);
                    halo.color = new Color(0.37f, 0.89f, 0.6f, 0.45f * (1 - k) + 0.1f);
                }
                if ((t -= Time.unscaledDeltaTime) > 0) return;
                t = 0.5f;
                int n = OnlineHooks.Searching != null ? OnlineHooks.Searching() : 0;
                if (n == shown) return;
                shown = n;
                group.alpha = n > 0 ? 1 : 0;
                if (n <= 0) return;
                text.text = n + " SEARCHING";
                float h = 5.2f * Cw, dx = 2.6f * Cw, w = dx + 1.6f * Cw + text.preferredWidth + 2 * Cw;
                box.sizeDelta = new Vector2(w, h);
                Mid(dot.rectTransform, 0, 0.5f, dx, 0, 1.6f * Cw, 1.6f * Cw);
                Mid(halo.rectTransform, 0, 0.5f, dx, 0, 2.6f * Cw, 2.6f * Cw);
                Mid(text.rectTransform, 0, 0.5f, dx + 1.6f * Cw + text.preferredWidth / 2, 0, text.preferredWidth + Cw, h);
            }
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
