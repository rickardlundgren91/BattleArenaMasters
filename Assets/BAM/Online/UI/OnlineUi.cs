using System;
using BAM.Audio;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Online.UI
{
    /// <summary>
    /// The online pages' one entry point for the rest of the game (only in builds with BAM_ONLINE). The game's menus call:
    ///   Boot, after BAM.Online.Online.Boot():  OnlineUi.Init();
    ///   Home top bar:                          OnlineUi.HomeChip(parentRect);   (tap opens the profile)
    ///   Home friends button:                   OnlineUi.OpenFriends();
    ///   Settings, an "Account" row:            OnlineUi.OpenAccount();
    ///   Home MODES, "Play with friends":       OnlineUi.OpenPlayFriends();
    ///   Home MODES, "Online match":            OnlineUi.OnlineMatch();
    ///   After the tutorial, once:              OnlineUi.AskToGoOnline();
    ///   Android Back, before Screens.HandleBack: if (OnlineUi.Back()) return;
    /// </summary>
    public static class OnlineUi
    {
        const string AskedKey = "bam.online.asked";
        static OnlineView view;

        public static void Init()
        {
            if (view != null) return;
            view = OnlineView.Create();
            Account.Changed += () => { OnlineMenu.AccountChanged(); Dirty(); };
            Progress.Changed += Dirty;
            Social.Changed += Dirty;
            Social.Say += OnlineMenu.Toast;
            BAM.Online.Mp.MpSocial.Say += OnlineMenu.Toast;
        }
        static void Dirty() { if (view != null) view.chipDirty = true; }
        /// <summary>The Home chip and the party's pictures are drawn again.</summary>
        internal static void Repaint() => Dirty();

        /// <summary>True while an online page covers the menus.</summary>
        public static bool IsOpen => OnlineMenu.page != null || OnlineMenu.InviteShown;
        static bool SignedIn => Account.State == Account.Status.Ready || Account.State == Account.Status.NeedsName || Account.State == Account.Status.Offline && Account.Enabled;

        public static void OpenProfile() { Init(); OnlineMenu.Close(); OnlineMenu.Go(SignedIn ? "profile" : "golive"); }
        public static void OpenRank() { Init(); OnlineMenu.Close(); OnlineMenu.Go("rank"); }
        public static void OpenFriends() { Init(); OnlineMenu.Close(); OnlineMenu.OpenFriends(); }
        /// <summary>Friends online now (not in a match), for the badge on Home's FRIENDS button.</summary>
        public static int FriendsOnline() { int n = 0; foreach (var f in Social.Friends) if (f.Presence == Social.Presence.Online) n++; return n; }
        /// <summary>Home's + INVITE: the invite panel (friends first, then everyone online), or the sign-in first.</summary>
        public static void OpenInvite()
        {
            Init(); OnlineMenu.Close();
            if (Account.State != Account.Status.Ready) { OnlineMenu.Go(Account.State == Account.Status.NeedsName ? "hubname" : "golive"); return; }
            OnlineMenu.OpenInvite();
        }
        /// <summary>Other players searching ONLINE MATCH now (their "here I am" sign says queue), for the light on ONLINE MATCH.</summary>
        public static int Searching()
        {
            if (Account.State != Account.Status.Ready) return 0;
            int n = 0; foreach (var r in BAM.Online.Mp.MpPresence.Online) if (r.State == BAM.Online.Mp.PresenceState.Queue) n++;
            return n;
        }
        public static void OpenAccount() { Init(); OnlineMenu.Close(); OnlineMenu.Go("account"); }
        /// <summary>Play with friends: the invite panel; the party itself is on Home (OnlineParty.cs).</summary>
        public static void OpenPlayFriends() => OpenInvite();
        /// <summary>ONLINE MATCH from Home: straight into the queue, on the room page (CANCEL leaves the queue).</summary>
        public static void OnlineMatch()
        {
            Init(); OnlineMenu.Close();
            if (Account.State != Account.Status.Ready) { OnlineMenu.Go(Account.State == Account.Status.NeedsName ? "hubname" : "golive"); return; }
            BAM.Online.Mp.MpRoom.QuickMatch(); OnlineMenu.Go("mp");
        }
        public static void OpenName() { Init(); OnlineMenu.Close(); OnlineMenu.Go(Account.State == Account.Status.Ready || Account.State == Account.Status.NeedsName ? "hubname" : "golive"); }

        /// <summary>The first-time question (after the tutorial): asked once, never again after Not now. True when it was shown.</summary>
        public static bool AskToGoOnline()
        {
            if (Account.Enabled || PlayerPrefs.GetInt(AskedKey, 0) == 1) return false;
            PlayerPrefs.SetInt(AskedKey, 1); PlayerPrefs.Save();
            Init(); OnlineMenu.Close(); OnlineMenu.Go("golive");
            return true;
        }

        /// <summary>Android Back: closes the innermost online layer. False when no online page is open (the game handles Back).</summary>
        public static bool Back()
        {
            if (!IsOpen) return false;
            BamAudio.Play(SfxId.UiBack);
            OnlineMenu.Back();
            return true;
        }

        /// <summary>
        /// The old Home profile chip (Speltest MenuHome3.HubTopBar3): portrait, rank badge, name and rank line with its bar. Fills
        /// the given rect (the old chip is cw(40) x cw(12)); a tap opens the profile, or the sign-in when not online.
        /// </summary>
        public static RectTransform HomeChip(RectTransform parent)
        {
            Init();
            var rt = UiKit.NewRect("Online Chip", parent);
            UiKit.Stretch(rt);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); OpenProfile(); });
            view.AddChip(raw);
            return rt;
        }

        /// <summary>A party picture for Home (OnlineHooks.Paint): a name tag ("tag0" the player's, "tag1", "tag2") or "leave".</summary>
        public static RectTransform Paint(RectTransform parent, string piece)
        {
            Init();
            var rt = UiKit.NewRect("Online " + piece, parent);
            UiKit.Stretch(rt);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            view.AddChip(raw, piece);
            return rt;
        }
    }

    static partial class OnlineMenu
    {
        /// <summary>The Home chip into a texture of pw x ph pixels (MenuHome3.HubTopBar3 in its ONE_PLAY look, on the top bar).</summary>
        internal static void DrawChip(Ctx c, int pw, int ph)
        {
            ctx = c; W = 400; H = 400 * ph / (float)Math.Max(1, pw);
            float chipW = cw(40), chipH = cw(12), k = Math.Min(pw / chipW, ph / chipH);
            ctx.scale(k, k);
            var st = Account.State; var t = Progress.Shown;
            bool on = st == Account.Status.Ready || st == Account.Status.NeedsName || st == Account.Status.Offline && Account.Enabled;
            float px = 0, cy = chipH / 2;
            // BAM's Home is the old ONE_PLAY look: the chip sits on the top bar's glass, so it has no background of its own
            float acx = px + chipH / 2, ar = chipH / 2 - cw(1), acy = cy;
            ctx.fillStyle = VGrad(acy - ar, acy + ar, "#4c6bff", "#7a3cff"); ctx.beginPath(); ctx.arc(acx, acy, ar, 0, 7); ctx.fill();
            if (!DrawPortrait(TopHero(t), acx, acy, ar)) Text(on && !string.IsNullOrEmpty(Account.Name) ? Account.Name.Substring(0, 1).ToUpper() : "?", acx, acy, Lil(ar), "#fff", "center");
            int rk = RankInfo.RankOf(t.rating);
            float tx = px + chipH + cw(2.4f), tw = px + chipW - cw(3) - tx;
            if (on)
            {
                ctx.fillStyle = HUB_BG0; ctx.beginPath(); ctx.arc(acx + ar * 0.78f, acy + ar * 0.72f, cw(2.3f), 0, 7); ctx.fill();
                HubRankEmblem(rk, acx + ar * 0.78f, acy + ar * 0.72f, cw(1.9f));
            }
            string nm = on ? (string.IsNullOrEmpty(Account.Name) ? "Choose a name" : Account.Name) : "Play online";
            // shrink a long name a little before cutting it ("Choose a name" was cut to "Choose a na…")
            float ns = 3.8f; ctx.font = Lil(cw(ns));
            while (ns > 2.9f && ctx.measureText(nm) > tw) { ns -= 0.1f; ctx.font = Lil(cw(ns)); }
            while (nm.Length > 3 && ctx.measureText(nm) > tw) nm = nm.Substring(0, nm.Length - 2) + "…";
            Text(nm, tx, cy - cw(2.8f), Lil(cw(ns)), HUB_TEXT);
            string rl = on ? "Lv " + Progress.LevelOf(t.xp) + " · " + RankInfo.NAMES[rk] + " · " + t.rating : "Friends and Top 100";
            string rf = Treb(Min11(cw(2.7f)), true);
            ctx.font = rf; while (rl.Length > 3 && ctx.measureText(rl) > tw) rl = rl.Substring(0, rl.Length - 2) + "…";
            Text(rl, tx, cy + cw(1.1f), rf, on ? RankInfo.COLORS[rk] : HUB_ACCENT, shadow: "rgba(0,0,0,0.85)", sh: cw(0.35f), stroke: "rgba(6,8,26,0.9)", sw: cw(0.35f)); // a dark edge, so it reads on the glass
            if (on)
            {
                float bY = cy + cw(4), bH = cw(1), barW = tw;
                ctx.fillStyle = "rgba(255,255,255,0.14)"; ctx.beginPath(); ctx.roundRect(tx, bY - bH / 2, barW, bH, bH / 2); ctx.fill();
                float f = RankInfo.Progress(t.rating); if (f > 0) { ctx.fillStyle = RankInfo.COLORS[rk]; ctx.beginPath(); ctx.roundRect(tx, bY - bH / 2, Math.Max(bH, barW * f), bH, bH / 2); ctx.fill(); }
            }
        }
    }
}
