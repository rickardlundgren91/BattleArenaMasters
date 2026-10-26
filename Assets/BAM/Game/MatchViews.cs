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
            public Quaternion Rot;
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
            public Vector3 From, To;
            public float Age, Flight, Linger;
        }

        sealed class ZoneRec
        {
            public int Serial, Seen;
            public ZoneView V;
        }

        struct Pending { public int A; public Unit Src, Tgt; public ProjKind Kind; }

        const float MinionLinger = 0.9f;   // the death animation finishes after the rules drop a minion
        const float SlashLinger = 0.22f;   // a melee slash finishes its sweep after the hit

        readonly Match m;
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
        readonly Stack<ZoneRec> zoneRecPool = new Stack<ZoneRec>(16);
        int step;
        int lastWave;
        bool playerWasReady, playerInGrass, playerLow;
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
            var ev = m.Events;
            for (int i = 0; i < ev.Count; i++) OnEvent(ev[i]);
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
                case UnitKind.Hero: v = ArtKit.CreateHero(u.Hero, u.Team); break;
                case UnitKind.Tower: v = ArtKit.CreateTower(u.Team); break;
                case UnitKind.Wolf: v = ArtKit.CreateWolf(u.Team); break;
                default: v = u == TutDummy ? ArtKit.CreateTarget() : ArtKit.CreateMinion(u.Minion, u.Team); break; // the guided match's practice target is a board
            }
            var r = unitRecPool.Count > 0 ? unitRecPool.Pop() : new UnitRec();
            r.U = u; r.Serial = u.Serial; r.V = v; r.Dead = false; r.Linger = 0f;
            r.Hp01 = -1f; r.Shield01 = -1f; r.Vis = -1f; r.Level = 0; r.Flags = (StatusFlags)0xFFFF;
            r.Rot = Quaternion.LookRotation(Coords.Dir(u.Facing));
            var t = v.transform;
            t.SetPositionAndRotation(Coords.Of(u), r.Rot);
            if (u.Kind == UnitKind.Hero)
                v.SetMarker(u.IsPlayer ? UnitMarker.Player : u.Team == Teams.Player ? UnitMarker.Ally : UnitMarker.Enemy);
            if (!u.Alive) { r.Dead = true; v.SetDead(true); }
            else if (u.Kind == UnitKind.Minion && m.Time > 0f && u != TutDummy) ArtKit.PlayFx(FxKind.MinionSpawn, t.position, 1f, u.Team);
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
                    s.LastT = p.T; s.LastX = p.X; s.LastY = p.Y;
                    if (s.V == null && p.T >= 0f) LaunchView(s, p);
                }
                else if (p.IsZone || p.IsWarning)
                {
                    if (!zoneBySerial.TryGetValue(p.Serial, out ZoneRec z)) z = AddZone(p);
                    z.Seen = step;
                }
            }

            // shots the rules dropped this tick
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                if (s.Ghost || s.Seen == step) continue;
                shotBySerial.Remove(s.Serial);
                s.P = null;
                if (s.V == null)
                {
                    // it left the hand and landed inside one tick, so it was never drawn: show the flight now
                    if (s.LastT + Rules.Tick >= 0f) StartGhost(s, From(s.SrcSerial, s.LastX, s.LastY), Coords.World(s.LastX, s.LastY), 0.1f, s.Melee ? SlashLinger : 0f);
                    else { shots.RemoveAt(i); FreeShot(s); } // cancelled in the hand (stun, death): nothing flew
                }
                else if (s.Melee) StartGhost(s, s.V.transform.position, s.V.transform.position, 0f, SlashLinger);
                else { ArtKit.Release(s.V); shots.RemoveAt(i); FreeShot(s); }
            }

            // an attack whose shot started and ended inside this very tick never showed up in Projs
            for (int i = 0; i < pending.Count; i++)
            {
                var pd = pending[i];
                if (pd.A == 0) continue;
                var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
                Init(s, null, 0, pd.Kind, pd.Src.Team, pd.Src.Serial);
                shots.Add(s);
                Vector3 to = pd.Tgt != null ? Coords.Of(pd.Tgt) : Coords.Of(pd.Src);
                StartGhost(s, Coords.Of(pd.Src), to, 0.1f, s.Melee ? SlashLinger : 0f);
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
            Init(s, p, p.Serial, p.Kind, p.Team, p.Src != null ? p.Src.Serial : 0);
            shots.Add(s);
            shotBySerial[p.Serial] = s;
            // pair it with the AttackStart of this tick, so that attack is not drawn twice
            if (p.Mode == ProjMode.Auto && p.Src != null)
                for (int i = 0; i < pending.Count; i++)
                    if (pending[i].A == p.Src.Serial && pending[i].Kind == p.Kind) { var pd = pending[i]; pd.A = 0; pending[i] = pd; break; }
            return s;
        }

        static void Init(ShotRec s, Proj p, int serial, ProjKind kind, int team, int src)
        {
            s.P = p; s.Serial = serial; s.Kind = kind; s.Team = team; s.SrcSerial = src; s.V = null; s.Ghost = false;
            s.Melee = kind == ProjKind.FennSlash || kind == ProjKind.KnutHit || kind == ProjKind.WolfBite || kind == ProjKind.MeleeMinionHit;
            s.Age = 0f; s.Flight = 0f; s.Linger = 0f; s.LastT = -1f;
        }

        void FreeShot(ShotRec s) { s.P = null; s.V = null; shotRecPool.Push(s); }

        Vector3 From(int srcSerial, float x, float y) =>
            unitBySerial.TryGetValue(srcSerial, out UnitRec r) ? r.V.transform.position : Coords.World(x, y);

        void LaunchView(ShotRec s, Proj p)
        {
            var v = ArtKit.CreateProjectile(p.Kind, p.Team);
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
            if (p.Mode == ProjMode.Hook && p.Src != null) v.SetTether(ViewOf(p.Src));
        }

        void StartGhost(ShotRec s, Vector3 from, Vector3 to, float flight, float linger)
        {
            s.Ghost = true; s.From = from; s.To = to; s.Flight = flight; s.Linger = linger; s.Age = 0f;
            if (s.V == null)
            {
                s.V = ArtKit.CreateProjectile(s.Kind, s.Team);
                s.V.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Flat(to - from)));
                s.V.Launch(from, to, Mathf.Max(flight, 0.05f));
            }
        }

        ZoneRec AddZone(Proj p)
        {
            var z = zoneRecPool.Count > 0 ? zoneRecPool.Pop() : new ZoneRec();
            z.Serial = p.Serial;
            z.V = ArtKit.CreateZone(p.Zone, p.Team);
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

        UnitView V(Unit u) => u != null && unitBySerial.TryGetValue(u.Serial, out UnitRec r) ? r.V : null;

        void OnEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case EventType.AttackStart:
                {
                    var a = e.A;
                    V(a)?.PlayAttack(e.V);
                    pending.Add(new Pending { A = a.Serial, Src = a, Tgt = e.B, Kind = (ProjKind)e.Kind });
                    Sound(AttackSound(a), a, a.Kind == UnitKind.Minion ? 0.55f : a.Kind == UnitKind.Tower ? 0.9f : 0.8f);
                    break;
                }
                case EventType.Hit:
                {
                    var b = e.B;
                    var kind = (ProjKind)e.Kind;
                    Vector3 at = Coords.World(e.X, e.Y);
                    V(b)?.PlayHit(e.Big);
                    FxKind fx = kind == ProjKind.KnutAnchor ? FxKind.AnchorHit : kind == ProjKind.TowerShot ? FxKind.TowerShotFlash : e.Big ? FxKind.HitBig : FxKind.Hit;
                    ArtKit.PlayFx(fx, at, b != null && b.Kind == UnitKind.Tower ? 1.4f : 1f, b != null ? b.Team : 0);
                    if (fx == FxKind.TowerShotFlash) ArtKit.PlayFx(FxKind.Hit, at, 1f, b != null ? b.Team : 0);
                    SfxId id = kind == ProjKind.BramHammerSpecial ? SfxId.BramHammerBounce
                        : b != null && e.V <= 0f && (b.Flags & StatusFlags.Shielded) != 0 ? SfxId.ShieldBlock
                        : e.Big ? SfxId.HitHeavy : SfxId.HitLight;
                    BamAudio.PlayAt(id, at, b != null && b.Kind == UnitKind.Minion ? 0.5f : 0.8f);
                    break;
                }
                case EventType.Blocked:
                    ArtKit.PlayFx(FxKind.Hit, Coords.World(e.X, e.Y), 0.6f);
                    break;
                case EventType.Damage:
                    // special and passive damage without a shot of its own (zones, splashes) still flinches the target
                    if (e.Dmg != DmgKind.Auto && e.V > 0f && e.B != null) V(e.B)?.PlayHit(e.Dmg == DmgKind.Special);
                    break;
                case EventType.Death:
                {
                    var b = e.B;
                    if (b == null) break;
                    if (unitBySerial.TryGetValue(b.Serial, out UnitRec r) && !r.Dead) { r.Dead = true; r.V.SetDead(true); }
                    ArtKit.PlayFx(FxKind.Death, Coords.Of(b), b.Kind == UnitKind.Hero ? 1.2f : 0.8f, b.Team);
                    if (b.Kind == UnitKind.Hero)
                    {
                        if (b == m.Player) BamAudio.Play(SfxId.HeroDeath);
                        else BamAudio.PlayAt(b.Team == Teams.Player ? SfxId.AllyDeath : SfxId.HeroDeath, Coords.Of(b));
                    }
                    else BamAudio.PlayAt(SfxId.MinionDeath, Coords.Of(b), 0.5f);
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
                    ArtKit.PlayFx(FxKind.Respawn, Coords.Of(a), 1f, a.Team);
                    if (a == m.Player) BamAudio.Play(SfxId.Respawn);
                    break;
                }
                case EventType.Cast:
                {
                    var a = e.A;
                    V(a)?.PlayCast();
                    ArtKit.PlayFx(FxKind.Cast, Coords.Of(a), 1f, a.Team);
                    Sound(CastSound((HeroId)e.Kind, e.Big), a, 1f);
                    // Blast's tap bomb is a ground fuse in the rules; draw the lob onto it so the throw is seen
                    if ((HeroId)e.Kind == HeroId.Blast && !e.Big) LobGhost(a, Coords.World(e.X, e.Y), ProjKind.BlastBomb, 0.45f);
                    break;
                }
                case EventType.Interrupt:
                    if (e.A != null) BamAudio.PlayAt(SfxId.Stunned, Coords.Of(e.A));
                    break;
                case EventType.LevelUp:
                    if (m.Player != null && m.Player.Alive) ArtKit.PlayFx(FxKind.LevelUp, Coords.Of(m.Player), 1f, Teams.Player);
                    BamAudio.Play(SfxId.LevelUp, 0.8f);
                    break;
                case EventType.TowerDown:
                    if (e.A != null)
                    {
                        ArtKit.PlayFx(FxKind.TowerDestroyed, Coords.Of(e.A), 1f, e.A.Team);
                        BamAudio.PlayAt(SfxId.TowerDestroyed, Coords.Of(e.A));
                        if (unitBySerial.TryGetValue(e.A.Serial, out UnitRec r) && !r.Dead) { r.Dead = true; r.V.SetDead(true); }
                    }
                    break;
                case EventType.CloneSpawn:
                    if (e.A != null) { ArtKit.PlayFx(FxKind.CloneSpawn, Coords.Of(e.A), 1f, e.A.Team); BamAudio.PlayAt(SfxId.CloneAppear, Coords.Of(e.A)); }
                    break;
                case EventType.CloneEnd:
                    if (e.A != null) { ArtKit.PlayFx(FxKind.CloneEnd, Coords.Of(e.A), 1f, e.A.Team); BamAudio.PlayAt(SfxId.CloneVanish, Coords.Of(e.A)); }
                    break;
                case EventType.WolfSummon:
                    if (e.A != null) ArtKit.PlayFx(FxKind.WolfSummon, Coords.Of(e.A), 1f, e.A.Team);
                    break;
                case EventType.WolfPounce:
                    if (e.A != null)
                    {
                        Vector3 at = e.B != null ? Coords.Of(e.B) : Coords.Of(e.A);
                        ArtKit.PlayFx(FxKind.HitBig, at, 1f, e.A.Team);
                        BamAudio.PlayAt(SfxId.WolfBite, at);
                        if (e.B != null) V(e.B)?.PlayHit(true);
                    }
                    break;
                case EventType.Shield:
                    if (e.Big && e.B != null) BamAudio.PlayAt(SfxId.MoaShield, Coords.Of(e.B));
                    break;
                case EventType.Explosion:
                {
                    Vector3 at = Coords.World(e.X, e.Y);
                    float size = Mathf.Max(0.5f, e.V);
                    ArtKit.PlayFx(FxKind.Explosion, at, size, e.A != null ? e.A.Team : 0);
                    BamAudio.PlayAt((ProjKind)e.Kind == ProjKind.BlastBomb ? SfxId.BlastExplosion : SfxId.HitHeavy, at);
                    break;
                }
                case EventType.Pull:
                    if (e.B != null) BamAudio.PlayAt(SfxId.KnutAnchorHook, Coords.Of(e.B));
                    break;
            }
        }

        void LobGhost(Unit src, Vector3 to, ProjKind kind, float flight)
        {
            var s = shotRecPool.Count > 0 ? shotRecPool.Pop() : new ShotRec();
            Init(s, null, 0, kind, src.Team, src.Serial);
            shots.Add(s);
            StartGhost(s, DrawnAt(src), to, flight, 0f);
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
                case UnitKind.Hero: return SfxId.AttackBram + (int)a.Hero;
                case UnitKind.Tower: return SfxId.TowerShot;
                case UnitKind.Wolf: return SfxId.WolfBite;
                default: return a.Minion == MinionKind.Ranged ? SfxId.MinionRanged : a.Minion == MinionKind.Siege ? SfxId.SiegeShot : SfxId.MinionMelee;
            }
        }

        static SfxId CastSound(HeroId h, bool dragged)
        {
            switch (h)
            {
                case HeroId.Bram: return SfxId.BramHammerThrow;
                case HeroId.Fenn: return dragged ? SfxId.FennWolfThrow : SfxId.FennWolfCall;
                case HeroId.Tyra: return SfxId.TyraRelease;
                case HeroId.Moa: return dragged ? SfxId.MoaField : SfxId.MoaShield;
                case HeroId.Knut: return SfxId.KnutAnchorThrow;
                case HeroId.Tikta: return SfxId.TiktaNeedles;
                case HeroId.Blast: return dragged ? SfxId.BlastFireLane : SfxId.BlastBombThrow;
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
        }

        // ---------------------------------------------------------------- every frame

        public void Frame(float alpha, float dt)
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
                    s.Age += dt;
                    float k = s.Flight > 0f ? Mathf.Clamp01(s.Age / s.Flight) : 1f;
                    s.V.transform.position = Vector3.Lerp(s.From, s.To, k);
                    if (s.Age >= s.Flight + s.Linger) { ArtKit.Release(s.V); shots.RemoveAt(i); FreeShot(s); }
                }
                else if (s.V != null && s.P != null)
                {
                    var p = s.P;
                    s.V.transform.position = Coords.Lerp(p.PX, p.PY, p.X, p.Y, alpha);
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
            Quaternion want = Quaternion.LookRotation(Coords.Dir(u.Facing));
            r.Rot = Quaternion.RotateTowards(r.Rot, want, 900f * dt);
            t.SetPositionAndRotation(pos, r.Rot);

            if (u.Alive == r.Dead) { r.Dead = !u.Alive; v.SetDead(r.Dead); if (!r.Dead) { r.Hp01 = -1f; r.Vis = -1f; } }
            if (r.Dead) return;

            bool moving = m.Time - u.MovedAt < 0.1f;
            float speed01 = 0f;
            if (moving && u.BaseSpeed > 0f) speed01 = Mathf.Sqrt((u.X - u.PX) * (u.X - u.PX) + (u.Y - u.PY) * (u.Y - u.PY)) / Rules.Tick / u.BaseSpeed;
            v.SetMoving(moving, moving ? Mathf.Max(0.5f, speed01) : 0f);

            float hp01 = u.MaxHp > 0f ? Mathf.Clamp01(u.Hp / u.MaxHp) : 0f;
            float sh01 = u.MaxHp > 0f ? Mathf.Clamp01(u.ShieldAmount / u.MaxHp) : 0f;
            if (Mathf.Abs(hp01 - r.Hp01) > 0.0005f || Mathf.Abs(sh01 - r.Shield01) > 0.0005f) { r.Hp01 = hp01; r.Shield01 = sh01; v.SetHealth(hp01, sh01); }
            if (u.Kind == UnitKind.Hero && u.Level != r.Level) { r.Level = u.Level; v.SetLevel(u.Level); }

            var flags = u.Flags;
            if (u.Team != Teams.Player) flags &= ~StatusFlags.Illusion; // the enemy's clone looks exactly like Fenn
            if (m.Map.InGrass(u.X, u.Y)) flags |= StatusFlags.InGrass;
            if (flags != r.Flags) { r.Flags = flags; v.SetStatus(flags); }

            float vis = !m.IsVisibleTo(u, Teams.Player) ? 0f : u.Team == Teams.Player && u.Kind == UnitKind.Hero && m.HiddenFromFoes(u) ? 0.5f : 1f;
            if (Hidden != null && Hidden(u)) vis = 0f; // the match intro: not yet beamed in
            if (vis != r.Vis) { r.Vis = vis; v.SetVisibility(vis); }
        }

        /// <summary>Hands every view back (match end).</summary>
        public void Dispose()
        {
            for (int i = 0; i < units.Count; i++) ArtKit.Release(units[i].V);
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
