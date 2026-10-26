using BAM.Core;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The old game's vignette (ABC2 View3DWorld.cs): a soft dark tone at the screen's edges that frames the arena, turning red
    /// and pulsing while the player's hero is under 35 % health. On Low graphics only the red low-health pulse is drawn
    /// (Quality.cs). Under the HUD and the overhead marks, over the world.
    /// </summary>
    sealed partial class Hud
    {
        Image vignette;

        void BuildVignette()
        {
            var rt = UiKit.NewRect("Vignette", canvasRect);
            UiKit.Stretch(rt);
            rt.SetAsFirstSibling();
            vignette = rt.gameObject.AddComponent<Image>();
            vignette.sprite = VignetteSprite;
            vignette.raycastTarget = false;
            vignette.enabled = false;
        }

        void VignetteFrame(bool hidden)
        {
            var p = m.Player;
            float low = p != null && p.Alive && p.MaxHp > 0f ? Mathf.Clamp01((0.35f - p.Hp / p.MaxHp) / 0.35f) : 0f;
            bool on = !hidden && !(m.Tut != null && m.Tut.Island) && (BamSettings.Graphics != GraphicsQuality.Low || low > 0f);
            if (vignette.enabled != on) vignette.enabled = on;
            if (!on) return;
            var c = Color.Lerp(Color.black, new Color(0.75f, 0.05f, 0.05f), low);
            c.a = 0.35f + low * (0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f));
            vignette.color = c;
        }

        static Sprite vignetteSprite;
        static Sprite VignetteSprite
        {
            get
            {
                if (vignetteSprite != null) return vignetteSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[n * n];
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float dx = (i + 0.5f) / 32f - 1f, dy = (j + 0.5f) / 32f - 1f, r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                        px[j * n + i] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.55f) / 0.75f)));
                    }
                tex.SetPixels(px);
                tex.Apply(false, true);
                vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return vignetteSprite;
            }
        }
    }
}
