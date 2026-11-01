using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BAM.Game
{
    /// <summary>
    /// The floating joystick: a touch anywhere in its half of the screen puts the stick under the finger. Value is the stick
    /// in screen space (up = +y), length 0..1. Each touch is followed by its own pointer id, so the other thumb can use the
    /// special button at the same time.
    /// </summary>
    sealed class JoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IHudTouch
    {
        public const float Radius = 125f;
        // the old dead zone: 6 of the stick's 44 px; past it the hero walks at full speed (Rickard 2026-10-08, again 2026-10-26)
        const float DeadZone = 6f / 44f;
        const int None = int.MinValue;

        public RectTransform Area, Base, Knob;
        public CanvasGroup Look;
        public Vector2 Value;
        int pointer = None;
        Vector2 origin;

        public bool Held => pointer != None;

        /// <summary>Where the stick waits when nobody touches it (local to Area, which has its pivot in the middle).</summary>
        public Vector2 RestPosition(bool rightSide)
        {
            // the old game's joyRest: 22 % of the width in from the side, 13 % of the height up from the bottom, measured on the
            // whole HUD (the safe area), not on the touch zone, so it never depends on the zone's layout. On a foldable or tablet
            // (SideGap > 0) it is measured from the screen's edge instead, as far in as on a 9:16 phone (1080 units wide).
            var frame = (RectTransform)Area.parent;
            Rect f = frame.rect;
            float inset = 0.22f * Mathf.Min(f.width, 1080f);
            var at = new Vector3(rightSide ? f.xMax + SideGap - inset : f.xMin - SideGap + inset, f.yMin + 0.13f * f.height, 0f);
            return Area.InverseTransformPoint(frame.TransformPoint(at));
        }

        public bool RightSide;

        /// <summary>How far the screen reaches past the HUD's safe area on the stick's side (canvas units; 0 on a phone).</summary>
        public float SideGap;

        public void OnPointerDown(PointerEventData e) { if (!HudTouch.Direct) TouchDown(e.pointerId, e.position); }
        public void OnDrag(PointerEventData e) { if (!HudTouch.Direct) TouchMove(e.pointerId, e.position); }
        public void OnPointerUp(PointerEventData e) { if (!HudTouch.Direct) TouchUp(e.pointerId, e.position); }

        public void TouchDown(int id, Vector2 screen)
        {
            if (pointer != None) return;
            pointer = id;
            origin = Local(screen);
            Base.anchoredPosition = origin;
            Knob.anchoredPosition = origin;
            Value = Vector2.zero;
            Look.alpha = 0.85f; // the old game: 0.85
        }

        public void TouchMove(int id, Vector2 screen)
        {
            if (id != pointer) return;
            Vector2 d = Local(screen) - origin;
            float len = d.magnitude;
            if (len > Radius) d *= Radius / len;
            Knob.anchoredPosition = origin + d;
            Vector2 v = d / Radius;
            Value = v.sqrMagnitude < DeadZone * DeadZone ? Vector2.zero : v.normalized;
        }

        public void TouchUp(int id, Vector2 screen)
        {
            if (id == pointer) Release();
        }

        public void Release()
        {
            pointer = None;
            Value = Vector2.zero;
            Rest();
        }

        public void Rest()
        {
            if (Held) return;
            Vector2 p = RestPosition(RightSide);
            Base.anchoredPosition = p;
            Knob.anchoredPosition = p;
            // Speltest draws its default (fixed) stick at 0.85 at rest too; 0.45 is only its floating stick, and that was
            // too faint to find (a tester, 2026-10-28)
            Look.alpha = 0.85f;
        }

        Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, screen, null, out Vector2 v);
            return v;
        }
    }

    /// <summary>
    /// The special button: a tap fires it at the automatic target; a drag aims it and fires on release; letting go on the cancel
    /// mark above the button cancels (the old HudAim.cs; Rickard 2026-10-25: back on the button no longer cancels). A drag starts
    /// once the finger has moved half a button radius from where it touched, so a sloppy tap stays a tap. Offset is the finger
    /// relative to the button's centre in canvas units.
    /// </summary>
    sealed class SpecialButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IHudTouch
    {
        public const float Radius = 135f, DragStart = 0.5f * Radius, MaxDrag = 300f;
        /// <summary>The cancel mark: this far above the button's centre, this big. The old CANCEL_Y, CANCEL_R were 3.6 and 0.85 radii,
        /// only about one radius past full aim (1.8 radii, Hud.AimK), so a hard pull forward cancelled by mistake (tester feedback
        /// 2026-11-01); now about two radii past full aim and a little smaller, above the ATTACK/DEFEND buttons.</summary>
        public const float CancelUp = 4.6f * Radius, CancelRadius = 0.7f * Radius;
        const int None = int.MinValue;

        public RectTransform Rect;
        public bool Pressed, Dragging, OverCancel;
        public float PressedAt;        // when the finger landed (hold to read, HudHold.cs)
        public Vector2 Offset;
        Vector2 start;
        /// <summary>A tap, or an aimed release with the final offset.</summary>
        public Action Tapped;
        public Action<Vector2> Aimed;
        int pointer = None;

        public void OnPointerDown(PointerEventData e) { if (!HudTouch.Direct) TouchDown(e.pointerId, e.position); }
        public void OnDrag(PointerEventData e) { if (!HudTouch.Direct) TouchMove(e.pointerId, e.position); }
        public void OnPointerUp(PointerEventData e) { if (!HudTouch.Direct) TouchUp(e.pointerId, e.position); }

        public void TouchDown(int id, Vector2 screen)
        {
            if (pointer != None) return;
            pointer = id;
            Pressed = true; Dragging = false; OverCancel = false; Offset = Vector2.zero;
            PressedAt = Time.unscaledTime;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out Vector2 local);
            start = local - Rect.rect.center;
        }

        public void TouchMove(int id, Vector2 screen)
        {
            if (id != pointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, null, out Vector2 local);
            Offset = local - Rect.rect.center;
            if (!Dragging && (Offset - start).magnitude > DragStart) Dragging = true;
            OverCancel = Dragging && (Offset - new Vector2(0f, CancelUp)).magnitude < CancelRadius;
        }

        public void TouchUp(int id, Vector2 screen)
        {
            if (id != pointer) return;
            TouchMove(id, screen); // the last spot counts (a finger can lift between two moves)
            bool drag = Dragging, cancel = OverCancel;
            Vector2 off = Offset;
            Cancel();
            if (!drag) Tapped?.Invoke();
            else if (!cancel) Aimed?.Invoke(off);
        }

        public void Cancel()
        {
            pointer = None;
            Pressed = false; Dragging = false; OverCancel = false; Offset = Vector2.zero;
        }
    }
}
