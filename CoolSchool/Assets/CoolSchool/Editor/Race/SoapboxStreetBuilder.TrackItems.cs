using System.Collections.Generic;
using System.Linq;
using CoolSchool.Race;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;
using Kind = CoolSchool.EditorTools.SoapboxTrackLayout.Kind;

namespace CoolSchool.EditorTools
{
    /// <summary>The track items: obstacles, coins, ramps and learning gates, placed from SoapboxTrackLayout. No colliders (all scripted).</summary>
    internal static partial class SoapboxStreetBuilder
    {
        static readonly PrefabRef[] BoxPrefabs =
        {
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_01", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_02", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_03", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_04", "PolygonGeneric"),
            new PrefabRef("SM_Gen_Prop_Cardboard_Box_05", "PolygonGeneric"),
        };
        static readonly PrefabRef ConePrefab = new PrefabRef("SM_Prop_Cone_01", "PolygonCity");
        static readonly PrefabRef SprinklerPrefab = new PrefabRef("SM_Prop_Sprinkler_01", "PolygonTown");
        static readonly PrefabRef RampPrefab = new PrefabRef("SM_Gen_Env_Road_Ramp_01", "PolygonGeneric");
        static readonly PrefabRef RampFallbackPrefab = Kids("SM_Prop_Ramp_01");
        static readonly PrefabRef CoinPrefab = new PrefabRef("SM_Gen_Prop_Coin_01", "PolygonGeneric");

        static readonly Color[] ArchColours =
        {
            new Color(0.96f, 0.36f, 0.36f),
            new Color(0.3f, 0.78f, 0.4f),
            new Color(0.32f, 0.58f, 0.96f),
        };

        /// <summary>Counts of what was made, for the summary line.</summary>
        sealed class ItemCounts
        {
            public int Obstacles, Coins, Ramps, Gates, Animals;
        }

        /// <summary>The x of the middle of the lanes an item covers.</summary>
        static float ItemX(int lane, int span) => StreetX + (lane + (span - 1) * 0.5f - 1f) * LaneWidth;

        /// <summary>Creates every non-animal item under the parent. Gates get their glyph canvases with the given font.</summary>
        static ItemCounts BuildTrackItems(Transform parent, Font font)
        {
            var counts = new ItemCounts();
            Transform obstacles = NewGroup("Obstacles", parent);
            Transform coins = NewGroup("Coins", parent);
            Transform ramps = NewGroup("Ramps", parent);
            Transform gates = NewGroup("Gates", parent);

            Material gold = Mat("RaceCoinGold", new Color(1f, 0.8f, 0.15f), 0.8f, 0.8f, 0.35f);
            int boxIndex = 0;

            foreach (SoapboxTrackLayout.Entry e in SoapboxTrackLayout.Entries)
            {
                Vector3 position = new Vector3(ItemX(e.Lane, e.Span), RoadY, ZAt(e.Distance));
                switch (e.Kind)
                {
                    case Kind.Box:
                    case Kind.Cone:
                    case Kind.BoxStack:
                    case Kind.Puddle:
                    case Kind.Sprinkler:
                        BuildObstacle(e, position, obstacles, boxIndex++);
                        counts.Obstacles++;
                        break;
                    case Kind.Coin:
                        BuildCoin(e, position, coins, gold);
                        counts.Coins++;
                        break;
                    case Kind.Ramp:
                        BuildRamp(e, position, ramps);
                        counts.Ramps++;
                        break;
                    case Kind.Gate:
                        BuildGate(e, gates, font);
                        counts.Gates++;
                        break;
                }
            }
            return counts;
        }

        static void SetItemBase(Object item, SoapboxTrackLayout.Entry e, float length)
        {
            SetInt(item, "lane", e.Lane);
            SetInt(item, "laneSpan", e.Span);
            SetFloat(item, "length", length);
            SetInt(item, "minLevel", e.MinLevel);
        }

        // ---- Obstacles ----

        static void BuildObstacle(SoapboxTrackLayout.Entry e, Vector3 position, Transform parent, int variant)
        {
            var item = new GameObject($"{e.Kind}_d{e.Distance:0}_L{e.Lane}");
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            var visual = NewGroup("Visual", item.transform);
            visual.position = position;
            Transform jetPivot = null;
            float length = 1f;

            switch (e.Kind)
            {
                case Kind.Box:
                    PlaceBox(visual, position, variant, 0.7f, variant * 37f);
                    length = 0.9f;
                    break;
                case Kind.BoxStack:
                    PlaceBox(visual, position, variant, 0.85f, 10f + variant * 23f);
                    PlaceBox(visual, position + Vector3.up * 0.8f, variant + 2, 0.65f, 35f + variant * 17f);
                    length = 1.0f;
                    break;
                case Kind.Cone:
                    Place(ConePrefab, visual, variant * 40f, Fit.Height, 0.7f, position, RoadY, false, false);
                    length = 0.7f;
                    break;
                case Kind.Puddle:
                    BuildPuddle(visual, position, e.Span);
                    length = 1.6f;
                    break;
                case Kind.Sprinkler:
                    jetPivot = BuildSprinkler(visual, position);
                    length = 1.0f;
                    break;
            }

            var obstacle = item.AddComponent<RaceObstacle>();
            SetItemBase(obstacle, e, length);
            SetEnum(obstacle, "kind", (int)e.Kind);
            SetObject(obstacle, "visual", visual);
            SetObject(obstacle, "jetPivot", jetPivot);
        }

        static void PlaceBox(Transform parent, Vector3 position, int variant, float size, float yaw)
        {
            PrefabRef box = BoxPrefabs[variant % BoxPrefabs.Length];
            GameObject go = Place(box, parent, yaw, Fit.Longest, size, position, position.y, false, false);
            if (go == null)
            {
                // No cardboard prefab: a plain brown cube.
                Prim(PrimitiveType.Cube, "Box", parent, new Vector3(0f, size * 0.5f, 0f) + (position - parent.position), Vector3.one * size,
                    Quaternion.Euler(0f, yaw, 0f), Mat("RaceCardboard", new Color(0.72f, 0.55f, 0.35f), 0.1f), false, true);
            }
        }

        static void BuildPuddle(Transform parent, Vector3 position, int span)
        {
            float width = span * LaneWidth - 0.4f;
            float depth = span >= 3 ? 1.6f : 1.4f;
            if (span == 1) width = 1.8f;
            Prim(PrimitiveType.Cylinder, "Water", parent, new Vector3(0f, 0.03f, 0f) + (position - parent.position),
                new Vector3(width, 0.005f, depth), Quaternion.identity, MatTransparent("RaceWater", new Color(0.45f, 0.75f, 1f, 0.7f), 0.9f));
        }

        /// <summary>The sprinkler on the road, and the pivot "Jets" at its head with three light-blue jets that fan out toward the cart.</summary>
        static Transform BuildSprinkler(Transform parent, Vector3 position)
        {
            Place(SprinklerPrefab, parent, 0f, Fit.Height, 0.3f, position, position.y, false, false);
            float headY = 0.32f;
            var pivot = new GameObject("Jets").transform;
            pivot.SetParent(parent, false);
            pivot.position = position + Vector3.up * headY;
            pivot.rotation = Quaternion.identity;

            Material water = MatTransparent("RaceJet", new Color(0.55f, 0.82f, 1f, 0.6f), 0.9f);
            foreach (float yaw in new[] { -28f, 0f, 28f })
            {
                // A capsule is 2 m tall at scale 1: half its length as Y scale. It points south (toward the cart), up 35 degrees.
                var jet = new GameObject("Jet").transform;
                jet.SetParent(pivot, false);
                jet.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f) * Quaternion.Euler(-35f, 0f, 0f);
                Prim(PrimitiveType.Capsule, "Water", jet, new Vector3(0f, 0f, 0.55f), new Vector3(0.07f, 0.55f, 0.07f), Quaternion.Euler(90f, 0f, 0f), water);
            }
            return pivot;
        }

        // ---- Coins ----

        static void BuildCoin(SoapboxTrackLayout.Entry e, Vector3 groundPosition, Transform parent, Material gold)
        {
            Vector3 position = groundPosition + Vector3.up * e.Height;
            var item = new GameObject($"Coin_d{e.Distance:0.0}_L{e.Lane}");
            item.transform.SetParent(parent, false);
            item.transform.position = position;

            // "Model" spins in place; the coin prefab inside it is centred on the model's pivot.
            var model = NewGroup("Model", item.transform);
            model.position = position;
            GameObject coin = Place(CoinPrefab, model, 0f, Fit.Longest, 0.45f, position, 0f, false, false);
            if (coin != null)
            {
                // Stand it up: the thin side of the disc points along Z, so it faces the cart.
                if (TryGetBounds(coin, out Bounds b))
                {
                    if (b.size.y <= b.size.x && b.size.y <= b.size.z) coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * coin.transform.rotation;
                    else if (b.size.x <= b.size.y && b.size.x <= b.size.z) coin.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * coin.transform.rotation;
                    if (TryGetBounds(coin, out b)) coin.transform.position += position - b.center;
                }
                foreach (Renderer renderer in coin.GetComponentsInChildren<Renderer>(true))
                {
                    var painted = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < painted.Length; i++) painted[i] = gold;
                    renderer.sharedMaterials = painted;
                }
            }
            else
            {
                Prim(PrimitiveType.Cylinder, "Disc", model, Vector3.zero, new Vector3(0.45f, 0.03f, 0.45f), Quaternion.Euler(90f, 0f, 0f), gold);
            }

            var raceCoin = item.AddComponent<RaceCoin>();
            SetItemBase(raceCoin, e, 0.6f);
            SetFloat(raceCoin, "height", e.Height);
            SetObject(raceCoin, "visual", model);
        }

        // ---- Ramps ----

        /// <summary>A ramp about 1.8 m wide, sloping up toward the north (the low end faces the cart). Found by its measured shape.</summary>
        static void BuildRamp(SoapboxTrackLayout.Entry e, Vector3 position, Transform parent)
        {
            var item = new GameObject($"Ramp_d{e.Distance:0}_L{e.Lane}");
            item.transform.SetParent(parent, false);
            item.transform.position = position;

            float length = SoapboxTrackLayout.RampLength;
            GameObject prefab = LoadPrefab(RampPrefab) ?? LoadPrefab(RampFallbackPrefab);
            if (prefab != null)
            {
                GameObject ramp = Spawn(prefab, item.transform);
                StripColliders(ramp);
                if (TryGetBounds(ramp, out Bounds b))
                {
                    // The long side along Z, the narrow one is the width.
                    if (b.size.x > b.size.z) ramp.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    TryGetBounds(ramp, out b);
                    ramp.transform.localScale *= 1.8f / Mathf.Max(0.05f, b.size.x);
                    AlignToBounds(ramp, position.x, position.z, position.y, false);
                    TryGetBounds(ramp, out b);

                    using (var probes = new Probes(ramp))
                    {
                        float TopAtT(float t)
                        {
                            var ray = new Ray(new Vector3(b.center.x, b.max.y + 1f, Mathf.Lerp(b.min.z, b.max.z, t)), Vector3.down);
                            return Hit(probes.Colliders, ray, b.size.y + 2f, out RaycastHit hit) ? hit.point.y : float.MinValue;
                        }
                        float low = TopAtT(0.12f), high = TopAtT(0.88f);
                        bool risesNorth = high >= low;
                        if (!risesNorth)
                        {
                            ramp.transform.rotation = Quaternion.Euler(0f, ramp.transform.eulerAngles.y + 180f, 0f);
                            AlignToBounds(ramp, position.x, position.z, position.y, false);
                            TryGetBounds(ramp, out b);
                        }
                        Physics.SyncTransforms();
                        // The height profile along the ramp AFTER any turn, south to north, to check that it rises toward the north.
                        string profile = string.Join(" ", new[] { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f }.Select(t => TopAtT(t).ToString("0.00")));
                        Debug.Log($"CoolSchool race street: ramp {b.size.ToString("0.00")} m; before the turn the top was {low:0.00} (south end) / {high:0.00} (north end) -> "
                            + $"{(risesNorth ? "kept" : "turned round")}; height profile south to north after that: {profile}.");
                    }
                    length = Mathf.Clamp(b.size.z, 1.5f, 4f);
                }
                MarkStatic(ramp);
            }
            else
            {
                Debug.LogWarning("CoolSchool race street: no ramp prefab found; the ramp is a plain wedge-less box.");
                Prim(PrimitiveType.Cube, "Ramp", item.transform, new Vector3(0f, 0.2f, 0f), new Vector3(1.8f, 0.4f, 2.5f), Quaternion.identity,
                    Mat("RaceRampFallback", new Color(0.9f, 0.6f, 0.2f), 0.2f), false, true);
            }

            var raceRamp = item.AddComponent<RaceRamp>();
            SetItemBase(raceRamp, e, length);
        }

        // ---- Gates ----

        /// <summary>
        /// A gate: three arches across the street, each with two posts, a coloured board with a world-space glyph and a glowing
        /// strip. The glyphs read "1", "2", "3" until the race sets the question.
        /// </summary>
        static void BuildGate(SoapboxTrackLayout.Entry e, Transform parent, Font font)
        {
            var gate = new GameObject($"Gate_{e.Index + 1}_d{e.Distance:0}");
            gate.transform.SetParent(parent, false);
            gate.transform.position = new Vector3(StreetX, RoadY, ZAt(e.Distance));

            var glyphs = new Text[3];
            var glows = new Renderer[3];
            for (int lane = 0; lane < 3; lane++)
            {
                Color colour = ArchColours[lane];
                float cx = (lane - 1) * LaneWidth;
                Transform arch = NewGroup("Arch_" + (lane + 1), gate.transform);
                arch.localPosition = new Vector3(cx, 0f, 0f);

                Material post = Mat("RaceArch" + lane, colour, 0.4f);
                foreach (float side in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, "Post", arch, new Vector3(side * 1.0f, 1.3f, 0f), new Vector3(0.2f, 1.3f, 0.2f), Quaternion.identity, post, false, true);
                Prim(PrimitiveType.Cube, "Board", arch, new Vector3(0f, 2.2f, 0f), new Vector3(2.2f, 0.8f, 0.14f), Quaternion.identity, post, false, true);

                Transform glow = Prim(PrimitiveType.Cube, "Glow", arch, new Vector3(0f, 1.7f, 0f), new Vector3(2.0f, 0.08f, 0.16f), Quaternion.identity,
                    Mat("RaceArchGlow" + lane, colour, 0.3f));
                glows[lane] = glow.GetComponent<Renderer>();

                glyphs[lane] = BuildGlyph(arch, new Vector3(0f, 2.2f, -0.09f), (lane + 1).ToString(), font);
            }

            var learningGate = gate.AddComponent<LearningGate>();
            SetItemBase(learningGate, e, SoapboxTrackLayout.GateLength);
            SetObjectArray(learningGate, "glyphs", glyphs);
            SetObjectArray(learningGate, "archGlow", glows);
        }

        /// <summary>A world-space canvas (2.2 x 0.9 m) with one big white outlined glyph, readable from the south.</summary>
        static Text BuildGlyph(Transform parent, Vector3 localPosition, string text, Font font)
        {
            var canvasObject = new GameObject("Glyph", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 90f);
            rect.localScale = Vector3.one * 0.01f;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = 84;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            return label;
        }
    }
}
