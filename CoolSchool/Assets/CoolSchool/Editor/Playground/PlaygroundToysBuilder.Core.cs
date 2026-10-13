using System.Collections.Generic;
using System.Linq;
using CoolSchool.Playground;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The four toys of the core part: the slide, the two spring riders, the roundabout and the tunnel. Synty's pivots and orientations are
    /// unknown, so every anchor is worked out from the geometry (a grid of heights from rays onto the toy's meshes, and its vertices in the
    /// toy's own flat frame) and logged; where a feature cannot be found, a sensible fallback is used and a "fallback" line is logged.
    /// </summary>
    public static partial class PlaygroundToysBuilder
    {
        // How deep the kid's root sits below a floor he sits on, as a fraction of the measured hips height (the pelvis is above the floor
        // by about that much, but the sit-on-the-ground clip has its legs and seat at the root's own height).
        const float FloorSitSink = 0.25f;

        // For a seat, the hips bone sits a little above the seat surface: so the root is the hips height minus this below the surface.
        const float SeatHipsAboveSurface = 0.04f;

        // ====================================================================================================================
        // The slide
        // ====================================================================================================================

        /// <summary>
        /// The slide. The Slide_01 mesh is only a chute, so its analysis finds the chute's axis, back end and bed height; then BuildSlideTower
        /// (PlaygroundToysBuilder.SlideTower.cs) builds a tower of kit pieces round it and squashes the chute to fit. The kid's way up (round
        /// the tower, up the stairs, across the landing, up the second flight to the platform) and his way down (the chute) are logged.
        /// </summary>
        static partial void ConfigureSlide(ToySetup s)
        {
            ToyHeightGrid grid = ToyHeightGrid.Build(s, 0.1f);
            ToyFrame frame = grid.Frame;
            Bounds bounds = s.WorldBounds;
            float groundY = s.GroundY(bounds.center.x, bounds.center.z);
            float topY = grid.MaxHeight();
            s.Log($"slide analysis: ground y {groundY:0.00}, highest point {topY:0.00}, grid {grid.Nx} x {grid.Nz} cells of {grid.Cell:0.00} m");

            // ---- 1. The chute: the biggest sloping area. (The mesh has no platform and no ladder.) ----
            Vector2 chuteMean, chuteDir;
            float tTop, tBottom;
            bool haveChute = FindChute(s, grid, groundY, topY - 0.02f, out chuteMean, out chuteDir, out tTop, out tBottom);
            if (!haveChute)
            {
                s.Warn("fallback: no sloping chute area found; using the long axis of the toy, downhill toward its lower end.");
                FallbackChute(s, grid, Vector2.zero, groundY, topY - 0.02f, out chuteMean, out chuteDir, out tTop, out tBottom);
            }
            s.Log($"chute: through local {chuteMean.x:0.00}, {chuteMean.y:0.00}, direction downhill {chuteDir.x:0.00}, {chuteDir.y:0.00} "
                + $"(world {ToySetup.Fmt(frame.ToWorldDirection(chuteDir))}), sloping from t {tTop:0.00} (top) to t {tBottom:0.00} (foot)");

            // The very back of the chute mesh, and the height of its bed there.
            float tBack = float.MaxValue;
            foreach (Vector3 v in s.WorldVertices()) tBack = Mathf.Min(tBack, Vector2.Dot(frame.ToLocal(v) - chuteMean, chuteDir));
            float bedTop = groundY;
            for (float t = tBack + 0.05f; t <= tBack + 0.65f; t += 0.1f)
            {
                Vector3 w = frame.ToWorld(chuteMean + chuteDir * t, 0f);
                if (s.TopAt(w.x, w.z, out Vector3 hit)) bedTop = Mathf.Max(bedTop, hit.y);
            }
            s.Log($"chute back end at t {tBack:0.00}, bed there {bedTop:0.00} m high (ground {groundY:0.00})");

            // ---- 2. The tower round it ----
            List<Transform> chuteParts = TopLevelMeshObjects(s);   // the chute mesh only, before the tower pieces are added
            SlideTowerLayout tower = BuildSlideTower(s, frame, chuteMean, chuteDir, tBack, tBottom, groundY, bedTop);

            // The toy is bigger now: new bounds, a touch zone round the whole tower, a new height map for the log.
            s.Dirty();
            if (PlaygroundPrototypeBuilder.TryGetBounds(s.Root, out Bounds whole)) s.WorldBounds = whole;
            Transform oldZone = s.Root.transform.Find("TouchZone");
            if (oldZone != null) Object.DestroyImmediate(oldZone.gameObject);
            BuildTouchZone(s.Root, s.Toy);
            s.Log($"whole slide with tower: bounds size {ToySetup.Fmt(s.WorldBounds.size)}, centre {ToySetup.Fmt(s.WorldBounds.center)}; the touch zone was made again round all of it");
            s.LogHeightMap();

            // ---- 3. The way down: the chute, from the platform's front edge ----
            Vector3 dirWorld = frame.ToWorldDirection(chuteDir);
            Quaternion toChute = Quaternion.LookRotation(dirWorld, Vector3.up);
            float hips = s.PoseOffsets.sitSlideHips;
            List<Vector3> surface = SampleChute(s, frame, chuteMean, chuteDir, tower.Tc + 0.2f, tBottom, groundY, tower.DeckY, chuteParts, out int chuteEndIndex);
            var rootPoints = new List<Vector3>();
            for (int i = 0; i < surface.Count; i++)
            {
                Vector3 next = surface[Mathf.Min(i + 1, surface.Count - 1)];
                Vector3 previous = surface[Mathf.Max(i - 1, 0)];
                Vector3 tangent = (next - previous).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = dirWorld;
                Vector3 normal = Vector3.up - tangent * Vector3.Dot(Vector3.up, tangent);
                normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
                rootPoints.Add(surface[i] - normal * (hips * FloorSitSink));
            }

            // No point of the way down may lie above the platform (a ray that caught the roof or a railing would put him up there).
            float highest = float.MinValue;
            for (int i = 0; i < rootPoints.Count; i++)
            {
                highest = Mathf.Max(highest, rootPoints[i].y);
                if (rootPoints[i].y > tower.DeckY + 0.15f)
                {
                    s.Warn($"fallback: slide path point {i} was at y {rootPoints[i].y:0.00}, above the platform ({tower.DeckY:0.00}); put on the platform.");
                    Vector3 p = rootPoints[i];
                    p.y = tower.DeckY;
                    rootPoints[i] = p;
                }
            }
            s.Log($"slide path: {rootPoints.Count} points, highest y {highest:0.00} (platform top {tower.DeckY:0.00}), lowest y {rootPoints.Min(p => p.y):0.00}");

            var pathTransforms = new List<Transform>();
            for (int i = 0; i < rootPoints.Count; i++)
            {
                Vector3 direction = i + 1 < rootPoints.Count ? rootPoints[i + 1] - rootPoints[i] : rootPoints[i] - rootPoints[Mathf.Max(0, i - 1)];
                Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : toChute;
                pathTransforms.Add(s.AddPoint("SlidePath_" + i, rootPoints[i], rotation));
            }

            Vector3 last = surface[surface.Count - 1];
            Vector3 exitWorld = last + dirWorld * 0.9f;
            exitWorld.y = s.GroundY(exitWorld.x, exitWorld.z);
            Transform exit = s.AddPoint("SlideExit", exitWorld, toChute);

            // ---- 4. The way up: round the tower to the foot of the stairs, then up ----
            Vector3[] climb = tower.Climb;
            Quaternion FaceAlong(Vector3 from, Vector3 to)
            {
                Vector3 d = to - from;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d.normalized, Vector3.up) : toChute;
            }

            var climbTransforms = new List<Transform>();
            for (int i = 0; i < climb.Length; i++)
            {
                Quaternion rotation = i + 1 < climb.Length ? FaceAlong(climb[i], climb[i + 1]) : toChute;
                climbTransforms.Add(s.AddPoint("SlideStairs_" + i, climb[i], rotation));
            }
            var ringTransforms = new List<Transform>();
            for (int i = 0; i < tower.Ring.Length; i++)
                ringTransforms.Add(s.AddPoint("SlideRing_" + i, tower.Ring[i], FaceAlong(tower.Ring[i], tower.Ring[(i + 1) % tower.Ring.Length])));

            Transform rider = s.AddPoint("SlideRider", climb[0], FaceAlong(climb[0], climb[1]));
            Transform stairExit = s.AddPoint("SlideStairExit", climb[0], FaceAlong(climb[1], climb[0]));

            // Anchors for the preview shots.
            Vector3 Mid(Vector3 a, Vector3 b) => (a + b) * 0.5f;
            s.AddPoint("PreviewStairsLower", Mid(climb[1], climb[2]), FaceAlong(climb[1], climb[2]));
            s.AddPoint("PreviewLanding", climb[3], FaceAlong(climb[3], climb[4]));
            s.AddPoint("PreviewStairsUpper", Mid(climb[4], climb[5]), FaceAlong(climb[4], climb[5]));
            s.AddPoint("PreviewDeck", climb[6], toChute);
            s.AddPoint("PreviewSeat", rootPoints[0], pathTransforms[0].rotation);
            s.AddPoint("PreviewSlideMid", rootPoints[Mathf.Clamp(chuteEndIndex / 2, 0, rootPoints.Count - 1)], toChute);
            s.AddPoint("PreviewSlideEnd", rootPoints[Mathf.Clamp(chuteEndIndex, 0, rootPoints.Count - 1)], toChute);

            var routine = s.SetRoutine<SlideRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "rider", rider);
            PlaygroundPrototypeBuilder.SetObject(routine, "stairExit", stairExit);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "approachRing", ringTransforms.Select(t => (Object)t).ToList());
            PlaygroundPrototypeBuilder.SetInt(routine, "ringFootIndex", tower.RingFoot);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "climbPath", climbTransforms.Select(t => (Object)t).ToList());
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "slidePath", pathTransforms.Select(t => (Object)t).ToList());
            PlaygroundPrototypeBuilder.SetInt(routine, "chuteEndIndex", Mathf.Max(1, chuteEndIndex));
            s.Log($"slide ready: stairs foot {ToySetup.Fmt(climb[0])}, landing y {tower.LandingY:0.00}, top platform y {tower.DeckY:0.00} at {ToySetup.Fmt(tower.TopCentre)}, "
                + $"seat {ToySetup.Fmt(rootPoints[0])}, chute foot {ToySetup.Fmt(rootPoints[Mathf.Clamp(chuteEndIndex, 0, rootPoints.Count - 1)])}, exit {ToySetup.Fmt(exitWorld)}");
        }


        /// <summary>
        /// The chute: the biggest connected area of sloping cells between the ground and the platform. Its main axis, pointing downhill,
        /// is the sliding direction. Returns false when there is no such area.
        /// </summary>
        static bool FindChute(ToySetup s, ToyHeightGrid grid, float groundY, float platformY, out Vector2 mean, out Vector2 downhill, out float tTop, out float tBottom)
        {
            mean = Vector2.zero;
            downhill = Vector2.up;
            tTop = 0f;
            tBottom = 0f;

            bool Sloping(int ix, int iz)
            {
                if (!grid.Has(ix, iz)) return false;
                float y = grid.At(ix, iz);
                if (y < groundY + 0.12f || y > platformY - 0.1f) return false;

                float steepest = 0f;
                for (int d = 0; d < 4; d++)
                {
                    int nx = ix + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nz = iz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!grid.Has(nx, nz)) continue;
                    steepest = Mathf.Max(steepest, Mathf.Abs(grid.At(nx, nz) - y) / grid.Cell);
                }
                return steepest >= 0.18f && steepest <= 1.7f;
            }

            List<List<int>> regions = grid.Regions(Sloping, true);
            if (regions.Count == 0 || regions[0].Count < 20) return false;

            List<int> region = regions[0];
            List<Vector2> centres = grid.Centres(region);
            ToyHeightGrid.Pca(centres, out mean, out Vector2 axis, out float along, out float across);

            // Downhill is the way the height drops along the axis.
            float covariance = 0f;
            float meanHeight = grid.MeanHeight(region);
            for (int i = 0; i < region.Count; i++)
                covariance += Vector2.Dot(centres[i] - mean, axis) * (grid.Height[region[i]] - meanHeight);
            downhill = covariance > 0f ? -axis : axis;

            tTop = float.MaxValue;
            tBottom = float.MinValue;
            foreach (Vector2 p in centres)
            {
                float t = Vector2.Dot(p - mean, downhill);
                tTop = Mathf.Min(tTop, t);
                tBottom = Mathf.Max(tBottom, t);
            }
            s.Log($"sloping area: {region.Count} cells, {along:0.00} m long, {across:0.00} m wide, height {meanHeight:0.00} on average");
            return tBottom - tTop > 0.8f;
        }

        /// <summary>The long axis of the toy, downhill toward the lower end (found from the vertex heights at both ends).</summary>
        static void FallbackChute(ToySetup s, ToyHeightGrid grid, Vector2 platformCentre, float groundY, float platformY,
            out Vector2 mean, out Vector2 downhill, out float tTop, out float tBottom)
        {
            var points = new List<Vector2>();
            var heights = new List<float>();
            foreach (Vector3 v in s.WorldVertices())
            {
                points.Add(grid.Frame.ToLocal(v));
                heights.Add(v.y);
            }
            ToyHeightGrid.Pca(points, out mean, out Vector2 axis, out float along, out float across);

            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 p in points)
            {
                float t = Vector2.Dot(p - mean, axis);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
            }

            // Mean height of the vertices in the first and last third along the axis: the chute leaves the higher end.
            float lowSum = 0f, highSum = 0f;
            int lowCount = 0, highCount = 0;
            for (int i = 0; i < points.Count; i++)
            {
                float t = Vector2.Dot(points[i] - mean, axis);
                if (t < min + (max - min) * 0.33f) { lowSum += heights[i]; lowCount++; }
                else if (t > max - (max - min) * 0.33f) { highSum += heights[i]; highCount++; }
            }
            float lowMean = lowCount > 0 ? lowSum / lowCount : 0f;
            float highMean = highCount > 0 ? highSum / highCount : 0f;
            downhill = lowMean > highMean ? axis : -axis;

            tTop = float.MaxValue;
            tBottom = float.MinValue;
            foreach (Vector2 p in points)
            {
                float t = Vector2.Dot(p - mean, downhill);
                tTop = Mathf.Min(tTop, t);
                tBottom = Mathf.Max(tBottom, t);
            }
            // The chute is the lower two thirds.
            tTop = Mathf.Lerp(tTop, tBottom, 0.33f);
            s.Log($"fallback chute: long axis {along:0.00} m x {across:0.00} m, ends at mean heights {lowMean:0.00} and {highMean:0.00}");
        }

        /// <summary>
        /// World points of the sliding surface along the chute centre line, from before the top (on the platform) to the foot, then two points
        /// on the ground past it. The heights are the real top of the mesh, never rising on the way down. chuteEndIndex is the foot.
        /// </summary>
        static List<Vector3> SampleChute(ToySetup s, ToyFrame frame, Vector2 mean, Vector2 downhill, float tStart, float tEnd,
            float groundY, float platformY, IList<Transform> chuteParts, out int chuteEndIndex)
        {
            var samples = new List<Vector3>();
            const float Step = 0.2f;
            int count = Mathf.Max(3, Mathf.CeilToInt((tEnd - tStart) / Step) + 1);
            var heights = new float[count];
            var positions = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(tStart, tEnd, i / (float)(count - 1));
                positions[i] = mean + downhill * t;
                Vector3 world = frame.ToWorld(positions[i], 0f);
                // Only the chute itself: the tower round it (roof, posts, railings) must not catch the ray.
                heights[i] = s.TopAtParts(chuteParts, world.x, world.z, out Vector3 hit) ? Mathf.Min(hit.y, platformY + 0.05f) : float.NaN;
            }

            // Fill gaps from the neighbours; the start is the platform, the end is the lowest known point.
            if (float.IsNaN(heights[0])) heights[0] = platformY;
            for (int i = 1; i < count; i++)
                if (float.IsNaN(heights[i])) heights[i] = heights[i - 1];

            // Smooth a little and never climb: a slide only goes down.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < count - 1; i++)
                    heights[i] = (heights[i - 1] + 2f * heights[i] + heights[i + 1]) * 0.25f;
            for (int i = 1; i < count; i++) heights[i] = Mathf.Min(heights[i], heights[i - 1]);

            for (int i = 0; i < count; i++)
            {
                Vector3 world = frame.ToWorld(positions[i], heights[i]);
                samples.Add(world);
            }
            chuteEndIndex = samples.Count - 1;

            // On from the foot over the ground: the chute usually ends a little above it.
            Vector3 foot = samples[samples.Count - 1];
            Vector3 forward = frame.ToWorldDirection(downhill);
            for (int i = 1; i <= 2; i++)
            {
                Vector3 p = foot + forward * (0.45f * i);
                p.y = Mathf.Max(groundY, Mathf.Lerp(foot.y, groundY, i * 0.5f));
                float ground = s.GroundY(p.x, p.z);
                p.y = i == 2 ? ground : Mathf.Max(ground, p.y);
                samples.Add(p);
            }
            s.Log($"chute surface: {count} points from y {samples[0].y:0.00} to y {foot.y:0.00}, then 2 ground points");
            return samples;
        }

        // ====================================================================================================================
        // The spring riders
        // ====================================================================================================================

        static partial void ConfigureRocker(ToySetup s)
        {
            Transform root = s.Root.transform;
            ToyFrame frame = new ToyFrame(root);
            List<Transform> parts = TopLevelMeshObjects(s);
            foreach (Transform part in parts)
            {
                Bounds b = ToySetup.BoundsOf(part);
                s.Log($"mesh object '{part.name}': bounds centre {ToySetup.Fmt(b.center)}, min y {b.min.y:0.00}, max y {b.max.y:0.00}, size {ToySetup.Fmt(b.size)}");
            }

            // The body is the higher of two objects (the other is the spring); with one object the whole toy rocks.
            Transform body = null;
            Transform spring = null;
            if (parts.Count >= 2)
            {
                var ordered = parts.OrderByDescending(p => ToySetup.BoundsOf(p).center.y).ToList();
                Bounds high = ToySetup.BoundsOf(ordered[0]);
                Bounds low = ToySetup.BoundsOf(ordered[ordered.Count - 1]);
                if (high.center.y - low.center.y > 0.15f && high.min.y > low.min.y + 0.05f)
                {
                    body = ordered[0];
                    spring = ordered[ordered.Count - 1];
                    s.Log($"body is '{body.name}', spring is '{spring.name}'");
                }
            }
            if (body == null)
                s.Log("fallback: the toy is not two clearly stacked objects; the whole toy rocks (smaller swing).");

            Bounds bodyBounds = body != null ? ToySetup.BoundsOf(body) : s.WorldBounds;
            Bounds springBounds = spring != null ? ToySetup.BoundsOf(spring) : s.WorldBounds;
            float amplitude = 14f;
            Vector3 pivotWorld;
            if (body != null)
            {
                float lowest = bodyBounds.min.y;
                float springTop = springBounds.max.y;
                float pivotY = Mathf.Clamp(springTop, lowest, lowest + 0.3f * bodyBounds.size.y);
                pivotWorld = new Vector3(springBounds.center.x, pivotY, springBounds.center.z);
            }
            else
            {
                amplitude = 9f;
                float ground = s.GroundY(s.WorldBounds.center.x, s.WorldBounds.center.z);
                pivotWorld = new Vector3(s.WorldBounds.center.x, ground, s.WorldBounds.center.z);
            }

            // The animal's long axis, from the body's vertices.
            var points = new List<Vector2>();
            var heights = new List<float>();
            foreach (MeshFilter filter in (body != null ? body : root).GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || filter.transform.IsChildOf(s.Anchors)) continue;
                var vertices = new List<Vector3>();
                filter.sharedMesh.GetVertices(vertices);
                foreach (Vector3 v in vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(v);
                    points.Add(frame.ToLocal(world));
                    heights.Add(world.y);
                }
            }
            ToyHeightGrid.Pca(points, out Vector2 mean2, out Vector2 axis2, out float along, out float across);
            if (along < across * 1.25f)
            {
                s.Warn("fallback: the footprint is nearly round; the animal is taken to face the toy's forward.");
                axis2 = Vector2.up;
            }

            // Which end is the head? The higher end; if they are level, the toy's own forward.
            float tMin = float.MaxValue, tMax = float.MinValue;
            foreach (Vector2 p in points)
            {
                float t = Vector2.Dot(p - mean2, axis2);
                tMin = Mathf.Min(tMin, t);
                tMax = Mathf.Max(tMax, t);
            }
            float endA = 0f, endB = 0f;
            int countA = 0, countB = 0;
            for (int i = 0; i < points.Count; i++)
            {
                float t = Vector2.Dot(points[i] - mean2, axis2);
                if (t > tMax - 0.25f * (tMax - tMin)) { endB += heights[i]; countB++; }
                else if (t < tMin + 0.25f * (tMax - tMin)) { endA += heights[i]; countA++; }
            }
            float meanA = countA > 0 ? endA / countA : 0f;
            float meanB = countB > 0 ? endB / countB : 0f;
            Vector2 headDir2 = axis2;
            if (Mathf.Abs(meanA - meanB) > 0.08f) headDir2 = meanB > meanA ? axis2 : -axis2;
            else if (Vector2.Dot(axis2, Vector2.up) < 0f) headDir2 = -axis2;
            Vector3 headDir = frame.ToWorldDirection(headDir2);
            s.Log($"animal axis along {along:0.00} m, across {across:0.00} m; end heights {meanA:0.00} / {meanB:0.00}; head toward {ToySetup.Fmt(headDir)}");

            // The seat: the high, flat saddle in the middle of the animal.
            ToyHeightGrid grid = ToyHeightGrid.Build(s, 0.1f);
            Vector2 mid = mean2 + axis2 * ((tMin + tMax) * 0.5f);
            Vector2 sideDir = new Vector2(-axis2.y, axis2.x);
            var candidates = new List<(Vector2 position, float y)>();
            for (int iz = 0; iz < grid.Nz; iz++)
            {
                for (int ix = 0; ix < grid.Nx; ix++)
                {
                    if (!grid.Has(ix, iz)) continue;
                    Vector2 p = grid.CellCentre(ix, iz);
                    if (Mathf.Abs(Vector2.Dot(p - mid, axis2)) > 0.25f * along) continue;
                    if (Mathf.Abs(Vector2.Dot(p - mid, sideDir)) > 0.3f * Mathf.Max(across, 0.3f)) continue;
                    // Only the body (not the ground-level spring when the toy is two objects).
                    if (grid.At(ix, iz) < bodyBounds.min.y + 0.4f * bodyBounds.size.y) continue;
                    candidates.Add((p, grid.At(ix, iz)));
                }
            }

            Vector2 seatXZ;
            float seatY;
            if (candidates.Count >= 4)
            {
                var sorted = candidates.Select(c => c.y).OrderBy(y => y).ToList();
                seatY = sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * 0.75f), 0, sorted.Count - 1)];
                var saddle = candidates.Where(c => c.y >= seatY - 0.06f).ToList();
                seatXZ = Vector2.zero;
                foreach (var c in saddle) seatXZ += c.position;
                seatXZ /= saddle.Count;
                s.Log($"seat found: {candidates.Count} candidate cells, saddle {saddle.Count} cells at y {seatY:0.00}");
            }
            else
            {
                s.Warn("fallback: no saddle cells found; the seat is at 70% of the body height in its middle.");
                seatXZ = mid;
                seatY = Mathf.Lerp(bodyBounds.min.y, bodyBounds.max.y, 0.7f);
            }

            float rootY = seatY - Mathf.Max(0.05f, s.PoseOffsets.sitSeatHips - SeatHipsAboveSurface);
            Vector3 seatWorld = frame.ToWorld(seatXZ, rootY);

            // Make the pivot (moving the body, or every mesh object), then the seat under it.
            Transform[] moving = body != null ? new[] { body } : parts.ToArray();
            Transform pivot = s.MakePivot("RockerPivot", pivotWorld, moving);
            Transform seat = s.AddPoint("RockerSeat", seatWorld, Quaternion.LookRotation(headDir, Vector3.up));
            seat.SetParent(pivot, true);

            // The exit: beside the animal, on the side toward the middle of the playground (the toy's forward).
            Vector3 sideWorld = frame.ToWorldDirection(sideDir);
            if (Vector3.Dot(sideWorld, frame.Forward) < 0f) sideWorld = -sideWorld;
            Vector3 exitWorld = seatWorld + sideWorld * (0.5f * across + 0.8f);
            exitWorld.y = s.GroundY(exitWorld.x, exitWorld.z);
            Transform exit = s.AddPoint("RockerExit", exitWorld, Quaternion.LookRotation(sideWorld, Vector3.up));

            s.AddPoint("PreviewSeat", seatWorld, Quaternion.LookRotation(headDir, Vector3.up));

            // The rock axis in the pivot's own space: across the animal.
            Vector3 axisWorld = Vector3.Cross(Vector3.up, headDir).normalized;
            Vector3 axisLocal = pivot.InverseTransformDirection(axisWorld);

            var routine = s.SetRoutine<RockerRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "pivot", pivot);
            PlaygroundPrototypeBuilder.SetObject(routine, "seat", seat);
            PlaygroundPrototypeBuilder.SetVector3(routine, "rockAxis", axisLocal);
            PlaygroundPrototypeBuilder.SetFloat(routine, "amplitudeDegrees", amplitude);
            s.Log($"rocker ready: pivot {ToySetup.Fmt(pivotWorld)}, seat {ToySetup.Fmt(seatWorld)}, rock axis (pivot space) {ToySetup.Fmt(axisLocal)}, swing {amplitude:0}°, exit {ToySetup.Fmt(exitWorld)}");
        }

        // ====================================================================================================================
        // The roundabout
        // ====================================================================================================================

        static partial void ConfigureRoundabout(ToySetup s)
        {
            Bounds b = s.WorldBounds;
            ToyFrame frame = new ToyFrame(s.Root.transform);
            Vector3 centre = new Vector3(b.center.x, b.min.y, b.center.z);
            float groundY = s.GroundY(centre.x, centre.z);

            // The disc's radius and top: march outward from the middle in 12 directions while there is a flat top near the same height.
            float guessRadius = 0.5f * Mathf.Min(b.size.x, b.size.z);
            var rings = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float x = centre.x + d.x * guessRadius * 0.55f;
                float z = centre.z + d.z * guessRadius * 0.55f;
                if (s.TopAt(x, z, out Vector3 hit)) rings.Add(hit.y);
            }
            bool centreHit = s.TopAt(centre.x, centre.z, out Vector3 centreTop);
            float discTop;
            if (rings.Count >= 4)
            {
                rings.Sort();
                discTop = rings[rings.Count / 2];
            }
            else
            {
                s.Warn("fallback: few hits around the middle of the roundabout; the top is taken as 35% of its height.");
                discTop = Mathf.Lerp(b.min.y, b.max.y, 0.35f);
            }
            s.Log($"roundabout: middle {(centreHit ? centreTop.y.ToString("0.00") : "no hit")}, ring median (disc top) {discTop:0.00}, ground {groundY:0.00}, half size {guessRadius:0.00}");

            var radii = new List<float>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + 0.2f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float reached = 0f;
                for (float r = 0.1f; r < guessRadius + 0.6f; r += 0.05f)
                {
                    if (s.TopAt(centre.x + d.x * r, centre.z + d.z * r, out Vector3 hit) && Mathf.Abs(hit.y - discTop) < 0.15f) reached = r;
                }
                if (reached > 0f) radii.Add(reached);
            }
            float radius = guessRadius;
            if (radii.Count >= 4)
            {
                radii.Sort();
                radius = radii[radii.Count / 2];
            }
            else
            {
                s.Warn($"fallback: the disc edge could not be found; using the half size {guessRadius:0.00}.");
            }

            // Solid disc, or open spokes and bars?
            int solidHits = 0, solidTests = 0;
            for (int ring = 1; ring <= 3; ring++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f + 0.4f;
                    float r = radius * (0.3f + 0.2f * ring);
                    solidTests++;
                    if (s.TopAt(centre.x + Mathf.Sin(a) * r, centre.z + Mathf.Cos(a) * r, out Vector3 hit) && Mathf.Abs(hit.y - discTop) < 0.12f) solidHits++;
                }
            }
            bool solid = solidHits >= solidTests * 0.7f;
            s.Log($"disc radius {radius:0.00}, solid top: {solid} ({solidHits} of {solidTests} test points at the top height)");

            ToyPose pose = solid ? ToyPose.SitSlide : ToyPose.SitSeat;
            if (!solid) s.Warn("the disc is not a solid floor: he sits on the seat pose at the median height of the bars (check the preview).");

            // The spinner turns the whole mesh about the middle.
            List<Transform> parts = TopLevelMeshObjects(s);
            Transform spinner = s.MakePivot("Spinner", centre, parts.ToArray());

            // The seat near the edge, on the side facing the middle of the playground; he faces along the turn.
            float seatRadius = Mathf.Max(0.3f, radius - 0.4f);
            Vector3 outDir = frame.Forward;
            Vector3 seatWorld = centre + outDir * seatRadius;
            float sink = solid ? s.PoseOffsets.sitSlideHips * FloorSitSink : Mathf.Max(0.05f, s.PoseOffsets.sitSeatHips - SeatHipsAboveSurface);
            seatWorld.y = discTop - sink;
            // Turning positive about the vertical axis moves a point at r in the direction cross(up, r).
            Vector3 along = Vector3.Cross(Vector3.up, outDir).normalized;
            Transform seat = s.AddPoint("RoundaboutSeat", seatWorld, Quaternion.LookRotation(along, Vector3.up));
            seat.SetParent(spinner, true);

            Vector3 exitWorld = centre + outDir * (radius + 0.9f);
            exitWorld.y = s.GroundY(exitWorld.x, exitWorld.z);
            Transform exit = s.AddPoint("RoundaboutExit", exitWorld, Quaternion.LookRotation(outDir, Vector3.up));

            var routine = s.SetRoutine<RoundaboutRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "spinner", spinner);
            PlaygroundPrototypeBuilder.SetObject(routine, "seat", seat);
            ToySetup.SetSerialized(routine, "sitPose", p => p.enumValueIndex = (int)pose);
            s.Log($"roundabout ready: spinner at {ToySetup.Fmt(centre)}, seat {ToySetup.Fmt(seatWorld)} ({pose}), exit {ToySetup.Fmt(exitWorld)}");
        }

        // ====================================================================================================================
        // The tunnel
        // ====================================================================================================================

        static partial void ConfigureTunnel(ToySetup s)
        {
            ToyFrame frame = new ToyFrame(s.Root.transform);
            var points = new List<Vector2>();
            var ys = new List<float>();
            foreach (Vector3 v in s.WorldVertices())
            {
                points.Add(frame.ToLocal(v));
                ys.Add(v.y);
            }
            ToyHeightGrid.Pca(points, out Vector2 mean, out Vector2 axis, out float along, out float across);
            if (along < across * 1.3f)
            {
                s.Warn($"fallback: the footprint is nearly square ({along:0.00} x {across:0.00}); the tunnel runs along the toy's forward axis.");
                axis = Vector2.up;
            }
            Vector2 sideDir = new Vector2(-axis.y, axis.x);

            float tMin = float.MaxValue, tMax = float.MinValue;
            foreach (Vector2 p in points)
            {
                float t = Vector2.Dot(p - mean, axis);
                tMin = Mathf.Min(tMin, t);
                tMax = Mathf.Max(tMax, t);
            }
            s.Log($"tunnel axis local ({axis.x:0.00}, {axis.y:0.00}), length {tMax - tMin:0.00} m, width {across:0.00} m; mouths at t {tMin:0.00} and {tMax:0.00}");

            // The centre line: the mean sideways position of the vertices in slices along the axis.
            const int Slices = 8;
            var centreSide = new float[Slices];
            for (int i = 0; i < Slices; i++)
            {
                float t0 = Mathf.Lerp(tMin, tMax, i / (float)Slices);
                float t1 = Mathf.Lerp(tMin, tMax, (i + 1) / (float)Slices);
                float sum = 0f;
                int count = 0;
                foreach (Vector2 p in points)
                {
                    float t = Vector2.Dot(p - mean, axis);
                    if (t < t0 || t > t1) continue;
                    sum += Vector2.Dot(p - mean, sideDir);
                    count++;
                }
                centreSide[i] = count > 0 ? sum / count : 0f;
            }

            Vector2 AtT(float t)
            {
                float k = Mathf.Clamp01((t - tMin) / Mathf.Max(0.01f, tMax - tMin)) * Slices - 0.5f;
                int i0 = Mathf.Clamp(Mathf.FloorToInt(k), 0, Slices - 1);
                int i1 = Mathf.Clamp(i0 + 1, 0, Slices - 1);
                float side = Mathf.Lerp(centreSide[i0], centreSide[i1], Mathf.Clamp01(k - i0));
                return mean + axis * t + sideDir * side;
            }

            // The kid's path on the ground: from half a metre outside one mouth to half a metre outside the other (symmetrical, so it
            // also works the other way round). Exits another 0.8 m further out.
            const float Outside = 0.5f;
            var tValues = new List<float> { tMin - Outside, tMin + 0.1f };
            for (int i = 1; i < 5; i++) tValues.Add(Mathf.Lerp(tMin, tMax, i / 5f));
            tValues.Add(tMax - 0.1f);
            tValues.Add(tMax + Outside);

            Vector3 firstDirection = frame.ToWorldDirection(axis);
            Quaternion forward = Quaternion.LookRotation(firstDirection, Vector3.up);
            var pathTransforms = new List<Transform>();
            for (int i = 0; i < tValues.Count; i++)
            {
                Vector3 flat = frame.ToWorld(AtT(tValues[i]), 0f);
                flat.y = s.GroundY(flat.x, flat.z);
                pathTransforms.Add(s.AddPoint("TunnelPath_" + i, flat, forward));
            }

            Vector3 firstExit = frame.ToWorld(AtT(tMin - Outside - 0.8f), 0f);
            firstExit.y = s.GroundY(firstExit.x, firstExit.z);
            Vector3 lastExit = frame.ToWorld(AtT(tMax + Outside + 0.8f), 0f);
            lastExit.y = s.GroundY(lastExit.x, lastExit.z);
            Transform exitFirst = s.AddPoint("TunnelExitFirst", firstExit, Quaternion.LookRotation(-firstDirection, Vector3.up));
            Transform exitLast = s.AddPoint("TunnelExitLast", lastExit, forward);
            Transform rider = s.AddPoint("TunnelRider", pathTransforms[0].position, forward);

            // For the preview: inside the tunnel, at the middle.
            s.AddPoint("PreviewMiddle", pathTransforms[pathTransforms.Count / 2].position, forward);

            var routine = s.SetRoutine<TunnelRoutine>(exitLast);
            PlaygroundPrototypeBuilder.SetObject(routine, "rider", rider);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "path", pathTransforms.Select(t => (Object)t).ToList());
            PlaygroundPrototypeBuilder.SetObject(routine, "exitFirst", exitFirst);
            PlaygroundPrototypeBuilder.SetObject(routine, "exitLast", exitLast);
            s.Log($"tunnel ready: path from {ToySetup.Fmt(pathTransforms[0].position)} to {ToySetup.Fmt(pathTransforms[pathTransforms.Count - 1].position)}, "
                + $"exits {ToySetup.Fmt(firstExit)} and {ToySetup.Fmt(lastExit)}");
        }

        // ====================================================================================================================
        // Helpers
        // ====================================================================================================================

        /// <summary>The top-most objects of the toy that carry a mesh renderer (not the anchors), so each moves with its own children.</summary>
        static List<Transform> TopLevelMeshObjects(ToySetup s)
        {
            var all = new List<Transform>();
            foreach (MeshRenderer renderer in s.Root.GetComponentsInChildren<MeshRenderer>(false))
            {
                Transform t = renderer.transform;
                if (t == s.Root.transform || t.IsChildOf(s.Anchors)) continue;
                all.Add(t);
            }

            var top = new List<Transform>();
            foreach (Transform t in all)
            {
                bool nested = false;
                for (Transform p = t.parent; p != null && p != s.Root.transform; p = p.parent)
                {
                    if (all.Contains(p))
                    {
                        nested = true;
                        break;
                    }
                }
                if (!nested) top.Add(t);
            }
            return top;
        }
    }
}
