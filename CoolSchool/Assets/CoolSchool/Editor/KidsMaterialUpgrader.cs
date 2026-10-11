using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The Polygon Kids pack ships with Built-in "Standard" materials, which render pink in URP.
    /// This swaps them to URP/Lit and carries over texture, colour, smoothness, emission and blend mode.
    /// </summary>
    public static class KidsMaterialUpgrader
    {
        const string FallbackKidsFolder = "Assets/Imported/Synty/PolygonKids";

        [MenuItem("CoolSchool/Tools/Upgrade Polygon Kids Materials to URP")]
        static void UpgradeFromMenu()
        {
            int count = UpgradeAll();
            EditorUtility.DisplayDialog("CoolSchool", $"Upgraded {count} Polygon Kids material(s) to URP/Lit.", "OK");
        }

        public static int UpgradeAll()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CoolSchool: URP/Lit shader not found; is URP installed?");
                return 0;
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FindKidsFolder() }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null || material.shader.name != "Standard") continue;

                Upgrade(material, lit);
                count++;
            }

            if (count > 0) AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>Finds the PolygonKids folder wherever the pack was imported to.</summary>
        static string FindKidsFolder()
        {
            foreach (string guid in AssetDatabase.FindAssets("PolygonKids t:Folder"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/PolygonKids")) return path;
            }
            return FallbackKidsFolder;
        }

        static void Upgrade(Material material, Shader lit)
        {
            // Read everything from the Standard shader before swapping.
            Texture baseMap = material.GetTexture("_MainTex");
            Vector2 tiling = material.GetTextureScale("_MainTex");
            Vector2 offset = material.GetTextureOffset("_MainTex");
            Color baseColor = material.GetColor("_Color");
            float smoothness = material.GetFloat("_Glossiness");
            float metallic = material.GetFloat("_Metallic");
            Texture normalMap = material.GetTexture("_BumpMap");
            bool emissive = material.IsKeywordEnabled("_EMISSION");
            Texture emissionMap = material.GetTexture("_EmissionMap");
            Color emissionColor = material.GetColor("_EmissionColor");
            float mode = material.GetFloat("_Mode");
            float cutoff = material.GetFloat("_Cutoff");

            material.shader = lit;
            material.shaderKeywords = new string[0];

            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureOffset("_BaseMap", offset);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (emissive)
            {
                material.SetTexture("_EmissionMap", emissionMap);
                material.SetColor("_EmissionColor", emissionColor);
                material.EnableKeyword("_EMISSION");
            }

            switch ((int)mode)
            {
                case 1: // Cutout
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", cutoff);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    material.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case 2: // Fade
                case 3: // Transparent
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    break;
                default: // Opaque
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_AlphaClip", 0f);
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.renderQueue = (int)RenderQueue.Geometry;
                    break;
            }

            EditorUtility.SetDirty(material);
        }
    }
}
