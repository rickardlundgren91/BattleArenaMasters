using System;
using BAM.Audio;
using BAM.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BAM.UI
{
    /// <summary>
    /// Press feedback (the old MenuButtonFx.cs): the button shrinks a little while a finger is on it, and on release a soft
    /// white flash grows round it and fades in 0.28 s. PLAY has its own spring and shine instead (HomeScreen.cs).
    /// </summary>
    sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        static readonly Vector3 Pressed = new Vector3(0.94f, 0.94f, 1f);
        const float FlashTime = 0.28f, Grow = 15f; // cw(1.4) at the design width
        public bool PressScale = true;
        Selectable selectable;
        bool down;
        Image flash, flashEdge;
        float flashAt = -1;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerDown(PointerEventData e)
        {
            down = selectable == null || selectable.IsInteractable();
            if (down && PressScale) transform.localScale = Pressed;
        }

        public void OnPointerUp(PointerEventData e)
        {
            transform.localScale = Vector3.one;
            if (down) Flash();
            down = false;
        }

        public void OnPointerExit(PointerEventData e) { transform.localScale = Vector3.one; down = false; }

        void Flash()
        {
            if (flash == null)
            {
                flash = UiKit.Fill(transform, Color.clear, UiKit.Rounded);
                flash.name = "Flash";
                flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                flashEdge = UiKit.Fill(flash.transform, Color.clear, UiKit.Ring);
            }
            flash.transform.SetAsLastSibling();
            flash.gameObject.SetActive(true);
            flashAt = Time.unscaledTime;
            Update();
        }

        void Update()
        {
            if (flashAt < 0) return;
            float k = (Time.unscaledTime - flashAt) / FlashTime;
            if (k >= 1) { flashAt = -1; flash.gameObject.SetActive(false); return; }
            float g = Grow * k;
            var r = flash.rectTransform;
            r.offsetMin = new Vector2(-g, -g);
            r.offsetMax = new Vector2(g, g);
            float radius = Mathf.Min(((RectTransform)transform).rect.height * 0.3f, 43f) + g;
            flash.pixelsPerUnitMultiplier = flashEdge.pixelsPerUnitMultiplier = 28f / Mathf.Max(1f, radius);
            flash.color = new Color(1, 1, 1, 0.22f * (1 - k));
            flashEdge.color = new Color(1, 1, 1, 0.54f * (1 - k));
        }

        void OnDisable()
        {
            transform.localScale = Vector3.one;
            down = false;
            flashAt = -1;
            if (flash != null) flash.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Keeps its RectTransform on the standing frame (UiKit.PortraitFrame): inside Screen.safeArea, and on Android also clear
    /// of the navigation bar when the game is drawn behind it (edge-to-edge; Unity's safe area does not report that bar on
    /// every phone). On a phone the frame is the whole safe area; a wider window gets the phone-shaped middle. Re-checks when
    /// the screen changes and a few times after it is shown, since Android reports the bars a moment after start.
    /// </summary>
    sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect applied;
        Vector2Int size;
        float bottomExtra, nextCheck;
        int checksLeft;

        void OnEnable()
        {
            checksLeft = 5;
            nextCheck = 0;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != size.x || Screen.height != size.y) { checksLeft = 5; Apply(); }
            else if (checksLeft > 0 && Time.unscaledTime >= nextCheck) Apply();
        }

        void Apply()
        {
            if (checksLeft > 0) { checksLeft--; nextCheck = Time.unscaledTime + 0.5f; }
            var area = Screen.safeArea;
            applied = area;
            size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            bottomExtra = NavBarOverlap();
            UiKit.NavBarInset = bottomExtra;
            var f = UiKit.PortraitFrame();
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(f.xMin / Screen.width, f.yMin / Screen.height);
            rt.anchorMax = new Vector2(f.xMax / Screen.width, f.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // How many of Unity's pixels at the bottom are covered by the navigation bar (0 when the game ends above it).
        static float NavBarOverlap()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = activity.Call<AndroidJavaObject>("getWindow");
                using var decor = window.Call<AndroidJavaObject>("getDecorView");
                using var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets");
                if (insets == null) return 0;
                using var content = decor.Call<AndroidJavaObject>("findViewById", 0x01020002); // android.R.id.content
                if (content == null) return 0;
                using var rect = new AndroidJavaObject("android.graphics.Rect");
                if (!content.Call<bool>("getGlobalVisibleRect", rect)) return 0;
                int top = rect.Get<int>("top"), bottom = rect.Get<int>("bottom");
                int screenBottom = decor.Call<int>("getHeight");
                int nav = insets.Call<int>("getSystemWindowInsetBottom");
                int overlap = bottom - (screenBottom - nav);
                if (overlap <= 0 || bottom <= top) return 0;
                return overlap * (float)Screen.height / (bottom - top);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BAM.UI] could not read the navigation bar size: " + e.Message);
                return 0;
            }
#else
            return 0;
#endif
        }
    }

    /// <summary>
    /// Scales the canvas for the standing frame (UiKit.PortraitFrame). When the frame is as wide as the screen (a phone) the
    /// canvas keeps the 1080x1920 reference with a 0.5 width/height match, by height only when wider than 9:16, so it stays at
    /// least 1920 units tall. A wider window (a tablet, the editor's landscape Game view) is scaled as a phone of the frame's
    /// size, so the layouts inside the frame look as they do on a phone.
    /// </summary>
    sealed class AdaptiveScaler : MonoBehaviour
    {
        CanvasScaler scaler;
        int width, height;
        Rect frame;

        void Awake() => scaler = GetComponent<CanvasScaler>();
        void OnEnable() => Apply(UiKit.PortraitFrame());
        void Update()
        {
            var f = UiKit.PortraitFrame();
            if (Screen.width != width || Screen.height != height || f != frame) Apply(f);
        }

        void Apply(Rect f)
        {
            width = Screen.width;
            height = Screen.height;
            frame = f;
            if (scaler == null || height <= 0) return;
            if (f.width >= 1f && f.width < width - 1f)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Sqrt(f.width / 1080f * (f.height / 1920f));
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.matchWidthOrHeight = width * 1920f > height * 1080f ? 1f : 0.5f;
            }
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

    /// <summary>An on/off switch like the old game's: green track when on, dark with a lavender knob when off.</summary>
    sealed class SwitchView
    {
        static readonly Color OnColor = new Color(0.13f, 0.72f, 0.37f);
        static readonly Color OffColor = new Color(0.1f, 0.11f, 0.25f);
        static readonly Color KnobOff = new Color(0.48f, 0.5f, 0.82f);
        static readonly Color KnobOn = new Color(0.94f, 0.95f, 0.98f);
        readonly Image track, knobImg;
        readonly RectTransform knob;
        readonly Action<bool> changed;
        public bool On { get; private set; }

        public SwitchView(Transform parent, Action<bool> changed)
        {
            this.changed = changed;
            var rt = UiKit.NewRect("Switch", parent);
            rt.sizeDelta = new Vector2(136, 72);
            track = rt.gameObject.AddComponent<Image>();
            track.sprite = UiKit.Rounded;
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 0.75f; // fully round ends
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                Set(!On);
                BamAudio.Play(SfxId.UiToggle);
                this.changed?.Invoke(On);
            });
            knobImg = UiKit.Fill(rt, KnobOn, UiKit.Circle);
            knob = knobImg.rectTransform;
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(56, 56);
        }

        public RectTransform Rect => (RectTransform)track.transform;

        public void Set(bool on)
        {
            On = on;
            track.color = on ? OnColor : OffColor;
            knobImg.color = on ? KnobOn : KnobOff;
            knob.anchoredPosition = new Vector2(on ? 32 : -32, 0);
        }
    }

    /// <summary>
    /// Choices side by side (High/Low, Easy/Normal/Hard), the old game's way: dark cells, the chosen one with a mint edge and
    /// white text, the others with lavender text.
    /// </summary>
    sealed class Segmented
    {
        static readonly Color CellFill = new Color(0.06f, 0.07f, 0.16f);
        static readonly Color ChosenFill = new Color(0.03f, 0.1f, 0.11f);
        static readonly Color Lavender = new Color(0.4f, 0.42f, 0.8f);
        readonly Image[] cells, edges;
        readonly Text[] labels;
        readonly Action<int> chosen;
        public int Selected { get; private set; } = -1;

        public Segmented(RectTransform parent, string[] names, Action<int> chosen)
        {
            this.chosen = chosen;
            cells = new Image[names.Length];
            edges = new Image[names.Length];
            labels = new Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var rt = UiKit.NewRect(names[i], parent);
                rt.anchorMin = new Vector2((float)i / names.Length, 0);
                rt.anchorMax = new Vector2((float)(i + 1) / names.Length, 1);
                rt.offsetMin = new Vector2(5, 0);
                rt.offsetMax = new Vector2(-5, 0);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = UiKit.Rounded;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1.4f;
                var b = rt.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiToggle); Choose(index); });
                rt.gameObject.AddComponent<ButtonFx>();
                var edge = UiKit.Fill(rt, UiKit.Primary, UiKit.Ring);
                edge.pixelsPerUnitMultiplier = 1.4f;
                var label = UiKit.Label(rt, names[i], 40, TextAnchor.MiddleCenter, Lavender);
                UiKit.Stretch(label.rectTransform);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                cells[i] = img;
                edges[i] = edge;
                labels[i] = label;
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
            for (int i = 0; i < cells.Length; i++)
            {
                bool on = i == index;
                cells[i].color = on ? ChosenFill : CellFill;
                edges[i].enabled = on;
                labels[i].color = on ? UiKit.TextColor : Lavender;
            }
        }
    }

    static class SliderFactory
    {
        /// <summary>A horizontal 0..1 slider with a fat handle that is easy to grab with a thumb.</summary>
        public static Slider Create(Transform parent, Action<float> released)
        {
            var rt = UiKit.NewRect("Slider", parent);
            rt.sizeDelta = new Vector2(420, 100);
            var bg = UiKit.Fill(rt, new Color(0.1f, 0.11f, 0.25f), UiKit.Rounded);
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 28);
            bg.pixelsPerUnitMultiplier = 2f;

            var fillArea = UiKit.NewRect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-50, 28);
            var fill = UiKit.Fill(fillArea, new Color(0.13f, 0.72f, 0.37f), UiKit.Rounded);
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
