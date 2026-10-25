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
            // make sure Rickard's models and pictures are imported and the hero list is built, even if the files arrived
            // while Unity was busy (otherwise the game falls back to the code figures)
            AssetDatabase.Refresh();
            HeroArtImport.BuildAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BAM Tool Scene"); // tells Boot (BAM.Game) not to start the game in this scene
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.11f, 0.14f, 0.25f);
            c.fieldOfView = 38f;
            c.nearClipPlane = 0.3f;
            c.farClipPlane = 120f;
            SessionState.SetBool(Key, true);
            EditorSceneManager.playModeStartScene = null; // play this scene, not the game's (BAM.Build sets it again afterwards)
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

        // The guided match's training island (Sim TutorialIsland.cs IslandMap), built beside the arena at x + IslandX.
        const float IslandX = 40f;
        static readonly string[] IslandLower =
        {
            "     ...     ", "     ...     ", "     ...     ", "             ", "             ",
            "  .........  ", "  .........  ", "  .........  ", "  .........  ", "  .... ....  ",
            "  .........  ", "ggg.......ggg", "gg#.......ggg", "ggg.......#gg", "ggg.......#gg",
            "ggg.......#gg", "g...........g", "g...........g", "g...........g", "g...........g",
            "g...........g", "g...........g", "gg#.......ggg", "gg#.......ggg", "gg#.......ggg",
            "ggg.......#gg", "ggg.......ggg", "  .........  ", "  .... ....  ", "  .........  ",
            "  .........  ", "  .........  ", "  .........  ",
        };
        GameObject island;

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
        ZoneView[] zones = new ZoneView[5];

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
            BuildIsland(false);
            Camera mc = Camera.main;
            if (mc == null)
            {
                mc = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                mc.clearFlags = CameraClearFlags.SolidColor;
                mc.backgroundColor = new Color(0.11f, 0.14f, 0.25f);
                mc.fieldOfView = 38f;
                mc.farClipPlane = 120f;
            }
            camT = mc.transform;
            mc.depth = 100f; // draws over any camera the game itself started
            int heroArt = 0, arenaTex = 0;
            foreach (HeroId h in heroes) if (Resources.Load<HeroArt>("BAMArt/Heroes/" + h) != null) heroArt++;
            foreach (string t in new[] { "Cobble", "Road2", "Grass" }) if (Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_Ground_" + t) != null) arenaTex++;
            if (Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_Block") != null) arenaTex++;
            Debug.Log("[BAM.Art] Preview: " + heroArt + " of 7 hero models, " + arenaTex + " of 4 arena pictures found");

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
            AddOther(ArtKit.CreateTarget(), new Vector3(7.5f, 0f, 13.6f), 1);

            for (int t = 0; t < 2; t++)
                for (int k = 0; k < 17; k++)
                {
                    float x = 0.6f + k * 0.82f;
                    var a = new Vector3(x, 0f, 15f + t * 2f);
                    var s = new Shot { V = ArtKit.CreateProjectile((ProjKind)k, t), A = a, B = a + new Vector3(0f, 0f, 3.5f), Len = 0.9f, T = k * 0.05f };
                    // no tether here: Knut stands far away in the preview, so the chain would cross the whole map
                    s.V.transform.position = a;
                    s.V.Launch(s.A, s.B, s.Len);
                    shots[t * 17 + k] = s;
                }

            zones[0] = ArtKit.CreateZone(ZoneKind.HoneyField, 0);
            zones[1] = ArtKit.CreateZone(ZoneKind.FireLane, 1);
            zones[2] = ArtKit.CreateZone(ZoneKind.BombFuse, 1);
            zones[3] = ArtKit.CreateZone(ZoneKind.WarnLine, 1);
            zones[4] = ArtKit.CreateZone(ZoneKind.WarnCircle, 1);
            ShowZones();

            aim = ArtKit.CreateAim();

            focus = new[] { new Vector3(7.5f, 0f, 9f), new Vector3(7.5f, 0f, 13f), new Vector3(7.5f, 0f, 17f), new Vector3(7.5f, 0f, 21.5f), new Vector3(7.5f, 0f, 25f), new Vector3(7.5f, 0f, 17.5f),
                new Vector3(IslandX + 6.5f, 0f, 50f - 11.4f), new Vector3(IslandX + 6.5f, 0f, 50f - 24f) };
            focusName = new[] { "Heroes", "Minions, wolf", "Projectiles", "Zones", "Effects", "Whole map", "Island", "Island bridge, lane" };
            Debug.Log("[BAM.Art] Preview ready. Buttons top left; the Scene view works too.");
        }

        void BuildIsland(bool rise)
        {
            if (island != null) Destroy(island);
            const int w = 13, h = 50;
            var tiles = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte t;
                    if (y < 17)
                    {
                        float dx = x + 0.5f - 6.5f, dy = y + 0.5f - 11f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        bool neck = y == 16 && x >= 5 && x <= 7;
                        t = d > 5.25f && !neck ? (byte)0 : d > 4f && Mathf.Abs(dx) > 2.6f && Mathf.Abs(dy) > 2.6f ? (byte)2 : (byte)1;
                    }
                    else
                    {
                        char ch = IslandLower[y - 17][x];
                        t = ch == ' ' ? (byte)0 : ch == 'g' ? (byte)2 : ch == '#' ? (byte)3 : (byte)1;
                    }
                    tiles[y * w + x] = t;
                }
            island = ArtKit.BuildArena(new ArenaInfo { Width = w, Height = h, Tiles = tiles, Rocks = new Vector3[0], Island = true, LaneRises = rise });
            island.transform.position = new Vector3(IslandX, 0f, 0f);
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
            zones[3].Show(new Vector3(14f, 0f, 19.5f), new Vector3(-0.3f, 0f, 1f), 4.5f, 0.35f, 1.2f);
            zones[4].Show(new Vector3(7.5f, 0f, 24f), Vector3.forward, 0f, 1.1f, 1.2f);
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

        float uiCheck;

        // The preview is about the world: if the game's menus started anyway (Play pressed in the game scene first, or an older
        // Boot), switch their canvases off so they do not cover the view.
        void HideGameUi()
        {
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c.enabled && camT != null && c.transform != camT && c.targetTexture == null)
                {
                    c.enabled = false;
                    Debug.Log("[BAM.Art] Preview switched off the game camera '" + c.name + "'");
                }
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.isRootCanvas)
                {
                    c.gameObject.SetActive(false);
                    Debug.Log("[BAM.Art] Preview hid the menu canvas '" + c.name + "'");
                }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            timer += dt;
            uiCheck -= dt;
            if (uiCheck <= 0f) { uiCheck = 0.5f; HideGameUi(); }

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
                float dist = (focusIndex == 5 ? 3.2f : focusIndex >= 6 ? 2.2f : focusIndex == 0 ? 0.6f : 1f) * zoom; // whole map far, island pages wide, heroes up close
                camT.position = f + new Vector3(0f, 11.5f, -7.6f) * dist;
                camT.rotation = Quaternion.Euler(56.5f, 0f, 0f);
                // the island's sky (the match sets the same colour on its camera there)
                Camera cam = camT.GetComponent<Camera>();
                if (cam != null) cam.backgroundColor = focusIndex >= 6 ? new Color32(0x79, 0xb9, 0xee, 0xff) : new Color(0.11f, 0.14f, 0.25f);
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
            if (GUILayout.Button("Island: raise the bridge")) { BuildIsland(true); focusIndex = 7; }
            auto = GUILayout.Toggle(auto, "Attack every 2 s");
            GUILayout.EndArea();
        }
    }
}
