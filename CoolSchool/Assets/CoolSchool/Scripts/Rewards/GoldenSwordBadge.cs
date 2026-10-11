using System.Collections;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// The little golden sword next to the star jar. It is there for as long as the child has a golden sword
    /// waiting for the castle (ProgressStore.Current.pendingPowerUp), so he can see that he "has" it.
    /// The golden sword reward can hold the badge back while its big sword is still flying toward it.
    /// </summary>
    public class GoldenSwordBadge : MonoBehaviour
    {
        [Tooltip("The picture (and its backdrop). Switched on and off; this component's own object stays active.")]
        [SerializeField] GameObject icon;

        const float PulseSpeed = 3f;
        const float PulseAmount = 0.07f;
        const float PopSeconds = 0.45f;
        const float PopScale = 1.6f;

        static GoldenSwordBadge instance;

        bool heldBack;
        bool shown;
        Coroutine popRoutine;

        public static GoldenSwordBadge Instance => instance;

        /// <summary>Where the badge is on the screen, in pixels (the touch canvas covers the whole screen).</summary>
        public Vector2 ScreenPosition
        {
            get
            {
                var rect = transform as RectTransform;
                return rect != null ? (Vector2)rect.position : new Vector2(Screen.width * 0.85f, Screen.height * 0.88f);
            }
        }

        void OnEnable()
        {
            instance = this;
            ProgressStore.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (instance == this) instance = null;
            ProgressStore.Changed -= Refresh;
            heldBack = false;
            popRoutine = null;
        }

        /// <summary>While held back, the badge stays hidden even if the sword is pending (the sword is still on its way).</summary>
        public void HoldBack(bool hold)
        {
            heldBack = hold;
            Refresh();
        }

        /// <summary>A happy little bounce, for when the flying sword arrives.</summary>
        public void Pop()
        {
            if (!isActiveAndEnabled || icon == null) return;

            if (popRoutine != null) StopCoroutine(popRoutine);
            popRoutine = StartCoroutine(PopRoutine());
        }

        void Refresh()
        {
            if (icon == null) return;

            bool want = !heldBack && ProgressStore.Current.pendingPowerUp == Lessons.PowerUps.GoldenSword;
            if (want == shown && icon.activeSelf == want) return;

            shown = want;
            icon.SetActive(want);
            if (want) icon.transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (!shown || icon == null || popRoutine != null) return;

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * PulseSpeed) * PulseAmount;
            icon.transform.localScale = Vector3.one * pulse;
        }

        IEnumerator PopRoutine()
        {
            for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.unscaledDeltaTime)
            {
                if (icon == null) break;

                float k = elapsed / PopSeconds;
                icon.transform.localScale = Vector3.one * Mathf.Lerp(1f, PopScale, Mathf.Sin(k * Mathf.PI));
                yield return null;
            }

            if (icon != null) icon.transform.localScale = Vector3.one;
            popRoutine = null;
        }
    }
}
