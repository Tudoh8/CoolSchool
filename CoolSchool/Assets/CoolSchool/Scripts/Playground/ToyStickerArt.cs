using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>Run-time art for the stickers: a round disc sprite (made once from a generated texture) and a rim colour for each sticker.</summary>
    internal static class ToyStickerArt
    {
        const int DiscSize = 128;

        static readonly Color[] Rims =
        {
            new Color(0.96f, 0.36f, 0.30f), new Color(0.98f, 0.66f, 0.14f), new Color(0.98f, 0.84f, 0.20f), new Color(0.45f, 0.78f, 0.30f),
            new Color(0.22f, 0.72f, 0.62f), new Color(0.26f, 0.60f, 0.92f), new Color(0.46f, 0.42f, 0.90f), new Color(0.78f, 0.40f, 0.86f),
            new Color(0.94f, 0.42f, 0.66f), new Color(0.98f, 0.52f, 0.24f), new Color(0.40f, 0.70f, 0.86f), new Color(0.62f, 0.78f, 0.28f),
        };

        static Sprite disc;

        /// <summary>A white anti-aliased disc; tint it with a colour.</summary>
        public static Sprite Disc
        {
            get
            {
                if (disc != null) return disc;

                var texture = new Texture2D(DiscSize, DiscSize, TextureFormat.RGBA32, false)
                {
                    name = "ToyStickerDisc",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                float radius = DiscSize * 0.5f - 1.5f;
                float centre = (DiscSize - 1) * 0.5f;
                var pixels = new Color32[DiscSize * DiscSize];
                for (int y = 0; y < DiscSize; y++)
                {
                    for (int x = 0; x < DiscSize; x++)
                    {
                        float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                        float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                        pixels[y * DiscSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                disc = Sprite.Create(texture, new Rect(0, 0, DiscSize, DiscSize), new Vector2(0.5f, 0.5f), DiscSize);
                disc.hideFlags = HideFlags.HideAndDontSave;
                return disc;
            }
        }

        /// <summary>The coloured rim of a sticker, the same every time for the same id.</summary>
        public static Color RimColour(string stickerId)
        {
            int index = System.Array.IndexOf(ToyStickers.AllIds, stickerId);
            if (index < 0) index = 0;
            return Rims[index % Rims.Length];
        }
    }
}
