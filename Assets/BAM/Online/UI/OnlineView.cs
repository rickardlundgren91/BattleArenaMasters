using System.Collections.Generic;
using System.Text;
using BAM.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BAM.Online.UI
{
    /// <summary>
    /// Draws the online pages the way the old game drew its menu: Ctx in OnGUI, in the 400 px wide portrait frame
    /// (Speltest AbcGame.OnGUI: dpr = frame width / 400). While a page is open, a full-screen invisible image on a canvas above the
    /// game's menus takes the touches and hands them to the page in frame pixels, so nothing under the page reacts.
    /// Also renders the Home chip (name, level, rank) into a texture for the game's own Home top bar.
    /// </summary>
    sealed class OnlineView : MonoBehaviour
    {
        const float FrameCss = 400f;
        const int SortingOrder = 120; // above the menu canvas (100)

        Ctx ctx;
        GameObject blocker;
        readonly StringBuilder typed = new StringBuilder();
        Keyboard hooked;
        readonly List<Chip> chips = new List<Chip>();
        float chipAt;
        internal bool chipDirty = true;

        internal static OnlineView Create()
        {
            var go = new GameObject("BAM Online UI");
            DontDestroyOnLoad(go);
            return go.AddComponent<OnlineView>();
        }

        void Awake()
        {
            useGUILayout = false;
            ctx = new Ctx(Canvas.GetDefaultCanvasMaterial());
            var canvas = UiKit.CreateCanvas("BAM Online Input", SortingOrder);
            canvas.transform.SetParent(transform, false);
            var rt = UiKit.NewRect("Blocker", canvas.transform);
            UiKit.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
            rt.gameObject.AddComponent<Touches>();
            blocker = rt.gameObject;
            blocker.SetActive(false);
        }

        void OnDisable() { Unhook(); }

        static Rect Frame(out float dpr)
        {
            var f = UiKit.PortraitFrame();
            dpr = f.width / FrameCss;
            return f;
        }

        /// <summary>A screen position (bottom-left origin) in frame pixels (top-left origin).</summary>
        internal static Vector2 ToFrame(Vector2 screen)
        {
            var f = Frame(out float dpr);
            return new Vector2((screen.x - f.x) / dpr, (f.yMax - screen.y) / dpr);
        }

        void Update()
        {
            bool open = OnlineMenu.page != null || OnlineMenu.InviteShown;
            if (blocker.activeSelf != open) { blocker.SetActive(open); if (!open) OnlineMenu.ResetPointer(); }
            Social.Tick();
            OnlineMenu.MpTick();
            if (OnlineMenu.Typing) Hook(); else Unhook();
            var kbd = Keyboard.current;
            if (OnlineMenu.Typing && kbd != null)
            {
                if (kbd.backspaceKey.wasPressedThisFrame) typed.Append('\b');
                if (kbd.enterKey.wasPressedThisFrame || kbd.numpadEnterKey.wasPressedThisFrame) typed.Append('\n');
            }
            OnlineMenu.TextInput(typed.ToString());
            typed.Clear();
        }

        // keys typed on a computer (the editor); the phone uses its own keyboard (TouchScreenKeyboard in OnlineMenu)
        void Hook()
        {
            var k = Keyboard.current;
            if (k == hooked) return;
            Unhook();
            if (k == null) return;
            k.onTextInput += OnChar;
            hooked = k;
        }
        void Unhook()
        {
            if (hooked != null) hooked.onTextInput -= OnChar;
            hooked = null;
        }
        void OnChar(char c) { if (!char.IsControl(c)) typed.Append(c); }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || OnlineMenu.page == null && !OnlineMenu.InviteShown && !OnlineMenu.ToastLive) return;
            var f = Frame(out float dpr);
            ctx.Begin(f.x, Screen.height - f.yMax, FrameCss, f.height / dpr, dpr);
            try { OnlineMenu.Draw(ctx, FrameCss, f.height / dpr); }
            catch (System.Exception e)
            {
                // a page that throws would throw again every frame behind a blocker with no Back button: close it instead
                Debug.LogException(e);
                OnlineMenu.Close();
            }
            finally { ctx.End(); }
        }

        // ---------- the Home chip ----------
        internal void AddChip(RawImage image, string piece = null) { chips.Add(new Chip { image = image, piece = piece }); chipDirty = true; }

        void LateUpdate()
        {
            if (chips.Count == 0) return;
            if (!chipDirty && Time.unscaledTime - chipAt < 1f) return; // presence and loading change without an event now and then
            chipDirty = false; chipAt = Time.unscaledTime;
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                var c = chips[i];
                if (c.image == null) { if (c.rt != null) c.rt.Release(); chips.RemoveAt(i); continue; }
                if (!c.image.isActiveAndEnabled) continue;
                DrawChip(c);
            }
        }

        void DrawChip(Chip c)
        {
            var r = c.image.rectTransform.rect;
            var canvas = c.image.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            int pw = Mathf.Max(8, Mathf.RoundToInt(r.width * scale)), ph = Mathf.Max(8, Mathf.RoundToInt(r.height * scale));
            if (c.rt == null || c.rt.width != pw || c.rt.height != ph)
            {
                if (c.rt != null) c.rt.Release();
                c.rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32) { name = "BAM Online Chip" };
                c.image.texture = c.rt;
            }
            var prev = RenderTexture.active;
            ctx.Begin(c.rt);
            try { if (c.piece != null) OnlineMenu.DrawPiece(ctx, c.piece, pw, ph); else OnlineMenu.DrawChip(ctx, pw, ph); }
            finally { ctx.End(); RenderTexture.active = prev; }
        }

        void OnDestroy()
        {
            foreach (var c in chips) if (c.rt != null) c.rt.Release();
        }

        sealed class Chip { public RawImage image; public RenderTexture rt; public string piece; }

        /// <summary>The blocker's touches, in frame pixels. The drag threshold is off so a page scrolls from the first pixel.</summary>
        sealed class Touches : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler, IScrollHandler
        {
            public void OnPointerDown(PointerEventData e) { var p = ToFrame(e.position); OnlineMenu.PointerDown(p.x, p.y, e.pointerId); }
            public void OnPointerUp(PointerEventData e) { var p = ToFrame(e.position); OnlineMenu.PointerUp(e.pointerId, p.x, p.y); }
            public void OnDrag(PointerEventData e) { var p = ToFrame(e.position); OnlineMenu.PointerMove(e.pointerId, p.x, p.y); }
            public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
            public void OnScroll(PointerEventData e) { if (e.scrollDelta.y != 0) OnlineMenu.Wheel(-Mathf.Sign(e.scrollDelta.y) * 40f); } // one notch = 40 px, as AbcGame
        }
    }
}
