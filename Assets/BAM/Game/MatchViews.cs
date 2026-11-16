using System.Collections.Generic;
using BAM.Art;
using BAM.Audio;
using BAM.Sim;
using UnityEngine;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>Map space (tiles, y down the map) to world space (x right, z up the screen).</summary>
    static class Coords
    {
        /// <summary>The map's height in tiles (set when a match starts; the map's row 0 is at the top of the screen).</summary>
        public static float H = 35f;
        public static Vector3 World(float x, float y) => new Vector3(x, 0f, H - y);
        public static Vector3 Lerp(float px, float py, float x, float y, float a) => new Vector3(px + (x - px) * a, 0f, H - (py + (y - py) * a));
        public static Vector3 Dir(float facing) => new Vector3(Mathf.Cos(facing), 0f, -Mathf.Sin(facing));
        public static Vector3 Of(Unit u) => World(u.X, u.Y);

        /// <summary>Online, a player of the top team sees the map from the other end (MatchCamera turns half a circle), so a
        /// direction on the screen points the other way on the map. Set by GameFlow at match start.</summary>
        public static bool Turned;
        /// <summary>A screen direction (x right, y up) as the camera sees it: turned half a circle when Turned.</summary>
        public static Vector2 Screen(Vector2 v) => Turned ? -v : v;
        /// <summary>The rules' angle of a screen direction (screen up is -y on the map, or +y when Turned). The camera looks down at
        /// 55 degrees, so the map's depth shows 0.82 as long on screen: that is undone here, so a drag aims where the finger points
        /// (the old HudAim: atan2(-dy / YS, dx); Precision COORDS-02). The joystick does not use it (the old game did not either).</summary>
        public static float ScreenAngle(Vector2 v) { v = Screen(v); return Mathf.Atan2(-v.y / DepthOnScreen, v.x); }
        /// <summary>The sine of the camera's elevation (MatchCamera: asin 0.82): one tile of map depth is this long on screen.</summary>
        public const float DepthOnScreen = 0.82f;
    }

    /// <summary>
    /// Draws the rules with ArtKit: one pooled view per unit, shot and zone, keyed by Serial, released when it is gone.
    /// Reads the events after every Step and turns them into animations, effects and sounds. Every attack is shown
    /// (ARCHITECTURE rule 5): PlayAttack on every AttackStart, a projectile or slash for every shot (even one that starts and
    /// lands inside one tick), and a hit effect on every Hit.
    /// </summary>
    sealed class MatchViews
    {
        sealed class UnitRec
        {
            public Unit U;
            public int Serial, Seen;
            public UnitView V;
            public bool Dead;
            public float Hp01 = -1f, Shield01 = -1f, Vis = -1f, Linger;
            public int Level;
            public float MaxHp = -1f, Energy = -2f; // shown on the bar: the notches' max health, the special charge row
            public bool Ready;
            public StatusFlags Flags;
            public float Channel = -1f;    // the recall channel shown, -1 none
            public bool Sprint, Four;
            public int Form = -1;          // the body's form shown (UnitView.SetForm), -1 never told
            public bool Marked, Spark;     // Tikta's Deathmark and Blast's chain spark shown (Tikta 2.0 / Blast 2.0)
            public float ChainCd = -1f;    // the player's Blast: the chain cooldown arc shown, -1 none
            public float StoredFx;         // Oaken: when his Stored Impact glow was last puffed (OAKEN-02)
            public int Honey = HoneyOff;   // Moa's shield rings shown: lit cells per ring (HoneyKey)
            public Quaternion Rot;
            public float FixX, FixY;       // online guest: a correction of the guess, drawn away over ~0.1 s instead of a jump
            public float JoltX, JoltY, JoltT, JoltLen; // the player's own hit shoves the target a little away (OwnHit), map tiles; JoltT left of JoltLen s
            public bool Squared;           // the view was told it faces a target or an aim, not its path (AnimFix.LegsFollow)
            public MeshRenderer Bar;       // the art's health bar (its child named "Bar"), for the HUD's marks over it
        }

        sealed class ShotRec
        {
            public Proj P;
            public int Serial, Seen, SrcSerial;
            public ProjKind Kind;
            public int Team;
            public ProjectileView V;
            public bool Melee;
            public float LastT, LastX, LastY;
            // a ghost is drawn without the rules: a shot that ended before it was seen, a melee slash finishing its swing, a lob
            public bool Ghost;
            public bool Hit;               // the rules hit with it (MarkHit given)
            public bool EverHit;           // it hurt someone while flying on (Tyra's volley passes through): ends quietly
            public Vector3 From, To;
            public float Age, Flight, Linger;
            public bool Fresh;             // a ghost started this frame: it starts aging next frame
            public float DustT;            // Knut's hook: time to the next dust puff under its catch
        }

        sealed class ZoneRec
        {
            public int Serial, Seen;
            public ZoneView V;
            public int FollowShot; // Tyra's bolt warning: the bolt whose tip its fill follows (0 = none)
        }

        struct Pending { public int A; public Unit Src, Tgt; public ProjKind Kind; public bool Hit; public Vector3 At; }

        const float MinionLinger = 0.9f;   // the death animation finishes after the rules drop a minion
        const float SlashLinger = 0.22f;   // a melee slash finishes its sweep after the hit

        readonly Match m;
        // the team of this phone's player: its side is drawn blue and the other red (online the player may be on the top team)
        int Mine => m.Player != null ? m.Player.Team : Teams.Player;
        int Side(int team) => team == Mine ? Teams.Player : Teams.Enemy;
        readonly List<UnitRec> units = new List<UnitRec>(128);
        readonly Dictionary<int, UnitRec> unitBySerial = new Dictionary<int, UnitRec>(128);
        readonly List<UnitRec> dying = new List<UnitRec>(32);
        readonly List<ShotRec> shots = new List<ShotRec>(96);
        readonly Dictionary<int, ShotRec> shotBySerial = new Dictionary<int, ShotRec>(96);
        readonly List<ZoneRec> zones = new List<ZoneRec>(16);
        readonly Dictionary<int, ZoneRec> zoneBySerial = new Dictionary<int, ZoneRec>(16);
        readonly List<Pending> pending = new List<Pending>(32);
        readonly Stack<UnitRec> unitRecPool = new Stack<UnitRec>(64);
        readonly Stack<ShotRec> shotRecPool = new Stack<ShotRec>(64);
        // this tick's hits and wall stops by the shot that made them (SimEvent.Shot): a shot the rules drop in the same tick landed,
        // so exactly that shot plays its splash (ProjectileView.MarkHit) where it hit; a shot dropped without one fizzles as a miss.
        // Keyed by shot, not shooter + kind: Tyra's five volley arrows end in one tick (precision SHOT-01, TYRA-07, TIKTA-02)
        readonly Dictionary<int, Vector3> shotHitAt = new Dictionary<int, Vector3>();
        readonly Dictionary<int, Vector3> shotStopAt = new Dictionary<int, Vector3>();
        readonly List<SimEvent> shotHits = new List<SimEvent>(16); // each shot's first Hit this tick, for shots the view never saw
        readonly HashSet<int> droppedNow = new HashSet<int>();
        const float EndFlight = 0.05f; // a dropped shot flies the last bit to where the rules ended it before its splash (SHOT-04)
        readonly Stack<ZoneRec> zoneRecPool = new Stack<ZoneRec>(16);
        int step;
        int lastWave;
        bool playerWasReady, playerInGrass, playerLow;
        // the match-feel sounds (Speltest HeroJuice.cs, Sound.cs sound mix v2): each hero's last kill for Double/Triple Kill, the
        // shots in a row a tower has put on the player, and the player's own tower's health while the player is far from it
        readonly Dictionary<int, float> killAt = new Dictionary<int, float>();
        readonly Dictionary<int, int> killRun = new Dictionary<int, int>();
        int towerRunSerial = -1, towerRun;
        float towerRunAt = -99f, ownTowerHp = -1f;
        GameObject arena;
        bool bridgeShown;

        public MatchViews(Match match)
        {
            m = match;
            Coords.H = m.Map.H;
            lastWave = m.WaveN;
            BuildArena();
            SyncUnits();
        }

        bool Island => m.Tut != null && m.Tut.Island;
        bool roadShown; // the start-of-match road light ran (ArtKit.RoadRunesShow)

        // The training island's bridge and the lane below it appear only when the bridge opens: until then the arena is
        // built with those rows left out, then built again whole.
        void BuildArena()
        {
            bool rebuild = arena != null;
            if (rebuild) Object.Destroy(arena);
            bridgeShown = !Island || m.Tut.BridgeOpen;
            var info = ArenaInfoOf(m.Map, bridgeShown ? int.MaxValue : IslandPlatformRows, !Island);
            info.Island = Island;
            info.Turned = Coords.Turned;
            info.LaneRises = Island && rebuild; // the opened bridge and lane rise into place
            if (Island && bridgeShown)
            {
                // the island's boost pad stands on the bridge's last row and throws on down the map, out over the sky (TutorialIsland.cs)
                var tu = m.Tut;
                info.Pads = new[] { Coords.World(tu.PadX, tu.PadY) };
                info.PadTo = new[] { Coords.World(tu.PadX, tu.PadY + Tutorial.PadThrow) };
            }
            arena = ArtKit.BuildArena(info);
        }

        const int IslandPlatformRows = 17; // the island's round platform is rows 0-16 (TutorialIsland.cs)

        /// <summary>The static map for ArtKit.BuildArena: rows from rowsShown on are left out (void); towers optional.</summary>
        static ArenaInfo ArenaInfoOf(Arena map, int rowsShown, bool towers)
        {
            var tiles = new byte[map.W * map.H];
            System.Array.Copy(map.Tiles, tiles, tiles.Length);
            for (int i = Mathf.Min(rowsShown, map.H) * map.W; i < tiles.Length; i++) tiles[i] = Arena.Void;
            var rocks = new Vector3[map.RockR.Length];
            for (int i = 0; i < rocks.Length; i++)
                rocks[i] = new Vector3((map.RockX0[i] + map.RockX1[i]) * 0.5f, map.H - (map.RockY0[i] + map.RockY1[i]) * 0.5f, map.RockR[i] + 0.15f);
            Vector3[] pads = null, padTo = null;
            if (map.PadCount > 0)
            {
                pads = new Vector3[map.PadCount]; padTo = new Vector3[map.PadCount];
                for (int i = 0; i < map.PadCount; i++) { pads[i] = Coords.World(map.PadX[i], map.PadY[i]); padTo[i] = Coords.World(map.PadToX[i], map.PadToY[i]); }
            }
            return new ArenaInfo
            {
                Width = map.W, Height = map.H, Tiles = tiles, Rocks = rocks, Pads = pads, PadTo = padTo,
                Ground = map.Id == MapId.Grove ? "T_BAM_ArenaGround_Grove" : null,
                // the island parks its towers off the map: no tower bases there
                Towers = towers ? new[] { Coords.World(map.TowerX[0], map.TowerY[0]), Coords.World(map.TowerX[1], map.TowerY[1]) } : null,
            };
        }

        /// <summary>The view of a unit (null when it has none), e.g. for the camera or the aim.</summary>
        public Transform ViewOf(Unit u) => u != null && unitBySerial.TryGetValue(u.Serial, out UnitRec r) ? r.V.transform : null;

        /// <summary>Where the unit's health bar is drawn (its centre) and how wide it is; false when it has none or is not shown.</summary>
        public bool BarOf(Unit u, out Vector3 centre, out float width)
        {
            centre = Vector3.zero; width = 0f;
            if (u == null || !unitBySerial.TryGetValue(u.Serial, out UnitRec r) || r.Bar == null || !r.Bar.enabled || r.Vis <= 0f) return false;
            centre = r.Bar.transform.position;
            width = Mathf.Max(0.3f, r.Bar.bounds.size.x - 1f); // the bar's bounds are its width plus one
            return true;
        }

        /// <summary>Units the match intro keeps out of sight for now (null = none).</summary>
        public System.Predicate<Unit> Hidden;

        /// <summary>Where a unit is drawn now (between ticks).</summary>
        public Vector3 DrawnAt(Unit u)
        {
            if (u != null && unitBySerial.TryGetValue(u.Serial, out UnitRec r)) return r.V.transform.position;
            return u != null ? Coords.Of(u) : Vector3.zero;
        }

        // ---------------------------------------------------------------- after every Step

        /// <summary>
        /// Online guest with prediction: the shown match was copied from the confirmed one again and run ahead anew, so units and
        /// shots made in the guessed part are new objects with the same serials. Points the views at them (no events, nothing ends).
        /// </summary>
        // the units' spots before the shown match was copied again (BeginCorrect), by serial, to see how far each one moved
        readonly Dictionary<int, Vector2> before = new Dictionary<int, Vector2>(96);

        /// <summary>Online guest: remembers where every unit is now, before the shown match is copied anew and run back to this frame.</summary>
        public void BeginCorrect()
        {
            before.Clear();
            var us = m.Units;
            for (int i = 0; i < us.Count; i++) before[us[i].Serial] = new Vector2(us[i].X, us[i].Y);
        }

        /// <summary>Online guest: the shown match is back at the frame of BeginCorrect. A unit that is now somewhere else was guessed
        /// wrong (another player turned): it is drawn from where it was and slides to its spot, unless it is far (a blink, a respawn).</summary>
        public void EndCorrect()
        {
            var us = m.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (!before.TryGetValue(u.Serial, out Vector2 b) || !unitBySerial.TryGetValue(u.Serial, out UnitRec r)) continue;
                float dx = b.x - u.X, dy = b.y - u.Y;
                if (dx * dx + dy * dy > 4f) continue;
                r.FixX += dx; r.FixY += dy;
            }
            before.Clear();
        }

        public void Rebind()
        {
            var us = m.Units;
            for (int i = 0; i < us.Count; i++) if (unitBySerial.TryGetValue(us[i].Serial, out UnitRec r)) r.U = us[i];
            var ps = m.Projs;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p.Done || !p.IsShot) continue;
                if (shotBySerial.TryGetValue(p.Serial, out ShotRec s) && s.P != null) s.P = p;
            }
        }

        public void AfterStep()
        {
            step++;
            pending.Clear();
            shotHitAt.Clear(); shotStopAt.Clear(); shotHits.Clear();
            var ev = m.Events;
            for (int i = 0; i < ev.Count; i++) { tickNow = ev[i].Type == EventType.Damage && IsTick(ev, i); OnEvent(ev[i]); }
            if (!bridgeShown && m.Tut.BridgeOpen) BuildArena();
            SyncUnits();
            SyncProjs();
            PlayerCues();
        }

        void SyncUnits()
        {
            var list = m.Units;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                if (u.Kind == UnitKind.Tower && Island) continue; // parked off the island, never drawn
                if (!unitBySerial.TryGetValue(u.Serial, out UnitRec r)) r = AddUnit(u);
                r.Seen = step;
            }
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var r = units[i];
                if (r.Seen == step) continue;
                units.RemoveAt(i);
                unitBySerial.Remove(r.Serial);
                if (r.U.IsClone) { ArtKit.Release(r.V); FreeUnit(r); continue; }
                if (!r.Dead) { r.Dead = true; r.V.SetDead(true); }
                r.Linger = MinionLinger;
                dying.Add(r);
            }
        }

        // the file-name part of a hero's own sounds (HeroDeath_Nassla): the HeroId names, made once
        static readonly string[] heroVoice = System.Enum.GetNames(typeof(HeroId));
        static string HeroVoice(HeroId h) => (int)h < heroVoice.Length ? heroVoice[(int)h] : null;

        int SkinOf(Unit u)
        {
            var who = u.IsClone ? u.Owner : u;
            if (who == null) return 0;
            if (m.Player != null && who == m.Player) return HeroSkins.Chosen(u.Hero);
            var seats = m.Config.Seats;
            return seats != null && who.Seat >= 0 && who.Seat < seats.Length ? HeroSkins.Valid(u.Hero, seats[who.Seat].Skin) : 0;
        }

        UnitRec AddUnit(Unit u)
        {
            UnitView v;
            switch (u.Kind)
            {
                case UnitKind.Hero:
                    // the player's own hero wears the skin picked on Home, every other player's hero online the one its phone sent
                    // (SeatConfig.Skin); Fenn's decoy wears its owner's; bots their plain body (Rickard 2026-11-15)
                    v = ArtKit.CreateHero(u.Hero, Side(u.Team), SkinOf(u));
                    if (!u.IsClone) BamAudio.Preload(SfxId.HeroDeath, HeroVoice(u.Hero)); // its own death sound is ready before it dies
                    break;
                case UnitKind.Tower: v = ArtKit.CreateTower(Side(u.Team)); break;
                default: v = u == TutDummy ? ArtKit.CreateTarget() : ArtKit.CreateMinion(u.Minion, Side(u.Team)); break; // the guided match's practice target is a board
            }
            var r = unitRecPool.Count > 0 ? unitRecPool.Pop() : new UnitRec();
            r.U = u; r.Serial = u.Serial; r.V = v; r.Dead = false; r.Linger = 0f;
            r.Hp01 = -1f; r.Shield01 = -1f; r.Vis = -1f; r.Level = 0; r.MaxHp = -1f; r.Energy = -2f; r.Ready = false; r.Flags = (StatusFlags)0xFFFF; r.Honey = HoneyOff; r.Form = -1;
            r.JoltT = 0f;
            r.Rot = Quaternion.LookRotation(Coords.Dir(u.Facing));
            r.Bar = null;
            foreach (var mr in v.GetComponentsInChildren<MeshRenderer>(true)) if (mr.gameObject.name == "Bar") { r.Bar = mr; break; }
            var t = v.transform;
            t.SetPositionAndRotation(Coords.Of(u), r.Rot);
            if (u.Kind == UnitKind.Hero)
                v.SetMarker(u.IsPlayer ? UnitMarker.Player : u.Team == Mine ? UnitMarker.Ally : UnitMarker.Enemy);
            if (!u.Alive) { r.Dead = true; v.SetDead(true); }
            else if (u.Kind == UnitKind.Minion && m.Time > 0f && u != TutDummy) { ArtKit.PlayFx(FxKind.MinionSpawn, t.position, 1f, Side(u.Team)); ArtKit.RoadRunesOff(); }
            units.Add(r);
            unitBySerial[u.Serial] = r;
            return r;
        }

        Unit TutDummy => m.Tut != null ? m.Tut.Dummy : null;

        void FreeUnit(UnitRec r) { r.U = null; r.V = null; unitRecPool.Push(r); }

        void SyncProjs()
        {
            var list = m.Projs;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Done) continue;
                if (p.IsShot)
                {
                    if (!shotBySerial.TryGetValue(p.Serial, out ShotRec s)) s = AddShot(p);
                    s.Seen = step;
                    if (shotHitAt.ContainsKey(p.Serial)) s.EverHit = true;
                    s.LastT = p.T; s.LastX = p.X; s.LastY = p.Y;
                    if (s.V == null && p.T >= 0f) LaunchView(s, p);
                }
                else if (p.IsZone || p.IsWarning)
                {
                    // a hidden Tikta's drag strip and a hidden Moa's honey field are not drawn for her foes: dropped while she is
                    // hidden, drawn again (with the time it has left) once she is seen (TIKTA-05)
                    if (m.CastHiddenFrom(p, Mine)) continue;
                    if (!zoneBySerial.TryGetValue(p.Serial, out ZoneRec z)) z = AddZone(p);
                    z.Seen = step;
                    // a warning that follows its caster (Tikta's line while her needles are in her hand, TIKTA-01)
                    if (p.IsWarning && (p.X != p.PX || p.Y != p.PY)) { Vector3 w = Coords.World(p.X, p.Y); var tr = z.V.transform; tr.position = new Vector3(w.x, tr.position.y, w.z); }
                }
            }

            // shots the rules dropped this tick
            droppedNow.Clear();
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                if (s.Ghost || s.Seen == step) continue;
                shotBySerial.Remove(s.Serial);
                droppedNow.Add(s.Serial);
                HookLanded(s.P);
                s.Hit = shotHitAt.TryGetValue(s.Serial, out Vector3 hitAt);
                // where the rules ended it: on what it hit, at the wall, else its own last spot (its Proj is pooled, not yet reused)
                Vector3 end = s.Hit ? hitAt : shotStopAt.TryGetValue(s.Serial, out Vector3 stop) ? stop
                    : s.P != null ? Coords.World(s.P.X, s.P.Y) : Coords.World(s.LastX, s.LastY);
                s.P = null;
                if (s.V != null) { if (s.Hit) s.V.MarkHit(); else if (s.EverHit) s.V.MarkSpent(); }
                if (s.V == null)
                {
                    // it left the hand and landed inside one tick, so it was never drawn: show the flight now
                    // (its last seen spot is still in the hand, so it flies to where it ended, not shooter to shooter)
                    if (s.LastT + Rules.Tick >= 0f) { StartGhost(s, From(s.SrcSerial, s.LastX, s.LastY), end, 0.1f, s.Melee ? SlashLinger : 0f); ReleaseSound(s.SrcSerial, s.Kind == ProjKind.TyraBolt ? ProjMode.Bolt : s.Kind == ProjKind.TyraVolley ? ProjMode.Volley : ProjMode.Auto); }
                    else { shots.RemoveAt(i); FreeShot(s); } // cancelled in the hand (stun, death): nothing flew
                }
                else if (s.Melee) StartGhost(s, s.V.transform.position, s.V.transform.position, 0f, SlashLinger);
                // drawn: it sits a tick or two short of where the rules ended it (skillshots stop at their hit box, up to a tile in
                // front of the target), so it flies that last bit before its splash, which then plays on the hit (SHOT-04, TYRA-06);
                // this also shows a shot launched and dropped inside one frame (60 Hz ticks at 30 fps)
                else StartGhost(s, s.V.transform.position, end, EndFlight, 0f);
            }

            // a shot the view never saw (born and ended inside this tick) that hit: its AttackStart's ghost flies to the hit, and
            // one with no AttackStart (Tikta's first drag needle point blank, SHOT-06) gets a ghost of its own, so every attack shows
            for (int k = 0; k < shotHits.Count; k++)
            {
                var e = shotHits[k];
                if (droppedNow.Contains(e.Shot) || shotBySerial.ContainsKey(e.Shot) || e.A == null) continue;
                Vector3 at = Coords.World(e.X, e.Y);
                bool paired = false;
                for (int i = 0; i < pending.Count && !paired; i++)
                {
                    var pd = pending[i];
                    if (pd.A != e.A.Serial || pd.Kind != (ProjKind)e.Kind || pd.Hit) continue;
                    pd.Hit = true; pd.At = at; pending[i] = pd; paired = true;
                }
                if (paired) continue;
                var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
                Init(s, null, 0, (ProjKind)e.Kind, e.A.Team, e.A.Serial);
                s.Hit = true;
                shots.Add(s);
                StartGhost(s, DrawnAt(e.A), at, 0.1f, s.Melee ? SlashLinger : 0f);
            }

            // an attack whose shot started and ended inside this very tick never showed up in Projs
            for (int i = 0; i < pending.Count; i++)
            {
                var pd = pending[i];
                if (pd.A == 0) continue;
                var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
                Init(s, null, 0, pd.Kind, pd.Src.Team, pd.Src.Serial);
                s.Hit = pd.Hit;
                shots.Add(s);
                Vector3 to = pd.Hit ? pd.At : pd.Tgt != null ? Coords.Of(pd.Tgt) : Coords.Of(pd.Src);
                StartGhost(s, Coords.Of(pd.Src), to, 0.1f, s.Melee ? SlashLinger : 0f);
                ReleaseSound(pd.Src.Serial, ProjMode.Auto);
            }

            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                if (z.Seen == step) continue;
                zones.RemoveAt(i);
                zoneBySerial.Remove(z.Serial);
                ArtKit.Release(z.V);
                z.V = null;
                zoneRecPool.Push(z);
            }
        }

        ShotRec AddShot(Proj p)
        {
            var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
            Init(s, p, p.Serial, p.Mode == ProjMode.Volley && p.Kind == ProjKind.TyraArrow ? ProjKind.TyraVolley : p.Kind, p.Team, p.Src != null ? p.Src.Serial : 0);
            shots.Add(s);
            shotBySerial[p.Serial] = s;
            // pair it with the AttackStart of this tick, so that attack is not drawn twice (any mode: Tikta's storm needles too)
            if (p.Src != null)
                for (int i = 0; i < pending.Count; i++)
                    if (pending[i].A == p.Src.Serial && pending[i].Kind == p.Kind) { var pd = pending[i]; pd.A = 0; pending[i] = pd; break; }
            return s;
        }

        static void Init(ShotRec s, Proj p, int serial, ProjKind kind, int team, int src)
        {
            s.P = p; s.Serial = serial; s.Kind = kind; s.Team = team; s.SrcSerial = src; s.V = null; s.Ghost = false;
            s.Melee = kind == ProjKind.FennSlash || kind == ProjKind.KnutHit || kind == ProjKind.WolfBite || kind == ProjKind.MeleeMinionHit
                || kind == ProjKind.BonkClub || kind == ProjKind.OakenFist;
            s.Age = 0f; s.Flight = 0f; s.Linger = 0f; s.LastT = -1f; s.Hit = false; s.EverHit = false;
        }

        void FreeShot(ShotRec s) { s.P = null; s.V = null; s.DustT = 0f; shotRecPool.Push(s); }

        // Knut's hook hauling its catch (the hook feel list, 2026-11-02): dust kicked up under the dragged hero while the reel
        // runs, and a thud with a dust ring where it lands at Knut's feet
        const float HookDustEvery = 0.05f;
        void HookDust(ShotRec s, Proj p, float dt)
        {
            var c = p.Caught;
            if (p.Out || c == null || !c.Alive || p.D > p.Travel - 0.05f || !m.IsVisibleTo(c, Mine)) return;
            s.DustT -= dt;
            if (s.DustT > 0f) return;
            s.DustT = HookDustEvery;
            ArtKit.PlayFx(FxKind.HookDrag, Coords.Of(c), 1f, Side(c.Team));
        }

        void HookLanded(Proj p)
        {
            if (p == null || p.Mode != ProjMode.Hook || p.Caught == null || !p.Caught.Alive || !m.IsVisibleTo(p.Caught, Mine)) return;
            Vector3 at = Coords.Of(p.Caught);
            ArtKit.PlayFx(FxKind.GroundSmash, at, 0.45f, Side(p.Caught.Team));
            BamAudio.PlayAt(SfxId.KnutHookLand, at); // Rickard's own clip (O-046)
        }

        Vector3 From(int srcSerial, float x, float y) =>
            unitBySerial.TryGetValue(srcSerial, out UnitRec r) ? r.V.transform.position : Coords.World(x, y);

        void LaunchView(ShotRec s, Proj p)
        {
            var v = ArtKit.CreateProjectile(s.Kind, Side(p.Team)); // s.Kind: the volley's arrows wear their own look
            s.V = v;
            Vector3 from = Coords.World(p.PX, p.PY), to;
            float flight;
            if (p.Mode == ProjMode.Auto && p.Tgt != null)
            {
                to = Coords.Of(p.Tgt);
                float d = Vector3.Distance(from, to);
                flight = p.Speed > 0f ? d / p.Speed : Mathf.Max(0.05f, p.Travel - p.T);
            }
            else
            {
                float reach = p.D > 0.5f ? p.D : 4f;
                to = from + Coords.Dir(p.Ang) * reach;
                flight = p.Speed > 0f ? reach / p.Speed : 0.4f;
            }
            v.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Flat(to - from)));
            v.Launch(from, to, flight);
            if (MuzzleOf(s.SrcSerial, out Vector3 mz))
            {
                v.LaunchFrom(mz); // from the hand or weapon, easing onto the rules' path (SHOT-02)
                if (s.Kind == ProjKind.TyraVolley) VolleyRelease(s.SrcSerial, mz);
            }
            ReleaseSound(s.SrcSerial, p.Mode);
            if (p.Mode == ProjMode.Hook && p.Src != null) v.SetTether(ViewOf(p.Src));
        }

        void StartGhost(ShotRec s, Vector3 from, Vector3 to, float flight, float linger)
        {
            s.Ghost = true; s.From = from; s.To = to; s.Flight = flight; s.Linger = linger; s.Age = 0f; s.Fresh = true;
            if (s.V == null)
            {
                s.V = ArtKit.CreateProjectile(s.Kind, Side(s.Team));
                s.V.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Flat(to - from)));
                s.V.Launch(from, to, Mathf.Max(flight, 0.05f));
                if (!s.Melee && MuzzleOf(s.SrcSerial, out Vector3 mz)) s.V.LaunchFrom(mz);
                if (s.Hit) s.V.MarkHit();
            }
        }

        ZoneRec AddZone(Proj p)
        {
            var z = zoneRecPool.Count > 0 ? zoneRecPool.Pop() : new ZoneRec();
            z.Serial = p.Serial;
            z.V = ArtKit.CreateZone(p.Zone, Side(p.Team));
            z.V.Show(Coords.World(p.X, p.Y), Coords.Dir(p.Ang), p.ZoneLength, p.Radius, Mathf.Max(0.05f, p.TimeLeft));
            if (p.Kind == ProjKind.HildeKeg) z.V.SetPush(p.Radius + HildeS.KegKnock); // which way her keg throws who it hits (HILDE-02)
            if (p.Zone == ZoneKind.BombFuse) CutFuse(z.V, p); // BLAST-05
            // Tyra's bolt (Rickard 2026-10-29): the strip's fill rides just behind the bolt's tip instead of racing ahead of it
            z.FollowShot = 0;
            if (p.Mode == ProjMode.WarnLine && p.WarnFor != 0)
                for (int i = 0; i < m.Projs.Count; i++)
                    if (m.Projs[i].Serial == p.WarnFor && m.Projs[i].Kind == ProjKind.TyraBolt) { z.FollowShot = p.WarnFor; z.V.SetFront(0f); break; }
            zones.Add(z);
            zoneBySerial[p.Serial] = z;
            return z;
        }

        // a bomb or keg fuse cut where its blast stops at walls (BLAST-05); left round on open ground
        readonly float[] fuseReach = new float[48];
        readonly Vector3[] fuseRim = new Vector3[48];
        void CutFuse(ZoneView v, Proj p)
        {
            m.BlastReach(p.X, p.Y, p.Radius, fuseReach);
            bool cut = false;
            for (int i = 0; i < fuseReach.Length; i++) if (fuseReach[i] < p.Radius - 1e-3f) cut = true;
            if (!cut) return;
            Vector3 c = Coords.World(p.X, p.Y);
            for (int i = 0; i < fuseReach.Length; i++)
            {
                float a = i * 2f * Mathf.PI / fuseReach.Length, d = Mathf.Max(0.05f, fuseReach[i]);
                fuseRim[i] = Coords.World(p.X + Mathf.Cos(a) * d, p.Y + Mathf.Sin(a) * d) - c;
            }
            v.CutTo(fuseRim);
        }

        static Vector3 Flat(Vector3 d)
        {
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d : Vector3.forward;
        }

        // ---------------------------------------------------------------- events

        /// <summary>Whose colours a hit wears: the hero, or the hero who owns the wolf or clone; -1 for minions and towers.</summary>
        static int HeroOf(Unit a)
        {
            if (a == null) return -1;
            if (a.Kind == UnitKind.Hero) return (int)a.Hero;
            return a.Owner != null && a.Owner.Kind == UnitKind.Hero ? (int)a.Owner.Hero : -1;
        }

        bool tickNow; // the event being read is a burn or poison tick

        /// <summary>Damage event i is a damage-over-time tick: passive damage with no Hit of the same attacker on the same target
        /// right after it in the tick (a hit's Damage is always followed by its Hit, Combat.DealDamage then Emit Hit).</summary>
        static bool PoisonedBy(Unit u, Unit by)
        {
            if (u == null || by == null) return false;
            for (int k = 0; k < u.StatusCount; k++)
            {
                var st = u.GetStatus(k);
                if (st.Type == St.Poison && st.By == by) return true;
            }
            return false;
        }

        public static bool IsTick(System.Collections.Generic.List<SimEvent> ev, int i)
        {
            var e = ev[i];
            if (e.Type != EventType.Damage || e.Dmg == DmgKind.Auto) return false;
            // burn ticks are passive damage; Tikta's poison ticks are special damage from a poison she put on the target
            if (e.Dmg == DmgKind.Special && !PoisonedBy(e.B, e.A)) return false;
            for (int j = i + 1; j < ev.Count && j <= i + 3; j++)
            {
                var h = ev[j];
                if (h.Type == EventType.Damage) break;
                if (h.Type == EventType.Hit && h.ASerial == e.ASerial && h.BSerial == e.BSerial) return false;
            }
            return true;
        }

        UnitView V(Unit u) => u != null && unitBySerial.TryGetValue(u.Serial, out UnitRec r) ? r.V : null;

        void OnEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case EventType.AttackStart:
                {
                    var a = e.A;
                    if ((ProjKind)e.Kind != ProjKind.KuggSpark) V(a)?.PlayAttack(e.V); // Kugg's Spark Shot fires by itself: no swing
                    // a swing by a hider the player cannot see is not heard: attacking does not reveal, only a hit does (BONK-09)
                    bool heard = m.IsVisibleTo(a, Mine);
                    // Bonk's club has no shot: it lands e.V s later as an Explosion of ProjKind.BonkClub (cracks and dust)
                    if ((ProjKind)e.Kind == ProjKind.BonkClub) { if (heard) Sound(SfxId.BonkSlamWindup, a, e.Big ? 0.8f : 0.55f); break; }
                    pending.Add(new Pending { A = a.Serial, Src = a, Tgt = e.B, Kind = (ProjKind)e.Kind });
                    if (a.Kind == UnitKind.Tower) ArtKit.TowerFired(Coords.Of(a), Side(a.Team)); // its plaza's runes flash
                    if (!heard) break;
                    // a tower's shot is heard only when it aims at you (Speltest), sharper from its third shot in a row on you, as its
                    // damage ramps up (Speltest towerHot at towerStreak 2; the rules' TowerRamp resets after TowerRampReset)
                    if (a.Kind == UnitKind.Tower && e.B == m.Player)
                    {
                        if (a.Serial == towerRunSerial && m.Time - towerRunAt < Rules.TowerRampReset) towerRun++; else towerRun = 0;
                        towerRunSerial = a.Serial; towerRunAt = m.Time;
                        if (towerRun >= 2) { Sound(SfxId.TowerHot, a, 0.9f); break; }
                    }
                    // Tyra's twang plays when her arrow leaves the bow, not at the draw (ReleaseSound, TYRA-03/04)
                    if ((ProjKind)e.Kind == ProjKind.KuggSpark || (ProjKind)e.Kind == ProjKind.KuggShell) { Sound((ProjKind)e.Kind == ProjKind.KuggSpark ? SfxId.KuggSpark : SfxId.KuggShotBig, a, 0.8f); break; } // Kugg (Hero7.cs)
                    if ((a.Kind != UnitKind.Tower || e.B == m.Player) && !(a.Kind == UnitKind.Hero && a.Hero == HeroId.Tyra)) Sound(AttackSound(a), a, a.Kind == UnitKind.Minion ? 0.55f : a.Kind == UnitKind.Tower ? 0.9f : a.Kind == UnitKind.Hero && a.Hero == HeroId.Tikta ? TiktaAutoVolume(a) : 0.8f);
                    // a tower shooting someone else within 8 tiles of the player is heard softly (VIS-02, Rickard 2026-10-29)
                    else if (a.Kind == UnitKind.Tower && m.Player != null && e.B != null && SimMath.Dist(e.B, m.Player) <= 8f) Sound(SfxId.TowerShot, a, 0.35f);
                    break;
                }
                case EventType.Hit:
                {
                    var b = e.B;
                    var kind = (ProjKind)e.Kind;
                    Vector3 at = Coords.World(e.X, e.Y);
                    // a victim hidden in grass from this phone's team shows nothing of the hit: no flinch, no stun or shield sound, no
                    // team-coloured burst at its spot (they gave a hidden hero away); only a shot's own landing gets an uncoloured spark
                    // where it ended, since every attack stays visible, rule 5 (full review 2026-11-09 nr 73)
                    bool seen = b == null || b.Team == Mine || m.IsVisibleTo(b, Mine);
                    if (seen) V(b)?.PlayHit(e.Big);
                    if (e.Shot != 0 && !shotHitAt.ContainsKey(e.Shot)) { shotHitAt[e.Shot] = at; shotHits.Add(e); } // its own hit comes first
                    if (seen && kind == ProjKind.BonkSlam && b != null && b.IsRealHero) BamAudio.PlayAt(SfxId.BonkStun, Coords.Of(b), 0.8f); // the slam stuns
                    // so does Bram's anvil (no shot; the tap's flying hammer has one): the same stun sound, one at a time (SfxRecipes)
                    if (seen && kind == ProjKind.BramHammerSpecial && e.Shot == 0 && b != null && b.IsRealHero) BamAudio.PlayAt(SfxId.BonkStun, Coords.Of(b), 0.8f);
                    // Bram's bounces get their own gold line; Tyra's bolt and Tikta's needles carry the special's look in their own
                    // impact, so they add only the small hit (fx review 2026-10-26, P1-2/P1-3)
                    FxKind fx = kind == ProjKind.KnutAnchor ? FxKind.AnchorHit : kind == ProjKind.TowerShot ? FxKind.TowerShotFlash
                        : kind == ProjKind.BramHammerSpecial ? (e.Shot != 0 ? FxKind.Bounce : FxKind.HitBig) // the tap's flying hammer bounces; the anvil (no shot) lands
                        : kind == ProjKind.TyraBolt || kind == ProjKind.TiktaNeedleSpecial ? FxKind.Hit
                        // a volley arrow (the rules fly it as TyraArrow, its hit a special's) wears the volley's gold on each enemy it
                        // passes through; its own splash at the end stays quiet once it hit (TYRA-07), so this is the volley's hit look
                        : kind == ProjKind.TyraArrow && e.Dmg == DmgKind.Special ? FxKind.VolleyHit
                        : e.Big ? FxKind.HitBig : FxKind.Hit;
                    float hs = (b != null && b.Kind == UnitKind.Tower ? 1.4f : 1f) * (e.A != null && e.A.Kind == UnitKind.Minion ? 0.6f : 1f); // VIS-03
                    if (seen)
                    {
                        ArtKit.PlayFx(fx, at, hs, b != null ? Side(b.Team) : 0, HeroOf(e.A));
                        if (fx == FxKind.TowerShotFlash) ArtKit.PlayFx(FxKind.Hit, at, 1f, b != null ? Side(b.Team) : 0);
                    }
                    else if (e.Shot != 0) ArtKit.PlayFx(fx, at, hs, 0, HeroOf(e.A)); // the shot was seen flying: its end too, in no team's colour
                    SfxId id = kind == ProjKind.BramHammerSpecial ? SfxId.BramHammerBounce
                        : seen && b != null && e.V <= 0f && (b.Flags & StatusFlags.Shielded) != 0 ? SfxId.ShieldBlock
                        // as in Speltest's sound mix v2: the plain hit thumps only when a hero's blow lands on you
                        : b == m.Player && e.A != null && e.A.Kind == UnitKind.Hero ? (e.Big ? SfxId.HitHeavy : SfxId.HitLight)
                        // a tower's shot landing on you gets its own heavy impact (Rickard 2026-10-30), silent until his clip exists
                        : b == m.Player && e.A != null && e.A.Kind == UnitKind.Tower ? SfxId.TowerImpact
                        : SfxId.None;
                    // Rickard 2026-10-26: minions fight silently; only heroes, the wolf and towers make combat sounds
                    if (id != SfxId.None && (e.A == null || e.A.Kind != UnitKind.Minion)) BamAudio.PlayAt(id, at, b != null && b.Kind == UnitKind.Minion ? 0.5f : 0.8f);
                    // someone else's Tyra bolt or Tikta needles landing on a hero you can see (Speltest tyrapierce, tiktasting); the
                    // player's own already sound in HitConfirm
                    if ((kind == ProjKind.TyraBolt || kind == ProjKind.TiktaNeedleSpecial) && b != null && b.IsRealHero && e.A != null
                        && e.A != m.Player && m.IsVisibleTo(b, Mine))
                        BamAudio.PlayAt(kind == ProjKind.TyraBolt ? SfxId.HitConfirmHeavy : SfxId.HitConfirmLight, at, 0.75f);
                    break;
                }
                case EventType.Blocked:
                    ArtKit.PlayFx(FxKind.Hit, Coords.World(e.X, e.Y), 0.6f);
                    if (e.Shot != 0) shotStopAt[e.Shot] = Coords.World(e.X, e.Y);
                    break;
                case EventType.Damage:
                    // special and passive damage without a shot of its own (zones, splashes) still flinches the target; a burn or
                    // poison tick does not (2 to 4 a second looked like flicker, Rickard 2026-10-26)
                    if (e.Dmg != DmgKind.Auto && e.V > 0f && e.B != null && !tickNow) V(e.B)?.PlayHit(e.Dmg == DmgKind.Special);
                    if (!tickNow) { HitConfirm(e); OwnHit(e); }
                    break;
                case EventType.Death:
                {
                    var b = e.B;
                    if (b == null) break;
                    if (unitBySerial.TryGetValue(b.Serial, out UnitRec r) && !r.Dead) { r.Dead = true; r.V.SetDead(true); }
                    ArtKit.PlayFx(FxKind.Death, Coords.Of(b), b.Kind == UnitKind.Hero ? 1.2f : 0.8f, Side(b.Team));
                    if (b.Kind == UnitKind.Hero)
                    {
                        bool myKill = b.Team != m.Player.Team && b.IsRealHero && e.A != null && (e.A == m.Player || e.A.Owner == m.Player);
                        if (myKill) BamAudio.Duck(1f); // the rest of the world steps back for the player's kill (Speltest sound mix v2)
                        if (myKill) ArtKit.PlayFx(FxKind.Kill, Coords.Of(b), 1.2f, Side(b.Team)); // Rickard's gold kill burst (2026-10-31)
                        // each hero has its own death sound, HeroDeath_<hero> (Rickard 2026-11-11, Cajsa's feedback), for the player, allies
                        // and enemies alike; until that file is in, the shared HeroDeath (AllyDeath for an ally) plays as before. A clone
                        // dies with its hero's voice too, so the sound never gives the decoy away.
                        string voice = HeroVoice(b.Hero);
                        bool own = BamAudio.HasVariant(SfxId.HeroDeath, voice);
                        if (b == m.Player) BamAudio.Play(SfxId.HeroDeath, voice);
                        else if (myKill) BamAudio.Play(SfxId.HeroDeath, voice); // the kill confirm: loud, not by distance (Brawl Stars idea 5); no extra chime (Speltest sound mix v2)
                        else if (own || b.Team != Mine) BamAudio.PlayAt(SfxId.HeroDeath, voice, Coords.Of(b));
                        else BamAudio.PlayAt(SfxId.AllyDeath, Coords.Of(b));
                        if (b.IsRealHero) MultiKill(e);
                    }
                    // the player's last hit on a minion: a small coin chime (Speltest sound mix v2); others' minion kills stay silent
                    else if (b.Kind == UnitKind.Minion && b.Team != Mine && e.A != null && (e.A == m.Player || e.A.Owner == m.Player))
                        BamAudio.Play(SfxId.LastHit, 0.6f);
                    break;
                }
                case EventType.Respawn:
                {
                    var a = e.A;
                    if (unitBySerial.TryGetValue(a.Serial, out UnitRec r))
                    {
                        r.V.transform.position = Coords.Of(a);
                        if (r.Dead) { r.Dead = false; r.V.SetDead(false); }
                        r.Hp01 = -1f; r.Vis = -1f;
                    }
                    ArtKit.PlayFx(FxKind.Respawn, Coords.Of(a), 1f, Side(a.Team));
                    if (a == m.Player) BamAudio.Play(SfxId.Respawn);
                    break;
                }
                case EventType.Cast:
                {
                    var a = e.A;
                    // a hidden enemy casting in a bush stays hidden: casting does not reveal in the rules (fx review P2-1)
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    V(a)?.PlayCast(e.Big); // the form times the gesture: a slam's blow lands with its hit (BodyView.CastLead)
                    ArtKit.PlayFx(FxKind.Cast, Coords.Of(a), 1f, Side(a.Team), e.Kind);
                    if (a == m.Player) BamAudio.Duck(0.8f);
                    // Fenn's Shadow Veil sounds on its own events (FennVeil, VeilStrike)
                    if ((HeroId)e.Kind != HeroId.Fenn) Sound(CastSound((HeroId)e.Kind, e.Big), a, 1f);
                    if ((HeroId)e.Kind == HeroId.Knut && e.Big) Sound(SfxId.KnutChain, a, 1f); // the chain rattles out with the anchor
                    // Nässla's thicket: roots growing fast where it lands, beside her throw at her hand (free CC0 mix "Rot1", Rickard 2026-11-13)
                    if ((HeroId)e.Kind == HeroId.Nassla) BamAudio.PlayAt(SfxId.NasslaBramble, Coords.World(e.X, e.Y), 0.9f);
                    // Tyra's tap is the Arrow Volley: a gold cone through the draw and the big bow draw (Speltest tyravolley)
                    if ((HeroId)e.Kind == HeroId.Tyra && !e.Big)
                    {
                        ArtKit.PlayVolleyDraw(Coords.Of(a), Coords.World(e.X, e.Y) - Coords.Of(a), a.Range, TyraS.VolleyWindup);
                    }
                    // Blast's bomb (his drag since 2026-10-30) is a ground fuse in the rules; draw the lob onto it so the throw is seen
                    if ((HeroId)e.Kind == HeroId.Blast && e.Big) LobGhost(a, Coords.World(e.X, e.Y), ProjKind.BlastBomb, BlastS.Delay * 0.9f); // lands just before it goes off (0.27 s since 2026-11-15)
                    // so are Hilde's kegs: the big one (drag) and the small quick one (tap)
                    // Bram's Anvil Drop: his hammer goes up high and comes down on the red circle as it lands (BRAM-04)
                    // thrown higher (2 -> 3 tiles) and with his gold ring closing in on the landing spot (Rickard 2026-10-30 "måste synas mer")
                    // not since he fires a lightning beam as Silver (Rickard 2026-11-12: "låt inte hammaren gå i en båge i luften")
                    if ((HeroId)e.Kind == HeroId.Bram && e.Big && !BramS.Silver)
                    {
                        LobGhost(a, Coords.World(e.X, e.Y), ProjKind.BramHammerSpecial, BramS.DropDelay, 3f);
                        ArtKit.PlayAnvilWarn(Coords.World(e.X, e.Y), BramS.DropRadius, BramS.DropDelay);
                    }
                    if ((HeroId)e.Kind == HeroId.Hilde) LobGhost(a, Coords.World(e.X, e.Y), e.Big ? ProjKind.HildeKeg : ProjKind.HildeBurst, e.Big ? 0.4f : 0.3f);
                    if ((HeroId)e.Kind == HeroId.Nassla) LobGhost(a, Coords.World(e.X, e.Y), ProjKind.NasslaThorn, 0.3f); // her seed pod flies onto the thicket's spot
                    // Kugg's Anchor: his heel spikes go into the ground (dust and cracks at his feet; his own rune seal O-065 comes later)
                    if ((HeroId)e.Kind == HeroId.Kugg) ArtKit.PlayFx(FxKind.GroundSmash, Coords.Of(a), 0.7f, Side(a.Team), (int)HeroId.Kugg);
                    break;
                }
                case EventType.RagnLand:
                {
                    // Ragn comes down from his Moon Leap: a crack and dust round him in his colours, and the thud (Hero8.cs)
                    var a = e.A;
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    ArtKit.PlayFx(FxKind.GroundSmash, Coords.World(e.X, e.Y), RagnS.LandRadius, Side(a.Team), (int)HeroId.Ragn);
                    Sound(SfxId.RagnLand, a, 1f);
                    break;
                }
                case EventType.TranceStep:
                {
                    // Skrall's Trance gathers a step: a small violet flash on him (the full three show as the gold ring, Unit.NextHitSpecial)
                    var a = e.A;
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    ArtKit.PlayFx(FxKind.Cast, Coords.Of(a), e.Big ? 0.8f : 0.5f, Side(a.Team), (int)HeroId.Skrall);
                    if (a == m.Player) Sound(SfxId.SkrallTrance, a, e.Big ? 0.8f : 0.5f);
                    break;
                }
                case EventType.FlockEnd:
                {
                    // Ravna is herself again: a puff of feathers where the flock lands, and a caw (Hero8.cs)
                    var a = e.A;
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    ArtKit.PlayFx(FxKind.HookDrag, Coords.World(e.X, e.Y), 1f, Side(a.Team), (int)HeroId.Ravna);
                    Sound(SfxId.RavnaCaw, a, 0.8f);
                    break;
                }
                case EventType.KuggPack:
                {
                    // Kugg pulls his spikes up: a puff at his feet and the clank (Hero7.cs)
                    var a = e.A;
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    ArtKit.PlayFx(FxKind.HookDrag, Coords.Of(a), 1f, Side(a.Team), (int)HeroId.Kugg);
                    Sound(SfxId.KuggPackUp, a, 0.9f);
                    break;
                }
                case EventType.Telegraph:
                {
                    // a tapped special's warning (Rules.SpecialTelegraph): the hero flashes and a ring closes in on it while the
                    // rules' red warning of the special's shape lies on the ground; a hidden caster in a bush stays hidden
                    var a = e.A;
                    if (a == null || !m.IsVisibleTo(a, Mine)) break;
                    ArtKit.PlayCastWarn(Coords.Of(a), Side(a.Team), e.Kind, e.V);
                    Sound(SfxId.SpecialWarn, a, a.Team == Mine ? 0.6f : 1f); // silent until Rickard's own clip SpecialWarn.mp3 exists (O-049)
                    break;
                }
                case EventType.Interrupt:
                    // the cut swing or cast stops in the picture too (BONK-01/CHAINMAP-03); only a stun cuts the auto attack. The sound
                    // only for a hero this phone can see: it gave a hidden one in grass away (nr 73)
                    if (e.A != null) { if (m.IsVisibleTo(e.A, Mine)) BamAudio.PlayAt(SfxId.Stunned, Coords.Of(e.A)); V(e.A)?.Interrupt(e.A.Has(St.Stun)); }
                    break;
                case EventType.LevelUp:
                    if (m.Player != null && m.Player.Alive) ArtKit.PlayFx(FxKind.LevelUp, Coords.Of(m.Player), 1f, 0);
                    BamAudio.Play(SfxId.LevelUp, 0.8f);
                    break;
                case EventType.TowerDown:
                    if (e.A != null)
                    {
                        ArtKit.PlayFx(FxKind.TowerDestroyed, Coords.Of(e.A), 1f, Side(e.A.Team));
                        BamAudio.Duck(1.5f);
                        BamAudio.PlayAt(SfxId.TowerDestroyed, Coords.Of(e.A));
                        if (unitBySerial.TryGetValue(e.A.Serial, out UnitRec r) && !r.Dead) { r.Dead = true; r.V.SetDead(true); }
                    }
                    break;
                case EventType.CloneSpawn:
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) { ArtKit.PlayFx(Spectral(e.A) ? FxKind.SpectralVeil : FxKind.CloneSpawn, Coords.Of(e.A), 1f, Side(e.A.Team)); BamAudio.PlayAt(SfxId.CloneAppear, Coords.Of(e.A)); } // FENN-09
                    break;
                case EventType.VeilStrike: // the Spectral Knight's Phantom Strike (Heroes.cs FennVeilStrike)
                    if (e.B != null && m.IsVisibleTo(e.B, Mine))
                    {
                        ArtKit.PlayFx(FxKind.SpectralStrike, Coords.Of(e.B), 1f, Side(e.A != null ? e.A.Team : 0));
                        BamAudio.PlayAt(SfxId.VeilStrike, Coords.Of(e.B), 1f);
                    }
                    break;
                case EventType.FennVeil: // he vanishes where he stood (the moment everyone saw); O-091
                    // heard only where this phone saw him a moment ago: his record still holds the last step's sight (OnEvent runs
                    // before SyncUnits), so a Fenn who veils unseen in the grass does not give his spot away (release review 0.4.1)
                    if (e.A != null && unitBySerial.TryGetValue(e.A.Serial, out UnitRec vr) && vr.Vis > 0f) BamAudio.PlayAt(SfxId.FennVeil, Coords.Of(e.A));
                    break;
                case EventType.VeilEnd: // the Veil ran out: he steps back out of the ghost light where this phone can see him
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) ArtKit.PlayFx(FxKind.SpectralVeil, Coords.Of(e.A), 0.7f, Side(e.A.Team));
                    break;
                case EventType.CloneEnd:
                    if (e.A != null && unitBySerial.TryGetValue(e.A.Serial, out UnitRec cv) && cv.Vis > 0f) { ArtKit.PlayFx(FxKind.CloneEnd, Coords.Of(e.A), 1f, Side(e.A.Team)); BamAudio.PlayAt(SfxId.CloneVanish, Coords.Of(e.A)); } // FENN-09
                    break;
                case EventType.Shield:
                    // Moa's tap shield, on an ally this phone can see (it gave a hidden one in grass away; nr 73)
                    if (e.Big && e.B != null) { if (m.IsVisibleTo(e.B, Mine)) BamAudio.PlayAt(SfxId.MoaShield, Coords.Of(e.B)); }
                    // her special's shield taking hold on an ally (Speltest "moashield": honey)
                    else if (e.B != null && m.IsVisibleTo(e.B, Mine)) BamAudio.PlayAt(SfxId.MoaHoney, Coords.Of(e.B), 0.75f);
                    break;
                case EventType.Explosion:
                {
                    Vector3 at = Coords.World(e.X, e.Y);
                    float size = Mathf.Max(0.5f, e.V);
                    var pk = (ProjKind)e.Kind;
                    if (pk == ProjKind.BonkClub && e.A != null && !m.IsVisibleTo(e.A, Mine)) break; // BONK-09
                    // Bram's passive (every 4th hammer) is the heavy anvil blow, Blast's Chain Reaction its own crack, not a bomb
                    bool bonk = pk == ProjKind.BonkSlam || pk == ProjKind.BonkClub;
                    // Hilde's kegs and Oaken's slam have their own looks (2026-10-29, HILDE-03 / OAKEN-02), drawn on the true radius
                    bool own = pk == ProjKind.HildeKeg || pk == ProjKind.HildeBurst || pk == ProjKind.OakenSlam;
                    // Bram's Anvil Drop and Knut's Anchor Slam (Simon's reworks) get their own heroes' blows, not Blast's fireball
                    FxKind ek = pk == ProjKind.BramHammerSpecial ? FxKind.AnvilDrop : pk == ProjKind.BramHammer ? FxKind.AnvilStrike : pk == ProjKind.KnutSlam ? FxKind.AnchorHit
                        : pk == ProjKind.BlastChain ? FxKind.ChainBoom : bonk ? FxKind.GroundSmash : pk == ProjKind.OakenSlam ? FxKind.OakenSlam : own ? FxKind.KegBlast
                        : pk == ProjKind.SkrallStrike ? FxKind.Cast : FxKind.Explosion; // Skrall's strike: a pillar of his violet from the sky, not a fireball
                    // Bonk: his auto slam and Ground Slam, both the club into the ground, drawn at the true hit radius (a power swing
                    // is no wider in the rules, so it is told by its sound and shake, not a bigger ring: BONK-06)
                    float esize = pk == ProjKind.BlastChain || bonk ? Mathf.Max(0.1f, e.V) : size;
                    if (own) esize = Mathf.Max(0.1f, e.V);
                    if (pk == ProjKind.BlastBomb || pk == ProjKind.BlastChain)
                    {   // Blast's blasts stop at walls: draw them only where they reach (BLAST-05)
                        esize = m.BlastLookDisc(e.X, e.Y, esize, 0.5f, out float lx, out float ly);
                        at = Coords.World(lx, ly);
                    }
                    ArtKit.PlayFx(ek, at, esize, e.A != null ? Side(e.A.Team) : 0, HeroOf(e.A));
                    SfxId boom = pk == ProjKind.BlastBomb ? SfxId.BlastExplosion : pk == ProjKind.HildeKeg || pk == ProjKind.HildeBurst ? SfxId.HildeKegBlast : pk == ProjKind.OakenSlam ? SfxId.BonkSlam : pk == ProjKind.BlastChain ? SfxId.BlastChainBoom
                        : pk == ProjKind.BonkSlam || (pk == ProjKind.BonkClub && e.Big) ? SfxId.BonkSlam : pk == ProjKind.BonkClub ? SfxId.AttackBonk
                        : pk == ProjKind.BramHammer || pk == ProjKind.BramHammerSpecial ? SfxId.BramAnvil // Bram's Anvil Drop: Speltest's anvil (bramsmash), BRAM-04
                        : pk == ProjKind.KnutSlam ? SfxId.KnutSlam : pk == ProjKind.SkrallStrike ? SfxId.SkrallStrike : SfxId.None; // Knut's Anchor Slam: Rickard's own clip (KNUT-03)
                    if (boom != SfxId.None) BamAudio.PlayAt(boom, at);
                    break;
                }
                // Tikta 2.0: her needle takes the Deathmark (its own Hit follows in the same tick)
                case EventType.TiktaMarkPop:
                    if (e.B != null && m.IsVisibleTo(e.B, Mine)) ArtKit.PlayFx(FxKind.DeathmarkTaken, Coords.Of(e.B), 1f, Side(e.B.Team), (int)HeroId.Tikta);
                    break;
                // Blast 2.0: Chain Reaction is set off; its blast follows BlastChainS.Delay later as an Explosion of ProjKind.BlastChain.
                // The warning shows to everyone, so enemies there can see it coming.
                case EventType.ChainSet:
                {   // its blast stops at walls, so its warning is drawn only where it reaches (BLAST-05)
                    float wr = m.BlastLookDisc(e.X, e.Y, Mathf.Max(0.1f, e.V), 0.4f, out float wx, out float wy);
                    ArtKit.PlayChainWarn(Coords.World(wx, wy), wr, BlastChainS.Delay);
                    break;
                }
                // Bonk's boulder knocks a hero back: the rules slide it (drawn between ticks as any move), a thud and dust where it was hit
                case EventType.Knock:
                    if (e.B != null && m.IsVisibleTo(e.B, Mine))
                    {
                        ProjKind kk = (ProjKind)e.Kind;
                        BamAudio.PlayAt(kk == ProjKind.HildeKeg || kk == ProjKind.HildeBurst ? SfxId.HildeKnock : SfxId.BonkKnock, Coords.Of(e.B));
                        bool keg = kk == ProjKind.HildeKeg || kk == ProjKind.HildeBurst;
                        if (keg) ArtKit.PlayFx(FxKind.HildeKnock, Coords.Of(e.B), 1f, Side(e.B.Team), (int)HeroId.Hilde); // her own puff (O-012)
                        else ArtKit.PlayFx(FxKind.GroundSmash, Coords.Of(e.B), 0.45f, Side(e.B.Team));
                    }
                    break;
                case EventType.Pull:
                    if (e.B != null) BamAudio.PlayAt(SfxId.KnutAnchorHook, Coords.Of(e.B));
                    if (e.A != null && e.B != null) BamAudio.PlayAt(SfxId.KnutChain, Coords.Of(e.A), 0.8f); // and is reeled in
                    break;
                // Hilde's keg slid a hero into a wall and stunned it (Wall Crash; Oaken's quake hurts at a wall but never stuns, so it
                // sends none): Speltest's own crack, flash and sound at the wall
                case EventType.WallCrash:
                    if (e.B != null && m.IsVisibleTo(e.B, Mine))
                    {
                        BamAudio.PlayAt(SfxId.HildeWallCrash, Coords.World(e.X, e.Y));
                        ArtKit.PlayFx(FxKind.WallCrash, Coords.World(e.X, e.Y), 1f, Side(e.B.Team), (int)HeroId.Hilde);
                    }
                    break;
                // Oaken's quake: his own gold-lit crack along the line from where he stood (e.V long) to its end (e.X, e.Y), seen by
                // everyone as the warning was (its sound is still Bonk's until Rickard has heard sp_oaken: OAKEN-01)
                case EventType.Quake:
                    if (e.A != null)
                    {
                        Vector3 qa = Coords.Of(e.A), qb = Coords.World(e.X, e.Y);
                        ArtKit.PlayOakenQuake(qa, qb, OakenS.QuakeHalfWidth);
                        BamAudio.PlayAt(SfxId.BonkSlam, Vector3.Lerp(qa, qb, 0.5f));
                    }
                    break;
                // Oaken's Stored Impact took part of a hit: his stone skin flashes gold
                case EventType.OakenGuard:
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) ArtKit.PlayFx(FxKind.OakenGuard, Coords.Of(e.A), 0.8f, Side(e.A.Team), (int)HeroId.Oaken);
                    break;
                // battle spells (the art's port of View3DSpellLooks.cs): a blink from where it started, the others on the caster
                case EventType.SpellCast:
                    if (e.A != null && m.IsVisibleTo(e.A, Mine))
                    {
                        ArtKit.PlaySpell((SpellId)e.Kind, Coords.World(e.X, e.Y), Coords.Of(e.A));
                        var sp = (SpellId)e.Kind;
                        Sound(sp == SpellId.Blink ? SfxId.SpellBlink : sp == SpellId.Shield ? SfxId.SpellShield : sp == SpellId.Haste ? SfxId.SpellHaste
                            : sp == SpellId.Sprint ? SfxId.SpellSprint : SfxId.None, e.A, 0.8f);
                    }
                    break;
                case EventType.SpellCancel:
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) ArtKit.PlaySpellCancel(Coords.Of(e.A));
                    break;
                case EventType.Recall:
                    if (e.A != null) ArtKit.PlayRecall(Coords.World(e.X, e.Y), Coords.World(e.A.SpawnX, e.A.SpawnY));
                    if (e.A == m.Player) BamAudio.Play(SfxId.SpellRecall, 0.8f); // the beam home (Speltest's teleport)
                    break;
            }
        }

        // Hilde's shell and kegs leave her cannon's mouth (Speltest CannonMouthOf), on the ground under it as every shot starts
        Vector3 FromMuzzle(int srcSerial, Vector3 from)
        {
            if (MuzzleOf(srcSerial, out Vector3 at)) { from.x = at.x; from.z = at.z; }
            return from;
        }

        // the shooter's hand or weapon tip now, where its art knows it (UnitView.Muzzle; the rest start at the body as before)
        bool MuzzleOf(int srcSerial, out Vector3 at)
        {
            at = default;
            return unitBySerial.TryGetValue(srcSerial, out UnitRec r) && r.V != null && r.V.Muzzle(out at);
        }

        // arc >= 0: the lob's height whatever the kind (a hammer flies flat as a shot); a lob lands quietly, its blast is the rules' own
        void LobGhost(Unit src, Vector3 to, ProjKind kind, float flight, float arc = -1f)
        {
            var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
            Init(s, null, 0, kind, src.Team, src.Serial);
            shots.Add(s);
            StartGhost(s, FromMuzzle(src.Serial, DrawnAt(src)), to, flight, 0f);
            if (arc >= 0f) s.V.SetLob(arc);
            s.V.MarkSpent();
        }

        // Speltest HeroJuice.cs hit confirm: the player's hit (or its wolf's) landing on an enemy hero gives a short tick, a dull thud for
        // the heavy heroes and a bright one 6 dB lower for the light ones, louder the bigger the hit. No buzz: Rickard 2026-10-26 keeps
        // vibration for the big moments (MatchJuice). Burn and poison ticks never tick.
        void HitConfirm(in SimEvent e)
        {
            var b = e.B; var p = m.Player;
            if (b == null || p == null || e.A == null || e.V < 1f || !b.IsRealHero || b.Team == p.Team) return;
            if (e.A != p && e.A.Owner != p) return;
            if (!m.IsVisibleTo(b, Mine)) return;
            float f = e.V / Mathf.Max(1f, b.MaxHp);
            bool heavy = MatchJuice.Heavy(p.Hero);
            BamAudio.Play(heavy ? SfxId.HitConfirmHeavy : SfxId.HitConfirmLight, (0.55f + Mathf.Min(0.35f, f * 2f)) * (heavy ? 1f : 0.5f));
        }

        // Tester feedback (Ski, Discord 2026-11-07, feedback row 37): "abc made it very clear that you are hitting something, in bam
        // it's much harder to understand what's going on and who you are hitting". Every hit of the player's own (its autos, special,
        // passive, Fenn's wolf) now shows on the one it lands on: a short white flash on the whole body (UnitView.PlayOwnHit, over
        // the red blink every hit has) and a small jolt away from the player, as Speltest's stagger did (SimLoop.updateJuice: 0.12 s
        // plus up to 0.14 s for a big hit). Others' hits keep only the red blink, so the player's own stand out. Burn and poison
        // ticks never jolt (they did not flinch before either). Picture only: the rules and the protocol never see it.
        void OwnHit(in SimEvent e)
        {
            var b = e.B; var p = m.Player;
            if (b == null || p == null || e.A == null || e.V < 1f || b.Team == p.Team || b.MaxHp <= 0f) return;
            if (e.A != p && e.A.Owner != p) return;
            if (!m.IsVisibleTo(b, Mine)) return;
            if (!unitBySerial.TryGetValue(b.Serial, out UnitRec r) || r.V == null) return;
            float f = Mathf.Min(1f, e.V / b.MaxHp);
            float len = 0.12f + Mathf.Min(0.14f, f * 0.6f);
            bool hero = b.Kind == UnitKind.Hero, tower = b.Kind == UnitKind.Tower;
            // white over the whole body: a hero strongest; a minion a little softer (Rickard 2026-10-18 found white flashes on
            // every minion hit cluttered, here only the player's own); a tower only softly (Speltest: full white on it was too much)
            r.V.PlayOwnHit(hero ? 0.85f : tower ? 0.35f : 0.6f, len);
            if (tower) return; // a tower rocks on its own (BodyView.PoseTower recoil), it is never shoved
            // the jolt: away from the one who hit (the player or its wolf). A hero moves less than a minion (Speltest: 0.025-0.09
            // and 0.05-0.24 tiles; the hero's floor raised to 0.06 so a small hit still reads on a phone)
            float k = hero ? Mathf.Min(0.12f, 0.06f + f * 0.4f) : Mathf.Min(0.24f, 0.08f + f * 0.8f);
            float dx = b.X - e.A.X, dy = b.Y - e.A.Y, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1e-3f) { float a = UnityEngine.Random.value * 6.2832f; dx = Mathf.Cos(a); dy = Mathf.Sin(a); d = 1f; } // picture only: Unity's random, never the match's
            if (r.JoltT > 0f && r.JoltT / r.JoltLen * Mathf.Sqrt(r.JoltX * r.JoltX + r.JoltY * r.JoltY) > k) return; // a bigger jolt still running wins
            r.JoltX = dx / d * k; r.JoltY = dy / d * k; r.JoltT = r.JoltLen = len;
        }

        // the old game's kill tags (ABC2 Sim.cs onDeath): a second kill within 8 s is a Double Kill, a third a Triple, the third kill
        // without dying a Killing Spree, the last enemy down an Ace; a tag for the player's team plays Speltest's streak sound
        void MultiKill(in SimEvent e)
        {
            var k = e.A; var b = e.B;
            if (k == null || k.Kind != UnitKind.Hero || k.Team == b.Team) return;
            bool tag = false;
            if (killAt.TryGetValue(k.Serial, out float t) && m.Time - t < 8f) { killRun[k.Serial] = killRun[k.Serial] + 1; tag = true; }
            else killRun[k.Serial] = 1;
            killAt[k.Serial] = m.Time;
            if (e.V == 3f) tag = true;
            bool ace = true;
            for (int i = 0; i < m.Heroes.Count; i++) { var h = m.Heroes[i]; if (h != b && h.IsRealHero && h.Team == b.Team && h.Alive) { ace = false; break; } }
            if (ace) tag = true;
            if (tag && k.Team == Mine) BamAudio.Play(SfxId.MultiKill, 0.9f);
        }

        // Tyra's twang when the arrow really leaves the bow (the rules release it 0.18 s after the auto's AttackStart, 0.25 s into the
        // volley and 0.33 s into the bolt; the clips peak at 0.05 s): the auto's sound, the special's release once per volley (TYRA-03/04)
        readonly Dictionary<int, int> volleySoundStep = new Dictionary<int, int>();
        void ReleaseSound(int srcSerial, ProjMode mode)
        {
            if (!unitBySerial.TryGetValue(srcSerial, out UnitRec r) || r.U == null) return;
            var a = r.U;
            if (a.Kind == UnitKind.Hero && a.Hero == HeroId.Bram && mode == ProjMode.HammerLine)
            {
                if (m.IsVisibleTo(a, Mine)) Sound(SfxId.BramBolt, a, 1f); // the hammer leaves his hand after the charge (O-096)
                return;
            }
            if (a.Kind != UnitKind.Hero || a.Hero != HeroId.Tyra) return;
            if (!m.IsVisibleTo(a, Mine)) return; // a hidden Tyra stays unheard: shooting does not reveal, only a hit does
            if (mode == ProjMode.Auto) { Sound(SfxId.AttackTyra, a, 0.8f); return; }
            if (mode != ProjMode.Bolt && mode != ProjMode.Volley) return;
            if (mode == ProjMode.Volley)
            {
                if (volleySoundStep.TryGetValue(srcSerial, out int at) && at == step) return; // five arrows, one twang
                volleySoundStep[srcSerial] = step;
            }
            Sound(SfxId.TyraRelease, a, 1f);
        }

        // the five volley arrows leave the bow on one tick: one gold pop at the bow for them (Skill Visibility 2.0), as the twang;
        // not for a Tyra the player cannot see (shooting does not reveal)
        readonly Dictionary<int, int> volleyFlashStep = new Dictionary<int, int>();
        void VolleyRelease(int srcSerial, Vector3 at)
        {
            if (!unitBySerial.TryGetValue(srcSerial, out UnitRec r) || r.U == null || !m.IsVisibleTo(r.U, Mine)) return;
            if (volleyFlashStep.TryGetValue(srcSerial, out int was) && was == step) return;
            volleyFlashStep[srcSerial] = step;
            ArtKit.PlayFx(FxKind.VolleyRelease, at, 1f, Side(r.U.Team), (int)HeroId.Tyra);
        }

        // Tikta's needle "tsip" sits wholly in 2.5-4 kHz, where the ear and a phone speaker are loudest, while the other autos are
        // mostly low thuds a phone barely plays; and she attacks fastest (1.3 s). At 0.8 it stood about 7 dB over the rest on a phone
        // and testers found it loud and pushy, most of all from someone else's Tikta (2026-11-03). Her own: -6 dB, level with
        // Knut's; anyone else's: -10 dB, a little under the others' autos. Rickard's clip stays as it is.
        const float TiktaAutoOwn = 0.4f, TiktaAutoOthers = 0.25f;
        float TiktaAutoVolume(Unit a) => a == m.Player ? TiktaAutoOwn : TiktaAutoOthers;

        void Sound(SfxId id, Unit a, float volume)
        {
            if (id == SfxId.None) return;
            if (a == m.Player) BamAudio.Play(id, volume);
            else BamAudio.PlayAt(id, Coords.Of(a), volume);
        }

        static SfxId AttackSound(Unit a)
        {
            switch (a.Kind)
            {
                case UnitKind.Hero: return a.Hero == HeroId.Bonk || a.Hero == HeroId.Oaken ? SfxId.AttackBonk : a.Hero == HeroId.Hilde ? SfxId.AttackHilde : a.Hero == HeroId.Kugg ? SfxId.AttackKugg
                    : a.Hero == HeroId.Ragn ? SfxId.AttackRagn : a.Hero == HeroId.Nassla ? SfxId.AttackNassla : a.Hero == HeroId.Skrall ? SfxId.AttackSkrall : a.Hero == HeroId.Ravna ? SfxId.AttackRavna : SfxId.AttackBram + (int)a.Hero; // Bonk's came after the seven; Oaken borrows Bonk's until he has his own
                case UnitKind.Tower: return SfxId.TowerShot;
                default: return SfxId.None; // minions are silent (Rickard 2026-10-26)
            }
        }

        static SfxId CastSound(HeroId h, bool dragged)
        {
            switch (h)
            {
                case HeroId.Bram: return BramS.Silver ? SfxId.BramCharge : SfxId.BramHammerThrow; // Hammer Line: the charge; the throw in ReleaseSound
                case HeroId.Tyra: return SfxId.TyraDraw; // the draw creak; the twang plays at the release (ReleaseSound)
                case HeroId.Moa: return dragged ? SfxId.MoaField : SfxId.MoaShield;
                case HeroId.Knut: return dragged ? SfxId.KnutAnchorThrow : SfxId.None; // the tap is silent until the anchor lands (KnutSlam on its Explosion); Rickard 2026-10-30 22:09: the wind-up's jingle out
                case HeroId.Tikta: return SfxId.TiktaNeedles;
                case HeroId.Blast: return dragged ? SfxId.BlastBombThrow : SfxId.BlastFireLane; // tap = fire strip, drag = bomb (2026-10-30)
                case HeroId.Bonk: return dragged ? SfxId.BonkBoulderThrow : SfxId.BonkSlamWindup; // the slam's landing sounds on its Explosion
                case HeroId.Hilde: return SfxId.HildeKegThrow; // both kegs (Speltest sp_hilde)
                case HeroId.Oaken: return SfxId.BonkSlamWindup; // borrowed until he has his own
                case HeroId.Kugg: return SfxId.KuggDeploy;
                case HeroId.Ragn: return SfxId.RagnLeap;
                case HeroId.Nassla: return SfxId.NasslaThrow;
                case HeroId.Skrall: return SfxId.SkrallStrike;
                case HeroId.Ravna: return SfxId.RavnaFlock;
                default: return SfxId.None;
            }
        }

        // Cues for the player only: special ready, entering grass, low health, a new wave.
        void PlayerCues()
        {
            var p = m.Player;
            if (p == null) return;
            bool ready = p.Alive && p.SpecialReady;
            if (ready && !playerWasReady) BamAudio.Play(SfxId.SpecialReady);
            playerWasReady = ready;
            bool grass = p.Alive && m.Map.InGrass(p.X, p.Y);
            if (grass && !playerInGrass) BamAudio.Play(SfxId.GrassEnter, 0.6f);
            playerInGrass = grass;
            bool low = p.Alive && p.Hp < p.MaxHp * 0.25f;
            if (low && !playerLow) BamAudio.Play(SfxId.LowHealth);
            playerLow = low;
            // the horn only for the match's first wave (Rickard 2026-10-30: "första wave")
            if (m.WaveN != lastWave) { lastWave = m.WaveN; if (m.WaveN == 1) BamAudio.Play(SfxId.WaveSpawn, 0.5f); }
            // the player's own tower losing health while the player is more than 8 tiles from it (Speltest towerHit; at most every 8 s)
            for (int i = 0; i < m.Units.Count; i++)
            {
                var t = m.Units[i];
                if (t.Kind != UnitKind.Tower || t.Team != Mine || !t.Alive) continue;
                float dx = t.X - p.X, dy = t.Y - p.Y;
                if (ownTowerHp >= 0f && t.Hp < ownTowerHp && dx * dx + dy * dy > 64f && !Island) BamAudio.Play(SfxId.TowerUnderAttack, 0.6f);
                ownTowerHp = t.Hp;
                break;
            }
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>paused: the ghost shots wait with the game (the art layer's own shots and zones wait on ArtKit.SetPaused).</summary>
        public void Frame(float alpha, float dt, bool paused = false)
        {
            // a second before GO the road's runes run from our tower to theirs, showing the way; the first minions put them out
            if (!roadShown && m.Time >= -1f)
            {
                roadShown = true;
                Unit own = m.Towers[Mine], foe = m.Towers[1 - Mine];
                if (m.Tut == null && !m.HeroTest && m.Time < 2f && own != null && foe != null)
                    ArtKit.RoadRunesShow(Coords.Of(own), Coords.Of(foe), Teams.Player);
            }
            for (int i = 0; i < units.Count; i++) DrawUnit(units[i], alpha, dt);
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var r = dying[i];
                r.Linger -= dt;
                if (r.Linger > 0f) continue;
                dying.RemoveAt(i);
                ArtKit.Release(r.V);
                FreeUnit(r);
            }
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                if (s.Ghost)
                {
                    if (!paused && !s.Fresh) s.Age += dt;
                    s.Fresh = false;
                    float k = s.Flight > 0f ? Mathf.Clamp01(s.Age / s.Flight) : 1f;
                    s.V.transform.position = Vector3.Lerp(s.From, s.To, k);
                    if (s.Age >= s.Flight + s.Linger) { ArtKit.Release(s.V); shots.RemoveAt(i); FreeShot(s); }
                }
                else if (s.V != null && s.P != null)
                {
                    var p = s.P;
                    s.V.transform.position = Coords.Lerp(p.PX, p.PY, p.X, p.Y, alpha);
                    if (p.Mode == ProjMode.Hook && !paused) HookDust(s, p, dt);
                    // a homing shot's arc (Blast's bomb) by the share of the way it has flown to where its target is now, so it
                    // comes down on the target even when the target walks (SHOT-05)
                    if (p.Mode == ProjMode.Auto && p.Tgt != null && p.Launched && p.Speed > 0f)
                    {
                        float x = p.PX + (p.X - p.PX) * alpha, y = p.PY + (p.Y - p.PY) * alpha;
                        float done = Mathf.Sqrt((x - p.OX) * (x - p.OX) + (y - p.OY) * (y - p.OY));
                        float left = Mathf.Sqrt((p.Tgt.X - x) * (p.Tgt.X - x) + (p.Tgt.Y - y) * (p.Tgt.Y - y));
                        if (done + left > 1e-3f) s.V.SetProgress(done / (done + left));
                    }
                }
            }
            // a warning strip that follows its bolt: the fill ends where the bolt is drawn now (its middle, just behind the tip)
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z.FollowShot == 0) continue;
                if (!shotBySerial.TryGetValue(z.FollowShot, out ShotRec s) || s.V == null) continue; // still in her hand (0 since Show), or just ended
                Transform zt = z.V.transform;
                Vector3 d = s.V.transform.position - zt.position; d.y = 0f;
                z.V.SetFront(Mathf.Max(0f, Vector3.Dot(d, zt.forward)));
            }
        }

        const float PadArcH = 1.6f; // how high a jump pad throws a hero, world units

        /// <summary>How high the Tripo tower's flat stone foot lifts a body standing at world point p (Rickard 2026-11-04 "fötterna går
        /// igenom plattformen": since the tower stops bodies at its wall, Rules.TowerBody 1.1, heroes stand on the foot, which is 0.20
        /// high at the wall, 0.14 at its rim 1.5 tiles out; measured from bam_tower_grass at its 3.0-wide match size). Only the view
        /// moves: the rules know no heights. Past the rim the lift eases to 0 over 0.15 tiles (a foot half on the stone).</summary>
        float TowerFootLift(Vector3 p)
        {
            float best = 0f;
            for (int t = 0; t < m.Towers.Length; t++)
            {
                var tw = m.Towers[t];
                if (tw == null) continue;
                Vector3 c = Coords.Of(tw);
                float dx = p.x - c.x, dz = p.z - c.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d >= 1.65f) continue;
                float lift = d <= 1.1f ? 0.2f : d <= 1.5f ? Mathf.Lerp(0.2f, 0.14f, (d - 1.1f) / 0.4f) : Mathf.Lerp(0.14f, 0f, (d - 1.5f) / 0.15f);
                if (lift > best) best = lift;
            }
            return best;
        }

        void DrawUnit(UnitRec r, float alpha, float dt)
        {
            var u = r.U;
            var v = r.V;
            var t = v.transform;
            // a respawn or a long pull jumps; drawing the jump between ticks would slide the hero across the map
            bool jump = (u.X - u.PX) * (u.X - u.PX) + (u.Y - u.PY) * (u.Y - u.PY) > 4f;
            Vector3 pos = jump ? Coords.Of(u) : Coords.Lerp(u.PX, u.PY, u.X, u.Y, alpha);
            pos.y += u.JumpLift * PadArcH; // a jump pad's throw flies in an arc
            if (u == m.Player && m.Tut != null) pos.y += m.Tut.JumpArc * PadArcH; // and so does the island's boost pad's (TutorialIsland.cs)
            if (u.Kind != UnitKind.Tower) pos.y += TowerFootLift(pos); // stands on the tower's stone foot, not in it
            if (r.JoltT > 0f)
            {
                // the player's own hit (OwnHit): out at once, back over the jolt's length (Speltest's stagger, View3DWorld/View3DCreeps)
                float q = r.JoltT / r.JoltLen;
                if (!jump && u.Alive) pos += new Vector3(r.JoltX * q, 0f, -r.JoltY * q);
                r.JoltT = Mathf.Max(0f, r.JoltT - dt);
            }
            if (r.FixX != 0f || r.FixY != 0f)
            {
                if (jump) { r.FixX = r.FixY = 0f; }
                else
                {
                    pos += new Vector3(r.FixX, 0f, -r.FixY);
                    float k = Mathf.Exp(-dt * 14f);
                    r.FixX *= k; r.FixY *= k;
                    if (r.FixX * r.FixX + r.FixY * r.FixY < 1e-5f) r.FixX = r.FixY = 0f;
                }
            }
            bool hero = u.Kind == UnitKind.Hero, aim = u.AimT > m.Time;
            // AnimFix.SquaredFacing (Speltest CombatFeel.Squared): in a fight a hero keeps facing its target between swings, its legs
            // turned to the travel (BodyView), instead of the rules' snap to it for each windup and back to the path after; an aimed
            // swing or special keeps its own direction; and it turns smoothly, not at 900 degrees a second
            bool fight = AnimFix.SquaredFacing && hero && !aim && Fighting(u);
            float face = fight ? SimMath.Atan2(u.Target.Y - u.Y, u.Target.X - u.X) : u.Facing;
            if (aim && hero && u.Hero == HeroId.Bonk && ClubLanding(u, out float cx, out float cy)) face = SimMath.Atan2(cy - u.Y, cx - u.X);
            Quaternion want = Quaternion.LookRotation(Coords.Dir(face));
            if (AnimFix.SquaredFacing && hero) r.Rot = Quaternion.RotateTowards(Quaternion.Slerp(r.Rot, want, 1f - Mathf.Exp(-14f * dt)), want, 60f * dt);
            else r.Rot = Quaternion.RotateTowards(r.Rot, want, 900f * dt);
            t.SetPositionAndRotation(pos, r.Rot);
            if (hero && (aim || fight) != r.Squared) { r.Squared = aim || fight; v.SetSquared(r.Squared); }

            if (u.Alive == r.Dead) { r.Dead = !u.Alive; v.SetDead(r.Dead); if (!r.Dead) { r.Hp01 = -1f; r.Vis = -1f; } }
            if (r.Dead) { HeroMarksOff(r); return; }

            bool moving = m.Time - u.MovedAt < 0.1f;
            float speed01 = 0f;
            if (moving && u.BaseSpeed > 0f) speed01 = Mathf.Sqrt((u.X - u.PX) * (u.X - u.PX) + (u.Y - u.PY) * (u.Y - u.PY)) / Rules.Tick / u.BaseSpeed;
            v.SetMoving(moving, moving ? Mathf.Max(0.5f, speed01) : 0f);

            float hp01 = u.MaxHp > 0f ? Mathf.Clamp01(u.Hp / u.MaxHp) : 0f;
            float sh01 = u.MaxHp > 0f ? Mathf.Clamp01(u.ShieldAmount / u.MaxHp) : 0f;
            if (Mathf.Abs(hp01 - r.Hp01) > 0.0005f || Mathf.Abs(sh01 - r.Shield01) > 0.0005f) { r.Hp01 = hp01; r.Shield01 = sh01; v.SetHealth(hp01, sh01); }
            if (u.Kind == UnitKind.Hero && u.Level != r.Level) { r.Level = u.Level; v.SetLevel(u.Level); }
            if (u.Kind == UnitKind.Hero)
            {
                // MLBB bars: a notch every 100 HP and the special's charge as the row under the bar; a clone shows its owner's
                // charge. Only on the own team: an enemy has no row, only the red diamond once its special is ready (HudOverhead),
                // so nobody reads how far an enemy's special has come (Rickard 2026-11-13 "man ska inte veta exakt hur långt");
                // its decoy has none either, so it still reads exactly like Fenn
                if (Mathf.Abs(u.MaxHp - r.MaxHp) > 0.5f) { r.MaxHp = u.MaxHp; v.SetMaxHp(u.MaxHp); }
                Unit who = u.IsClone && u.Owner != null ? u.Owner : u;
                bool foeRow = u.Team != Mine;
                float en = foeRow ? -1f : who.Charge01; bool ready = !foeRow && who.SpecialReady;
                if (Mathf.Abs(en - r.Energy) > 0.002f || ready != r.Ready) { r.Energy = en; r.Ready = ready; v.SetEnergy(en, ready); }
            }

            var flags = u.Flags;
            if (u.Team != Mine) flags &= ~StatusFlags.Illusion; // the enemy's clone looks exactly like Fenn
            if (m.Map.InGrass(u.X, u.Y)) flags |= StatusFlags.InGrass;
            if (flags != r.Flags) { r.Flags = flags; v.SetStatus(flags); }

            if (u.Kind == UnitKind.Hero)
            {
                // the recall's channel ring and the Sprint trail (battle spells)
                float ch = Match.SpellChanneling(u) ? Mathf.Clamp01((m.Time - u.SpellChanStart) / SpellDefs.RecallChannel) : -1f;
                if (ch != r.Channel) { r.Channel = ch; v.SetChannel(ch); }
                bool sprint = u.Spell == SpellId.Sprint && Sprinting(u);
                if (sprint != r.Sprint) { r.Sprint = sprint; v.SetSprint(sprint); }
                bool four = u.Hero == HeroId.Ragn && m.RagnRunning(u); // Wolf Run: on all fours (Rickard 2026-11-10)
                if (four != r.Four) { r.Four = four; v.SetFourLegs(four); }
                int form = u.Hero == HeroId.Kugg && u.KuggRooted ? 1 : 0; // Kugg stands as a turret in his Anchor (Hero7.cs)
                if (form != r.Form) { r.Form = form; v.SetForm(form); }
                HeroMarks(r, u, v);
            }

            float vis = !m.IsVisibleTo(u, Mine) ? 0f : u.Team == Mine && u.Kind == UnitKind.Hero && m.HiddenFromFoes(u) ? 0.5f : 1f;
            if (Hidden != null && Hidden(u)) vis = 0f; // the match intro: not yet beamed in
            if (vis != r.Vis) { r.Vis = vis; v.SetVisibility(vis); }
        }

        // BONK-03: where Bonk's club comes down if it lands now, as BonkSmash (Hero3.cs) moves its circle at the landing: with him as
        // he walks, then after its target at most SmashFollow tiles. Drawn, he turns to it through the wind-up instead of swinging
        // down the line he began on while the cracks open up to half a tile beside it, then snapping 25-48 degrees round
        bool ClubLanding(Unit h, out float x, out float y)
        {
            x = y = 0f;
            var list = m.Projs;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Done || p.Mode != ProjMode.Smash || p.Src != h || p.T >= 0f) continue;
                m.ClubSpot(p, out x, out y); // the rules' own landing spot (Hero3.cs)
                return true;
            }
            return false;
        }

        // the decoy Fenn leaves as he fades into the Spectral Veil: its puff is the ghost one (the clone of his passive keeps the violet)
        bool Spectral(Unit clone) => clone.Owner != null && clone.Owner.Hero == HeroId.Fenn && clone.Owner.Has(St.Veil);

        // a hero in a fight: it swung at its target within about one swing's time, the target alive, seen by the player (facing it must
        // not give a hider away) and still about in reach, and the hero can swing (not stunned)
        bool Fighting(Unit u)
        {
            var tg = u.Target;
            return u.Alive && tg != null && tg.Alive && m.Time - u.TargetT < u.AttackCd + 0.35f && !u.Has(St.Stun) && m.IsVisibleTo(tg, Mine)
                && SimMath.Dist(u, tg) <= u.Range + tg.R + 0.6f;
        }

        // Tikta 2.0's Deathmark over a marked hero (every frame while seen: it blinks at the end), and the player's Blast 2.0
        // Chain Reaction: a spark crown over each enemy burning from her while the chain is ready, a cooldown arc at her feet
        void HeroMarks(UnitRec r, Unit u, UnitView v)
        {
            bool seen = m.IsVisibleTo(u, Mine);
            float mark = seen ? MarkLeft(u) : -1f;
            if (mark > 0f) { v.SetDeathmark(mark); r.Marked = true; }
            else if (r.Marked) { v.SetDeathmark(-1f); r.Marked = false; }

            var me = m.Player;
            bool blast = me != null && me.Alive && me.Hero == HeroId.Blast && !me.IsClone;
            bool spark = blast && seen && u.Team != me.Team && m.ChainLeft(me) <= 0f && m.BurnsFrom(u, me);
            if (spark != r.Spark) { r.Spark = spark; v.SetChainSpark(spark); }

            float cd = -1f;
            if (u == me && blast) { float left = m.ChainLeft(me); if (left > 0f) cd = 1f - left / BlastChainS.Cooldown; }
            if (cd >= 0f || r.ChainCd >= 0f) { r.ChainCd = cd; v.SetChainCooldown(cd); }

            // Moa 2.0's shield rings (Simon, XYZ #610): told only when a cell goes dark, one comes or one goes
            float hg = -1f, hf = -1f, hq = -1f;
            if (seen) HoneyLeft(u, ref hg, ref hf, ref hq);
            int key = HoneyKey(hg, hf, hq);
            if (key != r.Honey) { r.Honey = key; v.SetHoneyShields(hg, hf, hq); }

            // Oaken's Stored Impact holds damage for his next punch: a gold glow at his fists while it lasts, for him and his foes alike
            if (seen && u.Hero == HeroId.Oaken && u.OakenStored > 0f && m.Time <= u.OakenStoredUntil && Time.time - r.StoredFx >= 0.12f)
            {
                r.StoredFx = Time.time;
                ArtKit.PlayFx(FxKind.OakenStored, v.transform.position, 1f, Side(u.Team), (int)HeroId.Oaken);
            }
        }

        const int HoneyOff = 31 | 31 << 5 | 31 << 10;
        static int Cells(float left, int n) => left < 0f ? 31 : Mathf.Clamp(Mathf.CeilToInt(left * n - 0.001f), 0, n);
        static int HoneyKey(float g, float f, float q) => Cells(g, 12) | Cells(f, 12) << 5 | Cells(q, 6) << 10;

        /// <summary>The share left of each of Moa's shields on u: Queen's Instinct by its flag, then Honey Guard and the field's
        /// shield told apart by what they started with (18 % and 6 % of max health). In the one-tap game every shield of hers but
        /// Queen's is the field's: since 2026-11-13 it is 15 % of MOA's max health, above the split on most allies, so the size cannot
        /// tell them apart there (Rickard "sköld 25% av moas maxhp", then 15 %). -1 where there is none.</summary>
        static void HoneyLeft(Unit u, ref float guard, ref float field, ref float queen)
        {
            float split = u.MaxHp * (MoaS.ShieldPercent + MoaS.AreaShieldPercent) * 0.5f;
            for (int i = 0; i < u.StatusCount; i++)
            {
                var st = u.GetStatus(i);
                if (st.Type != St.Shield || !(st.Amount > 0f) || !(st.T > 0f) || st.By == null || st.By.Kind != UnitKind.Hero || st.By.Hero != HeroId.Moa) continue;
                float left = st.Amount0 > 0f ? Mathf.Clamp01(st.Amount / st.Amount0) : 1f;
                if (st.Queen) queen = Mathf.Max(queen, left);
                else if (!Rules.OneTapSpecial && st.Amount0 >= split) guard = Mathf.Max(guard, left);
                else field = Mathf.Max(field, left);
            }
        }

        static void HeroMarksOff(UnitRec r)
        {
            if (r.Marked) { r.V.SetDeathmark(-1f); r.Marked = false; }
            if (r.Spark) { r.V.SetChainSpark(false); r.Spark = false; }
            if (r.ChainCd >= 0f) { r.V.SetChainCooldown(-1f); r.ChainCd = -1f; }
            if (r.Honey != HoneyOff) { r.V.SetHoneyShields(-1f, -1f, -1f); r.Honey = HoneyOff; }
        }

        static float MarkLeft(Unit u)
        {
            if ((u.Flags & StatusFlags.Marked) == 0) return -1f;
            for (int i = 0; i < u.StatusCount; i++)
            {
                var st = u.GetStatus(i);
                if (st.Type == St.TiktaMark && st.T > 0f) return st.T;
            }
            return -1f;
        }

        // the Sprint spell's speed status: the hero's own SpeedPct at SprintPct (Spells.cs CastSpell)
        static bool Sprinting(Unit u)
        {
            for (int i = 0; i < u.StatusCount; i++)
            {
                var st = u.GetStatus(i);
                if (st.Type == St.SpeedPct && st.By == u && Mathf.Abs(st.V - SpellDefs.SprintPct) < 1e-4f && st.T > 0f) return true;
            }
            return false;
        }

        /// <summary>Hands every view back (match end).</summary>
        public void Dispose()
        {
            for (int i = 0; i < units.Count; i++) { HeroMarksOff(units[i]); ArtKit.Release(units[i].V); } // pooled views go back plain
            for (int i = 0; i < dying.Count; i++) ArtKit.Release(dying[i].V);
            for (int i = 0; i < shots.Count; i++) if (shots[i].V != null) ArtKit.Release(shots[i].V);
            for (int i = 0; i < zones.Count; i++) ArtKit.Release(zones[i].V);
            units.Clear(); dying.Clear(); shots.Clear(); zones.Clear();
            if (arena != null) Object.Destroy(arena);
            ArtKit.TurnSun(false); // the menus are lit as always after a turned online match
            arena = null;
            unitBySerial.Clear(); shotBySerial.Clear(); zoneBySerial.Clear();
        }
    }
}
