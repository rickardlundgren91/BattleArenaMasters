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
                    Fx.Pic("tx_wow_mage_blink", from + up, c, 1.6f, 0.25f);
                    Fx.Pic("tx_wow_mage_blink", at + up, Color.white, 1.8f, 0.3f); // Rickard's WoW-mode blink star at both ends
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
                    // the beam switches on: a quick white-cored pillar and a swirl (the channel keeps it going, BodyView.SpellLooks)
                    Fx.Pillar(at, 1.0f, 5.5f, c, 0.3f);
                    Fx.Swirl(at, 1.3f, c, 0.6f);
                    Fx.Pic("tx_wow_magic_circle", at, c, 2.6f, 0.8f);
                    Fx.Ring(at, 0.3f, 1.2f, c, 0.4f);
                    Fx.Glow(at + Vector3.up * 0.1f, 6, c32, 0.2f, 0.8f, 0.5f, 1.2f, 0.4f);
                    break;
                case SpellId.Shield:
                    Fx.Pic("tx_wow_priest_shield", at + up, c, 1.9f, 0.45f);
                    Fx.Ring(at, 0.2f, 0.8f, c, 0.4f);
                    Fx.Flash(at + up, 0.3f, 1.0f, new Color(c.r, c.g, c.b, 0.85f), 0.12f);
                    Fx.Stars(at + up, 4, c32, 0.3f, 1.2f, 0.45f, 0.8f, 0.3f);
                    break;
                case SpellId.FirstAid:
                    // the caster starts healing (no kit on the ground since 2026-11-19): a soft red-white heal bloom
                    Fx.Ring(at, 0.2f, 0.9f, c, 0.35f);
                    Fx.Flash(at + up, 0.25f, 0.9f, new Color(1f, 1f, 1f, 0.8f), 0.1f);
                    Fx.Glow(at + Vector3.up * 0.2f, 8, c32, 0.18f, 0.9f, 0.5f, 1.4f, 0.3f);
                    break;
                case SpellId.Immune:
                    // a white-gold flash and a bright ring: nothing gets through for 4 s (BodyView keeps a glow on while it lasts)
                    Fx.Flash(at + up, 0.35f, 1.3f, new Color(1f, 0.97f, 0.85f, 0.95f), 0.14f);
                    Fx.Ring(at, 0.3f, 1.2f, c, 0.45f);
                    Fx.Pillar(at, 0.9f, 3.5f, new Color(c.r, c.g, c.b, 0.7f), 0.35f);
                    Fx.Stars(at + up, 6, c32, 0.3f, 1.6f, 0.5f, 0.9f, 0.3f);
                    break;
                case SpellId.Revive:
                    // a phoenix flare where the hero stands up again
                    Fx.Pillar(at, 1.4f, 6f, c, 0.5f);
                    Fx.Pillar(at, 0.6f, 6f, Color.white, 0.35f);
                    Fx.Flash(at + up, 0.4f, 1.6f, new Color(1f, 0.9f, 0.75f, 0.95f), 0.14f);
                    Fx.Ring(at, 0.3f, 1.6f, c, 0.5f);
                    Fx.Sparks(at + up, 18, c32, 0.12f, 5f, 0.35f, 0.8f, 0.3f);
                    break;
                case SpellId.Bladearmor:
                    // steel: a hard white pop and sparks thrown off the armour
                    Fx.Flash(at + up, 0.25f, 1.0f, new Color(0.9f, 0.95f, 1f, 0.9f), 0.1f);
                    Fx.Ring(at, 0.2f, 1.0f, c, 0.35f);
                    Fx.Sparks(at + up, 16, c32, 0.1f, 6f, 0.25f, 0.6f, 0.2f);
                    break;
                case SpellId.Haste:
                case SpellId.Sprint:
                    // the burst as the speed kicks in: a white pop, a wind ring blown out flat, streaks flung round and a dust ring
                    Fx.Flash(at + up, 0.2f, 0.8f, new Color(1f, 1f, 1f, 0.85f), 0.08f);
                    Fx.Ring(at, 0.2f, 1.2f, c, 0.3f);
                    Fx.Swirl(at, 1.0f, c, 0.35f);
                    Fx.Sparks(at + up, 14, c32, 0.12f, 6f, 0.25f, 0.6f, 0.2f);
                    Fx.Wisps(at + up, 4, c32, 0.5f, 1.6f, 0.35f, 0.4f, 0.2f);
                    Fx.Dust(at + Vector3.up * 0.1f, 8, new Color32(200, 190, 150, 200), 0.5f, 2f, 0.5f, 0.3f, 0.3f);
                    break;
            }
        }

        /// <summary>
        /// MLBB-style speed while a unit runs fast (Sprint, Haste): wind ribbons streaming back from both sides of the body, a
        /// soft afterglow of the body's colour left behind, and a dust kick at the feet every few steps. Call every 0.05 s on the move.
        /// </summary>
        public static void SpeedTrail(Vector3 pos, Vector3 fwd, Color c, float height, int n)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return;
            fwd.Normalize();
            Vector3 side = new Vector3(fwd.z, 0f, -fwd.x), back = -fwd;
            var c32 = C32(c);
            var white = new Color32(255, 255, 255, 220);
            float h = Mathf.Max(0.8f, height);
            // two ribbons of wind, at hip and chest, left and right in turn
            float s = (n & 1) == 0 ? 0.35f : -0.35f;
            Fx.SparksAlong(pos + side * s + Vector3.up * (h * 0.35f) + back * 0.15f, back, 1, c32, 0.14f, 5f, 0.22f, 6f);
            Fx.SparksAlong(pos - side * s * 0.6f + Vector3.up * (h * 0.65f) + back * 0.1f, back, 1, white, 0.1f, 5.5f, 0.18f, 6f);
            // the afterglow where the body just was (reads as a motion blur)
            Fx.Wisps(pos + Vector3.up * (h * 0.5f) + back * 0.35f, 1, new Color32(c32.r, c32.g, c32.b, 150), h * 0.45f, 0.1f, 0.25f, 0f, 0.05f);
            if (n % 4 == 0) Fx.Dust(pos + back * 0.25f + Vector3.up * 0.06f, 2, new Color32(200, 188, 150, 190), 0.4f, 0.9f, 0.45f, 0.35f, 0.12f);
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
            // MLBB: the beam flares and takes the hero up, and a beam brings him down at home with a white flash
            Fx.Pillar(from, 2.0f, 7f, c, 0.35f);
            Fx.Pillar(from, 0.8f, 7f, Color.white, 0.25f);
            Fx.Flash(from + Vector3.up * 0.8f, 0.4f, 1.4f, new Color(1f, 1f, 1f, 0.9f), 0.1f);
            Fx.Ring(from, 0.4f, 1.4f, c, 0.4f);
            Fx.Wisps(from + Vector3.up * 0.5f, 4, c32, 0.5f, 0.6f, 0.6f, 1.2f, 0.3f);
            Fx.Glow(from + Vector3.up * 0.5f, 10, c32, 0.2f, 0.6f, 0.6f, 4f, 0.4f);
            Fx.Pillar(to, 2.0f, 7f, c, 0.6f);
            Fx.Pillar(to, 0.8f, 7f, Color.white, 0.4f);
            Fx.Flash(to + Vector3.up * 0.8f, 0.4f, 1.6f, new Color(1f, 1f, 1f, 0.95f), 0.12f);
            Fx.Ring(to, 0.3f, 1.6f, c, 0.5f);
            Fx.Nova(to, c, 1.4f, 0.45f);
            Fx.Pic("tx_wow_warrior_whirl", to, c, 3f, 0.6f);
            Fx.Glow(to + Vector3.up * 0.2f, 14, c32, 0.22f, 1.4f, 0.6f, 1.6f, 0.3f);
        }
    }
}
