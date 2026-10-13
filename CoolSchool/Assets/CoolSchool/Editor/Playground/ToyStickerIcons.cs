using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Renders each playground toy prefab to a 256 x 256 transparent PNG sprite (a three-quarter view, framed by its bounds), saved in
    /// Assets/CoolSchool/Playground/StickerIcons/&lt;sticker id&gt;.png. A picture that already exists is kept, so delete the file to have it
    /// drawn again (the same way the pirate map icons and the reward icons work). The model is staged far from the playground, so
    /// nothing else is in the picture. The same sprite is used in colour for a sticker he has and tinted black for one he has not.
    /// The drawing follows RewardIconRenderer and PirateMapIconRenderer, which keep theirs private.
    /// </summary>
    internal static class ToyStickerIcons
    {
        const string IconFolder = "Assets/CoolSchool/Playground/StickerIcons";
        const int Size = 256;
        const float FieldOfView = 26f;
        static readonly Vector3 StagePosition = new Vector3(4400f, 4400f, 4400f);

        /// <summary>The icon of a sticker: the existing PNG, or a newly drawn one of the prefab. Null when there is no model.</summary>
        public static Sprite GetOrRender(GameObject prefab, string stickerId)
        {
            EnsureFolder("Assets/CoolSchool", "Playground");
            EnsureFolder("Assets/CoolSchool/Playground", "StickerIcons");

            string assetPath = $"{IconFolder}/{stickerId}.png";
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (File.Exists(fullPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                ConfigureImport(assetPath);
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (existing != null) return existing;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"PlaygroundToys: {stickerId}: no prefab to draw the sticker icon from.");
                return null;
            }

            byte[] png = Render(prefab, stickerId);
            if (png == null) return null;

            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImport(assetPath);
            Debug.Log($"PlaygroundToys: drew the sticker icon {assetPath}.");
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

        static byte[] Render(GameObject prefab, string label)
        {
            GameObject model = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture texture = null;
            Texture2D picture = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                // The front toward the camera (on the -Z side, a little to the right), like the map icons.
                model.transform.SetPositionAndRotation(StagePosition, Quaternion.Euler(0f, 195f, 0f));
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                if (!TryGetBounds(model, out Bounds bounds))
                {
                    Debug.LogWarning($"PlaygroundToys: {label} has no mesh, so there is no sticker icon.");
                    return null;
                }

                float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
                float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                Vector3 direction = new Vector3(0.5f, 0.42f, -1f).normalized;

                cameraObject = new GameObject("ToyIconCamera");
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

                lightObject = new GameObject("ToyIconLight");
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
                if (model != null) Object.DestroyImmediate(model);
            }
        }
    }
}
