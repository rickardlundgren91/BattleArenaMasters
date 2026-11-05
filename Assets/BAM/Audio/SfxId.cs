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
        // Hilde, hero 9 (2026-10-29, HILDE-01): Rickard's own Speltest clips: her cannon shot (atk_hilde), the keg throw (sp_hilde),
        // Wall Crash (wallcrash)
        AttackHilde, HildeKegThrow, HildeWallCrash,
        // Knut's Anchor Slam landing (KNUT-03): Rickard's own clip made for it 2026-10-29
        KnutSlam,
        // Hilde's keg going off and the hero it shoves (HILDE-01): Rickard's own clips made for them 2026-10-29
        HildeKegBlast, HildeKnock,
        // Knut's Anchor Slam wind-up (tap): Rickard's own clip ordered 2026-10-30, silent until it exists
        KnutSlamWindup,
        // the Haste and Sprint battle spells: Rickard's own clips made for them 2026-10-30 (O-026)
        SpellHaste, SpellSprint,
        // a tower's shot landing on the player (Rickard 2026-10-30 "bättre ljud när torn attackerar en", O-033): silent until his own
        // clip TowerImpact.mp3 is in Resources/BAM/Sfx
        TowerImpact,
        // Knut's hook (thread "Ge Knuts hook rätt känsla", Rickard's own clips 2026-11-02, O-045/O-046): the chain rattling out and
        // back, and the hauled hero landing at his feet
        KnutChain, KnutHookLand,
        // Tyra's Pinpoint Bolt (drag) landing on its target: Rickard's own clip 2026-11-02 (O-048), its impact since 2026-11-02 03:51 CET
        TyraBolt,
        // a hero taps its special and the warning starts (Rules.SpecialTelegraph, 2026-11-02): a short sharp wind-up cue so a player hears
        // that a special is coming, before it lands. Silent until Rickard's own clip SpecialWarn.mp3 is in Resources/BAM/Sfx (O-049)
        SpecialWarn,
        // Kugg, hero 11 (2026-11-05, orders O-066..O-070): his shot, the Anchor going in, his anchored shot, pulling the spikes up and the
        // Spark Shot. Silent until Rickard's own clips are in Resources/BAM/Sfx
        AttackKugg, KuggDeploy, KuggShotBig, KuggPackUp, KuggSpark,
    }
}
