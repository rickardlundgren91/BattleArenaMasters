using System;
using BAM.Audio;
using BAM.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BAM.UI
{
    /// <summary>
    /// The shared look, also used by the in-match HUD (BAM.Game). Everything is built in code: rounded sprites are generated
    /// once, text uses Lilita One. Sizes are in canvas units of the 1080x1920 reference (the real width is about 970-1080).
    /// </summary>
    public static class UiKit
    {
        static Font font;
        static Sprite rounded, circle, gradient;
        static bool eventSystemReady;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("BAM/Fonts/LilitaOne");
                if (font == null)
                {
                    Debug.LogError("[BAM.UI] Lilita One missing at Resources/BAM/Fonts/LilitaOne; using Unity's built-in font");
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        // Palette: a bright, saturated mobile look on a deep blue-purple background.
        public static readonly Color Ally = new Color(0.25f, 0.55f, 1f);
        public static readonly Color Enemy = new Color(0.95f, 0.25f, 0.2f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.2f);
        public static readonly Color Primary = new Color(0.2f, 0.56f, 1f);
        public static readonly Color Good = new Color(0.25f, 0.78f, 0.35f);
        public static readonly Color Bad = new Color(0.92f, 0.3f, 0.27f);
        public static readonly Color Muted = new Color(0.24f, 0.29f, 0.52f);
        public static readonly Color PanelColor = new Color(0.08f, 0.11f, 0.27f, 0.92f);
        public static readonly Color RowColor = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color TextDim = new Color(0.78f, 0.83f, 1f);
        public static readonly Color Ink = new Color(0.06f, 0.07f, 0.16f);
        public static readonly Color Dim = new Color(0.02f, 0.03f, 0.1f, 0.7f);

        /// <summary>9-sliced rounded rectangle (white, tint with Image.color).</summary>
        public static Sprite Rounded { get { if (rounded == null) rounded = MakeRounded(64, 28); return rounded; } }
        /// <summary>A white circle.</summary>
        public static Sprite Circle { get { if (circle == null) circle = MakeRounded(128, 64); return circle; } }

        static Sprite Gradient
        {
            get
            {
                if (gradient != null) return gradient;
                var tex = new Texture2D(1, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAM.UI.Gradient" };
                var bottom = new Color(0.29f, 0.12f, 0.46f);
                var top = new Color(0.1f, 0.2f, 0.55f);
                for (int y = 0; y < 128; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 127f));
                tex.Apply(false, true);
                gradient = Sprite.Create(tex, new Rect(0, 0, 1, 128), new Vector2(0.5f, 0.5f), 100f);
                return gradient;
            }
        }

        /// <summary>A screen-space canvas scaled for portrait phones (reference 1080x1920, match width/height 0.5).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>A full-screen child that keeps clear of notches and rounded corners (Screen.safeArea).</summary>
        public static RectTransform SafeArea(Canvas canvas) => SafeArea((RectTransform)canvas.transform);

        public static RectTransform SafeArea(RectTransform parent)
        {
            var rt = NewRect("SafeArea", parent);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = NewRect("Label", parent);
            rt.sizeDelta = new Vector2(900, size * 1.5f);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.05f;
            return t;
        }

        /// <summary>A chunky button (base + raised face) that shrinks while pressed and plays UiTap. Default 600x150, centred.</summary>
        public static Button Button(Transform parent, string text, Action onClick) => Button(parent, text, onClick, Primary, SfxId.UiTap);

        public static Button Button(Transform parent, string text, Action onClick, Color color, SfxId sfx = SfxId.UiTap, int fontSize = 60)
        {
            var rt = NewRect(text, parent);
            rt.sizeDelta = new Vector2(600, 150);
            var baseImg = rt.gameObject.AddComponent<Image>();
            baseImg.sprite = Rounded;
            baseImg.type = Image.Type.Sliced;

            var faceRt = NewRect("Face", rt);
            Stretch(faceRt, 0, 0, 0, 10);
            var face = faceRt.gameObject.AddComponent<Image>();
            face.sprite = Rounded;
            face.type = Image.Type.Sliced;
            face.raycastTarget = false;

            var label = Label(faceRt, text, fontSize, TextAnchor.MiddleCenter, TextColor);
            Stretch(label.rectTransform, 12, 4, 12, 4);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(label, 3);

            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            // Touch screens have no hover, and a "selected" look would stick after a tap.
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            Tint(b, color);
            rt.gameObject.AddComponent<ButtonFx>();
            b.onClick.AddListener(() =>
            {
                if (sfx != SfxId.None) BamAudio.Play(sfx);
                onClick?.Invoke();
            });
            return b;
        }

        /// <summary>Recolours a button made by <see cref="Button(Transform,string,Action)"/>.</summary>
        public static void Tint(Button b, Color color)
        {
            if (b.targetGraphic != null) b.targetGraphic.color = color;
            var baseImg = b.GetComponent<Image>();
            if (baseImg != null) baseImg.color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a);
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>(true);

        /// <summary>A rounded panel stretched over its parent. It catches touches, so nothing behind it reacts.</summary>
        public static Image Panel(Transform parent, Color color)
        {
            var rt = NewRect("Panel", parent);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>A plain image (no sprite = solid rectangle), stretched over its parent, not catching touches.</summary>
        public static Image Fill(Transform parent, Color color, Sprite sprite = null)
        {
            var rt = NewRect("Fill", parent);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == Rounded) img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary>The menus' blue-purple background.</summary>
        public static Image Background(Transform parent)
        {
            var img = Fill(parent, Color.white, Gradient);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>A round hero badge (code-drawn for now): hero colour, white ring, initial. Default 160x160, centred.</summary>
        public static RectTransform HeroBadge(Transform parent, HeroId id)
        {
            var rt = NewRect("Badge " + id, parent);
            rt.sizeDelta = new Vector2(160, 160);
            var ring = rt.gameObject.AddComponent<Image>();
            ring.sprite = Circle;
            ring.color = Color.white;
            ring.raycastTarget = false;
            var inner = Fill(rt, HeroColor(id), Circle);
            Stretch(inner.rectTransform, 7, 7, 7, 7);
            inner.preserveAspect = true;
            var shine = Fill(inner.rectTransform, new Color(1, 1, 1, 0.18f), Circle);
            shine.rectTransform.anchorMin = new Vector2(0.18f, 0.5f);
            shine.rectTransform.anchorMax = new Vector2(0.82f, 0.92f);
            var info = HeroCatalog.Get(id);
            var letter = Label(inner.rectTransform, info.Name.Substring(0, 1), 80, TextAnchor.MiddleCenter, Color.white);
            Stretch(letter.rectTransform);
            letter.resizeTextForBestFit = true;
            letter.resizeTextMinSize = 10;
            letter.resizeTextMaxSize = 200;
            AddOutline(letter, 3);
            return rt;
        }

        public static Color HeroColor(HeroId id)
        {
            switch (id)
            {
                case HeroId.Bram: return new Color(0.85f, 0.45f, 0.2f);
                case HeroId.Fenn: return new Color(0.15f, 0.7f, 0.65f);
                case HeroId.Tyra: return new Color(0.35f, 0.7f, 0.25f);
                case HeroId.Moa: return new Color(0.95f, 0.7f, 0.1f);
                case HeroId.Knut: return new Color(0.2f, 0.35f, 0.75f);
                case HeroId.Tikta: return new Color(0.6f, 0.3f, 0.85f);
                case HeroId.Blast: return new Color(0.9f, 0.25f, 0.2f);
                default: return Primary;
            }
        }

        // ---- layout helpers ----

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fills the parent, inset by the given margins.</summary>
        public static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Pins to one point of the parent (0..1 each axis); pos is measured from there, pivot sits on the same point.</summary>
        public static RectTransform Place(Component c, float ax, float ay, float x, float y, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A full-width band at a fixed distance from the top (y &gt;= 0 = down from the top) or bottom (y &lt; 0) edge.</summary>
        public static RectTransform Band(Component c, bool fromTop, float distance, float height, float sideMargin)
        {
            var rt = (RectTransform)c.transform;
            float a = fromTop ? 1f : 0f;
            rt.anchorMin = new Vector2(0, a);
            rt.anchorMax = new Vector2(1, a);
            rt.pivot = new Vector2(0.5f, a);
            rt.anchoredPosition = new Vector2(0, fromTop ? -distance : distance);
            rt.sizeDelta = new Vector2(-2 * sideMargin, height);
            return rt;
        }

        public static void AddShadow(Graphic g, float distance)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.45f);
            s.effectDistance = new Vector2(0, -distance);
        }

        public static void AddOutline(Graphic g, float distance)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.05f, 0.15f, 0.85f);
            o.effectDistance = new Vector2(distance, -distance);
        }

        static void EnsureEventSystem()
        {
            if (eventSystemReady && EventSystem.current != null) return;
            eventSystemReady = true;
            if (EventSystem.current != null) return;
            // The project has the Input System only: the old StandaloneInputModule would throw every frame.
            var go = new GameObject("BAM.EventSystem");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        // Anti-aliased rounded square with corners of `radius` px, 9-sliced so it stretches to any size.
        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAM.UI.Rounded" + size
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float dx = Mathf.Max(0f, Mathf.Max(radius - cx, cx - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - cy, cy - (size - radius)));
                float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }
    }
}
