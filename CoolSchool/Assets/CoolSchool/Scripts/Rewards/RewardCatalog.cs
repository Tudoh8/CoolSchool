using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>All the things that can be won, in the order they are given out.</summary>
    [CreateAssetMenu(fileName = "Catalog", menuName = "CoolSchool/Reward Catalog")]
    public class RewardCatalog : ScriptableObject
    {
        public List<RewardItem> items = new List<RewardItem>();

        /// <summary>The first item the child does not have yet, or null when everything is unlocked.</summary>
        public RewardItem NextLocked(PlayerProgress progress)
        {
            foreach (RewardItem item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                if (progress == null || !progress.IsUnlocked(item.id)) return item;
            }
            return null;
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
