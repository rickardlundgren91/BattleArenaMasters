namespace BAM.Audio
{
    /// <summary>How a sound behaves at play time.</summary>
    struct SfxInfo
    {
        /// <summary>Loudness of the rendered clip relative to the others (1 = the common level).</summary>
        public float Level;
        /// <summary>How many copies may sound at once.</summary>
        public byte MaxVoices;
        /// <summary>Shortest time between two starts (s), unless the new one is clearly louder (nearer).</summary>
        public float MinGap;
        /// <summary>Who wins a voice when all 16 are busy: higher steals from lower.</summary>
        public byte Priority;
        /// <summary>Random pitch spread per play (0.05 = +-5 %), so repeated sounds do not sound like one sample.</summary>
        public float Vary;

        public SfxInfo(float level, byte maxVoices, float minGap, byte priority, float vary)
        { Level = level; MaxVoices = maxVoices; MinGap = minGap; Priority = priority; Vary = vary; }
    }

    /// <summary>
    /// Every sound's recipe. Ideas and many numbers come from the old game's synth (ABC2 Sound.cs). Phone speakers play
    /// almost nothing under about 200 Hz, so every low thump also has a layer in the 200-600 Hz range that carries it.
    /// </summary>
    static class SfxRecipes
    {
        public const int Count = (int)SfxId.BramBolt + 1; // the last SfxId: raise it with every new one

        // Priority: 1 = constant small stuff, 2 = attacks and hits, 3 = specials and deaths, 4 = feedback for the player, 5 = match moments.
        public static SfxInfo Info(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiTap: return new SfxInfo(0.55f, 2, 0.04f, 5, 0f);
                case SfxId.UiBack: return new SfxInfo(0.55f, 2, 0.04f, 5, 0f);
                case SfxId.UiConfirm: return new SfxInfo(0.7f, 1, 0.08f, 5, 0f);
                case SfxId.UiToggle: return new SfxInfo(0.55f, 2, 0.04f, 5, 0f);
                case SfxId.Countdown: return new SfxInfo(0.75f, 1, 0.3f, 5, 0f);
                case SfxId.CountdownGo: return new SfxInfo(0.9f, 1, 0.5f, 5, 0f);
                case SfxId.Victory: return new SfxInfo(0.9f, 1, 1f, 5, 0f);
                case SfxId.Defeat: return new SfxInfo(0.85f, 1, 1f, 5, 0f);

                case SfxId.AttackBram: case SfxId.AttackFenn: case SfxId.AttackTyra: case SfxId.AttackMoa:
                case SfxId.AttackKnut: case SfxId.AttackBlast:
                    return new SfxInfo(0.62f, 3, 0.07f, 2, 0.06f);
                // Tikta attacks fastest and her "tsip" is one bright tone: two at a time, a wider gap and more pitch spread so a
                // fight with two Tiktas does not turn into a drill (tester feedback 2026-11-03; her volume is set in MatchViews)
                case SfxId.AttackTikta: return new SfxInfo(0.62f, 2, 0.12f, 2, 0.1f);

                case SfxId.BramHammerThrow: return new SfxInfo(0.85f, 2, 0.2f, 3, 0.03f);
                case SfxId.BramHammerBounce: return new SfxInfo(0.85f, 3, 0.08f, 3, 0.05f);
                case SfxId.TyraDraw: return new SfxInfo(0.7f, 2, 0.2f, 3, 0f);
                case SfxId.TyraRelease: return new SfxInfo(0.9f, 2, 0.15f, 3, 0.03f);
                case SfxId.MoaShield: return new SfxInfo(0.8f, 2, 0.15f, 3, 0.03f);
                case SfxId.MoaField: return new SfxInfo(0.85f, 2, 0.3f, 3, 0f);
                case SfxId.KnutAnchorThrow: return new SfxInfo(0.85f, 2, 0.2f, 3, 0.03f);
                case SfxId.KnutAnchorHook: return new SfxInfo(0.95f, 2, 0.15f, 3, 0.03f);
                case SfxId.TiktaNeedles: return new SfxInfo(0.75f, 2, 0.2f, 3, 0.03f);
                case SfxId.BlastBombThrow: return new SfxInfo(0.75f, 2, 0.2f, 3, 0.03f);
                case SfxId.BlastExplosion: return new SfxInfo(1f, 3, 0.1f, 3, 0.05f);
                case SfxId.BlastFireLane: return new SfxInfo(0.85f, 2, 0.25f, 3, 0.03f);

                case SfxId.HitLight: return new SfxInfo(0.6f, 3, 0.06f, 2, 0.07f);
                case SfxId.HitHeavy: return new SfxInfo(0.75f, 3, 0.08f, 2, 0.06f);
                case SfxId.ShieldBlock: return new SfxInfo(0.7f, 2, 0.1f, 3, 0.05f);
                case SfxId.Stunned: return new SfxInfo(0.65f, 2, 0.3f, 3, 0.04f);
                case SfxId.CloneAppear: return new SfxInfo(0.7f, 1, 0.3f, 3, 0f);
                case SfxId.CloneVanish: return new SfxInfo(0.6f, 1, 0.3f, 3, 0f);

                // 20 minion hits in a frame: one or two are heard, the rest are dropped
                case SfxId.MinionMelee: return new SfxInfo(0.35f, 2, 0.09f, 1, 0.1f);
                case SfxId.MinionRanged: return new SfxInfo(0.35f, 2, 0.09f, 1, 0.1f);
                case SfxId.SiegeShot: return new SfxInfo(0.55f, 2, 0.15f, 1, 0.06f);
                case SfxId.TowerShot: return new SfxInfo(0.75f, 2, 0.2f, 3, 0.03f);
                case SfxId.TowerDestroyed: return new SfxInfo(1f, 1, 1f, 5, 0f);

                case SfxId.HeroDeath: return new SfxInfo(0.8f, 2, 0.2f, 3, 0.02f);
                case SfxId.AllyDeath: return new SfxInfo(0.8f, 1, 0.3f, 4, 0f);
                case SfxId.MinionDeath: return new SfxInfo(0.3f, 2, 0.12f, 1, 0.1f);
                case SfxId.Respawn: return new SfxInfo(0.75f, 1, 0.5f, 4, 0f);
                case SfxId.LevelUp: return new SfxInfo(0.75f, 1, 0.5f, 4, 0f);
                case SfxId.WaveSpawn: return new SfxInfo(0.6f, 1, 1f, 4, 0f);
                case SfxId.GrassEnter: return new SfxInfo(0.45f, 1, 0.3f, 2, 0.08f);
                case SfxId.SpecialReady: return new SfxInfo(0.7f, 1, 0.5f, 4, 0f);
                case SfxId.LowHealth: return new SfxInfo(0.85f, 1, 1.5f, 4, 0f);
                case SfxId.BlastChainBoom: return new SfxInfo(0.95f, 2, 0.12f, 3, 0.04f); // wins a voice like Supernova's boom
                case SfxId.AttackBonk: return new SfxInfo(0.7f, 3, 0.07f, 2, 0.05f);
                // Kugg (Hero7.cs, O-066..O-070; full review 2026-11-09 nr 83): his shot as the other attacks, the anchored shell a little
                // heavier, the Spark Shot (his passive, by itself) softer; the Anchor going in and the spikes coming up as the specials
                case SfxId.AttackKugg: return new SfxInfo(0.7f, 3, 0.07f, 2, 0.05f);
                case SfxId.KuggShotBig: return new SfxInfo(0.75f, 3, 0.08f, 2, 0.05f);
                case SfxId.KuggSpark: return new SfxInfo(0.65f, 3, 0.07f, 2, 0.06f);
                case SfxId.FennVeil: return new SfxInfo(0.8f, 2, 0.15f, 2, 0.04f);
                case SfxId.VeilStrike: return new SfxInfo(0.9f, 2, 0.1f, 3, 0.04f);
                case SfxId.BramCharge: return new SfxInfo(0.8f, 2, 0.15f, 2, 0.04f);
                case SfxId.BramBolt: return new SfxInfo(0.9f, 2, 0.15f, 3, 0.04f);
                case SfxId.KuggDeploy: return new SfxInfo(0.8f, 2, 0.2f, 3, 0.03f);
                case SfxId.KuggPackUp: return new SfxInfo(0.7f, 2, 0.2f, 3, 0.02f);
                case SfxId.BonkSlamWindup: return new SfxInfo(0.8f, 2, 0.2f, 3, 0.03f);
                case SfxId.BonkSlam: return new SfxInfo(1f, 2, 0.12f, 3, 0.04f);
                case SfxId.BonkBoulderThrow: return new SfxInfo(0.85f, 2, 0.2f, 3, 0.03f);
                case SfxId.BonkKnock: return new SfxInfo(0.8f, 2, 0.1f, 3, 0.05f);
                case SfxId.BonkStun: return new SfxInfo(0.75f, 1, 0.5f, 3, 0f); // one at a time, however many he stuns
                // gaps as Speltest's Sound.cs GAP: a volley through four minions ticks once, the tower alarm at most every 8 s
                case SfxId.HitConfirmLight: return new SfxInfo(0.6f, 2, 0.08f, 4, 0.06f);
                case SfxId.HitConfirmHeavy: return new SfxInfo(0.7f, 2, 0.1f, 4, 0.06f);
                case SfxId.LastHit: return new SfxInfo(0.55f, 2, 0.08f, 2, 0.04f);
                case SfxId.MultiKill: return new SfxInfo(0.8f, 1, 0.5f, 5, 0f);
                case SfxId.TowerHot: return new SfxInfo(0.8f, 1, 0.6f, 4, 0.03f);
                case SfxId.TowerUnderAttack: return new SfxInfo(0.65f, 1, 8f, 4, 0f);
                case SfxId.BramAnvil: return new SfxInfo(0.9f, 2, 0.15f, 3, 0.04f);
                case SfxId.MoaHoney: return new SfxInfo(0.7f, 2, 0.15f, 3, 0.03f);
                case SfxId.SpellBlink: case SfxId.SpellRecall: case SfxId.SpellShield: case SfxId.SpellHaste: case SfxId.SpellSprint: return new SfxInfo(0.75f, 2, 0.2f, 3, 0.02f);
                case SfxId.Emote: return new SfxInfo(0.55f, 2, 0.5f, 2, 0f);
                case SfxId.AttackHilde: return new SfxInfo(0.7f, 3, 0.07f, 2, 0.05f);
                case SfxId.HildeKegThrow: return new SfxInfo(0.85f, 2, 0.2f, 3, 0.03f);
                case SfxId.HildeWallCrash: return new SfxInfo(0.9f, 1, 0.3f, 3, 0.03f); // one keg stuns one hero at most
                case SfxId.KnutSlam: return new SfxInfo(0.9f, 2, 0.15f, 3, 0.04f);
                case SfxId.HildeKegBlast: return new SfxInfo(0.9f, 2, 0.2f, 3, 0.04f);
                case SfxId.HildeKnock: return new SfxInfo(0.8f, 2, 0.1f, 2, 0.03f);
                case SfxId.KnutSlamWindup: return new SfxInfo(0.8f, 2, 0.2f, 3, 0.03f);
                case SfxId.TowerImpact: return new SfxInfo(0.9f, 1, 0.3f, 4, 0.03f); // feedback for the player: the tower hurts you
                case SfxId.KnutChain: return new SfxInfo(0.7f, 2, 0.15f, 2, 0.04f);
                case SfxId.KnutHookLand: return new SfxInfo(0.85f, 1, 0.2f, 3, 0.03f);
                case SfxId.TyraBolt: return new SfxInfo(0.85f, 2, 0.2f, 3, 0.02f);
                case SfxId.SpecialWarn: return new SfxInfo(0.8f, 3, 0.05f, 4, 0.03f); // a warning: it must be heard over the fight
                default: return new SfxInfo(0.6f, 1, 0.05f, 1, 0f);
            }
        }

        static readonly float[] ConfirmNotes = { 880f, 1318.5f };
        static readonly float[] VictoryArp = { 523.3f, 659.3f, 784f };
        static readonly float[] DefeatSteps = { 392f, 370f, 349.2f };
        static readonly float[] LevelArp = { 523.3f, 659.3f, 784f, 1046.5f };
        static readonly float[] ReadyNotes = { 880f, 1318.5f };

        /// <summary>Renders one sound into the synth (after Begin, before Finish).</summary>
        public static void Build(SfxSynth s, SfxId id)
        {
            switch (id)
            {
                // ---------- UI ----------
                case SfxId.UiTap: // a soft round tick
                    s.Tone(Wave.Sine, 1350, 1100, 0.04f, 0.6f, 0, 0.001f);
                    s.Tone(Wave.Tri, 2700, 2200, 0.018f, 0.12f, 0, 0.001f);
                    break;
                case SfxId.UiBack: // the same tick, falling
                    s.Tone(Wave.Tri, 760, 430, 0.08f, 0.5f, 0, 0.002f);
                    s.Tone(Wave.Sine, 380, 250, 0.07f, 0.3f, 0, 0.002f);
                    break;
                case SfxId.UiConfirm: // two bright rising notes
                    s.Notes(Wave.Tri, ConfirmNotes, 0.065f, 0.14f, 0.45f);
                    s.Tone(Wave.Sine, 1760, 1760, 0.16f, 0.12f, 0.065f, 0.004f, 0.04f);
                    break;
                case SfxId.UiToggle: // click-clack
                    s.Tone(Wave.Tri, 950, 900, 0.028f, 0.5f, 0, 0.001f);
                    s.Noise(Filt.Band, 3000, 3000, 2f, 0.012f, 0.25f, 0, 0.0005f);
                    s.Tone(Wave.Tri, 1420, 1350, 0.03f, 0.45f, 0.04f, 0.001f);
                    break;
                case SfxId.Countdown: // a clean beep
                    s.Tone(Wave.Sine, 880, 880, 0.14f, 0.6f, 0, 0.003f, 0.05f);
                    s.Tone(Wave.Tri, 1760, 1760, 0.08f, 0.1f, 0, 0.003f);
                    break;
                case SfxId.CountdownGo: // a bright chord stab, an octave above the beep
                    s.Tone(Wave.Square, 523.3f, 523.3f, 0.6f, 0.08f, 0, 0.005f, 0.15f);
                    s.Tone(Wave.Square, 784f, 784f, 0.6f, 0.07f, 0, 0.005f, 0.15f);
                    s.Tone(Wave.Tri, 1046.5f, 1046.5f, 0.6f, 0.35f, 0, 0.005f, 0.15f);
                    s.Tone(Wave.Sine, 2093f, 2093f, 0.3f, 0.08f, 0.01f, 0.005f);
                    break;
                case SfxId.Victory: // a rising fanfare that lands on a big bright chord with sparkles
                    s.Notes(Wave.Tri, VictoryArp, 0.12f, 0.28f, 0.4f);
                    s.Tone(Wave.Tri, 1046.5f, 1046.5f, 1f, 0.35f, 0.38f, 0.008f, 0.35f, 5f, 0.004f);
                    s.Tone(Wave.Sine, 659.3f, 659.3f, 1f, 0.22f, 0.38f, 0.008f, 0.35f);
                    s.Tone(Wave.Sine, 784f, 784f, 1f, 0.2f, 0.38f, 0.008f, 0.35f);
                    s.Tone(Wave.Square, 523.3f, 523.3f, 0.9f, 0.05f, 0.38f, 0.008f, 0.3f);
                    for (int i = 0; i < 6; i++) s.Tone(Wave.Sine, 2093f * s.Range(1f, 1.6f), 3000f, 0.08f, 0.06f, 0.42f + i * 0.09f, 0.002f);
                    break;
                case SfxId.Defeat: // three falling steps and a low sigh
                    s.Notes(Wave.Tri, DefeatSteps, 0.22f, 0.32f, 0.4f);
                    s.Tone(Wave.Tri, 262f, 196f, 1f, 0.35f, 0.66f, 0.01f, 0.2f, 4f, 0.006f);
                    s.Tone(Wave.Sine, 524f, 392f, 0.8f, 0.12f, 0.66f, 0.01f, 0.1f);
                    break;

                // ---------- auto attacks ----------
                case SfxId.AttackBram: // a thick short pop with a small iron ping (his hammer-gun)
                    s.Noise(Filt.Low, 3200, 450, 0.8f, 0.09f, 0.75f, 0, 0.001f);
                    s.Tone(Wave.Square, 210, 90, 0.07f, 0.2f, 0, 0.001f);
                    s.Tone(Wave.Tri, 330, 160, 0.06f, 0.35f, 0, 0.001f);
                    s.Fm(1250, 1180, 1.41f, 1.2f, 0.07f, 0.1f, 0.005f);
                    break;
                case SfxId.AttackFenn: // a double claw rake: two quick light swishes
                    s.Noise(Filt.Band, 900, 3200, 1.6f, 0.08f, 0.8f, 0, 0.012f);
                    s.Noise(Filt.Band, 1100, 3600, 1.6f, 0.07f, 0.6f, 0.05f, 0.01f);
                    s.Tone(Wave.Tri, 300, 170, 0.05f, 0.18f, 0, 0.002f);
                    break;
                case SfxId.AttackTyra: // bowstring twang and the hiss of the arrow
                    s.Fm(420, 300, 2f, 2.5f, 0.12f, 0.45f, 0, 0.001f);
                    s.Tone(Wave.Tri, 430, 190, 0.09f, 0.35f, 0, 0.001f);
                    s.Noise(Filt.Band, 3600, 1500, 2f, 0.13f, 0.3f, 0.01f, 0.01f);
                    break;
                case SfxId.AttackMoa: // a soft magic "pew" with a tiny bee buzz
                    s.Tone(Wave.Sine, 1250, 430, 0.15f, 0.45f, 0, 0.002f);
                    s.Tone(Wave.Tri, 1880, 650, 0.11f, 0.12f, 0.01f, 0.002f);
                    s.Tone(Wave.Saw, 230, 210, 0.12f, 0.08f, 0, 0.01f, 0, 0, 0, 28f, 0.8f);
                    break;
                case SfxId.AttackKnut: // a heavy, low swing
                    s.Noise(Filt.Band, 380, 1300, 1.3f, 0.17f, 0.8f, 0, 0.04f);
                    s.Tone(Wave.Tri, 320, 150, 0.12f, 0.4f, 0.04f, 0.003f);
                    s.Tone(Wave.Sine, 160, 90, 0.1f, 0.25f, 0.04f, 0.003f);
                    s.Noise(Filt.Low, 1200, 300, 1f, 0.06f, 0.35f, 0.04f, 0.001f);
                    break;
                case SfxId.AttackTikta: // a tiny sharp "tsip"
                    s.Tone(Wave.Tri, 2600, 1500, 0.05f, 0.4f, 0, 0.001f);
                    s.Noise(Filt.Band, 5200, 3400, 2f, 0.05f, 0.35f, 0, 0.001f);
                    s.Tone(Wave.Sine, 720, 520, 0.035f, 0.15f, 0, 0.001f);
                    break;
                case SfxId.AttackBlast: // a cartoon "fwoop" lob with a little crackle
                    s.Tone(Wave.Sine, 290, 720, 0.12f, 0.5f, 0, 0.004f);
                    s.Tone(Wave.Tri, 720, 360, 0.09f, 0.18f, 0.1f, 0.003f);
                    s.Noise(Filt.Band, 900, 2400, 1.2f, 0.12f, 0.25f, 0, 0.02f);
                    s.Crackle(0.15f, 60, 1800, 4200, 0.35f, 0.02f);
                    break;

                // ---------- specials ----------
                case SfxId.BramHammerThrow: // a spinning whoosh: whum-whum-whum
                    s.Noise(Filt.Band, 380, 1700, 1.4f, 0.4f, 0.9f, 0, 0.04f, 0.1f, 16f, 0.75f);
                    s.Tone(Wave.Tri, 190, 260, 0.38f, 0.22f, 0, 0.04f, 0.1f, 0, 0, 16f, 0.75f);
                    break;
                case SfxId.BramHammerBounce: // a metal clank
                    s.Tone(Wave.Tri, 280, 130, 0.2f, 0.45f, 0, 0.001f);
                    s.Fm(820, 800, 1.41f, 3f, 0.28f, 0.28f, 0, 0.001f);
                    s.Fm(1500, 1450, 2.76f, 1.5f, 0.18f, 0.12f, 0.002f, 0.001f);
                    s.Noise(Filt.Band, 2400, 900, 1.2f, 0.05f, 0.45f, 0, 0.0005f);
                    break;
                case SfxId.TyraDraw: // the bowstring stretching: a rising creak
                    s.Tone(Wave.Tri, 170, 420, 0.38f, 0.3f, 0, 0.12f, 0.15f, 0, 0, 35f, 0.4f);
                    s.Noise(Filt.Band, 1200, 2600, 3f, 0.38f, 0.2f, 0, 0.15f, 0.1f, 35f, 0.6f);
                    break;
                case SfxId.TyraRelease: // a strong deep twang and a heavy arrow swoosh
                    s.Fm(300, 210, 2f, 3.5f, 0.18f, 0.5f, 0, 0.001f);
                    s.Tone(Wave.Tri, 540, 150, 0.15f, 0.45f, 0, 0.001f);
                    s.Noise(Filt.Band, 4200, 800, 1.6f, 0.26f, 0.55f, 0.01f, 0.01f);
                    s.Noise(Filt.Low, 2500, 400, 0.8f, 0.06f, 0.4f, 0, 0.001f);
                    break;
                case SfxId.MoaShield: // a warm rising "bloop" with honey bubbles and a shimmer
                    s.Tone(Wave.Sine, 620, 990, 0.14f, 0.5f, 0, 0.006f);
                    s.Tone(Wave.Tri, 990, 1320, 0.2f, 0.25f, 0.06f, 0.006f, 0.05f);
                    s.Tone(Wave.Sine, 1980, 1980, 0.25f, 0.06f, 0.1f, 0.02f, 0.05f, 7f, 0.01f);
                    s.Bubbles(5, 0.14f, 650, 1300, 0.25f, 0.03f);
                    break;
                case SfxId.MoaField: // a warm swelling chord with lots of bubbles
                    s.Tone(Wave.Sine, 523.3f, 523.3f, 0.6f, 0.3f, 0, 0.1f, 0.2f, 5f, 0.006f);
                    s.Tone(Wave.Sine, 659.3f, 659.3f, 0.6f, 0.25f, 0, 0.1f, 0.2f, 5.3f, 0.006f);
                    s.Tone(Wave.Tri, 784f, 784f, 0.6f, 0.2f, 0.02f, 0.1f, 0.2f, 4.7f, 0.006f);
                    s.Noise(Filt.Band, 600, 1400, 1.2f, 0.5f, 0.12f, 0, 0.12f);
                    s.Bubbles(14, 0.45f, 500, 1300, 0.22f, 0.04f);
                    break;
                case SfxId.KnutAnchorThrow: // a heavy whoosh with a rattling chain
                    s.Noise(Filt.Band, 300, 1200, 1.3f, 0.32f, 0.85f, 0, 0.03f);
                    s.Tone(Wave.Tri, 160, 230, 0.3f, 0.2f, 0, 0.04f);
                    s.Clinks(10, 0.26f, 1800, 3000, 0.2f, 0.02f);
                    break;
                case SfxId.KnutAnchorHook: // a metal clink, then a heavy wet thud as the hero is caught
                    s.Fm(2100, 2050, 1.41f, 1.5f, 0.08f, 0.25f, 0, 0.001f);
                    s.Fm(3150, 3100, 1.41f, 1.2f, 0.07f, 0.18f, 0.03f, 0.001f);
                    s.Tone(Wave.Tri, 330, 140, 0.18f, 0.5f, 0.05f, 0.002f);
                    s.Tone(Wave.Sine, 150, 70, 0.2f, 0.3f, 0.05f, 0.002f);
                    s.Noise(Filt.Low, 900, 200, 1.2f, 0.2f, 0.55f, 0.05f, 0.004f);
                    break;
                case SfxId.TiktaNeedles: // four quick rising "tsip"s over a wing buzz
                    for (int i = 0; i < 4; i++)
                    {
                        float k = 1f + i * 0.07f, t = i * 0.055f;
                        s.Tone(Wave.Tri, 2500 * k, 1450 * k, 0.05f, 0.38f, t, 0.001f);
                        s.Noise(Filt.Band, 5000, 3400, 2.2f, 0.045f, 0.3f, t, 0.001f);
                    }
                    s.Tone(Wave.Square, 150, 160, 0.26f, 0.06f, 0, 0.02f, 0.1f, 0, 0, 40f, 0.6f);
                    break;
                case SfxId.BlastBombThrow: // a lob "whoop" and a fizzing fuse
                    s.Tone(Wave.Sine, 250, 620, 0.16f, 0.45f, 0, 0.005f);
                    s.Tone(Wave.Tri, 500, 1240, 0.12f, 0.1f, 0, 0.005f);
                    s.Noise(Filt.Band, 3200, 4500, 3f, 0.4f, 0.3f, 0.03f, 0.03f, 0.2f, 23f, 0.5f);
                    s.Crackle(0.38f, 45, 2500, 5000, 0.35f, 0.04f);
                    break;
                case SfxId.BlastExplosion: // a big round boom with a crackling tail
                    s.Noise(Filt.Low, 2400, 110, 0.8f, 0.6f, 1f, 0, 0.003f);
                    s.Tone(Wave.Tri, 300, 90, 0.32f, 0.55f, 0, 0.002f);
                    s.Tone(Wave.Sine, 130, 45, 0.45f, 0.35f, 0, 0.002f);
                    s.Noise(Filt.Band, 1500, 600, 1f, 0.05f, 0.45f, 0, 0.0005f);
                    s.Crackle(0.5f, 50, 1200, 3500, 0.3f, 0.05f);
                    break;
                case SfxId.BlastFireLane: // a "fwoosh" of fire catching, then crackling flames
                    s.Noise(Filt.Low, 350, 2600, 0.9f, 0.22f, 0.7f, 0, 0.06f);
                    s.Noise(Filt.Band, 2200, 700, 1f, 0.45f, 0.5f, 0.15f, 0.02f, 0.05f, 9f, 0.4f);
                    s.Tone(Wave.Tri, 150, 230, 0.35f, 0.25f, 0, 0.08f);
                    s.Crackle(0.55f, 70, 1500, 4500, 0.4f, 0.08f);
                    break;

                // ---------- combat ----------
                case SfxId.HitLight: // a bright tick (the old game's hitLight)
                    s.Tone(Wave.Tri, 1250, 820, 0.05f, 0.4f, 0, 0.001f);
                    s.Noise(Filt.Band, 4200, 2200, 1.5f, 0.045f, 0.4f, 0, 0.0005f);
                    s.Tone(Wave.Sine, 300, 160, 0.045f, 0.35f, 0, 0.001f);
                    break;
                case SfxId.HitHeavy: // a dull punchy thud
                    s.Tone(Wave.Tri, 360, 150, 0.09f, 0.6f, 0, 0.001f);
                    s.Tone(Wave.Sine, 150, 70, 0.1f, 0.3f, 0, 0.001f);
                    s.Noise(Filt.Low, 1900, 250, 0.8f, 0.07f, 0.6f, 0, 0.0005f);
                    s.Tone(Wave.Tri, 520, 420, 0.03f, 0.1f, 0.005f, 0.001f);
                    break;
                case SfxId.ShieldBlock: // a glassy "tink" as the shield takes the blow
                    s.Fm(980, 970, 2.76f, 1.8f, 0.22f, 0.35f, 0, 0.001f);
                    s.Tone(Wave.Sine, 1960, 1940, 0.18f, 0.15f, 0, 0.001f);
                    s.Noise(Filt.Band, 2600, 1800, 2f, 0.04f, 0.4f, 0, 0.0005f);
                    s.Tone(Wave.Tri, 280, 180, 0.05f, 0.25f, 0, 0.001f);
                    break;
                case SfxId.Stunned: // cartoon "seeing stars": three quick bird tweets
                    for (int i = 0; i < 3; i++)
                    {
                        float t = i * 0.13f, f = 2000 + i * 180;
                        s.Tone(Wave.Sine, f, f * 1.35f, 0.07f, 0.4f, t, 0.004f, 0, 30f, 0.03f);
                        s.Tone(Wave.Sine, f * 1.3f, f, 0.05f, 0.25f, t + 0.06f, 0.004f);
                    }
                    s.Tone(Wave.Tri, 620, 520, 0.08f, 0.2f, 0, 0.001f);
                    break;
                case SfxId.CloneAppear: // a shimmering whoosh upwards
                    s.Noise(Filt.Band, 700, 4000, 3f, 0.28f, 0.4f, 0, 0.05f);
                    s.Tone(Wave.Sine, 380, 1500, 0.28f, 0.35f, 0, 0.03f);
                    s.Tone(Wave.Tri, 760, 3000, 0.22f, 0.08f, 0.03f, 0.03f);
                    s.Tone(Wave.Sine, 1500, 1500, 0.12f, 0.1f, 0.25f, 0.005f, 0, 9f, 0.01f);
                    break;
                case SfxId.CloneVanish: // a falling shimmer and a soft "pff"
                    s.Noise(Filt.Band, 3500, 700, 3f, 0.25f, 0.35f, 0, 0.01f);
                    s.Tone(Wave.Sine, 1400, 380, 0.25f, 0.3f, 0, 0.005f);
                    s.Noise(Filt.Low, 1500, 300, 0.8f, 0.1f, 0.35f, 0.18f, 0.005f);
                    break;

                // ---------- minions and towers ----------
                case SfxId.MinionMelee: // a small dry thwack
                    s.Noise(Filt.Band, 1300, 800, 1.2f, 0.045f, 0.6f, 0, 0.0005f);
                    s.Tone(Wave.Tri, 320, 180, 0.05f, 0.35f, 0, 0.001f);
                    break;
                case SfxId.MinionRanged: // a small "pew"
                    s.Tone(Wave.Tri, 950, 480, 0.07f, 0.4f, 0, 0.001f);
                    s.Noise(Filt.Band, 3000, 1600, 1.5f, 0.04f, 0.15f, 0, 0.001f);
                    break;
                case SfxId.SiegeShot: // a little cannon: a thump and a puff
                    s.Tone(Wave.Tri, 340, 120, 0.14f, 0.5f, 0, 0.001f);
                    s.Tone(Wave.Sine, 140, 60, 0.16f, 0.3f, 0, 0.001f);
                    s.Noise(Filt.Low, 1800, 250, 0.8f, 0.12f, 0.6f, 0, 0.001f);
                    break;
                case SfxId.TowerShot: // a heavy zap-thump: the tower warns you it hurts
                    s.Tone(Wave.Saw, 150, 60, 0.22f, 0.25f, 0, 0.001f);
                    s.Tone(Wave.Square, 640, 280, 0.12f, 0.08f, 0, 0.001f);
                    s.Tone(Wave.Tri, 300, 120, 0.18f, 0.35f, 0, 0.001f);
                    s.Noise(Filt.Low, 1700, 220, 0.8f, 0.18f, 0.55f, 0, 0.001f);
                    break;
                case SfxId.TowerDestroyed: // a long crumbling crash with falling rubble
                    s.Noise(Filt.Low, 2000, 80, 0.8f, 1.2f, 0.9f, 0, 0.005f);
                    s.Tone(Wave.Tri, 280, 80, 0.7f, 0.55f, 0, 0.003f);
                    s.Tone(Wave.Sine, 120, 40, 0.9f, 0.35f, 0, 0.003f);
                    s.Noise(Filt.Band, 2600, 900, 1f, 0.08f, 0.5f, 0, 0.001f);
                    s.Crackle(0.9f, 40, 600, 2200, 0.5f, 0.12f);
                    for (int i = 0; i < 4; i++) s.Tone(Wave.Tri, s.Range(180, 280), 90, 0.12f, 0.3f, 0.2f + i * s.Range(0.12f, 0.2f), 0.002f);
                    break;

                // ---------- events ----------
                case SfxId.HeroDeath: // a falling "pyoww" (the old game's heroDeath)
                    s.Tone(Wave.Tri, 540, 130, 0.5f, 0.45f, 0, 0.005f);
                    s.Tone(Wave.Sine, 400, 100, 0.5f, 0.3f, 0.05f, 0.005f);
                    s.Noise(Filt.Low, 1400, 200, 0.8f, 0.12f, 0.3f, 0, 0.002f);
                    break;
                case SfxId.AllyDeath: // two sad falling notes: one of yours is down
                    s.Tone(Wave.Tri, 659.3f, 659.3f, 0.22f, 0.4f, 0, 0.006f, 0.06f);
                    s.Tone(Wave.Tri, 493.9f, 440f, 0.5f, 0.4f, 0.2f, 0.006f, 0.1f, 5f, 0.008f);
                    s.Tone(Wave.Sine, 247f, 220f, 0.5f, 0.25f, 0.2f, 0.01f, 0.1f);
                    break;
                case SfxId.MinionDeath: // a tiny poof
                    s.Noise(Filt.Band, 1500, 600, 1.2f, 0.09f, 0.6f, 0, 0.003f);
                    s.Tone(Wave.Tri, 520, 250, 0.08f, 0.25f, 0, 0.002f);
                    break;
                case SfxId.Respawn: // a rising shimmer: you are back
                    s.Tone(Wave.Tri, 300, 900, 0.38f, 0.35f, 0, 0.05f);
                    s.Tone(Wave.Sine, 600, 1800, 0.38f, 0.12f, 0.02f, 0.05f);
                    s.Noise(Filt.Band, 800, 3000, 2f, 0.32f, 0.25f, 0, 0.05f);
                    s.Tone(Wave.Sine, 1800, 1800, 0.25f, 0.1f, 0.35f, 0.005f, 0, 7f, 0.01f);
                    break;
                case SfxId.LevelUp: // a quick climbing arpeggio and a sparkle (the old game's levelUp)
                    s.Notes(Wave.Square, LevelArp, 0.06f, 0.2f, 0.12f);
                    s.Notes(Wave.Tri, LevelArp, 0.06f, 0.2f, 0.3f);
                    s.Tone(Wave.Sine, 1046.5f, 2093f, 0.25f, 0.15f, 0.24f, 0.01f);
                    break;
                case SfxId.WaveSpawn: // a short horn call: "dooo-doo"
                    s.Tone(Wave.Saw, 293.7f, 293.7f, 0.28f, 0.12f, 0, 0.04f, 0.12f, 5f, 0.004f);
                    s.Tone(Wave.Tri, 293.7f, 293.7f, 0.28f, 0.4f, 0, 0.04f, 0.12f, 5f, 0.004f);
                    s.Tone(Wave.Saw, 392f, 392f, 0.42f, 0.12f, 0.24f, 0.04f, 0.18f, 5f, 0.004f);
                    s.Tone(Wave.Tri, 392f, 392f, 0.42f, 0.4f, 0.24f, 0.04f, 0.18f, 5f, 0.004f);
                    break;
                case SfxId.GrassEnter: // a soft leafy rustle
                    s.Noise(Filt.Band, 2400, 3200, 1.5f, 0.22f, 0.25f, 0, 0.04f, 0, 22f, 0.6f);
                    s.Crackle(0.22f, 90, 2500, 5000, 0.4f);
                    break;
                case SfxId.SpecialReady: // two bright notes and a sparkle (the old game's ready)
                    s.Notes(Wave.Tri, ReadyNotes, 0.07f, 0.17f, 0.45f);
                    s.Tone(Wave.Sine, 2637f, 2637f, 0.18f, 0.08f, 0.09f, 0.003f, 0, 8f, 0.01f);
                    break;
                case SfxId.BlastChainBoom: // Chain Reaction's second blast (old "chainboom"): a sharp crack and a short bright ring, unlike Supernova's deep boom
                    s.Noise(Filt.Band, 5200, 1400, 1.2f, 0.16f, 0.6f, 0, 0.002f);
                    s.Tone(Wave.Square, 260, 120, 0.2f, 0.18f, 0, 0.003f);
                    s.Tone(Wave.Tri, 1320, 1180, 0.32f, 0.12f, 0.03f, 0.005f);
                    s.Tone(Wave.Sine, 1980, 1760, 0.26f, 0.07f, 0.05f, 0.005f);
                    break;
                case SfxId.AttackBonk: // a heavy stone club: a short low whoosh, then a dull wooden thud
                    s.Noise(Filt.Band, 300, 900, 1.2f, 0.12f, 0.55f, 0, 0.03f);
                    s.Tone(Wave.Sine, 150, 60, 0.14f, 0.55f, 0.06f, 0.002f);
                    s.Noise(Filt.Low, 1400, 250, 1f, 0.08f, 0.6f, 0.06f, 0.001f);
                    s.Tone(Wave.Tri, 260, 120, 0.08f, 0.25f, 0.06f, 0.002f);
                    break;
                case SfxId.BonkSlamWindup: // the club hauled up over his head: a rising heave of air
                    s.Noise(Filt.Band, 250, 1100, 1.1f, 0.35f, 0.5f, 0, 0.2f);
                    s.Tone(Wave.Tri, 90, 160, 0.35f, 0.2f, 0, 0.2f);
                    break;
                case SfxId.BonkSlam: // the club into the ground: a deep thump, a crack of stone and pebbles falling
                    s.Tone(Wave.Sine, 120, 38, 0.5f, 0.75f, 0, 0.002f);
                    s.Noise(Filt.Low, 2000, 90, 0.8f, 0.55f, 0.85f, 0, 0.002f);
                    s.Tone(Wave.Tri, 240, 70, 0.25f, 0.35f, 0, 0.002f);
                    s.Noise(Filt.Band, 2600, 900, 1.4f, 0.08f, 0.4f, 0.01f, 0.0005f);
                    s.Crackle(0.45f, 40, 900, 2800, 0.3f, 0.08f);
                    break;
                case SfxId.BonkBoulderThrow: // a grunt of effort and a heavy rolling rumble
                    s.Tone(Wave.Saw, 130, 95, 0.12f, 0.18f, 0, 0.01f);
                    s.Noise(Filt.Low, 260, 480, 1f, 0.45f, 0.55f, 0.08f, 0.05f, 0, 14f, 0.35f);
                    s.Tone(Wave.Sine, 70, 60, 0.4f, 0.3f, 0.08f, 0.05f, 0, 0, 0, 12f, 0.3f);
                    break;
                case SfxId.BonkKnock: // the boulder slams into a hero: a hard thud and a short scrape as they slide
                    s.Tone(Wave.Sine, 160, 55, 0.22f, 0.6f, 0, 0.001f);
                    s.Noise(Filt.Low, 1800, 200, 0.9f, 0.12f, 0.7f, 0, 0.001f);
                    s.Noise(Filt.Band, 1500, 600, 1.5f, 0.2f, 0.3f, 0.06f, 0.02f);
                    break;
                case SfxId.LowHealth: // two heartbeat thumps, pitched up so a phone speaker can play them
                    s.Tone(Wave.Tri, 300, 130, 0.12f, 0.55f, 0, 0.004f);
                    s.Tone(Wave.Sine, 150, 80, 0.12f, 0.3f, 0, 0.004f);
                    s.Noise(Filt.Low, 700, 250, 1f, 0.06f, 0.2f, 0, 0.002f);
                    s.Tone(Wave.Tri, 270, 115, 0.15f, 0.45f, 0.22f, 0.004f);
                    s.Tone(Wave.Sine, 135, 70, 0.15f, 0.25f, 0.22f, 0.004f);
                    s.Noise(Filt.Low, 650, 220, 1f, 0.07f, 0.16f, 0.22f, 0.002f);
                    break;
            }
        }

        /// <summary>The finished samples of one sound (mono, <see cref="SfxSynth.Rate"/> Hz).</summary>
        public static float[] Render(SfxSynth s, SfxId id)
        {
            s.Begin((int)id);
            Build(s, id);
            return s.Finish(Info(id).Level);
        }
    }
}
