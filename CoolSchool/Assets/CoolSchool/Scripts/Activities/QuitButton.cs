using CoolSchool.Audio;
using CoolSchool.Parent;
using CoolSchool.Rewards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CoolSchool.Activities
{
    /// <summary>
    /// The round back arrow in the top-left corner. It is shown only while an activity other than free play is running
    /// (and nothing covers the game); tapping it, or pressing Escape / the Android back button, opens the QuitPrompt.
    /// The root stays active so Update keeps running; only the Visual child (which also holds the touch area) is switched,
    /// so a hidden button never takes touches from the joystick underneath.
    /// </summary>
    public class QuitButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] QuitPrompt prompt;
        [Tooltip("The child that is shown and hidden (picture and touch area).")]
        [SerializeField] GameObject visual;

        const string TapCue = "ui.tap";
        const float PopSeconds = 0.2f;
        const float PopStartScale = 0.6f;

        PresentPopup presentPopup;
        float shownAt;
        bool wasVisible;
        bool blockedLastFrame;

        /// <summary>True while the button is on screen and can be used.</summary>
        public bool IsVisible => ShouldShow();

        void Update()
        {
            bool show = ShouldShow();
            // The Escape that closed the parent panel or this prompt must not also open the prompt again in the same frame,
            // whichever script updates first, so Escape only counts when nothing was covering the game a frame ago either.
            bool blocked = ParentGate.InputBlocked;
            bool escapeAllowed = show && !blockedLastFrame;
            blockedLastFrame = blocked;

            if (visual != null)
            {
                if (show != visual.activeSelf) visual.SetActive(show);

                if (show)
                {
                    if (!wasVisible) shownAt = Time.unscaledTime;
                    float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - shownAt) / PopSeconds);
                    visual.transform.localScale = Vector3.one * Mathf.Lerp(PopStartScale, 1f, t);
                }
            }
            wasVisible = show;

            if (escapeAllowed)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Open();
            }
        }

        void OnDisable()
        {
            wasVisible = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ShouldShow()) Open();
        }

        void Open()
        {
            if (prompt == null) return;

            Sfx.Play(TapCue);
            prompt.Open();
        }

        bool ShouldShow()
        {
            if (prompt == null || prompt.IsOpen || ParentGate.InputBlocked) return false;

            ActivityManager manager = ActivityManager.Instance;
            if (manager == null) return false;

            Activity current = manager.Current;
            if (current == null || current == manager.DefaultActivity || !current.IsRunning) return false;

            // The present popup covers the screen; leave it alone.
            if (presentPopup == null) presentPopup = FindFirstObjectByType<PresentPopup>(FindObjectsInactive.Include);
            return presentPopup == null || !presentPopup.IsOpen;
        }
    }
}
