using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The skin each hero wears, picked by the player (Home's skin list) and kept on this phone (PlayerPrefs). Cosmetic: the player's
    /// own hero (and Fenn's decoy of it) and the menu stages wear it, and online the room sends it to the other phones, which draw
    /// that player's hero (and decoy) in it too (Rickard 2026-11-15); bots show their plain body. Skin 0 = the plain body; 1.. =
    /// HeroArt.SkinModels.
    /// </summary>
    public static class HeroSkins
    {
        const string Key = "BAM.Skin.";

        /// <summary>Whether the player may wear the look now (gold skins must be bought, Gold.cs). Null: every look, as in offline builds.</summary>
        public static System.Func<HeroId, int, bool> Wearable;

        // online reads the player's look through BAM.Core (BAM.Online.Mp cannot see BAM.Art)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook() => BAM.Core.BamSettings.SkinOf = Chosen;

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
            return s > 0 && s < Count(hero) && (Wearable == null || Wearable(hero, s)) ? s : 0;
        }

        public static void Choose(HeroId hero, int skin)
        {
            if (skin <= 0) PlayerPrefs.DeleteKey(Key + hero); else PlayerPrefs.SetInt(Key + hero, skin);
            PlayerPrefs.Save();
            BAM.Core.BamSettings.RaiseSkinPicked(); // a room tells the other phones (MpRoom)
        }

        /// <summary>Another player's look, as their phone sent it: kept when this phone has it, else the plain body. Not checked
        /// against this player's gold skins (Wearable): the sender's phone only sends a look its player may wear.</summary>
        public static int Valid(HeroId hero, int skin) => skin > 0 && skin < Count(hero) ? skin : 0;

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
