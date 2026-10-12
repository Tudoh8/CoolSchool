using System.Collections.Generic;
using System.Linq;
using CoolSchool.Activities;
using CoolSchool.Battle;
using CoolSchool.Input;
using CoolSchool.Player;
using CoolSchool.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Builds the knights arena at the far end of the playground (east, +X): a big walk-in cardboard castle
    /// (the PolygonKids castle pieces scaled 5x: four corner towers, a front wall with a gate that faces the camera,
    /// two side walls and a back wall with goblin ladders leaning against its outside), imagination props (flags, braziers
    /// and the ladders), the ladder/dragon points, the glowing castle zone, the opponent pool, the dragon and the KnightsGame that
    /// ties it all together. The whole battle happens in the courtyard; the front wall turns see-through
    /// (CastleCutaway) while the kid is inside, and the dragon flies in over the back wall.
    /// The camera always looks north (+Z) and the arena lies east, so the castle is laid out directly in world X/Z:
    /// the gate wall is the south one, the goblin ladders are on the north wall. Only the ArenaLayout axis helpers
    /// (which the alien field reuses for its mirror-image field) stay axis-generic.
    /// Two steps, because the NavMesh must be baked after the castle exists but before any character:
    /// BuildCastle (static geometry) and Build (everything alive).
    /// </summary>
    public static class KnightsArenaBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string MeshFolder = RootFolder + "/Meshes";
        const string ZoneRingMaterialPath = RootFolder + "/Materials/CastleZoneRing.mat";
        const string TagZoneRingMaterialPath = RootFolder + "/Materials/TagZoneRing.mat";
        const string FireWarningMaterialPath = RootFolder + "/Materials/FireWarning.mat";
        const string TelegraphMaterialPath = RootFolder + "/Materials/Telegraph.mat";
        const string FireWarningMeshPath = MeshFolder + "/FireWarningFan.asset";

        const string CastleGhostMaterialPath = RootFolder + "/Materials/CastleGhost.mat";

        // Castle layout. The pieces are PolygonKids cardboard castle pieces, scaled up. Their pivots are at the bottom
        // centre and "front" is local +Z. Piece 04 (wall panel) is long along local Z; 05 (gate) and 06 (door) are wide along X.
        const float CastleScale = 5f;
        /// <summary>Gap between the castle's north edge (tower faces) and the north fence.</summary>
        const float CastleNorthMargin = 1.5f;
        /// <summary>Gap between the castle's east edge and the east fence; the rest of the arena's width is room on the playground side.</summary>
        const float CastleEastMargin = 2f;
        /// <summary>Panels are stretched this much (metres) beyond their slot so neighbours overlap instead of leaving gaps.</summary>
        const float PanelOverlap = 0.04f;
        /// <summary>Half the width of the gate's arch opening, as a fraction of the gate piece's width (0.63 m of 1.27 m at 1x).</summary>
        const float GateOpeningHalfFraction = 0.25f;
        /// <summary>
        /// The gate piece is laid out (width, spans, gate position) from its 5x, 180 degree measurement, then, as tweaked by hand
        /// in the scene, shown at 6x and unturned with its pivot kept where it was.
        /// </summary>
        const float GateFinalScale = 6f;
        /// <summary>The portcullis (the door piece in the gate arch): scale on top of the piece's own, and its offset from the gate's pivot (hand-tuned).</summary>
        static readonly Vector3 PortcullisScale = new Vector3(8f, 6.5f, 6f);
        const float PortcullisZOffset = -0.0281562f;
        /// <summary>Fallback for how high the arch opening is (3.3 m at 5x, so 3.96 m at 6x) when the gate mesh can't be measured.</summary>
        const float GateArchFallbackHeight = 3.96f;
        /// <summary>The raised portcullis' bottom ends this far above the top of the arch.</summary>
        const float PortcullisClearance = 0.05f;
        const float FlagHeight = 2.2f;
        /// <summary>A brazier (fire bowl on a stand) at this height reads as a torch beside the 5x cardboard gate.</summary>
        const float BrazierHeight = 1.4f;
        /// <summary>The braziers stand this far (metres) beside the gate's arch opening, and this far in front of the wall.</summary>
        const float BrazierGateClearance = 1.0f;
        const float BrazierFrontClearance = 1.2f;

        // Dragon.
        const float DragonEntryHeight = 9f;
        const float DragonHeight = 3.5f;
        const float FireRange = 5f;
        const float FireAngle = 50f;

        static readonly PrefabRef DragonPrefab = new PrefabRef("Dragon_LP", "Magic Pig");

        /// <summary>Corner towers in the order north-west, north-east, south-west, south-east.</summary>
        static readonly PrefabRef[] CastleTowers =
        {
            Kids("SM_Prop_Cardboard_Castle_Piece_01"),
            Kids("SM_Prop_Cardboard_Castle_Piece_02"),
            Kids("SM_Prop_Cardboard_Castle_Piece_03"),
            Kids("SM_Prop_Cardboard_Castle_Piece_01"),
        };
        static readonly PrefabRef CastlePanel = Kids("SM_Prop_Cardboard_Castle_Piece_04");
        static readonly PrefabRef CastleGate = Kids("SM_Prop_Cardboard_Castle_Piece_05");
        static readonly PrefabRef CastlePortcullis = Kids("SM_Prop_Cardboard_Castle_Piece_06");

        static readonly PrefabRef FlagA = Kids("SM_Prop_Flag_01");
        static readonly PrefabRef FlagB = Kids("SM_Prop_Flag_02");
        // Synty's SM_Prop_Torch_01 is a flashlight, so the gate "torches" are PolygonTown braziers.
        static readonly PrefabRef Brazier = new PrefabRef("SM_Prop_Brazier_01", "PolygonTown");

        // Goblins climb ladders up the outside of the back (north) wall. The first prefab that loads is used.
        const int LadderCount = 3;
        /// <summary>How far the ladder tops stick up above the wall, so the kid sees them from the camera side.</summary>
        const float LadderPoke = 0.65f;
        const float LadderLeanDegrees = 12f;
        /// <summary>The glowing start spot is this far in front of the courtyard's back wall, behind the dragon's landing spot and the goblins' ladder landings.</summary>
        const float CastleZoneBackOffset = 5.5f;
        /// <summary>Where everyone cheers after the dragon flew away: this far inside the gate, so the front wall stays see-through.</summary>
        const float CelebrateGateOffset = 2.5f;
        /// <summary>No ladder gets wider than this (metres), whatever the model's proportions are.</summary>
        const float LadderMaxWidth = 1.1f;
        /// <summary>The climber stands this far in front of the ladder's middle line (towards the outside).</summary>
        const float LadderClimberOffset = 0.12f;
        /// <summary>How far inside the back wall's inner face a goblin lands after jumping down.</summary>
        const float LadderLandingDistance = 1.8f;
        static readonly PrefabRef[] LadderPrefabs =
        {
            new PrefabRef("SM_Prop_Ladder_01", "PolygonTown"),
            new PrefabRef("SM_Gen_Bld_Ladder_01", "PolygonGeneric"),
        };

        static readonly PrefabRef[] OpponentPrefabs =
        {
            Kids("SM_Chr_Kid_Goblin_01"),
            Kids("SM_Chr_Kid_Goblin_02"),
            Kids("SM_Chr_Kid_Troll_01"),
            Kids("SM_Chr_Kid_Goblin_01"),
        };

        static readonly PrefabRef[] OpponentClubs =
        {
            Kids("SM_Wep_Goblin_Club_01"),
            Kids("SM_Wep_Goblin_Club_01"),
            Kids("SM_Wep_Club_Inflatable_01"),
            Kids("SM_Wep_Goblin_Club_01"),
        };

        /// <summary>
        /// Numbers from the castle step that the later steps need. The castle itself is described in world X/Z; the
        /// axis helpers (Axis, Lateral, Point, AxisYaw) turn "along the arena, across it" pairs into world positions,
        /// which the alien field reuses for its mirror-image layout.
        /// </summary>
        internal sealed class ArenaLayout
        {
            public Transform Root;

            /// <summary>Flat unit vector from the playground into the arena (a cardinal direction).</summary>
            public readonly Vector3 Axis;

            /// <summary>Flat unit vector across the arena: to the right when looking along the axis.</summary>
            public readonly Vector3 Lateral;

            /// <summary>Where the arena begins and ends, as distances along the axis from the world origin.</summary>
            public float Start;
            public float End;

            /// <summary>The "Castle" group that holds every castle piece (the CastleCutaway component goes on it).</summary>
            public Transform CastleGroup;

            /// <summary>
            /// The castle in world X/Z: the whole footprint (towers included) and the courtyard inside the four walls
            /// (any height; used as the cutaway's inside area).
            /// </summary>
            public Bounds Footprint;
            public Bounds Courtyard;

            /// <summary>The gate's centre on the ground.</summary>
            public Vector3 GatePosition;

            /// <summary>The goblin ladders against the back wall's outside, west to east.</summary>
            public readonly List<LadderSpot> Ladders = new List<LadderSpot>();

            /// <summary>The z of the back wall's outer (north) face.</summary>
            public float BackWallOuterZ;

            /// <summary>The front (south) wall's pieces and all their renderers: they go see-through while the kid is inside.</summary>
            public readonly List<GameObject> FrontPieces = new List<GameObject>();
            public readonly List<Renderer> FrontRenderers = new List<Renderer>();

            /// <summary>The portcullis in the gate arch (it rises when the kid comes near) and how far it rises, in metres.</summary>
            public GameObject Portcullis;
            public float PortcullisLift;

            public readonly List<GameObject> ImaginationProps = new List<GameObject>();

            public ArenaLayout(Vector3 axis)
            {
                axis.y = 0f;
                if (axis.sqrMagnitude < 0.0001f) axis = Vector3.forward;
                Axis = Mathf.Abs(axis.x) > Mathf.Abs(axis.z)
                    ? new Vector3(Mathf.Sign(axis.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(axis.z));
                Lateral = Vector3.Cross(Vector3.up, Axis);
            }

            /// <summary>The yaw (degrees) that turns a thing facing +Z to face along the axis.</summary>
            public float AxisYaw => Mathf.Atan2(Axis.x, Axis.z) * Mathf.Rad2Deg;

            /// <summary>The world position that is "along" metres along the axis and "across" metres to the right of it (y = 0).</summary>
            public Vector3 Point(float along, float across)
            {
                return Axis * along + Lateral * across;
            }
        }

        /// <summary>
        /// One goblin ladder as world points: the foot on the ground outside the wall, the shoulder (on the ladder, as high as the
        /// wall top), the rim (on top of the wall) and the landing inside the courtyard.
        /// </summary>
        internal sealed class LadderSpot
        {
            public Vector3 Foot;
            public Vector3 Shoulder;
            public Vector3 Rim;
            public Vector3 Landing;
        }

        /// <summary>What the second step wires into the KnightsGame.</summary>
        internal sealed class ArenaInputs
        {
            public RuntimeAnimatorController KidController;
            /// <summary>KidClub override (mace swings) for the opponents; null falls back to KidController.</summary>
            public RuntimeAnimatorController OpponentController;
            public RuntimeAnimatorController DragonController;
            public int KidsLayer = -1;
            public PlayerController Player;
            public PlayerBattleInput PlayerInput;
            public ActionButton ActionButton;
            public Sprite SwordIcon;
            public ImaginationEffect Imagination;
            public KnightAllyAI[] Allies;
            public WeaponHolder[] HeroWeaponHolders;
            public ParticleSystem FireConfettiPrefab;
        }

        internal sealed class ArenaResult
        {
            public bool DragonCreated;
            public bool DragonControllerMissing;
        }

        // ----- Step 1: static geometry ---------------------------------------------------------

        /// <summary>
        /// The big cardboard castle (pieces scaled 5x) plus the inactive imagination props (flags on the north towers,
        /// braziers at the gate, and the goblin ladders against the back wall's outside).
        /// The castle is a rectangle in world X/Z: corner towers, a south (front) wall of tower, panel, gate, panel, tower
        /// facing the camera, three stretched panels for each side wall, and stretched panels filling the back (north) wall.
        /// The towers' outer faces sit CastleNorthMargin inside the north fence (northFenceZ) and CastleEastMargin inside
        /// the east end of the arena (endDistance, which is the east fence because the arena lies along +X), so the playground
        /// side gets the extra room. The footprint and the approach strip in front of the gate are reserved (ReservedAreas),
        /// so no bench or tree ends up there. Pieces are placed by their measured bounds after scaling and turning.
        /// </summary>
        internal static ArenaLayout BuildCastle(Transform environmentRoot, Vector3 axis, float startDistance, float endDistance, float northFenceZ)
        {
            var layout = new ArenaLayout(axis) { Start = startDistance, End = endDistance };
            if (layout.Axis != Vector3.right)
                Debug.LogWarning($"CoolSchool: the castle is laid out for an arena along +X, but the axis is {layout.Axis.ToString("0.0")}.");

            layout.Root = NewGroup("Arena", environmentRoot);
            Transform castleGroup = NewGroup("Castle", layout.Root);
            Transform propsGroup = NewGroup("ImaginationProps", layout.Root);
            layout.CastleGroup = castleGroup;

            GameObject[] towerPrefabs = CastleTowers.Select(LoadPrefab).ToArray();
            GameObject panelPrefab = LoadPrefab(CastlePanel);
            GameObject gatePrefab = LoadPrefab(CastleGate);
            if (towerPrefabs.Any(t => t == null) || panelPrefab == null || gatePrefab == null)
                Debug.LogError("CoolSchool: some castle pieces are missing; the castle will have gaps (the layout uses the expected sizes).");

            // Measure the pieces at their final scale and turn (fallbacks are the 5x sizes of the real pieces).
            Quaternion southFacing = Quaternion.Euler(0f, 180f, 0f);
            var towerSize = Vector3.zero;
            foreach (GameObject towerPrefab in towerPrefabs)
                towerSize = Vector3.Max(towerSize, MeasureCastlePiece(towerPrefab, castleGroup, southFacing, new Vector3(2.85f, 5.47f, 2.85f)));
            Vector3 panelSize = MeasureCastlePiece(panelPrefab, castleGroup, Quaternion.identity, new Vector3(0.4f, 3.9f, 5f));
            Vector3 gateSize = MeasureCastlePiece(gatePrefab, castleGroup, southFacing, new Vector3(6.35f, 4.3f, 0.26f));

            // The panel is long along one local axis (Z for the real piece); its length fills the spans, its thickness sets the wall.
            bool panelLongZ = panelSize.z >= panelSize.x;
            float panelLength = Mathf.Max(panelSize.x, panelSize.z);
            float panelHalfThickness = Mathf.Min(panelSize.x, panelSize.z) * 0.5f;

            // The front wall (tower, panel, gate, panel, tower) sets the width, three side panels set the depth.
            float width = 2f * towerSize.x + 2f * panelLength + gateSize.x;
            float depth = 2f * towerSize.z + 3f * panelLength;
            float east = endDistance - CastleEastMargin;
            float west = east - width;
            float north = northFenceZ - CastleNorthMargin;
            float south = north - depth;
            float centreX = (west + east) * 0.5f;

            // The walls run through the middles of the corner towers.
            float wallWest = west + towerSize.x * 0.5f;
            float wallEast = east - towerSize.x * 0.5f;
            float wallSouth = south + towerSize.z * 0.5f;
            float wallNorth = north - towerSize.z * 0.5f;

            layout.Footprint = new Bounds(new Vector3(centreX, towerSize.y * 0.5f, (south + north) * 0.5f), new Vector3(width, towerSize.y, depth));
            Vector3 courtyardMin = new Vector3(wallWest + panelHalfThickness, 0f, wallSouth + panelHalfThickness);
            Vector3 courtyardMax = new Vector3(wallEast - panelHalfThickness, 20f, wallNorth - panelHalfThickness);
            layout.Courtyard = new Bounds((courtyardMin + courtyardMax) * 0.5f, courtyardMax - courtyardMin);
            layout.GatePosition = new Vector3(centreX, 0f, wallSouth);
            layout.BackWallOuterZ = wallNorth + panelHalfThickness;

            // Nothing from the scatter steps may stand on the castle (plus a margin) or in the way up to the gate.
            ReservedAreas.Add(new Rect(west - 2f, south - 2f, width + 4f, depth + 4f));
            float approachWidth = gateSize.x + 2f;
            ReservedAreas.Add(new Rect(centreX - approachWidth * 0.5f, south - 6f, approachWidth, 6f));

            // Corner towers: north-west, north-east, south-west, south-east.
            var towerSpots = new[]
            {
                new Vector2(wallWest, wallNorth), new Vector2(wallEast, wallNorth),
                new Vector2(wallWest, wallSouth), new Vector2(wallEast, wallSouth),
            };
            var towers = new GameObject[towerSpots.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                if (towerPrefabs[i] == null) continue;

                towers[i] = Spawn(towerPrefabs[i], castleGroup);
                towers[i].transform.localScale *= CastleScale;
                towers[i].transform.rotation = southFacing;
                AlignToBounds(towers[i], towerSpots[i].x, towerSpots[i].y, 0f, false);
                FinishEnvironmentObject(towers[i]);
            }

            // Front wall: panels on both sides of the gate. The gate's front is local +Z, so it turns to face the camera (south, -Z).
            float gateLeft = centreX - gateSize.x * 0.5f;
            float gateRight = centreX + gateSize.x * 0.5f;
            var frontPieces = new List<GameObject>();
            frontPieces.AddRange(FillSpan(panelPrefab, castleGroup, new Vector3(west + towerSize.x, 0f, wallSouth), new Vector3(gateLeft, 0f, wallSouth), panelLength, panelLongZ));
            frontPieces.AddRange(FillSpan(panelPrefab, castleGroup, new Vector3(gateRight, 0f, wallSouth), new Vector3(east - towerSize.x, 0f, wallSouth), panelLength, panelLongZ));
            if (gatePrefab != null)
            {
                GameObject gate = Spawn(gatePrefab, castleGroup);
                Vector3 gateBaseScale = gate.transform.localScale;
                gate.transform.localScale = gateBaseScale * CastleScale;
                gate.transform.rotation = southFacing;
                AlignToBounds(gate, centreX, wallSouth, 0f, false);
                // Hand tweak, kept: the piece is shown 6x and unturned, but its pivot stays where the 5x, 180 degree
                // placement put it (the layout above is based on that measurement, so nothing else moves).
                gate.transform.localScale = gateBaseScale * GateFinalScale;
                gate.transform.rotation = Quaternion.identity;
                // The prefab's MeshCollider points at a pre-generated convex hull asset ("Generated convex submesh"), and a
                // hull of a wall with an arch is a solid slab. Clearing "convex" on it is not enough (the mesh itself is the
                // hull), so drop the prefab's colliders and let FinishEnvironmentObject build them from the real render mesh:
                // the arch stays open for the player and the NavMesh.
                foreach (Collider collider in gate.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                FinishEnvironmentObject(gate);
                frontPieces.Add(gate);

                // The portcullis hangs in the arch; it rises when the kid comes near (CastlePortcullis).
                GameObject portcullis = PlacePortcullis(layout, castleGroup, gate, southFacing);
                if (portcullis != null) frontPieces.Add(portcullis);
            }
            frontPieces.AddRange(towers.Skip(2).Where(t => t != null));

            // Side walls (north to south between the towers) and the back wall (east to west).
            FillSpan(panelPrefab, castleGroup, new Vector3(wallWest, 0f, south + towerSize.z), new Vector3(wallWest, 0f, north - towerSize.z), panelLength, panelLongZ);
            FillSpan(panelPrefab, castleGroup, new Vector3(wallEast, 0f, south + towerSize.z), new Vector3(wallEast, 0f, north - towerSize.z), panelLength, panelLongZ);
            FillSpan(panelPrefab, castleGroup, new Vector3(west + towerSize.x, 0f, wallNorth), new Vector3(east - towerSize.x, 0f, wallNorth), panelLength, panelLongZ);

            // The front wall is see-through while the kid is inside: the camera must not pull in against it, so it goes on the
            // Ignore Raycast layer (colliders stay, so it still blocks walking and is still baked into the NavMesh).
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            foreach (GameObject piece in frontPieces)
            {
                SetLayerRecursive(piece, ignoreRaycast);
                layout.FrontRenderers.AddRange(piece.GetComponentsInChildren<Renderer>(true));
            }
            layout.FrontPieces.AddRange(frontPieces);

            PlaceImaginationProps(layout, propsGroup, towers[0], towers[1], gateSize.x, wallSouth - panelHalfThickness);

            // The goblins' ladders against the back wall's outside, spread between the two north towers.
            float backSpan = (east - towerSize.x) - (west + towerSize.x);
            PlaceLadders(layout, propsGroup, centreX, backSpan, wallNorth, panelHalfThickness, panelSize.y);
            return layout;
        }

        /// <summary>
        /// LadderCount ladders leaning against the outside of the back (north) wall, spread over its width, their tops sticking
        /// LadderPoke above the wall so the kid sees them from the camera side. They are imagination props (hidden in free play,
        /// shown during the battle), have no colliders (so they stay out of the NavMesh bake) and each one records the points a
        /// climbing goblin uses (LadderSpot).
        /// </summary>
        static void PlaceLadders(ArenaLayout layout, Transform parent, float centreX, float backSpan, float wallNorthZ, float wallHalfThickness, float wallHeight)
        {
            GameObject prefab = null;
            foreach (PrefabRef reference in LadderPrefabs)
            {
                prefab = LoadPrefab(reference);
                if (prefab != null) break;
            }
            if (prefab == null) Debug.LogWarning("CoolSchool: no ladder prefab found; the goblins' ladders are built from boxes.");

            float outerZ = wallNorthZ + wallHalfThickness;
            float innerZ = wallNorthZ - wallHalfThickness;
            float spacing = backSpan / (LadderCount + 1f) * 1.1f;
            float tan = Mathf.Tan(LadderLeanDegrees * Mathf.Deg2Rad);
            float slopeLength = (wallHeight + LadderPoke) / Mathf.Cos(LadderLeanDegrees * Mathf.Deg2Rad);

            for (int i = 0; i < LadderCount; i++)
            {
                float x = centreX + (i - (LadderCount - 1) * 0.5f) * spacing;

                // The wrapper carries the scale and the lean; the model inside is stood upright and square to the wall first.
                Transform wrapper = NewGroup($"GoblinLadder_{i + 1}", parent);
                GameObject model = prefab != null ? Spawn(prefab, wrapper) : CreateBoxLadder(wrapper);
                model.transform.localPosition = Vector3.zero;
                UprightLadder(model);

                if (!TryGetBounds(model, out Bounds upright) || upright.size.y < 0.01f)
                {
                    Debug.LogWarning("CoolSchool: the ladder model has no size; skipping this ladder.");
                    Object.DestroyImmediate(wrapper.gameObject);
                    continue;
                }

                float scaleY = slopeLength / upright.size.y;
                float scaleXZ = Mathf.Min(scaleY, LadderMaxWidth / Mathf.Max(0.01f, upright.size.x));
                wrapper.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                // Leaning with its top toward the wall (south).
                wrapper.rotation = Quaternion.Euler(-LadderLeanDegrees, 0f, 0f);

                // Feet on the ground, its top edge touching the wall's outer face.
                AlignToBounds(wrapper.gameObject, x, 0f, 0f, false);
                TryGetBounds(wrapper.gameObject, out Bounds leaning);
                wrapper.position += new Vector3(0f, 0f, outerZ + 0.02f - leaning.min.z);
                TryGetBounds(wrapper.gameObject, out Bounds placed);

                // The middle line of the leaning box (through its centre) is where a goblin climbs, a little in front of it.
                float lineZ(float y) => placed.center.z + (placed.center.y - y) * tan + LadderClimberOffset;
                layout.Ladders.Add(new LadderSpot
                {
                    Foot = new Vector3(x, 0f, lineZ(0f)),
                    Shoulder = new Vector3(x, wallHeight, lineZ(wallHeight)),
                    Rim = new Vector3(x, wallHeight, wallNorthZ),
                    Landing = new Vector3(x, 0f, innerZ - LadderLandingDistance),
                });

                HideProp(layout, wrapper.gameObject);
            }
        }

        /// <summary>Stands a ladder model upright (its longest side up) and turns it so its width runs along X and its thin side along Z.</summary>
        static void UprightLadder(GameObject model)
        {
            if (!TryGetBounds(model, out Bounds bounds)) return;

            Vector3 size = bounds.size;
            Quaternion fix = Quaternion.identity;
            if (size.x > size.y && size.x > size.z) fix = Quaternion.Euler(0f, 0f, 90f);
            else if (size.z > size.y && size.z > size.x) fix = Quaternion.Euler(90f, 0f, 0f);
            model.transform.rotation = fix;

            if (TryGetBounds(model, out bounds) && bounds.size.x < bounds.size.z)
                model.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * fix;
        }

        /// <summary>A plain ladder (two rails and rungs, 1 m wide, 3 m tall, upright and square to Z) for when no model loads.</summary>
        static GameObject CreateBoxLadder(Transform parent)
        {
            var root = new GameObject("BoxLadder");
            root.transform.SetParent(parent, false);
            Material wood = null;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null)
            {
                var woodMaterial = new Material(lit) { name = "LadderWood" };
                woodMaterial.SetColor("_BaseColor", new Color(0.55f, 0.36f, 0.18f));
                woodMaterial.SetFloat("_Smoothness", 0.1f);
                wood = SaveMaterial(woodMaterial, RootFolder + "/Materials/LadderWood.mat");
            }

            void Box(Vector3 position, Vector3 scale)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(box.GetComponent<Collider>());
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = position;
                box.transform.localScale = scale;
                if (wood != null) box.GetComponent<MeshRenderer>().sharedMaterial = wood;
            }

            Box(new Vector3(-0.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.1f));
            Box(new Vector3(0.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.1f));
            for (int i = 0; i < 9; i++)
                Box(new Vector3(0f, 0.3f + i * 0.3f, 0f), new Vector3(1f, 0.07f, 0.07f));
            return root;
        }

        /// <summary>
        /// The portcullis: castle piece 06 (a flat arched panel) standing in the gate arch, just in front of the gate wall,
        /// turned to face the camera. No colliders (so the gate stays walkable and out of the NavMesh) and not static (it
        /// moves at run time). Stores it and how far it must rise to clear the arch in the layout.
        /// </summary>
        static GameObject PlacePortcullis(ArenaLayout layout, Transform castleGroup, GameObject gate, Quaternion southFacing)
        {
            GameObject prefab = LoadPrefab(CastlePortcullis);
            if (prefab == null) return null;

            GameObject portcullis = Spawn(prefab, castleGroup);
            portcullis.name = "CastlePortcullis";
            portcullis.transform.localScale = Vector3.Scale(portcullis.transform.localScale, PortcullisScale);
            portcullis.transform.rotation = southFacing;
            Vector3 gateLocal = gate.transform.localPosition;
            portcullis.transform.localPosition = new Vector3(gateLocal.x, 0f, gateLocal.z + PortcullisZOffset);

            foreach (Collider collider in portcullis.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (Transform child in portcullis.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);

            float archTop = MeasureGateArchTop(gate);
            float bottom = TryGetBounds(portcullis, out Bounds bounds) ? bounds.min.y : 0f;
            layout.Portcullis = portcullis;
            layout.PortcullisLift = Mathf.Max(0.5f, archTop - bottom + PortcullisClearance);

            if (TryGetBounds(gate, out Bounds gateBounds))
                Debug.Log($"CoolSchool portcullis: arch top {archTop:0.00} m, panel {bounds.size.y:0.00} m tall, rises {layout.PortcullisLift:0.00} m "
                    + $"(raised top {bounds.max.y + layout.PortcullisLift:0.00} m, gate wall top {gateBounds.max.y:0.00} m).");
            return portcullis;
        }

        /// <summary>
        /// The height of the top of the gate's arch opening: the lowest vertex above the lower quarter of the wall in a
        /// narrow strip down the middle (the arch apex). Falls back to the 6x estimate when that isn't plausible.
        /// </summary>
        static float MeasureGateArchTop(GameObject gate)
        {
            if (!TryGetBounds(gate, out Bounds bounds)) return GateArchFallbackHeight;

            float halfStrip = bounds.size.x * 0.03f;
            float floor = bounds.min.y + bounds.size.y * 0.25f;
            float best = float.MaxValue;
            foreach (MeshFilter filter in gate.GetComponentsInChildren<MeshFilter>(false))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (Mathf.Abs(world.x - bounds.center.x) > halfStrip || world.y < floor) continue;
                    best = Mathf.Min(best, world.y);
                }
            }

            if (best > 2.5f && best < bounds.max.y - 0.3f) return best;

            Debug.LogWarning($"CoolSchool: could not measure the gate arch (got {best:0.00} m); using {GateArchFallbackHeight} m for the portcullis lift.");
            return GateArchFallbackHeight;
        }

        /// <summary>
        /// The size of a castle piece at its final scale and turn, measured on a temporary instance;
        /// the fallback is used when the prefab is missing.
        /// </summary>
        static Vector3 MeasureCastlePiece(GameObject prefab, Transform parent, Quaternion rotation, Vector3 fallback)
        {
            if (prefab == null) return fallback;

            GameObject probe = Spawn(prefab, parent);
            probe.transform.localScale *= CastleScale;
            probe.transform.rotation = rotation;
            Vector3 size = TryGetBounds(probe, out Bounds bounds) ? bounds.size : fallback;
            Object.DestroyImmediate(probe);
            return size;
        }

        /// <summary>
        /// Fills a straight wall span between two ground points with n = round(span / panelLength) panels, each stretched
        /// along its length so the span is exactly filled. Walls running north-south are yaw 0 and east-west ones yaw 90,
        /// because the real panel is long along its local Z.
        /// </summary>
        static List<GameObject> FillSpan(GameObject prefab, Transform parent, Vector3 from, Vector3 to, float panelLength, bool panelLongZ)
        {
            var panels = new List<GameObject>();
            Vector3 delta = to - from;
            float span = delta.magnitude;
            if (prefab == null || span < 0.1f) return panels;

            Vector3 direction = delta / span;
            bool eastWest = Mathf.Abs(direction.x) > Mathf.Abs(direction.z);
            float yaw = eastWest == panelLongZ ? 90f : 0f;

            int count = Mathf.Max(1, Mathf.RoundToInt(span / panelLength));
            float slot = span / count;
            float stretch = (slot + PanelOverlap) / panelLength;

            for (int i = 0; i < count; i++)
            {
                GameObject panel = Spawn(prefab, parent);
                Vector3 scale = panel.transform.localScale * CastleScale;
                if (panelLongZ) scale.z *= stretch;
                else scale.x *= stretch;
                panel.transform.localScale = scale;
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                Vector3 centre = from + direction * (slot * (i + 0.5f));
                AlignToBounds(panel, centre.x, centre.z, 0f, false);
                FinishEnvironmentObject(panel);
                panels.Add(panel);
            }
            return panels;
        }

        /// <summary>
        /// Flags on the two north towers and braziers on each side of the gate (outside).
        /// All face the camera (south) and start hidden.
        /// </summary>
        static void PlaceImaginationProps(ArenaLayout layout, Transform parent, GameObject towerNorthWest, GameObject towerNorthEast,
            float gateWidth, float frontFaceZ)
        {
            GameObject flagA = LoadPrefab(FlagA);
            GameObject flagB = LoadPrefab(FlagB);
            GameObject brazier = LoadPrefab(Brazier);

            Quaternion south = Quaternion.Euler(0f, 180f, 0f);

            void PlaceAt(GameObject prefab, float height, Vector3 spot)
            {
                GameObject prop = Spawn(prefab, parent);
                prop.transform.rotation = south;
                if (TryGetBounds(prop, out Bounds bounds) && bounds.size.y > 0.01f)
                    prop.transform.localScale *= height / bounds.size.y;
                AlignToBounds(prop, spot.x, spot.z, spot.y, false);
                HideProp(layout, prop);
            }

            void PlaceFlag(GameObject prefab, GameObject tower)
            {
                if (prefab == null || tower == null || !TryGetBounds(tower, out Bounds bounds)) return;
                PlaceAt(prefab, FlagHeight, new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            }

            PlaceFlag(flagA, towerNorthWest);
            PlaceFlag(flagB, towerNorthEast);

            if (brazier == null) return;

            // gateWidth is measured at CastleScale but the gate is shown at GateFinalScale, so the arch opening is wider than
            // the layout thinks; the braziers stand a clear step beside the real opening, in front of the wall.
            float gateX = layout.GatePosition.x;
            float openingHalf = gateWidth * (GateFinalScale / CastleScale) * GateOpeningHalfFraction;
            float gateSide = openingHalf + BrazierGateClearance;
            foreach (float side in new[] { -1f, 1f })
                PlaceAt(brazier, BrazierHeight, new Vector3(gateX + side * gateSide, 0f, frontFaceZ - BrazierFrontClearance));
        }

        /// <summary>Props are decoration only: no colliders, hidden until the imagination effect shows them.</summary>
        static void HideProp(ArenaLayout layout, GameObject prop)
        {
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            prop.SetActive(false);
            layout.ImaginationProps.Add(prop);
        }

        // ----- Step 2: everything alive --------------------------------------------------------

        /// <summary>
        /// Creates the points, the castle zone, the cutaway, the opponent pool, the dragon and the KnightsGame.
        /// Call after the NavMesh is baked and after the player and allies exist.
        /// </summary>
        internal static ArenaResult Build(ArenaLayout layout, ArenaInputs inputs)
        {
            var result = new ArenaResult();
            Transform arena = layout.Root != null ? layout.Root : new GameObject("Arena").transform;
            Transform points = NewGroup("Points", arena);

            // Goblins climb the ladders up the back wall's outside, jump down at the ladders' landing points and run home to them.
            // The dragon lands in the middle of the courtyard (a little north of centre) and starts high up behind the back wall,
            // so it flies in over it.
            float courtyardX = layout.Courtyard.center.x;
            WallLadder[] ladders = CreateLadders(layout, points);
            Vector3[] opponentStarts = ladders.Length > 0
                ? ladders.Select(l => l.Landing).ToArray()
                : new[] { new Vector3(courtyardX, 0f, layout.Courtyard.center.z) };

            Transform entry = NewPoint("DragonEntry", points, new Vector3(courtyardX, DragonEntryHeight, layout.BackWallOuterZ + 8f));
            Transform center = NewPoint("ArenaCenter", points, new Vector3(courtyardX, 0f, layout.Courtyard.center.z + 1f));
            // After the dragon flew away the player and the knights run here, just inside the gate, and cheer facing the camera.
            // It must stay inside the courtyard, or the front wall would turn solid again.
            Transform celebrateSpot = NewPoint("CelebrateSpot", points,
                SnapToNavMesh(new Vector3(layout.GatePosition.x, 0f, layout.Courtyard.min.z + CelebrateGateOffset)));

            var game = new GameObject("KnightsGame").AddComponent<KnightsGame>();
            game.transform.SetParent(arena, false);

            // At the back of the courtyard, in line with the gate: the kid walks in through the gate and across the courtyard
            // to the glowing spot, and that starts the battle. Never in front of the courtyard's middle.
            Material ringMaterial = CreateGlowMaterial(ZoneRingMaterialPath, new Color(0.35f, 0.55f, 1f), 0.4f);
            float zoneZ = Mathf.Max(layout.Courtyard.max.z - CastleZoneBackOffset, layout.Courtyard.center.z);
            Vector3 zonePosition = new Vector3(layout.GatePosition.x, 0f, zoneZ);
            ActivityZone castleZone = CreateActivityZone("CastleZone", arena, zonePosition, 2.0f, game, ringMaterial,
                ActivityBeaconBuilder.KnightsBeacon);

            WarnIfGateIsBlocked(layout, zonePosition);

            BuildCutaway(layout);
            BuildPortcullis(layout, game, inputs);

            BattleOpponentAI[] opponents = CreateOpponents(arena, opponentStarts, inputs, out WeaponHolder[] opponentHolders);

            // The dragon waits high behind the castle, facing the courtyard (south).
            DragonBoss dragon = CreateDragon(arena, entry, Vector3.back, inputs, result);

            WireGame(game, layout, inputs, ladders, entry, center, celebrateSpot, opponents, dragon,
                inputs.HeroWeaponHolders.Concat(opponentHolders).ToArray());

            Bounds footprint = layout.Footprint;
            Debug.Log($"CoolSchool arena: castle footprint x {footprint.min.x:0.0}..{footprint.max.x:0.0}, z {footprint.min.z:0.0}..{footprint.max.z:0.0} "
                + $"(gate {layout.GatePosition.ToString("0.0")} facing south, {ladders.Length} goblin ladders, landings {string.Join(" ", ladders.Select(l => l.Landing.ToString("0.0")))}), "
                + $"courtyard x {layout.Courtyard.min.x:0.0}..{layout.Courtyard.max.x:0.0}, z {layout.Courtyard.min.z:0.0}..{layout.Courtyard.max.z:0.0}, "
                + $"castle zone {castleZone.transform.position.ToString("0.0")}, dragon lands at {center.position.ToString("0.0")} "
                + $"and enters at {entry.position.ToString("0.0")}, arena from {layout.Point(layout.Start, 0f).ToString("0.0")} "
                + $"to {layout.Point(layout.End, 0f).ToString("0.0")}.");
            return result;
        }

        /// <summary>
        /// A WallLadder with its four points for each ladder the castle step recorded. The landing is snapped onto the NavMesh,
        /// because a goblin's agent is warped there after the jump.
        /// </summary>
        static WallLadder[] CreateLadders(ArenaLayout layout, Transform points)
        {
            var ladders = new List<WallLadder>();
            for (int i = 0; i < layout.Ladders.Count; i++)
            {
                LadderSpot spot = layout.Ladders[i];
                var root = new GameObject($"Ladder_{i + 1}");
                root.transform.SetParent(points, false);
                var ladder = root.AddComponent<WallLadder>();

                SetObject(ladder, "foot", NewPoint("Foot", root.transform, spot.Foot));
                SetObject(ladder, "shoulder", NewPoint("Shoulder", root.transform, spot.Shoulder));
                SetObject(ladder, "rim", NewPoint("Rim", root.transform, spot.Rim));
                SetObject(ladder, "landing", NewPoint("Landing", root.transform, SnapToNavMesh(spot.Landing)));
                ladders.Add(ladder);
            }
            return ladders.ToArray();
        }

        /// <summary>Logs an error when the baked NavMesh has no complete path from in front of the gate to the castle zone inside.</summary>
        static void WarnIfGateIsBlocked(ArenaLayout layout, Vector3 zonePosition)
        {
            Vector3 outside = layout.GatePosition + Vector3.back * 4f;
            if (!NavMesh.SamplePosition(outside, out NavMeshHit from, 3f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(zonePosition, out NavMeshHit to, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("CoolSchool: no NavMesh in front of the castle gate or at the castle zone.");
                return;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                Debug.LogError("CoolSchool: the castle gate is blocked: no complete NavMesh path from the approach to the castle zone.");
        }

        /// <summary>
        /// The CastleCutaway on the castle group: the front wall's renderers, the ghost material and the courtyard as the inside area.
        /// </summary>
        static void BuildCutaway(ArenaLayout layout)
        {
            if (layout.CastleGroup == null || layout.FrontRenderers.Count == 0) return;

            var cutaway = layout.CastleGroup.gameObject.AddComponent<CastleCutaway>();
            SetObjectArray(cutaway, "frontRenderers", layout.FrontRenderers);
            SetObject(cutaway, "ghostMaterial", CreateCastleGhostMaterial(layout.FrontRenderers[0].sharedMaterial));
            SetBounds(cutaway, "insideArea", layout.Courtyard);
        }

        /// <summary>
        /// The rising behaviour on the portcullis. The camera looks north from 7 m behind and 6.5 m above the kid at about
        /// 39 degrees down, so a closed gate (3-4 m high) is fully on screen only when he is within about 6 m of it south of
        /// the gate, but its lower 2 m are still seen at 11 m. It lowers beyond 11.5 m, so its bottom edge is still in view
        /// when it lands, and it rises from 7 m, so it is already up before he reaches it.
        /// </summary>
        static void BuildPortcullis(ArenaLayout layout, KnightsGame game, ArenaInputs inputs)
        {
            if (layout.Portcullis == null) return;

            var portcullis = layout.Portcullis.AddComponent<CastlePortcullis>();
            SetObject(portcullis, "player", inputs.Player != null ? inputs.Player.transform : null);
            SetBounds(portcullis, "courtyard", layout.Courtyard);
            SetObject(portcullis, "keepOpenWhileRunning", game);
            SetFloat(portcullis, "openRadius", 7f);
            SetFloat(portcullis, "closeRadius", 11.5f);
            SetFloat(portcullis, "liftHeight", layout.PortcullisLift);
        }

        /// <summary>
        /// An alpha-blended copy of the castle's cardboard material, for the front wall's see-through state.
        /// If the castle material is not URP/Lit (the material upgrade did not reach it), a URP/Lit one is made
        /// with the same base texture and colour.
        /// </summary>
        static Material CreateCastleGhostMaterial(Material source)
        {
            Material ghost;
            if (source != null && source.shader != null && source.shader.name == "Universal Render Pipeline/Lit")
            {
                ghost = new Material(source);
            }
            else
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null)
                {
                    Debug.LogError("CoolSchool: URP/Lit shader not found; the castle has no ghost material.");
                    return null;
                }

                Debug.LogWarning($"CoolSchool: the castle material uses '{(source != null && source.shader != null ? source.shader.name : "no shader")}', "
                    + "not URP/Lit; the ghost material is built from its texture and colour.");
                ghost = new Material(lit);
                if (source != null)
                {
                    Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                        : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    ghost.SetTexture("_BaseMap", texture);
                    if (source.HasProperty("_BaseColor")) ghost.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                    else if (source.HasProperty("_Color")) ghost.SetColor("_BaseColor", source.GetColor("_Color"));
                }
            }

            ghost.name = System.IO.Path.GetFileNameWithoutExtension(CastleGhostMaterialPath);
            BattleEffectsBuilder.SetAlphaBlended(ghost);
            Color color = ghost.GetColor("_BaseColor");
            color.a = 1f;
            ghost.SetColor("_BaseColor", color);
            return SaveMaterial(ghost, CastleGhostMaterialPath);
        }

        static Transform NewPoint(string name, Transform parent, Vector3 position)
        {
            Transform point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        static void WireGame(KnightsGame game, ArenaLayout layout, ArenaInputs inputs, WallLadder[] ladders,
            Transform entry, Transform center, Transform celebrateSpot, BattleOpponentAI[] opponents, DragonBoss dragon, WeaponHolder[] holders)
        {
            SetObject(game, "player", inputs.Player);
            SetObject(game, "playerInput", inputs.PlayerInput);
            SetObject(game, "actionButton", inputs.ActionButton);
            SetObject(game, "swordIcon", inputs.SwordIcon);
            SetObject(game, "imagination", inputs.Imagination);
            SetObjectArray(game, "ladders", ladders);
            SetObjectArray(game, "opponents", opponents);
            SetIntArray(game, "waveSizes", new[] { 2, 3 });
            SetObject(game, "dragon", dragon);
            SetObject(game, "dragonEntry", entry);
            SetObject(game, "arenaCenter", center);
            SetObject(game, "celebrateSpot", celebrateSpot);
            SetObjectArray(game, "allies", inputs.Allies);
            SetObjectArray(game, "weaponHolders", holders);
            SetFloat(game, "victorySeconds", 4f);
            SetBool(game, "dragonFliesIn", true);
        }

        // ----- Zone ----------------------------------------------------------------------------

        /// <summary>
        /// An ActivityZone whose visual holds a glowing flat disc (no collider) and the beacon that shows it from afar.
        /// </summary>
        internal static ActivityZone CreateActivityZone(string name, Transform parent, Vector3 position, float radius,
            Activity activity, Material ringMaterial, ActivityBeaconBuilder.Style beacon)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = position;

            // The ring is squashed flat, so the beacon cannot be its child: both go under one unscaled visual.
            Transform visual = NewGroup("Visual", root.transform);

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GlowRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(visual, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var renderer = ring.GetComponent<MeshRenderer>();
            if (ringMaterial != null) renderer.sharedMaterial = ringMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var zone = root.AddComponent<ActivityZone>();
            SetObject(zone, "activity", activity);
            SetFloat(zone, "radius", radius);
            ActivityBeaconBuilder.Build(visual, beacon);
            SetObject(zone, "visual", visual.gameObject);
            return zone;
        }

        /// <summary>The ring material for the tag zone: a warm yellow-green glow.</summary>
        internal static Material CreateTagZoneMaterial()
        {
            return CreateGlowMaterial(TagZoneRingMaterialPath, new Color(1f, 0.85f, 0.15f), 0.4f);
        }

        /// <summary>The ring material for the classroom zone: a warm green glow.</summary>
        internal static Material CreateLessonZoneMaterial()
        {
            return CreateGlowMaterial(RootFolder + "/Materials/LessonZoneRing.mat", new Color(0.4f, 0.9f, 0.45f), 0.4f);
        }

        // ----- Opponents -----------------------------------------------------------------------

        /// <summary>Four inactive goblin/troll kids with a club each, ready for KnightsGame to wake up in waves.</summary>
        static BattleOpponentAI[] CreateOpponents(Transform arena, Vector3[] starts, ArenaInputs inputs, out WeaponHolder[] holders)
        {
            Transform group = NewGroup("Opponents", arena);
            Material telegraphMaterial = CreateEmissiveMaterial(TelegraphMaterialPath, new Color(1f, 0.35f, 0.1f));
            var opponents = new List<BattleOpponentAI>();
            var holderList = new List<WeaponHolder>();

            for (int i = 0; i < OpponentPrefabs.Length; i++)
            {
                Vector3 position = SnapToNavMesh(starts[i % starts.Length]);
                string kidName = OpponentPrefabs[i].Name.Replace("SM_Chr_", "");
                GameObject root = CreateKid("Opponent_" + kidName + "_" + (i + 1), OpponentPrefabs[i],
                    inputs.OpponentController != null ? inputs.OpponentController : inputs.KidController,
                    position, out float height);
                root.transform.SetParent(group, true);

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = height;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.speed = 1.6f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = height;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);

                var hittable = root.AddComponent<Hittable>();
                SetEnum(hittable, "team", (int)BattleTeam.Opponents);
                SetInt(hittable, "hitsToDefeat", 3);
                SetBool(hittable, "canBeDefeated", true);

                root.AddComponent<SwordFighter>();
                root.AddComponent<LadderClimb>();

                var ai = root.AddComponent<BattleOpponentAI>();
                SetObject(ai, "telegraph", CreateTelegraph(root.transform, height, telegraphMaterial));

                // The club goes in the sword slot; there is no shield.
                holderList.Add(AddWeaponHolder(root, OpponentClubs[i], default));

                SetLayerRecursive(root, inputs.KidsLayer);
                root.SetActive(false);
                opponents.Add(ai);
            }

            holders = holderList.Where(h => h != null).ToArray();
            return opponents.ToArray();
        }

        /// <summary>A little "!" (bar plus dot) above the head, hidden until the wind-up.</summary>
        static GameObject CreateTelegraph(Transform kid, float height, Material material)
        {
            var root = new GameObject("Telegraph");
            root.transform.SetParent(kid, false);
            root.transform.localPosition = new Vector3(0f, height + 0.45f, 0f);

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar";
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            bar.transform.localScale = new Vector3(0.08f, 0.22f, 0.08f);

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Dot";
            dot.transform.SetParent(root.transform, false);
            dot.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            dot.transform.localScale = Vector3.one * 0.1f;

            foreach (GameObject part in new[] { bar, dot })
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
                var renderer = part.GetComponent<MeshRenderer>();
                if (material != null) renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            root.SetActive(false);
            return root;
        }

        // ----- Dragon --------------------------------------------------------------------------

        /// <summary>
        /// A "Dragon" root (Hittable + DragonBoss, scale 1) with the Dragon_LP model as its child, scaled to
        /// about 3.5 m tall. The fire origin is estimated at the mouth; the whole thing starts inactive.
        /// </summary>
        static DragonBoss CreateDragon(Transform arena, Transform entry, Vector3 facing, ArenaInputs inputs, ArenaResult result)
        {
            GameObject prefab = LoadPrefab(DragonPrefab);
            if (prefab == null) return null;

            var root = new GameObject("Dragon");
            root.transform.SetParent(arena, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            StripForeignScripts(model);

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (inputs.DragonController != null) animator.runtimeAnimatorController = inputs.DragonController;
            }
            result.DragonControllerMissing = inputs.DragonController == null;

            Transform mouthBone = FindMouthBone(model);
            FaceModelForward(model, mouthBone);

            if (TryGetBounds(model, out Bounds measured) && measured.size.y > 0.01f)
                model.transform.localScale *= DragonHeight / measured.size.y;
            AlignToBounds(model, 0f, 0f, 0f, false);

            Vector3 fireWorld = EstimateMouth(model, mouthBone);
            Transform fireOrigin = NewPoint("FireOrigin", root.transform, fireWorld);

            ParticleSystem fire = null;
            if (inputs.FireConfettiPrefab != null)
            {
                var fireObject = (GameObject)PrefabUtility.InstantiatePrefab(inputs.FireConfettiPrefab.gameObject, fireOrigin);
                fireObject.transform.localPosition = Vector3.zero;
                fireObject.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                fire = fireObject.GetComponent<ParticleSystem>();
            }

            GameObject warning = CreateFireWarning(root.transform, fireOrigin.localPosition);
            Transform aim = NewPoint("AimPoint", root.transform, new Vector3(0f, 1.4f, 0f));

            var hittable = root.AddComponent<Hittable>();
            SetEnum(hittable, "team", (int)BattleTeam.Opponents);
            SetInt(hittable, "hitsToDefeat", 8);
            SetBool(hittable, "canBeDefeated", true);
            SetFloat(hittable, "stunSeconds", 0.8f);
            SetFloat(hittable, "hitRadius", 1.8f);
            SetObject(hittable, "aimPoint", aim);

            var boss = root.AddComponent<DragonBoss>();
            SetObject(boss, "animator", animator);
            SetObject(boss, "fireOrigin", fireOrigin);
            SetObject(boss, "fireConfetti", fire);
            SetObject(boss, "fireWarning", warning);
            SetFloat(boss, "fireRange", FireRange);
            SetFloat(boss, "fireAngle", FireAngle);

            // Waiting high up behind the castle, facing the courtyard.
            root.transform.SetPositionAndRotation(entry.position, Quaternion.LookRotation(facing, Vector3.up));
            SetLayerRecursive(root, inputs.KidsLayer);
            root.SetActive(false);

            result.DragonCreated = true;
            return boss;
        }

        /// <summary>
        /// Removes the vendor's demo scripts (they would fight DragonBoss) and any missing-script components from
        /// the model and all its children. MonoBehaviours in a CoolSchool namespace and all other components stay.
        /// </summary>
        internal static void StripForeignScripts(GameObject model)
        {
            // Components of a prefab instance can't be removed reliably, so unpack it first.
            if (PrefabUtility.IsPartOfPrefabInstance(model))
            {
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            int removed = 0;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                foreach (MonoBehaviour behaviour in t.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;

                    string ns = behaviour.GetType().Namespace;
                    if (ns != null && ns.StartsWith("CoolSchool")) continue;

                    Object.DestroyImmediate(behaviour);
                    removed++;
                }
            }

            Debug.Log($"CoolSchool: removed {removed} vendor script component(s) from the dragon model.");
        }

        /// <summary>The jaw bone if the model has one, else the head bone (mesh nodes ending in _LP are skipped).</summary>
        internal static Transform FindMouthBone(GameObject model)
        {
            Transform[] all = model.GetComponentsInChildren<Transform>(true)
                .Where(t => !t.name.EndsWith("_LP") && t.GetComponent<Renderer>() == null).ToArray();
            return all.FirstOrDefault(t => t.name.Contains("Jaw"))
                ?? all.FirstOrDefault(t => t.name.EndsWith("Head"));
        }

        /// <summary>If the head sits behind the middle of the model, the model faces -Z: turn it half a circle.</summary>
        internal static void FaceModelForward(GameObject model, Transform mouthBone)
        {
            if (mouthBone == null || !TryGetBounds(model, out Bounds bounds)) return;

            float offset = mouthBone.position.z - bounds.center.z;
            if (offset < -0.1f * bounds.size.z)
            {
                model.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("CoolSchool: the dragon model faced -Z; turned it around.");
            }
        }

        /// <summary>The mouth in world space: the jaw bone nudged forward, or the front-top of the bounds as a fallback.</summary>
        static Vector3 EstimateMouth(GameObject model, Transform mouthBone)
        {
            if (!TryGetBounds(model, out Bounds bounds)) return new Vector3(0f, DragonHeight * 0.6f, 1.5f);

            if (mouthBone != null)
                return mouthBone.position + Vector3.forward * 0.3f;

            return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.7f, bounds.max.z - 0.2f);
        }

        /// <summary>A flat translucent orange fan on the ground in front of the dragon, shown during the telegraph.</summary>
        static GameObject CreateFireWarning(Transform dragon, Vector3 originLocal)
        {
            var go = new GameObject("FireWarning");
            go.transform.SetParent(dragon, false);
            go.transform.localPosition = new Vector3(originLocal.x, 0.05f, originLocal.z);

            go.AddComponent<MeshFilter>().sharedMesh = CreateFanMesh(FireRange, FireAngle);
            var renderer = go.AddComponent<MeshRenderer>();
            Material material = CreateGlowMaterial(FireWarningMaterialPath, new Color(1f, 0.45f, 0.1f), 0.4f);
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go;
        }

        /// <summary>A flat pie slice pointing along +Z, wound so it faces up.</summary>
        static Mesh CreateFanMesh(float range, float angleDegrees)
        {
            const int segments = 16;
            var vertices = new Vector3[segments + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-angleDegrees * 0.5f, angleDegrees * 0.5f, i / (float)segments);
                vertices[i + 1] = new Vector3(Mathf.Sin(angle) * range, 0f, Mathf.Cos(angle) * range);
            }

            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = "FireWarningFan", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();

            EnsureFolder(RootFolder, "Meshes");
            AssetDatabase.DeleteAsset(FireWarningMeshPath);
            AssetDatabase.CreateAsset(mesh, FireWarningMeshPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(FireWarningMeshPath);
        }

        // ----- Materials -----------------------------------------------------------------------

        /// <summary>A transparent, emissive URP/Lit material for glowing floor discs.</summary>
        internal static Material CreateGlowMaterial(string path, Color color, float alpha)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var material = new Material(lit) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BattleEffectsBuilder.SetAlphaBlended(material);

            return SaveMaterial(material, path);
        }

        /// <summary>An opaque, glowing URP/Lit material.</summary>
        internal static Material CreateEmissiveMaterial(string path, Color color)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var material = new Material(lit) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.3f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            return SaveMaterial(material, path);
        }

        static Material SaveMaterial(Material material, string path)
        {
            EnsureFolder(RootFolder, "Materials");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
