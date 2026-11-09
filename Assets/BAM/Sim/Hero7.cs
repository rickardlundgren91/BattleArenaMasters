using System;

namespace BAM.Sim
{
    /// <summary>Kugg, the Rune Golem (hero 11, Rickard 2026-11-05: "Eget koncept men samma princip som roi17"; his pick "Ny hjälte
    /// (Kugg)"). The old game's ROI17 ("Joker", Resourceful Robo, abc2_gamedata.json) in BAM's own shape: a squat stone and brass
    /// golem whose right forearm is a rune cannon. Auto attack: a short cannon ball. Special, Anchor (Rotfäste, ROI's Remote
    /// Demolition Mode): he drives his heel spikes into the ground and becomes a cannon: he cannot walk, his reach grows by
    /// RangeBonus (to 8 tiles), he fires faster (CdMult) for ShotCoeff of his damage, and he sees into grass GrassSight tiles away.
    /// Every shot spends one of Shots; with none left (or after MaxTime, BAM's own guard so a player is never stuck in an empty
    /// lane) he pulls the spikes up for PackTime s and walks again. No charge is gathered while anchored. Passive, Spark Shot
    /// (ROI's Misfire): every SparkMin to SparkMax s the cannon fires by itself at a random enemy hero or minion within SparkRange
    /// with a clear line, for SparkCoeff of his damage (a visible shot, as every attack).</summary>
    public static class KuggS
    {
        /// <summary>Anchor: ROI's AttackRangeBuff 5.75 on a 2.25 range gave 8 tiles; Kugg's own range is 2.5, so +5.5.</summary>
        public const float RangeBonus = 5.5f, CdMult = 0.7f, ShotCoeff = 0.9f, GrassSight = 6, DeployTime = 0.6f, PackTime = 1.1f, MaxTime = 10;
        public const int Shots = 6;
        /// <summary>The Anchor can go from this many stocks (Rickard 2026-11-09: "kuggs ult ska man kunna använda så fort man har 2 skott");
        /// it fires as many shots as he has stocks, up to <see cref="Shots"/> (ROI17's stocks).</summary>
        public const int MinShots = 2;
        /// <summary>His anchored shot flies homing at this speed (tiles/s): 8 tiles in about 0.6 s, a readable lob.</summary>
        public const float ShellSpeed = 13;
        /// <summary>Spark Shot (ROI's JokerCastProjectileRandomlyScript: RandomDelay 7.5-15 s, Range 8, DamageCoeff 0.35).</summary>
        public const float SparkMin = 7.5f, SparkMax = 15, SparkRange = 8, SparkCoeff = 0.35f, SparkSpeed = 14;
        /// <summary>How far an enemy must be for his tap to deploy (the grey button otherwise): the anchored reach.</summary>
        public const float TapReach = 2.5f + RangeBonus;
    }

    public sealed partial class Unit
    {
        /// <summary>Kugg: shots left in his Anchor (above 0 = anchored and firing), when the Anchor ends at the latest, and when his
        /// pack-up ends (he stays rooted till then). KuggRooted: he cannot walk now.</summary>
        public int KuggShots; public float KuggUntil, KuggPackEnd = -1;
        public bool KuggRooted;
        /// <summary>Kugg: when his next Spark Shot goes.</summary>
        internal float KuggSparkAt = float.NaN;
        public bool KuggAnchored => KuggShots > 0;
    }

    public sealed partial class Match
    {
        /// <summary>The Anchor (tap or drag alike: it has no aim). Not while he is already anchored or packing up.</summary>
        bool CastKugg(Unit h)
        {
            if (h.KuggRooted) return false;
            h.KuggShots = Math.Max(KuggS.MinShots, Math.Min(KuggS.Shots, (int)h.Charge)); h.KuggUntil = Time + KuggS.MaxTime; h.KuggRooted = true; h.KuggPackEnd = -1;
            AddStatus(h, St.Casting, KuggS.DeployTime); AddStatus(h, St.Disarm, KuggS.DeployTime);
            h.AtkTimer = h.AttackCd; // the first anchored shot goes as soon as the spikes are in
            return true;
        }

        /// <summary>Every tick for a living Kugg: ends his Anchor on time and his pack-up, and fires his Spark Shot.</summary>
        void KuggTick(Unit h, float dt)
        {
            if (h.KuggAnchored && Time >= h.KuggUntil) KuggPack(h);
            if (h.KuggRooted && !h.KuggAnchored && h.KuggPackEnd >= 0 && Time >= h.KuggPackEnd) { h.KuggRooted = false; h.KuggPackEnd = -1; }
            if (float.IsNaN(h.KuggSparkAt)) h.KuggSparkAt = Time + Rng.Range(KuggS.SparkMin, KuggS.SparkMax);
            if (Time < h.KuggSparkAt) return;
            h.KuggSparkAt = Time + Rng.Range(KuggS.SparkMin, KuggS.SparkMax);
            if (h.Has(St.Stun) || State != MatchState.Play) return;
            var l = EnemiesInRange(h, KuggS.SparkRange, Pick.HeroOrMinion, true, spBuf);
            if (l.Count == 0) return;
            var t = PickRandom(l);
            var p = NewProj(ProjMode.Auto, h, h.X, h.Y);
            p.Kind = ProjKind.KuggSpark; p.Tgt = t; p.Dmg = h.StatDmg() * KuggS.SparkCoeff; p.T = 0; p.Speed = KuggS.SparkSpeed; p.Travel = 0.1f; p.OnHit = false;
            Emit(EventType.AttackStart, h, t, 0, (byte)ProjKind.KuggSpark, false);
        }

        /// <summary>A shot left his cannon: an anchored one spends a shot, and the last one starts the pack-up.</summary>
        void KuggFired(Unit h)
        {
            if (!h.KuggAnchored) return;
            h.KuggShots--;
            if (h.KuggShots <= 0) KuggPack(h);
        }

        /// <summary>An anchored Kugg whose ready shot found nobody in reach: the shell goes out anyway, straight ahead the full anchored
        /// reach, lands on the ground and hurts nobody; it spends its stock as it leaves the hand like any shell (UpdateAuto), so the
        /// Anchor always ends after his shots and he is never held in place by an empty lane (Rickard 2026-11-09).</summary>
        void KuggBlank(Unit h)
        {
            float reach = h.Range, a = h.Facing, delay = h.HDef.SpawnDelay;
            var p = NewProj(ProjMode.Auto, h, h.X, h.Y);
            p.Kind = ProjKind.KuggShell; p.Tgt = null; p.Fizzle = true; p.FX = h.X + SimMath.Cos(a) * reach; p.FY = h.Y + SimMath.Sin(a) * reach;
            p.Ang = a; p.D = reach; p.Dmg = 0; p.T = -delay; p.Speed = KuggS.ShellSpeed; p.Travel = 0.1f; p.Big = true; p.OnHit = false;
            h.AimT = Time + delay + 0.1f;
            Emit(EventType.AttackStart, h, null, delay, (byte)ProjKind.KuggShell, true);
        }

        void KuggPack(Unit h)
        {
            h.KuggShots = 0; h.KuggPackEnd = Time + KuggS.PackTime;
            AddStatus(h, St.Disarm, KuggS.PackTime);
            Emit(EventType.KuggPack, h, null, KuggS.PackTime, (byte)HeroId.Kugg, false, h.X, h.Y);
        }

        /// <summary>His death ends the Anchor at once.</summary>
        static void KuggReset(Unit h) { h.KuggShots = 0; h.KuggRooted = false; h.KuggPackEnd = -1; h.KuggSparkAt = float.NaN; }
    }
}
