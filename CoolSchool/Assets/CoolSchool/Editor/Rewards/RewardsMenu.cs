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
        const string GiveStarsMenu = "CoolSchool/Rewards/Give 10 Stars (test)";

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
            RewardSystem.AddStars(10, position);
        }

        [MenuItem(GiveStarsMenu, true)]
        static bool CanGiveStars()
        {
            return Application.isPlaying;
        }
    }
}
