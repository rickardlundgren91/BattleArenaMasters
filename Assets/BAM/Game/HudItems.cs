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
    /// by side in the upper half of the field, under a "CHOOSE ITEM 1 OF 2" title (Rickard 2026-11-11 21:13 "FÖR dåligt med shoppen ...
    /// städa upp det ui": the first panel sat low over the joystick and the SPECIAL with tiny text). Each card has the item's own
    /// icon (drawn in code, ItemIconArt), its name, its rarity and one short line with its numbers in gold; a tap anywhere on a
    /// card takes it, no SELECT button. The match does not stop: the cards wait until the player picks (keys 1-3 in the editor),
    /// and they keep clear of every control. They open by themselves only while the player is dead, or stands still out of combat
    /// with no enemy hero near (Rickard 2026-11-15); otherwise a pulsing gold ITEM button by the SPECIAL waits (where the next
    /// item will sit) and opens them, and a hit takes them back to the button (review 2026-11-13 nr 2: at 1:20 and 2:56 they came
    /// up in the middle of tower fights and lay over RESPAWN IN). The items held sit as two small icons beside the SPECIAL on the
    /// screen's inner side, dark while they cool down; a finger held on one shows what it does, as on the SPECIAL (HudHold.cs;
    /// Rickard 2026-11-13). Pure display and input: the pick goes to the rules through PlayerInput.ItemPick.
    ///
    /// The polish round of 2026-11-16 (Rickard 2026-11-15 "sätt en annan på item systemet och dett ui med mera"; the web build
    /// played in phone format first, findings in bam-work/items/polish/README.md), all of it display only:
    ///  - the cards are solid (they were see-through over the lane) and a two line name no longer runs into the rarity word;
    ///  - the title counts the choices ("1 OF 2"), so a new player sees that a second one comes;
    ///  - taking a card is an event: the card pops and flashes, the other two fall away, its icon flies down to its place beside
    ///    the SPECIAL and lands with a ring in its rarity's colour (it was: the cards vanished and a small icon was there);
    ///  - the cards close with a short fade, and a finger already on its way still takes its card (they blinked shut, and the
    ///    tap fell through to the field);
    ///  - the ITEM button is gold and swells a ring when a choice arrives; the tutorial's gold arrow bounces over the first
    ///    item's button until the player has used the button twice.
    /// The score sheet lists every hero's items (HudScore.cs).
    /// Not kept: the two item places as empty rings with their level ("LV 6", "LV 10") from the match's start. Rickard saw the
    /// picture and said no (2026-11-16, a card: "Nej, som förut"), so nothing about items shows by the SPECIAL before the first
    /// choice arrives.
    /// </summary>
    sealed partial class Hud
    {
        // solid navy, as the menus' panels and the picture Rickard said yes to (bam-work/items/item-pick-mock.png). It was a 0.9
        // "glass", which in this project's linear colour space let the lane, the names and the health bars show through the words
        static readonly Color CardBody = new Color(0.047f, 0.063f, 0.165f, 1f), CardText = new Color(0.86f, 0.89f, 0.98f, 1f);
        // the ITEM button: gold with dark letters (as the menus' gold badges), so it reads as something to collect among the dark controls
        static readonly Color ItemGold = Hex(0xf2b632), ItemGoldRim = Hex(0xffe9a8);
        // rarity colours for the card's edge, glow and rarity word (common, rare, epic, legendary)
        static readonly Color[] RarityEdge = { Hex(0x8fb4d4), Hex(0x2fe0a0), Hex(0xd77bff), Hex(0xffc83a) };
        static readonly string[] RarityWord = { "COMMON", "RARE", "EPIC", "LEGENDARY" };

        RectTransform offerRoot, offerTitleRt;
        CanvasGroup offerTitleLook;
        readonly RectTransform[] offerCard = new RectTransform[ItemS.Choices];
        readonly CanvasGroup[] offerLook = new CanvasGroup[ItemS.Choices];
        readonly Image[] offerEdge = new Image[ItemS.Choices], offerGlow = new Image[ItemS.Choices], offerIcon = new Image[ItemS.Choices], offerFlash = new Image[ItemS.Choices];
        readonly EmoteButton[] offerTap = new EmoteButton[ItemS.Choices];
        readonly Text[] offerName = new Text[ItemS.Choices], offerRarity = new Text[ItemS.Choices], offerText = new Text[ItemS.Choices];
        Text offerTitle;
        readonly ItemId[] offerShown = new ItemId[ItemS.Choices];
        readonly int[] offerRar = new int[ItemS.Choices];
        int offerShownCount = -1, offerPressed = -1, offerTitleNr = -1;
        float offerShownAt = -99f, offerLaidW = -1f, offerOpenSeen = -99f;
        byte itemPickQueued;

        // the ITEM button and when the cards are open: by themselves (dead, or calm with no enemy hero near) or by a tap on the
        // button, which holds until a hit
        RectTransform offerBtn, itemArrow;
        CanvasGroup itemArrowGroup;
        EmoteButton offerBtnTap;
        Image offerBtnGlow, offerBtnBurst;
        Text offerBtnLabel;
        bool offerOpen, offerByTap, itemBtnHint;
        float offerBtnAt = -99f, offerHurtSeen = -99f, offerBtnBurstAt = -99f;
        int offerBtnBurstFor = -1;
        // the gold arrow over the ITEM button until the player has opened the cards with it this many times (kept on the phone)
        const string ItemBtnPref = "bam-itembtn";
        const int ItemBtnHintTaps = 2;

        readonly RectTransform[] heldRoot = new RectTransform[Unit.MaxItems];
        readonly Image[] heldIcon = new Image[Unit.MaxItems], heldCd = new Image[Unit.MaxItems], heldRing = new Image[Unit.MaxItems];
        readonly EmoteButton[] heldTap = new EmoteButton[Unit.MaxItems];
        // the item icon a finger is on (-1: none) and since when: held HoldTime, it shows the item's card (HudHold.cs)
        int heldPress = -1;
        float heldPressAt;
        readonly Text[] heldCount = new Text[Unit.MaxItems];
        readonly ItemId[] heldShown = new ItemId[Unit.MaxItems];
        readonly float[] heldPulse = new float[Unit.MaxItems];

        // taking a card: the card pops and flashes (PickPopTime) while the others fall away, then its icon flies to its place
        // beside the SPECIAL (from PickFlyAt, PickFlyTime long) and lands with a ring in its rarity's colour (PickBurstTime). The
        // flying icon waits on the place up to PickLandWait for the rules to hand the item over (online the pick takes a few ticks)
        const float PickPopTime = 0.16f, PickFlyAt = 0.1f, PickFlyTime = 0.42f, PickLandWait = 1.5f, PickBurstTime = 0.5f;
        Image pickFly, pickRing;
        int pickCard = -1, pickSlot, pickBurstSlot;
        ItemId pickId;
        float pickAt = -99f, pickBurstAt = -99f;
        Color pickBurstColor;
        bool PickFxOn => pickCard >= 0;

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

        // a line with its numbers in gold ("+15%", "25%", "1.5s"), so the eye lands on what the item gives (rich text)
        internal static string GoldNumbers(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 64);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                bool sign = (c == '+' || c == '-') && i + 1 < s.Length && char.IsDigit(s[i + 1]);
                if (!sign && !char.IsDigit(c)) { sb.Append(c); i++; continue; }
                int j = i + (sign ? 1 : 0);
                while (j < s.Length && (char.IsDigit(s[j]) || ((s[j] == '.' || s[j] == ',') && j + 1 < s.Length && char.IsDigit(s[j + 1])))) j++;
                if (j < s.Length && s[j] == '%') j++;
                else if (j < s.Length && s[j] == 's' && (j + 1 >= s.Length || !char.IsLetter(s[j + 1]))) j++; // seconds
                sb.Append("<color=#f2cf7a>").Append(s, i, j - i).Append("</color>"); // UiKit.Gold
                i = j;
            }
            return sb.ToString();
        }

        void BuildItems()
        {
            offerRoot = UiKit.NewRect("ItemOffer", safe);
            offerRoot.anchorMin = offerRoot.anchorMax = offerRoot.pivot = new Vector2(0.5f, 0.5f);
            offerRoot.sizeDelta = Vector2.zero;
            // the title: a dark pill with a gold edge
            offerTitleRt = UiKit.NewRect("Title", offerRoot);
            offerTitleRt.anchorMin = offerTitleRt.anchorMax = offerTitleRt.pivot = new Vector2(0.5f, 0.5f);
            offerTitleLook = offerTitleRt.gameObject.AddComponent<CanvasGroup>(); offerTitleLook.blocksRaycasts = false;
            var pill = UiKit.Fill(offerTitleRt, CardBody, UiKit.Rounded); pill.type = Image.Type.Sliced; pill.raycastTarget = false;
            var pillEdge = UiKit.Fill(offerTitleRt, UiKit.Trim, UiKit.Ring); pillEdge.type = Image.Type.Sliced; pillEdge.raycastTarget = false;
            offerTitle = UiKit.Label(offerTitleRt, "CHOOSE AN ITEM", 30, TextAnchor.MiddleCenter, UiKit.Gold);
            offerTitle.fontStyle = FontStyle.Bold; offerTitle.raycastTarget = false;
            offerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(offerTitle.rectTransform);
            for (int i = 0; i < ItemS.Choices; i++)
            {
                int idx = i;
                var card = offerCard[i] = UiKit.NewRect("Card" + i, offerRoot);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
                offerLook[i] = card.gameObject.AddComponent<CanvasGroup>(); // fades the whole card (closing, and the two not taken)
                var bg = UiKit.Fill(card, CardBody, UiKit.Rounded); bg.type = Image.Type.Sliced; bg.raycastTarget = true; // the whole card takes the tap
                var tap = offerTap[i] = card.gameObject.AddComponent<EmoteButton>(); // fingers read directly, as the other match controls
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
                tx.resizeTextForBestFit = true; tx.lineSpacing = 0.95f; tx.supportRichText = true; TopCentre(tx.rectTransform);
                // the white flash over the card as it is taken (and a faint one while a finger rests on it)
                var fl = offerFlash[i] = UiKit.Fill(card, Color.clear, UiKit.Rounded); fl.type = Image.Type.Sliced; fl.raycastTarget = false;
            }
            offerRoot.gameObject.SetActive(false);

            // the ITEM button: a gold round plate with a pale rim and ITEM in dark letters over a breathing gold glow; the whole
            // 0.11 W square takes the finger (7.6 mm, as the other small match buttons)
            offerBtn = UiKit.NewRect("ItemButton", safe);
            offerBtn.anchorMin = offerBtn.anchorMax = offerBtn.pivot = new Vector2(0.5f, 0.5f);
            var hit = UiKit.Fill(offerBtn, Color.clear); hit.raycastTarget = true;
            offerBtnGlow = UiKit.Fill(offerBtn, Color.clear, UiKit.Circle);
            UiKit.Stretch(offerBtnGlow.rectTransform, -6f, -6f, -6f, -6f);
            // the ring that swells out of it once when a choice arrives
            offerBtnBurst = UiKit.Fill(offerBtn, Color.clear, UiKit.CircleRing); offerBtnBurst.enabled = false;
            var plate = UiKit.Fill(offerBtn, ItemGold, UiKit.Circle);
            UiKit.Stretch(plate.rectTransform, 6f, 6f, 6f, 6f);
            var ring = UiKit.Fill(offerBtn, ItemGoldRim, UiKit.CircleRing);
            UiKit.Stretch(ring.rectTransform, 6f, 6f, 6f, 6f);
            offerBtnLabel = UiKit.Label(offerBtn, "ITEM", 26, TextAnchor.MiddleCenter, UiKit.Ink);
            offerBtnLabel.fontStyle = FontStyle.Bold; offerBtnLabel.raycastTarget = false;
            offerBtnLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(offerBtnLabel.rectTransform);
            var press = offerBtnTap = offerBtn.gameObject.AddComponent<EmoteButton>(); // fingers read directly, as the other match controls
            press.Up = _ => OfferButtonTapped();
            offerBtn.gameObject.SetActive(false);
            // the tutorial's gold arrow, over the button the first times (OfferButtonFrame)
            itemArrow = MakeChevron(safe);
            itemArrowGroup = itemArrow.gameObject.AddComponent<CanvasGroup>(); itemArrowGroup.blocksRaycasts = false;

            // the items held: small round icons beside the SPECIAL; a finger held on one reads it (HudHold.cs)
            for (int i = 0; i < Unit.MaxItems; i++)
            {
                int idx = i;
                var r = heldRoot[i] = UiKit.NewRect("Item" + i, safe);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                // the finger's square: the icon and 4 units round it (2.2 icon radii), clear of the other icon and of the SPECIAL's
                // round touch area (the icon sits 0.03 W outside it)
                var area = UiKit.Fill(r, Color.clear); area.raycastTarget = true;
                UiKit.Stretch(area.rectTransform, -4f, -4f, -4f, -4f);
                var tap = heldTap[i] = r.gameObject.AddComponent<EmoteButton>(); // fingers read directly, as the other match controls
                tap.Down = _ => { heldPress = idx; heldPressAt = Time.unscaledTime; };
                tap.Up = _ => { if (heldPress == idx) heldPress = -1; };
                var ic = heldIcon[i] = UiKit.Fill(r, Color.white); ic.raycastTarget = false; ic.preserveAspect = true;
                var cd = heldCd[i] = UiKit.Fill(r, new Color(0f, 0f, 0f, 0.6f), UiKit.Circle);
                cd.raycastTarget = false; cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
                cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = false;
                var n = heldCount[i] = UiKit.Label(r, "", 30, TextAnchor.LowerRight, Hex(0xbff3ff));
                n.raycastTarget = false; UiKit.AddOutline(n, 2);
                // the ring that fills round the icon while the finger stays, as round the SPECIAL (sized in HeldFrame)
                var fill = heldRing[i] = UiKit.Fill(r, new Color(1f, 1f, 1f, 0.85f), ItemRingSprite);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top; fill.fillClockwise = true;
                fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                fill.enabled = false;
                r.gameObject.SetActive(false);
            }

            // the ring where a taken item lands, and the icon that flies there from its card (over everything else in the HUD)
            var br = UiKit.NewRect("ItemLanded", safe);
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
            pickRing = br.gameObject.AddComponent<Image>();
            pickRing.sprite = UiKit.CircleRing; pickRing.raycastTarget = false;
            br.gameObject.SetActive(false);
            var fr = UiKit.NewRect("ItemFly", safe);
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0.5f, 0.5f);
            pickFly = fr.gameObject.AddComponent<Image>();
            pickFly.raycastTarget = false; pickFly.preserveAspect = true;
            fr.gameObject.SetActive(false);
        }

        // a child laid out from its parent's top centre
        static void TopCentre(RectTransform rt) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f); }

        void BeginItems()
        {
            itemPickQueued = 0; offerShownCount = -1; offerShownAt = -99f; offerPressed = -1; offerLaidW = -1f; offerOpenSeen = -99f;
            offerOpen = offerByTap = false; offerBtnAt = -99f; offerHurtSeen = float.MaxValue; // the first frame takes the hurt time as seen
            offerBtnBurstAt = -99f; offerBtnBurstFor = -1;
            pickCard = -1; pickAt = pickBurstAt = -99f;
            // the arrow over the ITEM button: in real matches, until the player has used the button twice
            itemBtnHint = !tutorial && !heroTest && PlayerPrefs.GetInt(ItemBtnPref, 0) < ItemBtnHintTaps;
            if (offerBtn != null) offerBtn.gameObject.SetActive(false);
            if (itemArrow != null) itemArrow.gameObject.SetActive(false);
            if (pickFly != null) pickFly.gameObject.SetActive(false);
            if (pickRing != null) pickRing.gameObject.SetActive(false);
            for (int i = 0; i < Unit.MaxItems; i++) { heldShown[i] = ItemId.None; heldPulse[i] = -99f; }
            ReleaseItemTouches();
            if (offerRoot != null) { offerRoot.gameObject.SetActive(false); WholeCards(); } // a match that ended in the middle of a pick
            for (int i = 0; i < Unit.MaxItems; i++)
                if (heldRoot[i] != null) heldRoot[i].gameObject.SetActive(false);
        }

        void OfferTapped(int i)
        {
            var p = m.Player;
            if (p == null || i >= p.OfferCount || itemPickQueued != 0 || PickFxOn) return;
            if (Time.unscaledTime - offerShownAt < 0.35f) return; // a tap already under way when the cards came does not take one
            if (i >= offerShownCount || offerShown[i] != p.Offer[i]) return; // the card must show what the rules offer now
            itemPickQueued = (byte)(i + 1);
            // the pick's own show (PickFxFrame): from this card to the place the item will sit in
            pickCard = i; pickId = p.Offer[i]; pickSlot = Mathf.Min(p.ItemCount, Unit.MaxItems - 1); pickAt = Time.unscaledTime;
            for (int k = 0; k < ItemS.Choices; k++) offerLook[k].blocksRaycasts = false; // fading out: the field under them is free again
            BamAudio.Play(SfxId.UiConfirm, 1f); // the menus' confirm clip (it was UiTap); an own "item taken" clip would be an order for Rickard
        }

        void OfferButtonTapped()
        {
            var p = m.Player;
            if (p == null || p.OfferCount == 0 || itemPickQueued != 0) return;
            offerOpen = true; offerByTap = true;
            BamAudio.Play(SfxId.UiTap, 1f);
            if (!tutorial && !heroTest)
            {   // the arrow has done its work once the button has been used ItemBtnHintTaps times
                int used = PlayerPrefs.GetInt(ItemBtnPref, 0) + 1;
                if (used <= ItemBtnHintTaps) { PlayerPrefs.SetInt(ItemBtnPref, used); PlayerPrefs.Save(); }
                if (used >= ItemBtnHintTaps) itemBtnHint = false;
            }
        }

        byte TakeItemPick() { var b = itemPickQueued; itemPickQueued = 0; return b; }

        // the item icon a finger holds (-1: none), only while it shows: a hidden icon never gets its finger's Up (EmoteButton.OnDisable)
        int HeldPressed() => heldPress >= 0 && heldRoot[heldPress].gameObject.activeSelf ? heldPress : -1;

        /// <summary>The fingers were lost (Hud.CancelTouches) or a match begins: the item cards, the ITEM button and the icons forget
        /// theirs, so none stays pressed (a stale finger would block every later press while the control shows).</summary>
        void ReleaseItemTouches()
        {
            heldPress = -1; offerPressed = -1;
            for (int i = 0; i < Unit.MaxItems; i++) if (heldTap[i] != null) heldTap[i].Release();
            for (int i = 0; i < ItemS.Choices; i++) if (offerTap[i] != null) offerTap[i].Release();
            if (offerBtnTap != null) offerBtnTap.Release();
        }

        // the cards close with a fade this long, in which they still take a tap (a finger that set off while they were up lands
        // within about a quarter of a second); a finger that rests on a card keeps them up to OfferPressGrace, so lifting it still
        // takes the card. They blinked shut in one frame, most often as the player's own hero began to hit a minion while the
        // player read them, and the tap fell through to the joystick (the web build, 2026-11-15)
        const float OfferCloseTime = 0.25f, OfferPressGrace = 0.6f;

        void ItemsFrame(bool hidden)
        {
            var p = m.Player;
            bool live = !hidden && p != null && m.ItemsOn && m.State == MatchState.Play;
            // a hit on the player since the last frame (LastHurt is stamped on every hit, a shield's too), seen every frame so an
            // old hit never counts when a choice comes
            bool hurt = false;
            if (p != null) { hurt = p.LastHurt > offerHurtSeen; offerHurtSeen = p.LastHurt; }
            if (!live && PickFxOn) EndPickFx(false);
            float now = Time.unscaledTime;
            // the taken card's show runs to its end before the next choice (a queued second one comes at once) or the button shows
            bool waiting = live && p.OfferCount > 0 && itemPickQueued == 0 && !PickFxOn;
            bool open = waiting && OfferOpen(p, hurt);
            if (p == null || p.OfferCount == 0) { offerOpen = offerByTap = false; } // the next choice starts by the rules again (a pause keeps a tap's opening)
            if (open) offerOpenSeen = now;
            bool closing = waiting && !open && offerShownCount >= 0
                && (now - offerOpenSeen < OfferCloseTime || (offerPressed >= 0 && now - offerOpenSeen < OfferPressGrace));
            var kb = Keyboard.current;
            if (open && kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) OfferTapped(0);
                else if (kb.digit2Key.wasPressedThisFrame) OfferTapped(1);
                else if (kb.digit3Key.wasPressedThisFrame) OfferTapped(2);
            }
            OfferFrame(open || closing || PickFxOn, open);
            OfferButtonFrame(waiting && !open);
            HeldFrame(live);
        }

        // Dead: open (the 10-20 s with nothing else to do are the best time to choose). Hit: back to the button. Otherwise a tap's
        // opening holds, and without one the cards are open exactly while the player stands still, is out of combat (no hit taken
        // or dealt for 3 s, Unit.InCombat) and has no enemy hero it can see within OfferFoeGap tiles (Rickard 2026-11-15: "bara
        // syns ... när man står stilla och ute ur combat helst en liten bit ifrån"), anywhere on the map; walking closes them again.
        // It was "behind our own tower" before (review 2026-11-13 nr 2).
        const float OfferStillTime = 0.4f, OfferFoeGap = 4.5f;
        bool OfferOpen(Unit p, bool hurt)
        {
            if (!p.Alive) { offerOpen = true; offerByTap = false; }
            else if (hurt) { offerOpen = false; offerByTap = false; }
            else if (!offerByTap) offerOpen = m.Time - p.MovedAt >= OfferStillTime && !p.InCombat(m.Time) && !FoeNear(p);
            return offerOpen;
        }

        // an enemy hero alive and in sight within OfferFoeGap tiles
        bool FoeNear(Unit p)
        {
            for (int i = 0; i < m.Heroes.Count; i++)
            {
                var e = m.Heroes[i];
                if (e.Team == p.Team || !e.Alive || !m.IsVisibleTo(e, p.Team)) continue;
                if (SimMath.Dist(e, p) <= OfferFoeGap) return true;
            }
            return false;
        }

        void OfferButtonFrame(bool show)
        {
            if (offerBtn.gameObject.activeSelf != show)
            {
                offerBtn.gameObject.SetActive(show);
                if (show)
                {
                    offerBtnAt = Time.unscaledTime; // no SetAsLastSibling: the score sheet, opened later, stays over it
                    // a choice's first time on the button: a gold ring swells out of it once (not every time the cards close again)
                    int nr = m.Player.ItemCount;
                    if (offerBtnBurstFor != nr) { offerBtnBurstFor = nr; offerBtnBurstAt = Time.unscaledTime; }
                }
            }
            if (!show) { Arrow(itemArrow, itemArrowGroup, false, Vector2.zero, 0f); return; }
            var p = m.Player;
            float W = SafeW, rb = W * 0.055f, ri = W * 0.042f;
            Vector3 c = SpecialCentre();
            // in the next item's place: level with the SPECIAL's middle while no item is held, in the lower slot under the first one
            var at = new Vector3(c.x + InnerSide * (SpecialButton.Radius + W * 0.03f + rb), p.ItemCount == 0 ? c.y : c.y - 1.15f * ri, 0f);
            offerBtn.localPosition = at;
            offerBtn.sizeDelta = new Vector2(2f * rb, 2f * rb);
            offerBtnLabel.fontSize = Mathf.RoundToInt(W * 0.03f);
            // it pops in over 0.25 s, then breathes (a little bigger and a brighter glow about once a second)
            float t = Time.unscaledTime - offerBtnAt, wave = 0.5f + 0.5f * Mathf.Sin(t * 5.5f);
            offerBtn.localScale = Vector3.one * ((0.6f + 0.4f * Ease(Mathf.Clamp01(t / 0.25f))) * (1f + 0.07f * wave));
            offerBtnGlow.color = new Color(1f, 0.78f, 0.28f, 0.22f + 0.35f * wave);
            float b = (Time.unscaledTime - offerBtnBurstAt) / 0.6f;
            bool burst = b >= 0f && b < 1f;
            if (offerBtnBurst.enabled != burst) offerBtnBurst.enabled = burst;
            if (burst)
            {
                offerBtnBurst.rectTransform.localScale = Vector3.one * (1f + 1.1f * Ease(b));
                offerBtnBurst.color = new Color(1f, 0.86f, 0.45f, 0.9f * (1f - b));
            }
            // the first times: the gold arrow of the tutorial points at it (until the player has used the button twice). Only over
            // the first item's button: over the second one, in the lower place, the arrow would lie on the first item's icon
            Arrow(itemArrow, itemArrowGroup, itemBtnHint && p.ItemCount == 0, new Vector2(at.x, at.y - rb * 0.5f), rb * 1.5f);
        }

        // the SPECIAL's middle in the safe area's local space
        Vector3 SpecialCentre() => safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));

        // the side of the SPECIAL towards the screen's middle: left for the right hand, right for the left hand
        float InnerSide => leftHanded ? 1f : -1f;

        // the middle of item place 0 or 1: beside the SPECIAL on the screen's inner side, one above the other (always left of it
        // until 2026-11-13, which put them half off the screen's left edge for the left hand, whose SPECIAL sits bottom left)
        Vector3 ItemSlotPos(int slot, float W)
        {
            float r = W * 0.042f;
            Vector3 c = SpecialCentre();
            return new Vector3(c.x + InnerSide * (SpecialButton.Radius + W * 0.03f + r), c.y + (slot == 0 ? 1.15f : -1.15f) * r, 0f);
        }

        void OfferFrame(bool show, bool open)
        {
            if (offerRoot.gameObject.activeSelf != show) offerRoot.gameObject.SetActive(show);
            if (!show) { offerShownCount = -1; offerPressed = -1; return; }
            var p = m.Player;
            float W = SafeW, now = Time.unscaledTime;
            // the cards: 0.29 W wide with 0.025 W between, 0.5 W tall, the title 0.42 W under the safe area's top (under the score, the
            // team portraits, the kill feed and the menu and heart buttons), so they cover only the field's upper half and never a control
            float cw = W * 0.29f, gap = W * 0.025f, ch = W * 0.5f, titleH = W * 0.075f;
            float top = safe.rect.yMax - W * 0.42f, cardsY = top - titleH - W * 0.02f - ch / 2f, cx = safe.rect.center.x;
            float pad = W * 0.018f, ir = W * 0.17f; // the card's top margin and its icon
            if (!PickFxOn)
            {   // what the cards say (not while a taken card plays out: the rules may already offer the next three)
                int n = p.OfferCount;
                bool fresh = offerShownCount != n;
                for (int i = 0; i < n && !fresh; i++) if (offerShown[i] != p.Offer[i]) fresh = true;
                if (fresh)
                {
                    offerShownCount = n; offerShownAt = now; offerPressed = -1;
                    if (!scoreOpen) offerRoot.SetAsLastSibling(); // never over an open score sheet (online the match goes on under it)
                    // which choice of the match this is (one per ItemS.OfferLevels): tells a new player that a second one comes
                    int nr = Mathf.Min(p.ItemCount + 1, ItemS.OfferLevels.Length);
                    if (nr != offerTitleNr) { offerTitleNr = nr; offerTitle.text = "CHOOSE ITEM " + nr + " OF " + ItemS.OfferLevels.Length; }
                    for (int i = 0; i < ItemS.Choices; i++)
                    {
                        bool on = i < n; offerCard[i].gameObject.SetActive(on); if (!on) continue;
                        var id = offerShown[i] = p.Offer[i]; var info = ItemCatalog.Get(id);
                        offerRar[i] = info.Rarity;
                        offerEdge[i].color = RarityEdge[info.Rarity];
                        offerIcon[i].sprite = ItemSprite(id); offerIcon[i].enabled = true;
                        offerName[i].text = info.Name;
                        offerRarity[i].text = RarityWord[info.Rarity]; offerRarity[i].color = RarityEdge[info.Rarity];
                        offerText[i].text = GoldNumbers(info.Short);
                    }
                    offerLaidW = -1f;
                }
            }
            if (offerLaidW != W)
            {   // sizes follow the screen: laid out again only when it changes
                offerLaidW = W;
                offerTitleRt.sizeDelta = new Vector2(W * 0.5f, titleH);
                offerTitle.fontSize = Mathf.RoundToInt(W * 0.042f);
                int nameMax = Mathf.RoundToInt(W * 0.044f), textMax = Mathf.RoundToInt(W * 0.036f);
                for (int i = 0; i < ItemS.Choices; i++)
                {
                    offerCard[i].sizeDelta = new Vector2(cw, ch);
                    // the rarity's tint behind the icon: the card's top two fifths
                    offerGlow[i].rectTransform.anchorMin = new Vector2(0f, 0.6f); offerGlow[i].rectTransform.anchorMax = Vector2.one;
                    offerGlow[i].rectTransform.offsetMin = new Vector2(W * 0.008f, 0f); offerGlow[i].rectTransform.offsetMax = new Vector2(-W * 0.008f, -W * 0.008f);
                    UiKit.Stretch(offerEdge[i].rectTransform);
                    offerIcon[i].rectTransform.sizeDelta = new Vector2(ir, ir); offerIcon[i].rectTransform.anchoredPosition = new Vector2(0f, -pad - ir / 2f);
                    // the name: room for two full lines (0.1 W; it was 0.085 W, where "Crushing Spanner" ran into the rarity word),
                    // 0.009 W under the icon so a two-line name stays clear of the tint's lower edge
                    float y = -pad - ir - W * 0.009f, nh = W * 0.1f;
                    var nr = offerName[i].rectTransform; nr.sizeDelta = new Vector2(cw - W * 0.02f, nh); nr.anchoredPosition = new Vector2(0f, y - nh / 2f);
                    offerName[i].resizeTextMaxSize = nameMax; offerName[i].resizeTextMinSize = Mathf.RoundToInt(nameMax * 0.6f); offerName[i].fontSize = nameMax;
                    y -= nh + W * 0.004f;
                    var rr = offerRarity[i].rectTransform; rr.sizeDelta = new Vector2(cw, W * 0.03f); rr.anchoredPosition = new Vector2(0f, y - W * 0.015f);
                    offerRarity[i].fontSize = Mathf.RoundToInt(W * 0.026f);
                    y -= W * 0.036f;
                    float th = ch + y - W * 0.015f; // what is left down to the card's bottom edge
                    var tr = offerText[i].rectTransform; tr.sizeDelta = new Vector2(cw - W * 0.03f, th); tr.anchoredPosition = new Vector2(0f, y - th / 2f);
                    offerText[i].resizeTextMaxSize = textMax; offerText[i].resizeTextMinSize = Mathf.RoundToInt(textMax * 0.7f); offerText[i].fontSize = textMax;
                }
            }
            offerRoot.localPosition = Vector3.zero;
            int shown = offerShownCount;
            float total = shown * cw + (shown - 1) * gap;
            if (PickFxOn) { PickFxFrame(W, cx, cardsY, cw, gap, ch, total, pad, ir); return; }

            // the title and the cards drop in one after another over 0.25 s; a pressed card sinks a little. Closing (the rules shut
            // them: a step, a hit, an enemy near), they fade and lift a little over OfferCloseTime and still take a tap meanwhile
            float vis = open ? 1f : Mathf.Clamp01(1f - (now - offerOpenSeen) / OfferCloseTime);
            if (offerPressed >= 0) vis = Mathf.Max(vis, 0.45f); // a finger rests on a card: it stays to be taken (OfferPressGrace)
            float lift = (1f - vis) * W * 0.02f;
            float kt = Ease(Mathf.Clamp01((now - offerShownAt) / 0.2f));
            offerTitleLook.alpha = vis * Mathf.Clamp01((now - offerShownAt) / 0.1f);
            offerTitleRt.localPosition = new Vector3(cx, top - titleH / 2f + (1f - kt) * W * 0.04f + lift, 0f);
            offerTitleRt.localScale = Vector3.one * (0.9f + 0.1f * kt);
            for (int i = 0; i < shown; i++)
            {
                float since = now - offerShownAt - 0.05f * i;
                float k = Ease(Mathf.Clamp01(since / 0.22f));
                float x = cx - total / 2f + cw / 2f + i * (cw + gap);
                offerCard[i].localPosition = new Vector3(x, cardsY + (1f - k) * W * 0.06f + lift, 0f);
                float s = (0.85f + 0.15f * k) * (offerPressed == i ? 0.95f : 1f);
                offerCard[i].localScale = new Vector3(s, s, 1f);
                offerLook[i].alpha = vis * Mathf.Clamp01(since / 0.1f);
                // epic and legendary cards breathe a little behind their icon, so a rare find stands out among the three
                var rc = RarityEdge[offerRar[i]];
                offerGlow[i].color = new Color(rc.r, rc.g, rc.b, offerRar[i] >= 2 ? 0.27f + 0.11f * Mathf.Sin(now * 4.2f + i * 1.3f) : 0.22f);
                offerFlash[i].color = offerPressed == i ? new Color(1f, 1f, 1f, 0.07f) : Color.clear;
            }
        }
        static float Ease(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        // A card was taken (OfferTapped): it pops and flashes white, the others sink and fade, and from PickFlyAt its icon flies
        // in a soft arc to the item's place beside the SPECIAL, shrinking to the held icon's size. It lands when the rules have
        // handed the item over (HeldFrame hides the real icon until then), with a pop and a ring (EndPickFx).
        void PickFxFrame(float W, float cx, float cardsY, float cw, float gap, float ch, float total, float pad, float ir)
        {
            var p = m.Player;
            float t = Time.unscaledTime - pickAt;
            offerTitleLook.alpha = 1f - Mathf.Clamp01(t / 0.12f);
            Vector3 from = new Vector3(cx, cardsY, 0f);
            for (int i = 0; i < offerShownCount; i++)
            {
                float x = cx - total / 2f + cw / 2f + i * (cw + gap);
                if (i != pickCard)
                {
                    float k = Mathf.Clamp01(t / 0.16f);
                    offerLook[i].alpha = 1f - k;
                    offerCard[i].localPosition = new Vector3(x, cardsY - k * W * 0.03f, 0f);
                    offerCard[i].localScale = Vector3.one * (1f - 0.06f * k);
                    offerFlash[i].color = Color.clear;
                    continue;
                }
                float pop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / PickPopTime));
                offerCard[i].localPosition = new Vector3(x, cardsY, 0f);
                offerCard[i].localScale = Vector3.one * (1f + 0.08f * pop);
                offerFlash[i].color = new Color(1f, 1f, 1f, 0.45f * (1f - Mathf.Clamp01(t / PickPopTime)));
                offerLook[i].alpha = 1f - Mathf.Clamp01((t - PickFlyAt) / 0.2f);
                offerIcon[i].enabled = t < PickFlyAt; // the flying icon takes over from the card's own
                from = new Vector3(x, cardsY + ch / 2f - pad - ir / 2f, 0f); // the middle of the card's icon
            }
            bool flying = t >= PickFlyAt;
            if (pickFly.gameObject.activeSelf != flying)
            {
                pickFly.gameObject.SetActive(flying);
                if (flying) { pickFly.sprite = ItemSprite(pickId); if (!scoreOpen) pickFly.transform.SetAsLastSibling(); }
            }
            if (flying)
            {
                float k = Mathf.Clamp01((t - PickFlyAt) / PickFlyTime), e = k * k * (3f - 2f * k);
                Vector3 pos = Vector3.Lerp(from, ItemSlotPos(pickSlot, W), e);
                pos.y += W * 0.06f * Mathf.Sin(Mathf.PI * e); // a toss: up a little first, then down to its place
                float size = Mathf.Lerp(ir, 2f * W * 0.042f, e);
                pickFly.rectTransform.localPosition = pos;
                pickFly.rectTransform.sizeDelta = new Vector2(size, size);
            }
            if (t >= PickFlyAt + PickFlyTime)
            {
                bool has = p != null && p.ItemCount > pickSlot;
                if (has || t >= PickFlyAt + PickFlyTime + PickLandWait) EndPickFx(has);
            }
        }

        /// <summary>The taken card's show is over. Landed: the item is in its place, which pops (heldPulse) and sends out a ring
        /// in the rarity's colour. Not landed (the match ended, or the rules never took the pick): everything just goes away.</summary>
        void EndPickFx(bool landed)
        {
            if (landed)
            {
                heldPulse[pickSlot] = Time.unscaledTime;
                pickBurstAt = Time.unscaledTime; pickBurstSlot = pickSlot; pickBurstColor = RarityEdge[ItemCatalog.Rarity(pickId)];
            }
            pickCard = -1;
            pickFly.gameObject.SetActive(false);
            // the cards go at once (the next frame decides what shows) and are whole again for the next choice, which drops in as new
            offerRoot.gameObject.SetActive(false);
            offerShownCount = -1; offerPressed = -1;
            WholeCards();
        }

        // the cards as they are before a pick: opaque, taking fingers, their icons in place
        void WholeCards()
        {
            for (int i = 0; i < ItemS.Choices; i++)
            {
                offerLook[i].alpha = 1f; offerLook[i].blocksRaycasts = true;
                offerFlash[i].color = Color.clear; offerIcon[i].enabled = true;
            }
            offerTitleLook.alpha = 1f;
        }

        void HeldFrame(bool live)
        {
            var p = m.Player;
            var ev = m.Events;
            if (live)
                for (int k = 0; k < ev.Count; k++)
                    if (ev[k].Type == EventType.ItemProc && ev[k].A == p)
                        for (int i = 0; i < p.ItemCount; i++) if ((byte)p.Items[i].Id == ev[k].Kind) heldPulse[i] = Time.unscaledTime;
            float W = SafeW, r = W * 0.042f, now = Time.unscaledTime;
            for (int i = 0; i < Unit.MaxItems; i++)
            {
                // an item just taken shows once its flying icon has landed (PickFxFrame)
                bool show = live && i < p.ItemCount && !(PickFxOn && i == pickSlot);
                if (heldRoot[i].gameObject.activeSelf != show) heldRoot[i].gameObject.SetActive(show);
                if (!show) { if (heldPress == i) heldPress = -1; continue; }
                var it = p.Items[i];
                if (heldShown[i] != it.Id) { heldShown[i] = it.Id; heldIcon[i].sprite = ItemSprite(it.Id); }
                heldRoot[i].localPosition = ItemSlotPos(i, W);
                float pulse = Mathf.Clamp01(1f - (now - heldPulse[i]) / 0.35f);
                heldRoot[i].sizeDelta = new Vector2(2f * r, 2f * r);
                heldRoot[i].localScale = Vector3.one * (1f + 0.25f * pulse);
                heldRing[i].rectTransform.sizeDelta = Vector2.one * (2f * 1.12f * r + 8f); // just outside the icon (the SPECIAL's: 1.12 radii + 8)
                float cd = ItemCooldown(it.Id), left = it.ReadyAt - m.Time;
                heldCd[i].fillAmount = cd > 0 && left > 0 ? Mathf.Clamp01(left / cd) : 0f;
            }
            // the ring that swells out of the place a taken item just landed in, in its rarity's colour
            float b = (now - pickBurstAt) / PickBurstTime;
            bool burst = live && b >= 0f && b < 1f;
            if (pickRing.gameObject.activeSelf != burst) pickRing.gameObject.SetActive(burst);
            if (burst)
            {
                float s = 2f * r * (1.1f + 1.4f * Ease(b));
                pickRing.rectTransform.localPosition = ItemSlotPos(pickBurstSlot, W);
                pickRing.rectTransform.sizeDelta = new Vector2(s, s);
                pickRing.color = new Color(pickBurstColor.r, pickBurstColor.g, pickBurstColor.b, 0.9f * (1f - b));
            }
        }

        // ---------- the marks by the health bars (the old HudItemsV2.cs), kept few and plain so they read on a phone ----------
        //   Quarry Mark: its icon over a marked enemy while the mark lasts (everyone sees it: it tells the team whom to hit)
        //   Thicket Fang: its icon over the holder while its next hero hit is armed (own team only: the enemy should not know)

        void BuildItemMarks(Overhead o)
        {
            var mk = UiKit.NewRect("ItemMark", o.Root);
            mk.anchorMin = mk.anchorMax = mk.pivot = new Vector2(0.5f, 0.5f);
            o.ItemMark = mk.gameObject.AddComponent<Image>(); o.ItemMark.raycastTarget = false; o.ItemMark.preserveAspect = true; o.ItemMark.enabled = false;
        }

        void ItemMarksFrame(Overhead o, Unit u, float ts, float bw)
        {
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
