using System;
using BAM.Audio;
using BAM.Core;
using BAM.Online.Mp;
using BAM.Sim;
using UnityEngine;

// ONLINE MATCH's queue as one card with clear steps (Rickard 2026-10-31, thread "Förbättra menyns UI efter Speltest": the Speltest
// copy is done, now develop it). Before this the queue was one line of text with the seconds and CANCEL. Now:
//   SEARCHING   the player's hero in a radar, the time, your team against three "?" places, how many others search right now,
//               after MpRoom.BotFillAfter (12 s) bots take the empty places (2026-11-15; was PLAY BOTS INSTEAD after 30 s), and CANCEL;
//   JOINING     a lobby was found and the phone is getting into it;
//   MATCH FOUND both teams with their heroes, "Starting in 2" (one sound and one vibration, and the page comes back if the
//               player had gone to another page);
//   error card  when the search stopped by itself: what went wrong, TRY AGAIN and PLAY BOTS.
// Drawn with the old menu code (Ctx) like the rest of the page, colours from OnlineMenu (HUB_*).
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static bool foundSeen;

        // the editor's look test (BAM > Test Online Search): 1 searching, 2 searching for 9 s, 3 joining, 4 match found, 5 error;
        // 0 = the real queue. Only the picture: nothing goes online, and every button here just ends the test.
        static int preview; static float previewSince;
        static readonly HeroId[] PreviewThem = { HeroId.Tyra, HeroId.Knut, HeroId.Bonk };
        internal static bool Previewing => preview > 0 && preview < 5;
        internal static bool PreviewError => preview == 5;
        static void EndPreview() { preview = 0; Close(); }

        static bool SearchFound(out int mySide)
        {
            mySide = MpRoom.MySide;
            if (preview > 0) return preview == 4;
            if (!MpRoom.QuickFound) return false;
            foreach (var m in MpRoom.Members) if (m.Id != MpRoom.MyId && m.Side != mySide) return true;
            return false;
        }

        static string Clock(float s) { int t = Mathf.Max(0, Mathf.FloorToInt(s)); return (t / 60) + ":" + (t % 60).ToString("00"); }

        /// <summary>The queue card (MpRoom.Quick, not in the match yet); returns its height.</summary>
        static float SearchCard(float x, float y0, float w, float rowH, bool draw)
        {
            bool found = SearchFound(out int mySide);
            bool joining = !found && (preview > 0 ? preview == 3 : MpRoom.State == MpRoom.Phase.Connecting && MpRoom.QuickJoining);
            float t = preview > 0 ? Now - previewSince + (preview == 2 ? 9 : 0) : Now - MpRoom.SearchSince, y = y0, cx = x + w / 2;

            // the title
            string title = found ? "MATCH FOUND!" : joining ? "JOINING" : "SEARCHING";
            if (draw)
            {
                float size = 22;
                Text(title, cx, y + 16, Lil(size), found ? "#ffd166" : "#fff", "center", shadow: found ? "#8a4b00" : "#1b2050", sh: size * 0.08f);
                if (!found)
                {
                    ctx.font = Lil(size); float tw = ctx.measureText(title);
                    int dots = (int)(Now * 2.5f) % 4;
                    Text(new string('.', dots), cx + tw / 2 + 1, y + 16, Lil(size), "#fff", "left");
                }
            }
            y += 34;

            // the radar: the player's hero in a circle, rings going out while searching, a gold ring when found
            float r = 30, ry = y + 44;
            if (draw)
            {
                var me = MpRoom.Me;
                var hero = me != null ? me.Hero : HeroCatalog.Clean(BamSettings.LastHero);
                if (!found)
                    for (int i = 0; i < 3; i++)
                    {
                        float p = (Now * 0.6f + i / 3f) % 1f;
                        ctx.globalAlpha = 0.55f * (1 - p);
                        ctx.strokeStyle = HUB_ACCENT; ctx.lineWidth = 2.2f * (1 - p) + 0.6f;
                        ctx.beginPath(); ctx.arc(cx, ry, r + 4 + p * 34, 0, Mathf.PI * 2); ctx.stroke();
                    }
                ctx.globalAlpha = 1;
                ctx.fillStyle = "#0d1020"; ctx.beginPath(); ctx.arc(cx, ry, r + 3, 0, Mathf.PI * 2); ctx.fill();
                var im = HeroIcon(HeroCatalog.Get(hero).Name);
                if (im != null && im.ok)
                {
                    ctx.save(); ctx.beginPath(); ctx.arc(cx, ry, r, 0, Mathf.PI * 2); ctx.clip();
                    Cover(im, cx - r, ry - r, 2 * r, 2 * r, 0.2f); ctx.restore();
                }
                float pulse = found ? 1 : 0.75f + 0.25f * Mathf.Sin(Now * 4f);
                ctx.globalAlpha = pulse;
                ctx.strokeStyle = found ? "#ffd166" : HUB_ACCENT; ctx.lineWidth = 3;
                ctx.beginPath(); ctx.arc(cx, ry, r + 2, 0, Mathf.PI * 2); ctx.stroke();
                ctx.globalAlpha = 1;
            }
            y += 92;

            // the time, or the start countdown
            if (draw)
            {
                string line;
                if (found) { float s = preview > 0 ? 2 - (Now - previewSince) % 2 : MpRoom.IsHost ? MpRoom.StartsIn : -1; line = s >= 0 ? "Starting in " + Mathf.CeilToInt(s) : "Starting…"; }
                else line = Clock(t);
                Text(line, cx, y + 10, Lil(found ? 16 : 20), found ? "#8fffb0" : "#f4f1ff", "center");
            }
            y += 28;

            // the teams: you (and your group) against the other side; "?" while nobody is there, BOT where bots will play
            y += SearchTeams(x, y, w, mySide, found, draw);

            // a line on what happens now
            string info;
            if (found) info = "Bots fill the empty places. Get ready!";
            else if (joining) info = "Someone is waiting. Getting you into their match…";
            else
            {
                int others = preview > 0 ? 0 : OnlineUi.Searching();
                float fill = preview > 0 ? MpRoom.BotFillAfter - t : MpRoom.BotFillIn;
                info = others > 0 ? others + (others == 1 ? " other player is" : " other players are") + " searching right now."
                    : "Nobody else is searching right now.";
                if (fill >= 0) info += fill <= 5 ? " Bots take the empty places in " + Mathf.CeilToInt(Mathf.Max(1, fill)) + "…"
                    : " Bots take the empty places if nobody comes.";
                if (MpRoom.PartySize > 1) info = "Your group is searching together. " + info;
            }
            var il = Wrap(info, w, Treb(12));
            if (draw) for (int i = 0; i < il.Count; i++) Text(il[i], cx, y + 8 + i * 16, Treb(12), "#a9a7c7", "center");
            y += il.Count * 16 + 8;

            if (found) return y - y0;

            // no PLAY BOTS INSTEAD any more: after MpRoom.BotFillAfter the bots come by themselves (SearchTick)
            if (draw) MpBtn("mpCancel", "CANCEL", x, y, w, rowH, "#39407a", preview > 0 ? (Action)EndPreview : () => { MpRoom.CancelSearch(); MpRoom.ClearQuickError(); });
            y += rowH + 6;
            return y - y0;
        }

        // three places a side (two in 2v2); the other side is "?" until someone is in
        static float SearchTeams(float x, float y, float w, int mySide, bool found, bool draw)
        {
            float vsW = 30, s = Math.Min(46, (w - vsW - 10 * 2) / 6f), gap = (w - vsW - 6 * s) / 6f;
            if (!draw) return s + 22;
            int a = 0, b = 0, n = preview > 0 ? 3 : MpRoom.Size; // places a side: the room's team size (2v2 or 3v3)
            float left = x + gap / 2 + (3 - n) * (s + gap) / 2, right = x + w / 2 + vsW / 2 + gap / 2 + (3 - n) * (s + gap) / 2;
            // me first, then my group, then the others
            var me = MpRoom.Me;
            var myHero = me != null ? me.Hero : HeroCatalog.Clean(BamSettings.LastHero);
            Place(left, y, s, myHero, "You", HUB_ACCENT, false); a++;
            if (preview == 4) for (; b < 2; b++) Place(right + b * (s + gap), y, s, PreviewThem[b], b == 0 ? "Simon" : "Ella", "#ff8a6b", false);
            foreach (var m in MpRoom.Members)
            {
                if (m.Id == MpRoom.MyId) continue;
                if (m.Side == mySide) { if (a < n) Place(left + a * (s + gap), y, s, m.Hero, m.Name, HUB_ACCENT, false); a++; }
                else { if (b < n) Place(right + b * (s + gap), y, s, m.Hero, m.Name, "#ff8a6b", false); b++; }
            }
            for (; a < n; a++) Place(left + a * (s + gap), y, s, null, "Bot", "#4a5088", false);
            for (; b < n; b++) Place(right + b * (s + gap), y, s, null, found ? "Bot" : "?", found ? "#4a5088" : "#ff8a6b", !found);
            Text("VS", x + w / 2, y + s / 2, Lil(18), "#ffb43a", "center", shadow: "#5a2a00", sh: 1.5f);
            return s + 22;
        }

        static void Place(float x, float y, float s, HeroId? hero, string name, string ring, bool waiting)
        {
            ctx.fillStyle = "#0d1020"; ctx.beginPath(); ctx.roundRect(x, y, s, s, 8); ctx.fill();
            if (hero.HasValue)
            {
                var im = HeroIcon(HeroCatalog.Get(hero.Value).Name);
                if (im != null && im.ok) { ctx.save(); ctx.beginPath(); ctx.roundRect(x + 2, y + 2, s - 4, s - 4, 6); ctx.clip(); Cover(im, x + 2, y + 2, s - 4, s - 4, 0.2f); ctx.restore(); }
            }
            else
            {
                float al = waiting ? 0.45f + 0.35f * Mathf.Sin(Now * 3f + x * 0.05f) : 0.6f;
                ctx.globalAlpha = al;
                Text(waiting ? "?" : "BOT", x + s / 2, y + s / 2, waiting ? Lil(s * 0.5f) : Treb(Math.Max(9, s * 0.24f), true), "#e8eaff", "center");
                ctx.globalAlpha = 1;
            }
            ctx.strokeStyle = ring; ctx.lineWidth = 1.6f; ctx.beginPath(); ctx.roundRect(x, y, s, s, 8); ctx.stroke();
            string nm = name ?? ""; ctx.font = Treb(10, true);
            while (nm.Length > 2 && ctx.measureText(nm) > s + 8) nm = nm.Substring(0, nm.Length - 2) + "…";
            if (waiting && name == "?") return;
            Text(nm, x + s / 2, y + s + 10, Treb(10, true), "#c9cbe8", "center");
        }

        /// <summary>The search stopped by itself (MpRoom.QuickError): what happened, TRY AGAIN and PLAY BOTS. Returns its height.</summary>
        static float SearchErrorCard(float x, float y0, float w, float rowH, bool draw)
        {
            float y = y0, pad = 10, iw = w - 2 * pad;
            var why = Wrap(preview == 5 ? "Could not reach the game server. Check your internet and try again." : MpRoom.QuickError, iw, Treb(12));
            float h = 12 + 22 + why.Count * 16 + 10 + rowH + 8 + 4;
            if (draw)
            {
                ctx.fillStyle = "rgba(90,24,40,0.55)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, 10); ctx.fill();
                ctx.strokeStyle = "rgba(255,120,120,0.45)"; ctx.lineWidth = 1.2f; ctx.beginPath(); ctx.roundRect(x, y, w, h, 10); ctx.stroke();
                Text("NO MATCH THIS TIME", x + w / 2, y + 12 + 10, Lil(17), "#ffb0b0", "center", shadow: "#3a0d18", sh: 1.2f);
                for (int i = 0; i < why.Count; i++) Text(why[i], x + w / 2, y + 12 + 22 + 8 + i * 16, Treb(12), "#f4e8ea", "center");
                float by = y + 12 + 22 + why.Count * 16 + 10, bw = (iw - 6) / 2;
                StartButton("mpRetry", x + pad, by, bw, rowH + 4, "TRY AGAIN", preview > 0 ? (Action)(() => { preview = 1; previewSince = Now; }) : () => { MpRoom.ClearQuickError(); MpRoom.QuickMatch(); }, null);
                MpBtn("mpBotsErr", "PLAY BOTS", x + pad + bw + 6, by + 2, bw, rowH, "#3a6cff", preview > 0 ? (Action)EndPreview : PlayBotsInstead);
            }
            return h + 10;
        }

        static void PlayBotsInstead()
        {
            if (MpRoom.Quick) MpRoom.CancelSearch();
            MpRoom.ClearQuickError();
            Close();
            BAM.UI.Screens.PlayBotMatch();
        }

        /// <summary>Every frame (MpTick): the moment a match is found, one sound and one vibration, and the queue page comes back
        /// if the player had gone elsewhere in the menus, so the start never comes as a surprise.</summary>
        static void SearchTick()
        {
            if (preview > 0 && page != "mp") preview = 0; // the test ends when its page closes
            if (page != "mp" && MpRoom.QuickError != null) MpRoom.ClearQuickError(); // the error card is shown once, on its page
            bool f = MpRoom.Quick && MpRoom.State != MpRoom.Phase.Match && SearchFound(out _);
            if (f && !foundSeen)
            {
                BamAudio.Play(SfxId.UiConfirm);
                BamHaptics.Pulse(0.6f);
                if (page != "mp" && Flow.Game != null && !Flow.Game.InMatch) { Close(); Go("mp"); }
            }
            foundSeen = f;

            // nobody came in MpRoom.BotFillAfter: bots take the empty places. Alone that is a bot match like Home's PLAY (no Relay
            // for one phone); a group's leader starts the group's room match with the other side all bots
            if (preview == 0 && MpRoom.BotFillIn == 0 && Flow.Game != null && !Flow.Game.InMatch)
            {
                if (MpRoom.PartySize > 1 && MpRoom.Members.Count > 1) MpRoom.FillWithBots();
                else { Debug.Log("[BAM.Online] queue: nobody found in " + MpRoom.BotFillAfter + " s, bots instead"); PlayBotsInstead(); }
            }
        }
    
#if UNITY_EDITOR
        static void Preview(int p)
        {
            if (!Application.isPlaying) { Debug.Log("[BAM.Online] Test Online Search: press Play first"); return; }
            OnlineUi.Init(); Close(); Go("mp"); preview = p; previewSince = Now;
            if (p == 4) { BamAudio.Play(SfxId.UiConfirm); foundSeen = true; }
        }
        [UnityEditor.MenuItem("BAM/Test Online Search/Searching")] static void TestSearching() => Preview(1);
        [UnityEditor.MenuItem("BAM/Test Online Search/Searching for 9 s (bots soon)")] static void TestLong() => Preview(2);
        [UnityEditor.MenuItem("BAM/Test Online Search/Joining")] static void TestJoining() => Preview(3);
        [UnityEditor.MenuItem("BAM/Test Online Search/Match found")] static void TestFound() => Preview(4);
        [UnityEditor.MenuItem("BAM/Test Online Search/Error")] static void TestError() => Preview(5);
        [UnityEditor.MenuItem("BAM/Test Online Search/Off")] static void TestOff() { if (preview > 0) EndPreview(); }
#endif
    }
}
