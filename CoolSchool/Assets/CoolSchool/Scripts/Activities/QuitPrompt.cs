using CoolSchool.Parent;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoolSchool.Activities
{
    /// <summary>
    /// The "do you want to stop?" screen: a dimmed overlay with two big picture buttons, a green check (stop, the current
    /// activity ends and free play returns) and a red cross (keep playing). No words, because the child cannot read; the
    /// teacher asks the question out loud. While it is open the game is frozen the same way as in the parent panel (time scale 0,
    /// audio paused, ParentGate.QuitPromptOpen set) and everything is put back exactly on close. Runs on unscaled time.
    /// </summary>
    public class QuitPrompt : MonoBehaviour
    {
        [Tooltip("The overlay (dim and window). Switched off while the prompt is closed.")]
        [SerializeField] GameObject root;
        [Tooltip("The green check: stop the activity.")]
        [SerializeField] Button stopButton;
        [Tooltip("The red cross: keep playing.")]
        [SerializeField] Button continueButton;
        [Tooltip("The window that pops in.")]
        [SerializeField] RectTransform window;
        [Tooltip("Speaks the question while the game is frozen. Ignores the listener pause; pitched like the teacher.")]
        [SerializeField] AudioSource voiceSource;

        const string AskGroup = "quit.ask";
        const string ByeGroup = "quit.bye";
        const float PopSeconds = 0.18f;
        const float PopStartScale = 0.85f;
        const float TapLockSeconds = 0.35f;

        float savedTimeScale = 1f;
        bool savedAudioPause;
        float openedAt;
        bool open;

        /// <summary>True while the prompt is showing and the game is frozen.</summary>
        public bool IsOpen => open;

        void Awake()
        {
            if (stopButton != null) stopButton.onClick.AddListener(Stop);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);

            if (voiceSource != null)
            {
                voiceSource.playOnAwake = false;
                voiceSource.spatialBlend = 0f;
                voiceSource.ignoreListenerPause = true;
            }

            if (root != null) root.SetActive(false);
        }

        void Update()
        {
            if (!open) return;

            // The Android back button arrives as Escape.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && Time.unscaledTime - openedAt >= TapLockSeconds)
            {
                Continue();
                return;
            }

            if (window != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - openedAt) / PopSeconds);
                window.localScale = Vector3.one * Mathf.Lerp(PopStartScale, 1f, t);
            }
        }

        void OnDisable()
        {
            // Never leave the game frozen if this object is switched off while open.
            if (open) Restore();
        }

        void OnDestroy()
        {
            if (open) Restore();
        }

        // ----- Open and close --------------------------------------------------------------------

        public void Open()
        {
            if (open || ParentGate.IsOpen) return;

            open = true;
            openedAt = Time.unscaledTime;
            savedTimeScale = Time.timeScale;
            savedAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ParentGate.QuitPromptOpen = true;

            if (window != null) window.localScale = Vector3.one * PopStartScale;
            if (root != null) root.SetActive(true);

            Ask();
        }

        /// <summary>The red cross, or Escape: keep playing exactly where the game was.</summary>
        public void Continue()
        {
            if (!open || Time.unscaledTime - openedAt < TapLockSeconds) return;

            Restore();
        }

        /// <summary>The green check: put everything back, then end the current activity so free play returns.</summary>
        public void Stop()
        {
            if (!open || Time.unscaledTime - openedAt < TapLockSeconds) return;

            Restore();

            ActivityManager manager = ActivityManager.Instance;
            Activity current = manager != null ? manager.Current : null;
            if (current != null && current != manager.DefaultActivity) current.End();

            // Interrupting clears the lines the game had queued, so they do not talk over the goodbye.
            VoicePlayer.Say(ByeGroup, interrupt: true);
        }

        /// <summary>Unfreezes the game and hides the overlay. Safe to call in any state.</summary>
        void Restore()
        {
            open = false;

            if (voiceSource != null) voiceSource.Stop();
            if (root != null) root.SetActive(false);

            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedAudioPause;
            ParentGate.QuitPromptOpen = false;
        }

        void Ask()
        {
            if (voiceSource == null) return;

            if (!VoicePlayer.TryGetLine(AskGroup, out AudioClip clip, out string text)) return;

            if (clip == null)
            {
                Debug.Log($"[Voice] {text}");
                return;
            }

            VoicePlayer player = VoicePlayer.Instance;
            voiceSource.pitch = player != null ? player.TeacherPitch : 1f;
            voiceSource.clip = clip;
            voiceSource.Play();
        }
    }
}
