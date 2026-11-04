using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Online.Mp;
using BAM.Sim;
using UnityEngine;

// The "Play with friends" page: the old game's 1v1 page (Speltest ABC2/Game/Menu.cs PageMp, read live 2026-10-26) with its look
// line for line (panel, blue buttons, code field, lobby rows, the orange PLAY button for START), for a room of up to six phones.
// 2026-10-27: QUICK MATCH is back as ONLINE MATCH (MpRoom.QuickMatch), and the old "Online now" list with INVITE (Menu.MpOnline)
// and its invite card (Menu.DrawInvite) are copied in (MpSocial). Left out: the game server and the direct address.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static string codeDraft = "";
        static int createMode = MpRoom.Together;

        // the old Menu.Panel (ONE_PLAY look)
        static void Panel(float x, float y, float w, float h, float r = 12)
        {
            ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.3f); ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.stroke();
        }

        // the old Menu.MpBtn
        static void MpBtn(string key, string label, float x, float y, float w, float h, string col, Action a)
        {
            float oy = Down(key) ? 1.5f : 0; ctx.fillStyle = col; ctx.beginPath(); ctx.roundRect(x, y + oy, w, h, 10); ctx.fill();
            Text(label, x + w / 2, y + oy + h / 2, Treb(13, true), "#fff", "center"); Btn(key, x, y, w, h, a);
        }

        // the old Menu.LobbyRow: who, hero, and a state on the right
        static void LobbyRow(string who, string hero, string state, bool ok, float x, float y, float w)
        {
            ctx.fillStyle = "#0d1020"; ctx.beginPath(); ctx.roundRect(x, y, w, 30, 8); ctx.fill();
            ctx.font = Treb(13); string wl = who; while (wl.Length > 2 && ctx.measureText(wl) > 92) wl = wl.Substring(0, wl.Length - 2) + "…";
            ctx.font = Treb(12); float sw = ctx.measureText(state);
            ctx.font = Treb(13, true); string hl = hero ?? ""; float room = w - 8 - 96 - 8 - sw - 10; while (hl.Length > 2 && ctx.measureText(hl) > room) hl = hl.Substring(0, hl.Length - 2) + "…";
            Text(wl, x + 8, y + 15, Treb(13), "#a9a7c7"); Text(hl, x + 8 + 96, y + 15, Treb(13, true), "#f4f1ff");
            Text(state, x + w - 8, y + 15, Treb(12), ok ? "#8fffb0" : "#a9a7c7", "right");
        }

        // the old Home PLAY button's look (MenuHome.PlayButton) for START: orange when it can start, grey with the reason when not
        static void StartButton(string key, float x, float y, float w, float h, string label, Action a, string offWhy)
        {
            bool on = offWhy == null, dn = on && Down(key); float r = Math.Min(h * 0.32f, cw(5)), b = cw(0.7f), oy = dn ? cw(0.8f) : 0;
            ctx.fillStyle = "rgba(0,0,0,0.45)"; ctx.beginPath(); ctx.roundRect(x, y + (dn ? cw(0.2f) : cw(1)), w, h, r); ctx.fill();
            y += oy;
            ctx.fillStyle = "#0b1330"; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill();
            if (on)
            {
                ctx.fillStyle = "#ff8230"; ctx.beginPath(); ctx.roundRect(x + b, y + b, w - 2 * b, h - 2 * b, r - b); ctx.fill();
                var im = Img.Get("menu_btn_orange"); if (im != null && im.ok) { ctx.save(); ctx.beginPath(); ctx.roundRect(x + b, y + b, w - 2 * b, h - 2 * b, r - b); ctx.clip(); ctx.drawImage(im, x + b, y + b, w - 2 * b, h - 2 * b); ctx.restore(); }
                if (dn) { ctx.fillStyle = "rgba(60,20,0,0.18)"; ctx.beginPath(); ctx.roundRect(x + b, y + b, w - 2 * b, h - 2 * b, r - b); ctx.fill(); }
                float size = 20;
                Text(label, x + w / 2, y + h / 2, Lil(size), "#fff", "center", shadow: "#b04a00", sh: size * 0.083f, stroke: "#8a3200", sw: size * 0.07f);
                Btn(key, x, y, w, h, a);
            }
            else
            {
                ctx.fillStyle = VGrad(y, y + h, "#5a6094", "#3a3f72"); ctx.beginPath(); ctx.roundRect(x + b, y + b, w - 2 * b, h - 2 * b, r - b); ctx.fill();
                Text(label, x + w / 2, y + h * 0.4f, Lil(16), "rgba(255,255,255,0.75)", "center", shadow: "#262b55", sh: 1);
                Text(offWhy, x + w / 2, y + h * 0.74f, Treb(11, true), "rgba(255,255,255,0.75)", "center");
                Btn(key, x, y, w, h, () => Toast(offWhy));
            }
        }

        static string ModeLine(int mode, int players) => mode == MpRoom.Together
            ? "You " + (players <= 2 ? "two" : "three") + " on one team" + (players < 3 ? " + " + (3 - players) + " bot" + (players == 2 ? "" : "s") : "") + " against 3 bots"
            : "Two teams of three, the empty places filled with bots";

        static void ModeButtons(float x, float y, float w, int mode, Action<int> pick, bool draw)
        {
            if (!draw) return;
            float bw = (w - 6) / 2;
            for (int i = 0; i < 2; i++)
            {
                int m = i;
                MpBtn("mpMode" + i, i == 0 ? "TOGETHER" : "AGAINST EACH OTHER", x + i * (bw + 6), y, bw, 30, mode == m ? "#2fae5b" : "#39407a", () => pick(m));
            }
        }

        // the bots' level (Rickard 2026-10-30: which bots do you meet when playing together?): EASY / NORMAL / HARD
        static readonly string[] DiffNames = { "EASY", "NORMAL", "HARD", "ULTRA" };
        static void DiffButtons(float x, float y, float w, Difficulty d, Action<Difficulty> pick, bool draw)
        {
            if (draw) Text("Bots", x, y + 13, Treb(12, true), "#ffb43a");
            int n = DiffNames.Length; float lx = 44, bw = (w - lx - 4 * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var k = (Difficulty)i;
                // Hard opens after 3 wins against Normal bots (Rickard 2026-11-04), Ultra after 30 wins against Hard bots
                bool hardLocked = k == Difficulty.Hard && !BamSettings.HardOpen;
                bool locked = hardLocked || (k == Difficulty.Ultra && !BamSettings.UltraOpen);
                string count = hardLocked ? Mathf.Min(BamSettings.NormalWins, BamSettings.NormalWinsForHard) + "/" + BamSettings.NormalWinsForHard
                    : Mathf.Min(BamSettings.HardWins, BamSettings.UltraWins) + "/" + BamSettings.UltraWins;
                if (draw) MpBtn("mpDiff" + i, locked ? DiffNames[i] + " " + count : DiffNames[i], x + lx + i * (bw + 4), y, bw, 26, d == k ? "#2fae5b" : locked ? "#262a4a" : "#39407a", () => { if (!locked) pick(k); });
            }
        }

        // the hero this player plays in the room: a row of portraits, the picked one framed
        static void HeroRow(float x, float y, float w, HeroId picked, bool draw)
        {
            if (!draw) return;
            int n = HeroCatalog.Count; float gap = 4, s = (w - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var info = HeroCatalog.All[i]; var id = info.Id; float hx = x + i * (s + gap); // list order, Tyra first
                bool on = id == picked;
                ctx.fillStyle = on ? "#2fae5b" : "#0d1020"; ctx.beginPath(); ctx.roundRect(hx, y, s, s, 8); ctx.fill();
                var im = HeroIcon(info.Name);
                if (im != null && im.ok) { ctx.save(); ctx.beginPath(); ctx.roundRect(hx + 2, y + 2, s - 4, s - 4, 6); ctx.clip(); Cover(im, hx + 2, y + 2, s - 4, s - 4, 0.2f); ctx.restore(); }
                else Text(info.Name.Substring(0, 1), hx + s / 2, y + s / 2, Lil(s * 0.5f), "#fff", "center");
                if (!on) { ctx.fillStyle = "rgba(10,13,38,0.45)"; ctx.beginPath(); ctx.roundRect(hx, y, s, s, 8); ctx.fill(); }
                if (!Progress.HeroOpen(id)) // opened by the account level (Progress.HeroLevel): veiled with the level, no pick
                {
                    ctx.fillStyle = "rgba(8,10,30,0.62)"; ctx.beginPath(); ctx.roundRect(hx, y, s, s, 8); ctx.fill();
                    Text("LV " + Progress.HeroLevel(id), hx + s / 2, y + s / 2, Treb(Math.Max(9, s * 0.26f), true), "#e8eaff", "center");
                    continue;
                }
                Btn("mpHero" + i, hx, y, s, s, () => MpRoom.Pick(id));
            }
        }

        static void TextLines(List<string> l, float x, float y, string col, bool draw, float lh = 16)
        {
            if (!draw) return;
            for (int i = 0; i < l.Count; i++) Text(l[i], x, y + 8 + i * lh, Treb(12), col);
        }

        // ---------- the page ----------
        static float PageMp(float x, float y, float w)
        {
            y = H2("Multiplayer", "Online", x, y);
            float h = MpBody(x, y, w, false);
            Panel(x, y, w, h);
            MpBody(x, y, w, true);
            y += h + 10;
            if (Account.State == Account.Status.Ready)
            {
                h = MpOnlineH(w);
                Panel(x, y, w, h);
                MpOnline(x + 12, y + 6, w - 24);
                y += h + 10;
            }
            const string help = "Both phones need internet and the game open. Pick TOGETHER (you and your friends against bots) or AGAINST EACH OTHER, then press INVITE next to a friend. They get a card to accept, and when they are in you press START. Everyone plays the hero they pick here.";
            var hl = Wrap(help, w - 4, Treb(11)); for (int i = 0; i < hl.Count; i++) Text(hl[i], x + 2, y + 8 + i * 15, Treb(11), "#a9a7c7");
            return y + hl.Count * 15 + 14;
        }

        // measures (draw false) or draws the panel's content; returns its height
        static float MpBody(float x, float y0, float w, bool draw)
        {
            float ix = x + 12, iw = w - 24, rowH = 38, y = y0 + 10;
            if (draw) Text("Play with friends", ix, y0 + 10 + 9, Treb(14, true), "#ffb43a");
            y += 22;
            if (Previewing) { y += SearchCard(ix, y, iw, rowH, draw); return y - y0 + 6; } // BAM > Test Online Search (editor)
            if (PreviewError) { y += SearchErrorCard(ix, y, iw, rowH, draw); return y - y0 + 6; }
            var st = MpRoom.State;
            bool signedIn = Account.State == Account.Status.Ready;
            if (!signedIn && st == MpRoom.Phase.Off)
            {
                var l = Wrap("Sign in with Google Play Games to play with your friends.", iw, Treb(12));
                TextLines(l, ix, y, "#a9a7c7", draw); y += l.Count * 16 + 6;
                if (draw) MpBtn("mpSignIn", "SIGN IN", ix, y, iw, rowH, "#3a6cff", () => Go(Account.State == Account.Status.NeedsName ? "hubname" : "golive"));
                y += rowH + 6;
                return y - y0 + 6;
            }
            if (MpRoom.Quick && st != MpRoom.Phase.Match)
            {
                // ONLINE MATCH: in the queue, alone or as a group of up to three (OnlineSearch.cs: searching, joining, match found)
                y += SearchCard(ix, y, iw, rowH, draw);
                return y - y0 + 6;
            }
            if (st == MpRoom.Phase.Off)
            {
                if (!string.IsNullOrEmpty(MpRoom.QuickError)) y += SearchErrorCard(ix, y, iw, rowH, draw); // the last search stopped by itself
                else
                {
                    if (draw) StartButton("mpQuick", ix, y, iw, rowH + 8, "ONLINE MATCH", MpRoom.QuickMatch, null);
                    y += rowH + 14;
                    var qh = Wrap("Play someone who is searching right now, or wait for the next one. Bots fill the empty places.", iw, Treb(12));
                    TextLines(qh, ix, y, "#a9a7c7", draw); y += qh.Count * 16 + 10;
                }
                // with friends: pick the mode, then INVITE someone (Rickard 2026-10-27: no codes to type, invite and start).
                // The room and its code are made by the first INVITE and only carried inside the invite.
                if (draw) Text("Or with friends:", ix, y + 8, Treb(13, true), "#ffb43a");
                y += 22;
                ModeButtons(ix, y, iw, createMode, m => createMode = m, draw); y += 36;
                DiffButtons(ix, y, iw, BamSettings.LastDifficulty, d => BamSettings.LastDifficulty = d, draw); y += 32;
                var ml = Wrap(ModeLine(createMode, 2), iw, Treb(12)); TextLines(ml, ix, y, "#a9a7c7", draw); y += ml.Count * 16 + 4;
                var il = Wrap("Press INVITE FRIENDS, or INVITE next to someone below. When they accept, you press START.", iw, Treb(12)); TextLines(il, ix, y, "#f4f1ff", draw); y += il.Count * 16 + 6;
                if (draw) MpBtn("mpFriends", "INVITE FRIENDS", ix, y, iw, rowH, "#3a6cff", OpenInvite);
                y += rowH + 6;
            }
            else if (st == MpRoom.Phase.Connecting)
            {
                if (draw) MpBtn("mpCancel", "CANCEL", ix, y, iw, rowH, "#39407a", MpRoom.Leave);
                y += rowH + 6;
            }
            else
            {
                // the room
                if (MpRoom.IsHost)
                {
                    // no code on screen: more friends are invited from the list below or from Friends
                    if (draw) Text("Your room", ix, y + 10, Treb(13, true), "#f4f1ff");
                    if (draw) MpBtn("mpFriends", "INVITE MORE", ix + iw - 120, y, 120, 30, "#3a6cff", OpenInvite);
                    y += 36;
                }
                var members = MpRoom.Members; int mode = MpRoom.Mode;
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    string who = m.Id == MpRoom.MyId ? "You" : m.Name;
                    string state = m.InMatch ? "in match" : i == 0 ? "host" : m.Ping > 0 ? Mathf.RoundToInt(m.Ping) + " ms" : "joined";
                    if (mode == MpRoom.Versus) state = (MpRoom.TeamOf(i, mode) == Teams.Player ? "Blue" : "Red") + " · " + state;
                    if (draw) LobbyRow(who, HeroCatalog.Get(m.Hero).Name, state, !m.InMatch, ix, y, iw);
                    y += 34;
                }
                y += 4;
                if (MpRoom.IsHost) { ModeButtons(ix, y, iw, mode, MpRoom.SetMode, draw); y += 36; DiffButtons(ix, y, iw, MpRoom.Diff, MpRoom.SetDiff, draw); y += 32; }
                else { var gl = Wrap("Mode: " + MpRoom.ModeNames[mode] + ", bots " + DiffNames[(int)MpRoom.Diff] + " (the host picks)", iw, Treb(12)); TextLines(gl, ix, y, "#f4f1ff", draw); y += gl.Count * 16; }
                var ml = Wrap(ModeLine(mode, Math.Max(2, members.Count)), iw, Treb(12)); TextLines(ml, ix, y, "#a9a7c7", draw); y += ml.Count * 16 + 6;
                if (draw) Text("Your hero", ix, y + 8, Treb(12, true), "#ffb43a");
                y += 18;
                var me = MpRoom.Me;
                HeroRow(ix, y, iw, me != null ? me.Hero : (HeroId)BamSettings.LastHero, draw);
                y += (iw - 4 * (HeroCatalog.Count - 1)) / HeroCatalog.Count + 10;
                if (MpRoom.IsHost)
                {
                    string off = members.Count < 2 ? "Waiting for friends to join" : members.Count > MpRoom.Capacity(mode) ? "Too many players for this mode" : MpRoom.State == MpRoom.Phase.Match ? "A match is on" : null;
                    if (draw) StartButton("mpStart", ix, y, iw - 106, rowH + 8, "START", MpRoom.StartMatch, off);
                    if (draw) MpBtn("mpLeave", "LEAVE", ix + iw - 100, y + 4, 100, rowH, "#39407a", MpRoom.Leave);
                    y += rowH + 14;
                    if (members.Count >= 2 && members.Count <= MpRoom.MaxGroup && MpRoom.State == MpRoom.Phase.Lobby)
                    {
                        // the group against another group (or a single player) from the queue
                        if (draw) MpBtn("mpGroupQuick", "ONLINE MATCH: PLAY ANOTHER GROUP", ix, y, iw, rowH, "#2fae5b", MpRoom.QuickMatch);
                        y += rowH + 6;
                    }
                }
                else
                {
                    string host = members.Count > 0 ? members[0].Name : "the host";
                    if (draw) Text(MpRoom.State == MpRoom.Phase.Match ? "Starting…" : "Waiting for " + host + " to start…", ix, y + rowH / 2, Treb(13, true), "#8fffb0");
                    if (draw) MpBtn("mpLeave", "LEAVE", ix + iw - 100, y, 100, rowH, "#39407a", MpRoom.Leave);
                    y += rowH + 6;
                }
            }
            if (st == MpRoom.Phase.Off && Debug.isDebugBuild)
            {
                // test builds: a bad network on purpose for the next room or ONLINE MATCH (Unity Transport's simulator, each way)
                var p = MpNetTest.Profiles;
                if (draw) Text("Network test (test builds only):", ix, y + 8, Treb(12, true), "#a9a7c7");
                y += 18;
                float sw = (iw - 4 * (p.Length - 1)) / p.Length;
                for (int i = 0; i < p.Length; i++)
                {
                    int k = i;
                    if (draw) MpBtn("mpNet" + i, p[i].name.ToUpperInvariant(), ix + i * (sw + 4), y, sw, 26, MpNetTest.Pick == k ? "#c0392b" : "#39407a", () => MpNetTest.Pick = k);
                }
                y += 30;
                if (MpNetTest.Pick > 0)
                {
                    var s = p[MpNetTest.Pick];
                    var nl = Wrap("+" + s.delay + " ms each way, jitter " + s.jitter + " ms, " + s.drop + "% lost. Both phones should pick the same.", iw, Treb(11));
                    TextLines(nl, ix, y, "#ffb0b0", draw, 15); y += nl.Count * 15 + 4;
                }
            }
            if (!string.IsNullOrEmpty(MpRoom.Status) && MpRoom.Status != MpRoom.QuickError) // the error card already says it
            {
                var sl = Wrap(MpRoom.Status, iw, Treb(12)); TextLines(sl, ix, y, "#ffb0b0", draw); y += sl.Count * 16 + 6;
            }
            return y - y0 + 6;
        }

        // the mode picked before CREATE ROOM is set once the room exists
        static int pendingMode = -1;
        internal static void MpTick()
        {
            if (Account.State == Account.Status.Ready) MpRoom.Ensure(); // the online list and invites run from the room pump
            MpPresence.Watching = page == "mp" || page == "friends" || page == "invite";
            if (MpRoom.State == MpRoom.Phase.Match && MpPresence.Invite != null) MpPresence.Invite = null;
            if (pendingMode >= 0 && MpRoom.IsHost && MpRoom.State == MpRoom.Phase.Lobby) { MpRoom.SetMode(pendingMode); pendingMode = -1; }
            if (MpRoom.State == MpRoom.Phase.Off) pendingMode = -1;
            // a match started: the page gives way; the match is over and the menus are back: Home, where the party is
            if (MpRoom.State == MpRoom.Phase.Match && (page == "mp" || page == "leaveparty" || page == "invite")) Close();
            if (MpRoom.WantPage && Flow.Game != null && !Flow.Game.InMatch) { MpRoom.WantPage = false; Close(); }
            SearchTick();
            PartyTick();
        }

        // ---------- Online now (the old Menu.MpOnline) ----------
        const float MP_ONLINE_ROW = 34;
        static float MpOnlineH(float w) => 22 + Math.Max(1, MpPresence.Online.Count) * MP_ONLINE_ROW + 4 + 36 + MissingLines(w).Count * 15;

        // friends the Friends service shows online who are not in Online now, and why (Rickard 2026-10-30: FUL online in Friends,
        // "Nobody else right now" here). Most often another game version: those phones cannot play each other.
        static readonly List<string> missing = new List<string>();
        static List<string> MissingLines(float w)
        {
            missing.Clear();
            if (!MpSocial.ShowMe || !MpPresence.Looked) return missing;
            var sb = new System.Text.StringBuilder();
            foreach (var f in Social.Friends)
            {
                if (f.Presence == Social.Presence.Offline || MpPresence.Find(f.PlayerId) != null) continue;
                string why = !string.IsNullOrEmpty(f.Version) && f.Version != Social.GameVersion
                    ? " has version " + f.Version + ", you have " + Social.GameVersion + ". Both need the same version from Google Play."
                    : " is online but not here yet: their game may still be connecting, or Show me as online is off on their phone.";
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(f.Name).Append(why);
            }
            if (sb.Length > 0) missing.AddRange(Wrap(sb.ToString(), w - 24, Treb(11)));
            return missing;
        }
        static void MpOnline(float x, float y, float w)
        {
            Text("Online now:", x, y + 8, Treb(12, true), "#ffb43a");
            float ry = y + 22; var list = MpPresence.Online;
            if (!MpSocial.ShowMe) Text("Hidden: turn on Show me as online below.", x, ry + 12, Treb(12), "#a9a7c7");
            else if (list.Count == 0)
            {
                string s = MpPresence.Status.Length > 0 ? MpPresence.Status : MpPresence.Looked ? "Nobody else right now. Ask your friend to open the game." : "Looking…";
                Text(s, x, ry + 12, Treb(12), "#a9a7c7");
            }
            else
                for (int i = 0; i < list.Count; i++)
                {
                    var r = list[i]; float yy = ry + i * MP_ONLINE_ROW; bool free = r.Free;
                    bool friend = Social.RelationTo(r.Owner) == Social.Relation.Friends;
                    ctx.fillStyle = "#0d1020"; ctx.beginPath(); ctx.roundRect(x, yy, w, 30, 8); ctx.fill();
                    ctx.fillStyle = free ? "#5fe39a" : "#ffb43a"; ctx.beginPath(); ctx.arc(x + 12, yy + 15, 4, 0, (float)Math.PI * 2); ctx.fill();
                    string nm = r.Name; ctx.font = Treb(13, true); while (nm.Length > 3 && ctx.measureText(nm) > 92) nm = nm.Substring(0, nm.Length - 2) + "…";
                    Text(nm, x + 22, yy + 15, Treb(13, true), friend ? HUB_ACCENT : "#f4f1ff");
                    string hero = r.Hero >= 0 && r.Hero < HeroCatalog.Count ? HeroCatalog.Get((HeroId)r.Hero).Name + " · " : "";
                    Text(hero + PresenceState.Text(r.State), x + 22 + 100, yy + 15, Treb(11), "#a9a7c7");
                    if (!free) continue;
                    var row = r;
                    if (MpSocial.IsInvited(r.Owner)) Text("INVITED", x + w - 43, yy + 15, Treb(12, true), "#8fffb0", "center");
                    else MpBtn("mpInv" + i, "INVITE", x + w - 84, yy + 2, 82, 26, "#2fae5b", () => InviteSomeone(row.Owner, row.Name));
                }
            float ty = ry + Math.Max(1, list.Count) * MP_ONLINE_ROW + 4;
            var ml = MissingLines(w + 24);
            for (int i = 0; i < ml.Count; i++) Text(ml[i], x, ty + 6 + i * 15, Treb(11), "#ffb0b0");
            ty += ml.Count * 15;
            Text("Show me as online", x, ty + 15, Treb(12), "#f4f1ff");
            string col = HubToggle("mpShowMe", x + w - cw(12), ty + 3, cw(12), 24, 12, MpSocial.ShowMe, true);
            Text(MpSocial.ShowMe ? "ON" : "OFF", x + w - cw(6), ty + 15, Treb(12, true), col, "center");
            Btn("mpShowMe", x + w - cw(12), ty, cw(12), 30, () => MpSocial.ShowMe = !MpSocial.ShowMe);
        }

        internal static void InviteSomeone(string playerId, string name)
        {
            var why = MpSocial.Invite(playerId, name, createMode);
            Toast(why ?? "Invite sent to " + name);
            // the invite panel stays open: more can be invited; the party shows on Home
        }

        // ---------- the invite card (the old Menu.DrawInvite): covers everything, ACCEPT joins the inviter's room ----------
        internal static bool InviteShown => MpPresence.Invite != null && !(Flow.Game != null && Flow.Game.InMatch);
        static void DrawInvite(PresenceRow r)
        {
            hits.Clear();
            ctx.fillStyle = "rgba(4,8,30,0.85)"; ctx.fillRect(0, 0, W, H);
            Btn("invScrim", 0, 0, W, H, null);
            float pw = Math.Min(cw(84), H * 0.62f), px = (W - pw) / 2, th = pw * 0.16f, lh = pw * 0.075f, bh = pw * 0.12f;
            float ph = th + lh * 5.2f + bh * 3 + pw * 0.2f, py = (H - ph) / 2, cx = W / 2;
            ctx.fillStyle = "#0e2840"; ctx.fillRect(px, py, pw, ph);
            ctx.fillStyle = "#14123e"; ctx.fillRect(px, py, pw, th);
            ctx.strokeStyle = "#2b3f72"; ctx.lineWidth = 2; ctx.strokeRect(px, py, pw, ph);
            Text("INVITE", cx, py + th * 0.55f, Lil(th * 0.5f), "#2ee6c8", "center", "middle", "rgba(0,0,0,0.6)", th * 0.04f);
            float ty = py + th + lh * 1.2f; int m = Math.Max(0, Math.Min(MpRoom.ModeNames.Length - 1, r.InvMode));
            Text(r.Name + " invites you", cx, ty, Treb(lh * 0.82f, true), "#f4f1ff", "center");
            Text(MpRoom.ModeNames[m] + ": " + ModeLine(m, 2), cx, ty + lh, Treb(lh * 0.6f), "#a9b8d8", "center");
            Text("You play " + HeroCatalog.Get((HeroId)Mathf.Clamp(BamSettings.LastHero, 0, HeroCatalog.Count - 1)).Name, cx, ty + lh * 2, Treb(lh * 0.7f), "#a9b8d8", "center");
            Text(MpPresence.InviteLeft(r) + " s", cx, ty + lh * 3, Treb(lh * 0.7f, true), "#ffd98a", "center"); // the invite runs out (MpPresence.InviteTtl)
            float bw = pw * 0.6f, bx = cx - bw / 2, by = py + th + lh * 5.2f;
            float oy = Down("invOk") ? 1.5f : 0; ctx.fillStyle = "#1fb46a"; ctx.fillRect(bx, by + oy, bw, bh);
            Text("ACCEPT", cx, by + oy + bh / 2, Lil(bh * 0.45f), "#fff", "center");
            Btn("invOk", bx, by, bw, bh, () => { MpSocial.Accept(r); Close(); }); // into the party on Home
            by += bh + pw * 0.04f; oy = Down("invNo") ? 1.5f : 0;
            ctx.fillStyle = "rgba(255,255,255,0.12)"; ctx.fillRect(bx, by + oy, bw, bh);
            Text("NO THANKS", cx, by + oy + bh / 2, Lil(bh * 0.45f), "#fff", "center");
            Btn("invNo", bx, by, bw, bh, () => MpSocial.Decline(r));
            by += bh + pw * 0.04f;
            Text("Block " + r.Name, cx, by + bh * 0.4f, Treb(lh * 0.6f, true), "#ff8a8a", "center");
            Btn("invBlock", bx, by, bw, bh * 0.8f, () => { MpSocial.Block(r); Toast(r.Name + " is blocked"); });
        }
    }
}
