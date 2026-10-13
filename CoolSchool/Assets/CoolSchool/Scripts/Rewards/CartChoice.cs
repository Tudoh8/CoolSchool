using System.Collections.Generic;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// What a soapbox cart looks like, as ids: the body, the paint, the add-ons that are switched off, the trail, the horn and the
    /// race number. The cart picker edits one of these, CartLooks shows one, ProgressStore saves and loads it, and the rivals get
    /// random ones. Nothing here knows about the catalog; "" means the default (no paint, no trail, the starter bell).
    /// </summary>
    public sealed class CartChoice
    {
        /// <summary>The number plate shows this until he has chosen one (he is four).</summary>
        public const int DefaultNumber = 4;
        public const int MinNumber = 1;
        public const int MaxNumber = 10;

        public string bodyId = EquipSlots.StarterCartId;
        /// <summary>A paint item id, or "" for the body's own colours.</summary>
        public string paintId = "";
        /// <summary>A trail item id, or "" for no trail.</summary>
        public string trailId = "";
        /// <summary>A horn item id, or "" for the starter bell.</summary>
        public string hornId = "";
        public int number = DefaultNumber;
        /// <summary>Add-on ids that are switched off. An add-on that is not listed here is on (if it is owned, for the player).</summary>
        public readonly HashSet<string> addonsOff = new HashSet<string>();

        public bool AddonOn(string id) => !addonsOff.Contains(id);

        public void SetAddon(string id, bool on)
        {
            if (on) addonsOff.Remove(id);
            else addonsOff.Add(id);
        }

        public CartChoice Clone()
        {
            var copy = new CartChoice { bodyId = bodyId, paintId = paintId, trailId = trailId, hornId = hornId, number = number };
            foreach (string id in addonsOff) copy.addonsOff.Add(id);
            return copy;
        }
    }
}
