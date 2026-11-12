using System;
namespace BAM.Sim
{
    public sealed partial class Match
    {
        /// <summary>Tester feedback 2026-11-12 on Ragn's Moon Leap ("close to an enemy it doesn't seem to have the expected effect";
        /// Rickard: "ragn charmade upp lite och rörde sig men ingen special hände", "träffade honom uppifrån och ner men han blev
        /// knockbackad uppåt"). With the red warning first, an enemy walking away was always gone by the landing, and one walking at
        /// him was shoved back the way he came. Now the tap and the leap aim where a walking hero will be, and the shove goes the
        /// way he rushed.</summary>
        public static void RagnWalkTable()
        {
            var hero = HeroId.Ragn;
            foreach (var dir in new[] { "away", "side", "toward", "still" })
            foreach (float d in new[] { 1.5f, 3f, 4.5f })
            {
                var m = OneTapScene(hero, out var p, out float cx, out float cy);
                m.Telegraph = true;
                Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                float dd = Math.Min(d, TapReach(hero) - 0.3f);
                float sx = dir == "away" ? -0.75f : dir == "toward" ? 0.75f : 0, sy = dir == "side" ? -0.75f : 0;
                Place(e, p.X + Math.Min(dd + sx, TapReach(hero) - 0.3f), p.Y + sy); m.AddStatus(e, St.Stun, 999);   // the scene has a wall just past his reach
                int hit = 0; float hx = 0;
                for (int k = 0; k < 120; k++)
                {   // it walks for 0.3 s before the tap, then keeps walking
                    if (dir == "away") m.MoveUnit(e, 1, 0, Rules.Tick); else if (dir == "toward") m.MoveUnit(e, -1, 0, Rules.Tick); else if (dir == "side") m.MoveUnit(e, 0, 1, Rules.Tick);
                    m.Step(k == 18 ? TapIn() : PlayerInput.None);
                    foreach (var ev in m.Events) { if (ev.Type == EventType.Hit && ev.B == e && ev.Dmg == DmgKind.Special) hit = 1; if (ev.Type == EventType.Knock && ev.B == e) hx = ev.X - e.X; }
                }
                Check(hit == 1, $"RAGN: a hero walking {dir}, {d} tiles off, is met by the tapped Moon Leap");
                if (hit == 0) continue;
                if (dir != "side") Check(hx > 0.5f, $"RAGN: the hero walking {dir} is shoved the way he rushed ({hx:0.00} tiles)");
            }
        }
    }
}
