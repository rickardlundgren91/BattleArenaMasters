using System;
using System.Collections.Generic;
using BAM.Core;
using BAM.Online.Mp;
using BAM.Sim;
using BAM.UI;
using UnityEngine;

// The lobby on Home (Rickard 2026-10-30: "i speltest dyker lobby heroes upp i menyn istället jag vill att hela lobby ska kunna
// skötas därifrån"): a room with friends is the old Home party (Speltest MenuHome3.HubTeam3, NetLobby.party). Home shows the
// party members beside the player's hero (BAM.UI HomeParty.cs, fed by Party()), the leader's PLAY starts the match
// (NetLobby.PartyPlay), the mode sheet holds the bots and TOGETHER / AGAINST EACH OTHER, and LEAVE PARTY asks first
// (HubLeaveBtn3 / HubLeaveConfirm3). The old notices come as toasts: "X joined your party. Press PLAY", "X left the party".
// The Multiplayer page stays for ONLINE MATCH's queue.
namespace BAM.Online.UI
{
    static partial class OnlineMenu
    {
        static readonly PartyView party = new PartyView();

        /// <summary>The party for Home, or null when this phone is not in a room with friends.</summary>
        internal static PartyView Party()
        {
            if (MpRoom.State == MpRoom.Phase.Off || MpRoom.Quick) return null;
            party.Leader = MpRoom.IsHost; party.Mode = MpRoom.Mode; party.Bots = MpRoom.Diff; party.Count = 0;
            foreach (var m in MpRoom.Members)
            {
                if (m.Id == MpRoom.MyId) continue;
                if (party.Count >= party.Mates.Length) break;
                party.Mates[party.Count++] = new PartyMate { Name = m.Name, Hero = m.Hero, Ready = !m.InMatch };
            }
            return party;
        }

        static string LeaderName => MpRoom.Members.Count > 0 ? MpRoom.Members[0].Name : "the leader";
        static bool startWhenBack;

        /// <summary>Home's PLAY in a party (NetLobby.PartyPlay). False when not in a party: Home plays as usual.</summary>
        internal static bool PartyPlay()
        {
            if (Party() == null) return false;
            if (!MpRoom.IsHost)
            {
                Toast(MpRoom.State == MpRoom.Phase.Connecting ? "Joining the party…" : "Waiting for " + LeaderName + " to press PLAY");
                return true;
            }
            if (MpRoom.State == MpRoom.Phase.Connecting) { Toast("Making the party…"); return true; }
            if (MpRoom.Members.Count < 2)
            {
                // nobody joined: wait while an invite is out (Menu.Play: "Waiting for X to answer the invite"), else play alone
                if (MpSocial.Inviting) { Toast("Waiting for your friend to answer the invite"); return true; }
                MpRoom.Leave();
                return false;
            }
            if (MpRoom.Members.Count > MpRoom.Capacity(MpRoom.Mode)) { Toast("Too many players to play together: pick AGAINST EACH OTHER in MODES"); return true; }
            foreach (var m in MpRoom.Members)
                if (m.InMatch) { startWhenBack = true; Toast("Starting when everyone is back on Home"); return true; }
            MpRoom.StartMatch();
            return true;
        }

        internal static void PartyBots(Difficulty d)
        {
            if (MpRoom.IsHost) MpRoom.SetDiff(d);
            else Toast(LeaderName + " picks the bots");
        }

        internal static void PartyMode(int mode)
        {
            if (!MpRoom.IsHost) { Toast(LeaderName + " picks the mode"); return; }
            if (MpRoom.Members.Count > MpRoom.Capacity(mode)) { Toast("Too many players to play together"); return; }
            MpRoom.SetMode(mode);
        }

        internal static void LeaveParty() { Close(); Go("leaveparty"); }

        // ---------- every frame (MpTick): the old notices, the hero, and the leader's PLAY that waits for everyone ----------
        static readonly List<int> partyIds = new List<int>();
        static readonly Dictionary<int, string> partyNames = new Dictionary<int, string>();
        static MpRoom.Phase partyWas;
        static string partySig = "";

        static void PartyTick()
        {
            var st = MpRoom.State;
            bool friends = st != MpRoom.Phase.Off && !MpRoom.Quick;
            if (!friends || st == MpRoom.Phase.Match) startWhenBack = false;

            // who joined and who left
            if (friends && st == MpRoom.Phase.Lobby)
            {
                if (partyWas == MpRoom.Phase.Connecting && !MpRoom.IsHost) Toast("You joined the party. " + LeaderName + " presses PLAY");
                foreach (var m in MpRoom.Members)
                {
                    if (m.Id == MpRoom.MyId || partyIds.Contains(m.Id)) continue;
                    if (MpRoom.IsHost && partyWas == MpRoom.Phase.Lobby) Toast(m.Name + " joined your party. Press PLAY");
                }
                foreach (var id in partyIds)
                {
                    bool still = false;
                    foreach (var m in MpRoom.Members) if (m.Id == id) still = true;
                    if (!still && partyNames.TryGetValue(id, out var nm)) Toast(nm + " left the party");
                }
            }
            if (partyWas != MpRoom.Phase.Off && st == MpRoom.Phase.Off && !string.IsNullOrEmpty(MpRoom.Status)) Toast(MpRoom.Status); // "The room has closed."
            if (st != MpRoom.Phase.Match)
            {
                partyIds.Clear(); partyNames.Clear();
                foreach (var m in MpRoom.Members) { if (m.Id == MpRoom.MyId) continue; partyIds.Add(m.Id); partyNames[m.Id] = m.Name; }
            }
            partyWas = st;

            // the hero (and battle spell) picked on Home's hero page is this player's hero in the room
            var me = MpRoom.Me;
            if (st == MpRoom.Phase.Lobby && me != null && (me.Hero != (HeroId)BamSettings.LastHero || me.Spell != BamSettings.Spell))
                MpRoom.Pick((HeroId)Mathf.Clamp(BamSettings.LastHero, 0, HeroCatalog.Count - 1));

            // the leader pressed PLAY while someone was still in the last match
            if (startWhenBack && MpRoom.CanStart)
            {
                bool all = true;
                foreach (var m in MpRoom.Members) if (m.InMatch) all = false;
                if (all) { startWhenBack = false; MpRoom.StartMatch(); }
            }

            // the name tags are pictures: draw them again when a name changes
            string sig = Account.Name ?? "";
            var p = Party();
            if (p != null) for (int i = 0; i < p.Count; i++) sig += "|" + p.Mates[i].Name;
            if (sig != partySig) { partySig = sig; OnlineUi.Repaint(); }
        }

        // ---------- the pictures for Home (OnlineHooks.Paint) ----------
        /// <summary>"tag0", "tag1", "tag2": a name tag in a cw(44) x cw(9) rect; "leave": LEAVE PARTY in cw(40) x cw(9.5).</summary>
        internal static void DrawPiece(Ctx c, string key, int pw, int ph)
        {
            ctx = c; W = 400; H = 400 * ph / (float)Math.Max(1, pw);
            float k = pw / cw(key == "leave" ? 40 : 44);
            ctx.scale(k, k);
            if (key == "leave") { HubLeaveBtn3(0, cw(1)); return; }
            if (key == "tag0") { HubNameTag3(cw(22), cw(4.5f), string.IsNullOrEmpty(Account.Name) ? "You" : Account.Name, null, true, true); return; }
            var p = Party(); int i = key == "tag1" ? 0 : 1;
            if (p != null && i < p.Count) HubNameTag3(cw(22), cw(4.5f), p.Mates[i].Name, null, false, false);
        }

        // MenuHome3.HubNameTag3 (ONE_PLAY: the slim, see-through plate): [READY dot] [crown] name [pencil], centred on (cx, cy)
        static void HubNameTag3(float cx, float cy, string name, bool? ready, bool leader, bool pen)
        {
            string nf = Lil(cw(3.5f)); float gap = cw(1.4f), crownW = leader ? cw(3.6f) : 0, penD = pen ? cw(5.4f) : 0, dot = ready.HasValue ? cw(1.1f) : 0;
            string nm = name ?? ""; ctx.font = nf; while (nm.Length > 3 && ctx.measureText(nm) > cw(16)) nm = nm.Substring(0, nm.Length - 2) + "…";
            float nw = ctx.measureText(nm);
            float w = (dot > 0 ? dot * 2 + gap : 0) + crownW + (crownW > 0 ? gap : 0) + nw + (penD > 0 ? gap + penD : 0);
            { float a1 = ctx.globalAlpha; ctx.globalAlpha = a1 * 0.5f; HubPlate3(cx, cy - cw(3.1f), w + cw(4), cw(6.2f)); ctx.globalAlpha = a1; }
            float x = cx - w / 2;
            if (dot > 0)
            {
                bool rd = ready.Value;
                ctx.fillStyle = rd ? HUB_ACCENT : "rgba(255,255,255,0.35)"; ctx.beginPath(); ctx.arc(x + dot, cy, dot, 0, 7); ctx.fill();
                x += dot * 2 + gap;
            }
            if (leader) { HubCrown(x + crownW / 2, cy, cw(1.6f)); x += crownW + gap; }
            Text(nm, x, cy, nf, HUB_TEXT, shadow: "rgba(0,0,0,0.5)", sh: cw(0.3f), stroke: "rgba(6,8,26,0.9)", sw: cw(0.45f)); x += nw;
            if (pen)
            {
                x += gap; float penX = x + penD / 2;
                ctx.fillStyle = "rgba(255,255,255,0.16)"; ctx.beginPath(); ctx.arc(penX, cy, penD / 2, 0, 7); ctx.fill();
                HubPencil(penX, cy, cw(1.4f), "#fff");
            }
        }

        // MenuHome3.HubPlate3: a dark see-through pill behind a name; centred on cx, top at y
        static void HubPlate3(float cx, float y, float w, float h)
        {
            ctx.fillStyle = "rgba(8,12,36,0.62)"; ctx.beginPath(); ctx.roundRect(cx - w / 2, y, w, h, Math.Min(h / 2, cw(3))); ctx.fill();
            ctx.strokeStyle = "rgba(255,255,255,0.08)"; ctx.lineWidth = cw(0.25f); ctx.beginPath(); ctx.roundRect(cx - w / 2, y, w, h, Math.Min(h / 2, cw(3))); ctx.stroke();
        }

        // MenuHome3.HubLeaveBtn3: LEAVE PARTY, a dark pill with a red edge and a door with an arrow out
        static void HubLeaveBtn3(float x, float y)
        {
            string f = Treb(Min11(cw(2.8f)), true); ctx.font = f;
            float h = cw(7.5f), w = ctx.measureText("LEAVE PARTY") + h + cw(4);
            ctx.fillStyle = "rgba(14,18,48,0.85)"; ctx.beginPath(); ctx.roundRect(x, y, w, h, h / 2); ctx.fill();
            ctx.strokeStyle = "rgba(255,138,138,0.75)"; ctx.lineWidth = cw(0.35f); ctx.beginPath(); ctx.roundRect(x, y, w, h, h / 2); ctx.stroke();
            float ix = x + h / 2 + cw(0.6f), iy = y + h / 2, s = cw(1.5f);
            ctx.strokeStyle = "#ff8a8a"; ctx.lineWidth = cw(0.35f);
            ctx.beginPath(); ctx.moveTo(ix + s * 0.1f, iy - s); ctx.lineTo(ix - s * 0.8f, iy - s); ctx.lineTo(ix - s * 0.8f, iy + s); ctx.lineTo(ix + s * 0.1f, iy + s); ctx.stroke();
            ctx.beginPath(); ctx.moveTo(ix - s * 0.3f, iy); ctx.lineTo(ix + s, iy); ctx.moveTo(ix + s * 0.5f, iy - s * 0.5f); ctx.lineTo(ix + s, iy); ctx.lineTo(ix + s * 0.5f, iy + s * 0.5f); ctx.stroke();
            Text("LEAVE PARTY", x + h + cw(1), y + h / 2, f, "#ff8a8a");
        }

        // MenuHome3.HubLeaveConfirm3 (page "leaveparty")
        static void HubLeaveConfirm3()
        {
            ctx.fillStyle = "rgba(0,0,0,0.6)"; ctx.fillRect(0, 0, W, H);
            Btn("hubLeaveScrim", 0, 0, W, H, Close);
            float dw = W - cw(12), dx = cw(6), ix = dx + cw(5), iw = dw - cw(10);
            var body = Wrap("You will play on your own. You can invite your friends again at any time.", iw, Treb(cw(3.3f), true));
            float h2 = cw(5) + cw(9) + body.Count * cw(4.6f) + cw(4) + cw(11) + cw(5), y2 = H / 2 - h2 / 2;
            ctx.fillStyle = "#151a42"; ctx.beginPath(); ctx.roundRect(dx, y2, dw, h2, cw(4)); ctx.fill();
            ctx.strokeStyle = "#ff8a8a"; ctx.lineWidth = cw(0.4f); ctx.beginPath(); ctx.roundRect(dx, y2, dw, h2, cw(4)); ctx.stroke();
            Btn("hubLeaveBox", dx, y2, dw, h2, null);
            float y3 = y2 + cw(5);
            Text("Leave party?", ix, y3 + cw(3), Lil(cw(5)), HUB_TEXT); y3 += cw(9);
            for (int i = 0; i < body.Count; i++) Text(body[i], ix, y3 + cw(2.3f) + i * cw(4.6f), Treb(cw(3.3f), true), HUB_DIM);
            y3 += body.Count * cw(4.6f) + cw(4);
            float bw = (iw - cw(3)) / 2, bh = cw(11);
            string nc = HubToggle("hubLeaveNo", ix, y3, bw, bh, cw(3), false, true); Text("CANCEL", ix + bw / 2, y3 + bh / 2, Lil(cw(4)), nc, "center");
            Btn("hubLeaveNo", ix, y3, bw, bh, Close);
            float rx = ix + bw + cw(3);
            ctx.fillStyle = Down("hubLeaveYes") ? "#c94848" : "#e05a5a"; ctx.beginPath(); ctx.roundRect(rx, y3, bw, bh, cw(3)); ctx.fill();
            Text("LEAVE", rx + bw / 2, y3 + bh / 2, Lil(cw(4)), "#fff", "center");
            Btn("hubLeaveYes", rx, y3, bw, bh, () => { Close(); MpRoom.Leave(); Toast("You left the party"); });
        }
    }
}
