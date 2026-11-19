using BAM.Art;
using BAM.Sim;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// Keeps the world's health bars and name tags out from under the HUD's solid parts (UI/UX review 2026-11-19 nr 1 and 6). The
    /// top strip is glass and the item cards a dark glass (Rickard 2026-11-16, kept), so a hero walking up under them showed its
    /// red bar and its name through the glass, right into the timer and the cards' words ("Moa", "Ravna" over Quarry Mark's text).
    /// Each frame the HUD notes the screen rectangles of the top strip and of the item cards that are up; a bar whose point falls
    /// inside one (with a little room under it for the name above the bar) is not drawn, and so neither is its name tag
    /// (HudOverhead follows the bar through MatchViews.BarOf). The heroes' bodies still show through the glass as before.
    /// </summary>
    sealed partial class Hud
    {
        readonly Vector3[] coverCorners = new Vector3[4];
        readonly UnityEngine.Rect[] covers = new UnityEngine.Rect[2 + ItemS.Choices];
        int coverCount;
        System.Func<Vector3, bool> coverTest; // made once: ArtKit.BarCover is asked for every bar every frame

        void CoverBegin() { if (coverTest == null) coverTest = BarCovered; ArtKit.BarCover = coverTest; coverCount = 0; }
        void CoverEnd() { if (ArtKit.BarCover == coverTest) ArtKit.BarCover = null; coverCount = 0; }

        UnityEngine.Rect ScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(coverCorners); // a screen-space overlay canvas: world corners are screen pixels
            return UnityEngine.Rect.MinMaxRect(coverCorners[0].x, coverCorners[0].y, coverCorners[2].x, coverCorners[2].y);
        }

        /// <summary>Notes where the solid parts are this frame; after Top() and ItemsFrame() have placed them.</summary>
        void CoverFrame(bool hidden)
        {
            coverCount = 0;
            if (hidden || m == null || (Intro != null && Intro.Active)) return;
            if (topRoot != null && topRoot.gameObject.activeInHierarchy)
            {
                UnityEngine.Rect r = ScreenRect(strip);
                if (pill != null && pill.gameObject.activeInHierarchy) { UnityEngine.Rect p = ScreenRect(pill); r = UnityEngine.Rect.MinMaxRect(Mathf.Min(r.xMin, p.xMin), Mathf.Min(r.yMin, p.yMin), Mathf.Max(r.xMax, p.xMax), Mathf.Max(r.yMax, p.yMax)); }
                covers[coverCount++] = r;
            }
            if (offerRoot != null && offerRoot.gameObject.activeInHierarchy)
            {
                if (offerTitleLook.alpha > 0.3f) covers[coverCount++] = ScreenRect(offerTitleRt);
                for (int i = 0; i < offerCard.Length && coverCount < covers.Length; i++)
                    if (offerCard[i] != null && offerCard[i].gameObject.activeInHierarchy && offerLook[i].alpha > 0.3f)
                        covers[coverCount++] = ScreenRect(offerCard[i]);
            }
        }

        bool BarCovered(Vector3 world)
        {
            if (coverCount == 0 || cam == null || cam.Camera == null) return false;
            Vector3 sp = cam.Camera.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            // a bar a little under a solid part still has its name inside it: the name stands about 3 % of the screen over the bar
            float nameRoom = Screen.height * 0.03f, side = Screen.height * 0.01f;
            for (int i = 0; i < coverCount; i++)
            {
                UnityEngine.Rect r = covers[i];
                if (sp.x >= r.xMin - side && sp.x <= r.xMax + side && sp.y >= r.yMin - nameRoom && sp.y <= r.yMax) return true;
            }
            return false;
        }
    }
}
