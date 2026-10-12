using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A real trampoline on the ground. When the kid runs onto it he is shot straight up in a big arc (about one and a
    /// half seconds in the air). With alwaysBouncy it launches all the time, also in free play. The glow (a ring that
    /// pulses, a disc and a light) only shows when the trampoline is highlighted: that is the one the rescue needs.
    /// </summary>
    public class JumpPad : MonoBehaviour
    {
        [Tooltip("The glowing disc, ring and light. Hidden until the pad is highlighted (SetLit).")]
        [SerializeField] GameObject visual;
        [Tooltip("The pulsing ring (a child of the visual).")]
        [SerializeField] Transform ring;
        [Tooltip("The trampoline model. It squashes when the kid bounces. Optional: a child called Model is used when empty.")]
        [SerializeField] Transform model;
        [Tooltip("How close to the middle the kid has to be (metres on the ground): about the trampoline's rim.")]
        [SerializeField] float radius = 1.2f;
        [Tooltip("Upward speed of the launch, in metres per second. About 14.5 makes a 1.4 second flight that peaks a little over 5 m.")]
        [SerializeField] float launchSpeed = 14.5f;
        [Tooltip("Launches the kid whenever he runs onto it, also when it is not highlighted (free play).")]
        [SerializeField] bool alwaysBouncy = true;
        [Tooltip("Rest after a launch, so landing on the pad again does not shoot the kid up straight away.")]
        [SerializeField] float cooldownSeconds = 1.5f;

        [Header("Pulse")]
        [SerializeField] float pulseAmount = 0.2f;
        [SerializeField] float pulseSpeed = 4f;

        [Tooltip("While highlighted with an aim, the launch carries the kid to this far short of the target (metres, sideways).")]
        [SerializeField] float aimStandOff = 0.7f;

        const float SquashLow = 0.8f;
        const float SquashSeconds = 0.25f;
        const float MaxAimSpeed = 8f;

        // While set, no pad launches anybody: the kid floats down with the baby and must not be shot up again.
        static float blockedUntil;

        PlayerController player;
        Vector3 ringBaseScale = Vector3.one;
        Vector3 modelBaseScale = Vector3.one;
        float originalLaunchSpeed;
        float nextLaunchTime;
        Coroutine squashRoutine;
        Transform aimTarget;

        /// <summary>True while the pad is highlighted (glowing).</summary>
        public bool IsLit { get; private set; }

        /// <summary>The middle of the pad on the ground.</summary>
        public Vector3 Position => transform.position;

        public float Radius => radius;

        public float LaunchSpeed => launchSpeed;

        /// <summary>Raised each time the kid is launched.</summary>
        public event Action Launched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearBlock() => blockedUntil = 0f;

        /// <summary>No pad launches the kid for this long (seconds).</summary>
        public static void BlockFor(float seconds)
        {
            blockedUntil = Mathf.Max(blockedUntil, Time.time + seconds);
        }

        /// <summary>Pads launch again.</summary>
        public static void Unblock()
        {
            blockedUntil = 0f;
        }

        void Awake()
        {
            originalLaunchSpeed = launchSpeed;
            if (ring != null) ringBaseScale = ring.localScale;

            if (model == null) model = transform.Find("Model");
            if (model != null) modelBaseScale = model.localScale;
        }

        void OnDisable()
        {
            if (ring != null) ring.localScale = ringBaseScale;
            squashRoutine = null;
            if (model != null) model.localScale = modelBaseScale;
        }

        /// <summary>Highlights the pad (the glow shows) or puts the highlight out. The model is always there and bouncy.</summary>
        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (visual != null) visual.SetActive(lit);
            if (lit) nextLaunchTime = Mathf.Max(nextLaunchTime, Time.time + 0.5f);
            if (!lit && ring != null) ring.localScale = ringBaseScale;
            if (!lit) aimTarget = null;
        }

        /// <summary>
        /// While highlighted, each launch also pushes the kid sideways so he comes up right beside this target (the baby's
        /// seat), wherever on the trampoline he stood. Cleared when the highlight goes out.
        /// </summary>
        public void AimAt(Transform target)
        {
            aimTarget = target;
        }

        /// <summary>Changes how hard the pad launches (the rocket's trampoline must reach the nose).</summary>
        public void SetLaunchSpeed(float speed)
        {
            launchSpeed = speed;
        }

        /// <summary>The launch speed that carries the kid's feet to this height above the pad (v = sqrt(2 * g * h)).</summary>
        public static float SpeedForHeight(float heightAbovePad, float gravity)
        {
            return Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0.5f, heightAbovePad));
        }

        /// <summary>Back to the launch speed it was built with and no highlight.</summary>
        public void ResetPad()
        {
            if (originalLaunchSpeed > 0f) launchSpeed = originalLaunchSpeed;
            SetLit(false);
        }

        void Update()
        {
            if (!alwaysBouncy && !IsLit) return;

            if (IsLit && ring != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                ring.localScale = ringBaseScale * pulse;
            }

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null || Time.time < nextLaunchTime || Time.time < blockedUntil) return;
            if (!player.IsGrounded || !player.InputEnabled || CoolSchool.Parent.ParentGate.InputBlocked) return;

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) return;

            nextLaunchTime = Time.time + cooldownSeconds;
            player.Launch(Vector3.up * launchSpeed + AimVelocity());

            Sfx.Play("aliens.jump_pad", transform.position);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            BattleEffects.Sparkle(transform.position + Vector3.up * 1.5f);
            Squash();
            Launched?.Invoke();
        }

        /// <summary>
        /// The sideways push that brings the kid to aimStandOff short of the aim target by the top of the jump. The player's
        /// push fades at CarryDecay, so a short way needs only a small push that dies out before the top.
        /// </summary>
        Vector3 AimVelocity()
        {
            if (!IsLit || aimTarget == null) return Vector3.zero;

            Vector3 toTarget = aimTarget.position - player.transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude - aimStandOff;
            if (distance <= 0.05f) return Vector3.zero;

            float decay = PlayerController.CarryDecay;
            float timeToTop = launchSpeed / Mathf.Max(0.1f, Mathf.Abs(player.Gravity));
            float speed = distance <= 0.5f * decay * timeToTop * timeToTop
                ? Mathf.Sqrt(2f * decay * distance)
                : distance / timeToTop + 0.5f * decay * timeToTop;

            return toTarget.normalized * Mathf.Min(speed, MaxAimSpeed);
        }

        /// <summary>The model squashes flat and springs back (height 0.8 to 1.0 in a quarter of a second).</summary>
        void Squash()
        {
            if (model == null) return;

            if (squashRoutine != null) StopCoroutine(squashRoutine);
            squashRoutine = StartCoroutine(SquashRoutine());
        }

        IEnumerator SquashRoutine()
        {
            for (float elapsed = 0f; elapsed < SquashSeconds; elapsed += Time.deltaTime)
            {
                float height = Mathf.Lerp(SquashLow, 1f, elapsed / SquashSeconds);
                Vector3 scale = modelBaseScale;
                scale.y *= height;
                model.localScale = scale;
                yield return null;
            }

            model.localScale = modelBaseScale;
            squashRoutine = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
