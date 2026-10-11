using System.IO;
using CoolSchool.Shop;
using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The generated assets of the ice cream shop: the flavour and prop materials, the waffle cone mesh, the waffle
    /// texture and the white triangle that is the cone (and the tail of the speech bubble) in the order picture.
    /// All of them are re-made on every build.
    /// </summary>
    internal static class ShopAssets
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string MaterialFolder = RootFolder + "/Materials/Shop";
        const string ConeMeshPath = RootFolder + "/Meshes/IceCreamCone.asset";
        const string WaffleTexturePath = RootFolder + "/Textures/ShopWaffle.png";
        const string TriangleSpritePath = RootFolder + "/Sprites/ShopConeTriangle.png";

        // The shapes of the Modular Game UI Kit, in their known place; found by name when the kit was moved.
        const string ShapesFolder = "Assets/Imported/ModularGameUIKit/Common/Sprites/Shapes/";

        /// <summary>The cone is a little taller than wide, with its tip at the origin and its rim at ConeHeight.</summary>
        internal const float ConeHeight = 0.42f;
        internal const float ConeRimRadius = 0.13f;

        // ----- Materials -------------------------------------------------------------------------

        /// <summary>An opaque URP/Lit material saved under Materials/Shop. Null if the shader is missing.</summary>
        internal static Material Lit(string fileName, Color colour, float smoothness = 0.35f, float metallic = 0f, float emission = 0f)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found.");
                return null;
            }

            var material = new Material(lit) { name = fileName };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return Save(material, fileName);
        }

        /// <summary>A transparent glowing material (the halo of a tub, the drop hint, the zone ring).</summary>
        internal static Material Glow(string fileName, Color colour, float alpha)
        {
            EnsureShopFolder();
            return KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/" + fileName + ".mat", colour, alpha);
        }

        static Material Save(Material material, string fileName)
        {
            EnsureShopFolder();
            string path = MaterialFolder + "/" + fileName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void EnsureShopFolder()
        {
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "Shop");
        }

        // ----- The cone --------------------------------------------------------------------------

        /// <summary>
        /// The waffle cone as a mesh asset: a pointed cone with its tip at the origin and its rim ConeHeight above it,
        /// closed by a flat top. UVs go round the cone (u) and up it (v), for the waffle texture.
        /// </summary>
        internal static Mesh CreateConeMesh()
        {
            const int segments = 24;
            float slope = Mathf.Sqrt(ConeHeight * ConeHeight + ConeRimRadius * ConeRimRadius);

            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            // Side: rim vertices all the way round (the last one repeats the first, so the texture closes), and a tip
            // vertex for every segment, so each flat quad shades smoothly up to the point.
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices.Add(new Vector3(cos * ConeRimRadius, ConeHeight, sin * ConeRimRadius));
                normals.Add(new Vector3(cos * ConeHeight, -ConeRimRadius, sin * ConeHeight) / slope);
                uvs.Add(new Vector2(i / (float)segments, 1f));
            }
            int tipStart = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float middle = (i + 0.5f) / segments * Mathf.PI * 2f;
                vertices.Add(Vector3.zero);
                normals.Add(new Vector3(Mathf.Cos(middle) * ConeHeight, -ConeRimRadius, Mathf.Sin(middle) * ConeHeight) / slope);
                uvs.Add(new Vector2((i + 0.5f) / segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                // Clockwise seen from outside: tip, this rim vertex, the next one.
                triangles.Add(tipStart + i);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            // Top: a flat disc facing up, so nothing can be seen through the cone from above.
            int centre = vertices.Count;
            vertices.Add(new Vector3(0f, ConeHeight, 0f));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));
            int ringStart = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * ConeRimRadius, ConeHeight, Mathf.Sin(angle) * ConeRimRadius));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(centre);
                triangles.Add(ringStart + (i + 1) % segments);
                triangles.Add(ringStart + i);
            }

            var mesh = new Mesh { name = "IceCreamCone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            EnsureFolder(RootFolder, "Meshes");
            AssetDatabase.DeleteAsset(ConeMeshPath);
            AssetDatabase.CreateAsset(mesh, ConeMeshPath);
            return AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
        }

        // ----- Pictures --------------------------------------------------------------------------

        /// <summary>A waffle: light diagonal lines on a golden brown. Tiled round the cone by the material.</summary>
        internal static Texture2D CreateWaffleTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var baseColour = new Color32(0xE0, 0xA8, 0x5A, 0xFF);
            var lineColour = new Color32(0xB0, 0x76, 0x32, 0xFF);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool line = (x + y) % 16 < 3 || ((x - y) % 16 + 16) % 16 < 3;
                    pixels[y * size + x] = line ? lineColour : baseColour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            EnsureFolder(RootFolder, "Textures");
            SavePng(WaffleTexturePath, texture);
            Object.DestroyImmediate(texture);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WaffleTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaffleTexturePath);
        }

        /// <summary>A white triangle with its point down and soft edges: the cone in the order picture, and the tail of the bubble.</summary>
        internal static Sprite CreateTriangleSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                // Row 0 is the bottom, where the point is. The triangle widens toward the top.
                float halfWidth = y / (float)(size - 1) * (size * 0.5f - 1f);
                for (int x = 0; x < size; x++)
                {
                    float coverage = Mathf.Clamp01(halfWidth - Mathf.Abs(x + 0.5f - size * 0.5f) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            EnsureFolder(RootFolder, "Sprites");
            SavePng(TriangleSpritePath, texture);
            Object.DestroyImmediate(texture);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TriangleSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(TriangleSpritePath);
        }

        static void SavePng(string assetPath, Texture2D texture)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            File.WriteAllBytes(full, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>A shape of the Modular Game UI Kit (Background, Circle, Circle-Outline). Null if the kit has none.</summary>
        internal static Sprite FindShape(string shapeName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShapesFolder + shapeName + ".png");
            if (sprite != null) return sprite;

            Debug.LogWarning($"CoolSchool: UI shape '{shapeName}' not found in {ShapesFolder}; looking for it by name.");
            return ActivityBeaconBuilder.FindSprite(new[] { shapeName });
        }
    }
}
