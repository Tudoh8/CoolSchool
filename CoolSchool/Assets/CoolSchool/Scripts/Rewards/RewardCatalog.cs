using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>All the things that can be won, in the order they are given out.</summary>
    [CreateAssetMenu(fileName = "Catalog", menuName = "CoolSchool/Reward Catalog")]
    public class RewardCatalog : ScriptableObject
    {
        public List<RewardItem> items = new List<RewardItem>();

        /// <summary>The first item the child does not have yet (starters are skipped), or null when everything is unlocked.</summary>
        public RewardItem NextLocked(PlayerProgress progress)
        {
            foreach (RewardItem item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id) || item.starter) continue;
                if (progress == null || !progress.IsUnlocked(item.id)) return item;
            }
            return null;
        }

        /// <summary>All items worn in this slot, in catalog order (starters, presents, gold).</summary>
        public List<RewardItem> ItemsFor(EquipSlot slot)
        {
            var result = new List<RewardItem>();
            foreach (RewardItem item in items)
                if (item != null && !string.IsNullOrEmpty(item.id) && item.Slot == slot) result.Add(item);
            return result;
        }

        /// <summary>All items of this kind, in catalog order.</summary>
        public List<RewardItem> ItemsOfKind(RewardKind kind)
        {
            var result = new List<RewardItem>();
            foreach (RewardItem item in items)
                if (item != null && !string.IsNullOrEmpty(item.id) && item.kind == kind) result.Add(item);
            return result;
        }

        /// <summary>All cart items that are this part of the cart, in catalog order (the cart picker's cards).</summary>
        public List<RewardItem> ItemsOfPart(CartPart part)
        {
            var result = new List<RewardItem>();
            foreach (RewardItem item in items)
                if (item != null && !string.IsNullOrEmpty(item.id) && item.IsCartPart(part)) result.Add(item);
            return result;
        }

        public RewardItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (RewardItem item in items)
                if (item != null && item.id == id) return item;
            return null;
        }
    }
}
