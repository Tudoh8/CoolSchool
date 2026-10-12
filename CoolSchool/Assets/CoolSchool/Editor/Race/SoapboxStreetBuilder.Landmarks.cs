using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The start area (banner, the announcer's stand, the return point) and the finish area (arch, podium, camera and spectator spots).</summary>
    internal static partial class SoapboxStreetBuilder
    {
        const float PostOffsetX = RoadHalf + SidewalkWidth * 0.5f;
        const float PodiumBlockSize = 1.5f;

        // ---- Start area (d < 0) ----

        static void BuildStartArea(StreetLayout layout, Transform parent)
        {
            Transform group = NewGroup("StartArea", parent);

            // The start line transform: lane 1 at d = 0, facing north. The RaceTrack measures everything from here.
            layout.StartLine = NewPoint("StartLine", group, new Vector3(StreetX, RoadY, StartLineZ));

            layout.ZonePosition = new Vector3(StreetX, RoadY, StartLineZ - 4f);
            layout.ReturnPoint = NewPoint("ReturnPoint", group, new Vector3(StreetX, RoadY, StartLineZ - 7f));
            layout.ReturnPoint.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            BuildBanner("StartBanner", group, StartLineZ, 4.4f);

            // The announcer's stand on the west sidewalk, just past the start line, facing the street.
            Material standMaterial = Mat("RaceStand", new Color(0.85f, 0.2f, 0.18f), 0.3f);
            float standZ = StartLineZ + 3.2f;
            Transform stand = Prim(PrimitiveType.Cube, "AnnouncerStand", group, new Vector3(WestSidewalkX, SidewalkY + 0.15f, standZ),
                new Vector3(1.5f, 0.3f, 1.5f), Quaternion.identity, standMaterial, true, true);
            MarkStatic(stand.gameObject);
            layout.AnnouncerStart = NewPoint("AnnouncerStartSpot", group, new Vector3(WestSidewalkX, SidewalkY + 0.3f, standZ));
            layout.AnnouncerStart.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        }

        /// <summary>
        /// A banner across the street: a post on each sidewalk and a chequered board between them (chequered texture on both sides),
        /// with a red cap on each post. Returns the board.
        /// </summary>
        static Transform BuildBanner(string name, Transform parent, float z, float height)
        {
            Transform group = NewGroup(name, parent);
            Material postMaterial = Mat("RacePost", new Color(0.92f, 0.92f, 0.9f), 0.25f);
            Material capMaterial = Mat("RaceStand", new Color(0.85f, 0.2f, 0.18f), 0.3f);

            foreach (float sign in new[] { -1f, 1f })
            {
                float x = StreetX + sign * PostOffsetX;
                Prim(PrimitiveType.Cylinder, "Post", group, new Vector3(x, SidewalkY + height * 0.5f, z), new Vector3(0.3f, height * 0.5f, 0.3f),
                    Quaternion.identity, postMaterial, false, true);
                Prim(PrimitiveType.Sphere, "Cap", group, new Vector3(x, SidewalkY + height + 0.1f, z), Vector3.one * 0.42f,
                    Quaternion.identity, capMaterial);
            }

            float width = PostOffsetX * 2f;
            const float boardHeight = 1.0f;
            Material chequer = ChequerBannerMaterial(name + "Chequer", width, boardHeight);
            Transform board = Prim(PrimitiveType.Cube, "Board", group, new Vector3(StreetX, SidewalkY + height - boardHeight * 0.5f - 0.05f, z),
                new Vector3(width, boardHeight, 0.12f), Quaternion.identity, chequer, false, true);
            MarkStatic(group.gameObject);
            return board;
        }

        // ---- Finish area ----

        static void BuildFinishArea(StreetLayout layout, Transform parent)
        {
            Transform group = NewGroup("FinishArea", parent);
            float finishZ = ZAt(FinishDistance);

            // The arch: the banner plus a line of bunting under it. The board is not static (it is the confetti's target).
            Transform arch = NewGroup("FinishArch", group);
            Transform board = BuildBanner("FinishBanner", arch, finishZ, 4.6f);
            layout.FinishArchRoot = arch;
            layout.FinishBanner = board;
            BuildBunting(arch, finishZ);

            BuildPodium(layout, group);

            float podiumZ = ZAt(PodiumDistance);
            layout.AnnouncerFinish = NewPoint("AnnouncerFinishSpot", group, new Vector3(WestSidewalkX, SidewalkY, podiumZ + 1.5f));
            layout.AnnouncerFinish.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

            layout.PodiumCameraSpot = NewPoint("PodiumCameraSpot", group, new Vector3(StreetX, 3f, podiumZ - 7f));
            Vector3 lookAt = new Vector3(StreetX, 1.4f, podiumZ);
            layout.PodiumCameraSpot.rotation = Quaternion.LookRotation(lookAt - layout.PodiumCameraSpot.position, Vector3.up);

            // Four spectators on the sidewalks near the podium, two each side, facing the street.
            layout.SpectatorSpots = new[]
            {
                NewPoint("Spectator_1", group, new Vector3(WestSidewalkX, SidewalkY, podiumZ - 5f)),
                NewPoint("Spectator_2", group, new Vector3(EastSidewalkX, SidewalkY, podiumZ - 3f)),
                NewPoint("Spectator_3", group, new Vector3(WestSidewalkX, SidewalkY, podiumZ - 1.5f)),
                NewPoint("Spectator_4", group, new Vector3(EastSidewalkX, SidewalkY, podiumZ + 1f)),
            };
            for (int i = 0; i < layout.SpectatorSpots.Length; i++)
            {
                Vector3 toStreet = i % 2 == 0 ? Vector3.right : Vector3.left;
                layout.SpectatorSpots[i].rotation = Quaternion.LookRotation(toStreet + Vector3.forward * 0.3f, Vector3.up);
            }
        }

        /// <summary>A sagging string of little coloured flags between the two posts, in front of the board.</summary>
        static void BuildBunting(Transform parent, float z)
        {
            Transform bunting = NewGroup("Bunting", parent);
            Color[] colours =
            {
                new Color(0.95f, 0.3f, 0.3f), new Color(1f, 0.82f, 0.2f), new Color(0.3f, 0.78f, 0.4f),
                new Color(0.3f, 0.6f, 1f), new Color(0.78f, 0.45f, 0.95f),
            };
            const int flags = 15;
            float x0 = StreetX - PostOffsetX;
            float x1 = StreetX + PostOffsetX;
            float top = SidewalkY + 3.9f;
            for (int i = 0; i < flags; i++)
            {
                float t = (i + 0.5f) / flags;
                float x = Mathf.Lerp(x0, x1, t);
                float y = top - Mathf.Sin(t * Mathf.PI) * 0.45f - 0.2f;
                Material material = Mat("RaceBunting" + (i % colours.Length), colours[i % colours.Length], 0.2f);
                Prim(PrimitiveType.Cube, "Flag", bunting, new Vector3(x, y, z - 0.12f), new Vector3(0.34f, 0.34f, 0.03f),
                    Quaternion.Euler(0f, 0f, 45f), material);
            }
            MarkStatic(bunting.gameObject);
        }

        /// <summary>Three blocks on the street's middle: 1st in the middle (0.9 m), 2nd left (0.6 m), 3rd right (0.4 m). The spots are their tops.</summary>
        static void BuildPodium(StreetLayout layout, Transform parent)
        {
            Transform group = NewGroup("Podium", parent);
            float z = ZAt(PodiumDistance);
            float[] heights = { 0.9f, 0.6f, 0.4f };
            float[] xs = { StreetX, StreetX - PodiumBlockSize - 0.2f, StreetX + PodiumBlockSize + 0.2f };
            Material[] materials =
            {
                Mat("RaceGold", new Color(1f, 0.8f, 0.2f), 0.7f, 0.8f),
                Mat("RaceSilver", new Color(0.8f, 0.82f, 0.88f), 0.7f, 0.8f),
                Mat("RaceBronze", new Color(0.82f, 0.5f, 0.26f), 0.6f, 0.7f),
            };
            string[] names = { "First", "Second", "Third" };

            layout.PodiumSpots = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                Transform block = Prim(PrimitiveType.Cube, "Podium_" + names[i], group, new Vector3(xs[i], RoadY + heights[i] * 0.5f, z),
                    new Vector3(PodiumBlockSize, heights[i], PodiumBlockSize), Quaternion.identity, materials[i], true, true);
                MarkStatic(block.gameObject);
                layout.PodiumSpots[i] = NewPoint("PodiumSpot_" + (i + 1), group, new Vector3(xs[i], RoadY + heights[i], z));
                // Facing south, toward the chase camera that comes to the podium.
                layout.PodiumSpots[i].rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            }
        }
    }
}
