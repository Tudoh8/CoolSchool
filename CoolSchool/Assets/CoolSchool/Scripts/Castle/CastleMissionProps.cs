using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The three things on the table (and the nest) in the west of the courtyard that goblins want: the party cake with its slice,
    /// the golden crown on its cushion, and the dragon egg in its nest. A visit shows the one that belongs to its mission, or
    /// none (the knight mission). Each set holds a MissionLoot, which the thief carries off and which flies home again.
    /// </summary>
    public class CastleMissionProps : MonoBehaviour
    {
        [Tooltip("The whole cake set: the cake and the slice beside it.")]
        [SerializeField] GameObject cake;
        [Tooltip("The golden crown and the cushion it lies on.")]
        [SerializeField] GameObject crown;
        [Tooltip("The egg and its nest.")]
        [SerializeField] GameObject egg;

        /// <summary>Shows the set for this mission and hides the others. The knight mission shows nothing.</summary>
        public void Show(CastleMission mission)
        {
            ResetAll();

            switch (mission)
            {
                case CastleMission.Cake: SetShown(cake, true); break;
                case CastleMission.Crown: SetShown(crown, true); break;
                case CastleMission.Egg: SetShown(egg, true); break;
            }
        }

        /// <summary>Everything hidden again, and every loot back at its place.</summary>
        public void ResetAll()
        {
            ResetLoot(cake);
            ResetLoot(crown);
            ResetLoot(egg);

            SetShown(cake, false);
            SetShown(crown, false);
            SetShown(egg, false);
        }

        static void SetShown(GameObject set, bool shown)
        {
            if (set != null && set.activeSelf != shown) set.SetActive(shown);
        }

        static void ResetLoot(GameObject set)
        {
            if (set == null) return;

            foreach (MissionLoot loot in set.GetComponentsInChildren<MissionLoot>(true))
                loot.ResetLoot();
        }
    }
}
