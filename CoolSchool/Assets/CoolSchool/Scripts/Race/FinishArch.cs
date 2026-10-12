using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// The finish arch over the street. When the race is won (or finished) it cheers: bursts of colourful confetti pop along
    /// the banner, one after the other. It is not a track item: the race calls <see cref="Celebrate"/>.
    /// </summary>
    public class FinishArch : MonoBehaviour
    {
        static readonly Color[] BurstColors =
        {
            new Color(1f, 0.35f, 0.35f, 1f),
            new Color(1f, 0.85f, 0.25f, 1f),
            new Color(0.4f, 0.85f, 0.4f, 1f),
            new Color(0.4f, 0.7f, 1f, 1f),
            new Color(0.85f, 0.5f, 1f, 1f),
        };

        const int Bursts = 5;
        const float BurstGapSeconds = 0.18f;

        [Tooltip("The banner across the street. The confetti pops along it.")]
        [SerializeField] Transform banner;

        Coroutine running;

        /// <summary>Confetti bursts along the banner, and a cheer.</summary>
        public void Celebrate()
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(CelebrateRoutine());
        }

        IEnumerator CelebrateRoutine()
        {
            Bounds bounds = BannerBounds();
            Sfx.Play("race.cheer", bounds.center);

            for (int i = 0; i < Bursts; i++)
            {
                float across = Bursts > 1 ? i / (float)(Bursts - 1) : 0.5f;
                // Left to right along the banner, at its height.
                Vector3 where = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, across),
                    bounds.center.y,
                    bounds.center.z);

                BattleEffects.Confetti(where, BurstColors[i % BurstColors.Length]);
                if (i % 2 == 0) BattleEffects.Sparkle(where);
                yield return new WaitForSeconds(BurstGapSeconds);
            }

            running = null;
        }

        Bounds BannerBounds()
        {
            Transform target = banner != null ? banner : transform;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(target.position, new Vector3(6f, 1f, 1f));

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
