using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>The neighbourhood along the street: houses (Synty presets), gardens, parked cars, lamps. All static, no colliders.</summary>
    internal static partial class SoapboxStreetBuilder
    {
        // How far the front of a house stands from the picket fence, and the budget of renderers for all the houses together.
        const float YardDepth = 5.5f;
        const float HouseRendererBudget = 4500f;
        const float HouseSpacing = 26f;
        const float HousesFromZ = 36f;
        const float HousesToZ = 506f;
        // The school stands west of the street's east side up to z 33.5; no house on the east side before this z.
        const float EastHousesFromZ = 48f;

        static readonly PrefabRef[] CarPrefabs =
        {
            new PrefabRef("SM_Veh_Car_Small_01", "PolygonCity"),
            new PrefabRef("SM_Veh_Car_Sedan_01", "PolygonCity"),
            new PrefabRef("SM_Veh_Car_Medium_01", "PolygonCity"),
            new PrefabRef("SM_Veh_Car_Van_01", "PolygonCity"),
        };

        static readonly PrefabRef[] TreePrefabs =
        {
            new PrefabRef("SM_Env_Tree_01", "PolygonTown"),
            new PrefabRef("SM_Env_Tree_02", "PolygonTown"),
            new PrefabRef("SM_Env_Tree_03", "PolygonTown"),
            Kids("SM_Env_Tree_Large_01"),
        };

        static readonly PrefabRef[] FlowerPrefabs =
        {
            new PrefabRef("SM_Env_FlowerPatch_01", "PolygonTown"),
            new PrefabRef("SM_Env_FlowerPatch_02", "PolygonTown"),
            new PrefabRef("SM_Env_FlowerPatch_03", "PolygonTown"),
        };

        static readonly PrefabRef MailboxPrefab = new PrefabRef("SM_Prop_Mailbox_01", "PolygonCity");
        static readonly PrefabRef LampPrefab = new PrefabRef("SM_Prop_Streetlamp_01", "PolygonTown");
        static readonly PrefabRef LampFallbackPrefab = Kids("SM_Prop_Park_Lamp_01");

        sealed class HouseInfo
        {
            public GameObject Prefab;
            public string Name;
            public int Renderers;
            public Vector3 Front;
            public bool FrontFound;
        }

        // ---- Houses ----

        static void BuildHouses(StreetLayout layout, Transform parent, float z0)
        {
            Transform group = NewGroup("Houses", parent);
            Transform garden = NewGroup("Gardens", parent);

            var infos = new List<HouseInfo>();
            for (int i = 1; i <= 11; i++)
            {
                GameObject prefab = LoadPrefab(new PrefabRef($"SM_Bld_House_Preset_{i:00}", "PolygonTown"));
                if (prefab == null) continue;

                GameObject probe = Spawn(prefab, group);
                var info = new HouseInfo { Prefab = prefab, Name = prefab.name };
                info.Renderers = probe.GetComponentsInChildren<Renderer>(true).Length;
                info.Front = FindFront(probe, out info.FrontFound);
                Object.DestroyImmediate(probe);
                infos.Add(info);
            }

            if (infos.Count == 0)
            {
                Debug.LogWarning("CoolSchool race street: no SM_Bld_House_Preset prefab found, so the street has no houses.");
                return;
            }
            Debug.Log("CoolSchool race street: house presets: " + string.Join(", ",
                infos.Select(i => $"{i.Name} {i.Renderers} renderers, front {(i.FrontFound ? i.Front.ToString("0") : "unknown (+Z assumed)")}")));

            // Fewer, further apart houses when the presets are heavy.
            float average = (float)infos.Average(i => i.Renderers);
            int perSide = Mathf.FloorToInt((HousesToZ - HousesFromZ) / HouseSpacing) + 1;
            int affordable = Mathf.Max(6, Mathf.FloorToInt(HouseRendererBudget / Mathf.Max(1f, average) / 2f));
            if (perSide > affordable) perSide = affordable;
            float spacing = perSide > 1 ? (HousesToZ - HousesFromZ) / (perSide - 1) : 0f;

            GameObject hedgeA = LoadPrefab(HedgePrefabA);
            GameObject hedgeB = LoadPrefab(HedgePrefabB);
            GameObject hedgeC = LoadPrefab(HedgePrefabC);
            GameObject[] hedges = new[] { hedgeA, hedgeB, hedgeC }.Where(h => h != null).ToArray();

            int placed = 0;
            int renderers = 0;
            foreach (int side in new[] { -1, 1 })
            {
                for (int k = 0; k < perSide; k++)
                {
                    float z = HousesFromZ + k * spacing + (side > 0 ? spacing * 0.5f : 0f);
                    if (side > 0 && (z < EastHousesFromZ || z > HousesToZ + 4f)) continue;

                    HouseInfo info = infos[(k * 5 + (side > 0 ? 3 : 0)) % infos.Count];
                    GameObject house = PlaceHouse(info, group, side, z);
                    placed++;
                    renderers += house.GetComponentsInChildren<Renderer>(true).Length;

                    DecorateHouse(garden, side, z, k, spacing, hedges);
                }
            }
            layout.HouseCount = placed;
            layout.HouseRenderers = renderers;
            Debug.Log($"CoolSchool race street: {placed} houses, {perSide} per side, {spacing:0.0} m apart, about {average:0} renderers each.");
        }

        /// <summary>The house turned so its door side faces the street, its front a yard's depth behind the fence, centred on z.</summary>
        static GameObject PlaceHouse(HouseInfo info, Transform parent, int side, float z)
        {
            GameObject house = Spawn(info.Prefab, parent);
            // West houses (side -1) look east, east houses look west.
            Vector3 wanted = side < 0 ? Vector3.right : Vector3.left;
            float yaw = Vector3.SignedAngle(info.Front, wanted, Vector3.up);
            house.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (TryGetBounds(house, out Bounds b))
            {
                float dx = side < 0 ? (FenceWestX - YardDepth) - b.max.x : (FenceEastX + YardDepth) - b.min.x;
                house.transform.position += new Vector3(dx, -b.min.y, z - b.center.z);
            }
            StripColliders(house);
            MarkStatic(house);
            return house;
        }

        /// <summary>The axis (in the house's own frame) of the side with the front door; +Z when no door is found.</summary>
        static Vector3 FindFront(GameObject house, out bool found)
        {
            found = false;
            if (!TryGetBounds(house, out Bounds b)) return Vector3.forward;

            for (int pass = 0; pass < 2 && !found; pass++)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (Renderer r in house.GetComponentsInChildren<Renderer>(false))
                {
                    string label = r.name + " " + (r.transform.parent != null ? r.transform.parent.name : string.Empty);
                    if (!label.Contains("Door")) continue;
                    if (label.Contains("Interior") || label.Contains("Internal")) continue;
                    if (pass == 0 && label.Contains("Garage")) continue;
                    sum += r.bounds.center;
                    n++;
                }
                if (n == 0) continue;

                Vector3 d = sum / n - b.center;
                float nx = Mathf.Abs(d.x) / Mathf.Max(0.1f, b.extents.x);
                float nz = Mathf.Abs(d.z) / Mathf.Max(0.1f, b.extents.z);
                found = true;
                return nx > nz ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
            }
            return Vector3.forward;
        }

        /// <summary>Hedges, a tree, flowers and a mailbox in front of one house; a parked car on every third driveway.</summary>
        static void DecorateHouse(Transform parent, int side, float z, int index, float spacing, GameObject[] hedges)
        {
            float fenceX = side < 0 ? FenceWestX : FenceEastX;
            float inward = side < 0 ? -1f : 1f;   // from the fence toward the houses
            var random = new System.Random(1000 + index * 7 + (side > 0 ? 500 : 0));

            if (hedges.Length > 0)
            {
                GameObject hedge = hedges[random.Next(hedges.Length)];
                FenceRun(hedge, parent, new Vector2(fenceX + inward * 1.1f, z + 8f), new Vector2(fenceX + inward * 1.1f, z + 12.5f), 0f, true);
                if (index % 3 != 1)
                    FenceRun(hedge, parent, new Vector2(fenceX + inward * 1.1f, z - 12.5f), new Vector2(fenceX + inward * 1.1f, z - 8f), 0f, true);
            }

            if (index % 3 == 1)
            {
                PrefabRef car = CarPrefabs[random.Next(CarPrefabs.Length)];
                // Nose toward the street, in the driveway between the fence and the house.
                Place(car, parent, side < 0 ? 90f : -90f, Fit.Longest, 3.8f, new Vector3(fenceX + inward * 3.0f, 0f, z - 10f), 0f);
            }

            Place(TreePrefabs[random.Next(TreePrefabs.Length)], parent, random.Next(360), Fit.Height, 4.5f + (float)random.NextDouble() * 2f,
                new Vector3(fenceX + inward * 2.8f, 0f, z + spacing * 0.5f), 0f);
            Place(FlowerPrefabs[random.Next(FlowerPrefabs.Length)], parent, 0f, Fit.Widest, 1.6f,
                new Vector3(fenceX + inward * 1.1f, 0f, z + 5.5f), 0f);
            Place(MailboxPrefab, parent, side < 0 ? 90f : -90f, Fit.Height, 1.15f, new Vector3(fenceX + inward * 0.6f, 0f, z - 5f), 0f);
        }

        // ---- Street furniture ----

        /// <summary>Street lamps on the sidewalks (one every 40 m, alternating) and a few trees along the first stretch by the school.</summary>
        static void BuildStreetFurniture(StreetLayout layout, Transform parent, float z0)
        {
            Transform group = NewGroup("Furniture", parent);
            int index = 0;
            for (float z = z0 + 14f; z < RoadEndZ - 6f; z += 40f, index++)
            {
                bool west = index % 2 == 0;
                float x = west ? StreetX - RoadHalf - SidewalkWidth + 0.4f : StreetX + RoadHalf + SidewalkWidth - 0.4f;
                GameObject lamp = Place(LampPrefab, group, 0f, Fit.Height, 3.8f, new Vector3(x, 0f, z), SidewalkY);
                if (lamp == null) Place(LampFallbackPrefab, group, 0f, Fit.Height, 3.8f, new Vector3(x, 0f, z), SidewalkY);
            }

            // The first houses on the west side start at z 36: a few trees on the lawn behind the west fence before them.
            // (The school is only 2.8 m behind the east fence, so nothing grows there.)
            var random = new System.Random(77);
            for (float z = z0 + 5f; z < HousesFromZ - 4f; z += 6f)
                Place(TreePrefabs[random.Next(TreePrefabs.Length)], group, random.Next(360), Fit.Height, 4.5f + (float)random.NextDouble() * 1.5f,
                    new Vector3(FenceWestX - 2.2f, 0f, z), 0f);
        }
    }
}
