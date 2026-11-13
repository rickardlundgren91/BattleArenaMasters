using System;
namespace BAM.Sim
{
    public sealed partial class Match
    {
        /// <summary>The hero review of 2026-11-13 (bam-work/review/hjaltar-2026-11-13/, Rickard "ja" to Ragn's ring and text, then
        /// "fixa alla punkter"). Run with the live forms (Fenn's Veil, Bram's Hammer Line). Bram's Focus in Silver's steps: SilverTests.
        /// The bot audit the same day (Gold, Platinum and Ultra bots drag the one special as a player may): BotDragChecks.</summary>
        public static void HeroReview1113()
        {
            RagnRingStays();
            FennStrikeChecks();
            KuggChecks();
            TiktaPastMinions();
            BotDragChecks();
            Check(HeroCatalog.Get(HeroId.Nassla).SpecialText.EndsWith(HeroInfo.DragLine), "NASSLA text: the hold card says a drag aims the thicket");
            Check(HeroCatalog.Get(HeroId.Blast).SpecialText.Contains("1 s after the tap") && Math.Abs(TeleTime(HeroId.Blast) + BlastS.Delay - 1f) < 1e-4f, "BLAST text: the bomb explodes 1 s after the tap (warning + fuse)");
        }

        /// <summary>Ragn's red ring lies on his landing spot for the whole leap, not only for the 0.3 s warning before it.</summary>
        static void RagnRingStays()
        {
            var m = OneTapScene(HeroId.Ragn, out var p, out _, out _);
            m.Telegraph = true;
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 4f, p.Y); m.AddStatus(e, St.Stun, 999);
            int ringTicks = 0, leapTicks = 0; bool landed = false;
            for (int k = 0; k < 90 && !landed; k++)
            {
                m.Step(k == 0 ? TapIn() : PlayerInput.None);
                foreach (var ev in m.Events) if (ev.Type == EventType.RagnLand && ev.A == p) landed = true;
                if (!p.RagnLeaping) continue;
                leapTicks++;
                foreach (var q in m.Projs)
                    if (!q.Done && q.Src == p && q.Mode == ProjMode.WarnCircle && SimMath.Hypot(q.X - p.RagnToX, q.Y - p.RagnToY) < 0.05f) { ringTicks++; break; }
            }
            Check(landed && leapTicks > 0 && ringTicks == leapTicks, $"RAGN ring: the red ring lies where he lands for his whole leap ({ringTicks} of {leapTicks} ticks, landed {landed})");
            Check(HeroCatalog.Get(HeroId.Ragn).SpecialText.EndsWith(HeroInfo.DragLine), "RAGN text: the hold card says a drag aims the leap");
        }

        /// <summary>Fenn's Shadow Strike never goes into a tower (only the base in reach: SPECIAL stays grey and does nothing), and a
        /// drag picks whom it hits: the enemy nearest the aimed spot, while the tap still rolls among all enemies in reach.</summary>
        static void FennStrikeChecks()
        {
            var m = Make(HeroId.Fenn, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var f = m.Player; f.Bot = null;
            foreach (var u in m.Units) if (u.Kind == UnitKind.Minion) u.Alive = false;
            Unit tower = m.Units.Find(u => u.Kind == UnitKind.Tower && u.Team != f.Team);
            foreach (var u in m.Heroes) if (u != f) { Place(u, u.Team == f.Team ? 3 : m.Map.W - 3, 8); m.AddStatus(u, St.Stun, 999); }
            // beside the enemy base, nobody else near
            Place(f, tower.X, tower.Y + tower.R + f.R + 0.6f);
            f.AddRaw(St.Veil, 2, 0, 0, f, false);
            Check(m.EnemiesInRange(f, f.Range, Pick.Any, true, new System.Collections.Generic.List<Unit>()).Contains(tower), "FENN tower: the enemy base is in his reach for this check");
            Check(!m.VeilStrikeHasTarget(f) && !m.TryTapSpecial(f) && f.Has(St.Veil), "FENN tower: with only the tower in reach SPECIAL is grey and the strike does not go into it");
            Check(!m.TryAimSpecial(f, 0, 1) && f.Has(St.Veil), "FENN tower: a dragged strike does not go into it either");

            // two enemies in reach on either side: a drag toward one strikes that one
            var a = m.Foe(HeroId.Bram); var b = m.Foe(HeroId.Knut);
            float x = 7.5f, y = 20;
            Place(f, x, y); Place(a, x + 2f, y); Place(b, x - 2f, y); m.AddStatus(a, St.Stun, 999); m.AddStatus(b, St.Stun, 999);
            foreach (var side in new[] { 0, 1 })
            {
                f.AddRaw(St.Veil, 2, 0, 0, f, false);
                var want = side == 0 ? a : b; float ang = side == 0 ? 0 : SimMath.PI;
                var aim = m.AimFor(f, ang, DragShare(HeroId.Fenn, 0.7f));
                var marks = m.AimTargets(f, aim, new System.Collections.Generic.List<Unit>());
                Check(marks.Count == 1 && marks[0] == want, $"FENN drag: the preview marks the enemy nearest the aimed spot ({(side == 0 ? "east" : "west")})");
                Check(m.TryAimSpecial(f, ang, 0.7f), "FENN drag: a drag while veiled strikes");
                var ps = m.Projs.Find(q => q.Mode == ProjMode.Auto && q.Src == f && !q.Done);
                Check(ps != null && ps.Big && ps.Tgt == want && !f.Has(St.Veil), $"FENN drag: it strikes the enemy it was dragged toward ({(side == 0 ? "east" : "west")})");
                if (ps != null) { ps.Done = true; m.Projs.Remove(ps); }
            }
            Check(m.DragAimNow(f) == false && !CanDragAim(HeroId.Fenn), "FENN drag: unveiled, nothing to drag (the Veil is cast where he stands)");
            f.AddRaw(St.Veil, 2, 0, 0, f, false);
            Check(m.DragAimNow(f), "FENN drag: veiled, SPECIAL drags for the strike");
            Check(HeroCatalog.Get(HeroId.Fenn).SpecialText.Contains("drag to pick"), "FENN text: the hold card says a drag picks whom he strikes");
        }

        /// <summary>Kugg stays anchored till his special is done (Rickard 2026-11-13: Blink and Recall wait while he is rooted, the pack-up
        /// too; a Recall he was channelling ends as he anchors), and his bot never anchors with an enemy hero within 3.5 tiles.</summary>
        static void KuggChecks()
        {
            var m = OneTapScene(HeroId.Kugg, out var k, out _, out _);
            var e = m.Heroes.Find(h => h.Team != k.Team); Place(e, k.X + 3f, k.Y); m.AddStatus(e, St.Stun, 999);
            k.Charge = 4; k.Spell = SpellId.Recall; k.SpellAt = 0;
            Check(m.CastSpell(k) && k.SpellChanEnd >= 0, "KUGG lock: a Recall channel starts");
            Check(m.TryTapSpecial(k) && k.KuggRooted && k.SpellChanEnd < 0, "KUGG lock: anchoring ends the Recall he was channelling");
            for (int i = 0; i < 60; i++) m.Step(PlayerInput.None); // past the deploy's Casting
            Check(k.KuggRooted && !k.Has(St.Casting), "KUGG lock: still rooted after the deploy");
            k.SpellAt = 0; float x0 = k.X, y0 = k.Y;
            Check(m.SpellLocked(k) && !m.CastSpell(k) && k.SpellChanEnd < 0, "KUGG lock: an anchored Kugg cannot start a Recall");
            k.Spell = SpellId.Blink;
            Check(m.SpellLocked(k) && !m.CastSpell(k, 0, 1) && k.X == x0 && k.Y == y0 && k.KuggRooted, "KUGG lock: nor Blink away with his turret");
            k.Spell = SpellId.Shield;
            Check(!m.SpellLocked(k) && m.CastSpell(k), "KUGG lock: a Shield still works while anchored");
            k.Spell = SpellId.Blink; k.SpellAt = 0;
            for (int i = 0; i < 1200 && k.KuggRooted; i++) m.Step(PlayerInput.None); // the shots, the pack-up
            Check(!k.KuggRooted && !m.SpellLocked(k) && m.CastSpell(k, 0, 1), "KUGG lock: walking again, he can Blink");

            // the bot never anchors with an enemy hero right on him
            var mb = MakeBots(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            var kb = mb.Player; var b = kb.Bot; var ty = mb.Foe(HeroId.Tyra);
            float x = 7.5f, y = 17.5f;
            Place(kb, x, y); Place(ty, x, y - 5); kb.Charge = 4;
            Check(b.SpecialWanted(false), "KUGG bot: with 4 stocks and an enemy hero 5 tiles off he anchors (as before)");
            Place(ty, x, y - 3);
            Check(!b.SpecialWanted(false), "KUGG bot: with an enemy hero 3 tiles off he does not (BotTables.KuggAnchorClear)");
        }

        /// <summary>Tikta's needles fly past minions to the enemy hero behind them, as Tyra's bolt and Knut's hook (TiktaS.NeedlesPassMinions);
        /// a minion alone in reach still lights the button, and the needles thrown at it then hurt nobody.</summary>
        static void TiktaPastMinions()
        {
            Check(PassesMinions(HeroId.Tikta), "TIKTA: her needles pass minions (OneTap.PassesMinions)");
            var m = OneTapScene(HeroId.Tikta, out var p, out float cx, out float cy);
            var mn = m.NewMinion(MinionKind.Melee, 1 - p.Team, p.X + 0.9f, p.Y); m.AddStatus(mn, St.Stun, 999);
            Check(m.TapHasTarget(p), "TIKTA: a minion alone in reach lights the button");
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 1.8f, p.Y); m.AddStatus(e, St.Stun, 999);
            var aim = m.AimFor(p, 0, 1);
            var marks = m.AimTargets(p, aim, new System.Collections.Generic.List<Unit>());
            Check(marks.Count == 1 && marks[0] == e, "TIKTA: the aim preview marks the hero behind the minion, not the minion");
            float mh = mn.Hp, eh = e.Hp; int heroHits = 0;
            Check(m.TryCastSpecial(p, 0, 1), "TIKTA: a drag at the minion with the hero behind it");
            for (int k = 0; k < 90; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) if (ev.Type == EventType.Hit && ev.B == e && ev.Dmg == DmgKind.Special) heroHits++; }
            Check(mn.Hp == mh && e.Hp < eh && heroHits == TiktaS.Count, $"TIKTA: all {TiktaS.Count} needles pass the minion ({mh:0} -> {mn.Hp:0}) and hit the hero behind ({heroHits} hits)");
            // only the minion on the line: the needles fly through it to the end of her ring and hurt nobody
            m = OneTapScene(HeroId.Tikta, out p, out cx, out cy);
            mn = m.NewMinion(MinionKind.Melee, 1 - p.Team, p.X + 0.9f, p.Y); m.AddStatus(mn, St.Stun, 999); mh = mn.Hp;
            m.Step(TapIn()); for (int k = 0; k < 90; k++) m.Step(PlayerInput.None);
            Check(mn.Hp == mh && !p.SpecialReady, "TIKTA: a tap at a lone minion spends the charge and the needles pass it unhurt");
        }

        static bool Toward(float ang, float want, float tol = 0.06f) => !float.IsNaN(ang) && Math.Abs(SimMath.Atan2(SimMath.Sin(ang - want), SimMath.Cos(ang - want))) < tol;

        /// <summary>The bot audit of 2026-11-13 (Rickard "Se om det är fel på fler bottar som med Knut", "ja bygg ... den bot fixen"): Gold,
        /// Platinum and Ultra bots drag the one special by hand as a player may (BotSpecial.OneTapDrag): Knut's hook, Bram's hammer and
        /// Tyra's bolt at a hero past the white ring, the hero rather than a random minion, Moa's field onto the most hurt ally. Easy and
        /// Normal bots tap as before.</summary>
        static void BotDragChecks()
        {
            float x = 7.5f, y = 17.5f, down = -SimMath.PI / 2; // the open stretch KuggChecks uses: a clear line 5 tiles down
            foreach (var id in new[] { HeroId.Knut, HeroId.Bram, HeroId.Tyra })
            {
                foreach (var d in new[] { Difficulty.Hard, Difficulty.Normal })
                {
                    var m = MakeBots(id, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Blast, HeroId.Bonk, HeroId.Kugg }, d, 11);
                    var p = m.Player; var b = p.Bot; var e = m.Foe(HeroId.Blast);
                    Place(p, x, y); Place(e, x, y - 5); m.AddStatus(e, St.Stun, 999); p.Charge = p.HDef.MaxCharge;
                    bool hard = d == Difficulty.Hard, wanted = b.SpecialWanted(false), drag = b.OneTapDrag(out float ang, out _);
                    Check(5 > TapReach(id) + e.R && !m.TapHasTarget(p), $"BOT DRAG {id}: the enemy hero 5 tiles off is past his white ring ({TapReach(id)}), the tap has nobody");
                    if (hard) Check(wanted && drag && Toward(ang, down), $"BOT DRAG {id}: a Hard bot wants it and drags it at the hero ({ang:0.00} rad)");
                    else Check(!wanted && !drag, $"BOT DRAG {id}: a Normal bot taps as before, so it waits");
                    if (!hard || id != HeroId.Knut) continue;
                    bool caught = false; // the whole way: the Hard bot Knut hooks the hero 5 tiles off
                    for (int k = 0; k < 240 && !caught; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) if (ev.Type == EventType.Pull && ev.A == p && ev.B == e) caught = true; }
                    Check(caught, "BOT DRAG Knut: a Hard bot hooks an enemy hero 5 tiles off (the tap's ring is 2.3)");
                }
            }
            // a hero and a minion both in Ragn's reach: the Hard bot leaps at the hero, never the minion
            for (int seed = 1; seed <= 6; seed++)
            {
                var m = MakeBots(HeroId.Ragn, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Blast, HeroId.Bonk, HeroId.Kugg }, Difficulty.Hard, seed);
                var p = m.Player; var e = m.Foe(HeroId.Blast);
                Place(p, x, y); Place(e, x, y - 2.5f); m.AddStatus(e, St.Stun, 999); p.Charge = p.HDef.MaxCharge;
                var mn = m.NewMinion(MinionKind.Melee, e.Team, x - 1.2f, y); m.AddStatus(mn, St.Stun, 999);
                Check(p.Bot.OneTapDrag(out float ang, out float k) && Toward(ang, down) && Math.Abs(k * DragRing0(m, HeroId.Ragn) - 2.5f) < 0.3f, $"BOT DRAG Ragn (seed {seed}): the leap goes at the hero, not the minion beside him ({ang:0.00} rad, k {k:0.00})");
            }
            // Moa: the field onto the most hurt ally in her ring, not a random one
            {
                var m = MakeBots(HeroId.Moa, new[] { HeroId.Knut, HeroId.Fenn }, new[] { HeroId.Blast, HeroId.Bonk, HeroId.Kugg }, Difficulty.Hard, 5);
                var p = m.Player; var hurt = m.Heroes.Find(u => u.Hero == HeroId.Knut && u.Team == p.Team); var fine = m.Heroes.Find(u => u.Hero == HeroId.Fenn && u.Team == p.Team);
                Place(p, x, y); Place(hurt, x, y - 2); Place(fine, x - 2, y); hurt.Hp = hurt.MaxHp * 0.3f; fine.Hp = fine.MaxHp * 0.9f;
                Check(p.Bot.OneTapDrag(out float ang, out float k) && Toward(ang, down) && Math.Abs(k * DragRing0(m, HeroId.Moa) - 2) < 0.05f, $"BOT DRAG Moa: the field goes onto the hurt ally ({ang:0.00} rad, k {k:0.00})");
                hurt.Hp = hurt.MaxHp; p.Hp = p.MaxHp * 0.2f;
                Check(p.Bot.OneTapDrag(out ang, out k) && k == 0, "BOT DRAG Moa: hurt herself, the field goes where she stands");
            }
        }
        static float DragRing0(Match m, HeroId id) => m.DragRing(id);
    }
}
