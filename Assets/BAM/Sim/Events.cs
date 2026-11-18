namespace BAM.Sim
{
    public enum DmgKind : byte { Auto = 0, Special = 1, Passive = 2 }

    public enum EventType : byte
    {
        AttackStart,   // A starts an auto attack on B; V = wind-up in s (the shot leaves then); Kind = ProjKind
        Hit,           // a shot or special lands: A on B, V = damage dealt (can be 0); Kind = ProjKind; Big = a strong hit
        Damage,        // B lost V health to A (every damage, ticks too); Dmg says what kind
        Heal,          // B got V health back
        Death,         // B died, A = the killer (can be null: tower, minion); V = the killer's kill streak
        Respawn,       // A is back at its spawn
        Cast,          // A used its special; Kind = HeroId, Big = it was dragged; X/Y = where it aims
        Interrupt,     // a stun or root on A cut its special
        LevelUp,       // V = the new level
        TowerDown,     // tower A fell
        CloneSpawn,    // Fenn's clone A appeared beside Fenn B
        CloneEnd,      // clone A is gone
        WolfSummon,    // Fenn B called wolf A; Big = it was thrown
        WolfPounce,    // wolf A landed on B (B can be null: it landed on nothing)
        Shield,        // A shielded B for V; Big = Queen's Instinct
        Ambush,        // A's attack from a bush hits harder
        Explosion,     // at X/Y with radius V; Kind = ProjKind (Blast's bomb, Bram's splash)
        Blocked,       // a shot hit a wall at X/Y; Kind = ProjKind
        MatchStart,    // the countdown is over
        MatchOver,     // V = the winning team
        SpecialDenied, // the player pressed the special but it could not fire (no target in reach)
        Ignite,        // A set B on fire
        Pull,          // A's anchor caught B
        SpellCast,     // A used its battle spell; Kind = SpellId; X/Y = where A stood (a blink starts there, A is now at its end)
        SpellCancel,   // A's recall was cut (moved, hit, stunned); its spell is ready again within 4 s
        Recall,        // A's recall finished: A is back at its spawn; X/Y = where it left from
        TiktaMark,     // Tikta A put her Deathmark on hero B
        TiktaMarkPop,  // Tikta A's needle took her Deathmark off B: the hit (its own Hit event) dealt V extra
        ChainSet,      // Blast A's Supernova set off Chain Reaction on burning hero B: X/Y = the frozen spot, V = its radius (it blasts 0.25 s later)
        Knock,         // A's boulder (or Hilde's keg, Kind) knocked B back: X/Y = where B would land, V = the slide time
        ShadowEscape,  // Fenn A ran off faster because enemy hero B's attack hit his decoy (Fenn 2.0); V = seconds
        WallCrash,     // Hilde A's keg knocked hero B into a wall and stunned it (Wall Crash): X/Y = the wall side, V = the stun
        Quake,         // Oaken A's quake went off from where he stands to X/Y (V = its length); its hits and knocks are their own events
        OakenGuard,    // Oaken A's Stored Impact took V off B's hit and stored part of it for his next attack
        Telegraph,     // A tapped its special: the aim is locked and it goes in V s (Rules.SpecialTelegraph); B = the target, Kind = HeroId, X/Y = where it aims
        Jump,          // A stepped on a jump pad (Grove): it flies V s to X/Y, over walls
        KuggPack,      // Kugg A's Anchor ran out of shots (or time): he pulls his spikes up and can walk again in V s
        RagnLand,      // Ragn A came down from his Moon Leap at X/Y (B = the enemy the landing hit, can be null); V = the shield he got
        TranceStep,    // Skrall A's Trance gathered one more step: V = the steps he has now (1-3); Big = the full three
        FlockEnd,      // Ravna A is herself again at X/Y after her Crow Flock
        ItemPicked,    // hero A took item Kind (ItemId) from its offer (Items.cs)
        ItemProc,      // item Kind (ItemId) worked on A: Quarry Mark 0 = marked, 1 = spent
        FennVeil,      // Fenn A vanished (his Veil): V = seconds; his decoy comes with its own CloneSpawn
        VeilStrike,    // Fenn A attacked B out of his Veil: he is seen again, this hit is the Shadow Strike (Big)
        VeilEnd,       // Fenn A's Veil ran out without an attack: he is seen again at X/Y
        CcBlocked,     // Moa's shield on hero B stopped A's stun, root, pull or knock (Match.CcImmune); X/Y = where B stands. Display only:
                       // no rule reads it (Rickard 2026-11-18 "gör den fixen": the enemy saw the hook land and nothing happen)
    }

    /// <summary>One thing that happened in the last Step. A and B are the units involved (check ASerial/BSerial if you keep them).</summary>
    public struct SimEvent
    {
        public EventType Type;
        public Unit A, B;
        public int ASerial, BSerial;
        public float X, Y, V;
        public byte Kind;
        public bool Big;
        public DmgKind Dmg;
        /// <summary>The flying shot (Proj.Serial, IsShot) whose update sent this event, 0 for none: the view marks exactly the shot that
        /// hit (two shots of one kind from one shooter can end in the same tick, Tyra's volley). Metadata only: no rule reads it.</summary>
        public int Shot;
    }
}
