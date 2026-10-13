using System.Collections.Generic;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The cart garage's items in one place (contract Docs/specs/cart-garage-contract.md): every paint, add-on, trail and horn with its
    /// id, the name of its reveal prefab (made by the race builder) and what the seed needs besides (the paint colour, the horn's
    /// sound cue). The reward catalog seeder and the race builder both read this list, so a new paint is one line here (plus a voice
    /// line); a new add-on or trail also needs its visual in SoapboxStreetBuilder.Garage.cs.
    /// The cart BODIES (racer 1-3 and the ones agent G2 adds) are not here: they are seeded in the catalog seeder and built from
    /// SoapboxStreetBuilder.BodyDefs.
    /// </summary>
    internal static class CartGarageData
    {
        internal const string PaintRed = "cart.paint_red";
        internal const string PaintBlue = "cart.paint_blue";
        internal const string PaintGreen = "cart.paint_green";
        internal const string PaintPink = "cart.paint_pink";
        internal const string PaintPurple = "cart.paint_purple";

        internal const string Eyes = "cart.eyes";
        internal const string Balloons = "cart.balloons";
        internal const string Propeller = "cart.propeller";
        internal const string Spoiler = "cart.spoiler";

        internal const string TrailRainbow = "cart.trail_rainbow";
        internal const string TrailStars = "cart.trail_stars";
        internal const string TrailBubbles = "cart.trail_bubbles";
        internal const string TrailSparkles = "cart.trail_sparkles";

        internal const string HornBell = "cart.horn_bell";
        internal const string HornCow = "cart.horn_cow";
        internal const string HornDog = "cart.horn_dog";
        internal const string HornCat = "cart.horn_cat";
        internal const string HornElephant = "cart.horn_elephant";
        internal const string HornMagic = "cart.horn_magic";

        /// <summary>One garage item as the catalog seeds it.</summary>
        internal readonly struct Item
        {
            public readonly string Id;
            public readonly CartPart Part;
            /// <summary>The generated reveal prefab (CS_Cart_...), made by the race builder before the catalog is seeded.</summary>
            public readonly string PrefabName;
            /// <summary>A paint's colour (the paint pot and the painted body).</summary>
            public readonly Color Colour;
            /// <summary>A horn's sound cue.</summary>
            public readonly string SoundCue;
            /// <summary>Owned from the start and never given in a present.</summary>
            public readonly bool Starter;
            /// <summary>The reveal pose: carts with an add-on lie like the racers, the rest stand as they are built.</summary>
            public readonly bool CartPose;

            public Item(string id, CartPart part, string prefabName, Color colour, string soundCue, bool starter, bool cartPose)
            {
                Id = id;
                Part = part;
                PrefabName = prefabName;
                Colour = colour;
                SoundCue = soundCue;
                Starter = starter;
                CartPose = cartPose;
            }
        }

        static Item Paint(string id, string prefab, Color colour) => new Item(id, CartPart.Paint, prefab, colour, "", false, false);
        static Item Addon(string id, string prefab) => new Item(id, CartPart.Addon, prefab, Color.white, "", false, true);
        static Item Trail(string id, string prefab) => new Item(id, CartPart.Trail, prefab, Color.white, "", false, false);
        static Item Horn(string id, string prefab, string cue, bool starter = false) => new Item(id, CartPart.Horn, prefab, Color.white, cue, starter, false);

        /// <summary>All garage items except the bodies, the flag, the stickers and the gold paint (those three keep their old seeds).</summary>
        internal static readonly Item[] All =
        {
            Paint(PaintRed, "CS_Cart_PaintRed", new Color(0.92f, 0.16f, 0.16f)),
            Paint(PaintBlue, "CS_Cart_PaintBlue", new Color(0.16f, 0.42f, 0.96f)),
            Paint(PaintGreen, "CS_Cart_PaintGreen", new Color(0.22f, 0.76f, 0.30f)),
            Paint(PaintPink, "CS_Cart_PaintPink", new Color(1f, 0.45f, 0.72f)),
            Paint(PaintPurple, "CS_Cart_PaintPurple", new Color(0.62f, 0.32f, 0.92f)),

            Addon(Eyes, "CS_Cart_Eyes"),
            Addon(Balloons, "CS_Cart_Balloons"),
            Addon(Propeller, "CS_Cart_Propeller"),
            Addon(Spoiler, "CS_Cart_Spoiler"),

            Trail(TrailRainbow, "CS_Cart_TrailRainbow"),
            Trail(TrailStars, "CS_Cart_TrailStars"),
            Trail(TrailBubbles, "CS_Cart_TrailBubbles"),
            Trail(TrailSparkles, "CS_Cart_TrailSparkles"),

            Horn(HornBell, "CS_Cart_HornBell", "race.horn_bell", starter: true),
            Horn(HornCow, "CS_Cart_HornCow", "race.horn_cow"),
            Horn(HornDog, "CS_Cart_HornDog", "race.horn_dog"),
            Horn(HornCat, "CS_Cart_HornCat", "race.horn_cat"),
            Horn(HornElephant, "CS_Cart_HornElephant", "race.horn_elephant"),
            Horn(HornMagic, "CS_Cart_HornMagic", "race.horn_magic"),
        };

        /// <summary>The items of one part, in the order of <see cref="All"/>.</summary>
        internal static List<Item> OfPart(CartPart part)
        {
            var result = new List<Item>();
            foreach (Item item in All)
                if (item.Part == part) result.Add(item);
            return result;
        }

        /// <summary>The item with this id (Id is null when there is none).</summary>
        internal static Item Find(string id)
        {
            foreach (Item item in All)
                if (item.Id == id) return item;
            return default;
        }
    }
}
