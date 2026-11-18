using System.Collections.Generic;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The BATTLE SPELL card on the hero page (old MenuSpell.cs SpellPicker): BATTLE SPELL in small dim letters, one chip per
    // spell (ten since 2026-11-18, two rows of five: SpellDefs.All) with the spell's glyph in its colour and its name, the chosen one mint with a mint edge,
    // and the chosen spell's text under them. The pick counts for every hero (BamSettings.Spell).
    public static partial class Screens
    {
        sealed class SpellChip { public SpellId Id; public Image Back, Edge; public Text Name; }

        static readonly List<SpellChip> spellChips = new List<SpellChip>();
        static Text spellDesc;
        static readonly Color ChipOn = new Color(62 / 255f, 230 / 255f, 176 / 255f, 0.22f);

        // two rows of five chips since the five new spells of 2026-11-18 (one row of five before)
        const int ChipsPerRow = 5;
        const float ChipGap = 2 * Cw;
        const float SpellCardH = 8 * Cw + 2 * 15 * Cw + ChipGap + 10 * Cw;

        static void BuildSpellCard(RectTransform content)
        {
            float chipH = 15 * Cw, h = SpellCardH;
            var card = UiKit.NewRect("BattleSpell", content);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            CardBack(card, 4 * Cw);
            var title = Treb(card, "BATTLE SPELL", 2.6f, UiKit.TextDim);
            Stretched(title.rectTransform, 4 * Cw, 4.2f * Cw - 2.5f * Cw, 4 * Cw, 5 * Cw);

            int rows = (SpellDefs.All.Length + ChipsPerRow - 1) / ChipsPerRow;
            var rowRects = new RectTransform[rows];
            for (int ri = 0; ri < rows; ri++)
            {
                var row = UiKit.NewRect("Chips" + ri, card);
                Stretched(row, 3 * Cw, 7 * Cw + ri * (chipH + ChipGap), 3 * Cw, chipH);
                var lay = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                lay.spacing = 2 * Cw;
                lay.childControlWidth = lay.childControlHeight = true;
                lay.childForceExpandWidth = lay.childForceExpandHeight = true;
                rowRects[ri] = row;
            }

            spellChips.Clear();
            int ci = 0;
            foreach (var id in SpellDefs.All)
            {
                var c = new SpellChip { Id = id };
                var chip = UiKit.NewRect(SpellDefs.Name(id), rowRects[ci++ / ChipsPerRow]);
                c.Back = Pic(chip, "Back", UiKit.Rounded, HubPicBack);
                UiKit.Stretch(c.Back.rectTransform);
                Radius(c.Back, 3 * Cw);
                c.Back.raycastTarget = true;
                c.Edge = Pic(chip, "Edge", HomeArt.RoundRing(0.45f / 3f), UiKit.Primary);
                UiKit.Stretch(c.Edge.rectTransform);
                RingRadius(c.Edge, 3 * Cw);
                // the glyph: k = cw(3.2) around 38 % of the chip's height; the sprite spans 2.4 k
                var glyph = Pic(chip, "Glyph", SpellGlyph(id), SpellColor(id));
                Mid(glyph.rectTransform, 0.5f, 1, 0, -chipH * 0.38f, 2.4f * 3.2f * Cw, 2.4f * 3.2f * Cw);
                c.Name = Treb(chip, SpellDefs.Name(id), 2.8f, UiKit.TextDim);
                c.Name.alignment = TextAnchor.MiddleCenter;
                Mid(c.Name.rectTransform, 0.5f, 1, 0, -chipH * 0.82f, 20 * Cw, 4 * Cw);
                var b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = c.Back;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                var pick = id;
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); BamSettings.Spell = pick; RefreshSpellCard(); });
                chip.gameObject.AddComponent<ButtonFx>();
                spellChips.Add(c);
            }

            spellDesc = Treb(card, "", 3.1f, HubBody);
            spellDesc.fontStyle = FontStyle.Normal;
            spellDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretched(spellDesc.rectTransform, 4 * Cw, 7 * Cw + rows * chipH + (rows - 1) * ChipGap + 5 * Cw - 2.5f * Cw, 4 * Cw, 5 * Cw);
            RefreshSpellCard();
        }

        static void RefreshSpellCard()
        {
            if (spellDesc == null) return;
            var chosen = BamSettings.Spell;
            foreach (var c in spellChips)
            {
                bool on = c.Id == chosen;
                c.Back.color = on ? ChipOn : HubPicBack;
                c.Edge.enabled = on;
                c.Name.color = on ? HubText : UiKit.TextDim;
                c.Name.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }
            spellDesc.text = SpellDefs.Desc(chosen);
        }

        /// <summary>The spell's colour (old spellColor).</summary>
        public static Color SpellColor(SpellId id) => Hex(SpellDefs.Color(id));

        /// <summary>
        /// The spell's sign as a white sprite to tint (old Hud.SpellGlyph): Blink two chevrons, Recall a house, Shield a shield,
        /// Haste a lightning bolt; Sprint (new) three speed lines and a chevron. The sprite is 2.4 k wide for the old size k.
        /// </summary>
        public static Sprite SpellGlyph(SpellId id) => HomeArt.Glyph(id);
    }

    static partial class HomeArt
    {
        static readonly Dictionary<SpellId, Sprite> glyphs = new Dictionary<SpellId, Sprite>();

        // The old shapes in units of k with y down; drawn on 96 px for 2.4 k.
        static readonly Vector2[] RecallShape = { V(-1, 0), V(0, -1), V(1, 0), V(0.7f, 0), V(0.7f, 0.9f), V(-0.7f, 0.9f), V(-0.7f, 0) };
        static readonly Vector2[] ShieldShape = { V(-0.8f, -0.8f), V(0.8f, -0.8f), V(0.8f, 0.1f), V(0, 1), V(-0.8f, 0.1f) };
        static readonly Vector2[] CrossShape = { V(-0.3f, -0.9f), V(0.3f, -0.9f), V(0.3f, -0.3f), V(0.9f, -0.3f), V(0.9f, 0.3f), V(0.3f, 0.3f), V(0.3f, 0.9f), V(-0.3f, 0.9f), V(-0.3f, 0.3f), V(-0.9f, 0.3f), V(-0.9f, -0.3f), V(-0.3f, -0.3f) };
        static readonly Vector2[] StarShape = { V(0, -1), V(0.25f, -0.25f), V(1, 0), V(0.25f, 0.25f), V(0, 1), V(-0.25f, 0.25f), V(-1, 0), V(-0.25f, -0.25f) };
        static readonly Vector2[] RiseShape = { V(0, -1), V(0.75f, -0.15f), V(0.3f, -0.15f), V(0.3f, 0.55f), V(-0.3f, 0.55f), V(-0.3f, -0.15f), V(-0.75f, -0.15f) };
        static readonly Vector2[] SmallShieldShape = { V(-0.6f, -0.6f), V(0.6f, -0.6f), V(0.6f, 0.08f), V(0, 0.75f), V(-0.6f, 0.08f) };
        static readonly Vector2[] BoltShape = { V(0.3f, -1), V(-0.6f, 0.15f), V(-0.05f, 0.15f), V(-0.3f, 1), V(0.6f, -0.2f), V(0.05f, -0.2f) };
        static Vector2 V(float x, float y) => new Vector2(x, y);

        public static Sprite Glyph(SpellId id)
        {
            if (glyphs.TryGetValue(id, out var s)) return s;
            // Rickard's own icon (2026-10-27, Resources/BAM/Menu/spell_*.png, white to tint) when there is one
            var pic = Resources.Load<Texture2D>("BAM/Menu/spell_" + id.ToString().ToLowerInvariant());
            if (pic != null) return glyphs[id] = Sprite.Create(pic, new Rect(0, 0, pic.width, pic.height), new Vector2(0.5f, 0.5f), 100f);
            const int N = 96;
            const float Scale = N / 2.4f, Line = 0.28f;
            Vector2[] poly = id == SpellId.Recall ? RecallShape : id == SpellId.Shield ? ShieldShape : id == SpellId.Haste ? BoltShape
                : id == SpellId.FirstAid ? CrossShape : id == SpellId.Immune ? StarShape : id == SpellId.Revive ? RiseShape : id == SpellId.Bladearmor ? SmallShieldShape : null;
            var strokes = new List<Vector2>(); // pairs: from, to
            if (id == SpellId.Blink)
                for (int i = 0; i < 2; i++)
                {
                    float o = (i - 0.5f) * 0.9f;
                    Chevron(strokes, o);
                }
            else if (id == SpellId.Revive) { strokes.Add(V(-0.75f, 0.88f)); strokes.Add(V(0.75f, 0.88f)); }
            else if (id == SpellId.Fury)
            {
                strokes.Add(V(-0.55f, 0.55f)); strokes.Add(V(0.85f, -0.85f));
                strokes.Add(V(-0.75f, 0.05f)); strokes.Add(V(-0.05f, 0.75f));
                strokes.Add(V(-0.6f, 0.6f)); strokes.Add(V(-0.95f, 0.95f));
            }
            else if (id == SpellId.Bladearmor)
            {
                strokes.Add(V(-0.6f, -0.6f)); strokes.Add(V(-1.05f, -1.05f));
                strokes.Add(V(0.6f, -0.6f)); strokes.Add(V(1.05f, -1.05f));
                strokes.Add(V(-0.6f, 0.08f)); strokes.Add(V(-1.1f, 0.25f));
                strokes.Add(V(0.6f, 0.08f)); strokes.Add(V(1.1f, 0.25f));
                strokes.Add(V(0, 0.75f)); strokes.Add(V(0, 1.15f));
            }
            else if (id == SpellId.Sprint)
            {
                Chevron(strokes, 0.45f);
                strokes.Add(V(-1f, -0.45f)); strokes.Add(V(-0.25f, -0.45f));
                strokes.Add(V(-1.1f, 0)); strokes.Add(V(-0.05f, 0));
                strokes.Add(V(-1f, 0.45f)); strokes.Add(V(-0.25f, 0.45f));
            }
            s = Make("Glyph" + id, N, N, (x, y) =>
            {
                var p = new Vector2((x - N / 2f) / Scale, -(y - N / 2f) / Scale);
                float a = 0;
                if (poly != null)
                {
                    float d = EdgeDistance(poly, p) * Scale;
                    a = Mathf.Clamp01(0.5f + (Inside(poly, p) ? d : -d));
                }
                for (int i = 0; i + 1 < strokes.Count; i += 2)
                {
                    float d = (SegmentDistance(p, strokes[i], strokes[i + 1]) - Line / 2) * Scale;
                    a = Mathf.Max(a, Mathf.Clamp01(0.5f - d));
                }
                return White(a);
            });
            glyphs[id] = s;
            return s;
        }

        static void Chevron(List<Vector2> strokes, float o)
        {
            strokes.Add(V(o - 0.35f, -0.7f)); strokes.Add(V(o + 0.35f, 0));
            strokes.Add(V(o + 0.35f, 0)); strokes.Add(V(o - 0.35f, 0.7f));
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) best = Mathf.Min(best, SegmentDistance(p, poly[j], poly[i]));
            return best;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }
    }
}
