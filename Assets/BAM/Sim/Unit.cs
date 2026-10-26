using System;

namespace BAM.Sim
{
    /// <summary>Kinds of status effects. Several of one kind may stand at once (shields from different casts, slows).</summary>
    public enum St : byte
    {
        Stun, Root, SlowFlat, SlowPct, SpeedFlat, SpeedPct, Disarm, Untargetable, Invulnerable,
        Casting,  // busy with a cast: no walking, no other cast
        Channel,  // a cast that a stun or root can still cut
        Shield,   // Amount absorbs damage first
        Burn,     // Blast's passive: Amount still to burn, V counts time to the next tick
        Poison,   // Tikta's needles: V per second, Amount gathered for the next tick
        AtkHaste, // V more attacks per second, as a share (0.4 = 40 % faster): the Haste battle spell
        TiktaMark, // Tikta 2.0 Deathmark: By = the Tikta whose next Needle Storm needle on this hero hits harder
    }

    public struct Status
    {
        public St Type;
        public float T, V, Amount;
        public Unit By;
        public bool Queen; // Moa's Queen's Instinct shield: it never blocks control
    }

    /// <summary>How Fenn's wolf is behaving.</summary>
    public enum WolfMode : byte { Follow, Fly, Free }

    /// <summary>Lane minion states (the old game's SimMinionAI).</summary>
    public enum MinionState : byte { Advance, Approach, Attack, Reposition, Return }

    /// <summary>
    /// A hero, Fenn's clone, a minion, Fenn's wolf or a tower. Coordinates are tiles: x to the right, y down the map (row 0 is the
    /// enemy's end). Everything is read by the game and the bots; only the rules (BAM.Sim) change it.
    /// </summary>
    public sealed class Unit
    {
        public const int MaxStatus = 24;

        // ---------- identity ----------
        /// <summary>Unique within a match, never reused: views and bots key on it.</summary>
        public int Serial;
        public UnitKind Kind;
        public int Team;
        public HeroId Hero;          // heroes and clones
        public MinionKind Minion;    // minions
        public HeroDef HDef;         // heroes and clones
        public UnitDef UDef;         // minions, wolf, tower
        /// <summary>0-5 for the match's real heroes (their index in Match.Heroes), -1 for every other unit.</summary>
        public int HeroIndex = -1;

        // ---------- body ----------
        public bool Alive = true;
        public float Hp = 1, MaxHp = 1, R = 0.25f, X, Y, Facing;
        /// <summary>Position at the start of the last tick, for drawing between ticks.</summary>
        public float PX, PY;
        /// <summary>Tiles walked in all (drives the walk animation) and when it last moved.</summary>
        public float WalkT, MovedAt = -99;
        /// <summary>Facing is held towards an attack or cast until this time (walking does not turn it).</summary>
        public float AimT = -1e9f;

        // ---------- statuses ----------
        internal readonly Status[] Sts = new Status[MaxStatus];
        internal int StCount;
        public int StatusCount => StCount;
        public Status GetStatus(int i) => Sts[i];

        // ---------- combat ----------
        public float AtkTimer, LastCombat = -99, LastHurt = -99, RevealT, RegenT;
        /// <summary>The unit the last auto attack went for and when (towers keep theirs).</summary>
        public Unit Target; public float TargetT = -99;
        public float DmgOverride = float.NaN;
        /// <summary>When each of the match's real heroes (by HeroIndex) last hurt this unit; a clone's hits count for Fenn.</summary>
        public readonly float[] LastHitBy = { -99, -99, -99, -99, -99, -99 };

        // ---------- heroes ----------
        public bool IsPlayer, IsClone, Dummy;
        /// <summary>The guided match makes the player's hero stronger: its damage times DmgMult, damage it takes times TakenMult.</summary>
        public float DmgMult = 1, TakenMult = 1;
        /// <summary>The hero a clone or wolf belongs to.</summary>
        public Unit Owner;
        public float LifeT;
        public int Level = 1, Pips, Deaths, Kills, Assists, Streak;
        public float Charge;
        public float RespawnT, DiedAt = -99, SpawnX, SpawnY;
        /// <summary>The bot playing this hero (null for the player and for tutorial dummies).</summary>
        public BotBrain Bot;
        // passives and specials
        internal int Needles; internal float NeedleT, NeedleEnd;   // Tikta's Needle Storm
        internal int TiktaCastId, TiktaBonusCast;                   // Tikta 2.0: her casts counted, the cast that already had its Deathmark bonus
        public float BlastChainReadyAt;                             // Blast 2.0: Chain Reaction is ready again at this time
        internal int PassHits;                                      // Bram: hits towards the next splash
        public float CloneReadyAt;                                  // Fenn: his passive is ready again at this time
        internal bool ClonePending;
        public float QueenReadyAt;                                  // Moa: Queen's Instinct is ready again at this time
        internal float AimAng = float.NaN, AimK;                    // the aim of the cast being made (NaN = a tap)
        // the battle spell (Spells.cs): which, when it is ready again, the recall channel (end -1 = none) and where it started
        public SpellId Spell; public float SpellAt, SpellChanEnd = -1, SpellChanStart; internal float SpellChanX, SpellChanY;
        // what the bots know: the smoothed motion (lead aim), where each team last saw it, when it last cast where they saw it
        public float MoVx, MoVy; internal float MoX, MoY; internal bool MoInit;
        public readonly float[] LastSeenX = new float[2], LastSeenY = new float[2], LastSeenT = { -99, -99 };
        public readonly float[] CastSeen = { float.NaN, float.NaN };

        // ---------- minions ----------
        public int Wp;
        internal float DeadT = float.NaN;
        internal bool TutStatic, TutUnkillable;
        internal MinionState MbState; internal Unit MbTgt;
        internal float MbScanT, MbLockUntil, MbAng, MbStuckT, MbSideT, MbSideSign, MbTowerSide, MbDx, MbDy, MbGx = float.NaN, MbGy, MbPx = float.NaN, MbPy;
        internal float AiT;
        internal readonly PathBuf Path = new PathBuf();

        // ---------- Fenn's wolf ----------
        public WolfMode Wolf;
        internal float WolfGx, WolfGy;
        /// <summary>The enemy hero a thrown wolf hunts.</summary>
        public Unit WolfPrey;

        // ---------- towers ----------
        internal int TowerStreak; internal Unit TowerFocus; internal float TowerShotT = -99;

        // ---------- reading ----------
        public bool IsHero => Kind == UnitKind.Hero;
        public bool IsRealHero => Kind == UnitKind.Hero && !IsClone;
        /// <summary>Minions and Fenn's wolf (a tower shoots these first, a hero at a tower needs one of these of its own).</summary>
        public bool IsMinionLike => Kind == UnitKind.Minion || Kind == UnitKind.Wolf;
        public float Range => HDef != null ? HDef.Range : UDef.Range;
        public float BaseSpeed => HDef != null ? HDef.Speed : UDef.Speed;
        public bool SpecialReady => HDef != null && Charge >= HDef.MaxCharge;
        public float Charge01 => HDef != null ? Math.Min(1, Charge / HDef.MaxCharge) : 0;
        public float DefDmg => float.IsNaN(DmgOverride) ? UDef.Dmg : DmgOverride;

        public bool Has(St t)
        {
            for (int i = 0; i < StCount; i++) if (Sts[i].Type == t) return true;
            return false;
        }
        /// <summary>Shield left on this unit (all shields together).</summary>
        public float ShieldAmount
        {
            get { float a = 0; for (int i = 0; i < StCount; i++) if (Sts[i].Type == St.Shield) a += Sts[i].Amount; return a; }
        }
        public bool InCombat(float now) => now - LastCombat < (Kind == UnitKind.Tower ? Rules.TowerOutOfCombat : Rules.HeroOutOfCombat);

        /// <summary>Statuses as display flags (the art shows stars, slows, shields...). Illusion is set for clones; the game
        /// shows it only to the clone's own team.</summary>
        public StatusFlags Flags
        {
            get
            {
                StatusFlags f = StatusFlags.None;
                for (int i = 0; i < StCount; i++)
                {
                    switch (Sts[i].Type)
                    {
                        case St.Stun: f |= StatusFlags.Stunned; break;
                        case St.Root: f |= StatusFlags.Rooted; break;
                        case St.SlowFlat: if (Sts[i].V < 0) f |= StatusFlags.Slowed; break;
                        case St.SlowPct: if (Sts[i].V < 0) f |= StatusFlags.Slowed; break;
                        case St.SpeedFlat: case St.SpeedPct: if (Sts[i].V > 0) f |= StatusFlags.Hasted; break;
                        case St.AtkHaste: f |= StatusFlags.Hasted; break;
                        case St.Shield: if (Sts[i].Amount > 0) f |= StatusFlags.Shielded; break;
                        case St.Burn: if (Sts[i].Amount > 0) f |= StatusFlags.Burning; break;
                        case St.Poison: f |= StatusFlags.Poisoned; break;
                        case St.TiktaMark: f |= StatusFlags.Marked; break;
                    }
                }
                if (RevealT > 0) f |= StatusFlags.Revealed;
                if (IsClone) f |= StatusFlags.Illusion;
                return f;
            }
        }

        // ---------- hero stats ----------
        internal int StarsFor(bool noStars) => noStars ? 0 : Math.Min(Defs.MaxStars, Math.Max(0, Pips));
        internal bool NoStars; // set by the match (the tutorial plays without stars)
        public float StatHp() { int s = StarsFor(NoStars); return (HDef.Hp + HDef.HpGrowth * (Level - 1)) * (1 + Defs.StarHp[s]); }
        public float StatDmg() { int s = StarsFor(NoStars); return (HDef.Dmg + HDef.DmgGrowth * (Level - 1)) * (1 + Defs.StarDmg[s]) * DmgMult; }
        public float StatCd() { int s = StarsFor(NoStars); return Math.Max(0.35f, HDef.Cd + HDef.CdGrowth * (Level - 1)) * (1 + Defs.StarCdr[s]); }
        public float AttackCd => HDef != null ? StatCd() / AtkHaste() : UDef.Cd;
        /// <summary>1 plus every attack-speed boost on the unit (old SimHeroes6.atkHaste).</summary>
        public float AtkHaste() { float a = 1; for (int i = 0; i < StCount; i++) if (Sts[i].Type == St.AtkHaste) a += Sts[i].V; return a; }

        /// <summary>A new level: a clone only takes the number, a hero gains the extra health.</summary>
        internal void SetLevel(int l)
        {
            if (IsClone) { Level = l; return; }
            float old = StatHp(); Level = l; float nw = StatHp(); MaxHp = nw;
            if (Alive) Hp += nw - old;
        }

        // ---------- statuses (Match.AddStatus is the way in, it applies control immunity and interrupts) ----------
        internal int AddRaw(St type, float t, float v, float amount, Unit by, bool queen)
        {
            if (StCount >= MaxStatus) return -1; // never in a real match: the oldest-ending one would be a fine victim, but none is needed
            ref var s = ref Sts[StCount];
            s.Type = type; s.T = t; s.V = v; s.Amount = amount; s.By = by; s.Queen = queen;
            return StCount++;
        }
        internal void RemoveAt(int i)
        {
            StCount--;
            for (int k = i; k < StCount; k++) Sts[k] = Sts[k + 1];
            Sts[StCount] = default;
        }
        internal void RemoveAll(St type)
        {
            for (int i = StCount - 1; i >= 0; i--) if (Sts[i].Type == type) RemoveAt(i);
        }
        internal void ClearStatuses()
        {
            for (int i = 0; i < StCount; i++) Sts[i] = default;
            StCount = 0;
        }
        internal int Find(St type)
        {
            for (int i = 0; i < StCount; i++) if (Sts[i].Type == type) return i;
            return -1;
        }
        internal int FindBy(St type, Unit by)
        {
            for (int i = 0; i < StCount; i++) if (Sts[i].Type == type && Sts[i].By == by) return i;
            return -1;
        }
    }
}
