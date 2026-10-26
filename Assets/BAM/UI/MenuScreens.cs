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

        static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        static readonly string[] StatNames = { "Health", "Damage", "Speed", "Difficulty" };

        static void SetBadge(RectTransform badge, HeroId id)
        {
            badge.GetChild(0).GetComponent<Image>().color = UiKit.HeroColor(id);
            badge.GetComponentInChildren<Text>().text = HeroCatalog.Get(id).Name.Substring(0, 1);
        }

        // The tutorial is not offered here any more: the very first start goes into it by itself (GameFlow.StartFirstTutorial,
        // the old MenuNewPlayer), with a Skip button. Training in MODES plays it again.
        static void OnPlay() => StartBotMatch();

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

        // ---- Heroes (HubPages.cs) ----

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

        public RawImage Picture;
        public Vector3 Spot = new Vector3(500, 0, 500); // far from the arena; each stage needs its own spot
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
            heroHeight = MeasureHeight(view, Spot.y);
            shownId = id;
            hasHero = true;
            Picture.enabled = true;
            cam.enabled = isActiveAndEnabled;
            Frame();
            return true;
        }

        // From the feet to the top of the model's meshes; 1.8 if that looks wrong.
        static float MeasureHeight(UnitView v, float feet)
        {
            float top = 0;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y - feet);
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

        /// <summary>Plays one attack (a tap on the hero page's stage).</summary>
        public void Swing() { if (hasHero) view.PlayAttack(0.3f); }

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
