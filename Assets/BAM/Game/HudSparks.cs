using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// Small lightning sparks on the SPECIAL's rim while it is ready (Rickard 2026-11-03 "en animation på special när den är
    /// redo? blixtrar lite exempelvis", then on the card "Blixtar bara runt knappen": a golden crackle round the button, his
    /// Midjourney picture 1 without its big ring over the arena). Several short sparks at a time, all round the rim, never
    /// over the arena (he found the glow too much on 2026-11-02). Drawn as thin zigzag lines; a lightning picture from Rickard
    /// could replace them later.
    /// </summary>
    sealed partial class Hud
    {
        const int SparkCount = 6, SparkSegs = 4;
        const float SparkLife = 0.16f, SparkGapMin = 0.08f, SparkGapMax = 0.22f;
        static readonly Color SparkColor = new Color(1f, 0.93f, 0.62f, 1f);
        readonly Image[,] sparkSeg = new Image[SparkCount, SparkSegs];
        readonly float[] sparkAt = new float[SparkCount];
        float nextSpark;

        // after the rim, so the sparks lie over it
        void BuildSparks()
        {
            for (int s = 0; s < SparkCount; s++)
            {
                sparkAt[s] = -99f;
                for (int i = 0; i < SparkSegs; i++)
                {
                    var rt = UiKit.NewRect("Spark", specialRoot);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    var img = rt.gameObject.AddComponent<Image>();
                    img.raycastTarget = false;
                    img.enabled = false;
                    sparkSeg[s, i] = img;
                }
            }
        }

        // every frame, from ReadyLook
        void Sparks(bool ready)
        {
            float now = Time.unscaledTime;
            if (ready && now >= nextSpark)
            {
                nextSpark = now + Random.Range(SparkGapMin, SparkGapMax);
                int s = 0;
                for (int k = 1; k < SparkCount; k++) if (sparkAt[k] < sparkAt[s]) s = k;
                Spark(s);
                sparkAt[s] = now;
            }
            for (int s = 0; s < SparkCount; s++)
            {
                float t = (now - sparkAt[s]) / SparkLife;
                bool on = ready && t >= 0f && t < 1f;
                // a quick flicker as it dies: full, dim, full, gone
                float a = on ? (t < 0.35f ? 1f : t < 0.55f ? 0.35f : 0.9f * (1f - t) / 0.45f) : 0f;
                for (int i = 0; i < SparkSegs; i++)
                {
                    var img = sparkSeg[s, i];
                    if (img.enabled != on) img.enabled = on;
                    if (on) img.color = new Color(SparkColor.r, SparkColor.g, SparkColor.b, a);
                }
            }
        }

        // one zigzag from the rim outwards, at a random place round the button
        void Spark(int s)
        {
            float R = SpecialButton.Radius;
            float ang = Random.Range(0f, 2f * Mathf.PI);
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            var side = new Vector2(-dir.y, dir.x);
            float r0 = 0.92f * R, r1 = Random.Range(1.12f, 1.28f) * R;
            Vector2 a = dir * r0;
            for (int i = 0; i < SparkSegs; i++)
            {
                float f = (i + 1f) / SparkSegs;
                float jag = i == SparkSegs - 1 ? 0f : (i % 2 == 0 ? 1f : -1f) * Random.Range(0.06f, 0.12f) * R;
                // the zigzag also leans along the rim, so it reads as a crackle on the edge and not a ray
                Vector2 b = dir * Mathf.Lerp(r0, r1, f) + side * (jag + 0.08f * R * f);
                var rt = sparkSeg[s, i].rectTransform;
                Vector2 d = b - a;
                rt.anchoredPosition = a;
                rt.sizeDelta = new Vector2(d.magnitude + 1f, Mathf.Max(2f, R * 0.035f * (1f - 0.5f * f)));
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                a = b;
            }
        }
    }
}
