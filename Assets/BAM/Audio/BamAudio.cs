using UnityEngine;

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
    }

    public static class BamAudio
    {
        /// <summary>Once at boot: synthesizes the clips and builds the voice pool. Safe to call twice.</summary>
        public static void Init() { }

        /// <summary>A 2D sound (UI, the player's own hero).</summary>
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f) { }

        /// <summary>A sound in the world: quieter the farther it is from the listener point.</summary>
        public static void PlayAt(SfxId id, Vector3 worldPosition, float volume = 1f) { }

        /// <summary>The game layer moves the listener point with the camera's focus every frame.</summary>
        public static void SetListener(Vector3 worldPosition) { }

        /// <summary>Silences everything at once (pause, leaving a match).</summary>
        public static void StopAll() { }
    }
}
