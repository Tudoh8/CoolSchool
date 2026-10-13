using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Makes the painted copies of a cart body's materials (contract Docs/specs/cart-garage-contract.md). The Synty bodies share one
    /// colour atlas, so a paint is a copy of the atlas in which every saturated, not-too-dark texel is recoloured toward the paint
    /// colour (its hue becomes the paint's, its saturation and brightness move toward the paint's). Greys, whites and darks (tyres,
    /// metal, seat edges) stay, so only the bright body colours change and the details keep their shading. The copy is scaled down
    /// to at most 1024 pixels (the atlas is blocks of flat colour, so nothing is lost) to keep five paints cheap on the tablet.
    /// It works on any body material: textured ones get recoloured copies of every colour texture, plain-coloured ones a recoloured
    /// colour. Everything is saved under Assets/CoolSchool/Race/Paint with names that start with the paint, and shared by all carts of
    /// one build through the cache.
    /// A body that is mostly white (the police car) asks for <c>paintWhites</c>: its copies also tint the light, grey-ish texels (the white
    /// panels) toward the paint, keeping their shading, while darks (tyres, windows, black trim) and mid greys (hubs, chrome) stay. Those
    /// copies get their own names and files ("Paint_blue_Whites_..."), so a material that other bodies share is never changed by it.
    /// </summary>
    internal static class CartPaintMaker
    {
        internal const string Folder = "Assets/CoolSchool/Race/Paint";
        const int MaxSize = 1024;

        // A texel counts as paintable from this saturation and brightness on (a smooth step, so edges blend).
        const float SaturationFrom = 0.2f;
        const float SaturationFull = 0.42f;
        const float BrightnessFrom = 0.12f;
        const float BrightnessFull = 0.3f;

        // With paintWhites, a texel counts as a white panel up to this saturation (fading out between the two) and from this brightness on.
        const float WhiteSaturationFull = 0.1f;
        const float WhiteSaturationTo = 0.22f;
        const float WhiteBrightnessFrom = 0.58f;
        const float WhiteBrightnessFull = 0.78f;
        /// <summary>How much of the paint's saturation a white panel gets (a little lighter than the stripes, so the stripes still show).</summary>
        const float WhitePaintSaturation = 0.75f;

        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>Forget the copies made in earlier builds (call at the start of a build; the files are replaced when made again).</summary>
        internal static void ClearCache()
        {
            Textures.Clear();
            Materials.Clear();
        }

        /// <summary>
        /// The painted copy of a body material for this paint. Never null (the source itself if nothing could be made). With
        /// <paramref name="paintWhites"/> the white panels are tinted too, and the copy (and its textures) get their own names.
        /// </summary>
        internal static Material Painted(string paintId, Color colour, Material source, bool paintWhites = false)
        {
            if (source == null) return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string key = paintId + "|" + sourcePath + "|" + source.name + (paintWhites ? "|whites" : "");
            if (Materials.TryGetValue(key, out Material done)) return done;

            PlaygroundPrototypeBuilder.EnsureFolder("Assets/CoolSchool/Race", "Paint");

            var copy = new Material(source) { name = "Paint_" + ShortName(paintId) + (paintWhites ? "_Whites_" : "_") + source.name };
            bool recoloured = false;
            foreach (string property in source.GetTexturePropertyNames())
            {
                if (IsDataMap(property)) continue;
                if (!(source.GetTexture(property) is Texture2D texture)) continue;

                Texture2D painted = PaintedTexture(paintId, colour, texture, paintWhites);
                if (painted == null) continue;

                copy.SetTexture(property, painted);
                recoloured = true;
            }

            // A material without a colour texture (or one that could not be read): recolour its colour instead.
            if (!recoloured)
            {
                foreach (string property in new[] { "_BaseColor", "_Color" })
                {
                    if (!copy.HasProperty(property)) continue;
                    copy.SetColor(property, RecolourOne(copy.GetColor(property), colour, paintWhites));
                }
            }

            string path = Folder + "/" + copy.name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            Material saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            Materials[key] = saved;
            return saved;
        }

        static string ShortName(string paintId)
        {
            return paintId.StartsWith("cart.") ? paintId.Substring(5) : paintId.Replace('.', '_');
        }

        /// <summary>Normal, emission, occlusion, mask and other maps hold data, not colours: they are left as they are.</summary>
        static bool IsDataMap(string property)
        {
            string lower = property.ToLowerInvariant();
            foreach (string word in new[] { "normal", "bump", "emission", "emissive", "occlusion", "mask", "metal", "smooth", "rough", "height", "detail", "parallax", "lightmap" })
                if (lower.Contains(word)) return true;
            return false;
        }

        // ----- Textures -----------------------------------------------------------------------------

        static Texture2D PaintedTexture(string paintId, Color colour, Texture2D source, bool paintWhites)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string key = paintId + "|" + sourcePath + (paintWhites ? "|whites" : "");
            if (Textures.TryGetValue(key, out Texture2D known)) return known;

            Texture2D result = null;
            if (TryLoadSmall(sourcePath, out Color32[] pixels, out int width, out int height))
            {
                Recolour(pixels, colour, paintWhites);
                result = Save(paintId, source, pixels, width, height, paintWhites);
            }
            else
            {
                Debug.LogWarning($"CoolSchool race paint: could not read the texture '{sourcePath}' (only png and jpg files are read); material copies that use it are tinted instead.");
            }
            Textures[key] = result;
            return result;
        }

        /// <summary>Reads the image file as it is stored (no colour space change) and averages blocks of pixels down to at most MaxSize.</summary>
        static bool TryLoadSmall(string assetPath, out Color32[] small, out int width, out int height)
        {
            small = null;
            width = 0;
            height = 0;
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") return false;

            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (!File.Exists(full)) return false;

            var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(loaded, File.ReadAllBytes(full), false)) return false;

                int sourceWidth = loaded.width;
                int sourceHeight = loaded.height;
                int factor = Mathf.Max(1, Mathf.Max(sourceWidth, sourceHeight) / MaxSize);
                width = Mathf.Max(1, sourceWidth / factor);
                height = Mathf.Max(1, sourceHeight / factor);

                Color32[] all = loaded.GetPixels32();
                small = new Color32[width * height];
                int count = factor * factor;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0;
                        for (int dy = 0; dy < factor; dy++)
                        {
                            int row = (y * factor + dy) * sourceWidth + x * factor;
                            for (int dx = 0; dx < factor; dx++)
                            {
                                Color32 c = all[row + dx];
                                r += c.r;
                                g += c.g;
                                b += c.b;
                                a += c.a;
                            }
                        }
                        small[y * width + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                    }
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(loaded);
            }
        }

        /// <summary>Moves every paintable texel toward the paint colour. See the class comment for the rule.</summary>
        static void Recolour(Color32[] pixels, Color paint, bool paintWhites)
        {
            Color.RGBToHSV(paint, out float targetHue, out float targetSaturation, out float targetValue);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                var original = new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
                if (!TryRecolour(original, targetHue, targetSaturation, targetValue, paintWhites, out Color mix)) continue;

                pixels[i] = new Color32((byte)(mix.r * 255f + 0.5f), (byte)(mix.g * 255f + 0.5f), (byte)(mix.b * 255f + 0.5f), c.a);
            }
        }

        static Color RecolourOne(Color original, Color paint, bool paintWhites)
        {
            Color.RGBToHSV(paint, out float targetHue, out float targetSaturation, out float targetValue);
            if (!TryRecolour(original, targetHue, targetSaturation, targetValue, paintWhites, out Color mix)) return original;

            mix.a = original.a;
            return mix;
        }

        /// <summary>
        /// The recoloured texel, or false when it stays as it is. A saturated, bright texel moves toward the paint (the normal rule). With
        /// paintWhites a light, nearly grey texel (a white panel) takes the paint's hue instead and keeps its own brightness, so the
        /// panel's shading (and its slight differences between panels) survives; darks and mid greys are not light enough to count.
        /// </summary>
        static bool TryRecolour(Color original, float targetHue, float targetSaturation, float targetValue, bool paintWhites, out Color mix)
        {
            Color.RGBToHSV(original, out _, out float saturation, out float value);

            float weight = Smooth(SaturationFrom, SaturationFull, saturation) * Smooth(BrightnessFrom, BrightnessFull, value);
            float whiteWeight = paintWhites ? (1f - Smooth(WhiteSaturationFull, WhiteSaturationTo, saturation)) * Smooth(WhiteBrightnessFrom, WhiteBrightnessFull, value) : 0f;
            mix = original;

            if (whiteWeight > weight && whiteWeight > 0.001f)
            {
                Color tinted = Color.HSVToRGB(targetHue, targetSaturation * WhitePaintSaturation, Mathf.Clamp01(value * Mathf.Lerp(1f, targetValue, 0.5f)));
                mix = Color.Lerp(original, tinted, whiteWeight);
                return true;
            }

            if (weight <= 0.001f) return false;

            Color painted = Color.HSVToRGB(targetHue, Mathf.Lerp(saturation, targetSaturation, 0.65f), Mathf.Clamp01(Mathf.Lerp(value, targetValue, 0.45f)));
            mix = Color.Lerp(original, painted, weight);
            return true;
        }

        static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / Mathf.Max(0.0001f, to - from));
            return t * t * (3f - 2f * t);
        }

        static Texture2D Save(string paintId, Texture2D source, Color32[] pixels, int width, int height, bool paintWhites)
        {
            string path = Folder + "/Paint_" + ShortName(paintId) + (paintWhites ? "_Whites_" : "_") + source.name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = MaxSize;
                importer.filterMode = source.filterMode;
                importer.wrapMode = source.wrapMode;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
