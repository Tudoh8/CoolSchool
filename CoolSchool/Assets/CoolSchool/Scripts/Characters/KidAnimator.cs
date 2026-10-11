using System.Collections.Generic;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Drives the kid's Animator from how fast the kid actually moves, so it works
    /// the same for the player (CharacterController) and computer kids (NavMeshAgent).
    /// Expects the Kid animator controller with a "Speed" float and "Jump"/"Cheer" triggers.
    /// Fighting moves (Attack, Hit, Tumble, Laugh, Blocking) are optional: if the controller
    /// has no such parameter, the call is quietly ignored.
    /// </summary>
    public class KidAnimator : MonoBehaviour
    {
        const string UpperBodyLayerName = "UpperBody";

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int CheerId = Animator.StringToHash("Cheer");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int TumbleId = Animator.StringToHash("Tumble");
        static readonly int LaughId = Animator.StringToHash("Laugh");
        static readonly int BlockingId = Animator.StringToHash("Blocking");
        static readonly int AttackVariantId = Animator.StringToHash("AttackVariant");
        static readonly int BlockHitId = Animator.StringToHash("BlockHit");

        [SerializeField] Animator animator;
        [SerializeField] float speedDamping = 0.1f;

        [Header("Upper body (sword arm) layer")]
        [Tooltip("How long the upper body layer stays active after an attack.")]
        [SerializeField] float attackLayerSeconds = 0.7f;
        [Tooltip("Seconds the layer takes to fade fully in or out.")]
        [SerializeField] float layerFadeSeconds = 0.12f;

        readonly HashSet<int> parameterIds = new HashSet<int>();
        RuntimeAnimatorController cachedController;
        int upperBodyLayer = -1;
        float upperBodyWeight;
        float attackLayerUntil = -1f;
        bool blocking;
        Vector3 lastPosition;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            lastPosition = transform.position;

            var player = GetComponent<PlayerController>();
            if (player != null) player.Jumped += PlayJump;
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;

            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);

            RefreshCache();
            UpdateUpperBodyLayer();
        }

        public void PlayJump() => SetTrigger(JumpId);

        public void PlayCheer() => SetTrigger(CheerId);

        /// <summary>Swing the sword. Also lifts the upper body layer for a moment.</summary>
        public void PlayAttack()
        {
            attackLayerUntil = Time.time + attackLayerSeconds;
            if (HasParameter(AttackVariantId)) animator.SetInteger(AttackVariantId, Random.Range(0, 3));
            SetTrigger(AttackId);
        }

        /// <summary>The shield takes a bump while blocking.</summary>
        public void PlayBlockHit() => SetTrigger(BlockHitId);

        /// <summary>Hold the shield up (true) or lower it (false).</summary>
        public void SetBlocking(bool value)
        {
            blocking = value;
            if (!HasParameter(BlockingId)) return;
            animator.SetBool(BlockingId, value);
        }

        /// <summary>A little wobble when something bumps into the kid.</summary>
        public void PlayHit() => SetTrigger(HitId);

        /// <summary>Tumble over, giggle on the ground and stand up again.</summary>
        public void PlayTumble() => SetTrigger(TumbleId);

        public void PlayLaugh() => SetTrigger(LaughId);

        void SetTrigger(int id)
        {
            if (!HasParameter(id)) return;
            animator.SetTrigger(id);
        }

        void UpdateUpperBodyLayer()
        {
            if (upperBodyLayer < 0) return;

            float target = blocking || Time.time < attackLayerUntil ? 1f : 0f;
            float step = layerFadeSeconds > 0f ? Time.deltaTime / layerFadeSeconds : 1f;
            upperBodyWeight = Mathf.MoveTowards(upperBodyWeight, target, step);
            animator.SetLayerWeight(upperBodyLayer, upperBodyWeight);
        }

        /// <summary>True when the animator can take this parameter right now. Never throws or warns.</summary>
        bool HasParameter(int id)
        {
            if (animator == null || !animator.isActiveAndEnabled) return false;
            if (animator.runtimeAnimatorController == null) return false;

            RefreshCache();
            return parameterIds.Contains(id);
        }

        void RefreshCache()
        {
            if (animator.runtimeAnimatorController == cachedController) return;

            cachedController = animator.runtimeAnimatorController;
            parameterIds.Clear();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                parameterIds.Add(parameter.nameHash);

            upperBodyLayer = animator.GetLayerIndex(UpperBodyLayerName);
            upperBodyWeight = 0f;
            if (upperBodyLayer >= 0) animator.SetLayerWeight(upperBodyLayer, 0f);
        }
    }
}
