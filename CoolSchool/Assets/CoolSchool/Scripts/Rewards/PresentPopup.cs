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

        [Header("Swap question and medal")]
        [Tooltip("Used to find the picture of what he wears now.")]
        [SerializeField] RewardCatalog catalog;
        [Tooltip("Shown after the reveal for wearable items. Inactive by default.")]
        [SerializeField] RectTransform swapPanel;
        [Tooltip("Picture of what he wears now (left of the 3D item). The empty-slot sprite when nothing is worn.")]
        [SerializeField] Image currentIcon;
        [SerializeField] Sprite emptySlotSprite;
        [Tooltip("Big green circle with a check mark: use the new one.")]
        [SerializeField] Button useNewButton;
        [Tooltip("Big red circle with a cross: keep the old one.")]
        [SerializeField] Button keepOldButton;
        [Tooltip("Shown instead of the 3D item when the present holds a medal.")]
        [SerializeField] Image medalImage;
        [Tooltip("The teacher repeats the swap question this often until he answers. There is no timeout.")]
        [SerializeField] float swapNudgeSeconds = 8f;

        const float AppearSeconds = 0.45f;
        const float SwellSeconds = 0.3f;
        const float RevealPopSeconds = 0.55f;
        const float TapLockSeconds = 1f;
        const float FadeSeconds = 0.25f;
        const float RaysSpinSpeed = -20f;
        const float SwapPopSeconds = 0.4f;
        const float AnswerVoiceMaxSeconds = 3f;
        const float EmptySlotAlpha = 0.45f;

        enum State
        {
            Closed,
            Appearing,
            Waiting,
            Shaking,
            Opening,
            Asking,
            Revealed
        }

        enum SwapAnswer
        {
            None,
            UseNew,
            KeepOld
        }

        /// <summary>Raised when the popup has gone away again.</summary>
        public event Action Closed;

        State state = State.Closed;
        RewardItem item;
        PlayerController player;
        bool tapped;
        SwapAnswer swapAnswer;
        bool asked;

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

        void Awake()
        {
            if (useNewButton != null) useNewButton.onClick.AddListener(() => Answer(SwapAnswer.UseNew));
            if (keepOldButton != null) keepOldButton.onClick.AddListener(() => Answer(SwapAnswer.KeepOld));
        }

        void Answer(SwapAnswer answer)
        {
            if (state == State.Asking && swapAnswer == SwapAnswer.None) swapAnswer = answer;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // While the swap question is up only the two buttons answer.
            if (state == State.Asking) return;
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

            asked = false;
            if (item != null && item.Slot != EquipSlot.None && ProgressStore.EquippedId(item.Slot) != item.id)
            {
                // A new sword, shield, hat or buddy: ask whether to use it. The answer closes the popup, no extra tap.
                asked = true;
                yield return AskSwap();
            }
            else if (item != null && item.kind == RewardKind.Cart)
            {
                RewardVoice.Say("reward.cart_new");
            }

            if (!asked)
            {
                // A short pause, so a double tap does not close it before it has been seen.
                yield return new WaitForSecondsRealtime(TapLockSeconds);
                yield return WaitForTap(false);
            }

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
            // No item means everything is won already: the present holds a medal for the Toy Room wall.
            bool isMedal = item == null && medalImage != null;
            RewardVoice.Say(item == null ? "reward.medal" : "reward.present_open");
            Sfx.Play("reward.present_open");
            ProgressStore.OpenPresent(item);
            SpawnConfetti();

            bool hasReveal = stage != null && stage.Show(item);
            yield return Swell(hasReveal || isMedal);

            if (rays != null) rays.gameObject.SetActive(true);
            if (isMedal)
            {
                medalImage.gameObject.SetActive(true);
                Sfx.Play("reward.item_reveal");
                yield return Pop(medalImage.rectTransform, RevealPopSeconds);
            }
            else if (hasReveal && revealImage != null)
            {
                revealImage.gameObject.SetActive(true);
                Sfx.Play("reward.item_reveal");
                yield return Pop(revealImage.rectTransform, RevealPopSeconds);
                RewardVoice.Say(ItemVoice.GroupFor(item));
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

        /// <summary>
        /// "Do you want to use the new one?": the picture of what he wears now, the spinning new item, and a green check
        /// and a red cross. There is no timeout; the teacher asks again every few seconds. The answer closes the popup.
        /// </summary>
        IEnumerator AskSwap()
        {
            EquipSlot slot = item.Slot;
            state = State.Asking;
            swapAnswer = SwapAnswer.None;

            ShowCurrentIcon(slot);
            if (swapPanel != null)
            {
                swapPanel.gameObject.SetActive(true);
                Sfx.Play("ui.popup_open");
                yield return Pop(swapPanel, SwapPopSeconds);
            }

            string question = "reward.swap_" + SlotWord(slot);
            RewardVoice.Say(question);
            float nextNudge = Time.unscaledTime + swapNudgeSeconds;
            while (swapAnswer == SwapAnswer.None)
            {
                if (Time.unscaledTime > nextNudge)
                {
                    if (!RewardVoice.IsBusy()) RewardVoice.Say(question);
                    nextNudge = Time.unscaledTime + swapNudgeSeconds;
                }
                yield return null;
            }

            if (swapAnswer == SwapAnswer.UseNew)
            {
                ProgressStore.Equip(slot, item.id);
                Sfx.Play("wardrobe.equip");
                SpawnConfetti();
                RewardVoice.Say("reward.swap_yes");
            }
            else
            {
                RewardVoice.Say("reward.swap_no");
            }

            if (swapPanel != null) swapPanel.gameObject.SetActive(false);
            state = State.Revealed;

            // Let the teacher finish, then close without another tap.
            yield return new WaitForSecondsRealtime(0.3f);
            float giveUp = Time.unscaledTime + AnswerVoiceMaxSeconds;
            while (RewardVoice.IsSpeaking() && Time.unscaledTime < giveUp) yield return null;
        }

        void ShowCurrentIcon(EquipSlot slot)
        {
            if (currentIcon == null) return;

            string wornId = ProgressStore.EquippedId(slot);
            RewardItem worn = catalog != null ? catalog.Find(wornId) : null;
            if (worn != null && worn.icon != null)
            {
                currentIcon.sprite = worn.icon;
                currentIcon.color = Color.white;
            }
            else
            {
                currentIcon.sprite = emptySlotSprite;
                currentIcon.color = new Color(1f, 1f, 1f, EmptySlotAlpha);
            }
            currentIcon.enabled = currentIcon.sprite != null;
        }

        static string SlotWord(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Sword: return "sword";
                case EquipSlot.Shield: return "shield";
                case EquipSlot.Hat: return "hat";
                default: return "buddy";
            }
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
            if (medalImage != null) medalImage.gameObject.SetActive(false);
            if (swapPanel != null)
            {
                swapPanel.gameObject.SetActive(false);
                swapPanel.localScale = Vector3.zero;
            }
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

            // Switched off in the middle of the question: nothing is equipped, he keeps the old one.
            state = State.Closed;
            swapAnswer = SwapAnswer.None;
            if (swapPanel != null) swapPanel.gameObject.SetActive(false);
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
