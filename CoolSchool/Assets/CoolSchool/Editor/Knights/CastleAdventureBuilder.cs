using System.Collections.Generic;
using System.Linq;
using CoolSchool.Battle;
using CoolSchool.Input;
using CoolSchool.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The builder of the castle adventure (contract Docs/specs/castle-adventure-contract.md): the queen, the password stands,
    /// the hole in the wall with its blocks, the mission table, the barrels, the wall perches, the cookie table, the dragon's
    /// seat and ride path, the name flag, the growing castle and the tower rescue, plus every component and its wiring.
    /// Two steps, like the castle itself:
    /// - BuildProps (called at the end of KnightsArenaBuilder.BuildCastle, before the NavMesh bake): static props only. None of
    ///   them has a collider, so nothing can change the NavMesh.
    /// - Build (called from PlaygroundPrototypeBuilder.RunBuild after the lesson setup): characters, the runtime components and the
    ///   wiring into the KnightsGame.
    /// Files: this one (entry points, shared helpers), .Props (step 1), .Moments (step 2), .Dragon (seat, baby dragon, ride path)
    /// and .Materials (the materials and generated meshes).
    /// All positions are worked out from the ArenaLayout (courtyard, gate, ladders), never hard-coded.
    /// </summary>
    internal static partial class CastleAdventureBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string MaterialFolder = RootFolder + "/Materials/Castle";
        const string MeshFolder = RootFolder + "/Meshes";
        internal const string BabyDragonPrefabPath = RootFolder + "/Prefabs/BabyDragon.prefab";
        const string DragonControllerPath = RootFolder + "/Animation/Dragon.controller";

        // ----- Models (found by name) ---------------------------------------------------------

        static readonly PrefabRef[] StandBoxPrefabs =
        {
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_01", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_02", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_03", "PolygonGeneric"),
        };
        static readonly PrefabRef TablePrefab = new PrefabRef("SM_Gen_Prop_Table_01", "PolygonGeneric");
        // The cake and its slice come from the same pack, so their sizes match.
        static readonly PrefabRef CakePrefab = new PrefabRef("SM_Prop_Cake_01", "PolygonTown");
        static readonly PrefabRef CakeSlicePrefab = new PrefabRef("SM_Prop_Cake_Slice_01", "PolygonTown");
        static readonly PrefabRef CrownPrefab = Kids("SM_Chr_Attach_Crown_01");
        static readonly PrefabRef[] ToyBlockPrefabs =
        {
            Kids("SM_Prop_Toy_Block_01"), Kids("SM_Prop_Toy_Block_02"), Kids("SM_Prop_Toy_Block_03"), Kids("SM_Prop_Toy_Block_04"),
        };
        static readonly PrefabRef[] BarrelPrefabs =
        {
            new PrefabRef("SM_Gen_Prop_Barrel_Wood_01", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Barrel_Wood_02", "PolygonGeneric"),
        };
        static readonly PrefabRef BowlPrefab = new PrefabRef("SM_Prop_PetBowl_01", "PolygonTown");
        static readonly PrefabRef LadderPrefab = new PrefabRef("SM_Prop_Ladder_01", "PolygonTown");
        static readonly PrefabRef FlagA = Kids("SM_Prop_Flag_01");
        static readonly PrefabRef FlagB = Kids("SM_Prop_Flag_02");
        static readonly PrefabRef PeekGoblinPrefab = Kids("SM_Chr_Kid_Goblin_01");
        static readonly PrefabRef PrincessPrefab = Kids("SM_Chr_Kid_Princess_01");
        static readonly PrefabRef PaperCrownPrefab = Kids("SM_Chr_Attach_Hat_PaperCrown_01");
        static readonly PrefabRef DragonModelPrefab = new PrefabRef("Dragon_LP", "Magic Pig");

        // ----- Sizes (metres) -----------------------------------------------------------------

        const float StandSize = 0.6f;
        const float TableHeight = 0.75f;
        const float BlockSize = 0.42f;
        const float BarrelHeight = 0.9f;
        const float GrowthFlagHeight = 2.2f;
        const int PerchCount = 7;
        const int ThiefPointCount = 8;
        const int GrowthTierCount = 7;
        const int StarCount = 8;

        /// <summary>The three password stands, the queen, and where each of them is relative to the gate (contract numbers).</summary>
        const float TalkDistance = 4f;
        const float StandDistance = 2.2f;
        const float StandSpacing = 1.6f;

        // ----- What the two steps share ------------------------------------------------------

        /// <summary>What Build needs from the rest of the scene.</summary>
        internal sealed class CastleInputs
        {
            public RuntimeAnimatorController KidController;
            public int KidsLayer = -1;
            public PlayerController Player;
            public ActionButton ActionButton;
            public KnightAllyAI[] Allies;
            public Sprite SwordIcon;
        }

        /// <summary>
        /// The static props of step 1 and the points step 2 hangs its components on. Positions that need the NavMesh are
        /// snapped in Build (the NavMesh does not exist yet when these are made).
        /// </summary>
        internal sealed class PropLayout
        {
            public Transform Root;
            public Transform PointsGroup;

            // The gate end.
            public Transform GateTalkSpot;
            public Transform QueenHome;
            public Transform QueenSpot;
            public Transform QueenCelebrateSpot;
            public Transform BabyCelebrateSpot;
            public Transform CelebrateSpot;
            public Transform DismountPoint;
            public Transform PasswordGate;
            public Transform[] Stands = new Transform[3];

            // The wall patch.
            public Transform WallPatch;
            public Vector3 HolePosition;
            public Transform StandPoint;
            public Transform[] Slots = new Transform[8];
            public GameObject[] Ghosts = new GameObject[8];
            public Transform[] PileBlocks = new Transform[10];
            /// <summary>Where the goblin that peeks stands (hidden behind the wall, head just under its top).</summary>
            public Vector3 PeekGoblinFeet;

            // The mission spot.
            public Transform MissionSpot;
            public GameObject CakeSet;
            public GameObject CrownSet;
            public GameObject EggSet;
            public GameObject CakeLoot;
            public GameObject CrownLoot;
            public GameObject EggLoot;
            public GameObject[] ShellPieces = new GameObject[0];
            public Vector3 NestPosition;

            // Barrels, perches and thief points.
            public GameObject[] Barrels = new GameObject[2];
            public Transform[] BarrelInside = new Transform[2];
            public Transform[] BarrelHopOut = new Transform[2];
            public Transform[] Perches = new Transform[PerchCount];
            public Transform[] PerchLandings = new Transform[PerchCount];
            public Transform[] ThiefPoints = new Transform[ThiefPointCount];

            // The cookie table.
            public Transform CookieTable;
            public Transform[] Cookies = new Transform[10];
            public Transform BigCookie;
            public Transform Bowl;

            // The name flag.
            public Transform FlagPole;
            public RectTransform LetterRow;
            public GameObject Emblem;
            public Canvas NameCanvas;

            // The growing castle.
            public Transform Growth;
            public GameObject[] Tiers = new GameObject[GrowthTierCount];

            // The tower rescue.
            public Transform TowerRescue;
            public Transform TowerTop;
            public GameObject RescueRing;
            public GameObject RescueLadder;

            // The ride.
            public Transform RideGroup;
            public Transform[] RidePoints = new Transform[0];
            public GameObject[] RideStars = new GameObject[0];
            public float RideLength;

            // Where the baby dragon waits (the nest) and the arena centre the dragon lands on.
            public Vector3 Landing;
        }

        // ----- Entry points -----------------------------------------------------------------------

        /// <summary>
        /// Step 1: the static props, placed from the layout. Called at the end of KnightsArenaBuilder.BuildCastle. Nothing here has a
        /// collider, so the NavMesh is unchanged; whatever is hidden at the start is built visible first (bounds are only measured on
        /// active objects) and switched off at the end.
        /// </summary>
        internal static void BuildProps(KnightsArenaBuilder.ArenaLayout layout)
        {
            if (layout == null || layout.Root == null) return;

            ClearMaterialCache();
            var props = new PropLayout();
            layout.Props = props;
            props.Root = NewGroup("CastleAdventure", layout.Root);
            props.PointsGroup = NewGroup("Points", props.Root);

            Bounds court = layout.Courtyard;
            props.Landing = new Vector3(court.center.x, 0f, court.center.z + 1f);

            BuildGateEnd(layout, props);
            BuildWallPatch(layout, props);
            BuildMissionSpot(layout, props);
            BuildBarrels(layout, props);
            BuildPerchesAndThiefPoints(layout, props);
            BuildCookieTable(layout, props);
            BuildFlagPole(layout, props);
            BuildGrowthTiers(layout, props);
            BuildTowerRescueProps(layout, props);
            BuildRidePathProps(layout, props);
        }

        /// <summary>
        /// Step 2: the queen, the peeking goblin, the baby dragon, the help markers, every runtime component and the wiring into the
        /// KnightsGame. Needs the game, the dragon, the ladders and the allies (all made by KnightsArenaBuilder.Build) and the LessonUI
        /// and letter set (made by the lesson setup), so it runs after the lesson setup.
        /// </summary>
        internal static void Build(KnightsArenaBuilder.ArenaLayout layout, CastleInputs inputs)
        {
            PropLayout p = layout != null ? layout.Props : null;
            var game = Object.FindFirstObjectByType<KnightsGame>();
            if (p == null || inputs == null || game == null)
            {
                Debug.LogWarning("CoolSchool: the castle adventure was not built (no castle props or no KnightsGame).");
                return;
            }

            BuildMoments(layout, p, inputs, game);
        }

        // ----- Small shared helpers ----------------------------------------------------------------

        internal static Transform MakePoint(string pointName, Transform parent, Vector3 worldPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.position = worldPosition;
            return point;
        }

        /// <summary>A primitive in the parent's frame, without its collider and its shadow.</summary>
        internal static Transform MakePrimitive(PrimitiveType type, string objectName, Transform parent, Vector3 localPosition,
            Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        static Quaternion SouthFacing => Quaternion.Euler(0f, 180f, 0f);

        /// <summary>Takes every collider off a model: props must never change the NavMesh or push anybody.</summary>
        internal static void StripColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        /// <summary>The first model of the list that exists in the project (by exact name), else the first one.</summary>
        internal static PrefabRef FirstExisting(PrefabRef[] references)
        {
            foreach (PrefabRef reference in references)
                if (PrefabExists(reference.Name)) return reference;
            return references[0];
        }

        static bool PrefabExists(string prefabName)
        {
            foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
                if (System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == prefabName) return true;
            return false;
        }

        /// <summary>True when the model has an Animator with a valid humanoid avatar (so the Kid controller can drive it).</summary>
        internal static bool IsHumanoidPrefab(PrefabRef reference)
        {
            if (!PrefabExists(reference.Name)) return false;

            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return false;

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Animator animator = probe.GetComponentInChildren<Animator>();
            bool humanoid = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
            Object.DestroyImmediate(probe);
            return humanoid;
        }

        /// <summary>
        /// Puts a model in a wrapper: spawned in the wrapper, turned by yaw, scaled so its largest side (or its height) is `size`, and
        /// shifted so the middle of its bottom (or the middle of its bounds) sits on the wrapper's origin. The wrapper must be active,
        /// unscaled and unrotated, and already at its place (bounds are measured in the world). Colliders are taken off.
        /// Returns the model, or null when the model is missing.
        /// </summary>
        internal static GameObject SpawnFitted(PrefabRef reference, Transform wrapper, float size, bool bySize = true, bool byHeight = false,
            bool centre = false, float yaw = 0f)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject model = Spawn(prefab, wrapper);
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            StripColliders(model);

            if (bySize && TryGetBounds(model, out Bounds measured))
            {
                float side = byHeight ? measured.size.y : Mathf.Max(measured.size.x, Mathf.Max(measured.size.y, measured.size.z));
                if (side > 0.001f) model.transform.localScale *= size / side;
            }

            if (TryGetBounds(model, out Bounds fitted))
            {
                float y = centre ? fitted.center.y : fitted.min.y;
                model.transform.position += wrapper.position - new Vector3(fitted.center.x, y, fitted.center.z);
            }
            else
            {
                model.transform.position = wrapper.position;
            }
            return model;
        }

        /// <summary>Switches every group of a list off (they are built visible so their bounds can be measured).</summary>
        static void Hide(params GameObject[] things)
        {
            foreach (GameObject thing in things)
                if (thing != null) thing.SetActive(false);
        }

        /// <summary>The x of goblin ladder i (west to east), from the real ladders or, when a ladder is missing, spread over the back wall.</summary>
        static float LadderX(KnightsArenaBuilder.ArenaLayout layout, int index)
        {
            if (index < layout.Ladders.Count) return layout.Ladders[index].Foot.x;

            float spread = layout.Courtyard.size.x * 0.25f;
            return layout.Courtyard.center.x + (index - 1) * spread;
        }

        /// <summary>The meshes made by this builder are saved as assets (a prefab or scene must not hold loose meshes).</summary>
        static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            EnsureFolder(RootFolder, "Meshes");
            string path = MeshFolder + "/" + assetName + ".asset";
            mesh.name = assetName;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>
        /// Adds a triangle whose face points the way `facing` says, whatever order the corners came in (Unity's front face is the one
        /// the corners go round clockwise).
        /// </summary>
        static void AddTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 facing)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, facing) >= 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
