using BAM.Core;
using BAM.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// BAM > Art Preview: opens an empty scene, enters Play mode and lines up every hero (both teams), minion, tower,
    /// wolf, projectile, zone and effect on the BAM map, with buttons to play each animation and status. Uses only
    /// ArtKit's public API, the same way the game does, so what you see here is what the match will show.
    /// </summary>
    static class ArtPreviewMenu
    {
        const string Key = "BAM.ArtPreview.Pending";

        [MenuItem("BAM/Art Preview")]
        static void Open()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.11f, 0.14f, 0.25f);
            c.fieldOfView = 38f;
            c.nearClipPlane = 0.3f;
            c.farClipPlane = 120f;
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false);
                new GameObject("BAM Art Preview").AddComponent<ArtPreviewRunner>();
            };
        }
    }

    /// <summary>Plays the role of the game layer in the preview: builds the map and units and moves projectiles.</summary>
    sealed class ArtPreviewRunner : MonoBehaviour
    {
        // BAM_OriginalArena_v1_2, top half (row 0 = the enemy's back row); mirrored below the middle row like the game does.
        static readonly string[] Top =
        {
            "...............", "......000......", "...............", "g###.......###g", "ggg#...2...#ggg", "ggg#...1...#ggg",
            "ggg#.......#ggg", "ggg#.......#ggg", "ggg#.......#ggg", "ggg#.......#ggg", "ggg#.......#ggg", "ggg.........ggg",
            "ggg.........ggg", "ggg.........ggg", "...............", "...............", "...##.....##...", "...##.....##...",
        };

        static readonly HeroId[] heroes = { HeroId.Bram, HeroId.Fenn, HeroId.Tyra, HeroId.Moa, HeroId.Knut, HeroId.Tikta, HeroId.Blast };

        UnitView[,] heroViews = new UnitView[2, 7];
        UnitView[] others = new UnitView[12];
        int otherCount;
        Transform camT;
        Vector3[] focus;
        string[] focusName;
        int focusIndex;
        float zoom = 1f, timer, fxTimer, zoneTimer;
        int fxIndex;
        bool walking, auto = true, low;
        int statusIndex, visIndex;
        AimView aim;
        ZoneView[] zones = new ZoneView[3];

        struct Shot { public ProjectileView V; public Vector3 A, B; public float T, Len; }
        Shot[] shots = new Shot[17 * 2];

        static readonly StatusFlags[] statusCycle =
        {
            StatusFlags.None, StatusFlags.Stunned, StatusFlags.Slowed, StatusFlags.Rooted, StatusFlags.Burning, StatusFlags.Poisoned,
            StatusFlags.Shielded, StatusFlags.Hasted, StatusFlags.Illusion, StatusFlags.Stunned | StatusFlags.Burning | StatusFlags.Shielded,
        };

        void Start()
        {
            ArtKit.Init();
            ArtKit.ApplyQuality(GraphicsQuality.High);
            ArtKit.BuildArena(MakeArena());
            camT = Camera.main != null ? Camera.main.transform : null;

            for (int t = 0; t < 2; t++)
                for (int i = 0; i < 7; i++)
                {
                    var v = ArtKit.CreateHero(heroes[i], t);
                    v.transform.SetPositionAndRotation(new Vector3(1.5f + i * 2f, 0f, 7.5f + t * 2.2f), Quaternion.Euler(0f, t == 0 ? 180f : 0f, 0f));
                    v.SetHealth(0.35f + 0.1f * i, i == 3 ? 0.25f : 0f);
                    v.SetLevel(1 + i * 2);
                    v.SetMarker(t == 0 ? (i == 0 ? UnitMarker.Player : UnitMarker.Ally) : UnitMarker.Enemy);
                    // the outermost heroes stand in the bushes: own team half visible
                    if ((i == 0 || i == 6) && t == 0) v.SetVisibility(0.5f);
                    heroViews[t, i] = v;
                }

            for (int t = 0; t < 2; t++)
            {
                AddOther(ArtKit.CreateMinion(MinionKind.Melee, t), new Vector3(4.5f + t * 4f, 0f, 12.5f), t);
                AddOther(ArtKit.CreateMinion(MinionKind.Ranged, t), new Vector3(5.5f + t * 4f, 0f, 12.5f), t);
                AddOther(ArtKit.CreateMinion(MinionKind.Siege, t), new Vector3(6.6f + t * 4f, 0f, 12.5f), t);
                AddOther(ArtKit.CreateWolf(t), new Vector3(3f + t * 9f, 0f, 12.5f), t);
                AddOther(ArtKit.CreateTower(t), new Vector3(7.5f, 0f, t == 0 ? 5.5f : 29.5f), t);
            }

            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 17; k++)
                {
                    float x = 0.6f + k * 0.82f;
                    var a = new Vector3(x, 0f, 15f + t * 2f);
                    var s = new Shot { V = ArtKit.CreateProjectile((ProjKind)k, t), A = a, B = a + new Vector3(0f, 0f, 3.5f), Len = 0.9f, T = k * 0.05f };
                    if ((ProjKind)k == ProjKind.KnutAnchor) s.V.SetTether(heroViews[t, 4].transform);
                    s.V.transform.position = a;
                    s.V.Launch(s.A, s.B, s.Len);
                    shots[t * 17 + k] = s;
                }

            zones[0] = ArtKit.CreateZone(ZoneKind.HoneyField, 0);
            zones[1] = ArtKit.CreateZone(ZoneKind.FireLane, 1);
            zones[2] = ArtKit.CreateZone(ZoneKind.BombFuse, 1);
            ShowZones();

            aim = ArtKit.CreateAim();

            focus = new[] { new Vector3(7.5f, 0f, 9f), new Vector3(7.5f, 0f, 13f), new Vector3(7.5f, 0f, 17f), new Vector3(7.5f, 0f, 21.5f), new Vector3(7.5f, 0f, 25f), new Vector3(7.5f, 0f, 17.5f) };
            focusName = new[] { "Heroes", "Minions, wolf", "Projectiles", "Zones", "Effects", "Whole map" };
            Debug.Log("[BAM.Art] Preview ready. Buttons top left; the Scene view works too.");
        }

        void AddOther(UnitView v, Vector3 p, int team)
        {
            v.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, team == 0 ? 180f : 0f, 0f));
            v.SetHealth(0.7f, 0f);
            v.SetMarker(team == 0 ? UnitMarker.Ally : UnitMarker.Enemy);
            others[otherCount++] = v;
        }

        void ShowZones()
        {
            zones[0].Show(new Vector3(3f, 0f, 21.5f), Vector3.forward, 0f, 1.5f, 3f);
            zones[1].Show(new Vector3(7.5f, 0f, 19.8f), Vector3.forward, 4f, 0.6f, 3f);
            zones[2].Show(new Vector3(12f, 0f, 21.5f), Vector3.forward, 0f, 1.6f, 1.4f);
        }

        static ArenaInfo MakeArena()
        {
            int w = Top[0].Length, h = Top.Length * 2 - 1;
            var tiles = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = y < Top.Length ? Top[y] : Top[h - 1 - y];
                for (int x = 0; x < w; x++)
                {
                    char ch = row[x];
                    tiles[y * w + x] = ch == '#' ? (byte)3 : ch == 'g' ? (byte)2 : (byte)1;
                }
            }
            return new ArenaInfo
            {
                Width = w, Height = h, Tiles = tiles,
                Rocks = new[] { new Vector3(4f, 17.5f, 1.15f), new Vector3(11f, 17.5f, 1.15f) },
                Towers = new[] { new Vector3(7.5f, 0f, 5.5f), new Vector3(7.5f, 0f, 29.5f) },
            };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            timer += dt;

            for (int i = 0; i < shots.Length; i++)
            {
                ref Shot s = ref shots[i];
                if (s.V == null) continue;
                s.T += dt;
                if (s.T > s.Len + 0.35f)
                {
                    s.T = 0f;
                    s.V.transform.position = s.A;
                    s.V.Launch(s.A, s.B, s.Len);
                }
                s.V.transform.position = Vector3.Lerp(s.A, s.B, Mathf.Clamp01(s.T / s.Len));
            }

            zoneTimer += dt;
            if (zoneTimer > 3.4f)
            {
                zoneTimer = 0f;
                ArtKit.PlayFx(FxKind.Explosion, new Vector3(12f, 0f, 21.5f), 1.6f, 1);
                ShowZones();
            }

            fxTimer += dt;
            if (fxTimer > 0.6f)
            {
                fxTimer = 0f;
                FxKind k = (FxKind)(fxIndex % 16);
                ArtKit.PlayFx(k, new Vector3(1f + (fxIndex % 16) * 0.85f, 0f, 25f), 1f, fxIndex / 16 % 2);
                fxIndex++;
            }

            if (auto && timer > 2.2f)
            {
                timer = 0f;
                ForAll(v => v.PlayAttack(0.35f));
            }

            Vector3 player = heroViews[0, 0].transform.position;
            aim.ShowRange(player, 3.5f);
            float wob = Mathf.Sin(Time.time) * 0.8f;
            if (Mathf.Repeat(Time.time, 6f) < 3f) aim.ShowLine(heroViews[0, 2].transform.position, new Vector3(wob, 0f, 1f), 5f, 0.5f, wob < 0.5f);
            else aim.ShowCircle(heroViews[0, 6].transform.position + new Vector3(-1f, 0f, 3f), 1.4f, wob < 0.5f);

            if (camT != null && focus != null)
            {
                Vector3 f = focus[focusIndex];
                float dist = (focusIndex == focus.Length - 1 ? 3.2f : 1f) * zoom;
                camT.position = f + new Vector3(0f, 11.5f, -7.6f) * dist;
                camT.rotation = Quaternion.Euler(56.5f, 0f, 0f);
            }
        }

        void ForAll(System.Action<UnitView> a)
        {
            for (int t = 0; t < 2; t++) for (int i = 0; i < 7; i++) a(heroViews[t, i]);
            for (int i = 0; i < otherCount; i++) a(others[i]);
        }

        bool dead;

        void OnGUI()
        {
            float s = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUILayout.BeginArea(new Rect(10, 10, 230, 900));
            GUILayout.Box("BAM Art Preview");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<")) focusIndex = (focusIndex + focus.Length - 1) % focus.Length;
            GUILayout.Label(focusName[focusIndex]);
            if (GUILayout.Button(">")) focusIndex = (focusIndex + 1) % focus.Length;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Zoom in")) zoom = Mathf.Max(0.35f, zoom - 0.15f);
            if (GUILayout.Button("Zoom out")) zoom = Mathf.Min(2.5f, zoom + 0.15f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(walking ? "Stop walking" : "Walk"))
            {
                walking = !walking;
                bool w = walking;
                ForAll(v => v.SetMoving(w, 1f));
            }
            if (GUILayout.Button("Attack")) ForAll(v => v.PlayAttack(0.35f));
            if (GUILayout.Button("Cast special")) ForAll(v => v.PlayCast());
            if (GUILayout.Button("Hit")) ForAll(v => v.PlayHit(false));
            if (GUILayout.Button("Big hit")) ForAll(v => v.PlayHit(true));
            if (GUILayout.Button(dead ? "Respawn" : "Die"))
            {
                dead = !dead;
                bool d = dead;
                ForAll(v => v.SetDead(d));
                if (!d) ArtKit.PlayFx(FxKind.Respawn, heroViews[0, 0].transform.position);
            }
            statusIndex = Mathf.Clamp(statusIndex, 0, statusCycle.Length - 1);
            if (GUILayout.Button("Status: " + statusCycle[statusIndex]))
            {
                statusIndex = (statusIndex + 1) % statusCycle.Length;
                StatusFlags f = statusCycle[statusIndex];
                ForAll(v => v.SetStatus(f));
            }
            if (GUILayout.Button("Visibility: " + (visIndex == 0 ? "normal" : visIndex == 1 ? "half (grass)" : "hidden")))
            {
                visIndex = (visIndex + 1) % 3;
                float a = visIndex == 0 ? 1f : visIndex == 1 ? 0.5f : 0f;
                ForAll(v => v.SetVisibility(a));
            }
            if (GUILayout.Button(low ? "Quality: Low" : "Quality: High"))
            {
                low = !low;
                ArtKit.ApplyQuality(low ? GraphicsQuality.Low : GraphicsQuality.High);
            }
            auto = GUILayout.Toggle(auto, "Attack every 2 s");
            GUILayout.EndArea();
        }
    }
}
