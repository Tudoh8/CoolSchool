using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Finds where the real walking surface of a ground tile is. A tile's renderer bounds also include grass tufts and bumps,
    /// so aligning the bounds top to y = 0 leaves the flat surface below 0 and everything standing "on y = 0" floats.
    /// The surface is measured by shooting rays straight down onto the tile's mesh and taking the height most of them agree on.
    /// </summary>
    internal static class GroundSurface
    {
        const int SamplesPerSide = 24;
        const float BinSize = 0.005f;

        /// <summary>The result of measuring one tile instance.</summary>
        internal struct Measurement
        {
            public bool Valid;
            /// <summary>World height of the main flat surface.</summary>
            public float SurfaceY;
            /// <summary>World height of the highest point (renderer bounds top) and of the lowest hit.</summary>
            public float BoundsTop;
            public float LowestHit;
            public float HighestHit;
            /// <summary>How much of the tile lies within 2 cm of the main surface (0..1).</summary>
            public float FlatShare;

            /// <summary>How far the bounds top lies above the main surface. Subtract it when aligning the bounds top.</summary>
            public float BumpHeight => BoundsTop - SurfaceY;
        }

        /// <summary>
        /// Measures a tile that is already spawned in the scene (anywhere, any rotation). Temporary mesh colliders are added to
        /// cast against and removed again.
        /// </summary>
        internal static Measurement Measure(GameObject tile)
        {
            var result = new Measurement();
            if (!PlaygroundPrototypeBuilder.TryGetBounds(tile, out Bounds bounds)) return result;

            var temporary = new List<MeshCollider>();
            foreach (MeshFilter filter in tile.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                temporary.Add(collider);
            }
            Physics.SyncTransforms();

            var heights = new List<float>();
            float top = bounds.max.y + 1f;
            for (int ix = 0; ix < SamplesPerSide; ix++)
            {
                for (int iz = 0; iz < SamplesPerSide; iz++)
                {
                    float x = Mathf.Lerp(bounds.min.x, bounds.max.x, (ix + 0.5f) / SamplesPerSide);
                    float z = Mathf.Lerp(bounds.min.z, bounds.max.z, (iz + 0.5f) / SamplesPerSide);
                    var ray = new Ray(new Vector3(x, top, z), Vector3.down);

                    bool hitAny = false;
                    float best = float.MinValue;
                    foreach (MeshCollider collider in temporary)
                    {
                        if (collider.Raycast(ray, out RaycastHit hit, top - bounds.min.y + 2f) && hit.point.y > best)
                        {
                            best = hit.point.y;
                            hitAny = true;
                        }
                    }
                    if (hitAny) heights.Add(best);
                }
            }

            foreach (MeshCollider collider in temporary) Object.DestroyImmediate(collider);

            if (heights.Count < 8) return result;

            // The height most samples agree on: the fullest bin of the histogram, averaged with its neighbours' samples.
            var bins = new Dictionary<int, int>();
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (float h in heights)
            {
                int bin = Mathf.RoundToInt(h / BinSize);
                bins.TryGetValue(bin, out int count);
                bins[bin] = count + 1;
                lowest = Mathf.Min(lowest, h);
                highest = Mathf.Max(highest, h);
            }

            int bestBin = 0;
            int bestCount = -1;
            foreach (KeyValuePair<int, int> pair in bins)
            {
                // Ties go to the higher bin: a visible surface beats one hidden under it.
                if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key > bestBin))
                {
                    bestBin = pair.Key;
                    bestCount = pair.Value;
                }
            }

            float sum = 0f;
            int n = 0;
            int flat = 0;
            foreach (float h in heights)
            {
                if (Mathf.Abs(Mathf.RoundToInt(h / BinSize) - bestBin) <= 1)
                {
                    sum += h;
                    n++;
                }
            }
            result.SurfaceY = n > 0 ? sum / n : bestBin * BinSize;
            foreach (float h in heights)
                if (Mathf.Abs(h - result.SurfaceY) <= 0.02f) flat++;

            result.BoundsTop = bounds.max.y;
            result.LowestHit = lowest;
            result.HighestHit = highest;
            result.FlatShare = flat / (float)heights.Count;
            result.Valid = true;
            return result;
        }

        /// <summary>Spawns the prefab as a probe under the parent, measures it, and removes it again.</summary>
        internal static Measurement MeasurePrefab(GameObject prefab, Transform parent)
        {
            GameObject probe = PlaygroundPrototypeBuilder.Spawn(prefab, parent);
            Measurement result = Measure(probe);
            Object.DestroyImmediate(probe);
            return result;
        }

        /// <summary>
        /// Gives a placed, bumpy ground tile one flat box collider whose top is at surfaceY (world), instead of its lumpy mesh
        /// colliders. Kids walk and the NavMesh is baked on the flat surface, not on every tuft. The tile must not be rotated.
        /// </summary>
        internal static void UseFlatCollider(GameObject tile, float surfaceY)
        {
            if (!PlaygroundPrototypeBuilder.TryGetBounds(tile, out Bounds bounds)) return;

            foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            float bottom = Mathf.Min(bounds.min.y, surfaceY - 0.05f);
            var worldCentre = new Vector3(bounds.center.x, (surfaceY + bottom) * 0.5f, bounds.center.z);
            var worldSize = new Vector3(bounds.size.x, surfaceY - bottom, bounds.size.z);

            Vector3 scale = tile.transform.lossyScale;
            var box = tile.AddComponent<BoxCollider>();
            box.center = tile.transform.InverseTransformPoint(worldCentre);
            box.size = new Vector3(worldSize.x / Mathf.Max(0.0001f, scale.x), worldSize.y / Mathf.Max(0.0001f, scale.y), worldSize.z / Mathf.Max(0.0001f, scale.z));
        }

        internal static string Describe(string name, Measurement m)
        {
            if (!m.Valid) return $"{name}: surface could not be measured; the bounds top is used.";
            return $"{name}: main surface y {m.SurfaceY:0.000} ({m.FlatShare * 100f:0}% of the tile within 2 cm), bounds top y {m.BoundsTop:0.000}, "
                + $"highest hit {m.HighestHit:0.000}, lowest hit {m.LowestHit:0.000}; the bounds top is {m.BumpHeight * 100f:0.0} cm above the surface.";
        }
    }
}
