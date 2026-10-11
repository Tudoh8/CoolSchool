using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>The four ice cream flavours, which are also the four colours the child learns.</summary>
    public enum Flavour { Pink, Brown, Yellow, Green }

    /// <summary>The colour and the voice line name of each flavour.</summary>
    public static class FlavourInfo
    {
        public const int Count = 4;

        // Strawberry, chocolate, vanilla and pistachio.
        static readonly Color[] Colours =
        {
            new Color32(0xFF, 0x8F, 0xB8, 0xFF),
            new Color32(0x7A, 0x4A, 0x2A, 0xFF),
            new Color32(0xFF, 0xE2, 0x7A, 0xFF),
            new Color32(0x8F, 0xD9, 0x8A, 0xFF),
        };

        static readonly string[] VoiceNames = { "pink", "brown", "yellow", "green" };

        public static Color ColourOf(Flavour flavour) => Colours[(int)flavour];

        /// <summary>The colour in the voice line keys, for example "pink" in shop.order.2.pink.</summary>
        public static string VoiceName(Flavour flavour) => VoiceNames[(int)flavour];
    }
}
