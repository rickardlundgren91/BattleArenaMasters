using System;

namespace BAM.Sim
{
    // The five battle spells of 2026-11-18 (Rickard: potion, immune shield, ress, 20 % hero damage; then First Aid as a kit on the
    // ground with 40 % over 7 s, and Heavy Bladearmor reflecting 75 % of the damage taken, never from towers). Spells.cs.
    public sealed partial class Match
    {
        static Match MakeQuiet(HeroId player, HeroId[] allies, HeroId[] enemies)
        {
            var m = Make(player, allies, enemies);
            foreach (var u in m.Heroes) { u.Bot = null; u.Spell = SpellId.None; u.SpellAt = 0; u.SpellChanEnd = -1; }
            m.WaveT = 1e9f; // no minions
            return m;
        }

        float HealFrom(Unit by, Unit to)
        {
            float s = 0;
            foreach (var e in Events) if (e.Type == EventType.Heal && e.A == by && e.B == to) s += e.V;
            return s;
        }

        static void SpellsV3()
        {
            float x = 7.5f, y = 20;

            // ---------- First Aid ----------
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                var p = m.Player; var al = m.Ally(HeroId.Bram); var fo = m.Foe(HeroId.Knut);
                Place(m.Foe(HeroId.Moa), 1, 1); Place(al, x + 4, y); Place(fo, x, y); Place(p, x, y);
                p.Spell = SpellId.FirstAid; p.Hp = p.MaxHp * 0.2f; fo.Hp = fo.MaxHp * 0.2f;
                Check(m.CastSpell(p), "First Aid: cast");
                Proj kit = null; foreach (var q in m.Projs) if (q.Mode == ProjMode.FirstAidKit) kit = q;
                Check(kit != null && kit.IsZone && kit.Zone == ZoneKind.FirstAid, "First Aid: a kit lies on the ground");
                for (int i = 0; i < (int)(0.5f / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(!kit.Done, "First Aid: the dropper does not take it at once, and an enemy on it never does");
                float foHp = fo.Hp; fo.X = fo.PX = x; fo.Y = fo.PY = y + 8; // away, so it does not fight
                m.Events.Clear();
                for (int i = 0; i < (int)(0.7f / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(kit.Done && p.Has(St.HealOverTime), "First Aid: after a second the dropper standing on it picks it up");
                float max0 = p.MaxHp; // the level can rise during the heal: it counts from the max at the pick-up
                float got = 0;
                for (int i = 0; i < (int)(7.5f / Rules.Tick); i++) { p.LastFight = fo.LastFight = m.Time; m.Step(PlayerInput.None); got += m.HealFrom(p, p); }
                Check(Near(got, max0 * SpellDefs.FirstAidHeal, 0.5f) && !p.Has(St.HealOverTime), $"First Aid: 40 % of max HP over 7 s ({got / max0 * 100:0.0} %)");
                Check(fo.Hp <= foHp + 0.01f, "First Aid: the enemy on it got nothing");

                // an ally takes it; a hero at full health walks over it
                p.SpellAt = 0; Place(p, x, y); Place(fo, x, y + 8); Place(al, x, y); al.Hp = al.MaxHp * 0.5f;
                Check(m.CastSpell(p), "First Aid: cast again");
                m.Step(PlayerInput.None);
                Check(al.Has(St.HealOverTime) && !p.Has(St.HealOverTime), "First Aid: an ally on it takes it at once");
                p.SpellAt = 0; Place(al, x + 4, y); Place(p, x, y);
                m.CastSpell(p);
                for (int i = 0; i < (int)(2f / Rules.Tick); i++) m.Step(PlayerInput.None);
                bool lies = false; foreach (var q in m.Projs) if (q.Mode == ProjMode.FirstAidKit && !q.Done) lies = true;
                Check(lies && !p.Has(St.HealOverTime), "First Aid: at full health the kit stays on the ground");
            }

            // ---------- Immune ----------
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                var p = m.Player; var fo = m.Foe(HeroId.Knut);
                Place(p, x, y); Place(fo, x + 2, y); p.Spell = SpellId.Immune;
                m.AddStatus(p, St.SlowPct, 2f, -0.4f, 0, fo);
                Check(m.CastSpell(p), "Immune: cast");
                Check(m.DealDamage(fo, p, 100, DmgKind.Special) == 0 && p.Hp == p.MaxHp, "Immune: no damage");
                Check(m.AddStatus(p, St.Stun, 1f, 0, 0, fo) < 0 && !p.Has(St.Stun), "Immune: no stun");
                Check((p.Flags & StatusFlags.Immune) != 0, "Immune: the art is told");
                float sp = 0; for (int i = 0; i < p.StCount; i++) { var s = p.GetStatus(i); if (s.Type == St.SlowPct) sp += s.V; }
                Check(Near(sp, SpellDefs.ImmuneSlow), $"Immune: only its own slow is left ({sp:0.00})");
                for (int i = 0; i < (int)((SpellDefs.ImmuneDur + 0.1f) / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(m.DealDamage(fo, p, 10, DmgKind.Special) > 0, "Immune: it ends after 4 s");
            }

            // ---------- Revive ----------
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                var p = m.Player; var fo = m.Foe(HeroId.Knut);
                Place(p, x, y); Place(fo, x + 2, y); p.Spell = SpellId.Revive;
                Check(!m.CastSpell(p), "Revive: no button press");
                m.DealDamage(fo, p, p.MaxHp * 5, DmgKind.Special);
                Check(!p.Alive, "Revive: the hero dies");
                for (int i = 0; i < (int)((SpellDefs.ReviveDelay + 0.1f) / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(p.Alive && Near(p.X, x) && Near(p.Y, y), $"Revive: back where it fell after {SpellDefs.ReviveDelay} s ({p.X:0.0}, {p.Y:0.0})");
                Check(Near(p.Hp, p.MaxHp * SpellDefs.RevivePct, 1f), $"Revive: with 75 % health ({p.Hp / p.MaxHp * 100:0} %)");
                Check(p.Has(St.Invulnerable), "Revive: with the respawn's protection");
                Check(m.SpellLeft(p) > SpellDefs.ReviveCd - 3, "Revive: the cooldown runs");
                p.ClearStatuses();
                m.DealDamage(fo, p, p.MaxHp * 5, DmgKind.Special);
                Check(!p.Alive && p.RespawnT > 5, "Revive: on cooldown the hero waits the normal respawn");
                p.RespawnT = 0.01f; m.Step(PlayerInput.None);
                Check(p.Alive && Near(p.X, p.SpawnX) && p.Hp == p.MaxHp, "Revive: on cooldown it comes back at the base");
            }

            // ---------- Fury ----------
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                var p = m.Player; var fo = m.Foe(HeroId.Knut); var al = m.Ally(HeroId.Bram);
                Place(p, x, y); Place(fo, x + 2, y);
                float plain = m.DealDamage(p, fo, 50, DmgKind.Auto);
                Place(fo, x + 2, y); p.Spell = SpellId.Fury;
                Check(!m.CastSpell(p), "Fury: no button press");
                float fury = m.DealDamage(p, fo, 50, DmgKind.Auto);
                Check(Near(fury, plain * 1.2f, 0.05f), $"Fury: +20 % on heroes ({plain:0.0} -> {fury:0.0})");
                var tw = m.Towers[1]; float th = tw.Hp;
                m.DealDamage(p, tw, 50, DmgKind.Auto); float onTower = th - tw.Hp;
                p.Spell = SpellId.None; tw.Hp = th; m.DealDamage(p, tw, 50, DmgKind.Auto);
                Check(Near(onTower, th - tw.Hp, 0.05f), "Fury: not on towers");
            }

            // ---------- Heavy Bladearmor ----------
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                var p = m.Player; var fo = m.Foe(HeroId.Knut); var fo2 = m.Foe(HeroId.Moa);
                Place(p, x, y); Place(fo, x + 2, y); Place(fo2, x - 2, y); p.Spell = SpellId.Bladearmor;
                Check(m.CastSpell(p), "Bladearmor: cast");
                Check((p.Flags & StatusFlags.Reflecting) != 0, "Bladearmor: the art is told");
                float took = m.DealDamage(fo, p, 40, DmgKind.Auto);
                Check(took > 0 && Near(fo.MaxHp - fo.Hp, took * SpellDefs.BladePct, 0.05f), $"Bladearmor: 75 % back to the hero who hit ({fo.MaxHp - fo.Hp:0.0} of {took:0.0})");
                var tw = m.Towers[1]; float th = tw.Hp;
                m.DealDamage(tw, p, 40, DmgKind.Auto);
                Check(tw.Hp == th, "Bladearmor: nothing back to a tower");
                // two armours: the bounce never bounces again
                fo2.Spell = SpellId.Bladearmor; fo2.SpellAt = 0; m.CastSpell(fo2);
                float p0 = p.Hp, f0 = fo2.Hp;
                m.DealDamage(fo2, p, 20, DmgKind.Auto);
                Check(p0 - p.Hp > 0 && f0 - fo2.Hp > 0 && f0 - fo2.Hp < 20, "Bladearmor: two armours bounce once, no loop");
                // a shield still sends the hit back
                Place(fo, x + 2, y); m.AddStatus(p, St.Shield, 5, 0, 1000);
                m.DealDamage(fo, p, 40, DmgKind.Auto);
                Check(fo.Hp < fo.MaxHp, "Bladearmor: a hit the shield takes still bounces");
                for (int i = 0; i < (int)((SpellDefs.BladeDur + 0.1f) / Rules.Tick); i++) m.Step(PlayerInput.None);
                Place(fo, x + 2, y); p.RemoveAll(St.Shield);
                m.DealDamage(fo, p, 40, DmgKind.Auto);
                Check(fo.Hp == fo.MaxHp, "Bladearmor: it ends after 5 s");
            }

            // ---------- bots ----------
            {
                var seen = new bool[16]; bool recall = false;
                for (int s = 1; s <= 60; s++)
                {
                    var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Bram, HeroId.Fenn }, Enemies = new[] { HeroId.Knut, HeroId.Moa, HeroId.Tikta }, Seed = s, PlayerIsBot = true });
                    foreach (var h in m.Heroes) { if (h == m.Player) continue; seen[(int)h.Spell] = true; recall |= h.Spell == SpellId.Recall; }
                }
                bool all = true; foreach (var sp in SpellDefs.All) if (sp != SpellId.Recall && !seen[(int)sp]) all = false;
                Check(all && !recall, "bots draw every spell but Recall");
            }
        }
    }
}
