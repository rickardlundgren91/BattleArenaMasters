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
    /// The match items (Sim/Items.cs; Rickard 2026-11-11 "välja av tre items lvl 6 och lvl 12"). The choice: three item cards side
    /// by side in the upper half of the field, under a "CHOOSE AN ITEM" title (Rickard 2026-11-11 21:13 "FÖR dåligt med shoppen ...
    /// städa upp det ui": the first panel sat low over the joystick and the SPECIAL with tiny text). Each card has the item's own
    /// icon (drawn in code, ItemIconArt), its name, its rarity and one short line; a tap anywhere on a card takes it, no SELECT
    /// button. The match does not stop: the cards wait until the player picks (keys 1-3 in the editor), and they keep clear of
    /// every control. The items held sit as two small icons left of the SPECIAL, dark while they cool down, Resonance Core with
    /// its Echoes. Pure display and input: the pick goes to the rules through PlayerInput.ItemPick.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color CardGlass = new Color(0.035f, 0.05f, 0.13f, 0.9f), CardText = new Color(0.86f, 0.89f, 0.98f, 1f);
        // rarity colours for the card's edge, glow and rarity word (common, rare, epic, legendary)
        static readonly Color[] RarityEdge = { Hex(0x8fb4d4), Hex(0x2fe0a0), Hex(0xd77bff), Hex(0xffc83a) };
        static readonly string[] RarityWord = { "COMMON", "RARE", "EPIC", "LEGENDARY" };

        RectTransform offerRoot, offerTitleRt;
        readonly RectTransform[] offerCard = new RectTransform[ItemS.Choices];
        readonly Image[] offerEdge = new Image[ItemS.Choices], offerGlow = new Image[ItemS.Choices], offerIcon = new Image[ItemS.Choices];
        readonly Text[] offerName = new Text[ItemS.Choices], offerRarity = new Text[ItemS.Choices], offerText = new Text[ItemS.Choices];
        Text offerTitle;
        readonly ItemId[] offerShown = new ItemId[ItemS.Choices];
        int offerShownCount = -1, offerPressed = -1;
        float offerShownAt = -99f, offerLaidW = -1f;
        byte itemPickQueued;

        readonly RectTransform[] heldRoot = new RectTransform[Unit.MaxItems];
        readonly Image[] heldIcon = new Image[Unit.MaxItems], heldCd = new Image[Unit.MaxItems];
        readonly Text[] heldCount = new Text[Unit.MaxItems];
        readonly ItemId[] heldShown = new ItemId[Unit.MaxItems];
        readonly float[] heldPulse = new float[Unit.MaxItems];

        // the icons, drawn once per item in code (ItemIconArt) and kept for the app's life
        const int IconPx = 160;
        static readonly Sprite[] itemSprites = new Sprite[ItemCatalog.Count + 1];
        static Sprite ItemSprite(ItemId id)
        {
            int k = (int)id; if (k <= 0 || k > ItemCatalog.Count) return null;
            if (itemSprites[k] != null) return itemSprites[k];
            var tex = new Texture2D(IconPx, IconPx, TextureFormat.RGBA32, false) { name = "BAM.Item." + id, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.LoadRawTextureData(ItemIconArt.Render(k, ItemCatalog.Rarity(id), IconPx));
            tex.Apply(false, true);
            return itemSprites[k] = Sprite.Create(tex, new Rect(0, 0, IconPx, IconPx), new Vector2(0.5f, 0.5f), 100f);
        }

        void BuildItems()
        {
            offerRoot = UiKit.NewRect("ItemOffer", safe);
            offerRoot.anchorMin = offerRoot.anchorMax = offerRoot.pivot = new Vector2(0.5f, 0.5f);
            offerRoot.sizeDelta = Vector2.zero;
            // the title: a dark pill with a gold edge
            offerTitleRt = UiKit.NewRect("Title", offerRoot);
            offerTitleRt.anchorMin = offerTitleRt.anchorMax = offerTitleRt.pivot = new Vector2(0.5f, 0.5f);
            var pill = UiKit.Fill(offerTitleRt, CardGlass, UiKit.Rounded); pill.type = Image.Type.Sliced; pill.raycastTarget = false;
            var pillEdge = UiKit.Fill(offerTitleRt, UiKit.Trim, UiKit.Ring); pillEdge.type = Image.Type.Sliced; pillEdge.raycastTarget = false;
            offerTitle = UiKit.Label(offerTitleRt, "CHOOSE AN ITEM", 30, TextAnchor.MiddleCenter, UiKit.Gold);
            offerTitle.fontStyle = FontStyle.Bold; offerTitle.raycastTarget = false;
            UiKit.Stretch(offerTitle.rectTransform);
            for (int i = 0; i < ItemS.Choices; i++)
            {
                int idx = i;
                var card = offerCard[i] = UiKit.NewRect("Card" + i, offerRoot);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                var bg = UiKit.Fill(card, CardGlass, UiKit.Rounded); bg.type = Image.Type.Sliced; bg.raycastTarget = true; // the whole card takes the tap
                var tap = card.gameObject.AddComponent<EmoteButton>(); // fingers read directly, as the other match controls
                tap.Down = _ => offerPressed = idx;
                tap.Up = _ => { offerPressed = -1; OfferTapped(idx); };
                var glow = offerGlow[i] = UiKit.Fill(card, Color.white, UiKit.Rounded); glow.type = Image.Type.Sliced; glow.raycastTarget = false;
                var edge = offerEdge[i] = UiKit.Fill(card, Color.white, UiKit.Ring); edge.type = Image.Type.Sliced; edge.raycastTarget = false;
                var icon = offerIcon[i] = UiKit.Fill(card, Color.white); icon.raycastTarget = false; icon.preserveAspect = true;
                TopCentre(icon.rectTransform);
                var nm = offerName[i] = UiKit.Label(card, "", 34, TextAnchor.MiddleCenter, Color.white);
                nm.fontStyle = FontStyle.Bold; nm.raycastTarget = false; nm.horizontalOverflow = HorizontalWrapMode.Wrap; nm.verticalOverflow = VerticalWrapMode.Truncate;
                nm.resizeTextForBestFit = true; nm.lineSpacing = 0.9f;
                UiKit.AddOutline(nm, 2); TopCentre(nm.rectTransform);
                var rw = offerRarity[i] = UiKit.Label(card, "", 22, TextAnchor.MiddleCenter, Color.white);
                rw.fontStyle = FontStyle.Bold; rw.raycastTarget = false; TopCentre(rw.rectTransform);
                var tx = offerText[i] = UiKit.Label(card, "", 26, TextAnchor.UpperCenter, CardText);
                tx.raycastTarget = false; tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Truncate;
                tx.resizeTextForBestFit = true; tx.lineSpacing = 0.95f; TopCentre(tx.rectTransform);
            }
            offerRoot.gameObject.SetActive(false);

            // the items held: small round icons left of the SPECIAL
            for (int i = 0; i < Unit.MaxItems; i++)
            {
                var r = heldRoot[i] = UiKit.NewRect("Item" + i, safe);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                var ic = heldIcon[i] = UiKit.Fill(r, Color.white); ic.raycastTarget = false; ic.preserveAspect = true;
                var cd = heldCd[i] = UiKit.Fill(r, new Color(0f, 0f, 0f, 0.6f), UiKit.Circle);
                cd.raycastTarget = false; cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
                cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = false;
                var n = heldCount[i] = UiKit.Label(r, "", 30, TextAnchor.LowerRight, Hex(0xbff3ff));
                n.raycastTarget = false; UiKit.AddOutline(n, 2);
                r.gameObject.SetActive(false);
            }
        }

        // a child laid out from its parent's top centre
        static void TopCentre(RectTransform rt) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f); }

        void BeginItems()
        {
            itemPickQueued = 0; offerShownCount = -1; offerShownAt = -99f; offerPressed = -1; offerLaidW = -1f;
            for (int i = 0; i < Unit.MaxItems; i++) { heldShown[i] = ItemId.None; heldPulse[i] = -99f; }
            if (offerRoot != null) offerRoot.gameObject.SetActive(false);
            for (int i = 0; i < Unit.MaxItems; i++) if (heldRoot[i] != null) heldRoot[i].gameObject.SetActive(false);
        }

        void OfferTapped(int i)
        {
            var p = m.Player;
            if (p == null || i >= p.OfferCount || itemPickQueued != 0) return;
            if (Time.unscaledTime - offerShownAt < 0.35f) return; // a tap already under way when the cards came does not take one
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
            if (!show) { offerShownCount = -1; offerPressed = -1; return; }
            var p = m.Player; int n = p.OfferCount;
            bool fresh = offerShownCount != n;
            for (int i = 0; i < n && !fresh; i++) if (offerShown[i] != p.Offer[i]) fresh = true;
            float W = SafeW;
            // the cards: 0.29 W wide with 0.025 W between, 0.5 W tall, the title 0.42 W under the safe area's top (under the score, the
            // team portraits, the kill feed and the menu and heart buttons), so they cover only the field's upper half and never a control
            float cw = W * 0.29f, gap = W * 0.025f, ch = W * 0.5f, titleH = W * 0.075f;
            float top = safe.rect.yMax - W * 0.42f, cardsY = top - titleH - W * 0.02f - ch / 2f, cx = safe.rect.center.x;
            if (fresh)
            {
                offerShownCount = n; offerShownAt = Time.unscaledTime; offerPressed = -1;
                offerRoot.SetAsLastSibling();
                for (int i = 0; i < ItemS.Choices; i++)
                {
                    bool on = i < n; offerCard[i].gameObject.SetActive(on); if (!on) continue;
                    var id = offerShown[i] = p.Offer[i]; var info = ItemCatalog.Get(id); var rc = RarityEdge[info.Rarity];
                    offerEdge[i].color = rc;
                    offerGlow[i].color = new Color(rc.r, rc.g, rc.b, 0.22f);
                    offerIcon[i].sprite = ItemSprite(id);
                    offerName[i].text = info.Name;
                    offerRarity[i].text = RarityWord[info.Rarity]; offerRarity[i].color = rc;
                    offerText[i].text = info.Short;
                }
                offerLaidW = -1f;
            }
            if (offerLaidW != W)
            {   // sizes follow the screen: laid out again only when it changes
                offerLaidW = W;
                offerTitleRt.sizeDelta = new Vector2(W * 0.5f, titleH);
                offerTitle.fontSize = Mathf.RoundToInt(W * 0.042f);
                int nameMax = Mathf.RoundToInt(W * 0.045f), textMax = Mathf.RoundToInt(W * 0.036f);
                for (int i = 0; i < ItemS.Choices; i++)
                {
                    offerCard[i].sizeDelta = new Vector2(cw, ch);
                    offerGlow[i].rectTransform.anchorMin = new Vector2(0f, 0.55f); offerGlow[i].rectTransform.anchorMax = Vector2.one;
                    offerGlow[i].rectTransform.offsetMin = new Vector2(W * 0.008f, 0f); offerGlow[i].rectTransform.offsetMax = new Vector2(-W * 0.008f, -W * 0.008f);
                    UiKit.Stretch(offerEdge[i].rectTransform);
                    float ir = W * 0.19f;
                    offerIcon[i].rectTransform.sizeDelta = new Vector2(ir, ir); offerIcon[i].rectTransform.anchoredPosition = new Vector2(0f, -W * 0.02f - ir / 2f);
                    float y = -W * 0.02f - ir - W * 0.005f;
                    var nr = offerName[i].rectTransform; nr.sizeDelta = new Vector2(cw - W * 0.02f, W * 0.085f); nr.anchoredPosition = new Vector2(0f, y - W * 0.0425f);
                    offerName[i].resizeTextMaxSize = nameMax; offerName[i].resizeTextMinSize = Mathf.RoundToInt(nameMax * 0.6f); offerName[i].fontSize = nameMax;
                    y -= W * 0.085f;
                    var rr = offerRarity[i].rectTransform; rr.sizeDelta = new Vector2(cw, W * 0.03f); rr.anchoredPosition = new Vector2(0f, y - W * 0.012f);
                    offerRarity[i].fontSize = Mathf.RoundToInt(W * 0.026f);
                    y -= W * 0.032f;
                    float th = ch + y - W * 0.015f; // what is left down to the card's bottom edge
                    var tr = offerText[i].rectTransform; tr.sizeDelta = new Vector2(cw - W * 0.03f, th); tr.anchoredPosition = new Vector2(0f, y - th / 2f);
                    offerText[i].resizeTextMaxSize = textMax; offerText[i].resizeTextMinSize = Mathf.RoundToInt(textMax * 0.7f); offerText[i].fontSize = textMax;
                }
            }
            // the title and the cards drop in one after another over 0.25 s; a pressed card sinks a little
            float now = Time.unscaledTime;
            float kt = Ease(Mathf.Clamp01((now - offerShownAt) / 0.2f));
            offerRoot.localPosition = Vector3.zero;
            offerTitleRt.localPosition = new Vector3(cx, top - titleH / 2f + (1f - kt) * W * 0.04f, 0f);
            offerTitleRt.localScale = Vector3.one * (0.9f + 0.1f * kt);
            float total = n * cw + (n - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                float k = Ease(Mathf.Clamp01((now - offerShownAt - 0.05f * i) / 0.22f));
                float x = cx - total / 2f + cw / 2f + i * (cw + gap);
                offerCard[i].localPosition = new Vector3(x, cardsY + (1f - k) * W * 0.06f, 0f);
                float s = (0.85f + 0.15f * k) * (offerPressed == i ? 0.95f : 1f);
                offerCard[i].localScale = new Vector3(s, s, 1f);
            }
        }
        static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

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
                if (heldShown[i] != it.Id) { heldShown[i] = it.Id; heldIcon[i].sprite = ItemSprite(it.Id); }
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
