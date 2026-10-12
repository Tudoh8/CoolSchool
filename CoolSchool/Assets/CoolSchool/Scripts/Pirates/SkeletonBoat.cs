using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The skeletons' cardboard boat. It comes in from the west, drifts back and forth in front of the ship with balloons
    /// on sticks (the cannon's targets), and when they are all popped it sails to the island's west beach.
    /// The path points must NOT be children of the boat; their positions are read the first time the boat is used.
    /// </summary>
    public class SkeletonBoat : MonoBehaviour
    {
        [SerializeField] Transform startPoint;
        [SerializeField] Transform driftLeft;
        [SerializeField] Transform driftRight;
        [SerializeField] Transform islandBeachPoint;
        [Tooltip("The first N are used, depending on the level.")]
        [SerializeField] BalloonTarget[] balloons;
        [SerializeField] Transform[] skeletonSeats;
        [SerializeField] float speed = 1.6f;
        [Tooltip("Degrees per second when the boat turns to face where it is going.")]
        [SerializeField] float turnSpeed = 70f;
        [Header("Look")]
        [SerializeField] float bobHeight = 0.06f;
        [SerializeField] float rollDegrees = 3f;

        enum State { Idle, ToDrift, Drifting, ToIsland, Arrived }

        readonly List<BalloonTarget> activeBalloons = new List<BalloonTarget>();
        State state = State.Idle;
        bool cached;
        Vector3 start;
        Vector3 left;
        Vector3 right;
        Vector3 beach;
        Quaternion beachRotation;
        float baseY;
        Vector3 position;
        float yaw;
        float driftPhase;

        public Transform[] SkeletonSeats => skeletonSeats;

        public IReadOnlyList<BalloonTarget> ActiveBalloons => activeBalloons;

        /// <summary>True once the boat has reached the island's beach.</summary>
        public bool AtIsland => state == State.Arrived;

        /// <summary>Wakes up at the start point and sails in to the drift line. N balloons are on the sticks.</summary>
        public void Appear(int balloonCount)
        {
            CachePoints();
            Place(start, FacingYaw(start, left));
            gameObject.SetActive(true);

            activeBalloons.Clear();
            if (balloons != null)
            {
                for (int i = 0; i < balloons.Length; i++)
                {
                    if (balloons[i] == null) continue;

                    bool used = i < balloonCount;
                    balloons[i].gameObject.SetActive(used);
                    balloons[i].ResetBalloon();
                    if (used) activeBalloons.Add(balloons[i]);
                }
            }

            driftPhase = 0f;
            state = State.ToDrift;
        }

        /// <summary>Leaves the drift line and sails to the island's west beach.</summary>
        public void SailToIsland()
        {
            if (state == State.Arrived || state == State.ToIsland) return;

            CachePoints();
            state = State.ToIsland;
        }

        /// <summary>Switched off, back at the start, balloons whole again.</summary>
        public void ResetBoat()
        {
            CachePoints();
            state = State.Idle;
            activeBalloons.Clear();

            if (balloons != null)
            {
                foreach (BalloonTarget balloon in balloons)
                {
                    if (balloon == null) continue;

                    balloon.gameObject.SetActive(true);
                    balloon.ResetBalloon();
                }
            }

            Place(start, FacingYaw(start, left));
            gameObject.SetActive(false);
        }

        void Update()
        {
            switch (state)
            {
                case State.ToDrift: SailTo(left, State.Drifting); break;
                case State.Drifting: Drift(); break;
                case State.ToIsland: SailTo(beach, State.Arrived); break;
                case State.Arrived: Settle(); break;
            }

            ApplyPose();
        }

        // ----- Moving ------------------------------------------------------------------------------

        void SailTo(Vector3 target, State next)
        {
            Vector3 toTarget = target - position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            float step = speed * Time.deltaTime;

            if (distance <= step)
            {
                position = new Vector3(target.x, baseY, target.z);
                state = next;
                return;
            }

            position += toTarget / distance * step;
            TurnToward(toTarget);
        }

        /// <summary>Back and forth between the two drift points, slow at both ends.</summary>
        void Drift()
        {
            float span = Vector3.Distance(left, right);
            if (span < 0.1f) return;

            float omega = 2f * speed / span;
            float before = 0.5f - 0.5f * Mathf.Cos(driftPhase);
            driftPhase += omega * Time.deltaTime;
            float after = 0.5f - 0.5f * Mathf.Cos(driftPhase);

            Vector3 flat = Vector3.Lerp(left, right, after);
            position = new Vector3(flat.x, baseY, flat.z);

            Vector3 direction = (right - left) * Mathf.Sign(after - before + 0.00001f);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) TurnToward(direction);
        }

        /// <summary>At the beach: turn to the pose of the beach point and stay.</summary>
        void Settle()
        {
            float wanted = beachRotation.eulerAngles.y;
            yaw = Mathf.MoveTowardsAngle(yaw, wanted, turnSpeed * Time.deltaTime);
        }

        void TurnToward(Vector3 direction)
        {
            float wanted = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, wanted, turnSpeed * Time.deltaTime);
        }

        void ApplyPose()
        {
            if (state == State.Idle) return;

            float bob = Mathf.Sin(Time.time * 1.7f) * bobHeight;
            float roll = Mathf.Sin(Time.time * 1.3f + 1f) * rollDegrees;
            transform.SetPositionAndRotation(position + Vector3.up * bob, Quaternion.Euler(0f, yaw, roll));
        }

        // ----- Helpers ----------------------------------------------------------------------------

        void Place(Vector3 where, float facing)
        {
            position = new Vector3(where.x, baseY, where.z);
            yaw = facing;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        static float FacingYaw(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        /// <summary>Reads the path points once, before the boat has moved anywhere.</summary>
        void CachePoints()
        {
            if (cached) return;

            cached = true;
            start = startPoint != null ? startPoint.position : transform.position;
            left = driftLeft != null ? driftLeft.position : start + Vector3.right * 5f;
            right = driftRight != null ? driftRight.position : left + Vector3.right * 10f;
            beach = islandBeachPoint != null ? islandBeachPoint.position : right;
            beachRotation = islandBeachPoint != null ? islandBeachPoint.rotation : Quaternion.identity;
            baseY = startPoint != null ? startPoint.position.y : transform.position.y;
        }
    }
}
