using System.Collections;
using CoolSchool.Characters;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A goblin or troll classmate in the knights game. Slow and very clear about what it will do:
    /// it walks up, stands still and shows a "!" while winding up, swings, then steps back.
    /// When the knights bump it enough it tumbles, giggles and runs off to hide until the next wave.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable), typeof(SwordFighter))]
    public class BattleOpponentAI : MonoBehaviour
    {
        [Tooltip("Slower than the player, who runs at about 4.")]
        [SerializeField] float moveSpeed = 1.6f;
        [Tooltip("How long it stands still showing the '!' before swinging.")]
        [SerializeField] float windUpSeconds = 0.9f;
        [Tooltip("Rest after a swing, so the player has plenty of time to answer.")]
        [SerializeField] float recoverSeconds = 2.5f;
        [Tooltip("Chance of picking the player rather than a knight classmate as the target.")]
        [SerializeField, Range(0f, 1f)] float preferPlayerChance = 0.6f;
        [Tooltip("A '!' object shown during the wind-up. Optional.")]
        [SerializeField] GameObject telegraph;

        const float AttackDistance = 1.4f;
        const float HitPauseSeconds = 0.8f;
        const float GiveUpApproachSeconds = 10f;
        const float DefeatedRunSpeed = 3f;

        NavMeshAgent agent;
        Hittable hittable;
        SwordFighter fighter;
        KidAnimator kidAnimator;
        Transform visualRoot;
        Vector3 visualBaseScale = Vector3.one;
        PlayerController player;
        Transform retreatPoint;
        Coroutine routine;
        float hitPauseUntil;

        /// <summary>True when this one is defeated or switched off, so it can be used again.</summary>
        public bool IsOut => !gameObject.activeInHierarchy || (hittable != null && hittable.IsDefeated);

        void Awake() => CacheReferences();

        /// <summary>Opponents start switched off, so Activate may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            fighter = GetComponent<SwordFighter>();
            kidAnimator = GetComponent<KidAnimator>();

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visualRoot = animator.transform;
                visualBaseScale = visualRoot.localScale;
            }
        }

        void OnEnable()
        {
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
            SetTelegraph(false);
            RestoreScale();
        }

        /// <summary>Appears at the spawn point and starts looking for a knight to challenge.</summary>
        public void Activate(Vector3 spawnPosition, Transform retreat)
        {
            CacheReferences();
            retreatPoint = retreat;

            // Place it on the floor before switching on, so the agent starts on the NavMesh.
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                spawnPosition = hit.position;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            if (AgentReady) agent.Warp(spawnPosition);
            else transform.position = spawnPosition;
            gameObject.SetActive(true);

            // The knights game could not show our weapon while we were switched off, so do it now.
            WeaponHolder weapons = GetComponent<WeaponHolder>();
            if (weapons != null) weapons.ShowWeapons(true);

            hittable.ResetState();
            hitPauseUntil = 0f;
            SetTelegraph(false);
            RestoreScale();

            if (agent.enabled && !agent.isOnNavMesh) agent.Warp(spawnPosition);
            if (AgentReady)
            {
                agent.speed = moveSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }

            routine = StartCoroutine(FightRoutine());
        }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        IEnumerator FightRoutine()
        {
            // A moment to look around before the first challenge.
            yield return new WaitForSeconds(0.5f);

            while (true)
            {
                Hittable target = PickTarget();
                if (target == null)
                {
                    StopMoving();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                yield return Approach(target);

                if (!target.IsAlive || Hittable.FlatDistance(transform.position, target.transform.position) > AttackDistance + 1f)
                {
                    yield return null;
                    continue;
                }

                yield return WindUpAndSwing(target);
                yield return Recover(target);
            }
        }

        IEnumerator Approach(Hittable target)
        {
            float giveUpTime = Time.time + GiveUpApproachSeconds;
            while (target.IsAlive && Time.time < giveUpTime)
            {
                if (Hittable.FlatDistance(transform.position, target.transform.position) <= AttackDistance) break;

                if (Time.time < hitPauseUntil)
                {
                    StopMoving();
                }
                else if (AgentReady)
                {
                    agent.updateRotation = true;
                    agent.speed = moveSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(target.transform.position);
                }
                yield return null;
            }
            StopMoving();
        }

        IEnumerator WindUpAndSwing(Hittable target)
        {
            StopMoving();
            if (AgentReady) agent.updateRotation = false;
            SetTelegraph(true);

            bool interrupted = false;
            float elapsed = 0f;
            while (elapsed < windUpSeconds)
            {
                if (Time.time < hitPauseUntil || !target.IsAlive)
                {
                    interrupted = true;
                    break;
                }

                FaceTarget(target);
                float pulse = Mathf.Sin(elapsed / windUpSeconds * Mathf.PI);
                SetCrouch(pulse);

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetTelegraph(false);
            RestoreScale();

            if (!interrupted)
            {
                FaceTarget(target);
                fighter.Attack();
                // Let the swing play out before stepping back.
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator Recover(Hittable target)
        {
            float elapsed = 0f;
            bool steppedBack = false;
            while (elapsed < recoverSeconds)
            {
                // Step back a little (facing the target), then stand and rest.
                if (!steppedBack && elapsed >= 0.2f && Time.time >= hitPauseUntil && AgentReady)
                {
                    steppedBack = true;
                    Vector3 away = -Hittable.FlatDirection(transform.position, target.transform.position);
                    Vector3 spot = transform.position + away * 1f;
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    {
                        agent.updateRotation = false;
                        agent.speed = moveSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(hit.position);
                    }
                }

                if (target.IsAlive) FaceTarget(target);
                elapsed += Time.deltaTime;
                yield return null;
            }

            StopMoving();
            if (AgentReady) agent.updateRotation = true;
        }

        IEnumerator DefeatedRoutine()
        {
            StopMoving();
            SetTelegraph(false);
            RestoreScale();

            if (kidAnimator != null) kidAnimator.PlayTumble();
            yield return new WaitForSeconds(1.5f);
            if (kidAnimator != null) kidAnimator.PlayLaugh();
            yield return new WaitForSeconds(0.8f);

            // Run off and hide behind the castle.
            if (retreatPoint != null && AgentReady)
            {
                agent.updateRotation = true;
                agent.speed = DefeatedRunSpeed;
                agent.isStopped = false;
                agent.SetDestination(retreatPoint.position);

                float timeout = Time.time + 8f;
                while (Time.time < timeout && (agent.pathPending || agent.remainingDistance > 1f))
                    yield return null;
            }

            routine = null;
            gameObject.SetActive(false);
        }

        void OnHit(Hittable who)
        {
            if (who.IsDefeated) return;

            hitPauseUntil = Time.time + HitPauseSeconds;
            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        void OnDefeated(Hittable who)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(DefeatedRoutine());
        }

        Hittable PickTarget()
        {
            Hittable playerHittable = FindPlayerHittable();
            if (playerHittable != null && playerHittable.IsAlive && Random.value < preferPlayerChance)
                return playerHittable;

            Hittable nearest = null;
            float best = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (other.Team != BattleTeam.Heroes || !other.IsAlive) continue;

                float distance = Hittable.FlatDistance(transform.position, other.transform.position);
                if (distance < best)
                {
                    best = distance;
                    nearest = other;
                }
            }
            return nearest;
        }

        Hittable FindPlayerHittable()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            return player != null ? player.GetComponent<Hittable>() : null;
        }

        void FaceTarget(Hittable target)
        {
            Vector3 direction = Hittable.FlatDirection(transform.position, target.transform.position);
            if (direction == Vector3.zero) return;

            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 360f * Time.deltaTime);
        }

        void StopMoving()
        {
            if (!AgentReady) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        void SetTelegraph(bool on)
        {
            if (telegraph != null && telegraph.activeSelf != on) telegraph.SetActive(on);
        }

        /// <summary>Squashes the model a little, as if getting ready to swing. 0 = normal, 1 = most crouched.</summary>
        void SetCrouch(float amount)
        {
            if (visualRoot == null) return;

            visualRoot.localScale = new Vector3(
                visualBaseScale.x * (1f + 0.06f * amount),
                visualBaseScale.y * (1f - 0.12f * amount),
                visualBaseScale.z * (1f + 0.06f * amount));
        }

        void RestoreScale()
        {
            if (visualRoot != null) visualRoot.localScale = visualBaseScale;
        }
    }
}
