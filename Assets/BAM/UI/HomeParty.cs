using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The party on Home, as the old Home v3 (Speltest MenuHome3.HubTeam3, UiV3.TEAM / PARTY_ZOOM / GROUNDED, ONE_PLAY): the whole
    // lobby lives on Home instead of a page of its own (Rickard 2026-10-30: "i speltest dyker lobby heroes upp i menyn istället").
    // Up to two party members stand beside the player's hero, a little further back and 0.7 as tall, turned toward the middle,
    // each on a ring that is mint when they are back on Home and grey while they are still in the last match, with a name tag
    // under the feet. The view zooms out with the party (alone 1, two 0.88, three 0.78). An empty place keeps its + INVITE pill.
    // The player gets the name tag with crown and pencil, and LEAVE PARTY sits top left. Only in online builds (OnlineHooks.Party).
    public static partial class Screens
    {
        sealed partial class HomeFx
        {
            sealed class Slot
            {
                public GameObject Root, Ready, NotReady;
                public HeroStage Stage;
                public RectTransform Tag;
            }

            readonly Slot[] slots = new Slot[2];
            RectTransform[] invitePills;
            RectTransform myTag, leaveBtn;
            float zoom = 1;
            int shownCount = -1;
            bool shownParty;
            readonly PartyMate[] shownMates = new PartyMate[2];

            const float SideH = 0.7f * HeroH, SideDx = 30 * Cw, SideUp = 10 * Cw;

            void BuildPartySlots()
            {
                if (OnlineHooks.Party == null && !Application.isEditor) return;
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    float cx = 540 + side * SideDx, feet = FeetY + SideUp, rx = 0.3f * SideH, ry = 0.075f * SideH;
                    var s = slots[i] = new Slot();
                    var root = UiKit.NewRect(i == 0 ? "Party Left" : "Party Right", stage);
                    UiKit.Stretch(root);
                    s.Root = root.gameObject;

                    // HubRing3 (half, GROUNDED): the soft shadow, the contact shadow and the back half of the ring
                    At(Img(root, "Shadow", HomeArt.RingShadow, Color.white), cx, feet, 2 * rx * 1.1f, 2 * ry * 1.4f);
                    At(Img(root, "Contact", HomeArt.ContactShadow, Color.white), cx, feet, 2 * rx * 0.55f, 2 * ry * 0.75f);
                    var readyBack = Ring(root, cx, feet, rx, true, true);
                    var notBack = Ring(root, cx, feet, rx, false, true);

                    // HubHero3: the member's 3D model, its picture 0.9 as wide as tall
                    var pic = UiKit.NewRect("Hero3D", root);
                    At(pic, cx, feet - 0.1f * SideH, 1.35f * SideH * 0.9f, 1.35f * SideH, 0.5f, 0);
                    var picture = pic.gameObject.AddComponent<RawImage>();
                    picture.raycastTarget = false;
                    picture.enabled = false;
                    s.Stage = pic.gameObject.AddComponent<HeroStage>(); // no raycast target: a party member is not turned by a drag
                    s.Stage.Picture = picture;
                    s.Stage.FeetAt = 0.1f / 1.35f;
                    s.Stage.HeroFill = 1f / 1.35f;
                    s.Stage.Spot = new Vector3(580 + 40 * i, 0, 500);
                    s.Stage.Yaw = side < 0 ? 160 : 200; // turned 0.35 rad toward the middle

                    readyFrontOf[i] = Ring(root, cx, feet, rx, true, false).gameObject;
                    notFrontOf[i] = Ring(root, cx, feet, rx, false, false).gameObject;
                    s.Ready = readyBack.gameObject;
                    s.NotReady = notBack.gameObject;

                    // the old hubSlot button: a tap on a member opens Friends
                    var hit = UiKit.NewRect("Hit", root);
                    At(hit, cx, feet - SideUp, 28 * Cw, SideH + 2 * Cw + SideUp, 0.5f, 0);
                    var img = hit.gameObject.AddComponent<Image>();
                    img.color = Color.clear;
                    var b = hit.gameObject.AddComponent<Button>();
                    b.targetGraphic = img;
                    b.transition = Selectable.Transition.None;
                    b.navigation = new Navigation { mode = Navigation.Mode.None };
                    b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); OnlineHooks.OpenFriends?.Invoke(); });
                    s.Root.SetActive(false);
                }
            }

            readonly GameObject[] readyFrontOf = new GameObject[2], notFrontOf = new GameObject[2];

            // one half of a member's ring: mint with its glow when ready, else a dim grey line (HubRing3 / HubRingFront3)
            RectTransform Ring(RectTransform parent, float cx, float feet, float rx, bool ready, bool back)
            {
                var r = UiKit.NewRect(ready ? "Ring Ready" : "Ring", parent);
                At(r, cx, feet, 0, 0);
                if (ready)
                {
                    Half(Ellipse(r, rx, 1.8f * Cw, Fade(UiKit.Primary, 0.3f), 0), back);
                    Half(Ellipse(r, rx, 0.65f * Cw, UiKit.Primary, 0), back);
                }
                else Half(Ellipse(r, rx, 0.45f * Cw, new Color(170 / 255f, 178 / 255f, 200 / 255f, 0.45f), 0), back);
                return r;
            }

            // the name tags (drawn by the online side with the old HubNameTag3) and LEAVE PARTY (HubLeaveBtn3)
            void BuildPartyTags()
            {
                if (OnlineHooks.Paint == null) return;
                myTag = PaintButton(low, "tag0", 44 * Cw, 9 * Cw, () => ShowTab(1)); // the tag's pencil: change hero
                for (int i = 0; i < 2; i++)
                    if (slots[i] != null) slots[i].Tag = PaintButton(low, "tag" + (i + 1), 44 * Cw, 9 * Cw, null);
                leaveBtn = PaintButton(top, "leave", 40 * Cw, 9.5f * Cw, () => { if (TestParty != null) TestParty = null; else OnlineHooks.LeaveParty?.Invoke(); });
                // top left under the top bar: the old (cw(3), topH + cw(1)) with topH = cw(15), a cw(1) taller touch area
                leaveBtn.anchorMin = leaveBtn.anchorMax = leaveBtn.pivot = new Vector2(0, 1);
                leaveBtn.anchoredPosition = new Vector2(3 * Cw, -15 * Cw);
                myTag.gameObject.SetActive(false);
                leaveBtn.gameObject.SetActive(false);
            }

            static RectTransform PaintButton(RectTransform parent, string key, float w, float h, System.Action tap)
            {
                var holder = UiKit.NewRect(key, parent);
                holder.anchorMin = holder.anchorMax = Vector2.zero;
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.sizeDelta = new Vector2(w, h);
                var rt = OnlineHooks.Paint(holder, key);
                var raw = rt.GetComponent<RawImage>();
                if (raw != null) raw.raycastTarget = tap != null;
                if (tap != null)
                {
                    var b = rt.gameObject.AddComponent<Button>();
                    b.targetGraphic = raw;
                    b.transition = Selectable.Transition.None;
                    b.navigation = new Navigation { mode = Navigation.Mode.None };
                    b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); tap(); });
                    rt.gameObject.AddComponent<ButtonFx>();
                }
                return holder;
            }

            void FrameParty()
            {
                foreach (var s in slots) if (s != null && s.Root.activeSelf) s.Stage.Frame();
            }

            /// <summary>Every frame: follows the party and eases the zoom; returns the background's zoom.</summary>
            float PartyTick()
            {
                var p = CurrentParty();
                int n = p != null ? p.Count : 0;
                if (p != null != shownParty || n != shownCount || p != null && Changed(p)) ShowParty(p);
                float want = n <= 0 ? 1 : n == 1 ? 0.88f : 0.78f;
                if (zoom != want)
                {
                    zoom += (want - zoom) * (1 - Mathf.Exp(-6 * Mathf.Min(Time.unscaledDeltaTime, 0.1f)));
                    if (Mathf.Abs(zoom - want) < 0.001f) zoom = want;
                    PlaceParty();
                }
                // the painting never gets narrower than the screen
                float dw = homeBg != null ? homeBg.rectTransform.sizeDelta.x : 0, w = homeBackdrop != null ? homeBackdrop.rect.width : 0;
                return dw > 0 && zoom < 0.999f ? Mathf.Max(zoom, w / dw) : 1;
            }

            bool Changed(PartyView p)
            {
                for (int i = 0; i < p.Count && i < 2; i++)
                {
                    var a = p.Mates[i]; var b = shownMates[i];
                    if (a.Name != b.Name || a.Hero != b.Hero || a.Ready != b.Ready) return true;
                }
                return false;
            }

            void ShowParty(PartyView p)
            {
                shownParty = p != null;
                shownCount = p != null ? p.Count : 0;
                for (int i = 0; i < 2; i++)
                {
                    var s = slots[i];
                    bool on = p != null && i < p.Count;
                    if (on) shownMates[i] = p.Mates[i]; else shownMates[i] = default;
                    if (s == null) continue;
                    s.Root.SetActive(on);
                    if (s.Tag != null) s.Tag.gameObject.SetActive(on);
                    if (!on) continue;
                    var m = p.Mates[i];
                    s.Ready.SetActive(m.Ready);
                    s.NotReady.SetActive(!m.Ready);
                    readyFrontOf[i].SetActive(m.Ready);
                    notFrontOf[i].SetActive(!m.Ready);
                    if (!s.Stage.Show(m.Hero)) s.Stage.Picture.enabled = false;
                    s.Stage.Frame();
                }
                if (myTag != null) myTag.gameObject.SetActive(shownCount > 0); // alone (ONE_PLAY): no plate, the name is in the top bar
                if (leaveBtn != null) leaveBtn.gameObject.SetActive(p != null);
                if (invitePills != null)
                    for (int i = 0; i < 2; i++) invitePills[i].gameObject.SetActive(i >= shownCount);
                playSub.text = HomePlaySub();
                PlaceParty();
            }

            // the zoom scales the platform's heroes around the feet; the tags and the pills keep their size and follow
            void PlaceParty()
            {
                float z = zoom;
                stage.localScale = new Vector3(z, z, 1);
                float sideFeet = FeetY + SideUp * z, sideH = SideH * z;
                if (myTag != null) myTag.anchoredPosition = new Vector2(540, FeetY - (0.075f * HeroH * z + 4.2f * Cw));
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? -1 : 1;
                    if (slots[i] != null && slots[i].Tag != null)
                        slots[i].Tag.anchoredPosition = new Vector2(540 + side * SideDx * z, sideFeet - (0.075f * sideH + 4.2f * Cw));
                    if (invitePills != null)
                    {
                        float dx = (shownCount <= 0 ? 34 * Cw : SideDx) * z; // alone: the INVITE pills a bit further from the hero
                        invitePills[i].anchoredPosition = new Vector2(540 + side * dx, sideFeet + sideH * 0.86f * 0.42f);
                    }
                }
            }
        }

        /// <summary>What Home's PLAY says it starts: "WITH YOUR PARTY" in a party (the old sub line), else the mode.</summary>
        static string HomePlaySub() => CurrentParty() != null ? "WITH YOUR PARTY" : PlaySubFor(homeMode, true);

        /// <summary>The party now: the editor's test party (BAM > Test Party) first, else the online room.</summary>
        static PartyView CurrentParty() => TestParty ?? OnlineHooks.Party?.Invoke();

        // Rickard 2026-10-30: "kan du göra i unity så det är en till i party": the party on Home without a second phone. Only the
        // look: PLAY still starts a match against bots, LEAVE PARTY ends the test.
        static PartyView TestParty;

        static void SetTestParty(int n)
        {
            if (n <= 0) { TestParty = null; return; }
            var p = new PartyView { Leader = true, Mode = 0, Bots = BamSettings.LastDifficulty, Count = n };
            int me = BamSettings.LastHero;
            for (int i = 0; i < n; i++)
                p.Mates[i] = new PartyMate { Name = i == 0 ? "Testvän" : "Testvän 2", Hero = (HeroId)((me + 1 + i) % HeroCatalog.Count), Ready = i == 0 };
            TestParty = p;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("BAM/Test Party/One friend")] static void TestOne() => SetTestParty(1);
        [UnityEditor.MenuItem("BAM/Test Party/Two friends (one still in a match)")] static void TestTwo() => SetTestParty(2);
        [UnityEditor.MenuItem("BAM/Test Party/Off")] static void TestOff() => SetTestParty(0);
#endif
    }
}
