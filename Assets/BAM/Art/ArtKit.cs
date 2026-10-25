using BAM.Core;
using BAM.Sim;
using UnityEngine;

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
        /// <summary>Took damage. big = a crit-like hit (passive bonus, special).</summary>
        public abstract void PlayHit(bool big);
        /// <summary>Died (play the death and hide) or came back (respawn).</summary>
        public abstract void SetDead(bool dead);
        /// <summary>World-space bar above the unit. hp01 and shield01 are fractions of max health.</summary>
        public abstract void SetHealth(float hp01, float shield01);
        /// <summary>Hero level shown on the bar (1..15). Ignored by minions and towers.</summary>
        public abstract void SetLevel(int level);
        /// <summary>Status effects to show (stun stars, slow, burn, shield bubble, ...).</summary>
        public abstract void SetStatus(StatusFlags flags);
        /// <summary>1 = fully visible, about 0.5 = your own unit hidden in grass, 0 = not visible to the player (hide everything).</summary>
        public abstract void SetVisibility(float alpha);
        /// <summary>Ring under the unit: the player's hero, allies, enemies.</summary>
        public abstract void SetMarker(UnitMarker marker);
    }

    public abstract class ProjectileView : MonoBehaviour
    {
        /// <summary>The game layer moves it every frame; Launch tells the look where it goes and how long it flies.</summary>
        public abstract void Launch(Vector3 from, Vector3 to, float flightTime);
        /// <summary>Knut's anchor: draw a chain from the projectile back to this point every frame (null = no chain).</summary>
        public virtual void SetTether(Transform anchor) { }
    }

    public abstract class ZoneView : MonoBehaviour
    {
        /// <summary>A circle (length = 0) or a strip from center along dir. duration = how long it lasts (for fades/fuses).</summary>
        public abstract void Show(Vector3 center, Vector3 dir, float length, float radius, float duration);
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
    }

    public static class ArtKit
    {
        /// <summary>Once at boot. Builds shared materials and meshes and warms the pools.</summary>
        public static void Init() { }

        /// <summary>Low: no shadows, fewer particles. High: the full look. Called at boot and when the setting changes.</summary>
        public static void ApplyQuality(GraphicsQuality quality) { }

        /// <summary>Static map geometry (ground, walls, grass, rocks, towers' bases, void edges), batched for few draw calls.</summary>
        public static GameObject BuildArena(ArenaInfo arena) => null;

        public static UnitView CreateHero(HeroId hero, int team) => null;
        public static UnitView CreateMinion(MinionKind kind, int team) => null;
        public static UnitView CreateTower(int team) => null;
        /// <summary>Fenn's spectral wolf.</summary>
        public static UnitView CreateWolf(int team) => null;
        public static ProjectileView CreateProjectile(ProjKind kind, int team) => null;
        public static ZoneView CreateZone(ZoneKind kind, int team) => null;
        public static AimView CreateAim() => null;

        /// <summary>One-shot effect at a world position. size scales it (1 = default for that kind).</summary>
        public static void PlayFx(FxKind kind, Vector3 position, float size = 1f, int team = 0) { }

        /// <summary>Hands a view back to its pool (minions, projectiles and zones come and go all match long).</summary>
        public static void Release(Component view) { }
    }
}
