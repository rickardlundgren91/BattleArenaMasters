using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The battle spells' looks (old SimSpells.cs fx rings, in each spell's colour from SpellDefs.Color), drawn a little richer
    /// with the shared effect systems: Blink, Recall (channel and arrival), Shield, Haste and the new Sprint. Display only.
    /// </summary>
    static class SpellFx
    {
        internal static Color Of(SpellId s)
        {
            int c = SpellDefs.Color(s);
            return new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1f);
        }

        static Color32 C32(Color c, byte a = 255) => new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), a);

        public static void Cast(SpellId s, Vector3 from, Vector3 at)
        {
            Color c = Of(s);
            Color32 c32 = C32(c);
            Vector3 up = Vector3.up * 0.5f;
            switch (s)
            {
                case SpellId.Blink:
                    // old: a 0.8 ring where the hero left and one where it landed (0.3 s); here also a flash, a few wisps and a
                    // streak of sparks along the jump so the eye follows it
                    Fx.Ring(from, 0.2f, 0.8f, c, 0.3f);
                    Fx.Flash(from + up, 0.2f, 0.7f, new Color(c.r, c.g, c.b, 0.8f), 0.12f);
                    Fx.Wisps(from + up, 3, c32, 0.45f, 0.5f, 0.4f, 0.6f, 0.2f);
                    Fx.Ring(at, 0.2f, 0.8f, c, 0.3f);
                    Fx.Flash(at + up, 0.3f, 0.9f, new Color(1f, 1f, 1f, 0.9f), 0.08f);
                    Fx.Stars(at + up, 3, c32, 0.3f, 1.5f, 0.35f, 0.8f, 0.2f);
                    Vector3 d = at - from; d.y = 0f;
                    if (d.sqrMagnitude > 0.01f)
                    {
                        int n = Mathf.Clamp(Mathf.RoundToInt(d.magnitude * 2f), 2, 8);
                        for (int i = 1; i < n; i++) Fx.Glow(Vector3.Lerp(from, at, i / (float)n) + up, 1, c32, 0.28f, 0.2f, 0.3f, 0.2f);
                        Fx.SparksAlong(at + up, d, 5, c32, 0.1f, 3.5f, 0.25f, 25f);
                    }
                    break;
                case SpellId.Recall:
                    Fx.Ring(at, 0.3f, 1.0f, c, 0.4f);
                    Fx.Glow(at + Vector3.up * 0.1f, 6, c32, 0.2f, 0.8f, 0.5f, 1.2f, 0.4f);
                    break;
                case SpellId.Shield:
                    Fx.Ring(at, 0.2f, 0.8f, c, 0.4f);
                    Fx.Flash(at + up, 0.3f, 1.0f, new Color(c.r, c.g, c.b, 0.85f), 0.12f);
                    Fx.Stars(at + up, 4, c32, 0.3f, 1.2f, 0.45f, 0.8f, 0.3f);
                    break;
                case SpellId.Haste:
                    Fx.Ring(at, 0.2f, 0.7f, c, 0.4f);
                    Fx.Sparks(at + up, 10, c32, 0.1f, 3.5f, 0.35f, 1.4f, 0.2f);
                    Fx.Flames(at + Vector3.up * 0.3f, 3, c32, 0.3f, 0.6f, 0.4f, 1.2f, 0.2f);
                    break;
                case SpellId.Sprint:
                    Fx.Ring(at, 0.2f, 0.8f, c, 0.35f);
                    Fx.Dust(at + Vector3.up * 0.1f, 6, new Color32(200, 190, 150, 200), 0.5f, 1.4f, 0.5f, 0.3f, 0.2f);
                    Fx.Wisps(at + up, 3, c32, 0.4f, 0.8f, 0.35f, 0.4f, 0.2f);
                    break;
            }
        }

        /// <summary>A recall cut short: the ring breaks into a grey puff.</summary>
        public static void Cancel(Vector3 at)
        {
            Fx.Smoke(at + Vector3.up * 0.3f, 5, new Color32(170, 170, 170, 160), 0.35f, 0.8f, 0.5f, 0.4f, 0.3f);
            Fx.Ring(at, 0.9f, 0.4f, new Color(0.7f, 0.7f, 0.7f, 0.7f), 0.25f);
        }

        /// <summary>A recall lands: old rings of 1.2 at both ends, plus a pillar at home.</summary>
        public static void Recall(Vector3 from, Vector3 to)
        {
            Color c = Of(SpellId.Recall);
            Color32 c32 = C32(c);
            Fx.Ring(from, 0.4f, 1.2f, c, 0.4f);
            Fx.Wisps(from + Vector3.up * 0.5f, 4, c32, 0.5f, 0.6f, 0.6f, 1.2f, 0.3f);
            Fx.Ring(to, 0.3f, 1.2f, c, 0.5f);
            Fx.Pillar(to, 1.0f, 2.6f, c, 0.6f);
            Fx.Flash(to + Vector3.up * 0.6f, 0.3f, 1.1f, new Color(1f, 1f, 1f, 0.85f), 0.1f);
            Fx.Glow(to + Vector3.up * 0.2f, 10, c32, 0.22f, 1.4f, 0.6f, 1.6f, 0.3f);
        }
    }
}
