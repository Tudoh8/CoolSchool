using CoolSchool.Player;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>Editor menu items for trying out rewards without playing whole games.</summary>
    public static class RewardsMenu
    {
        const string ResetMenu = "CoolSchool/Rewards/Reset Progress";
        const string GiveStarsMenu = "CoolSchool/Rewards/Give 15 Stars (test)";
        const string UnlockAllMenu = "CoolSchool/Rewards/Unlock All (test)";
        const string NextPresentMenu = "CoolSchool/Rewards/Unlock Next Present (test)";
        const string GiveMedalMenu = "CoolSchool/Rewards/Give Medal (test)";
        const string CatalogPath = "Assets/CoolSchool/Rewards/Catalog.asset";

        [MenuItem(ResetMenu)]
        static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("Reset progress", "Forget all stars, presents and unlocked items?", "Reset", "Cancel")) return;

            ProgressStore.ResetAll();
            Debug.Log("CoolSchool: progress reset.");
        }

        [MenuItem(GiveStarsMenu)]
        static void GiveStars()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 position = player != null ? player.transform.position : Vector3.zero;
            RewardSystem.AddStars(15, position);
        }

        [MenuItem(GiveStarsMenu, true)]
        static bool CanGiveStars()
        {
            return Application.isPlaying;
        }

        /// <summary>Unlocks every item that is not a starter and marks it as new in the Toy Room. Works in and out of Play mode.</summary>
        [MenuItem(UnlockAllMenu)]
        static void UnlockAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("CoolSchool: no reward catalog yet. Run CoolSchool > Rewards > Seed Catalog first.");
                return;
            }

            PlayerProgress progress = ProgressStore.Current;
            int added = 0;
            foreach (RewardItem item in catalog.items)
            {
                if (item == null || string.IsNullOrEmpty(item.id) || item.starter) continue;

                if (!progress.IsUnlocked(item.id))
                {
                    progress.unlockedItems.Add(item.id);
                    added++;
                }
                if (!progress.unseenItems.Contains(item.id)) progress.unseenItems.Add(item.id);
                if (item.kind == RewardKind.Cart && !progress.unseenCartItems.Contains(item.id)) progress.unseenCartItems.Add(item.id);
            }
            ProgressStore.Save();
            Debug.Log($"CoolSchool: unlocked {added} items (everything that is not a starter).");
        }

        /// <summary>Shows the present popup for the next locked item, to try the swap question. Play mode only.</summary>
        [MenuItem(NextPresentMenu)]
        static void UnlockNextPresent()
        {
            PresentPopup popup = FindPopup();
            if (popup == null)
            {
                Debug.LogWarning("CoolSchool: no present popup in the scene. Rebuild the playground first.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<RewardCatalog>(CatalogPath);
            RewardItem next = catalog != null ? catalog.NextLocked(ProgressStore.Current) : null;
            if (next == null) ProgressStore.AddMedal();
            popup.Show(next);
        }

        [MenuItem(NextPresentMenu, true)]
        static bool CanUnlockNextPresent()
        {
            return Application.isPlaying;
        }

        /// <summary>Adds a medal. In Play mode the popup shows it too.</summary>
        [MenuItem(GiveMedalMenu)]
        static void GiveMedal()
        {
            ProgressStore.AddMedal();

            PresentPopup popup = Application.isPlaying ? FindPopup() : null;
            if (popup != null) popup.Show(null);
            Debug.Log($"CoolSchool: gave a medal ({ProgressStore.Current.medals} in total).");
        }

        static PresentPopup FindPopup()
        {
            // The popup is switched off until a present is shown, so look for inactive objects too.
            return Object.FindFirstObjectByType<PresentPopup>(FindObjectsInactive.Include);
        }
    }
}
