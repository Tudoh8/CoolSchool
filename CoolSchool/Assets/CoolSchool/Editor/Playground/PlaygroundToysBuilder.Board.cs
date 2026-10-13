using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Player;
using CoolSchool.Playground;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The sticker board: a free-standing wooden-framed panel on two legs with 12 round sticker spots in a 4 x 3 grid, facing south (toward
    /// the fixed camera). It stands on a free patch of the playground's south side, a few metres north of the spawn and a little to one
    /// side, so the first walk north is not blocked. The spot is found by testing candidate places for colliders, reserved areas, activity
    /// zones and the toys' touch zones, and logged.
    /// </summary>
    public static partial class PlaygroundToysBuilder
    {
        const string MaterialFolder = "Assets/CoolSchool/Materials/Playground";

        const int BoardColumns = 4;
        const int BoardRows = 3;
        const float SpotPitch = 0.72f;
        const float SpotSize = 0.6f;
        const float BoardMargin = 0.4f;
        const float BoardLegHeight = 0.6f;
        const float BoardDepth = 0.12f;

        static void BuildStickerBoard(PlayerController player, Dictionary<string, Sprite> icons)
        {
            Transform environment = GameObject.Find("Environment") != null ? GameObject.Find("Environment").transform : null;
            Vector3 spawn = player != null ? player.transform.position : new Vector3(0f, 0f, -16.9f);

            float width = BoardColumns * SpotPitch + BoardMargin;
            float height = BoardRows * SpotPitch + BoardMargin;
            Vector3 place = FindBoardPlace(spawn, width, out bool free);
            if (!free)
                Debug.LogWarning($"{LogPrefix}sticker board: fallback: no free patch found near the spawn; put it at {ToySetup.Fmt(place)} anyway.");

            var root = new GameObject("StickerBoard");
            if (environment != null) root.transform.SetParent(environment, false);
            root.transform.SetPositionAndRotation(place, Quaternion.identity);

            Material wood = BoardMaterial("StickerBoardWood", new Color(0.55f, 0.36f, 0.2f));
            Material panel = BoardMaterial("StickerBoardPanel", new Color(0.97f, 0.93f, 0.82f));
            Material cap = BoardMaterial("StickerBoardCap", new Color(0.92f, 0.32f, 0.28f));

            float panelCentreY = BoardLegHeight + height * 0.5f;
            Cube(root.transform, "Panel", new Vector3(0f, panelCentreY, 0f), new Vector3(width, height, BoardDepth), panel);

            // The frame: four bars around the panel, a little proud of it.
            const float bar = 0.14f;
            Cube(root.transform, "FrameTop", new Vector3(0f, panelCentreY + height * 0.5f, -0.02f), new Vector3(width + bar, bar, BoardDepth + 0.08f), wood);
            Cube(root.transform, "FrameBottom", new Vector3(0f, panelCentreY - height * 0.5f, -0.02f), new Vector3(width + bar, bar, BoardDepth + 0.08f), wood);
            Cube(root.transform, "FrameLeft", new Vector3(-width * 0.5f, panelCentreY, -0.02f), new Vector3(bar, height, BoardDepth + 0.08f), wood);
            Cube(root.transform, "FrameRight", new Vector3(width * 0.5f, panelCentreY, -0.02f), new Vector3(bar, height, BoardDepth + 0.08f), wood);

            // Two legs, and a coloured cap on top.
            float legX = width * 0.5f - 0.35f;
            Cube(root.transform, "LegLeft", new Vector3(-legX, BoardLegHeight * 0.5f, 0.02f), new Vector3(0.16f, BoardLegHeight, 0.16f), wood);
            Cube(root.transform, "LegRight", new Vector3(legX, BoardLegHeight * 0.5f, 0.02f), new Vector3(0.16f, BoardLegHeight, 0.16f), wood);
            Cube(root.transform, "Cap", new Vector3(0f, panelCentreY + height * 0.5f + bar * 0.5f + 0.06f, -0.02f), new Vector3(width + 0.34f, 0.12f, BoardDepth + 0.2f), cap);

            // The 12 spots, in the order of ToyStickers.AllIds, row by row from the top left.
            var spots = new List<Object>();
            var iconList = new List<Object>();
            float frontZ = -BoardDepth * 0.5f - 0.015f;
            for (int i = 0; i < ToyStickers.AllIds.Length; i++)
            {
                int column = i % BoardColumns;
                int row = i / BoardColumns;
                float x = (column - (BoardColumns - 1) * 0.5f) * SpotPitch;
                float y = panelCentreY + ((BoardRows - 1) * 0.5f - row) * SpotPitch;

                var spot = new GameObject("Spot_" + ToyStickers.AllIds[i]).transform;
                spot.SetParent(root.transform, false);
                spot.localPosition = new Vector3(x, y, frontZ);

                AddSpotSprite(spot, "Rim", 0, SpotSize, 0f);
                AddSpotSprite(spot, "Face", 1, SpotSize * 0.84f, -0.004f);
                Sprite icon = icons != null && icons.TryGetValue(ToyStickers.AllIds[i], out Sprite found) ? found : null;
                Transform iconTransform = AddSpotSprite(spot, "Icon", 2, 1f, -0.008f);
                if (icon != null)
                {
                    // Fit the picture into the middle of the disc whatever its pixel size is.
                    Vector3 size = icon.bounds.size;
                    float fit = SpotSize * 0.66f / Mathf.Max(0.01f, Mathf.Max(size.x, size.y));
                    iconTransform.localScale = new Vector3(fit, fit, 1f);
                }

                spots.Add(spot);
                iconList.Add(icon);
            }

            var intro = new GameObject("IntroPoint").transform;
            intro.SetParent(root.transform, false);
            intro.localPosition = new Vector3(0f, 0f, -1.6f);

            var board = root.AddComponent<StickerBoard>();
            PlaygroundPrototypeBuilder.SetObjectArray(board, "spots", spots);
            PlaygroundPrototypeBuilder.SetObjectArray(board, "icons", iconList);
            PlaygroundPrototypeBuilder.SetObject(board, "introPoint", intro);

            Debug.Log($"{LogPrefix}sticker board at {ToySetup.Fmt(place)} facing south, size {width:0.00} x {height:0.00} m, "
                + $"{spawn.z - place.z:0.0} m north of the spawn at {ToySetup.Fmt(spawn)}; icons: {CountSprites(iconList)} of {iconList.Count}.");
        }

        static int CountSprites(List<Object> list)
        {
            int count = 0;
            foreach (Object o in list) if (o != null) count++;
            return count;
        }

        /// <summary>A SpriteRenderer child of a spot. The sprite itself is set at run time by the board (the disc is generated then).</summary>
        static Transform AddSpotSprite(Transform spot, string name, int order, float scale, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spot, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return go.transform;
        }

        static Transform Cube(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            // The follow camera only tests the Default layer for things in its way: the board is solid for the kid but never pulls the camera in.
            cube.layer = 2;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = size;
            if (material != null) cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube.transform;
        }

        static Material BoardMaterial(string fileName, Color colour)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;

            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool", "Materials");
            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool/Materials", "Playground");

            var material = new Material(lit) { name = fileName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.25f);
            string path = MaterialFolder + "/" + fileName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ---- Finding a free patch ----

        /// <summary>
        /// Candidate places north of the spawn, on the left or right: the nearest free one to the preferred spot wins. Free means nothing solid in
        /// the board's box, outside the reserved areas, away from the activity zones and out of every toy's touch zone.
        /// </summary>
        static Vector3 FindBoardPlace(Vector3 spawn, float width, out bool free)
        {
            Physics.SyncTransforms();
            var zones = new List<ActivityZone>(Object.FindObjectsByType<ActivityZone>(FindObjectsSortMode.None));
            var touch = new List<Bounds>();
            foreach (BoxCollider box in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
                if (box.isTrigger && box.GetComponent<ToyTouchZone>() != null) touch.Add(box.bounds);

            float halfWidth = width * 0.5f + 0.5f;
            const float halfDepth = 0.7f;

            Vector3 best = default;
            float bestScore = float.MaxValue;
            bool found = false;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 preferred = new Vector2(spawn.x + side * 3.6f, spawn.z + 4.5f);
                for (float dz = -1.5f; dz <= 3.5f; dz += 0.5f)
                {
                    for (float dx = -8f; dx <= 8f; dx += 0.5f)
                    {
                        Vector2 centre = new Vector2(spawn.x + dx, spawn.z + 3f + dz);
                        float score = (centre - preferred).sqrMagnitude;
                        if (score >= bestScore) continue;
                        if (!IsBoardPlaceFree(centre, halfWidth, halfDepth, spawn, zones, touch)) continue;

                        bestScore = score;
                        best = new Vector3(centre.x, 0f, centre.y);
                        found = true;
                    }
                }
            }

            free = found;
            if (!found) best = new Vector3(spawn.x - 3.6f, 0f, spawn.z + 4.5f);

            if (Physics.Raycast(new Vector3(best.x, 8f, best.z), Vector3.down, out RaycastHit ground, 20f, ~0, QueryTriggerInteraction.Ignore))
                best.y = ground.point.y;
            return best;
        }

        static bool IsBoardPlaceFree(Vector2 centre, float halfWidth, float halfDepth, Vector3 spawn, List<ActivityZone> zones, List<Bounds> touch)
        {
            // Not in the way of the very first steps.
            if (Mathf.Abs(centre.x - spawn.x) < halfWidth + 0.5f && Mathf.Abs(centre.y - spawn.z) < 3f) return false;

            var footprint = Rect.MinMaxRect(centre.x - halfWidth, centre.y - halfDepth, centre.x + halfWidth, centre.y + halfDepth);
            foreach (Rect reserved in PlaygroundPrototypeBuilder.ReservedAreas)
                if (reserved.Overlaps(footprint)) return false;

            foreach (ActivityZone zone in zones)
            {
                if (zone == null) continue;
                Vector2 zoneCentre = new Vector2(zone.transform.position.x, zone.transform.position.z);
                if (DistanceToRect(footprint, zoneCentre) < zone.Radius + 1.5f) return false;
            }

            foreach (Bounds bounds in touch)
            {
                var zoneRect = Rect.MinMaxRect(bounds.min.x - 0.5f, bounds.min.z - 0.5f, bounds.max.x + 0.5f, bounds.max.z + 0.5f);
                if (zoneRect.Overlaps(footprint)) return false;
            }

            // Nothing solid: trees, benches, bins, fence. The box starts above the ground so the grass does not count.
            Vector3 boxCentre = new Vector3(centre.x, 1.7f, centre.y);
            Vector3 boxHalf = new Vector3(halfWidth, 1.3f, halfDepth);
            Collider[] hits = Physics.OverlapBox(boxCentre, boxHalf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0) return false;

            // Level ground at both ends of the board.
            float left = GroundHeight(centre.x - halfWidth + 0.5f, centre.y);
            float right = GroundHeight(centre.x + halfWidth - 0.5f, centre.y);
            return !float.IsNaN(left) && !float.IsNaN(right) && Mathf.Abs(left - right) < 0.15f;
        }

        static float GroundHeight(float x, float z)
        {
            return Physics.Raycast(new Vector3(x, 8f, z), Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : float.NaN;
        }

        static float DistanceToRect(Rect rect, Vector2 point)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
