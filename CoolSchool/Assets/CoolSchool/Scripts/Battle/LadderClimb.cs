using System.Collections;
using CoolSchool.Characters;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Battle
{
    /// <summary>
    /// Brings a goblin into the castle the fun way: it starts at a ladder's foot outside the back wall, climbs up
    /// (running on the spot), steps onto the wall top, jumps down in an arc and lands in the courtyard, where
    /// BattleOpponentAI takes over again. While this runs the NavMeshAgent is off and the goblin cannot be hit or hit
    /// back (its Hittable is switched off, so no sword finds it). OnDisable puts everything back, so ending the game
    /// at any moment leaves a clean goblin for the next time.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable))]
    public class LadderClimb : MonoBehaviour
    {
        const float ClimbSpeed = 2.2f;
        /// <summary>The animator speed used while climbing (a run, so the legs move on the rungs).</summary>
        const float ClimbPoseSpeed = 2.5f;
        const float LeanDegrees = 12f;
        const float StepOverSeconds = 0.35f;
        const float StepOverHop = 0.15f;
        const float JumpSeconds = 1f;
        const float JumpHop = 0.9f;
        const float LandingPauseSeconds = 0.3f;
        /// <summary>The next goblin may start on the same ladder this long after the one before it.</summary>
        const float SlotGapSeconds = 0.9f;

        NavMeshAgent agent;
        Hittable hittable;
        KidAnimator kid;
        Collider body;

        /// <summary>True from Begin until the goblin has landed (or was switched off).</summary>
        public bool IsClimbing { get; private set; }

        void Awake() => CacheReferences();

        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            kid = GetComponent<KidAnimator>();
            body = GetComponent<Collider>();
        }

        void OnDisable() => Restore();

        /// <summary>
        /// Call before switching the goblin on: turns the agent and the hitting off and puts the goblin at the ladder's foot,
        /// so the agent never wakes up outside the NavMesh.
        /// </summary>
        public void Begin(WallLadder ladder)
        {
            CacheReferences();
            IsClimbing = true;
            agent.enabled = false;
            hittable.enabled = false;
            if (body != null) body.enabled = false;

            transform.position = ladder.Foot;
            transform.rotation = ClimbRotation(ladder);
        }

        /// <summary>The whole trip. The goblin ends standing at the ladder's landing, with the agent back on.</summary>
        public IEnumerator Run(WallLadder ladder)
        {
            // Wait for a free place on the ladder, so goblins don't overlap.
            float startTime = ladder.ReserveSlot(SlotGapSeconds);
            while (Time.time < startTime)
                yield return null;

            if (kid != null) kid.SpeedOverride = ClimbPoseSpeed;

            // Up the ladder.
            Quaternion climbRotation = ClimbRotation(ladder);
            Vector3 foot = ladder.Foot;
            Vector3 shoulder = ladder.Shoulder;
            float duration = Mathf.Max(0.1f, Vector3.Distance(foot, shoulder) / ClimbSpeed);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(foot, shoulder, elapsed / duration);
                transform.rotation = climbRotation;
                yield return null;
            }

            // Hop from the ladder onto the wall top. A sparkle shows the kid who is popping up.
            BattleEffects.Sparkle(ladder.Rim + Vector3.up * 0.8f);
            if (kid != null) kid.SpeedOverride = -1f;
            Vector3 rim = ladder.Rim;
            Quaternion landingRotation = FlatLook(ladder.Landing - rim, climbRotation);
            for (float elapsed = 0f; elapsed < StepOverSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / StepOverSeconds;
                Vector3 position = Vector3.Lerp(shoulder, rim, t);
                position.y += Mathf.Sin(t * Mathf.PI) * StepOverHop;
                transform.position = position;
                transform.rotation = Quaternion.Slerp(climbRotation, landingRotation, t);
                yield return null;
            }

            // Jump down into the courtyard.
            Vector3 landing = ladder.Landing;
            if (kid != null) kid.PlayJump();
            for (float elapsed = 0f; elapsed < JumpSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / JumpSeconds;
                Vector3 position = Vector3.Lerp(rim, landing, t);
                // The fall speeds up like a real one, with a little hop on top at the start.
                position.y = Mathf.Lerp(rim.y, landing.y, t * t) + 4f * JumpHop * t * (1f - t);
                transform.position = position;
                transform.rotation = landingRotation;
                yield return null;
            }

            transform.position = landing;
            transform.rotation = landingRotation;
            BattleEffects.Sparkle(landing + Vector3.up * 0.2f);
            yield return new WaitForSeconds(LandingPauseSeconds);

            Finish(landing);
        }

        /// <summary>Back on the NavMesh and hittable: the normal fighting AI can take over.</summary>
        void Finish(Vector3 landing)
        {
            Restore();

            if (agent != null && agent.isActiveAndEnabled)
            {
                if (!agent.Warp(landing) && NavMesh.SamplePosition(landing, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
        }

        /// <summary>Puts the agent, the Hittable and the animator back. Safe to call more than once.</summary>
        void Restore()
        {
            IsClimbing = false;
            if (kid != null) kid.SpeedOverride = -1f;

            if (agent != null) agent.enabled = true;
            if (hittable != null) hittable.enabled = true;
            if (body != null) body.enabled = true;
        }

        /// <summary>Facing the wall (from the foot toward the shoulder), leaning forward into the ladder.</summary>
        static Quaternion ClimbRotation(WallLadder ladder)
        {
            return FlatLook(ladder.Rim - ladder.Foot, Quaternion.identity) * Quaternion.Euler(LeanDegrees, 0f, 0f);
        }

        static Quaternion FlatLook(Vector3 direction, Quaternion fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Quaternion.Euler(0f, fallback.eulerAngles.y, 0f) : Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
