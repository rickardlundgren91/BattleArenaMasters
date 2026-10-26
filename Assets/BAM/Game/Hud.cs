using BAM.Analytics;
using BAM.Art;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = BAM.Sim.PlayerInput;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The in-match HUD (essentials only) and the player's controls: timer, team kills and stars, pause, countdown, respawn
    /// countdown, the floating joystick, the special button (charge ring, ready glow, tap/drag/cancel, the aim preview) and
    /// the guided match's hint. Built once with UiKit; texts change only when their value does (no per-frame garbage).
    /// Editor and keyboard: WASD or the arrows move, Space taps the special.
    /// </summary>
    sealed partial class Hud
    {
        const int HudOrder = 10; // under the hint bubble (60) and the menus (100)
        const float DeniedTime = 1.2f, ShakeTime = 0.35f;
        static readonly string[] Numbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };
        static readonly string[] TutStepNames = { "move", "auto_attack", "special", "fight", "tower" };
        static readonly string[] IslandStepNames = { "island_walk", "island_minion", "island_grass", "island_target", "island_pad" };
        const float FadeInTime = 0.6f;
        const string MarkRed = "<color=#E0392B>", MarkTeal = "<color=#13A89E>", MarkEnd = "</color>";

        readonly GameFlow flow;
        Canvas canvas;
        RectTransform safe;
        Text deniedText, specialName;
        RectTransform specialRoot, joyArea;
        Image specialFill, specialFace, specialGlow, specialIcon, specialDisc, gainRing, cancelBack, dragDot;
        RectTransform cancelMark, dragRing;
        Text cancelText;
        readonly Image[] cancelCross = new Image[2];
        SpecialButton special;
        JoystickZone joy;
        AimView aim, marks;
        Image fade;
        RectTransform canvasRect, pointChevron, ringChevron;
        float fadeInAt = -1f;
        string hintSource, hintRich;
        readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(256);

        Match m;
        MatchViews views;
        MatchCamera cam;
        bool built, leftHanded, laidOutHand, tutorial, island, joyLogged;
        int lastTutStep;
        float lastDenied, deniedUntil, shakeUntil, bannerUntil, gainAt, lastCharge;
        // who killed the player last ("Defeated by" under the respawn countdown)
        Unit killedBy;
        const float BannerTime = 2.5f, GainTime = 0.35f; // the old bannerT and CombatFeel.PULSE_T
        MatchState lastState;
        bool hintShown;
        // the special waits here until the next tick reads it
        bool specialQueued;
        float queuedAngle, queuedK;

        readonly MatchRings rings = new MatchRings();

        public Hud(GameFlow owner) { flow = owner; }

        public void Begin(Match match, MatchViews matchViews, MatchCamera camera)
        {
            m = match; views = matchViews; cam = camera;
            tutorial = m.Tut != null;
            island = tutorial && m.Tut.Island;
            if (!built) Build();
            specialRoot.gameObject.SetActive(!island); // no special on the training island
            marks = tutorial ? ArtKit.CreateAim() : null;
            fadeInAt = -1f;
            SetFade(0f);
            hintSource = null;
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            aim = ArtKit.CreateAim();
            lastTutStep = -1;
            lastDenied = m.SpecialDeniedAt; deniedUntil = shakeUntil = bannerUntil = 0f;
            gainAt = -99f; lastCharge = 0f; killedBy = null;
            lastState = m.State;
            specialQueued = false; hintShown = false;
            BeginTop();
            BeginIntro();
            BeginOverhead();
            BeginScore();
            BeginEmote();
            rings.Begin(m, views);
            // the hero's own special icon covers the disc (the old SpIcon); the name only if the picture is missing
            var icon = Resources.Load<Texture2D>("BAM/Menu/special_" + HeroCatalog.Get(m.Player.Hero).Name.ToLowerInvariant());
            if (specialIcon.sprite != null) Object.Destroy(specialIcon.sprite); // the last match's sprite (the texture stays)
            specialIcon.sprite = icon != null ? Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f), 100f) : null;
            specialIcon.enabled = icon != null;
            specialName.text = icon != null ? "" : HeroCatalog.Get(m.Player.Hero).SpecialName;
            BeginCenter();
            BeginTutorial();
            deniedText.gameObject.SetActive(false);
            joyLogged = false;
            leftHanded = !BamSettings.LeftHanded; // forces the first layout
            ApplyLayout();
            BamSettings.Changed += ApplyLayout;
            CancelTouches();
        }

        public void End()
        {
            BamSettings.Changed -= ApplyLayout;
            CancelTouches();
            if (aim != null) ArtKit.Release(aim);
            aim = null;
            if (marks != null) ArtKit.Release(marks);
            marks = null;
            rings.End();
            if (introRoot != null) ShowIntro(false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (hintShown) Screens.HideHint();
            hintShown = false;
            m = null; views = null;
        }

        public void CancelTouches()
        {
            if (joy != null) joy.Release();
            if (special != null) special.Cancel();
            specialQueued = false;
        }

        // ---------------------------------------------------------------- input for one tick

        public PlayerInput TakeInput()
        {
            var input = PlayerInput.None;
            Vector2 v = joy.Value;
            var kb = Keyboard.current;
            if (kb != null)
            {
                float kx = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                float ky = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                if (kx != 0f || ky != 0f) v = new Vector2(kx, ky).normalized;
            }
            // screen up is up the map, which is -y in map space
            input.MoveX = v.x;
            input.MoveY = -v.y;
            if (specialQueued)
            {
                specialQueued = false;
                input.Special = true;
                input.AimAngle = queuedAngle;
                input.AimK = queuedK;
            }
            return input;
        }

        void OnTap()
        {
            if (m == null || !m.Player.Alive) return;
            if (!m.Player.SpecialReady) { Denied("Special not ready"); return; }
            Queue(float.NaN, 0f);
        }

        void OnAimed(Vector2 offset)
        {
            if (m == null || !m.Player.Alive) return;
            if (!m.Player.SpecialReady) { Denied("Special not ready"); return; }
            Queue(Mathf.Atan2(-offset.y, offset.x), Mathf.Clamp01(offset.magnitude / SpecialButton.MaxDrag));
        }

        void Queue(float angle, float k)
        {
            specialQueued = true; queuedAngle = angle; queuedK = k;
        }

        void Denied(string text)
        {
            deniedText.text = text;
            deniedUntil = Time.unscaledTime + DeniedTime;
            shakeUntil = Time.unscaledTime + ShakeTime;
            BamAudio.Play(SfxId.UiBack, 0.6f);
        }

        // ---------------------------------------------------------------- after every tick

        public void AfterStep()
        {
            if (m.SpecialDeniedAt != lastDenied) { lastDenied = m.SpecialDeniedAt; Denied("No target in reach"); }
            if (lastState == MatchState.Countdown && m.State == MatchState.Play && !tutorial)
            {
                bannerUntil = Time.unscaledTime + BannerTime;
                BamAudio.Play(SfxId.CountdownGo);
            }
            lastState = m.State;
            ReadEvents();
            rings.AfterStep();
            if (m.Tut != null && m.Tut.Step != lastTutStep)
            {
                // one numbering over both halves: the island's steps 0-4, then the arena's as 5-9
                lastTutStep = m.Tut.Step;
                var names = island ? IslandStepNames : TutStepNames;
                string name = lastTutStep >= 0 && lastTutStep < names.Length ? names[lastTutStep] : "step";
                BamAnalytics.TutorialStep(island ? lastTutStep : IslandStepNames.Length + lastTutStep, name);
            }
        }

        // ---------------------------------------------------------------- every frame

        public void Frame(float dt, bool hidden)
        {
            if (m == null) return;
            canvas.enabled = !hidden;
            rings.Frame(hidden || (Intro != null && Intro.Active), 1f / cam.PixelsPerTile);
            OverheadFrame(hidden || (Intro != null && Intro.Active));
            VignetteFrame(hidden || (Intro != null && Intro.Active));
            if (IntroFrame()) return; // the match intro: its own bars and name tags, no controls
            var kb = Keyboard.current;
            if (!hidden && kb != null && kb.spaceKey.wasPressedThisFrame) OnTap();

            Top(dt);
            Center();
            ScoreFrame();
            EmoteFrame(hidden);
            Special();
            Aim(hidden);
            Hint(hidden);
            IslandMarks(hidden);
            TutorialFrame(hidden); // the arena half's line, arrows, "+ CHARGE", tower blocks and end; Skip (HudTutorial.cs)
            Fade();
            Fps(dt);
            joy.Rest();
            if (!m.Player.Alive && joy.Held) joy.Release();
            if (!joyLogged && m.State == MatchState.Play)
            {
                // the joystick did not show on Rickard's PC (2026-10-26): one line on where it is, to find out why if it still does not
                joyLogged = true;
                var bi = joy.Base.GetComponent<Image>();
                Debug.Log("[BAM.Game] joystick: on " + joy.Base.gameObject.activeInHierarchy + ", screen " + RectTransformUtility.WorldToScreenPoint(null, joy.Base.position)
                          + " of " + Screen.width + "x" + Screen.height + ", size " + joy.Base.rect.size + ", alpha " + joy.Look.alpha
                          + ", picture " + (bi.sprite != null ? bi.sprite.texture.name : "none") + ", zone " + joyArea.rect);
            }
        }

        // the frames of the last half second, averaged (so a single slow frame does not flicker the number)
        Text fpsText;
        static readonly bool ShowFps = false;
        string[] fpsStrings;
        float fpsTime;
        int fpsFrames;

        void Fps(float dt)
        {
            if (fpsText == null) return;
            fpsTime += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsTime < 0.5f) return;
            int fps = Mathf.Clamp(Mathf.RoundToInt(fpsFrames / fpsTime), 0, fpsStrings.Length - 1);
            fpsText.text = fpsStrings[fps];
            fpsText.color = fps >= 55 ? Color.white : fps >= 45 ? UiKit.Gold : UiKit.Bad;
            fpsTime = 0f; fpsFrames = 0;
        }

        static string Num(int n) => n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString();

        void Special()
        {
            var p = m.Player;
            bool ready = p.Alive && p.SpecialReady;
            specialFill.fillAmount = p.Alive ? p.Charge01 : 0f;
            // the Glass HUD's button (Hud.cs 739-749, HudGlass.drawGlassSpecialRim): a dark disc, brown when ready, an orange
            // charge ring, and when ready an orange ring breathing just outside it (sin 3.5 t)
            specialFill.color = ready ? GlassOrangeHi : GlassOrange;
            specialDisc.color = ready ? DiscReady : DiscCharging;
            float pulse = Mathf.Sin(Time.unscaledTime * 3.5f);
            specialGlow.enabled = ready;
            if (ready)
            {
                specialGlow.color = new Color(1f, 0.55f, 0.12f, 0.5f + 0.3f * pulse);
                specialGlow.rectTransform.localScale = Vector3.one * ((1.1f + 0.03f * pulse) / 1.1f);
            }
            // a charge just came in: a short gold ring swells out of the button
            if (p.Alive && p.Charge > lastCharge + 0.001f) gainAt = Time.unscaledTime;
            lastCharge = p.Charge;
            float g = 1f - (Time.unscaledTime - gainAt) / GainTime;
            if (g > 0f)
            {
                gainRing.enabled = true;
                gainRing.color = new Color(1f, 0.86f, 0.45f, 0.85f * g);
                gainRing.rectTransform.localScale = Vector3.one * (1.18f - 0.18f * g);
            }
            else if (gainRing.enabled) gainRing.enabled = false;
            float shake = Time.unscaledTime < shakeUntil ? Mathf.Sin(Time.unscaledTime * 70f) * 14f * (shakeUntil - Time.unscaledTime) / ShakeTime : 0f;
            specialFace.rectTransform.anchoredPosition = new Vector2(shake, 0f);
            if (special.Pressed && !p.Alive) special.Cancel();
            DragMarks();
        }

        static readonly Color CancelOff = new Color(0.04f, 0.05f, 0.1f, 0.5f), CancelOn = new Color(0.9f, 0.2f, 0.2f, 0.55f), DotRed = new Color(1f, 0.35f, 0.35f, 0.85f);

        // while the special is dragged (the old HudAim.cs): the reach ring round the button, a dot under the finger and the
        // cancel mark above, red with CANCEL while the finger is on it
        void DragMarks()
        {
            bool drag = special.Dragging;
            if (cancelMark.gameObject.activeSelf != drag)
            {
                cancelMark.gameObject.SetActive(drag);
                dragRing.gameObject.SetActive(drag);
                dragDot.gameObject.SetActive(drag);
            }
            if (!drag) { if (cancelText.gameObject.activeSelf) cancelText.gameObject.SetActive(false); return; }
            bool on = special.OverCancel;
            cancelBack.color = on ? CancelOn : CancelOff;
            cancelMark.localScale = Vector3.one * (on ? 1.12f : 1f);
            for (int i = 0; i < 2; i++) cancelCross[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            if (cancelText.gameObject.activeSelf != on) cancelText.gameObject.SetActive(on);
            Vector2 o = special.Offset;
            if (o.magnitude > SpecialButton.MaxDrag) o = o.normalized * SpecialButton.MaxDrag;
            dragDot.rectTransform.anchoredPosition = o;
            dragDot.color = on ? DotRed : new Color(1f, 1f, 1f, 0.85f);
        }

        void Aim(bool hidden)
        {
            if (aim == null) return;
            var p = m.Player;
            aim.Hide();
            rings.SetAim(false, false, Vector2.zero, Vector2.zero, 0f);
            if (hidden || !p.Alive || m.State != MatchState.Play) return;
            var d = p.HDef;
            Vector3 hero = views.DrawnAt(p);
            bool ready = p.SpecialReady;
            if (special.Dragging && !special.OverCancel)
            {
                Vector2 o = special.Offset;
                Vector3 dir = new Vector3(o.x, 0f, o.y).normalized;
                Vector2 from = new Vector2(p.X, p.Y), sdir = new Vector2(dir.x, -dir.z); // the rules' y runs the other way
                if (d.Drag == AimShape.Line)
                {
                    aim.ShowLine(hero, dir, d.DragReach, d.DragWidth * 2f, ready);
                    rings.SetAim(true, true, from, from + sdir * d.DragReach, d.DragWidth);
                }
                else if (d.Drag == AimShape.Circle)
                {
                    float k = Mathf.Clamp01(o.magnitude / SpecialButton.MaxDrag);
                    float dist = Mathf.Min(d.DragReach, Mathf.Max(1.5f, k * d.DragReach)); // as the rules' AimPoint
                    // Fenn's thrown wolf stops at the first wall: show where it really lands
                    if (p.Hero == HeroId.Fenn) dist = m.WolfReach(p.X, p.Y, Mathf.Atan2(-o.y, o.x), dist);
                    aim.ShowCircle(hero + dir * dist, d.DragWidth, ready);
                    rings.SetAim(true, false, from + sdir * dist, Vector2.zero, d.DragWidth);
                    aim.ShowRange(hero, d.DragReach);
                }
            }
        }

        void Hint(bool hidden)
        {
            // the island's lines in the bubble; the arena half has the old pill at the top instead (HudTutorial.cs)
            string line = island && !hidden ? m.Tut.Line : null;
            if (line == null)
            {
                if (hintShown) { Screens.HideHint(); hintShown = false; }
                return;
            }
            hintShown = true;
            Vector2? at = null;
            var tut = m.Tut;
            if (tut.PointAt != null) at = cam.Camera.WorldToScreenPoint(views.DrawnAt(tut.PointAt) + Vector3.up * 1.3f);
            else if (tut.RingOn) at = cam.Camera.WorldToScreenPoint(Coords.World(tut.RingX, tut.RingY) + Vector3.up * 0.8f);
            Screens.ShowHint(IslandText(tut), at);
        }

        // The island's line with its |marked| words coloured and "FENN SAYS" above it; rebuilt only when the line changes.
        string IslandText(Tutorial tut)
        {
            string src = tut.LineMarked ?? tut.Line;
            if (ReferenceEquals(src, hintSource)) return hintRich;
            hintSource = src;
            sb.Length = 0;
            if (!string.IsNullOrEmpty(tut.Speaker)) sb.Append("<size=32><color=#3B5BDB>").Append(tut.Speaker).Append("</color></size>\n");
            bool open = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c != '|') { sb.Append(c); continue; }
                sb.Append(open ? MarkEnd : tut.LineTip ? MarkTeal : MarkRed);
                open = !open;
            }
            if (open) sb.Append(MarkEnd);
            hintRich = sb.ToString();
            return hintRich;
        }

        // The island's ground ring, boost pad and bouncing chevrons.
        void IslandMarks(bool hidden)
        {
            var tut = m.Tut;
            bool on = !hidden && island;
            if (marks != null)
            {
                marks.Hide();
                if (on)
                {
                    float pulse = Mathf.Sin(Time.unscaledTime * 5f);
                    if (tut.RingOn) marks.ShowRange(Coords.World(tut.RingX, tut.RingY), 0.8f + 0.1f * pulse);
                    if (tut.PadOn && !tut.Jumping) marks.ShowCircle(Coords.World(tut.PadX, tut.PadY), 0.55f + 0.05f * pulse, true);
                }
            }
            Chevron(pointChevron, on && tut.PointAt != null && tut.PointAt.Alive, on && tut.PointAt != null ? views.DrawnAt(tut.PointAt) + Vector3.up * 1.4f : Vector3.zero);
            Chevron(ringChevron, on && tut.RingOn && tut.ArrowOnRing, on ? Coords.World(tut.RingX, tut.RingY) + Vector3.up * 0.9f : Vector3.zero);
        }

        void Chevron(RectTransform c, bool show, Vector3 world)
        {
            if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
            if (!show) return;
            Vector3 sp = cam.Camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local);
            c.anchoredPosition = local + new Vector2(0f, 14f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
        }

        /// <summary>The arena half of the guided match starts under black and fades in.</summary>
        public void FadeIn()
        {
            fadeInAt = Time.unscaledTime;
            SetFade(1f);
        }

        void Fade()
        {
            float a = 0f;
            if (island) a = m.Tut.Fade;
            else if (fadeInAt >= 0f) a = 1f - Mathf.Clamp01((Time.unscaledTime - fadeInAt) / FadeInTime);
            SetFade(a);
        }

        void SetFade(float a)
        {
            if (fade.color.a == a) return;
            fade.color = new Color(0f, 0f, 0f, a);
            fade.enabled = a > 0f;
        }

        // ---------------------------------------------------------------- layout

        void ApplyLayout()
        {
            if (!built || (laidOutHand && leftHanded == BamSettings.LeftHanded)) return;
            leftHanded = BamSettings.LeftHanded;
            laidOutHand = true; // the first call lays the controls out even when the setting is still the default (right-handed)
            CancelTouches();
            // right-handed: joystick on the left half, special bottom right; left-handed mirrors both
            joyArea.anchorMin = new Vector2(leftHanded ? 0.5f : 0f, 0f);
            joyArea.anchorMax = new Vector2(leftHanded ? 1f : 0.5f, 0.82f);
            joyArea.offsetMin = joyArea.offsetMax = Vector2.zero;
            joy.RightSide = leftHanded;
            float side = leftHanded ? 0f : 1f;
            UiKit.Place(specialRoot, side, 0f, leftHanded ? 70f : -70f, 150f, 270f, 270f);
            UiKit.Place(deniedText, side, 0f, leftHanded ? 30f : -30f, 440f, 520f, 70f);
            deniedText.alignment = leftHanded ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            Canvas.ForceUpdateCanvases();
            joy.Rest();
        }

        void Build()
        {
            built = true;
            canvas = UiKit.CreateCanvas("BAM.Hud", HudOrder);
            safe = UiKit.SafeArea(canvas);

            // joystick: a see-through half of the screen that catches touches (first, so the buttons sit above it)
            joyArea = UiKit.NewRect("Joystick", safe);
            joyArea.pivot = new Vector2(0.5f, 0.5f);
            var catcher = joyArea.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            joy = joyArea.gameObject.AddComponent<JoystickZone>();
            joy.Area = joyArea;
            var look = UiKit.NewRect("Look", joyArea);
            UiKit.Stretch(look);
            joy.Look = look.gameObject.AddComponent<CanvasGroup>();
            joy.Look.blocksRaycasts = false;
            joy.Look.interactable = false;
            // the old game's pictures, at its sizes (base 2.5 R across, stick 1.2 R)
            joy.Base = Picture(look, "Base", 2.5f * JoystickZone.Radius, "ui_ingameView_joystick_base", Color.white).rectTransform;
            joy.Knob = Picture(look, "Knob", 1.2f * JoystickZone.Radius, "ui_ingameView_joystick", Color.white).rectTransform;

            // special button
            specialRoot = UiKit.NewRect("Special", safe);
            var hit = specialRoot.gameObject.AddComponent<Image>();
            hit.sprite = UiKit.Circle;
            hit.color = Color.clear;
            special = specialRoot.gameObject.AddComponent<SpecialButton>();
            special.Rect = specialRoot;
            special.Tapped = OnTap;
            special.Aimed = OnAimed;
            // the old game's button pictures (ui_ingameView_specialButton_*): glow, dark disc, ring track, the charge ring
            // the Glass HUD's SPECIAL: a dark glass disc, a faint track and the orange charge (the old ring picture,
            // ui_ingameView_specialButton_fill, has the old ring's width: 0.14 of the radius)
            const float D = 2f * SpecialButton.Radius;
            specialGlow = Circle(specialRoot, "Ready", D * 1.1f, Color.clear).GetComponent<Image>();
            RoundRing(specialGlow, D * 0.55f, 3f);
            specialGlow.enabled = false;
            specialDisc = Circle(specialRoot, "Disc", D, DiscCharging).GetComponent<Image>();
            Picture(specialRoot, "Track", D, "ui_ingameView_specialButton_fill", new Color(1f, 1f, 1f, 0.15f));
            specialFill = Picture(specialRoot, "Charge", D, "ui_ingameView_specialButton_fill", GlassOrange);
            specialFill.type = Image.Type.Filled;
            specialFill.fillMethod = Image.FillMethod.Radial360;
            specialFill.fillOrigin = (int)Image.Origin360.Top;
            specialFill.fillClockwise = true;
            specialFace = Circle(specialRoot, "Face", D * 0.87f, Color.white).GetComponent<Image>();
            specialFace.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var iconRt = UiKit.NewRect("Icon", specialFace.transform);
            UiKit.Stretch(iconRt);
            specialIcon = iconRt.gameObject.AddComponent<Image>();
            specialIcon.preserveAspect = false;
            specialIcon.raycastTarget = false;
            // the gold frame round it all and a thin gold line round the icon, so it reads as a framed metal button
            RoundRing(Circle(specialRoot, "Rim", D, Hex(0xd9a441)).GetComponent<Image>(), D * 0.5f, Mathf.Max(1.5f, SpecialButton.Radius * 0.035f));
            RoundRing(Circle(specialRoot, "IconRim", D * 0.86f, new Color(1f, 214f / 255f, 130f / 255f, 0.55f)).GetComponent<Image>(), D * 0.43f, 1f);
            gainRing = Circle(specialRoot, "Gain", 290f, Color.clear).GetComponent<Image>();
            gainRing.sprite = UiKit.CircleRing;
            gainRing.enabled = false;
            dragRing = Circle(specialRoot, "Reach", 2f * SpecialButton.MaxDrag, new Color(1f, 1f, 1f, 0.35f));
            dragRing.GetComponent<Image>().sprite = UiKit.CircleRing;
            dragRing.gameObject.SetActive(false);
            dragDot = Circle(specialRoot, "Finger", 0.64f * SpecialButton.Radius, Color.white).GetComponent<Image>();
            dragDot.gameObject.SetActive(false);
            cancelMark = UiKit.NewRect("Cancel", specialRoot);
            cancelMark.anchorMin = cancelMark.anchorMax = cancelMark.pivot = new Vector2(0.5f, 0.5f);
            cancelMark.sizeDelta = Vector2.one * 2f * SpecialButton.CancelRadius;
            cancelMark.anchoredPosition = new Vector2(0f, SpecialButton.CancelUp);
            cancelBack = UiKit.Fill(cancelMark, CancelOff, UiKit.Circle);
            var edge = UiKit.Fill(cancelMark, new Color(1f, 1f, 1f, 0.5f), UiKit.CircleRing);
            edge.raycastTarget = false;
            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.NewRect("Cross", cancelMark);
                bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                bar.sizeDelta = new Vector2(SpecialButton.CancelRadius * 1.07f, Mathf.Max(6f, SpecialButton.CancelRadius * 0.16f));
                bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                cancelCross[i] = bar.gameObject.AddComponent<Image>();
                cancelCross[i].sprite = UiKit.Rounded;
                cancelCross[i].type = Image.Type.Sliced;
                cancelCross[i].pixelsPerUnitMultiplier = 4f;
                cancelCross[i].raycastTarget = false;
            }
            cancelMark.gameObject.SetActive(false);
            cancelText = UiKit.Label(specialRoot, "CANCEL", 38, TextAnchor.MiddleCenter, new Color(1f, 0.7f, 0.7f));
            UiKit.Place(cancelText, 0.5f, 0.5f, 0f, SpecialButton.Radius * 1.25f, 300f, 50f);
            UiKit.AddOutline(cancelText, 2);
            cancelText.gameObject.SetActive(false);
            specialName = UiKit.Label(specialFace.transform, "", 40, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(specialName.rectTransform, 20, 20, 20, 20);
            specialName.resizeTextForBestFit = true;
            specialName.resizeTextMinSize = 20;
            specialName.resizeTextMaxSize = 44;
            UiKit.AddOutline(specialName, 3);

            deniedText = UiKit.Label(safe, "", 44, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.8f));
            UiKit.AddOutline(deniedText, 3);

            BuildTop();

            BuildPause();
            BuildScore();

            BuildCenter();

            // off: the old HUD has no fps line (Rickard 2026-10-26 compared the two). Set ShowFps to see it in development builds.
            if (ShowFps && Debug.isDebugBuild)
            {
                fpsText = UiKit.Label(safe, "", 36, TextAnchor.UpperLeft, Color.white);
                UiKit.Place(fpsText, 0f, 1f, 18f, -150f, 220f, 50f);
                UiKit.AddOutline(fpsText, 2);
                fpsStrings = new string[241];
                for (int i = 0; i < fpsStrings.Length; i++) fpsStrings[i] = i + " fps";
            }

            // the guided match: chevrons over what to look at, and the black for the jump to the arena (on top of everything)
            canvasRect = (RectTransform)canvas.transform;
            BuildFloats();
            BuildIntro();
            BuildOverhead();
            BuildVignette();
            BuildEmote(); // after the overhead marks: its bubbles sit just above them
            pointChevron = MakeChevron(canvasRect);
            ringChevron = MakeChevron(canvasRect);
            fade = UiKit.Fill(canvasRect, Color.clear);
            fade.enabled = false;
            BuildTutorial(); // the guided match's own pieces; its picture of Home over everything, even the black

            canvas.gameObject.SetActive(false);
        }

        // A bold down-pointing V from two rounded bars, gold with a dark edge.
        static RectTransform MakeChevron(RectTransform parent)
        {
            var rt = UiKit.NewRect("Chevron", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(90f, 70f);
            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.NewRect("Bar", rt);
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(24f, 66f);
                bar.anchoredPosition = Vector2.zero;
                bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 38f : -38f);
                var img = bar.gameObject.AddComponent<Image>();
                img.sprite = UiKit.Rounded;
                img.type = Image.Type.Sliced;
                img.color = UiKit.Gold;
                img.raycastTarget = false;
                var o = bar.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);
                o.effectDistance = new Vector2(3f, -3f);
            }
            rt.gameObject.SetActive(false);
            return rt;
        }

        static readonly Color GlassOrange = Hex(0xff8a1f), GlassOrangeHi = Hex(0xffb35a);
        static readonly Color DiscReady = new Color(70f / 255f, 34f / 255f, 6f / 255f, 0.72f), DiscCharging = new Color(10f / 255f, 16f / 255f, 40f / 255f, 0.62f);

        // A round picture from the old game (Resources/BAM/Hud), a plain circle if it is missing.
        static Image Picture(Transform parent, string name, float size, string file, Color color)
        {
            var img = Circle(parent, name, size, color).GetComponent<Image>();
            var tex = Resources.Load<Texture2D>("BAM/Hud/" + file);
            if (tex != null) img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            else Debug.LogError("[BAM.Game] HUD picture missing: Resources/BAM/Hud/" + file);
            return img;
        }

        static RectTransform Circle(Transform parent, string name, float size, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.Circle;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }
    }
}
