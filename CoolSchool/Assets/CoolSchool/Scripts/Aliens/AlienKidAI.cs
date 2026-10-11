using System;
using System.Collections;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A silly alien kid. First it walks out of the rocket toward the player and keeps its distance, throwing slime
    /// when the alien landing asks it to. One zap turns it into a friend: it cheers, runs to its spot in the party
    /// line and dances there. Friends never throw again.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable))]
    public class AlienKidAI : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 1.4f;
        [SerializeField] float friendSpeed = 2.4f;
        [Tooltip("The alien stops walking this far from the player and waits there.")]
        [SerializeField] float keepDistance = 5f;
        [Tooltip("The alien stands still for this long when it throws.")]
        [SerializeField] float throwStandSeconds = 1.6f;
        [SerializeField] Color friendConfettiColor = new Color(0.35f, 0.9f, 0.25f, 1f);

        const float RepathSeconds = 0.4f;
        const float ArriveDistance = 0.25f;
        const float GiveUpWalkSeconds = 10f;
        const float HopSeconds = 0.9f;
        const float HopHeight = 0.4f;

        NavMeshAgent agent;
        Hittable hittable;
        KidAnimator kidAnimator;
        SlimeThrower thrower;
        Transform visual;
        Vector3 visualBasePosition;
        Transform player;
        Coroutine throwRoutine;
        Coroutine friendRoutine;
        Coroutine hopRoutine;
        float nextRepathTime;

        Vector3 partySpot;
        Vector3 partyLookAt;
        int danceVariant;
        bool hasPartySpot;
        bool dancing;

        public Hittable Hittable
        {
            get
            {
                CacheReferences();
                return hittable;
            }
        }

        public bool IsFriend { get; private set; }

        /// <summary>True while it is winding up or lobbing slime.</summary>
        public bool IsThrowing => throwRoutine != null;

        /// <summary>True once it is standing at its spot in the party line, dancing.</summary>
        public bool IsDancing => dancing;

        /// <summary>Raised when a zap (or the safety net) has turned this alien into a friend.</summary>
        public event Action<AlienKidAI> BecameFriend;

        void Awake() => CacheReferences();

        /// <summary>Aliens start switched off, so Activate may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            kidAnimator = GetComponent<KidAnimator>();
            thrower = GetComponent<SlimeThrower>();

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visual = animator.transform;
                visualBasePosition = visual.localPosition;
            }
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

            throwRoutine = null;
            friendRoutine = null;
            hopRoutine = null;
            RestoreVisual();
        }

        /// <summary>
        /// Appears at the spawn point (at the foot of the rocket) and starts walking toward the player.
        /// The party spot is where it will dance if it turns into a friend.
        /// </summary>
        public void Activate(Vector3 spawnPosition, Transform playerTransform, Vector3 spot, Vector3 lookAt, int dance)
        {
            CacheReferences();
            ResetAlien();

            player = playerTransform;
            partySpot = spot;
            partyLookAt = lookAt;
            danceVariant = dance;
            hasPartySpot = true;

            // Place it on the floor before switching on, so the agent starts on the NavMesh.
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                spawnPosition = hit.position;
            transform.position = spawnPosition;
            gameObject.SetActive(true);

            if (agent.enabled && !agent.isOnNavMesh) agent.Warp(spawnPosition);
            if (AgentReady)
            {
                agent.speed = walkSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }
        }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        void Update()
        {
            if (!AgentReady || IsFriend || IsThrowing || player == null) return;

            WalkTowardPlayer();
        }

        /// <summary>Walks until it is keepDistance from the player, then stands and watches him.</summary>
        void WalkTowardPlayer()
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (distance <= keepDistance + 0.5f)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
                FaceDirection(toPlayer, 360f);
                return;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = walkSpeed;
            if (Time.time < nextRepathTime) return;

            nextRepathTime = Time.time + RepathSeconds;
            Vector3 stopPoint = player.position - toPlayer / distance * keepDistance;
            if (NavMesh.SamplePosition(stopPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ----- Slime ---------------------------------------------------------------------------

        /// <summary>Stands still, swings its arm and lobs a slime ball at the target.</summary>
        public void ThrowAt(Transform target)
        {
            if (IsFriend || IsThrowing || target == null || !isActiveAndEnabled) return;

            throwRoutine = StartCoroutine(ThrowRoutine(target));
        }

        IEnumerator ThrowRoutine(Transform target)
        {
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
            }

            FaceDirection(target.position - transform.position, 720f);
            if (kidAnimator != null) kidAnimator.PlayThrow();
            if (thrower != null) thrower.Throw(target);

            for (float elapsed = 0f; elapsed < throwStandSeconds; elapsed += Time.deltaTime)
            {
                FaceDirection(target.position - transform.position, 360f);
                yield return null;
            }

            throwRoutine = null;
        }

        // ----- Friends -------------------------------------------------------------------------

        void OnHit(Hittable who)
        {
            if (who.IsDefeated) return;

            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        void OnDefeated(Hittable who)
        {
            BecomeFriend();
        }

        /// <summary>
        /// The alien turns friendly: green confetti, a happy shout, and off to the party line. Also used as the safety net
        /// that makes a stubborn alien a friend after a long time.
        /// </summary>
        public void BecomeFriend()
        {
            if (IsFriend) return;

            IsFriend = true;
            CancelThrow();

            Vector3 above = transform.position + Vector3.up * 1f;
            BattleEffects.Confetti(above, friendConfettiColor);
            BattleEffects.Sparkle(above);
            VoicePlayer.Say("aliens.zapped");
            if (kidAnimator != null) kidAnimator.PlayCheer();

            BecameFriend?.Invoke(this);
            friendRoutine = StartCoroutine(FriendRoutine());
        }

        IEnumerator FriendRoutine()
        {
            // A moment of cheering where it stands.
            yield return new WaitForSeconds(0.9f);

            if (hasPartySpot && AgentReady)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = friendSpeed;
                agent.SetDestination(partySpot);

                float giveUp = Time.time + GiveUpWalkSeconds;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > ArriveDistance))
                    yield return null;

                agent.isStopped = true;
                agent.updateRotation = false;
            }

            // Face the camera side, then dance.
            if (hasPartySpot)
            {
                Vector3 facing = partyLookAt - transform.position;
                for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
                {
                    FaceDirection(facing, 540f);
                    yield return null;
                }
            }

            StartDancing();
            friendRoutine = null;
        }

        void StartDancing()
        {
            dancing = true;
            if (kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
        }

        /// <summary>A little hop in the line, used while the aliens are counted out loud.</summary>
        public void Hop()
        {
            if (!isActiveAndEnabled) return;

            if (hopRoutine != null) StopCoroutine(hopRoutine);
            hopRoutine = StartCoroutine(HopRoutine());
        }

        IEnumerator HopRoutine()
        {
            if (kidAnimator != null)
            {
                kidAnimator.SetDancing(false);
                kidAnimator.PlayJump();
            }
            BattleEffects.Sparkle(transform.position + Vector3.up * 1.2f);

            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                if (visual != null)
                    visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(elapsed / HopSeconds * Mathf.PI) * HopHeight);
                yield return null;
            }

            RestoreVisual();
            hopRoutine = null;
            if (dancing && kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
        }

        /// <summary>Stops the dance and cheers goodbye.</summary>
        public void Wave()
        {
            dancing = false;
            if (kidAnimator == null) return;

            kidAnimator.SetDancing(false);
            kidAnimator.PlayCheer();
        }

        /// <summary>Disappears with a puff of sparkles.</summary>
        public void Vanish()
        {
            BattleEffects.Sparkle(transform.position + Vector3.up * 1f);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.4f);
            ResetAlien();
        }

        /// <summary>Back to the way it was before the game: switched off, not a friend, no dance, no slime.</summary>
        public void ResetAlien()
        {
            CacheReferences();

            if (throwRoutine != null) StopCoroutine(throwRoutine);
            if (friendRoutine != null) StopCoroutine(friendRoutine);
            if (hopRoutine != null) StopCoroutine(hopRoutine);
            throwRoutine = null;
            friendRoutine = null;
            hopRoutine = null;
            CancelThrow();
            RestoreVisual();

            if (kidAnimator != null && dancing) kidAnimator.SetDancing(false);
            dancing = false;
            IsFriend = false;
            hasPartySpot = false;
            hittable.ResetState();

            gameObject.SetActive(false);
        }

        // ----- Helpers -------------------------------------------------------------------------

        void CancelThrow()
        {
            if (throwRoutine != null) StopCoroutine(throwRoutine);
            throwRoutine = null;
            if (thrower != null) thrower.CancelThrow();
        }

        void RestoreVisual()
        {
            if (visual != null) visual.localPosition = visualBasePosition;
        }

        void FaceDirection(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, degreesPerSecond * Time.deltaTime);
        }
    }
}
