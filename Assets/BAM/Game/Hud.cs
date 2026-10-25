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
    sealed class Hud
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
        RectTransform safe, scorePanel, starsRow;
        Text timeText, blueKills, redKills, centerText, respawnText, deniedText, specialName;
        readonly Image[] stars = new Image[6];
        RectTransform specialRoot, joyArea;
        Image specialFill, specialFace, specialGlow;
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
        int shownSeconds, shownK0, shownK1, shownS0, shownS1, shownRespawn, shownCount, lastTutStep;
        float lastDenied, deniedUntil, shakeUntil, goUntil;
        MatchState lastState;
        bool hintShown;
        // the special waits here until the next tick reads it
        bool specialQueued;
        float queuedAngle, queuedK;

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
            shownSeconds = shownK0 = shownK1 = shownS0 = shownS1 = shownRespawn = shownCount = lastTutStep = -1;
            lastDenied = m.SpecialDeniedAt; deniedUntil = shakeUntil = goUntil = 0f;
            lastState = m.State;
            specialQueued = false; hintShown = false;
            scorePanel.gameObject.SetActive(!tutorial);
            starsRow.gameObject.SetActive(!tutorial);
            specialName.text = HeroCatalog.Get(m.Player.Hero).SpecialName;
            specialFace.color = UiKit.HeroColor(m.Player.Hero);
            centerText.text = "";
            respawnText.gameObject.SetActive(false);
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
                BamAudio.Play(SfxId.CountdownGo);
            }
            lastState = m.State;
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
            var kb = Keyboard.current;
            if (!hidden && kb != null && kb.spaceKey.wasPressedThisFrame) OnTap();

            Top();
            Center();
            Special();
            Aim(hidden);
            Hint(hidden);
            IslandMarks(hidden);
            Fade();
            joy.Rest();
            if (!m.Player.Alive && joy.Held) joy.Release();
        }

        void Top()
        {
            if (tutorial) return;
            int secs = Mathf.Max(0, Mathf.FloorToInt(m.Time));
            if (secs != shownSeconds) { shownSeconds = secs; timeText.text = (secs / 60) + ":" + (secs % 60).ToString("00"); }
            if (m.Kills[0] != shownK0) { shownK0 = m.Kills[0]; blueKills.text = Num(shownK0); }
            if (m.Kills[1] != shownK1) { shownK1 = m.Kills[1]; redKills.text = Num(shownK1); }
            if (m.Stars[0] != shownS0 || m.Stars[1] != shownS1)
            {
                shownS0 = m.Stars[0]; shownS1 = m.Stars[1];
                for (int i = 0; i < 3; i++)
                {
                    stars[i].color = i < shownS0 ? UiKit.Gold : new Color(1f, 1f, 1f, 0.18f);
                    stars[3 + i].color = i < shownS1 ? UiKit.Gold : new Color(1f, 1f, 1f, 0.18f);
                }
            }
        }

        static string Num(int n) => n >= 0 && n < Numbers.Length ? Numbers[n] : n.ToString();

        void Center()
        {
            // the countdown: Ready, 3, 2, 1, GO!
            int count = -1;
            if (m.State == MatchState.Countdown) count = Mathf.CeilToInt(-m.Time);
            else if (Time.unscaledTime < goUntil) count = 0;
            if (count != shownCount)
            {
                shownCount = count;
                centerText.text = count < 0 ? "" : count == 0 ? "GO!" : count > 3 ? "Ready?" : Numbers[count];
                if (count >= 1 && count <= 3) BamAudio.Play(SfxId.Countdown);
            }

            var p = m.Player;
            bool waiting = !p.Alive && m.State == MatchState.Play;
            if (respawnText.gameObject.activeSelf != waiting) respawnText.gameObject.SetActive(waiting);
            if (waiting)
            {
                int s = Mathf.Max(1, Mathf.CeilToInt(p.RespawnT));
                if (s != shownRespawn) { shownRespawn = s; respawnText.text = "Back in " + s; }
            }

            bool denied = Time.unscaledTime < deniedUntil;
            if (deniedText.gameObject.activeSelf != denied) deniedText.gameObject.SetActive(denied);
        }

        void Special()
        {
            var p = m.Player;
            bool ready = p.Alive && p.SpecialReady;
            specialFill.fillAmount = p.Alive ? p.Charge01 : 0f;
            specialFill.color = ready ? UiKit.Gold : UiKit.Trim; // charging reads darker than ready
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            specialGlow.color = ready ? new Color(1f, 0.85f, 0.3f, 0.35f + 0.35f * pulse) : Color.clear;
            specialGlow.rectTransform.localScale = Vector3.one * (ready ? 1f + 0.05f * pulse : 1f);
            Color face = UiKit.HeroColor(p.Hero);
            face.a = p.Alive ? (ready ? 1f : 0.75f) : 0.35f;
            specialFace.color = face;
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
            else if (ready && d.TapReach > 0f) aim.ShowRange(hero, d.TapReach);
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
            joy.Base = Circle(look, "Base", 2f * JoystickZone.Radius + 40f, new Color(1f, 1f, 1f, 0.22f));
            joy.Knob = Circle(look, "Knob", 130f, new Color(1f, 1f, 1f, 0.75f));

            // special button
            specialRoot = UiKit.NewRect("Special", safe);
            var hit = specialRoot.gameObject.AddComponent<Image>();
            hit.sprite = UiKit.Circle;
            hit.color = Color.clear;
            special = specialRoot.gameObject.AddComponent<SpecialButton>();
            special.Rect = specialRoot;
            special.Tapped = OnTap;
            special.Aimed = OnAimed;
            specialGlow = Circle(specialRoot, "Glow", 340f, Color.clear).GetComponent<Image>();
            Circle(specialRoot, "Back", 270f, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f));
            specialFill = Circle(specialRoot, "Charge", 270f, UiKit.Gold).GetComponent<Image>();
            specialFill.type = Image.Type.Filled;
            specialFill.fillMethod = Image.FillMethod.Radial360;
            specialFill.fillOrigin = (int)Image.Origin360.Top;
            specialFill.fillClockwise = true;
            specialFace = Circle(specialRoot, "Face", 214f, UiKit.Primary).GetComponent<Image>();
            specialName = UiKit.Label(specialFace.transform, "", 40, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(specialName.rectTransform, 20, 20, 20, 20);
            specialName.resizeTextForBestFit = true;
            specialName.resizeTextMinSize = 20;
            specialName.resizeTextMaxSize = 44;
            Edge(specialName, 3);

            deniedText = UiKit.Label(safe, "", 44, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.8f));
            Edge(deniedText, 3);

            // top: blue kills, time, red kills; the team stars under them
            scorePanel = UiKit.Panel(safe, new Color(UiKit.PanelColor.r, UiKit.PanelColor.g, UiKit.PanelColor.b, 0.75f)).rectTransform;
            scorePanel.GetComponent<Image>().raycastTarget = false;
            UiKit.Place(scorePanel, 0.5f, 1f, 0f, -24f, 520f, 116f);
            blueKills = UiKit.Label(scorePanel, "0", 72, TextAnchor.MiddleCenter, UiKit.Ally);
            UiKit.Place(blueKills, 0f, 0.5f, 20f, 0f, 120f, 110f);
            Edge(blueKills, 3);
            timeText = UiKit.Label(scorePanel, "0:00", 56, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(timeText, 0.5f, 0.5f, 0f, 0f, 220f, 110f);
            Edge(timeText, 3);
            redKills = UiKit.Label(scorePanel, "0", 72, TextAnchor.MiddleCenter, UiKit.Enemy);
            UiKit.Place(redKills, 1f, 0.5f, -20f, 0f, 120f, 110f);
            Edge(redKills, 3);
            starsRow = UiKit.NewRect("Stars", safe);
            UiKit.Place(starsRow, 0.5f, 1f, 0f, -146f, 520f, 40f);
            for (int i = 0; i < 6; i++)
            {
                bool red = i >= 3;
                int k = red ? i - 3 : i;
                var s = Circle(starsRow, "Star", 32f, new Color(1f, 1f, 1f, 0.18f));
                UiKit.Place(s, red ? 1f : 0f, 0.5f, red ? -(30f + k * 42f) : 30f + k * 42f, 0f, 32f, 32f);
                stars[i] = s.GetComponent<Image>();
            }

            var pause = UiKit.Button(safe, "II", () => flow.Pause(), UiKit.Muted, SfxId.UiTap, 56);
            UiKit.Place(pause, 1f, 1f, -24f, -24f, 120f, 120f);

            centerText = UiKit.Label(safe, "", 200, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(centerText, 0.5f, 0.5f, 0f, 200f, 900f, 260f);
            Edge(centerText, 6);
            respawnText = UiKit.Label(safe, "", 72, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(respawnText, 0.5f, 0.5f, 0f, 380f, 900f, 110f);
            Edge(respawnText, 4);

            // the guided match: chevrons over what to look at, and the black for the jump to the arena (on top of everything)
            canvasRect = (RectTransform)canvas.transform;
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

        // In-match text sits over the bright arena, so it keeps the old game's dark edge (UiKit.AddOutline became the menus'
        // soft shadow in the 2026-10-25 restyle).
        static void Edge(Text t, float distance)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.05f, 0.15f, 0.85f);
            o.effectDistance = new Vector2(distance, -distance);
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
