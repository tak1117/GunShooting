using System.Collections.Generic;
using System.IO;
using System.Linq;
using Detections;
using Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;
using Zombies;

namespace ShootingRange.EditorTools
{
    /// <summary>
    /// Generates the "IndoorRange" scene: a 5-lane, 25 m indoor pistol range with a lobby,
    /// built entirely from generated meshes, textures and materials.
    /// Layout: the shooter faces +Z. Firing line at z = 0.6, lobby behind the glass partition.
    /// </summary>
    public static class IndoorRangeBuilder
    {
        private const string Root = "Assets/Range";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshAsset = Root + "/Meshes/RangeMeshes.asset";
        private const string ScenePath = "Assets/Scenes/IndoorRange/IndoorRange.unity";
        private const string VolumeProfilePath = Root + "/RangeVolumeProfile.asset";
        private const int EnvironmentLayer = 6; // "Baked": walked on / collided with by SmoothLocomotion

        // Building dimensions (metres)
        private const float HalfWidth = 8f;
        private const float LobbyBack = -9f;
        private const float Partition = -3.6f;
        private const float BoothBack = -1.2f;
        private const float FiringLine = 0.6f;
        private const float BermStart = 27f;
        private const float BackWall = 30f;
        private const float RoomHeight = 4.8f;
        private const float TileCeiling = 3.0f;
        private const float BoothHeader = 2.3f;
        private const float CounterTop = 1.0f;
        private const float RailBottom = 3.78f;

        private static readonly float[] Lanes = { -4.8f, -2.4f, 0f, 2.4f, 4.8f };

        private static RangeMaterials _m;
        private static RangeMeshFactory _meshes;
        private static TMP_FontAsset _fontBold;
        private static TMP_FontAsset _fontRegular;
        private static ParticleComponentRef _hitParticle;

        [MenuItem("Tools/Shooting Range/Build Indoor Range")]
        public static void BuildMenu() => Build(false);

        [MenuItem("Tools/Shooting Range/Build Indoor Range (regenerate textures)")]
        public static void BuildForceTextures() => Build(true);

        public static void Build(bool forceTextures)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var dir in new[] { TextureFolder, MaterialFolder, Root + "/Meshes", Path.GetDirectoryName(ScenePath) })
                Directory.CreateDirectory(dir!);
            AssetDatabase.Refresh();

            try
            {
                EditorUtility.DisplayProgressBar("Indoor Range", "Generating textures and materials...", 0.1f);
                _m = new RangeMaterials(TextureFolder, MaterialFolder, forceTextures);
                _meshes = new RangeMeshFactory(MeshAsset);
                _fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/RobotoFontAssets/Roboto-Bold SDF.asset");
                _fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/RobotoFontAssets/Roboto-Medium SDF.asset");
                _hitParticle = new ParticleComponentRef("Assets/Prefabs/Particles/Bullet_Particle.prefab");

                EditorUtility.DisplayProgressBar("Indoor Range", "Building scene...", 0.5f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var env = new GameObject("Environment").transform;
                BuildShell(env);
                BuildPartition(env);
                BuildBooths(env);
                BuildDownrange(env);
                BuildBerm(env);
                BuildLobby(env);
                BuildDetails(env);
                BuildAmbience(forceTextures);
                BuildLighting();
                BuildGameplay();
                var player = BuildPlayer();
                BuildZombieGame(player);

                _meshes.Finish();
                ConfigureRenderSettings();
                ConfigurePipeline();

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuildSettings();
                AssetDatabase.SaveAssets();
                Debug.Log("[IndoorRange] Scene built: " + ScenePath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ================================================================ shell

        private static void BuildShell(Transform env)
        {
            var shell = Group("Shell", env);

            // Floors: polished concrete in the lobby / shooter area, sealed concrete downrange.
            var floors = Group("Floor", shell);
            FloorStrip(floors, LobbyBack, FiringLine, _m.ConcreteFloor);
            FloorStrip(floors, FiringLine, BackWall, _m.ConcreteDownrange);

            // Perimeter walls (0.3 m block), split into 4 m segments so each receives nearby lights.
            var walls = Group("Walls", shell);
            for (var z = LobbyBack; z < BackWall - 0.01f; z += 4f)
            {
                var z1 = Mathf.Min(z + 4f, BackWall);
                foreach (var side in new[] { -1f, 1f })
                {
                    var front = z1 <= FiringLine + 0.01f;
                    var mat = front ? _m.BlockWall : _m.BlockWallDark;
                    Box($"Wall_{(side < 0 ? "L" : "R")}_{z:0}", walls,
                        new Vector3(side * (HalfWidth + 0.15f), RoomHeight / 2f, (z + z1) / 2f),
                        new Vector3(0.3f, RoomHeight, z1 - z), mat, Env.Hit);
                }
            }

            for (var x = -HalfWidth; x < HalfWidth - 0.01f; x += 4f)
            {
                Box($"Wall_Back_{x:0}", walls, new Vector3(x + 2f, RoomHeight / 2f, LobbyBack - 0.15f),
                    new Vector3(4f, RoomHeight, 0.3f), _m.BlockWall, Env.Hit);
                Box($"Wall_End_{x:0}", walls, new Vector3(x + 2f, RoomHeight / 2f, BackWall + 0.15f),
                    new Vector3(4f, RoomHeight, 0.3f), _m.SteelDark, Env.Hit);
            }

            // Dark wainscot band in the lobby / shooter area.
            var trim = Group("Wainscot", shell);
            foreach (var side in new[] { -1f, 1f })
            foreach (var (z0, z1) in new[] { (LobbyBack, -5f), (-5f, -1f), (-1f, BoothBack) })
            {
                if (z1 <= z0) continue;
                Box("Wainscot", trim, new Vector3(side * (HalfWidth - 0.01f), 0.55f, (z0 + z1) / 2f),
                    new Vector3(0.02f, 1.1f, z1 - z0), _m.BlockWallDark, Env.Decor);
                Box("WainscotCap", trim, new Vector3(side * (HalfWidth - 0.025f), 1.11f, (z0 + z1) / 2f),
                    new Vector3(0.05f, 0.025f, z1 - z0), _m.SteelBrushed, Env.Decor);
            }

            for (var x = -HalfWidth; x < HalfWidth - 0.01f; x += 4f)
            {
                Box("Wainscot", trim, new Vector3(x + 2f, 0.55f, LobbyBack + 0.01f), new Vector3(4f, 1.1f, 0.02f),
                    _m.BlockWallDark, Env.Decor);
                Box("WainscotCap", trim, new Vector3(x + 2f, 1.11f, LobbyBack + 0.025f),
                    new Vector3(4f, 0.025f, 0.05f), _m.SteelBrushed, Env.Decor);
            }

            // Skirting
            foreach (var side in new[] { -1f, 1f })
                Box("Skirting", trim, new Vector3(side * (HalfWidth - 0.015f), 0.05f, (LobbyBack + BoothBack) / 2f),
                    new Vector3(0.03f, 0.1f, BoothBack - LobbyBack), _m.PlasticBlack, Env.Decor);

            // Lobby / shooter-area suspended ceiling.
            var ceiling = Group("Ceiling", shell);
            for (var x = -HalfWidth; x < HalfWidth - 0.01f; x += 4f)
            foreach (var (z0, z1) in new[] { (LobbyBack, -6.6f), (-6.6f, Partition), (Partition, BoothBack) })
                Box("CeilingTiles", ceiling, new Vector3(x + 2f, TileCeiling + 0.025f, (z0 + z1) / 2f),
                    new Vector3(4f, 0.05f, z1 - z0), _m.CeilingTile, Env.Hit);

            // Range ceiling deck (above the booths and downrange).
            var deck = Instantiate(_m.CeilingDeck, "M_CeilingDeckDark", new Color(0.46f, 0.47f, 0.49f));
            for (var z = BoothBack; z < BackWall - 0.01f; z += 4f)
            {
                var z1 = Mathf.Min(z + 4f, BackWall);
                for (var x = -HalfWidth; x < HalfWidth - 0.01f; x += 4f)
                    Box("Deck", ceiling, new Vector3(x + 2f, RoomHeight + 0.05f, (z + z1) / 2f),
                        new Vector3(4f, 0.1f, z1 - z), deck, Env.Hit);
            }

            // Bulkhead closing the step between the tile ceiling and the booth header.
            Box("Bulkhead", ceiling, new Vector3(0f, (BoothHeader + RoomHeight) / 2f + 0.05f, BoothBack - 0.1f),
                new Vector3(HalfWidth * 2f, RoomHeight - BoothHeader - 0.1f, 0.2f), _m.BlockWallDark, Env.Hit);
        }

        private static void FloorStrip(Transform parent, float z0, float z1, Material mat)
        {
            for (var z = z0; z < z1 - 0.01f; z += 4f)
            {
                var zEnd = Mathf.Min(z + 4f, z1);
                for (var x = -HalfWidth; x < HalfWidth - 0.01f; x += 4f)
                    Box("Floor", parent, new Vector3(x + 2f, -0.1f, (z + zEnd) / 2f), new Vector3(4f, 0.2f, zEnd - z),
                        mat, Env.Hit);
            }
        }

        // ================================================================ partition

        private static void BuildPartition(Transform env)
        {
            var root = Group("GlassPartition", env);
            const float door = 1.0f; // half width of the opening
            const float glassBottom = 1.0f, glassTop = 2.6f, doorTop = 2.3f;

            foreach (var side in new[] { -1f, 1f })
            {
                var x0 = side * door;
                var x1 = side * HalfWidth;
                var cx = (x0 + x1) / 2f;
                var w = Mathf.Abs(x1 - x0);
                Box("LowerWall", root, new Vector3(cx, glassBottom / 2f, Partition), new Vector3(w, glassBottom, 0.16f),
                    _m.BlockWall, Env.Hit);
                Box("Sill", root, new Vector3(cx, glassBottom + 0.015f, Partition), new Vector3(w, 0.03f, 0.22f),
                    _m.SteelBrushed, Env.Hit);
                Box("Glass", root, new Vector3(cx, (glassBottom + glassTop) / 2f, Partition),
                    new Vector3(w, glassTop - glassBottom, 0.02f), _m.Glass, Env.Hit);
                Box("Header", root, new Vector3(cx, (glassTop + TileCeiling) / 2f, Partition),
                    new Vector3(w, TileCeiling - glassTop, 0.16f), _m.BlockWall, Env.Hit);

                // Mullions
                var count = Mathf.CeilToInt(w / 1.5f);
                for (var i = 0; i <= count; i++)
                {
                    var x = Mathf.Lerp(x0, x1, i / (float)count);
                    Box("Mullion", root, new Vector3(x, (glassBottom + glassTop) / 2f, Partition),
                        new Vector3(0.05f, glassTop - glassBottom, 0.07f), _m.SteelBrushed, Env.Hit);
                }

                Box("Transom", root, new Vector3(cx, glassTop, Partition), new Vector3(w, 0.05f, 0.07f), _m.SteelBrushed,
                    Env.Hit);
                Box("DoorJamb", root, new Vector3(x0, doorTop / 2f, Partition), new Vector3(0.08f, doorTop, 0.2f),
                    _m.SteelBrushed, Env.Hit);
            }

            Box("DoorHeader", root, new Vector3(0f, (doorTop + TileCeiling) / 2f, Partition),
                new Vector3(door * 2f, TileCeiling - doorTop, 0.16f), _m.BlockWall, Env.Hit);
            Box("DoorHeadTrim", root, new Vector3(0f, doorTop + 0.02f, Partition),
                new Vector3(door * 2f + 0.08f, 0.04f, 0.2f), _m.SteelBrushed, Env.Hit);

            // Threshold hazard strip
            FloorDecal("DoorThreshold", root, new Vector3(0f, 0.002f, Partition), 2f, 0.3f, _m.Hazard);
        }

        // ================================================================ booths

        private static void BuildBooths(Transform env)
        {
            var root = Group("FiringLine", env);
            const float depth = FiringLine - BoothBack;
            var centreZ = (FiringLine + BoothBack) / 2f;

            // Dividers between lanes and at the outer edges
            var dividerXs = new[] { -6f, -3.6f, -1.2f, 1.2f, 3.6f, 6f };
            foreach (var x in dividerXs)
            {
                var d = Group($"Divider_{x:0.0}", root);
                Box("Panel", d, new Vector3(x, 0.05f + 1.1f, centreZ - 0.01f), new Vector3(0.07f, 2.2f, depth - 0.04f),
                    _m.AcousticNavy, Env.Hit);
                Box("TopCap", d, new Vector3(x, 2.265f, centreZ - 0.01f), new Vector3(0.09f, 0.03f, depth - 0.02f),
                    _m.SteelBrushed, Env.Hit);
                Box("FrontCap", d, new Vector3(x, 1.15f, FiringLine - 0.025f), new Vector3(0.09f, 2.3f, 0.03f),
                    _m.SteelBrushed, Env.Hit);
                Box("BackCap", d, new Vector3(x, 1.15f, BoothBack + 0.005f), new Vector3(0.09f, 2.3f, 0.03f),
                    _m.SteelBrushed, Env.Hit);
                Box("Foot", d, new Vector3(x, 0.025f, centreZ), new Vector3(0.12f, 0.05f, depth), _m.SteelDark, Env.Hit);
            }

            // Solid end sections from the outer dividers to the side walls
            foreach (var side in new[] { -1f, 1f })
            {
                var cx = side * (6f + HalfWidth) / 2f;
                Box("EndWall", root, new Vector3(cx, BoothHeader / 2f, centreZ), new Vector3(HalfWidth - 6.04f, BoothHeader, depth),
                    _m.BlockWall, Env.Hit);
            }

            // Booth header (canopy) and the first ballistic baffle at the firing line
            Box("BoothHeader", root, new Vector3(0f, BoothHeader + 0.05f, centreZ), new Vector3(HalfWidth * 2f, 0.1f, depth),
                _m.AcousticGraphite, Env.Hit);
            Box("SafetyBaffle", root, new Vector3(0f, (BoothHeader + RoomHeight) / 2f, FiringLine + 0.05f),
                new Vector3(HalfWidth * 2f, RoomHeight - BoothHeader, 0.1f), _m.SteelDark, Env.Hit);
            Box("SafetyBaffleRubber", root, new Vector3(0f, BoothHeader + 0.3f, FiringLine + 0.11f),
                new Vector3(HalfWidth * 2f, 0.6f, 0.04f), _m.RubberBerm, Env.Hit);
            Box("BaffleEdge", root, new Vector3(0f, BoothHeader + 0.015f, FiringLine + 0.05f),
                new Vector3(HalfWidth * 2f, 0.03f, 0.14f), _m.PaintedYellow, Env.Decor);

            for (var i = 0; i < Lanes.Length; i++)
            {
                var lx = Lanes[i];
                var lane = Group($"Booth_{i + 1}", root);

                // Shooting counter (blocks walking downrange)
                Box("CounterTop", lane, new Vector3(lx, CounterTop - 0.02f, 0.35f), new Vector3(2.32f, 0.04f, 0.5f),
                    _m.WoodCounter, Env.Hit);
                Box("CounterEdge", lane, new Vector3(lx, CounterTop - 0.02f, 0.095f), new Vector3(2.32f, 0.05f, 0.025f),
                    _m.SteelBrushed, Env.Hit);
                Box("CounterFront", lane, new Vector3(lx, (CounterTop - 0.04f) / 2f, FiringLine - 0.035f),
                    new Vector3(2.32f, CounterTop - 0.04f, 0.03f), _m.SteelDark, Env.Hit);
                foreach (var s in new[] { -1f, 1f })
                    Box("Bracket", lane, new Vector3(lx + s * 1.1f, CounterTop / 2f, 0.33f),
                        new Vector3(0.03f, CounterTop - 0.04f, 0.46f), _m.SteelDark, Env.Hit);

                // Anti-fatigue mat and the ready line
                Box("Mat", lane, new Vector3(lx, 0.006f, -0.45f), new Vector3(1.9f, 0.012f, 1.2f), _m.RubberMat,
                    Env.Decor);

                // Booth light under the canopy
                Box("BoothLight", lane, new Vector3(lx, BoothHeader - 0.015f, -0.1f), new Vector3(1.6f, 0.03f, 0.12f),
                    _m.LedWhite, Env.Decor);

                // Monitor arm + housing (decorative; screen is blank)
                var monitor = Group("Monitor", lane);
                monitor.position = new Vector3(lx + 0.8f, 2.1f, 0.5f);
                monitor.rotation = Quaternion.Euler(-15f, 0f, 0f);
                Box("Arm", lane, new Vector3(lx + 0.8f, 2.24f, 0.52f), new Vector3(0.025f, 0.12f, 0.025f), _m.SteelDark,
                    Env.Decor);
                LocalBox("Housing", monitor, Vector3.zero, new Vector3(0.34f, 0.22f, 0.035f), _m.PlasticBlack, Env.Decor);
                LocalBox("Screen", monitor, new Vector3(0f, 0f, -0.0185f), new Vector3(0.31f, 0.19f, 0.002f),
                    _m.ScreenGlass, Env.Decor);
            }

            // Floor markings on the shooter side
            FloorDecal("ReadyLine", root, new Vector3(0f, 0.002f, BoothBack - 0.12f), HalfWidth * 2f - 0.1f, 0.12f,
                _m.Hazard);
        }

        // ================================================================ downrange

        private static void BuildDownrange(Transform env)
        {
            var root = Group("Downrange", env);

            // Firing line, lane lines, distance lines
            FloorDecal("FiringLine", root, new Vector3(0f, 0.002f, FiringLine + 0.08f), HalfWidth * 2f - 0.1f, 0.1f,
                _m.FloorLineRed);
            foreach (var x in new[] { -6f, -3.6f, -1.2f, 1.2f, 3.6f, 6f })
                FloorDecal("LaneLine", root, new Vector3(x, 0.002f, (FiringLine + BermStart) / 2f + 0.2f), 0.05f,
                    BermStart - FiringLine - 0.4f, _m.FloorLineWhite);
            for (var d = 5; d <= 25; d += 5)
            {
                var z = FiringLine + d;
                FloorDecal($"Distance_{d}m", root, new Vector3(0f, 0.002f, z), 12f, 0.04f, _m.FloorLineWhite);
                FloorText($"{d} m", root, new Vector3(-7.0f, 0.004f, z + 0.35f), 0.32f, new Color(0.9f, 0.9f, 0.88f));
                FloorText($"{d} m", root, new Vector3(7.0f, 0.004f, z + 0.35f), 0.32f, new Color(0.9f, 0.9f, 0.88f));
            }

            for (var i = 0; i < Lanes.Length; i++)
                FloorText((i + 1).ToString("00"), root, new Vector3(Lanes[i], 0.004f, FiringLine + 1.3f), 0.7f,
                    new Color(0.92f, 0.92f, 0.9f));

            // Side walls: steel kick plate + acoustic panels
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (HalfWidth - 0.03f);
                for (var z = FiringLine + 0.4f; z < BermStart - 1f; z += 4f)
                {
                    Box("KickPlate", root, new Vector3(x, 0.45f, z + 2f), new Vector3(0.06f, 0.9f, 4f), _m.SteelDark,
                        Env.Hit);
                    Box("AcousticPanel", root, new Vector3(side * (HalfWidth - 0.04f), 2.05f, z + 2f),
                        new Vector3(0.08f, 2.1f, 3.6f), _m.AcousticGraphite, Env.Hit);
                }
            }

            // Ballistic ceiling baffles (angled so their rubber face looks at the shooter)
            for (var z = 4f; z < BermStart; z += 4f)
            {
                var baffle = Group($"Baffle_{z:0}", root);
                baffle.position = new Vector3(0f, 4.35f, z);
                baffle.rotation = Quaternion.Euler(35f, 0f, 0f);
                LocalBox("Plate", baffle, Vector3.zero, new Vector3(HalfWidth * 2f - 0.02f, 0.03f, 1.5f), _m.SteelDark,
                    Env.Hit);
                LocalBox("Rubber", baffle, new Vector3(0f, -0.04f, 0f), new Vector3(HalfWidth * 2f - 0.06f, 0.05f, 1.45f),
                    _m.RubberBerm, Env.Hit);
            }

            // Overhead target carrier rails with hangers
            foreach (var lx in Lanes)
            {
                Box("Rail", root, new Vector3(lx, RailBottom + 0.05f, 13.5f), new Vector3(0.09f, 0.1f, 24f),
                    _m.SteelBrushed, Env.Decor);
                Box("RailLip", root, new Vector3(lx, RailBottom + 0.005f, 13.5f), new Vector3(0.14f, 0.01f, 24f),
                    _m.SteelDark, Env.Decor);
                foreach (var z in new[] { 2f, 6f, 10f, 14f, 18f, 22f, 25.4f })
                    Cylinder("Hanger", root, new Vector3(lx, (RailBottom + 0.1f + RoomHeight) / 2f, z), 0.012f,
                        RoomHeight - RailBottom - 0.1f, _m.SteelBrushed, Quaternion.identity);
                Box("CarrierMotor", root, new Vector3(lx, RailBottom + 0.12f, 1.55f), new Vector3(0.22f, 0.24f, 0.3f),
                    _m.PaintedGrey, Env.Decor);
            }

            // Ventilation ducts along both walls
            foreach (var side in new[] { -1f, 1f })
            {
                Cylinder("Duct", root, new Vector3(side * 7.25f, 4.3f, (FiringLine + BackWall) / 2f), 0.28f,
                    BackWall - FiringLine - 0.4f, _m.SteelBrushed, Quaternion.Euler(90f, 0f, 0f));
                for (var z = 3f; z < BermStart; z += 6f)
                    Box("DuctStrap", root, new Vector3(side * 7.25f, 4.3f, z), new Vector3(0.62f, 0.62f, 0.04f),
                        _m.SteelDark, Env.Decor);
            }

            // Linear LED fixtures between baffles
            foreach (var z in new[] { 2f, 10f, 18f, 26f })
            foreach (var x in new[] { -5f, 0f, 5f })
            {
                Box("Fixture", root, new Vector3(x, 4.52f, z), new Vector3(4.2f, 0.08f, 0.18f), _m.Fixture, Env.Decor);
                Box("FixtureLens", root, new Vector3(x, 4.475f, z), new Vector3(4.1f, 0.01f, 0.12f), _m.LedWhite,
                    Env.Decor);
            }
        }

        // ================================================================ backstop

        private static void BuildBerm(Transform env)
        {
            var root = Group("Backstop", env);
            var ramp = Group("DeflectorRamp", root);
            ramp.position = new Vector3(0f, 1.1f, 28.4f);
            ramp.rotation = Quaternion.Euler(-40f, 0f, 0f);
            LocalBox("Plate", ramp, Vector3.zero, new Vector3(HalfWidth * 2f - 0.02f, 0.04f, 3.4f), _m.SteelDark, Env.Hit);
            for (var x = -7f; x <= 7.01f; x += 1f)
                LocalBox("Stiffener", ramp, new Vector3(x, 0.04f, 0f), new Vector3(0.05f, 0.05f, 3.3f), _m.SteelBrushed,
                    Env.Decor);
            LocalBox("WearStrip", ramp, new Vector3(0f, 0.03f, -1.55f), new Vector3(HalfWidth * 2f - 0.04f, 0.03f, 0.25f),
                _m.SteelBrushed, Env.Decor);

            var upper = Group("UpperDeflector", root);
            upper.position = new Vector3(0f, 3.4f, 29.3f);
            upper.rotation = Quaternion.Euler(25f, 0f, 0f);
            LocalBox("Plate", upper, Vector3.zero, new Vector3(HalfWidth * 2f - 0.02f, 0.04f, 3.0f), _m.SteelDark, Env.Hit);
            for (var x = -7f; x <= 7.01f; x += 1f)
                LocalBox("Stiffener", upper, new Vector3(x, -0.04f, 0f), new Vector3(0.05f, 0.05f, 2.9f), _m.SteelBrushed,
                    Env.Decor);

            var berm = Group("RubberBerm", root);
            berm.position = new Vector3(0f, 0.25f, 27.6f);
            berm.rotation = Quaternion.Euler(-22f, 0f, 0f);
            LocalBox("Granulate", berm, Vector3.zero, new Vector3(HalfWidth * 2f - 0.02f, 0.5f, 1.6f), _m.RubberBerm,
                Env.Hit);

            Box("BermCurb", root, new Vector3(0f, 0.1f, BermStart - 0.1f), new Vector3(HalfWidth * 2f, 0.2f, 0.2f),
                _m.Hazard, Env.Hit);
        }

        // ================================================================ lobby

        private static void BuildLobby(Transform env)
        {
            var root = Group("Lobby", env);

            // ---- Armory counter along the left wall
            var armory = Group("ArmoryCounter", root);
            const float ax = -7.35f;
            Box("Cabinet", armory, new Vector3(ax, 0.46f, -6.6f), new Vector3(0.8f, 0.92f, 4f), _m.PaintedBlueGrey,
                Env.Hit);
            Box("Plinth", armory, new Vector3(ax + 0.03f, 0.05f, -6.6f), new Vector3(0.76f, 0.1f, 3.98f),
                _m.PlasticBlack, Env.Decor);
            Box("Top", armory, new Vector3(ax + 0.02f, 0.94f, -6.6f), new Vector3(0.86f, 0.04f, 4.04f), _m.WoodCounter,
                Env.Hit);
            for (var i = 0; i < 5; i++)
            {
                var z = -8.2f + i * 0.8f;
                Box("Drawer", armory, new Vector3(ax + 0.405f, 0.72f, z), new Vector3(0.012f, 0.26f, 0.74f),
                    _m.PaintedBlueGrey, Env.Decor);
                Box("Door", armory, new Vector3(ax + 0.405f, 0.33f, z), new Vector3(0.012f, 0.44f, 0.74f),
                    _m.PaintedBlueGrey, Env.Decor);
                Box("Pull", armory, new Vector3(ax + 0.42f, 0.78f, z), new Vector3(0.02f, 0.02f, 0.3f),
                    _m.SteelBrushed, Env.Decor);
                Box("Pull", armory, new Vector3(ax + 0.42f, 0.5f, z), new Vector3(0.02f, 0.02f, 0.3f), _m.SteelBrushed,
                    Env.Decor);
            }

            // Ammunition boxes stacked at the end of the counter
            var boxes = new[] { (-4.95f, 0f), (-5.2f, 0f), (-5.08f, 1f) };
            foreach (var (z, level) in boxes)
            {
                var b = Box("AmmoCase", armory, new Vector3(ax - 0.05f, 0.96f + 0.06f + level * 0.12f, z),
                    new Vector3(0.18f, 0.12f, 0.24f), _m.Cardboard, Env.Prop);
                Label("9x19 mm\n50 RDS", b.transform, new Vector3(0.091f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.035f,
                    new Color(0.15f, 0.1f, 0.05f), _fontBold, 0.2f);
            }

            // ---- Lockers and bench along the right wall
            var lockers = Group("Lockers", root);
            for (var i = 0; i < 8; i++)
            {
                var z = -8.55f + i * 0.45f;
                Box("Locker", lockers, new Vector3(HalfWidth - 0.24f, 0.97f, z), new Vector3(0.46f, 1.86f, 0.44f),
                    _m.PaintedBlueGrey, Env.Hit);
                Box("Door", lockers, new Vector3(HalfWidth - 0.475f, 1.0f, z), new Vector3(0.01f, 1.72f, 0.4f),
                    _m.PaintedBlueGrey, Env.Decor);
                for (var v = 0; v < 4; v++)
                    Box("Vent", lockers, new Vector3(HalfWidth - 0.482f, 1.7f - v * 0.04f, z), new Vector3(0.004f, 0.012f, 0.22f),
                        _m.PlasticBlack, Env.Decor);
                Box("Handle", lockers, new Vector3(HalfWidth - 0.49f, 1.05f, z - 0.14f), new Vector3(0.025f, 0.12f, 0.02f),
                    _m.SteelBrushed, Env.Decor);
                Label((i + 1).ToString("00"), lockers, new Vector3(HalfWidth - 0.483f, 1.5f, z),
                    Quaternion.Euler(0f, 90f, 0f), 0.06f, Color.white, _fontBold, 0.2f);
            }

            Box("LockerBase", lockers, new Vector3(HalfWidth - 0.24f, 0.02f, -6.98f), new Vector3(0.46f, 0.04f, 3.6f),
                _m.PlasticBlack, Env.Decor);
            Box("BenchSeat", lockers, new Vector3(HalfWidth - 1.2f, 0.45f, -6.95f), new Vector3(0.36f, 0.04f, 2.6f),
                _m.WoodCounter, Env.Hit);
            foreach (var z in new[] { -8.1f, -5.8f })
                Box("BenchLeg", lockers, new Vector3(HalfWidth - 1.2f, 0.215f, z), new Vector3(0.3f, 0.43f, 0.05f),
                    _m.SteelDark, Env.Hit);

            // ---- Exit door on the back wall
            var exit = Group("ExitDoor", root);
            const float dx = -5.4f;
            Box("Frame", exit, new Vector3(dx, 1.1f, LobbyBack + 0.03f), new Vector3(1.16f, 2.2f, 0.06f), _m.SteelBrushed,
                Env.Decor);
            Box("Door", exit, new Vector3(dx, 1.05f, LobbyBack + 0.06f), new Vector3(1.0f, 2.08f, 0.05f), _m.PaintedGrey,
                Env.Hit);
            Box("PushBar", exit, new Vector3(dx, 1.0f, LobbyBack + 0.12f), new Vector3(0.8f, 0.05f, 0.05f),
                _m.SteelBrushed, Env.Decor);
            Box("KickPlate", exit, new Vector3(dx, 0.15f, LobbyBack + 0.09f), new Vector3(0.96f, 0.25f, 0.005f),
                _m.SteelBrushed, Env.Decor);
            FloorDecal("EntryMat", exit, new Vector3(dx, 0.006f, LobbyBack + 0.8f), 1.4f, 1.0f, _m.RubberMat);

            // ---- Fire extinguisher + first-aid cabinet + clock + bin
            var safety = Group("SafetyEquipment", root);
            const float fx = -3.6f;
            Cylinder("Extinguisher", safety, new Vector3(fx, 0.62f, LobbyBack + 0.11f), 0.08f, 0.48f, _m.PaintedRed,
                Quaternion.identity, Env.Prop);
            Cylinder("Valve", safety, new Vector3(fx, 0.9f, LobbyBack + 0.11f), 0.025f, 0.08f, _m.PlasticBlack,
                Quaternion.identity, Env.Prop);
            Box("Bracket", safety, new Vector3(fx, 0.62f, LobbyBack + 0.02f), new Vector3(0.12f, 0.2f, 0.04f),
                _m.SteelDark, Env.Decor);
            var aid = Box("FirstAid", safety, new Vector3(5.6f, 1.55f, LobbyBack + 0.08f), new Vector3(0.42f, 0.42f, 0.15f),
                _m.PaintedWhite, Env.Decor);
            Box("CrossV", safety, new Vector3(5.6f, 1.55f, LobbyBack + 0.157f), new Vector3(0.06f, 0.2f, 0.004f),
                _m.PaintedRed, Env.Decor);
            Box("CrossH", safety, new Vector3(5.6f, 1.55f, LobbyBack + 0.157f), new Vector3(0.2f, 0.06f, 0.004f),
                _m.PaintedRed, Env.Decor);
            Label("FIRST AID", aid.transform, new Vector3(0f, -0.15f, 0.077f), Quaternion.Euler(0f, 180f, 0f), 0.035f,
                new Color(0.7f, 0.05f, 0.05f), _fontBold, 0.4f);

            var clock = Group("Clock", safety);
            clock.position = new Vector3(1.6f, 2.72f, LobbyBack + 0.03f);
            Cylinder("Rim", clock, clock.position, 0.19f, 0.04f, _m.PlasticBlack, Quaternion.Euler(90f, 0f, 0f));
            Cylinder("Face", clock, clock.position + new Vector3(0f, 0f, 0.021f), 0.17f, 0.004f, _m.PaintedWhite,
                Quaternion.Euler(90f, 0f, 0f));
            Box("HourHand", clock, clock.position + new Vector3(0.03f, 0.04f, 0.027f), new Vector3(0.012f, 0.1f, 0.003f),
                _m.PlasticBlack, Env.Decor).transform.rotation = Quaternion.Euler(0f, 0f, -35f);
            Box("MinuteHand", clock, clock.position + new Vector3(-0.05f, 0.04f, 0.029f), new Vector3(0.008f, 0.14f, 0.003f),
                _m.PlasticBlack, Env.Decor).transform.rotation = Quaternion.Euler(0f, 0f, 50f);

            Cylinder("WasteBin", safety, new Vector3(-6.5f, 0.3f, LobbyBack + 0.35f), 0.2f, 0.6f, _m.SteelBrushed,
                Quaternion.identity, Env.Prop);

            // ---- Check-in desk next to the range door
            var desk = Group("CheckInDesk", root);
            const float kx = 4.3f, kz = -4.35f;
            Box("Body", desk, new Vector3(kx, 0.53f, kz), new Vector3(2.6f, 1.06f, 0.6f), _m.PaintedWhite, Env.Hit);
            Box("Plinth", desk, new Vector3(kx, 0.05f, kz - 0.02f), new Vector3(2.56f, 0.1f, 0.58f), _m.PlasticBlack,
                Env.Decor);
            Box("Top", desk, new Vector3(kx, 1.08f, kz - 0.03f), new Vector3(2.7f, 0.04f, 0.7f), _m.WoodCounter, Env.Hit);
            Box("AccentStripe", desk, new Vector3(kx, 0.72f, kz - 0.301f), new Vector3(2.6f, 0.08f, 0.004f),
                _m.PaintedYellow, Env.Decor);
            Label("CHECK-IN", desk, new Vector3(kx, 0.45f, kz - 0.304f), Quaternion.identity, 0.12f,
                new Color(0.15f, 0.2f, 0.28f), _fontBold, 1.4f);
            var screen = Group("DeskMonitor", desk);
            screen.SetPositionAndRotation(new Vector3(kx + 0.6f, 1.3f, kz + 0.12f), Quaternion.Euler(-8f, 160f, 0f));
            LocalBox("Housing", screen, Vector3.zero, new Vector3(0.5f, 0.3f, 0.03f), _m.PlasticBlack, Env.Decor);
            LocalBox("Panel", screen, new Vector3(0f, 0f, 0.016f), new Vector3(0.47f, 0.27f, 0.002f), _m.ScreenGlass,
                Env.Decor);
            Box("MonitorStand", desk, new Vector3(kx + 0.6f, 1.16f, kz + 0.15f), new Vector3(0.05f, 0.12f, 0.05f),
                _m.PlasticBlack, Env.Decor);
            Box("MonitorFoot", desk, new Vector3(kx + 0.6f, 1.105f, kz + 0.15f), new Vector3(0.2f, 0.01f, 0.15f),
                _m.PlasticBlack, Env.Decor);
            Box("Keyboard", desk, new Vector3(kx + 0.55f, 1.108f, kz - 0.12f), new Vector3(0.42f, 0.015f, 0.14f),
                _m.PlasticBlack, Env.Decor).transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            var clipboard = Box("Clipboard", desk, new Vector3(kx - 0.6f, 1.106f, kz - 0.18f),
                new Vector3(0.23f, 0.01f, 0.32f), _m.Cardboard, Env.Decor);
            clipboard.transform.rotation = Quaternion.Euler(0f, 12f, 0f);
            Box("Sheet", desk, Vector3.zero, new Vector3(0.21f, 0.002f, 0.28f), _m.PaintedWhite, Env.Decor).transform
                .SetPositionAndRotation(clipboard.transform.TransformPoint(new Vector3(0f, 0.006f, -0.01f)),
                    clipboard.transform.rotation);
            Label("SHOOTER LOG\n____  ____  ____\n____  ____  ____\n____  ____  ____", clipboard.transform,
                new Vector3(0f, 0.0075f, -0.01f), Quaternion.Euler(90f, 0f, 0f), 0.016f, new Color(0.1f, 0.1f, 0.12f),
                _fontRegular, 0.2f);
            Cylinder("Pen", desk, new Vector3(kx - 0.43f, 1.107f, kz - 0.2f), 0.004f, 0.14f, _m.PlasticBlack,
                Quaternion.Euler(90f, 30f, 0f));
            // ---- Waiting bench in the shooter area
            Box("WaitBench", root, new Vector3(-5.5f, 0.45f, Partition + 0.45f), new Vector3(2.4f, 0.04f, 0.38f),
                _m.WoodCounter, Env.Hit);
            foreach (var x in new[] { -6.5f, -4.5f })
                Box("WaitBenchLeg", root, new Vector3(x, 0.215f, Partition + 0.45f), new Vector3(0.05f, 0.43f, 0.32f),
                    _m.SteelDark, Env.Hit);
        }

        // ================================================================ details & ambience

        private static void BuildDetails(Transform env)
        {
            var root = Group("Details", env);

            // Brass catch bins under each counter
            foreach (var lx in Lanes)
            {
                Box("BrassBin", root, new Vector3(lx + 0.6f, 0.16f, 0.3f), new Vector3(0.4f, 0.3f, 0.3f), _m.SteelDark,
                    Env.Hit);
                Box("BrassBinRim", root, new Vector3(lx + 0.6f, 0.31f, 0.3f), new Vector3(0.42f, 0.02f, 0.32f),
                    _m.PaintedYellow, Env.Decor);
                Label("BRASS", root, new Vector3(lx + 0.6f, 0.2f, 0.149f), Quaternion.identity, 0.05f, Color.white,
                    _fontBold, 0.38f);
            }

            // Ceiling PA speakers along the range and in the lobby
            foreach (var z in new[] { 8f, 16f, 24f })
            foreach (var side in new[] { -1f, 1f })
            {
                var spk = Group("Speaker", root);
                spk.SetPositionAndRotation(new Vector3(side * 6.6f, 4.55f, z), Quaternion.Euler(30f, side * -90f, 0f));
                LocalBox("Cabinet", spk, Vector3.zero, new Vector3(0.24f, 0.36f, 0.2f), _m.PlasticBlack, Env.Decor);
                LocalBox("Grille", spk, new Vector3(0f, 0f, 0.101f), new Vector3(0.2f, 0.32f, 0.004f), _m.SteelDark,
                    Env.Decor);
            }

            // Electrical conduit with junction boxes along the downrange walls
            foreach (var side in new[] { -1f, 1f })
            {
                Cylinder("Conduit", root, new Vector3(side * (HalfWidth - 0.04f), 3.62f, (FiringLine + BermStart) / 2f),
                    0.02f, BermStart - FiringLine - 1f, _m.SteelBrushed, Quaternion.Euler(90f, 0f, 0f));
                for (var z = 3f; z < BermStart - 1f; z += 8f)
                    Box("JunctionBox", root, new Vector3(side * (HalfWidth - 0.06f), 3.62f, z),
                        new Vector3(0.08f, 0.14f, 0.14f), _m.PaintedGrey, Env.Decor);
            }

            // Lobby: ceiling vents and a cable tray above the firing-line bulkhead
            foreach (var x in new[] { -2.4f, 2.4f })
            {
                Box("SupplyVent", root, new Vector3(x, TileCeiling - 0.006f, -6.3f), new Vector3(0.6f, 0.012f, 0.6f),
                    _m.PaintedWhite, Env.Decor);
                for (var i = 0; i < 5; i++)
                    Box("VentSlot", root, new Vector3(x, TileCeiling - 0.013f, -6.5f + i * 0.1f),
                        new Vector3(0.5f, 0.004f, 0.02f), _m.PlasticBlack, Env.Decor);
            }

            Box("CableTray", root, new Vector3(0f, 4.55f, -0.9f), new Vector3(HalfWidth * 2f - 0.4f, 0.06f, 0.3f),
                _m.SteelBrushed, Env.Decor);
        }

        private static void BuildAmbience(bool force)
        {
            var clip = RangeAudioFactory.GetOrCreateRoomTone(Root + "/Audio/RoomTone.wav", force);
            var go = new GameObject("Ambience_RoomTone");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.12f;
            source.spatialBlend = 0f;
            source.priority = 200;
        }

        // ================================================================ lighting

        private static void BuildLighting()
        {
            var root = new GameObject("Lighting").transform;

            // Lobby LED panels (geometry) + lights
            for (var x = -4.8f; x <= 4.81f; x += 4.8f)
            foreach (var z in new[] { -7.6f, -5.0f })
            {
                Box("LedPanel", root, new Vector3(x, TileCeiling - 0.005f, z), new Vector3(1.2f, 0.012f, 0.6f), _m.LedWhite,
                    Env.Decor);
            }

            foreach (var x in new[] { -4.8f, 0f, 4.8f })
                Box("LedPanel", root, new Vector3(x, TileCeiling - 0.005f, -2.4f), new Vector3(1.2f, 0.012f, 0.6f),
                    _m.LedWhite, Env.Decor);

            var warm = new Color(1f, 0.93f, 0.84f);
            var neutral = new Color(1f, 0.98f, 0.95f);
            // URP lights fall off with the inverse square of distance, so intensities are in "candela-like" units.
            // Lobby / shooter area: wide downlights under the LED panels (points would blow out the ceiling)
            var down = Quaternion.Euler(90f, 0f, 0f);
            foreach (var x in new[] { -4.8f, 0f, 4.8f })
            foreach (var z in new[] { -7.6f, -5.0f })
                SpotLight($"Lobby_{x:0}_{z:0}", root, new Vector3(x, TileCeiling - 0.03f, z), down, warm, 4.5f, 6f, 150f,
                    false);
            foreach (var x in new[] { -4.8f, 0f, 4.8f })
                SpotLight($"ShooterArea_{x:0}", root, new Vector3(x, TileCeiling - 0.03f, -2.4f), down, neutral, 4f, 6f,
                    150f, false);
            PointLight("LobbyFill", root, new Vector3(0f, 1.6f, -6.3f), warm, 1.5f, 9f);

            for (var i = 0; i < Lanes.Length; i++)
            {
                SpotLight($"Booth_{i + 1}", root, new Vector3(Lanes[i], BoothHeader - 0.05f, -0.15f), Quaternion.Euler(75f, 0f, 0f),
                    neutral, 7f, 5f, 120f, i == 2);
                SpotLight($"Target_{i + 1}", root, new Vector3(Lanes[i], 4.2f, 1.2f), Quaternion.Euler(11f, 0f, 0f),
                    neutral, 140f, 34f, 30f, false);
            }

            foreach (var z in new[] { 5f, 11f, 17f, 23f })
            {
                PointLight($"Downrange_L_{z:0}", root, new Vector3(-4.5f, 4.1f, z), neutral, 14f, 11f);
                PointLight($"Downrange_R_{z:0}", root, new Vector3(4.5f, 4.1f, z), neutral, 14f, 11f);
            }

            PointLight("Backstop", root, new Vector3(0f, 3.6f, 26.5f), new Color(1f, 0.9f, 0.8f), 10f, 8f);

            // Reflection probes
            Probe("Probe_Lobby", root, new Vector3(0f, 1.5f, -6.3f), new Vector3(16f, 3f, 5.4f));
            Probe("Probe_FiringLine", root, new Vector3(0f, 1.5f, -1.5f), new Vector3(16f, 3f, 4.2f));
            Probe("Probe_Downrange", root, new Vector3(0f, 2.4f, 15f), new Vector3(16f, 4.8f, 29f));

            // Post-processing
            var volume = new GameObject("PostProcessVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();
        }

        private static VolumeProfile CreateVolumeProfile()
        {
            AssetDatabase.DeleteAsset(VolumeProfilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.65f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(10f);
            color.saturation.Override(-6f);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(-4f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.43f, 0.45f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.34f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.22f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        private static void ConfigurePipeline()
        {
            // The project's pipeline asset shipped with additional (point / spot) lights disabled because the
            // original scene relied on baked lighting. The indoor range uses realtime lights, so enable them.
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            var so = new SerializedObject(urp);
            so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            so.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        // ================================================================ gameplay objects

        private static void BuildGameplay()
        {
            var root = new GameObject("Gameplay").transform;
            var glock = Load("Assets/Prefabs/Weapons/Pistols/Glock17.prefab");
            var doubleBarrel = Load("Assets/Prefabs/Weapons/Shotguns/DoubleBarrelGun.prefab");
            var magazine = Load("Assets/Prefabs/Magazines/MagazineGlock_9x19.prefab");
            var luger = Load("Assets/Prefabs/Cartridges/Cartridge_9x19_Luger.prefab");

            var weapons = Group("Weapons", root);
            for (var i = 0; i < Lanes.Length; i++)
            {
                var lx = Lanes[i];
                PlaceOnSurface(glock, weapons, $"Glock17_Lane{i + 1}", new Vector3(lx - 0.55f, CounterTop, 0.36f),
                    Quaternion.Euler(0f, 0f, 83.6f));
                for (var m = 0; m < 2; m++)
                    PlaceMagazine(magazine, luger, weapons, $"Magazine_Lane{i + 1}_{m + 1}",
                        new Vector3(lx - 0.05f + m * 0.2f, CounterTop, 0.36f));

                PlaceOnSurface(doubleBarrel, weapons, $"DoubleBarrelGun_Lane{i + 1}", new Vector3(lx + 0.65f, CounterTop, 0.3f),
                    Quaternion.Euler(0f, -90f, 0f));
            }

            // Armory: spare pistols and one magazine per ammunition type, each with a label card
            var armory = Group("Armory", root);
            const float ax = -7.3f;
            // Muzzles point at the wall (-X), never into the room.
            PlaceOnSurface(glock, armory, "Glock17_Spare1", new Vector3(ax, 0.96f, -8.1f), Quaternion.Euler(0f, -90f, 83.6f));
            PlaceOnSurface(glock, armory, "Glock17_Spare2", new Vector3(ax, 0.96f, -7.6f), Quaternion.Euler(0f, -90f, 83.6f));
            var ammo = new[] { ("Luger", "9x19 LUGER\nFMJ"), ("PBP", "9x19 PBP\nAP"), ("PS", "9x19 PS\nSTEEL CORE"), ("T", "9x19 T\nTRACER") };
            for (var a = 0; a < ammo.Length; a++)
            {
                var z = -7.0f + a * 0.45f;
                var cartridge = Load($"Assets/Prefabs/Cartridges/Cartridge_9x19_{ammo[a].Item1}.prefab");
                PlaceMagazine(magazine, cartridge, armory, $"Magazine_{ammo[a].Item1}", new Vector3(ax + 0.05f, 0.96f, z),
                    Quaternion.Euler(0f, 90f, 87.6f));
                var card = Box("LabelCard", armory, new Vector3(ax + 0.3f, 0.99f, z), new Vector3(0.02f, 0.05f, 0.16f),
                    _m.PaintedWhite, Env.Decor);
                card.transform.rotation = Quaternion.Euler(0f, 0f, -20f);
                Label(ammo[a].Item2, card.transform, new Vector3(0.011f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.018f,
                    Color.black, _fontBold, 0.15f);
            }
        }

        private static void PlaceOnSurface(GameObject prefab, Transform parent, string name, Vector3 surfacePoint,
            Quaternion rotation)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(surfacePoint, rotation);
            // MeshRenderer/SkinnedMeshRenderer only: a LineRenderer (e.g. a laser sight) reports
            // bounds at whatever its last-drawn points were (world origin before it ever fires),
            // which would otherwise blow out this resting-height calculation.
            var renderers = go.GetComponentsInChildren<Renderer>()
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            if (renderers.Length == 0) return;
            var bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            go.transform.position += Vector3.up * (surfacePoint.y - bounds.min.y + 0.004f);
        }

        private static void PlaceMagazine(GameObject prefab, GameObject cartridge, Transform parent, string name,
            Vector3 surfacePoint, Quaternion? rotation = null)
        {
            PlaceOnSurface(prefab, parent, name, surfacePoint, rotation ?? Quaternion.Euler(0f, 90f, 87.6f));
            var go = parent.Find(name).gameObject;
            // MagazineCartridgeContainer is internal to the game assembly, so go through SerializedObject.
            var container = go.GetComponent("MagazineCartridgeContainer");
            var so = new SerializedObject(container);
            so.FindProperty("_isAutoLoaded").boolValue = true;
            var cartridgeType = container.GetType().Assembly.GetType("Ammunition.CartridgeStructure.Cartridge");
            so.FindProperty("_cartridge").objectReferenceValue = cartridge.GetComponent(cartridgeType);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildPlayer()
        {
            var playerPrefab = Load("Assets/SteamVR/InteractionSystem/Core/Prefabs/Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, -0.55f), Quaternion.identity);
            player.AddComponent<Utilities.SmoothLocomotion>();
            player.AddComponent<Utilities.SmoothTurn>(); // right stick: continuous view turning

            // The Player prefab ships with its own fixed-angle "Snap Turn" child bound to the
            // same stick; remove it so only the continuous SmoothTurn above drives rotation.
            var legacySnapTurn = player.transform.Find("Snap Turn");
            if (legacySnapTurn != null) Object.DestroyImmediate(legacySnapTurn.gameObject);

            foreach (var cam in player.GetComponentsInChildren<Camera>(true))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                cam.nearClipPlane = 0.05f;
            }

            return player;
        }

        // ================================================================ zombie game

        // The zombies roam freely within this area; the player stays outside it (behind
        // the firing line, same as regular target practice) and shoots in.
        private static readonly Vector3 ZombieZoneMin = new Vector3(-HalfWidth + 1.5f, 0f, FiringLine + 4f);
        private static readonly Vector3 ZombieZoneMax = new Vector3(HalfWidth - 1.5f, 0f, BermStart - 2f);

        private static void BuildZombieGame(GameObject player)
        {
            var root = new GameObject("ZombieGame").transform;

            var button = BuildZombieStartButton(root);
            var zombies = BuildZombieSpawns(root);
            var hud = BuildZombieHud(player);

            BuildZombieGameManager(button, hud, zombies);
        }

        private static HoverButton BuildZombieStartButton(Transform parent)
        {
            // Just beside the player's spawn point, inside the same booth (lane dividers sit at x = ±1.2).
            var pedestalPos = new Vector3(0.7f, 0f, -1.0f);
            Box("StartButtonPedestal", parent, pedestalPos + new Vector3(0f, CounterTop / 2f, 0f),
                new Vector3(0.35f, CounterTop, 0.35f), _m.SteelDark, Env.Hit);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "StartButton";
            cap.transform.SetParent(parent, false);
            cap.transform.position = pedestalPos + new Vector3(0f, CounterTop + 0.05f, 0f);
            cap.transform.localScale = new Vector3(0.26f, 0.1f, 0.26f);
            cap.GetComponent<Renderer>().sharedMaterial = _m.PaintedRed;

            cap.AddComponent<Interactable>();
            var hoverButton = cap.AddComponent<HoverButton>();
            hoverButton.movingPart = cap.transform;
            hoverButton.localMoveDistance = new Vector3(0f, -0.025f, 0f);
            hoverButton.engageAtPercent = 0.5f;
            hoverButton.disengageAtPercent = 0.3f;

            Label("START", cap.transform, new Vector3(0f, 0.7f, 0f), Quaternion.identity, 0.16f,
                Color.white, _fontBold, 0.5f);

            return hoverButton;
        }

        private static Zombie[] BuildZombieSpawns(Transform parent)
        {
            // Starting spots only; once released each zombie wanders anywhere in the zone.
            var positions = new[]
            {
                new Vector3(0f, 0f, FiringLine + 7f),
                new Vector3(-2.4f, 0f, FiringLine + 13f),
                new Vector3(2.4f, 0f, FiringLine + 13f),
                new Vector3(0f, 0f, FiringLine + 19f),
                new Vector3(-3.5f, 0f, BermStart - 3f),
                new Vector3(0f, 0f, BermStart - 3f),
                new Vector3(3.5f, 0f, BermStart - 3f),
            };

            var zombies = new List<Zombie>();
            foreach (var position in positions)
                zombies.Add(BuildZombie(parent, position));

            return zombies.ToArray();
        }

        private static Zombie BuildZombie(Transform parent, Vector3 position)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Zombie";
            body.transform.SetParent(parent, false);
            body.transform.position = position + new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = ZombieBodyMaterial();

            var headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGo.name = "Head";
            headGo.transform.SetParent(body.transform, false);
            headGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            headGo.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            headGo.GetComponent<Renderer>().sharedMaterial = ZombieHeadMaterial();

            var canvasGo = new GameObject("HealthBar", typeof(RectTransform));
            canvasGo.transform.SetParent(body.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            canvasGo.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(70f, 14f);

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            bg.rectTransform.sizeDelta = new Vector2(64f, 10f);

            var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
            fill.transform.SetParent(canvasGo.transform, false);
            fill.color = new Color(0.8f, 0.1f, 0.1f);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
            fill.rectTransform.sizeDelta = new Vector2(60f, 8f);

            var healthBar = canvasGo.AddComponent<ZombieHealthBar>();
            var hbSo = new SerializedObject(healthBar);
            hbSo.FindProperty("_fill").objectReferenceValue = fill.rectTransform;
            hbSo.FindProperty("_fullWidth").floatValue = 60f;
            hbSo.ApplyModifiedPropertiesWithoutUndo();

            var zombie = body.AddComponent<Zombie>();
            var zSo = new SerializedObject(zombie);
            zSo.FindProperty("_healthBar").objectReferenceValue = healthBar;
            var flashArray = zSo.FindProperty("_hitFlashRenderers");
            flashArray.arraySize = 2;
            flashArray.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            flashArray.GetArrayElementAtIndex(1).objectReferenceValue = headGo.GetComponent<Renderer>();
            zSo.ApplyModifiedPropertiesWithoutUndo();
            zombie.SetWanderBounds(ZombieZoneMin, ZombieZoneMax);

            var relay = headGo.AddComponent<ZombieHeadHitRelay>();
            var relaySo = new SerializedObject(relay);
            relaySo.FindProperty("_zombie").objectReferenceValue = zombie;
            relaySo.ApplyModifiedPropertiesWithoutUndo();

            return zombie;
        }

        private static Material _zombieBodyMat;
        private static Material _zombieHeadMat;

        private static Material ZombieBodyMaterial()
        {
            if (_zombieBodyMat == null)
                _zombieBodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = new Color(0.2f, 0.35f, 0.18f) };
            return _zombieBodyMat;
        }

        private static Material ZombieHeadMaterial()
        {
            if (_zombieHeadMat == null)
                _zombieHeadMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = new Color(0.15f, 0.28f, 0.14f) };
            return _zombieHeadMat;
        }

        private static GameHud BuildZombieHud(GameObject player)
        {
            var head = player.GetComponent<Player>().hmdTransform;

            var canvasGo = new GameObject("GameHud", typeof(RectTransform));
            canvasGo.transform.SetParent(head, false);
            canvasGo.transform.localPosition = new Vector3(0f, -0.2f, 1.0f);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * 0.001f;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(400f, 200f);

            var messageGo = new GameObject("Message", typeof(RectTransform));
            messageGo.transform.SetParent(canvasGo.transform, false);
            var message = messageGo.AddComponent<TextMeshProUGUI>();
            message.alignment = TextAlignmentOptions.Center;
            message.fontSize = 28f;
            message.color = Color.white;
            if (_fontBold != null) message.font = _fontBold;
            ((RectTransform)messageGo.transform).sizeDelta = new Vector2(380f, 100f);
            ((RectTransform)messageGo.transform).anchoredPosition = new Vector2(0f, 20f);

            var hud = canvasGo.AddComponent<GameHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("_messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        private static void BuildZombieGameManager(HoverButton button, GameHud hud, Zombie[] zombies)
        {
            var go = new GameObject("ZombieGameManager");
            var manager = go.AddComponent<ZombieGameManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("_startButton").objectReferenceValue = button;
            so.FindProperty("_hud").objectReferenceValue = hud;

            var zombiesProp = so.FindProperty("_zombies");
            zombiesProp.arraySize = zombies.Length;
            for (var i = 0; i < zombies.Length; i++)
                zombiesProp.GetArrayElementAtIndex(i).objectReferenceValue = zombies[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ helpers

        [System.Flags]
        private enum Env
        {
            None = 0,
            Collider = 1,
            Hit = Collider | 2,    // collider + bullet impact particles
            Static = 4,
            Movable = 8,
            Decor = Static,         // static, no collider
            Prop = Collider | Movable // collider, not marked static
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 worldCentre, Vector3 size, Material mat,
            Env flags)
        {
            var isStructural = flags.HasFlag(Env.Collider) && !flags.HasFlag(Env.Movable);
            var go = MeshObject(name, parent, _meshes.Box(size), mat, isStructural ? flags | Env.Static : flags);
            go.transform.position = worldCentre;
            if (flags.HasFlag(Env.Collider)) go.AddComponent<BoxCollider>().size = size;
            if ((flags & Env.Hit) == Env.Hit) _hitParticle.Attach(go);
            return go;
        }

        private static GameObject LocalBox(string name, Transform parent, Vector3 localCentre, Vector3 size, Material mat,
            Env flags)
        {
            var go = Box(name, parent, Vector3.zero, size, mat, flags);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 worldCentre, float radius, float height,
            Material mat, Quaternion rotation, Env flags = Env.Decor)
        {
            var go = MeshObject(name, parent, _meshes.Cylinder(radius, height), mat, flags);
            go.transform.SetPositionAndRotation(worldCentre, rotation);
            if (flags.HasFlag(Env.Collider))
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = radius;
                c.height = height;
            }

            return go;
        }

        private static void FloorDecal(string name, Transform parent, Vector3 centre, float width, float depth, Material mat)
        {
            var go = MeshObject(name, parent, _meshes.FloorQuad(width, depth), mat, Env.Decor);
            go.transform.position = centre;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Env flags)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = EnvironmentLayer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (mat.renderQueue >= (int)RenderQueue.Transparent) r.shadowCastingMode = ShadowCastingMode.Off;
            if (flags.HasFlag(Env.Static))
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        private static TextMeshPro Label(string text, Transform parent, Vector3 position, Quaternion rotation, float height,
            Color color, TMP_FontAsset font, float width, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text_" + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = height * 10f;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(width, height * 1.4f * (text.Count(c => c == '\n') + 1));
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            if (parent.lossyScale != Vector3.one)
                go.transform.localScale = new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y,
                    1f / parent.lossyScale.z);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return tmp;
        }

        private static void FloorText(string text, Transform parent, Vector3 position, float height, Color color)
        {
            var label = Label(text, parent, Vector3.zero, Quaternion.identity, height, color, _fontBold, height * 4f);
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        }

        private static void PointLight(string name, Transform parent, Vector3 position, Color color, float intensity,
            float range)
        {
            var light = NewLight(name, parent, position, Quaternion.identity, LightType.Point, color, intensity, range);
            light.shadows = LightShadows.None;
        }

        private static void SpotLight(string name, Transform parent, Vector3 position, Quaternion rotation, Color color,
            float intensity, float range, float angle, bool shadows)
        {
            var light = NewLight(name, parent, position, rotation, LightType.Spot, color, intensity, range);
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static Light NewLight(string name, Transform parent, Vector3 position, Quaternion rotation, LightType type,
            Color color, float intensity, float range)
        {
            var go = new GameObject("Light_" + name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        private static void Probe(string name, Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = size;
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        }

        private static Material Instantiate(Material source, string name, Color tint)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var mat = new Material(source) { name = name };
            mat.SetColor("_BaseColor", tint);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) throw new FileNotFoundException("Missing prefab " + path);
            return go;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Attaches StaticHitDetection (bullet impact particles) using the project's impact prefab.</summary>
        private sealed class ParticleComponentRef
        {
            private readonly Object _particle;

            public ParticleComponentRef(string prefabPath)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                _particle = prefab != null ? prefab.GetComponent("ParticleComponent") : null;
            }

            public void Attach(GameObject go)
            {
                if (_particle == null) return;
                var hit = go.AddComponent<StaticHitDetection>();
                var so = new SerializedObject(hit);
                so.FindProperty("_particle").objectReferenceValue = _particle;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
