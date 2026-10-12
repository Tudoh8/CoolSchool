using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The sandpit inside the grass cove: a solid sand base, sand tiles on top, and the playground edging around it.</summary>
    internal static partial class PirateCoveBuilder
    {
        const string MeshFolder = RootFolder + "/Meshes";

        // ---- Grass around the sandpit ----

        /// <summary>
        /// The cove is grass, but not under the sandpit (grass tufts would poke through the sand). The playground's main grass also
        /// reaches into the cove, so every main ground tile that touches the sandpit (plus 0.5 m) or lies wholly inside the cove's outer
        /// rectangle is removed. The cove's own grass then goes in as four strips around the sandpit, stretched to fit, with the
        /// 4 m margin beyond the fence; the strips are widened to cover whatever the removed tiles covered outside the cove (the lawn
        /// next to the school), so no hole opens. The walking surface of the new grass is y = 0 like the rest.
        /// </summary>
        static void BuildCoveGrass(CoveLayout c, EnvironmentInfo info, Transform root, Transform parent)
        {
            Rect pit = c.Pit;
            float x0 = c.SouthWest.x - 4f;
            float x1 = info.MaxX + 4f;
            float z0 = c.SouthWest.z;
            float z1 = c.SouthWest.z + CoveDepth + 4f;
            Rect expanded = Rect.MinMaxRect(pit.xMin - 0.5f, pit.yMin - 0.5f, pit.xMax + 0.5f, pit.yMax + 0.5f);

            // Remove the main tiles under the sandpit and inside the cove.
            float removedMinX = float.MaxValue, removedMaxX = float.MinValue, removedMinZ = float.MaxValue, removedMaxZ = float.MinValue;
            var doomed = new List<GameObject>();
            Transform mainGround = root.Find("Ground");
            if (mainGround != null)
            {
                foreach (Transform tile in mainGround)
                {
                    if (!TryGetBounds(tile.gameObject, out Bounds b)) continue;

                    Rect footprint = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                    bool underPit = footprint.Overlaps(expanded);
                    bool insideCove = footprint.xMin >= x0 - 0.01f && footprint.xMax <= x1 + 0.01f
                        && footprint.yMin >= z0 - 0.01f && footprint.yMax <= z1 + 0.01f;
                    if (!underPit && !insideCove) continue;

                    doomed.Add(tile.gameObject);
                    removedMinX = Mathf.Min(removedMinX, footprint.xMin);
                    removedMaxX = Mathf.Max(removedMaxX, footprint.xMax);
                    removedMinZ = Mathf.Min(removedMinZ, footprint.yMin);
                    removedMaxZ = Mathf.Max(removedMaxZ, footprint.yMax);
                }
            }
            else
            {
                Debug.LogWarning("CoolSchool pirate cove: no 'Ground' group found in the environment, so no main grass tile was removed.");
            }
            foreach (GameObject tile in doomed) Object.DestroyImmediate(tile);

            if (doomed.Count > 0)
            {
                // Cover what the removed tiles covered beyond the cove.
                x0 = Mathf.Min(x0, removedMinX);
                x1 = Mathf.Max(x1, removedMaxX);
                z0 = Mathf.Min(z0, removedMinZ);
                z1 = Mathf.Max(z1, removedMaxZ);
            }
            Debug.Log($"CoolSchool pirate cove: removed {doomed.Count} main ground tiles under the sandpit; "
                + (doomed.Count > 0 ? $"they covered x {removedMinX:0.0}..{removedMaxX:0.0}, z {removedMinZ:0.0}..{removedMaxZ:0.0}; " : "")
                + $"the cove grass covers x {x0:0.0}..{x1:0.0}, z {z0:0.0}..{z1:0.0} around the sandpit.");

            GameObject grass = LoadPrefab(GrassTilePrefab);
            LayTiles(grass, parent, x0, x1, z0, pit.yMin, 0f);              // south
            LayTiles(grass, parent, x0, x1, pit.yMax, z1, 0f);              // north
            LayTiles(grass, parent, x0, pit.xMin, pit.yMin, pit.yMax, 0f);  // west
            LayTiles(grass, parent, pit.xMax, x1, pit.yMin, pit.yMax, 0f);  // east
        }

        // ---- Sand base ----

        /// <summary>
        /// A solid sand quad over the whole sandpit (the sand tile is a ragged patch with holes, so grass would show through) with
        /// the sand tile's own material, and the sand tiles on top for texture. The quad's UVs all sit on one texel of the tile's
        /// atlas, so the base has exactly the colour of the tile.
        /// </summary>
        static void BuildSandpit(CoveLayout c, Transform parent)
        {
            Transform group = NewGroup("Sandpit", parent);
            GameObject tilePrefab = LoadPrefab(SandTilePrefab);
            Rect pit = c.Pit;

            Material sand = null;
            Vector2 uv = Vector2.zero;
            bool haveUv = false;
            if (tilePrefab != null)
            {
                haveUv = TryGetSandUv(ref tilePrefab, out uv);
                sand = CopySandMaterial(tilePrefab);
            }
            if (sand == null)
            {
                Debug.LogWarning("CoolSchool pirate cove: no sand tile material to copy; the sandpit base uses a plain sand colour.");
                sand = LitMaterial("PirateSand", new Color(0.93f, 0.82f, 0.55f), 0.1f);
            }
            else if (!haveUv)
            {
                // The atlas texel could not be read: a plain colour is better than the whole atlas smeared over the quad.
                Debug.LogWarning("CoolSchool pirate cove: could not read the sand tile's UVs; the sandpit base uses a plain sand colour instead of the atlas.");
                sand.SetColor("_BaseColor", new Color(0.93f, 0.82f, 0.55f));
                if (sand.HasProperty("_BaseMap")) sand.SetTexture("_BaseMap", null);
                EditorUtility.SetDirty(sand);
            }

            EnsureFolder(RootFolder, "Meshes");
            string meshPath = MeshFolder + "/PirateSandBase.asset";
            Mesh mesh = CreateFlatQuad(uv);
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            var baseObject = new GameObject("SandBase");
            baseObject.transform.SetParent(group, false);
            baseObject.transform.position = new Vector3(pit.xMin, SandY, pit.yMin);
            baseObject.transform.localScale = new Vector3(pit.width, 1f, pit.height);
            baseObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            baseObject.AddComponent<MeshRenderer>().sharedMaterial = sand;
            baseObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            FinishEnvironmentObject(baseObject);
        }

        /// <summary>A 1 x 1 quad in the XZ plane facing up, from (0,0,0) to (1,0,1), with every UV on the same point.</summary>
        static Mesh CreateFlatQuad(Vector2 uv)
        {
            var mesh = new Mesh { name = "PirateSandBase" };
            mesh.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { uv, uv, uv, uv };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A copy of the sand tile's own material (same shader and atlas), saved as Materials/Pirates/PirateSand.mat.</summary>
        static Material CopySandMaterial(GameObject tilePrefab)
        {
            Renderer renderer = tilePrefab.GetComponentInChildren<Renderer>(true);
            Material source = renderer != null ? renderer.sharedMaterial : null;
            if (source == null) return null;

            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Pirates");
            var copy = new Material(source) { name = "PirateSand" };
            string path = MaterialFolder + "/PirateSand.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>
        /// The UV that the sand tile mostly samples (the most common UV among its vertices; the first vertex's is logged too).
        /// The mesh must be readable; if it is not, Read/Write is switched on in its model importer (once) and the prefab reloaded.
        /// </summary>
        static bool TryGetSandUv(ref GameObject tilePrefab, out Vector2 uv)
        {
            uv = Vector2.zero;
            MeshFilter filter = tilePrefab.GetComponentInChildren<MeshFilter>(true);
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            if (!mesh.isReadable)
            {
                string meshAsset = AssetDatabase.GetAssetPath(mesh);
                var importer = AssetImporter.GetAtPath(meshAsset) as ModelImporter;
                if (importer == null) return false;

                string prefabPath = AssetDatabase.GetAssetPath(tilePrefab);
                Debug.Log($"CoolSchool pirate cove: switching on Read/Write for {meshAsset} so the sand tile's UVs can be read.");
                importer.isReadable = true;
                importer.SaveAndReimport();

                tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                filter = tilePrefab != null ? tilePrefab.GetComponentInChildren<MeshFilter>(true) : null;
                mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) return false;
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
                if (count > bestCount)
                {
                    best = key;
                    bestCount = count;
                }
            }

            uv = new Vector2(best.x / 1000f, best.y / 1000f);
            Debug.Log($"CoolSchool pirate cove: sand tile mesh has {uvs.Length} vertices; most common UV {uv.ToString("0.000")} "
                + $"({bestCount} vertices), first vertex UV {uvs[0].ToString("0.000")}; the sand base uses the most common one.");
            return true;
        }

        // ---- Edging ----

        sealed class EdgePiece
        {
            public string Label;
            public GameObject Prefab;
            public Vector3 Size;
            public Vector3 CentreOffset;
            public bool LongIsX;
            public float Length;
            public float Thickness;
            public float HeightScale = 1f;
            public bool Ok => Prefab != null && Length > 0.01f;
        }

        /// <summary>
        /// The playground edging around the sandpit: an L piece at each corner, straight pieces stretched along each side so every
        /// span is filled exactly, a gap where the dock comes in on the south side (with a post at each end), all squashed to
        /// a step-over height. Static, with the pieces' own colliders. Every piece's measurements are logged.
        /// </summary>
        static void BuildEdging(CoveLayout c, Transform parent)
        {
            Transform group = NewGroup("SandpitEdging", parent);
            EdgePiece straight = MeasureEdge(EdgeStraightPrefab, group, "straight");
            EdgePiece post = MeasureEdge(EdgeCornerPostPrefab, group, "corner post");
            EdgePiece corner = MeasureEdge(EdgeLPrefab, group, "L corner");

            if (!straight.Ok)
            {
                Debug.LogWarning("CoolSchool pirate cove: the straight edging piece is missing, so the sandpit has no edging.");
                return;
            }

            float w = Mathf.Max(0.05f, straight.Thickness);
            c.EdgeWidth = w;
            Rect pit = c.Pit;
            float arm = corner.Ok ? corner.Size.x : 0f;

            // Corners: an L in each, with its outer corner on the sandpit's corner (or a post if there is no L).
            if (corner.Ok)
            {
                PlaceL(corner, group, new Vector2(pit.xMin, pit.yMin), 1, 1);
                PlaceL(corner, group, new Vector2(pit.xMin, pit.yMax), 1, -1);
                PlaceL(corner, group, new Vector2(pit.xMax, pit.yMax), -1, -1);
                PlaceL(corner, group, new Vector2(pit.xMax, pit.yMin), -1, 1);
            }
            else if (post.Ok)
            {
                float half = post.Size.x * 0.5f;
                PlacePost(post, group, new Vector2(pit.xMin + half, pit.yMin + half));
                PlacePost(post, group, new Vector2(pit.xMin + half, pit.yMax - half));
                PlacePost(post, group, new Vector2(pit.xMax - half, pit.yMax - half));
                PlacePost(post, group, new Vector2(pit.xMax - half, pit.yMin + half));
            }

            // The sides, between the corner pieces. The south side has a gap for the dock.
            float gapHalf = DockHalfWidth + 0.2f;
            float south = pit.yMin + w * 0.5f;
            float north = pit.yMax - w * 0.5f;
            float west = pit.xMin + w * 0.5f;
            float east = pit.xMax - w * 0.5f;
            FillEdge(straight, group, new Vector2(pit.xMin + arm, south), new Vector2(c.CentreX - gapHalf, south));
            FillEdge(straight, group, new Vector2(c.CentreX + gapHalf, south), new Vector2(pit.xMax - arm, south));
            FillEdge(straight, group, new Vector2(pit.xMin + arm, north), new Vector2(pit.xMax - arm, north));
            FillEdge(straight, group, new Vector2(west, pit.yMin + arm), new Vector2(west, pit.yMax - arm));
            FillEdge(straight, group, new Vector2(east, pit.yMin + arm), new Vector2(east, pit.yMax - arm));

            if (post.Ok)
            {
                PlacePost(post, group, new Vector2(c.CentreX - gapHalf, south));
                PlacePost(post, group, new Vector2(c.CentreX + gapHalf, south));
            }
        }

        /// <summary>Measures an edging piece on a temporary instance and logs what it looks like in metres.</summary>
        static EdgePiece MeasureEdge(PrefabRef reference, Transform parent, string label)
        {
            var piece = new EdgePiece { Label = label, Prefab = LoadPrefab(reference) };
            if (piece.Prefab == null)
            {
                Debug.LogWarning($"CoolSchool pirate cove edging: {label} ({reference.Name}) is missing.");
                return piece;
            }

            GameObject probe = Spawn(piece.Prefab, parent);
            if (!TryGetBounds(probe, out Bounds bounds))
            {
                Debug.LogWarning($"CoolSchool pirate cove edging: {label} ({reference.Name}) has no renderer.");
                Object.DestroyImmediate(probe);
                piece.Prefab = null;
                return piece;
            }

            piece.Size = bounds.size;
            piece.CentreOffset = bounds.center - probe.transform.position;
            piece.LongIsX = bounds.size.x > bounds.size.z;
            piece.Length = Mathf.Max(bounds.size.x, bounds.size.z);
            piece.Thickness = Mathf.Min(bounds.size.x, bounds.size.z);
            if (bounds.size.y > EdgeMaxHeight) piece.HeightScale = EdgeHeight / bounds.size.y;

            string heavier = "mesh not readable, so which side is heavier is unknown";
            Vector3 centroid = MeshCentroid(probe);
            if (!float.IsNaN(centroid.x))
            {
                Vector3 shift = centroid - bounds.center;
                heavier = piece.LongIsX
                    ? $"material is heavier toward {(shift.z >= 0f ? "+z" : "-z")} by {Mathf.Abs(shift.z):0.000} m across its thickness"
                    : $"material is heavier toward {(shift.x >= 0f ? "+x" : "-x")} by {Mathf.Abs(shift.x):0.000} m across its thickness";
            }

            Debug.Log($"CoolSchool pirate cove edging: {label} {reference.Name} measures {bounds.size.x:0.000} x {bounds.size.y:0.000} x {bounds.size.z:0.000} m "
                + $"(x, y, z); bounds centre {piece.CentreOffset.ToString("0.000")} from its pivot, bottom at y {bounds.min.y - probe.transform.position.y:0.000}, "
                + $"top at y {bounds.max.y - probe.transform.position.y:0.000}; long side along {(piece.LongIsX ? "X" : "Z")} ({piece.Length:0.000} m), "
                + $"thickness {piece.Thickness:0.000} m; {heavier}; height scale used {piece.HeightScale:0.00} "
                + $"(result {bounds.size.y * piece.HeightScale:0.00} m tall).");

            Object.DestroyImmediate(probe);
            return piece;
        }

        /// <summary>The average of all vertex positions in world space; NaN if no mesh is readable.</summary>
        static Vector3 MeshCentroid(GameObject root)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    sum += filter.transform.TransformPoint(vertex);
                    count++;
                }
            }
            return count > 0 ? sum / count : new Vector3(float.NaN, 0f, 0f);
        }

        /// <summary>Straight pieces along a side, from one point to another on its centre line, stretched so the span is filled exactly.</summary>
        static void FillEdge(EdgePiece piece, Transform parent, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float span = delta.magnitude;
            if (span < 0.2f) return;

            Vector2 direction = delta / span;
            bool alongX = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            int count = Mathf.Max(1, Mathf.RoundToInt(span / piece.Length));
            float slot = span / count;
            float stretch = (slot + 0.02f) / piece.Length;
            // First the long side onto Z, then the whole piece turned if the side runs east-west.
            float yaw = (piece.LongIsX ? 90f : 0f) + (alongX ? 90f : 0f);

            for (int i = 0; i < count; i++)
            {
                GameObject go = Spawn(piece.Prefab, parent);
                Vector3 scale = go.transform.localScale;
                if (piece.LongIsX) scale.x *= stretch;
                else scale.z *= stretch;
                scale.y *= piece.HeightScale;
                go.transform.localScale = scale;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                Vector2 centre = from + direction * (slot * (i + 0.5f));
                AlignToBounds(go, centre.x, centre.y, 0f, false);
                FinishEnvironmentObject(go);
            }
        }

        /// <summary>An L piece with its outer corner on the point; its arms run the given directions (+1 / -1 in x and z).</summary>
        static void PlaceL(EdgePiece piece, Transform parent, Vector2 cornerPoint, int armX, int armZ)
        {
            // The arms lie on the side of the pivot that the bounds centre is on.
            int modelX = piece.CentreOffset.x >= 0f ? 1 : -1;
            int modelZ = piece.CentreOffset.z >= 0f ? 1 : -1;
            float yaw = 0f;
            for (int turn = 0; turn < 4; turn++)
            {
                Vector3 turned = Quaternion.Euler(0f, turn * 90f, 0f) * new Vector3(modelX, 0f, modelZ);
                if (Mathf.RoundToInt(turned.x) == armX && Mathf.RoundToInt(turned.z) == armZ)
                {
                    yaw = turn * 90f;
                    break;
                }
            }

            GameObject go = Spawn(piece.Prefab, parent);
            Vector3 scale = go.transform.localScale;
            scale.y *= piece.HeightScale;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (TryGetBounds(go, out Bounds bounds))
            {
                float dx = armX > 0 ? cornerPoint.x - bounds.min.x : cornerPoint.x - bounds.max.x;
                float dz = armZ > 0 ? cornerPoint.y - bounds.min.z : cornerPoint.y - bounds.max.z;
                go.transform.position += new Vector3(dx, -bounds.min.y, dz);
            }
            FinishEnvironmentObject(go);
        }

        /// <summary>A small corner post standing with the middle of its footprint on the point.</summary>
        static void PlacePost(EdgePiece piece, Transform parent, Vector2 point)
        {
            GameObject go = Spawn(piece.Prefab, parent);
            Vector3 scale = go.transform.localScale;
            scale.y *= piece.HeightScale;
            go.transform.localScale = scale;
            AlignToBounds(go, point.x, point.y, 0f, false);
            FinishEnvironmentObject(go);
        }
    }
}
