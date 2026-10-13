using System.Collections.Generic;
using System.Linq;
using CoolSchool.School;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.ModularBuilding;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The school and the spots the lesson needs. Anchors are empty transforms in the scene.</summary>
    public sealed class SchoolResult
    {
        public Transform root, tableSurface, lessonCamera, blackboardFace, teacherSpot, playerSeat, classroomZone;
        public TeacherCharacter teacher;

        /// <summary>The building's footprint in world space (walls plus the roof overhang), so the ground can be extended under it.</summary>
        public Bounds footprint;

        /// <summary>The floor tiles' bounds in world space (size 0 when there is no floor). Grass must not lie under it: its tufts poke through.</summary>
        public Bounds floorArea;

        /// <summary>The middle of the doorway on the floor line, in world space.</summary>
        public Vector3 doorPoint;

        /// <summary>The way the door faces (flat, unit length): from the school toward the playground.</summary>
        public Vector3 doorFacing;

        /// <summary>The yaw (degrees) the school root was turned by; 0 means the door faces +Z.</summary>
        public float yaw;
    }

    /// <summary>
    /// Builds the school beyond the playground's fence: one classroom from Synty Generic modular pieces (walls, windows,
    /// a wide doorway facing the playground, floor, flat roof), kid-sized furniture, the teacher, warm lights and the
    /// anchors the lesson uses. Everything is placed by renderer bounds, because the pack's pivots are not trustworthy.
    /// Orientation assumptions (measured from the meshes where possible):
    /// a wall segment runs along its local X, its thin axis is Z, and the plaster side (the inside) faces local +Z.
    /// Props face local +Z.
    /// The school is first built around the origin in its own frame: the door is in the +Z wall and the blackboard on the
    /// -Z wall. Then the root is turned (yaw) so the door faces the playground, and moved into place. That keeps every
    /// anchor, the lesson camera and the interior reveal box consistent whatever the facing is.
    /// With the playground south of the school (the door facing -Z) the yaw is 180: the door is in the south wall,
    /// facing the fixed-angle camera, and the blackboard is on the north wall, so the lesson camera looks north like the
    /// game camera does. Names in the code (Board and Door walls, "west" and "east") are in the school's own frame, before the turn.
    /// </summary>
    public static class SchoolBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string TeacherControllerPath = RootFolder + "/Animation/Teacher.controller";
        const string GenericPack = "PolygonGeneric";
        const string KidsLayerName = "Kids";

        // ----- Tunables ------------------------------------------------------------------------

        /// <summary>Width of the opening in the playground fence in front of the door. The playground builder uses it too.</summary>
        public const float FenceGapWidth = 3f;

        /// <summary>Half-width of the strip from the playground to the school door that stays free of trees and benches.</summary>
        public const float CorridorHalfWidth = 4f;

        /// <summary>The way from the playground toward the follow camera (it sits south of the player and looks north).</summary>
        static readonly Vector3 CameraSideDirection = Vector3.back;

        /// <summary>Wall segments along the door and blackboard walls. Odd, so the door piece sits in the middle.</summary>
        const int WallsAlongX = 5;

        /// <summary>Wall segments along the side walls.</summary>
        const int WallsAlongZ = 4;

        /// <summary>Free ground between the fence and the school's front (door) wall.</summary>
        const float ForecourtDepth = 2f;

        /// <summary>The floor sits a hair above the grass so the two never flicker.</summary>
        const float FloorY = 0.02f;

        /// <summary>The flat roof reaches this far past the walls.</summary>
        const float RoofOverhang = 0.4f;

        // Blackboard.
        const float BlackboardScale = 1.5f;
        const float BlackboardBottomY = 0.85f;
        const string BlackboardMaterialPath = "Assets/Imported/Synty/PolygonKids/Materials/PolygonKids_Material_04_A.mat";
        /// <summary>How far in front of the board's back plane the chalk surface (the canvas) is.</summary>
        const float BlackboardFaceOffset = 0.08f;

        // Lesson table (a stretched Kids table) and everything placed relative to it.
        // The table is 2.86 m wide and 1.37 m deep (the Kids table is 1.1 x 0.92 m before scaling), so two rows of toys 0.6 m apart fit.
        static readonly Vector3 LessonTableScale = new Vector3(2.6f, 1.25f, 1.5f);
        // The table stands close to the blackboard so the board and the toys fit in one picture from above.
        const float TableCentreFromBoard = 1.8f;
        const float RugScale = 1.8f;
        const float SeatBesideTable = 0.1f;
        const float SeatInFrontOfTable = 1f;
        // The lesson camera: 3.4 m toward the door and 3 m above the table top, looking at a point 0.5 m above the table's middle.
        // That is about 41 degrees down onto the table, so toys in different rows do not hide each other, and still low enough
        // (the top of the picture is about 11 degrees down) to keep the whole blackboard writing area in the picture.
        // It sits above the roof; the roof is hidden while the kid is in the classroom (BuildingInteriorReveal).
        const float LessonCameraDistance = 3.4f;
        const float LessonCameraHeight = 3f;
        const float LessonLookHeight = 0.5f;
        const float TeacherBesideBoard = 0.8f;
        const float TeacherFromWall = 1.3f;

        // Furniture rows (distance from the door wall's inside face) and kid desk columns.
        const float DeskRowOneFromDoorWall = 3.3f;
        const float DeskRowTwoFromDoorWall = 1.9f;
        const float ChairBehindDesk = 0.5f;
        static readonly float[] DeskRowOneX = { -3.6f, -2.2f, 2.2f, 3.6f };
        static readonly float[] DeskRowTwoX = { -2.9f, 2.9f };

        // Lights.
        static readonly Color LightColor = new Color(1f, 0.86f, 0.62f);
        const float LightIntensity = 1.6f;
        const float LightRange = 8f;

        // ----- Prefabs (resolved by name) ------------------------------------------------------

        static readonly PrefabRef BlackboardPrefab = Kids("SM_Prop_Blackboard_01");
        static readonly PrefabRef TablePrefab = Kids("SM_Prop_Table_01");
        static readonly PrefabRef DeskPrefab = Kids("SM_Prop_Desk_01");
        static readonly PrefabRef ChairPrefab = Kids("SM_Prop_Chair_01");
        static readonly PrefabRef LockerSinglePrefab = Kids("SM_Prop_Locker_01");
        static readonly PrefabRef LockerDoublePrefab = Kids("SM_Prop_Locker_02");
        static readonly PrefabRef BookshelfTallPrefab = new PrefabRef("SM_Prop_Bookshelf_01", "PolygonTown");
        static readonly PrefabRef BookshelfLowPrefab = new PrefabRef("SM_Prop_Bookshelf_02", "PolygonTown");
        static readonly PrefabRef ClockPrefab = new PrefabRef("SM_Prop_Clock_01", "PolygonTown");
        static readonly PrefabRef RugPrefab = new PrefabRef("SM_Prop_Rug_01", "PolygonTown");
        static readonly PrefabRef FloorMatPrefab = new PrefabRef("SM_Prop_FloorMat_01", "PolygonTown");
        static readonly PrefabRef GlobePrefab = new PrefabRef("SM_Prop_Globe_01", "PolygonTown");

        static readonly PrefabRef TeacherPrefab = new PrefabRef("SM_Chr_Mother_01", "PolygonTown");
        static readonly PrefabRef TeacherFallbackPrefab = new PrefabRef("Character_BusinessWoman", "PolygonCity");

        // ----- Data ----------------------------------------------------------------------------

        /// <summary>The footprint in the school's own frame (centred on the origin, door on +Z). The walls' centre lines form the rectangle.</summary>
        sealed class Plan
        {
            public float Length, Height, MaxThickness;
            public float Width, Depth;
            public float WestX, EastX, BoardZ, DoorZ;
            public float InnerWest, InnerEast, InnerBoard, InnerDoor;
        }

        /// <summary>What the later steps need to know about the placed furniture.</summary>
        sealed class Furniture
        {
            public Bounds Table;
            public Bounds Board;
            public bool HasTable;
            public bool HasBoard;
        }

        // ----- Entry point ---------------------------------------------------------------------

        /// <summary>
        /// Creates the "School" root beyond the playground fence. Call before the NavMesh bake.
        /// fenceGapPoint is the middle of the opening in the fence (on the fence line); doorFacing is the direction from the
        /// school toward the playground (snapped to the nearest of +/-X, +/-Z), so the door faces that way and the school
        /// stands on the other side of the fence. For a school north of the playground that is (0, 0, -1).
        /// </summary>
        public static SchoolResult Build(Transform parent, Vector3 fenceGapPoint, Vector3 doorFacing)
        {
            var result = new SchoolResult();
            Transform root = new GameObject("School").transform;
            root.SetParent(parent, false);
            result.root = root;
            doorFacing = SnapToAxis(doorFacing);
            result.doorFacing = doorFacing;

            Kit kit = MeasureKit(root);
            if (kit == null)
            {
                Debug.LogError("CoolSchool: the Synty Generic wall, floor or ceiling pieces were not found; no school was built.");
                return result;
            }

            // Built around the origin in the school's own frame (door on +Z); turned and moved into place at the end.
            Plan plan = MakePlan(kit);

            Transform shell = NewGroup("Shell", root);
            Transform props = NewGroup("Props", root);

            var wallsBySide = new Dictionary<Side, List<GameObject>>();
            BuildWalls(NewGroup("Walls", shell), kit, plan, wallsBySide);
            BuildCornerPillars(NewGroup("Pillars", shell), kit, plan);
            Transform floorGroup = NewGroup("Floor", shell);
            BuildFloor(floorGroup, kit, plan);
            List<Renderer> roof = BuildRoof(NewGroup("Roof", shell), kit, plan);

            Furniture furniture = BuildFurniture(props, plan);
            BuildLights(NewGroup("Lights", root), plan);

            BuildAnchors(root, plan, furniture, result);
            BuildTeacher(root, result);

            // The reveal box is in the root's local space, so it can be set up before the turn.
            AddInteriorReveal(root, plan, roof, wallsBySide, doorFacing);

            PlaceRoot(root, plan, fenceGapPoint, doorFacing, result);
            if (TryGetBounds(floorGroup.gameObject, out Bounds floorBounds)) result.floorArea = floorBounds;

            LogSummary(kit, plan, result);
            return result;
        }

        /// <summary>The nearest flat axis direction; the camera side (-Z) when the vector is empty.</summary>
        static Vector3 SnapToAxis(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return Vector3.back;

            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }

        /// <summary>
        /// Turns the finished school so its door faces doorFacing and puts it beyond the fence gap, a forecourt's depth from
        /// the fence line. Fills in the footprint and the door point.
        /// </summary>
        static void PlaceRoot(Transform root, Plan plan, Vector3 fenceGapPoint, Vector3 doorFacing, SchoolResult result)
        {
            // The door wall's centre line is half a wall thickness (plus the corner pillars' extra) and the forecourt from the fence.
            float doorWallToFence = ForecourtDepth + plan.MaxThickness * 0.5f + 0.1f;
            Vector3 centre = fenceGapPoint - doorFacing * (doorWallToFence + plan.DoorZ);
            centre.y = 0f;

            float yaw = Mathf.Atan2(doorFacing.x, doorFacing.z) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            root.SetPositionAndRotation(centre, rotation);
            result.yaw = yaw;

            var footprint = new Bounds(centre, Vector3.zero);
            float halfX = plan.Width * 0.5f + RoofOverhang;
            float halfZ = plan.Depth * 0.5f + RoofOverhang;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    footprint.Encapsulate(centre + rotation * new Vector3(sx * halfX, 0f, sz * halfZ));
            footprint.Encapsulate(centre + Vector3.up * plan.Height);
            result.footprint = footprint;

            result.doorPoint = root.TransformPoint(new Vector3(0f, 0f, plan.DoorZ));
        }

        // ----- Measuring -----------------------------------------------------------------------

        /// <summary>The footprint: walls' centre lines, as a rectangle centred on the origin with the door wall on +Z.</summary>
        static Plan MakePlan(Kit kit)
        {
            var plan = new Plan
            {
                Length = kit.Wall.Length,
                Height = kit.Wall.Size.y,
                MaxThickness = new[] { kit.Wall, kit.Window, kit.Door }.Where(p => p != null).Max(p => p.Thickness),
            };

            plan.Width = WallsAlongX * plan.Length;
            plan.Depth = WallsAlongZ * plan.Length;
            plan.WestX = -plan.Width * 0.5f;
            plan.EastX = plan.Width * 0.5f;

            plan.DoorZ = plan.Depth * 0.5f;
            plan.BoardZ = -plan.Depth * 0.5f;

            float inset = plan.MaxThickness * 0.5f + 0.02f;
            plan.InnerWest = plan.WestX + inset;
            plan.InnerEast = plan.EastX - inset;
            plan.InnerBoard = plan.BoardZ + inset;
            plan.InnerDoor = plan.DoorZ - inset;
            return plan;
        }

        // ----- Shell ---------------------------------------------------------------------------

        /// <summary>The four walls, named in the school's own frame: Board is the blackboard wall (-Z), Door the door wall (+Z).</summary>
        enum Side
        {
            Board,
            Door,
            West,
            East,
        }

        /// <summary>The way a wall faces outwards, in the school's own frame.</summary>
        static Vector3 Outward(Side side)
        {
            switch (side)
            {
                case Side.Board: return Vector3.back;
                case Side.Door: return Vector3.forward;
                case Side.West: return Vector3.left;
                default: return Vector3.right;
            }
        }

        /// <summary>Four rows of wall pieces. The pieces of each wall are also returned, for the interior reveal.</summary>
        static void BuildWalls(Transform parent, Kit kit, Plan plan, Dictionary<Side, List<GameObject>> wallsBySide)
        {
            foreach (Side side in new[] { Side.Board, Side.Door, Side.West, Side.East })
            {
                var pieces = new List<GameObject>();
                BuildWallSide(parent, kit, plan, side, pieces);
                wallsBySide[side] = pieces;
            }
        }

        /// <summary>
        /// One side of the rectangle. Each piece is turned so its plaster side faces into the room, and centred on the
        /// wall line by its bounds, so the thin axis sits on the line whatever the pivot is.
        /// </summary>
        static void BuildWallSide(Transform parent, Kit kit, Plan plan, Side side, List<GameObject> pieces)
        {
            bool alongX = side == Side.Board || side == Side.Door;
            int count = alongX ? WallsAlongX : WallsAlongZ;

            Vector3 inward;
            Vector3 start;
            Vector3 along;
            switch (side)
            {
                case Side.Board:
                    inward = Vector3.forward;
                    start = new Vector3(plan.WestX, 0f, plan.BoardZ);
                    along = Vector3.right;
                    break;
                case Side.Door:
                    inward = Vector3.back;
                    start = new Vector3(plan.WestX, 0f, plan.DoorZ);
                    along = Vector3.right;
                    break;
                case Side.West:
                    inward = Vector3.right;
                    start = new Vector3(plan.WestX, 0f, plan.BoardZ);
                    along = Vector3.forward;
                    break;
                default:
                    inward = Vector3.left;
                    start = new Vector3(plan.EastX, 0f, plan.BoardZ);
                    along = Vector3.forward;
                    break;
            }

            for (int i = 0; i < count; i++)
            {
                Piece piece = PieceFor(kit, side, i, count);
                Vector3 point = start + along * (plan.Length * (i + 0.5f));

                GameObject wall = Spawn(piece.Prefab, parent);
                wall.name = $"Wall_{side}_{i + 1}";
                wall.transform.rotation = Quaternion.LookRotation(inward, Vector3.up) * piece.Fix;
                AlignToBounds(wall, point.x, point.z, 0f, false);
                FinishEnvironmentObject(wall);
                pieces.Add(wall);
            }
        }

        /// <summary>
        /// The doorway in the middle of the door wall with a window each side of it, windows at both ends of the
        /// blackboard wall (its middle stays plain), windows on every other piece of the west wall, one at the blackboard end
        /// of the east wall; the rest is plain wall (where the lockers and bookshelves stand).
        /// </summary>
        static Piece PieceFor(Kit kit, Side side, int index, int count)
        {
            Piece wanted = kit.Wall;
            switch (side)
            {
                case Side.Door:
                    if (index == count / 2) wanted = kit.Door;
                    else if (index % 2 == 1) wanted = kit.Window;
                    break;
                case Side.Board:
                    if (index == 0 || index == count - 1) wanted = kit.Window;
                    break;
                case Side.West:
                    if (index % 2 == 0) wanted = kit.Window;
                    break;
                case Side.East:
                    if (index == 0) wanted = kit.Window;
                    break;
            }
            return wanted ?? kit.Wall;
        }

        static void BuildCornerPillars(Transform parent, Kit kit, Plan plan)
        {
            if (kit.Pillar == null) return;

            foreach (float x in new[] { plan.WestX, plan.EastX })
            {
                foreach (float z in new[] { plan.BoardZ, plan.DoorZ })
                {
                    GameObject pillar = Spawn(kit.Pillar.Prefab, parent);
                    pillar.name = "Pillar";
                    AlignToBounds(pillar, x, z, 0f, false);
                    FinishEnvironmentObject(pillar);
                }
            }
        }

        static void BuildFloor(Transform parent, Kit kit, Plan plan)
        {
            LayTiles(parent, kit.Floor, plan.Width, plan.Depth, 0f, FloorY, Quaternion.identity, true);
        }

        /// <summary>
        /// A flat roof: the ceiling tile twice, once as it is and once upside down on top, so it is solid
        /// from below and from above. No colliders; the follow camera must not bump into it. Returns every roof renderer.
        /// </summary>
        static List<Renderer> BuildRoof(Transform parent, Kit kit, Plan plan)
        {
            var renderers = new List<Renderer>();
            Piece tile = kit.Ceiling ?? kit.Floor;
            LayTiles(parent, tile, plan.Width, plan.Depth, RoofOverhang, plan.Height, Quaternion.identity, false);
            LayTiles(parent, tile, plan.Width, plan.Depth, RoofOverhang, plan.Height + 0.02f, Quaternion.Euler(180f, 0f, 0f), false);

            foreach (Renderer renderer in parent.GetComponentsInChildren<Renderer>(true)) renderers.Add(renderer);
            return renderers;
        }

        // ----- Furniture -----------------------------------------------------------------------

        static Furniture BuildFurniture(Transform parent, Plan plan)
        {
            var furniture = new Furniture();
            float tableZ = plan.InnerBoard + TableCentreFromBoard;

            PlaceBlackboard(parent, plan, furniture);
            PlaceClock(parent, plan);
            PlaceTableAndRug(parent, plan, tableZ, furniture);
            PlaceDesks(parent, plan);
            PlaceLockers(parent, plan);
            PlaceBookshelves(parent, plan);
            PlaceDoorMat(parent, plan);
            return furniture;
        }

        static void PlaceBlackboard(Transform parent, Plan plan, Furniture furniture)
        {
            GameObject board = PlaceProp(parent, BlackboardPrefab, Vector3.one * BlackboardScale, 0f, 0f, plan.InnerBoard, 0f);
            if (board == null) return;

            ApplyBlackboardMaterial(board);
            PushAgainstWall(board, Vector3.forward, plan.InnerBoard, 0.01f);
            SetBottomY(board, BlackboardBottomY);
            if (TryGetBounds(board, out Bounds bounds))
            {
                furniture.Board = bounds;
                furniture.HasBoard = true;
            }
        }

        /// <summary>The board's one MeshRenderer gets the hand-picked Kids material (slot 0), as tweaked in the saved scene.</summary>
        static void ApplyBlackboardMaterial(GameObject board)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BlackboardMaterialPath);
            if (material == null)
            {
                Debug.LogWarning("CoolSchool: blackboard material not found at " + BlackboardMaterialPath + "; the board keeps its prefab material.");
                return;
            }

            MeshRenderer renderer = board.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null) return;

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) return;
            materials[0] = material;
            renderer.sharedMaterials = materials;
        }

        /// <summary>A clock on the blackboard wall, right of the board as seen from the door.</summary>
        static void PlaceClock(Transform parent, Plan plan)
        {
            GameObject clock = PlaceProp(parent, ClockPrefab, Vector3.one * 1.5f, 0f, 2.6f, plan.InnerBoard, 0f, false);
            if (clock == null) return;

            PushAgainstWall(clock, Vector3.forward, plan.InnerBoard, 0.01f);
            if (TryGetBounds(clock, out Bounds bounds)) SetBottomY(clock, 2.35f - bounds.size.y * 0.5f);
        }

        static void PlaceTableAndRug(Transform parent, Plan plan, float tableZ, Furniture furniture)
        {
            PlaceProp(parent, RugPrefab, new Vector3(RugScale, 1f, RugScale), 0f, 0f, tableZ + 0.2f, FloorY, false);

            GameObject table = PlaceProp(parent, TablePrefab, LessonTableScale, 0f, 0f, tableZ, FloorY);
            if (table != null && TryGetBounds(table, out Bounds bounds))
            {
                furniture.Table = bounds;
                furniture.HasTable = true;
            }
            else
            {
                Debug.LogWarning("CoolSchool: the lesson table prefab (SM_Prop_Table_01) was not found; the lesson anchors use default spots.");
            }
        }

        /// <summary>Kid desks in two groups with an aisle from the door to the lesson table, each with a chair behind it.</summary>
        static void PlaceDesks(Transform parent, Plan plan)
        {
            PlaceDeskRow(parent, plan.InnerDoor - DeskRowOneFromDoorWall, DeskRowOneX);
            PlaceDeskRow(parent, plan.InnerDoor - DeskRowTwoFromDoorWall, DeskRowTwoX);
        }

        /// <summary>Desks face the blackboard (-Z in the school's frame, so north once the school is turned); the chair is on the door side of its desk.</summary>
        static void PlaceDeskRow(Transform parent, float z, float[] columns)
        {
            foreach (float x in columns)
            {
                PlaceProp(parent, DeskPrefab, Vector3.one, 180f, x, z, FloorY);
                PlaceProp(parent, ChairPrefab, Vector3.one, 180f, x, z + ChairBehindDesk, FloorY);
            }
        }

        /// <summary>Lockers along the east wall (in the school's frame), from the lesson area up to the door wall, facing into the room.</summary>
        static void PlaceLockers(Transform parent, Plan plan)
        {
            float cursor = plan.InnerBoard + 4f;
            float limit = plan.InnerDoor - 0.4f;
            bool doubleWide = true;

            for (int i = 0; i < 8; i++)
            {
                GameObject locker = PlaceProp(parent, doubleWide ? LockerDoublePrefab : LockerSinglePrefab,
                    Vector3.one, -90f, plan.InnerEast, cursor, FloorY);
                doubleWide = !doubleWide;
                if (locker == null) break;

                PushAgainstWall(locker, Vector3.left, plan.InnerEast, 0.01f);
                if (!TryGetBounds(locker, out Bounds bounds)) break;

                // The next locker starts where this one ends; it was placed by its centre, so line up its blackboard-side edge.
                locker.transform.position += new Vector3(0f, 0f, cursor - bounds.min.z);
                bounds.center += new Vector3(0f, 0f, cursor - bounds.min.z);
                if (bounds.max.z > limit)
                {
                    Object.DestroyImmediate(locker);
                    break;
                }

                cursor = bounds.max.z + 0.05f;
            }
        }

        /// <summary>Tall bookshelves on the plain pieces of the west wall, a low one (with a globe) between them.</summary>
        static void PlaceBookshelves(Transform parent, Plan plan)
        {
            float firstCentre = plan.BoardZ + plan.Length * 1.5f;
            foreach (int slot in new[] { 0, 2 })
            {
                GameObject tall = PlaceProp(parent, BookshelfTallPrefab, Vector3.one, 90f, plan.InnerWest, firstCentre + plan.Length * slot, 0f);
                if (tall != null) PushAgainstWall(tall, Vector3.right, plan.InnerWest, 0.01f);
            }

            GameObject low = PlaceProp(parent, BookshelfLowPrefab, Vector3.one, 90f, plan.InnerWest, firstCentre + plan.Length, 0f);
            if (low == null) return;

            PushAgainstWall(low, Vector3.right, plan.InnerWest, 0.01f);
            if (!TryGetBounds(low, out Bounds bounds)) return;

            GameObject globe = PlaceProp(parent, GlobePrefab, Vector3.one * 0.7f, 90f, bounds.center.x, bounds.center.z, bounds.max.y, false);
            if (globe != null) SetBottomY(globe, bounds.max.y);
        }

        static void PlaceDoorMat(Transform parent, Plan plan)
        {
            PlaceProp(parent, FloorMatPrefab, Vector3.one, 0f, 0f, plan.InnerDoor - 0.9f, FloorY, false);
        }

        // ----- Lights --------------------------------------------------------------------------

        /// <summary>Two warm point lights near the ceiling, one each side of the aisle.</summary>
        static void BuildLights(Transform parent, Plan plan)
        {
            float z = (plan.InnerBoard + plan.InnerDoor) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                var lightObject = new GameObject(side < 0f ? "WarmLight_West" : "WarmLight_East");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.position = new Vector3(side * plan.Width * 0.25f, plan.Height - 0.5f, z);

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LightColor;
                light.intensity = LightIntensity;
                light.range = LightRange;
                light.shadows = LightShadows.None;
            }
        }

        // ----- Anchors -------------------------------------------------------------------------

        static void BuildAnchors(Transform root, Plan plan, Furniture furniture, SchoolResult result)
        {
            Transform anchors = NewGroup("LessonAnchors", root);

            // Fall back to sensible spots if a prop was missing.
            float tableX = furniture.HasTable ? furniture.Table.center.x : 0f;
            float tableZ = furniture.HasTable ? furniture.Table.center.z : plan.InnerBoard + TableCentreFromBoard;
            float tableTop = furniture.HasTable ? furniture.Table.max.y : FloorY + 0.6f;
            float tableMaxX = furniture.HasTable ? furniture.Table.max.x : 1.4f;
            var tableCentre = new Vector3(tableX, tableTop, tableZ);

            result.tableSurface = NewAnchor("TableSurface", anchors, tableCentre + Vector3.up * 0.005f, Quaternion.identity);

            float cameraZ = Mathf.Min(tableZ + LessonCameraDistance, plan.InnerDoor - 1f);
            var cameraPosition = new Vector3(tableX, tableTop + LessonCameraHeight, cameraZ);
            Vector3 lookTarget = tableCentre + Vector3.up * LessonLookHeight;
            result.lessonCamera = NewAnchor("LessonCamera", anchors, cameraPosition, Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up));

            // The canvas goes just in front of the chalk surface, facing into the room.
            Vector3 facePosition = furniture.HasBoard
                ? new Vector3(furniture.Board.center.x, furniture.Board.center.y, furniture.Board.min.z + BlackboardFaceOffset)
                : new Vector3(0f, 1.7f, plan.InnerBoard + 0.05f);
            result.blackboardFace = NewAnchor("BlackboardFace", anchors, facePosition, Quaternion.identity);

            float teacherX = (furniture.HasBoard ? furniture.Board.min.x : -1.5f) - TeacherBesideBoard;
            var teacherPosition = new Vector3(teacherX, FloorY, plan.InnerBoard + TeacherFromWall);
            result.teacherSpot = NewAnchor("TeacherSpot", anchors, teacherPosition, FlatLookRotation(teacherPosition, tableCentre));

            var seatPosition = new Vector3(tableMaxX + SeatBesideTable, FloorY, tableZ + SeatInFrontOfTable);
            result.playerSeat = NewAnchor("PlayerSeat", anchors, seatPosition, FlatLookRotation(seatPosition, tableCentre));

            result.classroomZone = NewAnchor("ClassroomZone", anchors, new Vector3(0f, FloorY, plan.InnerDoor - 2.2f), Quaternion.identity);
        }

        // ----- Teacher -------------------------------------------------------------------------

        /// <summary>
        /// "Teacher" root at the TeacherSpot with the character model as its child, the Teacher.controller on the
        /// model's Animator and a TeacherCharacter on the root.
        /// </summary>
        static void BuildTeacher(Transform parent, SchoolResult result)
        {
            TeacherAnimatorBuilder.Result clips = TeacherAnimatorBuilder.CreateOrReplace(TeacherControllerPath);

            var rootObject = new GameObject("Teacher");
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.SetPositionAndRotation(result.teacherSpot.position, result.teacherSpot.rotation);

            GameObject prefab = LoadPrefab(TeacherPrefab) ?? LoadPrefab(TeacherFallbackPrefab);
            Animator animator = null;
            float height = 1.7f;
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootObject.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    if (clips.Controller != null) animator.runtimeAnimatorController = clips.Controller;
                    animator.applyRootMotion = false;
                }

                if (TryGetBounds(model, out Bounds bounds)) height = Mathf.Clamp(bounds.size.y, 1f, 2.2f);
            }
            else
            {
                Debug.LogWarning("CoolSchool: neither SM_Chr_Mother_01 nor Character_BusinessWoman was found; the classroom has an empty Teacher object.");
            }

            var capsule = rootObject.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            var character = rootObject.AddComponent<TeacherCharacter>();
            SetObject(character, "animator", animator);

            int kidsLayer = LayerMask.NameToLayer(KidsLayerName);
            SetLayerRecursive(rootObject, kidsLayer);
            result.teacher = character;

            Debug.Log($"CoolSchool teacher: model {(prefab != null ? prefab.name : "none")}, {height:0.00} m tall; idle={clips.IdleClip ?? "none"}, "
                + $"talk={(clips.TalkClips.Count > 0 ? string.Join(" / ", clips.TalkClips) : "none")}, wave={clips.WaveClip ?? "none"}.");
        }

        // ----- Interior reveal -----------------------------------------------------------------

        /// <summary>
        /// The roof hides when the player is inside or approaching the door. The wall that hides while the camera is
        /// outside is the one facing the follow camera, which sits south of the player and looks north. The school is
        /// turned so its door faces the playground, so that is whichever wall points most toward -Z after the turn
        /// (the door wall for a school north of the playground).
        /// </summary>
        static void AddInteriorReveal(Transform root, Plan plan, List<Renderer> roof, Dictionary<Side, List<GameObject>> wallsBySide,
            Vector3 doorFacing)
        {
            Quaternion turn = Quaternion.Euler(0f, Mathf.Atan2(doorFacing.x, doorFacing.z) * Mathf.Rad2Deg, 0f);
            Side cameraSide = Side.Door;
            float best = float.MinValue;
            foreach (KeyValuePair<Side, List<GameObject>> entry in wallsBySide)
            {
                float facing = Vector3.Dot(turn * Outward(entry.Key), CameraSideDirection);
                if (facing <= best) continue;

                best = facing;
                cameraSide = entry.Key;
            }

            var reveal = root.gameObject.AddComponent<BuildingInteriorReveal>();
            SetObjectArray(reveal, "hideWhenInside", roof.Cast<Object>().ToList());
            SetObjectArray(reveal, "hideWhenCameraOutside", wallsBySide[cameraSide].Cast<Object>().ToList());

            // The box and the direction are stored in the School root's local space, which is turned with the building:
            // the approach side is local +Z, the door wall.
            Vector3 centre = root.InverseTransformPoint(new Vector3(0f, plan.Height * 0.5f, 0f));
            SetVector3(reveal, "interiorCenter", centre);
            SetVector3(reveal, "interiorSize", new Vector3(plan.Width - plan.MaxThickness, plan.Height, plan.Depth - plan.MaxThickness));
            SetFloat(reveal, "approachDistance", 3f);
            SetVector3(reveal, "approachDirection", Vector3.forward);
        }

        // ----- Log -----------------------------------------------------------------------------

        static void LogSummary(Kit kit, Plan plan, SchoolResult result)
        {
            string Describe(Piece piece) => piece == null
                ? "missing"
                : $"{piece.Name} {piece.Size.x:0.00} x {piece.Size.y:0.00} x {piece.Size.z:0.00} m"
                  + (piece.LengthAlongX ? "" : " (runs along Z)") + (piece.InsideOnNegativeZ ? " (inside on -Z, turned)" : "");

            string door = kit.Door == null
                ? "missing"
                : $"{Describe(kit.Door)}, opening {kit.Door.OpeningWidth:0.00} m wide x {kit.Door.OpeningHeight:0.00} m tall";

            Bounds area = result.footprint;
            Debug.Log("CoolSchool school measured: "
                + $"wall {Describe(kit.Wall)}; window {Describe(kit.Window)}; door {door}; floor {Describe(kit.Floor)}; "
                + $"ceiling {Describe(kit.Ceiling)}; pillar {Describe(kit.Pillar)}. "
                + $"Building {plan.Width:0.0} x {plan.Depth:0.0} x {plan.Height:0.0} m, centre {result.root.position.ToString("0.00")}, "
                + $"yaw {result.yaw:0}, door faces {result.doorFacing.ToString("0.0")}, "
                + $"x {area.min.x:0.0}..{area.max.x:0.0}, z {area.min.z:0.0}..{area.max.z:0.0}; "
                + $"door at {result.doorPoint.ToString("0.00")}, tableSurface {Format(result.tableSurface)}, lessonCamera {Format(result.lessonCamera)}, "
                + $"blackboardFace {Format(result.blackboardFace)}, teacherSpot {Format(result.teacherSpot)}, "
                + $"playerSeat {Format(result.playerSeat)}, classroomZone {Format(result.classroomZone)}.");
        }

        static string Format(Transform anchor)
        {
            return anchor == null ? "none" : anchor.position.ToString("0.00");
        }
    }
}
