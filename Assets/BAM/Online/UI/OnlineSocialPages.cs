using System;
using UnityEngine;

// Another player's public profile ("pprofile") with the friend buttons, Block and Report, and their confirmations.
// Ported from Speltest MenuSocial.cs (PagePublicProfile, HubProfileActions, HubSocialOverlays). Left out: INVITE TO PARTY and
// MESSAGE (no party or chat in BAM yet). New: the profile is the server's own data (GetProfile.js), so there is no "self-reported"
// note, and SEE AS OTHERS opens your own server profile instead of a local preview.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static string profileId; static PublicProfile profile; static bool profileLoading, profileFailed;
        internal static void OpenPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (playerId != profileId || profile == null) { profileId = playerId; profile = null; LoadProfile(); }
            Go("pprofile");
        }
        static async void LoadProfile()
        {
            string id = profileId;
            profileLoading = true; profileFailed = false;
            var p = await Server.GetProfile(id);
            if (id != profileId) return;
            profileLoading = false;
            profile = p; profileFailed = p == null;
        }
        static float PagePublicProfile(float x, float y, float w)
        {
            var p = profile;
            if (p == null)
            {
                y = H2("Player", null, x, y);
                if (profileLoading) return HubStateBox(null, "Loading…", false, null, null, x, y, w);
                return HubStateBox("hubProfRetry", profileFailed ? "Could not load this player." : "This player was not found.", true, "RETRY", LoadProfile, x, y, w);
            }
            bool me = p.playerId == Account.PlayerId;
            var t = p.totals ?? new Totals();
            var rel = Social.RelationTo(p.playerId);
            string name = string.IsNullOrEmpty(p.name) ? "Player" : p.name;
            float chh = cw(34); HubCardBack(x, y, w, chh);
            float ar = cw(8); if (!DrawPortrait(TopHero(t), x + cw(4) + ar, y + cw(4) + ar, ar)) HubAvatar(p.playerId, name, x + cw(4) + ar, y + cw(4) + ar, ar);
            float tx = x + cw(8) + ar * 2, tw = x + w - cw(4) - tx;
            float ns = cw(7.4f); ctx.font = Lil(ns); while (ns > cw(4.5f) && ctx.measureText(name) > tw) { ns -= cw(0.4f); ctx.font = Lil(ns); }
            Text(name, tx, y + cw(9), Lil(ns), HUB_TEXT);
            string sub = me ? "You" : PresenceOf(p.playerId);
            Text(sub + "   Level " + Progress.LevelOf(t.xp) + (p.rank > 0 ? "   #" + p.rank + " in Top 100" : ""), tx, y + cw(15.5f), Treb(cw(3), true), sub == "Online" ? HUB_ACCENT : HUB_DIM);
            int rk = RankInfo.RankOf(t.rating);
            HubRankEmblem(rk, x + cw(4) + cw(5), y + cw(27), cw(4.5f));
            Text(RankInfo.NAMES[rk].ToUpper(), x + cw(19), y + cw(25.5f), Lil(cw(4.6f)), RankInfo.COLORS[rk]);
            Text(t.rating + " Rating", x + cw(19), y + cw(30), Treb(cw(2.9f), true), HUB_TEXT);
            y += chh + cw(3);
            if (me) { Text("This is what other players see when they open your profile.", x + cw(1), y + cw(1), Treb(Min11(cw(2.9f)), true), HUB_ACCENT); y += cw(6); }
            else y = HubProfileActions(p.playerId, name, rel, x, y, w);
            y = HubSetHead("MATCH STATS", x, y);
            string[] vals = { t.matches.ToString(), t.wins.ToString(), t.losses.ToString(), WinRateText(t.wins, t.losses) };
            string[] labs = { "Matches", "Wins", "Losses", "Win rate" };
            float gap = cw(2), tlw = (w - 3 * gap) / 4, tlh = cw(15);
            for (int i = 0; i < 4; i++)
            {
                float tlx = x + i * (tlw + gap);
                ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(tlx, y, tlw, tlh, cw(3)); ctx.fill();
                Text(vals[i], tlx + tlw / 2, y + tlh * 0.42f, Lil(cw(5.6f)), HUB_TEXT, "center");
                Text(labs[i], tlx + tlw / 2, y + tlh * 0.78f, Treb(cw(2.6f), true), HUB_DIM, "center");
            }
            y += tlh + cw(3);
            {
                string[] fv = { t.kills.ToString(), t.deaths.ToString(), TimeText(t.seconds), t.bestStreak.ToString() };
                string[] fl = { "Kills", "Deaths", "Played", "Best streak" };
                float fh = cw(12);
                for (int i = 0; i < 4; i++)
                {
                    float tlx = x + i * (tlw + gap);
                    ctx.fillStyle = "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(tlx, y, tlw, fh, cw(3)); ctx.fill();
                    Text(fv[i], tlx + tlw / 2, y + fh * 0.4f, Lil(cw(4.6f)), HUB_TEXT, "center");
                    Text(fl[i], tlx + tlw / 2, y + fh * 0.78f, Treb(Min11(cw(2.5f)), true), HUB_DIM, "center");
                }
                y += fh + cw(3);
            }
            t.TopHeroes(topList, 3);
            if (topList.Count > 0)
            {
                y = HubSetHead("TOP HEROES", x, y);
                for (int i = 0; i < topList.Count; i++)
                {
                    var hs = topList[i]; float hh = cw(14), pp = cw(10);
                    ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, hh, cw(3)); ctx.fill();
                    ctx.fillStyle = "#1d2350"; ctx.beginPath(); ctx.roundRect(x + cw(3), y + (hh - pp) / 2, pp, pp, cw(2)); ctx.fill();
                    ctx.save(); ctx.beginPath(); ctx.roundRect(x + cw(3), y + (hh - pp) / 2, pp, pp, cw(2)); ctx.clip(); Contain(HeroIcon(hs.id), x + cw(3), y + (hh - pp) / 2, pp, pp); ctx.restore();
                    Text(hs.id, x + cw(16), y + hh * 0.38f, Lil(cw(4)), HUB_TEXT);
                    Text(hs.m + (hs.m == 1 ? " match" : " matches"), x + cw(16), y + hh * 0.7f, Treb(cw(2.7f), true), HUB_DIM);
                    Text(WinRateText(hs.w, hs.m - hs.w), x + w - cw(4), y + hh / 2, Lil(cw(4.4f)), HUB_ACCENT, "right");
                    y += hh + cw(1.5f);
                }
            }
            else { Text("This player has no finished matches yet.", x + cw(1), y + cw(2), Treb(cw(3)), HUB_DIM); y += cw(6); }
            return y;
        }
        static string PresenceOf(string id)
        {
            foreach (var f in Social.Friends) if (f.PlayerId == id) return f.Presence == Social.Presence.Online ? "Online" : f.Presence == Social.Presence.Playing ? "In a match" : "Offline";
            return "Player";
        }
        static float HubProfileActions(string id, string name, Social.Relation rel, float x, float y, float w)
        {
            float bh = cw(11), gap = cw(2), half = (w - gap) / 2;
            bool live = Social.Active;
            Action<string, string, float, float, bool, bool, Action> B = (key, label, bx, bw, primary, enabled, a) =>
            {
                bool dn = enabled && Down(key);
                if (primary && enabled) { ctx.fillStyle = VGrad(y, y + bh, "#4c6bff", "#3a3fd0"); ctx.beginPath(); ctx.roundRect(bx, y + (dn ? cw(0.5f) : 0), bw, bh, cw(3)); ctx.fill(); Text(label, bx + bw / 2, y + bh / 2 + (dn ? cw(0.5f) : 0), Lil(cw(3.8f)), "#fff", "center"); }
                else { string c = HubToggle(key, bx, y, bw, bh, cw(3), false, enabled); Text(label, bx + bw / 2, y + bh / 2, Lil(cw(3.8f)), c, "center"); }
                Btn(key, bx, y, bw, bh, enabled ? a : () => { });
            };
            switch (rel)
            {
                case Social.Relation.Outgoing:
                    B("hubPPending", "PENDING", x, half, false, false, null);
                    B("hubPCancel", "CANCEL REQUEST", x + half + gap, half, false, true, () => Run(Social.Cancel(id), null)); break;
                case Social.Relation.Incoming:
                    B("hubPAccept", "ACCEPT", x, half, true, true, () => Run(Social.Accept(id), null));
                    B("hubPDecline", "DECLINE", x + half + gap, half, false, true, () => Run(Social.Decline(id), null)); break;
                case Social.Relation.Friends:
                    B("hubPFriends", "FRIENDS", x, half, false, false, null);
                    B("hubPRemove", "REMOVE FRIEND", x + half + gap, half, false, true, () => hubSocialConfirm = "remove:" + id); break;
                case Social.Relation.Blocked:
                    B("hubPUnblock", "UNBLOCK", x, w, false, true, () => Run(Social.Unblock(id), null)); break;
                default:
                    B("hubPAdd", "ADD FRIEND", x, w, true, live, () => Run(Social.AddById(id), null)); break;
            }
            y += bh + cw(2);
            float sw = (w - gap) / 2, sh = cw(8.5f);
            if (rel != Social.Relation.Blocked)
            {
                string c1 = HubToggle("hubPBlock", x, y, sw, sh, cw(2.5f), false, live); Text("Block", x + sw / 2, y + sh / 2, Treb(cw(3), true), live ? "#ff8a8a" : c1, "center");
                Btn("hubPBlock", x, y, sw, sh, live ? (Action)(() => hubSocialConfirm = "block:" + id) : () => Toast("Friends is not available right now"));
            }
            string c2 = HubToggle("hubPReport", x + sw + gap, y, sw, sh, cw(2.5f), false, true); Text("Report", x + sw + gap + sw / 2, y + sh / 2, Treb(cw(3), true), "#ffb07a", "center");
            Btn("hubPReport", x + sw + gap, y, sw, sh, () => hubReportFor = id);
            return y + sh + cw(4);
        }

        // confirmations (remove, block) and the report reasons, over the public profile
        static string hubSocialConfirm, hubReportFor;
        static readonly (string id, string label)[] REPORT_REASONS = { ("name", "Rude or offensive name"), ("cheating", "Cheating"), ("other", "Something else") };
        static void HubSocialOverlays()
        {
            if (page != "pprofile" || (hubSocialConfirm == null && hubReportFor == null)) return;
            var p = profile; if (p == null) { hubSocialConfirm = hubReportFor = null; return; }
            string name = string.IsNullOrEmpty(p.name) ? "Player" : p.name;
            ctx.fillStyle = "rgba(0,0,0,0.6)"; ctx.fillRect(0, 0, W, H);
            Btn("hubSocScrim", 0, 0, W, H, () => { hubSocialConfirm = null; hubReportFor = null; });
            float dw = W - cw(12), dx = cw(6), ix = dx + cw(5), iw = dw - cw(10);
            if (hubReportFor != null)
            {
                var rs = REPORT_REASONS; float dh = cw(5) + cw(9) + rs.Length * cw(11) + cw(12) + cw(4), dy = H / 2 - dh / 2;
                ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(dx, dy, dw, dh, cw(4)); ctx.fill();
                ctx.strokeStyle = "#ffb07a"; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(dx, dy, dw, dh, cw(4)); ctx.stroke();
                Btn("hubSocBox", dx, dy, dw, dh, null);
                float yy = dy + cw(5);
                Text("Report " + name, ix, yy + cw(3), Lil(cw(5)), HUB_TEXT); yy += cw(9);
                foreach (var r in rs)
                {
                    string c = HubToggle("hubRep:" + r.id, ix, yy, iw, cw(9.5f), cw(3), false, true); Text(r.label, ix + cw(4), yy + cw(4.75f), Treb(cw(3.3f), true), c);
                    string rr = r.id, id = p.playerId; Btn("hubRep:" + r.id, ix, yy, iw, cw(9.5f), () => { hubReportFor = null; SendReport(id, rr); });
                    yy += cw(11);
                }
                string cc = HubToggle("hubRepCancel", ix, yy, iw, cw(10), cw(3), false, true); Text("CANCEL", ix + iw / 2, yy + cw(5), Lil(cw(4)), cc, "center");
                Btn("hubRepCancel", ix, yy, iw, cw(10), () => hubReportFor = null);
                return;
            }
            bool remove = hubSocialConfirm.StartsWith("remove:", StringComparison.Ordinal);
            var body = Wrap(remove ? "Remove " + name + " from your friends?" : "Block " + name + "? They can no longer send you friend requests, and they are removed from your friends.", iw, Treb(cw(3.3f), true));
            float h2 = cw(5) + cw(9) + body.Count * cw(4.6f) + cw(4) + cw(11) + cw(5), y2 = H / 2 - h2 / 2;
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(dx, y2, dw, h2, cw(4)); ctx.fill();
            ctx.strokeStyle = "#ff8a8a"; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(dx, y2, dw, h2, cw(4)); ctx.stroke();
            Btn("hubSocBox", dx, y2, dw, h2, null);
            float y3 = y2 + cw(5);
            Text(remove ? "Remove friend?" : "Block player?", ix, y3 + cw(3), Lil(cw(5)), HUB_TEXT); y3 += cw(9);
            for (int i = 0; i < body.Count; i++) Text(body[i], ix, y3 + cw(2.3f) + i * cw(4.6f), Treb(cw(3.3f), true), HUB_DIM);
            y3 += body.Count * cw(4.6f) + cw(4);
            float bw = (iw - cw(3)) / 2, bh = cw(11);
            string nc = HubToggle("hubSocNo", ix, y3, bw, bh, cw(3), false, true); Text("CANCEL", ix + bw / 2, y3 + bh / 2, Lil(cw(4)), nc, "center");
            Btn("hubSocNo", ix, y3, bw, bh, () => hubSocialConfirm = null);
            float rx = ix + bw + cw(3);
            ctx.fillStyle = Down("hubSocYes") ? "#c94848" : "#e05a5a"; ctx.beginPath(); ctx.roundRect(rx, y3, bw, bh, cw(3)); ctx.fill();
            Text(remove ? "REMOVE" : "BLOCK", rx + bw / 2, y3 + bh / 2, Lil(cw(4)), "#fff", "center");
            string pid = p.playerId;
            Btn("hubSocYes", rx, y3, bw, bh, () => { hubSocialConfirm = null; Run(remove ? Social.Remove(pid) : Social.Block(pid), null); });
        }
        static async void SendReport(string id, string reason)
        {
            bool ok = await Server.ReportPlayer(id, reason);
            Toast(ok ? "Report sent. Thank you." : "Could not send the report. Try again later.");
        }
    }
}
