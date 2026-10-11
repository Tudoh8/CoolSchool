using CoolSchool.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The beacon on top of an activity zone, so the zone can be seen from far away: a tall pillar of light
    /// and a floating icon of the activity. Both are added to the zone's visual, so they hide with the ring.
    /// </summary>
    internal static class ActivityBeaconBuilder
    {
        const string RootFolder = "Assets/CoolSchool";
        const string MaterialFolder = RootFolder + "/Materials";

        const float PillarHeight = 8f;
        const float PillarWidth = 2f;
        const float IconHeight = 3f;
        const float IconWidth = 1.2f;

        static readonly PrefabRef LightRayPrefab = new PrefabRef("LightRay_Round_01", "PolygonGeneric");

        /// <summary>What a beacon looks like: its colour and the sprite names to try, best first.</summary>
        internal readonly struct Style
        {
            public readonly string Name;
            public readonly Color Tint;
            public readonly string[] IconNames;

            public Style(string name, Color tint, string[] iconNames)
            {
                Name = name;
                Tint = tint;
                IconNames = iconNames;
            }
        }

        // Tints are HDR on purpose: the light ray's own colour is brighter than 1 too.
        internal static readonly Style TagBeacon = new Style("Tag", new Color(2f, 1.5f, 0.4f, 1f),
            new[] { "Run", "Running", "Hand", "Bolt", "Star" });

        internal static readonly Style KnightsBeacon = new Style("Knights", new Color(0.5f, 1.1f, 2f, 1f),
            new[] { "Sword" });

        /// <summary>Adds the pillar and the floating icon as children of the zone's visual. Ground level is the visual's y.</summary>
        internal static void Build(Transform visual, Style style)
        {
            CreatePillar(visual, style);
            CreateIcon(visual, style);
        }

        // ----- Light pillar --------------------------------------------------------------------

        static void CreatePillar(Transform visual, Style style)
        {
            GameObject prefab = LoadPrefab(LightRayPrefab);
            GameObject pillar = prefab != null ? CreatePillarFromPrefab(prefab, visual, style) : CreateFallbackPillar(visual, style);
            pillar.name = "BeaconPillar";

            foreach (Collider collider in pillar.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        /// <summary>The Synty light ray, scaled to the pillar size by its bounds and standing on the ground.</summary>
        static GameObject CreatePillarFromPrefab(GameObject prefab, Transform visual, Style style)
        {
            GameObject pillar = Spawn(prefab, visual);
            Vector3 ground = visual.position;

            Vector3 scale = new Vector3(PillarWidth, PillarHeight, PillarWidth);
            if (TryGetBounds(pillar, out Bounds bounds) && bounds.size.x > 0.01f && bounds.size.y > 0.01f && bounds.size.z > 0.01f)
            {
                scale = new Vector3(PillarWidth / bounds.size.x, PillarHeight / bounds.size.y, PillarWidth / bounds.size.z);
                scale = Vector3.Scale(pillar.transform.localScale, scale);
            }
            pillar.transform.localScale = scale;
            AlignToBounds(pillar, ground.x, ground.z, ground.y, false);

            Material tinted = CreateTintedCopy(pillar, style);
            foreach (Renderer renderer in pillar.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (tinted != null) renderer.sharedMaterial = tinted;

                // The pillar must still draw when it is far away and only its tip is on screen.
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
            return pillar;
        }

        /// <summary>A copy of the light ray's material with the tint, saved as an asset. Null if it has no colour to tint.</summary>
        static Material CreateTintedCopy(GameObject pillar, Style style)
        {
            var renderer = pillar.GetComponentInChildren<Renderer>(true);
            Material original = renderer != null ? renderer.sharedMaterial : null;
            if (original == null || !original.HasProperty("_BaseColor")) return null;

            var copy = new Material(original) { name = "Beacon" + style.Name };
            copy.SetColor("_BaseColor", style.Tint);
            return SaveMaterial(copy, MaterialFolder + "/Beacon" + style.Name + ".mat");
        }

        /// <summary>No light ray prefab: a tall thin cylinder with a see-through additive glow.</summary>
        static GameObject CreateFallbackPillar(Transform visual, Style style)
        {
            Debug.LogWarning("CoolSchool: LightRay_Round_01 not found; using a plain glowing cylinder as the beacon pillar.");

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(visual, false);
            // The primitive cylinder is 2 m tall, so half the height as scale gives PillarHeight.
            pillar.transform.localScale = new Vector3(PillarWidth * 0.5f, PillarHeight * 0.5f, PillarWidth * 0.5f);
            pillar.transform.localPosition = new Vector3(0f, PillarHeight * 0.5f, 0f);

            var renderer = pillar.GetComponent<MeshRenderer>();
            Material glow = CreateAdditiveMaterial(style);
            if (glow != null) renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return pillar;
        }

        static Material CreateAdditiveMaterial(Style style)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("CoolSchool: URP/Unlit shader not found.");
                return null;
            }

            var material = new Material(unlit) { name = "Beacon" + style.Name };
            material.SetColor("_BaseColor", new Color(style.Tint.r * 0.5f, style.Tint.g * 0.5f, style.Tint.b * 0.5f, 0.5f));
            BattleEffectsBuilder.SetAlphaBlended(material);

            // Additive: the glow adds to what is behind it instead of covering it.
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            return SaveMaterial(material, MaterialFolder + "/Beacon" + style.Name + ".mat");
        }

        // ----- Floating icon -------------------------------------------------------------------

        static void CreateIcon(Transform visual, Style style)
        {
            Sprite sprite = FindSprite(style.IconNames);
            if (sprite == null)
            {
                Debug.LogWarning($"CoolSchool: no icon sprite found for the {style.Name} beacon ({string.Join(", ", style.IconNames)}).");
                return;
            }

            var icon = new GameObject("BeaconIcon");
            icon.transform.SetParent(visual, false);
            icon.transform.localPosition = new Vector3(0f, IconHeight, 0f);

            // The sprite's own size depends on its import settings, so scale it to the wanted width.
            float width = Mathf.Max(0.01f, sprite.bounds.size.x);
            icon.transform.localScale = Vector3.one * (IconWidth / width);

            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 10;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            icon.AddComponent<Billboard>();
        }

        /// <summary>
        /// The first name that has a sprite. Among sprites with that exact name, the Modular Game UI Kit's
        /// Icon-Symbols are preferred (Weapons-Tools/Sword.png over Icons/Sword.png).
        /// </summary>
        internal static Sprite FindSprite(string[] names)
        {
            foreach (string spriteName in names)
            {
                Sprite best = null;
                int bestScore = -1;
                foreach (string guid in AssetDatabase.FindAssets(spriteName + " t:Sprite"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != spriteName) continue;

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) continue;

                    int score = (path.Contains("ModularGameUIKit") ? 2 : 0) + (path.Contains("/Icon-Symbols/") ? 1 : 0);
                    if (score > bestScore)
                    {
                        best = sprite;
                        bestScore = score;
                    }
                }

                if (best != null) return best;
            }
            return null;
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
