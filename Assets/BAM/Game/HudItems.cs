using BAM.Audio;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The match items (Sim/Items.cs; Rickard 2026-11-11 "välja av tre items lvl 6 och lvl 12"). The choice is the old game's compact
    /// offer (Speltest HudOffer.cs, Rickard 2026-10-24/25): a small glass panel with a gold edge above the controls, three round item
    /// icons with the name in its rarity colour and the effect under each; one tap on an item takes it, no SELECT button. The match
    /// does not stop: the panel waits until the player picks (keys 1-3 in the editor). The items held sit as two small icons left of
    /// the SPECIAL, dark while they cool down, Resonance Core with its Echoes. Pure display and input: the pick goes to the rules
    /// through PlayerInput.ItemPick.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color OfferGlass = new Color(0.03f, 0.05f, 0.13f, 0.78f), OfferGold = new Color(0.91f, 0.71f, 0.31f, 0.85f);
        // the old game's rarity colours: the disc behind the icon and the name (RARITY_BG, RARITY_COL)
        static readonly Color[] RarityBg = { Hex(0x4c7a98), Hex(0x00d98f), Hex(0xf500ff), Hex(0xffc004) };
        static readonly Color[] RarityText = { Hex(0xb9d6ea), Hex(0x5cf0b8), Hex(0xf08cff), Hex(0xffd75a) };

        RectTransform offerRoot;
        readonly RectTransform[] offerCol = new RectTransform[ItemS.Choices];
        readonly Image[] offerDisc = new Image[ItemS.Choices], offerIcon = new Image[ItemS.Choices];
        readonly Text[] offerName = new Text[ItemS.Choices], offerText = new Text[ItemS.Choices];
        Text offerTitle;
        readonly ItemId[] offerShown = new ItemId[ItemS.Choices];
        int offerShownCount = -1;
        float offerShownAt = -99f;
        byte itemPickQueued;

        readonly RectTransform[] heldRoot = new RectTransform[Unit.MaxItems];
        readonly Image[] heldIcon = new Image[Unit.MaxItems], heldCd = new Image[Unit.MaxItems];
        readonly Text[] heldCount = new Text[Unit.MaxItems];
        readonly ItemId[] heldShown = new ItemId[Unit.MaxItems];
        readonly float[] heldPulse = new float[Unit.MaxItems];

        static readonly Sprite[] itemSprites = new Sprite[ItemCatalog.Count + 1];
        static Sprite ItemSprite(ItemId id)
        {
            int k = (int)id; if (k <= 0 || k > ItemCatalog.Count) return null;
            if (itemSprites[k] != null) return itemSprites[k];
            var tex = Resources.Load<Texture2D>("BAM/Hud/" + ItemCatalog.Get(id).Icon);
            if (tex == null) { Debug.LogWarning("[BAM.Game] item icon missing: " + ItemCatalog.Get(id).Icon); return null; }
            return itemSprites[k] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        void BuildItems()
        {
            // the offer panel: a raycast target of its own, so a touch on its glass never reaches the joystick under it
            offerRoot = UiKit.NewRect("ItemOffer", safe);
            offerRoot.anchorMin = offerRoot.anchorMax = offerRoot.pivot = new Vector2(0.5f, 0.5f);
            var glass = UiKit.Fill(offerRoot, OfferGlass, UiKit.Rounded);
            glass.type = Image.Type.Sliced; glass.raycastTarget = true;
            var edge = UiKit.Fill(offerRoot, OfferGold, UiKit.Ring);
            edge.type = Image.Type.Sliced; edge.raycastTarget = false;
            offerTitle = UiKit.Label(offerRoot, "CHOOSE AN ITEM", 26, TextAnchor.UpperCenter, UiKit.Gold);
            offerTitle.raycastTarget = false;
            for (int i = 0; i < ItemS.Choices; i++)
            {
                int idx = i;
                var col = offerCol[i] = UiKit.NewRect("Choice" + i, offerRoot);
                col.anchorMin = col.anchorMax = col.pivot = new Vector2(0.5f, 0.5f);
                var hit = col.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                var tap = col.gameObject.AddComponent<EmoteButton>(); // down and up in the safe area's coordinates, fingers read directly
                tap.Up = _ => OfferTapped(idx);
                var disc = offerDisc[i] = UiKit.Fill(col, Color.white, UiKit.Circle);
                disc.raycastTarget = false; disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                var icon = offerIcon[i] = UiKit.Fill(col, Color.white);
                icon.raycastTarget = false; icon.preserveAspect = true; icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                var nm = offerName[i] = UiKit.Label(col, "", 28, TextAnchor.UpperCenter, Color.white);
                nm.raycastTarget = false; nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 16; nm.resizeTextMaxSize = 28;
                UiKit.AddOutline(nm, 2);
                var tx = offerText[i] = UiKit.Label(col, "", 22, TextAnchor.UpperCenter, new Color(0.9f, 0.93f, 1f, 0.9f));
                tx.raycastTarget = false; tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Truncate;
                tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 14; tx.resizeTextMaxSize = 22;
            }
            offerRoot.gameObject.SetActive(false);

            // the items held: small round icons left of the SPECIAL
            for (int i = 0; i < Unit.MaxItems; i++)
            {
                var r = heldRoot[i] = UiKit.NewRect("Item" + i, safe);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                var back = UiKit.Fill(r, new Color(0.03f, 0.05f, 0.13f, 0.7f), UiKit.Circle); back.raycastTarget = false;
                var ic = heldIcon[i] = UiKit.Fill(r, Color.white); ic.raycastTarget = false; ic.preserveAspect = true;
                var cd = heldCd[i] = UiKit.Fill(r, new Color(0f, 0f, 0f, 0.6f), UiKit.Circle);
                cd.raycastTarget = false; cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
                cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = false;
                var n = heldCount[i] = UiKit.Label(r, "", 30, TextAnchor.LowerRight, Hex(0xbff3ff));
                n.raycastTarget = false; UiKit.AddOutline(n, 2);
                r.gameObject.SetActive(false);
            }
        }

        void BeginItems()
        {
            itemPickQueued = 0; offerShownCount = -1; offerShownAt = -99f;
            for (int i = 0; i < Unit.MaxItems; i++) { heldShown[i] = ItemId.None; heldPulse[i] = -99f; }
            if (offerRoot != null) offerRoot.gameObject.SetActive(false);
            for (int i = 0; i < Unit.MaxItems; i++) if (heldRoot[i] != null) heldRoot[i].gameObject.SetActive(false);
        }

        void OfferTapped(int i)
        {
            var p = m.Player;
            if (p == null || i >= p.OfferCount || itemPickQueued != 0) return;
            if (Time.unscaledTime - offerShownAt < 0.35f) return; // a tap already under way when the panel came does not take one
            itemPickQueued = (byte)(i + 1);
            BamAudio.Play(SfxId.UiTap, 1f);
        }

        byte TakeItemPick() { var b = itemPickQueued; itemPickQueued = 0; return b; }

        void ItemsFrame(bool hidden)
        {
            var p = m.Player;
            bool live = !hidden && p != null && m.ItemsOn && m.State == MatchState.Play;
            var kb = Keyboard.current;
            if (live && kb != null && p.OfferCount > 0)
            {
                if (kb.digit1Key.wasPressedThisFrame) OfferTapped(0);
                else if (kb.digit2Key.wasPressedThisFrame) OfferTapped(1);
                else if (kb.digit3Key.wasPressedThisFrame) OfferTapped(2);
            }
            OfferFrame(live && p.OfferCount > 0 && itemPickQueued == 0);
            HeldFrame(live);
        }

        void OfferFrame(bool show)
        {
            if (offerRoot.gameObject.activeSelf != show) offerRoot.gameObject.SetActive(show);
            if (!show) { offerShownCount = -1; return; }
            var p = m.Player; int n = p.OfferCount;
            bool fresh = offerShownCount != n;
            for (int i = 0; i < n && !fresh; i++) if (offerShown[i] != p.Offer[i]) fresh = true;
            float W = SafeW, w = W * 0.66f, h = W * 0.4f;
            Vector3 c = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
            float x = safe.rect.xMin + W * 0.4f, y = c.y + SpecialButton.Radius + W * 0.06f + h / 2;
            if (fresh)
            {
                offerShownCount = n; offerShownAt = Time.unscaledTime;
                offerRoot.SetAsLastSibling();
                offerRoot.sizeDelta = new Vector2(w, h);
                offerTitle.rectTransform.anchorMin = new Vector2(0f, 1f); offerTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
                offerTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
                offerTitle.rectTransform.sizeDelta = new Vector2(0f, W * 0.05f); offerTitle.rectTransform.anchoredPosition = new Vector2(0f, -W * 0.012f);
                float cw = w / ItemS.Choices, r = W * 0.06f;
                for (int i = 0; i < ItemS.Choices; i++)
                {
                    bool on = i < n; offerCol[i].gameObject.SetActive(on); if (!on) continue;
                    var id = offerShown[i] = p.Offer[i]; var info = ItemCatalog.Get(id);
                    offerCol[i].sizeDelta = new Vector2(cw - W * 0.01f, h - W * 0.05f);
                    offerCol[i].localPosition = new Vector3(-w / 2 + cw * (i + 0.5f), -W * 0.022f, 0f);
                    float top = -W * 0.012f - r;
                    var dr = offerDisc[i].rectTransform; dr.sizeDelta = new Vector2(2f * r, 2f * r); dr.anchoredPosition = new Vector2(0f, top);
                    offerDisc[i].color = RarityBg[info.Rarity];
                    var ir = offerIcon[i].rectTransform; ir.sizeDelta = new Vector2(2.3f * r, 2.3f * r); ir.anchoredPosition = new Vector2(0f, top);
                    offerIcon[i].sprite = ItemSprite(id); offerIcon[i].enabled = offerIcon[i].sprite != null;
                    var nr = offerName[i].rectTransform; nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0.5f, 1f);
                    nr.sizeDelta = new Vector2(0f, W * 0.032f); nr.anchoredPosition = new Vector2(0f, top - r * 1.2f);
                    offerName[i].text = info.Name; offerName[i].color = RarityText[info.Rarity];
                    var tr = offerText[i].rectTransform; tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
                    tr.offsetMin = new Vector2(W * 0.006f, W * 0.006f); tr.offsetMax = new Vector2(-W * 0.006f, top - r * 1.2f - W * 0.034f);
                    offerText[i].text = info.Text;
                }
            }
            // slides up and fades in over 0.22 s, as the old panel
            float k = Mathf.Clamp01((Time.unscaledTime - offerShownAt) / 0.22f), e = 1f - (1f - k) * (1f - k) * (1f - k);
            offerRoot.localPosition = new Vector3(x, y - (1f - e) * W * 0.06f, 0f);
            offerRoot.localScale = Vector3.one * (0.96f + 0.04f * e);
        }

        void HeldFrame(bool live)
        {
            var p = m.Player;
            var ev = m.Events;
            if (live)
                for (int k = 0; k < ev.Count; k++)
                    if (ev[k].Type == EventType.ItemProc && ev[k].A == p)
                        for (int i = 0; i < p.ItemCount; i++) if ((byte)p.Items[i].Id == ev[k].Kind) heldPulse[i] = Time.unscaledTime;
            float W = SafeW, r = W * 0.042f;
            Vector3 c = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
            for (int i = 0; i < Unit.MaxItems; i++)
            {
                bool show = live && i < p.ItemCount;
                if (heldRoot[i].gameObject.activeSelf != show) heldRoot[i].gameObject.SetActive(show);
                if (!show) continue;
                var it = p.Items[i];
                if (heldShown[i] != it.Id) { heldShown[i] = it.Id; heldIcon[i].sprite = ItemSprite(it.Id); heldIcon[i].enabled = heldIcon[i].sprite != null; }
                // left of the SPECIAL, one above the other
                heldRoot[i].localPosition = new Vector3(c.x - SpecialButton.Radius - W * 0.03f - r, c.y + (i == 0 ? 1.15f : -1.15f) * r, 0f);
                float pulse = Mathf.Clamp01(1f - (Time.unscaledTime - heldPulse[i]) / 0.35f);
                heldRoot[i].sizeDelta = new Vector2(2f * r, 2f * r);
                heldRoot[i].localScale = Vector3.one * (1f + 0.25f * pulse);
                float cd = ItemCooldown(it.Id), left = it.ReadyAt - m.Time;
                heldCd[i].fillAmount = cd > 0 && left > 0 ? Mathf.Clamp01(left / cd) : 0f;
                int echoes = it.Id == ItemId.Echo ? it.Stacks : 0;
                heldCount[i].text = echoes > 0 ? EchoText[Mathf.Min(echoes, EchoText.Length - 1)] : "";
            }
        }
        static readonly string[] EchoText = { "", "1", "2" };

        // ---------- the marks by the health bars (the old HudItemsV2.cs), kept few and plain so they read on a phone ----------
        //   Resonance Core: one cyan diamond per stored Echo left on the bar, for every hero holding it
        //   Quarry Mark: its icon over a marked enemy while the mark lasts (everyone sees it: it tells the team whom to hit)
        //   Thicket Fang: its icon over the holder while its next hero hit is armed (own team only: the enemy should not know)
        static readonly Color EchoCyan = Hex(0x8fe9ff);

        void BuildItemMarks(Overhead o)
        {
            var mk = UiKit.NewRect("ItemMark", o.Root);
            mk.anchorMin = mk.anchorMax = mk.pivot = new Vector2(0.5f, 0.5f);
            o.ItemMark = mk.gameObject.AddComponent<Image>(); o.ItemMark.raycastTarget = false; o.ItemMark.preserveAspect = true; o.ItemMark.enabled = false;
            for (int k = 0; k < o.Echo.Length; k++)
            {
                var d = UiKit.NewRect("Echo" + k, o.Root);
                d.anchorMin = d.anchorMax = d.pivot = new Vector2(0.5f, 0.5f);
                d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var img = o.Echo[k] = d.gameObject.AddComponent<Image>(); img.raycastTarget = false; img.color = EchoCyan; img.enabled = false;
                var edge = img.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(0.04f, 0.12f, 0.24f, 0.9f); edge.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        void ItemMarksFrame(Overhead o, Unit u, float ts, float bw)
        {
            int echoes = u.IsClone ? 0 : u.Echoes;
            for (int k = 0; k < o.Echo.Length; k++)
            {
                bool on = k < echoes; if (o.Echo[k].enabled != on) o.Echo[k].enabled = on;
                if (!on) continue;
                float side = ts * 0.12f;
                o.Echo[k].rectTransform.sizeDelta = new Vector2(side, side);
                o.Echo[k].rectTransform.anchoredPosition = new Vector2(-bw * ts / 2f - 8f - k * side * 1.6f, 0f);
            }
            bool marked = false;
            for (int s = 0; s < u.StatusCount; s++) { var st = u.GetStatus(s); if (st.Type == St.QuarryMark && st.T > 0f) { marked = true; break; } }
            bool fang = !marked && !u.IsClone && u.Team == m.Player.Team && u.FangArmed(m.Time);
            ItemId want = marked ? ItemId.Mark : fang ? ItemId.Fang : ItemId.None;
            bool show = want != ItemId.None;
            if (o.ItemMark.enabled != show) o.ItemMark.enabled = show;
            if (!show) return;
            var sp = ItemSprite(want); if (o.ItemMark.sprite != sp) o.ItemMark.sprite = sp;
            float r = ts * 0.26f * (marked ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 9f) : 1f);
            o.ItemMark.rectTransform.sizeDelta = new Vector2(2f * r, 2f * r);
            o.ItemMark.rectTransform.anchoredPosition = new Vector2(-bw * ts / 2f - r - 6f, ts * 0.32f);
        }

        static float ItemCooldown(ItemId id)
        {
            switch (id)
            {
                case ItemId.Mark: return ItemS.MarkCd;
                case ItemId.Fang: return ItemS.FangCd;
                case ItemId.Thread: return ItemS.ThreadCd;
                case ItemId.Rally: return ItemS.RallyCd;
                case ItemId.Charm: return ItemS.CharmCd;
            }
            return 0f;
        }
    }
}
