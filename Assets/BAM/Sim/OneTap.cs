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
        /// <summary>This match lets a drag aim the one special by hand (Rules.DragAimSpecial; a test may turn it off).</summary>
        public bool DragAim = Rules.DragAimSpecial;
        /// <summary>A drag aims now: the one-tap game with drag aim on, outside the guided match (it teaches the tap only: new players
        /// got stuck on the old drag step, 2026-11-01).</summary>
        public bool DragAimOn => OneTap && DragAim && Tut == null;

        /// <summary>Can a drag aim h's special now (the HUD lets the SPECIAL button drag)? CanDragAim, plus veiled Fenn's Shadow Strike:
        /// the Veil itself has nothing to aim, the strike out of it has (hero review 2026-11-13).</summary>
        public bool DragAimNow(Unit h) => h != null && (CanDragAim(h.Hero) || h.VeilStrikeReady);

        /// <summary>Has h's special anything to aim? Bonk's Ground Slam lands round him and Kugg's turret where he stands: tap only.</summary>
        public static bool CanDragAim(HeroId id) => id != HeroId.Bonk && id != HeroId.Kugg && id != HeroId.Skrall && id != HeroId.Fenn && !(id == HeroId.Ravna && RavnaS.NightFlight); // Fenn's Veil is cast where he stands (Heroes.cs) // Skrall's three strikes pick their own enemies (Hero8.cs)

        /// <summary>The share of the form's reach (TryCastSpecial's k) a drag with share k aims at: a placed form (Bram's anvil, Blast's bomb,
        /// Fenn's thrown wolf, Moa's field) lands at most at the white ring (TapReach), as far as a tap can put it, so the drag gives
        /// control but no extra reach (proposal 2026-11-07). Lines (Tyra's bolt, Knut's hook, Tikta's needles) fly as far as a tap's.</summary>
        public static float DragShare(HeroId id, float k)
        {
            k = SimMath.Clamp(float.IsNaN(k) ? 0 : k, 0, 1);
            var d = Defs.Hero(id);
            if (d.Drag != AimShape.Circle || d.DragReach <= 0.01f) return k;
            return k * Math.Min(1f, TapReach(id) / d.DragReach);
        }

        /// <summary>How far a tapped special looks for its target: the hero's auto attack range, the white ring round them (Rickard
        /// 2026-11-02, Knut "samma som hans range", then Tikta, Bram, Tyra, Moa and Blast "fel range"), except Bonk, whose one special
        /// is the big Ground Slam's circle. Knut's long hook (Franco's, 2026-11-03) looked for a hero up to 6 tiles for a while; Rickard
        /// 2026-11-03 "den går att aktivera för lång range": his tap is back to his ring too, the hook still flies the full
        /// KnutS.HookDistance (7). Tyra's bolt still flies on until it hits something; Tikta's needles stop at the ring.</summary>
        public static float TapReach(HeroId id) => id == HeroId.Bonk ? BonkS.SlamRadiusOneTap : id == HeroId.Kugg ? KuggS.TapReach
            : id == HeroId.Ragn ? RagnS.LeapReach : id == HeroId.Ravna ? RavnaS.FlockReach : Defs.Hero(id).Range; // Ragn's leap and Ravna's flock reach further than their claws (Hero8.cs)

        /// <summary>How far Tikta's needle line flies: her ring in the one-tap game (TapReach), TiktaS.LineReach for the old drag.</summary>
        float NeedleReach => OneTap ? TapReach(HeroId.Tikta) : TiktaS.LineReach;

        /// <summary>The enemies a tap of h's special could pick now (heroes and minions in reach, seen, clear line). A straight
        /// shot (Tyra's bolt, Tikta's needles, Knut's hook) never picks one behind a base (either team's), which would
        /// stop it (TowerReach), so the tap is not wasted on the tower and the button stays grey with only such targets.</summary>
        List<Unit> TapTargets(Unit h)
        {
            var l = EnemiesInRange(h, TapReach(h.Hero), Pick.HeroOrMinion, true, tapBuf);
            float half = h.Hero == HeroId.Tyra ? TyraS.Hitbox : h.Hero == HeroId.Tikta ? TiktaS.LineHitbox : h.Hero == HeroId.Knut ? KnutS.HookRadius
                : h.Hero == HeroId.Ravna ? RavnaS.HitRadius : -1;
            if (half < 0) return l;
            for (int i = l.Count - 1; i >= 0; i--) if (TowerInWay(h, l[i], half)) l.RemoveAt(i);
            return l;
        }

        /// <summary>Does h's special fly past minions to the heroes behind (Knut's hook, Tyra's bolt, Tikta's needles)? Their tap still rolls among ALL
        /// enemies in reach, minions too, as every tap (Rickard 2026-11-03: "man ska inte få för mycket hjälp sådär, att det är random
        /// attack på dom inom range är viktigt för spelet ... focus"): the player picks the odds by how many targets stand in the ring.</summary>
        public static bool PassesMinions(HeroId id) => id == HeroId.Knut ? KnutS.HookPassesMinions : id == HeroId.Tyra ? TyraS.BoltPassesMinions : id == HeroId.Tikta && TiktaS.NeedlesPassMinions;

        /// <summary>Would a tap of h's special fire now (target-wise; the charge is SpecialReady)? Moa always can: herself.
        /// The HUD greys the button when this is false.</summary>
        public bool TapHasTarget(Unit h)
        {
            if (h == null || !h.Alive || h.Kind != UnitKind.Hero) return false;
            if (h.Hero == HeroId.Moa) return true;
            if (h.Hero == HeroId.Fenn) return true; // the Veil needs nobody in reach: it is an escape too
            if (h.Hero == HeroId.Ravna && NightFlightOn) return true; // Night Flight goes the way she runs: nobody needed in reach
            if (h.Hero == HeroId.Kugg && h.KuggRooted) return false;
            return TapTargets(h).Count > 0;
        }

        /// <summary>A random ally hero (not Moa herself, not a clone) within Moa's ring (TapReach); null when none.</summary>
        Unit TapAlly(Unit h)
        {
            tapBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u == h || !u.Alive || u.Kind != UnitKind.Hero || u.IsClone || u.Team != h.Team || u.Has(St.Untargetable)) continue;
                if (SimMath.Dist(h, u) > TapReach(HeroId.Moa) + u.R) continue; // her ring, as every tap (TapReach)
                tapBuf.Add(u);
            }
            return tapBuf.Count > 0 ? PickRandom(tapBuf) : null;
        }

        /// <summary>The one-tap cast (Rules.OneTapSpecial): picks the target and casts the hero's form at it. False (nothing spent,
        /// no random roll) when it cannot fire now.</summary>
        public bool TryTapSpecial(Unit h)
        {
            if (h.VeilStrikeReady) return TryCastSpecial(h, float.NaN, 0); // the Shadow Strike (Heroes.cs FennStrikeNow)
            if (!h.Alive || h.Kind != UnitKind.Hero || h.IsClone || !h.SpecialReady || h.Has(St.Stun) || State != MatchState.Play || Telegraphing(h)) return false;
            Unit t;
            if (h.Hero == HeroId.Moa) t = TapAlly(h) ?? h;
            else if (h.Hero == HeroId.Fenn) return TryCastSpecial(h, float.NaN, 0); // the Veil where he stands: no aim, no warning (it hurts nobody)
            else if (h.Hero == HeroId.Ravna && NightFlightOn) return TryCastSpecial(h, RavnaRunAng(h), 1); // the flock sets off the way she runs, at once: no target, no aim, no warning (Rickard 2026-11-12, as ABC2's Ron)
            else
            {
                var l = TapTargets(h);
                if (l.Count == 0) return false;
                if (h.Hero == HeroId.Kugg) return TryCastSpecial(h, float.NaN, 0); // the Anchor where he stands: no aim, no warning (it hurts nobody)
                if (h.Hero == HeroId.Skrall) return TeleOn ? CastWithFlash(h, null, float.NaN, 0) : TryCastSpecial(h, float.NaN, 0); // his strikes pick their enemies and warn by themselves (Hero8.cs)
                if (h.Hero == HeroId.Bonk) return !TeleOn ? TryCastSpecial(h, float.NaN, 0) : TeleTime(h.Hero) > 0 ? StartTelegraph(h, null, float.NaN, 0) : CastWithFlash(h, null, float.NaN, 0); // the big Ground Slam round him: no aim, no roll
                t = PickRandom(l); // every hero, Knut and Tyra too: a random enemy in reach, never heroes first (focus, Rickard 2026-11-03)
            }
            float dx = t.X - h.X, dy = t.Y - h.Y, d = SimMath.Hypot(dx, dy);
            if (h.Hero == HeroId.Tyra && TyraS.AimLead && t.Kind == UnitKind.Hero && !t.IsClone)
            {   // where the running hero will be when the bolt gets there, once, at the tap (TyraS.AimLead; Bram's anvil does not lead)
                float lead = (TeleOn ? TeleTime(h.Hero) : 0) + TyraS.Windup + d / TyraS.Speed;
                dx += t.MoVx * lead; dy += t.MoVy * lead; d = SimMath.Hypot(dx, dy);
            }
            else if (h.Hero == HeroId.Ragn && t.Kind == UnitKind.Hero && !t.IsClone && TeleOn)
            {   // his leap starts after the red warning: aim where the walking hero will be then (the leap itself leads, RagnStopShort)
                float lead = TeleTime(h.Hero);
                dx += t.MoVx * lead; dy += t.MoVy * lead; d = SimMath.Hypot(dx, dy);
            }
            float ang = d > 1e-4f ? SimMath.Atan2(dy, dx) : h.Facing;
            float reach = Math.Max(0.01f, h.HDef.DragReach);
            float k = SimMath.Clamp(d / reach, 0, 1); // placed forms (Bram, Fenn, Blast, Moa) land on the target's spot
            if (TeleOn) return TeleTime(h.Hero) > 0 ? StartTelegraph(h, t, ang, k) : CastWithFlash(h, t, ang, k); // the warning first, aim locked now (Telegraph.cs)
            return TryCastSpecial(h, ang, k);
        }

        /// <summary>Night Flight's way (Rickard 2026-11-12: "ravnas special ska man inte sikta på någon den använder man och färdas i
        /// samma riktning som man går helt enkelt", "precis som ron"): a player's joystick, else the way she faces; a bot sets off at a
        /// random enemy hero in reach, or, hurt, the way it runs (BotSpecial: the same rule on both teams).</summary>
        float RavnaRunAng(Unit h)
        {
            if (h.Bot == null)
            {
                if (h.Seat >= 0)
                {
                    float mx = seatMoveX[h.Seat], my = seatMoveY[h.Seat];
                    if (mx * mx + my * my >= 0.04f) return SimMath.Atan2(my, mx);
                }
                return h.Facing;
            }
            if (h.Hp < h.MaxHp * 0.35f) return h.Facing; // hurt: away, the way she is running (BotSpecial)
            var l = TapTargets(h); l.RemoveAll(u => u.Kind != UnitKind.Hero || u.IsClone);
            if (l.Count == 0) return h.Facing;
            var t = PickRandom(l);
            return SimMath.Atan2(t.Y - h.Y, t.X - h.X);
        }

        /// <summary>How far the aim ring round the hero reaches while a drag aims (the HUD): with the one-tap special a placed form and
        /// Tikta's needles stop at the white ring (DragShare, NeedleReach), the other lines fly their own length (Knut's hook 7).</summary>
        public float DragRing(HeroId id)
        {
            var d = Defs.Hero(id);
            if (!OneTap) return d.DragReach;
            return d.Drag == AimShape.Circle || id == HeroId.Tikta ? TapReach(id) : d.DragReach;
        }

        /// <summary>The drag cast (Rules.DragAimSpecial): the hero's one special the way ang points (map space), k of its reach
        /// (DragShare), through the same warning as a tap (aim locked, red shape, TeleTime). It needs no target in reach: the player
        /// chooses where it goes. Bonk and Kugg have nothing to aim, so their drag is a tap. False (nothing spent) when it cannot fire now.</summary>
        public bool TryAimSpecial(Unit h, float ang, float k)
        {
            // veiled Fenn's drag picks whom his Shadow Strike hits: the enemy nearest the aimed spot (Heroes.cs FennStrikeNow); no warning,
            // as the tapped strike (hero review 2026-11-13)
            if (h.VeilStrikeReady && !float.IsNaN(ang)) return TryCastSpecial(h, ang, DragShare(h.Hero, k));
            if (float.IsNaN(ang) || !CanDragAim(h.Hero)) return TryTapSpecial(h);
            if (!h.Alive || h.Kind != UnitKind.Hero || h.IsClone || !h.SpecialReady || h.Has(St.Stun) || State != MatchState.Play || Telegraphing(h)) return false;
            k = DragShare(h.Hero, k);
            if (TeleOn) return TeleTime(h.Hero) > 0 ? StartTelegraph(h, null, ang, k) : CastWithFlash(h, null, ang, k);
            return TryCastSpecial(h, ang, k);
        }
    }
}
