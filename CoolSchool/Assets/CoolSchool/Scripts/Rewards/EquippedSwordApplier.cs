using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// Lives on the player. Gives the player's WeaponHolder the sword that was won last,
    /// at the start and whenever the progress changes.
    /// </summary>
    public class EquippedSwordApplier : MonoBehaviour
    {
        [SerializeField] RewardCatalog catalog;

        WeaponHolder holder;
        GameObject appliedPrefab;

        void Awake()
        {
            holder = GetComponent<WeaponHolder>();
        }

        void OnEnable()
        {
            ProgressStore.Changed += Apply;
        }

        void Start()
        {
            Apply();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= Apply;
        }

        void Apply()
        {
            if (holder == null || catalog == null) return;

            string id = ProgressStore.Current.equippedSword;
            if (string.IsNullOrEmpty(id)) return;

            RewardItem item = catalog.Find(id);
            if (item == null || item.prefab == null || item.prefab == appliedPrefab) return;

            holder.SetSwordPrefab(item.prefab);
            appliedPrefab = item.prefab;
        }
    }
}
