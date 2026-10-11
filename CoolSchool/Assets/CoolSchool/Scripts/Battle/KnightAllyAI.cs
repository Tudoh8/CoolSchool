using CoolSchool.Characters;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A classmate who plays a knight. Stays close to the player, and when a goblin or the dragon
    /// is nearby, goes over and gives it a bump every now and then. Switched off until the knights game.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable), typeof(SwordFighter))]
    public class KnightAllyAI : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 2.6f;
        [Tooltip("Closer than this to the player: stand still. Further than followMax: walk over.")]
        [SerializeField] float followMin = 2f;
        [SerializeField] float followMax = 4f;
        [Tooltip("An opponent closer than this gets challenged.")]
        [SerializeField] float engageRadius = 5f;
        [SerializeField] float minAttackInterval = 1.5f;
        [SerializeField] float maxAttackInterval = 2.5f;
        [SerializeField] float hitPauseSeconds = 0.6f;

        NavMeshAgent agent;
        Hittable hittable;
        SwordFighter fighter;
        KidAnimator kidAnimator;
        PlayerController player;
        float nextAttackTime;
        float hitPauseUntil;
        float nextRepathTime;

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            fighter = GetComponent<SwordFighter>();
            kidAnimator = GetComponent<KidAnimator>();
        }

        void OnEnable()
        {
            hittable.Hit += OnHit;
            hittable.Blocked += OnBlocked;
            hittable.Defeated += OnDefeated;
            nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
            hitPauseUntil = 0f;

            if (AgentReady)
            {
                agent.speed = moveSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Blocked -= OnBlocked;
                hittable.Defeated -= OnDefeated;
            }

            if (fighter != null) fighter.SetBlocking(false);
            if (AgentReady) agent.ResetPath();
        }

        void Update()
        {
            if (!AgentReady) return;

            agent.speed = moveSpeed;

            if (Time.time < hitPauseUntil)
            {
                agent.isStopped = true;
                return;
            }
            agent.isStopped = false;

            Hittable foe = FindNearestOpponent();
            if (foe != null) Fight(foe);
            else FollowPlayer();
        }

        void Fight(Hittable foe)
        {
            // Stand just inside sword reach, measured to the edge of the opponent.
            float stopDistance = 0.9f + foe.HitRadius;
            float distance = Hittable.FlatDistance(transform.position, foe.transform.position);

            if (distance > stopDistance)
            {
                MoveTo(foe.transform.position);
                return;
            }

            agent.ResetPath();
            Face(foe.transform.position);

            if (Time.time >= nextAttackTime && fighter.Attack())
                nextAttackTime = Time.time + Random.Range(minAttackInterval, maxAttackInterval);
        }

        void FollowPlayer()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            Vector3 playerPosition = player.transform.position;
            float distance = Hittable.FlatDistance(transform.position, playerPosition);

            if (distance > followMax)
            {
                // Aim for a spot at a comfortable distance, not on top of the player.
                Vector3 away = -Hittable.FlatDirection(transform.position, playerPosition);
                MoveTo(playerPosition + away * ((followMin + followMax) * 0.5f));
            }
            else if (distance < followMin)
            {
                agent.ResetPath();
            }
        }

        void MoveTo(Vector3 destination)
        {
            if (Time.time < nextRepathTime) return;
            nextRepathTime = Time.time + 0.2f;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        void Face(Vector3 point)
        {
            Vector3 direction = Hittable.FlatDirection(transform.position, point);
            if (direction == Vector3.zero) return;

            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 540f * Time.deltaTime);
        }

        Hittable FindNearestOpponent()
        {
            Hittable nearest = null;
            float best = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (other.Team != BattleTeam.Opponents || !other.IsAlive) continue;

                // Distance to the edge, so the big dragon counts as close sooner.
                float distance = Hittable.FlatDistance(transform.position, other.transform.position) - other.HitRadius;
                if (distance > engageRadius || distance >= best) continue;

                best = distance;
                nearest = other;
            }
            return nearest;
        }

        void OnHit(Hittable who)
        {
            hitPauseUntil = Time.time + hitPauseSeconds;
            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        void OnBlocked(Hittable who)
        {
            if (kidAnimator != null) kidAnimator.PlayBlockHit();
        }

        /// <summary>Knights never fall over. If this is ever set up wrongly, they just get up again.</summary>
        void OnDefeated(Hittable who)
        {
            who.ResetState();
        }
    }
}
