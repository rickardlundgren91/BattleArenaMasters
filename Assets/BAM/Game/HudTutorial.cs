using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The guided match's own screen, copied from the old game (ABC2 Hud.cs drawTutHUD, drawTutWorld, drawTutTowerBar,
    /// drawTutSkip, drawTutEnd and HudTutorialIsland.drawTutDissolve; Rickard 2026-10-26 "alla"):
    /// - Skip, small and quiet in the top right corner in both halves: the tutorial counts as done, then Home.
    /// - The arena half: DESTROY THE / ENEMY TOWER while the camera rests on the tower, then one short line in a navy pill at the
    ///   top, a soft trail of gold lights up the lane to the next landmark (the ring, the target, Knut, the tower), a gold arrow
    ///   over the joystick in moment 1 and over SPECIAL when it is ready (with a pulsing ring), "+ CHARGE" flying from Tyra into
    ///   the button on every confirmed hit, the enemy tower's bar in Tutorial.TowerHits blocks, and at the end TOWER DESTROYED!
    ///   Great job! You're ready for BAM! with Tap to continue (no VICTORY, no result card).
    /// - The very first start: a picture of Home zooms in and fades into the island (Dissolve).
    /// Everything is placed in the safe area's own units, as the old code drew in the frame's W and H.
    /// </summary>
    sealed partial class Hud
    {
        const float TutDissolveTime = 1.6f, TutLineY = 0.165f, TutLineH = 0.05f, TutOverBanner = 1f, TutOverTap = 2.2f;
        const float ChargeFlight = 0.65f, ChargeArrive = 0.15f;
        const int TrailDots = 28;
        static readonly Color TutGold = Hex(0xffcf4a), ChargeGold = Hex(0xffc94a), ChargeEdge = Hex(0x3a1a00), TowerRed = Hex(0xff3b4e);
        static readonly Color TowerOff = new Color(0.25f, 0.12f, 0.2f, 1f), TutPillFill = new Color(14f / 255f, 20f / 255f, 58f / 255f, 0.86f);

        RectTransform tutSkip, tutPill, tutBar, tutEnd, joyArrow, specialArrow;
        CanvasGroup tutPillGroup, tutOpenGroup;
        Image tutPillEdge, tutPillPlate, specialPulse, chargeRing, tutEndDim;
        Text tutPillText, tutOpen1, tutOpen2, tutEnd1, tutEnd2, tutEnd3, tutEndTap, tutSkipText;
        readonly Text[] chargeTexts = new Text[8];
        readonly Image[] towerBlocks = new Image[Tutorial.TowerHits], towerShine = new Image[Tutorial.TowerHits];
        readonly Image[] trailGlow = new Image[TrailDots], trailCore = new Image[TrailDots];
        Image towerBack, tutSkipPlate;
        Button tutEndButton;
        RawImage dissolve;
        float dissolveAt = -1f;
        string pillShown;

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
            dissolveAt = -1f;
            dissolve.enabled = false;
            Object.Destroy(dissolve.texture);
            dissolve.texture = null;
        }

        // ---------------------------------------------------------------- every frame

        void TutorialFrame(bool hidden)
        {
            DissolveFrame();
            var tu = m.Tut;
            bool on = tu != null && !hidden;
            float over = flow.OverSeconds;
            bool skip = on && m.State == MatchState.Play && !tu.Done && !tu.Jumping;
            if (tutSkip.gameObject.activeSelf != skip) tutSkip.gameObject.SetActive(skip);
            bool arena = on && !island;
            Vector2 size = safe.rect.size;
            float W = size.x, H = size.y;
            if (W < 1f) return;
            float t = m.Time;
            var p = m.Player;
            bool play = arena && m.State == MatchState.Play && !tu.Done;

            // the skip button: W*0.8, H*0.055, W*0.15 x H*0.04 from the top left
            if (skip) { Top(tutSkip, W * 0.875f, H * 0.075f, W * 0.15f, H * 0.04f, W, H); tutSkipText.fontSize = Mathf.RoundToInt(W * 0.034f); Corners(tutSkipPlate, H * 0.02f); }

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

            // the one line in a pill at the top, fading in
            string line = play && !opening ? tu.Line : null;
            if (tutPill.gameObject.activeSelf != (line != null)) tutPill.gameObject.SetActive(line != null);
            if (line != null)
            {
                float fs = Mathf.Round(W * 0.05f), ph = H * TutLineH;
                tutPillText.fontSize = Mathf.RoundToInt(fs);
                if (!ReferenceEquals(line, pillShown)) { pillShown = line; tutPillText.text = line; }
                float pw = Mathf.Min(W - 32f, tutPillText.preferredWidth + fs * 1.6f);
                Top(tutPill, W * 0.5f, H * TutLineY, pw, ph, W, H);
                Corners(tutPillPlate, ph / 2f);
                RoundRing(tutPillEdge, ph / 2f, 2.5f * W / 400f);
                tutPillGroup.alpha = Mathf.Clamp01(tu.LineShown / 0.3f);
            }

            // the arrows over the joystick (moment 1) and over SPECIAL when it is ready (moment 3), and SPECIAL's gold pulse
            bool joyOn = play && !opening && tu.Step == Tutorial.StepMove;
            Arrow(joyArrow, joyOn, joyOn ? Local(RectTransformUtility.WorldToScreenPoint(null, joy.Base.position)) : Vector2.zero, JoystickZone.Radius * 1.25f);
            bool spOn = play && tu.Step == Tutorial.StepSpecial && !tu.BurstDone && p.Alive && p.SpecialReady;
            Arrow(specialArrow, spOn, spOn ? Local(RectTransformUtility.WorldToScreenPoint(null, specialRoot.position)) : Vector2.zero, SpecialButton.Radius);
            if (specialPulse.enabled != spOn) specialPulse.enabled = spOn;
            if (spOn)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                specialPulse.color = new Color(1f, 0.86f, 0.4f, 0.45f + 0.45f * k);
                specialPulse.rectTransform.sizeDelta = Vector2.one * 2f * SpecialButton.Radius * (1.12f + 0.1f * k);
            }

            ChargeFly(play, p, W, H);
            Trail(play && !opening, tu, p);
            TutTowerBar(arena && m.State != MatchState.Over, tu);
            TutorialEnd(arena && over >= 0f && tu.Done, over, W, H);
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

        // a soft trail of gold lights from Tyra up the lane to the next landmark, each swelling as a wave runs along it; none once
        // the landmark is in her reach
        void Trail(bool play, Tutorial tu, Unit p)
        {
            int n = 0;
            if (play && p.Alive)
            {
                float tx = 0f, ty = 0f, stop = 0.6f; bool any = false;
                Unit goal = null;
                if (tu.Step == Tutorial.StepMove) { tx = Tutorial.MarkX; ty = Tutorial.MarkY; any = true; }
                else if (tu.Step == Tutorial.StepTower) goal = tu.Tower;
                else if (tu.Step == Tutorial.StepFight) goal = tu.Guard;
                else if (tu.Dummy != null && tu.Dummy.Alive && !tu.BurstDone && (tu.Step == Tutorial.StepAuto || p.SpecialReady)) goal = tu.Dummy;
                if (goal != null && goal.Alive && SimMath.Hypot(goal.X - p.X, goal.Y - p.Y) > p.Range + goal.R)
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
            // moment 1's ring on the lane: the ground marks the island uses (gold pulse as the old pair of widening ellipses)
            if (marks != null && !island)
            {
                marks.Hide();
                if (play && tu.Step == Tutorial.StepMove) marks.ShowRange(Coords.World(Tutorial.MarkX, Tutorial.MarkY), 0.6f + 0.25f * ((Time.unscaledTime / 0.9f) % 1f));
            }
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

        // the end: the tower falls, then TOWER DESTROYED! and two lines; after 2.2 s a tap goes on to Home
        void TutorialEnd(bool on, float o, float W, float H)
        {
            on = on && o > TutOverBanner;
            if (tutEnd.gameObject.activeSelf != on) tutEnd.gameObject.SetActive(on);
            if (!on) return;
            float k = Mathf.Clamp01((o - TutOverBanner) / 0.35f), k2 = Mathf.Clamp01((o - TutOverBanner - 0.6f) / 0.35f);
            tutEndDim.color = new Color(0.04f, 0.06f, 0.16f, 0.55f * k);
            float fs = W * 0.095f;
            Title(tutEnd1, fs * (1f + 0.25f * (1f - k)), H * 0.4f, W, H);
            Fade(tutEnd1, k);
            Title(tutEnd2, W * 0.055f, H * 0.48f, W, H);
            Title(tutEnd3, W * 0.055f, H * 0.48f + W * 0.075f, W, H);
            Fade(tutEnd2, k2); Fade(tutEnd3, k2);
            bool tap = o > TutOverTap;
            if (tutEndTap.gameObject.activeSelf != tap) tutEndTap.gameObject.SetActive(tap);
            if (tap) Title(tutEndTap, W * 0.045f, H * 0.86f, W, H);
            tutEndButton.interactable = tap;
        }

        static void Fade(Text t, float a) { var c = t.color; if (c.a != a) { c.a = a; t.color = c; } }

        // ---------------------------------------------------------------- helpers

        // one tile in the safe area's units (as HudOverhead sizes its marks from the camera)
        float TilePx() => cam.PixelsPerTile * (Screen.width > 1f ? safe.rect.width / Screen.width : 1f);

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
        void Arrow(RectTransform a, bool show, Vector2 at, float r)
        {
            if (a.gameObject.activeSelf != show) a.gameObject.SetActive(show);
            if (!show) return;
            float now = Time.unscaledTime;
            a.anchoredPosition = at + new Vector2(0f, r * 1.1f + Mathf.Sin(now * 5f) * r * 0.15f);
            a.localScale = Vector3.one * (r / 110f);
            a.GetComponent<CanvasGroup>().alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(now * 1000f / 320f));
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

            // the opening title, the line's pill, the arrows and "+ CHARGE"
            var opening = CentredRect("Opening", safe);
            tutOpenGroup = opening.gameObject.AddComponent<CanvasGroup>();
            tutOpenGroup.blocksRaycasts = false;
            tutOpen1 = Centred(opening, "DESTROY THE", Color.white, 7);
            tutOpen2 = Centred(opening, "ENEMY TOWER", TutGold, 7);
            tutOpen1.gameObject.SetActive(false); tutOpen2.gameObject.SetActive(false);

            tutPill = CentredRect("Line", safe);
            tutPillGroup = tutPill.gameObject.AddComponent<CanvasGroup>();
            tutPillGroup.blocksRaycasts = false;
            tutPillPlate = UiKit.Fill(tutPill, TutPillFill, UiKit.Rounded);
            tutPillPlate.type = Image.Type.Sliced;
            tutPillPlate.raycastTarget = false;
            tutPillEdge = UiKit.Fill(tutPill, new Color(1f, 207f / 255f, 74f / 255f, 0.85f));
            tutPillEdge.raycastTarget = false;
            tutPillText = UiKit.Label(tutPill, "", 40, TextAnchor.MiddleCenter, Color.white);
            tutPillText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tutPillText.raycastTarget = false;
            UiKit.Stretch(tutPillText.rectTransform);
            tutPill.gameObject.SetActive(false);

            joyArrow = MakeChevron(safe);
            joyArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            specialArrow = MakeChevron(safe);
            specialArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            for (int i = 0; i < chargeTexts.Length; i++)
            {
                var c = Centred(CentredRect("Charge", safe), "+ CHARGE", ChargeGold, 5);
                c.GetComponent<Outline>().effectColor = ChargeEdge;
                c.gameObject.SetActive(false);
                chargeTexts[i] = c;
            }

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

            // the end: a dim screen with the words, and a tap anywhere after 2.2 s goes on
            tutEnd = UiKit.NewRect("TutorialEnd", safe);
            UiKit.Stretch(tutEnd);
            tutEndDim = UiKit.Fill(tutEnd, Color.clear);
            UiKit.Stretch(tutEndDim.rectTransform, -400, -400, -400, -400); // past the safe area's edges too
            tutEndDim.raycastTarget = true;
            tutEndButton = tutEnd.gameObject.AddComponent<Button>();
            tutEndButton.targetGraphic = tutEndDim;
            tutEndButton.transition = Selectable.Transition.None;
            tutEndButton.onClick.AddListener(() => { BAM.Audio.BamAudio.Play(BAM.Audio.SfxId.UiConfirm); flow.FinishTutorial(); });
            tutEnd1 = Centred(tutEnd, "TOWER DESTROYED!", TutGold, 7);
            tutEnd2 = Centred(tutEnd, "Great job!", Color.white, 5);
            tutEnd3 = Centred(tutEnd, "You're ready for BAM!", Color.white, 5);
            tutEndTap = Centred(tutEnd, "Tap to continue", Color.white, 4);
            tutEnd.gameObject.SetActive(false);

            // the picture of Home over everything at the very first start
            var pic = UiKit.NewRect("Dissolve", canvasRect);
            UiKit.Stretch(pic);
            dissolve = pic.gameObject.AddComponent<RawImage>();
            dissolve.raycastTarget = false;
            dissolve.enabled = false;
            pic.pivot = new Vector2(0.5f, 0.4f); // it grows towards the hero on Home's platform
        }

        void BeginTutorial()
        {
            pillShown = null;
            tutSkip.gameObject.SetActive(false);
            tutPill.gameObject.SetActive(false);
            tutBar.gameObject.SetActive(false);
            tutEnd.gameObject.SetActive(false);
            joyArrow.gameObject.SetActive(false);
            specialArrow.gameObject.SetActive(false);
            specialPulse.enabled = chargeRing.enabled = false;
            for (int i = 0; i < chargeTexts.Length; i++) chargeTexts[i].gameObject.SetActive(false);
            for (int i = 0; i < TrailDots; i++) trailGlow[i].enabled = trailCore[i].enabled = false;
        }
    }
}
