using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// A skin's own weapons and worn parts (Rickard 2026-11-14: "skins till vapen tar vi in", "och allt extra"). The parts come from
    /// ModelsV2/&lt;Hero&gt;/Skins/&lt;Skin&gt;/Parts/*.obj, converted on his PC by HeroArtImport (SkinParts) into
    /// Resources/BAMArt/Props/v2s_&lt;hero&gt;_&lt;skin&gt;_&lt;slot&gt; (slot = the file name's last word: Blast_Inferno_Belt = belt).
    /// The skin's gear is the V2 gear with:
    ///  - every held weapon and shield whose slot the skin has swapped for the skin's own (one it lacks keeps the plain one);
    ///  - the worn parts: only the skin's own (its body has its own pauldrons, belts and so on built in), except a worn part that is
    ///    the hero's weapon (Kugg's cannon), which stays when the skin has none;
    ///  - a skin part with no slot in the plain gear (Blast's pack, Bonk's helmet, Moa's wings) worn where a like part sits
    ///    (first guesses, as the V2 parts were).
    /// </summary>
    static class SkinGear
    {
        const string Folder = "BAMArt/Props/";
        static readonly Dictionary<string, GearDef> made = new Dictionary<string, GearDef>();

        public static string PropName(HeroId hero, string skin, string slot) => ("v2s_" + hero + "_" + skin + "_" + slot).ToLowerInvariant();

        static bool Has(string model) => model != null && Resources.Load<TextAsset>(Folder + model) != null;

        /// <summary>The slot a V2 model name stands for ("v2_bram_hammer" = hammer).</summary>
        static string SlotOf(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            int i = model.LastIndexOf('_');
            return i >= 0 ? model.Substring(i + 1) : model;
        }

        // a skin part named for another slot than the plain one it replaces (Tikta's jade blades are her needles)
        static readonly Dictionary<string, string> sameAs = new Dictionary<string, string> { { "blade", "needle" } };

        /// <summary>The skin's model for the plain model's slot, or null when the skin has none.</summary>
        static string SkinModel(HeroId hero, string skin, string plain)
        {
            string slot = SlotOf(plain);
            if (slot == null) return null;
            string m = PropName(hero, skin, slot);
            if (Has(m)) return m;
            foreach (var kv in sameAs) if (kv.Value == slot && Has(PropName(hero, skin, kv.Key))) return PropName(hero, skin, kv.Key);
            return null;
        }

        static T Clone<T>(T o) where T : class => o == null ? null : (T)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(o, null);

        // worn parts that are the hero's weapon, or her mark (Tikta's glowing wings, 2026-11-16): kept when the skin has none of its own
        static bool IsWeapon(HeroId hero, string slot) => (hero == HeroId.Kugg && slot == "cannon") || (hero == HeroId.Tikta && (slot == "glowwingr" || slot == "glowwingl"));

        public static GearDef For(HeroId hero, string skin, GearDef plain)
        {
            if (plain == null || string.IsNullOrEmpty(skin)) return plain;
            string key = hero + "/" + skin;
            if (made.TryGetValue(key, out GearDef g)) return g;
            g = Clone(plain);
            var used = new HashSet<string>();
            if (plain.Weapons != null)
            {
                g.Weapons = new WeaponDef[plain.Weapons.Length];
                for (int i = 0; i < plain.Weapons.Length; i++)
                {
                    g.Weapons[i] = Clone(plain.Weapons[i]);
                    string m = g.Weapons[i] != null ? SkinModel(hero, skin, g.Weapons[i].Model) : null;
                    if (m != null) { g.Weapons[i].Model = m; used.Add(m); }
                }
            }
            // Fenn's Phantom look is a hooded duelist with no shield (Rickard 2026-11-16: "ha phantom som skin på fenn"): he drops the
            // knight's shield instead of keeping the plain one
            if (plain.Shield != null && hero == HeroId.Fenn && skin == "Phantom") g.Shield = null;
            else if (plain.Shield != null)
            {
                g.Shield = Clone(plain.Shield);
                string m = SkinModel(hero, skin, g.Shield.Model);
                if (m != null) { g.Shield.Model = m; used.Add(m); }
            }
            var worn = new List<WornDef>();
            if (plain.Worn != null)
                foreach (WornDef w in plain.Worn)
                {
                    if (w == null) continue;
                    string m = SkinModel(hero, skin, w.Model);
                    if (m == null && !IsWeapon(hero, SlotOf(w.Model))) continue; // the skin's body has its own look there
                    var c = Clone(w);
                    if (m != null) { c.Model = m; used.Add(m); }
                    worn.Add(c);
                }
            // the skin's parts with no plain slot
            foreach (string slot in extraSlots)
            {
                string m = PropName(hero, skin, slot);
                if (used.Contains(m) || !Has(m)) continue;
                // Kugg's round V3 bodies hide a pack or a cape wholly inside the ball (as the plain one's, Weapons.MakeV2)
                if (hero == HeroId.Kugg && (slot == "pack" || slot == "cape")) continue;
                // Moa's wings are see-through bee wings made in code, not the skin's solid Tripo wings (Rickard 2026-11-19: "lätt
                // transparenta precis som bivingar", Weapons.MoaBeeWing)
                if (hero == HeroId.Moa && slot == "wings") { worn.Add(Weapons.MoaBeeWing(true)); worn.Add(Weapons.MoaBeeWing(false)); continue; }
                WornDef w = Extra(hero, skin, slot);
                if (w == null) { Debug.LogWarning("[BAM.Art] " + hero + " skin " + skin + ": no place for its " + slot + " yet"); continue; }
                w.Model = m;
                worn.Add(w);
            }
            g.Worn = worn.ToArray();
            made[key] = g;
            return g;
        }

        static readonly string[] extraSlots = { "pack", "cape", "mantle", "wings", "helmet", "halo", "necklace", "quiver" }; // no "belt": Blast's skins wear no bomb belt (Rickard 2026-11-19)

        const HumanBodyBones Chest = HumanBodyBones.UpperChest, LArm = HumanBodyBones.LeftUpperArm, RArm = HumanBodyBones.RightUpperArm,
            Hips = HumanBodyBones.Hips, LLeg = HumanBodyBones.LeftUpperLeg, RLeg = HumanBodyBones.RightUpperLeg, Head = HumanBodyBones.Head;

        // where a skin's extra part sits: copied from the V2 part most like it (Weapons.MakeV2)
        static WornDef Extra(HeroId hero, string skin, string slot)
        {
            switch (slot)
            {
                case "pack": // Kugg's rune-engine backpack
                    return new WornDef { Bone = Chest, From = LArm, To = RArm, Height = 0.3f, At = new Vector3(0f, -0.2f, -0.1f) };
                case "cape": // Kugg's short cape
                    return new WornDef { Bone = Chest, From = LArm, To = RArm, Height = 0.32f, At = new Vector3(0f, 0.02f, -0.07f), Flat = 0.8f, Sway = 10f };
                case "mantle": // Bonk's pelt over the shoulders; on anyone else a cape like Fenn's
                    // Bonk Lava: his rock body is broader, and at the pelt's size the feather mantle sank into it (only a strip showed
                    // under the shoulder blades, Rickard's menu screenshots 2026-11-16); bigger, higher and not pressed flat it hangs
                    // over his back from the shoulders
                    if (hero == HeroId.Bonk && string.Equals(skin, "Lava", System.StringComparison.OrdinalIgnoreCase))
                        return new WornDef { Bone = Chest, From = LArm, To = RArm, Wide = 1.6f, At = new Vector3(0f, 0.16f, -0.05f), Flat = 1f, Sway = 8f };
                    return hero == HeroId.Bonk
                        ? new WornDef { Bone = Chest, From = LArm, To = RArm, Wide = Weapons.BonkPeltWide, At = Weapons.BonkPeltAt, Flat = 0.8f, Skin = true } // skinned like his own pelt (Weapons)
                        : new WornDef { Bone = Chest, From = LArm, To = RArm, Height = 0.42f, At = new Vector3(0f, 0.01f, -0.07f), Yaw = 180f, Flat = 0.8f, Sway = 22f };
                case "wings": // Tikta's wing plate
                    return new WornDef { Bone = Chest, From = LArm, To = RArm, Height = 0.3f, At = new Vector3(0f, -0.14f, -0.07f), Yaw = 180f };
                case "helmet": // on the head, like Nässla's cap
                    // Bonk Mammoth's sabre-tooth skull: at the cap's size it sat inside his big hair (not seen in the menu, 2026-11-16);
                    // as a hood over the head, the fangs framing his face
                    // (18 Nov: his clothed Tripo-rigged body has its Head joint mid-head, not at the neck, so 0.085 floated it above him)
                    if (hero == HeroId.Bonk)
                        return new WornDef { Bone = Head, From = Head, Height = 0.25f, At = new Vector3(0f, -0.05f, 0f) };
                    return new WornDef { Bone = Head, From = Head, Height = 0.16f, At = new Vector3(0f, 0.06f, 0f) };
                case "halo": // a ring behind the head
                    return new WornDef { Bone = Head, From = Head, Height = 0.22f, At = new Vector3(0f, 0.0f, -0.08f) };
                case "necklace":
                    return new WornDef { Bone = Chest, From = LArm, To = RArm, Wide = 0.85f, At = new Vector3(0f, -0.05f, 0.05f) };
                case "quiver":
                    return new WornDef { Bone = Chest, From = LArm, To = RArm, Height = 0.3f, At = new Vector3(0.03f, -0.2f, -0.08f), Turn = new Vector3(0f, 0f, -20f) };
            }
            return null;
        }
    }
}
