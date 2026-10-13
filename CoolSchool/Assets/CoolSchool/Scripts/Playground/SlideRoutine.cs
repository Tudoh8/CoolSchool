using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The slide tower: hop onto the ground where he stands, run round the tower to the foot of the stairs, run up the stairs (on the spot,
    /// the anchor carries him), across the landing and up the second flight to the platform, sit at the top of the chute, slide down (faster
    /// and faster), glide off the end onto the ground and hop off. The builder works out the points from the tower it builds round the chute:
    /// approachRing (ground points round the tower, in order, ringFootIndex being the one next to the foot of the stairs), climbPath (the kid's
    /// root from the foot of the stairs up to the platform) and slidePath (the kid's root along the chute and a little past its foot,
    /// chuteEndIndex being the last point on the chute itself).
    /// </summary>
    public class SlideRoutine : ToyRoutine
    {
        [Tooltip("The anchor the kid rides on. It is moved by this routine.")]
        [SerializeField] Transform rider;
        [Tooltip("Ground points round the tower, in order.")]
        [SerializeField] Transform[] approachRing;
        [Tooltip("Index in approachRing of the point next to the foot of the stairs.")]
        [SerializeField] int ringFootIndex;
        [Tooltip("The kid's root from the foot of the stairs up to the platform (the first point is on the ground).")]
        [SerializeField] Transform[] climbPath;
        [Tooltip("Where he lands when he skips on the way up: on the ground at the foot of the stairs.")]
        [SerializeField] Transform stairExit;
        [Tooltip("The kid's root positions from the seat at the top of the chute to the glide past its foot.")]
        [SerializeField] Transform[] slidePath;
        [Tooltip("Index in slidePath of the foot of the chute (the points after it lie on the ground).")]
        [SerializeField] int chuteEndIndex = 1;

        [Header("Timing")]
        [Tooltip("Metres per second on the way up (the kid runs on the spot).")]
        [SerializeField] float climbSpeed = 2.3f;
        [Tooltip("The run animation speed while he climbs.")]
        [SerializeField] float runAnimationSpeed = 3.2f;
        [SerializeField] float slideSeconds = 1.3f;

        bool running;

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (rider == null || climbPath == null || climbPath.Length < 2 || slidePath == null || slidePath.Length < 2)
            {
                yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);
                yield break;
            }

            // Hop onto the ground where he stands (a hop in place), facing the way he will run.
            Vector3 start = ctx.Player.transform.position;
            Vector3 first = approachRing != null && approachRing.Length > 0 ? NearestRing(start).position : climbPath[0].position;
            rider.SetPositionAndRotation(start, Facing(first - start, rider.rotation));
            yield return ctx.HopOnto(rider);

            // Run round the tower to the foot of the stairs and up them. The kid runs on the spot (the anchor does the moving).
            SetRunning(ctx, true);
            var route = BuildRoute(start);
            float nextStep = 0f;
            Quaternion rotation = rider.rotation;
            for (int i = 1; i < route.Length && !ctx.SkipRequested; i++)
            {
                Vector3 from = route[i - 1];
                Vector3 to = route[i];
                float length = Vector3.Distance(from, to);
                float seconds = Mathf.Max(0.05f, length / climbSpeed);
                Quaternion face = Facing(to - from, rotation);
                for (float t = 0f; t < seconds && !ctx.SkipRequested; t += Time.deltaTime)
                {
                    rider.position = Vector3.Lerp(from, to, t / seconds);
                    rotation = Quaternion.Slerp(rotation, face, Mathf.Clamp01(Time.deltaTime * 12f));
                    rider.rotation = rotation;
                    nextStep -= Time.deltaTime;
                    if (nextStep <= 0f && to.y > from.y + 0.05f)
                    {
                        ctx.Cue("toys.climb_step");
                        nextStep = 0.3f;
                    }
                    yield return null;
                }
            }
            SetRunning(ctx, false);

            // Sit down at the top of the chute and slide.
            if (!ctx.SkipRequested)
            {
                ctx.SetPose(ToyPose.SitSlide);
                Vector3 from = rider.position;
                Quaternion fromRotation = rider.rotation;
                Quaternion seatRotation = SlideRotation(0, fromRotation);
                for (float t = 0f; t < 0.35f && !ctx.SkipRequested; t += Time.deltaTime)
                {
                    float k = Smooth(t / 0.35f);
                    rider.position = Vector3.Lerp(from, slidePath[0].position, k);
                    rider.rotation = Quaternion.Slerp(fromRotation, seatRotation, k);
                    yield return null;
                }
            }

            bool slid = false;
            if (!ctx.SkipRequested)
            {
                slid = true;
                ctx.MarkPlayed();
                ctx.Say("toys.whee", 0.5f);
                ctx.Cue("toys.slide_whoosh");
                yield return Slide(ctx);
            }

            // A skip on the way up lands at the foot of the stairs, a skip on the chute at its foot.
            Vector3 exit = !slid && stairExit != null ? stairExit.position
                : ExitPoint != null ? ExitPoint.position : slidePath[slidePath.Length - 1].position;
            yield return ctx.HopOff(exit);
        }

        /// <summary>The points he runs through: from where he stands, round the tower the shorter way, then up the stairs.</summary>
        Vector3[] BuildRoute(Vector3 start)
        {
            var points = new System.Collections.Generic.List<Vector3> { start };
            if (approachRing != null && approachRing.Length > 2)
            {
                int count = approachRing.Length;
                int from = NearestRingIndex(start);
                int to = Mathf.Clamp(ringFootIndex, 0, count - 1);
                int forward = (to - from + count) % count;
                int backward = (from - to + count) % count;
                int step = forward <= backward ? 1 : -1;
                int index = from;
                points.Add(approachRing[index].position);
                while (index != to)
                {
                    index = (index + step + count) % count;
                    points.Add(approachRing[index].position);
                }
            }
            foreach (Transform point in climbPath) points.Add(point.position);
            return points.ToArray();
        }

        int NearestRingIndex(Vector3 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < approachRing.Length; i++)
            {
                Vector3 d = approachRing[i].position - position;
                d.y = 0f;
                if (d.sqrMagnitude < bestDistance)
                {
                    bestDistance = d.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        Transform NearestRing(Vector3 position)
        {
            return approachRing[NearestRingIndex(position)];
        }

        static Quaternion Facing(Vector3 direction, Quaternion fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;
        }

        /// <summary>Run on the spot while the anchor carries him (the animator is told a speed; it is switched off again when he sits).</summary>
        void SetRunning(ToyPlayContext ctx, bool on)
        {
            running = on;
            if (ctx.Kid == null) return;

            if (on)
            {
                ctx.SetPose(ToyPose.None);
                ctx.Kid.ForceIdle = false;
                ctx.Kid.SpeedOverride = runAnimationSpeed;
            }
            else
            {
                ctx.Kid.SpeedOverride = -1f;
                ctx.Kid.ForceIdle = true;
            }
        }

        IEnumerator Slide(ToyPlayContext ctx)
        {
            int last = slidePath.Length - 1;
            int chuteEnd = Mathf.Clamp(chuteEndIndex, 1, last);
            var lengths = new float[slidePath.Length];
            for (int i = 1; i < slidePath.Length; i++)
                lengths[i] = lengths[i - 1] + Vector3.Distance(slidePath[i - 1].position, slidePath[i].position);

            float chuteLength = Mathf.Max(0.1f, lengths[chuteEnd]);
            float total = lengths[last];
            float groundLength = Mathf.Max(0f, total - chuteLength);

            // Speeds up on the chute (distance grows with time squared), then slows down evenly to a stop on the ground.
            float endSpeed = 2f * chuteLength / slideSeconds;
            float glideSeconds = groundLength > 0.01f ? Mathf.Clamp(2f * groundLength / endSpeed, 0.25f, 1.2f) : 0f;

            Quaternion rotation = rider.rotation;
            for (float t = 0f; t < slideSeconds + glideSeconds && !ctx.SkipRequested; t += Time.deltaTime)
            {
                float distance;
                if (t < slideSeconds)
                {
                    float k = t / slideSeconds;
                    distance = chuteLength * k * k;
                }
                else
                {
                    float k = (t - slideSeconds) / glideSeconds;
                    distance = chuteLength + groundLength * (2f * k - k * k);
                }

                rider.position = PointAt(lengths, distance, out int segment);
                rotation = Quaternion.Slerp(rotation, SlideRotation(segment, rotation), Mathf.Clamp01(Time.deltaTime * 14f));
                rider.rotation = rotation;
                yield return null;
            }
        }

        /// <summary>The point at a distance along the path, and the index of the segment it lies on.</summary>
        Vector3 PointAt(float[] lengths, float distance, out int segment)
        {
            int last = slidePath.Length - 1;
            segment = 0;
            while (segment < last - 1 && distance > lengths[segment + 1]) segment++;

            float span = Mathf.Max(0.0001f, lengths[segment + 1] - lengths[segment]);
            float k = Mathf.Clamp01((distance - lengths[segment]) / span);
            return Vector3.Lerp(slidePath[segment].position, slidePath[segment + 1].position, k);
        }

        /// <summary>The way the kid sits on a segment: facing down it, with his up pitched to the slope.</summary>
        Quaternion SlideRotation(int segment, Quaternion fallback)
        {
            int last = slidePath.Length - 1;
            segment = Mathf.Clamp(segment, 0, last - 1);
            Vector3 direction = slidePath[segment + 1].position - slidePath[segment].position;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;
        }

        public override void SnapToRest()
        {
            if (rider != null && climbPath != null && climbPath.Length > 0 && climbPath[0] != null)
                rider.SetPositionAndRotation(climbPath[0].position, climbPath[0].rotation);
            running = false;
        }

        void OnDisable()
        {
            SnapToRest();
        }
    }
}
