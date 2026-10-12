using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the pirate cove north-east of the playground, behind the castle (contract Docs/specs/pirate-cove-contract.md).
    /// Two steps, like the alien field: BuildCove (static geometry, before the NavMesh is baked) and Build (characters, the
    /// activity, the UI and all the wiring; see PirateCoveBuilder.Build.cs).
    ///
    /// Everything is laid out in cove coordinates: u metres east of the cove's south-west corner (SW) and v metres north of it.
    /// The camera looks north, so the things the kid must see in each phase (the boat of balloons, the island, the landmarks,
    /// the treasure) lie north of where he stands then. Pieces are placed by their measured bounds, never by pivots.
    /// </summary>
    internal static partial class PirateCoveBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string MaterialFolder = RootFolder + "/Materials/Pirates";

        // ---- Layout (metres) ----
        const float CoveDepth = 40f;
        const float EntranceWidth = 3f;
        const float EntranceFromWest = 2.6f;

        // Heights: the sand sits 1 cm above the grass, the ship and boat 6 cm, the island a hand above the sand.
        const float SandY = 0.01f;
        const float SeaY = 0.06f;
        const float IslandTopY = 0.14f;

        const float DockHalfWidth = 6f;
        const float DockSouth = 2f;
        const float DockNorth = 6f;
        // Every plank has its top at this height (the planks differ in thickness), so the dock is flush.
        const float DockSurfaceY = 0.12f;

        // The sandpit inside the grass cove: 2 m in from the west and east fence, 5 m from the south edge, 2 m from the north fence.
        const float PitWestInset = 2f;
        const float PitEastInset = 2f;
        const float PitSouthV = 5f;
        const float PitNorthInset = 2f;
        // Edging pieces taller than this are squashed to EdgeHeight, so the kid (step offset 0.25 m) can step over them.
        const float EdgeMaxHeight = 0.25f;
        const float EdgeHeight = 0.24f;

        const float ShipLength = 7f;
        const float ShipCentreV = 9f;
        const float ShipBaseY = 0.02f;
        const float IslandDockV = 22f;
        const float IslandCentreV = 30f;
        const float IslandWidth = 18f;
        const float IslandDepth = 12f;
        const float DriftV = 22f;

        const float ZoneRadius = 2f;
        const float ZoneV = 4.2f;
        const float BoatLength = 4.5f;
        const float ChestWidth = 0.9f;
        const int BalloonSlots = 7;

        // Landmarks on the island: (u from the cove's middle, v from its south edge). All north of the landing point, >= 4 m apart.
        static readonly Vector2[] LandmarkSpots =
        {
            new Vector2(-6.2f, 32.8f),   // flag
            new Vector2(6.2f, 32.6f),    // sandcastle
            new Vector2(-2.2f, 34.7f),   // rock
            new Vector2(2.4f, 34.6f),    // telescope on a barrel
        };

        static readonly PrefabRef SandTilePrefab = Kids("SM_Env_Ground_Tile_Sand_01");
        static readonly PrefabRef GrassTilePrefab = Kids("SM_Env_Ground_Tile_Grass");
        static readonly PrefabRef EdgeStraightPrefab = Kids("SM_Env_Playground_Edge_01");
        static readonly PrefabRef EdgeCornerPostPrefab = Kids("SM_Env_Playground_Edge_Corner_02");
        static readonly PrefabRef EdgeLPrefab = Kids("SM_Env_Playground_Edge_L_01");
        static readonly PrefabRef FencePrefab = Kids("SM_Env_Park_Fence_01");
        static readonly PrefabRef PlankA = new PrefabRef("SM_Gen_Prop_Plank_01", "PolygonGeneric");
        static readonly PrefabRef PlankB = new PrefabRef("SM_Gen_Prop_Plank_02", "PolygonGeneric");
        static readonly PrefabRef BarrelPrefab = new PrefabRef("SM_Gen_Prop_Barrel_Wood_01", "PolygonGeneric");
        static readonly PrefabRef AnchorPrefab = new PrefabRef("SM_Gen_Prop_Chain_Anchor_01", "PolygonGeneric");
        static readonly PrefabRef ShipPrefab = Kids("SM_Prop_Playground_Ship_01");
        static readonly PrefabRef WheelPrefab = new PrefabRef("SM_Veh_Steering_Wheel_01", "PolygonCity");
        static readonly PrefabRef IslandPrefab = Kids("SM_Env_Ground_Round_Sand_01");
        static readonly PrefabRef FlagPrefab = Kids("SM_Prop_Flag_01");
        static readonly PrefabRef SandCastlePrefab = Kids("SM_Prop_Sand_Castle_01");
        static readonly PrefabRef RockPrefab = new PrefabRef("SM_Gen_Env_Rock_03", "PolygonGeneric");
        static readonly PrefabRef TelescopePrefab = Kids("SM_Prop_Telescope_01");
        static readonly PrefabRef ChestPrefab = new PrefabRef("SM_Gen_Prop_Chest_01", "PolygonGeneric");
        static readonly PrefabRef BoatPrefab = Kids("SM_Prop_Cardboard_Boat_01");
        static readonly PrefabRef CoinPilePrefabA = new PrefabRef("SM_Gen_Prop_Coin_Pile_01", "PolygonGeneric");
        static readonly PrefabRef CoinPilePrefabB = new PrefabRef("SM_Gen_Prop_Coin_Pile_02", "PolygonGeneric");
        static readonly PrefabRef CoinStackPrefabA = new PrefabRef("SM_Gen_Prop_Coin_Stack_01", "PolygonGeneric");
        static readonly PrefabRef CoinStackPrefabB = new PrefabRef("SM_Gen_Prop_Coin_Stack_02", "PolygonGeneric");

        // Sea-blue beacon with an anchor, a ship, a map or a compass on it, whichever the UI kit has first.
        internal static readonly ActivityBeaconBuilder.Style PirateBeacon = new ActivityBeaconBuilder.Style(
            "Pirates", new Color(0.4f, 1.3f, 2.2f, 1f), new[] { "Anchor", "Ship", "Map", "Compass", "Map-Target-Circle" });

        /// <summary>Everything the second step needs from the first. All positions are in world space.</summary>
        internal sealed class CoveLayout
        {
            public Transform Root;
            public Vector3 SouthWest;
            public float Width;
            public float CentreX;
            public Rect Rect;
            /// <summary>The sandpit (x, z rectangle) inside the cove, and the thickness of its edging.</summary>
            public Rect Pit;
            public float EdgeWidth = 0.1f;
            /// <summary>The gap that was cut in the main north fence (x range), after removing the overlapping pieces.</summary>
            public Vector2 EntranceGap;

            // Ship
            public Transform Ship;
            public Transform HelmSeat;
            public Transform[] CrewSeats;
            public Transform Wheel;
            public Transform IslandDockPoint;
            public Bounds SeaArea;
            public Transform CannonBarrel;
            public Transform CannonMuzzle;
            public float ShipHalfLength = ShipLength * 0.5f;
            public Vector3 ShipStart;

            // Island
            public Vector3 IslandCentre;
            public Transform IslandLandingPoint;
            public Transform[] CrewLandingPoints;
            public Transform[] SkeletonLandingPoints;
            public Transform[] PartyPoints;

            // Landmarks, in this order: flag, sandcastle, rock, telescope. The prefab is what the map icon is rendered from.
            public Transform[] Landmarks;
            public PrefabRef[] LandmarkPrefabs;

            // Treasure
            public Transform DigSpot;
            public GameObject RedX;
            public Transform Chest;
            public Transform ChestLid;
            public GameObject LockVisual;
            public Transform[] CoinSpots;

            // Skeleton boat (balloon spheres are plain meshes here; the runtime components are added by Build)
            public Transform Boat;
            public Transform BoatStart;
            public Transform BoatDriftLeft;
            public Transform BoatDriftRight;
            public Transform BoatIslandBeach;
            public Transform[] SkeletonSeats;
            /// <summary>The seven balloon slots (stick plus sphere; BalloonTarget goes on the slot) and each sphere's renderer.</summary>
            public GameObject[] Balloons;
            public Renderer[] BalloonRenderers;

            // Trophy by the dock
            public Transform Trophy;
            public Transform TrophyLid;
            public GameObject[] TrophyTiers;

            // Zone and character spots
            public Vector3 ZonePosition;
            public float DockTopY = DockSurfaceY;
            public Vector3 ParrotSpot;
            public Vector3 CrewSpot;

            public bool ShipCreated;
        }

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The cove: sand, own fence on three sides, the entrance gap in the main north fence, dock, ship,
        /// island, landmarks, dig spot with the buried chest, skeleton boat (inactive) and trophy.
        /// Call after the main fence and the castle exist (it needs the final MaxX) and before the NavMesh is baked.
        /// </summary>
        internal static CoveLayout BuildCove(Transform root, EnvironmentInfo info)
        {
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Pirates");

            var c = new CoveLayout();
            c.SouthWest =new Vector3(info.FenceHalf, 0f, info.MaxZ);
            c.Width = info.MaxX - info.FenceHalf;
            c.CentreX = c.SouthWest.x + c.Width * 0.5f;
            c.Rect = new Rect(c.SouthWest.x, c.SouthWest.z, c.Width, CoveDepth);
            c.Root = NewGroup("PirateCove", root);

            if (c.Width < 24f)
                Debug.LogWarning($"CoolSchool pirate cove: only {c.Width:0.0} m wide; the layout expects about 30 m.");

            if (info.School != null && info.School.footprint.size.sqrMagnitude > 0f && info.School.footprint.max.x > c.SouthWest.x - 1f)
                Debug.LogWarning($"CoolSchool pirate cove: the school's footprint reaches x {info.School.footprint.max.x:0.0}, "
                    + $"which is too close to the cove's west fence at x {c.SouthWest.x:0.0}.");

            // Keep benches, bins and trees out of the cove and out of the walk from the playground to the entrance.
            float gateX = c.SouthWest.x + EntranceFromWest;
            ReservedAreas.Add(c.Rect);
            ReservedAreas.Add(new Rect(gateX - 2.5f, c.SouthWest.z - 9f, 5f, 9f));

            Transform ground = NewGroup("Ground", c.Root);
            Transform fence = NewGroup("Fence", c.Root);
            Transform props = NewGroup("Props", c.Root);

            // The whole cove is grass (1 cm below the playground grass, so no tile fights another). A big framed sandpit lies
            // inside it: 2 m from the west and east fence, 5 m from the south edge (the dock and the trophy stand on grass south of
            // it) and 2 m from the north fence.
            c.Pit = Rect.MinMaxRect(c.SouthWest.x + PitWestInset, c.SouthWest.z + PitSouthV, info.MaxX - PitEastInset,
                c.SouthWest.z + CoveDepth - PitNorthInset);
            BuildCoveGrass(c, info, root, ground);
            BuildSandpit(c, ground);
            BuildEdging(c, ground);

            BuildCoveFence(LoadPrefab(FencePrefab), fence, c, info);
            c.EntranceGap = CutEntrance(root, c, info);

            BuildDock(c, props);
            BuildShip(c, props);
            BuildIsland(c, props);
            BuildLandmarks(c, props);
            BuildTreasureSpot(c, props);
            BuildSkeletonBoat(c, props);
            BuildTrophy(c, props);
            BuildLandingPoints(c, props);

            c.ZonePosition = new Vector3(c.CentreX, c.DockTopY, c.SouthWest.z + ZoneV);

            // The skeleton boat is inactive when the NavMesh is baked, and stays so until the adventure starts.
            if (c.Boat != null) c.Boat.gameObject.SetActive(false);

            // The ship and the boat move, so the follow camera (whose obstacle mask skips Ignore Raycast) must not pull in
            // against their hulls while sailing. Their colliders still block walking and still go into the NavMesh bake.
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            if (c.Ship != null) PlaygroundPrototypeBuilder.SetLayerRecursive(c.Ship.gameObject, ignoreRaycast);
            if (c.Boat != null) PlaygroundPrototypeBuilder.SetLayerRecursive(c.Boat.gameObject, ignoreRaycast);

            Vector3 landmarks = c.Landmarks != null && c.Landmarks.Length > 0 && c.Landmarks[0] != null ? c.Landmarks[0].position : Vector3.zero;
            Debug.Log($"CoolSchool pirate cove: rect x {c.Rect.xMin:0.0}..{c.Rect.xMax:0.0}, z {c.Rect.yMin:0.0}..{c.Rect.yMax:0.0}; "
                + $"entrance gap x {c.EntranceGap.x:0.0}..{c.EntranceGap.y:0.0}; ship {(c.Ship != null ? c.Ship.position.ToString("0.0") : "missing")} "
                + $"({ShipLength:0.0} m, bow north), island dock point {(c.IslandDockPoint != null ? c.IslandDockPoint.position.ToString("0.0") : "missing")}; "
                + $"island {c.IslandCentre.ToString("0.0")} ({IslandWidth:0} x {IslandDepth:0} m); first landmark {landmarks.ToString("0.0")}; "
                + $"zone {c.ZonePosition.ToString("0.0")}; sandpit x {c.Pit.xMin:0.0}..{c.Pit.xMax:0.0}, z {c.Pit.yMin:0.0}..{c.Pit.yMax:0.0}, "
                + $"edging {c.EdgeWidth:0.00} m thick.");
            return c;
        }

        // ---- Ground ----

        /// <summary>
        /// Lays tiles over x in xMin..xMax and z in zMin..zMax with their walking surface at topY. The tiles are stretched a
        /// little so the grid fits the rectangle exactly (nothing sticks out over the fence or into the playground).
        /// </summary>
        static void LayTiles(GameObject prefab, Transform parent, float xMin, float xMax, float zMin, float zMax, float topY, bool solid = true)
        {
            if (prefab == null) return;

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds) || probeBounds.size.x < 0.01f || probeBounds.size.z < 0.01f)
            {
                Debug.LogWarning($"CoolSchool pirate cove: tile has no usable size, skipping: {prefab.name}");
                Object.DestroyImmediate(probe);
                return;
            }
            Vector3 baseScale = probe.transform.localScale;
            // The walking surface is not the bounds top (tufts and bumps stick up): topY is where the real surface goes.
            GroundSurface.Measurement surface = GroundSurface.Measure(probe);
            float bump = surface.Valid ? Mathf.Max(0f, surface.BumpHeight) : 0f;
            Object.DestroyImmediate(probe);

            float naturalX = probeBounds.size.x < 0.5f ? 4f : probeBounds.size.x;
            float naturalZ = probeBounds.size.z < 0.5f ? 4f : probeBounds.size.z;
            int countX = Mathf.Clamp(Mathf.RoundToInt((xMax - xMin) / naturalX), 1, 60);
            int countZ = Mathf.Clamp(Mathf.RoundToInt((zMax - zMin) / naturalZ), 1, 60);
            float tileX = (xMax - xMin) / countX;
            float tileZ = (zMax - zMin) / countZ;
            var scale = new Vector3(baseScale.x * tileX / probeBounds.size.x, baseScale.y, baseScale.z * tileZ / probeBounds.size.z);

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    GameObject tile = Spawn(prefab, parent);
                    tile.transform.localScale = scale;
                    AlignToBounds(tile, xMin + tileX * (ix + 0.5f), zMin + tileZ * (iz + 0.5f), topY + bump, true);
                    if (solid && surface.Valid && surface.FlatShare < 0.9f) GroundSurface.UseFlatCollider(tile, topY);
                    FinishEnvironmentObject(tile);
                    if (!solid)
                        foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                }
            }
        }

        /// <summary>The cove's own fence: west, north and east sides (the south side is the main fence), like BuildFence.</summary>
        static void BuildCoveFence(GameObject prefab, Transform parent, CoveLayout c, EnvironmentInfo info)
        {
            if (prefab == null) return;

            GameObject probe = Spawn(prefab, parent);
            if (!TryGetBounds(probe, out Bounds probeBounds))
            {
                Debug.LogWarning("CoolSchool pirate cove: fence has no renderers, skipping.");
                Object.DestroyImmediate(probe);
                return;
            }
            Vector3 baseScale = probe.transform.localScale;
            Object.DestroyImmediate(probe);

            bool longAxisIsX = probeBounds.size.x >= probeBounds.size.z;
            float length = Mathf.Max(probeBounds.size.x, probeBounds.size.z);
            if (length < 0.2f)
            {
                Debug.LogWarning("CoolSchool pirate cove: fence segment is tiny, skipping.");
                return;
            }

            float z0 = c.SouthWest.z;
            float z1 = c.SouthWest.z + CoveDepth;
            var sides = new[]
            {
                new CoveSide(new Vector3(c.SouthWest.x, 0f, (z0 + z1) * 0.5f), Vector3.right, CoveDepth),
                new CoveSide(new Vector3(c.CentreX, 0f, z1), Vector3.back, c.Width),
                new CoveSide(new Vector3(info.MaxX, 0f, (z0 + z1) * 0.5f), Vector3.left, CoveDepth),
            };

            foreach (CoveSide side in sides)
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(side.Length / length));
                float spacing = side.Length / count;

                float stretch = spacing / length;
                if (stretch < 0.8f || stretch > 1.25f) stretch = 1f;
                Vector3 scale = baseScale;
                if (longAxisIsX) scale.x *= stretch;
                else scale.z *= stretch;

                Vector3 along = Vector3.Cross(Vector3.up, side.ToCentre);
                Quaternion rotation = Quaternion.LookRotation(side.ToCentre, Vector3.up) * Quaternion.Euler(0f, longAxisIsX ? 0f : 90f, 0f);

                for (int i = 0; i < count; i++)
                {
                    Vector3 point = side.Centre + along * (-side.Length * 0.5f + spacing * (i + 0.5f));
                    GameObject segment = Spawn(prefab, parent);
                    segment.transform.localScale = scale;
                    segment.transform.rotation = rotation;
                    AlignToBounds(segment, point.x, point.z, 0f, false);
                    FinishEnvironmentObject(segment);
                }
            }
        }

        readonly struct CoveSide
        {
            public readonly Vector3 Centre;
            public readonly Vector3 ToCentre;
            public readonly float Length;

            public CoveSide(Vector3 centre, Vector3 toCentre, float length)
            {
                Centre = centre;
                ToCentre = toCentre;
                Length = length;
            }
        }

        /// <summary>
        /// Removes the main north fence pieces that overlap the entrance gap (3 m wide, centred 2.6 m east of the cove's west
        /// side). Returns the x range that is open afterwards (a whole piece goes, so it can be wider than 3 m).
        /// </summary>
        static Vector2 CutEntrance(Transform root, CoveLayout c, EnvironmentInfo info)
        {
            float gateX = c.SouthWest.x + EntranceFromWest;
            float gapMin = gateX - EntranceWidth * 0.5f;
            float gapMax = gateX + EntranceWidth * 0.5f;
            var opened = new Vector2(gapMin, gapMax);

            Transform fenceGroup = root.Find("Fence");
            if (fenceGroup == null)
            {
                Debug.LogWarning("CoolSchool pirate cove: no 'Fence' group found, so the entrance gap was not cut.");
                return opened;
            }

            var doomed = new List<GameObject>();
            foreach (Transform piece in fenceGroup)
            {
                if (!TryGetBounds(piece.gameObject, out Bounds bounds)) continue;

                bool onNorthLine = bounds.max.z > info.MaxZ - 1.2f && bounds.min.z < info.MaxZ + 1.2f;
                bool inGap = bounds.max.x > gapMin + 0.05f && bounds.min.x < gapMax - 0.05f;
                if (!onNorthLine || !inGap) continue;

                doomed.Add(piece.gameObject);
                opened.x = Mathf.Min(opened.x, bounds.min.x);
                opened.y = Mathf.Max(opened.y, bounds.max.x);
            }

            if (doomed.Count == 0)
                Debug.LogWarning($"CoolSchool pirate cove: no main fence piece found in the entrance gap x {gapMin:0.0}..{gapMax:0.0}.");
            foreach (GameObject piece in doomed) Object.DestroyImmediate(piece);
            return opened;
        }

        // ---- Dock ----

        /// <summary>A wooden dock of planks along the south strip, with barrels and an anchor on it.</summary>
        static void BuildDock(CoveLayout c, Transform parent)
        {
            Transform dock = NewGroup("Dock", parent);
            // The planks are the dock's ground ("Floor" counts as ground for the grounding check); barrels and chests stand on their top.
            Transform floor = NewGroup("Floor", dock);
            float xMin = c.CentreX - DockHalfWidth;
            float xMax = c.CentreX + DockHalfWidth;
            float zMid = c.SouthWest.z + (DockSouth + DockNorth) * 0.5f;
            float length = DockNorth - DockSouth;

            GameObject[] planks = { LoadPrefab(PlankA), LoadPrefab(PlankB) };
            if (planks[0] == null && planks[1] == null)
            {
                Debug.LogWarning("CoolSchool pirate cove: no plank prefabs, so the dock is missing.");
            }
            else
            {
                if (planks[0] == null) planks[0] = planks[1];
                if (planks[1] == null) planks[1] = planks[0];

                float cursor = xMin;
                int guard = 0;
                while (cursor < xMax - 0.05f && guard++ < 300)
                {
                    GameObject plank = PlacePlank(planks[guard % 2], floor, cursor, zMid, length, out float width);
                    if (plank == null) break;
                    if (TryGetBounds(plank, out Bounds plankBounds)) c.DockTopY = Mathf.Max(c.DockTopY, plankBounds.max.y);
                    cursor += width;
                }
            }

            // A few barrels and an anchor on the east half of the dock, clear of the zone.
            float v = c.SouthWest.z;
            PlaceProp(BarrelPrefab, dock, 0f, SizeBy.Height, 0.9f, new Vector3(c.CentreX + 4.8f, 0f, v + 3.6f), c.DockTopY, true);
            PlaceProp(BarrelPrefab, dock, 40f, SizeBy.Height, 0.9f, new Vector3(c.CentreX + 5.3f, 0f, v + 4.6f), c.DockTopY, true);
            PlaceProp(BarrelPrefab, dock, 90f, SizeBy.Height, 0.8f, new Vector3(c.CentreX + 3.3f, 0f, v + 2.5f), c.DockTopY, true);
            PlaceProp(AnchorPrefab, dock, 20f, SizeBy.Widest, 1.1f, new Vector3(c.CentreX + 5.0f, 0f, v + 2.3f), c.DockTopY, true);
        }

        /// <summary>One plank laid across the dock (long axis north-south, stretched to the dock's depth only). Returns its width.</summary>
        static GameObject PlacePlank(GameObject prefab, Transform parent, float xStart, float zMid, float length, out float width)
        {
            width = 0.3f;
            if (prefab == null) return null;

            GameObject plank = Spawn(prefab, parent);
            if (!TryGetBounds(plank, out Bounds bounds) || Mathf.Max(bounds.size.x, bounds.size.z) < 0.05f)
            {
                Object.DestroyImmediate(plank);
                return null;
            }

            // Turn the long side north-south and stretch only that side to the dock's depth, so the planks keep their thickness.
            bool longIsX = bounds.size.x >= bounds.size.z;
            float natural = Mathf.Max(bounds.size.x, bounds.size.z);
            Vector3 scale = plank.transform.localScale;
            if (longIsX)
            {
                plank.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                scale.x *= length / natural;
            }
            else
            {
                scale.z *= length / natural;
            }
            plank.transform.localScale = scale;

            AlignToBounds(plank, xStart, zMid, DockSurfaceY, true);
            TryGetBounds(plank, out bounds);
            width = Mathf.Max(0.15f, bounds.size.x);
            AlignToBounds(plank, xStart + width * 0.5f, zMid, DockSurfaceY, true);
            FinishEnvironmentObject(plank);
            return plank;
        }

        // ---- Ship ----

        /// <summary>
        /// The playground ship (a second instance) as a "PirateShip" root: bow north, about 7 m long, standing on the sand and
        /// NOT static (it sails). The helm seat, two crew seats, the steering wheel and the cannon are children of the root.
        /// </summary>
        static void BuildShip(CoveLayout c, Transform parent)
        {
            var root = new GameObject("PirateShip").transform;
            root.SetParent(parent, false);
            c.ShipStart = new Vector3(c.CentreX, 0f, c.SouthWest.z + ShipCentreV);
            root.position = c.ShipStart;
            root.rotation = Quaternion.identity;
            c.Ship = root;

            // The arrival pose and the sailing area exist even if the ship model is missing.
            c.IslandDockPoint = NewPoint("IslandDockPoint", parent, new Vector3(c.CentreX, SeaY, c.SouthWest.z + IslandDockV));
            float seaX0 = c.Pit.xMin + c.EdgeWidth + 1.8f;
            float seaX1 = c.Pit.xMax - c.EdgeWidth - 1.8f;
            c.SeaArea = new Bounds(new Vector3((seaX0 + seaX1) * 0.5f, SeaY, c.SouthWest.z + 16f), new Vector3(seaX1 - seaX0, 2f, 18f));

            Bounds bounds = default;
            float deckY = ShipBaseY + 0.5f;
            float bowDeckY = deckY;
            DeckPatch patch = new DeckPatch();
            GameObject prefab = LoadPrefab(ShipPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, root);
                model.name = "ShipModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                AddMeshCollidersIfNone(model);

                float yaw = DetectBowYaw(model, "ship");
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (TryGetBounds(model, out bounds) && Mathf.Max(bounds.size.x, bounds.size.z) > 0.01f)
                    model.transform.localScale *= ShipLength / Mathf.Max(bounds.size.x, bounds.size.z);
                AlignToBounds(model, c.ShipStart.x, c.ShipStart.z, ShipBaseY, false);
                TryGetBounds(model, out bounds);
                Physics.SyncTransforms();
                c.ShipCreated = true;
                c.ShipHalfLength = bounds.size.z * 0.5f;

                // Measure against the ship's own triangles (temporary colliders), not a possibly convex collider.
                using (var probes = new ProbeSet(model))
                {
                    Collider[] colliders = probes.Colliders;
                    float helmZ = bounds.min.z + 0.22f * bounds.size.z;
                    deckY = FindDeckHeight(colliders, bounds, bounds.center.x, helmZ, ShipBaseY + 0.4f * bounds.size.y, "ship");
                    patch = FindHelmPatch(colliders, bounds, deckY);
                    // The cannon stands on the deck at the bow, which can be lower than the helm platform.
                    bowDeckY = FindDeckHeight(colliders, bounds, bounds.center.x, bounds.max.z - 1.1f, deckY, "ship bow");
                }
                Debug.Log($"CoolSchool pirate cove: ship bounds {bounds.size.ToString("0.00")} (after scaling), deck height at 22% {deckY:0.00}, bow deck {bowDeckY:0.00}, bow yaw {yaw:0}; "
                    + (patch.Found ? "helm " : "helm patch not found: ") + patch.Report + ".");
            }
            else
            {
                bounds = new Bounds(c.ShipStart + Vector3.up * 1f, new Vector3(2.5f, 2f, ShipLength));
                Debug.LogWarning("CoolSchool pirate cove: the ship prefab is missing; the seats are placed on an imaginary hull.");
            }

            // Seats and wheel, in the ship's frame (the root has no rotation, so local = world minus the root position).
            float lengthV = bounds.size.z;
            float helmV = bounds.min.z + 0.22f * lengthV - c.ShipStart.z;
            float lateral = Mathf.Min(0.9f, bounds.size.x * 0.25f);
            float centreU = bounds.center.x - c.ShipStart.x;

            Vector3 helmLocal = new Vector3(centreU, deckY, helmV);
            Vector3 crewLocal1 = new Vector3(centreU - lateral, deckY, helmV + 0.35f);
            Vector3 crewLocal2 = new Vector3(centreU + lateral, deckY, helmV + 0.35f);
            float wheelAhead = 0.6f;
            if (patch.Found)
            {
                // Seats from the real platform (world positions made local to the ship root, which is not turned).
                helmLocal = patch.Helm - c.ShipStart;
                crewLocal1 = patch.Crew[0] - c.ShipStart;
                crewLocal2 = patch.Crew[1] - c.ShipStart;
                deckY = patch.Y;
                wheelAhead = patch.WheelAhead;
            }
            helmLocal.y = deckY;

            c.HelmSeat = NewLocalPoint("HelmSeat", root, helmLocal);
            c.CrewSeats = new[]
            {
                NewLocalPoint("CrewSeat_1", root, crewLocal1),
                NewLocalPoint("CrewSeat_2", root, crewLocal2),
            };

            BuildWheel(c, root, helmLocal + new Vector3(0f, 0.75f, wheelAhead));
            BuildCannon(c, root, bounds, c.ShipStart, bowDeckY);
            Debug.Log($"CoolSchool pirate cove: ship seats (local to the ship): helm {helmLocal.ToString("0.00")}, crew {crewLocal1.ToString("0.00")} and {crewLocal2.ToString("0.00")}, "
                + $"wheel {(helmLocal + new Vector3(0f, 0.75f, wheelAhead)).ToString("0.00")}, cannon deck y {bowDeckY:0.00}.");
        }

        /// <summary>The steering wheel on a pivot "Wheel" whose forward axis is the wheel's axle, facing south (toward the helm).</summary>
        static void BuildWheel(CoveLayout c, Transform shipRoot, Vector3 localPosition)
        {
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(shipRoot, false);
            pivot.localPosition = localPosition;
            c.Wheel = pivot;

            GameObject prefab = LoadPrefab(WheelPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, pivot);
                model.name = "WheelModel";
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                // The axle is the thinnest side. Turn the model so that side runs along the pivot's Z.
                if (TryGetBounds(model, out Bounds bounds))
                {
                    Vector3 size = bounds.size;
                    if (size.y <= size.x && size.y <= size.z) model.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    else if (size.x <= size.y && size.x <= size.z) model.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    TryGetBounds(model, out bounds);
                    float diameter = Mathf.Max(bounds.size.x, bounds.size.y);
                    if (diameter > 0.01f) model.transform.localScale *= 0.8f / diameter;
                    TryGetBounds(model, out bounds);
                    model.transform.position += pivot.position - bounds.center;
                }
            }
            else
            {
                Debug.LogWarning("CoolSchool pirate cove: the steering wheel prefab is missing; the ship has a plain disc wheel.");
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "WheelModel";
                Object.DestroyImmediate(disc.GetComponent<Collider>());
                disc.transform.SetParent(pivot, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localScale = new Vector3(0.8f, 0.03f, 0.8f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = LitMaterial("PirateCannon", new Color(0.35f, 0.22f, 0.12f));
            }

            // Facing south, so the kid at the helm (looking north) sees its face.
            pivot.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        }

        /// <summary>A primitive cannon at the bow: a box base, two wheels, and a barrel (pivot "Barrel") with a muzzle at its tip, pointing north.</summary>
        static void BuildCannon(CoveLayout c, Transform shipRoot, Bounds shipBounds, Vector3 shipPosition, float deckY)
        {
            Material dark = LitMaterial("PirateCannon", new Color(0.18f, 0.18f, 0.2f), 0.45f, 0.4f);
            float bowV = shipBounds.max.z - shipPosition.z;
            var cannon = new GameObject("Cannon").transform;
            cannon.SetParent(shipRoot, false);
            cannon.localPosition = new Vector3(shipBounds.center.x - shipPosition.x, deckY, bowV - 1.1f);

            Primitive(PrimitiveType.Cube, "Base", cannon, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.24f, 0.9f), Quaternion.identity, dark);
            foreach (float side in new[] { -1f, 1f })
                Primitive(PrimitiveType.Cylinder, "Wheel", cannon, new Vector3(side * 0.34f, 0.17f, 0f), new Vector3(0.34f, 0.04f, 0.34f),
                    Quaternion.Euler(0f, 0f, 90f), dark);

            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(cannon, false);
            barrel.localPosition = new Vector3(0f, 0.45f, -0.2f);
            barrel.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            // A cylinder stands along Y and is 2 m tall at scale 1, so half the wanted length as Y scale; turned to lie along Z.
            Primitive(PrimitiveType.Cylinder, "BarrelMesh", barrel, new Vector3(0f, 0f, 0.45f), new Vector3(0.28f, 0.45f, 0.28f),
                Quaternion.Euler(90f, 0f, 0f), dark);
            Primitive(PrimitiveType.Cylinder, "BarrelRim", barrel, new Vector3(0f, 0f, 0.88f), new Vector3(0.34f, 0.04f, 0.34f),
                Quaternion.Euler(90f, 0f, 0f), dark);

            c.CannonBarrel = barrel;
            c.CannonMuzzle = NewLocalPoint("Muzzle", barrel, new Vector3(0f, 0f, 0.95f));
        }

        // ---- Island ----

        /// <summary>The island: one round sand disc scaled into an oval, a hand above the sand so it stands out of the sand pit.</summary>
        static void BuildIsland(CoveLayout c, Transform parent)
        {
            c.IslandCentre = new Vector3(c.CentreX, IslandTopY, c.SouthWest.z + IslandCentreV);
            Transform group = NewGroup("Island", parent);
            // The island's disc is ground for the things on it ("Floor" counts as ground for the grounding check).
            Transform floor = NewGroup("Floor", group);

            GameObject prefab = LoadPrefab(IslandPrefab);
            if (prefab != null)
            {
                GameObject island = Spawn(prefab, floor);
                if (TryGetBounds(island, out Bounds bounds) && bounds.size.x > 0.01f && bounds.size.z > 0.01f)
                {
                    Vector3 scale = island.transform.localScale;
                    scale.x *= IslandWidth / bounds.size.x;
                    scale.z *= IslandDepth / bounds.size.z;
                    // A thick disc would pile up too high; keep it a low mound.
                    if (bounds.size.y > 0.4f) scale.y *= 0.3f / bounds.size.y;
                    island.transform.localScale = scale;
                    // Its walkable top is where most of the surface is, not the bounds top (bumps stick up): measure it and put it at IslandTopY.
                    GroundSurface.Measurement surface = GroundSurface.Measure(island);
                    float bump = surface.Valid ? Mathf.Max(0f, surface.BumpHeight) : 0f;
                    Debug.Log("CoolSchool pirate cove: " + GroundSurface.Describe("island disc", surface));
                    AlignToBounds(island, c.IslandCentre.x, c.IslandCentre.z, IslandTopY + bump, true);
                    FinishEnvironmentObject(island);

                    // The mound is lumpy (only about a third of it is at the surface height), so kids and the NavMesh get a flat oval
                    // top at IslandTopY instead of every bump, and the landmarks stand exactly on it.
                    foreach (Collider collider in island.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    var flat = new GameObject("FlatTop");
                    flat.transform.SetParent(floor, false);
                    const float flatThickness = 0.3f;
                    // Scaled by the cylinder mesh's measured size (so it is exactly IslandWidth x IslandDepth wide), centre on the middle.
                    Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                    Vector3 meshSize = cylinder.bounds.size;
                    flat.transform.localScale = new Vector3(IslandWidth / Mathf.Max(0.01f, meshSize.x), flatThickness / Mathf.Max(0.01f, meshSize.y), IslandDepth / Mathf.Max(0.01f, meshSize.z));
                    flat.transform.position = new Vector3(c.IslandCentre.x, IslandTopY - flatThickness * 0.5f, c.IslandCentre.z) - Vector3.Scale(cylinder.bounds.center, flat.transform.localScale);
                    var flatCollider = flat.AddComponent<MeshCollider>();
                    flatCollider.sharedMesh = cylinder;
                    flatCollider.convex = true;
                    return;
                }
                Object.DestroyImmediate(island);
            }

            Debug.LogWarning("CoolSchool pirate cove: round sand prefab missing or unusable; the island is a plain sand cylinder.");
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "IslandDisc";
            disc.transform.SetParent(floor, false);
            disc.transform.localScale = new Vector3(IslandWidth, 0.1f, IslandDepth);
            disc.transform.position = new Vector3(c.IslandCentre.x, IslandTopY - 0.1f, c.IslandCentre.z);
            disc.GetComponent<MeshRenderer>().sharedMaterial = LitMaterial("PirateSand", new Color(0.93f, 0.82f, 0.55f), 0.1f);
            FinishEnvironmentObject(disc);
        }

        // ---- Landmarks, dig spot and chest ----

        /// <summary>Four landmarks on the island: a big flag, a sandcastle, a big rock and a telescope on a barrel. Static.</summary>
        static void BuildLandmarks(CoveLayout c, Transform parent)
        {
            Transform group = NewGroup("Landmarks", parent);
            c.LandmarkPrefabs = new[] { FlagPrefab, SandCastlePrefab, RockPrefab, TelescopePrefab };
            c.Landmarks = new Transform[LandmarkSpots.Length];

            string[] names = { "Landmark_Flag", "Landmark_SandCastle", "Landmark_Rock", "Landmark_Telescope" };
            for (int i = 0; i < LandmarkSpots.Length; i++)
            {
                Vector3 spot = SpotAt(c, LandmarkSpots[i], IslandTopY);
                c.Landmarks[i] = NewPoint(names[i], group, spot);
            }

            // Everything faces the camera (south), so the kid sees the front.
            PlaceProp(FlagPrefab, c.Landmarks[0], 180f, SizeBy.Height, 3f, c.Landmarks[0].position, IslandTopY, true);
            PlaceProp(SandCastlePrefab, c.Landmarks[1], 180f, SizeBy.Widest, 1.6f, c.Landmarks[1].position, IslandTopY, true);
            PlaceProp(RockPrefab, c.Landmarks[2], 25f, SizeBy.Widest, 2f, c.Landmarks[2].position, IslandTopY - 0.02f, true);

            // The telescope stands on a barrel.
            Vector3 telescopeSpot = c.Landmarks[3].position;
            GameObject barrel = PlaceProp(BarrelPrefab, c.Landmarks[3], 0f, SizeBy.Height, 0.9f, telescopeSpot, IslandTopY, true);
            float topY = IslandTopY + 0.9f;
            if (barrel != null && TryGetBounds(barrel, out Bounds barrelBounds)) topY = barrelBounds.max.y;
            PlaceProp(TelescopePrefab, c.Landmarks[3], 180f, SizeBy.Widest, 1f, telescopeSpot, topY, true);
        }

        /// <summary>
        /// The dig spot near the island's north middle with a hidden red X made of two flat boxes, and the chest buried under it
        /// (the runtime TreasureChest lifts it out). The coin spots form a fan south of the chest, on the camera side.
        /// </summary>
        static void BuildTreasureSpot(CoveLayout c, Transform parent)
        {
            Vector3 spot = SpotAt(c, new Vector2(0f, 32.4f), IslandTopY);
            Transform group = NewGroup("Treasure", parent);

            c.DigSpot = NewPoint("DigSpot", group, spot);
            Material red = LitMaterial("PirateRedX", new Color(0.9f, 0.1f, 0.08f), 0.2f, 0f, 0.5f);
            var x = new GameObject("RedX").transform;
            x.SetParent(c.DigSpot, false);
            x.localPosition = new Vector3(0f, 0.03f, 0f);
            foreach (float angle in new[] { 45f, -45f })
                Primitive(PrimitiveType.Cube, "Bar", x, Vector3.zero, new Vector3(1.1f, 0.03f, 0.2f), Quaternion.Euler(0f, angle, 0f), red);
            x.gameObject.SetActive(false);
            c.RedX = x.gameObject;

            // The chest, turned to face the camera, standing at the surface for now (the runtime sinks it by its buried depth).
            c.Chest = NewPoint("TreasureChest", group, spot);
            c.Chest.rotation = Quaternion.identity;
            GameObject prefab = LoadPrefab(ChestPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, c.Chest);
                model.name = "ChestModel";
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                if (TryGetBounds(model, out Bounds bounds) && bounds.size.x > 0.01f)
                    model.transform.localScale *= ChestWidth / bounds.size.x;
                AlignToBounds(model, spot.x, spot.z, spot.y, false);

                c.ChestLid = FindDeep(model.transform, "Lid");
                if (c.ChestLid == null)
                    Debug.LogWarning("CoolSchool pirate cove: the chest model has no child named '*Lid*'; the chest will open with a pop instead of a lid.");

                // A gold box padlock on the front (south side), a little below the lid line.
                if (TryGetBounds(model, out bounds))
                {
                    Material gold = LitMaterial("PirateLock", new Color(1f, 0.78f, 0.15f), 0.7f, 0.8f, 0.25f);
                    var padlock = new GameObject("Padlock").transform;
                    padlock.SetParent(c.Chest, false);
                    padlock.position = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.6f, bounds.min.z - 0.03f);
                    Primitive(PrimitiveType.Cube, "LockBody", padlock, Vector3.zero, new Vector3(0.2f, 0.18f, 0.1f), Quaternion.identity, gold);
                    Primitive(PrimitiveType.Cylinder, "LockShackle", padlock, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.03f, 0.12f),
                        Quaternion.Euler(90f, 0f, 0f), gold);
                    c.LockVisual = padlock.gameObject;
                }
            }
            else
            {
                Debug.LogWarning("CoolSchool pirate cove: the chest prefab is missing, so there is no treasure to dig up.");
            }

            // Ten coin spots in a fan south of the chest: five near, five further out, between them 25 degrees apart.
            var spots = new List<Transform>();
            Transform fan = NewGroup("CoinSpots", group);
            float[] nearAngles = { -60f, -30f, 0f, 30f, 60f };
            float[] farAngles = { -48f, -24f, 0f, 24f, 48f };
            for (int i = 0; i < 10; i++)
            {
                bool far = i >= 5;
                float angle = (far ? farAngles[i - 5] : nearAngles[i]) * Mathf.Deg2Rad;
                float radius = far ? 1.9f : 1.15f;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * radius;
                spots.Add(NewPoint($"CoinSpot_{i + 1}", fan, spot + offset + Vector3.up * 0.12f));
            }
            c.CoinSpots = spots.ToArray();
        }

        // ---- Skeleton boat and trophy ----

        /// <summary>
        /// The skeleton boat: the cardboard boat (bow along the root's +Z) with four skeleton seats, seven balloon sticks and
        /// spheres, and its path points. Inactive at the end. The path points are not children of the boat.
        /// </summary>
        static void BuildSkeletonBoat(CoveLayout c, Transform parent)
        {
            Transform points = NewGroup("BoatPoints", parent);
            float v = c.SouthWest.z;
            c.BoatStart = NewPoint("BoatStart", points, new Vector3(c.Pit.xMin + c.EdgeWidth + 1.2f, SeaY, v + DriftV));
            c.BoatDriftLeft = NewPoint("BoatDriftLeft", points, new Vector3(c.CentreX - 5f, SeaY, v + DriftV));
            c.BoatDriftRight = NewPoint("BoatDriftRight", points, new Vector3(c.CentreX + 5f, SeaY, v + DriftV));
            c.BoatIslandBeach = NewPoint("BoatIslandBeach", points, new Vector3(c.CentreX - 8.0f, SeaY, v + 26.2f));

            var root = new GameObject("SkeletonBoat").transform;
            root.SetParent(parent, false);
            root.position = c.BoatStart.position;
            c.Boat = root;

            float deckY = 0.2f;
            Vector3[] boatSeats = null;
            float lengthScale = 1f;
            GameObject prefab = LoadPrefab(BoatPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, root);
                model.name = "BoatModel";
                model.transform.localPosition = Vector3.zero;
                bool hadColliders = model.GetComponentInChildren<Collider>(true) != null;
                if (!hadColliders) AddMeshCollidersIfNone(model);

                float yaw = DetectBowYaw(model, "skeleton boat");
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (TryGetBounds(model, out Bounds bounds) && Mathf.Max(bounds.size.x, bounds.size.z) > 0.01f)
                    model.transform.localScale *= BoatLength / Mathf.Max(bounds.size.x, bounds.size.z);
                AlignToBounds(model, c.BoatStart.position.x, c.BoatStart.position.z, SeaY - 0.05f, false);
                TryGetBounds(model, out bounds);
                Physics.SyncTransforms();
                BoatFloor floor;
                using (var probes = new ProbeSet(model))
                    floor = FindBoatFloor(probes, bounds);
                if (floor.Found)
                {
                    deckY = floor.Y;
                    boatSeats = floor.Seats.Select(p => p - root.position).ToArray();
                }
                else
                {
                    // No seat cells found: stand on the measured floor, or just above the bottom of the hull if even that failed.
                    deckY = floor.FloorKnown ? floor.Y : bounds.min.y + 0.2f;
                }
                Debug.Log($"CoolSchool pirate cove: skeleton boat bounds {bounds.size.ToString("0.00")}; "
                    + (floor.Found ? "" : "floor not found, using bottom + 0.2 m: ") + floor.Report + ".");
                lengthScale = bounds.size.z / BoatLength;

                // The boat moves: its own colliders would only get in the way of the kids and the cannon ball.
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                deckY -= root.position.y;
            }
            else
            {
                Debug.LogWarning("CoolSchool pirate cove: the cardboard boat prefab is missing; the skeletons sail on thin air with their balloons.");
            }

            // Skeleton seats along the middle of the deck.
            c.SkeletonSeats = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 seat = boatSeats != null
                    ? boatSeats[i]
                    : new Vector3(0f, deckY, Mathf.Lerp(-1.4f, 1.4f, i / 3f) * lengthScale);
                c.SkeletonSeats[i] = NewLocalPoint($"SkeletonSeat_{i + 1}", root, seat);
            }

            // Seven balloons on sticks, spread along the boat and at three heights.
            Color[] colours =
            {
                new Color(1f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.85f, 0.3f),
                new Color(0.2f, 0.55f, 1f), new Color(0.65f, 0.3f, 0.95f), new Color(1f, 0.45f, 0.75f),
            };
            Material stickMaterial = LitMaterial("PirateStick", new Color(0.5f, 0.35f, 0.2f));
            c.Balloons = new GameObject[BalloonSlots];
            c.BalloonRenderers = new Renderer[BalloonSlots];
            for (int i = 0; i < BalloonSlots; i++)
            {
                float t = i / (float)(BalloonSlots - 1);
                float z = Mathf.Lerp(-1.8f, 1.8f, t) * lengthScale;
                float x = i % 2 == 0 ? -0.4f : 0.4f;
                float stickHeight = 1.2f + (i % 3) * 0.35f;

                var slot = new GameObject($"BalloonSlot_{i + 1}").transform;
                slot.SetParent(root, false);
                slot.localPosition = new Vector3(x, deckY, z);

                Primitive(PrimitiveType.Cylinder, "Stick", slot, new Vector3(0f, stickHeight * 0.5f, 0f), new Vector3(0.05f, stickHeight * 0.5f, 0.05f),
                    Quaternion.identity, stickMaterial);
                Material balloonMaterial = LitMaterial($"PirateBalloon_{i}", colours[i], 0.7f, 0f, 0.3f);
                Transform sphere = Primitive(PrimitiveType.Sphere, "Balloon", slot, new Vector3(0f, stickHeight + 0.3f, 0f), Vector3.one * 0.6f,
                    Quaternion.identity, balloonMaterial);
                c.Balloons[i] = slot.gameObject;
                c.BalloonRenderers[i] = sphere.GetComponent<MeshRenderer>();
            }
        }

        /// <summary>The trophy chest at the dock's west end, visible from the entrance, with four coin tiers (shown by progress at runtime).</summary>
        static void BuildTrophy(CoveLayout c, Transform parent)
        {
            Vector3 spot = new Vector3(c.CentreX - 4.6f, 0f, c.SouthWest.z + 3.8f);
            float floorY = c.DockTopY;
            c.Trophy = NewPoint("TrophyChest", parent, new Vector3(spot.x, floorY, spot.z));

            GameObject prefab = LoadPrefab(ChestPrefab);
            if (prefab != null)
            {
                GameObject model = Spawn(prefab, c.Trophy);
                model.name = "TrophyModel";
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                if (TryGetBounds(model, out Bounds bounds) && bounds.size.x > 0.01f)
                    model.transform.localScale *= ChestWidth / bounds.size.x;
                AlignToBounds(model, spot.x, spot.z, floorY, false);
                c.TrophyLid = FindDeep(model.transform, "Lid");

                if (TryGetBounds(model, out bounds))
                {
                    var box = c.Trophy.gameObject.AddComponent<BoxCollider>();
                    box.center = c.Trophy.InverseTransformPoint(bounds.center);
                    box.size = bounds.size;
                }
            }

            // Tier i appears at pirateCoinsTotal >= thresholds[i]: a stack, a pile, another pile, a big stack, around the chest's front.
            PrefabRef[] tierPrefabs = { CoinStackPrefabA, CoinPilePrefabA, CoinPilePrefabB, CoinStackPrefabB };
            Vector2[] tierOffsets = { new Vector2(-0.85f, -0.55f), new Vector2(0.85f, -0.55f), new Vector2(-1.15f, 0.2f), new Vector2(1.15f, 0.2f) };
            c.TrophyTiers = new GameObject[tierPrefabs.Length];
            for (int i = 0; i < tierPrefabs.Length; i++)
            {
                Vector3 p = spot + new Vector3(tierOffsets[i].x, 0f, tierOffsets[i].y);
                GameObject tier = PlaceProp(tierPrefabs[i], c.Trophy, 0f, SizeBy.Widest, 0.5f, p, floorY, false);
                if (tier == null) continue;
                tier.name = $"Tier_{i + 1}";
                foreach (Collider collider in tier.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                tier.SetActive(false);
                c.TrophyTiers[i] = tier;
            }
        }

        /// <summary>Landing and party points on the island's south beach, and a spot for the crew and the parrot at the dock.</summary>
        static void BuildLandingPoints(CoveLayout c, Transform parent)
        {
            Transform group = NewGroup("AdventurePoints", parent);

            float landingV = IslandDockV + c.ShipHalfLength + 1.5f;
            c.IslandLandingPoint = NewPoint("IslandLandingPoint", group, SpotAt(c, new Vector2(0f, landingV), IslandTopY));
            c.IslandLandingPoint.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // The crew lands east of the player, the skeletons west of him and a little north: all in view of the camera.
            // Nine skeleton spots: 1-4 on the west side for the first wave (which uses the first 3 or 4), 5-9 for the second wave
            // (3, 4 or 5) spread north and east. All of them are north of the player.
            c.CrewLandingPoints = new[]
            {
                NewPoint("CrewLanding_1", group, SpotAt(c, new Vector2(1.8f, landingV + 0.5f), IslandTopY)),
                NewPoint("CrewLanding_2", group, SpotAt(c, new Vector2(3.3f, landingV + 0.1f), IslandTopY)),
            };
            c.SkeletonLandingPoints = new[]
            {
                NewPoint("SkeletonLanding_1", group, SpotAt(c, new Vector2(-3.4f, landingV + 2.2f), IslandTopY)),
                NewPoint("SkeletonLanding_2", group, SpotAt(c, new Vector2(-5.4f, landingV + 1.6f), IslandTopY)),
                NewPoint("SkeletonLanding_3", group, SpotAt(c, new Vector2(-4.2f, landingV + 3.8f), IslandTopY)),
                NewPoint("SkeletonLanding_4", group, SpotAt(c, new Vector2(-6.4f, landingV + 3.4f), IslandTopY)),
                NewPoint("SkeletonLanding_5", group, SpotAt(c, new Vector2(-1.4f, landingV + 3.4f), IslandTopY)),
                NewPoint("SkeletonLanding_6", group, SpotAt(c, new Vector2(1.6f, landingV + 4.6f), IslandTopY)),
                NewPoint("SkeletonLanding_7", group, SpotAt(c, new Vector2(4.4f, landingV + 3.2f), IslandTopY)),
                NewPoint("SkeletonLanding_8", group, SpotAt(c, new Vector2(6.2f, landingV + 2.0f), IslandTopY)),
                NewPoint("SkeletonLanding_9", group, SpotAt(c, new Vector2(-2.6f, landingV + 5.4f), IslandTopY)),
            };
            foreach (Transform point in c.CrewLandingPoints.Concat(c.SkeletonLandingPoints))
                point.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // Eight party spots in a row on the beach, facing the camera, and a second row of four staggered behind them (north of
            // them, between two spots of the front row), so all twelve dancers (player, crew and nine skeletons) are in view.
            c.PartyPoints = new Transform[12];
            for (int i = 0; i < 8; i++)
            {
                c.PartyPoints[i] = NewPoint($"PartySpot_{i + 1}", group, SpotAt(c, new Vector2(-4.2f + i * 1.2f, landingV + 1.7f), IslandTopY));
                c.PartyPoints[i].rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }
            for (int i = 0; i < 4; i++)
            {
                c.PartyPoints[8 + i] = NewPoint($"PartySpotBack_{i + 1}", group, SpotAt(c, new Vector2(-3.6f + i * 2.4f, landingV + 3.5f), IslandTopY));
                c.PartyPoints[8 + i].rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }

            // The crew and the parrot wait at the dock beside the zone.
            c.CrewSpot = new Vector3(c.CentreX - 2.2f, SandY, c.SouthWest.z + 3.2f);
            c.ParrotSpot = new Vector3(c.CentreX + 1.5f, SandY + 1.6f, c.SouthWest.z + 3.4f);
        }

        // ----- Helpers -------------------------------------------------------------------------

        /// <summary>World position: u metres east of the cove's middle, v metres north of its south edge.</summary>
        static Vector3 SpotAt(CoveLayout c, Vector2 uv, float y)
        {
            return new Vector3(c.CentreX + uv.x, y, c.SouthWest.z + uv.y);
        }

        internal enum SizeBy { None, Height, Widest }

        /// <summary>
        /// A prefab uniformly scaled so its height or widest side is targetSize (None keeps its size), turned by yaw and stood on
        /// footY with the middle of its footprint at the point. Null if the prefab is missing.
        /// </summary>
        static GameObject PlaceProp(PrefabRef reference, Transform parent, float yaw, SizeBy sizeBy, float targetSize, Vector3 point, float footY, bool finish)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject go = Spawn(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (sizeBy != SizeBy.None && TryGetBounds(go, out Bounds bounds))
            {
                float measured = sizeBy == SizeBy.Height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z);
                if (measured > 0.01f) go.transform.localScale *= Mathf.Clamp(targetSize / measured, 0.05f, 20f);
            }
            AlignToBounds(go, point.x, point.z, footY, false);
            if (finish) FinishEnvironmentObject(go);
            return go;
        }

        /// <summary>A primitive in the parent's frame, without its collider (unless asked to keep it).</summary>
        static Transform Primitive(PrimitiveType type, string objectName, Transform parent, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, Material material, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        static Transform NewPoint(string pointName, Transform parent, Vector3 worldPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.position = worldPosition;
            return point;
        }

        static Transform NewLocalPoint(string pointName, Transform parent, Vector3 localPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        /// <summary>The first descendant whose name contains the text (case-insensitive).</summary>
        internal static Transform FindDeep(Transform root, string text)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }

        /// <summary>Gives every mesh without a collider a MeshCollider; no static flags (the object may move).</summary>
        static void AddMeshCollidersIfNone(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        /// <summary>A flat URP/Lit material saved under Materials/Pirates (re-made on each build).</summary>
        internal static Material LitMaterial(string materialName, Color colour, float smoothness = 0.3f, float metallic = 0f, float emission = 0f)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Pirates");

            var material = new Material(lit) { name = materialName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            string path = MaterialFolder + "/" + materialName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ---- Measuring models with their colliders ----

        /// <summary>
        /// The yaw (degrees, about Y) that turns the model so its bow (the narrower end of its long side) points along +Z.
        /// Measured by casting rays at the model's colliders at both ends; if that tells nothing the model's own +Z is kept.
        /// </summary>
        static float DetectBowYaw(GameObject model, string label)
        {
            if (!TryGetBounds(model, out Bounds bounds)) return 0f;
            Physics.SyncTransforms();

            bool longIsZ = bounds.size.z >= bounds.size.x;
            float low = WidthAcross(model, bounds, longIsZ, 0.12f);
            float high = WidthAcross(model, bounds, longIsZ, 0.88f);
            bool bowAtHigh = true;
            string reason = "no taper found, keeping the model's own front";
            if (low > 0f && high > 0f)
            {
                if (high < low * 0.93f) { bowAtHigh = true; reason = "narrower at the high end"; }
                else if (low < high * 0.93f) { bowAtHigh = false; reason = "narrower at the low end"; }
            }

            float yaw = longIsZ ? (bowAtHigh ? 0f : 180f) : (bowAtHigh ? -90f : 90f);
            Debug.Log($"CoolSchool pirate cove: {label} bow detected ({(longIsZ ? "long along Z" : "long along X")}, widths {low:0.00} / {high:0.00}: {reason}) -> yaw {yaw:0}.");
            return yaw;
        }

        /// <summary>The model's width across its long axis at a station along it (0 = low end, 1 = high end), using the widest of four heights.</summary>
        static float WidthAcross(GameObject model, Bounds bounds, bool longIsZ, float t)
        {
            Collider[] colliders = model.GetComponentsInChildren<Collider>(false);
            if (colliders.Length == 0) return 0f;

            float along = longIsZ ? Mathf.Lerp(bounds.min.z, bounds.max.z, t) : Mathf.Lerp(bounds.min.x, bounds.max.x, t);
            float span = (longIsZ ? bounds.size.x : bounds.size.z) + 2f;
            Vector3 direction = longIsZ ? Vector3.right : Vector3.forward;
            float best = 0f;

            for (int k = 1; k <= 4; k++)
            {
                float y = Mathf.Lerp(bounds.min.y, bounds.max.y, k / 5f);
                Vector3 start = longIsZ
                    ? new Vector3(bounds.min.x - 1f, y, along)
                    : new Vector3(along, y, bounds.min.z - 1f);

                float first = FirstHit(colliders, new Ray(start, direction), span);
                float second = FirstHit(colliders, new Ray(start + direction * span, -direction), span);
                if (first >= 0f && second >= 0f) best = Mathf.Max(best, span - first - second);
            }
            return best;
        }

        static float FirstHit(Collider[] colliders, Ray ray, float distance)
        {
            float nearest = -1f;
            foreach (Collider collider in colliders)
            {
                if (collider.Raycast(ray, out RaycastHit hit, distance) && (nearest < 0f || hit.distance < nearest))
                    nearest = hit.distance;
            }
            return nearest;
        }

        /// <summary>The height of the model's surface under the point, found by casting down from 75% of its height. The fallback if nothing is hit.</summary>
        static float FindDeckHeight(GameObject model, Bounds bounds, float x, float z, float fallback)
        {
            Collider[] colliders = model.GetComponentsInChildren<Collider>(false);
            var ray = new Ray(new Vector3(x, bounds.min.y + bounds.size.y * 0.75f, z), Vector3.down);
            float distance = FirstHit(colliders, ray, bounds.size.y);
            if (distance >= 0f) return ray.origin.y - distance;

            Debug.LogWarning($"CoolSchool pirate cove: no deck found under ({x:0.0}, {z:0.0}) on {model.name}; using height {fallback:0.00}.");
            return fallback;
        }
    }
}
