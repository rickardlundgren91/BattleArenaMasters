using System;
using System.Collections.Generic;
using UnityEngine;

// The player's own pages: Go online, Player Profile, choose / change name, Rank (MY RANK and TOP 100) and Account.
// Ported from Speltest MenuHome.cs (PageProfile, PageHubName, HubNameConfirm), MenuProgress.cs (HubProfileLevel, HubProfileFight),
// MenuSocial.cs (PageRank, HubPlayerRow, HubQueryState) and MenuHome.cs PageHubSet's ACCOUNT rows.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        // ---------- Go online (new: Speltest signed in on its own; BAM asks first, and the Google Play Games account is the player) ----------
        static float PageGoOnline(float x, float y, float w)
        {
            y = H2("Play online", null, x, y);
            var st = Account.State;
            float chh = cw(44); HubCardBack(x, y, w, chh);
            HubPeople(x + w / 2 - cw(2), y + cw(10), cw(4.4f), HUB_ACCENT);
            Text("Friends and the Top 100", x + w / 2, y + cw(22), Lil(cw(5.6f)), HUB_TEXT, "center");
            var lines = Wrap("Sign in with Google Play Games to get your own unique player name, level and Rating, add friends and climb the Top 100. Your progress follows your Google Play Games account to any phone.", w - cw(10), Treb(cw(3.2f)));
            for (int i = 0; i < lines.Count; i++) Text(lines[i], x + w / 2, y + cw(29) + i * cw(4.4f), Treb(cw(3.2f)), HUB_DIM, "center");
            chh = Math.Max(chh, cw(29) + lines.Count * cw(4.4f) + cw(3));
            y += chh + cw(4);
            bool busy = st == Account.Status.Working;
            float bh = cw(12), bdy = !busy && Down("olSignIn") ? cw(0.6f) : 0;
            ctx.fillStyle = !busy ? (object)VGrad(y + bdy, y + bdy + bh, "#ffcf4d", "#ff7a2f") : "#2a2f5a"; ctx.beginPath(); ctx.roundRect(x, y + bdy, w, bh, cw(3.5f)); ctx.fill();
            Text(busy ? "SIGNING IN…" : "SIGN IN WITH GOOGLE PLAY GAMES", x + w / 2, y + bdy + bh / 2, Lil(cw(4.2f)), !busy ? "#fff" : "rgba(255,255,255,0.45)", "center");
            Btn("olSignIn", x, y, w, bh, () => { if (!busy) { goOnlineAsked = true; Account.TurnOn(); } });
            y += bh + cw(3);
            if (st == Account.Status.NeedsSignIn || st == Account.Status.Offline)
            {
                string why = st == Account.Status.Offline ? "Could not reach the game server" : "Google Play Games did not sign you in";
                if (Account.LastError.Length > 0) why += ": " + Account.LastError;
                var el = Wrap(why, w - cw(2), Treb(cw(3), true));
                for (int i = 0; i < el.Count; i++) Text(el[i], x + cw(1), y + cw(1.5f) + i * cw(4.2f), Treb(cw(3), true), "#ff8a8a");
                y += el.Count * cw(4.2f) + cw(3);
            }
            float nh = cw(10); string c = HubToggle("olLater", x, y, w, nh, cw(3), false, !busy);
            Text("NOT NOW", x + w / 2, y + nh / 2, Lil(cw(4)), c, "center");
            Btn("olLater", x, y, w, nh, () => { if (!busy) Close(); });
            y += nh + cw(4);
            var note = Wrap("You can always play against bots without signing in. Sign in later from Settings > Account.", w - cw(2), Treb(cw(2.8f)));
            for (int i = 0; i < note.Count; i++) Text(note[i], x + cw(1), y + i * cw(4), Treb(cw(2.8f)), "rgba(154,162,214,0.8)");
            return y + note.Count * cw(4);
        }
        // the sign-in started from this page: when it ends, go on to the name page or close
        static bool goOnlineAsked;
        internal static void AccountChanged()
        {
            if (!goOnlineAsked || page != "golive") return;
            var st = Account.State;
            if (st == Account.Status.NeedsName) { goOnlineAsked = false; Go("hubname"); }
            else if (st == Account.Status.Ready) { goOnlineAsked = false; Toast("You are online as " + Account.Name); Close(); }
            else if (st != Account.Status.Working) goOnlineAsked = false;
        }

        // ---------- Player Profile (page "profile") ----------
        // The name first and largest, then the rank (opens the Rank page), SEE AS OTHERS, the level card, the match stats and the
        // three most played heroes. The numbers are the server's totals plus matches still waiting to be sent (Progress.Shown).
        static float PageProfile(float x, float y, float w)
        {
            var t = Progress.Shown;
            string nm = Account.Name ?? ""; bool noName = nm.Length == 0;
            int rating = t.rating;
            float chh = cw(52);
            HubCardBack(x, y, w, chh);
            float ar = cw(9), acx = x + cw(4) + ar, acy = y + cw(4) + ar;
            if (!DrawPortrait(TopHero(t), acx, acy, ar))
            {
                ctx.fillStyle = VGrad(acy - ar, acy + ar, "#4c6bff", "#7a3cff"); ctx.beginPath(); ctx.arc(acx, acy, ar, 0, 7); ctx.fill();
                ctx.strokeStyle = "rgba(255,255,255,0.35)"; ctx.lineWidth = cw(0.5f); ctx.beginPath(); ctx.arc(acx, acy, ar, 0, 7); ctx.stroke();
                Text(noName ? "?" : nm.Substring(0, 1).ToUpper(), acx, acy, Lil(cw(9)), "#fff", "center");
            }
            float tx = acx + ar + cw(4), tw = x + w - cw(4) - tx;
            Text("PLAYER PROFILE", tx, y + cw(6), Treb(cw(2.6f), true), HUB_DIM);
            float ns = cw(8.4f); string shown = noName ? "No name yet" : nm; ctx.font = Lil(ns); while (ns > cw(5) && ctx.measureText(shown) > tw) { ns -= cw(0.4f); ctx.font = Lil(ns); }
            Text(shown, tx, y + cw(13), Lil(ns), noName ? "rgba(243,244,255,0.8)" : HUB_TEXT);
            bool online = Account.State == Account.Status.Ready || Account.State == Account.Status.NeedsName;
            string nl = noName ? "Choose a name" : "Change name";
            ctx.font = Treb(cw(3), true); float pw = ctx.measureText(nl) + cw(10), ph = cw(7), ppx = tx, ppy = y + cw(19) + (Down("hubProfName") ? cw(0.4f) : 0);
            ctx.fillStyle = noName ? "rgba(255,176,122,0.18)" : "rgba(62,230,176,0.14)"; ctx.beginPath(); ctx.roundRect(ppx, ppy, pw, ph, ph / 2); ctx.fill();
            HubPencil(ppx + cw(3.4f), ppy + ph / 2, cw(1.5f), noName ? "#ffb07a" : HUB_ACCENT);
            Text(nl, ppx + cw(6.5f), ppy + ph / 2, Treb(cw(3), true), noName ? "#ffb07a" : HUB_ACCENT);
            Btn("hubProfName", ppx, y + cw(19), pw, ph, () => { if (online) Go("hubname"); else Go("golive"); });
            float ry = y + cw(31);
            ctx.fillStyle = HUB_LINE; ctx.fillRect(x + cw(4), ry - cw(1.5f), w - cw(8), cw(0.3f));
            int rk = RankInfo.RankOf(rating), nx = RankInfo.NextOf(rating); string col = RankInfo.COLORS[rk];
            float es = cw(6.5f), ecx = x + cw(4) + es * 1.1f, ecy = ry + cw(10) + (Down("hubProfRank") ? cw(0.4f) : 0);
            HubRankEmblem(rk, ecx, ecy, es);
            float rx = ecx + es * 1.6f + cw(2), rw = x + w - cw(9) - rx;
            Text(RankInfo.NAMES[rk].ToUpper(), rx, ry + cw(5), Lil(cw(5)), col);
            Text(rating + " Rating", rx + rw, ry + cw(5.2f), Treb(cw(3.2f), true), HUB_TEXT, "right");
            float bY = ry + cw(10.5f), bH = cw(1.8f);
            ctx.fillStyle = "rgba(255,255,255,0.1)"; ctx.beginPath(); ctx.roundRect(rx, bY, rw, bH, bH / 2); ctx.fill();
            float f = RankInfo.Progress(rating); if (f > 0) { ctx.fillStyle = col; ctx.beginPath(); ctx.roundRect(rx, bY, Math.Max(bH, rw * f), bH, bH / 2); ctx.fill(); }
            Text(nx < 0 ? "Top rank" : RankInfo.Needed(rating) + " Rating to " + RankInfo.NAMES[nx], rx, bY + bH + cw(3), Treb(cw(2.8f), true), HUB_DIM);
            HubChevron(x + w - cw(5), ry + cw(10), cw(1.6f), HUB_DIM);
            Btn("hubProfRank", x, ry, w, chh - (ry - y), () => { hubTop100 = false; Go("rank"); });
            y += chh + cw(2);
            if (online && Account.PlayerId != null)
            {
                float sh = cw(9); string c = HubToggle("hubProfSee", x, y, w, sh, cw(3), false, true);
                Text("SEE AS OTHERS", x + w / 2, y + sh / 2, Lil(cw(3.8f)), c, "center");
                Btn("hubProfSee", x, y, w, sh, () => OpenPlayer(Account.PlayerId));
                y += sh + cw(2);
            }
            y = HubProfileLevel(t, x, y, w);
            y = HubSetHead("MATCH STATS", x, y);
            string[] vals = { t.matches.ToString(), t.wins.ToString(), t.losses.ToString(), WinRateText(t.wins, t.losses) };
            string[] labs = { "Matches", "Wins", "Losses", "Win rate" };
            float gap = cw(2), tlw = (w - 3 * gap) / 4, tlh = cw(14);
            for (int i = 0; i < 4; i++)
            {
                float tlx = x + i * (tlw + gap);
                ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(tlx, y, tlw, tlh, cw(3)); ctx.fill();
                ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(tlx, y, tlw, tlh, cw(3)); ctx.stroke();
                Text(vals[i], tlx + tlw / 2, y + tlh * 0.42f, Lil(cw(5.8f)), i == 1 ? HUB_ACCENT : i == 2 ? "#ff8a8a" : HUB_TEXT, "center");
                Text(labs[i], tlx + tlw / 2, y + tlh * 0.8f, Treb(cw(2.6f), true), HUB_DIM, "center");
            }
            y += tlh + cw(1.5f);
            y = HubProfileFight(t, x, y, w);
            int waiting = Progress.PendingCount;
            var note = Wrap("Finished matches against bots. The tutorial and matches left early do not count." + (waiting > 0 ? " " + waiting + (waiting == 1 ? " match is" : " matches are") + " waiting to be sent to the server." : ""), w - cw(2), Treb(cw(2.8f)));
            for (int i = 0; i < note.Count; i++) Text(note[i], x + cw(1), y + cw(2) + i * cw(4), Treb(cw(2.8f)), "rgba(154,162,214,0.8)");
            y += cw(2) + note.Count * cw(4) + cw(2);
            y = HubSetHead("TOP HEROES", x, y);
            t.TopHeroes(topList, 3);
            if (topList.Count == 0)
            {
                var el = Wrap("No finished matches yet. Play a match and your most played heroes show up here.", w - cw(10), Treb(cw(3.1f)));
                float eh = cw(6) + el.Count * cw(4.4f);
                ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, eh, cw(3)); ctx.fill();
                for (int i = 0; i < el.Count; i++) Text(el[i], W / 2, y + cw(3) + cw(2.2f) + i * cw(4.4f), Treb(cw(3.1f)), HUB_DIM, "center");
                y += eh + cw(2);
            }
            for (int i = 0; i < topList.Count; i++)
            {
                var hs = topList[i]; float hh = cw(15), pp = cw(12), hpx = x + cw(9), hpy = y + (hh - pp) / 2;
                ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, hh, cw(3)); ctx.fill();
                ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y, w, hh, cw(3)); ctx.stroke();
                Text("#" + (i + 1), x + cw(4.5f), y + hh / 2, Lil(cw(3.6f)), i == 0 ? "#ffd257" : HUB_DIM, "center");
                ctx.fillStyle = "#1d2350"; ctx.beginPath(); ctx.roundRect(hpx, hpy, pp, pp, cw(2.5f)); ctx.fill();
                ctx.save(); ctx.beginPath(); ctx.roundRect(hpx, hpy, pp, pp, cw(2.5f)); ctx.clip(); Contain(HeroIcon(hs.id), hpx, hpy, pp, pp); ctx.restore();
                float nx2 = hpx + pp + cw(3);
                Text(hs.id, nx2, y + hh * 0.38f, Lil(cw(4.4f)), HUB_TEXT);
                Text(hs.m + (hs.m == 1 ? " match" : " matches"), nx2, y + hh * 0.7f, Treb(cw(2.9f), true), HUB_DIM);
                Text(WinRateText(hs.w, hs.m - hs.w), x + w - cw(4), y + hh * 0.4f, Lil(cw(5)), HUB_ACCENT, "right");
                Text("win rate", x + w - cw(4), y + hh * 0.72f, Treb(cw(2.6f), true), HUB_DIM, "right");
                y += hh + cw(1.5f);
            }
            return y;
        }
        // the level card: badge, XP bar and the next hero it opens, or how XP is earned once all are open (MenuProgress.HubProfileLevel)
        static float HubProfileLevel(Totals t, float x, float y, float w)
        {
            float h = cw(21);
            HubCardBack(x, y, w, h);
            int level = Progress.LevelOf(t.xp);
            Progress.IntoLevel(t.xp, out int have, out int need);
            float br = cw(6.5f), bcx = x + cw(4) + br, bcy = y + h / 2;
            ctx.fillStyle = VGrad(bcy - br, bcy + br, "#43f0bd", "#1f9e7a"); ctx.beginPath(); ctx.arc(bcx, bcy, br, 0, 7); ctx.fill();
            Text(level.ToString(), bcx, bcy, Lil(cw(6.4f)), "#06231a", "center");
            float tx = bcx + br + cw(4), tw = x + w - cw(4) - tx;
            Text("ACCOUNT LEVEL " + level, tx, y + cw(5), Lil(cw(4.4f)), HUB_TEXT);
            Text(level >= Progress.MaxLevel ? "Max level" : have + " / " + need + " XP", tx + tw, y + cw(5.2f), Treb(cw(2.9f), true), HUB_DIM, "right");
            float bY = y + cw(9.5f), bH = cw(1.8f);
            ctx.fillStyle = "rgba(255,255,255,0.1)"; ctx.beginPath(); ctx.roundRect(tx, bY, tw, bH, bH / 2); ctx.fill();
            float f = need > 0 ? (float)have / need : 1; if (f > 0) { ctx.fillStyle = HUB_ACCENT; ctx.beginPath(); ctx.roundRect(tx, bY, Math.Max(bH, tw * f), bH, bH / 2); ctx.fill(); }
            string next = Progress.NextUnlock();
            Text(next != null ? "Next: " + next : "Win " + Progress.XpWin + " XP, lose " + Progress.XpLoss + " XP, first win of the day +" + Progress.XpFirstWin,
                tx, bY + bH + cw(4), Treb(Min11(cw(2.8f)), true), next != null ? "#ffd257" : HUB_DIM);
            return y + h + cw(2);
        }
        // kills, deaths, time played and the streaks (MenuProgress.HubProfileFight; BAM counts no assists, so Streak takes that tile)
        static float HubProfileFight(Totals t, float x, float y, float w)
        {
            string[] vals = { t.kills.ToString(), t.deaths.ToString(), t.streak.ToString(), TimeText(t.seconds), t.bestStreak.ToString() };
            string[] labs = { "Kills", "Deaths", "Streak", "Played", "Best streak" };
            float gap = cw(1.5f), tlw = (w - 4 * gap) / 5, tlh = cw(11.5f);
            for (int i = 0; i < 5; i++)
            {
                float tlx = x + i * (tlw + gap);
                ctx.fillStyle = "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(tlx, y, tlw, tlh, cw(2.5f)); ctx.fill();
                Text(vals[i], tlx + tlw / 2, y + tlh * 0.4f, Lil(cw(4.4f)), i == 1 ? "#ff8a8a" : HUB_TEXT, "center");
                Text(labs[i], tlx + tlw / 2, y + tlh * 0.78f, Treb(Min11(cw(2.4f)), true), HUB_DIM, "center");
            }
            return y + tlh + cw(1.5f);
        }

        // ---------- choose / change name (page "hubname") ----------
        // Names are unique: the server (SetName.js) keeps one owner per name, ignoring capitals. The first name is free and can be
        // changed once more straight away (a typo); after that a new name every 7 days. There are no prices (Speltest showed
        // provisional Coin prices and an Undo; BAM has no currency, and the free second change does the Undo's job).
        static string hubNameDraft = "", hubConfirm; static NameAnswer nameAnswer; static bool nameSaving;
        static float PageHubName(float x, float y, float w)
        {
            string cur = Account.Name ?? ""; bool first = cur.Length == 0;
            y = H2(first ? "Choose your name" : "Change name", null, x, y);
            if (!first) { Text("Current name: " + cur, x + cw(1), y + cw(1), Treb(cw(3.2f), true), HUB_DIM); y += cw(6); }
            float fh = cw(11); Field("pname", x, y, w, fh, hubNameDraft, "YOUR NAME"); y += fh + cw(2.5f);
            string n = NameRules.Clean(hubNameDraft), err = NameRules.Check(n);
            if (err == null && n == cur) err = "That is already your name";
            bool typed = hubNameDraft.Length > 0;
            string line = nameSaving ? "Checking with the server…" : nameAnswer != null && !nameAnswer.ok ? nameAnswer.reason : err ?? "Allowed. The server checks that nobody has it.";
            bool bad = !nameSaving && (nameAnswer != null && !nameAnswer.ok || err != null);
            if (typed || !first || nameAnswer != null) Text(line, x + cw(1), y + cw(1.5f), Treb(cw(3.1f), true), bad ? "#ff8a8a" : HUB_ACCENT);
            y += cw(6);
            var rules = Wrap(NameRules.Min + "–" + NameRules.Max + " characters: letters, numbers, _ . and -, with at least one letter. Every name is unique: no other player can have yours.", w - cw(2), Treb(cw(2.9f)));
            for (int i = 0; i < rules.Count; i++) Text(rules[i], x + cw(1), y + i * cw(4.2f), Treb(cw(2.9f)), HUB_DIM);
            y += rules.Count * cw(4.2f) + cw(3);
            var price = Wrap(first ? "Your first name is free, and if you change your mind you can change it once more straight away."
                : "You can change your name once every 7 days. Changing only capital letters is always allowed.", w - cw(8), Treb(cw(3.1f), true));
            float prh = cw(5) + price.Count * cw(4.4f);
            ctx.fillStyle = "rgba(255,210,87,0.08)"; ctx.beginPath(); ctx.roundRect(x, y, w, prh, cw(3)); ctx.fill();
            ctx.strokeStyle = "rgba(255,210,87,0.35)"; ctx.lineWidth = cw(0.35f); ctx.beginPath(); ctx.roundRect(x, y, w, prh, cw(3)); ctx.stroke();
            for (int i = 0; i < price.Count; i++) Text(price[i], x + cw(4), y + cw(2.5f) + cw(2.2f) + i * cw(4.4f), Treb(cw(3.1f), true), "#ffd98a");
            y += prh + cw(4);
            bool ok = err == null && !nameSaving; float bh = cw(12), bdy = ok && Down("hubNameSave") ? cw(0.6f) : 0;
            ctx.fillStyle = ok ? (object)VGrad(y + bdy, y + bdy + bh, "#ffcf4d", "#ff7a2f") : "#2a2f5a"; ctx.beginPath(); ctx.roundRect(x, y + bdy, w, bh, cw(3.5f)); ctx.fill();
            Text(first ? "SAVE NAME" : "CHANGE NAME", x + w / 2, y + bdy + bh / 2, Lil(cw(5)), ok ? "#fff" : "rgba(255,255,255,0.45)", "center");
            Btn("hubNameSave", x, y, w, bh, () => { if (ok) { EndInput(); hubConfirm = n; } else if (err != null) Toast(err); });
            return y + bh + cw(4);
        }
        static void HubNameConfirm()
        {
            bool first = string.IsNullOrEmpty(Account.Name);
            ctx.fillStyle = "rgba(0,0,0,0.6)"; ctx.fillRect(0, 0, W, H);
            Btn("hubConfirmScrim", 0, 0, W, H, () => hubConfirm = null);
            float dw = W - cw(12), dx = cw(6), ix = dx + cw(5), iw = dw - cw(10);
            var body = Wrap(first ? "Your name will be " + hubConfirm + ". Other players see it on the Top 100 and in their friend lists."
                : "From " + Account.Name + " to " + hubConfirm + ". After this you can change it again in 7 days.", iw, Treb(cw(3.4f), true));
            float dh = cw(5) + cw(9) + body.Count * cw(4.8f) + cw(4) + cw(11) + cw(5), dy = H / 2 - dh / 2;
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(dx, dy, dw, dh, cw(4)); ctx.fill();
            ctx.strokeStyle = HUB_ACCENT; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(dx, dy, dw, dh, cw(4)); ctx.stroke();
            Btn("hubConfirmBox", dx, dy, dw, dh, null);
            float yy = dy + cw(5);
            Text(first ? "Use this name?" : "Change your name?", ix, yy + cw(3), Lil(cw(5.4f)), HUB_TEXT);
            yy += cw(9);
            for (int i = 0; i < body.Count; i++) Text(body[i], ix, yy + cw(2.4f) + i * cw(4.8f), Treb(cw(3.4f), true), HUB_DIM);
            yy += body.Count * cw(4.8f) + cw(4);
            float bw = (iw - cw(3)) / 2, bh = cw(11);
            string cancelCol = HubToggle("hubConfirmNo", ix, yy, bw, bh, cw(3), false, true);
            Text("CANCEL", ix + bw / 2, yy + bh / 2, Lil(cw(4.2f)), cancelCol, "center");
            Btn("hubConfirmNo", ix, yy, bw, bh, () => hubConfirm = null);
            float cx = ix + bw + cw(3), cdy = Down("hubConfirmYes") ? cw(0.5f) : 0;
            ctx.fillStyle = VGrad(yy + cdy, yy + cdy + bh, "#ffcf4d", "#ff7a2f"); ctx.beginPath(); ctx.roundRect(cx, yy + cdy, bw, bh, cw(3)); ctx.fill();
            Text("CONFIRM", cx + bw / 2, yy + cdy + bh / 2, Lil(cw(4.2f)), "#fff", "center");
            Btn("hubConfirmYes", cx, yy, bw, bh, () => { string want = hubConfirm; hubConfirm = null; SaveName(want); });
        }
        static async void SaveName(string want)
        {
            if (nameSaving) return;
            nameSaving = true; nameAnswer = null;
            try
            {
                var a = await Server.SetName(want);
                nameAnswer = a;
                if (a.ok)
                {
                    Toast("Your name is now " + a.name);
                    // chosen right after signing in: the sign-in page is done too
                    if (page == "hubname") { if (history.Count > 0 && history[history.Count - 1] == "golive") Close(); else Back(); }
                }
                else Toast(a.reason);
            }
            finally { nameSaving = false; }
        }

        // ---------- Rank: MY RANK (the ladder) and TOP 100 (MenuSocial.PageRank) ----------
        static bool hubTop100;
        struct PlayerCard { public string id, name; public int place, level, rating; }
        static float PageRank(float x, float y, float w)
        {
            y = H2(hubTop100 ? "Top 100" : "Rank Ladder", null, x, y);
            float th = cw(10), tw = (w - cw(2)) / 2;
            for (int i = 0; i < 2; i++)
            {
                bool top = i == 1, on = hubTop100 == top; string key = top ? "hubRankTop" : "hubRankMine";
                string c = HubToggle(key, x + i * (tw + cw(2)), y, tw, th, th / 2, on, true);
                Text(top ? "TOP 100" : "MY RANK", x + i * (tw + cw(2)) + tw / 2, y + th / 2, Lil(cw(4)), c, "center");
                bool t2 = top; Btn(key, x + i * (tw + cw(2)), y, tw, th, () => { if (hubTop100 == t2) return; hubTop100 = t2; pageScroll = 0; if (t2) _ = Leaderboard.Load(); });
            }
            y += th + cw(4);
            if (hubTop100) return Top100(x, y, w);
            int rating = Progress.Shown.rating;
            int rk = RankInfo.RankOf(rating), nx = RankInfo.NextOf(rating); string col = RankInfo.COLORS[rk];
            float ph2 = cw(30);
            ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, ph2, cw(4)); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y, w, ph2, cw(4)); ctx.stroke();
            HubRankEmblem(rk, x + cw(11), y + cw(11), cw(7));
            Text(RankInfo.NAMES[rk].ToUpper(), x + cw(21), y + cw(7.5f), Lil(cw(6)), col);
            Text(rating + " Rating", x + cw(21), y + cw(13.5f), Treb(cw(3.6f), true), HUB_TEXT);
            float bY = y + cw(21), bW = w - cw(8), bH = cw(2.4f);
            ctx.fillStyle = "rgba(255,255,255,0.08)"; ctx.beginPath(); ctx.roundRect(x + cw(4), bY, bW, bH, bH / 2); ctx.fill();
            float f = RankInfo.Progress(rating); if (f > 0) { ctx.fillStyle = col; ctx.beginPath(); ctx.roundRect(x + cw(4), bY, Math.Max(bH, bW * f), bH, bH / 2); ctx.fill(); }
            Text(nx < 0 ? "Top rank" : RankInfo.Needed(rating) + " Rating to " + RankInfo.NAMES[nx], x + cw(4), bY + bH + cw(3), Treb(cw(3)), HUB_DIM);
            y += ph2 + cw(3);
            var lines = Wrap("A win gives Rating by who you beat: Easy bots +" + Progress.RatingWinOf("Easy") + ", Normal bots +" + Progress.RatingWinOf("Normal") + ", Hard bots +" + Progress.RatingWinOf("Hard") + ", Ultra bots +" + Progress.RatingWinOf("Ultra") + ", real players +" + Progress.RatingWinOf(Progress.Players) + ". A loss against Hard bots costs " + Progress.RatingLossOf("Hard") + ", against Ultra bots " + Progress.RatingLossOf("Ultra") + ", against players " + Progress.RatingLossOf(Progress.Players) + ". Easy and Normal bots give Rating only up to " + Progress.RatingCapOf("Easy") + " and " + Progress.RatingCapOf("Normal") + ". The game server counts it, so it is the same on every phone.", w, Treb(cw(3.1f)));
            for (int i = 0; i < lines.Count; i++) Text(lines[i], x, y + cw(1.5f) + i * cw(4.4f), Treb(cw(3.1f)), "#ffb07a");
            y += cw(3) + lines.Count * cw(4.4f);
            float rh = cw(11);
            for (int r = RankInfo.NAMES.Length - 1; r >= 0; r--)
            {
                bool me = r == rk;
                ctx.fillStyle = me ? "rgba(62,230,176,0.12)" : HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, rh, cw(3)); ctx.fill();
                if (me) { ctx.strokeStyle = HUB_ACCENT; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y, w, rh, cw(3)); ctx.stroke(); }
                HubRankEmblem(r, x + cw(7), y + rh / 2, cw(3.8f));
                Text(RankInfo.NAMES[r], x + cw(14), y + rh / 2, Lil(cw(4.4f)), RankInfo.COLORS[r]);
                int lo = RankInfo.MIN[r];
                Text(r == RankInfo.NAMES.Length - 1 ? lo + "+" : lo + "–" + (RankInfo.MIN[r + 1] - 1), x + w - cw(4), y + rh / 2 + (me ? cw(1.4f) : 0), Treb(cw(3)), HUB_DIM, "right");
                if (me) Text("YOU", x + w - cw(4), y + rh / 2 - cw(2.4f), Treb(cw(2.3f), true), HUB_ACCENT, "right");
                y += rh + cw(1.6f);
            }
            return y;
        }
        static float Top100(float x, float y, float w)
        {
            if (Account.State != Account.Status.Ready)
                return HubStateBox("hubTopSignIn", "Sign in with Google Play Games to see the Top 100.", false, "SIGN IN", () => Go("golive"), x, y, w);
            var rows = Leaderboard.Rows;
            if (rows.Count == 0)
            {
                if (Leaderboard.Loading) return HubStateBox(null, "Loading…", false, null, null, x, y, w);
                if (Leaderboard.Error.Length > 0) return HubStateBox("hubTopRetry", "Could not load: " + Leaderboard.Error, true, "RETRY", () => _ = Leaderboard.Load(true), x, y, w);
                return HubStateBox(null, "No ranked players yet. Finish a match to be the first.", false, null, null, x, y, w);
            }
            Text("Global ranking by Rating", x + cw(1), y + cw(1), Treb(cw(2.8f), true), HUB_DIM); y += cw(5);
            for (int i = 0; i < rows.Count && i < Leaderboard.Top; i++)
            {
                var r = rows[i]; string id = r.PlayerId;
                HubPlayerRow("hubTop:" + id, new PlayerCard { id = id, name = r.Name, place = r.Rank, level = r.Level, rating = r.Rating }, x, y, w, cw(13),
                    () => { if (r.Me) Go("profile"); else OpenPlayer(id); }, r.Me ? "YOU" : null);
                y += cw(14);
            }
            // your own place when you are below the 100 (new: Speltest only listed the 100)
            if (Leaderboard.TryGetMe(out var me) && me.Rank > Leaderboard.Top)
            {
                y = HubSetHead("YOUR PLACE", x, y);
                HubPlayerRow("hubTopMe", new PlayerCard { id = me.PlayerId, name = me.Name, place = me.Rank, level = me.Level, rating = me.Rating }, x, y, w, cw(13), () => Go("profile"), "YOU");
                y += cw(14);
            }
            return y;
        }
        // a box for something that has nothing to show yet (MenuSocial.HubQueryState): a message and, for errors, a button
        static float HubStateBox(string key, string msg, bool error, string button, Action a, float x, float y, float w)
        {
            var lines = Wrap(msg, w - cw(8), Treb(cw(3.2f)));
            float h = cw(5) + lines.Count * cw(4.4f) + (button != null ? cw(12) : 0);
            ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            for (int i = 0; i < lines.Count; i++) Text(lines[i], x + w / 2, y + cw(2.5f) + cw(2.2f) + i * cw(4.4f), Treb(cw(3.2f)), error ? "#ff8a8a" : HUB_DIM, "center");
            if (button != null)
            {
                float by = y + h - cw(11), bw = cw(30);
                string c = HubToggle(key, x + w / 2 - bw / 2, by, bw, cw(8), cw(4), false, true);
                Text(button, x + w / 2, by + cw(4), Lil(cw(3.6f)), c, "center");
                Btn(key, x + w / 2 - bw / 2, by, bw, cw(8), a);
            }
            return y + h + cw(2);
        }
        // a row for a player: place, avatar letter, name, level, and the rank with its Rating (MenuSocial.HubPlayerRow)
        static void HubPlayerRow(string key, PlayerCard p, float x, float y, float w, float h, Action a, string tag = null)
        {
            bool dn = Down(key);
            ctx.fillStyle = dn || tag != null ? "rgba(62,230,176,0.12)" : HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.fill();
            ctx.strokeStyle = tag != null ? HUB_ACCENT : HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(3)); ctx.stroke();
            float lx = x + cw(3);
            if (p.place > 0) { string pl = p.place.ToString(); float ps = p.place >= 1000 ? cw(2.8f) : cw(3.8f); Text(pl, x + cw(5), y + h / 2, Lil(ps), p.place <= 3 ? "#ffd257" : HUB_DIM, "center"); lx = x + cw(10); }
            float ar = cw(4.2f); HubAvatar(p.id, p.name, lx + ar, y + h / 2, ar);
            float nx = lx + ar * 2 + cw(3), rightW = cw(24);
            string nm = p.name ?? ""; ctx.font = Lil(cw(3.8f)); while (nm.Length > 3 && ctx.measureText(nm) > x + w - rightW - nx) nm = nm.Substring(0, nm.Length - 2) + "…";
            Text(nm, nx, y + h * 0.38f, Lil(cw(3.8f)), HUB_TEXT);
            string sub = (tag != null ? tag + " · " : "") + (p.level > 0 ? "Level " + p.level : "");
            Text(sub, nx, y + h * 0.7f, Treb(cw(2.6f), true), tag != null ? HUB_ACCENT : HUB_DIM);
            if (p.rating >= 0)
            {
                int rk = RankInfo.RankOf(p.rating); HubRankEmblem(rk, x + w - cw(19), y + h / 2, cw(2.6f));
                Text(p.rating.ToString(), x + w - cw(3), y + h * 0.38f, Lil(cw(3.6f)), RankInfo.COLORS[rk], "right");
                Text(RankInfo.NAMES[rk], x + w - cw(3), y + h * 0.7f, Treb(cw(2.4f), true), HUB_DIM, "right");
            }
            Btn(key, x, y, w, h, a);
        }

        // ---------- Account (Settings > Account; Speltest PageHubSet's ACCOUNT rows, with Google Play Games in place of the backup code) ----------
        static bool deleting, deleted;
        static float PageAccount(float x, float y, float w)
        {
            y = H2("Account", null, x, y);
            var st = Account.State;
            bool signedIn = st == Account.Status.Ready || st == Account.Status.NeedsName;
            y = HubSetHead("GOOGLE PLAY GAMES", x, y);
            string status = st == Account.Status.Ready ? "Signed in" : st == Account.Status.NeedsName ? "Signed in, no name yet" : st == Account.Status.Working ? "Working…"
                : st == Account.Status.Offline ? "No connection" : st == Account.Status.NeedsSignIn ? "Not signed in" : "Offline play only";
            y = HubSetRow(null, "Status", status, false, null, x, y, w, plain: true);
            if (!signedIn && st != Account.Status.Working)
                y = HubSetRow("accSignIn", "Sign in", st == Account.Status.Offline ? "Try again" : "Google Play Games", true, () => { Account.TurnOn(); }, x, y, w);
            if (signedIn)
            {
                y = HubSetRow("accName", "Player name", string.IsNullOrEmpty(Account.Name) ? "Choose" : Account.Name, true, () => Go("hubname"), x, y, w);
                if (Account.PlatformName.Length > 0) y = HubSetRow(null, "Google Play Games", Account.PlatformName, false, null, x, y, w, plain: true);
                int waiting = Progress.PendingCount;
                y = HubSetRow(null, "Waiting to be sent", waiting + (waiting == 1 ? " match" : " matches"), false, null, x, y, w, plain: true);
            }
            if (st == Account.Status.NeedsSignIn || st == Account.Status.Offline)
            {
                if (Account.LastError.Length > 0) { var el = Wrap(Account.LastError, w - cw(2), Treb(cw(2.8f), true)); for (int i = 0; i < el.Count; i++) Text(el[i], x + cw(1), y + cw(1.5f) + i * cw(4), Treb(cw(2.8f), true), "#ff8a8a"); y += el.Count * cw(4) + cw(2); }
            }
            if (deleted)
            {
                var dl = Wrap("Your account and its data are deleted: your name, level, Rating, stats, friends and your place on the Top 100. You can keep playing against bots.", w - cw(2), Treb(cw(3), true));
                for (int i = 0; i < dl.Count; i++) Text(dl[i], x + cw(1), y + cw(2) + i * cw(4.2f), Treb(cw(3), true), HUB_ACCENT);
                y += cw(2) + dl.Count * cw(4.2f) + cw(2);
            }
            if (signedIn)
            {
                var blocked = Social.Blocked;
                if (blocked.Count > 0)
                {
                    y = HubSetHead("BLOCKED PLAYERS", x, y);
                    for (int i = 0; i < blocked.Count; i++)
                    {
                        string id = blocked[i].PlayerId, nm = blocked[i].Name;
                        y = HubSetRow("accUnblock:" + id, nm, "Unblock", true, () => Run(Social.Unblock(id), nm + " is unblocked"), x, y, w);
                    }
                }
                y = HubSetHead("DELETE ACCOUNT", x, y);
                y = HubSetRow("accDelete", deleting ? "Deleting…" : "Delete account and data", null, false, () =>
                {
                    if (deleting) return;
                    if (!ConfirmTap("delete", "Tap again to delete your account and all its data")) return;
                    DeleteAccount();
                }, x, y, w, plain: true, labelCol: "#ff8a8a");
                var dn = Wrap("Deletes your player name, level, Rating, stats, friends and your place on the Top 100 from the game server. Your Google account is not touched. This cannot be undone.", w - cw(2), Treb(cw(2.8f)));
                for (int i = 0; i < dn.Count; i++) Text(dn[i], x + cw(1), y + cw(1) + i * cw(4), Treb(cw(2.8f)), "rgba(154,162,214,0.8)");
                y += cw(1) + dn.Count * cw(4);
            }
            return y;
        }
        static async void DeleteAccount()
        {
            deleting = true;
            try
            {
                bool ok = await Account.Delete();
                deleted = ok;
                Toast(ok ? "Your account is deleted" : "Could not delete the account: " + Account.LastError);
            }
            finally { deleting = false; }
        }
        // runs a friends action and shows its answer
        static async void Run(System.Threading.Tasks.Task<string> t, string okText)
        {
            string err = await t;
            if (err != null) Toast(err); else if (okText != null) Toast(okText);
        }
    }
}
