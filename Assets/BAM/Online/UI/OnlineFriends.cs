using System;
using System.Collections.Generic;
using UnityEngine;

// The Friends panel on Home (page "friends"): the old v2.5 panel from the right edge, with the FRIENDS / REQUESTS tabs (v3) and
// the Add friend view. Ported from Speltest MenuHome.cs (HubFriends, HubFriendList, HubFriendRow, HubFriendMenu, HubRemoveConfirm,
// HubAddFriend) and MenuFriends3.cs (HubFriendPill3, HubFriendTabs, HubFriendsOther) and MenuSocial.cs (HubRequestRow).
// Left out: the party line, INVITE / WATCH, Message and the RECENT tab (no online matches yet). Changed: a friend is added by
// their unique player name (Speltest asked for a Player ID), and sent requests are listed under REQUESTS with a Cancel.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static string friendsView = "list", friendMenu, friendRemove, friendNameDraft = "", friendReqMsg; static bool friendReqOk, friendSending;
        static int friendsTab; // 0 friends, 1 requests
        static float HubFriendPillW => cw(21);
        internal static void OpenFriends() { friendsView = "list"; friendMenu = friendRemove = friendReqMsg = null; friendsTab = Social.Incoming.Count > 0 ? 1 : 0; Go("friends"); }
        static void HubFriendsBack()
        {
            if (friendRemove != null) friendRemove = null;
            else if (friendMenu != null) friendMenu = null;
            else if (friendsView != "list") { friendsView = "list"; friendReqMsg = null; EndInput(); }
            else HubBack();
        }
        static void HubFriends()
        {
            float topH = cw(16); // under Home's top bar
            float pw = cw(78), px = W - pw, pyy = topH + cw(2), pbot = H - cw(24), ph = pbot - pyy, ix = px + cw(5), iw = pw - cw(9);
            viewTop = viewBot = contentH = 0;
            ctx.fillStyle = "rgba(0,0,0,0.45)"; ctx.fillRect(0, 0, W, H);
            Btn("hubFriendsScrim", 0, 0, W, H, () => { EndInput(); HubBack(); });
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(px, pyy, pw + cw(4), ph, cw(4)); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(px, pyy, pw + cw(4), ph, cw(4)); ctx.stroke();
            Btn("hubFriendsPanel", px, pyy, pw, ph, () => friendMenu = null);
            if (Account.State != Account.Status.Ready) HubFriendsSignIn(ix, pyy + cw(4), iw);
            else if (friendsView == "add") HubAddFriend(ix, pyy + cw(4), iw);
            else HubFriendList(ix, pyy + cw(4), iw, pbot - cw(3));
        }
        // not signed in: the panel says why and offers the sign-in (new: Speltest said "needs a game server")
        static void HubFriendsSignIn(float ix, float y, float iw)
        {
            HubPeople(ix + cw(2.5f), y + cw(3.5f), cw(2.4f), HUB_ACCENT);
            Text("Friends", ix + cw(8), y + cw(3.5f), Lil(cw(5.2f)), HUB_TEXT);
            y += cw(10);
            bool needsName = Account.State == Account.Status.NeedsName;
            var msg = Wrap(needsName ? "Choose your player name first. Friends find you by it." : "Sign in with Google Play Games to add friends and see when they are online.", iw - cw(6), Treb(cw(3.2f)));
            float boxH = cw(4) + msg.Count * cw(4.4f);
            ctx.fillStyle = "rgba(255,255,255,0.05)"; ctx.beginPath(); ctx.roundRect(ix, y, iw, boxH, cw(3)); ctx.fill();
            for (int i = 0; i < msg.Count; i++) Text(msg[i], ix + cw(3), y + cw(2) + cw(2.2f) + i * cw(4.4f), Treb(cw(3.2f)), HUB_DIM);
            y += boxH + cw(4);
            float sh = cw(11), sdy = Down("hubFrSignIn") ? cw(0.5f) : 0;
            ctx.fillStyle = VGrad(y + sdy, y + sdy + sh, "#4c6bff", "#3a3fd0"); ctx.beginPath(); ctx.roundRect(ix, y + sdy, iw, sh, cw(3)); ctx.fill();
            Text(needsName ? "CHOOSE NAME" : "SIGN IN", ix + iw / 2, y + sdy + sh / 2, Lil(cw(3.8f)), "#fff", "center");
            Btn("hubFrSignIn", ix, y, iw, sh, () => Go(needsName ? "hubname" : "golive"));
        }
        static void HubFriendList(float ix, float y, float iw, float bottom)
        {
            var list = Social.Friends;
            HubPeople(ix + cw(2.5f), y + cw(3.5f), cw(2.4f), HUB_ACCENT);
            Text("Friends", ix + cw(8), y + cw(3.5f), Lil(cw(5.2f)), HUB_TEXT);
            // "2/7 online" (tester 2026-11-13: "man borde ändå se hur många som är online"), the same presence the rows show
            int onl = 0; foreach (var f in list) if (BAM.Online.Mp.MpSocial.Seen(f) != Social.Presence.Offline) onl++;
            ctx.font = Lil(cw(5.2f)); float cx0 = ix + cw(8) + ctx.measureText("Friends") + cw(2);
            string cnt = list.Count == 0 ? "0" : onl + "/" + list.Count + " online";
            Text(cnt, cx0, y + cw(3.7f), Treb(cw(3.2f), true), onl > 0 ? HUB_ACCENT : HUB_DIM);
            float ab = cw(11), ax = ix + iw - ab, ady = Down("hubFriendsAdd") ? cw(0.4f) : 0;
            ctx.fillStyle = "rgba(62,230,176,0.14)"; ctx.beginPath(); ctx.arc(ax + ab / 2, y + cw(3.5f) + ady, ab / 2 - cw(0.6f), 0, 7); ctx.fill();
            HubUserPlus(ax + ab / 2, y + cw(3.5f) + ady, cw(2.3f), HUB_ACCENT);
            Btn("hubFriendsAdd", ax, y + cw(3.5f) - ab / 2, ab, ab, () => { friendsView = "add"; friendMenu = null; friendReqMsg = null; });
            y += cw(10);
            int req = Social.Incoming.Count;
            HubSegments("hubFrTabs", ix, y, iw, cw(9), new[] { "FRIENDS", "REQUESTS" + (req > 0 ? " (" + req + ")" : "") }, friendsTab,
                i => { friendsTab = i; pageScroll = 0; friendMenu = null; }, cw(2.7f));
            y += cw(11.5f);
            float listBot = bottom - cw(1.5f);
            if (friendsTab != 0) { HubFriendsOther(ix, y, iw, listBot); return; }
            if (list.Count == 0)
            {
                var msg = Wrap(Social.Active ? "No friends yet. Tap the add button to send a friend request." : "Friends is not available right now. Check your connection.", iw - cw(6), Treb(cw(3.2f)));
                float boxH = cw(4) + msg.Count * cw(4.4f);
                ctx.fillStyle = "rgba(255,255,255,0.05)"; ctx.beginPath(); ctx.roundRect(ix, y, iw, boxH, cw(3)); ctx.fill();
                for (int i = 0; i < msg.Count; i++) Text(msg[i], ix + cw(3), y + cw(2) + cw(2.2f) + i * cw(4.4f), Treb(cw(3.2f)), HUB_DIM);
                y += boxH + cw(4);
                Text("Your name: " + (Account.Name ?? "not set"), ix, y + cw(2), Treb(Min11(cw(2.9f))), HUB_DIM);
                return;
            }
            float listTop = y, rh = cw(14), gap = cw(1.2f);
            viewTop = listTop; viewBot = listBot; contentH = list.Count * (rh + gap); ClampScroll();
            ctx.save(); ctx.beginPath(); ctx.rect(ix - cw(1), listTop, iw + cw(2), listBot - listTop); ctx.clip();
            clipOn = true; clipTop = listTop; clipBot = listBot; scrollOff = pageScroll; ctx.translate(0, -pageScroll);
            float menuRowY = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i]; float ry = listTop + i * (rh + gap);
                if (ry + rh < listTop + pageScroll - rh || ry > listBot + pageScroll + rh) continue;
                if (f.PlayerId == friendMenu) menuRowY = ry - pageScroll;
                HubFriendRow(f, ix, ry, iw, rh);
            }
            ctx.restore(); clipOn = false; scrollOff = 0;
            if (contentH > listBot - listTop)
            {
                float vis = listBot - listTop, bh = Math.Max(cw(6), vis * vis / contentH), by = listTop + (vis - bh) * pageScroll / Math.Max(1, contentH - vis);
                ctx.fillStyle = "rgba(154,162,214,0.35)"; ctx.beginPath(); ctx.roundRect(ix + iw + cw(1.2f), by, cw(0.8f), bh, cw(0.4f)); ctx.fill();
            }
            if (friendMenu != null && menuRowY >= 0 && FriendById(friendMenu, out var mf)) HubFriendMenu(mf, ix, iw, menuRowY + rh, listBot);
            if (friendRemove != null && FriendById(friendRemove, out var rf))
            {
                ctx.fillStyle = "rgba(0,0,0,0.4)"; ctx.fillRect(ix - cw(5), listTop - cw(14), iw + cw(13), bottom - listTop + cw(17));
                Btn("hubFrRemoveScrim", ix - cw(5), listTop - cw(14), iw + cw(9), bottom - listTop + cw(17), () => friendRemove = null);
                HubRemoveConfirm(rf, ix, iw, listTop);
            }
        }
        static bool FriendById(string id, out Social.Person p)
        {
            foreach (var f in Social.Friends) if (f.PlayerId == id) { p = f; return true; }
            p = default; return false;
        }
        static string PresenceCol(Social.Presence p) => p == Social.Presence.Online ? HUB_ACCENT : p == Social.Presence.Playing ? "#ffb43a" : "#6b7199";
        static string PresenceText(Social.Presence p) => p == Social.Presence.Online ? "Online" : p == Social.Presence.Playing ? "In a match" : "Offline";
        static void HubFriendRow(Social.Person f, float x, float y, float w, float h)
        {
            bool open = friendMenu == f.PlayerId, dn = Down("hubFr:" + f.PlayerId);
            ctx.fillStyle = open || dn ? "rgba(62,230,176,0.10)" : "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            float ar = cw(4.4f), acx = x + cw(2) + ar, acy = y + h / 2;
            HubAvatar(f.PlayerId, f.Name, acx, acy, ar);
            string pc = PresenceCol(BAM.Online.Mp.MpSocial.Seen(f));
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.6f), 0, 7); ctx.fill();
            ctx.fillStyle = pc; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.1f), 0, 7); ctx.fill();
            float nx = acx + ar + cw(2.5f), actW = HubFriendPillW, nw = x + w - actW - cw(2) - nx;
            string nm = f.Name ?? ""; ctx.font = Lil(cw(3.8f)); while (nm.Length > 3 && ctx.measureText(nm) > nw) nm = nm.Substring(0, nm.Length - 2) + "…";
            Text(nm, nx, y + h * 0.36f, Lil(cw(3.8f)), BAM.Online.Mp.MpSocial.Seen(f) == Social.Presence.Offline ? HUB_DIM : HUB_TEXT);
            Text(PresenceText(BAM.Online.Mp.MpSocial.Seen(f)), nx, y + h * 0.7f, Treb(Min11(cw(2.8f)), true), pc);
            string fid = f.PlayerId;
            Btn("hubFr:" + fid, x, y, x + w - actW - cw(1) - x, h, () => { friendMenu = friendMenu == fid ? null : fid; });
            HubFriendPill3(f, x + w - actW - cw(1), y, actW, h);
        }
        // the word at the right of a friend's row (MenuFriends3.HubFriendPill3): INVITE when the friend has the game open and is
        // free (the online list, MpSocial), else ONLINE, IN MATCH or OFFLINE
        static void HubFriendPill3(Social.Person f, float x, float y, float w, float h)
        {
            float ph = cw(8), py = y + (h - ph) / 2;
            string label, col;
            bool otherVersion = !string.IsNullOrEmpty(f.Version) && f.Version != Social.GameVersion;
            if (!otherVersion && BAM.Online.Mp.MpSocial.CanInvite(f.PlayerId))
            {
                bool sent = BAM.Online.Mp.MpSocial.IsInvited(f.PlayerId), dn = !sent && Down("hubFrInv:" + f.PlayerId);
                ctx.fillStyle = sent ? "rgba(255,255,255,0.05)" : dn ? "#24a050" : "#2fae5b"; ctx.beginPath(); ctx.roundRect(x, py, w, ph, ph / 2); ctx.fill();
                Text(sent ? "INVITED" : "INVITE", x + w / 2, py + ph / 2, Treb(Min11(cw(2.6f)), true), sent ? HUB_ACCENT : "#fff", "center");
                string id = f.PlayerId, nm = f.Name;
                if (!sent) Btn("hubFrInv:" + id, x, py, w, ph, () => InviteSomeone(id, nm));
                return;
            }
            if (otherVersion && BAM.Online.Mp.MpSocial.Seen(f) != Social.Presence.Offline)
            {
                // another game version: they cannot play together until both have the same one
                string id = f.PlayerId, nm = f.Name, v = f.Version;
                label = "OTHER VERSION"; col = "#ffb0b0";
                Btn("hubFrVer:" + id, x, py, w, ph, () => Toast(nm + " has version " + v + ", you have " + Social.GameVersion + ". Both need the same version."));
            }
            else if (BAM.Online.Mp.MpSocial.Seen(f) == Social.Presence.Playing) { label = "IN MATCH"; col = "rgba(154,162,214,0.6)"; }
            else if (BAM.Online.Mp.MpSocial.Seen(f) == Social.Presence.Offline) { label = "OFFLINE"; col = "rgba(154,162,214,0.5)"; }
            else { label = "ONLINE"; col = HUB_ACCENT; }
            ctx.fillStyle = "rgba(255,255,255,0.05)"; ctx.beginPath(); ctx.roundRect(x, py, w, ph, ph / 2); ctx.fill();
            float fs = Min11(cw(2.6f)); ctx.font = Treb(fs, true); while (fs > cw(2) && ctx.measureText(label) > w - cw(2)) { fs -= cw(0.1f); ctx.font = Treb(fs, true); }
            Text(label, x + w / 2, py + ph / 2, Treb(fs, true), col, "center");
        }
        // the menu under a friend's row: View Profile, Remove Friend (MenuHome.HubFriendMenu without Invite and Message)
        static void HubFriendMenu(Social.Person f, float ix, float iw, float y, float listBot)
        {
            float mw = iw - cw(8), mx = ix + cw(8), ih = cw(10), mh = ih * 2 + cw(2);
            if (y + mh > listBot) y = Math.Max(cw(20), y - mh - cw(14) - cw(1.2f));
            ctx.fillStyle = "#0d1030"; ctx.beginPath(); ctx.roundRect(mx, y, mw, mh, cw(3)); ctx.fill();
            ctx.strokeStyle = HUB_ACCENT; ctx.lineWidth = cw(0.35f); ctx.beginPath(); ctx.roundRect(mx, y, mw, mh, cw(3)); ctx.stroke();
            Btn("hubFrMenu", mx, y, mw, mh, null);
            var items = new[] { ("hubFrMenuProfile", "View Profile"), ("hubFrMenuRemove", "Remove Friend") };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i]; float yy = y + cw(1) + i * ih; bool dn = Down(it.Item1);
                if (dn) { ctx.fillStyle = "rgba(255,255,255,0.08)"; ctx.beginPath(); ctx.roundRect(mx + cw(1), yy, mw - cw(2), ih, cw(2)); ctx.fill(); }
                Text(it.Item2, mx + cw(4), yy + ih / 2, Treb(cw(3.3f), true), it.Item1 == "hubFrMenuRemove" ? "#ff8a8a" : HUB_TEXT);
                string k = it.Item1, id = f.PlayerId;
                Btn(k, mx, yy, mw, ih, () =>
                {
                    if (k == "hubFrMenuProfile") { friendMenu = null; OpenPlayer(id); }
                    else { friendRemove = id; friendMenu = null; }
                });
            }
        }
        static void HubRemoveConfirm(Social.Person f, float ix, float iw, float top)
        {
            var body = Wrap("Remove " + f.Name + " from your friends? You can send a new friend request later.", iw - cw(8), Treb(cw(3.2f), true));
            float dh = cw(4) + cw(7) + body.Count * cw(4.4f) + cw(3) + cw(10) + cw(4), dy = top + cw(4);
            ctx.fillStyle = "#0d1030"; ctx.beginPath(); ctx.roundRect(ix, dy, iw, dh, cw(3.5f)); ctx.fill();
            ctx.strokeStyle = "#ff8a8a"; ctx.lineWidth = cw(0.35f); ctx.beginPath(); ctx.roundRect(ix, dy, iw, dh, cw(3.5f)); ctx.stroke();
            Btn("hubFrRemoveBox", ix, dy, iw, dh, null);
            float yy = dy + cw(4);
            Text("Remove friend?", ix + cw(4), yy + cw(2.5f), Lil(cw(4.6f)), HUB_TEXT); yy += cw(7);
            for (int i = 0; i < body.Count; i++) Text(body[i], ix + cw(4), yy + cw(2.2f) + i * cw(4.4f), Treb(cw(3.2f), true), HUB_DIM);
            yy += body.Count * cw(4.4f) + cw(3);
            float bw = (iw - cw(11)) / 2, bh = cw(10);
            string nc = HubToggle("hubFrRemoveNo", ix + cw(4), yy, bw, bh, cw(2.5f), false, true);
            Text("CANCEL", ix + cw(4) + bw / 2, yy + bh / 2, Lil(cw(3.8f)), nc, "center");
            Btn("hubFrRemoveNo", ix + cw(4), yy, bw, bh, () => friendRemove = null);
            float rx = ix + cw(7) + bw;
            ctx.fillStyle = Down("hubFrRemoveYes") ? "#c94848" : "#e05a5a"; ctx.beginPath(); ctx.roundRect(rx, yy, bw, bh, cw(2.5f)); ctx.fill();
            Text("REMOVE", rx + bw / 2, yy + bh / 2, Lil(cw(3.8f)), "#fff", "center");
            string id = f.PlayerId;
            Btn("hubFrRemoveYes", rx, yy, bw, bh, () => { friendRemove = null; Run(Social.Remove(id), null); });
        }
        // REQUESTS: requests to you (accept / decline), then the ones you sent (cancel)
        static void HubFriendsOther(float ix, float y, float iw, float listBot)
        {
            var inc = Social.Incoming; var outg = Social.Outgoing;
            float rh = cw(12), gap = cw(1.2f);
            if (inc.Count + outg.Count == 0)
            {
                Text("No friend requests", ix + iw / 2, y + cw(8), Lil(cw(4)), HUB_DIM, "center");
                return;
            }
            viewTop = y; viewBot = listBot; contentH = (inc.Count + outg.Count) * (rh + gap); ClampScroll();
            ctx.save(); ctx.beginPath(); ctx.rect(ix - cw(1), y, iw + cw(2), listBot - y); ctx.clip();
            clipOn = true; clipTop = y; clipBot = listBot; scrollOff = pageScroll; ctx.translate(0, -pageScroll);
            for (int i = 0; i < inc.Count; i++) HubRequestRow(inc[i], ix, y + i * (rh + gap), iw, rh, true);
            for (int i = 0; i < outg.Count; i++) HubRequestRow(outg[i], ix, y + (inc.Count + i) * (rh + gap), iw, rh, false);
            ctx.restore(); clipOn = false; scrollOff = 0;
        }
        static void HubRequestRow(Social.Person p, float x, float y, float w, float h, bool incoming)
        {
            ctx.fillStyle = incoming ? "rgba(255,210,87,0.07)" : "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            HubAvatar(p.PlayerId, p.Name, x + cw(2) + cw(3.6f), y + h / 2, cw(3.6f));
            string nm = p.Name ?? ""; ctx.font = Lil(cw(3.4f)); while (nm.Length > 3 && ctx.measureText(nm) > w - cw(36)) nm = nm.Substring(0, nm.Length - 2) + "…";
            Text(nm, x + cw(11), y + h * 0.38f, Lil(cw(3.4f)), HUB_TEXT);
            Text(incoming ? "Wants to be friends" : "Request sent", x + cw(11), y + h * 0.72f, Treb(cw(2.4f), true), incoming ? "#ffd98a" : HUB_DIM);
            float bs = cw(9), bx = x + w - bs * 2 - cw(2); string id = p.PlayerId;
            if (incoming)
            {
                ctx.fillStyle = "rgba(62,230,176,0.2)"; ctx.beginPath(); ctx.arc(bx + bs / 2, y + h / 2, bs / 2 - cw(0.5f), 0, 7); ctx.fill(); HubCheck(bx + bs / 2, y + h / 2, cw(1.6f), HUB_ACCENT);
                Btn("hubReqYes:" + id, bx, y + (h - bs) / 2, bs, bs, () => Run(Social.Accept(id), null));
            }
            bx += bs + cw(1);
            ctx.fillStyle = "rgba(255,138,138,0.15)"; ctx.beginPath(); ctx.arc(bx + bs / 2, y + h / 2, bs / 2 - cw(0.5f), 0, 7); ctx.fill(); HubCross(bx + bs / 2, y + h / 2, cw(1.4f), "#ff8a8a");
            Btn("hubReqNo:" + id, bx, y + (h - bs) / 2, bs, bs, () => Run(incoming ? Social.Decline(id) : Social.Cancel(id), null));
        }
        // Add friend: a friend request by the friend's player name (names are unique, so the name is enough)
        static void HubAddFriend(float ix, float y, float iw)
        {
            float bb = cw(10), bdy = Down("hubFrAddBack") ? cw(0.4f) : 0;
            ctx.fillStyle = "rgba(255,255,255,0.08)"; ctx.beginPath(); ctx.arc(ix + bb / 2, y + cw(3.5f) + bdy, bb / 2 - cw(0.6f), 0, 7); ctx.fill();
            Text("‹", ix + bb / 2, y + cw(3.2f) + bdy, Lil(cw(6)), HUB_TEXT, "center");
            Btn("hubFrAddBack", ix, y + cw(3.5f) - bb / 2, bb, bb, () => { friendsView = "list"; friendReqMsg = null; EndInput(); });
            Text("Add friend", ix + bb + cw(2), y + cw(3.5f), Lil(cw(5.2f)), HUB_TEXT);
            y += cw(10);
            var info = Wrap("Send a friend request with your friend's player name. Every name is unique, so the name is all you need. They show up in your list once they accept.", iw, Treb(cw(3)));
            for (int i = 0; i < info.Count; i++) Text(info[i], ix, y + cw(2) + i * cw(4.2f), Treb(cw(3)), HUB_DIM);
            y += cw(2) + info.Count * cw(4.2f) + cw(3);
            Text("YOUR NAME", ix, y + cw(1.5f), Treb(Min11(cw(2.6f)), true), HUB_ACCENT);
            Text(Account.Name ?? "Not set", ix, y + cw(6), Lil(cw(4.4f)), HUB_TEXT);
            y += cw(11);
            Text("FRIEND'S NAME", ix, y + cw(1.5f), Treb(Min11(cw(2.6f)), true), HUB_ACCENT);
            y += cw(4);
            float fh = cw(11);
            Field("fname", ix, y, iw, fh, friendNameDraft, "PLAYER NAME");
            y += fh + cw(3);
            bool ok = Social.Active && !friendSending && friendNameDraft.Trim().Length > 0; float sh = cw(11), sdy = ok && Down("hubFrSend") ? cw(0.5f) : 0;
            ctx.fillStyle = ok ? (object)VGrad(y + sdy, y + sdy + sh, "#4c6bff", "#3a3fd0") : "#2a2f5a"; ctx.beginPath(); ctx.roundRect(ix, y + sdy, iw, sh, cw(3)); ctx.fill();
            Text(friendSending ? "SENDING…" : "SEND FRIEND REQUEST", ix + iw / 2, y + sdy + sh / 2, Lil(cw(3.8f)), ok ? "#fff" : "rgba(255,255,255,0.45)", "center");
            Btn("hubFrSend", ix, y, iw, sh, () => { if (ok) SendFriendRequest(); else if (!Social.Active) Toast("Friends is not available right now"); });
            y += sh + cw(3);
            if (friendReqMsg != null)
            {
                var m = Wrap(friendReqMsg, iw, Treb(cw(3), true));
                for (int i = 0; i < m.Count; i++) Text(m[i], ix, y + cw(2) + i * cw(4.2f), Treb(cw(3), true), friendReqOk ? HUB_ACCENT : "#ff8a8a");
            }
        }
        static async void SendFriendRequest()
        {
            string name = friendNameDraft.Trim();
            if (friendSending || name.Length == 0) return;
            EndInput(); friendSending = true;
            try
            {
                string err = await Social.Add(name);
                friendReqOk = err == null;
                friendReqMsg = err ?? "Friend request sent to " + name + ". They show up in your list once they accept.";
                if (friendReqOk) friendNameDraft = "";
            }
            finally { friendSending = false; }
        }
    }
}
