using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The tunnel: hop to the mouth nearest to him, crawl through along the tunnel, and at the other mouth pop out with a jump and
    /// "peekaboo". The builder finds the tunnel's long axis and sets the path of the kid's root on the ground from just outside one
    /// mouth, through the tunnel, to just inside the other (the same points work in both directions), and an exit point outside each mouth.
    /// </summary>
    public class TunnelRoutine : ToyRoutine
    {
        [Tooltip("The anchor the kid rides on. It is moved by this routine.")]
        [SerializeField] Transform rider;
        [Tooltip("Points of the kid's root from outside one mouth, through the tunnel, to the other mouth.")]
        [SerializeField] Transform[] path;
        [Tooltip("Where he lands outside the first mouth (the start of the path) and the last mouth (its end).")]
        [SerializeField] Transform exitFirst;
        [SerializeField] Transform exitLast;
        [SerializeField] float crawlSeconds = 2.5f;

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (rider == null || path == null || path.Length < 2 || exitFirst == null || exitLast == null)
            {
                yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);
                yield break;
            }

            // Go in at the mouth that is nearest to him.
            Vector3 here = ctx.Player.transform.position;
            bool forward = (here - path[0].position).sqrMagnitude <= (here - path[path.Length - 1].position).sqrMagnitude;
            var points = new Vector3[path.Length];
            for (int i = 0; i < points.Length; i++) points[i] = path[forward ? i : path.Length - 1 - i].position;
            Transform start = forward ? exitFirst : exitLast;
            Transform end = forward ? exitLast : exitFirst;
            exitPoint = end;

            var lengths = new float[points.Length];
            for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            float total = Mathf.Max(0.1f, lengths[lengths.Length - 1]);

            rider.SetPositionAndRotation(points[0], Quaternion.LookRotation(Flat(points[1] - points[0]), Vector3.up));
            ctx.CheerOnLand = false;
            yield return ctx.HopOnto(rider);

            // Crawl through.
            ctx.SetPose(ToyPose.Crawl);
            ctx.MarkPlayed();
            ctx.Cue("toys.crawl");
            Quaternion rotation = rider.rotation;
            float progress = 0f;
            for (float t = 0f; t < crawlSeconds && !ctx.SkipRequested; t += Time.deltaTime)
            {
                progress = Smooth(t / crawlSeconds);
                rider.position = PointAt(points, lengths, progress * total, out int segment);
                rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(Flat(points[segment + 1] - points[segment]), Vector3.up),
                    Mathf.Clamp01(Time.deltaTime * 10f));
                rider.rotation = rotation;
                yield return null;
            }

            // A skip in the first half lands at the mouth he came in by.
            bool skipped = ctx.SkipRequested;
            if (skipped && progress < 0.5f) exitPoint = start;

            if (!skipped)
            {
                ctx.Say("toys.peekaboo");
                ctx.Cue("toys.crawl");
            }
            yield return ctx.HopOff(exitPoint.position);
        }

        static Vector3 PointAt(Vector3[] points, float[] lengths, float distance, out int segment)
        {
            segment = 0;
            while (segment < points.Length - 2 && distance > lengths[segment + 1]) segment++;

            float span = Mathf.Max(0.0001f, lengths[segment + 1] - lengths[segment]);
            float k = Mathf.Clamp01((distance - lengths[segment]) / span);
            return Vector3.Lerp(points[segment], points[segment + 1], k);
        }

        static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        public override void SnapToRest()
        {
            if (rider != null && path != null && path.Length > 0 && path[0] != null) rider.SetPositionAndRotation(path[0].position, path[0].rotation);
        }
    }
}
