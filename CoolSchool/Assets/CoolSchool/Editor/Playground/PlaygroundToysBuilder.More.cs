using System;
using System.Collections.Generic;
using System.Linq;
using CoolSchool.Playground;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The configure methods of the monkey bars, seesaw, swings, fort, tyre wall, track ride and ship (Docs/specs/playground-toys-contract.md).
    /// Nothing here assumes where a Synty pivot is: anchors come from ray casts onto the toy's own triangles and from its local bounds, and
    /// every anchor, pivot and fallback is logged with the "PlaygroundToys:" prefix. What the Synty files really contain (checked in the
    /// FBX files): the seesaw plank, the two swings (seat with chains) and the track ride handle are separate mesh objects already, so they
    /// are moved as they are and only split with ToyMeshSplitter when such a part is missing. The monkey bars and the track ride are flat
    /// kit pieces (a ladder 1 x 2 m and a cable frame, 6 cm and 35 cm tall); the builder lifts them onto poles so he can hang from them.
    /// </summary>
    public static partial class PlaygroundToysBuilder
    {
        /// <summary>The kid's feet stay this far off the ground while hanging from the bars.</summary>
        const float MoreHangClearance = 0.30f;
        /// <summary>How far from the wall he stands when he climbs it (his root, to the face of the wall).</summary>
        const float MoreClimbStandoff = 0.25f;
        /// <summary>He stops climbing when his root is this far below the top edge, then steps over it.</summary>
        const float MoreClimbStopBelowTop = 0.60f;
        /// <summary>The cable of the track ride drops this much between the two ends of the handle's trip.</summary>
        const float MoreCableDrop = 0.30f;

        static partial void ConfigureMonkeyBars(ToySetup s) { MoreMonkeyBars(s); }
        static partial void ConfigureSeesaw(ToySetup s) { MoreSeesaw(s); }
        static partial void ConfigureSwings(ToySetup s) { MoreSwings(s); }
        static partial void ConfigureFort(ToySetup s) { MoreClimb(s, false); }
        static partial void ConfigureTyreWall(ToySetup s) { MoreClimb(s, true); }
        static partial void ConfigureTrackRide(ToySetup s) { MoreTrackRide(s); }
        static partial void ConfigureShip(ToySetup s) { MoreShip(s); }

        // ---- Small shared pieces ----

        static float MoreHips(ToySetup s)
        {
            return s.PoseOffsets.sitSeatHips > 0.1f ? s.PoseOffsets.sitSeatHips : 0.45f;
        }

        static float MoreHands(ToySetup s)
        {
            return s.PoseOffsets.hangHands > 0.5f ? s.PoseOffsets.hangHands : 1.35f;
        }

        /// <summary>The ground under a world point (any collider that is not this toy), as a point.</summary>
        static Vector3 MoreGroundPoint(ToySetup s, Vector3 world)
        {
            return s.GroundAt(world.x, world.z, out Vector3 hit) ? hit : new Vector3(world.x, s.WorldBounds.min.y, world.z);
        }

        static Quaternion MoreLook(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        // ================================================================================================
        // Monkey bars
        // ================================================================================================

        /// <summary>
        /// The Synty monkey bars are a flat ladder (1 x 2 m, 6 cm tall) meant to be fixed up high. The mesh is lifted so the bars are one
        /// kid's reach plus a little above the ground (measured by the Hang clip), four poles hold it, and he shimmies along the long side:
        /// the anchor sits under the middle of the ladder, turned a quarter so the sideways ledge shuffle follows the bars.
        /// </summary>
        static void MoreMonkeyBars(ToySetup s)
        {
            Transform root = s.Root.transform;
            Transform body = MoreToyGeometry.BodyOf(s);
            if (body == null || !MoreToyGeometry.LocalBounds(root, body, out Bounds lb))
            {
                s.Warn("fallback: no mesh found for the bars, so there is no routine.");
                return;
            }

            bool longZ = lb.size.z >= lb.size.x;
            float hands = MoreHands(s);
            float groundY = s.GroundY(root.position.x, root.position.z);
            float barY = groundY + hands + MoreHangClearance;
            float lift = barY - root.TransformPoint(lb.center).y;
            s.Log($"ladder size {ToySetup.Fmt(lb.size)} (long side local {(longZ ? "z" : "x")}); lifted by {lift:0.00} m so the bars are at y {barY:0.00} "
                + $"(ground {groundY:0.00} + hands {hands:0.00} + clearance {MoreHangClearance:0.00})");
            body.position += Vector3.up * lift;

            // Four poles under the two rails at both ends.
            float crossMin = longZ ? lb.min.x : lb.min.z;
            float crossSize = longZ ? lb.size.x : lb.size.z;
            float longMin = (longZ ? lb.min.z : lb.min.x) + 0.04f;
            float longMax = (longZ ? lb.max.z : lb.max.x) - 0.04f;
            int poleIndex = 0;
            foreach (float fraction in new[] { 0.25f, 0.75f })
            {
                foreach (float along in new[] { longMin, longMax })
                {
                    float across = crossMin + crossSize * fraction;
                    Vector3 local = longZ ? new Vector3(across, lb.center.y, along) : new Vector3(along, lb.center.y, across);
                    Vector3 top = root.TransformPoint(local);
                    top.y = barY;
                    MoreToyGeometry.Pole(s, "Pole_" + poleIndex++, new Vector3(top.x, MoreGroundPoint(s, top).y, top.z), top, 0.045f);
                }
            }
            MoreToyGeometry.RefreshBounds(s);
            s.LogHeightMap();

            // The shimmy runs along the long side, between 0.3 m in from each end, under the middle of the ladder.
            float startAlong = (longZ ? lb.min.z : lb.min.x) + 0.30f;
            float endAlong = (longZ ? lb.max.z : lb.max.x) - 0.30f;
            Vector3 PointAlong(float along)
            {
                Vector3 local = longZ ? new Vector3(lb.center.x, lb.center.y, along) : new Vector3(along, lb.center.y, lb.center.z);
                Vector3 world = root.TransformPoint(local);
                world.y = barY - hands;
                return world;
            }

            Vector3 a = PointAlong(startAlong);
            Vector3 b = PointAlong(endAlong);
            // Start at the end that is farther from the middle of the playground, so he drops off toward the open space.
            if (MoreToyGeometry.NearerToMiddle(a, b)) { Vector3 swap = a; a = b; b = swap; }

            Vector3 travel = MoreToyGeometry.Flat(b - a).normalized;
            Vector3 facing = Vector3.Cross(travel, Vector3.up).normalized;
            Quaternion hang = Quaternion.LookRotation(facing, Vector3.up);
            Transform start = s.AddPoint("HangStart", a, hang);
            Transform end = s.AddPoint("HangEnd", b, hang);
            Transform anchor = s.AddPoint("HangAnchor", a, hang);

            Vector3 exitGround = MoreGroundPoint(s, b + travel * 0.8f);
            Transform exit = s.AddPoint("Exit", exitGround, MoreLook(travel));

            var routine = s.SetRoutine<MonkeyBarsRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            PlaygroundPrototypeBuilder.SetObject(routine, "startPoint", start);
            PlaygroundPrototypeBuilder.SetObject(routine, "endPoint", end);
            s.Log($"hang route {ToySetup.Fmt(a)} -> {ToySetup.Fmt(b)}, he faces {ToySetup.Fmt(facing)} (the shuffle goes to his right)");
        }

        // ================================================================================================
        // Seesaw
        // ================================================================================================

        static void MoreSeesaw(ToySetup s)
        {
            Transform root = s.Root.transform;

            // The plank: the highest of the Synty model's own moving parts (it came as a separate mesh object "..._Top").
            List<Transform> parts = MoreToyGeometry.MovingParts(s);
            Transform plankPart = parts.OrderByDescending(p => ToySetup.BoundsOf(p).center.y).FirstOrDefault();
            Transform plank;
            Bounds lb;
            Vector3 pivotLocal;
            if (plankPart != null && MoreToyGeometry.LocalBounds(root, plankPart, out lb))
            {
                pivotLocal = new Vector3(lb.center.x, lb.min.y + 0.04f, lb.center.z);
                plank = s.MakePivot("PlankPivot", root.TransformPoint(pivotLocal), plankPart);
                s.Log($"plank found as the mesh object '{plankPart.name}' (local size {ToySetup.Fmt(lb.size)}); pivot at its underside, in the middle");
            }
            else
            {
                Transform body = MoreToyGeometry.BodyOf(s);
                if (body == null || !MoreToyGeometry.LocalBounds(root, body, out Bounds whole))
                {
                    s.Warn("fallback: no mesh found for the seesaw, so there is no routine.");
                    return;
                }

                float y0 = whole.min.y + 0.45f * whole.size.y;
                var box = new Bounds(new Vector3(whole.center.x, (y0 + whole.max.y) * 0.5f + 0.05f, whole.center.z),
                    new Vector3(whole.size.x + 0.2f, whole.max.y - y0 + 0.1f, whole.size.z + 0.2f));
                pivotLocal = new Vector3(whole.center.x, y0, whole.center.z);
                s.Warn($"fallback: the seesaw has no separate plank mesh; splitting the mesh above local y {y0:0.00} off as the plank.");
                plank = s.SplitMesh("Plank", box, root.TransformPoint(pivotLocal));
                MoreToyGeometry.LocalBounds(root, plank, out lb);
            }

            bool longZ = lb.size.z >= lb.size.x;
            Vector3 longLocal = longZ ? Vector3.forward : Vector3.right;
            Vector3 acrossLocal = longZ ? Vector3.right : Vector3.forward;
            float half = longZ ? lb.extents.z : lb.extents.x;
            float seatOffset = Mathf.Max(0.35f, half - 0.20f);

            Vector3 pivotWorld = root.TransformPoint(pivotLocal);
            Vector3 longWorld = root.TransformDirection(longLocal).normalized;
            Vector3 endA = pivotWorld + longWorld * seatOffset;
            Vector3 endB = pivotWorld - longWorld * seatOffset;
            // He sits on the end nearer to the middle of the playground (the toy faces it); the teddy keeps the other end.
            bool kidIsA = MoreToyGeometry.NearerToMiddle(endA, endB);
            Vector3 kidEnd = kidIsA ? endA : endB;
            Vector3 teddyEnd = kidIsA ? endB : endA;

            float kidTop;
            float teddyTop;
            using (var probe = new MoreToyGeometry.Probe(s))
            {
                float fromY = s.WorldBounds.max.y + 3f;
                float distance = s.WorldBounds.size.y + 6f;
                kidTop = probe.Top(kidEnd.x, kidEnd.z, fromY, distance, out RaycastHit hit1) ? hit1.point.y : lb.max.y + root.position.y - 0.12f;
                teddyTop = probe.Top(teddyEnd.x, teddyEnd.z, fromY, distance, out RaycastHit hit2) ? hit2.point.y : kidTop;
            }
            s.Log($"seat tops: his end {kidTop:0.00}, teddy end {teddyTop:0.00} (plank pivot at y {pivotWorld.y:0.00}, half length {half:0.00}, seats {seatOffset:0.00} m from the middle)");

            Vector3 toMiddle = (pivotWorld - kidEnd);
            Vector3 anchorPosition = new Vector3(kidEnd.x, kidTop - MoreHips(s), kidEnd.z);
            Transform anchor = s.AddPoint("SeatAnchor", anchorPosition, MoreLook(toMiddle));
            anchor.SetParent(plank, true);

            // The tilt axis, signed so that a positive angle lowers his end.
            Vector3 endLocal = plank.InverseTransformDirection(MoreToyGeometry.Flat(kidEnd - pivotWorld).normalized);
            if ((Quaternion.AngleAxis(10f, acrossLocal) * endLocal).y > 0f) acrossLocal = -acrossLocal;

            MorePlaceTeddy(s, plank, new Vector3(teddyEnd.x, teddyTop, teddyEnd.z), pivotWorld - teddyEnd);

            Vector3 outward = MoreToyGeometry.Flat(kidEnd - pivotWorld).normalized;
            Transform exit = s.AddPoint("Exit", MoreGroundPoint(s, kidEnd + outward * 1.0f), MoreLook(outward));

            var routine = s.SetRoutine<SeesawRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "plank", plank);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            PlaygroundPrototypeBuilder.SetVector3(routine, "tiltAxis", acrossLocal);
            s.Log($"tilt axis {ToySetup.Fmt(acrossLocal)} in the plank pivot's frame");
        }

        /// <summary>A plush teddy on the far seat, a child of the plank pivot so it tilts with the plank. Nothing happens when no plush is found.</summary>
        static void MorePlaceTeddy(ToySetup s, Transform parent, Vector3 seatPoint, Vector3 faceDirection)
        {
            GameObject prefab = MoreToyGeometry.FindPrefab("Teddy");
            string how = "Teddy";
            if (prefab == null) { prefab = MoreToyGeometry.FindPrefab("SM_Prop_Plush_Bear_01"); how = "Plush_Bear"; }
            if (prefab == null) { prefab = MoreToyGeometry.FindPrefab("Plush"); how = "Plush"; }
            if (prefab == null) { prefab = MoreToyGeometry.FindPrefab("Bear"); how = "Bear"; }
            if (prefab == null)
            {
                s.Warn("fallback: no teddy, plush or bear prefab found, so the far seat stays empty.");
                return;
            }

            var teddy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            teddy.name = "Teddy";
            MoreToyGeometry.MakeScenery(teddy);
            teddy.transform.rotation = MoreLook(faceDirection);

            // About half a metre tall, sitting on the plank.
            if (PlaygroundPrototypeBuilder.TryGetBounds(teddy, out Bounds bounds) && bounds.size.y > 0.01f)
            {
                teddy.transform.localScale *= 0.5f / bounds.size.y;
                PlaygroundPrototypeBuilder.TryGetBounds(teddy, out bounds);
                teddy.transform.position += new Vector3(seatPoint.x - bounds.center.x, seatPoint.y - bounds.min.y, seatPoint.z - bounds.center.z);
            }
            else
            {
                teddy.transform.position = seatPoint;
            }
            s.ClearStatic(teddy.transform);
            s.Dirty();
            s.Log($"teddy ({how}: {prefab.name}) sits at {ToySetup.Fmt(seatPoint)}, facing the middle of the plank");
        }

        // ================================================================================================
        // Swings
        // ================================================================================================

        static void MoreSwings(ToySetup s)
        {
            Transform root = s.Root.transform;
            Transform body = MoreToyGeometry.BodyOf(s);
            if (body == null || !MoreToyGeometry.LocalBounds(root, body, out Bounds frame))
            {
                s.Warn("fallback: no mesh found for the swing frame, so there is no routine.");
                return;
            }

            // The top bar runs along the longer horizontal side of the frame; the swings move across it.
            bool barAlongX = frame.size.x >= frame.size.z;
            Vector3 axisLocal = barAlongX ? Vector3.right : Vector3.forward;
            Vector3 motionLocal = barAlongX ? Vector3.forward : Vector3.right;

            // The swings: the Synty model's own moving parts (a seat with its chains each: "..._Swing_1" and "_Swing_2").
            List<Transform> parts = MoreToyGeometry.MovingParts(s)
                .OrderBy(p => ToySetup.BoundsOf(p).center.x + ToySetup.BoundsOf(p).center.z).ToList();
            if (parts.Count == 0)
            {
                s.Warn("fallback: the Synty model has no separate swing meshes; splitting a seat with its chains off by two boxes.");
                float y0 = frame.min.y + 0.18f * frame.size.y;
                float y1 = frame.min.y + 0.95f * frame.size.y;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 centre = frame.center + (barAlongX ? Vector3.right : Vector3.forward) * (side * 0.206f * (barAlongX ? frame.size.x : frame.size.z));
                    centre.y = (y0 + y1) * 0.5f;
                    var box = new Bounds(centre, barAlongX
                        ? new Vector3(0.213f * frame.size.x, y1 - y0, frame.size.z + 0.2f)
                        : new Vector3(frame.size.x + 0.2f, y1 - y0, 0.213f * frame.size.z));
                    Vector3 pivotLocal = new Vector3(centre.x, y1 - 0.06f, centre.z);
                    parts.Add(s.SplitMesh(side < 0 ? "SwingA" : "SwingB", box, root.TransformPoint(pivotLocal)));
                }
            }
            s.Log($"{parts.Count} swing part(s): " + string.Join(", ", parts.Select(p => p.name)));

            var pivots = new List<Object>();
            var anchors = new List<Object>();
            Vector3 middleOfToy = root.TransformPoint(frame.center);
            for (int i = 0; i < parts.Count; i++)
            {
                Transform part = parts[i];
                if (!MoreToyGeometry.LocalBounds(root, part, out Bounds pb)) continue;

                // The pivot sits in the top bar: the top of the chains.
                Vector3 pivotLocal = new Vector3(pb.center.x, pb.max.y - 0.03f, pb.center.z);
                Vector3 seatLocal = new Vector3(pb.center.x, pb.max.y - 0.35f, pb.center.z);
                Vector3 seatFrom = root.TransformPoint(seatLocal);

                // The seat's top: a ray from below the bar straight down onto this swing's own triangles.
                float seatTop;
                using (var probe = new MoreToyGeometry.Probe(part))
                {
                    if (probe.Top(seatFrom.x, seatFrom.z, seatFrom.y, 6f, out RaycastHit hit)) seatTop = hit.point.y;
                    else
                    {
                        seatTop = root.TransformPoint(new Vector3(pb.center.x, pb.min.y + 0.07f, pb.center.z)).y;
                        s.Warn($"fallback: no seat surface found under swing {i}; assuming it is 7 cm above the lowest point ({seatTop:0.00}).");
                    }
                }

                Transform pivot = s.MakePivot("SwingPivot_" + i, root.TransformPoint(pivotLocal), part);
                // He faces the way that points to the middle of the playground.
                Vector3 motionWorld = root.TransformDirection(motionLocal).normalized;
                Vector3 toMiddle = new Vector3(-seatFrom.x, 0f, -seatFrom.z);
                if (Vector3.Dot(motionWorld, toMiddle) < 0f) motionWorld = -motionWorld;

                Vector3 anchorPosition = new Vector3(seatFrom.x, seatTop - MoreHips(s), seatFrom.z);
                Transform anchor = s.AddPoint("SeatAnchor_" + i, anchorPosition, MoreLook(motionWorld));
                anchor.SetParent(pivot, true);
                pivots.Add(pivot);
                anchors.Add(anchor);
                s.Log($"swing {i}: pivot at {ToySetup.Fmt(pivot.position)} (chain top), seat top y {seatTop:0.00}, hips {MoreHips(s):0.00} below it");
            }

            if (pivots.Count == 0)
            {
                s.Warn("fallback: no swing could be set up, so there is no routine.");
                return;
            }

            Vector3 facingOut = root.TransformDirection(motionLocal).normalized;
            if (Vector3.Dot(facingOut, -MoreToyGeometry.Flat(middleOfToy)) < 0f) facingOut = -facingOut;
            float reach = barAlongX ? frame.extents.z : frame.extents.x;
            Transform exit = s.AddPoint("Exit", MoreGroundPoint(s, middleOfToy + facingOut * (reach + 0.8f)), MoreLook(facingOut));

            var routine = s.SetRoutine<SwingRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "pivots", pivots);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "anchors", anchors);
            PlaygroundPrototypeBuilder.SetVector3(routine, "axis", axisLocal);
            s.Log($"swing axis {ToySetup.Fmt(axisLocal)} (along the top bar), {pivots.Count} swing(s)");
        }

        // ================================================================================================
        // Fort and tyre wall: climb up, cheer, jump down
        // ================================================================================================

        /// <summary>
        /// Finds the highest flat platform by ray casts onto the toy's own triangles (a grid in the toy's local space), picks the face to
        /// climb (the edge of the platform that has the least stuff past it, preferring a wide edge and the side the toy faces), and puts the
        /// foot, the point under the top edge, the standing point on top and the exit point. With jumpFarSide he stands in the middle of a
        /// thin platform (the tyre wall) and jumps down the other side; otherwise he jumps out the way he climbed.
        /// </summary>
        static void MoreClimb(ToySetup s, bool jumpFarSide)
        {
            Transform root = s.Root.transform;
            if (!MoreToyGeometry.LocalBoundsOfToy(s, out Bounds lb))
            {
                s.Warn("fallback: no mesh found, so there is no routine.");
                return;
            }

            const float cell = 0.08f;
            int nx = Mathf.Clamp(Mathf.CeilToInt(lb.size.x / cell), 4, 140);
            int nz = Mathf.Clamp(Mathf.CeilToInt(lb.size.z / cell), 4, 140);
            float dx = lb.size.x / nx;
            float dz = lb.size.z / nz;
            var height = new float[nx, nz];
            var has = new bool[nx, nz];
            var facesUp = new bool[nx, nz];
            float groundY = s.GroundY(root.position.x, root.position.z);
            float hmax = float.MinValue;

            using (var probe = new MoreToyGeometry.Probe(s))
            {
                float fromY = s.WorldBounds.max.y + 3f;
                float distance = s.WorldBounds.size.y + 6f;
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        Vector3 world = root.TransformPoint(new Vector3(lb.min.x + (i + 0.5f) * dx, lb.center.y, lb.min.z + (j + 0.5f) * dz));
                        if (!probe.Top(world.x, world.z, fromY, distance, out RaycastHit hit)) continue;
                        has[i, j] = true;
                        height[i, j] = hit.point.y;
                        facesUp[i, j] = hit.normal.y >= 0.85f;
                        hmax = Mathf.Max(hmax, hit.point.y);
                    }
                }
            }

            // ---- A slope to climb (the fort's own slide) ----
            if (!jumpFarSide && MoreRampClimb(s, lb, nx, nz, dx, dz, height, has, groundY)) return;

            // ---- The platform ----
            bool IsFlat(int i, int j)
            {
                if (!has[i, j] || !facesUp[i, j]) return false;
                for (int di = -1; di <= 1; di++)
                {
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= nx || b >= nz || !has[a, b]) continue;
                        if (Mathf.Abs(height[a, b] - height[i, j]) > 0.03f) return false;
                    }
                }
                return true;
            }

            List<Vector2Int> Grow(Vector2Int seed, Func<int, int, bool> member)
            {
                var found = new List<Vector2Int>();
                var seen = new bool[nx, nz];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(seed);
                seen[seed.x, seed.y] = true;
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    found.Add(c);
                    foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        Vector2Int n = c + step;
                        if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y] || !member(n.x, n.y)) continue;
                        seen[n.x, n.y] = true;
                        queue.Enqueue(n);
                    }
                }
                return found;
            }

            List<Vector2Int> platform = null;
            string how = "";
            var flatCells = new List<Vector2Int>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    if (IsFlat(i, j)) flatCells.Add(new Vector2Int(i, j));
            flatCells.Sort((p, q) => height[q.x, q.y].CompareTo(height[p.x, p.y]));

            var visited = new bool[nx, nz];
            foreach (Vector2Int start in flatCells)
            {
                if (visited[start.x, start.y]) continue;
                float reference = height[start.x, start.y];

                List<Vector2Int> group = Grow(start, (a, b) => has[a, b] && IsFlat(a, b) && Mathf.Abs(height[a, b] - reference) <= 0.05f);
                foreach (Vector2Int c in group) visited[c.x, c.y] = true;
                if (group.Count >= 24)
                {
                    platform = group;
                    how = $"highest flat platform at y {reference:0.00}, {group.Count} cells of {cell:0.00} m";
                    break;
                }
            }

            if (platform == null && hmax > float.MinValue)
            {
                // No flat top (the tyre wall is round): the cells within 15 cm of the highest point, the biggest connected group.
                for (int i = 0; i < nx && platform == null; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        if (!has[i, j] || height[i, j] < hmax - 0.01f) continue;
                        List<Vector2Int> group = Grow(new Vector2Int(i, j), (a, b) => has[a, b] && height[a, b] >= hmax - 0.15f);
                        if (platform == null || group.Count > platform.Count) platform = group;
                    }
                }
                if (platform != null) how = $"fallback: no flat top; used the cells within 15 cm of the highest point y {hmax:0.00}, {platform.Count} cells";
            }

            Vector2 centre;
            float topY;
            var cellsLocal = new List<Vector2>();
            if (platform == null || platform.Count < 3)
            {
                s.Warn("fallback: no platform found; using the top of the toy's bounds and its middle.");
                centre = new Vector2(lb.center.x, lb.center.z);
                topY = s.WorldBounds.max.y;
                cellsLocal.Add(centre);
            }
            else
            {
                foreach (Vector2Int c in platform) cellsLocal.Add(new Vector2(lb.min.x + (c.x + 0.5f) * dx, lb.min.z + (c.y + 0.5f) * dz));
                centre = new Vector2(cellsLocal.Average(p => p.x), cellsLocal.Average(p => p.y));
                var heights = platform.Select(c => height[c.x, c.y]).OrderBy(h => h).ToList();
                topY = heights[heights.Count / 2];
            }
            s.Log(how.Length > 0 ? how : "platform from the bounds");
            s.Log($"platform middle (local) {centre.x:0.00}, {centre.y:0.00}, top y {topY:0.00}, ground {groundY:0.00}, climb height {topY - groundY:0.00} m");

            // ---- The face to climb ----
            Vector2[] faces = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };
            Vector3 forwardWorld = root.forward;
            int bestFace = 0;
            float bestScore = float.MaxValue;
            float[] edge = new float[4];
            for (int f = 0; f < 4; f++)
            {
                Vector2 d = faces[f];
                Vector2 lateral = new Vector2(-d.y, d.x);
                float e = cellsLocal.Max(p => Vector2.Dot(p - centre, d));
                float latMin = cellsLocal.Min(p => Vector2.Dot(p - centre, lateral));
                float latMax = cellsLocal.Max(p => Vector2.Dot(p - centre, lateral));
                float all = e;
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        if (!has[i, j] || height[i, j] < groundY + 0.2f) continue;
                        var p = new Vector2(lb.min.x + (i + 0.5f) * dx, lb.min.z + (j + 0.5f) * dz);
                        all = Mathf.Max(all, Vector2.Dot(p - centre, d));
                    }
                }

                float overhang = Mathf.Max(0f, all - e);
                float width = latMax - latMin;
                Vector3 faceWorld = root.TransformDirection(new Vector3(d.x, 0f, d.y));
                float score = overhang * 2f + (width < 0.5f ? 1f : 0f) + (Vector3.Dot(faceWorld, forwardWorld) > 0.5f ? 0f : 0.2f);
                edge[f] = e;
                s.Log($"face {f} (local {d.x:0},{d.y:0}): platform edge {e:0.00} from the middle, {overhang:0.00} m of toy past it, edge width {width:0.00} -> score {score:0.00}");
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFace = f;
                }
            }

            Vector2 chosen = faces[bestFace];
            Vector2 chosenLateral = new Vector2(-chosen.y, chosen.x);
            // The edge point: on the edge of the platform, in the middle of the cells that form that edge.
            var edgeCells = cellsLocal.Where(p => Vector2.Dot(p - centre, chosen) >= edge[bestFace] - 0.12f).ToList();
            float lateralMean = edgeCells.Count > 0 ? edgeCells.Average(p => Vector2.Dot(p - centre, chosenLateral)) : 0f;
            Vector2 edgeLocal = centre + chosen * edge[bestFace] + chosenLateral * lateralMean;
            Vector3 edgeWorld = root.TransformPoint(new Vector3(edgeLocal.x, 0f, edgeLocal.y));
            Vector3 outward = MoreToyGeometry.Flat(root.TransformDirection(new Vector3(chosen.x, 0f, chosen.y))).normalized;
            float oppositeEdge = edge[bestFace == 0 ? 1 : bestFace == 1 ? 0 : bestFace == 2 ? 3 : 2];

            Vector3 footXZ = edgeWorld + outward * MoreClimbStandoff;
            Vector3 footGround = MoreGroundPoint(s, footXZ);
            float climbTop = Mathf.Max(footGround.y + 0.8f, topY - MoreClimbStopBelowTop);
            Quaternion facingWall = MoreLook(-outward);
            Transform foot = s.AddPoint("ClimbFoot", footGround, facingWall);
            Transform wallTop = s.AddPoint("ClimbWallTop", new Vector3(footXZ.x, climbTop, footXZ.z), facingWall);

            float inward = Mathf.Clamp(edge[bestFace] * 0.9f, 0.05f, 0.40f);
            Vector3 standXZ = edgeWorld - outward * inward;
            Vector3 jumpDirection = jumpFarSide ? -outward : outward;
            Transform topStand = s.AddPoint("ClimbTop", new Vector3(standXZ.x, topY - (jumpFarSide ? 0.03f : 0f), standXZ.z), MoreLook(jumpDirection));

            Vector3 exitXZ;
            if (jumpFarSide)
            {
                Vector3 middleWorld = root.TransformPoint(new Vector3(centre.x, 0f, centre.y));
                exitXZ = middleWorld - outward * (oppositeEdge + 1.2f);
            }
            else
            {
                exitXZ = edgeWorld + outward * 1.4f;
            }
            Vector3 exitGround = MoreGroundPoint(s, exitXZ);
            Transform exit = s.AddPoint("Exit", exitGround, MoreLook(jumpDirection));
            Transform anchor = s.AddPoint("ClimbAnchor", footGround, facingWall);

            var routine = s.SetRoutine<ClimbTopRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "path", new List<Object> { foot, wallTop });
            PlaygroundPrototypeBuilder.SetObject(routine, "topStand", topStand);
            s.Log($"climb face {bestFace} (score {bestScore:0.00}); {(jumpFarSide ? "jumps down the far side" : "jumps down the way he climbed")}");
        }

        /// <summary>
        /// Looks for a slope that rises from near the ground to the platform (the fort's slide chute) in the height grid: along each of the four
        /// local directions and every line of cells, the longest run that only rises (steps of at most 12 cm) from at most 45 cm above the
        /// ground and gains at least 0.6 m over at least 0.9 m. The middle line of the direction with the most such lines is the way up. He
        /// crawls up it, cheers where it levels out at the top, and slides down it again. Returns false (and logs) when there is no such slope.
        /// </summary>
        static bool MoreRampClimb(ToySetup s, Bounds lb, int nx, int nz, float dx, float dz, float[,] height, bool[,] has, float groundY)
        {
            Transform root = s.Root.transform;
            Vector2Int[] directions = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            var runs = new List<(int dir, int line, int k0, int k1, float gain, float length)>();

            for (int f = 0; f < 4; f++)
            {
                Vector2Int d = directions[f];
                bool alongX = d.x != 0;
                int lines = alongX ? nz : nx;
                int count = alongX ? nx : nz;
                float step = alongX ? dx : dz;
                bool forward = d.x + d.y > 0;

                for (int line = 0; line < lines; line++)
                {
                    Vector2Int Cell(int k)
                    {
                        int kk = forward ? k : count - 1 - k;
                        return alongX ? new Vector2Int(kk, line) : new Vector2Int(line, kk);
                    }

                    int start = 0;
                    while (start < count - 1)
                    {
                        Vector2Int first = Cell(start);
                        if (!has[first.x, first.y]) { start++; continue; }

                        int end = start;
                        while (end < count - 1)
                        {
                            Vector2Int a = Cell(end);
                            Vector2Int b = Cell(end + 1);
                            if (!has[b.x, b.y]) break;
                            float rise = height[b.x, b.y] - height[a.x, a.y];
                            if (rise < -0.01f || rise > 0.12f) break;
                            end++;
                        }

                        Vector2Int last = Cell(end);
                        float gain = height[last.x, last.y] - height[first.x, first.y];
                        float length = (end - start) * step;
                        if (height[first.x, first.y] <= groundY + 0.45f && gain >= 0.6f && length >= 0.9f)
                            runs.Add((f, line, start, end, gain, length));
                        start = end + 1;
                    }
                }
            }

            if (runs.Count == 0)
            {
                s.Log("no slope that rises 0.6 m or more from the ground was found; climbing a wall instead.");
                return false;
            }

            // The direction with the most lines on a slope, then its middle line.
            int bestDirection = runs.GroupBy(r => r.dir).OrderByDescending(g => g.Count()).First().Key;
            var lineRuns = runs.Where(r => r.dir == bestDirection).OrderBy(r => r.line).ToList();
            var run = lineRuns[lineRuns.Count / 2];
            Vector2Int dir = directions[bestDirection];
            bool runAlongX = dir.x != 0;
            int total = runAlongX ? nx : nz;
            bool runForward = dir.x + dir.y > 0;
            s.Log($"slope found: {lineRuns.Count} line(s) of cells along local {(runAlongX ? "x" : "z")}{(runForward ? "+" : "-")}, middle line {run.line}: "
                + $"rises {run.gain:0.00} m over {run.length:0.00} m from y {height[CellOf(run.k0).x, CellOf(run.k0).y]:0.00}");

            Vector2Int CellOf(int k)
            {
                int kk = runForward ? k : total - 1 - k;
                return runAlongX ? new Vector2Int(kk, run.line) : new Vector2Int(run.line, kk);
            }

            Vector3 WorldOf(int k)
            {
                Vector2Int c = CellOf(k);
                Vector3 local = new Vector3(lb.min.x + (c.x + 0.5f) * dx, lb.center.y, lb.min.z + (c.y + 0.5f) * dz);
                Vector3 world = root.TransformPoint(local);
                world.y = height[c.x, c.y];
                return world;
            }

            Vector3 up = MoreToyGeometry.Flat(root.TransformDirection(new Vector3(dir.x, 0f, dir.y))).normalized;
            Quaternion facingUp = MoreLook(up);

            // The path: a point about every 0.32 m, then the point 0.24 m before the end of the slope where he stops, and the standing point
            // a little further on (still on the slope, short of whatever wall is at its top).
            int stopK = Mathf.Max(run.k0 + 1, run.k1 - 3);
            int standK = Mathf.Max(stopK, run.k1 - 1);
            var pathPoints = new List<Object>();
            int index = 0;
            for (int k = run.k0; k < stopK; k += 4) pathPoints.Add(s.AddPoint("ClimbPath_" + index++, WorldOf(k), facingUp));
            pathPoints.Add(s.AddPoint("ClimbPath_" + index, WorldOf(stopK), facingUp));
            Transform topStand = s.AddPoint("ClimbTop", WorldOf(standK), facingUp);
            Transform anchor = s.AddPoint("ClimbAnchor", WorldOf(run.k0), facingUp);

            Vector3 foot = WorldOf(run.k0);
            Transform exit = s.AddPoint("Exit", MoreGroundPoint(s, foot - up * 0.9f), MoreLook(-up));

            var routine = s.SetRoutine<ClimbTopRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            PlaygroundPrototypeBuilder.SetObjectArray(routine, "path", pathPoints);
            PlaygroundPrototypeBuilder.SetObject(routine, "topStand", topStand);
            PlaygroundPrototypeBuilder.SetEnum(routine, "climbPose", (int)ToyPose.Crawl);
            PlaygroundPrototypeBuilder.SetBool(routine, "slideDown", true);
            PlaygroundPrototypeBuilder.SetFloat(routine, "climbSpeed", 1.1f);
            s.Log($"he crawls up the slide chute ({pathPoints.Count} path points, top at y {WorldOf(standK).y:0.00}) and slides down it again");
            return true;
        }

        // ================================================================================================
        // Track ride
        // ================================================================================================

        /// <summary>
        /// The Synty track ride is a flat frame (3 m long, 35 cm tall with the handle) for fixing up high. It is lifted onto two poles and
        /// tilted so one end is 0.3 m lower; the high end is the one farther from the middle of the playground, so he zooms toward it. The
        /// handle (a separate mesh in the Synty model) is put under a pivot on the cable, at the high end, and travels with him.
        /// </summary>
        static void MoreTrackRide(ToySetup s)
        {
            Transform root = s.Root.transform;
            Transform body = MoreToyGeometry.BodyOf(s);
            if (body == null || !MoreToyGeometry.LocalBounds(root, body, out Bounds cable))
            {
                s.Warn("fallback: no mesh found for the cable frame, so there is no routine.");
                return;
            }

            bool longX = cable.size.x >= cable.size.z;
            Vector3 longLocal = longX ? Vector3.right : Vector3.forward;
            float longSize = longX ? cable.size.x : cable.size.z;

            // The handle: the Synty model's own small moving part.
            Transform handle = MoreToyGeometry.MovingParts(s)
                .OrderBy(p => ToySetup.BoundsOf(p).size.magnitude).FirstOrDefault();
            Bounds hb;
            if (handle == null || !MoreToyGeometry.LocalBounds(root, handle, out hb))
            {
                float y0 = cable.min.y - 0.02f;
                var box = new Bounds(new Vector3(cable.center.x, y0 + 0.17f, cable.center.z),
                    longX ? new Vector3(0.6f, 0.34f, 0.4f) : new Vector3(0.4f, 0.34f, 0.6f));
                s.Warn("fallback: the track ride has no separate handle mesh; splitting the part at the middle of the cable off as the handle.");
                handle = s.SplitMesh("Handle", box, root.TransformPoint(new Vector3(cable.center.x, y0 + 0.30f, cable.center.z)));
                MoreToyGeometry.LocalBounds(root, handle, out hb);
            }

            float pivotY = hb.max.y - 0.02f;                  // on the cable axis
            float gripDrop = pivotY - (hb.min.y + 0.025f);    // from the pivot down to where his hands hold
            float hands = MoreHands(s);
            float travel = Mathf.Max(0.8f, longSize - 0.6f);
            float theta = Mathf.Asin(Mathf.Min(0.5f, MoreCableDrop / travel)) * Mathf.Rad2Deg;
            float groundY = s.GroundY(root.position.x, root.position.z);
            float lowY = groundY + hands + gripDrop + 0.14f;
            float midY = lowY + MoreCableDrop * 0.5f;

            Vector3 midLocal = new Vector3(cable.center.x, pivotY, cable.center.z);
            float lift = midY - root.TransformPoint(midLocal).y;
            Vector3 attachBefore = root.TransformPoint(new Vector3(hb.center.x, pivotY, hb.center.z));
            Vector3 dirWorld = root.TransformDirection(longLocal).normalized;
            Vector3 mid = root.TransformPoint(midLocal) + Vector3.up * lift;

            // The high end is the one farther from the middle of the playground.
            float highSign = MoreToyGeometry.NearerToMiddle(mid + dirWorld, mid - dirWorld) ? -1f : 1f;
            Vector3 across = Vector3.Cross(dirWorld, Vector3.up).normalized;
            float angle = theta;
            if ((Quaternion.AngleAxis(angle, across) * dirWorld).y * highSign < 0f) angle = -angle;
            Quaternion tilt = Quaternion.AngleAxis(angle, across);
            s.Log($"cable frame length {longSize:0.00} m, handle trip {travel:0.00} m, tilt {Mathf.Abs(angle):0.0} degrees ({(highSign > 0 ? "+" : "-")} local {(longX ? "x" : "z")} end high), "
                + $"lifted by {lift:0.00} m; handle grip {gripDrop:0.00} m under its pivot, hands {hands:0.00}");

            body.position += Vector3.up * lift;
            body.RotateAround(mid, across, angle);

            // Two poles under the ends of the frame.
            float poleOffset = longSize * 0.5f - 0.03f;
            for (int end = 0; end < 2; end++)
            {
                Vector3 top = mid + tilt * (dirWorld * (end == 0 ? poleOffset : -poleOffset));
                MoreToyGeometry.Pole(s, "Pole_" + end, new Vector3(top.x, MoreGroundPoint(s, top).y, top.z), top, 0.05f);
            }

            // The handle: only moved onto the tilted cable (it keeps hanging straight), then put under its own pivot at the high end.
            Vector3 attachNow = mid + tilt * (attachBefore + Vector3.up * lift - mid);
            handle.position += attachNow - attachBefore;

            Vector3 highPoint = mid + tilt * (dirWorld * (highSign * travel * 0.5f));
            Vector3 lowPoint = mid - tilt * (dirWorld * (highSign * travel * 0.5f));
            Quaternion rideRotation = MoreLook(MoreToyGeometry.Flat(lowPoint - highPoint).sqrMagnitude > 0.0001f ? lowPoint - highPoint : dirWorld * highSign * -1f);

            var pivotObject = new GameObject("HandlePivot");
            pivotObject.transform.SetParent(root, false);
            pivotObject.transform.SetPositionAndRotation(attachNow, rideRotation);
            handle.SetParent(pivotObject.transform, true);
            pivotObject.transform.position = highPoint;
            s.ClearStatic(pivotObject.transform);
            s.Log($"pivot HandlePivot on the cable at {ToySetup.Fmt(highPoint)} (high end), low end {ToySetup.Fmt(lowPoint)}");

            Transform anchor = s.AddPoint("HangAnchor", highPoint + Vector3.down * (gripDrop + hands), rideRotation);
            anchor.SetParent(pivotObject.transform, true);
            Transform high = s.AddPoint("HighPoint", highPoint, rideRotation);
            Transform low = s.AddPoint("LowPoint", lowPoint, rideRotation);

            Vector3 rideDirection = MoreToyGeometry.Flat(lowPoint - highPoint).normalized;
            Transform exit = s.AddPoint("Exit", MoreGroundPoint(s, lowPoint + rideDirection * 0.9f), rideRotation);

            MoreToyGeometry.RefreshBounds(s);
            s.LogHeightMap();

            var routine = s.SetRoutine<TrackRideRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "handle", pivotObject.transform);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            PlaygroundPrototypeBuilder.SetObject(routine, "highPoint", high);
            PlaygroundPrototypeBuilder.SetObject(routine, "lowPoint", low);
        }

        // ================================================================================================
        // Ship
        // ================================================================================================

        /// <summary>
        /// The playground copy of the ship (the pirate cove has its own). The deck height is the commonest height of the upward surfaces in the
        /// lower part of the hull; he stands on the biggest flat patch at that height, with room around him, about a fifth of the way from
        /// the stern (the wide end; the bow is the narrow one), looking at the bow. A steering wheel prop stands in front of him if the City
        /// pack's wheel is found.
        /// </summary>
        static void MoreShip(ToySetup s)
        {
            Transform root = s.Root.transform;
            if (!MoreToyGeometry.LocalBoundsOfToy(s, out Bounds lb))
            {
                s.Warn("fallback: no mesh found, so there is no routine.");
                return;
            }

            bool longZ = lb.size.z >= lb.size.x;
            const float cell = 0.12f;
            int nx = Mathf.Clamp(Mathf.CeilToInt(lb.size.x / cell), 4, 100);
            int nz = Mathf.Clamp(Mathf.CeilToInt(lb.size.z / cell), 4, 100);
            float dx = lb.size.x / nx;
            float dz = lb.size.z / nz;
            var height = new float[nx, nz];
            var has = new bool[nx, nz];
            var facesUp = new bool[nx, nz];

            Bounds wb = s.WorldBounds;
            float lowLimit = wb.min.y + 0.55f * wb.size.y;
            using (var probe = new MoreToyGeometry.Probe(s))
            {
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        Vector3 world = root.TransformPoint(new Vector3(lb.min.x + (i + 0.5f) * dx, lb.center.y, lb.min.z + (j + 0.5f) * dz));
                        if (!probe.Top(world.x, world.z, wb.max.y + 3f, wb.size.y + 6f, out RaycastHit hit)) continue;
                        has[i, j] = true;
                        height[i, j] = hit.point.y;
                        facesUp[i, j] = hit.normal.y >= 0.6f;
                    }
                }
            }

            // The bow is the narrow end: the end band (a fifth of the length) with fewer surface hits.
            int bandCount = Mathf.Max(1, (longZ ? nz : nx) / 5);
            int lowEndHits = 0;
            int highEndHits = 0;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    int along = longZ ? j : i;
                    int size = longZ ? nz : nx;
                    if (!has[i, j]) continue;
                    if (along < bandCount) lowEndHits++;
                    else if (along >= size - bandCount) highEndHits++;
                }
            }
            float bowSign = highEndHits <= lowEndHits ? 1f : -1f;
            Vector3 bowLocal = (longZ ? Vector3.forward : Vector3.right) * bowSign;
            Vector3 bowWorld = MoreToyGeometry.Flat(root.TransformDirection(bowLocal)).normalized;

            // The deck: the height of the surface a fifth of the way from the stern, in the middle (the helm platform). The fallback is the
            // commonest upward-facing height in the lower part of the hull (5 cm bins).
            float length = longZ ? lb.size.z : lb.size.x;
            float targetAlong = bowSign > 0 ? (longZ ? lb.min.z : lb.min.x) + 0.22f * length : (longZ ? lb.max.z : lb.max.x) - 0.22f * length;
            float targetLateral = longZ ? lb.center.x : lb.center.z;
            int targetI = Mathf.Clamp(Mathf.FloorToInt(((longZ ? targetLateral : targetAlong) - lb.min.x) / dx), 0, nx - 1);
            int targetJ = Mathf.Clamp(Mathf.FloorToInt(((longZ ? targetAlong : targetLateral) - lb.min.z) / dz), 0, nz - 1);
            float deckY;
            string deckHow;
            if (has[targetI, targetJ] && height[targetI, targetJ] <= lowLimit + 0.8f)
            {
                deckY = height[targetI, targetJ];
                deckHow = "the surface at 22% of the length from the stern";
            }
            else
            {
                var bins = new Dictionary<int, List<float>>();
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        if (!has[i, j] || !facesUp[i, j] || height[i, j] > lowLimit) continue;
                        int bin = Mathf.RoundToInt(height[i, j] / 0.05f);
                        if (!bins.TryGetValue(bin, out List<float> list)) bins[bin] = list = new List<float>();
                        list.Add(height[i, j]);
                    }
                }
                if (bins.Count == 0)
                {
                    deckY = wb.min.y + 0.4f * wb.size.y;
                    deckHow = "fallback: no upward surface found in the lower hull, assumed";
                    s.Warn($"fallback: no deck found; assuming it is at y {deckY:0.00}.");
                }
                else
                {
                    deckY = bins.OrderByDescending(p => p.Value.Count).First().Value.Average();
                    deckHow = "fallback: the commonest upward height in the lower hull";
                }
            }

            bool OnPatch(int i, int j) => has[i, j] && facesUp[i, j] && Mathf.Abs(height[i, j] - deckY) <= 0.05f;

            // The biggest connected flat patch at the deck height.
            var label = new int[nx, nz];
            var best = new List<Vector2Int>();
            int labels = 0;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    if (!OnPatch(i, j) || label[i, j] != 0) continue;
                    labels++;
                    var group = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(i, j));
                    label[i, j] = labels;
                    while (queue.Count > 0)
                    {
                        Vector2Int c = queue.Dequeue();
                        group.Add(c);
                        foreach (Vector2Int step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                        {
                            Vector2Int n = c + step;
                            if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || label[n.x, n.y] != 0 || !OnPatch(n.x, n.y)) continue;
                            label[n.x, n.y] = labels;
                            queue.Enqueue(n);
                        }
                    }
                    if (group.Count > best.Count) best = group;
                }
            }
            var inBest = new bool[nx, nz];
            foreach (Vector2Int c in best) inBest[c.x, c.y] = true;

            s.Log($"deck height {deckY:0.00} ({deckHow}); biggest flat patch at it {best.Count} cells of {cell:0.00} m; end hits {lowEndHits} (low end) vs {highEndHits} (high end), "
                + $"so the bow is local {(bowSign > 0 ? "+" : "-")}{(longZ ? "z" : "x")}");

            // The stand point: the cell with room around it nearest to a fifth of the way from the stern, in the middle.
            const int window = 3;
            float Clearance(int i, int j)
            {
                float nearest = window + 1;
                for (int di = -window; di <= window; di++)
                {
                    for (int dj = -window; dj <= window; dj++)
                    {
                        int a = i + di, b = j + dj;
                        bool inside = a >= 0 && b >= 0 && a < nx && b < nz && inBest[a, b];
                        if (inside) continue;
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(di * di + dj * dj));
                    }
                }
                return Mathf.Max(0f, (nearest - 0.5f) * cell);
            }

            Vector2Int pick = default;
            float pickScore = float.MaxValue;
            bool anyRoomy = best.Any(c => Clearance(c.x, c.y) >= 0.30f);
            foreach (Vector2Int c in best)
            {
                float clearance = Clearance(c.x, c.y);
                if (anyRoomy && clearance < 0.30f) continue;
                float cx = lb.min.x + (c.x + 0.5f) * dx;
                float cz = lb.min.z + (c.y + 0.5f) * dz;
                float score = anyRoomy
                    ? Mathf.Abs((longZ ? cz : cx) - targetAlong) * 2f + Mathf.Abs((longZ ? cx : cz) - targetLateral)
                    : -clearance;
                if (score < pickScore)
                {
                    pickScore = score;
                    pick = c;
                }
            }

            Vector3 stand;
            float wheelAhead = 0.5f;
            if (best.Count >= 8)
            {
                Vector3 local = new Vector3(lb.min.x + (pick.x + 0.5f) * dx, lb.center.y, lb.min.z + (pick.y + 0.5f) * dz);
                stand = root.TransformPoint(local);
                stand.y = deckY;

                int steps = 0;
                while (steps < 8)
                {
                    int a = pick.x + (longZ ? 0 : (int)bowSign * (steps + 1));
                    int b = pick.y + (longZ ? (int)bowSign * (steps + 1) : 0);
                    if (a < 0 || b < 0 || a >= nx || b >= nz || !inBest[a, b]) break;
                    steps++;
                }
                wheelAhead = Mathf.Clamp(steps * cell - 0.2f, 0.3f, 0.6f);
                s.Log($"stand point on a patch with {Clearance(pick.x, pick.y):0.00} m of room: {ToySetup.Fmt(stand)}; wheel {wheelAhead:0.00} m ahead");
            }
            else
            {
                Vector3 local = new Vector3(lb.center.x, lb.center.y, lb.center.z);
                stand = root.TransformPoint(local);
                stand.y = deckY;
                s.Warn($"fallback: no flat patch of enough size ({best.Count} cells) at the deck height; standing in the middle of the hull at y {deckY:0.00}.");
            }

            Transform anchor = s.AddPoint("HelmAnchor", stand, MoreLook(bowWorld));

            // He hops off over the stern.
            Vector3 exitXZ = stand - bowWorld * (0.22f * length + 0.9f);
            Transform exit = s.AddPoint("Exit", MoreGroundPoint(s, exitXZ), MoreLook(-bowWorld));

            Transform wheel = MorePlaceWheel(s, stand + bowWorld * wheelAhead + Vector3.up * 0.60f, bowWorld);

            var routine = s.SetRoutine<ShipWheelRoutine>(exit);
            PlaygroundPrototypeBuilder.SetObject(routine, "anchor", anchor);
            if (wheel != null) PlaygroundPrototypeBuilder.SetObject(routine, "wheel", wheel);
        }

        /// <summary>The steering wheel from the City pack (the one the pirate ship has), standing in front of the helmsman with its axle along the ship.</summary>
        static Transform MorePlaceWheel(ToySetup s, Vector3 position, Vector3 bowWorld)
        {
            GameObject prefab = MoreToyGeometry.FindPrefab("SM_Veh_Steering_Wheel_01", "PolygonCity");
            if (prefab == null)
            {
                s.Warn("fallback: the steering wheel prefab was not found; he steers without a wheel.");
                return null;
            }

            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(s.Root.transform, false);
            // The pivot's forward is the axle, pointing back at the helmsman, so the wheel shows him its face.
            pivot.SetPositionAndRotation(position, MoreLook(-bowWorld));

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot);
            model.name = "WheelModel";
            MoreToyGeometry.MakeScenery(model);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // The axle is the model's thinnest side: turn the model so that side runs along the pivot's forward.
            if (PlaygroundPrototypeBuilder.TryGetBounds(model, out Bounds bounds))
            {
                Vector3 size = bounds.size;
                if (size.y <= size.x && size.y <= size.z) model.transform.rotation = pivot.rotation * Quaternion.Euler(90f, 0f, 0f);
                else if (size.x <= size.y && size.x <= size.z) model.transform.rotation = pivot.rotation * Quaternion.Euler(0f, 90f, 0f);

                PlaygroundPrototypeBuilder.TryGetBounds(model, out bounds);
                float diameter = Mathf.Max(bounds.size.x, bounds.size.y);
                if (diameter > 0.01f) model.transform.localScale *= 0.55f / diameter;
                PlaygroundPrototypeBuilder.TryGetBounds(model, out bounds);
                model.transform.position += position - bounds.center;
            }

            s.ClearStatic(pivot);
            s.Log($"steering wheel placed at {ToySetup.Fmt(position)}, axle along the ship");
            return pivot;
        }
    }
}
