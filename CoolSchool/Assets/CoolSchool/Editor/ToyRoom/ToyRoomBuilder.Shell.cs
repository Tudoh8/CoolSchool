using System.Collections.Generic;
using System.Linq;
using CoolSchool.School;
using UnityEngine;
using static CoolSchool.EditorTools.ModularBuilding;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The Toy Room's walls, corner pillars, roof, lights and the interior reveal (the same kit and measuring as the school).</summary>
    internal static partial class ToyRoomBuilder
    {
        static readonly Color LightColor = new Color(1f, 0.86f, 0.62f);
        const float LightIntensity = 1.7f;
        const float LightRange = 8f;

        // ----- Walls ---------------------------------------------------------------------------

        static void BuildWalls(Transform parent, Kit kit, Plan plan, Dictionary<Side, List<GameObject>> wallsBySide)
        {
            foreach (Side side in new[] { Side.South, Side.North, Side.West, Side.East })
            {
                var pieces = new List<GameObject>();
                BuildWallSide(parent, kit, plan, side, pieces);
                wallsBySide[side] = pieces;
            }
        }

        /// <summary>
        /// One side of the rectangle. Each piece is turned so its plaster side faces into the room, and centred on the wall line
        /// by its bounds, so the thin axis sits on the line whatever the pivot is.
        /// </summary>
        static void BuildWallSide(Transform parent, Kit kit, Plan plan, Side side, List<GameObject> pieces)
        {
            bool alongX = side == Side.South || side == Side.North;
            int count = alongX ? plan.PiecesX : plan.PiecesZ;

            Vector3 inward;
            Vector3 start;
            Vector3 along;
            switch (side)
            {
                case Side.South:
                    inward = Vector3.forward;
                    start = new Vector3(plan.WestX, 0f, plan.SouthZ);
                    along = Vector3.right;
                    break;
                case Side.North:
                    inward = Vector3.back;
                    start = new Vector3(plan.WestX, 0f, plan.NorthZ);
                    along = Vector3.right;
                    break;
                case Side.West:
                    inward = Vector3.right;
                    start = new Vector3(plan.WestX, 0f, plan.SouthZ);
                    along = Vector3.forward;
                    break;
                default:
                    inward = Vector3.left;
                    start = new Vector3(plan.EastX, 0f, plan.SouthZ);
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

        /// <summary>The doorway in the middle of the south wall with a window each side of it; the other walls are plain (the displays stand there).</summary>
        static Piece PieceFor(Kit kit, Side side, int index, int count)
        {
            Piece wanted = kit.Wall;
            if (side == Side.South)
                wanted = index == count / 2 ? kit.Door : kit.Window;
            return wanted ?? kit.Wall;
        }

        static void BuildCornerPillars(Transform parent, Kit kit, Plan plan)
        {
            if (kit.Pillar == null) return;

            foreach (float x in new[] { plan.WestX, plan.EastX })
            {
                foreach (float z in new[] { plan.SouthZ, plan.NorthZ })
                {
                    GameObject pillar = Spawn(kit.Pillar.Prefab, parent);
                    pillar.name = "Pillar";
                    AlignToBounds(pillar, x, z, 0f, false);
                    FinishEnvironmentObject(pillar);
                }
            }
        }

        /// <summary>
        /// A flat roof: the ceiling tile twice, once as it is and once upside down on top, so it is solid from below and from above.
        /// No colliders; the follow camera must not bump into it. Returns every roof renderer.
        /// </summary>
        static List<Renderer> BuildRoof(Transform parent, Kit kit, Plan plan)
        {
            Piece tile = kit.Ceiling ?? kit.Floor;
            LayTiles(parent, tile, plan.Width, plan.Depth, RoofOverhang, plan.Height, Quaternion.identity, false);
            LayTiles(parent, tile, plan.Width, plan.Depth, RoofOverhang, plan.Height + 0.02f, Quaternion.Euler(180f, 0f, 0f), false);
            return parent.GetComponentsInChildren<Renderer>(true).ToList();
        }

        // ----- Lights --------------------------------------------------------------------------

        /// <summary>Two warm point lights near the ceiling, one each side of the aisle, and a pink-white one at the mirror.</summary>
        static void BuildLights(Transform parent, Plan plan, ToyRoomLayout layout)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var lightObject = new GameObject(side < 0f ? "WarmLight_West" : "WarmLight_East");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.localPosition = new Vector3(side * plan.HalfX * 0.5f, plan.Height - 0.5f, 0f);
                AddLight(lightObject, LightColor, LightIntensity, LightRange);
            }

            var mirrorLight = new GameObject("MirrorLight");
            mirrorLight.transform.SetParent(parent, false);
            mirrorLight.transform.localPosition = new Vector3(MirrorCentreX(plan), 2.4f, plan.HalfZ - 1.6f);
            AddLight(mirrorLight, new Color(1f, 0.92f, 0.95f), 1.3f, 4.5f);
        }

        static void AddLight(GameObject lightObject, Color colour, float intensity, float range)
        {
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // ----- Interior reveal -----------------------------------------------------------------

        /// <summary>
        /// The roof hides when the player is inside or approaching the door, and so does the south wall (the one between the follow
        /// camera, which sits south of him and looks north, and the room) while the camera is outside. The box is in the root's local
        /// space; the approach side is local -Z, the door wall.
        /// </summary>
        static void AddInteriorReveal(Transform root, Plan plan, List<Renderer> roof, List<GameObject> southWall)
        {
            var reveal = root.gameObject.AddComponent<BuildingInteriorReveal>();
            SetObjectArray(reveal, "hideWhenInside", roof.Cast<Object>().ToList());
            SetObjectArray(reveal, "hideWhenCameraOutside", southWall.Cast<Object>().ToList());

            SetVector3(reveal, "interiorCenter", new Vector3(0f, plan.Height * 0.5f, 0f));
            SetVector3(reveal, "interiorSize", new Vector3(plan.Width - plan.MaxThickness, plan.Height, plan.Depth - plan.MaxThickness));
            SetFloat(reveal, "approachDistance", 3f);
            SetVector3(reveal, "approachDirection", Vector3.back);
        }
    }
}
