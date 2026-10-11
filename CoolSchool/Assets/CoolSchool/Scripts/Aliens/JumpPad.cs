using System;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A glowing trampoline on the ground. When it is lit and the kid runs onto it, he is shot straight up in a big arc
    /// (about one and a half seconds in the air). A ring pulses on it so it is easy to spot.
    /// </summary>
    public class JumpPad : MonoBehaviour
    {
        [Tooltip("The glowing disc and the ring. Hidden until the pad is lit.")]
        [SerializeField] GameObject visual;
        [Tooltip("The pulsing ring (a child of the visual).")]
        [SerializeField] Transform ring;
        [Tooltip("How close to the middle the kid has to be (metres on the ground).")]
        [SerializeField] float radius = 1f;
        [Tooltip("Upward speed of the launch, in metres per second. About 14.5 makes a 1.4 second flight that peaks a little over 5 m.")]
        [SerializeField] float launchSpeed = 14.5f;
        [Tooltip("Rest after a launch, so landing on the pad again does not shoot the kid up straight away.")]
        [SerializeField] float cooldownSeconds = 1.5f;

        [Header("Pulse")]
        [SerializeField] float pulseAmount = 0.2f;
        [SerializeField] float pulseSpeed = 4f;

        PlayerController player;
        Vector3 ringBaseScale = Vector3.one;
        float nextLaunchTime;

        /// <summary>True while the pad is lit and ready to launch.</summary>
        public bool IsLit { get; private set; }

        /// <summary>The middle of the pad on the ground.</summary>
        public Vector3 Position => transform.position;

        public float Radius => radius;

        /// <summary>Raised each time the kid is launched.</summary>
        public event Action Launched;

        void Awake()
        {
            if (ring != null) ringBaseScale = ring.localScale;
        }

        void OnDisable()
        {
            if (ring != null) ring.localScale = ringBaseScale;
        }

        /// <summary>Lights the pad up (it glows and launches) or puts it out (hidden and still).</summary>
        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (visual != null) visual.SetActive(lit);
            if (lit) nextLaunchTime = Time.time + 0.5f;
            if (!lit && ring != null) ring.localScale = ringBaseScale;
        }

        void Update()
        {
            if (!IsLit) return;

            if (ring != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                ring.localScale = ringBaseScale * pulse;
            }

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null || Time.time < nextLaunchTime || !player.IsGrounded) return;

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) return;

            nextLaunchTime = Time.time + cooldownSeconds;
            player.Launch(Vector3.up * launchSpeed);

            Sfx.Play("aliens.jump_pad", transform.position);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            BattleEffects.Sparkle(transform.position + Vector3.up * 1.5f);
            Launched?.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
