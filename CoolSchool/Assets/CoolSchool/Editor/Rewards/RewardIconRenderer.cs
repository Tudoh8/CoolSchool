using System.IO;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Renders every reward item's model to a 256 x 256 transparent PNG sprite (3/4 view, framed by its bounds),
    /// saved in Assets/CoolSchool/Rewards/Icons/&lt;item id&gt;.png. Long things (swords) are stood up like in the reveal,
    /// carts stay on their wheels, and gold variants are painted gold. An icon that already exists is kept, so delete
    /// the file to have it drawn again. Locked items show the icon tinted black (the alpha keeps the shape).
    /// The model is staged far away from the playground, so no other object is in the picture.
    /// </summary>
    public static class RewardIconRenderer
    {
        const string IconFolder = "Assets/CoolSchool/Rewards/Icons";
        const int Size = 256;
        const float FieldOfView = 26f;
        const float LongThingRatio = 1.8f;
        static readonly Vector3 StagePosition = new Vector3(4200f, 4200f, 4200f);

        [MenuItem("CoolSchool/Rewards/Render Item Icons")]
        static void RenderFromMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>("Assets/CoolSchool/Rewards/Catalog.asset");
            if (catalog == null)
            {
                Debug.LogWarning("CoolSchool: no reward catalog yet. Run CoolSchool > Rewards > Seed Catalog first.");
                return;
            }

            AssignIcons(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Draws (or loads) the icon of every item in the catalog and stores it in item.icon.</summary>
        public static void AssignIcons(RewardCatalog catalog)
        {
            if (catalog == null) return;

            foreach (RewardItem item in catalog.items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                item.icon = RenderIcon(item);
            }
        }

        /// <summary>The icon of one item: the existing PNG, or a newly drawn one. Null when the item has no model.</summary>
        public static Sprite RenderIcon(RewardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) return null;

            EnsureFolder("Assets/CoolSchool", "Rewards");
            EnsureFolder("Assets/CoolSchool/Rewards", "Icons");

            string assetPath = $"{IconFolder}/{item.id}.png";
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (File.Exists(fullPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                ConfigureImport(assetPath);
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (existing != null) return existing;
            }

            if (item.prefab == null) return null;

            byte[] png = Render(item);
            if (png == null) return null;

            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImport(assetPath);
            Debug.Log($"CoolSchool rewards: drew the item icon {assetPath}.");
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static void ConfigureImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.maxTextureSize != Size) { importer.maxTextureSize = Size; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>Draws the item with a temporary camera and light into a render texture and returns the PNG bytes (null if it has no mesh).</summary>
        static byte[] Render(RewardItem item)
        {
            GameObject pivot = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture texture = null;
            Texture2D picture = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                // The pivot turns the whole item a little, so the camera sees a 3/4 view.
                pivot = new GameObject("RewardIconPivot");
                pivot.transform.SetPositionAndRotation(StagePosition, Quaternion.Euler(0f, 195f, 0f));

                var model = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab);
                model.transform.SetParent(pivot.transform, false);
                model.transform.localRotation = Quaternion.Euler(item.revealEuler);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                // Carts stay on their wheels; everything else that lies on its side is stood up.
                if (item.kind != RewardKind.Cart) StandUp(model.transform);
                if (item.IsGold) GoldPaint.Apply(model);

                if (!TryGetBounds(model, out Bounds bounds))
                {
                    Debug.LogWarning($"CoolSchool rewards: {item.id} has no mesh, so there is no icon.");
                    return null;
                }

                float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
                float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                Vector3 direction = new Vector3(0.5f, 0.42f, -1f).normalized;

                cameraObject = new GameObject("RewardIconCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.fieldOfView = FieldOfView;
                camera.nearClipPlane = Mathf.Max(0.02f, distance - radius * 1.5f);
                camera.farClipPlane = distance + radius * 2f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;
                camera.aspect = 1f;
                camera.enabled = false;
                cameraObject.transform.position = bounds.center + direction * distance;
                cameraObject.transform.rotation = Quaternion.LookRotation(bounds.center - cameraObject.transform.position, Vector3.up);

                // A soft key light from the camera side, above.
                lightObject = new GameObject("RewardIconLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.color = new Color(1f, 0.97f, 0.92f);
                light.shadows = LightShadows.None;
                lightObject.transform.rotation = Quaternion.LookRotation(
                    (cameraObject.transform.forward + Vector3.down * 0.6f + cameraObject.transform.right * -0.3f).normalized, Vector3.up);

                texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                texture.Create();

                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    camera.targetTexture = null;
                }

                RenderTexture.active = texture;
                picture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                picture.Apply();
                return picture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (picture != null) Object.DestroyImmediate(picture);
                if (texture != null)
                {
                    texture.Release();
                    Object.DestroyImmediate(texture);
                }
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                if (lightObject != null) Object.DestroyImmediate(lightObject);
                if (pivot != null) Object.DestroyImmediate(pivot);
            }
        }

        /// <summary>A model that lies on its side (longer than tall) is turned so its long side points up, like in the reveal.</summary>
        static void StandUp(Transform model)
        {
            if (!TryGetBounds(model.gameObject, out Bounds bounds)) return;

            Vector3 size = bounds.size;
            if (size.x > size.y * LongThingRatio && size.x >= size.z)
                model.rotation = Quaternion.Euler(0f, 0f, 90f) * model.rotation;
            else if (size.z > size.y * LongThingRatio)
                model.rotation = Quaternion.Euler(90f, 0f, 0f) * model.rotation;
        }
    }
}
