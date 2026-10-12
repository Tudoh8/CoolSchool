using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Characters;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A goblin or troll classmate in the knights game. It climbs a ladder up the outside of the castle wall and
    /// jumps down into the courtyard (LadderClimb). Then it is slow and very clear about what it will do:
    /// it walks up, stands still and shows a "!" while winding up, swings, then steps back.
    /// When the knights bump it enough it tumbles, giggles and runs back to its ladder's landing, where it
    /// vanishes in a sparkle until the next wave.
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
        const float GoHomeTimeoutSeconds = 8f;
        const float GoHomeArriveDistance = 0.6f;

        NavMeshAgent agent;
        Hittable hittable;
        SwordFighter fighter;
        KidAnimator kidAnimator;
        LadderClimb ladderClimb;
        Transform visualRoot;
        Vector3 visualBaseScale = Vector3.one;
        PlayerController player;
        WallLadder ladder;
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
            ladderClimb = GetComponent<LadderClimb>();

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

        /// <summary>
        /// Starts at the foot of the ladder outside the wall, climbs over and jumps down into the courtyard, then starts
        /// looking for a knight to challenge. Without a ladder (or a LadderClimb) it just appears at the fallback position.
        /// </summary>
        public void Activate(WallLadder wallLadder, Vector3 fallbackPosition)
        {
            CacheReferences();
            ladder = wallLadder;
            bool climbs = ladder != null && ladderClimb != null;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            hittable.ResetState();
            hitPauseUntil = 0f;
            SetTelegraph(false);
            RestoreScale();

            if (climbs)
            {
                // The climber switches the agent off and stands at the ladder's foot, so the agent never wakes up off the NavMesh.
                ladderClimb.Begin(ladder);
                gameObject.SetActive(true);
            }
            else
            {
                // Place it on the floor before switching on, so the agent starts on the NavMesh.
                if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    fallbackPosition = hit.position;

                if (AgentReady) agent.Warp(fallbackPosition);
                else transform.position = fallbackPosition;
                gameObject.SetActive(true);
                if (agent.enabled && !agent.isOnNavMesh) agent.Warp(fallbackPosition);
            }

            // The knights game could not show our weapon while we were switched off, so do it now.
            WeaponHolder weapons = GetComponent<WeaponHolder>();
            if (weapons != null) weapons.ShowWeapons(true);

            if (!climbs) PrepareAgent();
            routine = StartCoroutine(climbs ? ClimbThenFight() : FightRoutine());
        }

        /// <summary>Sends a goblin that is still on the field home (a round that dragged on): it runs to its ladder and vanishes in a sparkle.</summary>
        public void SendHome()
        {
            if (!gameObject.activeInHierarchy || hittable.IsDefeated) return;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            // Still on the ladder or in the air: just vanish where it is.
            if (ladderClimb != null && ladderClimb.IsClimbing)
            {
                BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);
                gameObject.SetActive(false);
                return;
            }

            routine = StartCoroutine(GoHomeThenVanish());
        }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        void PrepareAgent()
        {
            if (!AgentReady) return;

            agent.speed = moveSpeed;
            agent.isStopped = false;
            agent.updateRotation = true;
        }

        IEnumerator ClimbThenFight()
        {
            yield return ladderClimb.Run(ladder);

            PrepareAgent();
            yield return FightRoutine();
        }

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
            Sfx.Play("battle.giggle", transform.position);
            yield return new WaitForSeconds(0.8f);

            yield return GoHomeThenVanish();
        }

        /// <summary>Runs to the ladder's landing spot (away from the wall, never into it) and vanishes in a sparkle.</summary>
        IEnumerator GoHomeThenVanish()
        {
            StopMoving();
            SetTelegraph(false);
            RestoreScale();

            if (ladder != null && AgentReady)
            {
                agent.updateRotation = true;
                agent.speed = DefeatedRunSpeed;
                agent.isStopped = false;
                agent.SetDestination(ladder.Landing);

                float timeout = Time.time + GoHomeTimeoutSeconds;
                while (Time.time < timeout && (agent.pathPending || agent.remainingDistance > GoHomeArriveDistance))
                    yield return null;
            }

            BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);
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
