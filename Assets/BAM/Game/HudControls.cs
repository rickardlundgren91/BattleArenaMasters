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
    sealed class JoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const float Radius = 125f;
        const float DeadZone = 0.12f;
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
            Rect r = Area.rect;
            float x = Mathf.Min(r.width * 0.5f - 60f, 260f);
            return new Vector2(rightSide ? r.xMax - x : r.xMin + x, r.yMin + 330f);
        }

        public bool RightSide;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != None) return;
            pointer = e.pointerId;
            origin = Local(e.position);
            Base.anchoredPosition = origin;
            Knob.anchoredPosition = origin;
            Value = Vector2.zero;
            Look.alpha = 1f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            Vector2 d = Local(e.position) - origin;
            float len = d.magnitude;
            if (len > Radius) d *= Radius / len;
            Knob.anchoredPosition = origin + d;
            Vector2 v = d / Radius;
            Value = v.sqrMagnitude < DeadZone * DeadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
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
            Look.alpha = 0.45f;
        }

        Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, screen, null, out Vector2 v);
            return v;
        }
    }

    /// <summary>
    /// The special button: a tap fires it at the automatic target; a drag aims it and fires on release; dragging back onto
    /// the button cancels. Offset is the finger relative to the button's centre in canvas units.
    /// </summary>
    sealed class SpecialButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const float DragStart = 55f, MaxDrag = 300f, CancelRadius = 125f;
        const int None = int.MinValue;

        public RectTransform Rect;
        public bool Pressed, Dragging, OverCancel;
        public Vector2 Offset;
        /// <summary>A tap, or an aimed release with the final offset.</summary>
        public Action Tapped;
        public Action<Vector2> Aimed;
        int pointer = None;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != None) return;
            pointer = e.pointerId;
            Pressed = true; Dragging = false; OverCancel = false; Offset = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, e.position, null, out Vector2 local);
            Offset = local - Rect.rect.center;
            float len = Offset.magnitude;
            if (!Dragging && len > DragStart) Dragging = true;
            OverCancel = Dragging && len < CancelRadius;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
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
