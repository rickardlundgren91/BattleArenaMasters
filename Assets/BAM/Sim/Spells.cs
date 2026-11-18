using System;

namespace BAM.Sim
{
    /// <summary>
    /// Battle spells (old SimSpells.cs, Rickard 2026-10-23, like Mobile Legends): before the match the player picks ONE extra
    /// skill for the hero: Blink, Recall (teleport to the base), Shield or Haste, and Sprint (BAM's own, Rickard 2026-10-26:
    /// "ta sprint också"; like MLBB's Sprint, shorter for BAM's short matches). One button next to the SPECIAL. Off in the
    /// guided match. Bots get a seeded random spell at the start and use it with simple rules (no random numbers while playing,
    /// so a replay matches). All values are the old SP_* constants.
    /// </summary>
    public static class SpellDefs
    {
        public const float BlinkCd = 25f, BlinkDist = 3.5f, RecallCd = 0.1f, RecallChannel = 2f, RecallCancelCd = 0.1f,
            // Recall is spammable: 0.1 s cooldown (Rickard 2026-10-26, "0.1 sec snarare"; old 15 s), the 2 s channel and its cancel rules stay
            ShieldCd = 25f, ShieldDur = 5f, ShieldPct = 0.25f, // Shield 20 % 3 s 30 s -> 25 % 4 s 25 s (Simon 2026-11-02 "för svag"; Hard bots 3000 matches: 47.0 -> 48.7 % wins with it) -> 5 s (Rickard 2026-11-03 "25% i 5s"); no CC immunity (only Moa's shields)
            HasteCd = 30f, HasteDur = 5f, HastePct = 0.4f,
            SprintCd = 25f, SprintDur = 4f, SprintPct = 0.5f;

        // The five of 2026-11-18 (Rickard: "lägg in potion, immune shield, ress, 20% hero dmg", then "förbandslåda att plocka upp,
        // first aid heter skillen" and "heavy bladearmor - reflects back any damage taken from any target except towers 75%"). The
        // effects are the old game's use items (ABC2 itemData: HealthPotion 50 %, InvincibilityPotion 4 s, RessurectionPotion 75 %,
        // DamageOnHit 5 s), with cooldowns cut for BAM's 4:30 matches (ABC2's were 90-200 s). Bot lab 2026-11-18, 1500 Hard matches each,
        // one team all on the spell vs random old ones: Immune 4 s/50 s won 61 % -> 3 s/60 s 56 % (Shield 54, Bladearmor 55); First Aid
        // cd 45 s 46.5 % -> 30 s 48.9 %; Revive 50 %; Fury 60 % (Rickard's 20 %, kept).
        /// <summary>First Aid: a kit at the caster's feet; the first hero of the team to step on it (the caster too, after
        /// FirstAidArm s) heals FirstAidHeal of max HP over FirstAidTime s (Rickard 2026-11-18: "låt bara en buff ligga på marken ...
        /// 40% of total hp over 7 seconds"). It lies FirstAidLife s; a new one replaces the caster's old one.</summary>
        public const float FirstAidCd = 30f, FirstAidHeal = 0.4f, FirstAidTime = 7f, FirstAidLife = 30f, FirstAidArm = 1f, FirstAidR = 0.55f;
        /// <summary>Immune: no damage and no stun, root or slow for ImmuneDur s, but the hero walks ImmuneSlow slower (ABC2's ale).</summary>
        public const float ImmuneCd = 60f, ImmuneDur = 3f, ImmuneSlow = -0.3f;
        /// <summary>Revive: works by itself. ReviveDelay s after a death the hero stands up where it fell with RevivePct of max HP
        /// (and the usual respawn protection); then the cooldown runs.</summary>
        public const float ReviveCd = 150f, ReviveDelay = 1.5f, RevivePct = 0.75f;
        /// <summary>Fury: always on, +FuryPct damage to enemy heroes (and their decoys), from every kind of hit.</summary>
        public const float FuryPct = 0.2f;
        /// <summary>Heavy Bladearmor: for BladeDur s, BladePct of every hit's damage on the hero goes back to whoever hit (heroes,
        /// minions, Fenn's wolf), never to a tower.</summary>
        public const float BladeCd = 35f, BladeDur = 5f, BladePct = 0.75f;
        /// <summary>V of the Invulnerable status Immune gives (the respawn's has 0): it also makes the hero CC immune.</summary>
        public const float ImmuneTag = -8f;

        /// <summary>Every spell in the picker's order.</summary>
        public static readonly SpellId[] All = { SpellId.Blink, SpellId.Recall, SpellId.Shield, SpellId.Haste, SpellId.Sprint,
            SpellId.FirstAid, SpellId.Immune, SpellId.Revive, SpellId.Fury, SpellId.Bladearmor };
        /// <summary>A spell with no button press: Revive and Fury work by themselves (the HUD button only shows them).</summary>
        public static bool Passive(SpellId s) => s == SpellId.Revive || s == SpellId.Fury;

        public static string Name(SpellId s) => s == SpellId.None ? "" : s == SpellId.FirstAid ? "First Aid" : s.ToString();
        static int I(float v) => (int)Math.Round(v);
        public static string Desc(SpellId s) =>
            s == SpellId.Blink ? "Dash 3.5 tiles the way you move. Cooldown " + (int)BlinkCd + " s"
            : s == SpellId.Recall ? "Channel " + (int)RecallChannel + " s to teleport to your base. Moving or damage cancels it. No cooldown."
            : s == SpellId.Shield ? "A shield of " + (int)(ShieldPct * 100) + "% max health for " + (int)ShieldDur + " s. Cooldown " + (int)ShieldCd + " s"
            : s == SpellId.Haste ? "+" + (int)(HastePct * 100) + "% attack speed for " + (int)HasteDur + " s. Cooldown " + (int)HasteCd + " s"
            : s == SpellId.Sprint ? "+" + (int)(SprintPct * 100) + "% movement speed for " + (int)SprintDur + " s. Cooldown " + (int)SprintCd + " s"
            : s == SpellId.FirstAid ? "Drop a first aid kit. You or an ally who picks it up heals " + I(FirstAidHeal * 100) + "% max health over " + I(FirstAidTime) + " s. Cooldown " + I(FirstAidCd) + " s"
            : s == SpellId.Immune ? "Take no damage and no stuns for " + I(ImmuneDur) + " s, but move " + I(-ImmuneSlow * 100) + "% slower. Cooldown " + I(ImmuneCd) + " s"
            : s == SpellId.Revive ? "When you die, stand up where you fell with " + I(RevivePct * 100) + "% health. Works by itself. Cooldown " + I(ReviveCd) + " s"
            : s == SpellId.Fury ? "+" + I(FuryPct * 100) + "% damage to heroes, all match long."
            : s == SpellId.Bladearmor ? "Heavy Bladearmor: for " + I(BladeDur) + " s, " + I(BladePct * 100) + "% of the damage you take goes back to the attacker (not towers). Cooldown " + I(BladeCd) + " s" : "";
        public static float Cooldown(SpellId s) => s switch
        {
            SpellId.Blink => BlinkCd, SpellId.Recall => RecallCd, SpellId.Shield => ShieldCd, SpellId.Sprint => SprintCd,
            SpellId.FirstAid => FirstAidCd, SpellId.Immune => ImmuneCd, SpellId.Revive => ReviveCd, SpellId.Fury => 0f, SpellId.Bladearmor => BladeCd,
            _ => HasteCd,
        };
        /// <summary>The spell's colour as 0xRRGGBB (old spellColor): Blink sky blue, Recall mint, Shield gold, Haste orange; Sprint lime;
        /// First Aid red-cross red, Immune white-gold, Revive phoenix orange-pink, Fury crimson, Bladearmor steel.</summary>
        public static int Color(SpellId s) => s switch
        {
            SpellId.Blink => 0x8fd8ff, SpellId.Recall => 0x9fffd0, SpellId.Shield => 0xffe08a, SpellId.Sprint => 0xc8ff6a,
            SpellId.FirstAid => 0xff6b6b, SpellId.Immune => 0xfff6d0, SpellId.Revive => 0xffa070, SpellId.Fury => 0xff3c50, SpellId.Bladearmor => 0xb8c6d8,
            _ => 0xff9a6a,
        };
    }

    public sealed partial class Unit
    {
        /// <summary>Revive: this dead hero comes back where it fell (Spells.cs ReviveOnDeath), not at its base.</summary>
        internal bool ReviveHere;
    }

    public sealed partial class Match
    {
        const float SpellShieldTag = -7f; // V of the shield a Shield spell gives, so a new one replaces it

        /// <summary>Battle spells are on in every match; in the guided one only from its Blink moment (Tutorial.SpellOn).</summary>
        public bool SpellsOn => Tut == null || Tut.SpellOn;
        public bool SpellReady(Unit h) => h != null && h.Alive && h.Spell != SpellId.None && SpellsOn && Time >= h.SpellAt && h.SpellChanEnd < 0;
        /// <summary>Seconds until h's spell is ready again (0 when ready).</summary>
        public float SpellLeft(Unit h) => h == null ? 0 : Math.Max(0, h.SpellAt - Time);
        public static bool SpellChanneling(Unit h) => h != null && h.SpellChanEnd >= 0;

        /// <summary>What every bot draws from (all difficulties, allies and enemies): never Recall (Rickard 2026-11-18: "bottar ska
        /// aldrig välja teleport som skill"). Bot analysis 2026-10-30: a bot with Recall has no way out of a lost fight and died about
        /// twice as often as one with Blink.</summary>
        static readonly SpellId[] BotSpells = { SpellId.Blink, SpellId.Shield, SpellId.Haste, SpellId.Sprint,
            SpellId.FirstAid, SpellId.Immune, SpellId.Revive, SpellId.Fury, SpellId.Bladearmor };

        // who gets which spell at match start: the player's pick, a seeded draw for bots
        void AssignSpells()
        {
            if (!SpellsOn) return;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                var pick = Config.Seats != null && h.Seat >= 0 ? Config.Seats[h.Seat].Spell : h == Player ? Config.PlayerSpell : SpellId.None;
                h.Spell = h.Seat >= 0 || h == Player ? (pick != SpellId.None ? pick : SpellId.Blink) : BotSpells[Rng.Int(BotSpells.Length)];
                h.SpellAt = 0; h.SpellChanEnd = -1;
            }
        }

        /// <summary>h's spell would take him off a spot he must keep: an anchored Kugg stays put until his special is done, the pack-up
        /// too, so Blink and Recall wait (Rickard 2026-11-13: "han ska vara förankrad tills special är klar ändå"; before, a Blink moved the
        /// whole turret with its shots). The HUD greys the button then.</summary>
        public bool SpellLocked(Unit h) => h != null && h.KuggRooted && (h.Spell == SpellId.Blink || h.Spell == SpellId.Recall);

        /// <summary>Casts h's battle spell. ang = where a blink goes (NaN = the way the hero faces), k 0..1 of its full distance.
        /// False when nothing happened (cooldown, stunned, busy casting, nowhere to go).</summary>
        public bool CastSpell(Unit h, float ang = float.NaN, float k = 1)
        {
            // a special's warning (Telegraph.cs) blocks the spell too: every other hero stands Casting in it, Tikta walks in hers
            // without St.Casting since 0.3.9, and a Blink there would move her needles' start (review 0.3.9 nr 59)
            // Nor in a jump pad's flight (Grove: a Blink in the air left the hero beside its landing spot), nor while Tikta's dragged
            // needles still leave her hand (a Blink moved their start off the red line; full review 2026-11-09 nr 78, 64)
            if (SpellDefs.Passive(h.Spell)) return false; // Revive and Fury work by themselves
            if (!SpellReady(h) || h.IsClone || h.Has(St.Stun) || h.Has(St.Casting) || h.TeleAt >= 0 || (h.KnockPad && h.KnockT > 0) || NeedleInHand(h) || SpellLocked(h)) return false;
            float fx = h.X, fy = h.Y;
            switch (h.Spell)
            {
                case SpellId.Blink:
                {
                    float a = float.IsNaN(ang) ? (Tut != null ? TutBlinkAngle(h) : h.Facing) : ang; // the guided match: away from Knut (Tutorial.cs)
                    BlinkPoint(h, a, k, out float x, out float y);
                    if (SimMath.Hypot(x - h.X, y - h.Y) < 0.5f) return false; // against a wall
                    LoseShots(h); // the shots on their way miss: a blink gets you out of them (Combat.cs)
                    h.X = h.PX = x; h.Y = h.PY = y; h.Facing = a; h.MoInit = false; h.MovedAt = Time;
                    break;
                }
                case SpellId.Recall:
                    h.SpellChanEnd = Time + SpellDefs.RecallChannel; h.SpellChanStart = Time; h.SpellChanX = h.X; h.SpellChanY = h.Y;
                    break;
                case SpellId.Shield:
                    for (int i = h.StCount - 1; i >= 0; i--) if (h.Sts[i].Type == St.Shield && h.Sts[i].V == SpellShieldTag) h.RemoveAt(i);
                    AddStatus(h, St.Shield, SpellDefs.ShieldDur, SpellShieldTag, h.MaxHp * SpellDefs.ShieldPct); // by nobody: a Moa shield would also make her CC immune
                    break;
                case SpellId.Sprint:
                    AddStatus(h, St.SpeedPct, SpellDefs.SprintDur, SpellDefs.SprintPct, 0, h);
                    break;
                case SpellId.FirstAid:
                {
                    for (int i = 0; i < Projs.Count; i++) if (Projs[i].Mode == ProjMode.FirstAidKit && Projs[i].Src == h) Projs[i].Done = true; // one kit each
                    var kit = NewProj(ProjMode.FirstAidKit, h, h.X, h.Y);
                    kit.Radius = SpellDefs.FirstAidR; kit.Dur = SpellDefs.FirstAidLife;
                    break;
                }
                case SpellId.Immune:
                    h.RemoveAll(St.Stun); h.RemoveAll(St.Root); // nothing holds it any more (it could not be cast stunned anyway)
                    for (int i = h.StCount - 1; i >= 0; i--) if ((h.Sts[i].Type == St.SlowPct || h.Sts[i].Type == St.SlowFlat) && h.Sts[i].V < 0) h.RemoveAt(i);
                    AddStatus(h, St.Invulnerable, SpellDefs.ImmuneDur, SpellDefs.ImmuneTag);
                    h.AddRaw(St.SlowPct, SpellDefs.ImmuneDur, SpellDefs.ImmuneSlow, 0, h, false); // its own slow: no control, so no Charm or immunity
                    break;
                case SpellId.Bladearmor:
                    h.RemoveAll(St.Reflect);
                    AddStatus(h, St.Reflect, SpellDefs.BladeDur, SpellDefs.BladePct, 0, h);
                    break;
                default: // Haste: keepStatus, so a second cast only renews it
                {
                    bool kept = false;
                    for (int i = 0; i < h.StCount; i++) if (h.Sts[i].Type == St.AtkHaste && h.Sts[i].By == h) { h.Sts[i].T = Math.Max(h.Sts[i].T, SpellDefs.HasteDur); kept = true; }
                    if (!kept) AddStatus(h, St.AtkHaste, SpellDefs.HasteDur, SpellDefs.HastePct, 0, h);
                    break;
                }
            }
            h.SpellAt = Time + SpellDefs.Cooldown(h.Spell);
            Emit(EventType.SpellCast, h, null, 0, (byte)h.Spell, false, fx, fy);
            return true;
        }

        /// <summary>Where a blink in direction ang lands: k (0..1) of the full distance, at least 1 tile. Blinks pass terrain
        /// (Rickard 2026-10-25): the farthest free spot on the line, even behind a wall or water; if the end is inside a wall
        /// it lands just in front of it. The HUD's aim preview reads it too.</summary>
        public void BlinkPoint(Unit h, float ang, float k, out float x, out float y)
        {
            float far = Math.Max(1f, SpellDefs.BlinkDist * SimMath.Clamp(k, 0, 1)), c = SimMath.Cos(ang), s = SimMath.Sin(ang);
            for (float d = far; d >= 0.25f - 1e-4f; d -= 0.25f)
            {
                float nx = h.X + c * d, ny = h.Y + s * d;
                if (Map.Free(nx, ny, h.R)) { x = nx; y = ny; return; }
            }
            x = h.X; y = h.Y;
        }

        void SpellBreak(Unit h)
        {
            if (h.SpellChanEnd < 0) return;
            h.SpellChanEnd = -1; h.SpellAt = Math.Min(h.SpellAt, Time + SpellDefs.RecallCancelCd);
            Emit(EventType.SpellCancel, h, null);
        }

        // per tick for every living hero: the recall channel, and the bots' use
        void SpellTick(Unit h)
        {
            if (h.Spell == SpellId.None || !SpellsOn) return;
            if (h.SpellChanEnd >= 0)
            {
                bool pushed = SimMath.Hypot(h.X - h.SpellChanX, h.Y - h.SpellChanY) > 0.05f; // a pull or knock moved it
                if (h.Has(St.Stun) || h.MovedAt > h.SpellChanStart + 0.05f || h.LastHurt >= h.SpellChanStart || pushed) { SpellBreak(h); return; }
                if (Time >= h.SpellChanEnd)
                {
                    h.SpellChanEnd = -1;
                    float fx = h.X, fy = h.Y;
                    // a special tapped in the channel's last moment is still in its red warning: cut it, the charge stays, so it never
                    // fires from the base at the old aim (bug review 2026-11-02 nr 16)
                    if (h.TeleAt >= 0) InterruptSpecial(h);
                    LoseShots(h);
                    h.X = h.PX = h.SpawnX; h.Y = h.PY = h.SpawnY; h.MoInit = false;
                    if (h.Bot != null) h.Bot.Reset(false);
                    Emit(EventType.Recall, h, null, 0, 0, false, fx, fy);
                }
                return;
            }
            if (h.Bot == null || h.Dummy || !SpellReady(h) || State != MatchState.Play) return;
            switch (h.Spell)
            {
                case SpellId.Shield: if (h.Hp < h.MaxHp * 0.6f && HurtRecently(h)) CastSpell(h); break;
                case SpellId.Haste: if (EnemyHeroNear(h, h.Range + 1f) || EnemyTowerNear(h)) CastSpell(h); break;
                case SpellId.Recall: if (h.Hp < h.MaxHp * 0.25f && !HurtRecently(h) && SimMath.Dist(h, h.SpawnX, h.SpawnY) > 8f) CastSpell(h); break;
                case SpellId.Blink: if (h.Hp < h.MaxHp * 0.35f && HurtRecently(h)) CastSpell(h, SimMath.Atan2(h.SpawnY - h.Y, h.SpawnX - h.X)); break; // flee towards the base
                case SpellId.Sprint: if (h.Hp < h.MaxHp * 0.4f && HurtRecently(h)) CastSpell(h); break; // run away (the bot walks home when this low)
                case SpellId.FirstAid: if (h.Hp < h.MaxHp * 0.55f) CastSpell(h); break; // drops it at its feet and takes it a second later
                case SpellId.Immune: if (h.Hp < h.MaxHp * 0.3f && HurtRecently(h)) CastSpell(h); break;
                case SpellId.Bladearmor: if (HurtRecently(h) && EnemyHeroNear(h, 3.5f)) CastSpell(h); break;
            }
        }

        // ---------- the five of 2026-11-18 ----------
        /// <summary>Fury: +20 % on enemy heroes from a hero who has it (its wolf's and decoy's hits too).</summary>
        float FuryMult(Unit att, Unit tgt) =>
            att != null && att.Kind == UnitKind.Hero && att.Spell == SpellId.Fury && SpellsOn && tgt.Kind == UnitKind.Hero && tgt.Team != att.Team ? 1 + SpellDefs.FuryPct : 1;

        /// <summary>Heavy Bladearmor: tgt was hit for amount by src; BladePct of it goes back to src, unless src is a tower.</summary>
        void Reflect(Unit src, Unit tgt, float amount)
        {
            if (src == null || !src.Alive || src.Kind == UnitKind.Tower || src.Team == tgt.Team || tgt.Kind != UnitKind.Hero || amount <= 0) return;
            float v = 0;
            for (int i = 0; i < tgt.StCount; i++) if (tgt.Sts[i].Type == St.Reflect && tgt.Sts[i].T > 0) v = Math.Max(v, tgt.Sts[i].V);
            if (v <= 0) return;
            DealDamage(tgt, src, amount * v, DmgKind.Passive, DmgFlags.Reflected | DmgFlags.NotDirect | DmgFlags.NoIgnite);
        }

        /// <summary>Revive: called as a hero with it dies. If it is ready, the hero comes back where it fell after ReviveDelay s.</summary>
        void ReviveOnDeath(Unit u)
        {
            u.ReviveHere = false;
            if (u.Spell != SpellId.Revive || !SpellsOn || Time < u.SpellAt || u.IsClone) return;
            u.ReviveHere = true;
            u.RespawnT = Math.Min(u.RespawnT, SpellDefs.ReviveDelay);
            u.SpellAt = Time + SpellDefs.ReviveCd;
        }

        /// <summary>The First Aid kit, every tick: the first living hero of its team to touch it (its dropper only after the
        /// arming time) heals and the kit is gone. A hero at full health walks over it.</summary>
        void FirstAidUpdate(Proj p)
        {
            if (p.T >= p.Dur) { p.Done = true; return; }
            for (int i = 0; i < Heroes.Count; i++)
            {
                var u = Heroes[i];
                if (!u.Alive || u.Team != p.Team || u.Hp >= u.MaxHp) continue;
                if (u == p.Src && p.T < SpellDefs.FirstAidArm) continue;
                if (SimMath.Dist(u, p.X, p.Y) > p.Radius + u.R) continue;
                u.RemoveAll(St.HealOverTime); // a second kit starts over, it does not stack
                // a hair over 7 s, so the 7th one-second tick still comes before the status runs out
                AddStatus(u, St.HealOverTime, SpellDefs.FirstAidTime + 0.05f, u.MaxHp * SpellDefs.FirstAidHeal / SpellDefs.FirstAidTime, 0, p.Src);
                Emit(EventType.SpellCast, u, null, 0, (byte)SpellId.FirstAid, true, p.X, p.Y); // Big = the pick-up (the view's flash)
                p.Done = true;
                return;
            }
        }


        bool HurtRecently(Unit h) => Time - h.LastHurt < 1.5f;
        bool EnemyHeroNear(Unit h, float d)
        {
            for (int i = 0; i < Units.Count; i++) { var u = Units[i]; if (u.Alive && u.Kind == UnitKind.Hero && u.Team != h.Team && SimMath.Dist(u, h) < d) return true; }
            return false;
        }
        bool EnemyTowerNear(Unit h)
        {
            var t = Towers[Teams.Other(h.Team)];
            return t != null && t.Alive && SimMath.Dist(t, h) < h.Range + 1f;
        }
    }
}
