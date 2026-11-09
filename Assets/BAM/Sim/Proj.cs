namespace BAM.Sim
{
    public enum ProjMode : byte
    {
        Auto,        // an auto attack: homing (Speed > 0) or a timed flight from the release point (Travel s)
        Hammer,      // Bram tap: homes in on an enemy hero or minion, then bounces to up to two more enemies
        HammerLine,  // unused since 2026-10-28 (Bram's old drag); kept so the numbers after it stay the same
        Bolt,        // Tyra: straight after a short draw, stops in the first enemy
        Hook,        // Knut drag: flies out and comes back on its chain, pulls the first enemy it touches
        Needle,      // Tikta tap: homes in on its target
        NeedleLine,  // Tikta drag: straight, stops in the first enemy
        Bomb,        // Blast tap: a fuse circle for 0.5 s, then the blast
        FireLane,    // Blast drag: a burning strip for 2 s
        HoneyField,  // Moa drag: a shield circle for 2 s
        WarnLine,    // a red warning line before a shot (bots dodge it)
        WarnCircle,  // a red warning circle
        ChainBlast,  // Blast 2.0 Chain Reaction: waits -T s at a frozen spot (Radius), then blasts the other enemies there
        Slam,        // Bonk tap: winds up -T s where he stands, then hits everyone round him (Radius)
        Boulder,     // Bonk drag: straight after a short wind-up, stops in the first enemy and knocks it back
        Smash,       // Bonk's auto attack: a slow overhead club slam, -T s of wind-up, then a ground circle (Radius) in front of him
        AnchorSlam,  // Knut tap: winds up -T s where he stands, then hits and slows everyone round him (Radius)
        Drop,        // Bram drag, Anvil Drop: waits -T s at its point (a red warning circle shows it), then hits and stuns round it (Radius)
        Volley,      // Tyra tap, Arrow Volley: one arrow of five, straight through enemies; the volley hurts each enemy once
        Echo,        // Bram's Backlash: flies from his target back towards him and hits the first other enemy
        Keg,         // Hilde: a keg on the ground, a fuse circle for -T s, then the blast (Kind HildeKeg = the drag, HildeBurst = the tap)
        Worldbreaker, // Oaken tap: winds up -T s where he stands, then hits everyone round him (Radius)
        Quake,       // Oaken drag: winds up -T s, then hits everyone along the line Ang, D long, Radius half wide, and knocks heroes along it
        FeatherTrail, // Tyra: the wind and feathers her bolt leaves along its path for Dur s, slowing the enemies in it (Ang, D, Radius)
        Bramble,     // Nässla: the Thorn Thicket, a circle (Radius) for Dur s that slows and poisons enemies and heals allies in it
        Strike,      // Skrall: one Thunder Judgement strike, waits -T s at its spot (a red warning circle shows it), then hits round it (Radius)
    }

    /// <summary>
    /// A shot, a special on its way, a ground area or a warning. Pooled inside the match: keep Serial with any reference.
    /// T counts up from 0 (or from a negative wind-up, during which a shot rides along in its shooter's hand).
    /// </summary>
    public sealed partial class Proj
    {
        public int Serial;
        public ProjMode Mode;
        /// <summary>What the art draws for a flying shot.</summary>
        public ProjKind Kind;
        public int Team;
        public Unit Src, Tgt, Caught;
        public float X, Y, PX, PY;
        /// <summary>Where a timed shot left the shooter.</summary>
        public float OX, OY;
        /// <summary>Straight shots, lanes and warning lines: the direction and the length (D) or distance flown.</summary>
        public float Ang, D;
        public float Travel, Speed, T, Dmg;
        /// <summary>Hit box, zone radius or warning half width.</summary>
        public float Radius;
        /// <summary>How long a zone or warning lasts.</summary>
        public float Dur;
        public bool Big, OnHit, Launched, Out, Done, Tied;
        /// <summary>An auto attack whose target fell with nobody else to go to: it flies on to the spot and ends there, no hit.</summary>
        public bool Fizzle;
        /// <summary>Where a fizzling auto attack flies to (needles and hammers keep theirs in OX/OY): the fallen target's spot, or where a target stood when it blinked or recalled away.</summary>
        public float FX, FY;
        public int Ticks;
        internal int TiktaCast; // Tikta 2.0: the cast a needle belongs to (at most one Deathmark bonus per cast)
        internal int VolleyCast; // Tyra: the Arrow Volley an arrow belongs to (its hits are shared through Unit.VolleyHit)
        /// <summary>A warning's own shot or drop (with its Serial, the pool reuses Projs): the warning ends with it, not with its caster (AIM / BRAM-01, TYRA-02).</summary>
        internal Proj Of;
        internal int OfSerial;
        /// <summary>The Serial of the shot or drop this warning belongs to (0 = none), for the view.</summary>
        public int WarnFor => OfSerial;
        internal BonkRoll Roll; // Bonk's Boulder Toss: everyone the throw has hurt, the boulder's and its pins' hits together
        internal Unit[] Hit = new Unit[16];
        internal int HitCount;

        public bool IsShot => Mode <= ProjMode.NeedleLine || Mode == ProjMode.Boulder || Mode == ProjMode.Volley || Mode == ProjMode.Echo;
        /// <summary>A shot that has left the shooter's hand and should be drawn.</summary>
        public bool Flying => !Done && IsShot && T >= 0;
        public bool IsZone => Mode == ProjMode.Bomb || Mode == ProjMode.Keg || Mode == ProjMode.FireLane || Mode == ProjMode.HoneyField || Mode == ProjMode.FeatherTrail || Mode == ProjMode.Bramble;
        public bool IsWarning => Mode == ProjMode.WarnLine || Mode == ProjMode.WarnCircle;
        /// <summary>The zone or warning shape: a circle when length is 0, else a strip from (X, Y) along Ang.</summary>
        public float ZoneLength => Mode == ProjMode.FireLane || Mode == ProjMode.WarnLine || Mode == ProjMode.FeatherTrail ? D : 0;
        /// <summary>Seconds a zone or warning has left.</summary>
        public float TimeLeft => Mode == ProjMode.Bomb || Mode == ProjMode.Keg ? -T : Dur - T;
        public ZoneKind Zone => Mode == ProjMode.HoneyField ? ZoneKind.HoneyField : Mode == ProjMode.Bramble ? ZoneKind.Bramble : Mode == ProjMode.FireLane ? ZoneKind.FireLane : Mode == ProjMode.FeatherTrail ? ZoneKind.FeatherTrail
            : Mode == ProjMode.Bomb || Mode == ProjMode.Keg ? ZoneKind.BombFuse : Mode == ProjMode.WarnLine ? ZoneKind.WarnLine : ZoneKind.WarnCircle;

        internal bool HasHit(Unit u)
        {
            for (int i = 0; i < HitCount; i++) if (Hit[i] == u) return true;
            return false;
        }
        internal void AddHit(Unit u)
        {
            if (HitCount == Hit.Length) System.Array.Resize(ref Hit, Hit.Length * 2);
            Hit[HitCount++] = u;
        }

        internal void Reset()
        {
            Mode = ProjMode.Auto; Kind = ProjKind.TowerShot; Team = 0; Src = Tgt = Caught = null;
            X = Y = PX = PY = OX = OY = Ang = D = Travel = Speed = T = Dmg = Radius = Dur = FX = FY = 0;
            Big = OnHit = Launched = Out = Done = Tied = Fizzle = false; Ticks = 0; TiktaCast = 0; VolleyCast = 0; Of = null; OfSerial = 0; Roll = null;
            for (int i = 0; i < HitCount; i++) Hit[i] = null;
            HitCount = 0;
        }
    }
}
