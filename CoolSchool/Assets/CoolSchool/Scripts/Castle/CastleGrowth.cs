using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The castle grows with every win: flags on the towers, banners on the wall, a golden flag on the gate tower. A tier appears
    /// once the number of castle wins reaches its threshold. At the start of the game every tier he has earned is simply there; a
    /// tier reached during play waits and pops in at the celebration (CelebrateNewest), with a sparkle trail from the queen.
    /// </summary>
    public class CastleGrowth : MonoBehaviour
    {
        [Tooltip("The 7 growth pieces in the order they appear. All hidden at the start.")]
        [SerializeField] GameObject[] tiers;
        [Tooltip("The castle wins needed for each piece.")]
        [SerializeField] int[] thresholds = { 1, 2, 3, 4, 6, 8, 11 };
        [Tooltip("Where the sparkle trail starts. Found in the scene when empty.")]
        [SerializeField] CastleQueen queen;

        readonly HashSet<int> shown = new HashSet<int>();
        readonly List<int> pending = new List<int>();
        readonly Dictionary<int, Vector3> scales = new Dictionary<int, Vector3>();
        bool started;
        float celebrateUntil;

        void OnEnable()
        {
            ProgressStore.Changed += OnProgressChanged;
            Refresh(true);
        }

        void OnDisable()
        {
            ProgressStore.Changed -= OnProgressChanged;
        }

        void OnProgressChanged()
        {
            Refresh(false);
        }

        /// <summary>
        /// Shows the pieces he has earned. At the start (immediate) they all appear at once; later, newly earned ones are kept back
        /// for the celebration, and pieces that are no longer earned (the progress was reset) disappear.
        /// </summary>
        void Refresh(bool immediate)
        {
            if (tiers == null) return;

            int wins = ProgressStore.Current.castleWins;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] == null) continue;

                bool earned = wins >= ThresholdOf(i);
                if (!earned)
                {
                    shown.Remove(i);
                    pending.Remove(i);
                    RestoreScale(i);
                    if (tiers[i].activeSelf) tiers[i].SetActive(false);
                }
                else if (shown.Contains(i))
                {
                    // A pop that was stopped half way (the back button) must not leave the piece too small.
                    if (Time.time > celebrateUntil) RestoreScale(i);
                }
                else if (!pending.Contains(i))
                {
                    if (immediate || !started)
                    {
                        shown.Add(i);
                        tiers[i].SetActive(true);
                    }
                    else
                    {
                        pending.Add(i);
                    }
                }
            }
            started = true;
        }

        /// <summary>
        /// If a piece was just earned, it pops in (small, bigger than its size, then its size) with a sparkle trail from the queen,
        /// the flag sound and "look, a new flag!". Otherwise nothing happens and nothing is said.
        /// </summary>
        public IEnumerator CelebrateNewest()
        {
            Refresh(false);
            if (pending.Count == 0) yield break;

            // Several at once (progress jumped): all but the newest just appear.
            int newest = pending[pending.Count - 1];
            foreach (int index in pending)
            {
                if (index == newest || tiers[index] == null) continue;

                tiers[index].SetActive(true);
                shown.Add(index);
            }
            pending.Clear();

            celebrateUntil = Time.time + 6f;
            GameObject tier = tiers[newest];
            Vector3 finalScale = tier.transform.localScale;
            scales[newest] = finalScale;
            tier.transform.localScale = Vector3.zero;
            tier.SetActive(true);
            shown.Add(newest);

            // The sparkle trail from the queen to the new piece.
            if (queen == null) queen = FindAnyObjectByType<CastleQueen>();
            Vector3 from = queen != null ? queen.transform.position + Vector3.up * 1.2f : tier.transform.position + Vector3.down * 3f;
            Vector3 to = CastleMotion.TopOf(tier.transform);
            for (int step = 0; step <= 6; step++)
            {
                BattleEffects.Sparkle(Vector3.Lerp(from, to, step / 6f));
                yield return new WaitForSeconds(0.08f);
            }

            Sfx.Play("castle.flag_pop", to);
            BattleEffects.Confetti(to);
            for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
            {
                float k = elapsed / 0.6f;
                // Past its size and back: 0 to 1.15 to 1.
                float scale = k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
                tier.transform.localScale = finalScale * scale;
                yield return null;
            }
            tier.transform.localScale = finalScale;

            yield return CastleMotion.SayAndWait("castle.grow");
        }

        /// <summary>A piece that was popping in when the game stopped gets its size back.</summary>
        public void ResetMoment()
        {
            StopAllCoroutines();
            foreach (KeyValuePair<int, Vector3> entry in scales)
                if (entry.Key < tiers.Length && tiers[entry.Key] != null) tiers[entry.Key].transform.localScale = entry.Value;
        }

        int ThresholdOf(int index)
        {
            if (thresholds != null && index < thresholds.Length) return thresholds[index];
            return int.MaxValue;
        }

        void RestoreScale(int index)
        {
            if (scales.TryGetValue(index, out Vector3 scale) && tiers[index] != null) tiers[index].transform.localScale = scale;
        }
    }
}
