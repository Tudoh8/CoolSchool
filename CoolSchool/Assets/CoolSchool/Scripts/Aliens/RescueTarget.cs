using System.Collections;
using CoolSchool.Battle;
using CoolSchool.Characters;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// The baby alien. It starts stuck up in the tree and waves for help. When the kid flies close enough it jumps into
    /// his arms and they float down together. Once on the ground it runs to its place in the party line and dances.
    /// </summary>
    public class RescueTarget : MonoBehaviour
    {
        [Tooltip("The kid must be this close (metres, in the air) for the baby to hold on.")]
        [SerializeField] float grabDistance = 1.5f;
        [Tooltip("Where the baby sits while it is carried, relative to the kid's feet.")]
        [SerializeField] Vector3 carryOffset = new Vector3(0f, 0.7f, 0.4f);
        [SerializeField] float runSpeed = 2.6f;
        [Tooltip("Seconds between the baby's waves for help while it is stuck.")]
        [SerializeField] float helpSeconds = 3f;

        const float GiveUpWalkSeconds = 10f;

        NavMeshAgent agent;
        KidAnimator kidAnimator;
        Collider body;
        Transform homeParent;
        Coroutine routine;
        int danceVariant;

        /// <summary>True while the baby is up in the tree or in the kid's arms.</summary>
        public bool IsStuck { get; private set; }

        public bool IsCarried { get; private set; }

        /// <summary>True once the baby has been put down safely.</summary>
        public bool IsRescued { get; private set; }

        public bool IsDancing { get; private set; }

        void Awake() => CacheReferences();

        /// <summary>The baby starts switched off, so ShowAt may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            kidAnimator = GetComponent<KidAnimator>();
            body = GetComponent<Collider>();
            homeParent = transform.parent;
        }

        void OnDisable()
        {
            routine = null;
        }

        /// <summary>Appears up in the tree at the perch, facing the camera, and waves for help every few seconds.</summary>
        public void ShowAt(Transform perch)
        {
            CacheReferences();
            ResetTarget();

            IsStuck = true;
            // The perch is in the air, off the NavMesh: the agent and the collider stay off until the baby is down.
            if (agent != null) agent.enabled = false;
            if (body != null) body.enabled = false;

            transform.SetPositionAndRotation(perch.position, perch.rotation);
            gameObject.SetActive(true);
            BattleEffects.Sparkle(perch.position + Vector3.up * 0.6f);

            routine = StartCoroutine(HelpRoutine());
        }

        IEnumerator HelpRoutine()
        {
            while (true)
            {
                if (kidAnimator != null) kidAnimator.PlayCheer();
                BattleEffects.Sparkle(transform.position + Vector3.up * 1f);
                yield return new WaitForSeconds(helpSeconds);
            }
        }

        /// <summary>The kid flew close: the baby jumps into his arms. Returns false if he is too far away or it already holds on.</summary>
        public bool TryGrab(Transform player)
        {
            if (!IsStuck || IsCarried || player == null) return false;

            Vector3 playerChest = player.position + Vector3.up * 0.8f;
            Vector3 babyChest = transform.position + Vector3.up * 0.4f;
            if (Vector3.Distance(playerChest, babyChest) > grabDistance) return false;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            IsCarried = true;
            transform.SetParent(player, true);
            transform.localPosition = carryOffset;
            transform.localRotation = Quaternion.identity;

            BattleEffects.Sparkle(transform.position + Vector3.up * 0.5f);
            if (kidAnimator != null) kidAnimator.PlayCheer();
            return true;
        }

        /// <summary>
        /// Puts the baby down on the ground at this spot (the kid has landed) and switches its legs back on.
        /// </summary>
        public void PutDown(Vector3 groundPosition)
        {
            CacheReferences();
            if (routine != null) StopCoroutine(routine);
            routine = null;

            transform.SetParent(homeParent, true);
            if (NavMesh.SamplePosition(groundPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                groundPosition = hit.position;
            transform.position = groundPosition;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            IsCarried = false;
            IsStuck = false;
            IsRescued = true;

            if (body != null) body.enabled = true;
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(groundPosition);
            }
        }

        /// <summary>Runs to its spot in the party line, faces the camera side and dances.</summary>
        public void RunTo(Vector3 spot, Vector3 lookAt, int dance)
        {
            if (!isActiveAndEnabled) return;

            danceVariant = dance;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(RunRoutine(spot, lookAt));
        }

        IEnumerator RunRoutine(Vector3 spot, Vector3 lookAt)
        {
            if (kidAnimator != null) kidAnimator.PlayCheer();
            yield return new WaitForSeconds(0.6f);

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = runSpeed;
                agent.SetDestination(spot);

                float giveUp = Time.time + GiveUpWalkSeconds;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > 0.25f))
                    yield return null;

                agent.isStopped = true;
                agent.updateRotation = false;
            }

            Vector3 facing = lookAt - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                Quaternion wanted = Quaternion.LookRotation(facing.normalized, Vector3.up);
                for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 540f * Time.deltaTime);
                    yield return null;
                }
            }

            IsDancing = true;
            if (kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
            routine = null;
        }

        /// <summary>A little hop, used while the dancers are counted out loud.</summary>
        public void Hop()
        {
            if (!isActiveAndEnabled) return;

            StartCoroutine(HopRoutine());
        }

        IEnumerator HopRoutine()
        {
            if (kidAnimator != null)
            {
                kidAnimator.SetDancing(false);
                kidAnimator.PlayJump();
            }
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);

            yield return new WaitForSeconds(0.9f);
            if (IsDancing && kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
        }

        /// <summary>Stops the dance and cheers goodbye.</summary>
        public void Wave()
        {
            IsDancing = false;
            if (kidAnimator == null) return;

            kidAnimator.SetDancing(false);
            kidAnimator.PlayCheer();
        }

        /// <summary>Disappears with a puff of sparkles.</summary>
        public void Vanish()
        {
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);
            ResetTarget();
        }

        /// <summary>Back to the way it was before the game: switched off, back under its old parent, not rescued.</summary>
        public void ResetTarget()
        {
            CacheReferences();
            StopAllCoroutines();
            routine = null;

            if (kidAnimator != null && IsDancing) kidAnimator.SetDancing(false);
            IsDancing = false;
            IsCarried = false;
            IsStuck = false;
            IsRescued = false;

            if (transform.parent != homeParent) transform.SetParent(homeParent, true);
            if (agent != null) agent.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
