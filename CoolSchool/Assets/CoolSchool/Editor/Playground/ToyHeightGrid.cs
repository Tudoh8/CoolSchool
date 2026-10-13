using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// A flat frame for measuring a toy: the origin is the toy root's position, the axes are the root's right and forward turned flat.
    /// Local coordinates are metres along those axes, so a toy that was turned toward the middle of the ring can be measured as if it were
    /// not turned, whatever its scale.
    /// </summary>
    internal readonly struct ToyFrame
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Right;
        public readonly Vector3 Forward;

        public ToyFrame(Transform root)
        {
            Origin = root.position;
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            Forward = forward.normalized;
            Right = Vector3.Cross(Vector3.up, Forward).normalized;
        }

        public Vector2 ToLocal(Vector3 world)
        {
            Vector3 d = world - Origin;
            return new Vector2(Vector3.Dot(d, Right), Vector3.Dot(d, Forward));
        }

        public Vector3 ToWorld(Vector2 local, float y)
        {
            Vector3 p = Origin + Right * local.x + Forward * local.y;
            p.y = y;
            return p;
        }

        /// <summary>A flat world direction from a local direction.</summary>
        public Vector3 ToWorldDirection(Vector2 local)
        {
            return (Right * local.x + Forward * local.y).normalized;
        }
    }

    /// <summary>
    /// A grid of TopAt heights over the toy's footprint, in the toy's flat frame. Cells with no mesh above them are empty (NaN).
    /// Also has the few shape tools the toy configures need: connected regions of cells and a 2D principal axis.
    /// </summary>
    internal sealed class ToyHeightGrid
    {
        public readonly ToyFrame Frame;
        public readonly float Cell;
        public readonly int Nx;
        public readonly int Nz;
        public readonly float X0;   // local x of the left edge of cell 0
        public readonly float Z0;
        public readonly float[] Height;

        public Vector2 LocalMin => new Vector2(X0, Z0);
        public Vector2 LocalMax => new Vector2(X0 + Nx * Cell, Z0 + Nz * Cell);

        ToyHeightGrid(ToyFrame frame, float cell, int nx, int nz, float x0, float z0)
        {
            Frame = frame;
            Cell = cell;
            Nx = nx;
            Nz = nz;
            X0 = x0;
            Z0 = z0;
            Height = new float[nx * nz];
        }

        /// <summary>Measures the toy's footprint (from its vertices) and samples TopAt at every cell centre.</summary>
        public static ToyHeightGrid Build(ToySetup setup, float cell)
        {
            var frame = new ToyFrame(setup.Root.transform);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 v in setup.WorldVertices())
            {
                Vector2 l = frame.ToLocal(v);
                min = Vector2.Min(min, l);
                max = Vector2.Max(max, l);
            }
            if (min.x > max.x) { min = Vector2.zero; max = Vector2.one; }

            min -= Vector2.one * 0.1f;
            max += Vector2.one * 0.1f;
            int nx = Mathf.Clamp(Mathf.CeilToInt((max.x - min.x) / cell), 1, 160);
            int nz = Mathf.Clamp(Mathf.CeilToInt((max.y - min.y) / cell), 1, 160);
            var grid = new ToyHeightGrid(frame, cell, nx, nz, min.x, min.y);

            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    Vector3 world = frame.ToWorld(grid.CellCentre(ix, iz), 0f);
                    grid.Height[iz * nx + ix] = setup.TopAt(world.x, world.z, out Vector3 hit) ? hit.y : float.NaN;
                }
            }
            return grid;
        }

        public Vector2 CellCentre(int ix, int iz)
        {
            return new Vector2(X0 + (ix + 0.5f) * Cell, Z0 + (iz + 0.5f) * Cell);
        }

        public bool InGrid(int ix, int iz)
        {
            return ix >= 0 && iz >= 0 && ix < Nx && iz < Nz;
        }

        public float At(int ix, int iz)
        {
            return InGrid(ix, iz) ? Height[iz * Nx + ix] : float.NaN;
        }

        public bool Has(int ix, int iz)
        {
            return !float.IsNaN(At(ix, iz));
        }

        public int Index(int ix, int iz) => iz * Nx + ix;

        public void IndexToCell(int index, out int ix, out int iz)
        {
            ix = index % Nx;
            iz = index / Nx;
        }

        /// <summary>The cell holding a local point (clamped into the grid).</summary>
        public void CellOf(Vector2 local, out int ix, out int iz)
        {
            ix = Mathf.Clamp(Mathf.FloorToInt((local.x - X0) / Cell), 0, Nx - 1);
            iz = Mathf.Clamp(Mathf.FloorToInt((local.y - Z0) / Cell), 0, Nz - 1);
        }

        /// <summary>The height at a local point (nearest cell), NaN when empty or outside.</summary>
        public float Sample(Vector2 local)
        {
            int ix = Mathf.FloorToInt((local.x - X0) / Cell);
            int iz = Mathf.FloorToInt((local.y - Z0) / Cell);
            return At(ix, iz);
        }

        public float MaxHeight()
        {
            float max = float.MinValue;
            foreach (float h in Height) if (!float.IsNaN(h) && h > max) max = h;
            return max;
        }

        /// <summary>The connected groups of cells for which the test is true, largest first (cell indices).</summary>
        public List<List<int>> Regions(Func<int, int, bool> test, bool eightNeighbours)
        {
            var seen = new bool[Nx * Nz];
            var regions = new List<List<int>>();
            var stack = new Stack<int>();
            for (int start = 0; start < Nx * Nz; start++)
            {
                if (seen[start]) continue;
                IndexToCell(start, out int sx, out int sz);
                if (!test(sx, sz)) continue;

                var region = new List<int>();
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    region.Add(current);
                    IndexToCell(current, out int cx, out int cz);
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx == 0 && dz == 0) || (!eightNeighbours && dx != 0 && dz != 0)) continue;
                            int nx = cx + dx;
                            int nz = cz + dz;
                            if (!InGrid(nx, nz)) continue;
                            int n = Index(nx, nz);
                            if (seen[n] || !test(nx, nz)) continue;
                            seen[n] = true;
                            stack.Push(n);
                        }
                    }
                }
                regions.Add(region);
            }
            regions.Sort((a, b) => b.Count.CompareTo(a.Count));
            return regions;
        }

        /// <summary>Local centres of the cells of a region.</summary>
        public List<Vector2> Centres(List<int> region)
        {
            var points = new List<Vector2>(region.Count);
            foreach (int index in region)
            {
                IndexToCell(index, out int ix, out int iz);
                points.Add(CellCentre(ix, iz));
            }
            return points;
        }

        /// <summary>Local min and max of a region's cell centres.</summary>
        public void Extent(List<int> region, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 p in Centres(region))
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }

        /// <summary>The mean height of a region.</summary>
        public float MeanHeight(List<int> region)
        {
            float sum = 0f;
            foreach (int index in region) sum += Height[index];
            return region.Count > 0 ? sum / region.Count : float.NaN;
        }

        /// <summary>The main direction of a point cloud: its centre, the unit axis of the greatest spread and the spreads along and across it.</summary>
        public static void Pca(IList<Vector2> points, out Vector2 mean, out Vector2 axis, out float along, out float across)
        {
            mean = Vector2.zero;
            axis = Vector2.up;
            along = 0f;
            across = 0f;
            if (points.Count == 0) return;

            foreach (Vector2 p in points) mean += p;
            mean /= points.Count;

            float xx = 0f, xy = 0f, yy = 0f;
            foreach (Vector2 p in points)
            {
                Vector2 d = p - mean;
                xx += d.x * d.x;
                xy += d.x * d.y;
                yy += d.y * d.y;
            }
            xx /= points.Count;
            xy /= points.Count;
            yy /= points.Count;

            float angle = 0.5f * Mathf.Atan2(2f * xy, xx - yy);
            axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float min = float.MaxValue, max = float.MinValue, minAcross = float.MaxValue, maxAcross = float.MinValue;
            Vector2 side = new Vector2(-axis.y, axis.x);
            foreach (Vector2 p in points)
            {
                float t = Vector2.Dot(p - mean, axis);
                float s = Vector2.Dot(p - mean, side);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
                minAcross = Mathf.Min(minAcross, s);
                maxAcross = Mathf.Max(maxAcross, s);
            }
            along = max - min;
            across = maxAcross - minAcross;
        }
    }
}
