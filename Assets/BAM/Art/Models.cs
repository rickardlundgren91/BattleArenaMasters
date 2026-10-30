using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    enum RigKind : byte { Humanoid, Wolf, Cart, Tower, Target }

    enum AttackStyle : byte { Swing, Throw, Bow, Cast, Flick, Cannon, Bite, Pulse, None }

    /// <summary>A built model: one rigidly skinned mesh plus its bones. Shared by every view of the same kind and team.</summary>
    sealed class RigDef
    {
        public Mesh Mesh;
        public Vector3[] Pivots;
        public int[] Parents;
        public RigKind Kind;
        public AttackStyle Attack;
        /// <summary>The prop leaves the hand when the attack releases (Bram's hammer, Blast's bomb) and comes back a moment later.</summary>
        public bool ThrowsProp;
        public float Height, Radius, BarWidth;
        /// <summary>Health bar height in tiles: heroes and towers thicker than minions (MLBB).</summary>
        public float BarThick = 0.1f;
        public bool Ghost;
        /// <summary>Rickard's imported model and clips; null = the model built in code.</summary>
        public HeroArt Art;
        /// <summary>Weapons and shield held by an imported hero (<see cref="Weapons"/>); null = none.</summary>
        public GearDef Gear;
        /// <summary>Rickard's Meshy model (Props), hung on bone PropBone; null = only the code mesh.</summary>
        public Mesh PropMesh;
        public Texture2D PropTex;
        /// <summary>Fenn's wolf: Rickard's rigged Meshy wolf with its walk (Resources/BAMArt/Wolf, as old Speltest's KovaForms/Wolf).</summary>
        public GameObject RigModel;
        public int PropBone;
        public Vector3 PropOffset, PropEuler;
        public float PropScale = 1f;
    }

    /// <summary>Humanoid bone indices. Every two-legged model uses the same rig so one animator drives them all.</summary>
    static class Hb
    {
        public const int Hips = 0, Chest = 1, Head = 2, ArmL = 3, ArmR = 4, LegL = 5, LegR = 6, Prop = 7, Extra = 8, PropL = 9, Count = 10;
        public static readonly int[] Parents = { -1, Hips, Chest, Chest, Chest, Hips, Hips, ArmR, Chest, ArmL };
    }

    /// <summary>Proportions of a two-legged model; positions every bone pivot from a few numbers.</summary>
    struct Humanoid
    {
        public Vector3 Hips, Chest, Neck, ShL, ShR, HandL, HandR, LegL, LegR, Back;
        public float Leg, Torso;

        public Humanoid(float leg, float hipW, float torso, float shoulderW, float armLen)
        {
            Leg = leg; Torso = torso;
            Hips = new Vector3(0f, leg, 0f);
            Chest = new Vector3(0f, leg + 0.03f, 0f);
            Neck = new Vector3(0f, leg + torso, 0f);
            ShL = new Vector3(-shoulderW, leg + torso - 0.06f, 0f);
            ShR = new Vector3(shoulderW, leg + torso - 0.06f, 0f);
            HandL = ShL + new Vector3(0f, -armLen, 0f);
            HandR = ShR + new Vector3(0f, -armLen, 0f);
            LegL = new Vector3(-hipW, leg, 0f);
            LegR = new Vector3(hipW, leg, 0f);
            Back = new Vector3(0f, leg + torso * 0.6f, -0.1f);
        }

        public Vector3[] Pivots() => new[] { Hips, Chest, Neck, ShL, ShR, LegL, LegR, HandR, Back, HandL };
    }

    static class Models
    {
        static readonly MeshBuilder b = new MeshBuilder();
        static readonly Dictionary<int, RigDef> cache = new Dictionary<int, RigDef>();

        static int Key(int kind, int sub, int team) => (kind * 64 + sub) * 4 + team;

        public static RigDef Hero(HeroId hero, int team)
        {
            int k = Key(0, (int)hero, team);
            if (cache.TryGetValue(k, out RigDef d)) return d;
            b.Clear();
            switch (hero)
            {
                case HeroId.Bram: d = Bram(team); break;
                case HeroId.Fenn: d = Fenn(team); break;
                case HeroId.Tyra: d = Tyra(team); break;
                case HeroId.Moa: d = Moa(team); break;
                case HeroId.Knut: d = Knut(team); break;
                case HeroId.Tikta: d = Tikta(team); break;
                // Bonk has no code figure (he only exists as Rickard's model): Knut's big melee rig gives him a ring, status rings
                // and a swing that fit his size, never Blast's small bomb thrower
                case HeroId.Bonk: d = Knut(team); break;
                case HeroId.Hilde: case HeroId.Oaken: d = Knut(team); break; // the same for Hilde and Oaken (models only)
                default: d = Blast(team); break;
            }
            d.Mesh = b.ToSkinnedMesh("Hero " + hero + " t" + team, d.Pivots);
            d.BarWidth = 1.1f; // MLBB-wide (was 0.95 before the 2026-10-30 MLBB bars)
            d.BarThick = 0.13f;
            d.Art = Resources.Load<HeroArt>("BAMArt/Heroes/" + hero);
            if (d.Art == null && hero >= HeroId.Bonk) Debug.LogError("[BAM.Art] " + hero + " has no HeroArt (run BAM > Art > Build Hero Art): showing a stand-in figure");
            if (d.Art != null)
            {
                d.Height = d.Art.Height;
                d.ThrowsProp = false;
                d.Gear = Weapons.For(hero);
            }
            cache[k] = d;
            return d;
        }

        public static RigDef Minion(MinionKind kind, int team)
        {
            int k = Key(1, (int)kind, team);
            if (cache.TryGetValue(k, out RigDef d)) return d;
            b.Clear();
            d = kind == MinionKind.Siege ? SiegeCart(team) : kind == MinionKind.Ranged ? RangedMinion(team) : MeleeMinion(team);
            d.Mesh = b.ToSkinnedMesh("Minion " + kind + " t" + team, d.Pivots);
            d.BarWidth = kind == MinionKind.Siege ? 0.8f : 0.6f;
            d.Art = Resources.Load<HeroArt>("BAMArt/Minions/" + kind);
            if (d.Art != null)
            {
                d.Height = d.Art.Height;
                d.ThrowsProp = false;
            }
            cache[k] = d;
            return d;
        }

        public static RigDef Tower(int team)
        {
            int k = Key(2, 0, team);
            if (cache.TryGetValue(k, out RigDef d)) return d;
            b.Clear();
            Mesh prop = Props.Mesh("blackbruk_tower");
            d = prop != null ? BuildTowerProp(team, prop) : BuildTower(team);
            d.Mesh = b.ToSkinnedMesh("Tower t" + team, d.Pivots);
            d.BarWidth = 1.6f;
            d.BarThick = 0.13f;
            cache[k] = d;
            return d;
        }

        public static RigDef Target()
        {
            int k = Key(4, 0, 1);
            if (cache.TryGetValue(k, out RigDef d)) return d;
            b.Clear();
            d = BuildTarget();
            d.Mesh = b.ToSkinnedMesh("Target", d.Pivots);
            d.BarWidth = 0.8f;
            cache[k] = d;
            return d;
        }

        public static RigDef Wolf(int team)
        {
            int k = Key(3, 0, team);
            if (cache.TryGetValue(k, out RigDef d)) return d;
            b.Clear();
            // the rigged wolf that runs with its own walk, as old Speltest (View3DWolf.cs); the still mesh only without it
            GameObject rig = Resources.Load<GameObject>("BAMArt/Wolf/Wolf");
            Mesh wolfProp = rig != null ? null : Props.Mesh("fenn_wolf");
            d = BuildWolf(team, wolfProp != null || rig != null);
            if (rig != null) { d.RigModel = rig; d.Height = 1.6f; d.Radius = 0.55f; }
            if (wolfProp != null)
            {
                // the Meshy wolf faces -z; scaled 1.05 as old Speltest (View3D.cs): about 2 tiles nose to tail, 1.6 high
                d.PropMesh = wolfProp; d.PropTex = Props.Texture("fenn_wolf"); d.PropBone = WolfBody;
                d.PropEuler = new Vector3(0f, 180f, 0f); d.PropScale = 1.05f;
                d.Height = 1.6f; d.Radius = 0.55f;
            }
            d.Mesh = b.ToSkinnedMesh("Wolf t" + team, d.Pivots);
            d.BarWidth = 0.6f;
            cache[k] = d;
            return d;
        }

        // ---------------------------------------------------------------- shared parts

        static RigDef Human(Humanoid h, AttackStyle attack, float height, float radius, bool throwsProp = false) => new RigDef
        {
            Pivots = h.Pivots(), Parents = Hb.Parents, Kind = RigKind.Humanoid, Attack = attack, Height = height, Radius = radius, ThrowsProp = throwsProp,
        };

        static void Eyes(Vector3 faceCenter, float spacing, float r, byte iris = Pal.Pupil)
        {
            b.Color(Pal.Eye).Sphere(faceCenter + new Vector3(-spacing, 0f, 0f), r, 8, 5);
            b.Sphere(faceCenter + new Vector3(spacing, 0f, 0f), r, 8, 5);
            b.Color(iris).Sphere(faceCenter + new Vector3(-spacing, 0f, r * 0.75f), r * 0.5f, 6, 4);
            b.Sphere(faceCenter + new Vector3(spacing, 0f, r * 0.75f), r * 0.5f, 6, 4);
        }

        static void Legs(Humanoid h, float r, byte pants, byte boots, float footLen = 0.16f)
        {
            for (int s = 0; s < 2; s++)
            {
                Vector3 top = s == 0 ? h.LegL : h.LegR;
                b.Bone = s == 0 ? Hb.LegL : Hb.LegR;
                Vector3 knee = top + new Vector3(0f, -h.Leg + 0.07f, 0f);
                b.Color(pants).Limb(top, knee, r, r * 0.85f, 7);
                b.Color(boots).Box(new Vector3(top.x, 0.045f, 0.03f), new Vector3(r * 2.2f, 0.09f, footLen), -1);
            }
        }

        static void Arms(Humanoid h, float r, byte sleeve, byte hand, float handR = -1f)
        {
            if (handR < 0f) handR = r * 1.25f;
            b.Bone = Hb.ArmL;
            b.Color(sleeve).Limb(h.ShL, h.HandL + new Vector3(0f, 0.04f, 0f), r, r * 0.85f, 7);
            b.Color(hand).Sphere(h.HandL, handR, 8, 5);
            b.Bone = Hb.ArmR;
            b.Color(sleeve).Limb(h.ShR, h.HandR + new Vector3(0f, 0.04f, 0f), r, r * 0.85f, 7);
            b.Color(hand).Sphere(h.HandR, handR, 8, 5);
        }

        static void Scarf(Vector3 neck, float r, int team)
        {
            b.Color(Pal.Team(team)).Torus(neck + new Vector3(0f, -0.01f, 0f), r, 0.035f, 12, 5);
            b.Color(Pal.TeamDark(team)).Beam(neck + new Vector3(r * 0.5f, -0.02f, -r * 0.8f), neck + new Vector3(r * 0.9f, -0.17f, -r * 1.3f), 0.06f, 0.025f);
        }

        // ---------------------------------------------------------------- heroes

        // Short and wide smith with a red beard and one huge hammer he throws.
        static RigDef Bram(int team)
        {
            var h = new Humanoid(0.24f, 0.11f, 0.36f, 0.27f, 0.27f);
            Legs(h, 0.085f, Pal.LeatherDark, Pal.WoodDark, 0.18f);
            b.Bone = Hb.Hips;
            b.Color(Pal.Leather).Box(h.Hips + new Vector3(0f, 0.04f, 0f), new Vector3(0.36f, 0.12f, 0.26f));
            b.Bone = Hb.Chest;
            b.Color(Pal.Cloth).Box(h.Chest + new Vector3(0f, 0.18f, 0f), new Vector3(0.46f, 0.34f, 0.32f), Pal.Cloth);
            b.Color(Pal.Leather).Box(h.Chest + new Vector3(0f, 0.15f, 0.165f), new Vector3(0.34f, 0.3f, 0.02f)); // apron
            b.Color(Pal.Team(team)).Box(h.Chest + new Vector3(0f, 0.04f, 0f), new Vector3(0.48f, 0.07f, 0.34f)); // belt
            b.Color(Pal.Gold).Box(h.Chest + new Vector3(0f, 0.04f, 0.175f), new Vector3(0.08f, 0.06f, 0.02f));
            b.Color(Pal.TeamDark(team)).Sphere(h.ShL + new Vector3(-0.02f, 0.02f, 0f), new Vector3(0.11f, 0.08f, 0.12f), 8, 5); // pauldrons
            b.Sphere(h.ShR + new Vector3(0.02f, 0.02f, 0f), new Vector3(0.11f, 0.08f, 0.12f), 8, 5);
            Arms(h, 0.075f, Pal.Skin, Pal.LeatherDark, 0.085f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.14f, 0.01f);
            b.Color(Pal.Skin).Sphere(hc, 0.15f, 10, 7);
            b.Color(Pal.Beard).Sphere(hc + new Vector3(0f, -0.08f, 0.08f), new Vector3(0.14f, 0.12f, 0.09f), 9, 6);
            b.Sphere(hc + new Vector3(0f, -0.01f, 0.13f), new Vector3(0.08f, 0.03f, 0.03f), 8, 4); // moustache
            b.Color(Pal.SkinDark).Sphere(hc + new Vector3(0f, 0.02f, 0.15f), 0.035f, 6, 4); // nose
            Eyes(hc + new Vector3(0f, 0.05f, 0.12f), 0.055f, 0.03f);
            b.Color(Pal.Team(team)).Torus(hc + new Vector3(0f, 0.07f, 0f), 0.145f, 0.025f, 14, 5); // headband
            b.Color(Pal.Beard).Sphere(hc + new Vector3(0f, 0.1f, -0.02f), new Vector3(0.13f, 0.06f, 0.12f), 8, 5);
            // the hammer: handle forward-up from the fist, a chunky block on top
            b.Bone = Hb.Prop;
            Vector3 hand = h.HandR, tip = hand + new Vector3(0f, 0.3f, 0.2f);
            b.Color(Pal.Wood).Beam(hand + new Vector3(0f, -0.06f, -0.04f), tip, 0.045f, 0.045f);
            b.M = Matrix4x4.TRS(tip, Quaternion.LookRotation(new Vector3(0f, 0.3f, 0.2f).normalized), Vector3.one);
            b.Color(Pal.Metal).Box(Vector3.zero, new Vector3(0.3f, 0.17f, 0.17f), Pal.Steel);
            b.Color(Pal.MetalDark).Box(new Vector3(0.155f, 0f, 0f), new Vector3(0.03f, 0.19f, 0.19f));
            b.Box(new Vector3(-0.155f, 0f, 0f), new Vector3(0.03f, 0.19f, 0.19f));
            b.Reset();
            return Human(h, AttackStyle.Throw, 1.0f, 0.42f, true);
        }

        // Lean hunter in a wolf-pelt hood with a long sword.
        static RigDef Fenn(int team)
        {
            var h = new Humanoid(0.36f, 0.075f, 0.32f, 0.19f, 0.3f);
            Legs(h, 0.06f, Pal.Leather, Pal.LeatherDark);
            b.Bone = Hb.Hips;
            b.Color(Pal.LeatherDark).Box(h.Hips + new Vector3(0f, 0.03f, 0f), new Vector3(0.24f, 0.1f, 0.17f));
            b.Bone = Hb.Chest;
            b.Color(Pal.Forest).Box(h.Chest + new Vector3(0f, 0.15f, 0f), new Vector3(0.28f, 0.3f, 0.19f), Pal.ForestDark);
            b.Color(Pal.Leather).Beam(h.Chest + new Vector3(-0.13f, 0.27f, 0.1f), h.Chest + new Vector3(0.13f, 0.03f, 0.1f), 0.05f, 0.02f); // strap
            b.Color(Pal.LeatherDark).Box(h.Chest + new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.05f, 0.21f));
            Scarf(h.Neck, 0.1f, team);
            b.Bone = Hb.Extra; // cape
            b.Color(Pal.ForestDark).Face(h.Neck + new Vector3(0.13f, -0.02f, -0.11f), h.Neck + new Vector3(0.16f, -0.36f, -0.2f),
                h.Neck + new Vector3(-0.16f, -0.36f, -0.2f), h.Neck + new Vector3(-0.13f, -0.02f, -0.11f));
            b.Face(h.Neck + new Vector3(-0.13f, -0.02f, -0.11f), h.Neck + new Vector3(-0.16f, -0.36f, -0.2f),
                h.Neck + new Vector3(0.16f, -0.36f, -0.2f), h.Neck + new Vector3(0.13f, -0.02f, -0.11f));
            Arms(h, 0.05f, Pal.Forest, Pal.Skin);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.13f, 0f);
            b.Color(Pal.Skin).Sphere(hc, 0.125f, 10, 7);
            Eyes(hc + new Vector3(0f, 0.01f, 0.1f), 0.045f, 0.028f);
            b.Color(Pal.Hair).Sphere(hc + new Vector3(0f, 0.04f, -0.02f), new Vector3(0.13f, 0.1f, 0.12f), 9, 5);
            // pelt hood: a grey cap with two pointed wolf ears
            b.Color(Pal.StoneDark).Sphere(hc + new Vector3(0f, 0.06f, -0.03f), new Vector3(0.145f, 0.11f, 0.14f), 10, 6);
            b.Color(Pal.Stone).Sphere(hc + new Vector3(0f, 0.12f, 0.08f), new Vector3(0.07f, 0.04f, 0.07f), 8, 4);
            b.Color(Pal.StoneDark).At(hc + new Vector3(-0.08f, 0.13f, -0.02f), new Vector3(0f, 0f, 20f)).Cylinder(Vector3.zero, 0.045f, 0f, 0.12f, 6);
            b.At(hc + new Vector3(0.08f, 0.13f, -0.02f), new Vector3(0f, 0f, -20f)).Cylinder(Vector3.zero, 0.045f, 0f, 0.12f, 6);
            b.Reset();
            // sword: grip in the fist, blade pointing forward and up
            b.Bone = Hb.Prop;
            Vector3 hand = h.HandR, dir = new Vector3(0f, 0.45f, 0.9f).normalized;
            b.Color(Pal.LeatherDark).Beam(hand - dir * 0.06f, hand + dir * 0.05f, 0.035f, 0.035f);
            b.M = Matrix4x4.TRS(hand + dir * 0.06f, Quaternion.LookRotation(dir), Vector3.one);
            b.Color(Pal.Gold).Box(Vector3.zero, new Vector3(0.16f, 0.035f, 0.035f));
            b.Color(Pal.Steel).Box(new Vector3(0f, 0f, 0.27f), new Vector3(0.06f, 0.018f, 0.5f));
            b.Color(Pal.Metal).Box(new Vector3(0f, 0f, 0.545f), new Vector3(0.035f, 0.018f, 0.05f));
            b.Reset();
            return Human(h, AttackStyle.Swing, 1.0f, 0.42f);
        }

        // Slim upright archer with small antlers and a big bow in the left hand.
        static RigDef Tyra(int team)
        {
            var h = new Humanoid(0.4f, 0.065f, 0.32f, 0.16f, 0.3f);
            Legs(h, 0.05f, Pal.Tan, Pal.LeatherDark, 0.14f);
            b.Bone = Hb.Hips;
            b.Color(Pal.Forest).Cylinder(h.Hips + new Vector3(0f, -0.12f, 0f), 0.16f, 0.12f, 0.16f, 10); // short skirt
            b.Bone = Hb.Chest;
            b.Color(Pal.Forest).Box(h.Chest + new Vector3(0f, 0.15f, 0f), new Vector3(0.23f, 0.3f, 0.16f), Pal.ForestDark);
            b.Color(Pal.Team(team)).Beam(h.Chest + new Vector3(-0.12f, 0.29f, 0.09f), h.Chest + new Vector3(0.12f, 0.02f, 0.09f), 0.06f, 0.02f); // sash
            b.Beam(h.Chest + new Vector3(-0.12f, 0.29f, -0.09f), h.Chest + new Vector3(0.12f, 0.02f, -0.09f), 0.06f, 0.02f);
            b.Bone = Hb.Extra; // quiver with team fletching
            b.Color(Pal.Leather).At(h.Back + new Vector3(0.05f, -0.1f, -0.02f), new Vector3(-15f, 0f, -20f)).Cylinder(Vector3.zero, 0.055f, 0.06f, 0.3f, 8);
            b.Color(Pal.TeamLight(team)).Box(new Vector3(-0.02f, 0.33f, 0f), new Vector3(0.04f, 0.07f, 0.015f));
            b.Box(new Vector3(0.025f, 0.34f, 0.01f), new Vector3(0.04f, 0.07f, 0.015f));
            b.Reset();
            Arms(h, 0.045f, Pal.Tan, Pal.Skin);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.12f, 0f);
            b.Color(Pal.Skin).Sphere(hc, 0.12f, 10, 7);
            Eyes(hc + new Vector3(0f, 0.01f, 0.095f), 0.045f, 0.028f, Pal.ForestDark);
            b.Color(Pal.Hair).Sphere(hc + new Vector3(0f, 0.035f, -0.025f), new Vector3(0.13f, 0.11f, 0.12f), 10, 6);
            b.Sphere(hc + new Vector3(0f, -0.06f, -0.12f), new Vector3(0.05f, 0.12f, 0.05f), 8, 5); // braid
            b.Color(Pal.Antler);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 root = hc + new Vector3(0.06f * s, 0.1f, -0.01f), top = root + new Vector3(0.07f * s, 0.15f, -0.02f);
                b.Beam(root, top, 0.025f, 0.025f);
                b.Beam(root + (top - root) * 0.5f, root + (top - root) * 0.5f + new Vector3(0.06f * s, 0.03f, 0.03f), 0.02f, 0.02f);
                b.Beam(top, top + new Vector3(-0.02f * s, 0.05f, 0.03f), 0.02f, 0.02f);
            }
            // bow: a thick curved limb held upright in the left fist, string toward the body
            b.Bone = Hb.PropL;
            Vector3 hand = h.HandL;
            const int seg = 6;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg * 2f - 1f;
                Vector3 p = hand + new Vector3(0f, t * 0.42f, 0.06f + (1f - t * t) * 0.1f);
                if (i > 0) b.Color(i == seg / 2 || i == seg / 2 + 1 ? Pal.LeatherDark : Pal.Wood).Beam(prev, p, 0.05f, 0.045f);
                prev = p;
            }
            b.Color(Pal.Cloth).Beam(hand + new Vector3(0f, -0.42f, 0.06f), hand + new Vector3(0f, 0.42f, 0.06f), 0.01f, 0.01f);
            b.Color(Pal.TeamLight(team)).Box(hand + new Vector3(0f, 0.43f, 0.06f), new Vector3(0.04f, 0.04f, 0.04f));
            b.Box(hand + new Vector3(0f, -0.43f, 0.06f), new Vector3(0.04f, 0.04f, 0.04f));
            return Human(h, AttackStyle.Bow, 1.08f, 0.3f);
        }

        // Round bee guardian with a honey staff.
        static RigDef Moa(int team)
        {
            var h = new Humanoid(0.16f, 0.09f, 0.42f, 0.24f, 0.18f);
            Legs(h, 0.055f, Pal.BeeBlack, Pal.BeeBlack, 0.12f);
            b.Bone = Hb.Chest;
            Vector3 bc = h.Chest + new Vector3(0f, 0.18f, 0f);
            b.Color(Pal.Honey).Sphere(bc, new Vector3(0.25f, 0.23f, 0.23f), 12, 8);
            b.Color(Pal.BeeBlack).Torus(bc + new Vector3(0f, -0.07f, 0f), 0.225f, 0.035f, 16, 5);
            b.Torus(bc + new Vector3(0f, 0.07f, 0f), 0.215f, 0.035f, 16, 5);
            b.Color(Pal.Cream).Sphere(bc + new Vector3(0f, -0.01f, 0.13f), new Vector3(0.14f, 0.15f, 0.12f), 10, 6); // fluffy belly
            b.Color(Pal.BeeBlack).At(bc + new Vector3(0f, -0.08f, -0.2f), new Vector3(-110f, 0f, 0f)).Cylinder(Vector3.zero, 0.06f, 0f, 0.12f, 8); // stinger
            b.Reset();
            Scarf(h.Neck + new Vector3(0f, -0.02f, 0f), 0.15f, team);
            b.Bone = Hb.Extra; // wings
            b.Color(Pal.Wing);
            for (int s = -1; s <= 1; s += 2)
            {
                b.At(h.Back + new Vector3(0.14f * s, 0.06f, -0.08f), new Vector3(10f, 25f * s, 30f * s));
                b.Sphere(Vector3.zero + new Vector3(0.14f * s, 0.06f, 0f), new Vector3(0.15f, 0.08f, 0.015f), 10, 4);
                b.Sphere(new Vector3(0.1f * s, -0.06f, 0f), new Vector3(0.1f, 0.06f, 0.015f), 10, 4);
            }
            b.Reset();
            Arms(h, 0.045f, Pal.BeeBlack, Pal.Honey, 0.055f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.13f, 0.02f);
            b.Color(Pal.Honey).Sphere(hc, new Vector3(0.17f, 0.15f, 0.15f), 12, 7);
            b.Color(Pal.Cream).Sphere(hc + new Vector3(0f, -0.04f, 0.08f), new Vector3(0.11f, 0.08f, 0.08f), 10, 5);
            Eyes(hc + new Vector3(0f, 0.02f, 0.12f), 0.065f, 0.038f);
            b.Color(Pal.Pink).Sphere(hc + new Vector3(-0.11f, -0.04f, 0.1f), 0.025f, 6, 4);
            b.Sphere(hc + new Vector3(0.11f, -0.04f, 0.1f), 0.025f, 6, 4);
            b.Color(Pal.BeeBlack);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 r0 = hc + new Vector3(0.06f * s, 0.12f, 0.02f), r1 = r0 + new Vector3(0.06f * s, 0.12f, 0.04f);
                b.Beam(r0, r1, 0.018f, 0.018f);
                b.Color(Pal.Team(team)).Sphere(r1, 0.035f, 6, 4);
                b.Color(Pal.BeeBlack);
                b.Sphere(hc + new Vector3(0.13f * s, 0.08f, -0.02f), new Vector3(0.04f, 0.05f, 0.03f), 6, 4); // round ears
            }
            // honey staff: wooden shaft with a glowing honey drop
            b.Bone = Hb.Prop;
            Vector3 hand = h.HandR;
            b.Color(Pal.Wood).Beam(hand + new Vector3(0f, -0.15f, 0f), hand + new Vector3(0f, 0.38f, 0.04f), 0.035f, 0.035f);
            b.Color(Pal.Honey).Sphere(hand + new Vector3(0f, 0.46f, 0.05f), 0.08f, 10, 6);
            b.Color(Pal.HoneyDark).Sphere(hand + new Vector3(0f, 0.4f, 0.05f), new Vector3(0.05f, 0.04f, 0.05f), 8, 4);
            b.Color(Pal.Team(team)).Box(hand + new Vector3(0f, 0.32f, 0.04f), new Vector3(0.06f, 0.04f, 0.06f));
            return Human(h, AttackStyle.Cast, 0.9f, 0.3f);
        }

        // Huge walrus sailor: oversized shoulders, tusks, cap, an iron anchor on a chain.
        static RigDef Knut(int team)
        {
            var h = new Humanoid(0.22f, 0.13f, 0.42f, 0.32f, 0.3f);
            Legs(h, 0.09f, Pal.Navy, Pal.Black, 0.18f);
            b.Bone = Hb.Hips;
            b.Color(Pal.Navy).Box(h.Hips + new Vector3(0f, 0.04f, 0f), new Vector3(0.42f, 0.12f, 0.3f));
            b.Bone = Hb.Chest;
            Vector3 bc = h.Chest + new Vector3(0f, 0.2f, 0f);
            b.Color(Pal.Cloth).Sphere(bc, new Vector3(0.3f, 0.24f, 0.25f), 12, 7);
            b.Color(Pal.Navy).Torus(bc + new Vector3(0f, 0.04f, 0f), 0.285f, 0.03f, 16, 4); // striped shirt
            b.Torus(bc + new Vector3(0f, -0.08f, 0f), 0.27f, 0.03f, 16, 4);
            b.Color(Pal.Team(team)).Sphere(h.ShL + new Vector3(-0.04f, 0.02f, 0f), new Vector3(0.15f, 0.13f, 0.15f), 10, 6); // huge shoulders
            b.Sphere(h.ShR + new Vector3(0.04f, 0.02f, 0f), new Vector3(0.15f, 0.13f, 0.15f), 10, 6);
            Arms(h, 0.08f, Pal.Walrus, Pal.WalrusDark, 0.095f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.11f, 0.03f);
            b.Color(Pal.Walrus).Sphere(hc, new Vector3(0.17f, 0.15f, 0.16f), 12, 7);
            b.Color(Pal.Cream).Sphere(hc + new Vector3(-0.055f, -0.05f, 0.13f), 0.07f, 8, 5); // muzzle
            b.Sphere(hc + new Vector3(0.055f, -0.05f, 0.13f), 0.07f, 8, 5);
            b.Color(Pal.Black).Sphere(hc + new Vector3(0f, -0.01f, 0.17f), new Vector3(0.04f, 0.025f, 0.025f), 6, 4);
            b.Color(Pal.Tusk).At(hc + new Vector3(-0.05f, -0.09f, 0.16f), new Vector3(180f, 0f, -5f)).Cylinder(Vector3.zero, 0.028f, 0f, 0.18f, 7);
            b.At(hc + new Vector3(0.05f, -0.09f, 0.16f), new Vector3(180f, 0f, 5f)).Cylinder(Vector3.zero, 0.028f, 0f, 0.18f, 7);
            b.Reset();
            Eyes(hc + new Vector3(0f, 0.05f, 0.13f), 0.06f, 0.025f);
            b.Color(Pal.Navy).Cylinder(hc + new Vector3(0f, 0.09f, -0.01f), 0.13f, 0.12f, 0.08f, 12, true, Pal.Navy); // sailor cap
            b.Color(Pal.Team(team)).Cylinder(hc + new Vector3(0f, 0.085f, -0.01f), 0.135f, 0.135f, 0.03f, 12, false);
            b.Color(Pal.Black).Box(hc + new Vector3(0f, 0.1f, 0.12f), new Vector3(0.16f, 0.02f, 0.06f));
            // anchor: ring at the fist, shaft hanging down beside the leg, flukes near the ground
            b.Bone = Hb.Prop;
            Vector3 hand = h.HandR + new Vector3(0.04f, 0f, 0.06f);
            b.Color(Pal.MetalDark).At(hand + new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0f, 90f)).Torus(Vector3.zero, 0.05f, 0.016f, 10, 4);
            b.Reset();
            b.Color(Pal.MetalDark).Box(hand + new Vector3(0f, -0.14f, 0f), new Vector3(0.06f, 0.4f, 0.06f));
            b.Box(hand + new Vector3(0f, 0.0f, 0f), new Vector3(0.05f, 0.05f, 0.22f)); // stock
            b.Color(Pal.Metal);
            Vector3 crown = hand + new Vector3(0f, -0.34f, 0f);
            b.Beam(crown, crown + new Vector3(0f, 0.1f, 0.15f), 0.055f, 0.055f);
            b.Beam(crown, crown + new Vector3(0f, 0.1f, -0.15f), 0.055f, 0.055f);
            b.At(crown + new Vector3(0f, 0.12f, 0.17f), new Vector3(0f, 90f, 0f));
            b.Extrude(new[] { new Vector2(-0.05f, -0.05f), new Vector2(0.06f, 0.0f), new Vector2(-0.05f, 0.08f) }, 0.05f, Pal.MetalDark);
            b.At(crown + new Vector3(0f, 0.12f, -0.17f), new Vector3(0f, -90f, 0f));
            b.Extrude(new[] { new Vector2(-0.05f, -0.05f), new Vector2(0.06f, 0.0f), new Vector2(-0.05f, 0.08f) }, 0.05f, Pal.MetalDark);
            b.Reset();
            b.Color(Pal.Team(team)).Box(hand + new Vector3(0f, -0.12f, 0f), new Vector3(0.07f, 0.05f, 0.07f));
            return Human(h, AttackStyle.Swing, 1.0f, 0.45f);
        }

        // Small insect assassin: big head, compound eyes, four wing fins and a needle in each hand.
        static RigDef Tikta(int team)
        {
            var h = new Humanoid(0.32f, 0.07f, 0.26f, 0.15f, 0.26f);
            Legs(h, 0.035f, Pal.TealDark, Pal.Ink, 0.11f);
            b.Bone = Hb.Chest;
            b.Color(Pal.Teal).Sphere(h.Chest + new Vector3(0f, 0.14f, 0f), new Vector3(0.12f, 0.16f, 0.11f), 10, 6);
            b.Color(Pal.TealDark).Box(h.Chest + new Vector3(0f, 0.14f, 0.09f), new Vector3(0.1f, 0.18f, 0.04f));
            b.Color(Pal.Team(team)).Torus(h.Neck + new Vector3(0f, -0.02f, 0f), 0.085f, 0.028f, 10, 4);
            b.Bone = Hb.Extra; // abdomen and wing fins
            Vector3 ab = h.Back + new Vector3(0f, -0.12f, -0.06f);
            b.Color(Pal.TealDark).Sphere(ab, new Vector3(0.1f, 0.1f, 0.15f), 10, 6);
            b.Color(Pal.Violet).Torus(ab + new Vector3(0f, 0f, -0.04f), 0.085f, 0.022f, 10, 4);
            b.Color(Pal.Wing);
            for (int s = -1; s <= 1; s += 2)
            {
                b.At(h.Back + new Vector3(0.08f * s, 0.08f, -0.05f), new Vector3(-20f, 30f * s, 55f * s));
                b.Sphere(new Vector3(0.17f * s, 0f, 0f), new Vector3(0.19f, 0.05f, 0.012f), 10, 4);
                b.At(h.Back + new Vector3(0.07f * s, 0.0f, -0.05f), new Vector3(-20f, 30f * s, 20f * s));
                b.Color(Pal.Teal).Sphere(new Vector3(0.13f * s, 0f, 0f), new Vector3(0.14f, 0.04f, 0.012f), 10, 4);
                b.Color(Pal.Wing);
            }
            b.Reset();
            Arms(h, 0.032f, Pal.Teal, Pal.TealDark, 0.04f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.12f, 0.02f);
            b.Color(Pal.Teal).Sphere(hc, new Vector3(0.15f, 0.13f, 0.14f), 12, 7);
            b.Color(Pal.Violet).Sphere(hc + new Vector3(-0.085f, 0.02f, 0.08f), new Vector3(0.07f, 0.08f, 0.07f), 8, 5); // compound eyes
            b.Sphere(hc + new Vector3(0.085f, 0.02f, 0.08f), new Vector3(0.07f, 0.08f, 0.07f), 8, 5);
            b.Color(Pal.Pink).Sphere(hc + new Vector3(-0.1f, 0.05f, 0.12f), 0.02f, 5, 3);
            b.Sphere(hc + new Vector3(0.1f, 0.05f, 0.12f), 0.02f, 5, 3);
            b.Color(Pal.TealDark).Box(hc + new Vector3(0f, -0.07f, 0.12f), new Vector3(0.06f, 0.03f, 0.04f)); // mandibles
            b.Color(Pal.Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 r0 = hc + new Vector3(0.04f * s, 0.11f, 0.06f), r1 = r0 + new Vector3(0.05f * s, 0.1f, 0.1f), r2 = r1 + new Vector3(0.05f * s, -0.02f, 0.07f);
                b.Beam(r0, r1, 0.014f, 0.014f);
                b.Beam(r1, r2, 0.014f, 0.014f);
                b.Color(Pal.TealDark).At(hc + new Vector3(0.12f * s, 0.07f, -0.03f), new Vector3(0f, 0f, -40f * s)).Extrude(
                    new[] { new Vector2(-0.04f, 0f), new Vector2(0.04f, 0f), new Vector2(0f, 0.14f) }, 0.02f); // head fins
                b.Color(Pal.Ink);
                b.Reset();
            }
            // two needles per hand, pointing forward
            for (int s = 0; s < 2; s++)
            {
                b.Bone = s == 0 ? Hb.PropL : Hb.Prop;
                Vector3 hand = s == 0 ? h.HandL : h.HandR;
                for (int n = -1; n <= 1; n += 2)
                {
                    Vector3 a = hand + new Vector3(0.025f * n, 0f, -0.04f), tip = a + new Vector3(0.03f * n, 0.06f, 0.3f);
                    b.Color(Pal.Steel).Beam(a, tip, 0.018f, 0.018f);
                    b.Color(Pal.Pink).Beam(tip, tip + (tip - a).normalized * 0.05f, 0.012f, 0.012f);
                }
            }
            return Human(h, AttackStyle.Flick, 0.95f, 0.3f);
        }

        // Goblin bomber: big ears, goggles, flame hair, big gloves, a bomb in hand and more on the back.
        static RigDef Blast(int team)
        {
            var h = new Humanoid(0.25f, 0.08f, 0.3f, 0.2f, 0.25f);
            Legs(h, 0.055f, Pal.LeatherDark, Pal.Ink);
            b.Bone = Hb.Hips;
            b.Color(Pal.Leather).Box(h.Hips + new Vector3(0f, 0.03f, 0f), new Vector3(0.26f, 0.1f, 0.19f));
            b.Bone = Hb.Chest;
            b.Color(Pal.Leather).Box(h.Chest + new Vector3(0f, 0.14f, 0f), new Vector3(0.28f, 0.27f, 0.2f), Pal.LeatherDark);
            b.Color(Pal.Goblin).Box(h.Chest + new Vector3(0f, 0.17f, 0.095f), new Vector3(0.12f, 0.18f, 0.02f));
            b.Color(Pal.Bomb).Beam(h.Chest + new Vector3(-0.14f, 0.26f, 0.105f), h.Chest + new Vector3(0.14f, 0.04f, 0.105f), 0.05f, 0.02f); // bandolier
            Scarf(h.Neck, 0.1f, team);
            b.Bone = Hb.Extra; // backpack of bombs
            b.Color(Pal.LeatherDark).Box(h.Back + new Vector3(0f, -0.02f, -0.04f), new Vector3(0.24f, 0.24f, 0.1f));
            b.Color(Pal.Bomb).Sphere(h.Back + new Vector3(-0.07f, 0.12f, -0.07f), 0.075f, 8, 5);
            b.Sphere(h.Back + new Vector3(0.07f, 0.12f, -0.07f), 0.075f, 8, 5);
            b.Color(Pal.Fuse).Beam(h.Back + new Vector3(-0.07f, 0.19f, -0.07f), h.Back + new Vector3(-0.06f, 0.24f, -0.08f), 0.015f, 0.015f);
            b.Beam(h.Back + new Vector3(0.07f, 0.19f, -0.07f), h.Back + new Vector3(0.08f, 0.24f, -0.08f), 0.015f, 0.015f);
            Arms(h, 0.04f, Pal.Goblin, Pal.Glove, 0.075f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.15f, 0.02f);
            b.Color(Pal.Goblin).Sphere(hc, new Vector3(0.16f, 0.15f, 0.15f), 12, 7);
            b.Color(Pal.GoblinDark).Sphere(hc + new Vector3(0f, -0.02f, 0.15f), new Vector3(0.035f, 0.05f, 0.05f), 6, 4); // nose
            b.Color(Pal.Goblin);
            for (int s = -1; s <= 1; s += 2)
                b.At(hc + new Vector3(0.13f * s, 0.02f, -0.01f), new Vector3(0f, 0f, -80f * s)).Cylinder(Vector3.zero, 0.05f, 0f, 0.2f, 6); // pointy ears
            b.Reset();
            Eyes(hc + new Vector3(0f, -0.005f, 0.115f), 0.06f, 0.035f, Pal.Fire);
            b.Color(Pal.Bomb).Torus(hc + new Vector3(0f, 0.065f, 0f), 0.148f, 0.018f, 14, 4); // goggle strap
            b.Color(Pal.Goggle).Sphere(hc + new Vector3(-0.05f, 0.08f, 0.12f), new Vector3(0.045f, 0.04f, 0.03f), 8, 5);
            b.Sphere(hc + new Vector3(0.05f, 0.08f, 0.12f), new Vector3(0.045f, 0.04f, 0.03f), 8, 5);
            b.Color(Pal.Black).Box(hc + new Vector3(0f, -0.07f, 0.12f), new Vector3(0.09f, 0.015f, 0.03f)); // grin
            b.Color(Pal.Cream).Box(hc + new Vector3(0.02f, -0.06f, 0.135f), new Vector3(0.02f, 0.02f, 0.01f));
            for (int i = 0; i < 3; i++) // flame hair
            {
                b.Color(i == 1 ? Pal.FireCore : Pal.Fire).At(hc + new Vector3((i - 1) * 0.055f, 0.11f, -0.02f - i * 0.02f), new Vector3(-20f, 0f, (1 - i) * 15f));
                b.Cylinder(Vector3.zero, 0.05f, 0f, i == 1 ? 0.2f : 0.15f, 6);
            }
            b.Reset();
            b.Bone = Hb.Prop; // the bomb in the right glove
            Vector3 bomb = h.HandR + new Vector3(0f, 0.04f, 0.09f);
            b.Color(Pal.Bomb).Sphere(bomb, 0.1f, 10, 6);
            b.Color(Pal.Cream).Sphere(bomb + new Vector3(-0.03f, 0.02f, 0.085f), 0.022f, 5, 3);
            b.Sphere(bomb + new Vector3(0.03f, 0.02f, 0.085f), 0.022f, 5, 3);
            b.Color(Pal.MetalDark).Cylinder(bomb + new Vector3(0f, 0.085f, 0f), 0.035f, 0.035f, 0.03f, 8);
            b.Color(Pal.Fuse).Beam(bomb + new Vector3(0f, 0.11f, 0f), bomb + new Vector3(0.02f, 0.17f, -0.02f), 0.018f, 0.018f);
            b.Color(Pal.Team(team)).Torus(bomb, 0.1f, 0.015f, 12, 4);
            return Human(h, AttackStyle.Throw, 0.95f, 0.32f, true);
        }

        // ---------------------------------------------------------------- minions, tower, wolf

        static RigDef MeleeMinion(int team)
        {
            var h = new Humanoid(0.2f, 0.07f, 0.24f, 0.16f, 0.2f);
            Legs(h, 0.045f, Pal.TeamDark(team), Pal.Ink, 0.12f);
            b.Bone = Hb.Chest;
            b.Color(Pal.Team(team)).Box(h.Chest + new Vector3(0f, 0.11f, 0f), new Vector3(0.26f, 0.24f, 0.2f), Pal.TeamLight(team));
            b.Color(Pal.MetalDark).Box(h.Chest + new Vector3(0f, 0.01f, 0f), new Vector3(0.28f, 0.04f, 0.22f));
            Arms(h, 0.04f, Pal.TeamDark(team), Pal.Skin, 0.05f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.1f, 0f);
            b.Color(Pal.Skin).Sphere(hc, 0.1f, 9, 6);
            Eyes(hc + new Vector3(0f, -0.01f, 0.08f), 0.035f, 0.022f);
            b.Color(Pal.Metal).Sphere(hc + new Vector3(0f, 0.03f, -0.01f), new Vector3(0.115f, 0.09f, 0.115f), 9, 5); // helmet
            b.Color(Pal.TeamLight(team)).Box(hc + new Vector3(0f, 0.12f, -0.01f), new Vector3(0.03f, 0.07f, 0.16f)); // crest
            b.Bone = Hb.Prop;
            Vector3 hand = h.HandR, dir = new Vector3(0f, 0.5f, 0.85f).normalized;
            b.Color(Pal.MetalDark).Box(hand + dir * 0.04f, new Vector3(0.1f, 0.03f, 0.03f));
            b.Color(Pal.Metal).Beam(hand + dir * 0.04f, hand + dir * 0.3f, 0.045f, 0.015f);
            b.Bone = Hb.PropL;
            b.Color(Pal.TeamLight(team)).At(h.HandL + new Vector3(-0.03f, 0.02f, 0.05f), new Vector3(0f, -70f, 90f)).Cylinder(Vector3.zero, 0.11f, 0.11f, 0.03f, 10, true, Pal.Team(team));
            b.Reset();
            b.Color(Pal.Metal).At(h.HandL + new Vector3(-0.065f, 0.02f, 0.06f), new Vector3(0f, 20f, 90f)).Torus(Vector3.zero, 0.11f, 0.012f, 12, 3);
            b.Reset();
            return new RigDef { Pivots = h.Pivots(), Parents = Hb.Parents, Kind = RigKind.Humanoid, Attack = AttackStyle.Swing, Height = 0.72f, Radius = 0.3f };
        }

        static RigDef RangedMinion(int team)
        {
            var h = new Humanoid(0.18f, 0.06f, 0.26f, 0.13f, 0.2f);
            Legs(h, 0.04f, Pal.LeatherDark, Pal.Ink, 0.1f);
            b.Bone = Hb.Chest;
            b.Color(Pal.Team(team)).Cylinder(h.Chest + new Vector3(0f, -0.1f, 0f), 0.17f, 0.1f, 0.36f, 10, true, Pal.TeamDark(team)); // robe
            b.Color(Pal.Leather).Box(h.Chest + new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.04f, 0.2f));
            Arms(h, 0.035f, Pal.Team(team), Pal.Skin, 0.045f);
            b.Bone = Hb.Head;
            Vector3 hc = h.Neck + new Vector3(0f, 0.1f, 0f);
            b.Color(Pal.Skin).Sphere(hc, 0.09f, 9, 6);
            Eyes(hc + new Vector3(0f, -0.01f, 0.075f), 0.032f, 0.02f);
            b.Color(Pal.TeamDark(team)).Sphere(hc + new Vector3(0f, 0.025f, -0.025f), new Vector3(0.11f, 0.1f, 0.11f), 9, 5); // hood
            b.At(hc + new Vector3(0f, 0.08f, -0.06f), new Vector3(-50f, 0f, 0f)).Cylinder(Vector3.zero, 0.07f, 0f, 0.12f, 7);
            b.Reset();
            b.Bone = Hb.PropL; // short bow
            Vector3 hand = h.HandL;
            b.Color(Pal.Wood).Beam(hand + new Vector3(0f, -0.2f, 0.04f), hand + new Vector3(0f, 0f, 0.1f), 0.03f, 0.03f);
            b.Beam(hand + new Vector3(0f, 0f, 0.1f), hand + new Vector3(0f, 0.2f, 0.04f), 0.03f, 0.03f);
            b.Color(Pal.Cloth).Beam(hand + new Vector3(0f, -0.2f, 0.04f), hand + new Vector3(0f, 0.2f, 0.04f), 0.008f, 0.008f);
            return new RigDef { Pivots = h.Pivots(), Parents = Hb.Parents, Kind = RigKind.Humanoid, Attack = AttackStyle.Bow, Height = 0.7f, Radius = 0.3f };
        }

        public const int CartBody = 0, CartBarrel = 1, CartWheelL = 2, CartWheelR = 3;

        static RigDef SiegeCart(int team)
        {
            var piv = new[] { new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.42f, -0.05f), new Vector3(-0.27f, 0.15f, -0.05f), new Vector3(0.27f, 0.15f, -0.05f) };
            b.Bone = CartBody;
            b.Color(Pal.Wood).Box(new Vector3(0f, 0.26f, 0f), new Vector3(0.44f, 0.16f, 0.62f), Pal.WoodDark);
            b.Color(Pal.Team(team)).Box(new Vector3(-0.225f, 0.28f, 0f), new Vector3(0.02f, 0.12f, 0.5f));
            b.Box(new Vector3(0.225f, 0.28f, 0f), new Vector3(0.02f, 0.12f, 0.5f));
            b.Color(Pal.WoodDark).Box(new Vector3(0f, 0.36f, -0.05f), new Vector3(0.22f, 0.08f, 0.26f));
            b.Color(Pal.MetalDark).Beam(new Vector3(-0.15f, 0.2f, 0.32f), new Vector3(0f, 0.2f, 0.45f), 0.03f, 0.03f); // tow bar
            b.Beam(new Vector3(0.15f, 0.2f, 0.32f), new Vector3(0f, 0.2f, 0.45f), 0.03f, 0.03f);
            b.Color(Pal.TeamLight(team)).Box(new Vector3(0f, 0.4f, -0.28f), new Vector3(0.05f, 0.2f, 0.03f)); // pennant pole
            b.Color(Pal.Team(team)).Box(new Vector3(0.06f, 0.46f, -0.28f), new Vector3(0.12f, 0.08f, 0.015f));
            b.Bone = CartBarrel;
            Vector3 bp = piv[CartBarrel];
            b.Color(Pal.MetalDark).At(bp, new Vector3(80f, 0f, 0f)).Cylinder(new Vector3(0f, -0.12f, 0f), 0.11f, 0.08f, 0.46f, 10, true, Pal.Black);
            b.Color(Pal.Metal).Torus(new Vector3(0f, 0.3f, 0f), 0.085f, 0.02f, 10, 4);
            b.Reset();
            b.Color(Pal.Team(team)).Sphere(bp + new Vector3(0f, 0f, -0.1f), 0.1f, 8, 5);
            for (int s = 0; s < 2; s++)
            {
                b.Bone = s == 0 ? CartWheelL : CartWheelR;
                Vector3 wp = piv[b.Bone];
                b.Color(Pal.WoodDark).At(wp, new Vector3(0f, 0f, 90f)).Cylinder(new Vector3(0f, -0.03f, 0f), 0.15f, 0.15f, 0.06f, 12, true, Pal.Wood);
                b.Color(Pal.MetalDark).Torus(Vector3.zero, 0.15f, 0.018f, 12, 3);
                b.Reset();
            }
            return new RigDef { Pivots = piv, Parents = new[] { -1, CartBody, CartBody, CartBody }, Kind = RigKind.Cart, Attack = AttackStyle.Cannon, Height = 0.75f, Radius = 0.45f };
        }

        public const int TowerBase = 0, TowerCrystal = 1;

        static RigDef BuildTower(int team)
        {
            var piv = new[] { Vector3.zero, new Vector3(0f, 2.75f, 0f) };
            b.Bone = TowerBase;
            b.Color(Pal.StoneDark).Cylinder(Vector3.zero, 0.95f, 0.9f, 0.35f, 8, true, Pal.Stone);
            b.Color(Pal.Stone).Cylinder(new Vector3(0f, 0.35f, 0f), 0.72f, 0.6f, 1.45f, 8, false);
            b.Color(Pal.Team(team)).Cylinder(new Vector3(0f, 1.15f, 0f), 0.665f, 0.655f, 0.18f, 8, false); // team band
            b.Color(Pal.StoneTop).Cylinder(new Vector3(0f, 1.8f, 0f), 0.78f, 0.78f, 0.22f, 8, true, Pal.StoneTop);
            for (int i = 0; i < 8; i++) // battlements
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                b.Color(Pal.Stone).Box(new Vector3(Mathf.Cos(a) * 0.68f, 2.1f, Mathf.Sin(a) * 0.68f), new Vector3(0.22f, 0.2f, 0.22f), Pal.StoneTop);
            }
            b.Color(Pal.Ink).Box(new Vector3(0f, 1.45f, 0.62f), new Vector3(0.18f, 0.28f, 0.1f)); // window
            b.Color(Pal.TeamDark(team)).Cylinder(new Vector3(0f, 2.02f, 0f), 0.45f, 0.38f, 0.4f, 8, true, Pal.Team(team));
            b.Color(Pal.Gold).Torus(new Vector3(0f, 2.42f, 0f), 0.26f, 0.04f, 10, 4);
            b.Bone = TowerCrystal;
            Vector3 c = piv[TowerCrystal];
            b.Color(Pal.TeamLight(team)).Cylinder(c, 0.24f, 0f, 0.42f, 6);
            b.At(c, new Vector3(180f, 0f, 0f)).Cylinder(Vector3.zero, 0.24f, 0f, 0.3f, 6);
            b.Reset();
            return new RigDef { Pivots = piv, Parents = new[] { -1, TowerBase }, Kind = RigKind.Tower, Attack = AttackStyle.Pulse, Height = 3.3f, Radius = 1.0f };
        }

        // Rickard's Meshy tower ("Arcane Crystal Forge"; the red team's texture has its crystal and banners turned red), about
        // 2.6 high and 2.4 across like in the old game, on a stone plinth, with four small team crystals circling its top.
        static RigDef BuildTowerProp(int team, Mesh prop)
        {
            Bounds pb = prop.bounds;
            float s = Mathf.Min(2.6f / pb.size.y, 2.4f / Mathf.Max(pb.size.x, pb.size.z));
            float top = 0.18f + pb.size.y * s;
            var piv = new[] { Vector3.zero, new Vector3(0f, top * 0.86f, 0f) };
            b.Bone = TowerBase;
            b.Color(Pal.StoneDark).Cylinder(Vector3.zero, 1.32f, 1.26f, 0.12f, 16, true, Pal.Stone);
            b.Color(Pal.Gold).Cylinder(new Vector3(0f, 0.12f, 0f), 1.12f, 1.1f, 0.03f, 16, true, Pal.Gold);
            b.Color(Pal.StoneTop).Cylinder(new Vector3(0f, 0.15f, 0f), 0.95f, 0.95f, 0.03f, 16, true, Pal.StoneTop);
            b.Bone = TowerCrystal;
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 c = piv[TowerCrystal] + new Vector3(Mathf.Cos(a) * 0.75f, 0f, Mathf.Sin(a) * 0.75f);
                b.Color(Pal.TeamLight(team)).Cylinder(c, 0.08f, 0f, 0.16f, 5);
                b.At(c, new Vector3(180f, 0f, 0f)).Cylinder(Vector3.zero, 0.08f, 0f, 0.12f, 5);
                b.Reset();
            }
            return new RigDef
            {
                Pivots = piv, Parents = new[] { -1, TowerBase }, Kind = RigKind.Tower, Attack = AttackStyle.Pulse, Height = top + 0.2f, Radius = 1.0f,
                PropMesh = prop, PropTex = Props.Texture(team == 1 ? "blackbruk_tower_red" : "blackbruk_tower"), PropBone = TowerBase,
                PropOffset = new Vector3(0f, 0.18f, 0f), PropScale = s,
            };
        }

        public const int TargetPost = 0, TargetBoard = 1;

        // The guided match's practice dummy: a round painted target board on a short post, rings on both faces.
        static RigDef BuildTarget()
        {
            var piv = new[] { Vector3.zero, new Vector3(0f, 0.75f, 0f) };
            b.Bone = TargetPost;
            b.Color(Pal.WoodDark).Cylinder(Vector3.zero, 0.22f, 0.18f, 0.08f, 10, true, Pal.Wood);
            b.Color(Pal.Wood).Box(new Vector3(0f, 0.4f, -0.06f), new Vector3(0.09f, 0.8f, 0.09f));
            b.Color(Pal.WoodDark).Beam(new Vector3(0f, 0.05f, -0.35f), new Vector3(0f, 0.55f, -0.08f), 0.06f, 0.06f); // back leg
            b.Bone = TargetBoard;
            Vector3 c = piv[TargetBoard];
            byte[] ring = { Pal.Cloth, Pal.Red, Pal.Cloth, Pal.Red, Pal.Gold };
            float[] rad = { 0.5f, 0.4f, 0.3f, 0.2f, 0.1f };
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? 0.04f : -0.04f;
                // x 90 turns the up-facing disc to face +z (side 0) or -z (side 1, x -90)
                for (int i = 0; i < ring.Length; i++)
                    b.Color(ring[i]).At(c + new Vector3(0f, 0f, z + (side == 0 ? i : -i) * 0.004f), new Vector3(side == 0 ? 90f : -90f, 0f, 0f)).Disc(Vector3.zero, rad[i], 24);
            }
            b.Reset();
            b.Color(Pal.WoodDark).At(c, new Vector3(90f, 0f, 0f)).Cylinder(new Vector3(0f, -0.035f, 0f), 0.53f, 0.53f, 0.07f, 24, false);
            b.Reset();
            return new RigDef { Pivots = piv, Parents = new[] { -1, TargetPost }, Kind = RigKind.Target, Attack = AttackStyle.None, Height = 1.3f, Radius = 0.45f };
        }

        public const int WolfBody = 0, WolfHead = 1, WolfFL = 2, WolfFR = 3, WolfBL = 4, WolfBR = 5, WolfTail = 6;

        static RigDef BuildWolf(int team, bool propBody = false)
        {
            var piv = new[]
            {
                new Vector3(0f, 0.36f, 0f), new Vector3(0f, 0.44f, 0.2f), new Vector3(-0.08f, 0.32f, 0.15f), new Vector3(0.08f, 0.32f, 0.15f),
                new Vector3(-0.08f, 0.32f, -0.16f), new Vector3(0.08f, 0.32f, -0.16f), new Vector3(0f, 0.42f, -0.22f),
            };
            if (propBody)
            {
                // Rickard's wolf mesh is the body. The code's ghostly glow disc under it read as "någon blå pöl" (tester Benny via
                // Rickard 2026-10-28: "skall vara bara en full spectral varg"): only a speck stays, so the skinned mesh is not empty
                b.Bone = WolfBody;
                b.Color(Pal.Ghost).Disc(new Vector3(0f, 0.03f, 0f), 0.001f, 3);
                return new RigDef
                {
                    Pivots = piv, Parents = new[] { -1, WolfBody, WolfBody, WolfBody, WolfBody, WolfBody, WolfBody }, Kind = RigKind.Wolf,
                    Attack = AttackStyle.Bite, Height = 0.7f, Radius = 0.3f, Ghost = true,
                };
            }
            b.Bone = WolfBody;
            b.Color(Pal.Ghost).Sphere(new Vector3(0f, 0.38f, -0.02f), new Vector3(0.12f, 0.12f, 0.24f), 10, 6);
            b.Sphere(new Vector3(0f, 0.41f, 0.12f), new Vector3(0.13f, 0.14f, 0.13f), 10, 6); // chest ruff
            b.Color(Pal.GhostDark).Sphere(new Vector3(0f, 0.47f, -0.02f), new Vector3(0.07f, 0.04f, 0.2f), 8, 4); // back stripe
            b.Bone = WolfHead;
            Vector3 hc = piv[WolfHead] + new Vector3(0f, 0.06f, 0.07f);
            b.Color(Pal.Ghost).Sphere(hc, new Vector3(0.1f, 0.09f, 0.1f), 10, 6);
            b.Box(hc + new Vector3(0f, -0.03f, 0.11f), new Vector3(0.08f, 0.07f, 0.14f)); // snout
            b.Color(Pal.GhostDark).Sphere(hc + new Vector3(0f, -0.01f, 0.18f), 0.025f, 6, 4);
            b.Color(Pal.GhostDark).At(hc + new Vector3(-0.06f, 0.07f, -0.02f), new Vector3(-10f, 0f, 15f)).Cylinder(Vector3.zero, 0.04f, 0f, 0.1f, 6);
            b.At(hc + new Vector3(0.06f, 0.07f, -0.02f), new Vector3(-10f, 0f, -15f)).Cylinder(Vector3.zero, 0.04f, 0f, 0.1f, 6);
            b.Reset();
            b.Color(Pal.TeamLight(team)).Sphere(hc + new Vector3(-0.045f, 0.02f, 0.08f), 0.022f, 6, 4); // glowing eyes in the team's colour
            b.Sphere(hc + new Vector3(0.045f, 0.02f, 0.08f), 0.022f, 6, 4);
            for (int i = 0; i < 4; i++)
            {
                b.Bone = WolfFL + i;
                Vector3 top = piv[b.Bone];
                b.Color(Pal.Ghost).Limb(top, new Vector3(top.x, 0.04f, top.z + 0.02f), 0.04f, 0.03f, 6);
            }
            b.Bone = WolfTail;
            b.Color(Pal.GhostDark).At(piv[WolfTail], new Vector3(-120f, 0f, 0f)).Cylinder(Vector3.zero, 0.05f, 0.0f, 0.24f, 7);
            b.Reset();
            return new RigDef
            {
                Pivots = piv, Parents = new[] { -1, WolfBody, WolfBody, WolfBody, WolfBody, WolfBody, WolfBody }, Kind = RigKind.Wolf,
                Attack = AttackStyle.Bite, Height = 0.7f, Radius = 0.3f, Ghost = true,
            };
        }
    }
}
