namespace BAM.Sim
{
    public enum ProjMode : byte
    {
        Auto,        // an auto attack: homing (Speed > 0) or a timed flight from the release point (Travel s)
        Hammer,      // Bram tap: homes in on an enemy hero, then bounces to up to two more enemies
        HammerLine,  // Bram drag: straight, through every enemy on the line
        Bolt,        // Tyra: straight after a short draw, stops in the first enemy
        Hook,        // Knut: flies out and comes back on its chain, pulls the first enemy hero
        Needle,      // Tikta tap: homes in on its target
        NeedleLine,  // Tikta drag: straight, stops in the first enemy
        Bomb,        // Blast tap: a fuse circle for 0.5 s, then the blast
        FireLane,    // Blast drag: a burning strip for 2 s
        HoneyField,  // Moa drag: a shield circle for 2 s
        WarnLine,    // a red warning line before a shot (bots dodge it)
        WarnCircle,  // a red warning circle
    }

    /// <summary>
    /// A shot, a special on its way, a ground area or a warning. Pooled inside the match: keep Serial with any reference.
    /// T counts up from 0 (or from a negative wind-up, during which a shot rides along in its shooter's hand).
    /// </summary>
    public sealed class Proj
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
        public int Ticks;
        internal Unit[] Hit = new Unit[16];
        internal int HitCount;

        public bool IsShot => Mode <= ProjMode.NeedleLine;
        /// <summary>A shot that has left the shooter's hand and should be drawn.</summary>
        public bool Flying => !Done && IsShot && T >= 0;
        public bool IsZone => Mode == ProjMode.Bomb || Mode == ProjMode.FireLane || Mode == ProjMode.HoneyField;
        public bool IsWarning => Mode == ProjMode.WarnLine || Mode == ProjMode.WarnCircle;
        /// <summary>The zone or warning shape: a circle when length is 0, else a strip from (X, Y) along Ang.</summary>
        public float ZoneLength => Mode == ProjMode.FireLane || Mode == ProjMode.WarnLine ? D : 0;
        /// <summary>Seconds a zone or warning has left.</summary>
        public float TimeLeft => Mode == ProjMode.Bomb ? -T : Dur - T;
        public ZoneKind Zone => Mode == ProjMode.HoneyField ? ZoneKind.HoneyField : Mode == ProjMode.FireLane ? ZoneKind.FireLane
            : Mode == ProjMode.Bomb ? ZoneKind.BombFuse : Mode == ProjMode.WarnLine ? ZoneKind.WarnLine : ZoneKind.WarnCircle;

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
            X = Y = PX = PY = OX = OY = Ang = D = Travel = Speed = T = Dmg = Radius = Dur = 0;
            Big = OnHit = Launched = Out = Done = Tied = false; Ticks = 0;
            for (int i = 0; i < HitCount; i++) Hit[i] = null;
            HitCount = 0;
        }
    }
}
