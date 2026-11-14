using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The skin each hero wears, picked by the player (Home's skin list) and kept on this phone (PlayerPrefs). Cosmetic and local:
    /// only the player's own hero (and Fenn's decoy of it) and the menu stages wear it; every other hero in a match, and the player's
    /// hero on other phones online, shows its plain body (Rickard 2026-11-14). Skin 0 = the plain body; 1.. = HeroArt.SkinModels.
    /// </summary>
    public static class HeroSkins
    {
        const string Key = "BAM.Skin.";

        static HeroArt Art(HeroId hero) => Resources.Load<HeroArt>("BAMArt/Heroes/" + hero);

        /// <summary>How many looks the hero has, the plain one included (1 = no skins).</summary>
        public static int Count(HeroId hero)
        {
            var a = Art(hero);
            return a != null && a.V2 ? 1 + a.SkinCount : 1;
        }

        public static string Name(HeroId hero, int skin)
        {
            if (skin <= 0) return "Classic";
            var a = Art(hero);
            return a != null && a.SkinNames != null && skin - 1 < a.SkinNames.Length ? a.SkinNames[skin - 1] : "Skin " + skin;
        }

        /// <summary>The colour the skin's name is written in (its picture's main colour; the plain look in soft white).</summary>
        public static Color Tint(HeroId hero, int skin)
        {
            if (skin <= 0) return new Color(0.92f, 0.94f, 1f);
            var a = Art(hero);
            return a != null && a.SkinColors != null && skin - 1 < a.SkinColors.Length ? a.SkinColors[skin - 1] : Color.white;
        }

        /// <summary>The look the player picked for this hero (0 when the pick no longer exists).</summary>
        public static int Chosen(HeroId hero)
        {
            int s = PlayerPrefs.GetInt(Key + hero, 0);
            return s > 0 && s < Count(hero) ? s : 0;
        }

        public static void Choose(HeroId hero, int skin)
        {
            if (skin <= 0) PlayerPrefs.DeleteKey(Key + hero); else PlayerPrefs.SetInt(Key + hero, skin);
            PlayerPrefs.Save();
        }

        /// <summary>The skin's folder name (its parts' names, SkinGear), from the HeroArt a skin was applied to or the plain one.</summary>
        internal static string SkinName(HeroArt art, int skin)
        {
            if (art == null || skin <= 0) return null;
            if (art.SkinNames != null && skin - 1 < art.SkinNames.Length) return art.SkinNames[skin - 1];
            return null;
        }

        /// <summary>The hero's HeroArt with skin's body and picture in place of the plain ones (a copy, made once per skin).</summary>
        internal static HeroArt Apply(HeroArt art, int skin)
        {
            if (art == null || skin <= 0 || skin > art.SkinCount || art.SkinModels[skin - 1] == null) return art;
            var a = Object.Instantiate(art);
            a.name = art.name + " skin " + skin;
            a.Model = art.SkinModels[skin - 1];
            // no picture of its own: whatever the skin's FBX material has, never the plain body's picture on the skin's UVs
            a.Texture = art.SkinTextures != null && skin - 1 < art.SkinTextures.Length ? art.SkinTextures[skin - 1] : null;
            return a;
        }
    }
}
