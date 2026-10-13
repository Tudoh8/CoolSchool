using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The slide tower. SM_Prop_Playground_Slide_01 is only the chute, so a small tower is built around it from the PolygonKids playground kit:
    /// a top platform at the chute's top end on four posts, a little roof on the posts, railings on the two open sides, a landing halfway up,
    /// and two flights of stairs (the lower one from the side onto the landing, the upper one up the back to the top platform). One flight
    /// of the kit's stairs is 1 m high, so the top platform lies 2 m above the ground; the chute is squashed in height until its top is
    /// level with that platform (the kit's posts are 3.7 m long, made for a 2 m platform with a roof 1.2 m above it). Every piece is
    /// measured from its mesh at build time (size, the direction the stairs rise), placed by its bounds, and given colliders like the other
    /// playground pieces. The whole layout is worked out in the chute's own frame: t runs along the chute downhill, u across it (to the right).
    /// </summary>
    public static partial class PlaygroundToysBuilder
    {
        /// <summary>Set to false to leave the little roof and its posts' upper half out.</summary>
        const bool TowerRoof = true;

        // How far the roof's posts reach above the top platform (the kit's Support_01 is made for a 1.2 m headroom).
        const float TowerHeadroom = 1.2f;

        // The platform's front edge overlaps the back of the chute by this much.
        const float TowerChuteOverlap = 0.1f;

        sealed class SlideTowerLayout
        {
            public float GroundY;
            public float StairHeight;
            public float DeckY;          // top platform surface
            public float LandingY;       // landing surface
            public float Tc;             // t of the top platform centre
            public float TcLanding;      // t of the landing centre
            public float Side;           // +1 or -1: the side of the lower flight
            public float PlatformSize;
            public float StairRun;
            public float ChuteScale = 1f;
            public Vector3[] Ring;       // ground points round the tower, in order
            public int RingFoot;         // index of the ring point next to the foot of the stairs
            public Vector3[] Climb;      // the kid's root from the foot of the stairs up to the top platform
            public Vector3 StairExit;    // on the ground at the foot of the stairs
            public Vector3 TopCentre;    // top platform centre
        }

        // ---- The tower ------------------------------------------------------------------------

        static SlideTowerLayout BuildSlideTower(ToySetup s, ToyFrame frame, Vector2 chuteMean, Vector2 chuteDir, float tBack, float tBottom,
            float groundY, float bedTop)
        {
            var layout = new SlideTowerLayout { GroundY = groundY };
            Vector2 sideDir = new Vector2(chuteDir.y, -chuteDir.x);   // to the right of the way down
            Vector3 dW = frame.ToWorldDirection(chuteDir);
            Vector3 rW = frame.ToWorldDirection(sideDir);
            Vector3 World(float u, float t, float y) => frame.ToWorld(chuteMean + chuteDir * t + sideDir * u, y);

            // ---- Measure the kit ----
            Bounds stairBounds = MeasurePiece(s, "SM_Prop_Playground_Stairs_01");
            Bounds platformBounds = MeasurePiece(s, "SM_Prop_Playground_Platform_01");
            float stairHeight = stairBounds.size.y > 0.2f ? stairBounds.size.y : 1.0f;
            float run = Mathf.Max(stairBounds.size.x, stairBounds.size.z);
            float width = Mathf.Min(stairBounds.size.x, stairBounds.size.z);
            float platform = Mathf.Max(platformBounds.size.x, platformBounds.size.z);
            if (platform < 0.5f) platform = 1.0f;
            if (run < 0.5f) run = 1.0f;
            if (width < 0.3f) width = 1.0f;
            float thickness = platformBounds.size.y > 0.01f ? platformBounds.size.y : 0.08f;
            s.Log($"tower kit: stairs {ToySetup.Fmt(stairBounds.size)} (rise {stairHeight:0.00}, run {run:0.00}, width {width:0.00}), "
                + $"platform {ToySetup.Fmt(platformBounds.size)}; the platform is {2f * stairHeight:0.00} m above the ground (two flights)");

            layout.StairHeight = stairHeight;
            layout.PlatformSize = platform;
            layout.StairRun = run;
            layout.LandingY = groundY + stairHeight;
            layout.DeckY = groundY + 2f * stairHeight;

            // ---- Squash the chute so that its top is level with the platform ----
            float chuteHeight = bedTop - groundY;
            if (chuteHeight > 0.3f)
            {
                float scale = (2f * stairHeight) / chuteHeight;
                layout.ChuteScale = scale;
                ScaleChuteHeight(s, scale, groundY);
                s.Log($"chute squashed to {scale:0.00} of its height: its top was {chuteHeight:0.00} m above the ground, now {2f * stairHeight:0.00} m");
            }
            else
            {
                s.Warn($"fallback: the chute top height {chuteHeight:0.00} looks wrong; the chute was left as it is.");
            }

            // ---- Layout in (u, t) ----
            float tc = tBack + TowerChuteOverlap - platform * 0.5f;
            float tFlight2 = tc - platform * 0.5f - run * 0.5f;
            float tcLanding = tc - platform * 0.5f - run - platform * 0.5f;
            layout.Tc = tc;
            layout.TcLanding = tcLanding;
            layout.TopCentre = World(0f, tc, layout.DeckY);

            // The lower flight goes on the side with more room.
            float clearancePlus = SideClearance(s, World, rW, dW, +1f, tcLanding, platform, run, groundY);
            float clearanceMinus = SideClearance(s, World, rW, dW, -1f, tcLanding, platform, run, groundY);
            float side = clearancePlus >= clearanceMinus ? 1f : -1f;
            layout.Side = side;
            s.Log($"tower: lower stairs on the {(side > 0f ? "right" : "left")} side (room to the next thing: right {clearancePlus:0.00} m, left {clearanceMinus:0.00} m; "
                + $"measured in steps of 0.25 up to 1.5)");

            float half = platform * 0.5f;
            Quaternion yawChute = Quaternion.LookRotation(dW, Vector3.up);

            // ---- Posts ----
            // Top platform: four long posts at the corners (the kit's Support_01), reaching above the railings.
            float postTop = layout.DeckY + TowerHeadroom;
            foreach (float cu in new[] { -half, half })
                foreach (float ct in new[] { -half, half })
                    PlacePost(s, "SM_Prop_Playground_Support_01", World(cu, tc + ct, 0f), postTop, 1f, "Tower_Post");

            // Landing: four short posts, the kit's Support_02 squashed to the landing's height.
            foreach (float cu in new[] { -half, half })
                foreach (float ct in new[] { -half, half })
                    PlacePost(s, "SM_Prop_Playground_Support_02", World(cu, tcLanding + ct, 0f), layout.LandingY - thickness, 0f, "Tower_LandingPost");

            // ---- Platforms ----
            PlaceFlat(s, "SM_Prop_Playground_Platform_01", World(0f, tc, 0f), layout.DeckY, "Tower_TopPlatform");
            PlaceFlat(s, "SM_Prop_Playground_Platform_01", World(0f, tcLanding, 0f), layout.LandingY, "Tower_Landing");

            // ---- Stairs ----
            // The upper flight rises toward the chute (+t) and ends at the back edge of the top platform.
            PlaceStairs(s, World(0f, tFlight2, 0f), layout.LandingY, dW, "Tower_Stairs_Upper");
            // The lower flight rises toward the landing, sideways.
            Vector3 lowerCentre = World(side * (half + run * 0.5f), tcLanding, 0f);
            PlaceStairs(s, lowerCentre, groundY, -side * rW, "Tower_Stairs_Lower");

            // ---- Railings on the open sides ----
            float railingInset = 0.04f;
            PlaceRailing(s, World(-half + railingInset, tc, 0f), dW, layout.DeckY, "Tower_Railing_TopLeft");
            PlaceRailing(s, World(half - railingInset, tc, 0f), dW, layout.DeckY, "Tower_Railing_TopRight");
            PlaceRailing(s, World(0f, tcLanding - half + railingInset, 0f), rW, layout.LandingY, "Tower_Railing_LandingBack");
            PlaceRailing(s, World(-side * (half - railingInset), tcLanding, 0f), dW, layout.LandingY, "Tower_Railing_LandingSide");

            // ---- A little roof on the four tall posts ----
            if (TowerRoof) PlaceRoof(s, World(0f, tc, 0f), postTop);

            // ---- Where the kid walks ----
            layout.StairExit = World(side * (half + run + 0.45f), tcLanding, groundY);
            float h = stairHeight;
            float foot = side * (half + run);          // u of the foot of the lower flight
            float head = side * half;                  // u of its top
            float lift = 0.04f;                        // the kid's root is a hair above the treads
            var climb = new List<Vector3>
            {
                layout.StairExit,
                World(foot - side * 0.05f, tcLanding, groundY + lift),
                World(head, tcLanding, layout.LandingY + lift),
                World(0f, tcLanding, layout.LandingY),
                World(0f, tcLanding + half, layout.LandingY),
                World(0f, tc - half, layout.DeckY),
                World(0f, tc, layout.DeckY),
            };
            layout.Climb = climb.ToArray();

            // The ring round the tower (0.6 m outside it) for walking round from wherever he stands to the foot of the stairs.
            float margin = 0.6f;
            float uLeft = (side < 0f ? -(half + run) : -half) - margin;
            float uRight = (side > 0f ? (half + run) : half) + margin;
            float tFar = tcLanding - half - margin;
            float tNear = Mathf.Max(tBottom, tc + half) + 0.5f;
            var ring = new List<Vector3>
            {
                World(uLeft, tFar, groundY), World(0f, tFar, groundY), World(uRight, tFar, groundY),
                World(uRight, tcLanding, groundY),
                World(uRight, tNear, groundY), World(0f, tNear, groundY), World(uLeft, tNear, groundY),
                World(uLeft, tcLanding, groundY),
            };
            layout.Ring = ring.ToArray();
            layout.RingFoot = side > 0f ? 3 : 7;
            for (int i = 0; i < layout.Ring.Length; i++) layout.Ring[i].y = s.GroundY(layout.Ring[i].x, layout.Ring[i].z);

            s.Dirty();
            LogTowerClearance(s, World, rW, dW, layout, half, run);
            return layout;
        }

        // ---- Measuring and placing pieces ------------------------------------------------------

        static GameObject SpawnKitPiece(ToySetup s, string prefabName, string label)
        {
            GameObject prefab = PlaygroundPrototypeBuilder.LoadPrefab(PlaygroundPrototypeBuilder.Kids(prefabName));
            if (prefab == null)
            {
                s.Warn($"fallback: the kit piece {prefabName} was not found; the tower goes without it.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s.Root.transform);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.name = label;
            return go;
        }

        /// <summary>The size of a kit piece (spawned at the origin, measured and thrown away).</summary>
        static Bounds MeasurePiece(ToySetup s, string prefabName)
        {
            GameObject go = SpawnKitPiece(s, prefabName, "Measure");
            if (go == null) return new Bounds(Vector3.zero, Vector3.zero);

            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds bounds);
            Object.DestroyImmediate(go);
            return bounds;
        }

        static void Settle(ToySetup s, GameObject go)
        {
            PlaygroundPrototypeBuilder.FinishEnvironmentObject(go);
        }

        /// <summary>Spawns a flat platform and puts its top surface at topY with its middle over the world point.</summary>
        static GameObject PlaceFlat(ToySetup s, string prefabName, Vector3 centre, float topY, string label)
        {
            GameObject go = SpawnKitPiece(s, prefabName, label);
            if (go == null) return null;

            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds b);
            go.transform.position += new Vector3(centre.x - b.center.x, topY - b.max.y, centre.z - b.center.z);
            Settle(s, go);
            s.Log($"{label} at {ToySetup.Fmt(centre)}, top at y {topY:0.00}");
            return go;
        }

        /// <summary>
        /// Spawns a post with its middle over the world point and its top at topY. A scale of 0 or less squashes it so that it
        /// reaches from 0.3 m below the ground up to topY.
        /// </summary>
        static GameObject PlacePost(ToySetup s, string prefabName, Vector3 centre, float topY, float scaleY, string label)
        {
            GameObject go = SpawnKitPiece(s, prefabName, label);
            if (go == null) return null;

            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds b);
            if (scaleY <= 0f)
            {
                float wanted = topY - (s.GroundY(centre.x, centre.z) - 0.3f);
                scaleY = Mathf.Clamp(wanted / Mathf.Max(0.1f, b.size.y), 0.2f, 2f);
            }
            go.transform.localScale = new Vector3(1f, scaleY, 1f);
            PlaygroundPrototypeBuilder.TryGetBounds(go, out b);
            go.transform.position += new Vector3(centre.x - b.center.x, topY - b.max.y, centre.z - b.center.z);
            Settle(s, go);
            return go;
        }

        /// <summary>A flight of stairs: turned so that it rises toward the direction, its middle over the point, its lowest end at baseY.</summary>
        static GameObject PlaceStairs(ToySetup s, Vector3 centre, float baseY, Vector3 riseToward, string label)
        {
            GameObject go = SpawnKitPiece(s, "SM_Prop_Playground_Stairs_01", label);
            if (go == null) return null;

            Vector3 rise = RiseDirection(go);
            float yaw = Vector3.SignedAngle(rise, Flat(riseToward), Vector3.up);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds b);
            go.transform.position += new Vector3(centre.x - b.center.x, baseY - b.min.y, centre.z - b.center.z);
            Settle(s, go);
            s.Log($"{label} at {ToySetup.Fmt(centre)}, rises toward {ToySetup.Fmt(Flat(riseToward))} (its own rise direction was {ToySetup.Fmt(rise)}, turned {yaw:0}°), "
                + $"from y {baseY:0.00} to {baseY + b.size.y:0.00}");
            return go;
        }

        /// <summary>A railing standing on a platform with its middle over the point and its long side along the direction.</summary>
        static GameObject PlaceRailing(ToySetup s, Vector3 centre, Vector3 along, float baseY, string label)
        {
            GameObject go = SpawnKitPiece(s, "SM_Prop_Playground_Railing_01", label);
            if (go == null) return null;

            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds b);
            Vector3 longAxis = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            float yaw = Vector3.SignedAngle(longAxis, Flat(along), Vector3.up);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            PlaygroundPrototypeBuilder.TryGetBounds(go, out b);
            go.transform.position += new Vector3(centre.x - b.center.x, baseY - b.min.y, centre.z - b.center.z);
            Settle(s, go);
            return go;
        }

        /// <summary>The roof: its own origin is at its eaves (the little posts hang below it), so the origin goes to the top of the posts.</summary>
        static GameObject PlaceRoof(ToySetup s, Vector3 centre, float postTop)
        {
            GameObject go = SpawnKitPiece(s, "SM_Prop_Playground_Roof_01", "Tower_Roof");
            if (go == null) return null;

            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds b);
            go.transform.position = new Vector3(centre.x - b.center.x, postTop, centre.z - b.center.z);
            Settle(s, go);
            PlaygroundPrototypeBuilder.TryGetBounds(go, out Bounds placed);
            s.Log($"Tower_Roof: origin at the top of the posts (y {postTop:0.00}); its bounds now y {placed.min.y:0.00}..{placed.max.y:0.00}, size {ToySetup.Fmt(placed.size)}");
            return go;
        }

        /// <summary>The flat horizontal direction toward which a piece of stairs rises, found from the heights of its vertices at its four ends.</summary>
        static Vector3 RiseDirection(GameObject go)
        {
            var vertices = new List<Vector3>();
            var buffer = new List<Vector3>();
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                buffer.Clear();
                filter.sharedMesh.GetVertices(buffer);
                foreach (Vector3 v in buffer) vertices.Add(filter.transform.TransformPoint(v));
            }
            if (vertices.Count == 0) return Vector3.right;

            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            Vector3 best = Vector3.right;
            float bestHeight = float.MinValue;
            foreach (Vector3 d in directions)
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (Vector3 v in vertices)
                {
                    float p = Vector3.Dot(v, d);
                    min = Mathf.Min(min, p);
                    max = Mathf.Max(max, p);
                }
                float threshold = max - 0.25f * (max - min);
                float sum = 0f;
                int count = 0;
                foreach (Vector3 v in vertices)
                {
                    if (Vector3.Dot(v, d) < threshold) continue;
                    sum += v.y;
                    count++;
                }
                float mean = count > 0 ? sum / count : 0f;
                if (mean > bestHeight)
                {
                    bestHeight = mean;
                    best = d;
                }
            }
            return best;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        /// <summary>Squashes the chute's mesh in height and lowers it so that its foot is on the ground again.</summary>
        static void ScaleChuteHeight(ToySetup s, float scale, float groundY)
        {
            List<Transform> parts = TopLevelMeshObjects(s);
            foreach (Transform part in parts)
            {
                Vector3 local = part.localScale;
                part.localScale = new Vector3(local.x, local.y * scale, local.z);
            }

            float lowest = float.MaxValue;
            foreach (Transform part in parts) lowest = Mathf.Min(lowest, ToySetup.BoundsOf(part).min.y);
            float shift = groundY - lowest;
            foreach (Transform part in parts) part.position += Vector3.up * shift;
            s.Dirty();
        }

        // ---- Clearance --------------------------------------------------------------------------

        /// <summary>
        /// How much free space there is around where the lower flight would go on one side: the biggest margin (0 to 1.5 m, in steps of
        /// 0.25) around its box with nothing solid from another object in it.
        /// </summary>
        static float SideClearance(ToySetup s, System.Func<float, float, float, Vector3> world, Vector3 rW, Vector3 dW, float side,
            float tLanding, float platform, float run, float groundY)
        {
            Vector3 centre = world(side * (platform * 0.5f + run * 0.5f + 0.2f), tLanding, groundY + 1.5f);
            Vector3 half = new Vector3((run + 0.6f) * 0.5f, 1.2f, platform * 0.5f + 0.1f);
            return Clearance(s, centre, half, Quaternion.LookRotation(dW, Vector3.up));
        }

        static float Clearance(ToySetup s, Vector3 centre, Vector3 half, Quaternion rotation)
        {
            Physics.SyncTransforms();
            for (float margin = 0f; margin <= 1.5f; margin += 0.25f)
            {
                Collider[] hits = Physics.OverlapBox(centre, half + new Vector3(margin, 0f, margin), rotation, ~0, QueryTriggerInteraction.Ignore);
                foreach (Collider hit in hits)
                {
                    if (hit == null || hit.transform.IsChildOf(s.Root.transform)) continue;
                    return Mathf.Max(0f, margin - 0.25f);
                }
            }
            return 1.5f;
        }

        /// <summary>Logs how close the finished tower comes to the colliders and the touch zones of the neighbouring toys.</summary>
        static void LogTowerClearance(ToySetup s, System.Func<float, float, float, Vector3> world, Vector3 rW, Vector3 dW, SlideTowerLayout layout, float half, float run)
        {
            float uMin = layout.Side < 0f ? -(half + run) : -half;
            float uMax = layout.Side > 0f ? (half + run) : half;
            Vector3 centre = world((uMin + uMax) * 0.5f, (layout.TcLanding + layout.Tc) * 0.5f, layout.GroundY + 1.5f);
            Vector3 box = new Vector3((uMax - uMin) * 0.5f, 1.2f, (layout.Tc - layout.TcLanding + 2f * half) * 0.5f);
            float clearance = Clearance(s, centre, box, Quaternion.LookRotation(dW, Vector3.up));
            s.Log($"tower clearance: no other object's collider within {clearance:0.00} m of the tower's footprint (a value of 1.50 means 1.5 m or more)");

            foreach (BoxCollider other in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (!other.isTrigger || other.transform.IsChildOf(s.Root.transform)) continue;
                if (other.GetComponent<CoolSchool.Playground.ToyTouchZone>() == null) continue;

                Collider[] mine = Physics.OverlapBox(centre, box, Quaternion.LookRotation(dW, Vector3.up), ~0, QueryTriggerInteraction.Collide);
                if (mine.Contains(other)) s.Warn($"the tower footprint overlaps the touch zone of {other.transform.parent.name}.");
            }
        }
    }
}
