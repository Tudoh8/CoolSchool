using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The monkey bars: hop up to hang under the first bar, shimmy along the bars to the other end (turned a quarter so the sideways
    /// ledge shuffle follows the bars) with short pauses, then drop to the ground. The anchor is the kid's root; the points are placed
    /// by the builder so the hands are at the bar height. The anchor's forward is the way he faces (across the bars).
    /// </summary>
    public class MonkeyBarsRoutine : ToyRoutine
    {
        const int Segments = 3;
        const float HangFirstSeconds = 0.5f;
        const float MoveSeconds = 1.5f;
        const float PauseSeconds = 0.5f;

        [Tooltip("The moving point the kid is parented to (his feet). At rest it sits at the start point.")]
        [SerializeField] Transform anchor;
        [Tooltip("Where the shimmy starts, with the rotation he hangs in.")]
        [SerializeField] Transform startPoint;
        [Tooltip("Where the shimmy ends.")]
        [SerializeField] Transform endPoint;

        void OnDisable()
        {
            SnapToRest();
        }

        public override void SnapToRest()
        {
            if (anchor != null && startPoint != null) anchor.SetPositionAndRotation(startPoint.position, startPoint.rotation);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            SnapToRest();
            yield break;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (anchor == null || startPoint == null || endPoint == null)
            {
                Debug.LogWarning("CoolSchool toys: the monkey bars routine of " + name + " is not set up.");
                yield break;
            }

            SnapToRest();
            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Drop(ctx);
                yield break;
            }

            // Hang under the first bar.
            ctx.SetPose(ToyPose.Hang);
            ctx.MarkPlayed();
            yield return ctx.Wait(HangFirstSeconds);

            Vector3 start = startPoint.position;
            Vector3 end = endPoint.position;
            Quaternion facing = startPoint.rotation;
            for (int segment = 0; segment < Segments && !ctx.SkipRequested; segment++)
            {
                Vector3 a = Vector3.Lerp(start, end, segment / (float)Segments);
                Vector3 b = Vector3.Lerp(start, end, (segment + 1) / (float)Segments);

                ctx.SetPose(ToyPose.HangMove);
                yield return ToyMoves.Over(ctx, MoveSeconds, k => anchor.SetPositionAndRotation(Vector3.Lerp(a, b, k), facing));

                ctx.SetPose(ToyPose.Hang);
                if (segment < Segments - 1) yield return ctx.Wait(PauseSeconds);
            }

            if (!ctx.SkipRequested) yield return ctx.Wait(0.3f);
            yield return Drop(ctx);
        }

        IEnumerator Drop(ToyPlayContext ctx)
        {
            Vector3 here = anchor != null ? anchor.position : transform.position;
            Vector3 exit = exitPoint != null ? exitPoint.position : new Vector3(here.x, 0f, here.z);
            yield return ctx.HopOff(exit);
        }
    }
}
