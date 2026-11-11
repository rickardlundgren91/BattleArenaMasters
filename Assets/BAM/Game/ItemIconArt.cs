using System;

namespace BAM.Game
{
    /// <summary>
    /// The item icons, drawn in code (Rickard 2026-11-11: "vi behöver ha egna ikoner, gör dom i kod"): a round badge in the item's
    /// rarity colour with a bright rim, and a cream glyph with a dark edge and a soft shadow on it. Each glyph is a few distance
    /// shapes (circles, boxes, capsules, polygons), so it stays sharp at any size and reads at 40 px on a phone. Plain C# with no
    /// Unity types (RGBA bytes out), so the same code can be run headless for a preview; HudItems turns the bytes into a sprite.
    /// </summary>
    static class ItemIconArt
    {
        // the rarity colours (common, rare, epic, legendary): the old game's RARITY_BG, a little deeper so the cream glyph stands out
        static readonly float[][] Rarity =
        {
            new[] { 0.36f, 0.56f, 0.72f },
            new[] { 0.10f, 0.72f, 0.50f },
            new[] { 0.72f, 0.28f, 0.90f },
            new[] { 0.98f, 0.70f, 0.12f },
        };
        static readonly float[] Cream = { 1f, 0.95f, 0.84f }, Ink = { 0.07f, 0.05f, 0.11f }, Blood = { 0.93f, 0.18f, 0.22f };

        /// <summary>size x size RGBA, rows bottom first (Unity's texture order). id = (int)ItemId.</summary>
        public static byte[] Render(int id, int rarity, int size)
        {
            var px = new byte[size * size * 4];
            var rc = Rarity[Math.Max(0, Math.Min(3, rarity))];
            var glyphCol = id == 7 ? Blood : Cream;
            float step = 2f / size; // one pixel in the icon's -1..1 space
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) * step - 1f, y = (j + 0.5f) * step - 1f;
                    float r = 0, g = 0, b = 0, a = 0;
                    // the badge: a dark core in the rarity colour, lighter towards the top, a bright rim and a thin dark edge
                    float dc = Len(x, y) - 0.94f;
                    float cov = Cover(dc, step);
                    if (cov > 0)
                    {
                        float t = 0.55f + 0.25f * (y + 1f) * 0.5f - 0.25f * Len(x, y);
                        Over(ref r, ref g, ref b, ref a, rc[0] * t * 0.75f, rc[1] * t * 0.75f, rc[2] * t * 0.75f, cov);
                        float rim = Cover(Math.Abs(dc + 0.06f) - 0.05f, step);
                        Over(ref r, ref g, ref b, ref a, Mix(rc[0], 1f, 0.25f), Mix(rc[1], 1f, 0.25f), Mix(rc[2], 1f, 0.25f), rim * cov);
                        float edge = Cover(Math.Abs(dc + 0.125f) - 0.012f, step);
                        Over(ref r, ref g, ref b, ref a, 0.05f, 0.04f, 0.1f, 0.6f * edge * cov);
                        // a soft shine on the upper half
                        float shine = Cover(Len(x, y - 0.55f) - 0.62f, step * 6f) * Cover(Len(x, y) - 0.8f, step) * 0.12f;
                        Over(ref r, ref g, ref b, ref a, 1f, 1f, 1f, shine);
                    }
                    // the glyph: shadow, dark edge, fill
                    float gs = Glyph(id, x - 0.03f, y + 0.05f);
                    Over(ref r, ref g, ref b, ref a, 0f, 0f, 0f, 0.45f * Cover(gs - 0.07f, step * 3f));
                    float gd = Glyph(id, x, y);
                    Over(ref r, ref g, ref b, ref a, Ink[0], Ink[1], Ink[2], Cover(gd - 0.065f, step));
                    float fc = Cover(gd, step);
                    if (fc > 0)
                    {
                        float lt = 0.82f + 0.3f * (y + 1f) * 0.5f;
                        Over(ref r, ref g, ref b, ref a, Clamp01(glyphCol[0] * lt), Clamp01(glyphCol[1] * lt), Clamp01(glyphCol[2] * lt), fc);
                    }
                    int o = (j * size + i) * 4;
                    // straight (not premultiplied) alpha for the texture
                    px[o] = B(a > 0 ? r / a : 0); px[o + 1] = B(a > 0 ? g / a : 0); px[o + 2] = B(a > 0 ? b / a : 0); px[o + 3] = B(a);
                }
            return px;
        }

        // ---------------------------------------------------------------- the glyphs (distance < 0 = inside; y up)
        static float Glyph(int id, float x, float y)
        {
            switch (id)
            {
                case 1: // Resonance Core: a crystal with two sound arcs
                {
                    float d = Poly(x, y, Crystal);
                    float arcs = Math.Max(Math.Abs(Len(x, y) - 0.6f) - 0.055f, Math.Max(Math.Abs(y) - 0.26f, 0.42f - Math.Abs(x)));
                    return Math.Min(d, arcs);
                }
                case 2: // Quarry Mark: a crosshair
                {
                    float ring = Math.Abs(Len(x, y) - 0.4f) - 0.065f;
                    float ticks = Math.Min(Box(x, y - 0.5f, 0.055f, 0.17f), Box(x, y + 0.5f, 0.055f, 0.17f));
                    ticks = Math.Min(ticks, Math.Min(Box(x - 0.5f, y, 0.17f, 0.055f), Box(x + 0.5f, y, 0.17f, 0.055f)));
                    return Math.Min(Math.Min(ring, ticks), Len(x, y) - 0.11f);
                }
                case 3: // Thicket Fang: three claw slashes
                {
                    float d = 9f;
                    for (int k = -1; k <= 1; k++)
                    {
                        float ox = k * 0.3f;
                        d = Math.Min(d, Taper(x, y, -0.22f + ox, -0.55f + Math.Abs(k) * 0.08f, 0.24f + ox, 0.58f - Math.Abs(k) * 0.08f, 0.02f, 0.11f));
                    }
                    return d;
                }
                case 4: // Guardian's Thread: a shield with a stitched seam
                {
                    float d = Poly(x, y, Shield);
                    for (int k = 0; k < 3; k++) d = Math.Max(d, -Cap(x, y, 0f, 0.3f - k * 0.3f, 0f, 0.14f - k * 0.3f, 0.045f));
                    float inner = Math.Abs(Poly(x * 1.35f, y * 1.35f + 0.05f, Shield)) - 0.03f; // an inner line, cut out
                    return Math.Max(d, -inner);
                }
                case 5: // Rally Beacon: a banner on a pole
                {
                    float pole = Cap(x, y, -0.36f, -0.62f, -0.36f, 0.58f, 0.06f);
                    float flag = Poly(x, y, Flag);
                    float knob = Len(x + 0.36f, y - 0.64f) - 0.1f;
                    return Math.Min(Math.Min(pole, flag), knob);
                }
                case 6: // Steadfast Charm: an amulet on a loop, a gem in it
                {
                    float body = Math.Abs(Len(x, y + 0.12f) - 0.36f) - 0.09f;
                    float loop = Math.Abs(Len(x, y - 0.52f) - 0.12f) - 0.05f;
                    float gem = Poly(x, y + 0.12f, Gem);
                    return Math.Min(Math.Min(body, loop), gem);
                }
                case 7: // Parascythe: a drop of blood
                {
                    float c = Len(x, y + 0.18f) - 0.38f;
                    float tip = Poly(x, y, Drop);
                    return Math.Min(c, tip);
                }
                case 8: // Minion Skewer: a spear down at a goblin's head
                {
                    float shaft = Cap(x, y, -0.62f, 0.62f, -0.06f, 0.06f, 0.06f);
                    float tip = Poly(x, y, SpearTip);
                    float head = Len(x - 0.44f, y + 0.46f) - 0.17f;
                    float ears = Math.Min(Poly(x, y, EarL), Poly(x, y, EarR));
                    return Math.Min(Math.Min(shaft, tip), Math.Min(head, ears));
                }
                case 9: // Crushing Spanner: a war hammer
                {
                    float handle = Cap(x, y, -0.5f, -0.55f, 0.2f, 0.25f, 0.07f);
                    Rot(x - 0.24f, y - 0.3f, 0.785f, out float hx, out float hy);
                    float head = Box(hx, hy, 0.16f, 0.42f) - 0.03f;
                    return Math.Min(handle, head);
                }
                case 10: // Heroes Bane: a sword
                {
                    Rot(x, y, -0.785f, out float sx, out float sy);
                    float blade = Poly(sx, sy, Blade);
                    float guard = Box(sx, sy + 0.32f, 0.26f, 0.055f) - 0.02f;
                    float grip = Cap(sx, sy, 0f, -0.36f, 0f, -0.6f, 0.06f);
                    float pommel = Len(sx, sy + 0.68f) - 0.09f;
                    return Math.Min(Math.Min(blade, guard), Math.Min(grip, pommel));
                }
            }
            return 9f;
        }

        static readonly float[] Crystal = { 0f, 0.62f, 0.3f, 0.12f, 0f, -0.62f, -0.3f, 0.12f };
        static readonly float[] Shield = { -0.46f, 0.5f, 0.46f, 0.5f, 0.46f, 0.02f, 0f, -0.62f, -0.46f, 0.02f };
        static readonly float[] Flag = { -0.34f, 0.56f, 0.52f, 0.4f, 0.3f, 0.24f, 0.52f, 0.08f, -0.34f, 0.0f };
        static readonly float[] Gem = { 0f, 0.17f, 0.13f, 0f, 0f, -0.17f, -0.13f, 0f };
        static readonly float[] Drop = { 0f, 0.66f, 0.33f, -0.06f, -0.33f, -0.06f };
        static readonly float[] SpearTip = { 0.2f, -0.2f, 0.05f, 0.17f, -0.17f, -0.05f };
        static readonly float[] EarL = { 0.2f, -0.36f, 0.34f, -0.36f, 0.3f, -0.46f };
        static readonly float[] EarR = { 0.68f, -0.56f, 0.58f, -0.62f, 0.56f, -0.48f };
        static readonly float[] Blade = { 0f, 0.7f, 0.11f, 0.52f, 0.11f, -0.3f, -0.11f, -0.3f, -0.11f, 0.52f };

        // ---------------------------------------------------------------- distance shapes
        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        static float Box(float x, float y, float hx, float hy)
        {
            float dx = Math.Abs(x) - hx, dy = Math.Abs(y) - hy;
            return Len(Math.Max(dx, 0), Math.Max(dy, 0)) + Math.Min(Math.Max(dx, dy), 0);
        }
        static float Cap(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float h = Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
            return Len(px - vx * h, py - vy * h) - r;
        }
        // a capsule that is r0 thick at a and r1 at b (a claw slash)
        static float Taper(float x, float y, float ax, float ay, float bx, float by, float r0, float r1)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float h = Clamp01((px * vx + py * vy) / (vx * vx + vy * vy));
            float r = r0 + (r1 - r0) * (float)Math.Sin(h * Math.PI) ;
            return Len(px - vx * h, py - vy * h) - r;
        }
        // a polygon from x,y pairs (Inigo Quilez's signed polygon distance)
        static float Poly(float x, float y, float[] v)
        {
            int n = v.Length / 2;
            float d = (x - v[0]) * (x - v[0]) + (y - v[1]) * (y - v[1]); float s = 1;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[2 * j] - v[2 * i], ey = v[2 * j + 1] - v[2 * i + 1], wx = x - v[2 * i], wy = y - v[2 * i + 1];
                float h = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Math.Min(d, bx * bx + by * by);
                bool c1 = y >= v[2 * i + 1], c2 = y < v[2 * j + 1], c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * (float)Math.Sqrt(d);
        }
        static void Rot(float x, float y, float a, out float rx, out float ry)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            rx = c * x + s * y; ry = -s * x + c * y;
        }

        // ---------------------------------------------------------------- painting
        static float Cover(float d, float px) => Clamp01(0.5f - d / px);
        static void Over(ref float r, ref float g, ref float b, ref float a, float sr, float sg, float sb, float sa)
        {
            if (sa <= 0) return;
            r = sr * sa + r * (1 - sa); g = sg * sa + g * (1 - sa); b = sb * sa + b * (1 - sa); a = sa + a * (1 - sa);
        }
        static float Mix(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        static byte B(float v) => (byte)Math.Round(Clamp01(v) * 255f);
    }
}
