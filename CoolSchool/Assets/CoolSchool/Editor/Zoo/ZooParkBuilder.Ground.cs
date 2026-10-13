using System.Collections.Generic;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The zoo park's ground (grass), its own fence, and the entrance gap in the main west fence.</summary>
    internal static partial class ZooParkBuilder
    {
        /// <summary>A measured fence prefab: which way its long side runs, how long and how tall it is, and its base scale.</summary>
        internal sealed class FenceKit
        {
            public GameObject Prefab;
            public Vector3 BaseScale = Vector3.one;
            public bool LongIsX = true;
            public float Length;
            public float Height = 1f;
            public bool Ok => Prefab != null && Length > 0.2f;
        }

        // ---- Grass ----

        /// <summary>
        /// The park is grass. The playground's main grass reaches 4 m beyond the west fence, so every main ground tile that sticks out
        /// west of the fence line is removed, and the park's own grass covers what they covered plus the park and a margin beyond its
        /// fence (the lawn meets the untouched main tiles exactly, no overlap). The walking surface of the new grass is y = 0 like the rest.
        /// Nothing is destroyed here: the main tiles to remove are returned, and BuildParkCore destroys them as its very last step (so a
        /// failed build never leaves a hole in the lawn).
        /// </summary>
        static List<GameObject> BuildParkGrass(ParkLayout p, EnvironmentInfo info, Transform root, Transform parent)
        {
            Rect park = p.Rect;
            float west = park.xMin - OuterMargin;
            float south = park.yMin - OuterMargin;
            float north = park.yMax + OuterMargin;

            float removedMinZ = float.MaxValue, removedMaxZ = float.MinValue, removedMaxX = float.MinValue;
            var doomed = new List<GameObject>();
            Transform mainGround = root.Find("Ground");
            if (mainGround != null)
            {
                foreach (Transform tile in mainGround)
                {
                    if (!TryGetBounds(tile.gameObject, out Bounds b)) continue;
                    if (b.min.x >= info.MinX - 0.01f) continue;

                    doomed.Add(tile.gameObject);
                    removedMinZ = Mathf.Min(removedMinZ, b.min.z);
                    removedMaxZ = Mathf.Max(removedMaxZ, b.max.z);
                    removedMaxX = Mathf.Max(removedMaxX, b.max.x);
                }
            }
            else
            {
                Debug.LogWarning("CoolSchool zoo park: no 'Ground' group found in the environment, so no main grass tile was removed.");
            }

            float east = info.MinX;
            float z0 = south;
            float z1 = north;
            if (doomed.Count > 0)
            {
                east = Mathf.Max(east, removedMaxX);
                z0 = Mathf.Min(z0, removedMinZ);
                z1 = Mathf.Max(z1, removedMaxZ);
            }
            Debug.Log($"CoolSchool zoo park: {doomed.Count} main ground tiles west of the fence line are replaced; "
                + $"the park grass covers x {west:0.0}..{east:0.0}, z {z0:0.0}..{z1:0.0}.");

            LayGrass(LoadPrefab(GrassTilePrefab), parent, west, east, z0, z1, 0f);
            return doomed;
        }

        /// <summary>
        /// Lays tiles over x in xMin..xMax and z in zMin..zMax with their walking surface at topY. The tiles are stretched a little so the
        /// grid fits the rectangle exactly. Lumpy tiles get a flat collider at the surface, so kids and the NavMesh stand on the surface.
        /// </summary>
        static void LayGrass(GameObject prefab, Transform parent, float xMin, float xMax, float zMin, float zMax, float topY)
        {
            if (prefab == null) return;

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds) || probeBounds.size.x < 0.01f || probeBounds.size.z < 0.01f)
            {
                Debug.LogWarning($"CoolSchool zoo park: tile has no usable size, skipping: {prefab.name}");
                Object.DestroyImmediate(probe);
                return;
            }
            Vector3 baseScale = probe.transform.localScale;
            GroundSurface.Measurement surface = GroundSurface.Measure(probe);
            float bump = surface.Valid ? Mathf.Max(0f, surface.BumpHeight) : 0f;
            Object.DestroyImmediate(probe);

            float naturalX = probeBounds.size.x < 0.5f ? 4f : probeBounds.size.x;
            float naturalZ = probeBounds.size.z < 0.5f ? 4f : probeBounds.size.z;
            int countX = Mathf.Clamp(Mathf.RoundToInt((xMax - xMin) / naturalX), 1, 80);
            int countZ = Mathf.Clamp(Mathf.RoundToInt((zMax - zMin) / naturalZ), 1, 80);
            float tileX = (xMax - xMin) / countX;
            float tileZ = (zMax - zMin) / countZ;
            var scale = new Vector3(baseScale.x * tileX / probeBounds.size.x, baseScale.y, baseScale.z * tileZ / probeBounds.size.z);

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    GameObject tile = Spawn(prefab, parent);
                    tile.transform.localScale = scale;
                    AlignToBounds(tile, xMin + tileX * (ix + 0.5f), zMin + tileZ * (iz + 0.5f), topY + bump, true);
                    if (surface.Valid && surface.FlatShare < 0.9f) GroundSurface.UseFlatCollider(tile, topY);
                    FinishEnvironmentObject(tile);
                }
            }
        }

        // ---- Fence ----

        /// <summary>Measures a fence prefab on a temporary instance.</summary>
        static FenceKit MeasureFence(PrefabRef reference, Transform parent)
        {
            var kit = new FenceKit { Prefab = LoadPrefab(reference) };
            if (kit.Prefab == null) return kit;

            GameObject probe = Spawn(kit.Prefab, parent);
            if (!TryGetBounds(probe, out Bounds bounds))
            {
                Debug.LogWarning("CoolSchool zoo park: the fence has no renderers, so there is no fence.");
                Object.DestroyImmediate(probe);
                kit.Prefab = null;
                return kit;
            }

            kit.BaseScale = probe.transform.localScale;
            kit.LongIsX = bounds.size.x >= bounds.size.z;
            kit.Length = Mathf.Max(bounds.size.x, bounds.size.z);
            kit.Height = Mathf.Max(0.05f, bounds.size.y);
            Object.DestroyImmediate(probe);
            return kit;
        }

        /// <summary>
        /// Fence pieces along a straight run from one ground point to another, the way the main fence is laid: the long side along
        /// the run, pieces stretched a little so the run is filled exactly. inward is the horizontal direction toward the inside
        /// (it only decides which way the pieces face). heightScale squashes the pieces (0.7 m low pen fences). With freeStretch
        /// every stretch is allowed (short runs); otherwise a stretch outside 0.8..1.25 is skipped, as in the main fence.
        /// </summary>
        static void FenceRun(FenceKit kit, Transform parent, Vector3 from, Vector3 to, Vector3 inward, float heightScale, bool freeStretch)
        {
            if (!kit.Ok) return;

            Vector3 delta = to - from;
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.1f) return;

            Vector3 along = delta / length;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / kit.Length));
            float spacing = length / count;
            float stretch = spacing / kit.Length;
            if (!freeStretch && (stretch < 0.8f || stretch > 1.25f)) stretch = 1f;

            Vector3 scale = kit.BaseScale;
            if (kit.LongIsX) scale.x *= stretch;
            else scale.z *= stretch;
            scale.y *= heightScale;

            Quaternion rotation = Quaternion.LookRotation(inward, Vector3.up) * Quaternion.Euler(0f, kit.LongIsX ? 0f : 90f, 0f);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = from + along * (spacing * (i + 0.5f));
                GameObject segment = Spawn(kit.Prefab, parent);
                segment.transform.localScale = scale;
                segment.transform.rotation = rotation;
                AlignToBounds(segment, point.x, point.z, 0f, false);
                FinishEnvironmentObject(segment);
            }
        }

        /// <summary>The park's own fence: west, north and south sides, and the east side beyond the end of the main fence (z MaxZ..30).</summary>
        static void BuildParkFence(FenceKit kit, Transform parent, ParkLayout p, EnvironmentInfo info)
        {
            if (!kit.Ok)
            {
                Debug.LogWarning("CoolSchool zoo park: no fence prefab, so the park has no fence.");
                return;
            }

            float xWest = p.Rect.xMin;
            float xEast = info.MinX;
            FenceRun(kit, parent, new Vector3(xWest, 0f, ParkSouthV), new Vector3(xWest, 0f, ParkNorthV), Vector3.right, 1f, false);
            FenceRun(kit, parent, new Vector3(xWest, 0f, ParkNorthV), new Vector3(xEast, 0f, ParkNorthV), Vector3.back, 1f, false);
            FenceRun(kit, parent, new Vector3(xWest, 0f, ParkSouthV), new Vector3(xEast, 0f, ParkSouthV), Vector3.forward, 1f, false);
            if (info.MaxZ < ParkNorthV - 0.5f)
                FenceRun(kit, parent, new Vector3(xEast, 0f, info.MaxZ), new Vector3(xEast, 0f, ParkNorthV), Vector3.left, 1f, false);
        }

        /// <summary>
        /// Removes the main west fence pieces that overlap the entrance gap (3 m wide, centred on v = -3), then fills what a removed
        /// whole piece leaves beyond the gap with stretched pieces of the park's fence, so the opening stays 3 m. Returns the z range
        /// that is open afterwards.
        /// The pieces to remove are returned in doomed and not destroyed here: BuildParkCore does that as its last step.
        /// </summary>
        static Vector2 CutEntrance(Transform root, FenceKit kit, Transform fenceParent, ParkLayout p, EnvironmentInfo info, out List<GameObject> doomed)
        {
            doomed = new List<GameObject>();
            float gapMin = EntranceV - EntranceWidth * 0.5f;
            float gapMax = EntranceV + EntranceWidth * 0.5f;
            var opened = new Vector2(gapMin, gapMax);

            Transform fenceGroup = root.Find("Fence");
            if (fenceGroup == null)
            {
                Debug.LogWarning("CoolSchool zoo park: no 'Fence' group found, so the entrance gap was not cut.");
                return opened;
            }

            foreach (Transform piece in fenceGroup)
            {
                if (!TryGetBounds(piece.gameObject, out Bounds bounds)) continue;

                bool onWestLine = bounds.max.x > info.MinX - 1.2f && bounds.min.x < info.MinX + 1.2f && bounds.size.z > bounds.size.x;
                bool inGap = bounds.max.z > gapMin + 0.05f && bounds.min.z < gapMax - 0.05f;
                if (!onWestLine || !inGap) continue;

                doomed.Add(piece.gameObject);
                opened.x = Mathf.Min(opened.x, bounds.min.z);
                opened.y = Mathf.Max(opened.y, bounds.max.z);
            }

            if (doomed.Count == 0)
                Debug.LogWarning($"CoolSchool zoo park: no main west fence piece found in the entrance gap z {gapMin:0.0}..{gapMax:0.0}.");

            // Whole pieces go, so the opening is wider than 3 m: close the extra bits again.
            var result = new Vector2(gapMin, gapMax);
            float xFence = info.MinX;
            if (opened.x < gapMin - 0.3f)
                FenceRun(kit, fenceParent, new Vector3(xFence, 0f, opened.x), new Vector3(xFence, 0f, gapMin), Vector3.right, 1f, true);
            else
                result.x = Mathf.Min(opened.x, gapMin);
            if (opened.y > gapMax + 0.3f)
                FenceRun(kit, fenceParent, new Vector3(xFence, 0f, gapMax), new Vector3(xFence, 0f, opened.y), Vector3.right, 1f, true);
            else
                result.y = Mathf.Max(opened.y, gapMax);
            return result;
        }

        /// <summary>Logs a warning for every alien-field prop whose bounds come within 2 m of the gap on the inside.</summary>
        static void CheckEntrance(ParkLayout p, EnvironmentInfo info)
        {
            if (info.AlienField == null || info.AlienField.Root == null) return;

            var probe = Rect.MinMaxRect(info.MinX - 0.1f, p.EntranceGap.x - 2f, info.MinX + 2f, p.EntranceGap.y + 2f);
            int near = 0;
            foreach (Renderer renderer in info.AlienField.Root.GetComponentsInChildren<Renderer>(false))
            {
                if (!(renderer is MeshRenderer) || renderer.bounds.size.y < 0.05f) continue;

                Bounds b = renderer.bounds;
                if (!probe.Overlaps(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z))) continue;

                near++;
                Debug.LogWarning($"CoolSchool zoo park: alien field prop '{renderer.name}' (x {b.min.x:0.0}..{b.max.x:0.0}, z {b.min.z:0.0}..{b.max.z:0.0}) "
                    + $"is within 2 m of the park entrance gap (z {p.EntranceGap.x:0.0}..{p.EntranceGap.y:0.0}).");
            }
            if (near == 0)
                Debug.Log("CoolSchool zoo park: nothing of the alien field stands within 2 m of the entrance gap.");
        }
    }
}
