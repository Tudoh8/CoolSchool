using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Shop;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the ice cream shop on the playground, in the north-west corner inside the fence, facing south (toward the follow camera).
    /// Two steps, like the aliens: BuildStall (static props, tubs, points: before the NavMesh bake) and Build (the activity,
    /// the customers, the order bubble, the zone and the invitation: after the UI and the player exist).
    /// The stall is laid out in its own frame, where x goes to the right as the shop camera sees it (west), z goes toward the
    /// customers (south) and y is up. The shop camera stands north of the counter and looks south over it.
    /// </summary>
    internal static class IceCreamShopBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string InviteGroup = "invite.shop";

        // ---- Where the stall stands: in the north-west quadrant, close to the north fence, clear of everything else ----
        // These are the preferred place. The real place is searched around it (ChoosePlacement), from the measured equipment bounds.
        const float StallX = -9f;
        const float StallFromFence = 4.7f;
        // The search range: the stall centre's x, and how far its centre is from the north fence.
        const float SearchMinX = -16f;
        const float SearchMaxX = -5.5f;
        const float SearchMinFromFence = 3.2f;
        const float SearchMaxFromFence = 9f;
        const float SearchStep = 0.25f;
        // Playground equipment is measured by its bounds (which are bigger than a rotated item), plus this margin.
        const float EquipmentMargin = 0.5f;
        // The footprint stays this far inside the fence, and west of the school path (its half width plus this).
        const float FenceClearance = 0.8f;
        const float SchoolPathClearance = 0.5f;
        // Benches, bins and trees keep out of the footprint plus this margin.
        const float TreeMargin = 1.2f;
        // The footprint in the stall frame: behind the counter for the keeper, in front for the order spot, to the side for the parasol.
        const float BehindCounter = 0.9f;
        const float BesideCounterEast = 0.4f;
        // The sign stands beyond the west end of the counter, outside the shop camera's picture (see SignCentreX).
        const float SignReach = 2.6f;
        const float LaneHalfThickness = 0.9f;
        // The customers' walk: at least this far to each side (out of the shop camera's picture), at most this far.
        const float MinLaneLength = 7f;
        const float MaxLaneLength = 8.5f;
        // The east end of the walk stays west of the bench by the school path.
        const float EastLimitX = 3.8f;


        // ---- The counter ----
        const float CounterHeight = 1f;
        const float MinCounterWidth = 3.8f;
        const float FallbackCounterWidth = 4.2f;
        const float FallbackCounterDepth = 1.1f;

        // ---- The things on the counter (frame metres: x to the camera's right, z toward the customers) ----
        // The tubs are sunk into the counter along the camera-side edge, like a real ice cream display: only a low mound shows
        // (TubMoundHeight above the counter), in two pairs with the cone between them. The coins lie in the same row to the right of
        // the tubs, then comes the register: all of it is in the shop camera's picture, and nothing stands in front of anything else.
        const float TubInnerX = 0.44f;
        const float TubSpacing = 0.56f;
        const float TubRadius = 0.26f;
        const float TubRimHeight = 0.03f;
        const float TubMoundHeight = 0.1f;
        const float TubEdgeInset = 0.01f;
        const float ConeBehindTubs = 0.12f;
        const float KeeperFromEnd = 0.35f;
        const float RegisterX = 2.62f;
        const float RegisterWidth = 0.5f;
        const float CoinSpotX = 1.85f;
        const float HolderHeight = 0.1f;

        // ---- Sign ----
        // A free-standing sign on two posts, beyond the west end of the counter: outside the shop camera's picture (4:3 and 16:9),
        // and still facing south, where the follow camera sees it from the playground.
        // The sign prefab is a tall cone-shaped pole sign (about 5.7 m when scaled to 2.2 m wide): it is scaled to this height and
        // stands on one post, low enough to be seen from the playground (the follow camera looks at it from the south).
        const float SignHeight = 2.0f;
        const float SignFootHeight = 1.1f;
        const float PostRadius = 0.07f;
        const float SignCentreX = 4.8f;

        // ---- Customers and camera ----
        const float OrderFromCounterFront = 0.85f;
        // The customer stands left of the cone (as the camera sees it), so the scoops never hide the face.
        const float OrderSpotX = -1.05f;
        // A high, steep view over the counter: the tubs, cone, coins and register fill the lower half, and even the 2.8 m dragon
        // fits above them. It is centred on the middle of the things on the counter, not on the stall.
        const float CameraHeight = 4.5f;
        const float CameraBehindCounterFront = 2.4f;
        const float CameraSideShift = 0.8f;
        const float CameraPitch = 36f;
        const float ZoneBeyondOrderSpot = 3.4f;
        const float ZoneRadius = 1.6f;

        static readonly PrefabRef CounterPrefab = new PrefabRef("SM_Prop_ShopCounter_01", "PolygonTown");
        static readonly PrefabRef FreezerPrefab = new PrefabRef("SM_Prop_Icecream_Freezer_01", "PolygonTown");
        static readonly PrefabRef SignPrefab = new PrefabRef("SM_Prop_LargeSign_Icecream_01", "PolygonCity");
        static readonly PrefabRef RegisterPrefab = Kids("SM_Prop_Cash_Register_01");

        // A pink beacon with a coin on it.
        static readonly ActivityBeaconBuilder.Style ShopBeacon = new ActivityBeaconBuilder.Style(
            "Shop", new Color(2f, 0.8f, 1.3f, 1f), new[] { "Coin", "Gold", "Coins" });

        /// <summary>Everything the second step needs from the first.</summary>
        internal sealed class StallLayout
        {
            public Transform Root;
            public Transform Stall;
            public Transform ShopkeeperSpot;
            public Transform ShopCamera;
            public Transform OrderSpot;
            public Transform CustomerEntry;
            public Transform CustomerExit;
            public Transform CoinSpot;
            public Transform ConeHolder;
            public Vector3 ZonePosition;
            public FlavourTub[] Tubs;
            public IceCreamCone ConeTemplate;
            public CashRegister Register;
            public Material Gold;
        }

        /// <summary>What the second step wires up.</summary>
        internal sealed class ShopInputs
        {
            public PlayerController Player;
            public ImaginationEffect Imagination;
        }

        // ----- Step 1: the stall -----------------------------------------------------------------

        /// <summary>
        /// The counter with the tubs, the cone holder, the register, the sign and an umbrella, plus the points the shop needs.
        /// The stall is first built at its preferred place, then moved (with all its points) to the nearest place that is clear of
        /// the playground equipment and the fence, found from the measured bounds. It reserves only the ground it really needs,
        /// so benches, bins and trees keep clear. Call before the NavMesh is baked.
        /// </summary>
        internal static StallLayout BuildStall(Transform environmentRoot, EnvironmentInfo info)
        {
            Vector3 preferred = new Vector3(StallX, 0f, info.MaxZ - StallFromFence);

            var layout = new StallLayout { Root = NewGroup("Shop", environmentRoot) };
            layout.Root.position = preferred;
            layout.Stall = NewGroup("Stall", layout.Root);
            // Facing south: the stall's forward is toward the customers, and its right is west (the shop camera's right).
            layout.Stall.SetPositionAndRotation(preferred, Quaternion.LookRotation(Vector3.back, Vector3.up));

            BuildCounter(layout, out float width, out float depth, out float top);
            BuildTubs(layout, top, depth);
            BuildConeHolder(layout, top, depth);
            BuildRegister(layout, width, depth, top);
            BuildSign(layout, depth);
            BuildPoints(layout, width, depth, top);

            // Everything is a child of the root, so moving the root moves the whole stall.
            float front = depth * 0.5f;
            var footprint = new Footprint(width, front, front + OrderFromCounterFront);
            Placement placement = ChoosePlacement(environmentRoot, info, preferred, footprint);
            Vector3 delta = placement.Centre - preferred;
            layout.Root.position += delta;
            layout.ZonePosition += delta;
            layout.CustomerEntry.position = layout.OrderSpot.position + Vector3.right * placement.East;
            layout.CustomerExit.position = layout.OrderSpot.position + Vector3.left * placement.West;

            foreach (Rect area in footprint.All(placement.Centre, placement.East, placement.West, TreeMargin))
                ReservedAreas.Add(area);

            Debug.Log($"CoolSchool ice cream shop: stall centre {placement.Centre.ToString("0.0")} facing south, counter {width:0.0} x {depth:0.0} m, "
                + $"top at {top:0.00} m, zone {layout.ZonePosition.ToString("0.0")}, shop camera {layout.ShopCamera.position.ToString("0.0")}, "
                + $"customers walk {placement.East:0.0} m from the east and {placement.West:0.0} m to the west. {placement.Report}");
            return layout;
        }

        // ---- Finding a clear place ----

        /// <summary>
        /// The ground the shop needs, in the stall frame (x to the right as the shop camera sees it, which is west; z toward the
        /// customers, which is south): the stall with the keeper behind it and the sign beside it, the camera behind that, the
        /// walk of the customers along the order spot, and the bay in front of the order spot that holds the big dragon and the zone.
        /// </summary>
        sealed class Footprint
        {
            readonly float width;
            readonly float front;
            readonly float orderZ;

            public Footprint(float width, float front, float orderZ)
            {
                this.width = width;
                this.front = front;
                this.orderZ = orderZ;
            }

            /// <summary>The east end of the stall (world +x), for keeping it west of the school path.</summary>
            public float EastReach => width * 0.5f + BesideCounterEast;

            /// <summary>The stall with the keeper and the sign beside it.</summary>
            public Rect Stall(Vector3 centre, float margin)
            {
                float half = width * 0.5f;
                return Local(centre, -(half + BesideCounterEast), half + SignReach, -(front + BehindCounter), front + 0.3f, margin);
            }

            /// <summary>The shop camera behind the stall.</summary>
            public Rect CameraStrip(Vector3 centre, float margin)
            {
                return Local(centre, CameraSideShift - 0.6f, CameraSideShift + 0.6f, -(front + CameraBehindCounterFront + 0.4f), -front, margin);
            }

            /// <summary>The walk of the customers to one side only: east (they come from there) or west (they leave that way).</summary>
            public Rect Lane(Vector3 centre, float length, bool eastSide, float margin)
            {
                return eastSide
                    ? Local(centre, OrderSpotX - length, OrderSpotX, orderZ - LaneHalfThickness, orderZ + LaneHalfThickness, margin)
                    : Local(centre, OrderSpotX, OrderSpotX + length, orderZ - LaneHalfThickness, orderZ + LaneHalfThickness, margin);
            }

            /// <summary>The dragon stands further back from the order spot, and the zone is in front of that.</summary>
            public Rect Bay(Vector3 centre, float margin)
            {
                float side = Mathf.Max(1.8f, ZoneRadius + 0.2f);
                return Local(centre, -side, side, orderZ - LaneHalfThickness, orderZ + ZoneBeyondOrderSpot + ZoneRadius + 0.2f, margin);
            }

            /// <summary>All the rectangles for a stall centre and the walk lengths, grown by margin.</summary>
            public List<Rect> All(Vector3 centre, float east, float west, float margin)
            {
                return new List<Rect>
                {
                    Stall(centre, margin),
                    CameraStrip(centre, margin),
                    Lane(centre, east, true, margin),
                    Lane(centre, west, false, margin),
                    Bay(centre, margin),
                };
            }

            // The stall faces south, so its x runs west and its z runs south: world = centre - local.
            static Rect Local(Vector3 centre, float xMin, float xMax, float zMin, float zMax, float margin)
            {
                return Rect.MinMaxRect(centre.x - xMax - margin, centre.z - zMax - margin, centre.x - xMin + margin, centre.z - zMin + margin);
            }
        }

        sealed class Placement
        {
            public Vector3 Centre;
            public float East = MinLaneLength;
            public float West = MinLaneLength;
            public string Report = string.Empty;
        }

        /// <summary>
        /// Looks for the place nearest to the preferred one where the footprint is inside the fence, west of the school path and
        /// clear of every piece of playground equipment (by its measured bounds), and where the customers can walk in and out at
        /// least MinLaneLength metres each way without going through anything. If there is no such place the preferred one is
        /// used, with a warning that names what is in the way.
        /// </summary>
        static Placement ChoosePlacement(Transform environmentRoot, EnvironmentInfo info, Vector3 preferred, Footprint footprint)
        {
            List<KeyValuePair<string, Rect>> obstacles = EquipmentObstacles(environmentRoot);
            Rect inside = Rect.MinMaxRect(info.MinX + FenceClearance, info.MinZ + FenceClearance, info.MaxX - FenceClearance, info.MaxZ - FenceClearance);
            float eastWall = -(SchoolBuilder.CorridorHalfWidth + SchoolPathClearance);

            Placement best = null;
            float bestCost = float.MaxValue;
            for (float fromFence = SearchMinFromFence; fromFence <= SearchMaxFromFence + 0.001f; fromFence += SearchStep)
            {
                for (float x = SearchMinX; x <= SearchMaxX + 0.001f; x += SearchStep)
                {
                    var centre = new Vector3(x, 0f, info.MaxZ - fromFence);
                    float cost = Mathf.Abs(x - preferred.x) + 1.5f * Mathf.Abs(fromFence - StallFromFence);
                    if (cost >= bestCost) continue;
                    if (!TryFit(centre, footprint, inside, eastWall, obstacles, out Placement fit)) continue;

                    best = fit;
                    bestCost = cost;
                }
            }

            if (best != null)
            {
                best.Report = NearestReport(footprint, best, obstacles);
                return best;
            }

            // Nothing fits: keep the preferred place and say what is in the way.
            var fallback = new Placement { Centre = preferred };
            foreach (Rect area in footprint.All(preferred, fallback.East, fallback.West, 0f))
            {
                foreach (KeyValuePair<string, Rect> obstacle in obstacles)
                    if (area.Overlaps(obstacle.Value))
                        Debug.LogWarning($"CoolSchool: the ice cream shop's ground overlaps {obstacle.Key}, and no clear place was found in the north-west. Move the stall or the equipment (see IceCreamShopBuilder).");
            }
            fallback.Report = "NO CLEAR PLACE FOUND.";
            return fallback;
        }

        static bool TryFit(Vector3 centre, Footprint footprint, Rect inside, float eastWall, List<KeyValuePair<string, Rect>> obstacles,
            out Placement placement)
        {
            placement = null;

            // The stall must be west of the school path, and the parts that can not be shortened must be inside the fence
            // and clear of the equipment.
            if (centre.x + footprint.EastReach > eastWall) return false;

            foreach (Rect area in new[] { footprint.Stall(centre, 0f), footprint.CameraStrip(centre, 0f), footprint.Bay(centre, 0f) })
                if (!IsClear(area, inside, obstacles)) return false;

            // The walk: as long as possible up to MaxLaneLength on each side, but at least MinLaneLength.
            float east = BestLaneLength(centre, footprint, true, inside, obstacles, EastLimitX - centre.x + OrderSpotX);
            float west = BestLaneLength(centre, footprint, false, inside, obstacles, float.MaxValue);
            if (east < 0f || west < 0f) return false;

            placement = new Placement { Centre = centre, East = east, West = west };
            return true;
        }

        /// <summary>The longest walk to one side (from MaxLaneLength down to MinLaneLength, in steps of half a metre) that is clear, or -1.</summary>
        static float BestLaneLength(Vector3 centre, Footprint footprint, bool eastSide, Rect inside, List<KeyValuePair<string, Rect>> obstacles, float limit)
        {
            for (float length = Mathf.Min(MaxLaneLength, limit); length >= MinLaneLength - 0.001f; length -= 0.5f)
                if (IsClear(footprint.Lane(centre, length, eastSide, 0f), inside, obstacles)) return length;
            return -1f;
        }

        static bool IsClear(Rect area, Rect inside, List<KeyValuePair<string, Rect>> obstacles)
        {
            if (area.xMin < inside.xMin || area.xMax > inside.xMax || area.yMin < inside.yMin || area.yMax > inside.yMax) return false;

            foreach (KeyValuePair<string, Rect> obstacle in obstacles)
                if (area.Overlaps(obstacle.Value)) return false;
            return true;
        }

        /// <summary>The ground footprint of every piece of playground equipment, from its measured bounds, with a margin.</summary>
        static List<KeyValuePair<string, Rect>> EquipmentObstacles(Transform environmentRoot)
        {
            var obstacles = new List<KeyValuePair<string, Rect>>();
            Transform equipment = environmentRoot.Find("Equipment");
            if (equipment == null)
            {
                Debug.LogWarning("CoolSchool: no 'Equipment' group found, so the ice cream shop can not avoid the playground equipment.");
                return obstacles;
            }

            foreach (Transform item in equipment)
            {
                if (!TryGetBounds(item.gameObject, out Bounds bounds)) continue;

                obstacles.Add(new KeyValuePair<string, Rect>(item.name, Rect.MinMaxRect(
                    bounds.min.x - EquipmentMargin, bounds.min.z - EquipmentMargin, bounds.max.x + EquipmentMargin, bounds.max.z + EquipmentMargin)));
            }
            return obstacles;
        }

        /// <summary>How close the chosen place is to the nearest piece of equipment, for the log.</summary>
        static string NearestReport(Footprint footprint, Placement placement, List<KeyValuePair<string, Rect>> obstacles)
        {
            string nearest = "none";
            float nearestGap = float.MaxValue;
            foreach (Rect area in footprint.All(placement.Centre, placement.East, placement.West, 0f))
            {
                foreach (KeyValuePair<string, Rect> obstacle in obstacles)
                {
                    Rect o = obstacle.Value;
                    float gapX = Mathf.Max(0f, Mathf.Max(o.xMin - area.xMax, area.xMin - o.xMax));
                    float gapZ = Mathf.Max(0f, Mathf.Max(o.yMin - area.yMax, area.yMin - o.yMax));
                    float gap = Mathf.Max(gapX, gapZ);
                    if (gap >= nearestGap) continue;

                    nearestGap = gap;
                    nearest = obstacle.Key;
                }
            }
            return $"Nearest equipment: {nearest}, {nearestGap:0.0} m clear (its bounds include a {EquipmentMargin:0.0} m margin).";
        }


        // ---- The counter ----

        /// <summary>
        /// The Synty counter, scaled to a metre high and repeated until it is wide enough, or a plain box when the prefab is
        /// missing. Reports its total width and depth and the height of its top.
        /// </summary>
        static void BuildCounter(StallLayout layout, out float width, out float depth, out float top)
        {
            Transform stall = layout.Stall;
            GameObject prefab = LoadPrefab(CounterPrefab);
            if (prefab == null) prefab = LoadPrefab(FreezerPrefab);

            if (prefab != null)
            {
                GameObject first = Spawn(prefab, stall);
                first.transform.rotation = stall.rotation;
                if (TryGetBounds(first, out Bounds bounds))
                {
                    // The long side of the counter runs across the stall.
                    if (bounds.size.z > bounds.size.x * 1.2f)
                    {
                        first.transform.rotation = stall.rotation * Quaternion.Euler(0f, 90f, 0f);
                        TryGetBounds(first, out bounds);
                    }

                    float scale = Mathf.Clamp(CounterHeight / Mathf.Max(0.1f, bounds.size.y), 0.5f, 2f);
                    first.transform.localScale *= scale;
                    TryGetBounds(first, out bounds);

                    float each = Mathf.Max(0.5f, bounds.size.x);
                    int count = Mathf.Clamp(Mathf.CeilToInt(MinCounterWidth / each), 1, 4);
                    width = count * each;
                    depth = Mathf.Clamp(bounds.size.z, 0.7f, 1.6f);

                    for (int i = 0; i < count; i++)
                    {
                        GameObject piece = i == 0 ? first : Spawn(prefab, stall);
                        if (i > 0)
                        {
                            piece.transform.rotation = first.transform.rotation;
                            piece.transform.localScale = first.transform.localScale;
                        }

                        Vector3 spot = stall.TransformPoint(new Vector3(-width * 0.5f + (i + 0.5f) * each, 0f, 0f));
                        AlignToBounds(piece, spot.x, spot.z, 0f, false);
                        FinishEnvironmentObject(piece);
                    }

                    TryGetBounds(first, out bounds);
                    top = bounds.max.y;
                    first.name = "Counter";
                    return;
                }
                Object.DestroyImmediate(first);
            }

            Debug.LogWarning("CoolSchool: no shop counter prefab found; using a plain box.");
            width = FallbackCounterWidth;
            depth = FallbackCounterDepth;
            top = CounterHeight;
            Material wood = ShopAssets.Lit("ShopCounterWood", new Color(0.85f, 0.65f, 0.45f), 0.2f);
            Primitive(PrimitiveType.Cube, "Counter", stall, new Vector3(0f, CounterHeight * 0.5f, 0f),
                new Vector3(width, CounterHeight, depth), wood, true);
        }

        // ---- Tubs ----

        /// <summary>The z of the row of tubs: along the camera-side edge of the counter.</summary>
        static float TubRowZ(float depth)
        {
            return -depth * 0.5f + TubRadius + TubEdgeInset;
        }

        /// <summary>The x of a tub: two on each side of the cone holder, which stands at x = 0.</summary>
        static float TubX(int index, int count)
        {
            int half = count / 2;
            return index < half
                ? -(TubInnerX + (half - 1 - index) * TubSpacing)
                : TubInnerX + (index - half) * TubSpacing;
        }

        /// <summary>
        /// Four tubs sunk into the counter: a rim on the counter top and a low mound of ice cream (TubMoundHeight high), so they
        /// stay big, clear tap targets without standing up in front of the cone, the customer or the coins.
        /// A tub's origin is on the counter top.
        /// </summary>
        static void BuildTubs(StallLayout layout, float top, float depth)
        {
            Material halo = ShopAssets.Glow("ShopTubGlow", new Color(1f, 0.95f, 0.5f), 0.55f);
            var tubs = new FlavourTub[FlavourInfo.Count];
            float z = TubRowZ(depth);

            for (int i = 0; i < tubs.Length; i++)
            {
                var flavour = (Flavour)i;
                Color colour = FlavourInfo.ColourOf(flavour);
                float x = TubX(i, tubs.Length);

                Material bodyMaterial = ShopAssets.Lit("ShopTub" + flavour, Color.Lerp(colour, Color.black, 0.18f), 0.4f);
                Material scoopMaterial = ShopAssets.Lit("ShopScoop" + flavour, colour, 0.55f);

                var tubObject = new GameObject("Tub_" + flavour);
                tubObject.transform.SetParent(layout.Stall, false);
                tubObject.transform.localPosition = new Vector3(x, top, z);

                // The cylinder primitive is 2 m tall, so half the wanted height is its y scale.
                Primitive(PrimitiveType.Cylinder, "Rim", tubObject.transform, new Vector3(0f, TubRimHeight * 0.5f, 0f),
                    new Vector3(TubRadius * 2f, TubRimHeight * 0.5f, TubRadius * 2f), bodyMaterial, false);
                // A dome: the upper half of a squashed sphere shows above the counter, the rest is inside it.
                Transform mound = Primitive(PrimitiveType.Sphere, "Mound", tubObject.transform, new Vector3(0f, TubRimHeight * 0.4f, 0f),
                    new Vector3(TubRadius * 1.8f, (TubMoundHeight - TubRimHeight * 0.4f) * 2f, TubRadius * 1.8f), scoopMaterial, false);
                Transform scoopPoint = NewPoint("ScoopPoint", tubObject.transform, Vector3.zero);
                scoopPoint.localPosition = new Vector3(0f, TubMoundHeight + 0.04f, 0f);

                Transform glow = Primitive(PrimitiveType.Cylinder, "Glow", tubObject.transform, new Vector3(0f, 0.012f, 0f),
                    new Vector3(TubRadius * 3.2f, 0.005f, TubRadius * 3.2f), halo, false);
                glow.gameObject.SetActive(false);

                // A roomy tap box, so small fingers do not miss: as wide as the tub's place in the row.
                var box = tubObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.12f, 0f);
                box.size = new Vector3(TubSpacing, 0.4f, TubRadius * 2.3f);

                var tub = tubObject.AddComponent<FlavourTub>();
                SetEnum(tub, "flavour", i);
                SetObject(tub, "scoopPoint", scoopPoint);
                SetObject(tub, "mound", mound);
                SetObject(tub, "scoopMaterial", scoopMaterial);
                SetObject(tub, "glow", glow.gameObject);
                tubs[i] = tub;
            }
            layout.Tubs = tubs;
        }

        // ---- Cone holder and the empty cone ----

        static void BuildConeHolder(StallLayout layout, float top, float depth)
        {
            float z = TubRowZ(depth) + ConeBehindTubs;
            Material cup = ShopAssets.Lit("ShopHolder", new Color(0.95f, 0.95f, 0.9f), 0.5f);
            Primitive(PrimitiveType.Cylinder, "ConeCup", layout.Stall, new Vector3(0f, top + HolderHeight * 0.5f, z),
                new Vector3(0.2f, HolderHeight * 0.5f, 0.2f), cup, false);

            // The cone's origin is its tip, so the holder is where the tip sits.
            layout.ConeHolder = NewPoint("ConeHolder", layout.Stall, Vector3.zero);
            layout.ConeHolder.localPosition = new Vector3(0f, top + HolderHeight, z);

            layout.ConeTemplate = BuildConeTemplate(layout);
        }

        /// <summary>An empty cone with a see-through drop hint, switched off. The shop makes a copy of it for each customer.</summary>
        static IceCreamCone BuildConeTemplate(StallLayout layout)
        {
            Mesh mesh = ShopAssets.CreateConeMesh();
            Texture2D waffle = ShopAssets.CreateWaffleTexture();
            Material coneMaterial = ShopAssets.Lit("ShopCone", new Color(1f, 1f, 1f), 0.2f);
            if (coneMaterial != null && waffle != null)
            {
                coneMaterial.SetTexture("_BaseMap", waffle);
                coneMaterial.SetTextureScale("_BaseMap", new Vector2(4f, 2f));
                EditorUtility.SetDirty(coneMaterial);
            }

            var template = new GameObject("IceCreamConeTemplate");
            template.transform.SetParent(layout.Root, false);
            template.transform.SetPositionAndRotation(layout.ConeHolder.position, layout.Stall.rotation);

            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(template.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = body.GetComponent<MeshRenderer>();
            if (coneMaterial != null) renderer.sharedMaterial = coneMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            Material hintMaterial = ShopAssets.Glow("ShopDropHint", new Color(1f, 1f, 0.8f), 0.45f);
            Transform hint = Primitive(PrimitiveType.Sphere, "DropHint", template.transform, Vector3.zero,
                Vector3.one * (ScoopBall.Radius * 2f), hintMaterial, false);
            hint.gameObject.SetActive(false);

            var cone = template.AddComponent<IceCreamCone>();
            SetFloat(cone, "rimHeight", ShopAssets.ConeHeight);
            SetObject(cone, "dropHint", hint.gameObject);

            template.SetActive(false);
            return cone;
        }

        // ---- Register ----

        static void BuildRegister(StallLayout layout, float width, float depth, float top)
        {
            Transform stall = layout.Stall;
            Vector3 spot = stall.TransformPoint(new Vector3(Mathf.Min(RegisterX, width * 0.5f - 0.3f), 0f, TubRowZ(depth) + 0.2f));

            // Not static and no extra colliders: the register hops.
            GameObject prop = PlaceProp(RegisterPrefab, stall, stall.rotation, RegisterWidth, spot, top, false);
            if (prop == null)
            {
                Debug.LogWarning("CoolSchool: no cash register prefab found; using a plain box.");
                Material grey = ShopAssets.Lit("ShopRegister", new Color(0.55f, 0.6f, 0.65f), 0.4f);
                Transform box = Primitive(PrimitiveType.Cube, "CashRegisterBox", stall, Vector3.zero, new Vector3(0.45f, 0.3f, 0.35f), grey, false);
                box.position = new Vector3(spot.x, top + 0.15f, spot.z);
                prop = box.gameObject;
            }
            prop.name = "RegisterProp";

            Vector3 target = spot + Vector3.up * (top + 0.25f);
            if (TryGetBounds(prop, out Bounds bounds)) target = new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z);

            var registerObject = new GameObject("CashRegister");
            registerObject.transform.SetParent(stall, true);
            registerObject.transform.position = spot;
            Transform coinTarget = NewPoint("CoinTarget", registerObject.transform, Vector3.zero);
            coinTarget.position = target;

            var register = registerObject.AddComponent<CashRegister>();
            SetObject(register, "register", prop.transform);
            SetObject(register, "coinTarget", coinTarget);
            layout.Register = register;
            layout.Gold = ShopAssets.Lit("ShopGold", new Color(1f, 0.8f, 0.15f), 0.85f, 0.6f, 0.45f);
        }

        // ---- Sign ----

        static void BuildSign(StallLayout layout, float depth)
        {
            Transform stall = layout.Stall;

            // The sign stands beyond the west end of the counter, facing south: read from the playground, never in the shop
            // camera's picture.
            float signZ = -(depth * 0.5f + 0.25f);
            Vector3 signSpot = stall.TransformPoint(new Vector3(SignCentreX, 0f, signZ));
            GameObject sign = PlaceProp(SignPrefab, stall, stall.rotation, 0f, signSpot, SignFootHeight, false);
            if (sign != null)
            {
                sign.name = "IceCreamSign";
                if (TryGetBounds(sign, out Bounds bounds) && bounds.size.y > 0.01f)
                    sign.transform.localScale *= Mathf.Clamp(SignHeight / bounds.size.y, 0.1f, 4f);
                AlignToBounds(sign, signSpot.x, signSpot.z, SignFootHeight, false);
                FinishEnvironmentObject(sign);
            }
            else
            {
                Debug.LogWarning("CoolSchool: no ice cream sign prefab found; the stall has no sign.");
            }

            // One post holds it up.
            Material wood = ShopAssets.Lit("ShopPost", new Color(0.55f, 0.38f, 0.22f), 0.2f);
            Transform post = Primitive(PrimitiveType.Cylinder, "Post", stall, new Vector3(SignCentreX, (SignFootHeight + 0.3f) * 0.5f, signZ),
                new Vector3(PostRadius * 2f, (SignFootHeight + 0.3f) * 0.5f, PostRadius * 2f), wood, true);
            FinishEnvironmentObject(post.gameObject);
        }

        // ---- Points ----

        static void BuildPoints(StallLayout layout, float width, float depth, float top)
        {
            Transform stall = layout.Stall;
            Transform points = NewGroup("Points", layout.Root);
            float front = depth * 0.5f;

            // Behind the counter at the left end (east), facing south.
            layout.ShopkeeperSpot = NewPoint("ShopkeeperSpot", points, stall.TransformPoint(new Vector3(-(width * 0.5f - KeeperFromEnd), 0f, -(front + 0.3f))));
            layout.ShopkeeperSpot.rotation = stall.rotation;

            // North of and above the counter, looking south and down over it.
            layout.ShopCamera = NewPoint("ShopCamera", points, stall.TransformPoint(new Vector3(CameraSideShift, CameraHeight, -(front + CameraBehindCounterFront))));
            layout.ShopCamera.rotation = stall.rotation * Quaternion.Euler(CameraPitch, 0f, 0f);

            // In front of the counter, facing north (toward the counter and the camera).
            float orderZ = front + OrderFromCounterFront;
            layout.OrderSpot = NewPoint("OrderSpot", points, stall.TransformPoint(new Vector3(OrderSpotX, 0f, orderZ)));
            layout.OrderSpot.rotation = stall.rotation * Quaternion.Euler(0f, 180f, 0f);

            // Customers come from the left of the shop camera's picture (east) and leave to the right (west), far out of view.
            layout.CustomerEntry = NewPoint("CustomerEntry", points, stall.TransformPoint(new Vector3(OrderSpotX - MinLaneLength, 0f, orderZ)));
            layout.CustomerExit = NewPoint("CustomerExit", points, stall.TransformPoint(new Vector3(OrderSpotX + MinLaneLength, 0f, orderZ)));

            // On the counter top, on the camera's side between the tubs and the register: the middle of the row of coins.
            layout.CoinSpot = NewPoint("CoinSpot", points, stall.TransformPoint(new Vector3(CoinSpotX, top, TubRowZ(depth))));
            layout.CoinSpot.rotation = stall.rotation;

            layout.ZonePosition = stall.TransformPoint(new Vector3(0f, 0f, orderZ + ZoneBeyondOrderSpot));
        }

        // ----- Step 2: everything alive ----------------------------------------------------------

        /// <summary>
        /// Creates the customers, the order bubble and the IceCreamShop that ties it all together, then the glowing zone
        /// and the invitation. Call after the NavMesh is baked, after the player and the lesson UI exist.
        /// </summary>
        internal static IceCreamShop Build(StallLayout layout, ShopInputs inputs)
        {
            if (layout == null || inputs == null || inputs.Player == null)
            {
                Debug.LogWarning("CoolSchool: the ice cream shop was not built (no stall layout or no player).");
                return null;
            }

            var shopObject = new GameObject("IceCreamShop");
            shopObject.transform.SetParent(layout.Root, false);
            var shop = shopObject.AddComponent<IceCreamShop>();
            var rig = shopObject.AddComponent<LessonCameraRig>();

            Transform customersGroup = NewGroup("Customers", layout.Root);
            ShopCustomer[] customers = ShopCustomerBuilder.BuildCustomers(customersGroup);
            OrderBubble bubble = BuildBubble();

            SetObject(shop, "player", inputs.Player);
            SetObject(shop, "cameraRig", rig);
            SetObject(shop, "lessonUI", Object.FindFirstObjectByType<LessonUI>());
            SetObject(shop, "imagination", CreateImagination(inputs.Imagination));
            SetObject(shop, "shopkeeperSpot", layout.ShopkeeperSpot);
            SetObject(shop, "shopCamera", layout.ShopCamera);
            SetObject(shop, "orderSpot", layout.OrderSpot);
            SetObject(shop, "customerEntry", layout.CustomerEntry);
            SetObject(shop, "customerExit", layout.CustomerExit);
            SetObject(shop, "coinSpot", layout.CoinSpot);
            SetObjectArray(shop, "tubs", layout.Tubs);
            SetObject(shop, "coneTemplate", layout.ConeTemplate);
            SetObject(shop, "coneHolder", layout.ConeHolder);
            SetObject(shop, "register", layout.Register);
            SetObject(shop, "bubble", bubble);
            SetObject(shop, "goldMaterial", layout.Gold);
            SetObjectArray(shop, "customers", customers);

            Material ring = ShopAssets.Glow("ShopZoneRing", new Color(1f, 0.55f, 0.75f), 0.4f);
            ActivityZone zone = KnightsArenaBuilder.CreateActivityZone("ShopZone", layout.Root, layout.ZonePosition, ZoneRadius, shop, ring, ShopBeacon);
            AddToInviter(zone);

            Debug.Log($"CoolSchool ice cream shop: {customers.Length} customers, zone at {layout.ZonePosition.ToString("0.0")}.");
            return shop;
        }

        /// <summary>The shop's own imagination glow: the shared gold overlay, without the castle's flags and torches.</summary>
        static ImaginationEffect CreateImagination(ImaginationEffect castleEffect)
        {
            var effectObject = new GameObject("ShopImagination");
            var effect = effectObject.AddComponent<ImaginationEffect>();

            if (castleEffect != null)
            {
                var source = new SerializedObject(castleEffect);
                SerializedProperty overlay = source.FindProperty("overlay");
                if (overlay != null) SetObject(effect, "overlay", overlay.objectReferenceValue);
            }
            SetFloat(effect, "flashSeconds", 1f);
            SetFloat(effect, "maxOverlayAlpha", 0.35f);
            return effect;
        }

        /// <summary>The order bubble: a full-screen, touch-transparent holder on the touch canvas. The bubble itself is made at runtime.</summary>
        static OrderBubble BuildBubble()
        {
            GameObject canvas = GameObject.Find("TouchUI");
            if (canvas == null)
            {
                Debug.LogWarning("CoolSchool: no 'TouchUI' canvas found, so the ice cream shop has no order bubble.");
                return null;
            }

            var holder = new GameObject("ShopOrderBubble", typeof(RectTransform));
            holder.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)holder.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Below the present popup, which must cover everything.
            Transform popup = canvas.transform.Find("PresentPopup");
            if (popup != null) rect.SetSiblingIndex(popup.GetSiblingIndex());

            var bubble = holder.AddComponent<OrderBubble>();
            SetObject(bubble, "bubbleSprite", ShopAssets.FindShape("Background"));
            SetObject(bubble, "circleSprite", ShopAssets.FindShape("Circle"));
            SetObject(bubble, "outlineSprite", ShopAssets.FindShape("Circle-Outline"));
            SetObject(bubble, "triangleSprite", ShopAssets.CreateTriangleSprite());
            return bubble;
        }

        /// <summary>Adds the shop zone at the END of the inviter's turns, with its own invite line. Skips quietly if there is no inviter.</summary>
        static void AddToInviter(ActivityZone zone)
        {
            var inviter = Object.FindFirstObjectByType<ActivityInviter>();
            if (inviter == null)
            {
                Debug.LogWarning("CoolSchool: no ActivityInviter in the scene, so the ice cream shop will not invite the kid.");
                return;
            }

            var serialized = new SerializedObject(inviter);
            SerializedProperty zones = serialized.FindProperty("zones");
            SerializedProperty groups = serialized.FindProperty("inviteGroups");
            if (zones == null || groups == null) return;

            // The groups line up with the zones; pad them first in case an earlier zone was missing.
            while (groups.arraySize < zones.arraySize)
            {
                groups.InsertArrayElementAtIndex(groups.arraySize);
                groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = string.Empty;
            }

            zones.InsertArrayElementAtIndex(zones.arraySize);
            zones.GetArrayElementAtIndex(zones.arraySize - 1).objectReferenceValue = zone;
            groups.InsertArrayElementAtIndex(groups.arraySize);
            groups.GetArrayElementAtIndex(groups.arraySize - 1).stringValue = InviteGroup;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Helpers ---------------------------------------------------------------------------

        /// <summary>
        /// A prefab scaled so its widest side is targetWidth (0 keeps its size), turned to the rotation and stood on the
        /// ground at footY, with the middle of its footprint at the world point. Null if the prefab is missing.
        /// </summary>
        static GameObject PlaceProp(PrefabRef reference, Transform parent, Quaternion rotation, float targetWidth, Vector3 point, float footY, bool finish)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject go = Spawn(prefab, parent);
            go.transform.rotation = rotation;
            if (targetWidth > 0f && TryGetBounds(go, out Bounds bounds))
            {
                float widest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (widest > 0.01f) go.transform.localScale *= Mathf.Clamp(targetWidth / widest, 0.2f, 4f);
            }

            AlignToBounds(go, point.x, point.z, footY, false);
            if (finish) FinishEnvironmentObject(go);
            return go;
        }

        /// <summary>A primitive without its collider (unless asked to keep it), placed in the parent's frame.</summary>
        static Transform Primitive(PrimitiveType type, string objectName, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        static Transform NewPoint(string pointName, Transform parent, Vector3 position)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }
    }
}
