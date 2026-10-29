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
        /// <summary>The rules' angle of a screen direction (screen up is -y on the map, or +y when Turned).</summary>
        public static float ScreenAngle(Vector2 v) { v = Screen(v); return Mathf.Atan2(-v.y, v.x); }
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
            public StatusFlags Flags;
            public float Channel = -1f;    // the recall channel shown, -1 none
            public bool Sprint;
            public bool Marked, Spark;     // Tikta's Deathmark and Blast's chain spark shown (Tikta 2.0 / Blast 2.0)
            public float ChainCd = -1f;    // the player's Blast: the chain cooldown arc shown, -1 none
            public float StoredFx;         // Oaken: when his Stored Impact glow was last puffed (OAKEN-02)
            public int Honey = HoneyOff;   // Moa's shield rings shown: lit cells per ring (HoneyKey)
            public Quaternion Rot;
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
        }

        sealed class ZoneRec
        {
            public int Serial, Seen;
            public ZoneView V;
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

        // The training island's bridge and the lane below it appear only when the bridge opens: until then the arena is
        // built with those rows left out, then built again whole.
        void BuildArena()
        {
            bool rebuild = arena != null;
            if (rebuild) Object.Destroy(arena);
            bridgeShown = !Island || m.Tut.BridgeOpen;
            var info = ArenaInfoOf(m.Map, bridgeShown ? int.MaxValue : IslandPlatformRows, !Island);
            info.Island = Island;
            info.LaneRises = Island && rebuild; // the opened bridge and lane rise into place
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
            return new ArenaInfo
            {
                Width = map.W, Height = map.H, Tiles = tiles, Rocks = rocks,
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

        UnitRec AddUnit(Unit u)
        {
            UnitView v;
            switch (u.Kind)
            {
                case UnitKind.Hero: v = ArtKit.CreateHero(u.Hero, Side(u.Team)); break;
                case UnitKind.Tower: v = ArtKit.CreateTower(Side(u.Team)); break;
                case UnitKind.Wolf: v = ArtKit.CreateWolf(Side(u.Team)); break;
                default: v = u == TutDummy ? ArtKit.CreateTarget() : ArtKit.CreateMinion(u.Minion, Side(u.Team)); break; // the guided match's practice target is a board
            }
            var r = unitRecPool.Count > 0 ? unitRecPool.Pop() : new UnitRec();
            r.U = u; r.Serial = u.Serial; r.V = v; r.Dead = false; r.Linger = 0f;
            r.Hp01 = -1f; r.Shield01 = -1f; r.Vis = -1f; r.Level = 0; r.Flags = (StatusFlags)0xFFFF; r.Honey = HoneyOff;
            r.Rot = Quaternion.LookRotation(Coords.Dir(u.Facing));
            r.Bar = null;
            foreach (var mr in v.GetComponentsInChildren<MeshRenderer>(true)) if (mr.gameObject.name == "Bar") { r.Bar = mr; break; }
            var t = v.transform;
            t.SetPositionAndRotation(Coords.Of(u), r.Rot);
            if (u.Kind == UnitKind.Hero)
                v.SetMarker(u.IsPlayer ? UnitMarker.Player : u.Team == Mine ? UnitMarker.Ally : UnitMarker.Enemy);
            if (!u.Alive) { r.Dead = true; v.SetDead(true); }
            else if (u.Kind == UnitKind.Minion && m.Time > 0f && u != TutDummy) ArtKit.PlayFx(FxKind.MinionSpawn, t.position, 1f, Side(u.Team));
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

        void FreeShot(ShotRec s) { s.P = null; s.V = null; shotRecPool.Push(s); }

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
            if (MuzzleOf(s.SrcSerial, out Vector3 mz)) v.LaunchFrom(mz); // from the hand or weapon, easing onto the rules' path (SHOT-02)
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
            zones.Add(z);
            zoneBySerial[p.Serial] = z;
            return z;
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
                    V(a)?.PlayAttack(e.V);
                    // a swing by a hider the player cannot see is not heard: attacking does not reveal, only a hit does (BONK-09)
                    bool heard = m.IsVisibleTo(a, Mine);
                    // Bonk's club has no shot: it lands e.V s later as an Explosion of ProjKind.BonkClub (cracks and dust)
                    if ((ProjKind)e.Kind == ProjKind.BonkClub) { if (heard) Sound(SfxId.BonkSlamWindup, a, e.Big ? 0.8f : 0.55f); break; }
                    pending.Add(new Pending { A = a.Serial, Src = a, Tgt = e.B, Kind = (ProjKind)e.Kind });
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
                    if ((a.Kind != UnitKind.Tower || e.B == m.Player) && !(a.Kind == UnitKind.Hero && a.Hero == HeroId.Tyra)) Sound(AttackSound(a), a, a.Kind == UnitKind.Minion ? 0.55f : a.Kind == UnitKind.Tower ? 0.9f : 0.8f);
                    // a tower shooting someone else within 8 tiles of the player is heard softly (VIS-02, Rickard 2026-10-29)
                    else if (a.Kind == UnitKind.Tower && m.Player != null && e.B != null && SimMath.Dist(e.B, m.Player) <= 8f) Sound(SfxId.TowerShot, a, 0.35f);
                    break;
                }
                case EventType.Hit:
                {
                    var b = e.B;
                    var kind = (ProjKind)e.Kind;
                    Vector3 at = Coords.World(e.X, e.Y);
                    V(b)?.PlayHit(e.Big);
                    if (e.Shot != 0 && !shotHitAt.ContainsKey(e.Shot)) { shotHitAt[e.Shot] = at; shotHits.Add(e); } // its own hit comes first
                    if (kind == ProjKind.BonkSlam && b != null && b.IsRealHero) BamAudio.PlayAt(SfxId.BonkStun, Coords.Of(b), 0.8f); // the slam stuns
                    // Bram's bounces get their own gold line; Tyra's bolt and Tikta's needles carry the special's look in their own
                    // impact, so they add only the small hit (fx review 2026-10-26, P1-2/P1-3)
                    FxKind fx = kind == ProjKind.KnutAnchor ? FxKind.AnchorHit : kind == ProjKind.TowerShot ? FxKind.TowerShotFlash
                        : kind == ProjKind.BramHammerSpecial ? (e.Shot != 0 ? FxKind.Bounce : FxKind.HitBig) // the tap's flying hammer bounces; the anvil (no shot) lands
                        : kind == ProjKind.TyraBolt || kind == ProjKind.TiktaNeedleSpecial ? FxKind.Hit
                        : e.Big ? FxKind.HitBig : FxKind.Hit;
                    float hs = (b != null && b.Kind == UnitKind.Tower ? 1.4f : 1f) * (e.A != null && e.A.Kind == UnitKind.Minion ? 0.6f : 1f); // VIS-03
                    ArtKit.PlayFx(fx, at, hs, b != null ? Side(b.Team) : 0, HeroOf(e.A));
                    if (fx == FxKind.TowerShotFlash) ArtKit.PlayFx(FxKind.Hit, at, 1f, b != null ? Side(b.Team) : 0);
                    SfxId id = kind == ProjKind.BramHammerSpecial ? SfxId.BramHammerBounce
                        : b != null && e.V <= 0f && (b.Flags & StatusFlags.Shielded) != 0 ? SfxId.ShieldBlock
                        // as in Speltest's sound mix v2: the plain hit thumps only when a hero's (or the wolf's) blow lands on you
                        : b == m.Player && e.A != null && (e.A.Kind == UnitKind.Hero || e.A.Kind == UnitKind.Wolf) ? (e.Big ? SfxId.HitHeavy : SfxId.HitLight)
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
                    if (!tickNow) HitConfirm(e);
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
                        if (b == m.Player) BamAudio.Play(SfxId.HeroDeath);
                        else if (myKill) BamAudio.Play(SfxId.HeroDeath); // the kill confirm: loud, not by distance (Brawl Stars idea 5); no extra chime (Speltest sound mix v2)
                        else BamAudio.PlayAt(b.Team == Mine ? SfxId.AllyDeath : SfxId.HeroDeath, Coords.Of(b));
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
                    V(a)?.PlayCast();
                    ArtKit.PlayFx(FxKind.Cast, Coords.Of(a), 1f, Side(a.Team), e.Kind);
                    if (a == m.Player) BamAudio.Duck(0.8f);
                    Sound(CastSound((HeroId)e.Kind, e.Big), a, 1f);
                    // Tyra's tap is the Arrow Volley: a gold cone through the draw and the big bow draw (Speltest tyravolley)
                    if ((HeroId)e.Kind == HeroId.Tyra && !e.Big)
                    {
                        ArtKit.PlayVolleyDraw(Coords.Of(a), Coords.World(e.X, e.Y) - Coords.Of(a), a.Range, TyraS.VolleyWindup);
                    }
                    // Blast's tap bomb is a ground fuse in the rules; draw the lob onto it so the throw is seen
                    if ((HeroId)e.Kind == HeroId.Blast && !e.Big) LobGhost(a, Coords.World(e.X, e.Y), ProjKind.BlastBomb, 0.45f);
                    // so are Hilde's kegs: the big one (drag) and the small quick one (tap)
                    // Bram's Anvil Drop: his hammer goes up high and comes down on the red circle as it lands (BRAM-04)
                    if ((HeroId)e.Kind == HeroId.Bram && e.Big) LobGhost(a, Coords.World(e.X, e.Y), ProjKind.BramHammerSpecial, BramS.DropDelay, 2f);
                    if ((HeroId)e.Kind == HeroId.Hilde) LobGhost(a, Coords.World(e.X, e.Y), e.Big ? ProjKind.HildeKeg : ProjKind.HildeBurst, e.Big ? 0.4f : 0.3f);
                    break;
                }
                case EventType.Interrupt:
                    if (e.A != null) BamAudio.PlayAt(SfxId.Stunned, Coords.Of(e.A));
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
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) { ArtKit.PlayFx(FxKind.CloneSpawn, Coords.Of(e.A), 1f, Side(e.A.Team)); BamAudio.PlayAt(SfxId.CloneAppear, Coords.Of(e.A)); } // FENN-09
                    break;
                case EventType.CloneEnd:
                    if (e.A != null && unitBySerial.TryGetValue(e.A.Serial, out UnitRec cv) && cv.Vis > 0f) { ArtKit.PlayFx(FxKind.CloneEnd, Coords.Of(e.A), 1f, Side(e.A.Team)); BamAudio.PlayAt(SfxId.CloneVanish, Coords.Of(e.A)); } // FENN-09
                    break;
                case EventType.WolfSummon:
                    if (e.A != null && m.IsVisibleTo(e.A, Mine)) ArtKit.PlayFx(FxKind.WolfSummon, Coords.Of(e.A), 1f, Side(e.A.Team));
                    break;
                case EventType.WolfPounce:
                    // the bite sounds on prey only; its hit look comes from the rules' own Hit event (fx review P2-3). A landing on
                    // nothing shows no bite, only a soft landing
                    if (e.A != null && e.B != null) BamAudio.PlayAt(SfxId.WolfBite, Coords.Of(e.B));
                    else if (e.A != null && m.IsVisibleTo(e.A, Mine)) ArtKit.PlayFx(FxKind.WolfLand, Coords.World(e.X, e.Y), 1f, Side(e.A.Team));
                    break;
                case EventType.Shield:
                    if (e.Big && e.B != null) BamAudio.PlayAt(SfxId.MoaShield, Coords.Of(e.B));
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
                    FxKind ek = pk == ProjKind.BramHammer || pk == ProjKind.BramHammerSpecial ? FxKind.AnvilStrike : pk == ProjKind.KnutSlam ? FxKind.AnchorHit
                        : pk == ProjKind.BlastChain ? FxKind.ChainBoom : bonk ? FxKind.GroundSmash : pk == ProjKind.OakenSlam ? FxKind.OakenSlam : own ? FxKind.KegBlast : FxKind.Explosion;
                    // Bonk: his auto slam and Ground Slam, both the club into the ground, drawn at the true hit radius (a power swing
                    // is no wider in the rules, so it is told by its sound and shake, not a bigger ring: BONK-06)
                    float esize = pk == ProjKind.BlastChain || bonk ? Mathf.Max(0.1f, e.V) : size;
                    if (own) esize = Mathf.Max(0.1f, e.V);
                    ArtKit.PlayFx(ek, at, esize, e.A != null ? Side(e.A.Team) : 0, HeroOf(e.A));
                    SfxId boom = pk == ProjKind.BlastBomb || pk == ProjKind.HildeKeg || pk == ProjKind.HildeBurst ? SfxId.BlastExplosion : pk == ProjKind.OakenSlam ? SfxId.BonkSlam : pk == ProjKind.BlastChain ? SfxId.BlastChainBoom
                        : pk == ProjKind.BonkSlam || (pk == ProjKind.BonkClub && e.Big) ? SfxId.BonkSlam : pk == ProjKind.BonkClub ? SfxId.AttackBonk
                        : pk == ProjKind.BramHammer || pk == ProjKind.BramHammerSpecial ? SfxId.BramAnvil // Bram's Anvil Drop: Speltest's anvil (bramsmash), BRAM-04
                        : pk == ProjKind.KnutSlam ? SfxId.BonkSlam : SfxId.None; // Knut's Anchor Slam borrows Bonk's ground slam until he has his own (KNUT-03)
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
                    ArtKit.PlayChainWarn(Coords.World(e.X, e.Y), Mathf.Max(0.1f, e.V), BlastChainS.Delay);
                    break;
                // Bonk's boulder knocks a hero back: the rules slide it (drawn between ticks as any move), a thud and dust where it was hit
                case EventType.Knock:
                    if (e.B != null && m.IsVisibleTo(e.B, Mine))
                    {
                        BamAudio.PlayAt(SfxId.BonkKnock, Coords.Of(e.B));
                        ArtKit.PlayFx(FxKind.GroundSmash, Coords.Of(e.B), 0.45f, Side(e.B.Team));
                    }
                    break;
                case EventType.Pull:
                    if (e.B != null) BamAudio.PlayAt(SfxId.KnutAnchorHook, Coords.Of(e.B));
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
                        Sound(sp == SpellId.Blink ? SfxId.SpellBlink : sp == SpellId.Shield ? SfxId.SpellShield : SfxId.None, e.A, 0.8f);
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
                case UnitKind.Hero: return a.Hero == HeroId.Bonk || a.Hero == HeroId.Oaken ? SfxId.AttackBonk : a.Hero == HeroId.Hilde ? SfxId.AttackHilde : SfxId.AttackBram + (int)a.Hero; // Bonk's came after the seven; Oaken borrows Bonk's until he has his own
                case UnitKind.Tower: return SfxId.TowerShot;
                case UnitKind.Wolf: return SfxId.WolfBite;
                default: return SfxId.None; // minions are silent (Rickard 2026-10-26)
            }
        }

        static SfxId CastSound(HeroId h, bool dragged)
        {
            switch (h)
            {
                case HeroId.Bram: return SfxId.BramHammerThrow;
                case HeroId.Fenn: return dragged ? SfxId.FennWolfThrow : SfxId.FennWolfCall;
                case HeroId.Tyra: return SfxId.TyraDraw; // the draw creak; the twang plays at the release (ReleaseSound)
                case HeroId.Moa: return dragged ? SfxId.MoaField : SfxId.MoaShield;
                case HeroId.Knut: return SfxId.KnutAnchorThrow;
                case HeroId.Tikta: return SfxId.TiktaNeedles;
                case HeroId.Blast: return dragged ? SfxId.BlastFireLane : SfxId.BlastBombThrow;
                case HeroId.Bonk: return dragged ? SfxId.BonkBoulderThrow : SfxId.BonkSlamWindup; // the slam's landing sounds on its Explosion
                case HeroId.Hilde: return SfxId.HildeKegThrow; // both kegs (Speltest sp_hilde)
                case HeroId.Oaken: return SfxId.BonkSlamWindup; // borrowed until he has his own
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
            if (m.WaveN != lastWave) { lastWave = m.WaveN; BamAudio.Play(SfxId.WaveSpawn, 0.5f); }
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
        }

        void DrawUnit(UnitRec r, float alpha, float dt)
        {
            var u = r.U;
            var v = r.V;
            var t = v.transform;
            // a respawn or a long pull jumps; drawing the jump between ticks would slide the hero across the map
            bool jump = (u.X - u.PX) * (u.X - u.PX) + (u.Y - u.PY) * (u.Y - u.PY) > 4f;
            Vector3 pos = jump ? Coords.Of(u) : Coords.Lerp(u.PX, u.PY, u.X, u.Y, alpha);
            bool hero = u.Kind == UnitKind.Hero, aim = u.AimT > m.Time;
            // AnimFix.SquaredFacing (Speltest CombatFeel.Squared): in a fight a hero keeps facing its target between swings, its legs
            // turned to the travel (BodyView), instead of the rules' snap to it for each windup and back to the path after; an aimed
            // swing or special keeps its own direction; and it turns smoothly, not at 900 degrees a second
            bool fight = AnimFix.SquaredFacing && hero && !aim && Fighting(u);
            float face = fight ? SimMath.Atan2(u.Target.Y - u.Y, u.Target.X - u.X) : u.Facing;
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
                HeroMarks(r, u, v);
            }

            float vis = !m.IsVisibleTo(u, Mine) ? 0f : u.Team == Mine && u.Kind == UnitKind.Hero && m.HiddenFromFoes(u) ? 0.5f : 1f;
            if (Hidden != null && Hidden(u)) vis = 0f; // the match intro: not yet beamed in
            if (vis != r.Vis) { r.Vis = vis; v.SetVisibility(vis); }
        }

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
        /// shield told apart by what they started with (18 % and 6 % of max health). -1 where there is none.</summary>
        static void HoneyLeft(Unit u, ref float guard, ref float field, ref float queen)
        {
            float split = u.MaxHp * (MoaS.ShieldPercent + MoaS.AreaShieldPercent) * 0.5f;
            for (int i = 0; i < u.StatusCount; i++)
            {
                var st = u.GetStatus(i);
                if (st.Type != St.Shield || !(st.Amount > 0f) || !(st.T > 0f) || st.By == null || st.By.Kind != UnitKind.Hero || st.By.Hero != HeroId.Moa) continue;
                float left = st.Amount0 > 0f ? Mathf.Clamp01(st.Amount / st.Amount0) : 1f;
                if (st.Queen) queen = Mathf.Max(queen, left);
                else if (st.Amount0 >= split) guard = Mathf.Max(guard, left);
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
            arena = null;
            unitBySerial.Clear(); shotBySerial.Clear(); zoneBySerial.Clear();
        }
    }
}
