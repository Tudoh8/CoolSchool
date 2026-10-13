using System;
using System.Collections;
using CoolSchool.Characters;
using CoolSchool.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace CoolSchool.Castle
{
    /// <summary>
    /// The queen of the castle: a classmate in a paper crown who greets the knight at the gate, gives the mission and walks
    /// into the courtyard. She uses the same Kid animator as the other kids (it follows how fast she moves), has no Hittable and
    /// is never part of a fight. In free play she stands at her home spot and waves now and then when the knight is near.
    /// </summary>
    public class CastleQueen : MonoBehaviour
    {
        [Tooltip("Where she stands outside the courtyard; its rotation is how she faces. ResetQueen puts her back here.")]
        [SerializeField] Transform home;
        [SerializeField] float walkSpeed = 2.2f;

        const float WalkTimeoutSeconds = 10f;
        const float ArriveDistance = 0.35f;
        const float WaveRadius = 10f;
        const float WaveMinSeconds = 6f;
        const float WaveMaxSeconds = 12f;

        NavMeshAgent agent;
        KidAnimator kid;
        PlayerController player;
        Coroutine walkRoutine;
        Coroutine turnRoutine;
        float nextWaveTime;
        bool scripted;

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        /// <summary>Where she stands in free play.</summary>
        public Vector3 HomePosition => home != null ? home.position : transform.position;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            kid = GetComponent<KidAnimator>();
            nextWaveTime = Time.time + UnityEngine.Random.Range(WaveMinSeconds, WaveMaxSeconds);
        }

        void Update()
        {
            // Free play: a wave now and then for a knight who comes close. Not while she is being walked around.
            if (scripted || walkRoutine != null || Time.time < nextWaveTime) return;

            nextWaveTime = Time.time + Random.Range(WaveMinSeconds, WaveMaxSeconds);
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            if (CastleMotion.FlatDistance(player.transform.position, transform.position) <= WaveRadius) Wave();
        }

        void OnDisable()
        {
            walkRoutine = null;
            turnRoutine = null;
        }

        // ----- What the story asks of her --------------------------------------------------------

        /// <summary>
        /// Walks to a point over the navigation mesh at walkSpeed. If she is not there after 10 s she is moved there. arrived is
        /// called when she stands at the point.
        /// </summary>
        public void WalkTo(Vector3 point, Action arrived = null)
        {
            scripted = true;
            StopWalking();
            if (!isActiveAndEnabled)
            {
                MoveNow(point);
                arrived?.Invoke();
                return;
            }

            walkRoutine = StartCoroutine(WalkRoutine(point, arrived));
        }

        /// <summary>A wave: the kids' cheer move.</summary>
        public void Wave()
        {
            if (kid != null) kid.PlayCheer();
        }

        /// <summary>A cheer with a little hop.</summary>
        public void Cheer()
        {
            if (kid != null)
            {
                kid.PlayCheer();
                kid.PlayJump();
            }
        }

        /// <summary>Turns her on the spot to look along a direction (the height is ignored).</summary>
        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            if (turnRoutine != null) StopCoroutine(turnRoutine);
            turnRoutine = null;

            if (!isActiveAndEnabled)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return;
            }
            turnRoutine = StartCoroutine(TurnRoutine(direction));
        }

        /// <summary>Back to the start: home, facing the talk spot, standing still. Safe at any moment.</summary>
        public void ResetQueen()
        {
            scripted = false;
            StopWalking();
            if (turnRoutine != null && isActiveAndEnabled) StopCoroutine(turnRoutine);
            turnRoutine = null;

            Quaternion facing = home != null ? home.rotation : transform.rotation;
            MoveNow(HomePosition);
            transform.rotation = facing;
            nextWaveTime = Time.time + Random.Range(WaveMinSeconds, WaveMaxSeconds);
        }

        // ----- Walking ---------------------------------------------------------------------------

        IEnumerator WalkRoutine(Vector3 point, Action arrived)
        {
            if (AgentReady)
            {
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
                agent.speed = walkSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.SetDestination(point);

                float giveUp = Time.time + WalkTimeoutSeconds;
                yield return null;
                while (Time.time < giveUp && AgentReady && (agent.pathPending || agent.remainingDistance > ArriveDistance))
                    yield return null;

                if (AgentReady)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }

            // Not there (no mesh, or it took too long): she is simply there.
            if (CastleMotion.FlatDistance(transform.position, point) > ArriveDistance * 2f) MoveNow(point);

            walkRoutine = null;
            arrived?.Invoke();
        }

        IEnumerator TurnRoutine(Vector3 direction)
        {
            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            for (float elapsed = 0f; elapsed < 1.2f; elapsed += Time.deltaTime)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 420f * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, wanted) < 1f) break;
                yield return null;
            }
            turnRoutine = null;
        }

        void StopWalking()
        {
            if (walkRoutine != null && isActiveAndEnabled) StopCoroutine(walkRoutine);
            walkRoutine = null;
            if (AgentReady)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        /// <summary>Puts her at a point at once: through the agent when she has one on the mesh, otherwise by the transform.</summary>
        void MoveNow(Vector3 point)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            if (AgentReady)
            {
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
                agent.Warp(point);
                agent.isStopped = true;
            }
            else
            {
                transform.position = point;
            }
        }
    }
}
