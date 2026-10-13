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
        [SerializeField] float minAttackInterval = 1.8f;
        [SerializeField] float maxAttackInterval = 3f;
        [SerializeField] float hitPauseSeconds = 0.6f;

        [Header("Help a friend (castle adventure)")]
        [Tooltip("A pulsing blue '!' about 0.45 m above the head, shown while this knight calls for help. Optional.")]
        [SerializeField] GameObject helpMarker;
        [Tooltip("How fast a knight who calls for help backs away from the nearest goblin.")]
        [SerializeField] float helpBackAwaySpeed = 1.2f;
        [Tooltip("A goblin closer than this makes the knight back away.")]
        [SerializeField] float helpBackAwayRadius = 4f;

        const float HelpPulseSpeed = 7f;
        const float HelpPulseAmount = 0.18f;

        NavMeshAgent agent;
        Hittable hittable;
        SwordFighter fighter;
        KidAnimator kidAnimator;
        PlayerController player;
        float nextAttackTime;
        float hitPauseUntil;
        float nextRepathTime;
        Vector3 helpMarkerBaseScale = Vector3.one;
        bool helpMarkerScaleCached;

        /// <summary>True while this knight calls for help: no attacking, slowly backing away from the nearest goblin.</summary>
        public bool IsCallingForHelp { get; private set; }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            fighter = GetComponent<SwordFighter>();
            kidAnimator = GetComponent<KidAnimator>();

            // Allies only make opponents wobble and giggle; the player's sword is the only one that defeats.
            fighter.CanDefeat = false;
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

        /// <summary>
        /// Shows or hides the blue "!" and, while on, stops this knight from attacking: he backs away slowly from the
        /// nearest goblin until somebody helps. Works also while the brain is switched off (the marker then stands still).
        /// </summary>
        public void CallForHelp(bool on)
        {
            IsCallingForHelp = on;
            if (helpMarker != null && helpMarker.activeSelf != on) helpMarker.SetActive(on);
            if (!on && helpMarker != null && helpMarkerScaleCached) helpMarker.transform.localScale = helpMarkerBaseScale;
            if (!on && AgentReady) agent.speed = moveSpeed;
        }

        void Update()
        {
            if (IsCallingForHelp) PulseHelpMarker();
            if (!AgentReady) return;

            if (IsCallingForHelp)
            {
                BackAwayFromGoblins();
                return;
            }

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

        void PulseHelpMarker()
        {
            if (helpMarker == null) return;

            if (!helpMarkerScaleCached)
            {
                helpMarkerScaleCached = true;
                helpMarkerBaseScale = helpMarker.transform.localScale;
            }
            float pulse = 1f + Mathf.Sin(Time.time * HelpPulseSpeed) * HelpPulseAmount;
            helpMarker.transform.localScale = helpMarkerBaseScale * pulse;
        }

        /// <summary>Calling for help: no swinging, a slow step away from the nearest goblin, or standing still when none is close.</summary>
        void BackAwayFromGoblins()
        {
            agent.speed = helpBackAwaySpeed;
            if (Time.time < hitPauseUntil)
            {
                agent.isStopped = true;
                return;
            }
            agent.isStopped = false;

            Hittable foe = FindNearestOpponent();
            if (foe == null)
            {
                agent.ResetPath();
                return;
            }

            float distance = Hittable.FlatDistance(transform.position, foe.transform.position);
            if (distance > helpBackAwayRadius)
            {
                agent.ResetPath();
                return;
            }

            if (Time.time < nextRepathTime) return;
            nextRepathTime = Time.time + 0.3f;

            Vector3 away = -Hittable.FlatDirection(transform.position, foe.transform.position);
            if (away == Vector3.zero) away = -transform.forward;
            Vector3 spot = transform.position + away * 2f;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
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
