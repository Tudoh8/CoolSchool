using UnityEditor;
using UnityEngine;
using static CoolSchool.EditorTools.PlaygroundPrototypeBuilder;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The generated materials of the Toy Room (stands, mirror, medals, the black silhouette). All of them are re-made on every
    /// build and saved under Materials/ToyRoom.
    /// </summary>
    internal static class ToyRoomAssets
    {
        const string RootFolder = "Assets/CoolSchool";
        internal const string MaterialFolder = RootFolder + "/Materials/ToyRoom";

        /// <summary>An opaque URP/Lit material. Null if the shader is missing.</summary>
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

        /// <summary>The black material put on every renderer slot of a locked item (URP/Unlit, so only the outline shows).</summary>
        internal static Material Silhouette()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("CoolSchool: URP/Unlit shader not found; locked toys will use a fallback material.");
                return null;
            }

            var material = new Material(unlit) { name = "ToyRoomSilhouette" };
            material.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.06f, 1f));
            return Save(material, "ToyRoomSilhouette");
        }

        /// <summary>A transparent glowing material (the wardrobe zone's ring).</summary>
        internal static Material Glow(string fileName, Color colour, float alpha)
        {
            EnsureToyFolder();
            return KnightsArenaBuilder.CreateGlowMaterial(MaterialFolder + "/" + fileName + ".mat", colour, alpha);
        }

        static Material Save(Material material, string fileName)
        {
            EnsureToyFolder();
            string path = MaterialFolder + "/" + fileName + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void EnsureToyFolder()
        {
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder + "/Materials", "ToyRoom");
        }
    }
}
