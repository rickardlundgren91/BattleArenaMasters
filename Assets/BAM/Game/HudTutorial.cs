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
    ///   over the joystick in moment 1, over SPECIAL when it is ready for the tap and the drag (with a pulsing ring) and over BLINK, "+ CHARGE" flying from Tyra into
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

        RectTransform tutSkip, tutPill, tutBar, tutEnd, joyArrow, specialArrow, blinkArrow;
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
        string pillShown, islandSrc;
        Text islandLine, islandWho;
        RectTransform islandPanel, islandTab;
        CanvasGroup islandGroup;
        Image islandPlate, islandEdge, islandShadow, islandSheen, islandTabPlate, tutPillShadow, tutPillSheen;
        float islandShownAt, pillShownAt;
        // healing: green "+N" over Tyra on every regen tick, and a soft green glow round him while the heal moment lasts
        float lastTutHp = -1f;
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
            bool skip = on && m.State == MatchState.Play && !tu.Done && !tu.Jumping;
            if (tutSkip.gameObject.activeSelf != skip) tutSkip.gameObject.SetActive(skip);
            bool arena = on && !island;
            Vector2 size = safe.rect.size;
            float W = size.x, H = size.y;
            if (W < 1f) return;
            float t = m.Time;
            var p = m.Player;
            bool play = arena && m.State == MatchState.Play && !tu.Done;

            // the skip button at the old W*0.8..0.95, but a little higher (H*0.03..0.07) so it clears the pause button under it
            if (skip) { Top(tutSkip, W * 0.875f, H * 0.05f, W * 0.15f, H * 0.04f, W, H); tutSkipText.fontSize = Mathf.RoundToInt(W * 0.034f); Corners(tutSkipPlate, H * 0.02f); }

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
                if (!ReferenceEquals(line, pillShown)) { pillShown = line; tutPillText.text = line; pillShownAt = Time.unscaledTime; }
                float pw = Mathf.Min(W - 32f, tutPillText.preferredWidth + fs * 1.6f);
                Top(tutPill, W * 0.5f, H * TutLineY, pw, ph, W, H);
                Corners(tutPillPlate, ph / 2f); Corners(tutPillShadow, ph / 2f); Corners(tutPillSheen, ph * 0.3f);
                RoundRing(tutPillEdge, ph / 2f, 2.5f * W / 400f);
                tutPillShadow.rectTransform.anchoredPosition = new Vector2(0f, -fs * 0.18f);
                tutPillSheen.rectTransform.sizeDelta = new Vector2(-ph * 0.8f, -ph * 0.6f);
                tutPillSheen.rectTransform.anchoredPosition = new Vector2(0f, ph * 0.2f);
                Pop(tutPill, tutPillGroup, pillShownAt);
            }

            // the arrows over the joystick (moment 1) and over SPECIAL when it is ready (moment 3), and SPECIAL's gold pulse
            bool joyOn = play && !opening && tu.Step == Tutorial.StepMove;
            Arrow(joyArrow, joyOn, joyOn ? Local(RectTransformUtility.WorldToScreenPoint(null, joy.Base.position)) : Vector2.zero, JoystickZone.Radius * 1.25f);
            bool spOn = play && (tu.Step == Tutorial.StepSpecial || tu.Step == Tutorial.StepThrow) && !tu.BurstDone && p.Alive && p.SpecialReady;
            Arrow(specialArrow, spOn, spOn ? Local(RectTransformUtility.WorldToScreenPoint(null, specialRoot.position)) : Vector2.zero, SpecialButton.Radius);
            bool blinkOn = play && tu.Step == Tutorial.StepBlink && p.Alive && m.SpellReady(p) && spellRoot.gameObject.activeSelf;
            Arrow(blinkArrow, blinkOn, blinkOn ? Local(RectTransformUtility.WorldToScreenPoint(null, spellRoot.position)) : Vector2.zero, SpellR);
            if (specialPulse.enabled != spOn) specialPulse.enabled = spOn;
            if (spOn)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                specialPulse.color = new Color(1f, 0.86f, 0.4f, 0.45f + 0.45f * k);
                specialPulse.rectTransform.sizeDelta = Vector2.one * 2f * SpecialButton.Radius * (1.12f + 0.1f * k);
            }

            IslandLine(on && island, tu, W, H);
            HealFrame(play, tu, p);
            ChargeFly(play, p, W, H);
            Trail(play && !opening, tu, p);
            TutTowerBar(arena && m.State != MatchState.Over, tu);
            TutorialEnd(arena && over >= 0f && tu.Done, over, W, H);
        }

        // The island's line (Rickard 2026-10-26: the bubble covered the action, then "snyggare text"): a dark navy card with a
        // soft drop shadow, a gold edge and a faint sheen, in the sky above the island (its top at 0.25 H), the marked words red
        // (teal for a tip), and the speaker on a teal tab over its top left edge, as a speaker in MLBB's tutorial. It pops in.
        void IslandLine(bool on, Tutorial tu, float W, float H)
        {
            string src = on ? tu.LineMarked ?? tu.Line : null;
            bool show = src != null;
            if (islandPanel.gameObject.activeSelf != show) islandPanel.gameObject.SetActive(show);
            if (!show) { islandSrc = null; return; }
            float fs = Mathf.Round(W * 0.048f), pad = fs * 0.75f, w = W * 0.88f;
            if (!ReferenceEquals(src, islandSrc))
            {
                islandSrc = src;
                islandShownAt = Time.unscaledTime;
                sb.Length = 0;
                bool open = false;
                for (int i = 0; i < src.Length; i++)
                {
                    char c = src[i];
                    if (c != '|') { sb.Append(c); continue; }
                    sb.Append(open ? "</color>" : tu.LineTip ? "<color=#7fe8d8>" : "<color=#ff6b78>");
                    open = !open;
                }
                if (open) sb.Append("</color>");
                islandLine.text = sb.ToString();
                islandWho.text = tu.Speaker ?? "";
                islandTab.gameObject.SetActive(!string.IsNullOrEmpty(tu.Speaker));
            }
            islandLine.fontSize = Mathf.RoundToInt(fs);
            islandLine.rectTransform.offsetMin = new Vector2(pad, pad * 0.8f);
            islandLine.rectTransform.offsetMax = new Vector2(-pad, -pad * 0.9f);
            islandPanel.sizeDelta = new Vector2(w, 10f);
            float th = islandLine.cachedTextGeneratorForLayout.GetPreferredHeight(islandLine.text,
                islandLine.GetGenerationSettings(new Vector2(w - 2f * pad, 0f))) / islandLine.pixelsPerUnit;
            float h = th + pad * 1.7f;
            islandPanel.sizeDelta = new Vector2(w, h);
            islandPanel.anchoredPosition = new Vector2(0f, H / 2f - H * 0.25f - h / 2f);
            float r = Mathf.Min(h / 2f, fs * 0.9f);
            Corners(islandPlate, r); Corners(islandShadow, r); Corners(islandSheen, r * 0.8f);
            RoundRing(islandEdge, r, Mathf.Max(2f, W * 0.005f));
            islandShadow.rectTransform.anchoredPosition = new Vector2(0f, -fs * 0.18f);
            islandSheen.rectTransform.sizeDelta = new Vector2(-pad * 0.6f, -h * 0.55f);
            islandSheen.rectTransform.anchoredPosition = new Vector2(0f, h * 0.2f);
            // the speaker tab: the speaker's name on teal, half over the card's top edge
            islandWho.fontSize = Mathf.RoundToInt(fs * 0.62f);
            float tw = islandWho.preferredWidth + fs * 1.1f, tab = fs * 1.15f;
            islandTab.sizeDelta = new Vector2(tw, tab);
            islandTab.anchoredPosition = new Vector2(pad + tw / 2f, tab * 0.2f); // mostly above the card, over its edge
            Corners(islandTabPlate, tab / 2f);
            Pop(islandPanel, islandGroup, islandShownAt);
        }

        // a new line springs in: from 0.85 of its size with a little overshoot, fading in over 0.2 s
        static void Pop(RectTransform rt, CanvasGroup g, float at)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - at) / 0.32f);
            float sc = k >= 1f ? 1f : 1f - 0.15f * Mathf.Cos(k * Mathf.PI * 1.5f) * (1f - k);
            rt.localScale = Vector3.one * sc;
            g.alpha = Mathf.Clamp01((Time.unscaledTime - at) / 0.2f);
        }

        // Healing shows (Rickard 2026-10-26: "ingen reg syntes"): the green "+N" comes from the HUD's heal numbers (OnHeal, as in
        // every match, so it is not doubled here), and in the heal moment a soft green glow breathes round her feet while she is below full.
        void HealFrame(bool play, Tutorial tu, Unit p)
        {
            lastTutHp = p.Alive ? p.Hp : -1f;
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
                else goal = tu.Goal; // the nearest enemy minion, Knut or the tower (Tutorial.cs)
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
            tutPillShadow = UiKit.Fill(tutPill, new Color(0f, 0f, 0.05f, 0.4f), UiKit.Rounded);
            tutPillShadow.type = Image.Type.Sliced;
            tutPillShadow.raycastTarget = false;
            tutPillPlate = UiKit.Fill(tutPill, TutPillFill, UiKit.Rounded);
            tutPillPlate.type = Image.Type.Sliced;
            tutPillPlate.raycastTarget = false;
            tutPillSheen = UiKit.Fill(tutPill, new Color(1f, 1f, 1f, 0.07f), UiKit.Rounded);
            tutPillSheen.type = Image.Type.Sliced;
            tutPillSheen.raycastTarget = false;
            tutPillEdge = UiKit.Fill(tutPill, new Color(1f, 207f / 255f, 74f / 255f, 0.85f));
            tutPillEdge.raycastTarget = false;
            tutPillText = UiKit.Label(tutPill, "", 40, TextAnchor.MiddleCenter, Color.white);
            tutPillText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tutPillText.raycastTarget = false;
            UiKit.Stretch(tutPillText.rectTransform);
            TextDepth(tutPillText);
            tutPill.gameObject.SetActive(false);

            joyArrow = MakeChevron(safe);
            joyArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            specialArrow = MakeChevron(safe);
            specialArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            blinkArrow = MakeChevron(safe);
            blinkArrow.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            for (int i = 0; i < chargeTexts.Length; i++)
            {
                var c = Centred(CentredRect("Charge", safe), "+ CHARGE", ChargeGold, 5);
                c.GetComponent<Outline>().effectColor = ChargeEdge;
                c.gameObject.SetActive(false);
                chargeTexts[i] = c;
            }

            // the island's line: no box, so it never hides what the hero is doing
            islandPanel = CentredRect("IslandLine", safe);
            islandGroup = islandPanel.gameObject.AddComponent<CanvasGroup>();
            islandGroup.blocksRaycasts = false;
            islandShadow = Sliced(islandPanel, new Color(0f, 0f, 0.05f, 0.4f));
            islandPlate = Sliced(islandPanel, new Color(14f / 255f, 20f / 255f, 58f / 255f, 0.84f));
            islandSheen = Sliced(islandPanel, new Color(1f, 1f, 1f, 0.07f));
            islandEdge = UiKit.Fill(islandPanel, new Color(1f, 207f / 255f, 74f / 255f, 0.8f));
            islandEdge.raycastTarget = false;
            islandLine = UiKit.Label(islandPanel, "", 40, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(islandLine.rectTransform);
            islandLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            islandLine.verticalOverflow = VerticalWrapMode.Overflow;
            islandLine.supportRichText = true;
            islandLine.raycastTarget = false;
            islandLine.lineSpacing = 1.05f;
            TextDepth(islandLine);
            islandTab = UiKit.NewRect("Speaker", islandPanel);
            islandTab.anchorMin = islandTab.anchorMax = new Vector2(0f, 1f);
            islandTab.pivot = new Vector2(0.5f, 0.5f);
            islandTabPlate = Sliced(islandTab, TabTeal);
            islandWho = UiKit.Label(islandTab, "", 28, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(islandWho.rectTransform);
            islandWho.horizontalOverflow = HorizontalWrapMode.Overflow;
            islandWho.raycastTarget = false;
            TextDepth(islandWho);
            islandPanel.gameObject.SetActive(false);

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
            pillShown = null; islandSrc = null;
            landPending = m.Tut != null && !m.Tut.Island;
            islandPanel.gameObject.SetActive(false); lastTutHp = -1f;
            healGlow.enabled = healRing.enabled = false;
            tutSkip.gameObject.SetActive(false);
            tutPill.gameObject.SetActive(false);
            tutBar.gameObject.SetActive(false);
            tutEnd.gameObject.SetActive(false);
            joyArrow.gameObject.SetActive(false);
            specialArrow.gameObject.SetActive(false);
            blinkArrow.gameObject.SetActive(false);
            specialPulse.enabled = chargeRing.enabled = false;
            for (int i = 0; i < chargeTexts.Length; i++) chargeTexts[i].gameObject.SetActive(false);
            for (int i = 0; i < TrailDots; i++) trailGlow[i].enabled = trailCore[i].enabled = false;
        }
    }
}
