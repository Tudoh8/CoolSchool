using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// The big present. It appears over a dimmed screen, wobbles until the child taps it, shakes,
    /// bursts open with confetti and shows the won item spinning in 3D. One more tap closes it.
    /// Put this on the full-screen dim Image; the object is switched off until a present is shown.
    /// The game keeps running behind it, but the player cannot move while it is open.
    /// </summary>
    public class PresentPopup : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("The full-screen dim picture on this object. It receives the taps.")]
        [SerializeField] Image dim;
        [Tooltip("The big gift picture (needs an Image).")]
        [SerializeField] RectTransform present;
        [Tooltip("Turning gold rays behind the revealed item.")]
        [SerializeField] RectTransform rays;
        [Tooltip("Shows the RenderTexture filmed by the reveal stage.")]
        [SerializeField] RawImage revealImage;
        [SerializeField] RevealStage stage;
        [SerializeField] float dimAlpha = 0.65f;
        [SerializeField] float shakeSeconds = 0.9f;
        [Tooltip("If nobody taps, the teacher reminds the child this often.")]
        [SerializeField] float nudgeSeconds = 7f;

        const float AppearSeconds = 0.45f;
        const float SwellSeconds = 0.3f;
        const float RevealPopSeconds = 0.55f;
        const float TapLockSeconds = 1f;
        const float FadeSeconds = 0.25f;
        const float RaysSpinSpeed = -20f;

        enum State
        {
            Closed,
            Appearing,
            Waiting,
            Shaking,
            Opening,
            Revealed
        }

        /// <summary>Raised when the popup has gone away again.</summary>
        public event Action Closed;

        State state = State.Closed;
        RewardItem item;
        PlayerController player;
        bool tapped;

        public bool IsOpen => state != State.Closed;

        /// <summary>Shows a present. The item is what is inside; null means everything is already won, so it is just a party.</summary>
        public void Show(RewardItem reward)
        {
            if (IsOpen) return;

            item = reward;
            state = State.Appearing;
            player = FindAnyObjectByType<PlayerController>();
            gameObject.SetActive(true);
            Sfx.Play("ui.popup_open");
            StartCoroutine(Flow());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            tapped = true;
        }

        void Update()
        {
            if (state == State.Closed) return;

            // Other game code may switch input back on, so keep the player still for as long as the present is here.
            if (player != null) player.InputEnabled = false;
            if (rays != null && rays.gameObject.activeSelf) rays.Rotate(0f, 0f, RaysSpinSpeed * Time.unscaledDeltaTime);
            if (state == State.Waiting) Wiggle();
        }

        void OnDisable()
        {
            Finish();
        }

        // ---- The steps of opening a present ----

        IEnumerator Flow()
        {
            ResetVisuals();
            yield return FadeDim(0f, dimAlpha, FadeSeconds);
            yield return Pop(present, AppearSeconds);

            RewardVoice.Say("reward.present_tap");
            yield return WaitForTap(true);

            yield return Shake();
            yield return Open();

            // A short pause, so a double tap does not close it before it has been seen.
            yield return new WaitForSecondsRealtime(TapLockSeconds);
            yield return WaitForTap(false);

            Sfx.Play("ui.popup_close");
            yield return FadeDim(dimAlpha, 0f, FadeSeconds);
            gameObject.SetActive(false);
        }

        IEnumerator WaitForTap(bool nudge)
        {
            state = nudge ? State.Waiting : State.Revealed;
            tapped = false;

            float nextNudge = Time.unscaledTime + nudgeSeconds;
            while (!tapped)
            {
                if (nudge && Time.unscaledTime > nextNudge)
                {
                    RewardVoice.Say("reward.present_tap");
                    nextNudge = Time.unscaledTime + nudgeSeconds;
                }
                yield return null;
            }
        }

        IEnumerator Shake()
        {
            state = State.Shaking;
            Sfx.Play("reward.present_shake");
            for (float elapsed = 0f; elapsed < shakeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float k = elapsed / shakeSeconds;
                present.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 45f) * Mathf.Lerp(6f, 18f, k));
                present.localScale = Vector3.one * (1f + 0.15f * k);
                yield return null;
            }
            present.localRotation = Quaternion.identity;
        }

        IEnumerator Open()
        {
            state = State.Opening;
            RewardVoice.Say("reward.present_open");
            Sfx.Play("reward.present_open");
            ProgressStore.OpenPresent(item);
            SpawnConfetti();

            bool hasReveal = stage != null && stage.Show(item);
            yield return Swell(hasReveal);

            if (rays != null) rays.gameObject.SetActive(true);
            if (hasReveal && revealImage != null)
            {
                revealImage.gameObject.SetActive(true);
                Sfx.Play("reward.item_reveal");
                yield return Pop(revealImage.rectTransform, RevealPopSeconds);
            }
            else
            {
                // Nothing left to win: the gift just pops back as a party.
                present.gameObject.SetActive(true);
                SetPresentAlpha(1f);
                yield return Pop(present, RevealPopSeconds);
            }
            SpawnConfetti();
        }

        /// <summary>The present swells and fades away. When there is nothing inside it stays.</summary>
        IEnumerator Swell(bool fadeAway)
        {
            if (!fadeAway) yield break;

            for (float elapsed = 0f; elapsed < SwellSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float k = elapsed / SwellSeconds;
                present.localScale = Vector3.one * Mathf.Lerp(1.15f, 1.8f, k);
                SetPresentAlpha(1f - k);
                yield return null;
            }
            present.gameObject.SetActive(false);
        }

        // ---- Small helpers ----

        void ResetVisuals()
        {
            SetDimAlpha(0f);
            present.gameObject.SetActive(true);
            present.localScale = Vector3.zero;
            present.localRotation = Quaternion.identity;
            SetPresentAlpha(1f);
            if (rays != null) rays.gameObject.SetActive(false);
            if (revealImage != null) revealImage.gameObject.SetActive(false);
        }

        void Wiggle()
        {
            float t = Time.unscaledTime;
            present.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4f) * 5f);
            present.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 6f));
        }

        void Finish()
        {
            if (state == State.Closed) return;

            state = State.Closed;
            if (stage != null) stage.Hide();
            if (player != null) player.InputEnabled = true;
            Closed?.Invoke();
        }

        /// <summary>Confetti in front of the game camera, so it is seen around the popup.</summary>
        static void SpawnConfetti()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Transform view = camera.transform;
            Vector3 middle = view.position + view.forward * 4f;
            BattleEffects.Confetti(middle + view.up * 1.2f);
            BattleEffects.Confetti(middle - view.right * 1.8f);
            BattleEffects.Confetti(middle + view.right * 1.8f);
        }

        IEnumerator Pop(RectTransform rect, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                rect.localScale = Vector3.one * EaseOutBack(elapsed / seconds);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        IEnumerator FadeDim(float from, float to, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                SetDimAlpha(Mathf.Lerp(from, to, elapsed / seconds));
                yield return null;
            }
            SetDimAlpha(to);
        }

        void SetDimAlpha(float alpha)
        {
            if (dim == null) return;

            Color color = dim.color;
            color.a = alpha;
            dim.color = color;
        }

        void SetPresentAlpha(float alpha)
        {
            var image = present.GetComponent<Image>();
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float u = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
