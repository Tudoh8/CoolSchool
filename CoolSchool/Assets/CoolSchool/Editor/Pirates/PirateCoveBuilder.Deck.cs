using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Finding where to stand on the ship and on the skeleton boat: rays are cast down against the models' own triangles
    /// (temporary non-convex MeshColliders, so a convex or capped collider cannot fool the measurement).
    /// </summary>
    internal static partial class PirateCoveBuilder
    {
        /// <summary>
        /// Temporary non-convex MeshColliders on every mesh of a model, for measuring only. The model's own colliders are switched off
        /// while it exists; Dispose removes the temporary ones and switches the old ones back on.
        /// </summary>
        sealed class ProbeSet : System.IDisposable
        {
            readonly List<Collider> original = new List<Collider>();
            readonly List<Collider> probes = new List<Collider>();

            public Collider[] Colliders => probes.ToArray();

            public bool Contains(Collider collider) => probes.Contains(collider);

            public ProbeSet(GameObject model)
            {
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(false))
                {
                    if (!collider.enabled) continue;
                    collider.enabled = false;
                    original.Add(collider);
                }

                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null) continue;
                    var probe = filter.gameObject.AddComponent<MeshCollider>();
                    probe.sharedMesh = filter.sharedMesh;
                    probe.convex = false;
                    probes.Add(probe);
                }
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                foreach (Collider probe in probes)
                    if (probe != null) Object.DestroyImmediate(probe);
                probes.Clear();
                foreach (Collider collider in original)
                    if (collider != null) collider.enabled = true;
                original.Clear();
                Physics.SyncTransforms();
            }
        }

        static bool NearestHit(Collider[] colliders, Ray ray, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool any = false;
            foreach (Collider collider in colliders)
            {
                if (collider == null) continue;
                if (collider.Raycast(ray, out RaycastHit hit, distance) && (!any || hit.distance < nearest.distance))
                {
                    nearest = hit;
                    any = true;
                }
            }
            return any;
        }

        // ---- The ship's helm platform ----

        sealed class DeckPatch
        {
            public bool Found;
            public float Y;
            public float MinX, MaxX, MinZ, MaxZ;
            public int Cells;
            public Vector3 Helm;
            public Vector3[] Crew = new Vector3[2];
            /// <summary>How far in front of the helm (north) the wheel can stand and still be on the platform.</summary>
            public float WheelAhead = 0.5f;
            public string Report = "";
        }

        const float DeckCell = 0.15f;
        const float FlatNormalY = 0.85f;
        const float HelmClearance = 0.6f;

        /// <summary>
        /// The largest flat patch at the deck height (the platform where the captain stands) in the ship's stern part, found by casting
        /// rays down on a 15 cm grid. The helm seat goes at its middle, a little toward the stern and at least 0.6 m from any rail or
        /// higher part; the crew seats beside it; and the wheel ahead of it, still on the patch.
        /// </summary>
        static DeckPatch FindHelmPatch(Collider[] colliders, Bounds bounds, float targetY)
        {
            var result = new DeckPatch();
            float x0 = bounds.min.x;
            float z0 = bounds.min.z;
            int nx = Mathf.CeilToInt(bounds.size.x / DeckCell);
            int nz = Mathf.CeilToInt(bounds.size.z * 0.65f / DeckCell);
            if (nx < 3 || nz < 3) return result;

            float startY = bounds.min.y + bounds.size.y * 0.75f;
            var onPatch = new bool[nx, nz];
            var height = new float[nx, nz];
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    var ray = new Ray(new Vector3(x0 + (i + 0.5f) * DeckCell, startY, z0 + (j + 0.5f) * DeckCell), Vector3.down);
                    if (!NearestHit(colliders, ray, bounds.size.y, out RaycastHit hit)) continue;
                    height[i, j] = hit.point.y;
                    onPatch[i, j] = hit.normal.y >= FlatNormalY && Mathf.Abs(hit.point.y - targetY) <= 0.07f;
                }
            }

            // The biggest connected group of flat cells at the deck height.
            var label = new int[nx, nz];
            var best = new List<Vector2Int>();
            int labels = 0;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!onPatch[i, j] || label[i, j] != 0) continue;

                    labels++;
                    var group = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(i, j));
                    label[i, j] = labels;
                    while (queue.Count > 0)
                    {
                        Vector2Int cell = queue.Dequeue();
                        group.Add(cell);
                        foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                        {
                            Vector2Int next = cell + step;
                            if (next.x < 0 || next.y < 0 || next.x >= nx || next.y >= nz) continue;
                            if (!onPatch[next.x, next.y] || label[next.x, next.y] != 0) continue;
                            label[next.x, next.y] = labels;
                            queue.Enqueue(next);
                        }
                    }
                    if (group.Count > best.Count) best = group;
                }
            }

            if (best.Count < 8)
            {
                result.Report = $"no flat patch found at height {targetY:0.00} (best group {best.Count} cells)";
                return result;
            }

            // Keep only the winning group.
            var inBest = new bool[nx, nz];
            foreach (Vector2Int cell in best) inBest[cell.x, cell.y] = true;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, sumX = 0f, sumZ = 0f, sumY = 0f;
            foreach (Vector2Int cell in best)
            {
                float x = x0 + (cell.x + 0.5f) * DeckCell;
                float z = z0 + (cell.y + 0.5f) * DeckCell;
                minX = Mathf.Min(minX, x - DeckCell * 0.5f);
                maxX = Mathf.Max(maxX, x + DeckCell * 0.5f);
                minZ = Mathf.Min(minZ, z - DeckCell * 0.5f);
                maxZ = Mathf.Max(maxZ, z + DeckCell * 0.5f);
                sumX += x;
                sumZ += z;
                sumY += height[cell.x, cell.y];
            }
            result.Cells = best.Count;
            result.MinX = minX; result.MaxX = maxX; result.MinZ = minZ; result.MaxZ = maxZ;
            result.Y = sumY / best.Count;
            float centreX = sumX / best.Count;
            float centreZ = sumZ / best.Count;

            // Distance from each patch cell to the nearest cell that is not part of the patch (a rail, a higher part, the edge).
            const int window = 8;
            float Clearance(int i, int j)
            {
                if (i < 0 || j < 0 || i >= nx || j >= nz || !inBest[i, j]) return 0f;
                float nearest = window;
                for (int di = -window; di <= window; di++)
                {
                    for (int dj = -window; dj <= window; dj++)
                    {
                        int a = i + di, b = j + dj;
                        bool inside = a >= 0 && b >= 0 && a < nx && b < nz && inBest[a, b];
                        if (inside) continue;
                        float d = Mathf.Sqrt(di * di + dj * dj);
                        if (d < nearest) nearest = d;
                    }
                }
                return Mathf.Max(0f, (nearest - 0.5f) * DeckCell);
            }

            Vector3 CellCentre(int i, int j) => new Vector3(x0 + (i + 0.5f) * DeckCell, result.Y, z0 + (j + 0.5f) * DeckCell);
            Vector2Int CellOf(float x, float z) => new Vector2Int(Mathf.FloorToInt((x - x0) / DeckCell), Mathf.FloorToInt((z - z0) / DeckCell));
            float ClearanceAt(float x, float z)
            {
                Vector2Int cell = CellOf(x, z);
                return Clearance(cell.x, cell.y);
            }

            // The helm: the cell nearest to a point a little south of the middle, among those with enough room around them.
            var wanted = new Vector2(centreX, centreZ - 0.12f * (maxZ - minZ));
            Vector2Int helmCell = best[0];
            float helmScore = float.MaxValue;
            float helmClearance = 0f;
            bool anyRoomy = best.Any(cell => Clearance(cell.x, cell.y) >= HelmClearance);
            foreach (Vector2Int cell in best)
            {
                float clearance = Clearance(cell.x, cell.y);
                if (anyRoomy && clearance < HelmClearance) continue;

                Vector3 p = CellCentre(cell.x, cell.y);
                float score = anyRoomy ? new Vector2(p.x - wanted.x, p.z - wanted.y).sqrMagnitude : -clearance;
                if (score < helmScore)
                {
                    helmScore = score;
                    helmCell = cell;
                    helmClearance = clearance;
                }
            }
            result.Helm = CellCentre(helmCell.x, helmCell.y);

            // Crew: left and right of the helm on the same patch, else a bit behind it.
            Vector2[][] sideOffsets =
            {
                new[] { new Vector2(-0.9f, 0f), new Vector2(-0.75f, 0f), new Vector2(-0.6f, 0f), new Vector2(-0.6f, -0.5f), new Vector2(-0.45f, -0.8f), new Vector2(0f, -0.9f) },
                new[] { new Vector2(0.9f, 0f), new Vector2(0.75f, 0f), new Vector2(0.6f, 0f), new Vector2(0.6f, -0.5f), new Vector2(0.45f, -0.8f), new Vector2(0f, -1.3f) },
            };
            for (int s = 0; s < 2; s++)
            {
                result.Crew[s] = result.Helm;
                bool placed = false;
                foreach (Vector2 offset in sideOffsets[s])
                {
                    float x = result.Helm.x + offset.x;
                    float z = result.Helm.z + offset.y;
                    if (ClearanceAt(x, z) < 0.3f) continue;
                    if (s == 1 && (new Vector2(x - result.Crew[0].x, z - result.Crew[0].z)).magnitude < 0.5f) continue;

                    result.Crew[s] = new Vector3(x, result.Y, z);
                    placed = true;
                    break;
                }
                if (!placed)
                {
                    // Anywhere on the patch with some room that is not on top of the others.
                    float bestClearance = -1f;
                    foreach (Vector2Int cell in best)
                    {
                        Vector3 p = CellCentre(cell.x, cell.y);
                        if (Vector3.Distance(p, result.Helm) < 0.6f) continue;
                        if (s == 1 && Vector3.Distance(p, result.Crew[0]) < 0.6f) continue;
                        float clearance = Clearance(cell.x, cell.y);
                        if (clearance > bestClearance)
                        {
                            bestClearance = clearance;
                            result.Crew[s] = p;
                        }
                    }
                }
            }

            // The wheel: ahead of the helm (north) as far as the patch goes, at most 0.6 m, leaving a hand's width to the edge.
            int steps = 0;
            while (steps < 8)
            {
                Vector2Int ahead = new Vector2Int(helmCell.x, helmCell.y + steps + 1);
                if (ahead.y >= nz || !inBest[ahead.x, ahead.y]) break;
                steps++;
            }
            result.WheelAhead = Mathf.Clamp(steps * DeckCell - 0.2f, 0.3f, 0.6f);

            result.Found = true;
            result.Report = $"platform at y {result.Y:0.00}: x {minX:0.00}..{maxX:0.00}, z {minZ:0.00}..{maxZ:0.00} ({best.Count} cells of {DeckCell:0.00} m), "
                + $"helm {result.Helm.ToString("0.00")} (room around it {helmClearance:0.00} m), crew {result.Crew[0].ToString("0.00")} and {result.Crew[1].ToString("0.00")}, "
                + $"wheel {result.WheelAhead:0.00} m ahead of the helm";
            return result;
        }

        /// <summary>The height of the model's surface under a point, found by casting down from 75% of its height; the fallback if nothing is hit.</summary>
        static float FindDeckHeight(Collider[] colliders, Bounds bounds, float x, float z, float fallback, string label)
        {
            var ray = new Ray(new Vector3(x, bounds.min.y + bounds.size.y * 0.75f, z), Vector3.down);
            if (NearestHit(colliders, ray, bounds.size.y, out RaycastHit hit)) return hit.point.y;

            Debug.LogWarning($"CoolSchool pirate cove: no deck found under ({x:0.0}, {z:0.0}) on {label}; using height {fallback:0.00}.");
            return fallback;
        }

        // ---- The skeleton boat's floor ----

        sealed class BoatFloor
        {
            public bool Found;
            /// <summary>The floor height was measured even if no seats could be placed on it.</summary>
            public bool FloorKnown;
            public float Y;
            public Vector3[] Seats = new Vector3[4];
            public string Report = "";
        }

        /// <summary>
        /// The floor inside the hull: rays are cast down from low inside it (below the sail), the most common hit height is the floor,
        /// and the cells there that have room for a skeleton (no mast, rail or sail within a capsule around it) are candidates. Four
        /// seats are spread along the boat's length on those cells. Positions are in world space.
        /// </summary>
        static BoatFloor FindBoatFloor(ProbeSet probes, Bounds bounds)
        {
            var result = new BoatFloor();
            Collider[] colliders = probes.Colliders;
            const float cell = 0.1f;
            int nx = Mathf.CeilToInt(bounds.size.x / cell);
            int nz = Mathf.CeilToInt(bounds.size.z / cell);
            float startY = bounds.min.y + Mathf.Min(1.0f, bounds.size.y * 0.3f);

            // The floor height: the most common upward-facing hit height (5 cm bins) among rays from low inside the hull.
            var hits = new List<Vector3>();
            var bins = new Dictionary<int, int>();
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    var ray = new Ray(new Vector3(bounds.min.x + (i + 0.5f) * cell, startY, bounds.min.z + (j + 0.5f) * cell), Vector3.down);
                    if (!NearestHit(colliders, ray, startY - bounds.min.y + 0.05f, out RaycastHit hit) || hit.normal.y < 0.8f) continue;

                    hits.Add(hit.point);
                    int bin = Mathf.RoundToInt(hit.point.y / 0.05f);
                    bins.TryGetValue(bin, out int count);
                    bins[bin] = count + 1;
                }
            }

            if (hits.Count < 10)
            {
                result.Report = $"no floor found (only {hits.Count} upward hits)";
                return result;
            }

            int floorBin = bins.OrderByDescending(pair => pair.Value).First().Key;
            result.Y = floorBin * 0.05f;
            result.FloorKnown = true;
            var floorCells = hits.Where(p => Mathf.Abs(p.y - result.Y) <= 0.05f).ToList();

            // Cells with room: nothing of the boat inside a capsule from 25 cm to 1.3 m above the floor (skeletons are about 1.2 m).
            var roomy = new List<Vector3>();
            var buffer = new Collider[64];
            foreach (Vector3 p in floorCells)
            {
                Vector3 bottom = new Vector3(p.x, result.Y + 0.3f, p.z);
                Vector3 top = new Vector3(p.x, result.Y + 1.2f, p.z);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, 0.15f, buffer);
                bool blocked = false;
                for (int k = 0; k < count && !blocked; k++)
                    if (probes.Contains(buffer[k])) blocked = true;
                if (!blocked) roomy.Add(new Vector3(p.x, result.Y, p.z));
            }

            if (roomy.Count < 4)
            {
                result.Report = $"floor at y {result.Y:0.00} ({floorCells.Count} cells) but only {roomy.Count} cells with room for a skeleton";
                result.Y = Mathf.Max(result.Y, bounds.min.y);
                return result;
            }

            // Four seats along the length of the roomy area, near its middle line, at least 0.6 m apart.
            float minZ = roomy.Min(p => p.z) + 0.1f;
            float maxZ = roomy.Max(p => p.z) - 0.1f;
            float middleX = roomy.Average(p => p.x);
            var seats = new List<Vector3>();
            for (int s = 0; s < 4; s++)
            {
                float targetZ = Mathf.Lerp(minZ, maxZ, s / 3f);
                Vector3 pick = default;
                float bestScore = float.MaxValue;
                foreach (Vector3 p in roomy)
                {
                    if (seats.Any(other => new Vector2(other.x - p.x, other.z - p.z).magnitude < 0.6f)) continue;
                    float score = Mathf.Abs(p.z - targetZ) * 2f + Mathf.Abs(p.x - middleX);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        pick = p;
                    }
                }
                if (bestScore == float.MaxValue) pick = roomy[Mathf.Min(roomy.Count - 1, s * roomy.Count / 4)];
                seats.Add(pick);
            }
            for (int s = 0; s < 4; s++) result.Seats[s] = seats[s];

            result.Found = true;
            result.Report = $"floor at y {result.Y:0.00} ({floorCells.Count} cells), {roomy.Count} cells with room; seats "
                + string.Join(", ", seats.Select(p => p.ToString("0.00")));
            return result;
        }
    }
}
