using System.Collections.Generic;
using System.Linq;
using CoolSchool.ToyRoom;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.ModularBuilding;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>What the second step of the Toy Room build needs from the first: the anchors, the shelf and the zone's place.</summary>
    internal sealed class ToyRoomLayout
    {
        public Transform Root;
        public bool Built;

        /// <summary>The floor tiles' bounds in world space (size 0 when there is no floor). Grass must not lie under it.</summary>
        public Bounds FloorArea;

        /// <summary>The ground the building and its forecourt use (x, z in world space).</summary>
        public Rect Footprint;

        /// <summary>The room's inside in world space (x/z tested by the shelf).</summary>
        public Bounds RoomArea;

        /// <summary>The middle of the doorway on the floor line, in world space. The inviter's trail leads here.</summary>
        public Transform DoorSpot;

        public ToyShelf Shelf;
        public MedalBoard Board;

        // The dress-up mirror's spots.
        public Transform ZoneSpot, StandSpot, BuddySpot, CameraSpot, ExitSpot;
    }

    /// <summary>
    /// Builds the Toy Room ("Speelgoedkamer"): a small building in the north-east corner of the playground, door facing south
    /// (toward the follow camera), where every reward stands on a shelf (locked ones as black silhouettes), the medals hang on a
    /// board and a dress-up mirror changes what he wears. Two steps, like the ice cream shop and the aliens:
    ///   BuildRoom  (static geometry, the shelf and the anchors: before the grass is laid and the NavMesh is baked)
    ///   Build      (the wardrobe activity, its zone and its picture menu, and the inviter's spot: after the rewards exist,
    ///               because it needs the catalog, the player, the touch canvas and the buddy)
    /// The building is made of the same Synty Generic modular walls as the school (ModularBuilding). It is built around the
    /// origin in its own frame (door on -Z, the mirror on the north wall) and moved to its place at the end.
    /// Sub-files: Shell (walls, roof, lights, the interior reveal), Displays (stands, shelf slots, medal board, mirror, decoration)
    /// and Wardrobe (the zone, the spots and the picture menu).
    /// </summary>
    internal static partial class ToyRoomBuilder
    {
        const string RootFolder = "Assets/CoolSchool";

        // ---- Where the building stands: the north-east quadrant, close to the north fence, clear of everything else ----
        // The preferred place; the real one is searched around it (ChooseSite), from the measured equipment bounds.
        const float PreferredX = 13.5f;
        const float PreferredFromFence = 5.0f;
        const float SearchMinX = 8f;
        const float SearchMinFromFence = 3.2f;
        const float SearchMaxFromFence = 10.5f;
        const float SearchStep = 0.25f;
        const float EquipmentMargin = 0.5f;
        /// <summary>The ground stays this far inside the fence, and east of the school path (its half width plus this).</summary>
        const float FenceClearance = 0.8f;
        const float SchoolPathClearance = 0.5f;
        /// <summary>Free ground in front of the door, so he walks straight in and the camera sees the door.</summary>
        const float Forecourt = 2f;
        /// <summary>Benches, bins and trees keep out of the footprint plus this margin.</summary>
        const float TreeMargin = 1.2f;
        const float RoofOverhang = 0.4f;

        /// <summary>The wall pieces along x and along z, biggest first: the footprint shrinks one piece at a time when nothing fits.</summary>
        static readonly (int x, int z)[] SizesToTry = { (3, 2), (2, 2), (2, 1) };

        /// <summary>The footprint in the building's own frame (centred on the origin, door in the -Z wall, the walls' centre lines).</summary>
        sealed class Plan
        {
            public int PiecesX, PiecesZ;
            public float Length, Height, MaxThickness;
            public float Width, Depth;
            public float WestX, EastX, SouthZ, NorthZ;
            /// <summary>The inside faces of the walls (half the inner width and depth, from the middle).</summary>
            public float HalfX, HalfZ;
        }

        /// <summary>The four walls, named by where they stand: South is the door wall (-Z), the one the follow camera sees.</summary>
        enum Side
        {
            South,
            North,
            West,
            East,
        }

        sealed class Site
        {
            public Vector3 Centre;
            public string Report = string.Empty;
        }

        // ----- Step 1: the building --------------------------------------------------------------

        /// <summary>
        /// The walls, floor, roof, lights, stands, shelf slots, medal board, mirror and decoration, plus the points the wardrobe needs.
        /// Reserves its ground so benches and trees keep clear, and returns the floor's bounds so the grass can leave it out.
        /// Call before the grass is laid and the NavMesh is baked (the playground builder calls it right after the school).
        /// </summary>
        internal static ToyRoomLayout BuildRoom(Transform environmentRoot, EnvironmentInfo info)
        {
            var layout = new ToyRoomLayout();
            Transform root = NewGroup("ToyRoom", environmentRoot);
            layout.Root = root;

            Kit kit = MeasureKit(root, "toy room");
            if (kit == null)
            {
                Debug.LogError("CoolSchool: the Synty Generic wall or floor pieces were not found; no Toy Room was built.");
                return layout;
            }

            Plan plan = null;
            Site site = null;
            List<KeyValuePair<string, Rect>> obstacles = EquipmentObstacles(environmentRoot);
            foreach ((int x, int z) size in SizesToTry)
            {
                plan = MakePlan(kit, size.x, size.z);
                site = ChooseSite(info, plan, obstacles);
                if (site != null) break;
            }

            if (site == null)
            {
                // Nothing fits: keep the preferred place at full size and say what is in the way.
                plan = MakePlan(kit, SizesToTry[0].x, SizesToTry[0].z);
                site = new Site { Centre = new Vector3(PreferredX, 0f, info.MaxZ - PreferredFromFence), Report = "NO CLEAR PLACE FOUND." };
                WarnAboutOverlaps(plan, site.Centre, obstacles);
            }

            Transform shell = NewGroup("Shell", root);
            Transform props = NewGroup("Props", root);

            var wallsBySide = new Dictionary<Side, List<GameObject>>();
            BuildWalls(NewGroup("Walls", shell), kit, plan, wallsBySide);
            BuildCornerPillars(NewGroup("Pillars", shell), kit, plan);
            Transform floorGroup = NewGroup("Floor", shell);
            LayTiles(floorGroup, kit.Floor, plan.Width, plan.Depth, 0f, FloorY, Quaternion.identity, true);
            List<Renderer> roof = BuildRoof(NewGroup("Roof", shell), kit, plan);

            BuildDisplays(props, root, plan, layout);
            BuildLights(NewGroup("Lights", root), plan, layout);
            AddInteriorReveal(root, plan, roof, wallsBySide[Side.South]);

            // Turned nowhere (the door already faces south); only moved into place.
            root.position = site.Centre;
            Physics.SyncTransforms();

            layout.Built = true;
            if (TryGetBounds(floorGroup.gameObject, out Bounds floorBounds)) layout.FloorArea = floorBounds;
            layout.Footprint = FootprintRect(plan, site.Centre, 0f);
            layout.RoomArea = new Bounds(site.Centre + new Vector3(0f, plan.Height * 0.5f, 0f),
                new Vector3(plan.HalfX * 2f, plan.Height, plan.HalfZ * 2f));
            layout.DoorSpot = NewAnchor("DoorSpot", root, site.Centre + new Vector3(0f, FloorY, plan.SouthZ - 0.4f), Quaternion.identity);

            FinishDisplays(layout, site.Centre);
            BuildBeacon(root, plan, layout);

            Rect grown = FootprintRect(plan, site.Centre, TreeMargin);
            ReservedAreas.Add(grown);

            Debug.Log($"ToyRoom: centre {site.Centre.ToString("0.0")}, {plan.PiecesX} x {plan.PiecesZ} wall pieces "
                + $"({plan.Width:0.0} x {plan.Depth:0.0} m, inside {plan.HalfX * 2f:0.0} x {plan.HalfZ * 2f:0.0} m), door facing south "
                + $"at {layout.DoorSpot.position.ToString("0.0")}, mirror zone {layout.ZoneSpot.position.ToString("0.0")}. {site.Report}");
            return layout;
        }

        // ---- Plan ----

        static Plan MakePlan(Kit kit, int piecesX, int piecesZ)
        {
            var plan = new Plan
            {
                PiecesX = piecesX,
                PiecesZ = piecesZ,
                Length = kit.Wall.Length,
                Height = kit.Wall.Size.y,
                MaxThickness = new[] { kit.Wall, kit.Window, kit.Door }.Where(p => p != null).Max(p => p.Thickness),
            };

            plan.Width = piecesX * plan.Length;
            plan.Depth = piecesZ * plan.Length;
            plan.WestX = -plan.Width * 0.5f;
            plan.EastX = plan.Width * 0.5f;
            plan.SouthZ = -plan.Depth * 0.5f;
            plan.NorthZ = plan.Depth * 0.5f;

            float inset = plan.MaxThickness * 0.5f + 0.02f;
            plan.HalfX = plan.EastX - inset;
            plan.HalfZ = plan.NorthZ - inset;
            return plan;
        }

        // ---- Finding a clear place ----

        /// <summary>The ground the building uses (walls plus the roof overhang), with the forecourt in front when margin is the tree margin.</summary>
        static Rect FootprintRect(Plan plan, Vector3 centre, float margin)
        {
            float halfX = plan.Width * 0.5f + RoofOverhang + 0.15f;
            float halfZ = plan.Depth * 0.5f + RoofOverhang + 0.15f;
            return Rect.MinMaxRect(centre.x - halfX - margin, centre.z - halfZ - Forecourt - margin,
                centre.x + halfX + margin, centre.z + halfZ + margin);
        }

        /// <summary>
        /// Looks for the place nearest to the preferred one where the footprint and the forecourt are inside the fence, east of the
        /// school path, and clear of every piece of playground equipment (by its measured bounds) and of ground that is reserved
        /// already. Null if there is none.
        /// </summary>
        static Site ChooseSite(EnvironmentInfo info, Plan plan, List<KeyValuePair<string, Rect>> obstacles)
        {
            float westLimit = SchoolBuilder.CorridorHalfWidth + SchoolPathClearance;
            float eastLimit = info.FenceHalf - FenceClearance;
            float northLimit = info.MaxZ - FenceClearance;

            Site best = null;
            float bestCost = float.MaxValue;
            for (float fromFence = SearchMinFromFence; fromFence <= SearchMaxFromFence + 0.001f; fromFence += SearchStep)
            {
                for (float x = SearchMinX; x <= info.FenceHalf + 0.001f; x += SearchStep)
                {
                    var centre = new Vector3(x, 0f, info.MaxZ - fromFence);
                    float cost = Mathf.Abs(x - PreferredX) + 1.5f * Mathf.Abs(fromFence - PreferredFromFence);
                    if (cost >= bestCost) continue;

                    Rect area = FootprintRect(plan, centre, 0f);
                    if (area.xMin < westLimit || area.xMax > eastLimit || area.yMax > northLimit) continue;
                    if (obstacles.Any(o => area.Overlaps(o.Value))) continue;
                    if (ReservedAreas.Any(r => area.Overlaps(r))) continue;

                    best = new Site { Centre = centre };
                    bestCost = cost;
                }
            }

            if (best != null) best.Report = NearestReport(FootprintRect(plan, best.Centre, 0f), obstacles);
            return best;
        }

        static void WarnAboutOverlaps(Plan plan, Vector3 centre, List<KeyValuePair<string, Rect>> obstacles)
        {
            Rect area = FootprintRect(plan, centre, 0f);
            foreach (KeyValuePair<string, Rect> obstacle in obstacles)
                if (area.Overlaps(obstacle.Value))
                    Debug.LogWarning($"CoolSchool: the Toy Room's ground overlaps {obstacle.Key}, and no clear place was found in the north-east. Move the room or the equipment (see ToyRoomBuilder).");
        }

        /// <summary>The ground footprint of every piece of playground equipment, from its measured bounds, with a margin.</summary>
        static List<KeyValuePair<string, Rect>> EquipmentObstacles(Transform environmentRoot)
        {
            var obstacles = new List<KeyValuePair<string, Rect>>();
            Transform equipment = environmentRoot.Find("Equipment");
            if (equipment == null)
            {
                Debug.LogWarning("CoolSchool: no 'Equipment' group found, so the Toy Room can not avoid the playground equipment.");
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
        static string NearestReport(Rect area, List<KeyValuePair<string, Rect>> obstacles)
        {
            string nearest = "none";
            float nearestGap = float.MaxValue;
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
            return $"Nearest equipment: {nearest}, {nearestGap:0.0} m clear (its bounds include a {EquipmentMargin:0.0} m margin).";
        }

        // ----- Small helpers shared by the sub-files -------------------------------------------------

        static void SetText(Object target, string field, string value)
        {
            if (target == null) return;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"CoolSchool: {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
