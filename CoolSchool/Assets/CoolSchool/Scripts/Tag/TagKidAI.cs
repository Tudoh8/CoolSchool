using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Tag
{
    /// <summary>
    /// A computer kid playing tag. Tuned so a 4-year-old always catches them
    /// eventually, and only gets caught now and then.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(TagParticipant))]
    public class TagKidAI : MonoBehaviour
    {
        [Header("Speeds (player runs at about 4 m/s)")]
        [SerializeField] float wanderSpeed = 1.2f;
        [SerializeField] float fleeSpeed = 3.2f;
        [Tooltip("Speed once out of breath. Lower than the player, so he always catches up.")]
        [SerializeField] float tiredSpeed = 1.8f;
        [SerializeField] float chaseSpeed = 2.8f;

        [Header("Fleeing")]
        [SerializeField] float fleeRadius = 6f;
        [Tooltip("Seconds of running away before the kid is fully out of breath.")]
        [SerializeField] float staminaSeconds = 5f;

        [Header("Chasing")]
        [Tooltip("Chance that the player is picked as the next target.")]
        [SerializeField, Range(0f, 1f)] float chasePlayerChance = 0.4f;
        [Tooltip("Give up chasing the player after this long, so he only gets caught now and then.")]
        [SerializeField] float giveUpOnPlayerSeconds = 6f;
        [SerializeField] float giveUpOnKidSeconds = 10f;

        [Header("Wandering")]
        [SerializeField] Vector3 playAreaCenter;
        [SerializeField] float playAreaRadius = 12f;

        NavMeshAgent agent;
        TagParticipant self;
        TagGame game;

        float stamina = 1f;
        float nextRepathTime;
        TagParticipant chaseTarget;
        TagParticipant lastGivenUpOn;
        float chaseStartTime;

        public void SetPlayArea(Vector3 center, float radius)
        {
            playAreaCenter = center;
            playAreaRadius = radius;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            self = GetComponent<TagParticipant>();
        }

        void Start()
        {
            game = FindAnyObjectByType<TagGame>();
        }

        void Update()
        {
            if (!agent.isOnNavMesh) return;

            // No game of tag right now: just play around.
            if (game == null || !game.IsRunning || game.It == null)
            {
                agent.isStopped = false;
                chaseTarget = null;
                Wander();
                return;
            }

            if (self.IsFrozen)
            {
                agent.isStopped = true;
                chaseTarget = null;
                return;
            }
            agent.isStopped = false;

            if (self.IsIt) Chase();
            else if (game.It.IsFrozen || !IsNear(game.It, fleeRadius)) Wander();
            else Flee(game.It);
        }

        void Chase()
        {
            stamina = 1f;
            agent.speed = chaseSpeed;

            float giveUpAfter = chaseTarget != null && chaseTarget.IsPlayer ? giveUpOnPlayerSeconds : giveUpOnKidSeconds;
            bool giveUp = chaseTarget != null && Time.time - chaseStartTime > giveUpAfter;
            if (giveUp) lastGivenUpOn = chaseTarget;

            if (chaseTarget == null || giveUp || !chaseTarget.CanBeTagged)
            {
                chaseTarget = PickTarget();
                chaseStartTime = Time.time;
                // Only skip the kid we gave up on for this one pick.
                lastGivenUpOn = null;
            }

            if (chaseTarget != null && Time.time >= nextRepathTime)
            {
                agent.SetDestination(chaseTarget.transform.position);
                nextRepathTime = Time.time + 0.2f;
            }
        }

        TagParticipant PickTarget()
        {
            TagParticipant player = game.PlayerParticipant;
            bool playerAvailable = player != null && player != lastGivenUpOn && player.CanBeTagged;
            if (playerAvailable && Random.value < chasePlayerChance) return player;

            TagParticipant nearest = null;
            float best = float.MaxValue;
            foreach (TagParticipant p in game.Participants)
            {
                if (p == self || p.IsPlayer || p == lastGivenUpOn || !p.CanBeTagged) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = p;
                }
            }
            return nearest != null ? nearest : (playerAvailable ? player : null);
        }

        void Flee(TagParticipant chaser)
        {
            stamina = Mathf.Max(0f, stamina - Time.deltaTime / staminaSeconds);
            agent.speed = Mathf.Lerp(tiredSpeed, fleeSpeed, stamina);

            if (Time.time < nextRepathTime) return;
            nextRepathTime = Time.time + 0.3f;

            Vector3 away = transform.position - chaser.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();

            // Try a fan of directions away from the chaser and take the one that ends furthest from it.
            Vector3 bestPoint = transform.position;
            float bestDistance = -1f;
            for (int angle = -90; angle <= 90; angle += 30)
            {
                Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * away * 5f;
                candidate = ClampToPlayArea(candidate);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

                float distance = (hit.position - chaser.transform.position).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestPoint = hit.position;
                }
            }
            agent.SetDestination(bestPoint);
        }

        void Wander()
        {
            stamina = Mathf.Min(1f, stamina + Time.deltaTime / staminaSeconds);
            agent.speed = wanderSpeed;
            chaseTarget = null;

            bool arrived = !agent.pathPending && agent.remainingDistance < 0.5f;
            if (!arrived && Time.time < nextRepathTime) return;

            Vector2 random = Random.insideUnitCircle * playAreaRadius;
            Vector3 candidate = playAreaCenter + new Vector3(random.x, 0f, random.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            nextRepathTime = Time.time + Random.Range(3f, 6f);
        }

        Vector3 ClampToPlayArea(Vector3 point)
        {
            Vector3 offset = point - playAreaCenter;
            offset.y = 0f;
            Vector3 clamped = playAreaCenter + Vector3.ClampMagnitude(offset, playAreaRadius);
            clamped.y = point.y;
            return clamped;
        }

        bool IsNear(TagParticipant other, float radius)
        {
            return (other.transform.position - transform.position).sqrMagnitude < radius * radius;
        }
    }
}
