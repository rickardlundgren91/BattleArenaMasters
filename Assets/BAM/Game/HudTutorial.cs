using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The guided match's own screen. It began as a copy of the old game's (ABC2 Hud.cs drawTutHUD, drawTutWorld, drawTutTowerBar,
    /// drawTutSkip, drawTutEnd and HudTutorialIsland.drawTutDissolve; Rickard 2026-10-26 "alla") and was made one piece in the
    /// tutorial review of 2026-11-15 (Rickard: "snygg, sammanhängande och lärorik"), after a look at how Brawl Stars, Mobile
    /// Legends, Wild Rift and Pokémon Unite teach their first minutes:
    /// - ONE guide card at the top in both halves (the island had a card in the sky, the arena a pill): Tyra's portrait and name,
    ///   one short line of at most two rows with the words to notice coloured, and a row of dots, one per lesson, so the
    ///   player sees how far it is. A finished lesson fills its dot with a pop and a ding, and while no new line is up the card
    ///   says a word of praise ("NICE!").
    /// - One mark at a time for where to look: a gold ring on the ground with a bouncing arrow, an arrow over a unit, a soft
    ///   trail of gold lights up the lane (the arena), an arrow over the joystick until it is first used, over SPECIAL only when
    ///   a tap would hit and over BLINK when it is asked for. The FOCUS chip needs none: for its lesson it comes up big
    ///   right under the card, with a gold ring round it (HudFocus.cs).
    /// - The drag lesson (Rickard 2026-11-16: "Sen har vi inte lärt spelaren att man kan dra skillen"): the drag is shown, not
    ///   only named. While it is asked for and no finger is on the button, a finger's dot lands on SPECIAL, slides out the way
    ///   Fenn stands and lets go, over and over, and the aim line shows from Tyra through him as it would for a real finger
    ///   there (DragGhost).
    /// - Skip, small and quiet in the top right corner in both halves: the tutorial counts as done, then Home.
    /// - The arena half: DESTROY THE / ENEMY TOWER over Tyra's landing, "+ CHARGE" flying from Tyra into the button on every
    ///   confirmed hit, the enemy tower's bar in Tutorial.TowerHits blocks, and at the end TOWER DESTROYED! with the dots
    ///   and, the first time, a big PLAY FIRST MATCH (straight into the first match) over a small Home; a replay gets CONTINUE.
    /// - The very first start: a picture of Home zooms in and fades into the island (Dissolve).
    /// Everything is placed in the safe area's own units, as the old code drew in the frame's W and H.
    /// </summary>
    sealed partial class Hud
    {
        const float TutDissolveTime = 1.6f, TutOverBanner = 1f, TutOverTap = 2.2f;
        const float ChargeFlight = 0.65f, ChargeArrive = 0.15f;
        // the card: a word of praise stays this long after a lesson (while no line is up), and the card fades in and out this fast
        const float PraiseTime = 0.9f, CardFade = 0.15f, DotPop = 0.45f;
        const int TrailDots = 28;
        static readonly Color TutGold = Hex(0xffcf4a), ChargeGold = Hex(0xffc94a), ChargeEdge = Hex(0x3a1a00), TowerRed = Hex(0xff3b4e);
        static readonly Color TowerOff = new Color(0.25f, 0.12f, 0.2f, 1f), CardFill = new Color(14f / 255f, 20f / 255f, 58f / 255f, 0.88f);
        static readonly Color DotTodo = new Color(1f, 1f, 1f, 0.22f);
        // Tyra's face in her 400 x 308 portrait (x 137-307, y 18-188 from the top): the top strip's cut shows her to the waist,
        // too small to know at the card's size
        static readonly Rect CardFace = new Rect(0.3425f, 0.39f, 0.425f, 0.552f);
        // one word per finished lesson, in lesson order (Tutorial.Lesson): the island's five, the arena's six. None where the
        // next line comes at once (the special is charged: "tap it!") or the end screen follows (the tower)
        static readonly string[] Praise = { "NICE!", "GOOD HIT!", "SNEAKY!", "GOT IT!", "PERFECT!", null, "BOOM!", "BULLSEYE!", "NICE DASH!", "GREAT SHOT!", null };
        // the drag lesson's demonstration: seconds a round, and how far the finger slides from the button's centre, in its radii
        // (a real drag aims from 0.5, SpecialButton.DragStart); GhostMin: never shorter than this, so its aim line shows whole
        const float GhostPeriod = 2.1f, GhostReach = 1.55f, GhostMin = 0.8f;

        RectTransform tutSkip, tutBar, tutEnd, joyArrow, specialArrow, blinkArrow;
        CanvasGroup tutOpenGroup, joyArrowGroup, specialArrowGroup, blinkArrowGroup; // the arrows' groups kept, not looked up every frame (full review 2026-11-09 nr 90)
        Image specialPulse, chargeRing, tutEndDim, ghostDot, ghostStreak;
        Vector2 ghostOffset;    // the demonstration's finger now, from the button's centre in canvas units (Hud.Aim aims by it)
        float ghostAim;         // how strongly its aim line shows, 0 = no demonstration now
        float ghostAt = -1f;    // when it began (unscaled time)
        Text tutOpen1, tutOpen2, tutEnd1, tutEnd2, tutSkipText;
        readonly Text[] chargeTexts = new Text[8];
        readonly Image[] towerBlocks = new Image[Tutorial.TowerHits], towerShine = new Image[Tutorial.TowerHits];
        readonly Image[] trailGlow = new Image[TrailDots], trailCore = new Image[TrailDots];
        Image towerBack, tutSkipPlate;
        RawImage dissolve;
        float dissolveAt = -1f;

        // the guide card
        RectTransform card, cardTab, cardFace, cardDots;
        CanvasGroup cardGroup;
        Image cardPlate, cardEdge, cardShadow, cardSheen, cardTabPlate, cardDotsPlate, cardDotsEdge;
        RawImage cardPic;
        Text cardLine, cardPraise, cardWho;
        readonly Image[] cardDot = new Image[Tutorial.AllLessons];
        string cardSrc;             // what the card says now: the line's or the praise's own string
        string cardLineSrc;         // the line (with its | marks) the card's text is to show, and whether it is a tip
        bool cardLineTip;
        string cardBuiltSrc;        // what cardLine's text was last built from, for which width (LayCard)
        float cardBuiltW;
        bool cardHasFace;
        float cardShownAt, cardTextAt, cardAlpha;
        float cardLaidW = -1f, cardLaidH, cardLaidTop; // what the card was last laid out for (LayCard)

        // the end screen
        readonly Image[] endDot = new Image[Tutorial.AllLessons];
        Button tutEndPlay, tutEndHome;
        Text tutEndPlayText, tutEndHomeText;
        CanvasGroup tutEndButtons;

        // the first time through (the tutorial was not done when this half began): the end offers the first match
        bool tutFirst;
        // the player has moved the stick in this half: the arrow over the joystick has done its job
        bool joyUsed;
        int lastLesson;

        // healing: a soft green glow round Tyra's feet while the heal moment lasts
        bool landPending;
        Image healGlow, healRing;
        static readonly Color HealGreen = Hex(0x5dff8a), TabTeal = Hex(0x13a89e);

        // ---------------------------------------------------------------- the first start

        /// <summary>The very first start: Home's picture (taken the frame before the match began) zooms in and fades away.</summary>
        public void Dissolve(Texture2D home)
        {
            if (home == null) return;
            if (dissolve.texture != null) Object.Destroy(dissolve.texture);
            dissolve.texture = home;
            dissolve.enabled = true;
            dissolveAt = Time.unscaledTime;
            DissolveFrame();
        }

        void DissolveFrame()
        {
            if (dissolveAt < 0f) return;
            float k = Mathf.Clamp01((Time.unscaledTime - dissolveAt) / TutDissolveTime);
            float e = Mathf.Clamp01((k - 0.15f) / 0.7f); e = e * e * (3f - 2f * e);
            dissolve.color = new Color(1f, 1f, 1f, 1f - e);
            // the old picture grew towards the hero standing on Home's platform (Menu.DrawDissolve's tutLeadZoom)
            dissolve.rectTransform.localScale = Vector3.one * (1f + 0.6f * e);
            if (k < 1f) return;
            StopDissolve();
        }

        // also when the match ends mid-fade (Skip in the first 1.6 s): the picture must not freeze over the next match's intro
        void StopDissolve()
        {
            if (dissolve == null || dissolveAt < 0f) return;
            dissolveAt = -1f;
            dissolve.enabled = false;
            if (dissolve.texture != null) Object.Destroy(dissolve.texture);
            dissolve.texture = null;
        }

        // ---------------------------------------------------------------- after every tick

        // a finished lesson: a ding (its dot pops on the card, CardDots). Not for the last one: the tower's fall is loud enough
        void TutorialAfterStep()
        {
            var tu = m.Tut;
            if (tu == null || tu.Lesson == lastLesson) return;
            if (tu.Lesson > lastLesson && !tu.Done) BAM.Audio.BamAudio.Play(BAM.Audio.SfxId.UiConfirm, 0.7f);
            lastLesson = tu.Lesson;
        }

        // ---------------------------------------------------------------- every frame

        void TutorialFrame(bool hidden)
        {
            DissolveFrame();
            // the arena half starts with Tyra landing from the island's boost pad: a puff of dust where she comes down
            if (landPending && m.Tut != null && !island && m.Player != null)
            {
                landPending = false;
                BAM.Art.ArtKit.PlayFx(BAM.Art.FxKind.WolfLand, views.DrawnAt(m.Player), 1.8f, m.Player.Team);
            }
            var tu = m.Tut;
            bool on = tu != null && !hidden;
            float over = flow.OverSeconds;
            // the lessons run: both halves, not during the pad's flight or once the tower has fallen
            bool lesson = on && m.State == MatchState.Play && !tu.Done && !tu.Jumping;
            if (tutSkip.gameObject.activeSelf != lesson) tutSkip.gameObject.SetActive(lesson);
            bool arena = on && !island;
            Vector2 size = safe.rect.size;
            float W = size.x, H = size.y;
            if (W < 1f) return;
            float t = m.Time;
            var p = m.Player;
            bool play = arena && lesson;

            // the skip button at the old W*0.8..0.95, but a little higher (H*0.03..0.07) so it clears the pause button under it
            if (lesson) { Top(tutSkip, W * 0.875f, H * 0.05f, W * 0.15f, H * 0.04f, W, H); tutSkipText.fontSize = Mathf.RoundToInt(W * 0.034f); Corners(tutSkipPlate, H * 0.02f); }

            // the opening title, fading in over 0.3 s and out over the last 0.4 s of the opening
            bool opening = play && tu.Opening;
            if (tutOpen1.gameObject.activeSelf != opening) { tutOpen1.gameObject.SetActive(opening); tutOpen2.gameObject.SetActive(opening); }
            if (opening)
            {
                float fs = W * 0.1f;
                tutOpenGroup.alpha = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((Tutorial.OpeningTime - t) / 0.4f);
                Title(tutOpen1, fs, H * 0.6f, W, H);
                Title(tutOpen2, fs, H * 0.6f + fs * 1.1f, W, H);
            }

            GuideCard(lesson && !opening, tu, W, H);

            // The arrows over the controls, each only while its line is up and the press would do something: the joystick until
            // the stick is first moved (the island's first line), SPECIAL when a tap has somebody to hit (never over a grey
            // button, bug review 2026-11-02 nr 13), BLINK once Knut's anchor has done its part
            bool lineUp = lesson && !opening && tu.Line != null;
            bool joyOn = lineUp && island && tu.Step == Tutorial.IslandWalk && !joyUsed;
            Arrow(joyArrow, joyArrowGroup, joyOn, joyOn ? Local(RectTransformUtility.WorldToScreenPoint(null, joy.Base.position)) : Vector2.zero, JoystickZone.Radius * 1.25f);
            bool spOn = lineUp && play && p.Alive && p.SpecialReady
                && (tu.Step == Tutorial.StepFight || tu.Step == Tutorial.StepSpecial && !tu.BurstDone)
                && (!m.OneTap || m.TapHasTarget(p));
            // the drag lesson has no arrow over SPECIAL: the drag itself is shown there until a finger lands on the button
            bool dragOn = lineUp && play && tu.DragAsked && !special.Pressed;
            DragGhost(dragOn, tu, p);
            Arrow(specialArrow, specialArrowGroup, spOn, spOn ? Local(RectTransformUtility.WorldToScreenPoint(null, specialRoot.position)) : Vector2.zero, SpecialButton.Radius);
            bool blinkOn = lineUp && play && tu.Step == Tutorial.StepBlink && tu.BlinkAsked && p.Alive && m.SpellReady(p) && spellRoot.gameObject.activeSelf;
            Arrow(blinkArrow, blinkArrowGroup, blinkOn, blinkOn ? Local(RectTransformUtility.WorldToScreenPoint(null, spellRoot.position)) : Vector2.zero, SpellR);
            bool pulseOn = spOn || dragOn;
            if (specialPulse.enabled != pulseOn) specialPulse.enabled = pulseOn;
            if (pulseOn)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                specialPulse.color = new Color(1f, 0.86f, 0.4f, 0.45f + 0.45f * k);
                specialPulse.rectTransform.sizeDelta = Vector2.one * 2f * SpecialButton.Radius * (1.12f + 0.1f * k);
            }

            HealFrame(play, tu, p);
            ChargeFly(play, p, W, H);
            Trail(play && !opening, tu, p);
            TutTowerBar(arena && m.State != MatchState.Over, tu);
            TutorialEnd(arena && over >= 0f && tu.Done, over, W, H);
        }

        // The marks on the ground and over units, both halves: the gold ring to walk into with its bouncing arrow (the island's
        // rings, the last of them round the boost pad; the arena's spot to wait at outside the tower's reach) and the arrow
        // over the unit to look at. The pad itself is drawn on the bridge since 2026-11-16 (Art Island.cs): until then a white
        // circle from here was all there was of it.
        void TutMarks(bool hidden)
        {
            var tut = m.Tut;
            bool on = !hidden && tut != null && m.State == MatchState.Play && !tut.Done;
            bool ring = on && tut.RingOn;
            rings.SetGoal(ring, ring ? Coords.World(tut.RingX, tut.RingY) : Vector3.zero);
            bool point = on && tut.PointAt != null && tut.PointAt.Alive;
            Vector3 over = Vector3.zero;
            if (point)
            {
                // over a minion's head; a hero (the drag lesson's Fenn) is taller and has his health bar and his name there:
                // over the name (HudOverhead.cs puts it just above the bar)
                var u = tut.PointAt;
                over = u.Kind != UnitKind.Hero ? views.DrawnAt(u) + Vector3.up * 1.4f
                    : views.BarOf(u, out Vector3 bar, out _) ? bar + Vector3.up * 0.9f : views.DrawnAt(u) + Vector3.up * 3f;
            }
            Chevron(pointChevron, point, over);
            // one arrow at a time: the ring's waits while the joystick's is up (the very first line)
            bool ringArrow = ring && tut.ArrowOnRing && !(island && tut.Step == Tutorial.IslandWalk && !joyUsed);
            Chevron(ringChevron, ringArrow, ringArrow ? Coords.World(tut.RingX, tut.RingY) + Vector3.up * 0.9f : Vector3.zero);
        }

        // The guide card (Rickard 2026-10-26: the old bubble covered the action, then "snyggare text"; 2026-11-15 one card for
        // both halves): a dark navy card with a soft drop shadow, a gold edge and a faint sheen under the pause button, never over
        // the island or the fight; the guide's face in a gold ring at its left with her name on a teal tab over it, the line
        // beside her with the marked words red (teal for a tip), and the lessons' dots on its top edge at the right.
        void GuideCard(bool on, Tutorial tu, float W, float H)
        {
            string src = on ? tu.LineMarked ?? tu.Line : null;
            bool praise = false;
            if (on && src == null && tu.Lesson > 0 && tu.Lesson <= Praise.Length && m.Time >= tu.LessonAt && m.Time - tu.LessonAt < PraiseTime)
            {
                src = Praise[tu.Lesson - 1];
                praise = src != null;
            }
            bool show = src != null;
            float now = Time.unscaledTime;
            bool wasHidden = cardAlpha <= 0f;
            cardAlpha = Mathf.MoveTowards(cardAlpha, show ? 1f : 0f, Time.unscaledDeltaTime / CardFade);
            bool active = show || cardAlpha > 0f;
            if (card.gameObject.activeSelf != active) card.gameObject.SetActive(active);
            if (!active) { cardSrc = null; return; }

            bool lay = false;
            if (show && !ReferenceEquals(src, cardSrc))
            {
                if (wasHidden) cardShownAt = now;
                cardSrc = src; cardTextAt = now;
                if (cardLine.gameObject.activeSelf == praise) cardLine.gameObject.SetActive(!praise);
                if (cardPraise.gameObject.activeSelf != praise) cardPraise.gameObject.SetActive(praise);
                if (praise) cardPraise.text = src;
                else
                {
                    cardLineSrc = src; cardLineTip = tu.LineTip;
                    cardWho.text = tu.Speaker ?? "";
                    cardTab.gameObject.SetActive(!string.IsNullOrEmpty(tu.Speaker));
                    lay = true; // a new line: its rows and its height are measured again
                }
            }
            // under the pause button (its ring's lower edge, HudCenter.cs), with room for the dots that sit on the card's edge
            float top = Mathf.Max(H * 0.105f, pauseRoot.sizeDelta.y - pauseRoot.anchoredPosition.y + W * 0.03f);
            if (lay || W != cardLaidW || H != cardLaidH || top != cardLaidTop)
            {
                cardLaidW = W; cardLaidH = H; cardLaidTop = top;
                LayCard(tu, W, H, top);
            }

            // every frame only what moves: a new line settles in from a little small and the word of praise lands from big, the
            // dots, and the whole card springing in (from 0.85 of its size with a little overshoot) and fading
            float tk = Mathf.Clamp01((now - cardTextAt) / 0.22f), back = (1f - tk) * (1f - tk);
            cardLine.rectTransform.localScale = Vector3.one * (1f - 0.06f * back);
            cardPraise.rectTransform.localScale = Vector3.one * (1f + 0.35f * back);
            CardDots(tu);
            float ck = Mathf.Clamp01((now - cardShownAt) / 0.32f);
            card.localScale = Vector3.one * (ck >= 1f ? 1f : 1f - 0.15f * Mathf.Cos(ck * Mathf.PI * 1.5f) * (1f - ck));
            cardGroup.alpha = cardAlpha;
        }

        // The card's sizes and places: only when its line, the screen or the pause button's place changed, not every frame.
        void LayCard(Tutorial tu, float W, float H, float top)
        {
            float fs = Mathf.Round(W * 0.048f), pad = fs * 0.75f, w = W * 0.9f;
            float pd = fs * 2.7f, inset = pad * 0.62f;                               // her face's width, and its gap to the card's edge
            float textL = cardHasFace ? inset + pd + inset : pad, textR = inset;     // the text's right margin is the face's left one
            cardLine.fontSize = Mathf.RoundToInt(fs);
            cardPraise.fontSize = Mathf.RoundToInt(fs * 1.4f);
            cardWho.fontSize = Mathf.RoundToInt(fs * 0.62f);
            float tw = w - textL - textR;
            if (cardLineSrc != null && (!ReferenceEquals(cardLineSrc, cardBuiltSrc) || tw != cardBuiltW))
            {
                cardBuiltSrc = cardLineSrc; cardBuiltW = tw;
                cardLine.text = LineText(cardLineSrc, cardLineTip, tw);
            }
            // two rows of text at least, so the card keeps its size from line to line (every line is written to fit two)
            var settings = cardLine.GetGenerationSettings(new Vector2(tw, 0f));
            var gen = cardLine.cachedTextGeneratorForLayout;
            float unit = Mathf.Max(0.01f, cardLine.pixelsPerUnit);
            float th = Mathf.Max(gen.GetPreferredHeight("A\nA", settings), gen.GetPreferredHeight(cardLine.text, settings)) / unit;
            float h = th + pad * 1.7f;
            card.sizeDelta = new Vector2(w, h);
            card.anchoredPosition = new Vector2(0f, H / 2f - top - h / 2f);
            float r = Mathf.Min(h / 2f, fs * 0.9f);
            Corners(cardPlate, r); Corners(cardShadow, r); Corners(cardSheen, r * 0.8f);
            RoundRing(cardEdge, r, Mathf.Max(2f, W * 0.005f));
            cardShadow.rectTransform.anchoredPosition = new Vector2(0f, -fs * 0.18f);
            cardSheen.rectTransform.sizeDelta = new Vector2(-pad * 0.6f, -h * 0.55f);
            cardSheen.rectTransform.anchoredPosition = new Vector2(0f, h * 0.2f);
            // her face at the left, the line (or the word of praise) beside it
            if (cardHasFace)
            {
                cardFace.sizeDelta = new Vector2(pd, pd);
                cardFace.anchoredPosition = new Vector2(inset + pd / 2f, 0f);
            }
            cardLine.rectTransform.offsetMin = new Vector2(textL, pad * 0.8f);
            cardLine.rectTransform.offsetMax = new Vector2(-textR, -pad * 0.9f);
            cardPraise.rectTransform.offsetMin = new Vector2(textL, 0f);
            cardPraise.rectTransform.offsetMax = new Vector2(-textR, 0f);
            // the speaker tab: her name on teal, over the card's top edge above her face
            float tabW = cardWho.preferredWidth + fs * 1.1f, tab = fs * 1.1f;
            cardTab.sizeDelta = new Vector2(tabW, tab);
            cardTab.anchoredPosition = new Vector2(cardHasFace ? Mathf.Max(inset + pd / 2f, pad * 0.4f + tabW / 2f) : pad + tabW / 2f, tab * 0.12f);
            Corners(cardTabPlate, tab / 2f);
            // the lessons' dots in a small plate on the top edge at the right, a wider gap between the island's five and the arena's
            int n = Mathf.Min(tu.Lessons, cardDot.Length);
            if (cardDots.gameObject.activeSelf != n > 0) cardDots.gameObject.SetActive(n > 0);
            float d = W * 0.017f, g = W * 0.009f, mid = W * 0.012f, side = W * 0.02f, dh = d + W * 0.022f;
            cardDots.sizeDelta = new Vector2(n * d + (n - 1) * g + (n > Tutorial.IslandLessons ? mid : 0f) + 2f * side, dh);
            cardDots.anchoredPosition = new Vector2(-pad * 1.5f, 0f);
            Corners(cardDotsPlate, dh / 2f);
            RoundRing(cardDotsEdge, dh / 2f, Mathf.Max(1.5f, W * 0.0035f));
            for (int i = 0; i < cardDot.Length; i++)
            {
                var img = cardDot[i];
                if (img.enabled != i < n) img.enabled = i < n;
                img.rectTransform.sizeDelta = new Vector2(d, d);
                img.rectTransform.anchoredPosition = new Vector2(side + d / 2f + i * (d + g) + (i >= Tutorial.IslandLessons ? mid : 0f), 0f);
            }
        }

        // The line as the card shows it: the |marked| words coloured (red, teal for a tip), and a line that needs two rows broken
        // by hand: at a sentence's end (. ! ? :) when both rows fit, else where the two rows are most even. Unity's own wrap fills
        // the first row and leaves the rest, which put a last word alone on its row ("Three enemies! Stand in the gold / ring.").
        // A line that fits one row, or needs three, is left to Unity. Run only when the line or the card's width changes.
        string LineText(string src, bool tip, float width)
        {
            int cut = -1;
            var gen = cardLine.cachedTextGeneratorForLayout;
            var settings = cardLine.GetGenerationSettings(Vector2.zero);
            float fit = width * 0.99f * cardLine.pixelsPerUnit; // the generator measures in pixels
            if (gen.GetPreferredWidth(Unmarked(src, 0, src.Length), settings) > fit)
            {
                float best = float.MaxValue;
                for (int i = 1; i < src.Length - 1; i++)
                {
                    if (src[i] != ' ') continue;
                    float l = gen.GetPreferredWidth(Unmarked(src, 0, i), settings), r = gen.GetPreferredWidth(Unmarked(src, i + 1, src.Length), settings);
                    if (l > fit || r > fit) continue;
                    char end = src[i - 1] == '|' && i > 1 ? src[i - 2] : src[i - 1]; // the first row's last character (a mark may close after it)
                    float cost = Mathf.Max(l, r) - (end == '.' || end == '!' || end == '?' || end == ':' ? fit : 0f);
                    if (cost < best) { best = cost; cut = i; }
                }
            }
            sb.Length = 0;
            bool open = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c != '|') { sb.Append(i == cut ? '\n' : c); continue; }
                sb.Append(open ? "</color>" : tip ? "<color=#7fe8d8>" : "<color=#ff6b78>");
                open = !open;
            }
            if (open) sb.Append("</color>");
            return sb.ToString();
        }

        // src from..to (to not included) without its | marks
        static string Unmarked(string src, int from, int to) => src.Substring(from, to - from).Replace("|", "");

        // The lessons' dots: gold when done (the newest pops), the one in hand white and breathing, the rest faint.
        void CardDots(Tutorial tu)
        {
            int n = Mathf.Min(tu.Lessons, cardDot.Length);
            float pop = Mathf.Clamp01((m.Time - tu.LessonAt) / DotPop);
            float breathe = 1.2f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < n; i++)
            {
                bool done = i < tu.Lesson, cur = i == tu.Lesson;
                cardDot[i].color = done ? TutGold : cur ? Color.white : DotTodo;
                float sc = cur ? breathe : done && i == tu.Lesson - 1 && pop < 1f && m.Time >= tu.LessonAt ? 1f + 0.9f * Mathf.Sin(pop * Mathf.PI) : 1f;
                cardDot[i].rectTransform.localScale = new Vector3(sc, sc, 1f);
            }
        }

        // The drag lesson's demonstration, a round every GhostPeriod s while the drag is asked for and no finger is on the button:
        // a finger's dot (the real drag's own, Hud.DragMarks) lands on SPECIAL, rests, slides out the way Fenn stands from Tyra
        // with a gold streak behind it, holds there and lets go. Hud.Aim draws the aim line for it from ghostOffset, as it
        // would for a real finger there, so the player sees what the slide does on the ground: the line from Tyra through Fenn.
        void DragGhost(bool on, Tutorial tu, Unit p)
        {
            var f = on ? tu.Hider : null;
            on = f != null && f.Alive && p.Alive;
            if (ghostDot.enabled != on) { ghostDot.enabled = on; ghostStreak.enabled = on; }
            if (!on) { ghostAim = 0f; ghostAt = -1f; return; }
            float now = Time.unscaledTime, R = SpecialButton.Radius;
            if (ghostAt < 0f) ghostAt = now;
            // the way Fenn stands from Tyra as a direction on the screen (what Coords.ScreenAngle turns back into the map's angle)
            Vector2 dir = new Vector2(f.X - p.X, -(f.Y - p.Y) * Coords.DepthOnScreen);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
            float k = (now - ghostAt) % GhostPeriod / GhostPeriod;
            // a round: the finger lands (to 0.1), rests, slides out (0.2 to 0.55), holds on him, lets go (0.8 to 0.9), a pause
            float land = Mathf.Clamp01(k / 0.1f), slide = Mathf.Clamp01((k - 0.2f) / 0.35f), gone = Mathf.Clamp01((k - 0.8f) / 0.1f);
            slide = slide * slide * (3f - 2f * slide);
            float dist = slide * GhostRoom(dir, R), a = land * (1f - gone);
            ghostOffset = dir * dist;
            // the line comes as a real drag's does, once the finger is past the drag's start, and goes when it lets go
            ghostAim = 0.85f * Mathf.Clamp01((dist - SpecialButton.DragStart) / (0.25f * R)) * (1f - gone);
            var dot = ghostDot.rectTransform;
            dot.anchoredPosition = ghostOffset;
            dot.localScale = Vector3.one * (1.3f - 0.3f * land + 0.5f * gone); // it lands from a little big and swells as it lets go
            ghostDot.color = new Color(1f, 1f, 1f, 0.9f * a);
            var streak = ghostStreak.rectTransform;
            streak.sizeDelta = new Vector2(Mathf.Max(1f, dist), 0.2f * R); // from the button's centre (its pivot) to the dot
            streak.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            ghostStreak.color = new Color(TutGold.r, TutGold.g, TutGold.b, 0.6f * a * Mathf.Clamp01(dist / (0.3f * R)));
        }

        // How far the demonstration's finger slides along dir, in canvas units: GhostReach radii, but it stops short of the
        // screen's edge, so the dot stays whole on the screen when Fenn stands the way the button's own corner lies (Tyra has
        // walked up past him, or round him; the left-handed layout mirrors the button).
        float GhostRoom(Vector2 dir, float R)
        {
            var cr = canvasRect.rect;
            Vector2 c = specialRoot.rect.center;
            Vector2 lo = (Vector2)specialRoot.InverseTransformPoint(canvasRect.TransformPoint(cr.min)) - c;
            Vector2 hi = (Vector2)specialRoot.InverseTransformPoint(canvasRect.TransformPoint(cr.max)) - c;
            float air = 0.32f * R + 12f, room = GhostReach * R; // the dot's own radius and a little air
            if (dir.x > 1e-3f) room = Mathf.Min(room, (hi.x - air) / dir.x); else if (dir.x < -1e-3f) room = Mathf.Min(room, (lo.x + air) / dir.x);
            if (dir.y > 1e-3f) room = Mathf.Min(room, (hi.y - air) / dir.y); else if (dir.y < -1e-3f) room = Mathf.Min(room, (lo.y + air) / dir.y);
            return Mathf.Max(room, GhostMin * R);
        }

        // Healing shows (Rickard 2026-10-26: "ingen reg syntes"): the green "+N" comes from the HUD's heal numbers (OnHeal, as in
        // every match, so it is not doubled here), and in the heal moment a soft green glow breathes round her feet while she is below full.
        void HealFrame(bool play, Tutorial tu, Unit p)
        {
            bool glow = play && p.Alive && tu.Step == Tutorial.StepHeal && p.Hp < p.MaxHp - 0.5f;
            if (healGlow.enabled != glow) { healGlow.enabled = glow; healRing.enabled = glow; }
            if (!glow) return;
            float ts = TilePx(), k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f), ring = (Time.unscaledTime * 0.9f) % 1f;
            Vector2 at = Local(cam.Camera.WorldToScreenPoint(views.DrawnAt(p)));
            healGlow.rectTransform.anchoredPosition = at;
            healGlow.rectTransform.sizeDelta = new Vector2(ts * (1.5f + 0.2f * k), ts * (1.1f + 0.15f * k));
            healGlow.color = new Color(HealGreen.r, HealGreen.g, HealGreen.b, 0.22f + 0.12f * k);
            healRing.rectTransform.anchoredPosition = at;
            healRing.rectTransform.sizeDelta = new Vector2(ts * (0.6f + 1.4f * ring), ts * (0.45f + 1.05f * ring));
            healRing.color = new Color(HealGreen.r, HealGreen.g, HealGreen.b, 0.8f * (1f - ring));
        }

        // "+ CHARGE" rises from Tyra and flies along an arc into the SPECIAL button, which takes it in with a gold ring
        void ChargeFly(bool play, Unit p, float W, float H)
        {
            var tu = m.Tut;
            float arrive = 0f, t = m.Time;
            Vector2 b = Local(RectTransformUtility.WorldToScreenPoint(null, specialRoot.position));
            Vector2 from = play && p.Alive ? Local(cam.Camera.WorldToScreenPoint(views.DrawnAt(p))) + new Vector2(0f, TilePx() * 2.2f) : Vector2.zero;
            int used = 0;
            for (int i = 0; play && i < tu.ChargeCount && used < chargeTexts.Length; i++)
            {
                float age = t - tu.ChargeTimes[i];
                if (age < 0f || age > ChargeFlight + ChargeArrive) continue;
                float k = Mathf.Clamp01(age / ChargeFlight), e = k * k * (3f - 2f * k);
                if (k >= 1f) { arrive = Mathf.Max(arrive, 1f - (age - ChargeFlight) / ChargeArrive); continue; }
                Vector2 c = new Vector2((from.x + b.x) / 2f + W * 0.08f, Mathf.Max(from.y, b.y) + H * 0.08f);
                Vector2 at = (1 - e) * (1 - e) * from + 2 * (1 - e) * e * c + e * e * b;
                var tx = chargeTexts[used++];
                if (!tx.gameObject.activeSelf) tx.gameObject.SetActive(true);
                tx.rectTransform.anchoredPosition = at;
                tx.fontSize = Mathf.Max(8, Mathf.RoundToInt(W * (0.055f - 0.02f * e)));
                var col = ChargeGold; col.a = Mathf.Clamp01(age / 0.08f); tx.color = col;
            }
            for (int i = used; i < chargeTexts.Length; i++) if (chargeTexts[i].gameObject.activeSelf) chargeTexts[i].gameObject.SetActive(false);
            bool ring = arrive > 0f;
            if (chargeRing.enabled != ring) chargeRing.enabled = ring;
            if (ring)
            {
                chargeRing.color = new Color(1f, 0.86f, 0.4f, arrive);
                chargeRing.rectTransform.sizeDelta = Vector2.one * 2f * SpecialButton.Radius * (1.05f + 0.25f * (1f - arrive));
            }
        }

        // A soft trail of gold lights from Tyra up the lane to the next landmark, each swelling as a wave runs along it: to the
        // gold ring when one is up (the spot to wait at outside the tower's reach), else to Tutorial.Goal (the nearest enemy
        // minion, Knut, the tower); none once the landmark is in her reach.
        void Trail(bool play, Tutorial tu, Unit p)
        {
            int n = 0;
            if (play && p.Alive)
            {
                float tx = 0f, ty = 0f, stop = 0.7f; bool any = false;
                Unit goal = tu.Goal;
                if (tu.RingOn) { tx = tu.RingX; ty = tu.RingY; any = true; }
                else if (goal != null && goal.Alive && SimMath.Hypot(goal.X - p.X, goal.Y - p.Y) > p.Range + goal.R)
                {
                    tx = goal.X; ty = goal.Y; stop = p.Range * 0.8f; any = true;
                }
                if (any)
                {
                    float px = p.X, py = p.Y;
                    float dx = tx - px, dy = ty - py, full = Mathf.Sqrt(dx * dx + dy * dy), len = full - stop;
                    if (len >= 0.8f && full > 0.01f)
                    {
                        dx /= full; dy /= full;
                        float ts = TilePx(), now = Time.unscaledTime;
                        for (float d = 0.7f; d <= len && n < TrailDots; d += 0.55f, n++)
                        {
                            float w = 0.5f + 0.5f * Mathf.Sin(d * 2.2f - now * 6f), a = 0.25f + 0.65f * w * w * w;
                            Vector2 at = Local(cam.Camera.WorldToScreenPoint(Coords.World(px + dx * d, py + dy * d)));
                            Dot(trailGlow[n], at, ts * 0.4f, new Color(1f, 0.84f, 0.4f, a * 0.35f));
                            Dot(trailCore[n], at, ts * 0.16f, new Color(1f, 0.93f, 0.62f, a));
                        }
                    }
                }
            }
            for (int i = n; i < TrailDots; i++) { if (trailGlow[i].enabled) trailGlow[i].enabled = false; if (trailCore[i].enabled) trailCore[i].enabled = false; }
        }

        static void Dot(Image img, Vector2 at, float w, Color c)
        {
            if (!img.enabled) img.enabled = true;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.sizeDelta = new Vector2(w, w * 0.75f);
            img.color = c;
        }

        // the enemy tower's bar in TowerHits blocks, one going dark with a white flash for each confirmed hit, over the art's bar
        void TutTowerBar(bool on, Tutorial tu)
        {
            var tw = tu != null ? tu.Tower : null;
            Vector3 centre = Vector3.zero; float bw = 0f;
            on = on && tw != null && tw.Alive && views.BarOf(tw, out centre, out bw);
            if (tutBar.gameObject.activeSelf != on) tutBar.gameObject.SetActive(on);
            if (!on) return;
            float ts = TilePx(), W = safe.rect.width;
            float w = Mathf.Max(ts * 1.7f, bw * ts + 6f), h = Mathf.Max(10f, W * 0.026f), gap = Mathf.Max(2f, W * 0.005f);
            float blk = (w - gap * (Tutorial.TowerHits - 1)) / Tutorial.TowerHits;
            tutBar.anchoredPosition = Local(cam.Camera.WorldToScreenPoint(centre));
            towerBack.rectTransform.sizeDelta = new Vector2(w + 2f * gap + 2f, h + 2f * gap + 2f);
            float flash = Mathf.Clamp01(1f - (m.Time - tu.LastTowerHit) / 0.35f);
            int left = Tutorial.TowerHits - tu.TowerHitsTaken;
            for (int i = 0; i < Tutorial.TowerHits; i++)
            {
                bool lit = i < left;
                var b = towerBlocks[i];
                b.rectTransform.sizeDelta = new Vector2(blk, h);
                b.rectTransform.anchoredPosition = new Vector2(-w / 2f + blk / 2f + i * (blk + gap), 0f);
                b.color = lit ? TowerRed : i == left && flash > 0f ? new Color(1f, 1f, 1f, flash) : TowerOff;
                var s = towerShine[i];
                if (s.enabled != lit) s.enabled = lit;
                s.rectTransform.sizeDelta = new Vector2(blk, h * 0.4f);
                s.rectTransform.anchoredPosition = b.rectTransform.anchoredPosition + new Vector2(0f, h * 0.3f);
            }
        }

        // The end: the tower falls, then TOWER DESTROYED! over the lessons' dots lighting up one by one and one line; after 2.2 s the
        // buttons. The first time a big PLAY FIRST MATCH goes straight into the first match (Rickard 2026-11-16; Home is where a
        // new player can lose the way, and Pokémon Unite follows its first lesson with a whole match against bots), with a small
        // Home under it; a replay from the menus gets one CONTINUE back to Home.
        void TutorialEnd(bool on, float o, float W, float H)
        {
            on = on && o > TutOverBanner;
            if (tutEnd.gameObject.activeSelf != on) tutEnd.gameObject.SetActive(on);
            if (!on) return;
            float k = Mathf.Clamp01((o - TutOverBanner) / 0.35f), k2 = Mathf.Clamp01((o - TutOverBanner - 0.9f) / 0.35f);
            tutEndDim.color = new Color(0.04f, 0.06f, 0.16f, 0.55f * k);
            float fs = W * 0.095f;
            Title(tutEnd1, fs * (1f + 0.25f * (1f - k)), H * 0.4f, W, H);
            Fade(tutEnd1, k);
            int n = endDot.Length;
            float d = W * 0.024f, g = W * 0.014f, mid = W * 0.02f, row = n * d + (n - 1) * g + mid;
            for (int i = 0; i < n; i++)
            {
                float dk = Mathf.Clamp01((o - TutOverBanner - 0.3f - i * 0.05f) / 0.18f);
                var rt = endDot[i].rectTransform;
                rt.sizeDelta = new Vector2(d, d);
                rt.anchoredPosition = new Vector2(-row / 2f + d / 2f + i * (d + g) + (i >= Tutorial.IslandLessons ? mid : 0f), H / 2f - H * 0.435f);
                float sc = 1f + 0.8f * Mathf.Sin(dk * Mathf.PI);
                rt.localScale = new Vector3(sc, sc, 1f);
                endDot[i].color = new Color(TutGold.r, TutGold.g, TutGold.b, dk);
            }
            Title(tutEnd2, W * 0.052f, H * 0.49f, W, H);
            Fade(tutEnd2, k2);
            bool tap = o > TutOverTap;
            if (tutEndButtons.gameObject.activeSelf != tap) tutEndButtons.gameObject.SetActive(tap);
            if (!tap) return;
            tutEndButtons.alpha = Mathf.Clamp01((o - TutOverTap) / 0.25f);
            float bw = W * 0.74f, bh = W * 0.16f, by = H * 0.6f;
            Top((RectTransform)tutEndPlay.transform, W * 0.5f, by + bh / 2f, bw, bh, W, H);
            tutEndPlayText.fontSize = Mathf.RoundToInt(W * 0.068f);
            if (tutEndHome.gameObject.activeSelf != tutFirst) tutEndHome.gameObject.SetActive(tutFirst);
            if (tutFirst)
            {
                float hw = W * 0.34f, hh = W * 0.105f;
                Top((RectTransform)tutEndHome.transform, W * 0.5f, by + bh + W * 0.05f + hh / 2f, hw, hh, W, H);
                tutEndHomeText.fontSize = Mathf.RoundToInt(W * 0.045f);
            }
        }

        static void Fade(Text t, float a) { var c = t.color; if (c.a != a) { c.a = a; t.color = c; } }

        // ---------------------------------------------------------------- helpers

        // one tile in the safe area's units (as HudOverhead sizes its marks from the camera)
        float TilePx() => cam.PixelsPerTile * (Screen.width > 1f ? canvasRect.rect.width / Screen.width : 1f); // canvas units per pixel (the frame can be narrower than the screen)

        Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 local);
            return local;
        }

        // a rect centred at (x, yDown) measured from the safe area's top left, as the old code placed things
        static void Top(RectTransform rt, float x, float yDown, float w, float h, float W, float H)
        {
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x - W / 2f, H / 2f - yDown);
        }

        // a text whose baseline sits at yDown (the old fillText), font size fs
        static void Title(Text t, float fs, float yDown, float W, float H)
        {
            t.fontSize = Mathf.Max(8, Mathf.RoundToInt(fs));
            t.rectTransform.sizeDelta = new Vector2(W * 1.2f, fs * 1.6f);
            t.rectTransform.anchoredPosition = new Vector2(0f, H / 2f - yDown + fs * 0.35f);
        }

        // a large, softly blinking gold arrow pointing down at a control (the old tutPointAt), r = the control's radius
        void Arrow(RectTransform a, CanvasGroup g, bool show, Vector2 at, float r)
        {
            if (a.gameObject.activeSelf != show) a.gameObject.SetActive(show);
            if (!show) return;
            float now = Time.unscaledTime;
            a.anchoredPosition = at + new Vector2(0f, r * 1.1f + Mathf.Sin(now * 5f) * r * 0.15f);
            a.localScale = Vector3.one * (r / 110f);
            g.alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(now * 1000f / 320f));
        }

        static Image Sliced(RectTransform parent, Color color)
        {
            var img = UiKit.Fill(parent, color, UiKit.Rounded);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        // the words get a dark navy edge and a soft shadow under them, so they stand off the card
        static void TextDepth(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(10f / 255f, 14f / 255f, 40f / 255f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
            sh.effectDistance = new Vector2(0f, -4f);
        }

        Text Centred(RectTransform parent, string text, Color color, float edge)
        {
            var t = InkLabel(parent, text, color, edge);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        RectTransform CentredRect(string name, RectTransform parent)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        // ---------------------------------------------------------------- building (after the rest of the HUD, so it sits on top)

        void BuildTutorial()
        {
            // the trail of lights and the tower's bar sit with the world marks, under the controls
            var world = UiKit.NewRect("TutorialWorld", safe);
            UiKit.Stretch(world);
            world.SetSiblingIndex(1);
            for (int i = 0; i < TrailDots; i++)
            {
                trailGlow[i] = Plain(world, "Glow", Color.clear);
                trailGlow[i].sprite = UiKit.Circle;
                trailCore[i] = Plain(world, "Core", Color.clear);
                trailCore[i].sprite = UiKit.Circle;
            }
            tutBar = CentredRect("TowerBlocks", world);
            towerBack = UiKit.Fill(tutBar, new Color(20f / 255f, 10f / 255f, 30f / 255f, 0.8f));
            towerBack.rectTransform.anchorMin = towerBack.rectTransform.anchorMax = towerBack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < Tutorial.TowerHits; i++)
            {
                towerBlocks[i] = UiKit.Fill(tutBar, TowerRed);
                towerBlocks[i].rectTransform.anchorMin = towerBlocks[i].rectTransform.anchorMax = towerBlocks[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                towerShine[i] = UiKit.Fill(tutBar, new Color(1f, 1f, 1f, 0.22f));
                towerShine[i].rectTransform.anchorMin = towerShine[i].rectTransform.anchorMax = towerShine[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            tutBar.gameObject.SetActive(false);

            // SPECIAL's pulse and the ring that takes "+ CHARGE" in
            specialPulse = Circle(specialRoot, "TutPulse", 2f * SpecialButton.Radius, Color.clear).GetComponent<Image>();
            specialPulse.sprite = UiKit.CircleRing;
            specialPulse.enabled = false;
            chargeRing = Circle(specialRoot, "TutCharge", 2f * SpecialButton.Radius, Color.clear).GetComponent<Image>();
            chargeRing.sprite = UiKit.CircleRing;
            chargeRing.enabled = false;
            // the drag lesson's demonstration: a gold streak from the button's centre and the finger's dot at its end, the size of
            // the real drag's dot (Hud.cs "Finger")
            var streak = UiKit.NewRect("TutDragStreak", specialRoot);
            streak.anchorMin = streak.anchorMax = new Vector2(0.5f, 0.5f);
            streak.pivot = new Vector2(0f, 0.5f);
            streak.anchoredPosition = Vector2.zero;
            ghostStreak = streak.gameObject.AddComponent<Image>();
            ghostStreak.sprite = UiKit.Rounded;
            ghostStreak.type = Image.Type.Sliced;
            ghostStreak.color = Color.clear;
            ghostStreak.raycastTarget = false;
            Corners(ghostStreak, 0.1f * SpecialButton.Radius);
            ghostStreak.enabled = false;
            ghostDot = Circle(specialRoot, "TutDragFinger", 0.64f * SpecialButton.Radius, Color.clear).GetComponent<Image>();
            ghostDot.raycastTarget = false;
            ghostDot.enabled = false;

            // the opening title, the guide card, the arrows and "+ CHARGE"
            var opening = CentredRect("Opening", safe);
            tutOpenGroup = opening.gameObject.AddComponent<CanvasGroup>();
            tutOpenGroup.blocksRaycasts = false;
            tutOpen1 = Centred(opening, "DESTROY THE", Color.white, 7);
            tutOpen2 = Centred(opening, "ENEMY TOWER", TutGold, 7);
            tutOpen1.gameObject.SetActive(false); tutOpen2.gameObject.SetActive(false);

            BuildCard();

            joyArrow = MakeChevron(safe);
            joyArrowGroup = joyArrow.gameObject.AddComponent<CanvasGroup>(); joyArrowGroup.blocksRaycasts = false;
            specialArrow = MakeChevron(safe);
            specialArrowGroup = specialArrow.gameObject.AddComponent<CanvasGroup>(); specialArrowGroup.blocksRaycasts = false;
            blinkArrow = MakeChevron(safe);
            blinkArrowGroup = blinkArrow.gameObject.AddComponent<CanvasGroup>(); blinkArrowGroup.blocksRaycasts = false;

            for (int i = 0; i < chargeTexts.Length; i++)
            {
                var c = Centred(CentredRect("Charge", safe), "+ CHARGE", ChargeGold, 5);
                c.GetComponent<Outline>().effectColor = ChargeEdge;
                c.gameObject.SetActive(false);
                chargeTexts[i] = c;
            }

            // the heal glow round Tyra's feet, under the controls
            healGlow = Plain(world, "HealGlow", Color.clear);
            healGlow.sprite = UiKit.Circle;
            healRing = Plain(world, "HealRing", Color.clear);
            healRing.sprite = UiKit.CircleRing;

            // Skip: small and quiet, always there, never the thing to look at
            tutSkip = CentredRect("Skip", safe);
            var plate = tutSkipPlate = tutSkip.gameObject.AddComponent<Image>();
            plate.sprite = UiKit.Rounded;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(20f / 255f, 26f / 255f, 70f / 255f, 0.4f);
            var skipButton = tutSkip.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = plate;
            skipButton.transition = Selectable.Transition.None;
            skipButton.onClick.AddListener(() => { BAM.Audio.BamAudio.Play(BAM.Audio.SfxId.UiBack); flow.SkipTutorial(); });
            tutSkipText = UiKit.Label(tutSkip, "Skip", 36, TextAnchor.MiddleCenter, new Color(232f / 255f, 236f / 255f, 1f, 0.7f));
            tutSkipText.raycastTarget = false;
            UiKit.Stretch(tutSkipText.rectTransform);
            tutSkip.gameObject.SetActive(false);

            BuildTutorialEnd();

            // the picture of Home over everything at the very first start
            var pic = UiKit.NewRect("Dissolve", canvasRect);
            UiKit.Stretch(pic);
            dissolve = pic.gameObject.AddComponent<RawImage>();
            dissolve.raycastTarget = false;
            dissolve.enabled = false;
            pic.pivot = new Vector2(0.5f, 0.4f); // it grows towards the hero on Home's platform
        }

        void BuildCard()
        {
            card = CentredRect("GuideCard", safe);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;
            cardShadow = Sliced(card, new Color(0f, 0f, 0.05f, 0.4f));
            cardPlate = Sliced(card, CardFill);
            cardSheen = Sliced(card, new Color(1f, 1f, 1f, 0.07f));
            cardEdge = UiKit.Fill(card, new Color(TutGold.r, TutGold.g, TutGold.b, 0.85f));
            cardEdge.raycastTarget = false;

            // the guide's face: her portrait cut round, in a gold ring (the top strip's portraits, HudTop.MakePortrait)
            cardFace = UiKit.NewRect("Face", card);
            cardFace.anchorMin = cardFace.anchorMax = new Vector2(0f, 0.5f);
            cardFace.pivot = new Vector2(0.5f, 0.5f);
            var mask = UiKit.Fill(cardFace, PortraitBack, UiKit.Circle);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var picRt = UiKit.NewRect("Pic", mask.transform);
            UiKit.Stretch(picRt);
            cardPic = picRt.gameObject.AddComponent<RawImage>();
            cardPic.raycastTarget = false;
            cardPic.uvRect = CardFace;
            var faceEdge = UiKit.Fill(cardFace, TutGold, UiKit.CircleRing);
            UiKit.Stretch(faceEdge.rectTransform, -1.5f, -1.5f, -1.5f, -1.5f);

            cardLine = UiKit.Label(card, "", 40, TextAnchor.MiddleLeft, Color.white);
            UiKit.Stretch(cardLine.rectTransform);
            cardLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            cardLine.verticalOverflow = VerticalWrapMode.Overflow;
            cardLine.supportRichText = true;
            cardLine.raycastTarget = false;
            cardLine.lineSpacing = 1.05f;
            TextDepth(cardLine);
            cardPraise = UiKit.Label(card, "", 56, TextAnchor.MiddleCenter, TutGold);
            UiKit.Stretch(cardPraise.rectTransform);
            cardPraise.horizontalOverflow = HorizontalWrapMode.Overflow;
            cardPraise.verticalOverflow = VerticalWrapMode.Overflow;
            cardPraise.raycastTarget = false;
            TextDepth(cardPraise);
            cardPraise.gameObject.SetActive(false);

            cardTab = UiKit.NewRect("Speaker", card);
            cardTab.anchorMin = cardTab.anchorMax = new Vector2(0f, 1f);
            cardTab.pivot = new Vector2(0.5f, 0.5f);
            cardTabPlate = Sliced(cardTab, TabTeal);
            cardWho = UiKit.Label(cardTab, "", 28, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(cardWho.rectTransform);
            cardWho.horizontalOverflow = HorizontalWrapMode.Overflow;
            cardWho.raycastTarget = false;
            TextDepth(cardWho);

            cardDots = UiKit.NewRect("Lessons", card);
            cardDots.anchorMin = cardDots.anchorMax = new Vector2(1f, 1f);
            cardDots.pivot = new Vector2(1f, 0.5f);
            cardDotsPlate = Sliced(cardDots, new Color(CardFill.r, CardFill.g, CardFill.b, 0.97f));
            cardDotsEdge = UiKit.Fill(cardDots, new Color(TutGold.r, TutGold.g, TutGold.b, 0.85f));
            cardDotsEdge.raycastTarget = false;
            for (int i = 0; i < cardDot.Length; i++)
            {
                var d = UiKit.NewRect("Dot", cardDots);
                d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f);
                d.pivot = new Vector2(0.5f, 0.5f);
                cardDot[i] = d.gameObject.AddComponent<Image>();
                cardDot[i].sprite = UiKit.Circle;
                cardDot[i].raycastTarget = false;
            }
            card.gameObject.SetActive(false);
        }

        // the end: a dim screen that takes every touch, the words, the dots and the buttons (the menus' own, UiKit.Button)
        void BuildTutorialEnd()
        {
            tutEnd = UiKit.NewRect("TutorialEnd", safe);
            UiKit.Stretch(tutEnd);
            tutEndDim = UiKit.Fill(tutEnd, Color.clear);
            UiKit.Stretch(tutEndDim.rectTransform, -2000, -400, -2000, -400); // past the safe area's edges too, and a foldable's sides
            tutEndDim.raycastTarget = true;
            tutEnd1 = Centred(tutEnd, "TOWER DESTROYED!", TutGold, 7);
            for (int i = 0; i < endDot.Length; i++)
            {
                endDot[i] = Plain(tutEnd, "Dot", Color.clear);
                endDot[i].sprite = UiKit.Circle;
                endDot[i].enabled = true;
            }
            tutEnd2 = Centred(tutEnd, "", Color.white, 5);
            var buttons = UiKit.NewRect("Buttons", tutEnd);
            UiKit.Stretch(buttons);
            tutEndButtons = buttons.gameObject.AddComponent<CanvasGroup>();
            tutEndPlay = UiKit.Button(buttons, "PLAY FIRST MATCH", () => { if (tutFirst) flow.FinishTutorialAndPlay(); else flow.FinishTutorial(); }, UiKit.Orange, BAM.Audio.SfxId.UiConfirm, 72);
            tutEndPlayText = UiKit.ButtonText(tutEndPlay);
            UiKit.AddOutline(tutEndPlayText, 2); // the burnt orange edge PLAY's text has on Home
            PlayLook(tutEndPlay);
            tutEndHome = UiKit.Button(buttons, "Home", () => flow.FinishTutorial(), UiKit.Muted, BAM.Audio.SfxId.UiTap, 48);
            tutEndHomeText = UiKit.ButtonText(tutEndHome);
            buttons.gameObject.SetActive(false);
            tutEnd.gameObject.SetActive(false);
        }

        // The big button wears Home's PLAY look: Rickard's own orange button picture on its face (Home's and the Modes sheet's
        // PLAY, HomeScreen.BuildPlay); without the picture it stays the menus' plain orange.
        static void PlayLook(Button b)
        {
            var tex = Resources.Load<Texture2D>("BAM/Menu/menu_btn_orange");
            var face = b.targetGraphic;
            if (tex == null || face == null) return;
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var art = UiKit.NewRect("Art", face.transform);
            UiKit.Stretch(art);
            art.SetAsFirstSibling(); // under the button's gloss and words
            var raw = art.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
        }

        void BeginTutorial()
        {
            // the first time through: asked here, in both halves, because the tutorial counts as done the moment the tower falls
            tutFirst = tutorial && !BAM.Core.BamSettings.TutorialDone;
            joyUsed = false;
            lastLesson = m.Tut != null ? m.Tut.Lesson : 0;
            cardSrc = cardLineSrc = cardBuiltSrc = null; cardAlpha = 0f; cardLaidW = -1f;
            landPending = m.Tut != null && !m.Tut.Island;
            card.gameObject.SetActive(false);
            if (tutorial)
            {
                // the guide's face (the hero played, Tyra): the card does without it if her picture is missing
                var face = Resources.Load<Texture2D>("BAM/Menu/portrait_" + HeroCatalog.FileName(HeroCatalog.Get(m.Player.Hero).Name));
                cardHasFace = face != null;
                cardPic.texture = face;
                cardFace.gameObject.SetActive(cardHasFace);
                tutEndPlayText.text = tutFirst ? "PLAY FIRST MATCH" : "CONTINUE";
                tutEnd2.text = tutFirst ? "You're ready for your first match!" : "Well played!";
            }
            healGlow.enabled = healRing.enabled = false;
            tutSkip.gameObject.SetActive(false);
            tutBar.gameObject.SetActive(false);
            tutEnd.gameObject.SetActive(false);
            tutEndButtons.gameObject.SetActive(false);
            joyArrow.gameObject.SetActive(false);
            specialArrow.gameObject.SetActive(false);
            blinkArrow.gameObject.SetActive(false);
            specialPulse.enabled = chargeRing.enabled = false;
            ghostDot.enabled = ghostStreak.enabled = false; ghostAim = 0f; ghostAt = -1f;
            for (int i = 0; i < chargeTexts.Length; i++) chargeTexts[i].gameObject.SetActive(false);
            for (int i = 0; i < TrailDots; i++) trailGlow[i].enabled = trailCore[i].enabled = false;
        }
    }
}
