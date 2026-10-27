namespace BAM.Audio
{
    // CONTRACT between BAM.Audio (owner: the sound thread) and its callers (BAM.Game, BAM.UI). Docs/CONTRACTS.md explains it.
    // Every sound is synthesized in code (no audio files). Entries are only ever added at the end of SfxId.
    public enum SfxId
    {
        None = 0,
        // UI
        UiTap, UiBack, UiConfirm, UiToggle, Countdown, CountdownGo, Victory, Defeat,
        // hero auto attacks (one per hero, in HeroId order)
        AttackBram, AttackFenn, AttackTyra, AttackMoa, AttackKnut, AttackTikta, AttackBlast,
        // specials
        BramHammerThrow, BramHammerBounce, FennWolfCall, FennWolfThrow, TyraDraw, TyraRelease, MoaShield, MoaField,
        KnutAnchorThrow, KnutAnchorHook, TiktaNeedles, BlastBombThrow, BlastExplosion, BlastFireLane,
        // combat
        HitLight, HitHeavy, ShieldBlock, Stunned, WolfBite, CloneAppear, CloneVanish,
        // minions and towers
        MinionMelee, MinionRanged, SiegeShot, TowerShot, TowerDestroyed,
        // events
        HeroDeath, AllyDeath, MinionDeath, Respawn, LevelUp, WaveSpawn, GrassEnter, SpecialReady, LowHealth,
        // Blast 2.0's Chain Reaction (XYZ #603 "chainboom"), added 2026-10-26
        BlastChainBoom,
        // Bonk, hero 8 (2026-10-26): his club swing, Ground Slam's wind-up and landing, Boulder Toss and its knockback
        AttackBonk, BonkSlamWindup, BonkSlam, BonkBoulderThrow, BonkKnock,
        BonkStun, // Ground Slam stuns a hero (Rickard's clip 2026-10-26)
        // match feel 2026-10-27, Rickard's own Speltest clips as Speltest plays them (HeroJuice.cs, Sound.cs sound mix v2): the player's
        // hit landing on an enemy hero, its last hit on a minion, a multi kill, the tower ramping up on you, its own tower under attack
        // far away, Bram's Anvil Strike, Moa's shield taking hold, three battle spells and an emote
        HitConfirmLight, HitConfirmHeavy, LastHit, MultiKill, TowerHot, TowerUnderAttack, BramAnvil, MoaHoney,
        SpellBlink, SpellRecall, SpellShield, Emote,
    }
}
