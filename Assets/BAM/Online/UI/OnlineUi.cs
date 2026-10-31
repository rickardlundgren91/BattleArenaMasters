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
        // a small mint pencil leaning right, s long (a hint that the name is chosen by tapping the chip)
        static void Pencil(float x, float y, float s)
        {
            ctx.save(); ctx.translate(x, y); ctx.rotate(Mathf.PI / 4);
            float w = s * 0.42f;
            ctx.fillStyle = HUB_ACCENT; ctx.fillRect(-w / 2, -s, w, s * 1.3f);
            ctx.beginPath(); ctx.moveTo(-w / 2, s * 0.3f); ctx.lineTo(w / 2, s * 0.3f); ctx.lineTo(0, s * 0.75f); ctx.closePath(); ctx.fill();
            ctx.restore();
        }

        internal static void DrawChip(Ctx c, int pw, int ph)
        {
            ctx = c; W = 400; H = 400 * ph / (float)Math.Max(1, pw);
            float chipW = cw(40), chipH = cw(12), k = Math.Min(pw / chipW, ph / chipH);
            ctx.scale(k, k);
            var st = Account.State; var t = Progress.Shown;
            bool on = st == Account.Status.Ready || st == Account.Status.NeedsName || st == Account.Status.Offline && Account.Enabled;
            float px = 0, cy = chipH / 2;
            // 2026-10-31 (Rickard: the top got better in the mockup): straight on Home's dark top, no glass. The portrait in a mint
            // ring with the account level on a gold badge; the name (or "Choose a name" in mint with a pencil); the rank line;
            // under it the bar to the next level (the rank's own bar is on the profile page).
            float acx = px + chipH / 2, ar = chipH / 2 - cw(1), acy = cy;
            ctx.fillStyle = VGrad(acy - ar, acy + ar, "#4c6bff", "#7a3cff"); ctx.beginPath(); ctx.arc(acx, acy, ar, 0, 7); ctx.fill();
            if (!DrawPortrait(TopHero(t), acx, acy, ar)) Text(on && !string.IsNullOrEmpty(Account.Name) ? Account.Name.Substring(0, 1).ToUpper() : "?", acx, acy, Lil(ar), "#fff", "center");
            ctx.strokeStyle = HUB_ACCENT; ctx.lineWidth = cw(0.6f); ctx.beginPath(); ctx.arc(acx, acy, ar + cw(0.2f), 0, 7); ctx.stroke();
            int rk = RankInfo.RankOf(t.rating);
            float tx = px + chipH + cw(2.4f), tw = px + chipW - cw(3) - tx;
            if (on)
            {
                float bx = acx + ar * 0.78f, by = acy + ar * 0.74f, br = cw(2.3f);
                ctx.fillStyle = HUB_BG0; ctx.beginPath(); ctx.arc(bx, by, br, 0, 7); ctx.fill();
                ctx.strokeStyle = HUB_GOLD; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.arc(bx, by, br, 0, 7); ctx.stroke();
                string lv = Progress.LevelOf(t.xp).ToString();
                Text(lv, bx, by + cw(0.1f), Lil(cw(lv.Length > 1 ? 2.2f : 2.7f)), "#ffd166", "center");
            }
            bool noName = on && string.IsNullOrEmpty(Account.Name);
            string nm = on ? (noName ? "Choose a name" : Account.Name) : "Play online";
            float pen = noName ? cw(3.4f) : 0; // room for the pencil
            float ns = 3.8f; ctx.font = Lil(cw(ns));
            while (ns > 2.9f && ctx.measureText(nm) > tw - pen) { ns -= 0.1f; ctx.font = Lil(cw(ns)); }
            while (nm.Length > 3 && ctx.measureText(nm) > tw - pen) nm = nm.Substring(0, nm.Length - 2) + "…";
            float nw = ctx.measureText(nm);
            Text(nm, tx, cy - cw(2.8f), Lil(cw(ns)), noName ? HUB_ACCENT : HUB_TEXT, shadow: "rgba(0,0,0,0.6)", sh: cw(0.3f));
            if (noName) Pencil(tx + nw + cw(1.9f), cy - cw(2.9f), cw(1.5f));
            if (on)
            {
                float ex = tx + cw(1.3f), ey = cy + cw(1.1f);
                HubRankEmblem(rk, ex, ey, cw(1.3f));
                string rl = RankInfo.NAMES[rk] + " · " + t.rating;
                string rf = Treb(Min11(cw(2.7f)), true);
                ctx.font = rf; while (rl.Length > 3 && ctx.measureText(rl) > tw - cw(3.4f)) rl = rl.Substring(0, rl.Length - 2) + "…";
                Text(rl, tx + cw(3.4f), ey, rf, RankInfo.COLORS[rk], shadow: "rgba(0,0,0,0.85)", sh: cw(0.35f), stroke: "rgba(6,8,26,0.9)", sw: cw(0.35f));
                Progress.IntoLevel(t.xp, out int have, out int need);
                float bY = cy + cw(4.2f), bH = cw(1), barW = Math.Min(tw, cw(24)), f = need > 0 ? Mathf.Clamp01(have / (float)need) : 0;
                ctx.fillStyle = "rgba(255,255,255,0.16)"; ctx.beginPath(); ctx.roundRect(tx, bY - bH / 2, barW, bH, bH / 2); ctx.fill();
                if (f > 0) { ctx.fillStyle = HUB_ACCENT; ctx.beginPath(); ctx.roundRect(tx, bY - bH / 2, Math.Max(bH, barW * f), bH, bH / 2); ctx.fill(); }
            }
            else Text("Friends and Top 100", tx, cy + cw(1.1f), Treb(Min11(cw(2.7f)), true), HUB_ACCENT, shadow: "rgba(0,0,0,0.85)", sh: cw(0.35f));
        }
    }
}
