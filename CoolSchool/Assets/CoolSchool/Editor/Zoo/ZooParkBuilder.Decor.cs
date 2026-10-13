using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The park is not an empty lawn: trees along (and just outside) the west and south fence, three benches and two lamp posts beside a path of round
    /// stepping stones (from the entrance gap past the zone to the pen walk point), a few flower patches and little bushes (all lower
    /// than 0.6 m, so they never look like hiding places). Everything stays at least 3 m from every hiding spot (and its glow), the
    /// pens, the zone, the count point, the keeper's home, the party points, the pen walk point and the walk from the entrance; only
    /// the stepping stones are allowed near those (they lead to the zone and the pens), and they keep 1.6 m from the hiding spots.
    /// Static, with colliders only where natural (trunks, benches, lamp posts). Built before the NavMesh is baked.
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        const float DecorClearMeters = 3f;
        const float PathStoneSpacing = 0.95f;
        const float PathSpotClearMeters = 1.6f;
        const float LittleGreenHeight = 0.5f;
        const float LampHeight = 2.6f;

        static readonly PrefabRef[] DecorTreePrefabs =
        {
            Kids("SM_Env_Tree_02"),
            Kids("SM_Env_Tree_Tall_01"),
            Kids("SM_Env_Tree_Pine_01"),
            Kids("SM_Env_Tree_Tall_02"),
            Kids("SM_Env_Tree_Pine_02"),
            new PrefabRef("SM_Gen_Env_Tree_01", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Env_Tree_02", "PolygonGeneric"),
        };

        static readonly PrefabRef[] FlowerPrefabs =
        {
            Kids("SM_Env_FlowerPatch_01"),
            Kids("SM_Env_FlowerPatch_02"),
            Kids("SM_Env_FlowerPatch_03"),
            new PrefabRef("SM_Gen_Env_Flowers_01", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Env_Flowers_03", "PolygonGeneric"),
        };

        static readonly PrefabRef[] LittleBushPrefabs =
        {
            Kids("SM_Env_Bush_02"),
            Kids("SM_Env_Bush_03"),
            Kids("SM_Env_Bush_04"),
        };

        static readonly PrefabRef ParkBenchPrefab = Kids("SM_Env_Park_Seat_01");
        static readonly PrefabRef ParkLampPrefab = Kids("SM_Prop_Park_Lamp_01");

        /// <summary>Ground (in world x, z) where something may not stand: rectangles and circles, each already grown by its margin.</summary>
        sealed class DecorField
        {
            readonly List<Rect> rects = new List<Rect>();
            // x = world x, y = world z, z = radius.
            readonly List<Vector3> circles = new List<Vector3>();
            public Rect Inside;

            public void AddRect(Rect area, float margin)
            {
                rects.Add(Rect.MinMaxRect(area.xMin - margin, area.yMin - margin, area.xMax + margin, area.yMax + margin));
            }

            public void AddCircle(Vector3 centre, float radius)
            {
                circles.Add(new Vector3(centre.x, centre.z, radius));
            }

            /// <summary>True when something with this footprint radius fits at the point.</summary>
            public bool Free(Vector2 point, float footprint)
            {
                if (point.x - footprint < Inside.xMin || point.x + footprint > Inside.xMax
                    || point.y - footprint < Inside.yMin || point.y + footprint > Inside.yMax) return false;

                foreach (Rect area in rects)
                {
                    if (point.x > area.xMin - footprint && point.x < area.xMax + footprint
                        && point.y > area.yMin - footprint && point.y < area.yMax + footprint) return false;
                }

                foreach (Vector3 circle in circles)
                {
                    float reach = circle.z + footprint;
                    if ((point - new Vector2(circle.x, circle.y)).sqrMagnitude < reach * reach) return false;
                }
                return true;
            }
        }

        /// <summary>One point on the stepping stone path: where, which way it runs, and how far along the path it is.</summary>
        struct PathSample
        {
            public Vector3 Point;
            public Vector3 Direction;
            public float Distance;
        }

        static void BuildDecor(ParkLayout p, Transform parent)
        {
            Transform group = NewGroup("Decor", parent);
            var random = new System.Random(20261013);

            // The ground that stays clear of trees, benches, lamps and flowers (3 m), and the lighter rules for the stones.
            DecorField clear = MakeDecorField(p, DecorClearMeters, 1f, true);
            DecorField stonesField = MakeDecorField(p, PathSpotClearMeters, 0.4f, false);

            List<PathSample> path = SamplePath(p);
            int stones = LayStones(path, stonesField, group, random);
            var taken = new List<Vector2>();
            int benches = PlaceParkFurniture(p, path, clear, group, taken);
            int trees = PlaceFenceTrees(p, clear, group, random, taken);
            int flowers = PlaceLittleGreen(clear, group, random, taken, FlowerPrefabs, 8, true);
            int bushes = PlaceLittleGreen(clear, group, random, taken, LittleBushPrefabs, 4, false);

            Debug.Log($"CoolSchool zoo park: decor: {trees} trees, {benches} benches and lamps, {flowers} flower patches, {bushes} little bushes, "
                + $"{stones} stepping stones on a {path.LastOrDefault().Distance:0} m path.");
        }

        // ---- What stays clear ----

        /// <summary>
        /// The clear ground. spotMargin is how far from the hiding spots (their props and their glow rings) it must stay, fence is the
        /// space left to the park's own fence. With full set it also keeps clear of the pens, the zone, the count point, the keeper's
        /// home, the base tree, the party points, the pen walk point and the walk from the entrance.
        /// </summary>
        static DecorField MakeDecorField(ParkLayout p, float spotMargin, float fence, bool full)
        {
            var field = new DecorField
            {
                Inside = Rect.MinMaxRect(p.Rect.xMin + fence, p.Rect.yMin + fence, p.Rect.xMax - fence, p.Rect.yMax - fence),
            };

            foreach (SpotData spot in p.Spots)
            {
                field.AddRect(Rect.MinMaxRect(spot.Bounds.min.x, spot.Bounds.min.z, spot.Bounds.max.x, spot.Bounds.max.z), spotMargin);
                if (spot.Glow == null) continue;

                // The glowing ring on the ground in front of it (1 m radius).
                Vector3 glow = spot.Glow.transform.position;
                field.AddRect(Rect.MinMaxRect(glow.x - 1f, glow.z - 1f, glow.x + 1f, glow.z + 1f), full ? 1.5f : 0.4f);
            }

            // Pens, in a row along the north fence (with their signs and gates).
            float penWest = p.MinX - (PenU0 + PenWidth * PenCount);
            float penEast = p.MinX - PenU0;
            field.AddRect(Rect.MinMaxRect(penWest, PenV0 - 0.8f, penEast, PenV1), full ? DecorClearMeters : 1f);

            field.AddCircle(p.ZonePoint.position, ZoneRadius + (full ? DecorClearMeters : 0.5f));
            field.AddCircle(p.CountPoint.position, full ? DecorClearMeters : 1.2f);
            field.AddCircle(p.KeeperHome.position, full ? DecorClearMeters : 1.2f);
            if (p.BaseTree != null) field.AddCircle(p.BaseTree.position, full ? DecorClearMeters + 1.5f : 1.5f);

            if (full)
            {
                field.AddCircle(p.PenWalkPoint.position, DecorClearMeters);
                foreach (Transform party in p.PartyPoints) field.AddCircle(party.position, DecorClearMeters);
                // The walk from the entrance gap to the zone.
                field.AddRect(Rect.MinMaxRect(p.MinX - 9f, EntranceV - 2.5f, p.MinX + 0.5f, EntranceV + 2.5f), 0.5f);
            }
            return field;
        }

        // ---- The stepping stones ----

        /// <summary>
        /// The path in park coordinates (u, v): from the entrance gap east-west past the zone, along the south of the park, north between
        /// the hiding spots and on to the pen walk point. It is a long way round on purpose: the hiding spots stand too close together for
        /// a straight line to keep its distance from them.
        /// </summary>
        static List<PathSample> SamplePath(ParkLayout p)
        {
            Vector2[] corners =
            {
                new Vector2(0.4f, -3f), new Vector2(9.6f, -3.4f), new Vector2(27.5f, -3.4f), new Vector2(27.5f, 8f),
                new Vector2(25f, 13f), new Vector2(24f, 17f), new Vector2(24f, PenWalkV - 1.3f),
            };

            var samples = new List<PathSample>();
            float travelled = 0f;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 from = p.At(corners[i].x, corners[i].y);
                Vector3 to = p.At(corners[i + 1].x, corners[i + 1].y);
                float length = Vector3.Distance(from, to);
                Vector3 direction = (to - from) / Mathf.Max(0.01f, length);
                for (float along = 0f; along < length - 0.01f; along += PathStoneSpacing)
                {
                    samples.Add(new PathSample { Point = from + direction * along, Direction = direction, Distance = travelled + along });
                }
                travelled += length;
            }
            samples.Add(new PathSample { Point = p.At(corners[corners.Length - 1].x, corners[corners.Length - 1].y), Direction = Vector3.forward, Distance = travelled });
            return samples;
        }

        /// <summary>A flat round stone every 0.95 m along the path, a little to the left or right and different in size; none where something stands.</summary>
        static int LayStones(List<PathSample> path, DecorField field, Transform parent, System.Random random)
        {
            Transform group = NewGroup("Path", parent);
            Material stone = LitMaterial("ZooStone", new Color(0.82f, 0.77f, 0.68f), 0.2f);

            int count = 0;
            foreach (PathSample sample in path)
            {
                var flat = new Vector2(sample.Point.x, sample.Point.z);
                if (!field.Free(flat, 0.35f)) continue;

                Vector3 side = Vector3.Cross(Vector3.up, sample.Direction);
                Vector3 position = sample.Point + side * ((count % 2 == 0 ? 1f : -1f) * 0.12f) + Vector3.up * 0.03f;
                float width = 0.62f + 0.16f * (float)random.NextDouble();
                // A cylinder primitive is 2 m tall and 1 m wide at scale 1: 0.04 gives a 8 cm thick stone, its top 7 cm above the lawn.
                Transform disc = Primitive(PrimitiveType.Cylinder, "Stone_" + (count + 1), group, position, new Vector3(width, 0.04f, width),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), stone);
                MarkStatic(disc.gameObject);
                count++;
            }
            return count;
        }

        // ---- Benches and lamp posts ----

        /// <summary>Three benches and two lamp posts beside the path, spread along it, each at a place that is clear.</summary>
        static int PlaceParkFurniture(ParkLayout p, List<PathSample> path, DecorField field, Transform parent, List<Vector2> taken)
        {
            if (path.Count == 0) return 0;

            Transform group = NewGroup("Furniture", parent);
            float total = path[path.Count - 1].Distance;
            GameObject bench = LoadPrefab(ParkBenchPrefab);
            GameObject lamp = LoadPrefab(ParkLampPrefab);

            // (what, where along the path as a fraction): benches and lamps alternate.
            var wanted = new List<(bool isBench, float fraction)>
            {
                (true, 0.3f), (false, 0.2f), (true, 0.52f), (false, 0.62f), (true, 0.82f),
            };

            int placed = 0;
            foreach ((bool isBench, float fraction) in wanted)
            {
                GameObject prefab = isBench ? bench : lamp;
                if (prefab == null) continue;

                // The best clear place near that part of the path, on either side of it.
                float bestGap = float.MaxValue;
                Vector3 bestPoint = default;
                Vector3 bestPath = default;
                foreach (PathSample sample in path)
                {
                    float gap = Mathf.Abs(sample.Distance - total * fraction);
                    if (gap >= bestGap) continue;

                    Vector3 side = Vector3.Cross(Vector3.up, sample.Direction);
                    foreach (float sign in new[] { 1f, -1f })
                    {
                        Vector3 point = sample.Point + side * (sign * 1.7f);
                        var flat = new Vector2(point.x, point.z);
                        if (!field.Free(flat, 1f) || taken.Any(t => (t - flat).sqrMagnitude < 3.5f * 3.5f)) continue;

                        bestGap = gap;
                        bestPoint = point;
                        bestPath = sample.Point;
                        break;
                    }
                }
                if (bestGap == float.MaxValue)
                {
                    Debug.LogWarning($"CoolSchool zoo park: no clear place for a {(isBench ? "bench" : "lamp post")} near {fraction * 100f:0} percent of the path.");
                    continue;
                }

                GameObject item = Spawn(prefab, group);
                item.name = isBench ? "Bench" : "LampPost";
                if (isBench)
                {
                    // The bench faces the path.
                    Vector3 toPath = bestPath - bestPoint;
                    toPath.y = 0f;
                    item.transform.rotation = Quaternion.LookRotation(toPath, Vector3.up);
                }
                else if (TryGetBounds(item, out Bounds lampBounds) && lampBounds.size.y > 0.01f)
                {
                    item.transform.localScale *= LampHeight / lampBounds.size.y;
                }
                AlignToBounds(item, bestPoint.x, bestPoint.z, 0f, false);
                FinishEnvironmentObject(item);
                taken.Add(new Vector2(bestPoint.x, bestPoint.z));
                placed++;
            }
            return placed;
        }

        // ---- Trees ----

        /// <summary>10 to 12 trees of different kinds and heights along the west and the south fence (inside where it is clear, the rest just outside), 3 m or more from the next tree.</summary>
        static int PlaceFenceTrees(ParkLayout p, DecorField field, Transform parent, System.Random random, List<Vector2> taken)
        {
            Transform group = NewGroup("Trees", parent);
            var kinds = DecorTreePrefabs.Where(PrefabExists).ToArray();
            if (kinds.Length == 0)
            {
                Debug.LogWarning("CoolSchool zoo park: none of the decoration trees exists, so the park has no extra trees.");
                return 0;
            }

            // Candidate trunks along the west fence and along the south fence, 2.6 m inside it (the field keeps 1 m from the fence plus
            // the 1.4 m footprint), every 2.5 m. The west fence is mostly blocked (the pond, the leaves and the pens keep 3 m clear), so
            // a few more stand just outside the west and the south fence, on the park's own grass (it reaches 6 m beyond the west fence
            // and 10 m beyond the south one), where nothing can be in the way.
            var west = new List<Vector2>();
            var south = new List<Vector2>();
            for (float z = p.Rect.yMin + 2.6f; z <= PenV0 - 3f; z += 2.5f) west.Add(new Vector2(p.Rect.xMin + 2.6f, z));
            for (float x = p.Rect.xMin + 5.1f; x <= p.Rect.xMax - 3f; x += 2.5f) south.Add(new Vector2(x, p.Rect.yMin + 2.6f));
            var inner = Interleave(Shuffled(west, random), Shuffled(south, random));

            var outerWest = new List<Vector2>();
            var outerSouth = new List<Vector2>();
            for (float z = p.Rect.yMin - 3.5f; z <= p.Rect.yMax; z += 2.5f) outerWest.Add(new Vector2(p.Rect.xMin - 3.5f, z));
            for (float x = p.Rect.xMin - 1f; x <= p.Rect.xMax - 4f; x += 2.5f) outerSouth.Add(new Vector2(x, p.Rect.yMin - 3.5f));
            var outer = Interleave(Shuffled(outerWest, random), Shuffled(outerSouth, random));

            const int innerWanted = 7;
            const int wanted = 12;
            var trunks = new List<Vector2>();
            foreach (float spacing in new[] { 4f, 3f })
            {
                foreach (Vector2 candidate in inner)
                {
                    if (trunks.Count >= innerWanted) break;
                    if (!field.Free(candidate, 1.4f)) continue;
                    if (trunks.Any(t => (t - candidate).sqrMagnitude < spacing * spacing)) continue;
                    trunks.Add(candidate);
                }
                foreach (Vector2 candidate in outer)
                {
                    if (trunks.Count >= wanted) break;
                    if (trunks.Any(t => (t - candidate).sqrMagnitude < spacing * spacing)) continue;
                    trunks.Add(candidate);
                }
                if (trunks.Count >= 10) break;
            }

            int count = 0;
            foreach (Vector2 trunk in trunks)
            {
                PrefabRef kind = kinds[count % kinds.Length];
                float height = 5.5f + 2.5f * (float)random.NextDouble();
                float yaw = (float)random.NextDouble() * 360f;
                GameObject tree = PlaceTree(kind, group, new Vector3(trunk.x, 0f, trunk.y), height, yaw, "Tree_" + (count + 1));
                if (tree == null) continue;

                FinishEnvironmentObject(tree);
                taken.Add(trunk);
                count++;
            }
            return count;
        }

        static List<Vector2> Shuffled(List<Vector2> items, System.Random random)
        {
            var result = new List<Vector2>(items);
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        /// <summary>The two lists woven into one: first of the first, first of the second, second of the first...</summary>
        static List<Vector2> Interleave(List<Vector2> first, List<Vector2> second)
        {
            var result = new List<Vector2>(first.Count + second.Count);
            for (int i = 0; i < Mathf.Max(first.Count, second.Count); i++)
            {
                if (i < first.Count) result.Add(first[i]);
                if (i < second.Count) result.Add(second[i]);
            }
            return result;
        }

        // ---- Flower patches and little bushes ----

        /// <summary>Low plants (under 0.6 m, no collider) scattered over the lawn, each 3 m clear of everything and of each other.</summary>
        static int PlaceLittleGreen(DecorField field, Transform parent, System.Random random, List<Vector2> taken, PrefabRef[] kinds, int wanted, bool flowers)
        {
            Transform group = NewGroup(flowers ? "Flowers" : "LittleBushes", parent);
            var prefabs = kinds.Where(PrefabExists).Select(LoadPrefab).Where(prefab => prefab != null).ToArray();
            if (prefabs.Length == 0) return 0;

            float xMin = field.Inside.xMin;
            float zMin = field.Inside.yMin;
            float xSize = field.Inside.width;
            float zSize = field.Inside.height;

            int count = 0;
            for (int attempt = 0; attempt < 800 && count < wanted; attempt++)
            {
                var point = new Vector2(xMin + (float)random.NextDouble() * xSize, zMin + (float)random.NextDouble() * zSize);
                if (!field.Free(point, 0.8f)) continue;
                if (taken.Any(t => (t - point).sqrMagnitude < 3f * 3f)) continue;

                GameObject item = Spawn(prefabs[count % prefabs.Length], group);
                item.name = (flowers ? "FlowerPatch_" : "LittleBush_") + (count + 1);
                item.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                if (TryGetBounds(item, out Bounds bounds) && bounds.size.y > 0.01f)
                {
                    // Under 0.5 m tall; a flower patch is also at most 1.4 m wide.
                    float scale = LittleGreenHeight / bounds.size.y;
                    float widest = Mathf.Max(bounds.size.x, bounds.size.z);
                    if (flowers && widest > 0.01f) scale = Mathf.Min(scale, 1.4f / widest);
                    item.transform.localScale *= Mathf.Clamp(scale, 0.02f, 5f);
                }
                AlignToBounds(item, point.x, point.y, 0f, false);
                foreach (Collider collider in item.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                MarkStatic(item);
                taken.Add(point);
                count++;
            }
            return count;
        }

        /// <summary>Marks the object and its children static for batching (no colliders are added).</summary>
        static void MarkStatic(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, GameObjectUtility.GetStaticEditorFlags(child.gameObject) | StaticEditorFlags.BatchingStatic);
        }
    }
}
