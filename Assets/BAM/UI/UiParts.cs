using System;
using BAM.Audio;
using BAM.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BAM.UI
{
    /// <summary>Press feedback: the button shrinks a little while a finger is on it.</summary>
    sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        static readonly Vector3 Pressed = new Vector3(0.94f, 0.94f, 1f);
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();
        public void OnPointerDown(PointerEventData e) { if (selectable == null || selectable.IsInteractable()) transform.localScale = Pressed; }
        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
        public void OnPointerExit(PointerEventData e) => transform.localScale = Vector3.one;
        void OnDisable() => transform.localScale = Vector3.one;
    }

    /// <summary>Keeps its RectTransform inside Screen.safeArea; re-checks only when the screen changes (rotation, split view).</summary>
    sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int size;

        void OnEnable() => Apply();
        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != size.x || Screen.height != size.y) Apply();
        }

        void Apply()
        {
            var area = Screen.safeArea;
            applied = area;
            size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Calls back when the finger leaves a slider, so a setting is saved once per drag instead of every frame.</summary>
    sealed class SliderRelease : MonoBehaviour, IPointerUpHandler
    {
        public Action Released;
        public void OnPointerUp(PointerEventData e) => Released?.Invoke();
    }

    /// <summary>Lives on the menu canvas: the Android back button outside a match and the hint bubble's bob.</summary>
    sealed class UiRoot : MonoBehaviour
    {
        void Update()
        {
            // Android's back button arrives as Escape in the Input System. During a match the game layer owns it
            // and calls Screens.HandleBack itself.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && (Flow.Game == null || !Flow.Game.InMatch))
                Screens.HandleBack();
            Screens.Tick();
        }
    }

    /// <summary>An on/off switch: a rounded track with a sliding knob.</summary>
    sealed class SwitchView
    {
        readonly Image track;
        readonly RectTransform knob;
        readonly Text label;
        readonly Action<bool> changed;
        public bool On { get; private set; }

        public SwitchView(Transform parent, Action<bool> changed)
        {
            this.changed = changed;
            var rt = UiKit.NewRect("Switch", parent);
            rt.sizeDelta = new Vector2(220, 100);
            track = rt.gameObject.AddComponent<Image>();
            track.sprite = UiKit.Rounded;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 0.6f; // fully round ends
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                Set(!On);
                BamAudio.Play(SfxId.UiToggle);
                this.changed?.Invoke(On);
            });
            label = UiKit.Label(rt, "", 36, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Stretch(label.rectTransform);
            var k = UiKit.Fill(rt, Color.white, UiKit.Circle);
            knob = k.rectTransform;
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(80, 80);
            UiKit.AddShadow(k, 3);
        }

        public RectTransform Rect => (RectTransform)track.transform;

        public void Set(bool on)
        {
            On = on;
            track.color = on ? UiKit.Good : UiKit.Muted;
            knob.anchoredPosition = new Vector2(on ? 58 : -58, 0);
            label.text = on ? "ON" : "OFF";
            label.rectTransform.offsetMin = new Vector2(on ? 0 : 90, 0);
            label.rectTransform.offsetMax = new Vector2(on ? -90 : 0, 0);
        }
    }

    /// <summary>A row of buttons where exactly one is chosen (Easy/Normal/Hard, Low/High).</summary>
    sealed class Segmented
    {
        readonly Button[] buttons;
        readonly Action<int> chosen;
        public int Selected { get; private set; } = -1;

        public Segmented(RectTransform parent, string[] names, Action<int> chosen)
        {
            this.chosen = chosen;
            buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var b = UiKit.Button(parent, names[i], () => Choose(index), UiKit.Muted, SfxId.UiToggle, 48);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2((float)i / names.Length, 0);
                rt.anchorMax = new Vector2((float)(i + 1) / names.Length, 1);
                rt.offsetMin = new Vector2(6, 0);
                rt.offsetMax = new Vector2(-6, 0);
                buttons[i] = b;
            }
        }

        void Choose(int i)
        {
            Set(i);
            chosen?.Invoke(i);
        }

        public void Set(int index)
        {
            Selected = index;
            for (int i = 0; i < buttons.Length; i++) UiKit.Tint(buttons[i], i == index ? UiKit.Primary : UiKit.Muted);
        }
    }

    static class SliderFactory
    {
        /// <summary>A horizontal 0..1 slider with a fat handle that is easy to grab with a thumb.</summary>
        public static Slider Create(Transform parent, Action<float> released)
        {
            var rt = UiKit.NewRect("Slider", parent);
            rt.sizeDelta = new Vector2(420, 100);
            var bg = UiKit.Fill(rt, UiKit.Muted, UiKit.Rounded);
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 28);
            bg.pixelsPerUnitMultiplier = 2f;

            var fillArea = UiKit.NewRect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-50, 28);
            var fill = UiKit.Fill(fillArea, UiKit.Gold, UiKit.Rounded);
            fill.pixelsPerUnitMultiplier = 2f;
            fill.rectTransform.sizeDelta = new Vector2(50, 0);

            var handleArea = UiKit.NewRect("Handle Slide Area", rt);
            UiKit.Stretch(handleArea, 40, 0, 40, 0);
            var handle = UiKit.Fill(handleArea, Color.white, UiKit.Circle);
            handle.rectTransform.sizeDelta = new Vector2(80, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0);
            handle.rectTransform.anchorMax = new Vector2(0, 1);
            handle.rectTransform.offsetMin = new Vector2(-40, 10);
            handle.rectTransform.offsetMax = new Vector2(40, -10);
            handle.preserveAspect = true;
            UiKit.AddShadow(handle, 3);

            // The whole row reacts to touches, not just the thin bar.
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var release = rt.gameObject.AddComponent<SliderRelease>();
            release.Released = () => released?.Invoke(slider.value);
            return slider;
        }
    }
}
