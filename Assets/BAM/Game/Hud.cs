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
        const float DeniedTime = 1.2f, ShakeTime = 0.35f, GoTime = 0.8f;
        static readonly string[] Numbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };
        static readonly string[] TutStepNames = { "move", "auto_attack", "special", "tower" };
        static readonly string[] IslandStepNames = { "island_walk", "island_minion", "island_grass", "island_random", "island_only", "island_pad" };
        const float FadeInTime = 0.6f;
        const string MarkRed = "<color=#E0392B>", MarkTeal = "<color=#13A89E>", MarkEnd = "</color>";

        readonly GameFlow flow;
        Canvas canvas;
        RectTransform safe;
        Text centerText, respawnTitle, respawnText, defeatedText, bannerText, deniedText, specialName;
        RectTransform specialRoot, joyArea;
        Image specialFill, specialFace, specialGlow, specialIcon, specialOff, gainRing;
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
        bool built, leftHanded, tutorial, island;
        int shownRespawn, shownCount, lastTutStep;
        float lastDenied, deniedUntil, shakeUntil, goUntil, bannerUntil, gainAt, lastCharge;
        // who killed the player last ("Defeated by" under the respawn countdown)
        Unit killedBy;
        const float BannerTime = 2.6f, GainTime = 0.45f;
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
            shownRespawn = shownCount = lastTutStep = -1;
            lastDenied = m.SpecialDeniedAt; deniedUntil = shakeUntil = goUntil = bannerUntil = 0f;
            gainAt = -99f; lastCharge = 0f; killedBy = null;
            lastState = m.State;
            specialQueued = false; hintShown = false;
            BeginTop();
            BeginIntro();
            rings.Begin(m, views);
            // the hero's own special icon covers the disc (the old SpIcon); the name only if the picture is missing
            var icon = Resources.Load<Texture2D>("BAM/Menu/special_" + HeroCatalog.Get(m.Player.Hero).Name.ToLowerInvariant());
            if (specialIcon.sprite != null) Object.Destroy(specialIcon.sprite); // the last match's sprite (the texture stays)
            specialIcon.sprite = icon != null ? Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f), 100f) : null;
            specialIcon.enabled = icon != null;
            specialName.text = icon != null ? "" : HeroCatalog.Get(m.Player.Hero).SpecialName;
            centerText.text = "";
            bannerText.gameObject.SetActive(false);
            respawnText.gameObject.SetActive(false);
            respawnTitle.gameObject.SetActive(false);
            defeatedText.gameObject.SetActive(false);
            deniedText.gameObject.SetActive(false);
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
                goUntil = Time.unscaledTime + GoTime;
                bannerUntil = Time.unscaledTime + BannerTime;
                BamAudio.Play(SfxId.CountdownGo);
            }
            lastState = m.State;
            ReadEvents();
            rings.AfterStep();
            if (m.Tut != null && m.Tut.Step != lastTutStep)
            {
                // one numbering over both halves: the island's steps 0-5, then the arena's as 6-9
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
            rings.Frame(hidden || (Intro != null && Intro.Active));
            if (IntroFrame()) return; // the match intro: its own bars and name tags, no controls
            var kb = Keyboard.current;
            if (!hidden && kb != null && kb.spaceKey.wasPressedThisFrame) OnTap();

            Top(dt);
            Center();
            Special();
            Aim(hidden);
            Hint(hidden);
            IslandMarks(hidden);
            Fade();
            Fps(dt);
            joy.Rest();
            if (!m.Player.Alive && joy.Held) joy.Release();
        }

        // the frames of the last half second, averaged (so a single slow frame does not flicker the number)
        Text fpsText;
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

        void Center()
        {
            // the countdown: Ready, 3, 2, 1, GO!
            int count = -1;
            if (m.State == MatchState.Countdown) count = Mathf.CeilToInt(-m.Time);
            else if (Time.unscaledTime < goUntil) count = 0;
            else if (m.State == MatchState.Over && !tutorial) count = -10; // the old over banner, until the result comes up
            if (count != shownCount)
            {
                shownCount = count;
                bool won = m.Winner == m.Player.Team;
                centerText.text = count == -10 ? (won ? "VICTORY!" : "DEFEAT") : count < 0 ? "" : count == 0 ? "GO!" : count > 3 ? "Ready?" : Numbers[count];
                centerText.color = count == -10 && !won ? DmgToPlayer : UiKit.Gold;
                if (count >= 1 && count <= 3) BamAudio.Play(SfxId.Countdown);
            }
            // the old start banner: DESTROY THE ENEMY TOWER!
            bool banner = Time.unscaledTime < bannerUntil;
            if (bannerText.gameObject.activeSelf != banner) bannerText.gameObject.SetActive(banner);
            if (banner)
            {
                float age = BannerTime - (bannerUntil - Time.unscaledTime);
                float pop = age < 0.18f ? 0.7f + 0.3f * age / 0.18f : 1f;
                bannerText.rectTransform.localScale = Vector3.one * pop;
                var bc = bannerText.color;
                bc.a = bannerUntil - Time.unscaledTime < 0.4f ? (bannerUntil - Time.unscaledTime) / 0.4f : 1f;
                bannerText.color = bc;
            }

            var p = m.Player;
            bool waiting = !p.Alive && m.State == MatchState.Play;
            if (respawnText.gameObject.activeSelf != waiting)
            {
                respawnText.gameObject.SetActive(waiting);
                respawnTitle.gameObject.SetActive(waiting);
                bool by = waiting && killedBy != null && (killedBy.IsHero || killedBy.Kind == UnitKind.Wolf || killedBy.Kind == UnitKind.Tower);
                defeatedText.gameObject.SetActive(by);
                if (by) defeatedText.text = "Defeated by " + Colored(killedBy.Kind == UnitKind.Wolf && killedBy.Owner != null ? killedBy.Owner : killedBy);
            }
            if (waiting)
            {
                int s = Mathf.Max(1, Mathf.CeilToInt(p.RespawnT));
                if (s != shownRespawn) { shownRespawn = s; respawnText.text = Num(s); }
            }

            bool denied = Time.unscaledTime < deniedUntil;
            if (deniedText.gameObject.activeSelf != denied) deniedText.gameObject.SetActive(denied);
        }

        void Special()
        {
            var p = m.Player;
            bool ready = p.Alive && p.SpecialReady;
            specialFill.fillAmount = p.Alive ? p.Charge01 : 0f;
            specialFill.color = ready ? ChargeReady : Charging; // the old #ffc94a ready, #f0b640 charging
            // the old ready ring: breathes slowly (sin 3.5 t)
            float pulse = Mathf.Sin(Time.unscaledTime * 3.5f);
            specialGlow.color = ready ? new Color(1f, 1f, 1f, 0.55f + 0.3f * pulse) : Color.clear;
            specialGlow.rectTransform.localScale = Vector3.one * (ready ? 1f + 0.03f * pulse : 1f);
            if (specialOff.enabled == p.Alive) specialOff.enabled = !p.Alive;
            // the icon over the old button's disc, greyed while charging or dead
            specialIcon.color = !p.Alive ? new Color(0.45f, 0.45f, 0.5f, 0.6f) : ready ? Color.white : new Color(0.7f, 0.72f, 0.8f, 0.85f);
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
        }

        void Aim(bool hidden)
        {
            if (aim == null) return;
            var p = m.Player;
            aim.Hide();
            if (hidden || !p.Alive || m.State != MatchState.Play) return;
            var d = p.HDef;
            Vector3 hero = views.DrawnAt(p);
            bool ready = p.SpecialReady;
            if (special.Dragging && !special.OverCancel)
            {
                Vector2 o = special.Offset;
                Vector3 dir = new Vector3(o.x, 0f, o.y).normalized;
                if (d.Drag == AimShape.Line) aim.ShowLine(hero, dir, d.DragReach, d.DragWidth * 2f, ready);
                else if (d.Drag == AimShape.Circle)
                {
                    float k = Mathf.Clamp01(o.magnitude / SpecialButton.MaxDrag);
                    float dist = Mathf.Min(d.DragReach, Mathf.Max(1.5f, k * d.DragReach)); // as the rules' AimPoint
                    // Fenn's thrown wolf stops at the first wall: show where it really lands
                    if (p.Hero == HeroId.Fenn) dist = m.WolfReach(p.X, p.Y, Mathf.Atan2(-o.y, o.x), dist);
                    aim.ShowCircle(hero + dir * dist, d.DragWidth, ready);
                    aim.ShowRange(hero, d.DragReach);
                }
            }
        }

        void Hint(bool hidden)
        {
            string line = m.Tut != null && !hidden ? m.Tut.Line : null;
            if (line == null)
            {
                if (hintShown) { Screens.HideHint(); hintShown = false; }
                return;
            }
            hintShown = true;
            Vector2? at = null;
            var tut = m.Tut;
            if (island)
            {
                if (tut.PointAt != null) at = cam.Camera.WorldToScreenPoint(views.DrawnAt(tut.PointAt) + Vector3.up * 1.3f);
                else if (tut.RingOn) at = cam.Camera.WorldToScreenPoint(Coords.World(tut.RingX, tut.RingY) + Vector3.up * 0.8f);
                Screens.ShowHint(IslandText(tut), at);
                return;
            }
            switch (tut.Step)
            {
                case Tutorial.StepMove: at = RectTransformUtility.WorldToScreenPoint(null, joy.Base.position); break;
                case Tutorial.StepAuto: if (tut.Dummy != null) at = cam.Camera.WorldToScreenPoint(views.DrawnAt(tut.Dummy) + Vector3.up * 1.2f); break;
                case Tutorial.StepSpecial: at = RectTransformUtility.WorldToScreenPoint(null, specialRoot.position); break;
                case Tutorial.StepTower: if (tut.Tower != null) at = cam.Camera.WorldToScreenPoint(Coords.Of(tut.Tower) + Vector3.up * 2.4f); break;
            }
            Screens.ShowHint(line, at);
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
            if (!built || leftHanded == BamSettings.LeftHanded) return;
            leftHanded = BamSettings.LeftHanded;
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
            specialGlow = Picture(specialRoot, "Glow", 440f, "ui_ingameView_specialButton_glow", Color.clear);
            Picture(specialRoot, "Back", 270f, "ui_ingameView_specialButton_base", Color.white);
            Picture(specialRoot, "Track", 270f, "ui_ingameView_specialButton_fillBG", Color.white);
            specialFill = Picture(specialRoot, "Charge", 270f, "ui_ingameView_specialButton_fill", Charging);
            specialFill.type = Image.Type.Filled;
            specialFill.fillMethod = Image.FillMethod.Radial360;
            specialFill.fillOrigin = (int)Image.Origin360.Top;
            specialFill.fillClockwise = true;
            specialFace = Circle(specialRoot, "Face", 228f, Color.white).GetComponent<Image>();
            specialFace.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var iconRt = UiKit.NewRect("Icon", specialFace.transform);
            UiKit.Stretch(iconRt);
            specialIcon = iconRt.gameObject.AddComponent<Image>();
            specialIcon.preserveAspect = false;
            specialIcon.raycastTarget = false;
            specialOff = Picture(specialRoot, "Disabled", 270f, "ui_ingameView_specialButton_disable", new Color(1f, 1f, 1f, 0.85f));
            specialOff.enabled = false;
            gainRing = Circle(specialRoot, "Gain", 290f, Color.clear).GetComponent<Image>();
            gainRing.sprite = UiKit.CircleRing;
            gainRing.enabled = false;
            specialName = UiKit.Label(specialFace.transform, "", 40, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(specialName.rectTransform, 20, 20, 20, 20);
            specialName.resizeTextForBestFit = true;
            specialName.resizeTextMinSize = 20;
            specialName.resizeTextMaxSize = 44;
            UiKit.AddOutline(specialName, 3);

            deniedText = UiKit.Label(safe, "", 44, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.8f));
            UiKit.AddOutline(deniedText, 3);

            BuildTop();

            var pause = UiKit.Button(safe, "II", () => flow.Pause(), UiKit.Muted, SfxId.UiTap, 56);
            UiKit.Place(pause, 1f, 1f, -18f, -(TopM + PortraitS + BarH + 22f), 96f, 96f);

            centerText = UiKit.Label(safe, "", 200, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(centerText, 0.5f, 0.5f, 0f, 200f, 900f, 260f);
            UiKit.AddOutline(centerText, 6);
            // the old respawn block: RESPAWN IN, the seconds, who did it
            respawnTitle = UiKit.Label(safe, "RESPAWN IN", 44, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Place(respawnTitle, 0.5f, 0.5f, 0f, 470f, 900f, 60f);
            UiKit.AddOutline(respawnTitle, 3);
            respawnText = UiKit.Label(safe, "", 130, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(respawnText, 0.5f, 0.5f, 0f, 370f, 900f, 150f);
            UiKit.AddOutline(respawnText, 5);
            defeatedText = UiKit.Label(safe, "", 40, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(defeatedText, 0.5f, 0.5f, 0f, 275f, 900f, 56f);
            UiKit.AddOutline(defeatedText, 3);
            bannerText = UiKit.Label(safe, "<size=64>DESTROY THE</size>\n<color=#ff5a4a>ENEMY</color> TOWER!", 92, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(bannerText, 0.5f, 0.5f, 0f, 470f, 1000f, 220f);
            UiKit.AddOutline(bannerText, 6);
            bannerText.supportRichText = true;

            // development builds only (BAM > Build test APK, the editor): never in the release AAB
            if (Debug.isDebugBuild)
            {
                fpsText = UiKit.Label(safe, "", 36, TextAnchor.UpperLeft, Color.white);
                UiKit.Place(fpsText, 0f, 1f, 18f, -(TopM + PortraitS + BarH + 40f), 220f, 50f);
                UiKit.AddOutline(fpsText, 2);
                fpsStrings = new string[241];
                for (int i = 0; i < fpsStrings.Length; i++) fpsStrings[i] = i + " fps";
            }

            // the guided match: chevrons over what to look at, and the black for the jump to the arena (on top of everything)
            canvasRect = (RectTransform)canvas.transform;
            BuildFloats();
            BuildIntro();
            pointChevron = MakeChevron(canvasRect);
            ringChevron = MakeChevron(canvasRect);
            fade = UiKit.Fill(canvasRect, Color.clear);
            fade.enabled = false;

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

        static readonly Color Charging = new Color32(0xf0, 0xb6, 0x40, 0xff), ChargeReady = new Color32(0xff, 0xc9, 0x4a, 0xff);

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
