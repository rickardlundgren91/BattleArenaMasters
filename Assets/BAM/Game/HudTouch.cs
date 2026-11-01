using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BAM.Game
{
    /// <summary>A match control that follows one finger: down, moves and up in screen pixels, with the finger's own id.</summary>
    interface IHudTouch
    {
        void TouchDown(int id, Vector2 screen);
        void TouchMove(int id, Vector2 screen);
        void TouchUp(int id, Vector2 screen);
    }

    /// <summary>
    /// On a phone the match controls (joystick, SPECIAL, spell, emote heart, ATTACK/DEFEND) read the fingers straight from the
    /// touchscreen instead of through the UI's event system: with a thumb on the joystick and one aiming, the event system's touch
    /// mixed the two fingers up (Rickard 2026-11-01: the special sometimes cast as a tap at once, and sometimes an emote came out
    /// while aiming). A finger belongs to the control it landed on until it lifts. The Editor keeps the event system (mouse).
    /// </summary>
    static class HudTouch
    {
        public static bool Direct => !Application.isEditor && Touchscreen.current != null;
    }

    sealed partial class Hud
    {
        readonly Dictionary<int, IHudTouch> fingers = new Dictionary<int, IHudTouch>(4);
        readonly List<RaycastResult> touchHits = new List<RaycastResult>(8);
        readonly HashSet<int> touchSeen = new HashSet<int>();
        PointerEventData touchProbe;
        EventSystem touchProbeOwner;

        void TouchFrame()
        {
            if (!HudTouch.Direct) return;
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            touchSeen.Clear();
            bool lost = false;
            var all = ETouch.activeTouches;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                int id = t.touchId;
                Vector2 at = t.screenPosition;
                touchSeen.Add(id);
                if (t.phase == TouchPhase.Began)
                {
                    if (fingers.ContainsKey(id)) continue;
                    var target = TouchTarget(at);
                    if (target == null) continue; // not on a match control: the event system has it (score tab, pause, menus)
                    fingers[id] = target;
                    target.TouchDown(id, at);
                    continue;
                }
                if (!fingers.TryGetValue(id, out var owner)) continue;
                if (t.phase == TouchPhase.Ended) { fingers.Remove(id); owner.TouchUp(id, at); }
                else if (t.phase == TouchPhase.Canceled) lost = true; // the system took the touch: nothing may cast from it
                else if (t.phase == TouchPhase.Moved) owner.TouchMove(id, at);
            }
            // a finger the screen dropped without an end (the app paused, a system gesture) counts as lost too
            if (!lost) foreach (var id in fingers.Keys) if (!touchSeen.Contains(id)) { lost = true; break; }
            if (lost) CancelTouches();
        }

        /// <summary>Forget which finger is whose (CancelTouches): a finger still down then belongs to nothing until it lifts.</summary>
        void ForgetFingers() => fingers.Clear();

        // the match control under a new finger: the same top-most hit the event system would pick, and the control that owns it
        IHudTouch TouchTarget(Vector2 at)
        {
            var es = EventSystem.current;
            if (es == null || canvas == null) return null;
            if (touchProbe == null || touchProbeOwner != es) { touchProbe = new PointerEventData(es); touchProbeOwner = es; }
            touchProbe.Reset();
            touchProbe.position = at;
            touchHits.Clear();
            es.RaycastAll(touchProbe, touchHits);
            if (touchHits.Count == 0) return null;
            var go = touchHits[0].gameObject;
            if (go == null || !go.transform.IsChildOf(canvas.transform)) return null;
            return go.GetComponentInParent<IHudTouch>();
        }
    }
}
