using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Renders a landmark prefab to a 256 x 256 transparent PNG sprite for the treasure map (3/4 view, framed by its bounds),
    /// saved in Assets/CoolSchool/Pirates/MapIcons/&lt;prefab name&gt;.png. An icon that already exists is kept, so delete the file to
    /// have it drawn again. The model is staged far away from the playground, so no other object is in the picture.
    /// </summary>
    internal static class PirateMapIconRenderer
    {
        const string IconFolder = "Assets/CoolSchool/Pirates/MapIcons";
        const int Size = 256;
        const float FieldOfView = 26f;
        static readonly Vector3 StagePosition = new Vector3(4000f, 4000f, 4000f);

        /// <summary>One sprite for each prefab, in the same order (null where a prefab is missing or could not be drawn).</summary>
        internal static Sprite[] RenderAll(PrefabRef[] prefabs)
        {
            var sprites = new Sprite[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
                sprites[i] = RenderIcon(prefabs[i]);
            return sprites;
        }

        internal static Sprite RenderIcon(PrefabRef reference)
        {
            EnsureFolder("Assets/CoolSchool", "Pirates");
            EnsureFolder("Assets/CoolSchool/Pirates", "MapIcons");

            string assetPath = $"{IconFolder}/{reference.Name}.png";
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (File.Exists(fullPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                ConfigureImport(assetPath);
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (existing != null) return existing;
            }

            GameObject prefab = LoadPrefab(reference);
            if (prefab == null) return null;

            byte[] png = Render(prefab, reference.Name);
            if (png == null) return null;

            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImport(assetPath);
            Debug.Log($"CoolSchool pirate cove: drew the map icon {assetPath}.");
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

        /// <summary>Draws the prefab with a temporary camera and light into a render texture and returns the PNG bytes (null if it has no mesh).</summary>
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
                // Stand on the stage with its front toward the camera (the camera is on the -Z side, a little to the right).
                model.transform.SetPositionAndRotation(StagePosition, Quaternion.Euler(0f, 195f, 0f));
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

                if (!TryGetBounds(model, out Bounds bounds))
                {
                    Debug.LogWarning($"CoolSchool pirate cove: {label} has no mesh, so there is no map icon.");
                    return null;
                }

                float radius = Mathf.Max(0.05f, bounds.extents.magnitude);
                float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                Vector3 direction = new Vector3(0.5f, 0.42f, -1f).normalized;

                cameraObject = new GameObject("PirateIconCamera");
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

                // A soft key light from the camera's side, above.
                lightObject = new GameObject("PirateIconLight");
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
