using System.Collections.Generic;
using System.Linq;
using CoolSchool.ToyRoom;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.ModularBuilding;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The Toy Room's furniture: the stepped stands (a weapon rack on the west side, a hat and cart podium on the east side), the hat board
    /// above the podium, the plush shelf and the medal board on the north wall, the dress-up mirror, and the rug, beanbags and toy chest. Every catalog item gets a
    /// ToyShelfSlot (the item itself is put there at run time by the ToyShelf, as a model or a black silhouette).
    /// The stands run along the side walls, because the camera looks north: a row of items along z stands one behind the other, but
    /// the stand's columns (steps) are side by side on the screen, so nothing hides anything.
    /// Everything is in the building's own frame (the root is at the origin while this is built).
    /// </summary>
    internal static partial class ToyRoomBuilder
    {
        // ---- Stands ----
        const float TierWidth = 0.55f;
        const float TierEndMargin = 0.15f;

        // ---- Plush shelf and medal board (north wall) ----
        const float ShelfBaseHeight = 0.5f;
        const float ShelfUpperHeight = 1.15f;
        const float ShelfDepth = 0.5f;
        const float ShelfMaxWidth = 3.4f;
        const float PlushSize = 0.38f;
        const float BoardBottom = 1.75f;
        const float BoardHeight = 1.1f;
        const int MedalColumns = 5;
        const int MedalRows = 4;

        // ---- Hat board (east wall, above the hat and cart podium) ----
        const float HatBoardBottom = 1.2f;
        const float HatBoardHeight = 1.1f;
        const float HatBoardDepth = 0.05f;
        const float HatPegLength = 0.42f;
        const float HatPegRadius = 0.035f;
        const float HatSize = 0.38f;
        const float HatPegTopRow = 1.85f;
        const float HatPegBottomRow = 1.4f;
        const float HatRowSpacing = 0.65f;

        // ---- Mirror ----
        const float MirrorWidth = 1.5f;
        const float MirrorBottom = 0.2f;
        const float MirrorHeight = 2.2f;
        const float MirrorFrame = 0.1f;

        static readonly PrefabRef RugPrefab = new PrefabRef("SM_Prop_Rug_01", "PolygonTown");
        static readonly PrefabRef FloorMatPrefab = new PrefabRef("SM_Prop_FloorMat_01", "PolygonTown");
        static readonly PrefabRef[] CushionPrefabs =
        {
            new PrefabRef("SM_Prop_Cushion_01", "PolygonTown"),
            new PrefabRef("SM_Prop_Cushion_02", "PolygonTown"),
            new PrefabRef("SM_Prop_Cushion_03", "PolygonTown"),
        };
        static readonly PrefabRef ChestPrefab = new PrefabRef("SM_Gen_Prop_Chest_01", "PolygonGeneric");
        static readonly PrefabRef[] BlockPrefabs = { Kids("SM_Prop_Toy_Block_01"), Kids("SM_Prop_Toy_Block_02"), Kids("SM_Prop_Toy_Block_03") };
        static readonly PrefabRef[] BallPrefabs = { Kids("SM_Prop_Ball_01"), Kids("SM_Prop_Ball_03"), Kids("SM_Prop_Ball_05") };
        // A kite on the wall instead of a poster: the city posters have words (and a skull), and he can not read.
        static readonly PrefabRef WallKitePrefab = Kids("SM_Prop_Kite_01");
        static readonly PrefabRef PlantPrefab = new PrefabRef("SM_Prop_PotPlant_01", "PolygonTown");

        /// <summary>One step of a stand: a column along the wall, with the items standing on it in a row.</summary>
        sealed class Column
        {
            public string Name;
            public float X;
            public float Height;
            public Color Colour;
            public string[] Ids;
            public float DisplaySize;
            /// <summary>A peg under each item (hats), this tall.</summary>
            public float PegHeight;
            public float ZMin, ZMax;
            /// <summary>Items stand alternately this far to either side of the column's middle, so a long row does not hide itself from the camera.</summary>
            public float Zigzag;
        }

        /// <summary>The x of the mirror's middle: in the north-east, with the hat and cart stand to the south of it.</summary>
        static float MirrorCentreX(Plan plan) => plan.HalfX - 1.95f;

        // ----- Entry point ---------------------------------------------------------------------

        static void BuildDisplays(Transform props, Transform root, Plan plan, ToyRoomLayout layout)
        {
            Material silhouette = ToyRoomAssets.Silhouette();
            var slots = new List<ToyShelfSlot>();
            Transform stands = NewGroup("Stands", props);
            Transform slotGroup = NewGroup("Slots", root);

            BuildWeaponRack(stands, slotGroup, plan, silhouette, slots);
            BuildHatAndCartPodium(stands, slotGroup, plan, silhouette, slots);
            BuildHatBoard(stands, slotGroup, plan, silhouette, slots);
            BuildPlushShelf(stands, slotGroup, plan, silhouette, slots, out float shelfCentreX, out float shelfWidth);
            MedalBoard board = BuildMedalBoard(root, plan, shelfCentreX, shelfWidth);
            BuildMirror(stands, plan);
            BuildDecoration(NewGroup("Decoration", props), plan, shelfCentreX + shelfWidth * 0.5f);
            BuildWardrobeSpots(root, plan, layout);

            Transform shelfObject = NewGroup("ToyShelf", root);
            var shelf = shelfObject.gameObject.AddComponent<ToyShelf>();
            SetObjectArray(shelf, "slots", slots.Cast<Object>().ToList());
            SetObject(shelf, "medalBoard", board);
            SetFloat(shelf, "talkRadius", 1.4f);
            layout.Shelf = shelf;
            layout.Board = board;
        }

        /// <summary>After the root has moved: the parts that are stored in world space.</summary>
        static void FinishDisplays(ToyRoomLayout layout, Vector3 centre)
        {
            if (layout.Shelf != null) SetBounds(layout.Shelf, "roomArea", layout.RoomArea);
        }

        // ----- Weapon rack (west) --------------------------------------------------------------

        /// <summary>
        /// Three steps along the west wall, rising toward the wall: the plain swords (lowest, nearest the room), the gold swords, and the
        /// shields against the wall. Swords stand upright. The nine plain swords stand in a zigzag, so none hides the next one.
        /// </summary>
        static void BuildWeaponRack(Transform stands, Transform slotGroup, Plan plan, Material silhouette, List<ToyShelfSlot> slots)
        {
            float zMin = -plan.HalfZ + 0.3f;
            float zMax = plan.HalfZ - 1.05f;
            float wall = -plan.HalfX;

            var columns = new[]
            {
                new Column { Name = "Shields", X = wall + TierWidth * 0.5f, Height = 0.54f, Colour = new Color(0.72f, 0.58f, 0.95f), DisplaySize = 0.55f,
                    Ids = new[] { "shield.basic", "shield.round", "shield.silver", "gold.shield.round" } },
                new Column { Name = "GoldSwords", X = wall + TierWidth * 1.5f, Height = 0.36f, Colour = new Color(1f, 0.82f, 0.3f), DisplaySize = 0.8f,
                    Ids = new[] { "gold.sword.wood", "gold.sword.pirate", "gold.sword.plastic", "gold.sword.foam_big" } },
                new Column { Name = "Swords", X = wall + TierWidth * 2.5f, Height = 0.18f, Colour = new Color(0.45f, 0.72f, 1f), DisplaySize = 0.8f,
                    Zigzag = 0.13f,
                    Ids = new[] { "sword.foam", "sword.plastic", "sword.wood", "sword.pirate", "sword.foam_big",
                        "sword.wand", "sword.foam_4", "sword.staff", "sword.plastic_3" } },
            };
            foreach (Column column in columns)
            {
                column.ZMin = zMin;
                column.ZMax = zMax;
                BuildColumn(stands, slotGroup, column, silhouette, slots);
            }
        }

        // ----- Hat and cart podium (east) -------------------------------------------------------

        /// <summary>
        /// Four steps along the east wall, south of the mirror: a low podium with the three cart bodies and the flag, stickers and gold paint
        /// as minis, and next to the wall the first hats on pegs (plain, then gold). The newer hats hang on the hat board above.
        /// </summary>
        static void BuildHatAndCartPodium(Transform stands, Transform slotGroup, Plan plan, Material silhouette, List<ToyShelfSlot> slots)
        {
            float zMin = -plan.HalfZ + 0.3f;
            float zMax = plan.HalfZ - 2.35f;
            float inner = plan.HalfX - TierWidth * 4f;

            var columns = new[]
            {
                new Column { Name = "Carts", X = inner + TierWidth * 0.5f, Height = 0.16f, Colour = new Color(1f, 0.62f, 0.3f), DisplaySize = 0.5f,
                    Ids = new[] { "cart.racer_1", "cart.racer_2", "cart.racer_3" } },
                new Column { Name = "CartExtras", X = inner + TierWidth * 1.5f, Height = 0.16f, Colour = new Color(1f, 0.5f, 0.6f), DisplaySize = 0.5f,
                    Ids = new[] { "cart.flag", "cart.stickers", "cart.gold" } },
                new Column { Name = "Hats", X = inner + TierWidth * 2.5f, Height = 0.34f, Colour = new Color(0.45f, 0.82f, 0.5f), DisplaySize = 0.34f, PegHeight = 0.2f,
                    Ids = new[] { "hat.knight_helmet", "hat.cap", "hat.pirate" } },
                new Column { Name = "GoldHats", X = inner + TierWidth * 3.5f, Height = 0.34f, Colour = new Color(1f, 0.82f, 0.3f), DisplaySize = 0.34f, PegHeight = 0.2f,
                    Ids = new[] { "gold.hat.knight_helmet", "gold.hat.cap", "gold.hat.pirate" } },
            };
            foreach (Column column in columns)
            {
                column.ZMin = zMin;
                column.ZMax = zMax;
                BuildColumn(stands, slotGroup, column, silhouette, slots);
            }
        }

        /// <summary>The step itself (a solid block) and a slot for each item, spread evenly along it.</summary>
        static void BuildColumn(Transform stands, Transform slotGroup, Column column, Material silhouette, List<ToyShelfSlot> slots)
        {
            float length = column.ZMax - column.ZMin + TierEndMargin * 2f;
            float centreZ = (column.ZMin + column.ZMax) * 0.5f;
            Material material = ToyRoomAssets.Lit("ToyRoomStand_" + column.Name, column.Colour, 0.3f);
            Transform block = Box("Stand_" + column.Name, stands, new Vector3(column.X, FloorY + column.Height * 0.5f, centreZ),
                new Vector3(TierWidth - 0.02f, column.Height, length), material, true);
            FinishEnvironmentObject(block.gameObject);

            float top = FloorY + column.Height;
            float[] zs = Spread(column.Ids.Length, column.ZMin, column.ZMax, 0.75f);
            Material peg = column.PegHeight > 0f ? ToyRoomAssets.Lit("ToyRoomPeg_" + column.Name, new Color(0.62f, 0.42f, 0.25f), 0.2f) : null;
            for (int i = 0; i < column.Ids.Length; i++)
            {
                float y = top;
                if (column.PegHeight > 0f)
                {
                    // The cylinder primitive is 2 m tall, so half the wanted height is its y scale.
                    Transform post = Shape(PrimitiveType.Cylinder, "Peg_" + column.Ids[i], stands,
                        new Vector3(column.X, top + column.PegHeight * 0.5f, zs[i]), new Vector3(0.09f, column.PegHeight * 0.5f, 0.09f), peg);
                    post.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    y += column.PegHeight;
                }
                float x = column.X + (i % 2 == 0 ? -column.Zigzag : column.Zigzag);
                slots.Add(MakeSlot(slotGroup, column.Ids[i], new Vector3(x, y, zs[i]), column.DisplaySize, silhouette));
            }
        }

        // ----- Hat board (east wall) -------------------------------------------------------------

        /// <summary>
        /// A wooden board on the east wall above the hat and cart podium, with two rows of pegs sticking out into the room and a hat resting
        /// on each peg (the seven newer hats). The hats face south, toward the follow camera; the lower row hangs between the upper ones.
        /// </summary>
        static void BuildHatBoard(Transform stands, Transform slotGroup, Plan plan, Material silhouette, List<ToyShelfSlot> slots)
        {
            string[] topRow = { "hat.crown", "hat.astronaut", "hat.wizard", "hat.viking" };
            string[] bottomRow = { "hat.propeller", "hat.fireman", "hat.cowboy" };

            float wall = plan.HalfX;
            float zStart = -plan.HalfZ + 0.45f;
            float[] topZ = Spread(topRow.Length, zStart, zStart + HatRowSpacing * topRow.Length, HatRowSpacing);
            var bottomZ = new float[bottomRow.Length];
            for (int i = 0; i < bottomZ.Length; i++) bottomZ[i] = (topZ[i] + topZ[i + 1]) * 0.5f;

            float length = topZ[topZ.Length - 1] - topZ[0] + 0.8f;
            float centreZ = (topZ[0] + topZ[topZ.Length - 1]) * 0.5f;
            float centreY = HatBoardBottom + HatBoardHeight * 0.5f;

            Material wood = ToyRoomAssets.Lit("ToyRoomHatBoard", new Color(0.78f, 0.55f, 0.32f), 0.25f);
            Material trim = ToyRoomAssets.Lit("ToyRoomHatBoardTrim", new Color(1f, 0.78f, 0.2f), 0.7f, 0.4f);
            Material peg = ToyRoomAssets.Lit("ToyRoomPeg_HatBoard", new Color(0.62f, 0.42f, 0.25f), 0.2f);

            Transform group = NewGroup("HatBoard", stands);
            Box("Trim", group, new Vector3(wall - 0.01f, centreY, centreZ), new Vector3(0.02f, HatBoardHeight + 0.12f, length + 0.12f), trim, false);
            Box("Board", group, new Vector3(wall - HatBoardDepth * 0.5f, centreY, centreZ), new Vector3(HatBoardDepth, HatBoardHeight, length), wood, false);

            // Pegs lie along x (the cylinder is 2 m tall, so half the length is its y scale); the hat rests on top of each, in its middle.
            float pegX = wall - HatBoardDepth - HatPegLength * 0.5f;
            for (int row = 0; row < 2; row++)
            {
                string[] ids = row == 0 ? topRow : bottomRow;
                float[] zs = row == 0 ? topZ : bottomZ;
                float pegY = row == 0 ? HatPegTopRow : HatPegBottomRow;
                for (int i = 0; i < ids.Length; i++)
                {
                    Transform post = Shape(PrimitiveType.Cylinder, "Peg_" + ids[i], group, new Vector3(pegX, pegY, zs[i]),
                        new Vector3(HatPegRadius * 2f, HatPegLength * 0.5f, HatPegRadius * 2f), peg);
                    post.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    post.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    slots.Add(MakeSlot(slotGroup, ids[i], new Vector3(pegX, pegY + HatPegRadius, zs[i]), HatSize, silhouette));
                }
            }
        }

        // ----- Plush shelf (north wall, left) ----------------------------------------------------

        /// <summary>
        /// A cabinet with an upper board against the north wall: seven plush toys below (the baby dragon among them), the three gold ones and four newer ones above. The
        /// right edge stays clear of the mirror. Returns the middle and the width, for the medal board above it.
        /// </summary>
        static void BuildPlushShelf(Transform stands, Transform slotGroup, Plan plan, Material silhouette, List<ToyShelfSlot> slots,
            out float centreX, out float width)
        {
            float left = -plan.HalfX + 0.15f;
            float right = Mathf.Min(left + ShelfMaxWidth, MirrorCentreX(plan) - MirrorWidth * 0.5f - 0.5f);
            width = Mathf.Max(1.4f, right - left);
            centreX = left + width * 0.5f;
            float wallZ = plan.HalfZ;
            float centreZ = wallZ - ShelfDepth * 0.5f - 0.01f;

            Material cabinet = ToyRoomAssets.Lit("ToyRoomCabinet", new Color(1f, 0.72f, 0.82f), 0.3f);
            Material wood = ToyRoomAssets.Lit("ToyRoomShelfBoard", new Color(0.95f, 0.78f, 0.55f), 0.3f);

            Transform baseBlock = Box("PlushCabinet", stands, new Vector3(centreX, FloorY + ShelfBaseHeight * 0.5f, centreZ),
                new Vector3(width, ShelfBaseHeight, ShelfDepth), cabinet, true);
            FinishEnvironmentObject(baseBlock.gameObject);

            // The upper level: a back panel, two sides and a board, open at the front.
            float backHeight = ShelfUpperHeight + 0.55f;
            Box("PlushBack", stands, new Vector3(centreX, FloorY + backHeight * 0.5f, wallZ - 0.02f), new Vector3(width, backHeight, 0.04f), cabinet, false);
            Box("PlushBoard", stands, new Vector3(centreX, FloorY + ShelfUpperHeight - 0.025f, centreZ), new Vector3(width, 0.05f, ShelfDepth), wood, false);
            foreach (float side in new[] { -1f, 1f })
                Box("PlushSide", stands, new Vector3(centreX + side * (width * 0.5f - 0.025f), FloorY + backHeight * 0.5f, centreZ),
                    new Vector3(0.05f, backHeight, ShelfDepth), wood, false);

            string[] lower = { "plush.dino", "plush.unicorn", "plush.bear", "plush.parrot", "plush.dragon", "plush.lion", "plush.rabbit" };
            string[] upper = { "gold.plush.dino", "gold.plush.unicorn", "gold.plush.bear", "plush.elephant", "plush.duck", "plush.pig", "plush.monster" };
            float[] lowerX = Spread(lower.Length, left + 0.15f, left + width - 0.15f, 0.8f);
            float[] upperX = Spread(upper.Length, left + 0.15f, left + width - 0.15f, 0.8f);
            for (int i = 0; i < lower.Length; i++)
                slots.Add(MakeSlot(slotGroup, lower[i], new Vector3(lowerX[i], FloorY + ShelfBaseHeight, centreZ), PlushSize, silhouette));
            for (int i = 0; i < upper.Length; i++)
                slots.Add(MakeSlot(slotGroup, upper[i], new Vector3(upperX[i], FloorY + ShelfUpperHeight, centreZ), PlushSize, silhouette));
        }

        // ----- Medal board ---------------------------------------------------------------------

        /// <summary>A navy board with a gold edge above the plush shelf and a 5 x 4 grid of spots for the medals (row by row from the top left).</summary>
        static MedalBoard BuildMedalBoard(Transform root, Plan plan, float centreX, float width)
        {
            Transform group = NewGroup("MedalBoard", root);
            float wallZ = plan.HalfZ;
            float centreY = BoardBottom + BoardHeight * 0.5f;

            Material navy = ToyRoomAssets.Lit("ToyRoomBoard", new Color(0.17f, 0.2f, 0.46f), 0.2f);
            Material gold = ToyRoomAssets.Lit("ToyRoomBoardTrim", new Color(1f, 0.78f, 0.2f), 0.7f, 0.4f);
            Box("Trim", group, new Vector3(centreX, centreY, wallZ - 0.02f), new Vector3(width + 0.16f, BoardHeight + 0.16f, 0.04f), gold, false);
            Box("Board", group, new Vector3(centreX, centreY, wallZ - 0.045f), new Vector3(width, BoardHeight, 0.04f), navy, false);

            // The spots hang just in front of the board and face south, toward the follow camera.
            var spots = new List<Transform>();
            float marginX = 0.3f;
            float dx = (width - marginX * 2f) / (MedalColumns - 1);
            float dy = (BoardHeight - 0.3f) / (MedalRows - 1);
            for (int row = 0; row < MedalRows; row++)
            {
                for (int column = 0; column < MedalColumns; column++)
                {
                    Vector3 position = new Vector3(centreX - width * 0.5f + marginX + column * dx,
                        BoardBottom + BoardHeight - 0.15f - 0.05f - row * dy, wallZ - 0.09f);
                    spots.Add(NewAnchor($"Spot_{row * MedalColumns + column + 1}", group, position, Quaternion.Euler(0f, 180f, 0f)));
                }
            }

            var board = group.gameObject.AddComponent<MedalBoard>();
            SetObjectArray(board, "spots", spots.Cast<Object>().ToList());
            SetObject(board, "goldMaterial", ToyRoomAssets.Lit("ToyRoomMedalGold", new Color(1f, 0.78f, 0.15f), 0.8f, 0.5f));
            SetObject(board, "ribbonMaterialA", ToyRoomAssets.Lit("ToyRoomMedalRibbonRed", new Color(0.9f, 0.2f, 0.2f), 0.2f));
            SetObject(board, "ribbonMaterialB", ToyRoomAssets.Lit("ToyRoomMedalRibbonBlue", new Color(0.2f, 0.4f, 0.9f), 0.2f));
            SetFloat(board, "medalSize", 0.22f);
            return board;
        }

        // ----- Mirror --------------------------------------------------------------------------

        /// <summary>The dress-up mirror: a pink frame on the north wall with a light-blue glossy glass.</summary>
        static void BuildMirror(Transform stands, Plan plan)
        {
            float centreX = MirrorCentreX(plan);
            float wallZ = plan.HalfZ;
            float centreY = MirrorBottom + MirrorHeight * 0.5f;

            Material frame = ToyRoomAssets.Lit("ToyRoomMirrorFrame", new Color(1f, 0.55f, 0.72f), 0.45f);
            Material glass = ToyRoomAssets.Lit("ToyRoomMirrorGlass", new Color(0.68f, 0.88f, 1f), 0.95f, 0.2f, 0.35f);

            Transform group = NewGroup("Mirror", stands);
            Box("Backing", group, new Vector3(centreX, centreY, wallZ - 0.03f), new Vector3(MirrorWidth, MirrorHeight, 0.06f), frame, false);

            // The glass is a quad; Unity's quad looks toward -Z, which is south, where the camera and the kid are.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Glass";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(group, false);
            quad.transform.localPosition = new Vector3(centreX, centreY, wallZ - 0.07f);
            quad.transform.localScale = new Vector3(MirrorWidth - MirrorFrame * 2f, MirrorHeight - MirrorFrame * 2f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            if (glass != null) renderer.sharedMaterial = glass;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The frame's four sides stand a little in front of the glass.
            float sideZ = wallZ - 0.06f;
            Box("FrameTop", group, new Vector3(centreX, MirrorBottom + MirrorHeight - MirrorFrame * 0.5f, sideZ), new Vector3(MirrorWidth, MirrorFrame, 0.08f), frame, false);
            Box("FrameBottom", group, new Vector3(centreX, MirrorBottom + MirrorFrame * 0.5f, sideZ), new Vector3(MirrorWidth, MirrorFrame, 0.08f), frame, false);
            Box("FrameLeft", group, new Vector3(centreX - MirrorWidth * 0.5f + MirrorFrame * 0.5f, centreY, sideZ), new Vector3(MirrorFrame, MirrorHeight, 0.08f), frame, false);
            Box("FrameRight", group, new Vector3(centreX + MirrorWidth * 0.5f - MirrorFrame * 0.5f, centreY, sideZ), new Vector3(MirrorFrame, MirrorHeight, 0.08f), frame, false);
        }

        // ----- Decoration ----------------------------------------------------------------------

        /// <summary>A rug, a door mat, two beanbags, a toy chest with blocks and balls, a kite on the wall and a plant. Props that are missing are skipped.</summary>
        static void BuildDecoration(Transform parent, Plan plan, float shelfRight)
        {
            float hx = plan.HalfX;
            float hz = plan.HalfZ;

            PlaceScaled(parent, RugPrefab, 2.6f, 0f, 0f, 0f, FloorY, false);
            PlaceProp(parent, FloorMatPrefab, Vector3.one, 0f, 0f, -hz + 0.8f, FloorY, false);

            PlaceScaled(parent, CushionPrefabs[0], 0.95f, 25f, -1.15f, 1.15f, FloorY, false);
            PlaceScaled(parent, CushionPrefabs[1], 0.8f, -30f, -0.95f, 0.2f, FloorY, false);

            GameObject chest = PlaceScaled(parent, ChestPrefab, 0.95f, 180f, -1.15f, -hz + 0.6f, FloorY, true);
            float blockBase = FloorY;
            for (int i = 0; i < BlockPrefabs.Length; i++)
                PlaceScaled(parent, BlockPrefabs[i], 0.24f, 20f * i, -0.55f + 0.18f * i, -hz + 0.45f + 0.1f * (i % 2), blockBase, false);
            for (int i = 0; i < BallPrefabs.Length; i++)
                PlaceScaled(parent, BallPrefabs[i], 0.3f, 0f, 0.35f - 0.7f * i, -0.2f + 0.55f * i, FloorY, false);

            // A kite on the north wall between the plush shelf and the mirror, if there is room.
            float gapLeft = shelfRight + 0.1f;
            float gapRight = MirrorCentreX(plan) - MirrorWidth * 0.5f - 0.1f;
            if (gapRight - gapLeft > 0.7f)
            {
                HangOnNorthWall(parent, WallKitePrefab, Mathf.Min(0.9f, gapRight - gapLeft), (gapLeft + gapRight) * 0.5f, 1.05f, hz);
            }

            PlaceScaled(parent, PlantPrefab, 0.9f, 0f, hx - 0.4f, hz - 0.45f, FloorY, true);
            if (chest == null) Debug.Log("ToyRoom: no toy chest prop found; the room has no chest.");
        }

        // ----- Helpers -------------------------------------------------------------------------

        /// <summary>
        /// Hangs a flat prop (a kite) on the north wall, its flat face toward the room. Its thinnest side is turned to point into the room,
        /// whatever way the model was made. Skipped quietly when the prop is missing.
        /// </summary>
        static void HangOnNorthWall(Transform parent, PrefabRef reference, float biggest, float x, float bottomY, float wallZ)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return;

            GameObject prop = Spawn(prefab, parent);
            prop.name = "WallKite";
            if (!TryGetBounds(prop, out Bounds bounds))
            {
                Object.DestroyImmediate(prop);
                return;
            }

            // Lying flat (thinnest side up): stand it up on the wall. Then the thin side must be along z.
            Quaternion turn = Quaternion.Euler(0f, 180f, 0f);
            if (bounds.size.y < Mathf.Min(bounds.size.x, bounds.size.z) * 0.7f) turn = Quaternion.Euler(90f, 180f, 0f);
            prop.transform.rotation = turn;
            if (TryGetBounds(prop, out bounds) && bounds.size.x < bounds.size.z * 0.8f) prop.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * turn;

            if (TryGetBounds(prop, out bounds))
            {
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (longest > 0.01f) prop.transform.localScale *= biggest / longest;
            }
            AlignToBounds(prop, x, wallZ - 0.1f, bottomY, false);
            PushAgainstWall(prop, Vector3.back, wallZ, 0.02f);
            MakeDecor(prop);
        }

        /// <summary>Spawns a prop scaled so its biggest side is targetBiggest, turned about the vertical, standing with its bottom middle at (x, z) and baseY.</summary>
        static GameObject PlaceScaled(Transform parent, PrefabRef reference, float targetBiggest, float yaw, float x, float z, float baseY, bool collide)
        {
            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            GameObject prop = Spawn(prefab, parent);
            prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (TryGetBounds(prop, out Bounds bounds))
            {
                float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (biggest > 0.01f) prop.transform.localScale *= targetBiggest / biggest;
            }
            AlignToBounds(prop, x, z, baseY, false);

            if (collide) FinishEnvironmentObject(prop);
            else MakeDecor(prop);
            return prop;
        }

        /// <summary>An empty ToyShelfSlot where an item stands, facing south (toward the follow camera).</summary>
        static ToyShelfSlot MakeSlot(Transform parent, string itemId, Vector3 position, float displaySize, Material silhouette)
        {
            var go = new GameObject("Slot_" + itemId);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var slot = go.AddComponent<ToyShelfSlot>();
            SetText(slot, "itemId", itemId);
            SetFloat(slot, "displaySize", displaySize);
            SetObject(slot, "silhouetteMaterial", silhouette);
            return slot;
        }

        /// <summary>Evenly spaced centres in min..max (at most maxSpacing apart), the row centred in the range.</summary>
        static float[] Spread(int count, float min, float max, float maxSpacing)
        {
            var result = new float[count];
            if (count == 0) return result;

            float spacing = count > 1 ? Mathf.Min(maxSpacing, (max - min) / count) : 0f;
            float start = (min + max) * 0.5f - spacing * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++) result[i] = start + spacing * i;
            return result;
        }

        static Transform Box(string name, Transform parent, Vector3 centre, Vector3 size, Material material, bool collide)
        {
            return Shape(PrimitiveType.Cube, name, parent, centre, size, material, collide);
        }

        static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 centre, Vector3 scale, Material material, bool collide = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }
}
