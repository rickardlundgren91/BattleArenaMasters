using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    /// <summary>One special per hero, cast with a tap (Rickard 2026-11-01, after tester feedback that aiming by drag feels bad
    /// and two specials per hero blur the roles; the ABC model). Each hero keeps its unique form (the old drag form: Tyra's bolt,
    /// Knut's hook, Blast's bomb, Fenn's thrown wolf, Bram's Anvil Drop, Tikta's needle line, Bonk's boulder, Moa's honey
    /// field) and the tap aims it by itself at a random enemy hero or minion in reach with a clear line (no hero first, never a
    /// tower: specials do not hurt towers). Placed forms land on the target's spot. Moa alone aims at friends: a random ally hero
    /// in her range, else herself. With nobody in reach the button is grey and the tap does nothing.
    /// Rules.OneTapSpecial false brings back the old tap + drag (kept whole for at least two versions, Rickard: "går att ta
    /// tillbaka"). The drag code paths are reused: a tap here just picks the aim the drag would have had.</summary>
    public sealed partial class Match
    {
        readonly List<Unit> tapBuf = new List<Unit>(48);
        /// <summary>This match plays the one-tap special (Rules.OneTapSpecial; a test may turn it off to check the old drag forms).</summary>
        public bool OneTap = Rules.OneTapSpecial;

        /// <summary>How far a tapped special looks for its target. The drag reach, except Bonk (his big Ground Slam's circle) and Tyra: her bolt flies on over the lane,
        /// so she takes any enemy within TyraS.TapTarget (7 tiles, about what the screen shows ahead of her).</summary>
        public static float TapReach(HeroId id) => id == HeroId.Tyra ? TyraS.TapTarget : id == HeroId.Bonk ? BonkS.SlamRadiusOneTap : Defs.Hero(id).DragReach;

        /// <summary>The enemies a tap of h's special could pick now (heroes and minions in reach, seen, clear line).</summary>
        List<Unit> TapTargets(Unit h) => EnemiesInRange(h, TapReach(h.Hero), Pick.HeroOrMinion, true, tapBuf);

        /// <summary>Would a tap of h's special fire now (target-wise; the charge is SpecialReady)? Moa always can: herself.
        /// The HUD greys the button when this is false.</summary>
        public bool TapHasTarget(Unit h)
        {
            if (h == null || !h.Alive || h.Kind != UnitKind.Hero) return false;
            if (h.Hero == HeroId.Moa) return true;
            return TapTargets(h).Count > 0;
        }

        /// <summary>A random ally hero (not Moa herself, not a clone) within Moa's range; null when none.</summary>
        Unit TapAlly(Unit h)
        {
            tapBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u == h || !u.Alive || u.Kind != UnitKind.Hero || u.IsClone || u.Team != h.Team || u.Has(St.Untargetable)) continue;
                if (SimMath.Dist(h, u) > MoaS.Range + u.R) continue;
                tapBuf.Add(u);
            }
            return tapBuf.Count > 0 ? PickRandom(tapBuf) : null;
        }

        /// <summary>The one-tap cast (Rules.OneTapSpecial): picks the target and casts the hero's form at it. False (nothing spent,
        /// no random roll) when it cannot fire now.</summary>
        public bool TryTapSpecial(Unit h)
        {
            if (!h.Alive || h.Kind != UnitKind.Hero || h.IsClone || !h.SpecialReady || h.Has(St.Stun) || State != MatchState.Play) return false;
            Unit t;
            if (h.Hero == HeroId.Moa) t = TapAlly(h) ?? h;
            else
            {
                var l = TapTargets(h);
                if (l.Count == 0) return false;
                if (h.Hero == HeroId.Bonk) return TryCastSpecial(h, float.NaN, 0); // the big Ground Slam round him: no aim, no roll
                t = PickRandom(l);
            }
            float dx = t.X - h.X, dy = t.Y - h.Y, d = SimMath.Hypot(dx, dy);
            float ang = d > 1e-4f ? SimMath.Atan2(dy, dx) : h.Facing;
            float reach = Math.Max(0.01f, h.HDef.DragReach);
            float k = SimMath.Clamp(d / reach, 0, 1); // placed forms (Bram, Fenn, Blast, Moa) land on the target's spot
            return TryCastSpecial(h, ang, k);
        }
    }
}
