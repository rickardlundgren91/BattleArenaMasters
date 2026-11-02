using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: InternalsVisibleTo("BAM.Art.Editor")]

namespace BAM.Art
{
    // CONTRACT between BAM.Art (owner: the graphics thread) and BAM.Game (owner: the main thread). Docs/CONTRACTS.md explains it.
    // The game layer creates every visual through ArtKit, places it each frame through its transform, and calls the view
    // methods when something happens. Art never reads the rules and never moves units itself.
    //
    // World space: 1 tile = 1 unit, y is up, the map lies in the xz plane. The player's base is at the bottom of the
    // screen (small z), the enemy's at the top (large z). ArenaInfo.TileCenter maps a tile to the world.

    /// <summary>The static map handed to <see cref="ArtKit.BuildArena"/>.</summary>
    public sealed class ArenaInfo
    {
        public int Width, Height;
        /// <summary>Width*Height tile codes, row-major, row 0 = the top row (enemy side): 0 void, 1 ground, 2 grass, 3 wall.</summary>
        public byte[] Tiles;
        /// <summary>Round boulders in the middle of the map: world x, world z, radius.</summary>
        public Vector3[] Rocks;
        /// <summary>Tower positions in world space, index = team.</summary>
        public Vector3[] Towers;
        /// <summary>The guided match's round training island (Tutorial island map): rows 0-16 are drawn as Rickard's painted
        /// platform, rows 17+ as the bridge and lane, all with his island textures, over a sky with a cloud sea.</summary>
        public bool Island;
        /// <summary>Island only: the bridge and lane (rows 17+) rise 3 units into place over 0.9 s instead of standing there.
        /// Set it when the arena is rebuilt because the bridge opened.</summary>
        public bool LaneRises;
        /// <summary>Online, top team: the camera looks from the other end of the map (MatchCamera turns half a circle), so the
        /// pictures that stand toward the camera (the middle boulders) stand on the boulder's other side.</summary>
        public bool Turned;

        public int Tile(int tx, int ty) => Tiles[ty * Width + tx];
        /// <summary>World centre of tile (tx, ty), ty counted from the top row.</summary>
        public Vector3 TileCenter(int tx, int ty) => new Vector3(tx + 0.5f, 0f, Height - ty - 0.5f);
    }

    public enum UnitMarker : byte { None = 0, Player = 1, Ally = 2, Enemy = 3 }

    /// <summary>Visual for one unit. The game layer sets transform.position (feet on y = 0) and rotation (facing) every frame.</summary>
    public abstract class UnitView : MonoBehaviour
    {
        /// <summary>Walk or idle animation. speed01 = current speed / normal speed.</summary>
        public abstract void SetMoving(bool moving, float speed01);
        /// <summary>An auto attack starts; the hit or projectile comes windup seconds later.</summary>
        public abstract void PlayAttack(float windup);
        /// <summary>The special is cast.</summary>
        public abstract void PlayCast();
        /// <summary>The special is cast, tapped or dragged (the Cast event's Big): a view that times the gesture by form (a slam lands
        /// later than a throw) takes it, the rest play the cast.</summary>
        public virtual void PlayCast(bool drag) => PlayCast();
        /// <summary>A stun, root or knockback cut the special before its release (EventType.Interrupt), with <paramref name="swing"/>
        /// (a stun) also a swing still winding up: the gesture stops where it is and fades out instead of playing its blow, and a
        /// weapon waiting to leave the hand stays in it.</summary>
        public virtual void Interrupt(bool swing) { }
        /// <summary>Took damage. big = a crit-like hit (passive bonus, special).</summary>
        public abstract void PlayHit(bool big);
        /// <summary>Died (play the death and hide) or came back (respawn).</summary>
        public abstract void SetDead(bool dead);
        /// <summary>World-space bar above the unit. hp01 and shield01 are fractions of max health.</summary>
        public abstract void SetHealth(float hp01, float shield01);
        /// <summary>Hero level shown on the bar (1..15). Ignored by minions and towers.</summary>
        public abstract void SetLevel(int level);
        /// <summary>Max health of a hero, for the notches every 100 HP on its bar; 0 = none (minions, towers).</summary>
        public virtual void SetMaxHp(float maxHp) { }
        /// <summary>A hero's special charge as the thin row under its bar (MLBB's mana row): 0..1, below 0 = no row.</summary>
        public virtual void SetEnergy(float charge01, bool ready) { }
        /// <summary>Status effects to show (stun stars, slow, burn, shield bubble, ...).</summary>
        public abstract void SetStatus(StatusFlags flags);
        /// <summary>1 = fully visible, about 0.5 = your own unit hidden in grass, 0 = not visible to the player (hide everything).</summary>
        public abstract void SetVisibility(float alpha);
        /// <summary>Ring under the unit: the player's hero, allies, enemies.</summary>
        public abstract void SetMarker(UnitMarker marker);
        /// <summary>Recall channel: 0..1 while Match.SpellChanneling(unit), below 0 when it stops. Mint motes rise round the feet.</summary>
        public virtual void SetChannel(float progress01) { }
        /// <summary>The Sprint spell is on: a lime speed trail while the unit moves.</summary>
        public virtual void SetSprint(bool on) { }
        /// <summary>The unit faces a target or an aim, not the way it walks (AnimFix.LegsFollow: its legs turn to the travel).</summary>
        public virtual void SetSquared(bool on) { }
        /// <summary>Tikta's Deathmark on this hero: seconds left, below 0 = off. A sigil over the head and a violet ring at the feet,
        /// blinking in the last 0.75 s. Call it only while the player can see the unit.</summary>
        public virtual void SetDeathmark(float left) { }
        /// <summary>A gold and violet spark crown over a burning enemy while the player's Blast can chain on it.</summary>
        public virtual void SetChainSpark(bool on) { }
        /// <summary>The player's own Blast: Chain Reaction cooldown 0..1 as a violet arc at the feet that fills, below 0 = off.</summary>
        public virtual void SetChainCooldown(float frac) { }
        /// <summary>Moa's shields on this hero, each as the share still left (0..1), below 0 = none: Honey Guard (the tap), the honey
        /// field's shield and Queen's Instinct. A ring of hex cells at the feet that go dark as the shield is used. Call it only while
        /// the player can see the unit.</summary>
        public virtual void SetHoneyShields(float guard, float field, float queen) { }
        /// <summary>Fenn's thrown wolf in the air: how far along its throw it is (0..1) and the throw's length in tiles; progress below 0
        /// = on the ground. It leaps over the flight (FENN-07).</summary>
        public virtual void SetLeap(float progress, float length) { }
        /// <summary>Free ground in front of the unit along its facing, in tiles: a long body (Fenn's wolf, about 2 tiles on a 0.2-tile
        /// disc) draws back from a wall instead of putting its head in it (FENN-06).</summary>
        public virtual void SetRoom(float ahead) { }
        /// <summary>Where a hero's shot leaves its hand now (world space, at its height), for the shot's first drawn point (SHOT-02):
        /// Hilde's cannon mouth, the arrow on Tyra's bow, Blast's bomb, the head of a held weapon (Bram's hammer, Knut's anchor, the
        /// needle in the fist Tikta throws with), Moa's casting hand, Bonk's boulder between his hands. False for the rest (minions,
        /// towers, the wolf, code figures).</summary>
        public virtual bool Muzzle(out Vector3 at) { at = Vector3.zero; return false; }
    }

    public abstract class ProjectileView : MonoBehaviour
    {
        /// <summary>The game layer moves it every frame; Launch tells the look where it goes and how long it flies.</summary>
        public abstract void Launch(Vector3 from, Vector3 to, float flightTime);
        /// <summary>Knut's anchor: draw a chain from the projectile back to this point every frame (null = no chain).</summary>
        public virtual void SetTether(Transform anchor) { }
        /// <summary>The shot hit something: its splash plays when it ends. Shots never marked end in a small fizzle (a miss).</summary>
        public virtual void MarkHit() { }
        /// <summary>The shot hurt someone earlier and flew on (Tyra's volley passes through): it ends quietly, no splash, no fizzle.</summary>
        public virtual void MarkSpent() { }
        /// <summary>How far along its flight a homing shot is (0..1) by the rules, so an arc lands when the shot does, not by the clock.</summary>
        public virtual void SetProgress(float k) { }
        /// <summary>Where the shooter's hand or weapon is at the release: the look starts there and eases onto the rules' path.</summary>
        public virtual void LaunchFrom(Vector3 muzzle) { }
        /// <summary>A ghost thrown high (Bram's Anvil Drop hammer): the arc's top in tiles over the flight, whatever its kind.</summary>
        public virtual void SetLob(float height) { }
    }

    public abstract class ZoneView : MonoBehaviour
    {
        /// <summary>A circle (length = 0) or a strip from center along dir. duration = how long it lasts (for fades/fuses).</summary>
        public abstract void Show(Vector3 center, Vector3 dir, float length, float radius, float duration);
        /// <summary>After Show: an arrow from the centre along dir, length tiles long, for the way the zone pushes who it hits
        /// (Hilde's keg, HILDE-02); 0 = none. Show clears it.</summary>
        public virtual void SetPush(float length) { }
        /// <summary>After Show: a circle cut to how far its blast reaches each way (world offsets from the centre), BLAST-05.</summary>
        public virtual void CutTo(Vector3[] reach) { }
        /// <summary>After Show: a strip warning's fill reaches along tiles from its start (it follows its shot's tip), instead of
        /// racing to the end by the clock.</summary>
        public virtual void SetFront(float along) { }
    }

    /// <summary>The aim shown while the player drags the special button, and the attack range ring.</summary>
    public abstract class AimView : MonoBehaviour
    {
        public abstract void ShowLine(Vector3 from, Vector3 dir, float length, float width, bool valid);
        public abstract void ShowCircle(Vector3 center, float radius, bool valid);
        public abstract void ShowRange(Vector3 center, float radius);
        public abstract void Hide();
    }

    public enum FxKind : byte
    {
        Hit = 0, HitBig = 1, Death = 2, Respawn = 3, LevelUp = 4, Explosion = 5, TowerDestroyed = 6, ShieldPop = 7,
        Cast = 8, Bounce = 9, WolfSummon = 10, CloneSpawn = 11, CloneEnd = 12, MinionSpawn = 13, TowerShotFlash = 14, AnchorHit = 15,
        AnvilStrike = 16, WolfLand = 17, DeathmarkTaken = 18, ChainBoom = 19,
        GroundSmash = 20, // Bonk's club into the ground (his slow overhead auto attack and Ground Slam): size = the hit radius
        // Hilde and Oaken's own looks (2026-10-29, HILDE-03 / OAKEN-02), size = the hit radius where there is one
        KegBlast = 21,    // Hilde's keg goes off (Speltest KegVerdictFx): a hot core, chips, and an amber edge exactly on the radius
        WallCrash = 22,   // a keg slid a hero into a wall: a crack, a flash and stone at the wall
        OakenSlam = 23,   // Oaken's fists into the ground: gold-lit cracks and a stone ring on the radius
        OakenGuard = 24,  // Stored Impact takes part of a hit: a stone skin flash in his gold
        OakenStored = 25, // while Stored Impact holds damage for his next punch: a gold glow at his fists (sent every few frames)
        HildeKnock = 26,  // a hero shoved by Hilde's keg: Rickard's powder puff (tx_fx_hilde_knock) where it was hit
        // Tyra's Arrow Volley read as hers (Skill Visibility 2.0, 2026-10-30)
        VolleyHit = 27,     // a volley arrow passes through an enemy: the volley's gold star on that enemy (the arrow flies on)
        VolleyRelease = 28, // the five arrows leave the bow: a short gold flash at the bow, once per volley
        AnvilDrop = 29,     // Bram's Anvil Drop lands (Rickard 2026-10-30 "måste synas mer"): its own big blow, size = the hit radius
        // Rickard's own pictures 2026-10-31 (thread "ultgra"): the heal plus signs and the player's kill burst
        Heal = 30,          // a green "+N" shows on a friendly hero (HudTop): his plus signs float up over it
        Kill = 31,          // the player's kill on an enemy hero: his gold burst on the body
        HookDrag = 32,      // a hero hauled in by Knut's hook: a small puff of ground dust at its feet (sent every few frames)
    }

    public static class ArtKit
    {
        static bool ready;
        static Transform root;
        static Light sun;
        static readonly Dictionary<int, Stack<BodyView>> bodyPools = new Dictionary<int, Stack<BodyView>>();
        static readonly Dictionary<int, Stack<ProjView>> projPools = new Dictionary<int, Stack<ProjView>>();
        static readonly Dictionary<int, Stack<ZoneLook>> zonePools = new Dictionary<int, Stack<ZoneLook>>();
        static readonly List<Renderer> casters = new List<Renderer>(64);

        static bool paused;
        static float slow = 1f;
        /// <summary>The art layer's frame time: 0 while the game is paused and slowed with the rules in a hit-stop, so warnings, zones,
        /// shots and effects wait with the sim.</summary>
        internal static float Dt => paused ? 0f : Time.deltaTime * slow;
        /// <summary>How fast the picture runs against the clock: 1, or less in a hit-stop while the rules' ticks come slower.</summary>
        internal static float Slow => slow;

        /// <summary>The hit-stop feeds the rules' ticks at k of the clock (GameFlow, MatchJuice.Stopping; 1 = normal): the bodies, shots
        /// and effects go as slow, so a swing never lands before its shot leaves (CHAINMAP-07: they ran on at full speed).</summary>
        public static void SetSlow(float k) => slow = Mathf.Clamp01(k);

        static long simTicks = -1;
        static float simAlpha;
        /// <summary>The rules' tick count now, -1 outside a match (Animation Foundation: the swings' clock, SetSimClock).</summary>
        internal static long SimTicks => simTicks;
        internal static float SimAlpha => simAlpha;

        /// <summary>Every frame of a match, after its ticks (GameFlow): the rules' tick count (Match.Ticks) and the drawn share of the
        /// next tick (alpha). A hero's swing is then timed on the rules' clock (AnimTiming.SimElapsed), so its blow shows on the frame
        /// its shot leaves at any frame rate, through hitches, pause, hit-stop and online catch-up. ticks below 0 = no match: the
        /// swings time themselves by the frame clock as before.</summary>
        public static void SetSimClock(long ticks, float alpha) { simTicks = ticks; simAlpha = alpha; }

        /// <summary>The game was paused or resumed (GameFlow.Pause/Resume): freezes the one-shot effects, particles, zones and shots.</summary>
        public static void SetPaused(bool on)
        {
            if (paused == on) return;
            paused = on;
            if (ready) Fx.SetPaused(on);
        }

        internal static bool High = true;
        internal static bool Shadows => High;
        /// <summary>Bumped on every quality change; views compare it to know when to re-apply their settings.</summary>
        internal static int QualityVersion;
        internal static Material TrailMaterial;
        /// <summary>The arena's static geometry: lit by the ambient only, as the old game (it had no sun). An unnamed layer.</summary>
        internal const int ArenaLayer = 29;

        /// <summary>Once at boot. Builds shared materials and meshes and warms the pools.</summary>
        public static void Init()
        {
            if (ready) return;
            ready = true;
            var go = new GameObject("BAM.Art");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
            go.AddComponent<ArtDriver>();
            go.AddComponent<ShaderWarm>(); // PERF-04: builds the recorded shader states in the menu (Resources/BAMArt/Shaders)

            ArtMaterials.Init();
            TrailMaterial = ArtMaterials.Instance(ArtMaterials.ParticleAdd);
            TrailMaterial.SetTexture(ArtMaterials.BaseMap, Texture2D.whiteTexture);
            TrailMaterial.mainTexture = Texture2D.whiteTexture;
            Fx.Init(root);
            SetupLighting();

            // build the shared meshes now so the first minion wave does not hitch
            for (int t = 0; t < 2; t++)
            {
                Models.Minion(MinionKind.Melee, t); Models.Minion(MinionKind.Ranged, t); Models.Minion(MinionKind.Siege, t);
                Models.Tower(t); Models.Wolf(t);
            }
            Shapes.Decal(UnitMarker.None); Shapes.Decal(UnitMarker.Player); Shapes.Decal(UnitMarker.Ally); Shapes.Decal(UnitMarker.Enemy);
            Warm();
            ApplyQuality(BamSettings.Graphics);
        }

        static void Warm()
        {
            // a wave is 2 melee + 1 ranged per team (+1 siege every third) every 16 s; a pushed lane can stack two or three waves
            // (PERF-06), so enough views that a long match never builds one in the middle of a fight
            for (int t = 0; t < 2; t++)
            {
                WarmBodies(MinionKind.Melee, t, 8);
                WarmBodies(MinionKind.Ranged, t, 8);
                WarmBodies(MinionKind.Siege, t, 3);
                WarmProjectiles(ProjKind.RangedMinionShot, t, 8);
                WarmProjectiles(ProjKind.MeleeMinionHit, t, 8);
                // one of every hero shot and ground zone, so a hero's first arrow, bomb or field does not build its look (textures,
                // materials, meshes) mid-fight (PERF-04); the pool grows from there if a fight needs more
                foreach (ProjKind k in System.Enum.GetValues(typeof(ProjKind)))
                    if (k != ProjKind.RangedMinionShot && k != ProjKind.MeleeMinionHit) WarmProjectiles(k, t, 1);
                foreach (ZoneKind z in System.Enum.GetValues(typeof(ZoneKind))) Release(CreateZone(z, t));
            }
        }

        static void WarmBodies(MinionKind kind, int team, int n)
        {
            var list = new UnitView[n];
            for (int i = 0; i < n; i++) list[i] = CreateMinion(kind, team);
            for (int i = 0; i < n; i++) Release(list[i]);
        }

        static void WarmProjectiles(ProjKind kind, int team, int n)
        {
            var list = new ProjectileView[n];
            for (int i = 0; i < n; i++) list[i] = CreateProjectile(kind, team);
            for (int i = 0; i < n; i++) Release(list[i]);
        }

        static Quaternion SunRotation(bool turned) => Quaternion.Euler(52f, turned ? 152f : -28f, 0f);

        /// <summary>Points the sun for a turned camera or back for the normal one (the menus always use the normal).</summary>
        public static void TurnSun(bool turned)
        {
            if (sun != null) sun.transform.rotation = SunRotation(turned);
        }

        static void SetupLighting()
        {
            var lg = new GameObject("BAM Sun");
            lg.transform.SetParent(root, false);
            // from behind the camera and a bit to the left, so faces (toward the bottom of the screen) are lit
            lg.transform.rotation = SunRotation(false);
            sun = lg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.cullingMask = ~(1 << ArenaLayer); // the units only: the arena keeps the old game's ambient-only look
            sun.intensity = 1f;     // as the old game's scene light; 1.3 washed the arena out (Rickard 2026-10-26: "det skiljer mycket")
            sun.shadowStrength = 0.55f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // the old game's ambient exactly (Speltest View3DMapStyle.cs, "a little darker, 2026-10-16"): warm grey, not blue
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.84f, 0.81f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.68f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.53f, 0.47f, 0.44f);
            RenderSettings.fog = false;
        }

        /// <summary>Low: no shadows, fewer particles. High: the full look. Called at boot and when the setting changes.</summary>
        public static void ApplyQuality(GraphicsQuality quality)
        {
            High = quality == GraphicsQuality.High;
            QualityVersion++;
            Fx.Amount = High ? 1f : 0.5f;
            if (sun != null) sun.shadows = High ? LightShadows.Soft : LightShadows.None;
            for (int i = casters.Count - 1; i >= 0; i--)
            {
                if (casters[i] == null) { casters.RemoveAt(i); continue; }
                casters[i].shadowCastingMode = High ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        internal static void RegisterCaster(Renderer r)
        {
            // every arena registers its renderers and only a quality change dropped the destroyed ones: prune them here, so
            // the list does not grow by one arena's worth every match
            if (casters.Count >= 64) casters.RemoveAll(c => c == null);
            casters.Add(r);
        }

        /// <summary>Static map geometry (ground, walls, grass, rocks, towers' bases, void edges), batched for few draw calls.</summary>
        public static GameObject BuildArena(ArenaInfo arena)
        {
            Init();
            if (arena == null || arena.Tiles == null) { Debug.LogError("[BAM.Art] BuildArena: no map"); return new GameObject("Arena"); }
            // the sun turns with the camera (online, top team): still from behind it and a bit to its left, so faces stay lit and
            // shadows fall the same way on screen for both players (Rickard 2026-10-31 "ser allt identiskt ut")
            TurnSun(arena.Turned);
            return Arena.Build(arena);
        }

        public static UnitView CreateHero(HeroId hero, int team) => Body(Models.Hero(hero, team), (int)hero, team);
        public static UnitView CreateMinion(MinionKind kind, int team) => Body(Models.Minion(kind, team), 10 + (int)kind, team);
        public static UnitView CreateTower(int team) => Body(Models.Tower(team), 20, team);
        /// <summary>Fenn's spectral wolf.</summary>
        public static UnitView CreateWolf(int team) => Body(Models.Wolf(team), 30, team);
        /// <summary>The guided match's practice dummy (a target board on a post). PlayAttack does nothing; SetDead bursts it into splinters.</summary>
        public static UnitView CreateTarget() => Body(Models.Target(), 40, Teams.Enemy);
        /// <summary>A hero for the menus (Home platform, hero cards): team 0 look, idle, no ground ring, bar or status
        /// markers, own pool. It is lit by the game's sun wherever it stands, and any camera can render it.</summary>
        public static UnitView CreateHeroPreview(HeroId hero) => Body(Models.Hero(hero, Teams.Player), 50 + (int)hero, Teams.Player, true);

        static BodyView Body(RigDef def, int kindKey, int team, bool preview = false)
        {
            Init();
            team = team == 1 ? 1 : 0;
            int key = kindKey * 2 + team;
            if (!bodyPools.TryGetValue(key, out Stack<BodyView> pool)) { pool = new Stack<BodyView>(); bodyPools[key] = pool; }
            BodyView v;
            if (pool.Count > 0)
            {
                v = pool.Pop();
                v.ResetState();
            }
            else
            {
                var go = new GameObject(def.Mesh.name);
                go.transform.SetParent(root, false);
                v = go.AddComponent<BodyView>();
                v.Pool = pool;
                v.Preview = preview;
                v.Build(def, team);
            }
            v.InPool = false;
            v.IsHero = !preview && kindKey < 10;
            v.HeroIndex = kindKey < 10 ? kindKey : kindKey >= 50 && kindKey < 60 ? kindKey - 50 : -1;
            v.gameObject.SetActive(true);
            if (v.IsHero && !BodyView.Heroes.Contains(v)) BodyView.Heroes.Add(v); // a new view was enabled before IsHero was set
            return v;
        }

        public static ProjectileView CreateProjectile(ProjKind kind, int team)
        {
            Init();
            team = team == 1 ? 1 : 0;
            int key = (int)kind * 2 + team;
            if (!projPools.TryGetValue(key, out Stack<ProjView> pool)) { pool = new Stack<ProjView>(); projPools[key] = pool; }
            ProjView v;
            if (pool.Count > 0) v = pool.Pop();
            else
            {
                var go = new GameObject("Proj " + kind);
                go.transform.SetParent(root, false);
                v = go.AddComponent<ProjView>();
                v.Pool = pool;
                v.Build(kind, team);
            }
            v.InPool = false;
            v.gameObject.SetActive(true);
            return v;
        }

        public static ZoneView CreateZone(ZoneKind kind, int team)
        {
            Init();
            team = team == 1 ? 1 : 0;
            int key = (int)kind * 2 + team;
            if (!zonePools.TryGetValue(key, out Stack<ZoneLook> pool)) { pool = new Stack<ZoneLook>(); zonePools[key] = pool; }
            ZoneLook v;
            if (pool.Count > 0) v = pool.Pop();
            else
            {
                var go = new GameObject("Zone " + kind);
                go.transform.SetParent(root, false);
                v = go.AddComponent<ZoneLook>();
                v.Pool = pool;
                v.Build(kind, team);
            }
            v.InPool = false;
            v.gameObject.SetActive(true);
            return v;
        }

        public static AimView CreateAim()
        {
            Init();
            var go = new GameObject("Aim");
            go.transform.SetParent(root, false);
            var v = go.AddComponent<AimLook>();
            v.Build();
            return v;
        }

        /// <summary>One-shot effect at a world position. size scales it (1 = default for that kind).</summary>
        /// <summary>A tower fired: the carved runes round its plaza glow briefly in its team's light (Rickard 2026-11-02).</summary>
        public static void TowerFired(Vector3 tower, int team) => TowerRunes.Pulse(tower, Pal.Get(Pal.TeamLight(team)));

        /// <summary>Match start: the road's runes light up from the player's tower to the enemy's, showing the way (Rickard 2026-11-02).</summary>
        public static void RoadRunesShow(Vector3 ownTower, Vector3 enemyTower, int team)
            => RoadRunes.Show(ownTower, enemyTower, Pal.Get(Pal.TeamLight(team)), Pal.Get(Pal.TeamLight(1 - team)));

        /// <summary>The first minions came: the road's runes go out.</summary>
        public static void RoadRunesOff() => RoadRunes.Hide();

        public static void PlayFx(FxKind kind, Vector3 position, float size = 1f, int team = 0, int hero = -1)
        {
            if (!ready) return;
            Fx.Play(kind, position, size <= 0f ? 1f : size, team == 1 ? 1 : 0, hero);
        }

        /// <summary>Oaken's quake (event Quake): a crack running from a to b on the ground, halfWidth to each side.</summary>
        public static void PlayOakenQuake(Vector3 a, Vector3 b, float halfWidth)
        {
            if (!ready) return;
            Fx.OakenQuake(a, b, halfWidth);
        }

        /// <summary>A battle spell went off (event SpellCast): from = where the caster stood (a blink's start), at = where it is now.</summary>
        public static void PlaySpell(SpellId spell, Vector3 from, Vector3 at)
        {
            if (!ready) return;
            SpellFx.Cast(spell, from, at);
        }

        /// <summary>A recall channel was cut (event SpellCancel).</summary>
        public static void PlaySpellCancel(Vector3 at)
        {
            if (!ready) return;
            SpellFx.Cancel(at);
        }

        /// <summary>A recall arrived (event Recall): from = where the hero left, to = its spawn.</summary>
        public static void PlayRecall(Vector3 from, Vector3 to)
        {
            if (!ready) return;
            SpellFx.Recall(from, to);
        }

        /// <summary>Hands a view back to its pool (minions, projectiles and zones come and go all match long).</summary>
        /// <summary>Blast's Chain Reaction warning at a frozen point: a violet ring with its edge on the radius, a gold ring growing
        /// from the middle that reaches the edge exactly when the fuse runs out, and a spark at the point. Then play FxKind.ChainBoom.</summary>
        /// <summary>Tyra's Arrow Volley being drawn (Speltest tyravolley): a warm gold fan on the ground from her toward the aim, the
        /// rules' own fan (five arrows, +-TyraS.VolleyHalfAngle, as long as her range; precision TYRA-09), held through the draw.</summary>
        public static void PlayVolleyDraw(Vector3 from, Vector3 dir, float range, float windup)
        {
            if (!ready) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            Color gold = new Color(1f, 0.92f, 0.55f, 0.85f);
            Fx.VolleyFan(from, dir, range, TyraS.VolleyHalfAngle, TyraS.VolleyArrows, gold, windup + 0.05f);
            Fx.Pic("tx_fx_tyra_charge", from + Vector3.up * 0.9f, gold, 1.6f, windup);
        }

        public static void PlayChainWarn(Vector3 at, float radius, float fuse)
        {
            if (!ready) return;
            Fx.ChainWarn(at, Mathf.Max(0.1f, radius), Mathf.Max(0.05f, fuse));
        }

        /// <summary>A hero's tapped special in its warning (Sim EventType.Telegraph, Rules.SpecialTelegraph): a bright flash on the
        /// hero and Rickard's telegraph ring closing in on its feet over the warning, strong red on an enemy, white on our side, so
        /// who is about to fire reads at a glance. hero = HeroId for its colour in the sparks (-1 none).</summary>
        public static void PlayCastWarn(Vector3 at, int team, int hero, float time)
        {
            if (!ready) return;
            Fx.CastWarn(at, team == 1 ? 1 : 0, hero, Mathf.Max(0.05f, time));
        }

        /// <summary>Bram's Anvil Drop on its way down: a forge-gold edge on the radius and a ring closing in to the middle as it lands.</summary>
        public static void PlayAnvilWarn(Vector3 at, float radius, float fall)
        {
            if (!ready) return;
            Fx.AnvilWarn(at, Mathf.Max(0.1f, radius), Mathf.Max(0.05f, fall));
        }

        /// <summary>A shown hero's height in world units, feet to crown, as the art fitted it (for the menu stages' framing).</summary>
        public static float HeightOf(UnitView view) => view is BodyView b ? b.ShownHeight : 1.8f;

        /// <summary>The highest point of a shown hero's held weapons now (world y); negative infinity without any.</summary>
        public static float GearTopOf(UnitView view) => view is BodyView b ? b.GearTop() : float.NegativeInfinity;

        public static void Release(Component view)
        {
            if (view == null) return;
            switch (view)
            {
                case BodyView b:
                    if (b.InPool) return;
                    b.InPool = true;
                    Park(b);
                    if (b.Pool != null) b.Pool.Push(b);
                    break;
                case ProjView p:
                    if (p.InPool) return;
                    p.InPool = true;
                    p.OnRelease();
                    Park(p);
                    if (p.Pool != null) p.Pool.Push(p);
                    break;
                case ZoneLook z:
                    if (z.InPool) return;
                    z.InPool = true;
                    Park(z);
                    if (z.Pool != null) z.Pool.Push(z);
                    break;
                default:
                    Object.Destroy(view.gameObject);
                    break;
            }
        }

        static void Park(Component c)
        {
            c.gameObject.SetActive(false);
            if (c.transform.parent != root) c.transform.SetParent(root, false);
        }

        /// <summary>Clears running particles and rings and lifts a pause and a hit-stop (GameFlow.Teardown, after MatchViews.Dispose).</summary>
        public static void ClearEffects()
        {
            SetPaused(false);
            slow = 1f;
            if (ready) Fx.ClearAll();
            ShaderWarm.MatchEnded();
        }
    }
}
