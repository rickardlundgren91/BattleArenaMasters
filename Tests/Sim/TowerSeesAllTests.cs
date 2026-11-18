using System;

namespace BAM.Sim
{
    // Towers see everything in their reach (Rickard 2026-11-18 "Torn borde se heroes i stealth", "Dom ska se allt"): an enemy hero
    // in grass or in Fenn's Veil inside a tower's reach is seen, picked and shot like anyone else (Match.IsVisibleTo, TowerSees).
    public sealed partial class Match
    {
        /// <summary>The enemy tower of the player's team with everyone else stunned far away and no minions; null if the map has no
        /// grass inside the tower's reach.</summary>
        static Match SeesScene(HeroId hero, MapId map, out Unit p, out Unit tw, out float gx, out float gy)
        {
            var m = Create(new MatchConfig { PlayerHero = hero, Allies = new[] { HeroId.Moa, HeroId.Tyra }, Enemies = new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta }, Seed = 7, Map = map });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear();
            p = m.Player; tw = m.Towers[1 - p.Team];
            float px = 2;
            foreach (var h in m.Heroes) if (h != p) { m.Still(h, px, m.Map.H / 2f); px += 1.2f; }
            gx = gy = -1;
            // a grass spot inside the reach with a clear line to the tower
            for (float r = tw.R + 1f; r <= tw.Range - 0.3f && gx < 0; r += 0.25f)
                for (int a = 0; a < 72 && gx < 0; a++)
                {
                    float x = tw.X + r * (float)Math.Cos(a * Math.PI / 36), y = tw.Y + r * (float)Math.Sin(a * Math.PI / 36);
                    if (m.Map.InGrass(x, y) && m.Map.InGrass(x + 0.3f, y) && m.Map.InGrass(x - 0.3f, y) && m.Map.InGrass(x, y + 0.3f) && m.Map.InGrass(x, y - 0.3f))
                    { Place(p, x, y); if (Map_LosClear(m, tw, p)) { gx = x; gy = y; } }
                }
            return m;
        }
        static bool Map_LosClear(Match m, Unit a, Unit b) => m.Map.LosClear(a, b);

        static void TowerSeesAllChecks()
        {
            // grass: a hero hidden in grass inside the tower's reach is seen and shot (any map with grass that close)
            bool grassTested = false;
            foreach (var map in new[] { MapId.Runes, MapId.Grove, MapId.Plateau })
            {
                var m = SeesScene(HeroId.Knut, map, out var p, out var tw, out float gx, out float gy);
                if (gx < 0) continue;
                grassTested = true;
                m.Still(p, gx, gy);
                Check(m.Map.InGrass(p.X, p.Y) && p.RevealT <= 0, $"TOWER SEES ALL ({map}): the hero stands hidden in grass");
                Check(m.IsVisibleTo(p, tw.Team), $"TOWER SEES ALL ({map}): the tower's team sees a hero in grass inside its reach");
                bool hit = false;
                for (int k = 0; k < 120; k++) { m.Step(PlayerInput.None); if (m.Events.Exists(e => e.Type == EventType.Damage && e.A == tw && e.B == p)) hit = true; }
                Check(tw.Target == p && hit, $"TOWER SEES ALL ({map}): the tower shoots the hero in grass");
                break;
            }
            Check(grassTested, "TOWER SEES ALL: some map has grass inside a tower's reach to test with");

            // the Veil: a veiled Fenn walking into the reach is seen, locked and shot; just outside it nobody sees him
            {
                var m = SeesScene(HeroId.Fenn, MapId.Runes, out var f, out var tw, out _, out _);
                float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
                m.Still(f, tw.X, tw.Y + dir * (tw.Range + f.R + 1.5f));
                m.CastFennVeil(f); m.FlushClones();
                var decoy = m.Units.Find(u => u.IsClone && u.Alive && u.Owner == f); if (decoy != null) decoy.Alive = false;
                f.AddRaw(St.Stun, 999, 0, 0, null, false);
                Check(!m.IsVisibleTo(f, tw.Team), "TOWER SEES ALL: a veiled Fenn outside the reach stays unseen");
                f.X = f.PX = tw.X; f.Y = f.PY = tw.Y + dir * (tw.Range - 1.5f);
                Check(m.IsVisibleTo(f, tw.Team) && !m.HiddenFromFoes(f), "TOWER SEES ALL: a veiled Fenn inside the reach is seen");
                bool hit = false;
                for (int k = 0; k < 150; k++) { m.Step(PlayerInput.None); if (m.Events.Exists(e => e.Type == EventType.Damage && e.A == tw && e.B == f)) hit = true; }
                Check(f.Has(St.Veil) && tw.Target == f && m.TowerLocked(tw) && hit, "TOWER SEES ALL: the tower locks on the veiled Fenn and shoots him");
            }
        }
    }
}
