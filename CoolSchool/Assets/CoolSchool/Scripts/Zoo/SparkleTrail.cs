using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Zoo
{
    /// <summary>
    /// A trail of sparkles on the ground from the player towards a place, along the walking route (so it goes around fences and
    /// covers). It is the same trail the activity inviter lays, but this one can lead anywhere: to a hiding spot he can't find,
    /// to a glowing hiding place, to the pens. It sparkles every 1.5 m and lays them again every 0.7 s while it shows.
    /// </summary>
    public class SparkleTrail : MonoBehaviour
    {
        const float Spacing = 1.5f;
        const float RefreshSeconds = 0.7f;
        const float StartDistance = 1f;
        const float Height = 0.3f;
        const int MaxPoints = 16;
        const float SampleDistance = 2f;
        const float DefaultStopDistance = 1f;

        Transform from;
        Vector3 to;
        float stopDistance = DefaultStopDistance;
        float endTime;
        float nextTime;
        bool showing;
        NavMeshPath path;

        public bool IsShowing => showing;

        /// <summary>Lays the trail from this transform (the player) to the point for that many seconds; it follows the player while it shows.</summary>
        public void Show(Transform start, Vector3 destination, float seconds = 5f)
        {
            Show(start, destination, seconds, DefaultStopDistance);
        }

        /// <summary>The same, stopping this far (along the route) before the destination.</summary>
        public void Show(Transform start, Vector3 destination, float seconds, float stopBefore)
        {
            from = start;
            to = destination;
            stopDistance = stopBefore;
            endTime = Time.time + Mathf.Max(0.1f, seconds);
            nextTime = 0f;
            showing = from != null;
        }

        public void Stop()
        {
            showing = false;
        }

        void OnDisable()
        {
            showing = false;
        }

        void Update()
        {
            if (!showing) return;

            if (Time.time >= endTime || from == null)
            {
                showing = false;
                return;
            }
            if (Time.time < nextTime) return;

            nextTime = Time.time + RefreshSeconds;
            if (!ShowPathTrail()) ShowStraightTrail();
        }

        /// <summary>Sparkles along the NavMesh path. Returns false when there is no usable path.</summary>
        bool ShowPathTrail()
        {
            if (!NavMesh.SamplePosition(from.position, out NavMeshHit start, SampleDistance, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit end, SampleDistance, NavMesh.AllAreas))
                return false;

            if (path == null) path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)) return false;
            if (path.status == NavMeshPathStatus.PathInvalid) return false;

            Vector3[] corners = path.corners;
            if (corners.Length < 2) return false;

            float totalLength = 0f;
            for (int i = 1; i < corners.Length; i++) totalLength += Vector3.Distance(corners[i - 1], corners[i]);

            float last = Mathf.Max(0f, totalLength - stopDistance);
            float along = StartDistance;
            float segmentStart = 0f;
            int segment = 1;
            for (int i = 0; i < MaxPoints && along <= last && segment < corners.Length; i++)
            {
                float segmentLength = Vector3.Distance(corners[segment - 1], corners[segment]);
                while (along > segmentStart + segmentLength && segment < corners.Length - 1)
                {
                    segmentStart += segmentLength;
                    segment++;
                    segmentLength = Vector3.Distance(corners[segment - 1], corners[segment]);
                }

                float t = segmentLength > 0.0001f ? Mathf.Clamp01((along - segmentStart) / segmentLength) : 1f;
                BattleEffects.Sparkle(Vector3.Lerp(corners[segment - 1], corners[segment], t) + Vector3.up * Height);
                along += Spacing;
            }
            return true;
        }

        /// <summary>The fallback: sparkles along the straight line.</summary>
        void ShowStraightTrail()
        {
            Vector3 origin = from.position;
            Vector3 toTarget = to - origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.01f) return;

            Vector3 direction = toTarget / distance;
            float last = Mathf.Max(0f, distance - stopDistance);
            float along = StartDistance;
            for (int i = 0; i < MaxPoints && along <= last; i++)
            {
                BattleEffects.Sparkle(origin + direction * along + Vector3.up * Height);
                along += Spacing;
            }
        }
    }
}
