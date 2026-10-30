namespace BAM.Art
{
    /// <summary>
    /// The rig and animation pass of 2026-10-28 (Rickard: "snurrande vapen, konstiga hållningar när dom springer och allmänt
    /// stela karaktärer"), one switch per part so a part he does not like can be turned off alone. View only: the rules,
    /// the hit times and online never read these. Analysis: bam-work/art/rigg-anim (clips.md) and the plan in the thread
    /// "Riggning och animationer".
    /// </summary>
    public static class AnimFix
    {
        /// <summary>Weapons without spins (HeldGear.Steady): the rest aim keeps the side it had in the idle (it flipped half a turn
        /// as the fist turned), follows a share of the hand, and turns into the swing's grip and back with the eased swing instead
        /// of being let go of in a tenth of a second at every swing; Moa's staff and Tikta's needles keep their aim through the
        /// swing (WeaponDef.SwingAim), Tyra's bow stands up for the shot (WeaponDef.SwingTurn).</summary>
        public static bool FixedGrip = true;
        /// <summary>A swing, cast or hit on the move plays on the upper body only; the legs keep running.</summary>
        public static bool UpperSwing = true;
        /// <summary>The hips and legs turn to the line of travel while the body faces its target, and the stride plays
        /// backwards when the hero backs off (old Speltest MxLegYaw, Simon 2026-10-21).</summary>
        public static bool LegsFollow = true;
        /// <summary>In a fight the hero keeps facing its target between swings and turns smoothly (MatchViews), not at
        /// 900 degrees a second and back again after every swing.</summary>
        public static bool SquaredFacing = true;
        /// <summary>Only the part of an attack clip around its measured blow is played, landing on the game's release, and a
        /// cast plays from just before its peak; clips that walk the body away are held in place.</summary>
        public static bool BlowTiming = true;
        /// <summary>Eased blends, a lean into turns and starts, and a faint breath on top of the idle in a match.</summary>
        public static bool Life = true;
        /// <summary>The arms turned out from a broad body (ArmSpread, Bram 36 degrees) only as far as they hang at the side; an arm
        /// raised in a swing plays as the clip made it instead of being twisted out (Speltest's ArmRoom left raised arms alone).</summary>
        public static bool ArmRoom = true;
        /// <summary>Weapon Precision 2.0 (2026-10-30): after every turn of a held weapon its grip point is shifted into the palm
        /// (HeldGear.LockGrip), so it never floats off the fist or slides along the shaft; a shift only, the silhouette stays.</summary>
        public static bool GripLock = true;
    }
}
