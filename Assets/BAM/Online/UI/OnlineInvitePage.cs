using System;
using System.Collections.Generic;
using BAM.Online.Mp;
using UnityEngine;

// The invite panel (page "invite"), opened by Home's "+ INVITE" pills beside the hero (Speltest MenuHome3.HubInvitePill3, which
// opened the friends panel to invite) and by INVITE FROM FRIENDS / INVITE MORE on the Multiplayer page. Rickard 2026-10-30: "man
// ska se alla online på något sätt också men viktigast och först friends". The same panel from the right edge as Friends: the
// mode (TOGETHER / AGAINST EACH OTHER), the friends with INVITE first, then everyone else who has the game open now.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static readonly List<Social.Person> invFriends = new List<Social.Person>();
        static readonly List<PresenceRow> invOthers = new List<PresenceRow>();

        internal static void OpenInvite() { friendMenu = friendRemove = null; Go("invite"); MpPresence.LookSoon(); }

        static void HubInvite()
        {
            float topH = cw(16);
            float pw = cw(78), px = W - pw, pyy = topH + cw(2), pbot = H - cw(24), ph = pbot - pyy, ix = px + cw(5), iw = pw - cw(9);
            viewTop = viewBot = contentH = 0;
            ctx.fillStyle = "rgba(0,0,0,0.45)"; ctx.fillRect(0, 0, W, H);
            Btn("hubInvScrim", 0, 0, W, H, () => { EndInput(); HubBack(); });
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(px, pyy, pw + cw(4), ph, cw(4)); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(px, pyy, pw + cw(4), ph, cw(4)); ctx.stroke();
            Btn("hubInvPanel", px, pyy, pw, ph, null);
            float y = pyy + cw(4);
            if (Account.State != Account.Status.Ready) { HubFriendsSignIn(ix, y, iw); return; }

            // the head: + and "Invite"
            float hr = cw(3.2f);
            ctx.fillStyle = "rgba(62,230,176,0.14)"; ctx.beginPath(); ctx.arc(ix + hr, y + cw(3.5f), hr, 0, 7); ctx.fill();
            HubPlus(ix + hr, y + cw(3.5f), cw(1.6f), HUB_ACCENT);
            Text("Invite", ix + 2 * hr + cw(2.5f), y + cw(3.5f), Lil(cw(5.2f)), HUB_TEXT);
            y += cw(9.5f);

            // the mode: before the room exists it is the next room's; in the room the host changes it
            bool guest = MpRoom.State != MpRoom.Phase.Off && !MpRoom.IsHost;
            int mode = MpRoom.State == MpRoom.Phase.Off ? createMode : MpRoom.Mode;
            if (!guest)
            {
                HubSegments("hubInvMode", ix, y, iw, cw(9), new[] { "TOGETHER", "AGAINST EACH OTHER" }, mode,
                    m => { if (MpRoom.State == MpRoom.Phase.Off) createMode = m; else MpRoom.SetMode(m); }, cw(2.6f));
                y += cw(10.5f);
            }
            var ml = Wrap(guest ? "Only the host can invite. Ask " + (MpRoom.Members.Count > 0 ? MpRoom.Members[0].Name : "the host") + " to invite more." : ModeLine(mode, 2), iw, Treb(Min11(cw(2.8f))));
            for (int i = 0; i < ml.Count; i++) Text(ml[i], ix, y + cw(1.8f) + i * cw(3.8f), Treb(Min11(cw(2.8f))), HUB_DIM);
            y += ml.Count * cw(3.8f) + cw(2);

            // no room button: the party shows on Home, beside the player's hero (OnlineParty.cs)
            float listBot = pbot - cw(3);

            // friends first: the ones you can invite, then online, then offline (Rickard: "viktigast och först friends")
            invFriends.Clear(); invFriends.AddRange(Social.Friends);
            invFriends.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            invOthers.Clear();
            foreach (var r in MpPresence.Online) if (Social.RelationTo(r.Owner) != Social.Relation.Friends) invOthers.Add(r);

            float listTop = y, rh = cw(13), gap = cw(1.2f), head = cw(7);
            float othersH = invOthers.Count > 0 ? invOthers.Count * (rh + gap) : cw(10);
            float friendsH = invFriends.Count > 0 ? invFriends.Count * (rh + gap) : cw(10);
            viewTop = listTop; viewBot = listBot; contentH = head + friendsH + cw(2) + head + othersH; ClampScroll();
            ctx.save(); ctx.beginPath(); ctx.rect(ix - cw(1), listTop, iw + cw(2), listBot - listTop); ctx.clip();
            clipOn = true; clipTop = listTop; clipBot = listBot; scrollOff = pageScroll; ctx.translate(0, -pageScroll);
            float ry = listTop;
            int on = 0; foreach (var f in invFriends) if (f.Presence != Social.Presence.Offline) on++;
            ry = InvHead("FRIENDS", invFriends.Count > 0 ? on + " online" : null, ix, ry);
            if (invFriends.Count == 0) { ry = InvNote(Social.Active ? "No friends yet. Add friends from the FRIENDS button on Home." : "Friends is not available right now. Check your connection.", ix, ry, iw); }
            foreach (var f in invFriends) { if (ry + rh >= listTop + pageScroll - rh && ry <= listBot + pageScroll + rh) InvFriendRow(f, ix, ry, iw, rh); ry += rh + gap; }
            ry += cw(2);
            ry = InvHead("EVERYONE ONLINE", invOthers.Count > 0 ? invOthers.Count.ToString() : null, ix, ry);
            if (invOthers.Count == 0)
            {
                string s = !MpSocial.ShowMe ? "Hidden: turn on Show me as online on the Multiplayer page to see the others."
                    : MpPresence.Status.Length > 0 ? MpPresence.Status : MpPresence.Looked ? "Nobody else has the game open right now." : "Looking…";
                ry = InvNote(s, ix, ry, iw);
            }
            for (int i = 0; i < invOthers.Count; i++) { if (ry + rh >= listTop + pageScroll - rh && ry <= listBot + pageScroll + rh) InvOtherRow(invOthers[i], ix, ry, iw, rh); ry += rh + gap; }
            ctx.restore(); clipOn = false; scrollOff = 0;
            if (contentH > listBot - listTop)
            {
                float vis = listBot - listTop, bh = Math.Max(cw(6), vis * vis / contentH), by = listTop + (vis - bh) * pageScroll / Math.Max(1, contentH - vis);
                ctx.fillStyle = "rgba(154,162,214,0.35)"; ctx.beginPath(); ctx.roundRect(ix + iw + cw(1.2f), by, cw(0.8f), bh, cw(0.4f)); ctx.fill();
            }
        }

        static int Rank(Social.Person f) => MpSocial.CanInvite(f.PlayerId) ? 0 : f.Presence == Social.Presence.Online ? 1 : f.Presence == Social.Presence.Playing ? 2 : 3;

        static float InvHead(string t, string n, float x, float y)
        {
            Text(t, x + cw(1), y + cw(3.5f), Treb(cw(2.8f), true), HUB_ACCENT);
            if (n != null) { ctx.font = Treb(cw(2.8f), true); Text(n, x + cw(1) + ctx.measureText(t) + cw(2), y + cw(3.5f), Treb(Min11(cw(2.6f))), HUB_DIM); }
            return y + cw(7);
        }
        static float InvNote(string s, float x, float y, float w)
        {
            var l = Wrap(s, w - cw(6), Treb(Min11(cw(3))));
            float h = cw(3) + l.Count * cw(4.2f);
            ctx.fillStyle = "rgba(255,255,255,0.05)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            for (int i = 0; i < l.Count; i++) Text(l[i], x + cw(3), y + cw(1.5f) + cw(2.1f) + i * cw(4.2f), Treb(Min11(cw(3))), HUB_DIM);
            return y + h + cw(1.2f);
        }
        // a friend: picture with the presence dot, name, what they do, and INVITE (or why not) at the right (HubFriendPill3)
        static void InvFriendRow(Social.Person f, float x, float y, float w, float h)
        {
            ctx.fillStyle = "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            float ar = cw(4), acx = x + cw(2) + ar, acy = y + h / 2;
            HubAvatar(f.PlayerId, f.Name, acx, acy, ar);
            string pc = PresenceCol(f.Presence);
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.6f), 0, 7); ctx.fill();
            ctx.fillStyle = pc; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.1f), 0, 7); ctx.fill();
            var row = MpPresence.Find(f.PlayerId);
            string what = row != null && f.Presence != Social.Presence.Offline ? PresenceState.Text(row.State) : PresenceText(f.Presence);
            InvName(f.Name, what, f.Presence == Social.Presence.Offline ? HUB_DIM : HUB_TEXT, pc, acx + ar + cw(2.5f), x + w - HubFriendPillW - cw(2), y, h);
            HubFriendPill3(f, x + w - HubFriendPillW - cw(1), y, HubFriendPillW, h);
        }
        // someone who is not a friend but has the game open now (the Online now list): INVITE when they are free
        static void InvOtherRow(PresenceRow r, float x, float y, float w, float h)
        {
            ctx.fillStyle = "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            float ar = cw(4), acx = x + cw(2) + ar, acy = y + h / 2;
            HubAvatar(r.Owner, r.Name, acx, acy, ar);
            string pc = r.Free ? HUB_ACCENT : "#ffb43a";
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.6f), 0, 7); ctx.fill();
            ctx.fillStyle = pc; ctx.beginPath(); ctx.arc(acx + ar * 0.72f, acy + ar * 0.72f, cw(1.1f), 0, 7); ctx.fill();
            string hero = r.Hero >= 0 && r.Hero < BAM.Sim.HeroCatalog.Count ? BAM.Sim.HeroCatalog.Get((BAM.Sim.HeroId)r.Hero).Name + " · " : "";
            float pw = HubFriendPillW, bx = x + w - pw - cw(1);
            InvName(r.Name, hero + PresenceState.Text(r.State), HUB_TEXT, pc, acx + ar + cw(2.5f), bx - cw(1), y, h);
            float ph = cw(8), py = y + (h - ph) / 2;
            if (!r.Free)
            {
                ctx.fillStyle = "rgba(255,255,255,0.05)"; ctx.beginPath(); ctx.roundRect(bx, py, pw, ph, ph / 2); ctx.fill();
                Text("BUSY", bx + pw / 2, py + ph / 2, Treb(Min11(cw(2.6f)), true), "rgba(154,162,214,0.6)", "center");
                return;
            }
            bool sent = MpSocial.IsInvited(r.Owner), dn = !sent && Down("hubInvO:" + r.Owner);
            ctx.fillStyle = sent ? "rgba(255,255,255,0.05)" : dn ? "#24a050" : "#2fae5b"; ctx.beginPath(); ctx.roundRect(bx, py, pw, ph, ph / 2); ctx.fill();
            Text(sent ? "INVITED" : "INVITE", bx + pw / 2, py + ph / 2, Treb(Min11(cw(2.6f)), true), sent ? HUB_ACCENT : "#fff", "center");
            string id = r.Owner, nm = r.Name;
            if (!sent) Btn("hubInvO:" + id, bx, py, pw, ph, () => InviteSomeone(id, nm));
        }
        static void InvName(string name, string line, string nameCol, string lineCol, float nx, float right, float y, float h)
        {
            float nw = right - nx;
            string nm = name ?? ""; ctx.font = Lil(cw(3.6f)); while (nm.Length > 3 && ctx.measureText(nm) > nw) nm = nm.Substring(0, nm.Length - 2) + "…";
            Text(nm, nx, y + h * 0.36f, Lil(cw(3.6f)), nameCol);
            string f = Treb(Min11(cw(2.7f)), true); ctx.font = f; while (line.Length > 3 && ctx.measureText(line) > nw) line = line.Substring(0, line.Length - 2) + "…";
            Text(line, nx, y + h * 0.7f, f, lineCol);
        }
    }
}
