using System.Collections.Generic;
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

        static string[] DifficultyNames => DifficultyOrder.Names; // in menu order, Beginner first: index with DifficultyOrder.Pos

        static void SetBadge(RectTransform badge, HeroId id)
        {
            badge.GetChild(0).GetComponent<Image>().color = UiKit.HeroColor(id);
            badge.GetComponentInChildren<Text>().text = HeroCatalog.Get(id).Name.Substring(0, 1);
        }

        // The tutorial is not offered here any more: the very first start goes into it by itself (GameFlow.StartFirstTutorial,
        // the old MenuNewPlayer), with a Skip button. Training in MODES plays it again.
        static void OnPlay()
        {
            // Rickard 2026-11-16 (phone): "man kommer direkt in i 3v3" when the game opens. A tap made while the game was still
            // loading, or the tap that brought it back to the front, landed on the full-width PLAY the moment Home came up.
            // PLAY only counts once Home has been on screen a moment after a start or a return to the game.
            if (Time.frameCount < playReadyFrame || Time.realtimeSinceStartup < playReadyAt) return;
            PlayMode();
        }

        static int playReadyFrame;
        static float playReadyAt;
        const float PlayHoldSeconds = 1f;
        const int PlayHoldFrames = 20;

        /// <summary>The game started or came back to the front: Home's PLAY waits a moment (OnPlay).</summary>
        internal static void HoldPlay()
        {
            playReadyFrame = Time.frameCount + PlayHoldFrames;
            playReadyAt = Time.realtimeSinceStartup + PlayHoldSeconds;
        }

        static void StartBotMatch()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = BamSettings.LastDifficulty, Tutorial = false, Map = BamSettings.LastMap, TeamSize = BamSettings.TeamSize });
        }

        /// <summary>A bot match with the last hero, bot level, map and team size, as Home's PLAY in CLASSIC: for the online queue's PLAY BOTS INSTEAD.</summary>
        public static void PlayBotMatch() => StartBotMatch();

        static void StartTutorial()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = Difficulty.Easy, Tutorial = true });
        }

        // ---- the Modes sheet: ModesSheet.cs ----

        // ---- Heroes (HubPages.cs) ----

        static Sprite IconSprite(string name)
        {
            var tex = Picture(name, true); // a new hero's ability icons can come later
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
        /// <summary>Degrees the hero slowly turns either way on his own (old Speltest: 0.25 rad on Home, 0.35 on the hero page).</summary>
        public float Sway = 14f;
        /// <summary>Where the hero faces before any drag (180 = straight at the camera); a party member on Home turns toward the middle.</summary>
        public float Yaw { get => yaw; set => yaw = value; }

        Camera cam;
        RenderTexture rt;
        UnitView view;
        HeroId shownId;
        int shownSkin; // the look shown (HeroSkins): a new pick on Home builds the hero again
        bool hasHero;
        float heroHeight = 1.8f; // world units, measured from the model when shown
        // the held weapons' highest point over the feet so far (world units, 0 = none yet), from the frame the idle holds them,
        // never while a tapped swing plays
        float gearTop, swingTill;
        // the body's half width about its middle (world units, 0 = none yet): a wide hero (round Kugg) is drawn smaller until he
        // fits across the picture too, not only from feet to crown
        float bodyHalfW;
        bool kuggV2 = true; // which Kugg body is built (HeroArt.V2: the round cannon-arm remake, or the old stone golem)
        int measureAt;

        /// <summary>Shows the hero; false if the model could not be made (the caller shows a fallback). mine: in this player's own skin
        /// (HeroSkins); else in other, the skin a party member's phone sent (Rickard 2026-11-15).</summary>
        public bool Show(HeroId id, bool mine = true, int other = 0)
        {
            int skin = mine ? HeroSkins.Chosen(id) : HeroSkins.Valid(id, other);
            if (hasHero && id == shownId && skin == shownSkin) return true;
            Setup();
            if (hasHero) { ArtKit.Release(view); view = null; hasHero = false; }
            try { view = ArtKit.CreateHeroPreview(id, skin); }
            catch (System.Exception e) { Debug.LogException(e); view = null; }
            if (view == null) { Picture.enabled = false; return false; }
            view.transform.SetPositionAndRotation(Spot, Quaternion.Euler(0, Facing(), 0));
            view.SetMoving(false, 0);
            heroHeight = MeasureHeight(view, Spot.y);
            if (id == HeroId.Kugg) { var ka = Resources.Load<HeroArt>("BAMArt/Heroes/Kugg"); kuggV2 = ka == null || ka.V2 || skin > 0; } // a skin is the V2 body
            gearTop = swingTill = bodyHalfW = 0;
            measureAt = Time.frameCount + 2; // the model plays its idle from the next frame, and the weapons follow in its LateUpdate
            shownId = id;
            shownSkin = skin;
            hasHero = true;
            view.gameObject.SetActive(isActiveAndEnabled);
            Picture.enabled = false; // shown once its weapons are measured (FitGear), so the hero never jumps smaller
            cam.enabled = isActiveAndEnabled;
            Frame();
            return true;
        }

        // From the feet to the crown, as the art fitted the model (a freshly made skinned model's bounds are not yet right,
        // and hidden parts must not count); 1.8 if that looks wrong.
        static float MeasureHeight(UnitView v, float feet)
        {
            float top = ArtKit.HeightOf(v);
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
            // a weapon held above the head stays in the picture: the hero is drawn only as much smaller as that needs (Rickard's
            // pick 2026-10-29; ANIM-23: Bram's hammer reaches 1.36 times his height, and since the heroes' true heights its top was cut)
            float aspect = size.x / size.y;
            float viewH = Mathf.Max(heroHeight / HeroFill, gearTop * 1.04f / Mathf.Max(0.05f, 1f - FeetAt));
            // the width may draw him at most a little smaller than the height does: fitted fully across, round Kugg with his cannon
            // held out to the side stood clearly smaller than the others in the hero pick (Rickard 2026-11-19: "kugg för liten", "jag
            // pratar om hur dom ser ut i menyn"); the barrel's mouth may touch the picture's edge in the narrowest pictures
            // The old stone body (Models/Kugg, Rickard 2026-11-19: "jag gillar gamla kuggmodellen mer") holds both big fists out wide
            // and filled the whole picture with his arms past its edges, so he is fitted across in full
            viewH = Mathf.Max(viewH, Mathf.Min(2f * bodyHalfW * 1.08f / aspect, viewH * (kuggV2 ? 1.05f : 10f)));
            cam.orthographicSize = viewH / 2;
            cam.aspect = aspect;
            cam.transform.position = Spot + new Vector3(0, viewH / 2 - FeetAt * viewH, -6f);
        }

        /// <summary>Plays one attack (a tap on the hero page's stage). Not Knut: his swing broke loose on the stage (Rickard 2026-11-02
        /// "ta bort knuts autoattack när man klickar på bilden"), so a tap on him does nothing.</summary>
        public void Swing()
        {
            if (!hasHero || shownId == HeroId.Knut) return;
            view.PlayAttack(0.3f);
            swingTill = Time.unscaledTime + 3f; // the longest attack clip is 2.4 s; the swing may leave the picture, as in the old game
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
            if (!dragging && spin != 0)
            {
                Turn(spin * Time.unscaledDeltaTime);
                spin = Mathf.MoveTowards(spin, 0, Mathf.Max(360f, Mathf.Abs(spin) * 3f) * Time.unscaledDeltaTime);
            }
            if (hasHero) view.transform.rotation = Quaternion.Euler(0, Facing(), 0);
            if (hasHero) FitGear();
        }

        // as the old ShowTopH (View3DTeam.cs): the frame only grows while a hero is shown, so his idle never pumps it, and a weapon
        // that sticks up far (thrown, a bad pose) is counted as at most 1.6 times his height
        void FitGear()
        {
            if (Time.frameCount < measureAt) return;
            if (Time.unscaledTime >= swingTill)
            {
                float top = Mathf.Min(ArtKit.GearTopOf(view) - Spot.y, 1.6f * heroHeight);
                if (top > gearTop + 0.001f) { gearTop = top; Frame(); }
                // only round Kugg is fitted across (what it was made for): Knut's baked width came out far too wide and drew him at about
                // half size on Home (Rickard 2026-11-16 "knut är liten dvärg nu bara i menyn", "fortfarande för liten" after a rebuild)
                if (bodyHalfW <= 0f && shownId != HeroId.Kugg) bodyHalfW = 0.001f;
                if (bodyHalfW <= 0f)
                {
                    // and his height is measured where the posed body really reaches, not taken from his Height: framed by that,
                    // Kugg stood about 0.6 of the others' height on Home (Rickard 2026-11-18, his picture: "kugg för liten")
                    float bodyTop = BodyTop();
                    // (his chimney stands well over his head, so he is framed by 0.9 of that top, about where the others' crowns are)
                    // (the old body has no chimney: its dome is its crown)
                    if (bodyTop > 0.3f * heroHeight && bodyTop < 1.5f * heroHeight) heroHeight = (kuggV2 ? 0.9f : 1f) * bodyTop;
                    bodyHalfW = Mathf.Max(0.001f, Mathf.Min(BodyHalfWidth(), 1.2f * heroHeight));
                    Frame();
                }
            }
            if (!Picture.enabled) Picture.enabled = true;
        }

        // how far the skinned body reaches to either side of the stage's middle in the picture, over his resting turn (the start yaw
        // and the sway either way), measured once on the posed mesh in its idle (a skinned renderer's bounds are the import's, T-pose
        // arms and all); held weapons left out, as the height above leaves them to FitGear. It was his reach all round the middle,
        // so the arm he held toward the viewer counted as width and Kugg came out about half the others' size on Home (Rickard
        // 2026-11-18, his picture: "kanske bara är för att han är så liten"); a drag that turns him side on may take an arm past the edge
        // the skinned body's highest point over the stage's feet in its idle (the baked, posed mesh, held weapons left out)
        float BodyTop()
        {
            float top = 0f;
            var baked = new Mesh();
            var verts = new List<Vector3>();
            foreach (var sk in view.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.enabled || !sk.gameObject.activeInHierarchy || sk.sharedMesh == null) continue;
                sk.BakeMesh(baked, true);
                baked.GetVertices(verts);
                Transform t = sk.transform;
                for (int k = 0; k < verts.Count; k++) top = Mathf.Max(top, (t.position + t.rotation * verts[k]).y - Spot.y);
            }
            Destroy(baked);
            return top;
        }

        float BodyHalfWidth()
        {
            float r = 0f;
            Quaternion toLocal = Quaternion.Inverse(view.transform.rotation);
            var baked = new Mesh();
            var verts = new List<Vector3>();
            foreach (var sk in view.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.enabled || !sk.gameObject.activeInHierarchy || sk.sharedMesh == null) continue;
                sk.BakeMesh(baked, true);
                baked.GetVertices(verts);
                Transform t = sk.transform;
                for (int k = 0; k < verts.Count; k++)
                {
                    Vector3 l = toLocal * (t.position + t.rotation * verts[k] - Spot);
                    for (int s = -1; s <= 1; s++)
                    {
                        float f = (yaw + s * Sway) * Mathf.Deg2Rad;
                        r = Mathf.Max(r, Mathf.Abs(l.x * Mathf.Cos(f) + l.z * Mathf.Sin(f)));
                    }
                }
            }
            Destroy(baked);
            return r;
        }

        // the dragged yaw plus a slow sway, so the hero never stands frozen (old Speltest View3DTeam: sin(t * 0.6))
        float Facing() => yaw + Sway * Mathf.Sin(Time.unscaledTime * 0.6f);

        void Turn(float degrees)
        {
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            if (hasHero) view.transform.rotation = Quaternion.Euler(0, Facing(), 0);
        }

        // a hidden stage also parks its hero: an active preview would keep animating and skinning through every match
        void OnEnable()
        {
            if (hasHero) view.gameObject.SetActive(true);
            if (cam != null) cam.enabled = hasHero;
        }

        void OnDisable()
        {
            if (hasHero && view != null) view.gameObject.SetActive(false);
            if (cam != null) cam.enabled = false;
        }
    }
}
