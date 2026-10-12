using System.Collections.Generic;
using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.CameraRig;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Player;
using CoolSchool.Tag;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the tag-in-the-playground prototype scene in one go: ring of playground equipment,
    /// ground, fence, the knights arena, NavMesh, touch UI, the player, computer kids, camera, the activity
    /// manager with its tag game and knights game, and the battle effects.
    /// Re-running it replaces the scene and the generated assets.
    /// </summary>
    public static class PlaygroundPrototypeBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string ScenePath = RootFolder + "/Scenes/Playground.unity";
        const string NavMeshPath = RootFolder + "/Scenes/Playground_NavMesh.asset";
        const string ControllerPath = RootFolder + "/Animation/Kid.controller";
        const string MarkerMaterialPath = RootFolder + "/Materials/ItMarker.mat";
        const string KidsLayerName = "Kids";

        const string KidsPack = "PolygonKids";
        const string TownPack = "PolygonTown";

        /// <summary>A prefab found by file name; Pack is the folder name preferred when several packs share the name.</summary>
        internal readonly struct PrefabRef
        {
            public readonly string Name;
            public readonly string Pack;

            public PrefabRef(string name, string pack)
            {
                Name = name;
                Pack = pack;
            }
        }

        internal static PrefabRef Kids(string name) =>new PrefabRef(name, KidsPack);
        static PrefabRef Town(string name) => new PrefabRef(name, TownPack);

        static readonly PrefabRef[] EquipmentPrefabs =
        {
            Kids("SM_Prop_Playground_Slide_01"),
            Town("SM_Prop_Playground_Roundabout_01"),
            Kids("SM_Prop_Playground_Swings_01"),
            Kids("SM_Prop_Playground_Tunnel_01"),
            Kids("SM_Prop_Playground_Monkey_Bars_01"),
            Kids("SM_Prop_Playground_Rocker_01"),
            Kids("SM_Prop_Playground_Ship_01"),
            Kids("SM_Prop_Playground_Seesaw_01"),
            Town("SM_Prop_Playground_Fort_01"),
            Kids("SM_Prop_Playground_Rocker_02"),
            Kids("SM_Prop_Playground_Tyrewall_01"),
            Kids("SM_Prop_Playground_Track_Ride_01"),
        };

        static readonly PrefabRef GrassPrefab = Kids("SM_Env_Ground_Tile_Grass");
        static readonly PrefabRef RubberPrefab = Kids("SM_Env_Ground_Tile_Rubber_01");
        static readonly PrefabRef FencePrefab = Kids("SM_Env_Park_Fence_01");
        static readonly PrefabRef TreePrefab = Kids("SM_Env_Tree_01");
        static readonly PrefabRef TreeLargePrefab = Kids("SM_Env_Tree_Large_01");
        static readonly PrefabRef BushPrefab = Kids("SM_Env_Bush_01");
        static readonly PrefabRef BenchPrefab = Kids("SM_Env_Park_Seat_01");
        static readonly PrefabRef BinPrefab = Kids("SM_Env_Park_Rubbish_Bin_01");

        static readonly PrefabRef PlayerKidPrefab = Kids("SM_Chr_Kid_Schoolboy_01");

        static readonly PrefabRef[] ComputerKidPrefabs =
        {
            Kids("SM_Chr_Kid_Hoodie_02"),
            Kids("SM_Chr_Kid_Schoolgirl_01"),
            Kids("SM_Chr_Kid_Superhero_01"),
            Kids("SM_Chr_Kid_Pirate_01"),
            Kids("SM_Chr_Kid_Onesie_Dino_01"),
        };

        const float MinRingRadius = 10f;
        const float EquipmentGap = 1.5f;

        /// <summary>How far the playground rectangle is extended toward the arena to make room for the knights arena.</summary>
        const float ArenaDepth = 30f;

        /// <summary>How far the playground rectangle is extended toward the alien landing field.</summary>
        const float AlienFieldDepth = 24f;

        // The camera always looks north (+Z), so "up" on screen is north and the player walks north to the school.

        /// <summary>Which way the knights arena lies from the playground (east, to the right on screen).</summary>
        internal static readonly Vector3 ArenaDirection = Vector3.right;

        /// <summary>Which way the alien landing field lies from the playground (west, to the left on screen).</summary>
        internal static readonly Vector3 AlienFieldDirection = Vector3.left;

        /// <summary>Which way the school lies from the playground (north, straight ahead of the camera).</summary>
        internal static readonly Vector3 SchoolDirection = Vector3.forward;

        /// <summary>The player starts this far inside the fence on the side opposite the school, facing the school.</summary>
        const float SpawnFromFence = 4f;

        // Weapon placement is computed at runtime from the rig and the weapon's bounds (WeaponFit). These are only an extra
        // tweak on top of that, in the weapon's own axes; leave them at zero.
        internal static readonly Vector3 SwordLocalPosition = Vector3.zero;
        internal static readonly Vector3 SwordLocalEuler = Vector3.zero;
        internal static readonly Vector3 ShieldLocalPosition = Vector3.zero;
        internal static readonly Vector3 ShieldLocalEuler = Vector3.zero;
        const float WeaponScale = 1f;

        const string DragonControllerPath = RootFolder + "/Animation/Dragon.controller";
        const string ClubControllerPath = RootFolder + "/Animation/KidClub.overrideController";

        static readonly PrefabRef PlayerSword = Kids("SM_Wep_Sword_Foam_01");
        static readonly PrefabRef PlayerShield = Kids("SM_Wep_Shield_01");
        static readonly PrefabRef AllySword = Kids("SM_Wep_Sword_Wood_01");
        static readonly PrefabRef AllyShield = Kids("SM_Wep_Shield_02");

        /// <summary>How many of the classmates become knight allies in the knights game.</summary>
        const int AllyCount = 3;

        /// <summary>Numbers the later build steps need from the environment.</summary>
        internal sealed class EnvironmentInfo
        {
            public float RingRadius = MinRingRadius;
            public float MaxItemRadius = 2f;
            /// <summary>Half the side of the square playground (the fence's distance from the origin).</summary>
            public float FenceHalf;

            /// <summary>Where the arena starts and ends along ArenaDirection, as distances from the origin.</summary>
            public float ArenaStart;
            public float ArenaEnd;

            /// <summary>The fence rectangle: the playground square plus the arena.</summary>
            public float MinX, MaxX, MinZ, MaxZ;

            /// <summary>Where the player starts, and the way the player faces (toward the school).</summary>
            public Vector3 PlayerSpawn;
            public Vector3 PlayerFacing = Vector3.forward;

            public KnightsArenaBuilder.ArenaLayout Arena;
            public AlienFieldBuilder.FieldLayout AlienField;
            public IceCreamShopBuilder.StallLayout Shop;
            public PirateCoveBuilder.CoveLayout PirateCove;
            public SoapboxStreetBuilder.StreetLayout RaceStreet;
            public SchoolResult School;
            public ToyRoomLayout ToyRoom;
        }

        /// <summary>Ground (x, z rectangles) that benches, bins and trees keep clear of, e.g. the ice cream stall. Cleared at the start of each build.</summary>
        internal static readonly List<Rect> ReservedAreas = new List<Rect>();

        static bool IsReserved(Vector3 point)
        {
            var flat = new Vector2(point.x, point.z);
            foreach (Rect area in ReservedAreas)
                if (area.Contains(flat)) return true;
            return false;
        }

        [MenuItem("CoolSchool/Build Playground Prototype")]
        static void Build()
        {
            RunBuild();
        }

        /// <summary>
        /// The whole build, shared by the menu item and CommandLineBuild. Returns false when the user cancelled.
        /// In batch mode no dialog is shown (nobody could click it): the new scene simply replaces the open one.
        /// </summary>
        internal static bool RunBuild()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            try
            {
                ShowProgress("Preparing folders", 0.02f);
                EnsureFolder(RootFolder, "Scenes");
                EnsureFolder(RootFolder, "Animation");
                EnsureFolder(RootFolder, "Materials");

                ShowProgress("Upgrading Polygon Kids materials", 0.05f);
                int upgraded = KidsMaterialUpgrader.UpgradeAll();

                int kidsLayer = EnsureLayer(KidsLayerName);

                ShowProgress("Building kid animator", 0.1f);
                var missingClips = new List<string>();
                AnimatorController controller = KidAnimatorBuilder.CreateOrReplace(ControllerPath, missingClips);
                AnimatorController dragonController = DragonAnimatorBuilder.CreateOrReplace(DragonControllerPath, missingClips);
                AnimatorOverrideController clubController = KidAnimatorBuilder.CreateClubOverride(ClubControllerPath, controller, missingClips);
                Material markerMaterial = CreateMarkerMaterial();

                ShowProgress("Building battle effects", 0.13f);
                BattleEffectsBuilder.Result effects = BattleEffectsBuilder.CreateAll();

                ShowProgress("Creating scene", 0.15f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                SetUpLighting();

                ShowProgress("Placing playground and castle", 0.2f);
                EnvironmentInfo environment = BuildEnvironment();

                ShowProgress("Baking NavMesh", 0.7f);
                BuildNavMesh();

                ShowProgress("Creating touch UI", 0.8f);
                FloatingJoystick joystick;
                ActionButton jumpButton;
                CanvasGroup imaginationOverlay;
                BuildTouchUI(out joystick, out jumpButton, out imaginationOverlay);

                ShowProgress("Creating kids", 0.85f);
                GameObject player = CreatePlayer(controller, kidsLayer, joystick, jumpButton, environment);
                List<GameObject> allyKids = CreateComputerKids(controller, kidsLayer, environment.RingRadius);

                ShowProgress("Setting up camera and games", 0.92f);
                SetUpCamera(player.transform, kidsLayer);
                BuildActivities(player.transform, markerMaterial);
                BuildBattleEffects(effects);
                ImaginationEffect imagination = BuildImaginationEffect(imaginationOverlay, environment.Arena);

                ShowProgress("Building knights arena", 0.95f);
                KnightsArenaBuilder.ArenaResult arena = KnightsArenaBuilder.Build(environment.Arena, new KnightsArenaBuilder.ArenaInputs
                {
                    KidController = controller,
                    OpponentController = clubController,
                    DragonController = dragonController,
                    KidsLayer = kidsLayer,
                    Player = player.GetComponent<PlayerController>(),
                    PlayerInput = player.GetComponent<PlayerBattleInput>(),
                    ActionButton = jumpButton,
                    SwordIcon = FindSwordIcon(),
                    Imagination = imagination,
                    Allies = allyKids.Select(k => k.GetComponent<KnightAllyAI>()).ToArray(),
                    HeroWeaponHolders = new[] { player }.Concat(allyKids).Select(k => k.GetComponent<WeaponHolder>()).ToArray(),
                    FireConfettiPrefab = effects.FireConfetti,
                });

                VoiceSetupBuilder.Setup();
                SoundSetupBuilder.Setup();
                LessonSetupBuilder.Setup();

                // After the lesson (the counting uses its number cards) and the voice (the inviter), before the rewards (which listen to it).
                ShowProgress("Building alien field", 0.96f);
                AlienFieldBuilder.Build(environment.AlienField, new AlienFieldBuilder.AlienInputs
                {
                    KidController = controller,
                    KidsLayer = kidsLayer,
                    Player = player.GetComponent<PlayerController>(),
                    ActionButton = jumpButton,
                    Imagination = imagination,
                });

                ShowProgress("Building ice cream shop", 0.97f);
                IceCreamShopBuilder.Build(environment.Shop, new IceCreamShopBuilder.ShopInputs
                {
                    Player = player.GetComponent<PlayerController>(),
                    Imagination = imagination,
                });

                ShowProgress("Building pirate cove", 0.975f);
                PirateCoveBuilder.Build(environment.PirateCove, new PirateCoveBuilder.PirateInputs
                {
                    KidController = controller,
                    KidsLayer = kidsLayer,
                    Player = player.GetComponent<PlayerController>(),
                    ActionButton = jumpButton,
                    Imagination = imagination,
                });

                ShowProgress("Building soapbox street", 0.977f);
                SoapboxStreetBuilder.Build(environment.RaceStreet, new SoapboxStreetBuilder.RaceInputs
                {
                    KidController = controller,
                    TeacherController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RootFolder + "/Animation/Teacher.controller"),
                    KidsLayer = kidsLayer,
                    Player = player.GetComponent<PlayerController>(),
                    Joystick = joystick,
                    ActionButton = jumpButton,
                });

                // The parent gear goes on the touch UI before the rewards add their popup, so the popup stays on top of it.
                ParentAreaBuilder.Build();
                QuitButtonBuilder.Build();

                RewardsSetupBuilder.Setup(player);

                // After the rewards: the wardrobe needs the catalog, the buddy and the present popup (the picture menu sits below it).
                ToyRoomBuilder.Build(environment.ToyRoom, new ToyRoomInputs { Player = player.GetComponent<PlayerController>() });

                ColorGradingSetup.ApplyToProfile();

                ShowProgress("Saving scene", 0.98f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings(ScenePath);
                ApplyTabletSettings();
                AssetDatabase.SaveAssets();

                ShowSummary(upgraded, environment.RingRadius, controller, missingClips, arena);
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ----- Project setup -------------------------------------------------------------------

        static void ShowProgress(string text, float fraction)
        {
            EditorUtility.DisplayProgressBar("CoolSchool playground", text, fraction);
        }

        internal static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>Makes sure a user layer with this name exists. Returns its index, or -1 if there is no free slot.</summary>
        static int EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("CoolSchool: could not open ProjectSettings/TagManager.asset.");
                return -1;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            int firstEmpty = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                string existing = layers.GetArrayElementAtIndex(i).stringValue;
                if (existing == layerName) return i;
                if (firstEmpty < 0 && string.IsNullOrEmpty(existing)) firstEmpty = i;
            }

            if (firstEmpty < 0)
            {
                Debug.LogError($"CoolSchool: no free user layer for '{layerName}'.");
                return -1;
            }

            layers.GetArrayElementAtIndex(firstEmpty).stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return firstEmpty;
        }

        /// <summary>Gold, softly glowing material for the "it" marker.</summary>
        static Material CreateMarkerMaterial()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var gold = new Color(1f, 0.8f, 0.1f, 1f);
            var material = new Material(lit) { name = "ItMarker" };
            material.SetColor("_BaseColor", gold);
            material.SetFloat("_Smoothness", 0.6f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(gold.r * 1.5f, gold.g * 1.5f, gold.b * 1.5f, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            AssetDatabase.DeleteAsset(MarkerMaterialPath);
            AssetDatabase.CreateAsset(material, MarkerMaterialPath);
            return AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
        }

        static void SetUpLighting()
        {
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun == null) return;

            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;
        }

        // ----- Environment ---------------------------------------------------------------------

        static EnvironmentInfo BuildEnvironment()
        {
            var info = new EnvironmentInfo();
            ReservedAreas.Clear();
            Transform root = new GameObject("Environment").transform;
            Transform equipmentGroup = NewGroup("Equipment", root);
            Transform groundGroup = NewGroup("Ground", root);
            Transform fenceGroup = NewGroup("Fence", root);
            Transform propsGroup = NewGroup("Props", root);
            Transform greeneryGroup = NewGroup("Greenery", root);

            PlaceEquipmentRing(equipmentGroup, info);

            // The playground is a square around the origin. The arena extends it toward ArenaDirection (east),
            // and the school stands beyond the fence on the SchoolDirection side (north).
            info.FenceHalf = info.RingRadius + info.MaxItemRadius + 8f;
            info.ArenaStart = info.FenceHalf;
            info.ArenaEnd = info.FenceHalf + ArenaDepth;
            float rubberHalf = info.RingRadius + info.MaxItemRadius + 1f;
            float grassMargin = 4f;
            float benchDistance = info.RingRadius + info.MaxItemRadius + 3f;

            info.MinX = info.MinZ = -info.FenceHalf;
            info.MaxX = info.MaxZ = info.FenceHalf;
            ExtendRectangle(info, ArenaDirection, ArenaDepth);
            ExtendRectangle(info, AlienFieldDirection, AlienFieldDepth);

            // The player starts on the side opposite the school, facing it, and walks up through the playground.
            info.PlayerFacing = SchoolDirection;
            info.PlayerSpawn = -SchoolDirection * (info.FenceHalf - SpawnFromFence);

            // The school sits just beyond the fence gap; it is static geometry too, so it goes in before the NavMesh bake.
            // Its door faces back toward the playground, and so toward the follow camera.
            info.School = SchoolBuilder.Build(root, SchoolDirection * info.FenceHalf, -SchoolDirection);

            // The Toy Room in the north-east corner (door facing south) has a floor like the school, so it goes in before the grass is laid
            // (the grass leaves its floor out) and before the NavMesh bake. It reserves its ground, so the benches and trees keep clear.
            // Its wardrobe and picture menu are added later, after the rewards exist (ToyRoomBuilder.Build).
            info.ToyRoom = ToyRoomBuilder.BuildRoom(root, info);

            // Grass under everything: the fenced area plus a margin, and the school's footprint plus a margin.
            var grass = new Bounds(new Vector3((info.MinX + info.MaxX) * 0.5f, 0f, (info.MinZ + info.MaxZ) * 0.5f),
                new Vector3(info.MaxX - info.MinX, 0f, info.MaxZ - info.MinZ));
            grass.Expand(new Vector3(grassMargin * 2f, 0f, grassMargin * 2f));
            if (info.School.footprint.size.sqrMagnitude > 0f)
            {
                Bounds schoolArea = info.School.footprint;
                schoolArea.Expand(new Vector3(grassMargin * 2f, 0f, grassMargin * 2f));
                grass.Encapsulate(new Vector3(schoolArea.min.x, 0f, schoolArea.min.z));
                grass.Encapsulate(new Vector3(schoolArea.max.x, 0f, schoolArea.max.z));
            }

            // Grass tufts stand about 10 cm above the grass surface, so grass must not lie under any floor: the rubber (1 cm above
            // the grass) and the classroom floor (2 cm). The grass grid leaves their rectangles out, and the tiles that straddle an
            // edge are cut down to the part outside, stretched to fit, so the lawn meets the floors without a gap.
            Rect rubberArea = BuildTileGrid(LoadPrefab(RubberPrefab), groundGroup, -rubberHalf, rubberHalf, -rubberHalf, rubberHalf, 0.01f, null);
            var floors = new List<Rect> { rubberArea };
            if (info.School.floorArea.size.sqrMagnitude > 0f)
            {
                Bounds floor = info.School.floorArea;
                floors.Add(Rect.MinMaxRect(floor.min.x, floor.min.z, floor.max.x, floor.max.z));
            }
            if (info.ToyRoom != null && info.ToyRoom.FloorArea.size.sqrMagnitude > 0f)
            {
                Bounds toyFloor = info.ToyRoom.FloorArea;
                floors.Add(Rect.MinMaxRect(toyFloor.min.x, toyFloor.min.z, toyFloor.max.x, toyFloor.max.z));
            }
            BuildTileGrid(LoadPrefab(GrassPrefab), groundGroup, grass.min.x, grass.max.x, grass.min.z, grass.max.z, 0f, floors);

            BuildFence(LoadPrefab(FencePrefab), fenceGroup, info, SchoolDirection, SchoolBuilder.FenceGapWidth);

            // The ice cream stall in the north-west corner. It reserves its ground, so the benches and trees below keep clear.
            info.Shop = IceCreamShopBuilder.BuildStall(root, info);

            // The castle is static geometry, so it goes in before the NavMesh bake. Like the stall it reserves its ground
            // (footprint and the approach to the gate), so it goes in before the benches and trees.
            info.Arena = KnightsArenaBuilder.BuildCastle(root, ArenaDirection, info.ArenaStart, info.ArenaEnd, info.MaxZ);

            // The pirate cove lies north of the arena (static geometry, reserves its ground, cuts a gap in the main north fence).
            info.PirateCove = PirateCoveBuilder.BuildCove(root, info);

            // The soapbox street lies north of the playground, west of the school (static geometry, reserves its ground, cuts a gap in the main north fence).
            info.RaceStreet = SoapboxStreetBuilder.BuildStreet(root, info);

            // Nothing may stand on the player's start.
            var occupied = new List<Vector3> { info.PlayerSpawn };
            PlaceBenchesAndBins(propsGroup, benchDistance, occupied);
            ScatterGreenery(greeneryGroup, benchDistance, info, occupied);

            // The rocket and the tree are static too. The field mirrors the arena on the other side (west).
            info.AlienField = AlienFieldBuilder.BuildField(root, AlienFieldDirection, info.FenceHalf, info.FenceHalf + AlienFieldDepth);

            Debug.Log($"CoolSchool playground: fence x {info.MinX:0.0}..{info.MaxX:0.0}, z {info.MinZ:0.0}..{info.MaxZ:0.0} "
                + $"(half {info.FenceHalf:0.0} m, ring {info.RingRadius:0.0} m); arena along {ArenaDirection.ToString("0.0")} from "
                + $"{info.ArenaStart:0.0} to {info.ArenaEnd:0.0} m; fence gap toward the school at {(SchoolDirection * info.FenceHalf).ToString("0.0")}; "
                + $"player spawn {info.PlayerSpawn.ToString("0.0")} facing {info.PlayerFacing.ToString("0.0")}.");

            return info;
        }

        /// <summary>Pushes the side of the fence rectangle that faces the direction (a cardinal one) out by depth.</summary>
        static void ExtendRectangle(EnvironmentInfo info, Vector3 direction, float depth)
        {
            if (direction.x > 0.5f) info.MaxX += depth;
            else if (direction.x < -0.5f) info.MinX -= depth;
            else if (direction.z > 0.5f) info.MaxZ += depth;
            else if (direction.z < -0.5f) info.MinZ -= depth;
        }

        internal static Transform NewGroup(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        internal static GameObject LoadPrefab(PrefabRef reference)
        {
            string path = FindPrefabPath(reference);
            var prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab == null) Debug.LogWarning($"CoolSchool: prefab not found, skipping: {reference.Name}");
            return prefab;
        }

        /// <summary>
        /// Finds a prefab by exact file name anywhere in the project. If several match,
        /// prefers the one inside the folder named by the reference's pack.
        /// </summary>
        static string FindPrefabPath(PrefabRef reference)
        {
            string fallback = null;
            foreach (string guid in AssetDatabase.FindAssets(reference.Name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != reference.Name) continue;

                if (path.Contains(reference.Pack)) return path;
                if (fallback == null) fallback = path;
            }
            return fallback;
        }

        /// <summary>Spawns a prefab instance at the origin with no rotation.</summary>
        internal static GameObject Spawn(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return instance;
        }

        /// <summary>Equipment around the edge of an open field, each piece facing the middle.</summary>
        static void PlaceEquipmentRing(Transform parent, EnvironmentInfo info)
        {
            var items = new List<GameObject>();
            var radii = new List<float>();
            foreach (PrefabRef reference in EquipmentPrefabs)
            {
                GameObject prefab = LoadPrefab(reference);
                if (prefab == null) continue;

                GameObject item = Spawn(prefab, parent);
                items.Add(item);
                radii.Add(FootprintRadius(item));
            }

            if (items.Count == 0) return;

            info.MaxItemRadius = radii.Max();
            float arcTotal = radii.Sum(r => 2f * r + EquipmentGap);
            float ring = Mathf.Max(MinRingRadius, arcTotal / (2f * Mathf.PI));
            info.RingRadius = ring;

            // A bigger-than-needed ring just spreads the leftover space evenly between items.
            float extraAngle = Mathf.Max(0f, 2f * Mathf.PI - arcTotal / ring) / items.Count;
            float angle = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                float step = (2f * radii[i] + EquipmentGap) / ring + extraAngle;
                float middle = angle + step * 0.5f;
                angle += step;

                var point = new Vector3(Mathf.Sin(middle) * ring, 0f, Mathf.Cos(middle) * ring);
                items[i].transform.rotation = Quaternion.LookRotation(-point.normalized, Vector3.up);
                AlignToBounds(items[i], point.x, point.z, 0f, false);
                FinishEnvironmentObject(items[i]);
            }
        }

        /// <summary>
        /// Lays a grid of tiles covering x in xMin..xMax and z in zMin..zMax with their flat surfaces at topY.
        /// Nothing is laid inside the avoid rectangles (x, z in world space): a tile that lies wholly inside one is left out, and a
        /// tile that straddles an edge is replaced by tiles cut down to the part outside, stretched to fit it exactly. (Lumpy tiles
        /// such as grass poke through a floor that lies only a few centimetres above them.)
        /// Returns the rectangle (x, z) that the grid covers.
        /// </summary>
        static Rect BuildTileGrid(GameObject prefab, Transform parent, float xMin, float xMax, float zMin, float zMax, float topY, IList<Rect> avoid)
        {
            if (prefab == null) return new Rect();

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds))
            {
                Debug.LogWarning($"CoolSchool: tile has no renderers, skipping: {prefab.name}");
                Object.DestroyImmediate(probe);
                return new Rect();
            }
            Vector3 baseScale = probe.transform.localScale;

            // The walking surface is not the bounds top: grass tufts and bumps stick up above it. Aligning the bounds top to
            // topY would leave the real surface lower, and every object placed "on the ground" would float above it.
            GroundSurface.Measurement surface = GroundSurface.Measure(probe);
            Debug.Log("CoolSchool ground: " + GroundSurface.Describe(prefab.name, surface));
            float bumpHeight = surface.Valid ? Mathf.Max(0f, surface.BumpHeight) : 0f;
            Object.DestroyImmediate(probe);

            float sizeX = GuardTileSize(probeBounds.size.x, out float scaleX);
            float sizeZ = GuardTileSize(probeBounds.size.z, out float scaleZ);
            if (scaleX != 1f || scaleZ != 1f)
                Debug.LogWarning($"CoolSchool: tile {prefab.name} is tiny ({probeBounds.size}); scaling it up.");

            int countX = Mathf.Clamp(Mathf.CeilToInt((xMax - xMin) / sizeX), 1, 80);
            int countZ = Mathf.Clamp(Mathf.CeilToInt((zMax - zMin) / sizeZ), 1, 80);
            float coverX = countX * sizeX * 0.5f;
            float coverZ = countZ * sizeZ * 0.5f;
            float centreX = (xMin + xMax) * 0.5f;
            float centreZ = (zMin + zMax) * 0.5f;

            int cut = 0;
            int skipped = 0;
            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    float x = centreX - coverX + sizeX * (ix + 0.5f);
                    float z = centreZ - coverZ + sizeZ * (iz + 0.5f);

                    var whole = Rect.MinMaxRect(x - sizeX * 0.5f, z - sizeZ * 0.5f, x + sizeX * 0.5f, z + sizeZ * 0.5f);
                    List<Rect> pieces = SubtractRects(whole, avoid);
                    if (pieces.Count == 1 && pieces[0] == whole)
                    {
                        PlaceGroundTile(prefab, parent, baseScale, scaleX, scaleZ, x, z, topY, bumpHeight, surface);
                        continue;
                    }

                    if (pieces.Count == 0) skipped++;
                    else cut++;
                    foreach (Rect piece in pieces)
                    {
                        // The tile is stretched (x and z only; the tufts keep their height) to exactly the width and depth of the piece.
                        PlaceGroundTile(prefab, parent, baseScale, scaleX * piece.width / sizeX, scaleZ * piece.height / sizeZ,
                            piece.center.x, piece.center.y, topY, bumpHeight, surface);
                    }
                }
            }

            if (avoid != null && avoid.Count > 0)
                Debug.Log($"CoolSchool ground: {prefab.name} grid left out {skipped} tiles under floors and cut {cut} tiles at their edges.");

            return Rect.MinMaxRect(centreX - coverX, centreZ - coverZ, centreX + coverX, centreZ + coverZ);
        }

        /// <summary>One ground tile of a grid, centred on (x, z), with its flat surface at topY. scaleX and scaleZ are the final scale factors.</summary>
        static void PlaceGroundTile(GameObject prefab, Transform parent, Vector3 baseScale, float scaleX, float scaleZ, float x, float z,
            float topY, float bumpHeight, GroundSurface.Measurement surface)
        {
            GameObject tile = Spawn(prefab, parent);
            tile.transform.localScale = Vector3.Scale(baseScale, new Vector3(scaleX, 1f, scaleZ));
            AlignToBounds(tile, x, z, topY + bumpHeight, true);
            // A lumpy tile (grass) gets a flat collider at the surface, so kids and the NavMesh stand on the surface
            // and not on the tufts. Flat tiles (rubber) keep their own mesh.
            if (surface.Valid && surface.FlatShare < 0.9f) GroundSurface.UseFlatCollider(tile, topY);
            FinishEnvironmentObject(tile);
        }

        /// <summary>
        /// What is left of the rectangle after cutting out the avoid rectangles: the rectangle itself when none touches it (by
        /// more than a millimetre), nothing when it is covered, otherwise up to four strips per cut. Pieces under 1 cm are dropped.
        /// </summary>
        static List<Rect> SubtractRects(Rect whole, IList<Rect> avoid)
        {
            var pieces = new List<Rect> { whole };
            if (avoid == null) return pieces;

            const float tolerance = 0.001f;
            const float smallest = 0.01f;
            foreach (Rect cut in avoid)
            {
                var next = new List<Rect>();
                foreach (Rect p in pieces)
                {
                    float ox0 = Mathf.Max(p.xMin, cut.xMin), ox1 = Mathf.Min(p.xMax, cut.xMax);
                    float oz0 = Mathf.Max(p.yMin, cut.yMin), oz1 = Mathf.Min(p.yMax, cut.yMax);
                    if (ox1 - ox0 <= tolerance || oz1 - oz0 <= tolerance)
                    {
                        next.Add(p);
                        continue;
                    }

                    if (ox0 - p.xMin > smallest) next.Add(Rect.MinMaxRect(p.xMin, p.yMin, ox0, p.yMax));
                    if (p.xMax - ox1 > smallest) next.Add(Rect.MinMaxRect(ox1, p.yMin, p.xMax, p.yMax));
                    if (oz0 - p.yMin > smallest) next.Add(Rect.MinMaxRect(ox0, p.yMin, ox1, oz0));
                    if (p.yMax - oz1 > smallest) next.Add(Rect.MinMaxRect(ox0, oz1, ox1, p.yMax));
                }
                pieces = next;
            }
            return pieces;
        }

        /// <summary>Tiles smaller than half a metre are scaled up to 4 m so the grid stays a sane size.</summary>
        static float GuardTileSize(float measured, out float scale)
        {
            scale = 1f;
            if (measured >= 0.5f) return measured;
            if (measured > 0.01f) scale = 4f / measured;
            return 4f;
        }

        /// <summary>
        /// Fence segments around all four sides of the rectangle MinX..MaxX, MinZ..MaxZ.
        /// The side that faces gapDirection (a cardinal direction) has an opening of gapWidth metres centred on the
        /// origin's line, the way to the school.
        /// </summary>
        static void BuildFence(GameObject prefab, Transform parent, EnvironmentInfo info, Vector3 gapDirection, float gapWidth)
        {
            if (prefab == null) return;

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds))
            {
                Debug.LogWarning("CoolSchool: fence has no renderers, skipping.");
                Object.DestroyImmediate(probe);
                return;
            }
            Vector3 baseScale = probe.transform.localScale;
            Object.DestroyImmediate(probe);

            // The long horizontal axis is the one that should run along each side.
            bool longAxisIsX = probeBounds.size.x >= probeBounds.size.z;
            float length = Mathf.Max(probeBounds.size.x, probeBounds.size.z);
            if (length < 0.2f)
            {
                Debug.LogWarning("CoolSchool: fence segment is tiny, skipping.");
                return;
            }

            float xMid = (info.MinX + info.MaxX) * 0.5f;
            float zMid = (info.MinZ + info.MaxZ) * 0.5f;
            float sizeX = info.MaxX - info.MinX;
            float sizeZ = info.MaxZ - info.MinZ;
            var wholeSides = new[]
            {
                new FenceSide(new Vector3(xMid, 0f, info.MaxZ), Vector3.back, sizeX),
                new FenceSide(new Vector3(xMid, 0f, info.MinZ), Vector3.forward, sizeX),
                new FenceSide(new Vector3(info.MaxX, 0f, zMid), Vector3.left, sizeZ),
                new FenceSide(new Vector3(info.MinX, 0f, zMid), Vector3.right, sizeZ),
            };

            // The side with the gap is two runs, one each side of it.
            var sides = new List<FenceSide>();
            foreach (FenceSide whole in wholeSides)
            {
                if (Vector3.Dot(-whole.ToCentre, gapDirection) > 0.5f) sides.AddRange(whole.SplitAround(gapWidth));
                else sides.Add(whole);
            }

            foreach (FenceSide side in sides)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(side.Length / length));
                float spacing = side.Length / count;

                // Stretch slightly so segments meet; skip that if it would look wrong.
                float stretch = spacing / length;
                if (stretch < 0.8f || stretch > 1.25f) stretch = 1f;
                Vector3 scale = baseScale;
                if (longAxisIsX) scale.x *= stretch;
                else scale.z *= stretch;

                Vector3 along = Vector3.Cross(Vector3.up, side.ToCentre);
                Quaternion rotation = Quaternion.LookRotation(side.ToCentre, Vector3.up)
                    * Quaternion.Euler(0f, longAxisIsX ? 0f : 90f, 0f);

                for (int i = 0; i < count; i++)
                {
                    Vector3 point = side.Centre + along * (-side.Length * 0.5f + spacing * (i + 0.5f));
                    GameObject segment = Spawn(prefab, parent);
                    segment.transform.localScale = scale;
                    segment.transform.rotation = rotation;
                    AlignToBounds(segment, point.x, point.z, 0f, false);
                    FinishEnvironmentObject(segment);
                }
            }
        }

        /// <summary>One side of the fence rectangle (or a run of it): its middle, the direction pointing inwards, and its length.</summary>
        readonly struct FenceSide
        {
            public readonly Vector3 Centre;
            public readonly Vector3 ToCentre;
            public readonly float Length;

            public FenceSide(Vector3 centre, Vector3 toCentre, float length)
            {
                Centre = centre;
                ToCentre = toCentre;
                Length = length;
            }

            /// <summary>
            /// The two runs left when an opening of gapWidth metres is cut out of this side, centred where the
            /// side crosses the origin's line. A run shorter than half a metre is left out.
            /// </summary>
            public IEnumerable<FenceSide> SplitAround(float gapWidth)
            {
                Vector3 along = Vector3.Cross(Vector3.up, ToCentre);
                float middle = Vector3.Dot(Centre, along);
                Vector3 fixedPart = Centre - along * middle;

                float start = middle - Length * 0.5f;
                float end = middle + Length * 0.5f;
                float gapHalf = Mathf.Clamp(gapWidth * 0.5f, 0f, Mathf.Max(0f, Mathf.Min(-start, end) - 1f));

                float firstLength = -gapHalf - start;
                if (firstLength >= 0.5f)
                    yield return new FenceSide(fixedPart + along * ((start - gapHalf) * 0.5f), ToCentre, firstLength);

                float secondLength = end - gapHalf;
                if (secondLength >= 0.5f)
                    yield return new FenceSide(fixedPart + along * ((gapHalf + end) * 0.5f), ToCentre, secondLength);
            }
        }

        /// <summary>
        /// A bench at the middle of each side facing the field, with a bin beside it. On the two sides along the school
        /// axis (the way to the school, and the player's start opposite it) the bench steps aside and its bin goes outwards.
        /// </summary>
        static void PlaceBenchesAndBins(Transform parent, float distance, List<Vector3> occupied)
        {
            GameObject benchPrefab = LoadPrefab(BenchPrefab);
            GameObject binPrefab = LoadPrefab(BinPrefab);
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

            foreach (Vector3 direction in directions)
            {
                Vector3 point = direction * distance;
                if (IsReserved(point)) continue;
                Vector3 toCentre = -direction;
                Vector3 binSide = Vector3.Cross(Vector3.up, toCentre);

                // The way to the school runs through the middle of its side, and the player starts in the middle of the opposite one.
                if (Mathf.Abs(Vector3.Dot(direction, SchoolDirection)) > 0.5f)
                {
                    Vector3 aside = Vector3.Cross(Vector3.up, direction);
                    point += aside * (SchoolBuilder.CorridorHalfWidth + 2f);
                    binSide = aside;
                }
                float benchRadius = 1f;

                if (benchPrefab != null)
                {
                    GameObject bench = Spawn(benchPrefab, parent);
                    benchRadius = FootprintRadius(bench);
                    bench.transform.rotation = Quaternion.LookRotation(toCentre, Vector3.up);
                    AlignToBounds(bench, point.x, point.z, 0f, false);
                    FinishEnvironmentObject(bench);
                    occupied.Add(point);
                }

                if (binPrefab != null)
                {
                    Vector3 binPoint = point + binSide * (benchRadius + 0.8f);
                    GameObject bin = Spawn(binPrefab, parent);
                    bin.transform.rotation = Quaternion.LookRotation(toCentre, Vector3.up);
                    AlignToBounds(bin, binPoint.x, binPoint.z, 0f, false);
                    FinishEnvironmentObject(bin);
                    occupied.Add(binPoint);
                }
            }
        }

        /// <summary>
        /// Trees and bushes in the corners outside the benches, at least 3 m apart. In the arena they only
        /// line the side fences, so the open ground in front of the castle stays clear; the strip in front of the
        /// school gap stays clear too.
        /// </summary>
        static void ScatterGreenery(Transform parent, float benchDistance, EnvironmentInfo info, List<Vector3> occupied)
        {
            float fenceHalf = info.FenceHalf;
            GameObject[] prefabs = { LoadPrefab(TreePrefab), LoadPrefab(BushPrefab), LoadPrefab(TreeLargePrefab), LoadPrefab(BushPrefab) };
            prefabs = prefabs.Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return;

            var random = new System.Random(20240607);
            const int wanted = 20;
            const float spacing = 3f;
            float minRadius = benchDistance + 2f;
            float limit = fenceHalf - 1.5f;
            float arenaClearHalf = limit - 4f;

            // The greenery area: the fenced rectangle, pulled in from the fence.
            float xMin = info.MinX + 1.5f;
            float xMax = info.MaxX - 1.5f;
            float zMin = info.MinZ + 1.5f;
            float zMax = info.MaxZ - 1.5f;
            Vector3 arenaAcross = Vector3.Cross(Vector3.up, ArenaDirection);
            Vector3 fieldAcross = Vector3.Cross(Vector3.up, AlienFieldDirection);
            Vector3 schoolAcross = Vector3.Cross(Vector3.up, SchoolDirection);

            int placed = 0;
            for (int attempt = 0; attempt < 4000 && placed < wanted; attempt++)
            {
                float x = xMin + (float)random.NextDouble() * (xMax - xMin);
                float z = zMin + (float)random.NextDouble() * (zMax - zMin);
                var point = new Vector3(x, 0f, z);
                if (point.magnitude < minRadius) continue;
                // Keep the arena itself clear: only a 4 m strip along its side fences is allowed.
                if (Vector3.Dot(point, ArenaDirection) > info.ArenaStart - 2f && Mathf.Abs(Vector3.Dot(point, arenaAcross)) < arenaClearHalf) continue;
                // The same for the alien field on the other side.
                if (Vector3.Dot(point, AlienFieldDirection) > info.FenceHalf - 2f && Mathf.Abs(Vector3.Dot(point, fieldAcross)) < arenaClearHalf) continue;
                // Keep the way from the playground to the school door clear.
                if (Vector3.Dot(point, SchoolDirection) > 0f && Mathf.Abs(Vector3.Dot(point, schoolAcross)) < SchoolBuilder.CorridorHalfWidth) continue;
                if (IsReserved(point)) continue;
                if (occupied.Any(o => (o - point).sqrMagnitude < spacing * spacing)) continue;

                GameObject item = Spawn(prefabs[placed % prefabs.Length], parent);
                float scale = 0.9f + (float)random.NextDouble() * 0.25f;
                item.transform.localScale *= scale;
                item.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                AlignToBounds(item, x, z, 0f, false);
                FinishEnvironmentObject(item);

                occupied.Add(point);
                placed++;
            }

            if (placed < wanted)
                Debug.LogWarning($"CoolSchool: only placed {placed} of {wanted} trees and bushes (not enough room).");
        }

        /// <summary>Gives the object colliders if it has none, and marks it static for batching.</summary>
        internal static void FinishEnvironmentObject(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) == null)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go,
                    GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.BatchingStatic);
            }
        }

        // ----- Bounds helpers ------------------------------------------------------------------

        /// <summary>Combined bounds of the active mesh renderers. Pivots in the Synty packs can't be trusted.</summary>
        internal static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null) continue;
                }
                else
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        /// <summary>Half the diagonal of the footprint (xz), a rotation-proof radius for spacing.</summary>
        static float FootprintRadius(GameObject root)
        {
            if (!TryGetBounds(root, out Bounds bounds)) return 1f;
            return Mathf.Max(0.5f, 0.5f * new Vector2(bounds.size.x, bounds.size.z).magnitude);
        }

        /// <summary>
        /// Moves the object so the xz centre of its bounds is at (x, z), and its bottom
        /// (or top, if alignTop) is at y.
        /// </summary>
        internal static void AlignToBounds(GameObject go, float x, float z, float y, bool alignTop)
        {
            if (!TryGetBounds(go, out Bounds bounds))
            {
                go.transform.position = new Vector3(x, y, z);
                return;
            }

            float referenceY = alignTop ? bounds.max.y : bounds.min.y;
            go.transform.position += new Vector3(x - bounds.center.x, y - referenceY, z - bounds.center.z);
        }

        // ----- NavMesh -------------------------------------------------------------------------

        /// <summary>Bakes before any character exists, so kids don't cut holes in the NavMesh.</summary>
        static void BuildNavMesh()
        {
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Agents are put at the real surface height instead of the voxel grid's (which floats them up to several cm).
            surface.buildHeightMesh = true;
            Physics.SyncTransforms();
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("CoolSchool: NavMesh bake produced no data.");
                return;
            }

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        internal static Vector3 SnapToNavMesh(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                return hit.position;

            Debug.LogWarning($"CoolSchool: no NavMesh near {position}; leaving the kid where it is.");
            return position;
        }

        // ----- Touch UI ------------------------------------------------------------------------

        static void BuildTouchUI(out FloatingJoystick joystick, out ActionButton jumpButton, out CanvasGroup imaginationOverlay)
        {
            var canvasObject = new GameObject("TouchUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            AssignPersistentUIActions(inputModule);
            EditorUtility.SetDirty(inputModule);

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");

            // First, so it is drawn behind the joystick and button.
            imaginationOverlay = BuildImaginationOverlay(canvasObject.transform);
            joystick = BuildJoystick(canvasObject.transform, knob);
            jumpButton = BuildJumpButton(canvasObject.transform, knob, arrow);
        }

        /// <summary>A full-screen soft gold wash for the imagination flash: invisible, never blocks touches.</summary>
        static CanvasGroup BuildImaginationOverlay(Transform canvas)
        {
            RectTransform overlay = NewRect("ImaginationOverlay", canvas);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            AddImage(overlay, null, new Color(1f, 0.88f, 0.45f, 1f), false);

            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        /// <summary>
        /// Points the UI module at the package's default UI actions as saved assets, so the references
        /// survive in the scene (AssignDefaultActions only creates in-memory objects).
        /// </summary>
        static void AssignPersistentUIActions(InputSystemUIInputModule module)
        {
            const string path = "Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"CoolSchool: default input actions not found at {path}; UI input will not work.");
                return;
            }

            InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().ToArray();

            InputActionReference Find(string actionName)
            {
                InputActionReference found = references.FirstOrDefault(r =>
                    r.action != null && r.action.actionMap.name == "UI" && r.action.name == actionName);
                if (found == null) Debug.LogWarning($"CoolSchool: UI action '{actionName}' not found in default input actions.");
                return found;
            }

            module.actionsAsset = asset;
            module.point = Find("Point");
            module.leftClick = Find("Click");
            module.rightClick = Find("RightClick");
            module.middleClick = Find("MiddleClick");
            module.scrollWheel = Find("ScrollWheel");
            module.move = Find("Navigate");
            module.submit = Find("Submit");
            module.cancel = Find("Cancel");
            module.trackedDevicePosition = Find("TrackedDevicePosition");
            module.trackedDeviceOrientation = Find("TrackedDeviceOrientation");
        }

        /// <summary>Left half of the screen is the touch zone; the joystick appears under the thumb.</summary>
        static FloatingJoystick BuildJoystick(Transform canvas, Sprite knob)
        {
            RectTransform zone = NewRect("JoystickZone", canvas);
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = new Vector2(0.5f, 1f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            AddImage(zone, null, new Color(0f, 0f, 0f, 0f), true);

            RectTransform background = NewRect("Background", zone);
            // Rests the same distance from the bottom-left edges as the jump button is from the bottom-right.
            SetCornerAnchored(background, Vector2.zero, new Vector2(230f, 230f), new Vector2(260f, 260f));
            AddImage(background, knob, new Color(1f, 1f, 1f, 0.35f), false);

            RectTransform handle = NewRect("Handle", background);
            SetCornerAnchored(handle, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            AddImage(handle, knob, new Color(1f, 1f, 1f, 0.85f), false);

            var joystick = zone.gameObject.AddComponent<FloatingJoystick>();
            SetObject(joystick, "background", background);
            SetObject(joystick, "handle", handle);
            SetBool(joystick, "hideWhenIdle", false);
            return joystick;
        }

        static ActionButton BuildJumpButton(Transform canvas, Sprite knob, Sprite arrow)
        {
            RectTransform button = NewRect("ActionButton", canvas);
            SetCornerAnchored(button, new Vector2(1f, 0f), new Vector2(-230f, 230f), new Vector2(260f, 260f));
            AddImage(button, knob, new Color(1f, 0.78f, 0.2f, 0.95f), true);
            var action = button.gameObject.AddComponent<ActionButton>();

            // The dropdown arrow points down; turned half a circle it points up, meaning "jump".
            RectTransform icon = NewRect("Icon", button);
            SetCornerAnchored(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            icon.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image iconImage = AddImage(icon, arrow, Color.white, false);
            SetObject(action, "icon", iconImage);

            return action;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Anchors to a single point (anchor == pivot == this point) with a fixed size.</summary>
        static void SetCornerAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            image.preserveAspect = true;
            return image;
        }

        // ----- Characters ----------------------------------------------------------------------

        static GameObject CreatePlayer(RuntimeAnimatorController controller, int layer, FloatingJoystick joystick, ActionButton jumpButton,
            EnvironmentInfo environment)
        {
            Vector3 position = SnapToNavMesh(environment.PlayerSpawn);
            GameObject root = CreateKid("Player", PlayerKidPrefab, controller, position, out float height);

            // Starts in the south of the playground facing north (+Z), the way the camera looks: the school is straight ahead.
            root.transform.rotation = Quaternion.LookRotation(environment.PlayerFacing, Vector3.up);

            var body = root.AddComponent<CharacterController>();
            body.height = height;
            body.radius = Mathf.Clamp(height * 0.22f, 0.2f, 0.35f);
            body.center = new Vector3(0f, height * 0.5f + body.skinWidth, 0f);
            body.stepOffset = 0.25f;

            var movement = root.AddComponent<PlayerController>();
            SetObject(movement, "joystick", joystick);
            SetObject(movement, "jumpButton", jumpButton);
            SetObject(movement, "cameraTransform", FindMainCamera().transform);

            var participant = root.AddComponent<TagParticipant>();
            participant.Configure(true, 1.3f, height);
            EditorUtility.SetDirty(participant);

            // Knights game: the player can hit and block, but is never defeated. Input stays off until the game starts.
            var hittable = root.AddComponent<Hittable>();
            SetEnum(hittable, "team", (int)BattleTeam.Heroes);
            SetBool(hittable, "canBeDefeated", false);
            root.AddComponent<SwordFighter>();
            var battleInput = root.AddComponent<PlayerBattleInput>();
            SetObject(battleInput, "button", jumpButton);
            battleInput.enabled = false;
            AddWeaponHolder(root, PlayerSword, PlayerShield);

            SetLayerRecursive(root, layer);
            return root;
        }

        /// <summary>
        /// Adds a WeaponHolder with the shared offsets. A default (nameless) shield reference means no shield.
        /// </summary>
        internal static WeaponHolder AddWeaponHolder(GameObject root, PrefabRef sword, PrefabRef shield)
        {
            var holder = root.AddComponent<WeaponHolder>();
            SetObject(holder, "swordPrefab", sword.Name != null ? LoadPrefab(sword) : null);
            SetObject(holder, "shieldPrefab", shield.Name != null ? LoadPrefab(shield) : null);
            SetVector3(holder, "swordLocalPosition", SwordLocalPosition);
            SetVector3(holder, "swordLocalEuler", SwordLocalEuler);
            SetVector3(holder, "shieldLocalPosition", ShieldLocalPosition);
            SetVector3(holder, "shieldLocalEuler", ShieldLocalEuler);
            SetFloat(holder, "weaponScale", WeaponScale);
            return holder;
        }

        /// <summary>
        /// Five computer kids on a small ring in the field, each snapped to the NavMesh.
        /// The first AllyCount of them can become knight allies. Returns those allies' roots.
        /// </summary>
        static List<GameObject> CreateComputerKids(RuntimeAnimatorController controller, int layer, float ringRadius)
        {
            var allies = new List<GameObject>();
            var spawnCentre = new Vector3(0f, 0f, 3f);
            for (int i = 0; i < ComputerKidPrefabs.Length; i++)
            {
                float angle = i * 2f * Mathf.PI / ComputerKidPrefabs.Length;
                Vector3 wanted = spawnCentre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 5f;
                Vector3 position = SnapToNavMesh(wanted);

                string kidName = ComputerKidPrefabs[i].Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid(kidName, ComputerKidPrefabs[i], controller, position, out float height);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 1.2f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                var participant = root.AddComponent<TagParticipant>();
                participant.Configure(false, 0.9f, height);
                EditorUtility.SetDirty(participant);

                var ai = root.AddComponent<TagKidAI>();
                ai.SetPlayArea(Vector3.zero, ringRadius + 2f);
                EditorUtility.SetDirty(ai);

                if (i < AllyCount)
                {
                    var hittable = root.AddComponent<Hittable>();
                    SetEnum(hittable, "team", (int)BattleTeam.Heroes);
                    SetBool(hittable, "canBeDefeated", false);
                    root.AddComponent<SwordFighter>();
                    root.AddComponent<KnightAllyAI>().enabled = false;
                    AddWeaponHolder(root, AllySword, AllyShield);
                    allies.Add(root);
                }

                SetLayerRecursive(root, layer);
            }

            return allies;
        }

        /// <summary>
        /// Creates an empty root with the kid model as its child, hooks up the animator,
        /// measures the kid's height and adds KidAnimator to the root.
        /// </summary>
        internal static GameObject CreateKid(string name, PrefabRef kidPrefab, RuntimeAnimatorController controller, Vector3 position, out float height)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            height = 1.2f;

            GameObject prefab = LoadPrefab(kidPrefab);
            Animator animator = null;
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    if (controller != null) animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }

                if (TryGetBounds(model, out Bounds bounds))
                    height = Mathf.Clamp(bounds.size.y, 0.8f, 1.8f);
            }

            var kidAnimator = root.AddComponent<KidAnimator>();
            if (animator != null) SetObject(kidAnimator, "animator", animator);
            return root;
        }

        internal static void SetLayerRecursive(GameObject root, int layer)
        {
            if (layer < 0) return;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        // ----- Camera and game -----------------------------------------------------------------

        static Camera FindMainCamera()
        {
            Camera camera = Camera.main;
            return camera != null ? camera : Object.FindFirstObjectByType<Camera>();
        }

        static void SetUpCamera(Transform player, int kidsLayer)
        {
            Camera camera = FindMainCamera();
            camera.fieldOfView = 50f;
            camera.farClipPlane = 200f;
            ColorGradingSetup.EnablePostProcessing(camera);

            // The camera ignores kids (so they never block the view) and Ignore Raycast.
            int ignored = 1 << LayerMask.NameToLayer("Ignore Raycast");
            if (kidsLayer >= 0) ignored |= 1 << kidsLayer;

            var follow = camera.gameObject.AddComponent<FollowCamera>();
            SetObject(follow, "target", player);
            SetInt(follow, "obstacleMask", ~ignored);

            Vector3 offset = GetVector3(follow, "offset");
            Vector3 lookOffset = GetVector3(follow, "lookOffset");
            camera.transform.position = player.position + offset;
            camera.transform.rotation = Quaternion.LookRotation(player.position + lookOffset - camera.transform.position, Vector3.up);
        }

        /// <summary>
        /// The ActivityManager with FreePlay as its default, and the tag game with its glowing zone.
        /// Nothing starts by itself: walking into the zone begins the game.
        /// </summary>
        static void BuildActivities(Transform player, Material markerMaterial)
        {
            var managerObject = new GameObject("ActivityManager");
            var manager = managerObject.AddComponent<ActivityManager>();
            var freePlay = managerObject.AddComponent<FreePlay>();
            SetObject(manager, "defaultActivity", freePlay);

            TagGame tagGame = BuildTagGame(player, markerMaterial);
            KnightsArenaBuilder.CreateActivityZone("TagZone", null, new Vector3(0f, 0f, 4f), 1.5f, tagGame,
                KnightsArenaBuilder.CreateTagZoneMaterial(), ActivityBeaconBuilder.TagBeacon);
        }

        static TagGame BuildTagGame(Transform player, Material markerMaterial)
        {
            var gameObject = new GameObject("TagGame");
            var game = gameObject.AddComponent<TagGame>();

            var marker = new GameObject("ItMarker");
            marker.transform.position = player.position + Vector3.up * 1.7f;

            GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gem.name = "Gem";
            Object.DestroyImmediate(gem.GetComponent<Collider>());
            gem.transform.SetParent(marker.transform, false);
            gem.transform.localScale = Vector3.one * 0.3f;
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            if (markerMaterial != null) gem.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;

            var glow = marker.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.9f, 0.3f);
            glow.range = 3f;
            glow.intensity = 2f;
            glow.shadows = LightShadows.None;

            SetObject(game, "itMarker", marker.transform);

            // TagGame shows the marker when a round begins.
            marker.SetActive(false);
            return game;
        }

        /// <summary>The scene's BattleEffects, wired to the particle prefabs.</summary>
        static void BuildBattleEffects(BattleEffectsBuilder.Result effects)
        {
            var effectsObject = new GameObject("BattleEffects");
            var battleEffects = effectsObject.AddComponent<BattleEffects>();
            SetObject(battleEffects, "confettiPrefab", effects.Confetti);
            SetObject(battleEffects, "blockSparkPrefab", effects.BlockSpark);
            SetObject(battleEffects, "sparklePrefab", effects.Sparkle);
            SetObject(battleEffects, "swooshPrefab", effects.Swoosh);
        }

        /// <summary>The imagination effect: the gold overlay and the castle's hidden flags and torches.</summary>
        static ImaginationEffect BuildImaginationEffect(CanvasGroup overlay, KnightsArenaBuilder.ArenaLayout arena)
        {
            var effectObject = new GameObject("ImaginationEffect");
            var effect = effectObject.AddComponent<ImaginationEffect>();
            SetObject(effect, "overlay", overlay);
            if (arena != null) SetObjectArray(effect, "imaginationProps", arena.ImaginationProps);
            SetFloat(effect, "flashSeconds", 1f);
            SetFloat(effect, "maxOverlayAlpha", 0.35f);
            return effect;
        }

        /// <summary>
        /// A sprite whose name contains "Sword", preferring the Modular Game UI Kit and an exact "Sword".
        /// Null if the project has none.
        /// </summary>
        static Sprite FindSwordIcon()
        {
            Sprite best = null;
            int bestScore = -1;
            foreach (string guid in AssetDatabase.FindAssets("Sword t:Sprite"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string spriteName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!spriteName.Contains("Sword")) continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                int score = (path.Contains("ModularGameUIKit") ? 2 : 0) + (spriteName == "Sword" ? 1 : 0);
                if (score > bestScore)
                {
                    best = sprite;
                    bestScore = score;
                }
            }
            return best;
        }

        // ----- Build settings ------------------------------------------------------------------

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Tablet in landscape, either way up, never portrait.</summary>
        static void ApplyTabletSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        static void ShowSummary(int upgraded, float ringRadius, AnimatorController controller, List<string> missingClips,
            KnightsArenaBuilder.ArenaResult arena)
        {
            string message = $"Scene saved to {ScenePath}\n\n"
                + $"Materials upgraded to URP: {upgraded}\n"
                + $"Playground ring radius: {ringRadius:0.0} m";

            // The sword, shield and mace clips come from a different pack than the other kid clips, so report them separately.
            List<string> missingSwordClips = missingClips.Where(KidAnimatorBuilder.IsCombatPackClip).ToList();
            List<string> missingDragonClips = missingClips.Where(c => c.StartsWith("Dragon")).ToList();
            List<string> missingDanceClips = missingClips.Where(c => System.Array.IndexOf(KidAnimatorBuilder.DanceClips, c) >= 0).ToList();
            List<string> missingHumanClips = missingClips.Except(missingSwordClips).Except(missingDragonClips).Except(missingDanceClips).ToList();

            if (controller == null)
            {
                message += "\n\nThe Kevin Iglesias \"Human Animations\" clips were not found. "
                    + "Import Human Animations, then run CoolSchool > Build Playground Prototype again. "
                    + "Until then the kids will be static.";
            }
            else if (missingHumanClips.Count > 0)
            {
                message += "\n\nSome optional Kevin Iglesias \"Human Animations\" clips are missing (" + string.Join(", ", missingHumanClips)
                    + "). Import Human Animations and re-run the builder to add them.";
            }

            if (missingSwordClips.Count > 0)
            {
                message += "\n\nSword, shield and club animations are missing (" + string.Join(", ", missingSwordClips)
                    + "). Import the " + KidAnimatorBuilder.CombatPackName + " and re-run the builder; "
                    + "until then knights attack without a sword animation.";
            }

            if (missingDanceClips.Count > 0)
            {
                message += "\n\nThe dance clips (" + string.Join(", ", missingDanceClips)
                    + ") from the Gestures MoCap Collection are missing. Import it and re-run the builder; "
                    + "until then the aliens and the superhero do not dance.";
            }

            if (!arena.DragonCreated)
            {
                message += "\n\nThe dragon (Dragon_LP prefab) was not found, so the knights game has no dragon fight. "
                    + "Import the Infinity PBR dragon and re-run the builder.";
            }
            else if (arena.DragonControllerMissing)
            {
                message += "\n\nThe dragon was placed, but Dragon.controller could not be built ("
                    + string.Join(", ", missingDragonClips) + "), so it keeps its own animator.";
            }
            else if (missingDragonClips.Count > 0)
            {
                message += "\n\nSome dragon clips are missing (" + string.Join(", ", missingDragonClips) + "); those moves will not animate.";
            }

            Debug.Log("CoolSchool: " + message.Replace("\n\n", " ").Replace("\n", " "));
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("CoolSchool playground", message, "OK");
        }

        // ----- Serialized field helpers --------------------------------------------------------

        /// <summary>Runs set on the named serialized field and applies it; warns if the field doesn't exist.</summary>
        static void SetField(Object target, string field, System.Action<SerializedProperty> set)
        {
            if (target == null)
            {
                Debug.LogWarning($"CoolSchool: cannot set '{field}' on a missing component.");
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            set(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetObject(Object target, string field, Object value)
        {
            SetField(target, field, property => property.objectReferenceValue = value);
        }

        internal static void SetInt(Object target, string field, int value)
        {
            SetField(target, field, property => property.intValue = value);
        }

        internal static void SetBool(Object target, string field, bool value)
        {
            SetField(target, field, property => property.boolValue = value);
        }

        internal static void SetFloat(Object target, string field, float value)
        {
            SetField(target, field, property => property.floatValue = value);
        }

        internal static void SetVector3(Object target, string field, Vector3 value)
        {
            SetField(target, field, property => property.vector3Value = value);
        }

        internal static void SetBounds(Object target, string field, Bounds value)
        {
            SetField(target, field, property => property.boundsValue = value);
        }

        /// <summary>Sets an enum field by its index in the declaration order.</summary>
        internal static void SetEnum(Object target, string field, int index)
        {
            SetField(target, field, property => property.enumValueIndex = index);
        }

        internal static void SetStringArray(Object target, string field, IReadOnlyList<string> values)
        {
            SetField(target, field, property =>
            {
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                    property.GetArrayElementAtIndex(i).stringValue = values[i];
            });
        }

        internal static void SetObjectArray(Object target, string field, IReadOnlyList<Object> values)
        {
            SetField(target, field, property =>
            {
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });
        }

        internal static void SetIntArray(Object target, string field, IReadOnlyList<int> values)
        {
            SetField(target, field, property =>
            {
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++)
                    property.GetArrayElementAtIndex(i).intValue = values[i];
            });
        }

        static Vector3 GetVector3(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property != null ? property.vector3Value : Vector3.zero;
        }
    }
}
