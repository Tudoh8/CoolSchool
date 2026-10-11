using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The friendly dragon. It walks into the arena, shows a clear warning, then breathes
    /// confetti fire or whips its tail. After a few bumps from the knights it does a happy
    /// dance and flies away. It moves by transform only (no NavMesh) and stays on the ground.
    /// </summary>
    [RequireComponent(typeof(Hittable))]
    public class DragonBoss : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int BreatheFireId = Animator.StringToHash("BreatheFire");
        static readonly int TailWhipId = Animator.StringToHash("TailWhip");
        static readonly int GotHitId = Animator.StringToHash("GotHit");
        static readonly int HappyId = Animator.StringToHash("Happy");
        static readonly int FlyingId = Animator.StringToHash("Flying");

        [SerializeField] Animator animator;
        [Tooltip("Where the confetti fire comes out (the mouth). Falls back to the dragon itself.")]
        [SerializeField] Transform fireOrigin;
        [SerializeField] ParticleSystem fireConfetti;
        [Tooltip("A glowing sign that shows the dragon is about to breathe. Optional.")]
        [SerializeField] GameObject fireWarning;

        [Header("Moving")]
        [SerializeField] float walkSpeed = 1.5f;
        [Tooltip("Degrees per second when turning toward the player. Slow, so the player can run around.")]
        [SerializeField] float turnSpeed = 70f;

        [Header("Attacks")]
        [SerializeField] float fireRange = 5f;
        [Tooltip("The fire reaches this many degrees to each side of where the dragon looks.")]
        [SerializeField] float fireAngle = 50f;
        [SerializeField] float telegraphSeconds = 1.2f;
        [SerializeField] float fireSeconds = 1.5f;
        [SerializeField] float attackInterval = 3.5f;

        const float TailWhipRange = 3f;
        const float GotHitCooldown = 0.6f;
        const float HappySeconds = 2f;
        const float FlySeconds = 4f;
        const float FlyUpSpeed = 3.5f;
        const float FlyAwaySpeed = 4f;

        Hittable hittable;
        PlayerController player;
        Coroutine routine;
        Vector3 arenaCenter;
        Vector3 awayDirection = Vector3.back;
        float groundY;
        float nextGotHitTime;
        bool defeated;
        readonly HashSet<int> animatorParameters = new HashSet<int>();
        RuntimeAnimatorController cachedController;
        readonly List<Hittable> buffer = new List<Hittable>();

        /// <summary>Raised when the dragon has walked to the middle of the arena and starts to fight.</summary>
        public event Action Arrived;

        /// <summary>Raised when the fire warning appears, just before the dragon breathes.</summary>
        public event Action FireTelegraphStarted;

        /// <summary>Raised when the dragon has flown out of sight.</summary>
        public event Action FlewAway;

        void Awake() => CacheReferences();

        void CacheReferences()
        {
            if (hittable == null) hittable = GetComponent<Hittable>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable()
        {
            CacheReferences();
            hittable.Hit += OnHit;
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Defeated -= OnDefeated;
            }

            routine = null;
            StopFireEffects();
        }

        /// <summary>Appears at "from", walks to the middle of the arena and starts fighting.</summary>
        public void Enter(Vector3 from, Vector3 arenaCenterPosition)
        {
            CacheReferences();
            StopAllCoroutines();
            StopFireEffects();

            arenaCenter = arenaCenterPosition;
            groundY = arenaCenterPosition.y;
            defeated = false;
            nextGotHitTime = 0f;

            from.y = groundY;
            Vector3 toCenter = Hittable.FlatDirection(from, arenaCenterPosition);
            awayDirection = toCenter == Vector3.zero ? Vector3.back : -toCenter;

            transform.position = from;
            if (toCenter != Vector3.zero) transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);

            gameObject.SetActive(true);
            Sfx.Play("dragon.roar", from);
            hittable.ResetState();
            ResetAnimator();

            routine = StartCoroutine(EnterRoutine());
        }

        /// <summary>Puts everything back and switches the dragon off, ready for another game.</summary>
        public void ResetDragon()
        {
            CacheReferences();
            StopAllCoroutines();
            routine = null;
            StopFireEffects();
            defeated = false;

            hittable.ResetState();
            if (gameObject.activeSelf) ResetAnimator();
            gameObject.SetActive(false);
        }

        IEnumerator EnterRoutine()
        {
            // Walk in, keeping both feet on the ground.
            SetFloat(SpeedId, walkSpeed);
            while (Hittable.FlatDistance(transform.position, arenaCenter) > 0.3f)
            {
                Vector3 direction = Hittable.FlatDirection(transform.position, arenaCenter);
                Vector3 position = transform.position + direction * (walkSpeed * Time.deltaTime);
                position.y = groundY;
                transform.position = position;
                RotateToward(direction, turnSpeed * 2f);
                yield return null;
            }
            SetFloat(SpeedId, 0f);

            Arrived?.Invoke();
            routine = StartCoroutine(FightRoutine());
        }

        IEnumerator FightRoutine()
        {
            yield return TurnTowardPlayer(1f);

            while (!defeated)
            {
                yield return TurnTowardPlayer(attackInterval);

                if (ShouldTailWhip()) yield return TailWhip();
                else yield return BreatheFire();
            }
        }

        IEnumerator TurnTowardPlayer(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                Transform target = PlayerTransform();
                if (target != null)
                    RotateToward(Hittable.FlatDirection(transform.position, target.position), turnSpeed);
                yield return null;
            }
        }

        bool ShouldTailWhip()
        {
            Transform target = PlayerTransform();
            if (target == null) return false;

            Vector3 flatForward = FlatForward();
            Vector3 toPlayer = Hittable.FlatDirection(transform.position, target.position);
            float distance = Hittable.FlatDistance(transform.position, target.position);

            // Behind or beside the dragon, and close enough for the tail to reach.
            return toPlayer != Vector3.zero && Vector3.Dot(flatForward, toPlayer) < 0.3f && distance <= TailWhipRange + 0.5f;
        }

        IEnumerator TailWhip()
        {
            SetTrigger(TailWhipId);
            yield return new WaitForSeconds(0.6f);

            Vector3 flatForward = FlatForward();
            CopyHittables();
            foreach (Hittable hero in buffer)
            {
                if (hero == null || hero.Team != BattleTeam.Heroes || !hero.IsAlive) continue;

                Vector3 toHero = Hittable.FlatDirection(transform.position, hero.transform.position);
                float distance = Hittable.FlatDistance(transform.position, hero.transform.position);
                bool behind = toHero != Vector3.zero && Vector3.Dot(flatForward, toHero) < 0.3f;
                if (behind && distance <= TailWhipRange + hero.HitRadius) hero.TakeHit(transform.position);
            }

            yield return new WaitForSeconds(0.9f);
        }

        IEnumerator BreatheFire()
        {
            // Show the warning first, so there is plenty of time to run or lift the shield.
            if (fireWarning != null) fireWarning.SetActive(true);
            FireTelegraphStarted?.Invoke();
            yield return TurnTowardPlayer(telegraphSeconds);
            if (fireWarning != null) fireWarning.SetActive(false);

            SetTrigger(BreatheFireId);
            if (fireConfetti != null) fireConfetti.Play();
            Sfx.Play("dragon.fire", fireOrigin != null ? fireOrigin.position : transform.position);

            var alreadyHit = new HashSet<Hittable>();
            float end = Time.time + fireSeconds;
            while (Time.time < end)
            {
                BurnHeroesInCone(alreadyHit);
                yield return null;
            }

            if (fireConfetti != null) fireConfetti.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>Everyone in the cone of confetti fire gets one bump per breath. Shields stop it.</summary>
        void BurnHeroesInCone(HashSet<Hittable> alreadyHit)
        {
            Vector3 origin = fireOrigin != null ? fireOrigin.position : transform.position;
            Vector3 flatForward = FlatForward();

            CopyHittables();
            foreach (Hittable hero in buffer)
            {
                if (hero == null || hero.Team != BattleTeam.Heroes || !hero.IsAlive || alreadyHit.Contains(hero)) continue;

                float distance = Hittable.FlatDistance(origin, hero.transform.position);
                if (distance > fireRange + hero.HitRadius) continue;

                Vector3 direction = Hittable.FlatDirection(origin, hero.transform.position);
                bool inCone = direction == Vector3.zero || Vector3.Angle(flatForward, direction) <= fireAngle;
                if (!inCone) continue;

                alreadyHit.Add(hero);
                hero.TakeHit(transform.position);
            }
        }

        void OnHit(Hittable who)
        {
            if (defeated || who.IsDefeated || Time.time < nextGotHitTime) return;

            nextGotHitTime = Time.time + GotHitCooldown;
            SetTrigger(GotHitId);
        }

        void OnDefeated(Hittable who)
        {
            if (defeated) return;

            defeated = true;
            StopAllCoroutines();
            StopFireEffects();
            routine = StartCoroutine(DefeatedRoutine());
        }

        IEnumerator DefeatedRoutine()
        {
            SetFloat(SpeedId, 0f);
            SetTrigger(HappyId);
            Sfx.Play("dragon.happy", transform.position);
            BattleEffects.Confetti(hittable.AimPosition);
            BattleEffects.Confetti(hittable.AimPosition + Vector3.up * 1.5f);

            float end = Time.time + HappySeconds;
            while (Time.time < end)
            {
                // More confetti pops while it is happy.
                if (UnityEngine.Random.value < 0.08f)
                {
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.5f;
                    offset.y = Mathf.Abs(offset.y) + 1f;
                    BattleEffects.Confetti(transform.position + offset);
                }
                yield return null;
            }

            // Take off: up and away from the arena.
            SetBool(FlyingId, true);
            Sfx.Play("dragon.wing_flap", transform.position);
            Vector3 flyDirection = (awayDirection * FlyAwaySpeed + Vector3.up * FlyUpSpeed);
            float flyEnd = Time.time + FlySeconds;
            while (Time.time < flyEnd)
            {
                transform.position += flyDirection * Time.deltaTime;
                RotateToward(awayDirection, turnSpeed * 2f);
                yield return null;
            }

            routine = null;
            gameObject.SetActive(false);
            FlewAway?.Invoke();
        }

        void StopFireEffects()
        {
            if (fireWarning != null) fireWarning.SetActive(false);
            if (fireConfetti != null) fireConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Transform PlayerTransform()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            return player != null ? player.transform : null;
        }

        Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        void RotateToward(Vector3 flatDirection, float degreesPerSecond)
        {
            if (flatDirection == Vector3.zero) return;

            Quaternion wanted = Quaternion.LookRotation(flatDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, degreesPerSecond * Time.deltaTime);
        }

        void CopyHittables()
        {
            // Copy first: a bump can switch things on or off while we are looping.
            buffer.Clear();
            buffer.AddRange(Hittable.All);
        }

        // Animator helpers: a missing parameter or controller is simply ignored.

        void ResetAnimator()
        {
            if (!CanUseAnimator()) return;

            ResetTrigger(BreatheFireId);
            ResetTrigger(TailWhipId);
            ResetTrigger(GotHitId);
            ResetTrigger(HappyId);
            SetBool(FlyingId, false);
            SetFloat(SpeedId, 0f);
        }

        void SetTrigger(int id)
        {
            if (HasParameter(id)) animator.SetTrigger(id);
        }

        void ResetTrigger(int id)
        {
            if (HasParameter(id)) animator.ResetTrigger(id);
        }

        void SetBool(int id, bool value)
        {
            if (HasParameter(id)) animator.SetBool(id, value);
        }

        void SetFloat(int id, float value)
        {
            if (HasParameter(id)) animator.SetFloat(id, value);
        }

        bool CanUseAnimator()
        {
            return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
        }

        bool HasParameter(int id)
        {
            if (!CanUseAnimator()) return false;

            if (animator.runtimeAnimatorController != cachedController)
            {
                cachedController = animator.runtimeAnimatorController;
                animatorParameters.Clear();
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                    animatorParameters.Add(parameter.nameHash);
            }
            return animatorParameters.Contains(id);
        }
    }
}
