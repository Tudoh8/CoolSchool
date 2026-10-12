using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Rewards;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The trophy chest at the dock. The more gold coins the child has counted over all his treasure hunts, the fuller it is:
    /// tier i (a pile of coins, a stack) shows once the total reaches thresholds[i], and the lid is open as soon as there
    /// is any gold. It follows ProgressStore.Changed, so it always shows the saved total. The adventure calls Celebrate at
    /// the end for a sparkle.
    /// </summary>
    public class TreasureTrophy : MonoBehaviour
    {
        [Tooltip("Coin piles, from the smallest to the biggest. Tier i is shown when the total reaches thresholds[i].")]
        [SerializeField] GameObject[] tiers;
        [SerializeField] int[] thresholds = { 1, 10, 25, 50 };
        [Tooltip("The chest model's lid child. Leave empty if there is none.")]
        [SerializeField] Transform lid;
        [Tooltip("How far the lid turns open, around its own axes.")]
        [SerializeField] Vector3 lidOpenEuler = new Vector3(-110f, 0f, 0f);

        static readonly Color GoldColor = new Color(1f, 0.8f, 0.3f, 1f);

        const float LidSeconds = 0.6f;
        const float TierPopSeconds = 0.4f;

        Quaternion lidClosedRotation = Quaternion.identity;
        Vector3[] tierBaseScale;
        bool[] tierShown;
        bool lidOpen;
        // False until the first Refresh has set everything up without animation.
        bool applied;
        bool started;
        Coroutine lidRoutine;

        void Awake()
        {
            if (lid != null) lidClosedRotation = lid.localRotation;

            int count = tiers != null ? tiers.Length : 0;
            tierBaseScale = new Vector3[count];
            tierShown = new bool[count];
            for (int i = 0; i < count; i++)
                if (tiers[i] != null) tierBaseScale[i] = tiers[i].transform.localScale;
        }

        void OnEnable()
        {
            ProgressStore.Changed += Refresh;
            if (started) Refresh();
        }

        void OnDisable()
        {
            ProgressStore.Changed -= Refresh;
        }

        void Start()
        {
            started = true;
            Refresh();
        }

        /// <summary>A sparkle on the chest, for when new gold has just been added.</summary>
        public void Celebrate()
        {
            Vector3 top = TopPoint();
            BattleEffects.Sparkle(top);
            BattleEffects.Confetti(top, GoldColor);
            Sfx.Play("reward.star_land", top);
        }

        void Refresh()
        {
            int total = ProgressStore.Current.pirateCoinsTotal;
            bool animate = applied;

            if (tiers != null)
            {
                for (int i = 0; i < tiers.Length; i++)
                {
                    if (tiers[i] == null) continue;

                    int threshold = thresholds != null && i < thresholds.Length ? thresholds[i] : int.MaxValue;
                    bool show = total >= threshold;
                    if (applied && show == tierShown[i]) continue;

                    tierShown[i] = show;
                    tiers[i].SetActive(show);
                    // A tier that appears while we watch pops up; the first look just shows the right state.
                    if (show && animate && isActiveAndEnabled)
                    {
                        tiers[i].transform.localScale = Vector3.zero;
                        StartCoroutine(TreasureMotion.PopIn(tiers[i].transform, tierBaseScale[i], TierPopSeconds));
                    }
                }
            }

            SetLid(total > 0, animate);
            applied = true;
        }

        void SetLid(bool open, bool animate)
        {
            if (lid == null || (applied && open == lidOpen)) return;

            lidOpen = open;
            Quaternion target = open ? lidClosedRotation * Quaternion.Euler(lidOpenEuler) : lidClosedRotation;
            if (lidRoutine != null) StopCoroutine(lidRoutine);

            if (!animate || !isActiveAndEnabled)
            {
                lid.localRotation = target;
                return;
            }
            lidRoutine = StartCoroutine(OpenLid(target));
        }

        IEnumerator OpenLid(Quaternion target)
        {
            yield return TreasureMotion.Rotate(lid, lid.localRotation, target, LidSeconds);
            lidRoutine = null;
        }

        Vector3 TopPoint()
        {
            if (TreasureMotion.TryGetBounds(gameObject, out Bounds bounds))
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return transform.position + Vector3.up * 0.6f;
        }
    }
}
