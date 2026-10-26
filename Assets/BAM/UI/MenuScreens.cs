using BAM.Art;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BAM.UI
{
    // The Modes sheet and the Heroes page, laid out like the old game's (ABC2 Hub v3, Rickard's screenshots 2026-10-25); Home is in HomeScreen.cs.
    // Heroes is a grid of cards.
    // Online, shop, rank, currencies and friends are left out. PLAY starts a match with the chosen hero and bot difficulty.
    public static partial class Screens
    {
        static GameObject mainMenu, heroSelect, modes;
        static bool tutorialOffered; // once per app run, so Play is never nagging

        static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        static readonly string[] StatNames = { "Health", "Damage", "Speed", "Difficulty" };

        static void SetBadge(RectTransform badge, HeroId id)
        {
            badge.GetChild(0).GetComponent<Image>().color = UiKit.HeroColor(id);
            badge.GetComponentInChildren<Text>().text = HeroCatalog.Get(id).Name.Substring(0, 1);
        }

        static void OnPlay()
        {
            if (!BamSettings.TutorialDone && !tutorialOffered)
            {
                tutorialOffered = true;
                dialog.Show("New to the arena?", "Play a short guided match first to learn how to move, attack and use your special.",
                    "Skip", StartBotMatch,
                    "Tutorial", StartTutorial,
                    UiKit.Muted, UiKit.Primary, () => dialog.Hide());
                return;
            }
            StartBotMatch();
        }

        static void StartBotMatch()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = BamSettings.LastDifficulty, Tutorial = false });
        }

        static void StartTutorial()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = Difficulty.Easy, Tutorial = true });
        }

        // ---- the Modes sheet ----

        static Segmented modeDifficulty;

        static void BuildModes()
        {
            var rt = UiKit.NewRect("Modes", root);
            UiKit.Stretch(rt);
            modes = rt.gameObject;
            var veil = UiKit.Fill(rt, UiKit.Dim);
            veil.raycastTarget = true;
            var close = veil.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => { BamAudio.Play(SfxId.UiBack); Pop(); });
            var safe = UiKit.SafeArea(rt);

            var sheet = UiKit.Panel(safe, PageFill);
            UiKit.Band(sheet, false, 24, 640, 20);
            UiKit.Frame(sheet.rectTransform);
            var title = UiKit.Label(sheet.transform, "Modes", 64, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Band(title, true, 20, 90, 36);

            var bots = UiKit.Label(sheet.transform, "3v3 against bots", 40, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Band(bots, true, 130, 60, 36);
            var diffRow = UiKit.NewRect("Difficulty", sheet.transform);
            UiKit.Band(diffRow, true, 200, 116, 36);
            modeDifficulty = new Segmented(diffRow, DifficultyNames, i =>
            {
                BamSettings.LastDifficulty = (Difficulty)i;
                RefreshHome();
            });

            var training = UiKit.Button(sheet.transform, "Training", () => StartTutorial(), UiKit.Muted, SfxId.UiConfirm, 52);
            var trt = (RectTransform)training.transform;
            UiKit.Band(trt, true, 350, 130, 36);
            var hint = UiKit.Label(sheet.transform, "A short guided match that teaches the basics.", 30, TextAnchor.MiddleCenter, UiKit.TextDim);
            hint.font = UiKit.BodyFont;
            UiKit.Band(hint, true, 486, 50, 36);
            var done = UiKit.Button(sheet.transform, "Done", () => Pop(), UiKit.Primary, SfxId.UiBack, 48);
            UiKit.Place(done, 0.5f, 0, 0, 30, 360, 100);
            modes.SetActive(false);
        }

        // ---- Heroes ----

        static Image[] cardFrames;
        static GameObject[] cardChecks;
        static Text detailName, detailTitle, detailRole, specialName, specialDesc, passiveName, passiveDesc;
        static Image[,] statBars;
        static ScrollRect heroScroll;

        static void BuildHeroSelect()
        {
            var content = NewPage("Heroes", out heroSelect, out var panel);
            tabPanels[1] = panel;
            heroScroll = panel.GetComponentInChildren<ScrollRect>();
            VList(content, 16, new RectOffset(8, 8, 8, 30));

            cardFrames = new Image[HeroCatalog.Count];
            cardChecks = new GameObject[HeroCatalog.Count];
            // 3 cards a row on a phone held upright; on a wide screen as many as fit, so the details stay in view.
            var grid = UiKit.NewRect("Grid", content);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.spacing = new Vector2(16, 16);
            gl.padding = new RectOffset(8, 8, 0, 0);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.childAlignment = TextAnchor.UpperCenter;
            grid.gameObject.AddComponent<HeroGridFit>().Grid = gl;
            for (int i = 0; i < HeroCatalog.Count; i++)
            {
                int index = i;
                var info = HeroCatalog.All[i];
                var card = UiKit.NewRect(info.Name, grid);
                var bg = card.gameObject.AddComponent<Image>();
                bg.sprite = UiKit.Rounded;
                bg.type = Image.Type.Sliced;
                bg.color = CardFill;
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); PickHero(index); });
                card.gameObject.AddComponent<ButtonFx>();
                cardFrames[i] = UiKit.Frame(card);

                // The whole portrait in the top of the card, rounded corners, never stretched or cut.
                var holder = UiKit.NewRect("PortraitArea", card);
                holder.anchorMin = new Vector2(0, 1);
                holder.anchorMax = new Vector2(1, 1);
                holder.pivot = new Vector2(0.5f, 1);
                holder.offsetMin = new Vector2(8, -248);
                holder.offsetMax = new Vector2(-8, -8);
                var mask = UiKit.Fill(holder, Color.white, UiKit.Rounded);
                var fit = mask.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                var tex = Portrait(info.Id);
                fit.aspectRatio = tex != null ? (float)tex.width / tex.height : 1.3f;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var pic = UiKit.NewRect("Portrait", mask.rectTransform);
                UiKit.Stretch(pic);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                var heroName = UiKit.Label(card, info.Name, 44, TextAnchor.MiddleCenter, UiKit.TextColor);
                UiKit.Band(heroName, false, 62, 56, 4);
                UiKit.AddShadow(heroName, 2);
                var role = UiKit.Label(card, info.Role, 30, TextAnchor.MiddleCenter, Lavender);
                role.font = UiKit.BodyFont;
                role.fontStyle = FontStyle.Bold;
                UiKit.Band(role, false, 22, 40, 4);

                // The old "selected" mark: a mint circle with a white tick in the top right corner.
                var check = UiKit.Fill(card, UiKit.Primary, UiKit.Circle);
                UiKit.Place(check, 1, 1, 10, 10, 56, 56);
                var tick1 = UiKit.Fill(check.rectTransform, Color.white);
                UiKit.Place(tick1, 0.5f, 0.5f, -8, -3, 16, 6).localRotation = Quaternion.Euler(0, 0, -45);
                var tick2 = UiKit.Fill(check.rectTransform, Color.white);
                UiKit.Place(tick2, 0.5f, 0.5f, 5, 1, 26, 6).localRotation = Quaternion.Euler(0, 0, 50);
                cardChecks[i] = check.gameObject;
            }

            // The chosen hero's details under the grid.
            var detail = UiKit.NewRect("Details", content);
            var dbg = detail.gameObject.AddComponent<Image>();
            dbg.sprite = UiKit.Rounded;
            dbg.type = Image.Type.Sliced;
            dbg.color = CardFill;
            dbg.raycastTarget = false;
            UiKit.Frame(detail).color = new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.75f);
            VList(detail, 10, new RectOffset(36, 36, 28, 36));

            detailName = DetailText(detail, 72, UiKit.Gold, 0);
            UiKit.AddOutline(detailName, 3);
            detailTitle = DetailText(detail, 42, UiKit.TextColor, 0);
            detailRole = DetailText(detail, 32, Lavender, 0);
            detailRole.font = UiKit.BodyFont;
            detailRole.fontStyle = FontStyle.Bold;

            statBars = new Image[StatNames.Length, 3];
            for (int s = 0; s < StatNames.Length; s++)
            {
                var statRow = UiKit.NewRect(StatNames[s], detail);
                statRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
                var label = UiKit.Label(statRow, StatNames[s], 36, TextAnchor.MiddleLeft, UiKit.TextColor);
                label.rectTransform.anchorMin = new Vector2(0, 0);
                label.rectTransform.anchorMax = new Vector2(0.42f, 1);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                for (int k = 0; k < 3; k++)
                {
                    var seg = UiKit.Fill(statRow, UiKit.Muted, UiKit.Rounded);
                    seg.pixelsPerUnitMultiplier = 2.5f;
                    var r = seg.rectTransform;
                    r.anchorMin = new Vector2(0.42f + 0.58f * k / 3f, 0.22f);
                    r.anchorMax = new Vector2(0.42f + 0.58f * (k + 1) / 3f, 0.78f);
                    r.offsetMin = new Vector2(5, 0);
                    r.offsetMax = new Vector2(-5, 0);
                    statBars[s, k] = seg;
                }
            }

            specialName = IconTitle(detail, out specialIcon, 14);
            specialDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            specialDesc.font = UiKit.BodyFont;
            passiveName = IconTitle(detail, out passiveIcon, 10);
            passiveDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            passiveDesc.font = UiKit.BodyFont;
        }

        static Image specialIcon, passiveIcon;

        // "Special: Hammer Throw" with the ability's icon in front, like the old hero page.
        static Text IconTitle(RectTransform list, out Image icon, float spaceAbove)
        {
            var gap = UiKit.NewRect("Gap", list);
            gap.gameObject.AddComponent<LayoutElement>().preferredHeight = spaceAbove;
            var row = UiKit.NewRect("Ability", list);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            var disc = UiKit.Fill(row, new Color(0.1f, 0.11f, 0.25f), UiKit.Circle);
            UiKit.Place(disc, 0, 0.5f, 0, 0, 72, 72);
            icon = UiKit.Fill(disc.rectTransform, UiKit.Gold);
            UiKit.Stretch(icon.rectTransform, 12, 12, 12, 12);
            icon.preserveAspect = true;
            var t = UiKit.Label(row, "", 44, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Stretch(t.rectTransform, 90, 0, 0, 0);
            return t;
        }

        static Sprite IconSprite(string name)
        {
            var tex = Picture(name);
            if (tex == null) return null;
            if (!iconSprites.TryGetValue(name, out var sp))
            {
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                iconSprites[name] = sp;
            }
            return sp;
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> iconSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        static void HeaderText(Text t, float left, float y, float h)
        {
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 0.5f);
            r.anchorMax = new Vector2(1, 0.5f);
            r.pivot = new Vector2(0.5f, 0);
            r.offsetMin = new Vector2(left, y);
            r.offsetMax = new Vector2(0, y + h);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static Text DetailText(RectTransform list, int size, Color color, float spaceAbove)
        {
            if (spaceAbove > 0)
            {
                var gap = UiKit.NewRect("Gap", list);
                gap.gameObject.AddComponent<LayoutElement>().preferredHeight = spaceAbove;
            }
            return UiKit.Label(list, "", size, TextAnchor.UpperLeft, color);
        }

        /// <summary>A vertical scroll area filling its parent; returns the content to fill (it grows with its children).</summary>
        static RectTransform ScrollContent(RectTransform parent, out ScrollRect scroll)
        {
            var viewport = UiKit.NewRect("Viewport", parent);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var content = UiKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        static void RefreshHeroSelect()
        {
            PickHero(BamSettings.LastHero, false);
            if (heroScroll != null) heroScroll.verticalNormalizedPosition = 1;
        }

        // Picking a card makes that hero the one PLAY uses (saved at once, like the old game).
        static void PickHero(int index, bool save = true)
        {
            index = Mathf.Clamp(index, 0, HeroCatalog.Count - 1);
            if (save) BamSettings.LastHero = index;
            for (int i = 0; i < cardFrames.Length; i++)
            {
                bool on = i == index;
                cardFrames[i].color = on ? UiKit.Primary : new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.75f);
                cardChecks[i].SetActive(on);
            }

            var info = HeroCatalog.All[index];
            detailName.text = info.Name;
            detailTitle.text = info.Title;
            detailRole.text = info.Role + (info.Ranged ? "  /  Ranged" : "  /  Melee");
            SetBar(0, info.Health);
            SetBar(1, info.Damage);
            SetBar(2, info.Speed);
            SetBar(3, info.Difficulty);
            specialName.text = "Special: " + info.SpecialName;
            specialIcon.sprite = IconSprite("special_" + Lower(info.Id));
            passiveIcon.sprite = IconSprite("passive_" + Lower(info.Id));
            specialDesc.text = info.SpecialDesc;
            passiveName.text = "Passive: " + info.PassiveName;
            passiveDesc.text = info.PassiveDesc;
        }

        static void SetBar(int stat, int value)
        {
            for (int k = 0; k < 3; k++) statBars[stat, k].color = k < value ? UiKit.Gold : UiKit.Muted;
        }
    }

    /// <summary>Picks the hero grid's column count and card size from its width (3 upright, more on wide screens).</summary>
    sealed class HeroGridFit : MonoBehaviour
    {
        public GridLayoutGroup Grid;
        float lastWidth = -1;

        void OnRectTransformDimensionsChange() => Fit();
        void OnEnable() => Fit();

        void Fit()
        {
            if (Grid == null) return;
            float w = ((RectTransform)transform).rect.width;
            if (w <= 0 || Mathf.Abs(w - lastWidth) < 0.5f) return;
            lastWidth = w;
            float inner = w - Grid.padding.left - Grid.padding.right;
            int count = transform.childCount;
            int cols = Mathf.Clamp(Mathf.FloorToInt((inner + Grid.spacing.x) / (380 + Grid.spacing.x)), 3, Mathf.Max(3, count));
            Grid.constraintCount = cols;
            Grid.cellSize = new Vector2((inner - Grid.spacing.x * (cols - 1)) / cols, 370);
        }
    }

    /// <summary>
    /// Shows one hero's 3D model (ArtKit.CreateHeroPreview) in the UI: a small orthographic camera far from the arena films
    /// it into a RenderTexture drawn by <see cref="Picture"/>, sized so the hero is HeroFill of the picture's height with the
    /// feet FeetAt up from its bottom (the old HubHero3). The camera only runs while this is visible. Sits on the touch area:
    /// a drag sideways turns the hero, a tap calls Tapped.
    /// </summary>
    sealed class HeroStage : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float StartYaw = 160f;  // faces the camera, a little turned
        const float DegPerUnit = 0.45f; // a finger across the whole screen turns him about one and a half times
        float yaw = StartYaw, spin;   // spin: degrees per second left after a flick, slowing down
        bool dragging, dragged;
        float lastDragAt;
        static readonly Vector3 Spot = new Vector3(500, 0, 500);

        public RawImage Picture;
        public System.Action Tapped;
        public float FeetAt = 0.07f, HeroFill = 0.75f;

        Camera cam;
        RenderTexture rt;
        UnitView view;
        HeroId shownId;
        bool hasHero;
        float heroHeight = 1.8f; // world units, measured from the model when shown

        /// <summary>Shows the hero; false if the model could not be made (the caller shows a fallback).</summary>
        public bool Show(HeroId id)
        {
            if (hasHero && id == shownId) return true;
            Setup();
            if (hasHero) { ArtKit.Release(view); view = null; hasHero = false; }
            try { view = ArtKit.CreateHeroPreview(id); }
            catch (System.Exception e) { Debug.LogException(e); view = null; }
            if (view == null) { Picture.enabled = false; return false; }
            view.transform.SetPositionAndRotation(Spot, Quaternion.Euler(0, yaw, 0));
            view.SetMoving(false, 0);
            heroHeight = MeasureHeight(view);
            shownId = id;
            hasHero = true;
            Picture.enabled = true;
            cam.enabled = isActiveAndEnabled;
            Frame();
            return true;
        }

        // From the feet to the top of the model's meshes; 1.8 if that looks wrong.
        static float MeasureHeight(UnitView v)
        {
            float top = 0;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y - Spot.y);
            }
            return top > 0.6f && top < 4f ? top : 1.8f;
        }

        /// <summary>Fits the RenderTexture and the camera to the picture's size on screen (call after a layout change).</summary>
        public void Frame()
        {
            if (cam == null || Picture == null) return;
            var r = Picture.rectTransform;
            Vector2 size = r.rect.size;
            if (size.x <= 1 || size.y <= 1) return;
            float scale = Mathf.Abs(r.lossyScale.x);
            int w = Mathf.Clamp(Mathf.RoundToInt(size.x * scale), 16, 1280);
            int h = Mathf.Clamp(Mathf.RoundToInt(size.y * scale), 16, 1280);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
                // the multisampled buffers stay on the chip (Vulkan, Metal), so the big picture costs little memory
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                { name = "BAM.UI.HeroStage", antiAliasing = 4, memorylessMode = RenderTextureMemoryless.MSAA };
                cam.targetTexture = rt;
                Picture.texture = rt;
            }
            float viewH = heroHeight / HeroFill;
            cam.orthographicSize = viewH / 2;
            cam.aspect = size.x / size.y;
            cam.transform.position = Spot + new Vector3(0, viewH / 2 - FeetAt * viewH, -6f);
        }

        void Setup()
        {
            if (cam != null) return;
            var go = new GameObject("BAM.HeroStageCamera");
            DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 12;
            cam.depth = -50;
            go.transform.rotation = Quaternion.identity; // level, straight in front of the hero (it looks toward -z)
            cam.enabled = false;
        }

        // Drag sideways to turn the hero; let go mid-drag and he keeps turning a moment, slowing down.
        public void OnPointerDown(PointerEventData e) => dragged = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!dragged && !e.dragging) Tapped?.Invoke();
        }

        public void OnBeginDrag(PointerEventData e) { dragging = true; dragged = true; spin = 0; }

        public void OnDrag(PointerEventData e)
        {
            float d = -e.delta.x / transform.lossyScale.x * DegPerUnit;
            Turn(d);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            spin = Mathf.Lerp(spin, d / dt, 0.5f);
            lastDragAt = Time.unscaledTime;
        }

        public void OnEndDrag(PointerEventData e)
        {
            dragging = false;
            if (Time.unscaledTime - lastDragAt > 0.1f) spin = 0; // the finger stood still before lifting: no flick
        }

        void Update()
        {
            if (dragging || spin == 0) return;
            Turn(spin * Time.unscaledDeltaTime);
            spin = Mathf.MoveTowards(spin, 0, Mathf.Max(360f, Mathf.Abs(spin) * 3f) * Time.unscaledDeltaTime);
        }

        void Turn(float degrees)
        {
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            if (hasHero) view.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        void OnEnable() { if (cam != null) cam.enabled = hasHero; }
        void OnDisable() { if (cam != null) cam.enabled = false; }
    }
}
