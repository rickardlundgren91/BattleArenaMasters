using System;

namespace BAM.Sim
{
    /// <summary>
    /// HERO TEST (old herotest.js / SimLoop.startHeroTest; Rickard's "träningsområde", 2026-10-26): try any hero against
    /// dummies. The player in its own half of the arena, an enemy dummy hero (Bram) 4 tiles up that never walks but hits back,
    /// a friendly dummy beside it kept hurt (to test heals and shields), five practice minions that stand still or walk between
    /// two spots and come back 3 s after dying, towers that cannot be hurt and never shoot, no waves, no stars, a fixed level
    /// (with the player's item choices up to it, HtOffers), heroes back 3 s after death. Never ends: the HUD's Reset and Exit
    /// leave it. The old layout, mirrored to BAM's map (the enemy is up the screen here).
    /// </summary>
    public sealed partial class Match
    {
        public const float HtAllyHp = 0.4f; // the ally dummy's health share
        const float HtPx = 7.5f, HtPy = 26.5f;
        /// <summary>The HERO TEST option on the HUD bar: the special is ready again 1 s after use.</summary>
        public bool HeroTestFastCd;
        public bool HeroTest => Config.HeroTest;
        /// <summary>The enemy dummy hero and the friendly one (HERO TEST only).</summary>
        public Unit HtFoe, HtAlly;
        float htAllyFullT, htCdT;

        sealed partial class HtSlot { public MinionKind Kind; public float Ax, Ay, Bx = float.NaN, By; public Unit M; public bool GoB; public float DeadT; }
        HtSlot[] htSlots;

        void StartHeroTest()
        {
            NoStars = true;
            State = MatchState.Play; Time = 0; WaveT = float.MaxValue;
            Level = Math.Max(1, Math.Min(Rules.MaxLevel, Config.HeroTestLevel));
            for (int t = 0; t < 2; t++) { AddStatus(Towers[t], St.Invulnerable, 1e9f); AddStatus(Towers[t], St.Disarm, 1e9f); }

            var p = NewHero(Config.PlayerHero, Teams.Player, HtPx, HtPy);
            p.IsPlayer = true; Player = p;
            if (Config.PlayerIsBot) p.Bot = new BotBrain(p, Defs.Tier(Config.Difficulty), this);
            p.Charge = p.HDef.MaxCharge;
            HtFoe = NewHero(HeroId.Bram, Teams.Enemy, HtPx, HtPy - 4f); HtFoe.Dummy = true;
            HtAlly = NewHero(Config.PlayerHero == HeroId.Bram ? HeroId.Knut : HeroId.Bram, Teams.Player, HtPx + 2f, HtPy); HtAlly.Dummy = true;
            HtAlly.Hp = HtAlly.MaxHp * HtAllyHp; htAllyFullT = 0; htCdT = 0;
            for (int i = 0; i < Heroes.Count; i++) Heroes[i].SetLevel(Level);
            HtAlly.Hp = HtAlly.MaxHp * HtAllyHp;

            htSlots = new[]
            {
                new HtSlot { Kind = MinionKind.Melee, Ax = HtPx - 2.5f, Ay = HtPy - 3.5f },
                new HtSlot { Kind = MinionKind.Ranged, Ax = HtPx + 2.5f, Ay = HtPy - 3.5f },
                new HtSlot { Kind = MinionKind.Siege, Ax = HtPx, Ay = HtPy - 6.5f },
                new HtSlot { Kind = MinionKind.Melee, Ax = HtPx - 3.5f, Ay = HtPy - 5.5f, Bx = HtPx + 3.5f, By = HtPy - 5.5f },
                new HtSlot { Kind = MinionKind.Ranged, Ax = HtPx + 3f, Ay = HtPy - 1f, Bx = HtPx + 3f, By = HtPy - 7f },
            };
            for (int i = 0; i < htSlots.Length; i++) HtSpawn(htSlots[i]);
            AssignSpells();
            HtOffers();
        }

        // the player's item choices for the level set with Lv (one from 6, a second queued behind it from 10: ItemS.OfferLevels), as a match that had
        // climbed to that level would have given them; the level never changes here, so ItemLevelUp never runs
        void HtOffers()
        {
            var p = Player;
            if (!ItemsOn || p == null) return;
            foreach (int l in ItemS.OfferLevels)
            {
                if (l > Level) continue;
                if (p.OfferCount > 0) p.OfferQueued++;
                else RollOffer(p);
            }
        }

        void HtSpawn(HtSlot s)
        {
            var m = NewMinion(s.Kind, Teams.Enemy, s.Ax, s.Ay);
            m.TutStatic = true; m.DmgOverride = 0; // stands on SpawnX/SpawnY (Minions.cs) and does no harm
            s.GoB = !float.IsNaN(s.Bx);
            if (s.GoB) { m.SpawnX = s.Bx; m.SpawnY = s.By; }
            s.M = m; s.DeadT = 0;
        }

        void HeroTestTick(float dt)
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                if (!h.Alive && h.RespawnT > 3f) h.RespawnT = 3f;
            }
            var pl = Player;
            if (HeroTestFastCd && pl != null && pl.Alive)
            {
                if (pl.SpecialReady) htCdT = 0;
                else if ((htCdT += dt) >= 1f) { htCdT = 0; pl.Charge = pl.HDef.MaxCharge; }
            }
            var al = HtAlly;
            if (al != null && al.Alive)
            {
                // keep the ally hurt: a full heal is taken back after 2 s, and it never drops below 5 %
                if (al.Hp >= al.MaxHp * 0.99f) { htAllyFullT += dt; if (htAllyFullT >= 2f) { al.Hp = al.MaxHp * HtAllyHp; htAllyFullT = 0; } }
                else htAllyFullT = 0;
                if (al.Hp < al.MaxHp * 0.05f) al.Hp = al.MaxHp * 0.05f;
            }
            for (int i = 0; i < htSlots.Length; i++)
            {
                var s = htSlots[i];
                if (!s.M.Alive) { s.DeadT += dt; if (s.DeadT >= 3f) HtSpawn(s); continue; }
                if (float.IsNaN(s.Bx)) continue;
                if (SimMath.Hypot(s.M.SpawnX - s.M.X, s.M.SpawnY - s.M.Y) < 0.2f)
                {
                    s.GoB = !s.GoB;
                    s.M.SpawnX = s.GoB ? s.Bx : s.Ax; s.M.SpawnY = s.GoB ? s.By : s.Ay;
                }
            }
        }
    }
}
