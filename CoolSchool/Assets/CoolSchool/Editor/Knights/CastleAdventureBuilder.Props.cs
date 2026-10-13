using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>Step 1 of the castle adventure: the static props (no colliders, all positions worked out from the ArenaLayout).</summary>
    internal static partial class CastleAdventureBuilder
    {
        // ----- The gate end: talk spot, queen, password stands ----------------------------------------

        static void BuildGateEnd(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Vector3 gate = layout.GatePosition;
            Bounds court = layout.Courtyard;
            Transform group = NewGroup("Gate", p.Root);

            // He stands here to talk to the queen and to say the password, facing the gate (north).
            Vector3 talk = new Vector3(gate.x, 0f, gate.z - TalkDistance);
            p.GateTalkSpot = MakePoint("GateTalkSpot", group, talk);
            p.GateTalkSpot.rotation = Quaternion.identity;

            // The queen waits to the right of the gate, turned toward the talk spot (CastleQueen faces the way her home point faces).
            Vector3 queenHome = AvoidBraziers(layout, new Vector3(gate.x + 2.6f, 0f, gate.z - 1.4f));
            p.QueenHome = MakePoint("QueenHome", group, queenHome);
            Vector3 toTalk = talk - queenHome;
            toTalk.y = 0f;
            if (toTalk.sqrMagnitude > 0.01f) p.QueenHome.rotation = Quaternion.LookRotation(toTalk, Vector3.up);

            // Inside, the queen walks to the east side of the courtyard and watches from there.
            p.QueenSpot = MakePoint("QueenSpot", group, new Vector3(court.max.x - 3.5f, 0f, court.min.z + 6f));

            // At the celebration she stands to the right of the player (the rest of the knights gather around the player's spot).
            Vector3 celebrate = KnightsArenaBuilder.CelebratePoint(layout);
            p.QueenCelebrateSpot = MakePoint("QueenCelebrateSpot", group, celebrate + new Vector3(2.8f, 0f, 0.6f));
            p.BabyCelebrateSpot = MakePoint("BabyCelebrateSpot", group, celebrate + new Vector3(-2.2f, 0f, 0.4f));

            // Where he hops off the dragon after the ride: next to the landing spot, toward the camera.
            p.DismountPoint = MakePoint("RideDismountPoint", group, p.Landing + new Vector3(-2f, 0f, -1.5f));

            // The three password stands: small cardboard boxes in a row in front of the gate, hidden until the password question.
            p.PasswordGate = NewGroup("PasswordGate", group);
            Material cardboard = CardboardMat;
            for (int i = 0; i < p.Stands.Length; i++)
            {
                Transform stand = NewGroup("Stand_" + (i + 1), p.PasswordGate);
                stand.position = new Vector3(gate.x + (i - 1) * StandSpacing, 0f, gate.z - StandDistance);

                GameObject box = SpawnFitted(StandBoxPrefabs[i % StandBoxPrefabs.Length], stand, StandSize);
                float top = StandSize;
                if (box != null)
                {
                    if (TryGetBounds(box, out Bounds boxBounds)) top = boxBounds.max.y;
                }
                else
                {
                    Debug.LogWarning("CoolSchool castle: a cardboard box model is missing; the password stand is a plain box.");
                    MakePrimitive(PrimitiveType.Cube, "Box", stand, new Vector3(0f, StandSize * 0.5f, 0f), Vector3.one * StandSize, Quaternion.identity, cardboard);
                }

                // The picture goes on top, on an empty point (PasswordGate finds it by this name).
                MakePoint("PropSpot", stand, new Vector3(stand.position.x, top, stand.position.z));
                stand.gameObject.SetActive(false);
                p.Stands[i] = stand;
            }
        }

        /// <summary>
        /// Moves a spot south if it would stand on top of one of the gate's braziers (imagination props beside the arch), so the queen
        /// never overlaps a fire bowl while the game runs. The braziers stand a step beside the arch opening, in front of the wall.
        /// </summary>
        static Vector3 AvoidBraziers(KnightsArenaBuilder.ArenaLayout layout, Vector3 wanted)
        {
            if (layout.GateBounds.size.x < 0.1f) return wanted;

            const float openingHalfFraction = 0.25f;   // as in KnightsArenaBuilder: half the arch opening as a fraction of the gate's width
            const float gateClearance = 1.0f;
            const float frontClearance = 1.2f;
            const float keepAway = 1.3f;

            float side = layout.GateBounds.size.x * openingHalfFraction + gateClearance;
            float brazierZ = layout.GateBounds.min.z - frontClearance;
            Vector3 result = wanted;
            foreach (float sign in new[] { -1f, 1f })
            {
                var brazier = new Vector2(layout.GatePosition.x + sign * side, brazierZ);
                if (Vector2.Distance(new Vector2(result.x, result.z), brazier) < keepAway) result.z = brazierZ - keepAway;
            }

            if (!Mathf.Approximately(result.z, wanted.z))
                Debug.Log($"CoolSchool castle: the queen's home moved from z {wanted.z:0.0} to {result.z:0.0} to keep clear of a gate brazier.");
            return result;
        }

        // ----- The hole in the wall -----------------------------------------------------------------

        static void BuildWallPatch(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("WallPatch", p.Root);
            p.WallPatch = group;

            float holeX = (LadderX(layout, 0) + LadderX(layout, 1)) * 0.5f;
            float innerZ = layout.BackWallInnerZ;
            const float holeLift = 0.03f;       // the decal floats this far in front of the wall
            const float blockGap = 0.02f;
            p.HolePosition = new Vector3(holeX, 0f, innerZ - holeLift);

            // The hole: a dark jagged decal on the inner face of the back wall, its bottom on the floor.
            var hole = new GameObject("Hole");
            hole.transform.SetParent(group, false);
            hole.transform.position = p.HolePosition;
            hole.AddComponent<MeshFilter>().sharedMesh = CreateHoleMesh();
            var holeRenderer = hole.AddComponent<MeshRenderer>();
            holeRenderer.sharedMaterial = HoleMat;
            holeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            holeRenderer.receiveShadows = false;

            // 8 block slots in front of the hole (2 columns, 4 rows, bottom row on the floor), each with a see-through ghost block.
            float slotZ = innerZ - holeLift - blockGap - BlockSize * 0.5f;
            Material ghost = GhostBlockMat;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    int index = row * 2 + column;
                    Transform slot = MakePoint("Slot_" + (index + 1), group,
                        new Vector3(holeX + (column - 0.5f) * BlockSize, BlockSize * 0.5f + row * BlockSize, slotZ));
                    Transform ghostBlock = MakePrimitive(PrimitiveType.Cube, "Ghost", slot, Vector3.zero, Vector3.one * BlockSize * 0.98f,
                        Quaternion.identity, ghost);
                    ghostBlock.gameObject.SetActive(false);
                    p.Slots[index] = slot;
                    p.Ghosts[index] = ghostBlock.gameObject;
                }
            }

            // Where he stands for the moment: 3.6 m south of the hole (snapped to the NavMesh later).
            p.StandPoint = MakePoint("StandPoint", group, new Vector3(holeX, 0f, innerZ - 3.6f));

            // The heap of toy blocks, 2.2 m west of the hole and 2 m into the courtyard. Each block's origin is its middle, so a block
            // that flies into a slot lands exactly on the slot's point.
            var heap = new[]
            {
                new Vector3(-0.55f, 0, -0.22f), new Vector3(0.00f, 0, -0.28f), new Vector3(0.55f, 0, -0.18f),
                new Vector3(-0.50f, 0, 0.26f), new Vector3(0.02f, 0, 0.24f), new Vector3(0.52f, 0, 0.30f),
                new Vector3(-0.30f, 1, 0.00f), new Vector3(0.30f, 1, 0.04f), new Vector3(0.02f, 1, 0.46f),
                new Vector3(0.02f, 2, 0.04f),
            };
            float[] yaws = { 10f, -15f, 20f, -8f, 25f, -20f, 12f, -18f, 5f, 15f };
            Vector3 pile = new Vector3(holeX - 2.2f, 0f, innerZ - 2f);
            for (int i = 0; i < p.PileBlocks.Length; i++)
            {
                Transform block = NewGroup("PileBlock_" + (i + 1), group);
                block.position = pile + new Vector3(heap[i].x, BlockSize * 0.5f + heap[i].y * BlockSize, heap[i].z);

                GameObject model = SpawnFitted(ToyBlockPrefabs[i % ToyBlockPrefabs.Length], block, BlockSize, centre: true);
                if (model == null)
                    MakePrimitive(PrimitiveType.Cube, "Block", block, Vector3.zero, Vector3.one * BlockSize, Quaternion.identity, CardboardMat);
                block.rotation = Quaternion.Euler(0f, yaws[i], 0f);
                p.PileBlocks[i] = block;
            }

            // The goblin that peeks is behind the back wall (see Build): its feet x/z are fixed here, its height once the model is known.
            p.PeekGoblinFeet = new Vector3(holeX, layout.BackWallTopY, layout.BackWallOuterZ + 0.5f);
        }

        // ----- The mission spot: table, cake, crown, egg in a nest ----------------------------------

        static void BuildMissionSpot(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Bounds court = layout.Courtyard;
            Vector3 spot = new Vector3(court.min.x + 3.5f, 0f, court.center.z);
            Transform group = NewGroup("MissionSpot", p.Root);
            p.MissionSpot = group;

            BuildTable("MissionTable", group, spot, out float top, out Bounds tableBounds);
            Vector3 onTable = new Vector3(spot.x, top, spot.z);

            // The cake (about 0.5 m) and, beside it, the slice that the thief steals.
            Transform cakeSet = NewGroup("CakeSet", group);
            cakeSet.position = onTable;
            Transform cake = NewGroup("Cake", cakeSet);
            cake.position = onTable + new Vector3(-0.28f, 0f, 0f);
            if (SpawnFitted(CakePrefab, cake, 0.5f) == null) Debug.LogWarning("CoolSchool castle: the cake model is missing.");
            Transform slice = NewGroup("CakeSlice", cakeSet);
            slice.position = onTable + new Vector3(0.3f, 0f, 0.05f);
            if (SpawnFitted(CakeSlicePrefab, slice, 0.22f) == null) Debug.LogWarning("CoolSchool castle: the cake slice model is missing.");
            p.CakeSet = cakeSet.gameObject;
            p.CakeLoot = slice.gameObject;

            // The golden crown (about 0.35 m) on a red cushion.
            Transform crownSet = NewGroup("CrownSet", group);
            crownSet.position = onTable;
            MakePrimitive(PrimitiveType.Cube, "Cushion", crownSet, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.08f, 0.5f), Quaternion.identity, CushionMat);
            Transform crown = NewGroup("Crown", crownSet);
            crown.position = onTable + new Vector3(0f, 0.08f, 0f);
            if (SpawnFitted(CrownPrefab, crown, 0.35f) == null) Debug.LogWarning("CoolSchool castle: the crown model is missing.");
            p.CrownSet = crownSet.gameObject;
            p.CrownLoot = crown.gameObject;

            // The egg in its nest: on the ground a step east of the table, not on the table.
            Vector3 nest = new Vector3(tableBounds.max.x + 0.9f, 0f, spot.z - 0.2f);
            p.NestPosition = nest;
            BuildEggSet(group, nest, p);

            Hide(p.CakeSet, p.CrownSet, p.EggSet);
        }

        /// <summary>A table about 0.75 m high with its long side along X, no collider. Falls back to a box when the model is missing.</summary>
        static void BuildTable(string tableName, Transform parent, Vector3 spot, out float top, out Bounds bounds)
        {
            Transform root = NewGroup(tableName, parent);
            root.position = spot;
            GameObject model = SpawnFitted(TablePrefab, root, TableHeight, byHeight: true);

            if (model != null && TryGetBounds(model, out Bounds measured))
            {
                // Long side along X, so things lie in a row facing the camera.
                if (measured.size.z > measured.size.x * 1.15f)
                {
                    model.transform.RotateAround(spot, Vector3.up, 90f);
                    TryGetBounds(model, out measured);
                }
                top = measured.max.y;
                bounds = measured;
                return;
            }

            Debug.LogWarning("CoolSchool castle: the table model is missing; using a plain box.");
            MakePrimitive(PrimitiveType.Cube, "Table", root, new Vector3(0f, TableHeight * 0.5f, 0f), new Vector3(1.4f, TableHeight, 0.8f),
                Quaternion.identity, CardboardMat);
            top = TableHeight;
            bounds = new Bounds(spot + Vector3.up * TableHeight * 0.5f, new Vector3(1.4f, TableHeight, 0.8f));
        }

        /// <summary>
        /// The egg set: a nest (a flattened brown ball with a ring of brown sticks) and the egg in it. The egg is a sphere scaled
        /// (0.45, 0.6, 0.45), cream with three coloured spots; five curved shell pieces sit hidden on it for the hatching.
        /// </summary>
        static void BuildEggSet(Transform parent, Vector3 nest, PropLayout p)
        {
            Transform set = NewGroup("EggSet", parent);
            set.position = nest;

            Material nestMaterial = NestMat;
            MakePrimitive(PrimitiveType.Sphere, "NestBase", set, new Vector3(0f, 0.05f, 0f), new Vector3(0.95f, 0.2f, 0.95f), Quaternion.identity, nestMaterial);
            const int sticks = 10;
            for (int i = 0; i < sticks; i++)
            {
                float angle = i * 360f / sticks;
                float radians = angle * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(radians) * 0.42f, 0.15f, Mathf.Cos(radians) * 0.42f);
                // A short cylinder lying along the ring's tangent.
                MakePrimitive(PrimitiveType.Cylinder, "Stick_" + (i + 1), set, position, new Vector3(0.09f, 0.16f, 0.09f),
                    Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(0f, 0f, 90f), nestMaterial);
            }

            // The egg's origin is its bottom, so a thief's hand or the dragon's feet can use it as a point on the ground.
            Transform egg = NewGroup("Egg", set);
            egg.position = nest + new Vector3(0f, 0.12f, 0f);
            MakePrimitive(PrimitiveType.Sphere, "EggBody", egg, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.6f, 0.45f),
                Quaternion.identity, EggMat);

            // Three coloured spots on the camera side of the egg, and shell pieces (hidden) spread over its whole surface.
            Vector3[] spotDirections =
            {
                new Vector3(0.0f, 0.30f, -0.95f), new Vector3(0.72f, -0.15f, -0.68f), new Vector3(-0.66f, 0.5f, -0.56f),
            };
            for (int i = 0; i < spotDirections.Length; i++)
            {
                Vector3 normal = spotDirections[i].normalized;
                MakePrimitive(PrimitiveType.Sphere, "Spot_" + (i + 1), egg, EggSurface(normal, 0.99f), new Vector3(0.13f, 0.13f, 0.04f),
                    Quaternion.LookRotation(normal), EggSpotMat(i));
            }

            Vector3[] shellDirections =
            {
                new Vector3(0.0f, 0.8f, -0.6f), new Vector3(0.8f, 0.2f, -0.5f), new Vector3(-0.8f, 0.2f, -0.5f),
                new Vector3(0.5f, -0.4f, -0.8f), new Vector3(-0.5f, -0.4f, -0.8f),
            };
            var shells = new List<GameObject>();
            for (int i = 0; i < shellDirections.Length; i++)
            {
                Vector3 normal = shellDirections[i].normalized;
                Transform piece = MakePrimitive(PrimitiveType.Sphere, "Shell_" + (i + 1), egg, EggSurface(normal, 1.0f), new Vector3(0.24f, 0.2f, 0.05f),
                    Quaternion.LookRotation(normal), ShellMat);
                piece.gameObject.SetActive(false);
                shells.Add(piece.gameObject);
            }

            p.EggSet = set.gameObject;
            p.EggLoot = egg.gameObject;
            p.ShellPieces = shells.ToArray();
        }

        /// <summary>A point on the egg's surface (local to the egg wrapper) in the direction of a normal, scaled a little in or out.</summary>
        static Vector3 EggSurface(Vector3 normal, float k)
        {
            var radii = new Vector3(0.225f, 0.3f, 0.225f);
            Vector3 onUnit = normal.normalized;
            return new Vector3(onUnit.x * radii.x, 0.3f + onUnit.y * radii.y, onUnit.z * radii.z) * k + new Vector3(0f, 0.3f * (1f - k), 0f);
        }

        // ----- Barrels, wall perches, thief run points -------------------------------------------------

        static void BuildBarrels(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Bounds court = layout.Courtyard;
            Transform group = NewGroup("Barrels", p.Root);
            float z = layout.BackWallInnerZ - 4f;
            float[] xs = { court.min.x + 3f, court.max.x - 3f };

            for (int i = 0; i < p.Barrels.Length; i++)
            {
                // The barrel's origin is the middle of its bottom, so CastleBarrel can tip it over around its foot.
                Transform barrel = NewGroup("Barrel_" + (i + 1), group);
                barrel.position = new Vector3(xs[i], 0f, z);
                if (SpawnFitted(BarrelPrefabs[i % BarrelPrefabs.Length], barrel, BarrelHeight, byHeight: true) == null)
                {
                    Debug.LogWarning("CoolSchool castle: a barrel model is missing; using a plain cylinder.");
                    MakePrimitive(PrimitiveType.Cylinder, "Barrel", barrel, new Vector3(0f, BarrelHeight * 0.5f, 0f),
                        new Vector3(0.7f, BarrelHeight * 0.5f, 0.7f), Quaternion.identity, CardboardMat);
                }

                // Where the tiny goblin hides (low in the barrel) and where it hops out to (toward the camera, on the floor; the
                // point is NOT a child of the barrel, which moves when it tips).
                p.BarrelInside[i] = MakePoint("Inside", barrel, barrel.position + new Vector3(0f, 0.1f, 0f));
                p.BarrelHopOut[i] = MakePoint("BarrelHopOut_" + (i + 1), p.PointsGroup, barrel.position + new Vector3(0f, 0f, -1.4f));
                p.Barrels[i] = barrel.gameObject;
            }
        }

        static void BuildPerchesAndThiefPoints(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Bounds court = layout.Courtyard;
            float innerZ = layout.BackWallInnerZ;
            Transform perches = NewGroup("Perches", p.Root);

            // Seven spots on top of the back wall, on its inner edge, spread evenly between the outer two ladders, all facing south;
            // each has a landing on the courtyard floor straight below, a little way out from the wall (the ladders' own landings are
            // 1.8 m out).
            float thickness = Mathf.Max(0.2f, layout.BackWallOuterZ - innerZ);
            float perchZ = innerZ + Mathf.Min(0.15f, thickness * 0.3f);
            float left = LadderX(layout, 0);
            float right = LadderX(layout, 2);
            for (int k = 0; k < PerchCount; k++)
            {
                float x = Mathf.Lerp(left, right, (k + 1f) / (PerchCount + 1f));
                Transform perch = MakePoint("Perch_" + (k + 1), perches, new Vector3(x, layout.BackWallTopY, perchZ));
                perch.rotation = SouthFacing;
                p.Perches[k] = perch;
                p.PerchLandings[k] = MakePoint("PerchLanding_" + (k + 1), perches, new Vector3(x, 0f, innerZ - 2.8f));
            }

            // Eight points on a loose ring 5-7 m around the courtyard's middle for the thief to run between. The ring skips the
            // southern part, so no point sits in the gate's path.
            Transform thief = NewGroup("ThiefRunPoints", p.Root);
            float[] radii = { 6.0f, 5.2f, 6.6f, 5.5f, 6.8f, 5.3f, 6.4f, 5.6f };
            for (int k = 0; k < ThiefPointCount; k++)
            {
                float angle = (305f + k * (290f / (ThiefPointCount - 1))) * Mathf.Deg2Rad;
                float x = court.center.x + Mathf.Cos(angle) * radii[k];
                float z = court.center.z + Mathf.Sin(angle) * radii[k];
                x = Mathf.Clamp(x, court.min.x + 1.5f, court.max.x - 1.5f);
                z = Mathf.Clamp(z, court.min.z + 3f, court.max.z - 2.5f);
                p.ThiefPoints[k] = MakePoint("RunPoint_" + (k + 1), thief, new Vector3(x, 0f, z));
            }
        }

        // ----- The cookie table and the dragon's bowl ------------------------------------------------

        static void BuildCookieTable(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("CookieTable", p.Root);
            p.CookieTable = group;

            // 2.8 m west of where the dragon lands and 1.5 m toward the camera, so the dragon can see it.
            Vector3 spot = new Vector3(p.Landing.x - 2.8f, 0f, p.Landing.z - 1.5f);
            BuildTable("Table", group, spot, out float top, out Bounds tableBounds);

            // An oval plate on the table with 10 cookies in two rows.
            float plateWidth = Mathf.Min(1.0f, tableBounds.size.x * 0.8f);
            float plateDepth = Mathf.Min(0.55f, tableBounds.size.z * 0.7f);
            const float plateThickness = 0.015f;
            MakePrimitive(PrimitiveType.Cylinder, "Plate", group, new Vector3(spot.x, top + plateThickness * 0.5f, spot.z),
                new Vector3(plateWidth, plateThickness * 0.5f, plateDepth), Quaternion.identity, PlateMat);

            float spacing = Mathf.Min(0.2f, plateWidth * 0.9f / 5f);
            float cookieSize = Mathf.Min(0.16f, spacing * 0.9f);
            float cookieY = top + plateThickness + cookieSize * 0.125f;
            for (int i = 0; i < p.Cookies.Length; i++)
            {
                int row = i / 5;
                int column = i % 5;
                Vector3 position = new Vector3(spot.x + (column - 2) * spacing, cookieY, spot.z + (row == 0 ? -1f : 1f) * plateDepth * 0.22f);
                p.Cookies[i] = BuildCookie("Cookie_" + (i + 1), group, position, cookieSize, 3);
            }

            // The big cookie for the quick version (hidden until needed).
            Vector3 bigPosition = new Vector3(spot.x, top + plateThickness + 0.03f, spot.z);
            p.BigCookie = BuildCookie("BigCookie", group, bigPosition, 0.4f, 5);
            p.BigCookie.gameObject.SetActive(false);

            // The dragon's bowl: on the ground 1.4 m in front of its landing spot (camera side); the cookies land in it.
            Transform bowl = NewGroup("Bowl", group);
            bowl.position = p.Landing + new Vector3(0f, 0f, -1.4f);
            if (SpawnFitted(BowlPrefab, bowl, 0.5f) == null)
            {
                Debug.LogWarning("CoolSchool castle: the pet bowl model is missing; the bowl is a flat cylinder.");
                MakePrimitive(PrimitiveType.Cylinder, "Bowl", bowl, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Quaternion.identity, NestMat);
            }
            p.Bowl = bowl;
        }

        /// <summary>A cookie: a flat brown disc with a few dark chips. The root is an empty point (the discs are flattened, so children of a scaled disc would be squashed).</summary>
        static Transform BuildCookie(string cookieName, Transform parent, Vector3 worldPosition, float width, int chips)
        {
            Transform root = MakePoint(cookieName, parent, worldPosition);
            float thickness = width * 0.25f;
            MakePrimitive(PrimitiveType.Cylinder, "Disc", root, Vector3.zero, new Vector3(width, thickness * 0.5f, width), Quaternion.identity, CookieMat);

            float chip = width * 0.22f;
            for (int i = 0; i < chips; i++)
            {
                float angle = (i * 360f / chips + 20f) * Mathf.Deg2Rad;
                float ring = i == 0 && chips > 3 ? 0f : width * 0.22f;
                MakePrimitive(PrimitiveType.Sphere, "Chip_" + (i + 1), root,
                    new Vector3(Mathf.Cos(angle) * ring, thickness * 0.5f, Mathf.Sin(angle) * ring), Vector3.one * chip, Quaternion.identity, ChipMat);
            }
            return root;
        }

        // ----- The name flag -------------------------------------------------------------------------

        static void BuildFlagPole(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("NameFlag", p.Root);
            Vector3 celebrate = KnightsArenaBuilder.CelebratePoint(layout);
            Vector3 foot = celebrate + new Vector3(2.6f, 0f, 1.4f);
            p.FlagPole = group;

            // A thin pole 3.2 m tall with a gold ball on top.
            const float poleHeight = 3.2f;
            MakePrimitive(PrimitiveType.Cylinder, "Pole", group, foot + new Vector3(0f, poleHeight * 0.5f, 0f), new Vector3(0.08f, poleHeight * 0.5f, 0.08f),
                Quaternion.identity, PoleMat);
            MakePrimitive(PrimitiveType.Sphere, "PoleTop", group, foot + new Vector3(0f, poleHeight + 0.05f, 0f), Vector3.one * 0.16f, Quaternion.identity, GoldMat);

            // The banner hangs from a cross rod and reaches west from the pole, so it faces the camera: 1.8 x 1.0 m, castle blue.
            Vector3 bannerCentre = foot + new Vector3(-0.95f, poleHeight - 0.65f, -0.06f);
            MakePrimitive(PrimitiveType.Cylinder, "Rod", group, new Vector3(bannerCentre.x, poleHeight - 0.1f, foot.z - 0.06f), new Vector3(0.05f, 0.95f, 0.05f),
                Quaternion.Euler(0f, 0f, 90f), PoleMat);
            Transform banner = NewGroup("Banner", group);
            banner.position = bannerCentre;
            MakePrimitive(PrimitiveType.Cube, "Cloth", banner, Vector3.zero, new Vector3(1.8f, 1.0f, 0.03f), Quaternion.identity, BannerMat);

            // The letters go on a world-space canvas on the cloth's south face (the text reads correctly seen from the south).
            var canvasObject = new GameObject("NameCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(banner, false);
            canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            canvasObject.transform.localScale = Vector3.one * 0.004f;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(450f, 250f);

            var rowObject = new GameObject("LetterRow", typeof(RectTransform));
            rowObject.transform.SetParent(canvasObject.transform, false);
            var row = (RectTransform)rowObject.transform;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(410f, 150f);

            // The gold shield emblem in front of the cloth, until his name is on it.
            Transform emblem = NewGroup("Emblem", banner);
            emblem.localPosition = new Vector3(0f, 0f, -0.05f);
            Material gold = GoldMat;
            MakePrimitive(PrimitiveType.Cube, "ShieldTop", emblem, new Vector3(0f, 0.12f, 0f), new Vector3(0.52f, 0.32f, 0.04f), Quaternion.identity, gold);
            MakePrimitive(PrimitiveType.Cube, "ShieldPoint", emblem, new Vector3(0f, -0.02f, 0f), new Vector3(0.368f, 0.368f, 0.04f),
                Quaternion.Euler(0f, 0f, 45f), gold);
            Transform star = new GameObject("ShieldStar").transform;
            star.SetParent(emblem, false);
            star.localPosition = new Vector3(0f, 0.06f, -0.03f);
            star.localScale = Vector3.one * 0.9f;
            star.gameObject.AddComponent<MeshFilter>().sharedMesh = CreateStarMesh();
            var starRenderer = star.gameObject.AddComponent<MeshRenderer>();
            starRenderer.sharedMaterial = BannerMat;
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;

            p.LetterRow = row;
            p.Emblem = emblem.gameObject;
            p.NameCanvas = canvas;
        }

        // ----- The growing castle ----------------------------------------------------------------------

        static void BuildGrowthTiers(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("CastleGrowth", p.Root);
            p.Growth = group;
            Bounds court = layout.Courtyard;
            GameObject flagA = LoadPrefab(FlagA);
            GameObject flagB = LoadPrefab(FlagB);

            // Tiers 1-4: a flag on each tower top, the two back towers first (north-west, north-east, then south-west, south-east).
            // The imagination flags already stand in the middle of the two north tower tops, so these stand a step toward the outside
            // and the camera.
            int[] towerOrder = { 0, 1, 2, 3 };
            for (int tier = 0; tier < towerOrder.Length; tier++)
            {
                Bounds tower = layout.TowerBounds[towerOrder[tier]];
                Transform wrapper = NewGroup("Tier_" + (tier + 1), group);
                if (tower.size.sqrMagnitude < 0.01f)
                {
                    wrapper.gameObject.SetActive(false);
                    p.Tiers[tier] = wrapper.gameObject;
                    continue;
                }

                float outward = tower.center.x >= court.center.x ? 1f : -1f;
                wrapper.position = new Vector3(tower.center.x + outward * 0.65f, tower.max.y, tower.center.z - 0.65f);
                PlaceFlag(tier % 2 == 0 ? flagA : flagB, wrapper, null);
                wrapper.gameObject.SetActive(false);
                p.Tiers[tier] = wrapper.gameObject;
            }

            // Tiers 5-6: tall coloured banners on the inner back wall, either side of the hole.
            float holeX = (LadderX(layout, 0) + LadderX(layout, 1)) * 0.5f;
            float innerZ = layout.BackWallInnerZ;
            float bannerHeight = Mathf.Min(2f, Mathf.Max(0.8f, layout.BackWallTopY - 1.8f));
            float bannerBottom = 1.2f;
            Material[] bannerMaterials = { BannerRedMat, BannerYellowMat };
            for (int i = 0; i < 2; i++)
            {
                Transform wrapper = NewGroup("Tier_" + (5 + i), group);
                wrapper.position = new Vector3(holeX + (i == 0 ? -1.7f : 1.7f), bannerBottom, innerZ - 0.06f);
                MakePrimitive(PrimitiveType.Cube, "Banner", wrapper, new Vector3(0f, bannerHeight * 0.5f, 0f), new Vector3(0.8f, bannerHeight, 0.04f),
                    Quaternion.identity, bannerMaterials[i]);
                MakePrimitive(PrimitiveType.Cube, "Trim", wrapper, new Vector3(0f, 0.05f, -0.01f), new Vector3(0.84f, 0.1f, 0.05f), Quaternion.identity, GoldMat);
                wrapper.gameObject.SetActive(false);
                p.Tiers[4 + i] = wrapper.gameObject;
            }

            // Tier 7: a golden flag on the gate tower (the gate piece).
            Transform gold = NewGroup("Tier_7", group);
            if (layout.GateBounds.size.sqrMagnitude > 0.01f)
            {
                gold.position = new Vector3(layout.GateBounds.center.x, layout.GateBounds.max.y, layout.GateBounds.center.z);
                PlaceFlag(flagA, gold, GoldMat);
            }
            gold.gameObject.SetActive(false);
            p.Tiers[6] = gold.gameObject;
        }

        /// <summary>A flag about 2.2 m tall standing at the wrapper's origin, turned to face the camera, optionally painted with one material.</summary>
        static void PlaceFlag(GameObject prefab, Transform wrapper, Material paint)
        {
            if (prefab == null) return;

            GameObject flag = Spawn(prefab, wrapper);
            flag.transform.rotation = SouthFacing;
            StripColliders(flag);
            if (TryGetBounds(flag, out Bounds measured) && measured.size.y > 0.01f)
                flag.transform.localScale *= GrowthFlagHeight / measured.size.y;
            if (TryGetBounds(flag, out Bounds fitted))
                flag.transform.position += wrapper.position - new Vector3(fitted.center.x, fitted.min.y, fitted.center.z);

            if (paint != null)
            {
                foreach (Renderer renderer in flag.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = paint;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        // ----- The tower rescue --------------------------------------------------------------------------

        static void BuildTowerRescueProps(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("TowerRescue", p.Root);
            p.TowerRescue = group;

            // The back-east tower: the north-east corner.
            Bounds tower = layout.TowerBounds[1];
            if (tower.size.sqrMagnitude < 0.01f)
            {
                Debug.LogWarning("CoolSchool castle: the north-east tower could not be measured; the tower rescue has no tower top.");
                tower = new Bounds(new Vector3(layout.Courtyard.max.x, 5f, layout.Courtyard.max.z), new Vector3(2.8f, 5.5f, 2.8f));
            }

            Vector3 topPoint = new Vector3(tower.center.x, tower.max.y, tower.center.z);
            p.TowerTop = MakePoint("TowerTop", group, topPoint);
            p.TowerTop.rotation = SouthFacing;

            // The glow ring on the floor at the tower's inner (south-west) corner, 1.5 m inside the courtyard.
            Vector3 ringCentre = new Vector3(tower.min.x - 1.5f, 0f, tower.min.z - 1.5f);
            Transform ring = MakePrimitive(PrimitiveType.Cylinder, "RescueRing", group, ringCentre + new Vector3(0f, 0.03f, 0f),
                new Vector3(2.4f, 0.01f, 2.4f), Quaternion.identity, RescueRingMat);
            ring.gameObject.SetActive(false);
            p.RescueRing = ring.gameObject;

            // The ladder from the ring up to the tower's inner roof corner, leaning, no colliders.
            Vector3 ladderTop = new Vector3(tower.min.x + 0.4f, tower.max.y, tower.min.z + 0.4f);
            p.RescueLadder = BuildLeaningLadder(group, ringCentre, ladderTop);
        }

        /// <summary>A ladder model standing at `foot` and reaching `top`, hidden. Falls back to the box ladder.</summary>
        static GameObject BuildLeaningLadder(Transform parent, Vector3 foot, Vector3 top)
        {
            Transform wrapper = NewGroup("RescueLadder", parent);
            GameObject prefab = LoadPrefab(LadderPrefab);
            GameObject model = prefab != null ? Spawn(prefab, wrapper) : KnightsArenaBuilder.CreateBoxLadder(wrapper);
            StripColliders(model);
            KnightsArenaBuilder.UprightLadder(model);

            if (!TryGetBounds(model, out Bounds upright) || upright.size.y < 0.01f)
            {
                Debug.LogWarning("CoolSchool castle: the rescue ladder model has no size.");
                wrapper.gameObject.SetActive(false);
                return wrapper.gameObject;
            }

            // Bottom middle of the upright ladder on the wrapper's origin (the wrapper sits at the world origin for now).
            model.transform.position += wrapper.position - new Vector3(upright.center.x, upright.min.y, upright.center.z);
            TryGetBounds(model, out upright);

            Vector3 direction = top - foot;
            float length = direction.magnitude;
            float scaleY = length / upright.size.y;
            float scaleXZ = Mathf.Min(scaleY, 1.1f / Mathf.Max(0.01f, upright.size.x));
            wrapper.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

            // Local Y runs up the ladder; local X stays horizontal.
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            Vector3 forward = Vector3.Cross(side.normalized, direction.normalized);
            wrapper.rotation = Quaternion.LookRotation(forward, direction.normalized);
            wrapper.position = foot;

            wrapper.gameObject.SetActive(false);
            return wrapper.gameObject;
        }

        // ----- The ride path ------------------------------------------------------------------------------

        static void BuildRidePathProps(KnightsArenaBuilder.ArenaLayout layout, PropLayout p)
        {
            Transform group = NewGroup("RidePath", p.Root);
            p.RideGroup = group;

            Bounds footprint = layout.Footprint;
            Vector3 gate = layout.GatePosition;
            Vector3 landing = p.Landing;
            float cx = layout.Courtyard.center.x;

            // 3 m outside the walls where there is room; the east and north sides are limited by the fence (the arena rectangle).
            float northOut = Mathf.Min(footprint.max.z + 3f, layout.NorthFenceZ - 0.6f);
            float eastOut = Mathf.Min(footprint.max.x + 3f, layout.End - 0.6f);
            float westOut = footprint.min.x - 3f;
            float southOut = footprint.min.z - 3f;

            // From the landing spot up to 6 m, out over the back wall at the north-east, then counter-clockwise around the castle at
            // 7-9 m (west along the north side, south along the west side, east along the south side), back over the gate, and down.
            Vector3[] points =
            {
                landing,
                new Vector3(cx + 2f, 5.5f, landing.z + 3.5f),
                new Vector3(eastOut, 8.5f, northOut),
                new Vector3(cx, 8.0f, northOut),
                new Vector3(westOut, 8.5f, northOut),
                new Vector3(westOut, 8.0f, layout.Courtyard.center.z),
                new Vector3(westOut, 8.5f, southOut),
                new Vector3(gate.x - 6f, 8.0f, southOut + 1f),
                new Vector3(gate.x, 6.5f, gate.z - 1.5f),
                new Vector3(gate.x, 3.5f, gate.z + 6f),
                landing,
            };

            p.RidePoints = new Transform[points.Length];
            float length = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                p.RidePoints[i] = MakePoint("Point_" + (i + 1).ToString("00"), group, points[i]);
                if (i > 0) length += Vector3.Distance(points[i - 1], points[i]);
            }
            p.RideLength = length;

            // Eight gold stars, one on each of the first eight corners after the landing, 1 m above the path (the curve passes
            // through its own points, so a star over a point is always on the path whatever the speed).
            Mesh starMesh = CreateStarMesh();
            Material starMaterial = StarMat;
            p.RideStars = new GameObject[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                Vector3 position = points[i + 1] + Vector3.up * 1f;
                var star = new GameObject("RideStar_" + (i + 1));
                star.transform.SetParent(group, false);
                star.transform.position = position;
                // The star mesh is 0.5 m across at scale 1; the contract wants 0.4 m.
                star.transform.localScale = Vector3.one * 0.8f;
                star.AddComponent<MeshFilter>().sharedMesh = starMesh;
                var renderer = star.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = starMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                star.SetActive(false);
                p.RideStars[i] = star;
            }
        }
    }
}
