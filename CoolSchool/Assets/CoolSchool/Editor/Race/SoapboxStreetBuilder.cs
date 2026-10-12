using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the soapbox street north of the playground (contract Docs/specs/soapbox-race-contract.md). Two steps, like the
    /// pirate cove: BuildStreet (static geometry: ground, road, sidewalks, fences, houses, start banner, finish arch, podium;
    /// before the NavMesh is baked) and Build (carts, kids, track items, UI and all the wiring; SoapboxStreetBuilder.Build.cs).
    ///
    /// The street runs straight north (+Z) at x = StreetX. Distance d along the track is world z = StartLineZ + d.
    /// Nothing here relies on pivots: every piece is placed by its measured renderer bounds.
    /// </summary>
    internal static partial class SoapboxStreetBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string RaceFolder = RootFolder + "/Race";
        internal const string MaterialFolder = RootFolder + "/Materials/Race";
        const string MeshFolder = RootFolder + "/Meshes";

        // ---- Layout (metres) ----
        internal const float StreetX = -16f;
        internal const float LaneWidth = 2.2f;
        internal const float StartLineZ = 30f;
        internal const float FinishDistance = 450f;
        internal const float PodiumDistance = 478f;
        internal const float StreetEndZ = 525f;

        const float RoadHalf = 4f;
        const float SidewalkWidth = 2.2f;
        // Layers of the street, bottom to top: grass (1 cm below the playground grass), road, painted lines, sidewalk.
        const float GrassY = -0.01f;
        internal const float RoadY = 0.02f;
        const float LineY = 0.04f;
        internal const float SidewalkY = 0.10f;
        // The picket fence stands 0.4 m outside the sidewalk.
        const float FenceWestX = StreetX - RoadHalf - SidewalkWidth - 0.4f;
        const float FenceEastX = StreetX + RoadHalf + SidewalkWidth + 0.4f;
        const float RoadEndZ = 520f;
        const float EntranceWidth = 4f;
        const float GrassWest = -50f;
        const float GrassEast = 18f;
        const float GrassEndZ = 532f;

        internal static float WestSidewalkX => StreetX - RoadHalf - SidewalkWidth * 0.5f;
        internal static float EastSidewalkX => StreetX + RoadHalf + SidewalkWidth * 0.5f;
        internal static float LaneX(int lane) => StreetX + (lane - 1) * LaneWidth;
        internal static float ZAt(float distance) => StartLineZ + distance;

        static readonly PrefabRef RoadTilePrefab = new PrefabRef("SM_Env_Road_Bare_01", "PolygonCity");
        static readonly PrefabRef RoadTileFallback = new PrefabRef("SM_Env_Road_01", "PolygonTown");
        static readonly PrefabRef GrassTilePrefab = Kids("SM_Env_Ground_Tile_Grass");
        static readonly PrefabRef PicketFencePrefab = new PrefabRef("SM_Env_Fence_White_Straight_01", "PolygonTown");
        static readonly PrefabRef PicketFenceFallback = Kids("SM_Env_Park_Fence_01");
        static readonly PrefabRef HedgePrefabA = new PrefabRef("SM_Env_Hedge_01", "PolygonTown");
        static readonly PrefabRef HedgePrefabB = new PrefabRef("SM_Env_Hedge_02", "PolygonTown");
        static readonly PrefabRef HedgePrefabC = new PrefabRef("SM_Env_Hedge_03", "PolygonTown");

        // Racing-red beacon with a flag, a trophy or a car on it, whichever the UI kit has first.
        internal static readonly ActivityBeaconBuilder.Style RaceBeacon = new ActivityBeaconBuilder.Style(
            "Race", new Color(2.2f, 0.45f, 0.35f, 1f), new[] { "Flag", "Trophy", "Car", "Race", "Map-Target-Circle" });

        /// <summary>Everything the second step needs from the first. All positions are in world space.</summary>
        internal sealed class StreetLayout
        {
            public Transform Root;
            /// <summary>The street area (x, z) that is reserved against benches and trees.</summary>
            public Rect Area;
            /// <summary>The x range that is open in the main north fence.</summary>
            public Vector2 EntranceGap;
            public Transform StartLine;
            public Vector3 ZonePosition;
            public Transform ReturnPoint;
            public Transform AnnouncerStart;
            public Transform AnnouncerFinish;
            public Transform[] PodiumSpots;
            public Transform PodiumCameraSpot;
            public Transform FinishArchRoot;
            public Transform FinishBanner;
            public Transform[] SpectatorSpots;
            public int HouseCount;
            public int HouseRenderers;
            public int StreetRenderers;
        }

        // Materials made during one build are reused (a second DeleteAsset/CreateAsset would orphan the first one's users).
        static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The street: grass, road with lane lines, sidewalks, picket fences along both sides and across the north end, the gap in
        /// the main north fence, houses and gardens, the start banner, the finish arch and the podium.
        /// Call after the main fence, the school and the pirate cove exist and before the NavMesh is baked.
        /// </summary>
        internal static StreetLayout BuildStreet(Transform root, EnvironmentInfo info)
        {
            MaterialCache.Clear();
            chequerTexture = null;
            EnsureFolder(RootFolder, "Race");
            EnsureFolder(RaceFolder, "Prefabs");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Race");
            EnsureFolder(MaterialFolder, "Animals");
            EnsureFolder(RootFolder, "Meshes");

            var layout = new StreetLayout();
            layout.Root = NewGroup("RaceStreet", root);
            float z0 = info.MaxZ;

            if (info.School != null && info.School.footprint.size.sqrMagnitude > 0f
                && info.School.footprint.max.x > FenceEastX - 2f && info.School.footprint.min.z < 60f)
            {
                Debug.LogWarning($"CoolSchool race street: the school's footprint reaches x {info.School.footprint.max.x:0.0}; "
                    + $"the street's east fence is at x {FenceEastX:0.0} (wanted at least 2 m clear).");
            }

            // Benches and trees stay out of the street and out of the walk from the playground to the entrance.
            layout.Area = Rect.MinMaxRect(StreetX - 30f, z0, StreetX + 14f, 530f);
            ReservedAreas.Add(layout.Area);
            ReservedAreas.Add(new Rect(StreetX - 2.5f, z0 - 9f, 5f, 9f));

            Transform ground = NewGroup("Ground", layout.Root);
            Transform fence = NewGroup("Fence", layout.Root);
            Transform props = NewGroup("Props", layout.Root);

            Rect removedGrass = RemoveMainGrass(root, z0);
            BuildGround(layout, z0, ground, removedGrass);
            BuildLaneLines(ground);
            BuildFences(fence, z0);
            layout.EntranceGap = CutEntrance(root, info);

            BuildStartArea(layout, props);
            BuildFinishArea(layout, props);
            BuildHouses(layout, props, z0);
            BuildStreetFurniture(layout, props, z0);

            layout.StreetRenderers = layout.Root.GetComponentsInChildren<Renderer>(true).Length;
            string problems = string.Join("; ", SoapboxTrackLayout.Validate());
            if (problems.Length > 0) Debug.LogWarning("CoolSchool race street: the track table breaks a contract rule: " + problems);

            Debug.Log($"CoolSchool race street: x {FenceWestX:0.0}..{FenceEastX:0.0} (road {StreetX - RoadHalf:0.0}..{StreetX + RoadHalf:0.0}), z {z0:0.0}..{StreetEndZ:0.0}; "
                + $"start line z {StartLineZ:0.0}, finish z {ZAt(FinishDistance):0.0}, podium z {ZAt(PodiumDistance):0.0}; "
                + $"entrance gap x {layout.EntranceGap.x:0.0}..{layout.EntranceGap.y:0.0}; zone {layout.ZonePosition.ToString("0.0")}; "
                + $"{layout.HouseCount} houses ({layout.HouseRenderers} renderers); {layout.StreetRenderers} renderers in the street.");
            return layout;
        }

        // ---- Ground ----

        /// <summary>
        /// The playground's main grass (one grid under the fence rectangle plus the school's footprint, margins included) reaches under
        /// the street, and its lumpy tiles poke through the road and sidewalks. Every main grass tile that overlaps the street's x range
        /// and reaches north of the main fence line is deleted; the street's own flat ground covers the same area (BuildGround fills the part
        /// inside the fence). Returns the footprint of the removed tiles (width 0 when none).
        /// </summary>
        static Rect RemoveMainGrass(Transform root, float z0)
        {
            Transform ground = root.Find("Ground");
            if (ground == null)
            {
                Debug.LogWarning("CoolSchool race street: no 'Ground' group in the environment, so no main grass was removed under the street.");
                return new Rect();
            }

            var doomed = new List<GameObject>();
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Transform tile in ground)
            {
                if (!tile.name.Contains("Grass") || !TryGetBounds(tile.gameObject, out Bounds b)) continue;
                if (b.max.x <= FenceWestX || b.min.x >= FenceEastX || b.max.z <= z0 + 0.05f) continue;

                doomed.Add(tile.gameObject);
                minX = Mathf.Min(minX, b.min.x);
                maxX = Mathf.Max(maxX, b.max.x);
                minZ = Mathf.Min(minZ, b.min.z);
                maxZ = Mathf.Max(maxZ, b.max.z);
            }
            foreach (GameObject tile in doomed) Object.DestroyImmediate(tile);

            if (doomed.Count == 0)
            {
                Debug.Log("CoolSchool race street: no main grass tile reached under the street.");
                return new Rect();
            }
            Debug.Log($"CoolSchool race street: removed {doomed.Count} main grass tiles under the street (x {minX:0.0}..{maxX:0.0}, z {minZ:0.0}..{maxZ:0.0}).");
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>
        /// Grass on both sides (a flat quad with the grass tile's own material, all UVs on its main colour), the road (the same trick
        /// with the Synty bare road tile), and a sidewalk slab on each side. Walkable surfaces are single box colliders, so the NavMesh
        /// and the kid walk on perfectly flat ground.
        /// </summary>
        static void BuildGround(StreetLayout layout, float z0, Transform parent, Rect removedGrass)
        {
            float zStart = z0 + 1f;

            // Grass west and east. The playground's own grass reaches 4 m past its fence; ours is 1 cm lower, so nothing fights.
            Mesh grassMesh;
            Material grassMaterial;
            if (!TryMakeSwatch(GrassTilePrefab, "RaceGrass", out grassMaterial, out grassMesh))
            {
                grassMaterial = Mat("RaceGrassFallback", new Color(0.42f, 0.7f, 0.25f), 0.05f);
                grassMesh = MakeQuad("RaceGrass", Vector2.zero);
            }
            Quad(parent, "GrassWest", grassMesh, grassMaterial, GrassWest, GrassY, zStart - 1f, StreetX - RoadHalf - SidewalkWidth, GrassEndZ);
            Quad(parent, "GrassEast", grassMesh, grassMaterial, StreetX + RoadHalf + SidewalkWidth, GrassY, zStart - 1f, GrassEast, GrassEndZ);
            Surface(parent, "GrassWestCollider", Rect.MinMaxRect(GrassWest, zStart - 1f, StreetX - RoadHalf - SidewalkWidth, GrassEndZ), GrassY);
            Surface(parent, "GrassEastCollider", Rect.MinMaxRect(StreetX + RoadHalf + SidewalkWidth, zStart - 1f, GrassEast, GrassEndZ), GrassY);

            // Where main grass tiles were removed (they straddled the main north fence line), our flat grass covers the part of their
            // footprint that lies inside the fence, level with the playground grass (y 0), so there is no hole beside the entrance.
            if (removedGrass.width > 0.01f && removedGrass.yMin < zStart - 0.01f)
            {
                var fill = Rect.MinMaxRect(removedGrass.xMin, removedGrass.yMin, removedGrass.xMax, zStart);
                Quad(parent, "GrassFill", grassMesh, grassMaterial, fill.xMin, 0f, fill.yMin, fill.xMax, fill.yMax);
                Surface(parent, "GrassFillCollider", fill, 0f);
                Debug.Log($"CoolSchool race street: flat grass fill x {fill.xMin:0.0}..{fill.xMax:0.0}, z {fill.yMin:0.0}..{fill.yMax:0.0} where the main tiles were removed.");
            }

            // Road.
            Mesh roadMesh;
            Material roadMaterial;
            if (!TryMakeSwatch(RoadTilePrefab, "RaceRoad", out roadMaterial, out roadMesh) && !TryMakeSwatch(RoadTileFallback, "RaceRoad", out roadMaterial, out roadMesh))
            {
                Debug.LogWarning("CoolSchool race street: no Synty road tile found, so the road is plain dark grey.");
                roadMaterial = Mat("RaceRoadFallback", new Color(0.27f, 0.28f, 0.3f), 0.1f);
                roadMesh = MakeQuad("RaceRoad", Vector2.zero);
            }
            Rect road = Rect.MinMaxRect(StreetX - RoadHalf, zStart, StreetX + RoadHalf, RoadEndZ);
            Quad(parent, "Road", roadMesh, roadMaterial, road.xMin, RoadY, road.yMin, road.xMax, road.yMax);
            Surface(parent, "RoadCollider", road, RoadY);

            // Sidewalks: light slabs with a 8 cm kerb against the road.
            Material pavement = Mat("RaceSidewalk", new Color(0.82f, 0.8f, 0.75f), 0.15f);
            foreach (float sign in new[] { -1f, 1f })
            {
                float inner = StreetX + sign * RoadHalf;
                float outer = StreetX + sign * (RoadHalf + SidewalkWidth);
                var rect = Rect.MinMaxRect(Mathf.Min(inner, outer), zStart, Mathf.Max(inner, outer), RoadEndZ);
                string side = sign < 0f ? "West" : "East";

                GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Sidewalk" + side;
                Object.DestroyImmediate(slab.GetComponent<Collider>());
                slab.transform.SetParent(parent, false);
                float thickness = SidewalkY - GrassY + 0.2f;
                slab.transform.position = new Vector3(rect.center.x, SidewalkY - thickness * 0.5f, rect.center.y);
                slab.transform.localScale = new Vector3(rect.width, thickness, rect.height);
                slab.GetComponent<MeshRenderer>().sharedMaterial = pavement;
                MarkStatic(slab);
                Surface(parent, "Sidewalk" + side + "Collider", rect, SidewalkY);
            }

            // The tile used for the street goes to the road quad only; the sidewalk kerb across the entrance stays open (a ramp of grass).
        }

        /// <summary>One flat quad (mesh from MakeQuad, 1 x 1 in the XZ plane) stretched over a rectangle at height y.</summary>
        static void Quad(Transform parent, string name, Mesh mesh, Material material, float xMin, float y, float zMin, float xMax, float zMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(xMin, y, zMin);
            go.transform.localScale = new Vector3(xMax - xMin, 1f, zMax - zMin);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            MarkStatic(go);
        }

        /// <summary>An invisible box collider whose top is at topY: the walkable ground over the rectangle.</summary>
        static void Surface(Transform parent, string name, Rect rect, float topY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            const float thickness = 0.5f;
            go.transform.position = new Vector3(rect.center.x, topY - thickness * 0.5f, rect.center.y);
            go.AddComponent<BoxCollider>().size = new Vector3(rect.width, thickness, rect.height);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>An invisible wall (box collider) from one point to another, height metres high.</summary>
        static void Wall(Transform parent, string name, Vector2 from, Vector2 to, float height, float thickness = 0.4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector2 mid = (from + to) * 0.5f;
            Vector2 delta = to - from;
            go.transform.position = new Vector3(mid.x, height * 0.5f, mid.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y).normalized, Vector3.up);
            go.AddComponent<BoxCollider>().size = new Vector3(thickness, height, delta.magnitude);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// A quad mesh asset (0,0,0)..(1,0,1) facing up with every UV on one point: all of it samples one texel of the Synty
        /// atlas, so it has exactly the colour of the tile.
        /// </summary>
        static Mesh MakeQuad(string name, Vector2 uv)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { uv, uv, uv, uv };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return SaveMesh(mesh, name);
        }

        internal static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshFolder + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        /// <summary>
        /// A copy of a Synty tile's material and a quad whose UVs sit on the tile's most common texel (its main surface colour).
        /// The mesh must be readable; if it is not, Read/Write is switched on once in its model importer.
        /// </summary>
        static bool TryMakeSwatch(PrefabRef reference, string name, out Material material, out Mesh quad)
        {
            material = null;
            quad = null;
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return false;

            Renderer renderer = prefab.GetComponentInChildren<Renderer>(true);
            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
            if (renderer == null || renderer.sharedMaterial == null || filter == null || filter.sharedMesh == null) return false;

            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                string meshAsset = AssetDatabase.GetAssetPath(mesh);
                var importer = AssetImporter.GetAtPath(meshAsset) as ModelImporter;
                if (importer == null) return false;

                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                Debug.Log($"CoolSchool race street: switching on Read/Write for {meshAsset} so the tile's UVs can be read.");
                importer.isReadable = true;
                importer.SaveAndReimport();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
                mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) return false;
                renderer = prefab.GetComponentInChildren<Renderer>(true);
            }

            Vector2[] uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return false;

            var counts = new Dictionary<Vector2Int, int>();
            Vector2Int best = Vector2Int.zero;
            int bestCount = 0;
            foreach (Vector2 sample in uvs)
            {
                var key = new Vector2Int(Mathf.RoundToInt(sample.x * 1000f), Mathf.RoundToInt(sample.y * 1000f));
                counts.TryGetValue(key, out int count);
                counts[key] = ++count;
                if (count > bestCount) { best = key; bestCount = count; }
            }
            var uv = new Vector2(best.x / 1000f, best.y / 1000f);
            Debug.Log($"CoolSchool race street: {reference.Name}: {uvs.Length} vertices, most common UV {uv.ToString("0.000")} ({bestCount} vertices).");

            var copy = new Material(renderer.sharedMaterial) { name = name };
            string path = MaterialFolder + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            quad = MakeQuad(name, uv);
            return true;
        }

        // ---- Lane lines, start and finish line ----

        /// <summary>
        /// White road paint as ONE mesh: solid edge lines, dashed lines between the lanes, the start line (a white bar) and the
        /// finish line (chequered, its own small quad).
        /// </summary>
        static void BuildLaneLines(Transform parent)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Rect4(float x0, float x1, float z0, float z1)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3(x0, 0f, z0));
                vertices.Add(new Vector3(x0, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z1));
                vertices.Add(new Vector3(x1, 0f, z0));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            float zStart = StartLineZ - 12f;
            float zEnd = RoadEndZ - 2f;
            // Solid edge lines 0.4 m inside the kerb.
            foreach (float sign in new[] { -1f, 1f })
            {
                float x = StreetX + sign * (RoadHalf - 0.35f);
                Rect4(x - 0.06f, x + 0.06f, zStart, zEnd);
            }
            // Dashes between the lanes: 1.6 m dash, 1.6 m gap.
            foreach (float x in new[] { StreetX - LaneWidth * 0.5f, StreetX + LaneWidth * 0.5f })
                for (float z = zStart; z < zEnd - 1.6f; z += 3.2f)
                    Rect4(x - 0.06f, x + 0.06f, z, z + 1.6f);
            // The start line.
            Rect4(StreetX - 3.4f, StreetX + 3.4f, StartLineZ - 0.15f, StartLineZ + 0.15f);

            var mesh = new Mesh { name = "RaceLaneLines", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(vertices.Select(v => Vector3.up).ToList());
            mesh.SetUVs(0, vertices.Select(v => Vector2.zero).ToList());
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, "RaceLaneLines");

            var lines = new GameObject("LaneLines");
            lines.transform.SetParent(parent, false);
            lines.transform.position = new Vector3(0f, LineY, 0f);
            lines.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = lines.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat("RaceLine", new Color(0.97f, 0.97f, 0.94f), 0.1f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            MarkStatic(lines);

            // The chequered finish line.
            float finishZ = ZAt(FinishDistance);
            var chequer = new Mesh { name = "RaceFinishLine" };
            float x0 = StreetX - 3.4f, x1 = StreetX + 3.4f, fz0 = finishZ - 0.4f, fz1 = finishZ + 0.4f;
            chequer.vertices = new[] { new Vector3(x0, 0f, fz0), new Vector3(x0, 0f, fz1), new Vector3(x1, 0f, fz1), new Vector3(x1, 0f, fz0) };
            chequer.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            float repeats = (x1 - x0) / 0.8f;
            chequer.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(repeats, 1f), new Vector2(repeats, 0f) };
            chequer.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            chequer.RecalculateBounds();
            chequer = SaveMesh(chequer, "RaceFinishLine");

            var finish = new GameObject("FinishLine");
            finish.transform.SetParent(parent, false);
            finish.transform.position = new Vector3(0f, LineY + 0.005f, 0f);
            finish.AddComponent<MeshFilter>().sharedMesh = chequer;
            var finishRenderer = finish.AddComponent<MeshRenderer>();
            finishRenderer.sharedMaterial = ChequerMaterial();
            finishRenderer.shadowCastingMode = ShadowCastingMode.Off;
            MarkStatic(finish);
        }

        // ---- Fences ----

        /// <summary>
        /// A white picket fence along both sides of the street and across its north end (behind a hedge), with invisible walls under
        /// it, so nobody walks out of the world. The fence pieces themselves have no colliders.
        /// </summary>
        static void BuildFences(Transform parent, float z0)
        {
            GameObject fence = LoadPrefab(PicketFencePrefab) ?? LoadPrefab(PicketFenceFallback);
            var west = new Vector2(FenceWestX, z0 + 0.3f);
            var westEnd = new Vector2(FenceWestX, StreetEndZ + 1f);
            var east = new Vector2(FenceEastX, z0 + 0.3f);
            var eastEnd = new Vector2(FenceEastX, StreetEndZ + 1f);
            var northA = new Vector2(FenceWestX, StreetEndZ + 1f);
            var northB = new Vector2(FenceEastX, StreetEndZ + 1f);

            FenceRun(fence, parent, west, westEnd, 0f);
            FenceRun(fence, parent, east, eastEnd, 0f);
            FenceRun(fence, parent, northA, northB, 0f);

            Wall(parent, "WallWest", west, westEnd, 2f);
            Wall(parent, "WallEast", east, eastEnd, 2f);
            Wall(parent, "WallNorth", northA, northB, 2f);

            // The end of the street: a hedge wall across the road.
            GameObject hedge = LoadPrefab(HedgePrefabA) ?? LoadPrefab(HedgePrefabB) ?? LoadPrefab(HedgePrefabC);
            float hedgeZ = RoadEndZ + 1f;
            FenceRun(hedge, parent, new Vector2(FenceWestX + 0.3f, hedgeZ), new Vector2(FenceEastX - 0.3f, hedgeZ), RoadY, true);
            Wall(parent, "WallStreetEnd", new Vector2(FenceWestX, hedgeZ), new Vector2(FenceEastX, hedgeZ), 2f, 1.2f);
        }

        /// <summary>
        /// Straight pieces along a line from a to b (the piece's long side along the line), stretched slightly so they meet.
        /// Colliders are removed. Returns how many pieces were placed.
        /// </summary>
        internal static int FenceRun(GameObject prefab, Transform parent, Vector2 a, Vector2 b, float footY, bool tightFit = false)
        {
            if (prefab == null) return 0;

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds))
            {
                Object.DestroyImmediate(probe);
                return 0;
            }
            Vector3 baseScale = probe.transform.localScale;
            Object.DestroyImmediate(probe);

            bool longIsX = probeBounds.size.x >= probeBounds.size.z;
            float length = Mathf.Max(probeBounds.size.x, probeBounds.size.z);
            if (length < 0.2f) return 0;

            Vector2 delta = b - a;
            float total = delta.magnitude;
            int count = Mathf.Max(1, Mathf.RoundToInt(total / length));
            float spacing = total / count;
            float stretch = spacing / length;
            if (!tightFit && (stretch < 0.8f || stretch > 1.25f)) stretch = 1f;
            Vector3 scale = baseScale;
            if (longIsX) scale.x *= stretch;
            else scale.z *= stretch;

            Vector3 along = new Vector3(delta.x, 0f, delta.y).normalized;
            Quaternion rotation = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, longIsX ? -90f : 0f, 0f);

            for (int i = 0; i < count; i++)
            {
                Vector2 point = a + delta.normalized * (spacing * (i + 0.5f));
                GameObject piece = Spawn(prefab, parent);
                piece.transform.localScale = scale;
                piece.transform.rotation = rotation;
                AlignToBounds(piece, point.x, point.y, footY, false);
                StripColliders(piece);
                MarkStatic(piece);
            }
            return count;
        }

        /// <summary>
        /// Removes the main north fence pieces that overlap the entrance gap (4 m, centred on the street) and logs whether the ice cream
        /// stall's or any other reserved area is in the way. Returns the x range that is open afterwards.
        /// </summary>
        static Vector2 CutEntrance(Transform root, EnvironmentInfo info)
        {
            float gapMin = StreetX - EntranceWidth * 0.5f;
            float gapMax = StreetX + EntranceWidth * 0.5f;
            var opened = new Vector2(gapMin, gapMax);

            Transform fenceGroup = root.Find("Fence");
            if (fenceGroup == null)
            {
                Debug.LogWarning("CoolSchool race street: no 'Fence' group found, so the entrance gap was not cut.");
                return opened;
            }

            var doomed = new List<GameObject>();
            foreach (Transform piece in fenceGroup)
            {
                if (!TryGetBounds(piece.gameObject, out Bounds bounds)) continue;

                bool onNorthLine = bounds.max.z > info.MaxZ - 1.2f && bounds.min.z < info.MaxZ + 1.2f;
                bool inGap = bounds.max.x > gapMin + 0.05f && bounds.min.x < gapMax - 0.05f;
                if (!onNorthLine || !inGap) continue;

                doomed.Add(piece.gameObject);
                opened.x = Mathf.Min(opened.x, bounds.min.x);
                opened.y = Mathf.Max(opened.y, bounds.max.x);
            }

            if (doomed.Count == 0)
                Debug.LogWarning($"CoolSchool race street: no main fence piece found in the entrance gap x {gapMin:0.0}..{gapMax:0.0}.");
            foreach (GameObject piece in doomed) Object.DestroyImmediate(piece);

            // Any area reserved by the stall, the castle or the cove that touches the gap (the street's own two are skipped).
            var gap = Rect.MinMaxRect(opened.x, info.MaxZ - 3f, opened.y, info.MaxZ + 3f);
            int overlaps = 0;
            foreach (Rect area in ReservedAreas)
            {
                bool ours = Mathf.Approximately(area.xMin, StreetX - 30f) || Mathf.Approximately(area.xMin, StreetX - 2.5f);
                if (!ours && area.Overlaps(gap))
                {
                    overlaps++;
                    Debug.LogWarning($"CoolSchool race street: a reserved area (x {area.xMin:0.0}..{area.xMax:0.0}, z {area.yMin:0.0}..{area.yMax:0.0}) overlaps the entrance gap.");
                }
            }
            Debug.Log($"CoolSchool race street: entrance gap x {opened.x:0.0}..{opened.y:0.0}, {doomed.Count} fence pieces removed, {overlaps} reserved areas in the way"
                + (info.Shop != null ? $" (ice cream stall zone at {info.Shop.ZonePosition.ToString("0.0")})." : "."));
            return opened;
        }

        // ----- Shared helpers ------------------------------------------------------------------

        internal enum Fit { None, Height, Widest, Longest }

        /// <summary>
        /// A prefab uniformly scaled so its height or widest/longest horizontal side is targetSize, turned by yaw and stood on footY
        /// with the middle of its footprint at the point. Colliders are removed unless asked for. Null if the prefab is missing.
        /// </summary>
        internal static GameObject Place(PrefabRef reference, Transform parent, float yaw, Fit fit, float targetSize, Vector3 point, float footY,
            bool keepColliders = false, bool isStatic = true)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;
            return Place(prefab, parent, yaw, fit, targetSize, point, footY, keepColliders, isStatic);
        }

        internal static GameObject Place(GameObject prefab, Transform parent, float yaw, Fit fit, float targetSize, Vector3 point, float footY,
            bool keepColliders = false, bool isStatic = true)
        {
            GameObject go = Spawn(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (fit != Fit.None && TryGetBounds(go, out Bounds bounds))
            {
                float measured = fit == Fit.Height ? bounds.size.y
                    : fit == Fit.Widest ? Mathf.Max(bounds.size.x, bounds.size.z)
                    : Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (measured > 0.01f) go.transform.localScale *= Mathf.Clamp(targetSize / measured, 0.02f, 50f);
            }
            AlignToBounds(go, point.x, point.z, footY, false);
            if (!keepColliders) StripColliders(go);
            if (isStatic) MarkStatic(go);
            return go;
        }

        internal static void StripColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != null) Object.DestroyImmediate(collider);
        }

        internal static void MarkStatic(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(child.gameObject) | StaticEditorFlags.BatchingStatic);
        }

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

        /// <summary>A primitive in the parent's frame, without its collider (unless asked), without shadows (unless asked).</summary>
        internal static Transform Prim(PrimitiveType type, string objectName, Transform parent, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, Material material, bool keepCollider = false, bool shadows = false)
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
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A flat URP/Lit material saved under Materials/Race; the same name returns the same asset within one build.</summary>
        internal static Material Mat(string materialName, Color colour, float smoothness = 0.3f, float metallic = 0f, float emission = 0f)
        {
            if (MaterialCache.TryGetValue(materialName, out Material cached) && cached != null) return cached;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

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
            return SaveMaterial(material, materialName);
        }

        /// <summary>A see-through URP/Lit material (water, jets).</summary>
        internal static Material MatTransparent(string materialName, Color colour, float smoothness = 0.7f)
        {
            if (MaterialCache.TryGetValue(materialName, out Material cached) && cached != null) return cached;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            var material = new Material(lit) { name = materialName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            BattleEffectsBuilder.SetAlphaBlended(material);
            return SaveMaterial(material, materialName);
        }

        static Material SaveMaterial(Material material, string materialName)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            Material saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            MaterialCache[materialName] = saved;
            return saved;
        }

        // ---- The chequered texture ----

        const string ChequerPath = RaceFolder + "/Chequer.png";

        /// <summary>Assets/CoolSchool/Race/Chequer.png: a 2 x 2 black and white pattern (repeating), generated on every build.</summary>
        static Texture2D chequerTexture;

        static Texture2D ChequerTexture()
        {
            if (chequerTexture != null) return chequerTexture;

            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    texture.SetPixel(x, y, ((x / 32) + (y / 32)) % 2 == 0 ? new Color(0.06f, 0.06f, 0.07f) : new Color(0.97f, 0.97f, 0.95f));
            texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(ChequerPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(ChequerPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ChequerPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            chequerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ChequerPath);
            return chequerTexture;
        }

        /// <summary>The chequered material (URP/Lit with the generated texture; tiling comes from the mesh's UVs).</summary>
        internal static Material ChequerMaterial()
        {
            const string name = "RaceChequer";
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null) return cached;

            Material material = Mat(name, Color.white, 0.15f);
            if (material == null) return null;
            material.SetTexture("_BaseMap", ChequerTexture());
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The chequered banner material for a board of the given size: one 2 x 2 pattern per ~0.9 m.</summary>
        internal static Material ChequerBannerMaterial(string name, float width, float height)
        {
            if (MaterialCache.TryGetValue(name, out Material cached) && cached != null) return cached;

            Material material = Mat(name, Color.white, 0.15f);
            if (material == null) return null;
            material.SetTexture("_BaseMap", ChequerTexture());
            material.SetTextureScale("_BaseMap", new Vector2(Mathf.Max(1f, Mathf.Round(width / 0.9f)), Mathf.Max(1f, Mathf.Round(height / 0.9f))));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
