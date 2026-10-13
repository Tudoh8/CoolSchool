using System.Collections.Generic;
using System.Linq;
using CoolSchool.Zoo;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the zoo park west of the alien field (contract Docs/specs/hide-and-seek-contract.md). Two steps, like the pirate cove:
    /// BuildPark (static geometry, before the NavMesh is baked) and Build (characters, the activity, the UI and all the wiring;
    /// see ZooParkBuilder.Build.cs).
    ///
    /// The park is laid out in park coordinates: u metres west of the main west fence (x = MinX - u) and v = the world z. The camera
    /// looks north, so the hiding spots lie north of the zone, the pens along the north fence. Every prop is placed by its measured
    /// renderer bounds, never by its pivot.
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string MaterialFolder = RootFolder + "/Materials/Zoo";
        internal const string AnimalMaterialFolder = MaterialFolder + "/Animals";

        // ---- Layout (park coordinates) ----
        const float ParkWidthU = 48f;
        const float ParkSouthV = -14f;
        const float ParkNorthV = 30f;
        const float EntranceV = -3f;
        const float EntranceWidth = 3f;
        // Grass beyond the park's own fence (so the camera never looks into the void).
        const float OuterMargin = 6f;

        const float ZoneU = 7f;
        const float ZoneV = -3f;
        const float ZoneRadius = 2f;
        const float BaseTreeU = 7f;
        const float BaseTreeV = 1f;
        const float BaseTreeHeight = 7f;
        const float KeeperU = 9.5f;
        const float KeeperV = -1.5f;
        const float CountU = 7f;
        const float CountV = -0.2f;
        const float PenWalkU = 24f;
        const float PenWalkV = 21.5f;
        const float PartyV = 21.5f;
        const float PartyU0 = 18f;
        const float PartyU1 = 30f;
        const int PartyCount = 6;

        // Park coordinates of the pen row.
        const int PenCount = 10;
        const float PenU0 = 2f;
        const float PenWidth = 4.4f;
        const float PenV0 = 24f;
        const float PenV1 = 28.5f;

        const float TrunkRadius = 0.5f;

        static readonly PrefabRef ParkFencePrefab = Kids("SM_Env_Park_Fence_01");
        static readonly PrefabRef GrassTilePrefab = Kids("SM_Env_Ground_Tile_Grass");
        static readonly PrefabRef BaseTreePrefab = Kids("SM_Env_Tree_Large_01");

        // Jungle-green beacon with a paw, an animal, an eye or a magnifier on it, whichever the UI kit has first.
        internal static readonly ActivityBeaconBuilder.Style ZooBeacon = new ActivityBeaconBuilder.Style(
            "Zoo", new Color(0.5f, 2f, 0.7f, 1f), new[] { "Paw", "Animal", "Eye", "Search", "Circle" });

        // ---- What the second step needs from the first ----

        internal sealed class SpotData
        {
            public string Id;
            /// <summary>The HidingSpotKind index: Cover 0, Container 1, Perch 2, Under 3.</summary>
            public int Kind;
            public float Sink;
            /// <summary>Containers: the rim's height above the ground, and the part of an animal that shows over it.</summary>
            public float Rim;
            public float Show;
            /// <summary>The tent: the part of an animal's length that sticks out of the front.</summary>
            public float FrontOut;
            public float CheckRadius = 1.4f;
            public Transform Root;
            public Transform Cover;
            public Transform Hide;
            public Transform Peek;
            public Transform PlayerHide;
            public GameObject Glow;
            public Bounds Bounds;
            public bool PropMissing;
        }

        internal sealed class PenData
        {
            public string KindId;
            public Transform Root;
            /// <summary>The hinge at the gate's west end; the gate swings around its up axis (positive angle swings it out to the south).</summary>
            public Transform Gate;
            public Transform Stand;
            public Transform GatePoint;
            public SpriteRenderer Sign;
        }

        internal sealed class ParkLayout
        {
            public Transform Root;
            public float MinX;
            /// <summary>The park rectangle in world x, z.</summary>
            public Rect Rect;
            /// <summary>The z range that is open in the main west fence.</summary>
            public Vector2 EntranceGap;

            public Transform BaseTree;
            public Transform ZonePoint;
            public Transform KeeperHome;
            public Transform CountPoint;
            public Transform PenWalkPoint;
            public Transform[] PartyPoints;

            public readonly List<SpotData> Spots = new List<SpotData>();
            public readonly List<PenData> Pens = new List<PenData>();

            public Vector3 At(float u, float v, float y = 0f) => new Vector3(MinX - u, y, v);
            public float UOf(float x) => MinX - x;
        }

        /// <summary>What Build needs from the rest of the scene.</summary>
        internal struct ZooInputs
        {
            public RuntimeAnimatorController KidController;
            public int KidsLayer;
            public CoolSchool.Player.PlayerController Player;
        }

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The park: grass, own fence, the entrance gap in the main west fence, the base tree, the twelve hiding spots with their poses
        /// and the ten pens. Call after the alien field (it needs the final MinX and the fence) and before the NavMesh is baked.
        /// </summary>
        internal static ParkLayout BuildPark(Transform root, EnvironmentInfo info)
        {
            // A mistake in the park must not break the whole world build: log it loudly and leave the park out. Nothing of the main
            // playground has been removed at that point (see BuildParkCore), so there is no hole.
            try
            {
                return BuildParkCore(root, info);
            }
            catch (System.Exception e)
            {
                Debug.LogError("CoolSchool zoo park: BuildPark failed, so the park is left out of this build: " + e);
                Transform leftover = root.Find("ZooPark");
                if (leftover != null) Object.DestroyImmediate(leftover.gameObject);
                return null;
            }
        }

        static ParkLayout BuildParkCore(Transform root, EnvironmentInfo info)
        {
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Zoo");
            EnsureFolder(MaterialFolder, "Animals");

            var p = new ParkLayout { MinX = info.MinX };
            p.Root = NewGroup("ZooPark", root);
            p.Rect = Rect.MinMaxRect(info.MinX - ParkWidthU, ParkSouthV, info.MinX, ParkNorthV);

            // Keep benches, bins and trees out of the park and out of the walk from the alien zone to the entrance.
            ReservedAreas.Add(p.Rect);
            ReservedAreas.Add(new Rect(info.MinX, EntranceV - 2.5f, 10f, 5f));

            Transform ground = NewGroup("Ground", p.Root);
            Transform fence = NewGroup("Fence", p.Root);
            Transform props = NewGroup("Props", p.Root);

            // Everything is built first; what is taken away from the main playground (its grass west of the fence, the fence pieces in the
            // gap) is only planned and destroyed as the very last step. So an error never leaves a hole in the main world.
            FenceKit kit = MeasureFence(ParkFencePrefab, fence);
            List<GameObject> doomedTiles = BuildParkGrass(p, info, root, ground);
            BuildParkFence(kit, fence, p, info);
            p.EntranceGap = CutEntrance(root, kit, fence, p, info, out List<GameObject> doomedFence);
            CheckEntrance(p, info);

            BuildPoints(p, props);
            p.BaseTree = BuildBaseTree(p, props);
            BuildSpots(p, props);
            BuildPens(p, props, kit);
            BuildDecor(p, props);

            LogSummary(p);

            foreach (GameObject piece in doomedFence)
                if (piece != null) Object.DestroyImmediate(piece);
            foreach (GameObject tile in doomedTiles)
                if (tile != null) Object.DestroyImmediate(tile);
            return p;
        }

        // ---- Points ----

        /// <summary>The zone, the keeper's home, the count point, the pen walk point and the six party points.</summary>
        static void BuildPoints(ParkLayout p, Transform parent)
        {
            Transform group = NewGroup("Points", parent);
            p.ZonePoint = NewPoint("ZonePoint", group, p.At(ZoneU, ZoneV));

            // The keeper waits west-north-west of the zone and faces it (south-east).
            p.KeeperHome = NewPoint("KeeperHome", group, p.At(KeeperU, KeeperV));
            Vector3 toZone = p.ZonePoint.position - p.KeeperHome.position;
            toZone.y = 0f;
            p.KeeperHome.rotation = Quaternion.LookRotation(toZone, Vector3.up);

            // In front of the base tree, where they count.
            p.CountPoint = NewPoint("CountPoint", group, p.At(CountU, CountV));
            p.CountPoint.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // In front of the pens, facing them (north).
            p.PenWalkPoint = NewPoint("PenWalkPoint", group, p.At(PenWalkU, PenWalkV));
            p.PenWalkPoint.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            p.PartyPoints = new Transform[PartyCount];
            for (int i = 0; i < PartyCount; i++)
            {
                float u = Mathf.Lerp(PartyU0, PartyU1, i / (float)(PartyCount - 1));
                p.PartyPoints[i] = NewPoint($"PartyPoint_{i + 1}", group, p.At(u, PartyV));
                p.PartyPoints[i].rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }
        }

        /// <summary>The base tree: ~7 m, its trunk (the prefab's origin) at the spot, a thin trunk collider, the treehouse side to the camera.</summary>
        static Transform BuildBaseTree(ParkLayout p, Transform parent)
        {
            Transform holder = NewGroup("BaseTree", parent);
            GameObject tree = PlaceTree(BaseTreePrefab, holder, p.At(BaseTreeU, BaseTreeV), BaseTreeHeight, 180f, "BaseTree");
            if (tree != null)
            {
                FinishEnvironmentObject(tree);
                return tree.transform;
            }
            return holder;
        }

        /// <summary>
        /// A tree standing on the ground with its trunk (the prefab's origin) at the spot, scaled to the height. Its own colliders are
        /// replaced by a thin trunk, so the kid can walk close under the crown and the NavMesh only avoids the trunk.
        /// </summary>
        static GameObject PlaceTree(PrefabRef reference, Transform parent, Vector3 trunk, float height, float yaw, string treeName)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject tree = Spawn(prefab, parent);
            tree.name = treeName;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (height > 0f && TryGetBounds(tree, out Bounds measured) && measured.size.y > 0.01f)
                tree.transform.localScale *= height / measured.size.y;
            tree.transform.position = new Vector3(trunk.x, 0f, trunk.z);
            if (TryGetBounds(tree, out Bounds bounds)) tree.transform.position += Vector3.up * -bounds.min.y;

            foreach (Collider collider in tree.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            float scale = Mathf.Max(0.01f, tree.transform.lossyScale.y);
            var trunkCollider = tree.AddComponent<CapsuleCollider>();
            trunkCollider.direction = 1;
            trunkCollider.radius = TrunkRadius / scale;
            trunkCollider.height = 3f / scale;
            trunkCollider.center = new Vector3(0f, 1.5f / scale, 0f);
            return tree;
        }

        static void LogSummary(ParkLayout p)
        {
            string spots = string.Join(", ", p.Spots.Select(s =>
                $"{s.Id} ({p.UOf(s.Bounds.center.x):0.0}, {s.Bounds.center.z:0.0})"));
            Vector3 firstPen = p.Pens.Count > 0 && p.Pens[0].Stand != null ? p.Pens[0].Stand.position : Vector3.zero;
            Vector3 lastPen = p.Pens.Count > 0 && p.Pens[p.Pens.Count - 1].Stand != null ? p.Pens[p.Pens.Count - 1].Stand.position : Vector3.zero;
            Debug.Log($"CoolSchool zoo park: rect x {p.Rect.xMin:0.0}..{p.Rect.xMax:0.0}, z {p.Rect.yMin:0.0}..{p.Rect.yMax:0.0}; "
                + $"entrance gap z {p.EntranceGap.x:0.0}..{p.EntranceGap.y:0.0}; zone {p.ZonePoint.position.ToString("0.0")}; "
                + $"base tree {(p.BaseTree != null ? p.BaseTree.position.ToString("0.0") : "missing")}; keeper home {p.KeeperHome.position.ToString("0.0")}; "
                + $"spots (u, v): {spots}; {p.Pens.Count} pens in a row from {firstPen.ToString("0.0")} (elephant) to {lastPen.ToString("0.0")}, "
                + $"pen walk point {p.PenWalkPoint.position.ToString("0.0")}.");
        }

        // ----- Shared helpers (copies of the cove's, so the cove's private ones stay untouched) -----

        internal static Transform NewPoint(string pointName, Transform parent, Vector3 worldPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.position = worldPosition;
            return point;
        }

        internal static Transform NewLocalPoint(string pointName, Transform parent, Vector3 localPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        /// <summary>A primitive in the parent's frame, without its collider (unless asked to keep it).</summary>
        internal static Transform Primitive(PrimitiveType type, string objectName, Transform parent, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, Material material, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A flat URP/Lit material saved under Materials/Zoo (re-made on each build).</summary>
        internal static Material LitMaterial(string materialName, Color colour, float smoothness = 0.3f, float metallic = 0f, float emission = 0f)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Zoo");

            var material = new Material(lit) { name = materialName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            string path = MaterialFolder + "/" + materialName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>True when a prefab with exactly this name exists anywhere in the project.</summary>
        static bool PrefabExists(PrefabRef reference)
        {
            if (string.IsNullOrEmpty(reference.Name)) return false;
            foreach (string guid in AssetDatabase.FindAssets(reference.Name + " t:Prefab"))
                if (System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == reference.Name) return true;
            return false;
        }

        /// <summary>The first reference whose prefab exists; the first one when none does (so LoadPrefab names the missing prefab).</summary>
        static PrefabRef FirstExisting(PrefabRef[] references)
        {
            foreach (PrefabRef reference in references)
                if (PrefabExists(reference)) return reference;
            return references[0];
        }

        /// <summary>The first descendant whose name contains the text (case-insensitive).</summary>
        internal static Transform FindDeep(Transform root, string text)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }

        /// <summary>Gives every mesh without a collider a MeshCollider. No static flags: the object may move (a cover shakes).</summary>
        static void AddMeshCollidersIfNone(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        // ---- Serialized helpers the shared builder does not have ----

        internal static void SetString(Object target, string field, string value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetColor(Object target, string field, Color value)
        {
            if (target == null) return;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
