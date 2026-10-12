using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The red X on the island where the treasure is buried. Standing on it, a tap on the one big button (or Space) digs:
    /// the kid swings, sand puffs up and the X gets smaller. After a few taps the chest is dug up (Dug).
    /// A tap only counts while the kid is near the X, so he cannot dig the wrong place. Armed by the adventure.
    /// </summary>
    public class DigSpot : MonoBehaviour
    {
        [SerializeField] ActionButton button;
        [SerializeField] Transform player;
        [Tooltip("The red X.")]
        [SerializeField] GameObject mark;
        [SerializeField] float radius = 1.4f;
        [SerializeField] int tapsNeeded = 4;

        static readonly Color SandColor = new Color(0.86f, 0.74f, 0.5f, 1f);

        const float ShrunkScale = 0.35f;
        const float PunchSeconds = 0.3f;
        const float VanishSeconds = 0.3f;
        const float TurnToSpotDegrees = 540f;

        /// <summary>True while a tap counts as a dig.</summary>
        public bool Armed { get; set; }

        /// <summary>True when the kid stands close enough to the X to dig.</summary>
        public bool PlayerNear
        {
            get
            {
                Transform kid = FindPlayer();
                if (kid == null) return false;

                Vector3 offset = kid.position - transform.position;
                offset.y = 0f;
                return offset.magnitude <= radius;
            }
        }

        /// <summary>Raised when the last dig has been made.</summary>
        public event System.Action Dug;

        int taps;
        bool shown;
        Vector3 markBaseScale = Vector3.one;
        KidAnimator kidAnimator;
        Coroutine markRoutine;

        void Awake()
        {
            if (mark != null) markBaseScale = mark.transform.localScale;
            ApplyShown(false);
        }

        void OnEnable()
        {
            if (button != null) button.Pressed += OnButtonPressed;
        }

        void OnDisable()
        {
            if (button != null) button.Pressed -= OnButtonPressed;
        }

        void Update()
        {
            if (!Armed || !shown) return;

            // Keys typed into the parent panel's name field are not game input.
            if (CoolSchool.Parent.ParentGate.InputBlocked) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Dig();
                return;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) Dig();
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Shows or hides the red X. Showing it again starts the digging over.</summary>
        public void Show(bool show)
        {
            if (show) ResetSpot();
            ApplyShown(show);
        }

        /// <summary>Back to the start: no digs yet, X hidden, not armed.</summary>
        public void ResetSpot()
        {
            taps = 0;
            Armed = false;
            if (markRoutine != null)
            {
                StopCoroutine(markRoutine);
                markRoutine = null;
            }
            if (mark != null) mark.transform.localScale = markBaseScale;
            ApplyShown(false);
        }

        // ----- Digging -----------------------------------------------------------------------------

        void OnButtonPressed()
        {
            if (!Armed || !shown) return;
            if (CoolSchool.Parent.ParentGate.InputBlocked) return;

            Dig();
        }

        void Dig()
        {
            if (!Armed || !shown || !PlayerNear) return;

            taps++;
            Transform kid = FindPlayer();
            TurnToSpot(kid);

            if (kidAnimator == null && kid != null) kidAnimator = kid.GetComponentInChildren<KidAnimator>();
            if (kidAnimator == null && kid != null) kidAnimator = kid.GetComponentInParent<KidAnimator>();
            if (kidAnimator != null) kidAnimator.PlayAttack();

            Vector3 at = transform.position + Vector3.up * 0.1f;
            BattleEffects.Confetti(at, SandColor);
            Sfx.Play("pirates.dig", transform.position);

            if (taps >= Mathf.Max(1, tapsNeeded))
            {
                Armed = false;
                if (markRoutine != null) StopCoroutine(markRoutine);
                markRoutine = StartCoroutine(VanishRoutine());
                Dug?.Invoke();
                return;
            }

            if (markRoutine != null) StopCoroutine(markRoutine);
            markRoutine = StartCoroutine(ShrinkRoutine(taps));
        }

        /// <summary>The kid faces the X while digging, so the swing looks aimed.</summary>
        void TurnToSpot(Transform kid)
        {
            if (kid == null) return;

            Vector3 direction = transform.position - kid.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.04f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            kid.rotation = Quaternion.RotateTowards(kid.rotation, wanted, TurnToSpotDegrees);
        }

        Transform FindPlayer()
        {
            if (player != null) return player;

            var found = FindFirstObjectByType<PlayerController>();
            if (found != null) player = found.transform;
            return player;
        }

        // ----- The X -------------------------------------------------------------------------------

        void ApplyShown(bool show)
        {
            shown = show;
            if (mark != null) mark.SetActive(show);
        }

        /// <summary>A punch, then the X settles a bit smaller than before.</summary>
        System.Collections.IEnumerator ShrinkRoutine(int digsDone)
        {
            if (mark == null) yield break;

            Transform xform = mark.transform;
            float progress = Mathf.Clamp01(digsDone / (float)Mathf.Max(1, tapsNeeded));
            Vector3 target = markBaseScale * Mathf.Lerp(1f, ShrunkScale, progress);
            Vector3 from = xform.localScale;

            for (float elapsed = 0f; elapsed < PunchSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / PunchSeconds;
                float wobble = 1f + LessonMotion.DampedWobble(t, 1.5f) * 0.25f;
                xform.localScale = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t)) * wobble;
                yield return null;
            }

            xform.localScale = target;
            markRoutine = null;
        }

        System.Collections.IEnumerator VanishRoutine()
        {
            if (mark == null) yield break;

            Transform xform = mark.transform;
            Vector3 from = xform.localScale;
            for (float elapsed = 0f; elapsed < VanishSeconds; elapsed += Time.deltaTime)
            {
                xform.localScale = Vector3.Lerp(from, Vector3.zero, Mathf.SmoothStep(0f, 1f, elapsed / VanishSeconds));
                yield return null;
            }

            xform.localScale = markBaseScale;
            ApplyShown(false);
            markRoutine = null;
        }
    }
}
