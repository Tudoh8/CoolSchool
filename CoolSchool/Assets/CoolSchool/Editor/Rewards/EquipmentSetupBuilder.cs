using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Player;
using CoolSchool.Rewards;
using UnityEditor;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// Sets up what the kid wears and carries: the hat holder and the equipment applier on the player, and the
    /// "Buddy" object with its follower. Called by RewardsSetupBuilder once the reward catalog exists.
    /// </summary>
    public static class EquipmentSetupBuilder
    {
        const string BuddyObjectName = "Buddy";

        public static void Setup(GameObject player, RewardCatalog catalog)
        {
            if (player == null)
            {
                Debug.LogWarning("CoolSchool: no player, so the equipment (hat, buddy, sword and shield) was not set up.");
                return;
            }

            // A scene that still has the old sword applier shows up as a missing script; take those off.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);

            WeaponHolder weapons = player.GetComponent<WeaponHolder>();
            if (weapons == null) Debug.LogWarning("CoolSchool: the player has no WeaponHolder, so swords and shields cannot be swapped.");

            HatHolder hat = player.GetComponent<HatHolder>();
            if (hat == null) hat = player.AddComponent<HatHolder>();

            BuddyFollower buddy = BuildBuddy(player);

            EquipmentApplier applier = player.GetComponent<EquipmentApplier>();
            if (applier == null) applier = player.AddComponent<EquipmentApplier>();
            PlaygroundPrototypeBuilder.SetObject(applier, "catalog", catalog);
            PlaygroundPrototypeBuilder.SetObject(applier, "weapons", weapons);
            PlaygroundPrototypeBuilder.SetObject(applier, "hat", hat);
            PlaygroundPrototypeBuilder.SetObject(applier, "buddy", buddy);
        }

        /// <summary>The plush buddy lives on its own scene object, not under the player, so it can walk by itself.</summary>
        static BuddyFollower BuildBuddy(GameObject player)
        {
            BuddyFollower buddy = Object.FindFirstObjectByType<BuddyFollower>();
            if (buddy == null)
            {
                var buddyObject = new GameObject(BuddyObjectName);
                buddyObject.transform.position = player.transform.position;
                buddy = buddyObject.AddComponent<BuddyFollower>();
            }

            PlaygroundPrototypeBuilder.SetObject(buddy, "player", player.GetComponent<PlayerController>());
            return buddy;
        }
    }
}
