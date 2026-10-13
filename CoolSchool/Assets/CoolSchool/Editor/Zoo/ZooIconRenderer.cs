using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Renders each zoo animal (the prepared model: unpacked, URP materials, scaled to its height) to a 256 x 256 transparent PNG sprite
    /// for the pen signs and the checklist: a 3/4 side view (the camera 55 degrees off the way the animal faces, towards its left side,
    /// a little above), framed tightly so the animal fills 85 percent of the larger side of the picture. The PNGs are saved in
    /// Assets/CoolSchool/Zoo/Icons/&lt;id&gt;.png and overwritten in place (their .meta files, and so their GUIDs, stay).
    /// A small version file next to them says which <see cref="IconVersion"/> drew them: when the const is raised (or the file is
    /// missing) all icons are drawn again, otherwise the existing ones are kept. A copy of the model is staged far away from the
    /// playground, so no other object is in the picture.
    /// </summary>
    internal static class ZooIconRenderer
    {
        /// <summary>Raise this number when the way the icons are drawn changes: they are all redrawn on the next build.</summary>
        const int IconVersion = 3;

        const string IconFolder = "Assets/CoolSchool/Zoo/Icons";
        const string VersionFile = IconFolder + "/version.txt";
        const int Size = 256;
        // The animal fills this much of the larger side of the picture.
        const float Fill = 0.85f;
        // The first, loose picture of the fit (the animal fills this much of it) before the second picture zooms in.
        const float LooseFill = 0.5f;
        // The camera stands this far off the animal's facing, towards its left side, and this much above the middle.
        const float SideAngle = 55f;
        const float Elevation = 0.28f;
        static readonly Vector3 StagePosition = new Vector3(4000f, 4000f, 4000f);

        /// <summary>One sprite for each id, in the same order (null where a model is missing or could not be drawn).</summary>
        internal static Sprite[] RenderAll(string[] ids, GameObject[] models)
        {
            EnsureFolder("Assets/CoolSchool", "Zoo");
            EnsureFolder("Assets/CoolSchool/Zoo", "Icons");

            bool current = ReadVersion() == IconVersion;
            var sprites = new Sprite[ids.Length];
            bool allDrawn = true;
            for (int i = 0; i < ids.Length; i++)
            {
                sprites[i] = RenderIcon(ids[i], i < models.Length ? models[i] : null, current);
                if (sprites[i] == null) allDrawn = false;
            }

            if (!current && allDrawn) WriteVersion();
            return sprites;
        }

        static int ReadVersion()
        {
            string path = FullPath(VersionFile);
            if (!File.Exists(path)) return 0;
            return int.TryParse(File.ReadAllText(path).Trim(), out int version) ? version : 0;
        }

        static void WriteVersion()
        {
            File.WriteAllText(FullPath(VersionFile), IconVersion.ToString());
            AssetDatabase.ImportAsset(VersionFile, ImportAssetOptions.ForceUpdate);
        }

        static string FullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        internal static Sprite RenderIcon(string id, GameObject model, bool keepExisting)
        {
            string assetPath = $"{IconFolder}/{id}.png";
            string fullPath = FullPath(assetPath);
            if (keepExisting && File.Exists(fullPath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                ConfigureImport(assetPath);
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (existing != null) return existing;
            }

            if (model == null)
            {
                Debug.LogWarning($"CoolSchool zoo park: no model for the {id}, so there is no icon.");
                return null;
            }

            byte[] png = Render(model, id);
            if (png == null) return null;

            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImport(assetPath);
            Debug.Log($"CoolSchool zoo park: drew the icon {assetPath}.");
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

        /// <summary>
        /// Every vertex of the model in the pose it has now, in world space (skinned meshes are baked). Falls back to the corners of a
        /// renderer's bounds when its mesh cannot be read.
        /// </summary>
        static List<Vector3> WorldVertices(GameObject model)
        {
            var points = new List<Vector3>();
            var baked = new Mesh();
            var vertices = new List<Vector3>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

                Mesh mesh = null;
                Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    try
                    {
                        // Baked with the scale included, so only the position and rotation of the renderer are applied.
                        skinned.BakeMesh(baked, true);
                        mesh = baked;
                        toWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                    }
                    catch (System.Exception)
                    {
                        mesh = null;
                    }
                }
                else if (renderer.TryGetComponent(out MeshFilter filter))
                {
                    mesh = filter.sharedMesh;
                }

                if (mesh != null && mesh.isReadable)
                {
                    vertices.Clear();
                    mesh.GetVertices(vertices);
                    foreach (Vector3 vertex in vertices) points.Add(toWorld.MultiplyPoint3x4(vertex));
                    continue;
                }

                // The corners of the mesh's own bounds, placed by the renderer's transform (the renderer's cached world bounds can be stale).
                Mesh shared = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.TryGetComponent(out MeshFilter meshFilter) ? meshFilter.sharedMesh : null;
                Bounds b = shared != null ? shared.bounds : renderer.bounds;
                Matrix4x4 place = shared != null ? renderer.transform.localToWorldMatrix : Matrix4x4.identity;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    points.Add(place.MultiplyPoint3x4(b.center + offset));
                }
            }

            Object.DestroyImmediate(baked);
            return points;
        }

        /// <summary>Puts the camera (and the light that follows it) on the target, looking along the given rotation, with the given half-height.</summary>
        static void PlaceCamera(Camera camera, Transform light, Vector3 target, Vector3 toCamera, Quaternion look, float size, float distance, float reach)
        {
            camera.orthographicSize = size;
            camera.farClipPlane = distance * 2f + reach * 2f + size * 4f;
            camera.transform.SetPositionAndRotation(target + toCamera * distance, look);
            light.rotation = Quaternion.LookRotation(
                (camera.transform.forward + Vector3.down * 0.6f + camera.transform.right * -0.3f).normalized, Vector3.up);
        }

        /// <summary>Draws the camera into the texture, reads it into the picture and finds the box of its (almost) opaque pixels. False when there are none.</summary>
        static bool Draw(Camera camera, RenderTexture texture, Texture2D picture, out RectInt box)
        {
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
            picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            picture.Apply();

            int xMin = Size, yMin = Size, xMax = -1, yMax = -1;
            Color32[] pixels = picture.GetPixels32();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    if (pixels[y * Size + x].a <= 8) continue;

                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }

            box = xMax < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
            return xMax >= 0;
        }

        /// <summary>Draws a copy of the model with a temporary camera and light into a render texture and returns the PNG bytes (null if it has no mesh).</summary>
        static byte[] Render(GameObject source, string label)
        {
            GameObject model = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture texture = null;
            Texture2D picture = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                model = Object.Instantiate(source);
                model.name = "ZooIconModel";
                model.SetActive(true);
                model.transform.SetParent(null, false);
                // Facing +Z (the way the prepared animals face), upright, far from everything else.
                model.transform.SetPositionAndRotation(StagePosition, Quaternion.identity);
                model.transform.localScale = source.transform.lossyScale;

                // Stand in the idle pose, not the bind pose.
                Animator animator = model.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    try
                    {
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.Rebind();
                        animator.Update(0.15f);
                    }
                    catch (System.Exception)
                    {
                        // The bind pose will do.
                    }
                }

                if (!TryGetBounds(model, out Bounds rendererBounds))
                {
                    Debug.LogWarning($"CoolSchool zoo park: the {label} has no mesh, so there is no icon.");
                    return null;
                }

                // The camera looks at the animal from 55 degrees off its facing (+Z), on its left side (-X), a little above.
                Vector3 toCamera = new Vector3(-Mathf.Sin(SideAngle * Mathf.Deg2Rad), Elevation, Mathf.Cos(SideAngle * Mathf.Deg2Rad)).normalized;
                Quaternion look = Quaternion.LookRotation(-toCamera, Vector3.up);
                Quaternion inverse = Quaternion.Inverse(look);

                // A first guess at the framing from the model's own vertices (not from the renderers' cached bounds, which can still
                // belong to the place the model was copied from and so aim the camera at an empty spot).
                List<Vector3> points = WorldVertices(model);
                if (points.Count == 0) points.Add(rendererBounds.center);
                var extent = new Bounds(points[0], Vector3.zero);
                foreach (Vector3 point in points) extent.Encapsulate(point);

                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector3 point in points)
                {
                    Vector3 local = inverse * (point - extent.center);
                    min = Vector2.Min(min, new Vector2(local.x, local.y));
                    max = Vector2.Max(max, new Vector2(local.x, local.y));
                }
                Vector2 middle = (min + max) * 0.5f;
                float larger = Mathf.Max(0.05f, Mathf.Max(max.x - min.x, max.y - min.y));
                float reach = extent.extents.magnitude;
                float distance = Mathf.Max(reach * 3f, 5f);

                cameraObject = new GameObject("ZooIconCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.nearClipPlane = 0.1f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;
                camera.aspect = 1f;
                camera.enabled = false;

                // A soft key light from the camera's side, above.
                lightObject = new GameObject("ZooIconLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.color = new Color(1f, 0.97f, 0.92f);
                light.shadows = LightShadows.None;

                texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                texture.Create();
                picture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

                // Fit by the picture itself: draw loosely, find the opaque pixels, centre and zoom on them, and draw again (twice).
                // A picture that comes out empty is drawn again from further away.
                Vector3 target = extent.center + look * new Vector3(middle.x, middle.y, 0f);
                float size = larger * 0.5f / LooseFill;
                int fits = 0;
                for (int attempt = 0; attempt < 8 && fits < 2; attempt++)
                {
                    PlaceCamera(camera, lightObject.transform, target, toCamera, look, size, distance, reach);
                    if (!Draw(camera, texture, picture, out RectInt box))
                    {
                        size *= 2.5f;
                        continue;
                    }

                    float metresPerPixel = 2f * size / Size;
                    var centre = new Vector2(box.xMin + box.xMax - Size, box.yMin + box.yMax - Size) * 0.5f * metresPerPixel;
                    target += look * new Vector3(centre.x, centre.y, 0f);

                    // Touching the edge: the animal is cut off, so zoom out and try again.
                    if (box.xMin <= 0 || box.yMin <= 0 || box.xMax >= Size || box.yMax >= Size)
                    {
                        size *= 1.5f;
                        continue;
                    }

                    size = Mathf.Max(box.width, box.height) * metresPerPixel * 0.5f / Fill;
                    fits++;
                }

                if (fits == 0)
                {
                    Debug.LogWarning($"CoolSchool zoo park: the {label} came out empty in every try, so there is no icon.");
                    return null;
                }

                PlaceCamera(camera, lightObject.transform, target, toCamera, look, size, distance, reach);
                if (!Draw(camera, texture, picture, out RectInt _)) return null;
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
