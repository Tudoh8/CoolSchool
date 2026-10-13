using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// What the school and the Toy Room share: the Synty Generic modular wall kit (pieces measured from probe instances, so the
    /// tiling does not depend on the pack's pivots), tile laying, prop placement by renderer bounds, and small anchor helpers.
    /// Orientation assumptions (measured from the meshes where possible): a wall segment runs along its local X, its thin axis is
    /// Z, and the plaster side (the inside) faces local +Z. Props face local +Z.
    /// This code used to live in <see cref="SchoolBuilder"/> and was moved here unchanged.
    /// </summary>
    internal static class ModularBuilding
    {
        /// <summary>The door opening must be at least this wide (metres) for the kid and the NavMesh agent.</summary>
        const float MinDoorWidth = 1.6f;

        /// <summary>The NavMesh agent is 2 m tall; the door needs a little more.</summary>
        const float MinDoorHeight = 2.05f;

        /// <summary>The floor sits a hair above the grass so the two never flicker.</summary>
        internal const float FloorY = 0.02f;

        const string GenericPack = "PolygonGeneric";

        // ----- Prefabs (resolved by name) ------------------------------------------------------

        static readonly PrefabRef WallPrefab = new PrefabRef("SM_Bld_Base_Wall_01", GenericPack);
        static readonly PrefabRef WindowPrefab = new PrefabRef("SM_Bld_Base_Wall_Window_01", GenericPack);
        static readonly PrefabRef FloorPrefab = new PrefabRef("SM_Bld_Base_Floor_01", GenericPack);
        static readonly PrefabRef CeilingPrefab = new PrefabRef("SM_Bld_Base_Ceiling_01", GenericPack);
        static readonly PrefabRef PillarPrefab = new PrefabRef("SM_Bld_Base_Pillar_01", GenericPack);

        /// <summary>
        /// Doorway pieces in order of preference. The "Large" double door has a 2.2 m x 2.5 m opening; the plain double
        /// door is 2.0 m wide but only 1.97 m tall, which a 2 m NavMesh agent can't pass. These are wall pieces with a hole:
        /// the door leaves are separate prefabs we do not place, so nothing blocks the opening.
        /// </summary>
        static readonly PrefabRef[] DoorPrefabs =
        {
            new PrefabRef("SM_Bld_Base_Wall_Door_Double_Large_01", GenericPack),
            new PrefabRef("SM_Bld_Base_Wall_Door_Double_01", GenericPack),
            new PrefabRef("SM_Bld_Base_Wall_Door_Large_01", GenericPack),
        };

        // ----- Data ----------------------------------------------------------------------------

        /// <summary>One modular piece, measured from a probe instance.</summary>
        internal sealed class Piece
        {
            public string Name;
            public GameObject Prefab;
            public Vector3 Size;
            public bool LengthAlongX = true;
            /// <summary>True if the plaster (inside) side faces local -Z, so the piece needs a half turn.</summary>
            public bool InsideOnNegativeZ;
            public float OpeningWidth;
            public float OpeningHeight;

            /// <summary>Turns the piece so that its thin axis points along the facing and its inside side faces that way.</summary>
            public Quaternion Fix
            {
                get
                {
                    Quaternion fix = LengthAlongX ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f);
                    return InsideOnNegativeZ ? fix * Quaternion.Euler(0f, 180f, 0f) : fix;
                }
            }

            public float Length => LengthAlongX ? Size.x : Size.z;
            public float Thickness => LengthAlongX ? Size.z : Size.x;
        }

        internal sealed class Kit
        {
            public Piece Wall, Window, Door, Floor, Ceiling, Pillar;
        }

        // ----- Measuring -----------------------------------------------------------------------

        /// <summary>
        /// Spawns probes of the modular pieces, measures them and removes the probes again. Null if essentials are missing.
        /// buildingName is only used in the log lines ("the school will have no door").
        /// </summary>
        internal static Kit MeasureKit(Transform scratch, string buildingName = "school")
        {
            var kit = new Kit
            {
                Wall = MeasurePiece(WallPrefab, scratch, false),
                Window = MeasurePiece(WindowPrefab, scratch, false),
                Floor = MeasurePiece(FloorPrefab, scratch, false),
                Ceiling = MeasurePiece(CeilingPrefab, scratch, false),
                Pillar = MeasurePiece(PillarPrefab, scratch, false),
                Door = ChooseDoor(scratch),
            };

            if (kit.Wall == null || kit.Floor == null) return null;
            if (kit.Door == null)
                Debug.LogError($"CoolSchool: no doorway wall piece found (SM_Bld_Base_Wall_Door_Double_Large_01 and friends); the {buildingName} will have no door!");

            foreach (Piece piece in new[] { kit.Window, kit.Door })
            {
                if (piece == null) continue;
                if (Mathf.Abs(piece.Length - kit.Wall.Length) > kit.Wall.Length * 0.05f)
                    Debug.LogWarning($"CoolSchool: {piece.Name} is {piece.Length:0.00} m long but the plain wall is {kit.Wall.Length:0.00} m; the tiling will not meet exactly.");
            }

            if (!kit.Wall.LengthAlongX)
                Debug.LogWarning($"CoolSchool: the wall segment runs along Z, not X; turning every wall piece a quarter. Check the {buildingName}'s walls.");
            return kit;
        }

        /// <summary>The first doorway piece whose opening is wide and tall enough; otherwise the widest one found.</summary>
        static Piece ChooseDoor(Transform scratch)
        {
            Piece best = null;
            foreach (PrefabRef reference in DoorPrefabs)
            {
                Piece piece = MeasurePiece(reference, scratch, true);
                if (piece == null) continue;

                if (piece.OpeningWidth >= MinDoorWidth && piece.OpeningHeight >= MinDoorHeight) return piece;
                if (best == null || piece.OpeningWidth > best.OpeningWidth) best = piece;
            }

            if (best != null)
                Debug.LogWarning($"CoolSchool: no doorway piece has an opening of {MinDoorWidth} x {MinDoorHeight} m; using {best.Name} "
                    + $"({best.OpeningWidth:0.00} x {best.OpeningHeight:0.00} m).");
            return best;
        }

        static Piece MeasurePiece(PrefabRef reference, Transform scratch, bool measureOpening)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject probe = Spawn(prefab, scratch);
            try
            {
                if (!TryGetBounds(probe, out Bounds bounds))
                {
                    Debug.LogWarning($"CoolSchool: {reference.Name} has no renderers, skipping.");
                    return null;
                }

                var piece = new Piece
                {
                    Name = reference.Name,
                    Prefab = prefab,
                    Size = bounds.size,
                    LengthAlongX = bounds.size.x >= bounds.size.z,
                };
                if (piece.LengthAlongX) piece.InsideOnNegativeZ = PlasterOnNegativeZ(probe, bounds.center.z);
                if (measureOpening) MeasureOpening(probe, bounds, out piece.OpeningWidth, out piece.OpeningHeight);
                return piece;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// The Generic walls are brick on the outside and plaster on the inside. Finds which side of the mesh the plaster
        /// triangles are on; false (no change) if the piece has no plaster material or the mesh can't be read.
        /// </summary>
        static bool PlasterOnNegativeZ(GameObject probe, float centreZ)
        {
            float sum = 0f;
            int count = 0;
            try
            {
                foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>())
                {
                    var meshRenderer = filter.GetComponent<MeshRenderer>();
                    Mesh mesh = filter.sharedMesh;
                    if (meshRenderer == null || mesh == null) continue;

                    Material[] materials = meshRenderer.sharedMaterials;
                    Vector3[] vertices = mesh.vertices;
                    for (int submesh = 0; submesh < mesh.subMeshCount && submesh < materials.Length; submesh++)
                    {
                        if (materials[submesh] == null || !materials[submesh].name.Contains("Plaster")) continue;

                        foreach (int index in mesh.GetTriangles(submesh))
                        {
                            sum += filter.transform.TransformPoint(vertices[index]).z;
                            count++;
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CoolSchool: could not read {probe.name}'s mesh to find its inside: {exception.Message}");
                return false;
            }

            return count > 0 && sum / count < centreZ;
        }

        /// <summary>
        /// The doorway: the widest gap between the vertices on the floor line, and the height of its lintel.
        /// Width is 0 if the piece has no gap.
        /// </summary>
        static void MeasureOpening(GameObject probe, Bounds bounds, out float width, out float height)
        {
            width = 0f;
            height = bounds.size.y;

            var points = new List<Vector3>();
            try
            {
                foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    foreach (Vector3 vertex in filter.sharedMesh.vertices) points.Add(filter.transform.TransformPoint(vertex));
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CoolSchool: could not read {probe.name}'s mesh to measure the doorway: {exception.Message}");
                return;
            }

            float floor = bounds.min.y;
            float[] bottom = points.Where(p => p.y < floor + 0.05f)
                .Select(p => Mathf.Round(p.x * 100f) / 100f)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            float gapStart = 0f;
            float gapEnd = 0f;
            for (int i = 1; i < bottom.Length; i++)
            {
                if (bottom[i] - bottom[i - 1] <= width) continue;

                width = bottom[i] - bottom[i - 1];
                gapStart = bottom[i - 1];
                gapEnd = bottom[i];
            }
            if (width <= 0f) return;

            float lintel = points.Where(p => p.y > floor + 0.3f && p.x >= gapStart - 0.02f && p.x <= gapEnd + 0.02f)
                .Select(p => p.y)
                .DefaultIfEmpty(bounds.max.y)
                .Min();
            height = lintel - floor;
        }

        // ----- Tiles ---------------------------------------------------------------------------

        /// <summary>
        /// Tiles covering a width x depth rectangle (centred on the origin of the parent's frame) plus a margin, centred on it,
        /// with their lowest point at y.
        /// </summary>
        internal static void LayTiles(Transform parent, Piece tile, float width, float depth, float margin, float y, Quaternion rotation, bool solid)
        {
            float sizeX = Mathf.Max(0.5f, tile.Size.x);
            float sizeZ = Mathf.Max(0.5f, tile.Size.z);
            int countX = Mathf.CeilToInt((width + 2f * margin) / sizeX - 0.001f);
            int countZ = Mathf.CeilToInt((depth + 2f * margin) / sizeZ - 0.001f);
            float startX = -countX * sizeX * 0.5f;
            float startZ = -countZ * sizeZ * 0.5f;

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    GameObject instance = Spawn(tile.Prefab, parent);
                    instance.transform.rotation = rotation;
                    AlignToBounds(instance, startX + sizeX * (ix + 0.5f), startZ + sizeZ * (iz + 0.5f), y, false);
                    if (solid) FinishEnvironmentObject(instance);
                    else MakeDecor(instance);
                }
            }
        }

        // ----- Prop helpers --------------------------------------------------------------------

        /// <summary>
        /// Spawns a prop, scales and turns it, and puts the centre of its bounds at (x, z) with its bottom at baseY.
        /// Without collide the prop is decoration: no colliders.
        /// </summary>
        internal static GameObject PlaceProp(Transform parent, PrefabRef reference, Vector3 scale, float yaw, float x, float z, float baseY,
            bool collide = true)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject prop = Spawn(prefab, parent);
            prop.transform.localScale = Vector3.Scale(prop.transform.localScale, scale);
            prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            AlignToBounds(prop, x, z, baseY, false);

            if (collide) FinishEnvironmentObject(prop);
            else MakeDecor(prop);
            return prop;
        }

        /// <summary>Moves the prop along the wall's normal until its back touches the wall face (inward = the way the room is).</summary>
        internal static void PushAgainstWall(GameObject prop, Vector3 inward, float wallFace, float gap)
        {
            if (!TryGetBounds(prop, out Bounds bounds)) return;

            Vector3 shift = Vector3.zero;
            if (inward.z > 0.5f) shift.z = wallFace + gap - bounds.min.z;
            else if (inward.z < -0.5f) shift.z = wallFace - gap - bounds.max.z;
            else if (inward.x > 0.5f) shift.x = wallFace + gap - bounds.min.x;
            else shift.x = wallFace - gap - bounds.max.x;
            prop.transform.position += shift;
        }

        internal static void SetBottomY(GameObject prop, float y)
        {
            if (!TryGetBounds(prop, out Bounds bounds)) return;
            prop.transform.position += new Vector3(0f, y - bounds.min.y, 0f);
        }

        /// <summary>Decoration: no colliders, but still static for batching.</summary>
        internal static void MakeDecor(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = child.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go,
                    GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.BatchingStatic);
            }
        }

        // ----- Anchors -------------------------------------------------------------------------

        internal static Transform NewAnchor(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            Transform anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.SetPositionAndRotation(position, rotation);
            return anchor;
        }

        /// <summary>Faces from one point toward another, staying upright.</summary>
        internal static Quaternion FlatLookRotation(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
