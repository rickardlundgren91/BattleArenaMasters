using System;

namespace BAM.Sim
{
    /// <summary>Tikta 2.0 Deathmark (old SimTiktaDeathmark.cs, Simon's locked design, XYZ #600/#602; Rickard 2026-10-26 "In med alla").</summary>
    public static class TiktaMarkS
    {
        public const float Duration = 3, DamageBonus = 0.5f;
    }

    /// <summary>Blast 2.0 Chain Reaction (old SimBlastChain.cs, XYZ #601/#603).</summary>
    public static class BlastChainS
    {
        public const float Coeff = 0.5f, Radius = 1.0f, Delay = 0.25f, Cooldown = 9;
    }

    public sealed partial class Match
    {
        // ---------- Tikta 2.0: Deathmark (replaces Final Sting) ----------
        // A normal auto attack from Tikta that takes health from a living, real enemy hero (dealt > 0 after shields) marks it for 3 s.
        // One mark per Tikta at a time: while it is on someone, more hits neither move it nor make it longer. The first Needle Storm
        // needle (tap or BAM's four-needle drag) that really hits the marked hero takes the mark and adds 50 % of her StatDmg to that
        // same hit: no second hit, at most one bonus per cast. Invulnerable or untargetable: no hit, the mark stays. It goes when it runs
        // out, or when Tikta or the marked hero dies. Clones, minions and towers are never marked.

        /// <summary>The hero carrying this Tikta's mark now, or null.</summary>
        public Unit TiktaMarkOn(Unit tikta)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.FindBy(St.TiktaMark, tikta) >= 0) return u;
            }
            return null;
        }

        void TiktaTryMark(Unit h, Unit t, float dealt)
        {
            if (h.IsClone || !h.Alive || t == null || !t.Alive || t.Kind != UnitKind.Hero || t.IsClone || t.Team == h.Team || !(dealt > 0)) return;
            if (TiktaMarkOn(h) != null) return; // one at a time: not moved, not made longer
            AddStatus(t, St.TiktaMark, TiktaMarkS.Duration, 0, 0, h);
            Emit(EventType.TiktaMark, h, t, 0, 0, false, t.X, t.Y);
        }

        /// <summary>The extra damage this needle's hit on tgt gets from the mark (0 without one); takes the mark. Called right before
        /// the hit's one SpecialHit, only for a hit that lands.</summary>
        float TiktaMarkBonus(Unit src, Unit tgt, Proj p)
        {
            if (src == null || src.IsClone || src.Hero != HeroId.Tikta || tgt == null || !tgt.Alive || tgt.Kind != UnitKind.Hero || tgt.Team == src.Team) return 0;
            if (tgt.Has(St.Invulnerable) || tgt.Has(St.Untargetable)) return 0; // no hit: the mark stays
            int cast = p != null ? p.TiktaCast : 0;
            if (cast != 0 && src.TiktaBonusCast == cast) return 0; // this cast already had its bonus
            int i = tgt.FindBy(St.TiktaMark, src);
            if (i < 0 || !(tgt.Sts[i].T > 0)) return 0;
            tgt.RemoveAt(i);
            if (cast != 0) src.TiktaBonusCast = cast;
            float bonus = src.StatDmg() * TiktaMarkS.DamageBonus;
            Emit(EventType.TiktaMarkPop, src, tgt, bonus, 0, true, tgt.X, tgt.Y);
            return bonus;
        }

        /// <summary>A hero died: the marks it put on others go with it (its own statuses are cleared by Kill).</summary>
        void TiktaOnDeath(Unit u)
        {
            if (u.Hero != HeroId.Tikta || u.IsClone) return;
            for (int i = 0; i < Units.Count; i++)
            {
                var o = Units[i];
                for (int k = o.StCount - 1; k >= 0; k--) if (o.Sts[k].Type == St.TiktaMark && o.Sts[k].By == u) o.RemoveAt(k);
            }
        }

        // ---------- Blast 2.0: Chain Reaction ----------
        // When Supernova's own blast reaches a living, real enemy hero (not a clone, not invulnerable or untargetable) that already
        // burns from this Blast (checked before the bomb's own damage lights anything) and her 9 s cooldown is ready, ONE hero is
        // picked: the nearest to the bomb's centre, then the lowest serial (no random). The cooldown starts at once. Where that hero
        // stands is frozen: 0.25 s later one blast of radius 1 (+ each unit's radius) hits every OTHER enemy hero and minion there once,
        // for 50 % of her StatDmg taken when it was set off. No Ignite, no new chain, no charge, never towers, allies or clones.
        // Blast dead when the bomb lands: no new chain; a chain already set off still goes off.
        // BAM's line-ups never repeat a hero, so "burns from this Blast" is the one burn's owner (the old per-Blast shares of a shared
        // burn pool only matter with two Blasts on one team).
        // BAM wall rule (Rickard 2026-10-28, as Supernova): a hero behind a wall from the bomb can't set it off, and the chain's
        // blast only reaches units with a clear line from its spot (precision audit CHAIN-01/02, 2026-10-29).

        /// <summary>Seconds until Blast's Chain Reaction is ready again (0 = ready).</summary>
        public float ChainLeft(Unit blast) => blast == null ? 0 : Math.Max(0, blast.BlastChainReadyAt - Time);

        /// <summary>u burns from this Blast right now.</summary>
        public bool BurnsFrom(Unit u, Unit blast)
        {
            if (u == null || blast == null) return false;
            int i = u.Find(St.Burn);
            return i >= 0 && u.Sts[i].T > 0 && u.Sts[i].Amount > 0 && u.Sts[i].By == blast;
        }

        /// <summary>BombBlast, before Supernova deals any damage: set off the chain if it may.</summary>
        void ChainOnImpact(Unit h, Proj bomb, float r)
        {
            if (h == null || !h.Alive || h.IsClone || h.Kind != UnitKind.Hero || h.Hero != HeroId.Blast || Time < h.BlastChainReadyAt) return;
            Unit trig = null; float best = float.MaxValue;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Kind != UnitKind.Hero || u.IsClone || u.Team == h.Team || u.Has(St.Untargetable) || u.Has(St.Invulnerable)) continue;
                float d = SimMath.Dist(u, bomb.X, bomb.Y);
                if (d > r + u.R || !BurnsFrom(u, h) || !Map.LosClear(bomb.X, bomb.Y, u.X, u.Y)) continue;
                if (d < best || d == best && u.Serial < trig.Serial) { best = d; trig = u; }
            }
            if (trig == null) return;
            h.BlastChainReadyAt = Time + BlastChainS.Cooldown;
            var p = NewProj(ProjMode.ChainBlast, h, trig.X, trig.Y);
            p.Kind = ProjKind.BlastChain; p.Tgt = trig; p.T = -BlastChainS.Delay; p.Dur = BlastChainS.Delay; p.Radius = BlastChainS.Radius;
            p.Dmg = h.StatDmg() * BlastChainS.Coeff;
            Emit(EventType.ChainSet, h, trig, BlastChainS.Radius, (byte)ProjKind.BlastChain, true, p.X, p.Y);
        }

        /// <summary>The second blast at the frozen spot: every other enemy hero and minion in reach, once each.</summary>
        void ChainBlast(Proj p)
        {
            var h = p.Src;
            var dk = h.Alive ? DmgKind.Special : DmgKind.Passive; // as the bomb: after Blast's death it still goes off
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u == p.Tgt || !u.Alive || u.Team == p.Team || u.IsClone || !(u.Kind == UnitKind.Hero || u.Kind == UnitKind.Minion)) continue;
                if (u.Has(St.Untargetable) || u.Has(St.Invulnerable) || SimMath.Dist(u, p.X, p.Y) > p.Radius + u.R || !Map.LosClear(p.X, p.Y, u.X, u.Y)) continue;
                areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++) if (areaBuf[i].Alive) SpecialHit(h, areaBuf[i], p.Dmg, ProjKind.BlastChain, true, dk, DmgFlags.NoIgnite);
            Emit(EventType.Explosion, h, null, p.Radius, (byte)ProjKind.BlastChain, true, p.X, p.Y, dk);
        }

        // ---------- Tikta 2.0: poison, Strongest Wins ----------
        /// <summary>A needle poisons an enemy hero for perSecond over dur s. One poison per Tikta per hero: a weaker one never shortens
        /// or weakens a stronger one, a stronger one replaces it (full time), the same strength renews its own time.</summary>
        void TiktaPoison(Unit src, Unit u, float perSecond, float dur)
        {
            if (!u.Alive || u.Kind != UnitKind.Hero || !(perSecond > 0) || !(dur > 0)) return;
            int i = u.FindBy(St.Poison, src);
            if (i < 0) { AddStatus(u, St.Poison, dur, perSecond, 0, src); return; }
            ref var st = ref u.Sts[i];
            float eps = Math.Max(1e-4f, Math.Abs(st.V) * 1e-4f);
            if (perSecond > st.V + eps) { st.V = perSecond; st.T = dur; }           // stronger replaces weaker (the gathered part is kept)
            else if (perSecond >= st.V - eps) st.T = Math.Max(st.T, dur);           // the same strength renews its own time
            // weaker: the stronger poison keeps its strength and its time
        }

        /// <summary>The first enemy (not a tower, not untargetable) whose circle (R + half) the segment from (ox, oy) along ang for len
        /// tiles touches; along = how far along it touches. Ties go to the earlier unit, the same on every machine.</summary>
        Unit SweptHit(Unit src, int team, float ox, float oy, float ang, float len, float half, out float along, Unit skip = null)
        {
            along = 0; Unit best = null; float bestAt = float.MaxValue, ca = SimMath.Cos(ang), sa = SimMath.Sin(ang);
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(src, u) || u.Team == team || u == skip) continue;
                float dx = u.X - ox, dy = u.Y - oy, a = dx * ca + dy * sa, perp2 = dx * dx + dy * dy - a * a, rr = u.R + half;
                if (perp2 > rr * rr) continue;
                float h = SimMath.Sqrt(Math.Max(0, rr * rr - perp2)), enter = a - h;
                if (a + h < 0 || enter > len) continue; // all of it behind the start, or past this tick's path
                enter = Math.Max(0, enter);
                if (enter < bestAt) { bestAt = enter; best = u; }
            }
            along = best != null ? bestAt : 0;
            return best;
        }
    }
}
