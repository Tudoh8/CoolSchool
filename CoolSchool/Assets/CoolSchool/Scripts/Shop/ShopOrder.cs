using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// What one customer wants: a stack of scoops, bottom to top, in one or two colours.
    /// Level 1 is one or two scoops of one colour, level 2 one to three of one colour,
    /// level 3 two or three scoops in up to two colours (for example two pink and one brown).
    /// </summary>
    public sealed class ShopOrder
    {
        public const int MaxLevel = 3;

        /// <summary>The flavour of each scoop, bottom to top. Scoops of one colour come together.</summary>
        public Flavour[] Slots { get; private set; }

        public int Count => Slots.Length;

        /// <summary>How many scoops of each flavour, indexed by (int)Flavour.</summary>
        public int[] Totals { get; private set; }

        /// <summary>The colour groups in order, for example pink x2 then brown x1.</summary>
        public List<KeyValuePair<Flavour, int>> Groups { get; private set; }

        /// <summary>
        /// Makes an order for the level. The first colour is not one the last customer had, when that can be avoided.
        /// With forceThree the order has three scoops whatever the level (the dragon's).
        /// </summary>
        public static ShopOrder Create(int level, ShopOrder previous, bool forceThree)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);

            int count;
            if (forceThree) count = 3;
            else if (level == 1) count = Random.Range(1, 3);
            else if (level == 2) count = Random.Range(1, 4);
            else count = Random.Range(2, 4);

            Flavour first = PickFlavour(previous, null);
            var slots = new List<Flavour>();

            bool twoColours = level >= 3 && count >= 2 && Random.value < 0.7f;
            if (twoColours)
            {
                int firstCount = Random.Range(1, count);
                Flavour second = PickFlavour(null, first);
                for (int i = 0; i < count; i++) slots.Add(i < firstCount ? first : second);
            }
            else
            {
                for (int i = 0; i < count; i++) slots.Add(first);
            }

            return FromSlots(slots.ToArray());
        }

        static ShopOrder FromSlots(Flavour[] slots)
        {
            var order = new ShopOrder { Slots = slots, Totals = new int[FlavourInfo.Count], Groups = new List<KeyValuePair<Flavour, int>>() };
            foreach (Flavour flavour in slots) order.Totals[(int)flavour]++;

            for (int i = 0; i < slots.Length;)
            {
                int run = 1;
                while (i + run < slots.Length && slots[i + run] == slots[i]) run++;
                order.Groups.Add(new KeyValuePair<Flavour, int>(slots[i], run));
                i += run;
            }
            return order;
        }

        /// <summary>A random flavour that the previous order did not use and that is not 'not', when there is one.</summary>
        static Flavour PickFlavour(ShopOrder previous, Flavour? not)
        {
            var options = new List<Flavour>();
            for (int i = 0; i < FlavourInfo.Count; i++)
            {
                var flavour = (Flavour)i;
                if (not.HasValue && flavour == not.Value) continue;
                if (previous != null && previous.Totals[i] > 0) continue;
                options.Add(flavour);
            }

            if (options.Count == 0)
            {
                // Everything was used last time: any flavour but the excluded one.
                for (int i = 0; i < FlavourInfo.Count; i++)
                    if (!not.HasValue || (Flavour)i != not.Value) options.Add((Flavour)i);
            }
            return options[Random.Range(0, options.Count)];
        }
    }
}
