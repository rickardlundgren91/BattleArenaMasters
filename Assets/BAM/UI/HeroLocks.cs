using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // Heroes opened by the account level (Speltest MenuProgress.cs, Rickard 2026-10-27: "Hjältar låses upp med nivå"). The level
    // and which hero opens when live in BAM.Online (Progress.HeroLevel) and reach the menus through OnlineHooks; offline builds
    // have no hook and every hero open. A locked hero's card gets a dark veil, a padlock and LEVEL n; its page can still be
    // opened and tried (TRY), but SELECT is grey and says the level. A chosen hero that is locked (a new account on this phone)
    // falls back to the first open one.
    public static partial class Screens
    {
        static readonly Color LockVeil = new Color(8 / 255f, 10 / 255f, 30 / 255f, 0.62f), LockInk = new Color32(0xe8, 0xea, 0xff, 0xff);
        static readonly Color LockButton = new Color(30 / 255f, 34 / 255f, 70 / 255f, 0.95f), LockKeyhole = new Color(8 / 255f, 10 / 255f, 30 / 255f, 0.8f);

        /// <summary>The account level that opens hero number index of HeroCatalog.All, 0 when it is open.</summary>
        static int LockLevel(int index) => OnlineHooks.HeroLockLevel != null ? OnlineHooks.HeroLockLevel(HeroCatalog.All[index].Id) : 0;

        /// <summary>A locked chosen hero becomes the first open one (Progression.ClampHero).</summary>
        static void ClampHero()
        {
            int chosen = HeroCatalog.IndexOf((HeroId)BamSettings.LastHero);
            if (chosen >= 0 && LockLevel(chosen) == 0) return;
            for (int i = 0; i < HeroCatalog.Count; i++)
                if (LockLevel(i) == 0) { BamSettings.LastHero = (int)HeroCatalog.All[i].Id; return; }
        }

        /// <summary>The veil, padlock and LEVEL n over a hero's card, built hidden (RefreshHeroSelect shows it).</summary>
        static GameObject BuildCardLock(RectTransform card, out Text level)
        {
            var veil = Pic(card, "Lock", UiKit.Rounded, LockVeil);
            Radius(veil, 3.4f * Cw);
            UiKit.Stretch(veil.rectTransform);
            veil.raycastTarget = false;
            float s = 2.6f * Cw;
            var lockRt = Padlock(veil.rectTransform, s, LockInk);
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 0.64f);
            level = UiKit.Label(veil.rectTransform, "", Mathf.RoundToInt(2.8f * Cw), TextAnchor.MiddleCenter, LockInk);
            level.font = UiKit.BodyFont;
            level.fontStyle = FontStyle.Bold;
            level.horizontalOverflow = HorizontalWrapMode.Overflow;
            level.raycastTarget = false;
            Mid(level.rectTransform, 0.5f, 0.64f, 0, -2.6f * s, 30 * Cw, 4 * Cw);
            veil.gameObject.SetActive(false);
            return veil.gameObject;
        }

        // MenuBotLock.cs DrawPadlock: the shackle (an arc), the body (a rounded box) and a dark keyhole; centred on its rect
        static RectTransform Padlock(RectTransform parent, float s, Color col)
        {
            var rt = UiKit.NewRect("Padlock", parent);
            rt.sizeDelta = new Vector2(2 * s, 3 * s);
            float lw = s * 0.38f, outer = s * 0.62f + lw / 2;
            var shackle = Pic(rt, "Shackle", HomeArt.CircleRing(lw / outer), col);
            Mid(shackle.rectTransform, 0.5f, 0.5f, 0, s * 0.15f, 2 * outer, 2 * outer);
            var body = Pic(rt, "Body", UiKit.Rounded, col);
            Radius(body, s * 0.3f);
            Mid(body.rectTransform, 0.5f, 0.5f, 0, -s * 0.625f, 2 * s, 1.55f * s);
            var hole = Pic(rt, "Keyhole", UiKit.Circle, LockKeyhole);
            Mid(hole.rectTransform, 0.5f, 0.5f, 0, -s * 0.55f, 0.48f * s, 0.48f * s);
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            return rt;
        }

        // the bottom button on a locked hero's page: grey with a padlock, "UNLOCKS AT LEVEL n"; set or cleared on each refresh
        static void SelectLocked(int lockLevel, string heroName)
        {
            bool locked = lockLevel > 0;
            heroSelectMint.color = locked ? LockButton : Color.white;
            heroSelectMint.texture = locked ? null : heroSelectMintTex;
            heroSelectLock.SetActive(locked);
            heroSelectText.color = locked ? UiKit.TextDim : HubDark;
            heroSelectText.text = locked ? "UNLOCKS AT LEVEL " + lockLevel : "SELECT " + heroName.ToUpperInvariant();
            heroSelectText.rectTransform.anchoredPosition = new Vector2(locked ? 2 * Cw : 0, 0);
        }
    }
}
