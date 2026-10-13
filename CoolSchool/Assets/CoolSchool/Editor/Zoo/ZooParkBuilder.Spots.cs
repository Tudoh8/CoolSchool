using System.Collections.Generic;
using System.Linq;
using CoolSchool.Zoo;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The twelve hiding spots: a prop placed by its measured bounds at its size, a root "Spot_&lt;id&gt;" with the pose points
    /// (HidePoint, PeekPoint, PlayerHidePoint) and the player's glow ring. The HidingSpot component itself is added in step 2.
    /// </summary>
    internal static partial class ZooParkBuilder
    {
        // Kinds (the HidingSpotKind order).
        const int KindCover = 0;
        const int KindContainer = 1;
        const int KindPerch = 2;
        const int KindUnder = 3;

        const float PerchHeight = 3.2f;
        const float PondWidth = 5f;
        const float PondSurfaceY = 0.12f;

        sealed class SpotSpec
        {
            public string Id;
            public int Kind;
            public PrefabRef[] Prefabs;
            public float U;
            public float V;
            /// <summary>Target size of the widest side (0 = not used).</summary>
            public float Width;
            /// <summary>Target height (0 = not used).</summary>
            public float Height;
            /// <summary>The widest side sets the uniform scale, then only the height is squashed or stretched to its target.</summary>
            public bool Both;
            public bool LongAxisAlongX;
            public float Yaw;
            /// <summary>0 = the animal never goes below the ground; above 0 (pond, leaves) the ground may swallow part of it.</summary>
            public float Sink;
            /// <summary>Containers: the part of the animal's height that stands above the rim (0 = not a rim container).</summary>
            public float Show;
            /// <summary>The tent: the front part of the animal's length that sticks out past the south edge (0 = off).</summary>
            public float FrontOut;
            /// <summary>Covers and the table keep a collider, containers have none, trees have a thin trunk.</summary>
            public bool Solid;
            /// <summary>A tree: the prefab's origin is the trunk, which is what the hider stands behind.</summary>
            public bool Thin;
            public bool PlayerCanHide = true;
        }

        static readonly SpotSpec[] SpotSpecs =
        {
            new SpotSpec { Id = "hedge", Kind = KindCover, U = 15f, V = 8f, Width = 4f, Height = 1.3f, Both = true, LongAxisAlongX = true, Solid = true,
                Prefabs = new[] { new PrefabRef("SM_Env_Hedge_01", "PolygonTown"), new PrefabRef("SM_Env_Hedge_02", "PolygonTown") } },
            new SpotSpec { Id = "tree", Kind = KindCover, U = 23f, V = 6f, Height = 6f, Thin = true,
                Prefabs = new[] { Kids("SM_Generic_Tree_01"), Kids("SM_Env_Tree_01") } },
            new SpotSpec { Id = "treetop", Kind = KindPerch, U = 33f, V = 8f, Height = 6f, Thin = true, PlayerCanHide = false,
                Prefabs = new[] { new PrefabRef("SM_Env_Tree_03", "PolygonTown"), Kids("SM_Generic_Tree_02") } },
            new SpotSpec { Id = "pond", Kind = KindContainer, U = 41f, V = 5f, Width = PondWidth, Sink = 0.75f, Show = 0.45f, PlayerCanHide = false,
                Prefabs = new PrefabRef[0] },
            // A giant bin: 1.1 m wide and 1.5 m tall (the widest side sets the size, the height is stretched or squashed to fit).
            new SpotSpec { Id = "bin", Kind = KindContainer, U = 4f, V = 11f, Width = 1.1f, Height = 1.5f, Both = true, Yaw = 180f, Show = 0.4f,
                Prefabs = new[] { Kids("SM_Env_Park_Rubbish_Bin_01") } },
            // The hider stands inside at ground level, its head sticks out of the front.
            new SpotSpec { Id = "tent", Kind = KindContainer, U = 12f, V = 15f, Width = 2.4f, Yaw = 180f, FrontOut = 0.35f, PlayerCanHide = false,
                Prefabs = new[] { Kids("SM_Prop_Blanket_Fort_01") } },
            new SpotSpec { Id = "box", Kind = KindContainer, U = 21f, V = 13f, Height = 1.2f, Yaw = 180f, Show = 0.4f,
                Prefabs = new[] { Kids("SM_Prop_Cardboard_Box_01"), new PrefabRef("SM_Gen_Prop_Cardboard_Box_01", "PolygonGeneric") } },
            new SpotSpec { Id = "bush", Kind = KindCover, U = 29f, V = 17f, Height = 2f, Solid = true,
                Prefabs = new[] { new PrefabRef("SM_Gen_Env_Bush_Large_01", "PolygonGeneric"), Kids("SM_Env_Bush_01") } },
            // 2.2 m instead of the contract's 2.6 m: a giraffe (3.4 m) then looks out over the roof (2.2 m + 35 percent of it).
            new SpotSpec { Id = "house", Kind = KindContainer, U = 37f, V = 15f, Height = 2.2f, Yaw = 180f, Show = 0.35f, PlayerCanHide = false,
                Prefabs = new[] { Kids("SM_Prop_Cardboard_Building_01") } },
            new SpotSpec { Id = "leaves", Kind = KindContainer, U = 44f, V = 12f, Width = 2.8f, Height = 0.7f, Both = true, Sink = 0.6f, Show = 0.45f,
                Prefabs = new[] { new PrefabRef("SM_Gen_Env_Leaves_Pile_01", "PolygonGeneric") } },
            new SpotSpec { Id = "table", Kind = KindUnder, U = 6f, V = 19f, Width = 2f, LongAxisAlongX = true, Solid = true,
                Prefabs = new[] { new PrefabRef("SM_Prop_PicnicTable_01", "PolygonCity"), Kids("SM_Env_Park_Table_01") } },
            new SpotSpec { Id = "rock", Kind = KindCover, U = 18f, V = 20f, Height = 1.7f, Yaw = 25f, Solid = true,
                Prefabs = new[] { new PrefabRef("SM_Gen_Env_Rock_02", "PolygonGeneric"), new PrefabRef("SM_Gen_Env_Rock_05", "PolygonGeneric") } },
        };

        // Ground that no hiding spot may stand on: (centre, radius) circles and rectangles, in world x, z.
        struct ClearCircle
        {
            public Vector2 Centre;
            public float Radius;
        }

        static void BuildSpots(ParkLayout p, Transform parent)
        {
            Transform group = NewGroup("Spots", parent);
            Material glow = KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/ZooGlow.mat", new Color(1f, 0.9f, 0.35f), 0.45f);

            var circles = new List<ClearCircle>
            {
                new ClearCircle { Centre = Flat(p.ZonePoint.position), Radius = ZoneRadius + 0.8f },
                new ClearCircle { Centre = Flat(p.KeeperHome.position), Radius = 1.4f },
                new ClearCircle { Centre = Flat(p.CountPoint.position), Radius = 1.2f },
                new ClearCircle { Centre = Flat(p.PenWalkPoint.position), Radius = 1.8f },
            };
            if (p.BaseTree != null) circles.Add(new ClearCircle { Centre = Flat(p.BaseTree.position), Radius = 1.6f });
            foreach (Transform party in p.PartyPoints)
                circles.Add(new ClearCircle { Centre = Flat(party.position), Radius = 1.3f });

            var rects = new List<Rect>
            {
                // The walk from the entrance gap to the zone.
                Rect.MinMaxRect(p.MinX - 8f, EntranceV - 2.3f, p.MinX, EntranceV + 2.3f),
                // The pens, their signs and the gates' approach.
                Rect.MinMaxRect(p.Rect.xMin, PenV0 - 1.8f, p.Rect.xMax, p.Rect.yMax),
            };

            var taken = new List<Bounds>();
            for (int i = 0; i < SpotSpecs.Length; i++)
                p.Spots.Add(BuildSpot(SpotSpecs[i], i, p, group, glow, taken, circles, rects));
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        static SpotData BuildSpot(SpotSpec spec, int index, ParkLayout p, Transform group, Material glow, List<Bounds> taken,
            List<ClearCircle> circles, List<Rect> rects)
        {
            var data = new SpotData { Id = spec.Id, Kind = spec.Kind, Sink = spec.Sink, Show = spec.Show, FrontOut = spec.FrontOut };
            Vector3 nominal = p.At(spec.U, spec.V);

            // 1. The prop, at its nominal place.
            float surfaceY = 0f;
            GameObject prop = spec.Id == "pond"
                ? BuildPond(group, nominal, out surfaceY)
                : PlaceSpotProp(spec, group, nominal);
            data.PropMissing = prop == null;

            Bounds bounds = new Bounds(nominal + Vector3.up * 0.75f, new Vector3(2f, 1.5f, 2f));
            if (prop != null && TryGetBounds(prop, out Bounds measured)) bounds = measured;

            // A thin tree counts as its trunk, not its crown, when it comes to room.
            Vector3 anchor = spec.Thin ? nominal : new Vector3(bounds.center.x, 0f, bounds.center.z);
            Bounds room = spec.Thin ? new Bounds(nominal + Vector3.up, new Vector3(2.4f, 2f, 2.4f)) : bounds;

            // 2. Nudged by up to 2 m if it hits another spot, a point to keep clear, the walk or the pens.
            Vector3 delta = FindFreeOffset(room, p, taken, circles, rects, out bool free);
            if (!free)
                Debug.LogWarning($"CoolSchool zoo park: the {spec.Id} spot could not be moved clear of something within 2 m; it stays at u {spec.U:0.0}, v {spec.V:0.0}.");
            if (delta != Vector3.zero)
            {
                if (prop != null) prop.transform.position += delta;
                bounds.center += delta;
                room.center += delta;
                anchor += delta;
            }
            taken.Add(room);

            // 3. The root, at the spot's middle on the ground; the prop below it.
            var root = new GameObject("Spot_" + spec.Id).transform;
            root.SetParent(group, false);
            root.position = new Vector3(anchor.x, 0f, anchor.z);
            if (prop != null)
            {
                prop.transform.SetParent(root, true);
                prop.name = spec.Id;
                ApplyColliders(prop, spec);
            }
            data.Root = root;
            data.Cover = prop != null ? prop.transform : null;
            data.Bounds = bounds;
            // The rim: the top of the container above the ground (the water level for the pond).
            data.Rim = spec.Show > 0f ? (spec.Id == "pond" ? surfaceY : Mathf.Max(0.1f, bounds.max.y)) : 0f;

            // 4. The poses.
            BuildPoses(spec, index, p, data, root, bounds, anchor, glow);
            return data;
        }

        // ---- The prop ----

        static GameObject PlaceSpotProp(SpotSpec spec, Transform parent, Vector3 nominal)
        {
            PrefabRef reference = FirstExisting(spec.Prefabs);
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject prop = Spawn(prefab, parent);
            prop.name = spec.Id;
            prop.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);

            // A long thing (hedge, table) must run east-west: the camera then sees its long side.
            if (spec.LongAxisAlongX && TryGetBounds(prop, out Bounds turn) && turn.size.z > turn.size.x * 1.05f)
                prop.transform.rotation = Quaternion.Euler(0f, spec.Yaw + 90f, 0f);

            ScaleToSpec(prop, spec);

            if (spec.Thin)
            {
                // Trunk (the prefab's origin) at the spot, standing on the ground by its lowest point.
                prop.transform.position = new Vector3(nominal.x, 0f, nominal.z);
                if (TryGetBounds(prop, out Bounds tree)) prop.transform.position += Vector3.up * -tree.min.y;
            }
            else
            {
                AlignToBounds(prop, nominal.x, nominal.z, 0f, false);
            }

            if (TryGetBounds(prop, out Bounds placed))
                Debug.Log($"CoolSchool zoo park: {spec.Id} is {reference.Name}, {placed.size.x:0.00} x {placed.size.y:0.00} x {placed.size.z:0.00} m (x, y, z).");
            return prop;
        }

        static void ScaleToSpec(GameObject prop, SpotSpec spec)
        {
            if (!TryGetBounds(prop, out Bounds bounds)) return;

            Vector3 scale = prop.transform.localScale;
            float widest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (spec.Both && widest > 0.01f && bounds.size.y > 0.01f)
            {
                // The widest side sets the size; only the height is then squashed or stretched to its target (within reason).
                float k = spec.Width / widest;
                float ratio = Mathf.Clamp(spec.Height / (bounds.size.y * k), 0.4f, 1.6f);
                prop.transform.localScale = new Vector3(scale.x * k, scale.y * k * ratio, scale.z * k);
            }
            else if (spec.Height > 0f && bounds.size.y > 0.01f)
            {
                prop.transform.localScale = scale * Mathf.Clamp(spec.Height / bounds.size.y, 0.05f, 20f);
            }
            else if (spec.Width > 0f && widest > 0.01f)
            {
                prop.transform.localScale = scale * Mathf.Clamp(spec.Width / widest, 0.05f, 20f);
            }
        }

        /// <summary>Covers and the table keep (or get) a collider; trees get a thin trunk; containers have none, so the child can hop in.</summary>
        static void ApplyColliders(GameObject prop, SpotSpec spec)
        {
            if (spec.Thin)
            {
                foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                float scale = Mathf.Max(0.01f, prop.transform.lossyScale.y);
                var trunk = prop.AddComponent<CapsuleCollider>();
                trunk.direction = 1;
                trunk.radius = TrunkRadius / scale;
                trunk.height = 3f / scale;
                trunk.center = new Vector3(0f, 1.5f / scale, 0f);
            }
            else if (spec.Solid)
            {
                AddMeshCollidersIfNone(prop);
            }
            else
            {
                foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
            }
        }

        // ---- Room ----

        static Vector3 FindFreeOffset(Bounds room, ParkLayout p, List<Bounds> taken, List<ClearCircle> circles, List<Rect> rects, out bool free)
        {
            free = true;
            if (!Blocked(room, p, taken, circles, rects)) return Vector3.zero;

            for (float radius = 0.5f; radius <= 2.01f; radius += 0.5f)
            {
                for (int step = 0; step < 8; step++)
                {
                    float angle = step * 45f * Mathf.Deg2Rad;
                    var delta = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                    Bounds moved = room;
                    moved.center += delta;
                    if (!Blocked(moved, p, taken, circles, rects)) return delta;
                }
            }
            free = false;
            return Vector3.zero;
        }

        /// <summary>True when the bounds (on the ground plane) hit another spot, a point to keep clear, a rectangle to keep clear, or the park's fence.</summary>
        static bool Blocked(Bounds room, ParkLayout p, List<Bounds> taken, List<ClearCircle> circles, List<Rect> rects)
        {
            const float between = 1.0f;
            var mine = Rect.MinMaxRect(room.min.x - between * 0.5f, room.min.z - between * 0.5f, room.max.x + between * 0.5f, room.max.z + between * 0.5f);

            // Inside the park, 1.5 m clear of its fence.
            Rect inside = Rect.MinMaxRect(p.Rect.xMin + 1.5f, p.Rect.yMin + 1.5f, p.Rect.xMax - 1.5f, p.Rect.yMax - 1.5f);
            if (mine.xMin < inside.xMin || mine.xMax > inside.xMax || mine.yMin < inside.yMin || mine.yMax > inside.yMax) return true;

            foreach (Bounds other in taken)
                if (mine.Overlaps(Rect.MinMaxRect(other.min.x - between * 0.5f, other.min.z - between * 0.5f, other.max.x + between * 0.5f, other.max.z + between * 0.5f)))
                    return true;

            foreach (Rect rect in rects)
                if (mine.Overlaps(rect)) return true;

            foreach (ClearCircle circle in circles)
            {
                float nearestX = Mathf.Clamp(circle.Centre.x, mine.xMin, mine.xMax);
                float nearestZ = Mathf.Clamp(circle.Centre.y, mine.yMin, mine.yMax);
                if ((new Vector2(nearestX, nearestZ) - circle.Centre).sqrMagnitude < circle.Radius * circle.Radius) return true;
            }
            return false;
        }

        // ---- The pond ----

        /// <summary>
        /// A flat, friendly blue-green disc 5 m across (its top 12 cm above the grass; deliberately not realistic water) in a ring of small
        /// rocks (0.4 to 0.7 m, so they do not hide the pond from the camera). Everything has no collider (a container: the hider stands in
        /// the water, the child may wade in). The contract's SM_Env_Park_Pond_01 is a composite whose water level is unknown without a
        /// render, so the predictable disc is used.
        /// </summary>
        static GameObject BuildPond(Transform parent, Vector3 centre, out float surfaceY)
        {
            surfaceY = PondSurfaceY;
            var root = new GameObject("pond");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(centre.x, 0f, centre.z);

            Material water = LitMaterial("ZooPondWater", new Color(0.3f, 0.55f, 0.55f), 0.1f, 0f);
            const float thickness = 0.1f;
            // A cylinder primitive is 2 m tall and 1 m wide at scale 1.
            Primitive(PrimitiveType.Cylinder, "Water", root.transform, new Vector3(0f, PondSurfaceY - thickness * 0.5f, 0f),
                new Vector3(PondWidth, thickness * 0.5f, PondWidth), Quaternion.identity, water);

            // The ring of rocks, 9 small rocks (the rock models of the generic pack), a few centimetres outside the water's edge.
            PrefabRef[] rockRefs =
            {
                new PrefabRef("SM_Gen_Env_Rock_02", "PolygonGeneric"),
                new PrefabRef("SM_Gen_Env_Rock_05", "PolygonGeneric"),
                new PrefabRef("SM_Gen_Env_Rock_03", "PolygonGeneric"),
            };
            const int rocks = 9;
            for (int i = 0; i < rocks; i++)
            {
                GameObject prefab = LoadPrefab(rockRefs[i % rockRefs.Length]);
                if (prefab == null) continue;

                float angle = (i / (float)rocks) * Mathf.PI * 2f + 0.2f;
                float radius = PondWidth * 0.5f + 0.15f;
                GameObject rock = Spawn(prefab, root.transform);
                rock.name = "PondRock_" + (i + 1);
                rock.transform.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                // The biggest side of the rock is 0.4, 0.55 or 0.7 m.
                if (TryGetBounds(rock, out Bounds b))
                {
                    float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (biggest > 0.01f) rock.transform.localScale *= Mathf.Clamp((0.4f + 0.15f * (i % 3)) / biggest, 0.02f, 20f);
                }
                AlignToBounds(rock, centre.x + Mathf.Sin(angle) * radius, centre.z + Mathf.Cos(angle) * radius, -0.05f, false);
                foreach (Collider collider in rock.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
            }
            return root;
        }

        // ---- Poses ----

        static float MeanHeight(string spotId)
        {
            var heights = ZooAnimalKind.Order.Where(k => k.AllowedSpots.Contains(spotId)).Select(k => k.Height).ToList();
            return heights.Count > 0 ? heights.Average() : 1f;
        }

        // The child is about 1.2 m tall; in a container the top 45 percent of him shows over the rim.
        const float KidHeight = 1.2f;
        const float KidShowFraction = 0.45f;
        // The glowing ring: 1 m radius, 1 m south of the prop's south edge (where he can see it and reach it).
        const float GlowRadius = 1f;
        const float GlowDistance = 1f;

        /// <summary>
        /// HidePoint, PeekPoint, PlayerHidePoint and the player's glow, all in world space (the points are children of the spot root).
        /// Covers: a hider stands 0.4 m north of the cover's north edge (HidingSpot adds half its depth), peeks 0.8 m beside an end.
        /// Containers: the hide point is the middle on the ground; HidingSpot raises or sinks the hider from the rim height and the show
        /// fraction. A perch is up in the crown on its south side. The child's own point: covers: right behind (a tall bush or rock: at
        /// its side end, so his head shows); the table: behind it; bin and box: inside, raised so the top 45 percent of him shows over
        /// the rim; leaves: the middle, sunk so the top 45 percent shows. His glow lies south of the prop (it is where he walks to).
        /// </summary>
        static void BuildPoses(SpotSpec spec, int index, ParkLayout p, SpotData data, Transform root, Bounds b, Vector3 anchor, Material glow)
        {
            Quaternion south = Quaternion.LookRotation(Vector3.back, Vector3.up);
            Vector3 hide;
            Vector3 peek;
            Vector3? player = null;
            float meanHeight = MeanHeight(spec.Id);
            float side = index % 2 == 0 ? 1f : -1f;

            switch (spec.Kind)
            {
                case KindCover:
                {
                    float northEdge = spec.Thin ? anchor.z + TrunkRadius : b.max.z;
                    float middleX = spec.Thin ? anchor.x : b.center.x;
                    float halfWidth = spec.Thin ? TrunkRadius : b.extents.x;
                    hide = new Vector3(middleX, 0f, northEdge + 0.4f);
                    peek = new Vector3(middleX + side * (halfWidth + 0.8f), 0f, hide.z);

                    if (spec.Thin)
                    {
                        // Half behind the trunk: his shoulders show on one side of it.
                        player = new Vector3(middleX + side * (TrunkRadius + 0.05f), 0f, northEdge + 0.15f);
                    }
                    else if (b.size.y > 1.5f)
                    {
                        // A bush or a rock this tall would hide him completely: he stands tucked in at its rounded end, where it is low.
                        const float endShare = 0.9f;
                        float depthShare = Mathf.Sqrt(1f - endShare * endShare);
                        player = new Vector3(middleX + side * halfWidth * endShare, 0f, b.center.z + b.extents.z * depthShare + 0.35f);
                    }
                    else
                    {
                        // A hedge: right behind it, at ground height (his head and shoulders show over it).
                        player = new Vector3(middleX, 0f, northEdge + 0.3f);
                    }
                    break;
                }
                case KindPerch:
                {
                    float height = Mathf.Min(PerchHeight, b.size.y * 0.7f);
                    float southReach = Mathf.Clamp((anchor.z - b.min.z) * 0.55f, 1f, 2f);
                    hide = new Vector3(anchor.x, height, anchor.z - southReach);
                    peek = hide + new Vector3(0f, -0.2f, -0.3f);
                    break;
                }
                case KindUnder:
                    hide = new Vector3(b.center.x, 0f, b.center.z);
                    peek = hide + Vector3.up * (0.35f * meanHeight);
                    // The child cannot fit under a table: he stands behind it, on the north side, on the ground.
                    player = new Vector3(b.center.x, 0f, b.max.z + 0.35f);
                    break;
                default:
                {
                    // On the ground in the middle; HidingSpot places the animal from the rim height and the show fraction.
                    hide = new Vector3(b.center.x, 0f, b.center.z);
                    peek = hide + Vector3.up * (0.35f * meanHeight);
                    // The child stands inside, raised (or sunk in a low pile of leaves) so the top 45 percent of him shows over the rim.
                    float y = data.Rim - (1f - KidShowFraction) * KidHeight;
                    player = new Vector3(b.center.x, y, b.center.z);
                    break;
                }
            }

            data.Hide = NewPoint("HidePoint", root, hide);
            data.Hide.rotation = south;
            data.Peek = NewPoint("PeekPoint", root, peek);
            data.Peek.rotation = south;

            if (player.HasValue && spec.PlayerCanHide)
            {
                data.PlayerHide = NewPoint("PlayerHidePoint", root, player.Value);
                data.PlayerHide.rotation = south;

                // The glow: a flat soft disc on the ground south of the prop, where he can see it and walk to it.
                Transform ring = Primitive(PrimitiveType.Cylinder, "PlayerGlow", root, Vector3.zero, new Vector3(GlowRadius * 2f, 0.01f, GlowRadius * 2f),
                    Quaternion.identity, glow);
                ring.position = new Vector3(b.center.x, 0.12f, b.min.z - GlowDistance);
                var renderer = ring.GetComponent<MeshRenderer>();
                renderer.receiveShadows = false;
                ring.gameObject.SetActive(false);
                data.Glow = ring.gameObject;
            }

            if (spec.Kind == KindContainer)
                data.CheckRadius = 1.4f;
        }
    }
}
