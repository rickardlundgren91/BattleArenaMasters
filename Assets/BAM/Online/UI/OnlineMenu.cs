using System;
using System.Collections.Generic;
using System.Globalization;
using BAM.Audio;
using UnityEngine;

// The online pages, ported from the old game's Home style (Speltest ABC2/Game: Menu.cs, MenuHome.cs, MenuSocial.cs, MenuFriends3.cs,
// MenuProgress.cs, MenuV3.cs, read live 2026-10-26). They are drawn the same way as there: an HTML-canvas emulator (Ctx.cs) in the
// old 400 "CSS pixel" wide frame, cw(v) = v % of the frame width, so sizes, colours and fonts are the old ones line for line.
// This file holds the shared parts (hit regions, scrolling, text fields, Back history, toasts, the drawing helpers); the pages are
// in OnlinePages.cs, OnlineSocialPages.cs and OnlineFriends.cs. What differs from Speltest is written where it is: the data comes
// from BAM.Online (server-checked), and party, invites, chat, gear and shop are left out.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static Ctx ctx; static float W, H;
        static float cw(float v) => W * v / 100f;
        static string N(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static readonly Dictionary<float, string> lilF = new Dictionary<float, string>(), trebF = new Dictionary<float, string>(), trebBF = new Dictionary<float, string>();
        static string FontStr(Dictionary<float, string> d, float px, string pre, string fam)
        {
            if (d.TryGetValue(px, out var f)) return f;
            if (d.Count > 512) d.Clear();
            return d[px] = pre + N(px) + "px " + fam;
        }
        static string Lil(float px) => FontStr(lilF, px, "", "\"Lilita One\"");
        static string Treb(float px, bool bold = false) => bold ? FontStr(trebBF, px, "bold ", "\"Trebuchet MS\"") : FontStr(trebF, px, "", "\"Trebuchet MS\"");
        static float Min11(float px) => px; // UiV3.BIGGER is off in the old game's shipped look
        static float Now => Time.realtimeSinceStartup;

        // the old colours (MenuHome.cs, MenuHome3.cs; ONE_PLAY look)
        const string HUB_BG0 = "#0e1230", HUB_ACCENT = "#3ee6b0", HUB_TEXT = "#f3f4ff", HUB_DIM = "#9aa2d6", HUB_GOLD = "#d9a441";
        const string HUB_PANEL = "rgba(16,20,54,0.8)", HUB_LINE = "rgba(217,164,65,0.42)";

        // ---------- text wrapping (Hud.wrapLines) ----------
        static readonly Dictionary<(string, float, string), List<string>> wrapped = new Dictionary<(string, float, string), List<string>>();
        static List<string> Wrap(string t, float maxW, string font)
        {
            ctx.font = font;
            var k = (t, maxW, font); if (wrapped.TryGetValue(k, out var l)) return l;
            if (wrapped.Count > 256) wrapped.Clear();
            l = new List<string>(); string line = "";
            foreach (var word in (t ?? "").Split(' '))
            {
                string test = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && ctx.measureText(test) > maxW) { l.Add(line); line = word; }
                else line = test;
            }
            if (line.Length > 0 || l.Count == 0) l.Add(line);
            return wrapped[k] = l;
        }

        // ---------- pages and Back history (Menu.Go, MenuHome.HubTrack / HubBack) ----------
        // page null = nothing open: the game's own menus show and get the touches
        internal static string page;
        static float pageScroll, contentH, viewTop, viewBot;
        static float scrollOff; static bool clipOn; static float clipTop, clipBot;
        static readonly List<string> history = new List<string>();
        static readonly List<float> histScroll = new List<float>();
        static bool backNav;

        internal static void Go(string p)
        {
            string from = page;
            if (p == "hubname" && from != "hubname") { hubNameDraft = Account.Name ?? Suggest(Account.PlatformName); hubConfirm = null; nameAnswer = null; }
            if (p != "hubname") hubConfirm = null;
            hubSocialConfirm = hubReportFor = null;
            if (p != "friends" && p != "invite") { friendMenu = friendRemove = null; }
            // a toast from before the page opened goes with it (video review 2026-11-03: "You left the party" lay over the
            // Invite panel); toasts the new page raises itself come after this and show as usual
            if (from != p) hubToast = null;
            EndInput();
            if (!backNav && from != null && from != p)
            {
                int i = history.IndexOf(p);
                if (i >= 0) { history.RemoveRange(i, history.Count - i); histScroll.RemoveRange(i, histScroll.Count - i); }
                else { history.Add(from); histScroll.Add(pageScroll); if (history.Count > 12) { history.RemoveAt(0); histScroll.RemoveAt(0); } }
            }
            backNav = false;
            page = p; pageScroll = 0;
            if (p == "rank" && hubTop100) _ = Leaderboard.Load();
        }
        // the Play Games name as a first suggestion, with what the name rules do not allow taken out
        static string Suggest(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') b.Append(c);
            return b.Length > NameRules.Max ? b.ToString(0, NameRules.Max) : b.ToString();
        }
        internal static void Close() { EndInput(); page = null; history.Clear(); histScroll.Clear(); hubSocialConfirm = hubReportFor = hubConfirm = null; friendMenu = friendRemove = null; }
        static void HubBack()
        {
            if (history.Count == 0) { Close(); return; }
            string prev = history[history.Count - 1]; float sc = histScroll[histScroll.Count - 1];
            history.RemoveAt(history.Count - 1); histScroll.RemoveAt(histScroll.Count - 1);
            backNav = true; Go(prev); pageScroll = sc;
        }
        // Back and the Android Back button: the innermost open layer first (MenuSocial.HubOverlayBack). True when something closed.
        internal static bool Back()
        {
            if (InviteShown) { BAM.Online.Mp.MpSocial.Decline(BAM.Online.Mp.MpPresence.Invite); return true; }
            if (page == null) return false;
            if (hubReportFor != null) { hubReportFor = null; return true; }
            if (hubSocialConfirm != null) { hubSocialConfirm = null; return true; }
            if (hubConfirm != null) { hubConfirm = null; return true; }
            if (page == "friends") { HubFriendsBack(); return true; }
            HubBack();
            return true;
        }

        // ---------- toasts (MenuHome.Toast) ----------
        static string hubToast; static float hubToastT;
        internal static void Toast(string t) { hubToast = t; hubToastT = Now; }
        /// <summary>A toast is showing: drawn over Home too, where no online page is open (the party's notices).</summary>
        internal static bool ToastLive => hubToast != null && Now - hubToastT < 2.4f;
        static void DrawToast()
        {
            if (hubToast == null || Now - hubToastT >= 2.4f) return;
            float topH = cw(14);
            var lines = Wrap(hubToast, W - cw(16), Treb(cw(3.4f), true)); float lh = cw(4.6f), th = cw(4.4f) + lines.Count * lh, tw = W - cw(10), tx = cw(5), ty = topH + cw(2);
            ctx.globalAlpha = Mathf.Clamp01((2.4f - (Now - hubToastT)) * 3);
            ctx.fillStyle = "rgba(0,0,0,0.82)"; ctx.beginPath(); ctx.roundRect(tx, ty, tw, th, cw(3)); ctx.fill();
            for (int i = 0; i < lines.Count; i++) Text(lines[i], W / 2, ty + cw(2.2f) + lh / 2 + i * lh, Treb(cw(3.4f), true), HUB_TEXT, "center");
            ctx.globalAlpha = 1;
        }
        // a second tap within 3 s confirms (MenuNewPlayer.ConfirmTap)
        static string askKey; static float askT;
        static bool ConfirmTap(string key, string ask)
        {
            if (askKey != key || Now - askT > 3) { askKey = key; askT = Now; Toast(ask); return false; }
            askKey = null; return true;
        }

        // ---------- hit regions (rebuilt every frame, topmost last) ----------
        class Hit { public string key; public float x, y, w, h; public Action a; }
        static readonly List<Hit> hits = new List<Hit>(), hitPool = new List<Hit>(); static int hitsUsed;
        static void Btn(string key, float x, float y, float w, float h, Action a)
        {
            float sy = y - scrollOff, sy2 = sy + h;
            if (clipOn) { sy = Math.Max(sy, clipTop); sy2 = Math.Min(sy2, clipBot); if (sy2 <= sy) return; }
            if (hitsUsed == hitPool.Count) hitPool.Add(new Hit());
            var ht = hitPool[hitsUsed++]; ht.key = key; ht.x = x; ht.y = sy; ht.w = w; ht.h = sy2 - sy; ht.a = a; hits.Add(ht);
        }
        static Hit HitAt(float x, float y) { for (int i = hits.Count - 1; i >= 0; i--) { var h = hits[i]; if (x >= h.x && x <= h.x + h.w && y >= h.y && y <= h.y + h.h) return h; } return null; }
        static string downKey; static float downX, downY, lastY; static bool dragging; static int downId = -1;
        static bool InPage(float y) => y >= viewTop && y <= viewBot;
        internal static void PointerDown(float x, float y, int id)
        {
            if (downId >= 0) return; downId = id; downX = x; downY = y; lastY = y; dragging = false;
            var h = HitAt(x, y); downKey = h?.key;
        }
        internal static void PointerMove(int id, float x, float y)
        {
            if (id != downId) return;
            if (!dragging && InPage(downY) && Math.Abs(y - downY) > 8) dragging = true;
            if (dragging) { pageScroll -= y - lastY; ClampScroll(); }
            lastY = y;
        }
        internal static void PointerUp(int id, float x, float y)
        {
            if (id != downId) return; downId = -1;
            if (dragging) { dragging = false; downKey = null; return; }
            var h = HitAt(x, y);
            if (h == null || !h.key.StartsWith("in:", StringComparison.Ordinal)) EndInput();
            if (h != null && h.key == downKey) { if (h.a != null) BamAudio.Play(SfxId.UiTap); h.a?.Invoke(); }
            downKey = null;
        }
        internal static void ResetPointer() { downId = -1; downKey = null; dragging = false; }
        internal static void Wheel(float dy) { pageScroll += dy; ClampScroll(); }
        static void ClampScroll() { pageScroll = Mathf.Clamp(pageScroll, 0, Math.Max(0, contentH - (viewBot - viewTop))); }
        static bool Down(string key) => downKey == key && downId >= 0;

        // ---------- drawing helpers (Menu.cs) ----------
        static void Text(string t, float x, float y, string font, object col, string align = "left", string baseline = "middle", string shadow = null, float sh = 0, string stroke = null, float sw = 0)
        {
            ctx.font = font; ctx.textAlign = align; ctx.textBaseline = baseline;
            if (shadow != null) { ctx.fillStyle = shadow; ctx.fillText(t, x, y + sh); }
            if (stroke != null) { ctx.lineWidth = sw; ctx.strokeStyle = stroke; ctx.strokeText(t, x, y); }
            ctx.fillStyle = col; ctx.fillText(t, x, y);
        }
        static void Contain(Img im, float x, float y, float w, float h, float alpha = 1)
        {
            if (im == null || !im.ok) return; float s = Math.Min(w / im.w, h / im.h); float dw = im.w * s, dh = im.h * s;
            float a0 = ctx.globalAlpha; ctx.globalAlpha = a0 * alpha; ctx.drawImage(im, x + (w - dw) / 2, y + (h - dh) / 2, dw, dh); ctx.globalAlpha = a0;
        }
        static void Cover(Img im, float x, float y, float w, float h, float py = 0.5f)
        {
            if (im == null || !im.ok) return; float s = Math.Max(w / im.w, h / im.h), sw = w / s, sh2 = h / s;
            ctx.drawImage(im, (im.w - sw) / 2, (im.h - sh2) * py, sw, sh2, x, y, w, h);
        }
        static Grad VGrad(float y0, float y1, string a, string b) { var g = ctx.createLinearGradient(0, y0, 0, y1); g.addColorStop(0, a); g.addColorStop(1, b); return g; }

        // Rickard's hero portraits (Resources/BAM/Menu/portrait_<hero>), the same pictures as the game's own menus
        static Img HeroIcon(string hero) => Img.Get("portrait_" + (hero ?? "").ToLowerInvariant());
        // the heroes in the game, from the one list (Bonk was missing from this private copy, bug review 2026-11-02 nr 36, then Kugg,
        // full review 2026-11-09 nr 82): a hero's portrait file is portrait_<id in lower case>, as the game's own menus
        static readonly string[] HEROES = Array.ConvertAll(BAM.Sim.HeroCatalog.All, h => h.Id.ToString());
        static bool IsHero(string h) => Array.IndexOf(HEROES, h) >= 0;

        // ---------- the page frame (Menu.DrawPage, MenuHome.HubPage) ----------
        static void DrawPage(float top, float bottom)
        {
            float r = cw(4); viewTop = top; viewBot = bottom;
            ctx.fillStyle = "rgba(0,0,0,0.35)"; ctx.beginPath(); ctx.roundRect(0, top - cw(1), W, bottom - top + cw(1), new[] { r, r, 0, 0 }); ctx.fill();
            ctx.fillStyle = VGrad(top, bottom, "#1a1f4a", "#0a0d26");
            ctx.beginPath(); ctx.roundRect(0, top, W, bottom - top, new[] { r, r, 0, 0 }); ctx.fill();
            ctx.strokeStyle = HUB_GOLD; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(0, top, W, bottom - top + r, new[] { r, r, 0, 0 }); ctx.stroke();
            float px = cw(3.5f), w = W - 2 * px;
            ctx.save(); ctx.beginPath(); ctx.rect(0, top, W, bottom - top); ctx.clip();
            clipOn = true; clipTop = top; clipBot = bottom; scrollOff = pageScroll;
            ctx.translate(0, -pageScroll);
            float y = top + cw(2); float end = y;
            switch (page)
            {
                case "golive": end = PageGoOnline(px, y, w); break;
                case "profile": end = PageProfile(px, y, w); break;
                case "hubname": end = PageHubName(px, y, w); break;
                case "rank": end = PageRank(px, y, w); break;
                case "pprofile": end = PagePublicProfile(px, y, w); break;
                case "account": end = PageAccount(px, y, w); break;
                case "mp": end = PageMp(px, y, w); break;
            }
            contentH = end - top + cw(4); ClampScroll();
            ctx.restore(); clipOn = false; scrollOff = 0;
        }
        static void HubPage()
        {
            // the Home picture behind the page, as the old pages had the Home background above their panel
            ctx.fillStyle = HUB_BG0; ctx.fillRect(0, 0, W, H);
            Cover(Img.Get("home_bg"), 0, 0, W, H);
            float top = cw(14), bottom = H; // the new game's tab bar is under this page, so the panel goes to the bottom
            DrawPage(top, bottom);
            ctx.font = Lil(cw(3.6f)); float bw = ctx.measureText("‹ Back") + cw(8), bh = cw(3.6f) * 1.2f + cw(3.6f), bdy = Down("hubBack") ? cw(0.5f) : 0;
            Btn("hubBack", 0, 0, bw + cw(6), cw(13), () => Back());
            ctx.fillStyle = "rgba(255,255,255,0.1)"; ctx.beginPath(); ctx.roundRect(cw(3), cw(2.5f) + bdy, bw, bh, bh / 2); ctx.fill();
            Text("‹ Back", cw(3) + bw / 2, cw(2.5f) + bdy + bh / 2, Lil(cw(3.6f)), "#fff", "center");
            Btn("hubBack", cw(3), cw(2.5f), bw, bh, () => Back());
        }
        static float H2(string t, string small, float x, float y)
        {
            float s = cw(6.4f), by = y + cw(1) + s * 0.6f;
            Text(t, x, by, Lil(s), "#fff", baseline: "alphabetic", shadow: "#0b1330", sh: cw(0.6f));
            if (small != null) { ctx.font = Lil(s); float tw = ctx.measureText(t); Text(small, x + tw + cw(2), by, Treb(cw(3)), "#a9a7c7", baseline: "alphabetic"); }
            float ly = by + cw(1.6f), lw = cw(16), k = cw(0.8f);
            ctx.strokeStyle = HUB_GOLD; ctx.lineWidth = cw(0.3f); ctx.beginPath(); ctx.moveTo(x, ly); ctx.lineTo(x + lw, ly); ctx.stroke();
            ctx.fillStyle = "#f2cf7a"; ctx.beginPath(); ctx.moveTo(x + lw + k * 1.6f, ly - k); ctx.lineTo(x + lw + k * 2.6f, ly); ctx.lineTo(x + lw + k * 1.6f, ly + k); ctx.lineTo(x + lw + k * 0.6f, ly); ctx.closePath(); ctx.fill();
            return y + cw(1) + s + cw(4.5f);
        }

        // ---------- one frame ----------
        internal static void Draw(Ctx c, float w, float h)
        {
            ctx = c; W = w; H = h; hits.Clear(); hitsUsed = 0; scrollOff = 0; clipOn = false;
            Social.Tick();
            if (page == "friends") HubFriends();
            else if (page == "invite") HubInvite();
            else if (page == "leaveparty") HubLeaveConfirm3();
            else if (page != null)
            {
                HubPage();
                if (page == "hubname" && hubConfirm != null) HubNameConfirm();
                HubSocialOverlays();
            }
            if (InviteShown) DrawInvite(BAM.Online.Mp.MpPresence.Invite); // an invite covers any page, and the game's own menus
            DrawToast();
        }

        // ---------- the shared look (MenuHome.cs, MenuV3.cs) ----------
        static string HubToggle(string key, float x, float y, float w, float h, float r, bool on, bool enabled)
        {
            bool dn = enabled && Down(key);
            ctx.fillStyle = on ? "#161b46" : "rgba(255,255,255,0.04)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill();
            if (on) { ctx.fillStyle = "rgba(62,230,176,0.10)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill(); }
            if (dn) { ctx.fillStyle = "rgba(255,255,255,0.10)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.fill(); }
            if (on) { ctx.strokeStyle = enabled ? HUB_ACCENT : "rgba(62,230,176,0.35)"; ctx.lineWidth = cw(0.45f); ctx.beginPath(); ctx.roundRect(x, y, w, h, r); ctx.stroke(); }
            return !enabled ? "rgba(154,162,214,0.4)" : on ? "#ffffff" : HUB_DIM;
        }
        static void HubSegments(string key, float x, float y, float w, float h, string[] labels, int sel, Action<int> pick, float font = 0)
        {
            ctx.fillStyle = "rgba(8,10,30,0.55)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(2.6f)); ctx.fill();
            float gap = cw(1), sw = (w - gap * (labels.Length + 1)) / labels.Length, fs = font > 0 ? font : cw(3.6f);
            for (int i = 0; i < labels.Length; i++)
            {
                float sx = x + gap + i * (sw + gap); string k = key + ":" + i; int ii = i;
                string col = HubToggle(k, sx, y + gap, sw, h - 2 * gap, cw(2), i == sel, true);
                string l = labels[i]; ctx.font = Lil(fs); while (l.Length > 2 && ctx.measureText(l) > sw - cw(2)) l = l.Substring(0, l.Length - 2) + "…";
                Text(l, sx + sw / 2, y + h / 2, Lil(fs), col, "center");
                Btn(k, sx, y, sw, h, () => pick(ii));
            }
        }
        static void HubCardBack(float x, float y, float w, float h)
        {
            ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(4)); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y, w, h, cw(4)); ctx.stroke();
        }
        static float HubSetHead(string t, float x, float y) { Text(t, x + cw(1), y + cw(3.2f), Treb(cw(2.8f), true), HUB_ACCENT); return y + cw(6.5f); }
        static float HubSetRow(string key, string label, string value, bool avail, Action a, float x, float y, float w, bool plain = false, string labelCol = null)
        {
            float rh = cw(11), dy = key != null && Down(key) ? cw(0.4f) : 0;
            bool locked = !avail && !plain;
            float a0 = ctx.globalAlpha; if (locked) ctx.globalAlpha = a0 * 0.5f;
            ctx.fillStyle = HUB_PANEL; ctx.beginPath(); ctx.roundRect(x, y + dy, w, rh, cw(3)); ctx.fill();
            ctx.strokeStyle = HUB_LINE; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(x, y + dy, w, rh, cw(3)); ctx.stroke();
            ctx.globalAlpha = a0;
            Text(label, x + cw(4), y + dy + rh / 2, Treb(cw(3.4f), true), labelCol ?? (avail || plain ? HUB_TEXT : "rgba(154,162,214,0.55)"));
            if (avail)
            {
                ctx.font = Lil(cw(3.6f)); string v = value; while (v.Length > 3 && ctx.measureText(v) > w * 0.42f) v = v.Substring(0, v.Length - 2) + "…";
                float vw = ctx.measureText(v) + cw(9), vx = x + w - cw(2.5f) - vw, vh = rh - cw(3.6f);
                ctx.fillStyle = "rgba(62,230,176,0.16)"; ctx.beginPath(); ctx.roundRect(vx, y + dy + cw(1.8f), vw, vh, vh / 2); ctx.fill();
                Text(v, vx + cw(3.5f), y + dy + rh / 2, Lil(cw(3.6f)), HUB_ACCENT);
            }
            else if (value != null)
            {
                ctx.font = Treb(cw(3), true); string v = value; while (v.Length > 3 && ctx.measureText(v) > w * 0.5f) v = v.Substring(0, v.Length - 2) + "…";
                Text(v, x + w - cw(4), y + dy + rh / 2, Treb(cw(3), true), HUB_DIM, "right");
            }
            if (key != null) Btn(key, x, y, w, rh, a);
            return y + rh + cw(1.5f);
        }
        static void HubChevron(float cx, float cy, float s, string col)
        {
            ctx.fillStyle = col; ctx.beginPath(); ctx.moveTo(cx - s * 0.6f, cy - s); ctx.lineTo(cx + s * 0.6f, cy); ctx.lineTo(cx - s * 0.6f, cy + s); ctx.closePath(); ctx.fill();
        }
        static void HubPencil(float cx, float cy, float s, string col)
        {
            ctx.save(); ctx.translate(cx, cy); ctx.rotate(Mathf.PI / 4); ctx.fillStyle = col;
            ctx.fillRect(-s * 0.32f, -s * 1.1f, s * 0.64f, s * 1.6f);
            HubPoly(-s * 0.32f, s * 0.6f, s * 0.32f, s * 0.6f, 0, s * 1.15f);
            ctx.restore();
        }
        static void HubPoly(float x0, float y0, float x1, float y1, float x2, float y2) { ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.lineTo(x2, y2); ctx.closePath(); ctx.fill(); }
        static void HubHex(float cx, float cy, float r)
        {
            ctx.beginPath();
            for (int i = 0; i < 6; i++) { float a = Mathf.PI / 3 * i - Mathf.PI / 2; if (i == 0) ctx.moveTo(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); else ctx.lineTo(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); }
            ctx.closePath();
        }
        static void HubCrown(float cx, float cy, float s)
        {
            ctx.fillStyle = "#ffd257"; ctx.beginPath();
            ctx.moveTo(cx - s, cy + s * 0.7f); ctx.lineTo(cx - s, cy - s * 0.5f); ctx.lineTo(cx - s * 0.45f, cy); ctx.lineTo(cx, cy - s * 0.85f);
            ctx.lineTo(cx + s * 0.45f, cy); ctx.lineTo(cx + s, cy - s * 0.5f); ctx.lineTo(cx + s, cy + s * 0.7f); ctx.closePath(); ctx.fill();
        }
        // the six rank emblems (MenuHome.HubRankEmblem)
        static void HubRankEmblem(int rank, float cx, float cy, float s)
        {
            string col = RankInfo.COLORS[rank], dark = RankInfo.DARK[rank];
            if (s >= cw(3))
            {
                ctx.fillStyle = col; ctx.globalAlpha *= 0.9f;
                if (rank >= 1) HubPoly(cx - s * 0.32f, cy + s * 0.8f, cx, cy + s * 1.32f, cx + s * 0.32f, cy + s * 0.8f);
                for (int d = -1; d <= 1; d += 2)
                {
                    if (rank >= 2) HubPoly(cx + d * s * 0.75f, cy - s * 0.42f, cx + d * s * 1.5f, cy - s * 0.12f, cx + d * s * 0.75f, cy + s * 0.3f);
                    if (rank >= 3) HubPoly(cx + d * s * 0.7f, cy + s * 0.1f, cx + d * s * 1.28f, cy + s * 0.62f, cx + d * s * 0.55f, cy + s * 0.62f);
                    if (rank >= 4) HubPoly(cx + d * s * 0.3f, cy - s * 0.85f, cx + d * s * 1.15f, cy - s * 1.05f, cx + d * s * 0.8f, cy - s * 0.35f);
                }
                if (rank >= 5) HubPoly(cx - s * 0.24f, cy - s * 0.9f, cx, cy - s * 1.55f, cx + s * 0.24f, cy - s * 0.9f);
                ctx.globalAlpha /= 0.9f;
            }
            ctx.fillStyle = VGrad(cy - s, cy + s, col, dark); HubHex(cx, cy, s); ctx.fill();
            ctx.strokeStyle = "rgba(255,255,255,0.35)"; ctx.lineWidth = Math.Max(1, s * 0.05f); ctx.stroke();
            ctx.fillStyle = "#141838"; HubHex(cx, cy, s * 0.76f); ctx.fill();
            ctx.fillStyle = col; float k = s * 0.44f;
            switch (rank)
            {
                case 0:
                    ctx.beginPath(); ctx.moveTo(cx, cy - k); ctx.quadraticCurveTo(cx + k * 0.9f, cy, cx + k * 0.45f, cy + k * 0.7f); ctx.quadraticCurveTo(cx, cy + k, cx - k * 0.45f, cy + k * 0.7f); ctx.quadraticCurveTo(cx - k * 0.9f, cy, cx, cy - k); ctx.fill();
                    break;
                case 1:
                    HubPoly(cx - k * 0.12f, cy - k, cx - k * 0.7f, cy, cx - k * 0.12f, cy + k);
                    HubPoly(cx + k * 0.12f, cy - k, cx + k * 0.7f, cy, cx + k * 0.12f, cy + k);
                    break;
                case 2:
                    ctx.beginPath(); ctx.moveTo(cx - k * 0.75f, cy - k * 0.75f); ctx.lineTo(cx + k * 0.75f, cy - k * 0.75f); ctx.lineTo(cx + k * 0.75f, cy); ctx.quadraticCurveTo(cx + k * 0.6f, cy + k * 0.7f, cx, cy + k); ctx.quadraticCurveTo(cx - k * 0.6f, cy + k * 0.7f, cx - k * 0.75f, cy); ctx.closePath(); ctx.fill();
                    break;
                case 3:
                    ctx.beginPath(); ctx.arc(cx, cy, k * 0.8f, 0, 7); ctx.fill();
                    ctx.fillStyle = "#141838"; ctx.beginPath(); ctx.arc(cx + k * 0.32f, cy - k * 0.18f, k * 0.66f, 0, 7); ctx.fill();
                    break;
                case 4:
                    HubCrown(cx, cy, k * 0.85f);
                    break;
                default:
                    ctx.beginPath(); ctx.moveTo(cx, cy - k); ctx.lineTo(cx + k * 0.22f, cy - k * 0.22f); ctx.lineTo(cx + k * 0.8f, cy); ctx.lineTo(cx + k * 0.22f, cy + k * 0.22f);
                    ctx.lineTo(cx, cy + k * 0.6f); ctx.lineTo(cx - k * 0.22f, cy + k * 0.22f); ctx.lineTo(cx - k * 0.8f, cy); ctx.lineTo(cx - k * 0.22f, cy - k * 0.22f); ctx.closePath(); ctx.fill();
                    break;
            }
        }
        // a letter in a colour made from the player ID (MenuSocial.HubAvatar)
        static void HubAvatar(string id, string name, float cx, float cy, float r)
        {
            int hue = 7; foreach (char ch in id ?? "") hue = hue * 31 + ch;
            Color col = Color.HSVToRGB(((hue & 0x7fffffff) % 360 * 47 % 360) / 360f, 0.5f, 0.75f);
            ctx.fillStyle = "#" + ColorUtility.ToHtmlStringRGB(col); ctx.beginPath(); ctx.arc(cx, cy, r, 0, 7); ctx.fill();
            Text(!string.IsNullOrEmpty(name) ? name.Substring(0, 1).ToUpper() : "?", cx, cy, Lil(r), "#fff", "center");
        }
        // a round hero portrait (MenuPortrait.DrawPortrait); false when there is no hero to show
        static bool DrawPortrait(string hero, float cx, float cy, float r)
        {
            if (!IsHero(hero)) return false;
            var im = HeroIcon(hero);
            ctx.fillStyle = VGrad(cy - r, cy + r, "#4c6bff", "#7a3cff"); ctx.beginPath(); ctx.arc(cx, cy, r, 0, 7); ctx.fill();
            if (im.ok)
            {
                ctx.save(); ctx.beginPath(); ctx.arc(cx, cy, r, 0, 7); ctx.clip();
                float ih = r * 2.1f, iw = ih * im.w / Math.Max(1, im.h); ctx.drawImage(im, cx - iw / 2, cy - r * 0.9f, iw, ih);
                ctx.restore();
            }
            ctx.strokeStyle = "rgba(255,255,255,0.35)"; ctx.lineWidth = Math.Max(cw(0.4f), r * 0.06f); ctx.beginPath(); ctx.arc(cx, cy, r, 0, 7); ctx.stroke();
            return true;
        }
        // the most played hero, the player's picture (MenuPortrait.MyPortrait without the pick: there is no portrait page yet)
        static string TopHero(Totals t)
        {
            HeroTotals best = null;
            if (t != null) foreach (var h in t.heroes) if (IsHero(h.id) && (best == null || h.m > best.m)) best = h;
            return best?.id;
        }
        static readonly List<HeroTotals> topList = new List<HeroTotals>();
        static string WinRateText(int wins, int losses) => wins + losses <= 0 ? "–" : Mathf.RoundToInt(wins * 100f / (wins + losses)) + "%";
        static string TimeText(long s) => s >= 3600 ? s / 3600 + "h " + (s % 3600) / 60 + "m" : s / 60 + "m";

        // small icons (MenuHome.cs, MenuSocial.cs)
        static void HubPlus(float cx, float cy, float s, string col) { ctx.fillStyle = col; ctx.fillRect(cx - s, cy - s * 0.2f, s * 2, s * 0.4f); ctx.fillRect(cx - s * 0.2f, cy - s, s * 0.4f, s * 2); }
        static void HubCheck(float cx, float cy, float s, string col)
        {
            ctx.strokeStyle = col; ctx.lineWidth = s * 0.45f; ctx.beginPath(); ctx.moveTo(cx - s, cy); ctx.lineTo(cx - s * 0.3f, cy + s * 0.7f); ctx.lineTo(cx + s, cy - s * 0.7f); ctx.stroke();
        }
        static void HubCross(float cx, float cy, float s, string col)
        {
            ctx.strokeStyle = col; ctx.lineWidth = s * 0.45f; ctx.beginPath(); ctx.moveTo(cx - s, cy - s); ctx.lineTo(cx + s, cy + s); ctx.moveTo(cx + s, cy - s); ctx.lineTo(cx - s, cy + s); ctx.stroke();
        }
        static void HubUserPlus(float cx, float cy, float s, string col)
        {
            ctx.fillStyle = col; float px = cx - s * 0.35f;
            ctx.beginPath(); ctx.arc(px, cy - s * 0.45f, s * 0.5f, 0, 7); ctx.fill();
            ctx.beginPath(); ctx.ellipse(px, cy + s * 0.95f, s * 0.9f, s * 0.75f, 0, Mathf.PI, Mathf.PI * 2); ctx.fill();
            HubPlus(cx + s * 0.9f, cy - s * 0.25f, s * 0.42f, col);
        }
        static void HubPeople(float cx, float cy, float s, string col)
        {
            ctx.fillStyle = col; ctx.globalAlpha *= 0.55f;
            ctx.beginPath(); ctx.arc(cx + s * 0.9f, cy - s * 0.35f, s * 0.6f, 0, 7); ctx.fill();
            ctx.beginPath(); ctx.ellipse(cx + s * 0.9f, cy + s * 1.05f, s * 0.95f, s * 0.6f, 0, Mathf.PI, Mathf.PI * 2); ctx.fill();
            ctx.globalAlpha /= 0.55f;
            ctx.beginPath(); ctx.arc(cx - s * 0.2f, cy - s * 0.2f, s * 0.75f, 0, 7); ctx.fill();
            ctx.beginPath(); ctx.ellipse(cx - s * 0.2f, cy + s * 1.35f, s * 1.15f, s * 0.75f, 0, Mathf.PI, Mathf.PI * 2); ctx.fill();
        }

        // ---------- text fields (Menu.Field / BeginInput / TextInput) ----------
        static string focus; static TouchScreenKeyboard kb;
        static string FieldGet(string k) => k == "pname" ? hubNameDraft : k == "code" ? codeDraft : friendNameDraft;
        static void FieldSet(string k, string v)
        {
            if (k == "code") { v = v.Trim().ToUpperInvariant(); codeDraft = v.Length > 8 ? v.Substring(0, 8) : v; return; } // a room code (MpRoom)
            int max = NameRules.Max + 4; if (v.Length > max) v = v.Substring(0, max);
            if (k == "pname") { hubNameDraft = v; nameAnswer = null; } else { friendNameDraft = v; friendReqMsg = null; }
        }
        static void FieldDone(string k) { if (k == "fname") SendFriendRequest(); else if (k == "code" && codeDraft.Length >= 4) BAM.Online.Mp.MpRoom.Join(codeDraft); }
        static void Field(string key, float x, float y, float w, float h, string val, string placeholder)
        {
            bool on = focus == key;
            ctx.fillStyle = "#0d1020"; ctx.beginPath(); ctx.roundRect(x, y, w, h, 10); ctx.fill();
            ctx.strokeStyle = on ? "#ffb43a" : "#39407a"; ctx.lineWidth = on ? 2 : 1; ctx.beginPath(); ctx.roundRect(x + 0.5f, y + 0.5f, w - 1, h - 1, 10); ctx.stroke();
            string font = Treb(14, true); bool empty = string.IsNullOrEmpty(val);
            string shown = empty ? placeholder : val; float tx = x + 8;
            Text(shown, tx, y + h / 2, font, empty ? "#5c6290" : "#f4f1ff");
            if (on && (int)(Now * 2) % 2 == 0) { ctx.font = font; float cx = tx + (empty ? 0 : ctx.measureText(val)); ctx.fillStyle = "#ffb43a"; ctx.fillRect(cx + 1, y + 9, 2, h - 18); }
            Btn("in:" + key, x, y, w, h, () => BeginInput(key));
        }
        static void BeginInput(string key)
        {
            focus = key;
            if (TouchScreenKeyboard.isSupported)
                kb = TouchScreenKeyboard.Open(FieldGet(key), TouchScreenKeyboardType.Default, false, false, false, false, key == "pname" ? "PLAYER NAME" : key == "code" ? "ROOM CODE" : "FRIEND'S NAME", key == "code" ? 8 : NameRules.Max + 4);
        }
        static void EndInput() { if (focus == null) return; focus = null; if (kb != null) { kb.active = false; kb = null; } }
        /// <summary>Every frame: the phone's keyboard, or keys typed on a computer (OnlineView feeds them in).</summary>
        internal static void TextInput(string typed)
        {
            if (focus == null) return;
            if (page == null) { EndInput(); return; }
            if (kb != null)
            {
                FieldSet(focus, kb.text ?? "");
                if (kb.status != TouchScreenKeyboard.Status.Visible) { bool done = kb.status == TouchScreenKeyboard.Status.Done; var f = focus; EndInput(); if (done) FieldDone(f); }
                return;
            }
            if (string.IsNullOrEmpty(typed)) return;
            string cur = FieldGet(focus);
            foreach (char c in typed)
            {
                if (c == '\b') { if (cur.Length > 0) cur = cur.Substring(0, cur.Length - 1); }
                else if (c == '\n' || c == '\r') { var f = focus; FieldSet(f, cur); EndInput(); FieldDone(f); return; }
                else if (!char.IsControl(c)) cur += c;
            }
            FieldSet(focus, cur);
        }
        internal static bool Typing => focus != null;
    }

    /// <summary>Player ranks by Rating (Speltest MenuHome.cs RankInfo): one rank per tier, no divisions.</summary>
    public static class RankInfo
    {
        public static readonly string[] NAMES = { "Ember", "Rift", "Vanguard", "Eclipse", "Sovereign", "Ascendant" };
        public static readonly string[] COLORS = { "#ff7a3d", "#9a7bff", "#3ec7ff", "#c9d1ff", "#ffd257", "#3ee6b0" };
        public static readonly string[] DARK = { "#6a2a12", "#3a2a72", "#124a66", "#454b78", "#6e5310", "#0f5a45" };
        public static readonly int[] MIN = { 0, 300, 700, 1200, 1800, 2500 };
        public static int RankOf(int rating) { int r = 0; for (int i = 1; i < MIN.Length; i++) if (rating >= MIN[i]) r = i; return r; }
        public static int NextOf(int rating) { int r = RankOf(rating); return r + 1 < MIN.Length ? r + 1 : -1; }
        public static int Needed(int rating) { int n = NextOf(rating); return n < 0 ? 0 : MIN[n] - Math.Max(0, rating); }
        public static float Progress(int rating)
        {
            int r = RankOf(rating), n = NextOf(rating); if (n < 0) return 1;
            return Math.Max(0, Math.Min(1, (float)(Math.Max(0, rating) - MIN[r]) / (MIN[n] - MIN[r])));
        }
    }
}
