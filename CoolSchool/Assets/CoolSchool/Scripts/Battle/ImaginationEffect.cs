using System.Collections;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The "let's imagine..." moment: a soft golden glow over the screen, sparkles around the
    /// player, and the pretend castle things (flags, torches) appear or disappear.
    /// </summary>
    public class ImaginationEffect : MonoBehaviour
    {
        [Tooltip("A full-screen soft gold image. Fades in and out once per Play.")]
        [SerializeField] CanvasGroup overlay;
        [Tooltip("Things that only exist in the imagination, e.g. flags and torches.")]
        [SerializeField] GameObject[] imaginationProps;
        [SerializeField] float flashSeconds = 1f;
        [SerializeField] float maxOverlayAlpha = 0.35f;

        const int SparkleCount = 8;

        PlayerController player;
        Coroutine flashRoutine;

        void OnDisable()
        {
            flashRoutine = null;
            SetOverlayAlpha(0f);
        }

        /// <summary>Starts (true) or ends (false) the imagination, with a short glow either way.</summary>
        public void Play(bool on)
        {
            SetProps(on);
            SpawnSparkles();

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = null;

            if (overlay == null) return;

            if (isActiveAndEnabled) flashRoutine = StartCoroutine(FlashRoutine());
            else SetOverlayAlpha(0f);
        }

        IEnumerator FlashRoutine()
        {
            float duration = Mathf.Max(0.01f, flashSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Unscaled, so the glow still plays if the game is ever paused.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetOverlayAlpha(Mathf.Sin(t * Mathf.PI) * maxOverlayAlpha);
                yield return null;
            }

            SetOverlayAlpha(0f);
            flashRoutine = null;
        }

        void SetProps(bool on)
        {
            if (imaginationProps == null) return;

            foreach (GameObject prop in imaginationProps)
                if (prop != null) prop.SetActive(on);
        }

        void SpawnSparkles()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            for (int i = 0; i < SparkleCount; i++)
            {
                Vector2 ring = Random.insideUnitCircle * 1.5f;
                Vector3 position = player.transform.position + new Vector3(ring.x, Random.Range(0.2f, 1.6f), ring.y);
                BattleEffects.Sparkle(position);
            }
        }

        void SetOverlayAlpha(float alpha)
        {
            if (overlay != null) overlay.alpha = alpha;
        }
    }
}
