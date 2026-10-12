using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Colour grading for the whole game: a Color Lookup (LUT) override on the default Volume profile, plus post-processing
    /// switched on for the main camera. The PlaygroundPrototypeBuilder runs this on every rebuild; the menu item does it by hand.
    /// </summary>
    public static class ColorGradingSetup
    {
        // Tune the look here. Other LUTs live in Assets/Imported/AmplifyColor/LUT/.
        public const string LutPath = "Assets/Imported/AmplifyColor/LUT/Base/Vibrance_B.png";
        public const float LutContribution = 0.75f;

        /// <summary>The profile both URP assets use as their default Volume profile (the scene has no Volume object).</summary>
        const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        /// <summary>Safe to run again: fixes the LUT import settings and puts the lookup on the profile once.</summary>
        public static void ApplyToProfile()
        {
            var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath);
            if (lut == null)
            {
                Debug.LogError("CoolSchool: the colour grading LUT is missing: " + LutPath);
                return;
            }
            FixLutImport();
            lut = AssetDatabase.LoadAssetAtPath<Texture2D>(LutPath);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("CoolSchool: the Volume profile is missing: " + ProfilePath);
                return;
            }

            if (!profile.TryGet(out ColorLookup lookup))
            {
                lookup = profile.Add<ColorLookup>(true);
                lookup.name = nameof(ColorLookup);
                // Same as the Volume inspector: the component is saved inside the profile asset, hidden from the project view.
                lookup.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                if (!AssetDatabase.Contains(lookup)) AssetDatabase.AddObjectToAsset(lookup, profile);
            }

            lookup.active = true;
            lookup.texture.Override(lut);
            lookup.contribution.Override(LutContribution);

            EditorUtility.SetDirty(lookup);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>URP only accepts a strip LUT that is linear, uncompressed, unfiltered by mipmaps and clamped.</summary>
        static void FixLutImport()
        {
            var importer = AssetImporter.GetAtPath(LutPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.sRGBTexture) { importer.sRGBTexture = false; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }
            if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; changed = true; }

            if (changed) importer.SaveAndReimport();
        }

        /// <summary>Without this the camera skips all Volume effects, including the grading.</summary>
        public static void EnablePostProcessing(Camera camera)
        {
            if (camera == null) return;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        [MenuItem("CoolSchool/Apply Color Grading")]
        static void ApplyFromMenu()
        {
            ApplyToProfile();

            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                EnablePostProcessing(camera);
                EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            }

            Debug.Log("CoolSchool: colour grading applied (" + LutPath + ", contribution " + LutContribution + "). "
                + (camera != null ? "Post-processing is on for '" + camera.name + "'; save the scene to keep it." : "No camera found in the open scene."));
        }
    }
}
