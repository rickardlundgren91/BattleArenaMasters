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
            ShieldCd = 30f, ShieldDur = 3f, ShieldPct = 0.2f, HasteCd = 30f, HasteDur = 5f, HastePct = 0.4f,
            SprintCd = 25f, SprintDur = 4f, SprintPct = 0.5f;
        /// <summary>The four spells in the picker's order.</summary>
        public static readonly SpellId[] All = { SpellId.Blink, SpellId.Recall, SpellId.Shield, SpellId.Haste, SpellId.Sprint };

        public static string Name(SpellId s) => s == SpellId.None ? "" : s.ToString();
        public static string Desc(SpellId s) =>
            s == SpellId.Blink ? "Dash 3.5 tiles the way you move. Cooldown " + (int)BlinkCd + " s"
            : s == SpellId.Recall ? "Channel " + (int)RecallChannel + " s to teleport to your base. Moving or damage cancels it. No cooldown."
            : s == SpellId.Shield ? "A shield of " + (int)(ShieldPct * 100) + "% max health for " + (int)ShieldDur + " s. Cooldown " + (int)ShieldCd + " s"
            : s == SpellId.Haste ? "+" + (int)(HastePct * 100) + "% attack speed for " + (int)HasteDur + " s. Cooldown " + (int)HasteCd + " s"
            : s == SpellId.Sprint ? "+" + (int)(SprintPct * 100) + "% movement speed for " + (int)SprintDur + " s. Cooldown " + (int)SprintCd + " s" : "";
        public static float Cooldown(SpellId s) => s == SpellId.Blink ? BlinkCd : s == SpellId.Recall ? RecallCd : s == SpellId.Shield ? ShieldCd : s == SpellId.Sprint ? SprintCd : HasteCd;
        /// <summary>The spell's colour as 0xRRGGBB (old spellColor): Blink sky blue, Recall mint, Shield gold, Haste orange; Sprint lime.</summary>
        public static int Color(SpellId s) => s == SpellId.Blink ? 0x8fd8ff : s == SpellId.Recall ? 0x9fffd0 : s == SpellId.Shield ? 0xffe08a : s == SpellId.Sprint ? 0xc8ff6a : 0xff9a6a;
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

        /// <summary>What a Normal or Hard bot draws from: no Recall. Bot analysis 2026-10-30: a bot with Recall has no way out of a
        /// lost fight and died about twice as often as one with Blink. Easy bots draw from all five.</summary>
        static readonly SpellId[] BotSpells = { SpellId.Blink, SpellId.Shield, SpellId.Haste, SpellId.Sprint };

        // who gets which spell at match start: the player's pick, a seeded draw for bots
        void AssignSpells()
        {
            if (!SpellsOn) return;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                var pick = Config.Seats != null && h.Seat >= 0 ? Config.Seats[h.Seat].Spell : h == Player ? Config.PlayerSpell : SpellId.None;
                var draw = h.Bot != null && h.Bot.Tier.V3 ? BotSpells : SpellDefs.All;
                h.Spell = h.Seat >= 0 || h == Player ? (pick != SpellId.None ? pick : SpellId.Blink) : draw[Rng.Int(draw.Length)];
                h.SpellAt = 0; h.SpellChanEnd = -1;
            }
        }

        /// <summary>Casts h's battle spell. ang = where a blink goes (NaN = the way the hero faces), k 0..1 of its full distance.
        /// False when nothing happened (cooldown, stunned, busy casting, nowhere to go).</summary>
        public bool CastSpell(Unit h, float ang = float.NaN, float k = 1)
        {
            if (!SpellReady(h) || h.IsClone || h.Has(St.Stun) || h.Has(St.Casting)) return false;
            float fx = h.X, fy = h.Y;
            switch (h.Spell)
            {
                case SpellId.Blink:
                {
                    float a = float.IsNaN(ang) ? h.Facing : ang;
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
