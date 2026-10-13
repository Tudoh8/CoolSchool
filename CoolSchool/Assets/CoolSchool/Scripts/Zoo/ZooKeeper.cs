using System;
using System.Collections;
using CoolSchool.Battle;
using CoolSchool.Characters;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// The zookeeper: a classmate in an explorer outfit who runs in when the animals escape, wanders along behind the child while
    /// he searches, counts with her back to the camera, walks to the hiding spots when it is her turn to seek, and cheers. She
    /// walks on the NavMesh like the knight allies (and hops the last bit when she is stuck, so she never holds up the game).
    /// </summary>
    public class ZooKeeper : MonoBehaviour
    {
        [SerializeField] NavMeshAgent agent;
        [SerializeField] KidAnimator kidAnimator;
        [SerializeField] Transform home;

        const float WalkSpeed = 2.8f;
        const float RunSpeed = 4.4f;
        const float FarDistance = 7f;
        const float ArriveDistance = 0.45f;
        const float RepathSeconds = 0.3f;
        const float TurnSpeed = 540f;
        const float HopSeconds = 0.5f;
        const float HopHeight = 0.7f;
        const float SampleDistance = 2.5f;

        Transform followTarget;
        float followDistance = 3f;
        float nextRepath;
        Coroutine walkRoutine;
        bool facing;
        Quaternion faceRotation = Quaternion.identity;
        bool hopping;

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        /// <summary>True while she is walking to a point (WalkTo).</summary>
        public bool IsWalking => walkRoutine != null;

        void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (kidAnimator == null) kidAnimator = GetComponent<KidAnimator>();
        }

        // ----- Coming and going --------------------------------------------------------------------

        /// <summary>Appears at her home spot with a sparkle.</summary>
        public void AppearAtHome()
        {
            Vector3 position = home != null ? home.position : transform.position;
            Quaternion rotation = home != null ? home.rotation : transform.rotation;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, SampleDistance, NavMesh.AllAreas)) position = hit.position;

            ClearOrders();
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(position);
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.speed = WalkSpeed;
                agent.angularSpeed = TurnSpeed;
                agent.acceleration = 14f;
                agent.stoppingDistance = 0f;
            }
            if (kidAnimator != null) kidAnimator.SetDancing(false);
            BattleEffects.Sparkle(position + Vector3.up * 1f);
        }

        /// <summary>Puffs away: a sparkle, and she is switched off.</summary>
        public void GoHome()
        {
            if (!gameObject.activeSelf) return;

            ClearOrders();
            if (kidAnimator != null) kidAnimator.SetDancing(false);
            BattleEffects.Sparkle(transform.position + Vector3.up * 1f);
            gameObject.SetActive(false);
        }

        // ----- Orders ------------------------------------------------------------------------------

        /// <summary>Wanders along behind the target, staying about this far away. Cancels any other order.</summary>
        public void Follow(Transform target, float distance = 3f)
        {
            ClearOrders();
            followTarget = target;
            followDistance = Mathf.Max(1f, distance);
            nextRepath = 0f;
        }

        /// <summary>
        /// Walks to the point on the NavMesh and calls arrived. After the timeout she hops the rest of the way, so arrived is
        /// always called (unless another order cancels this one).
        /// </summary>
        public void WalkTo(Vector3 point, Action arrived, float timeout = 8f)
        {
            ClearOrders();
            if (!isActiveAndEnabled)
            {
                arrived?.Invoke();
                return;
            }
            walkRoutine = StartCoroutine(WalkRoutine(point, arrived, Mathf.Max(0.5f, timeout)));
        }

        /// <summary>Walks to the point, then turns to face north (away from the camera), ready to count.</summary>
        public void CountAt(Vector3 point, Action arrived, float timeout = 6f)
        {
            WalkTo(point, () =>
            {
                FaceNorth();
                arrived?.Invoke();
            }, timeout);
        }

        public void FaceNorth()
        {
            FaceDirection(Vector3.forward);
        }

        /// <summary>Turns on the spot to face a direction (flat) and keeps facing it while she stands still.</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            followTarget = null;
            faceRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            facing = true;
        }

        /// <summary>Stands still: no following, no walking.</summary>
        public void Stop()
        {
            ClearOrders();
        }

        void ClearOrders()
        {
            followTarget = null;
            facing = false;
            if (walkRoutine != null) StopCoroutine(walkRoutine);
            walkRoutine = null;
            if (hopping)
            {
                hopping = false;
                if (agent != null) agent.updatePosition = true;
            }
            if (AgentReady) agent.ResetPath();
        }

        // ----- Walking -----------------------------------------------------------------------------

        IEnumerator WalkRoutine(Vector3 point, Action arrived, float timeout)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, SampleDistance, NavMesh.AllAreas)) point = hit.position;

            bool reached = false;
            if (AgentReady)
            {
                agent.speed = RunSpeed;
                agent.isStopped = false;
                agent.SetDestination(point);
                yield return null;

                float end = Time.time + timeout;
                while (Time.time < end)
                {
                    if (!AgentReady) break;
                    if (!agent.pathPending && agent.remainingDistance <= ArriveDistance)
                    {
                        reached = true;
                        break;
                    }
                    yield return null;
                }
                if (AgentReady) agent.ResetPath();
            }

            // Stuck, too slow or not on the NavMesh: hop the rest of the way.
            if (!reached) yield return HopRoutine(point);

            walkRoutine = null;
            arrived?.Invoke();
        }

        IEnumerator HopRoutine(Vector3 end)
        {
            hopping = true;
            if (kidAnimator != null) kidAnimator.PlayJump();
            if (agent != null) agent.updatePosition = false;

            Vector3 start = transform.position;
            Vector3 flat = end - start;
            flat.y = 0f;
            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat, Vector3.up) : startRotation;
            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / HopSeconds;
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * HopHeight);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, k);
                yield return null;
            }

            transform.position = end;
            hopping = false;
            if (agent != null)
            {
                agent.updatePosition = true;
                if (agent.isOnNavMesh) agent.Warp(end);
            }
        }

        void Update()
        {
            if (hopping) return;

            if (followTarget != null) UpdateFollow();
            else if (facing && walkRoutine == null) UpdateFacing();
        }

        void UpdateFollow()
        {
            if (!AgentReady) return;

            Vector3 offset = followTarget.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance <= followDistance)
            {
                // Close enough: stand and watch the child.
                if (agent.hasPath) agent.ResetPath();
                if (distance > 0.5f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset / distance, Vector3.up), TurnSpeed * 0.3f * Time.deltaTime);
                return;
            }

            if (Time.time < nextRepath) return;
            nextRepath = Time.time + RepathSeconds;

            agent.speed = distance > FarDistance ? RunSpeed : WalkSpeed;
            Vector3 stand = followTarget.position - offset / distance * (followDistance * 0.8f);
            if (NavMesh.SamplePosition(stand, out NavMeshHit hit, SampleDistance, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }

        void UpdateFacing()
        {
            if (AgentReady && agent.hasPath && agent.remainingDistance > 0.2f) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, faceRotation, TurnSpeed * Time.deltaTime);
        }

        // ----- Moods ------------------------------------------------------------------------------

        public void PlayLaugh()
        {
            if (kidAnimator != null) kidAnimator.PlayLaugh();
        }

        public void PlayCheer()
        {
            if (kidAnimator != null) kidAnimator.PlayCheer();
        }

        public void PlayJump()
        {
            if (kidAnimator != null) kidAnimator.PlayJump();
        }

        public void SetDancing(bool dancing)
        {
            if (kidAnimator != null) kidAnimator.SetDancing(dancing);
        }

        void OnDisable()
        {
            walkRoutine = null;
            hopping = false;
        }
    }
}
