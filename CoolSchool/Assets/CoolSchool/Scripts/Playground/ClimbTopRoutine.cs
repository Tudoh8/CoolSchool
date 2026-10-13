using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The fort and the tyre wall: hop onto the foot of the climb, climb up it, step onto the top, cheer ("Ik ben helemaal boven!"), and
    /// come down again: either a jump in an arc to the exit point (the tyre wall) or sitting down and sliding along the same path
    /// (the fort's own slide). The kid is parented to the anchor, which rests at the first path point. The builder places the points from
    /// the toy's geometry; see PlaygroundToysBuilder.More.cs.
    /// </summary>
    public class ClimbTopRoutine : ToyRoutine
    {
        const float StepOverSeconds = 0.5f;
        const float JumpSeconds = 0.8f;
        const float JumpHeight = 0.9f;
        const float StepCueSeconds = 0.45f;
        const float SlideSeconds = 1.5f;
        const float DefaultSlideHips = 0.12f;

        [Tooltip("The moving point the kid is parented to. At rest it sits at the first path point.")]
        [SerializeField] Transform anchor;
        [Tooltip("The way up: the foot first, the last point is where he stops climbing. Each point's forward is the way he faces there.")]
        [SerializeField] Transform[] path;
        [Tooltip("Where he stands on top (his feet), facing the way he jumps or slides.")]
        [SerializeField] Transform topStand;
        [Tooltip("The pose while climbing: Climb on a wall, Crawl up a slope.")]
        [SerializeField] ToyPose climbPose = ToyPose.Climb;
        [Tooltip("Come down by sitting and sliding back along the path instead of jumping.")]
        [SerializeField] bool slideDown;
        [SerializeField] float climbSpeed = 0.9f;
        [SerializeField] float cheerSeconds = 1.4f;
        [Tooltip("The voice group said at the top.")]
        [SerializeField] string topLine = "toys.top";

        void OnDisable()
        {
            SnapToRest();
        }

        bool IsSetUp => anchor != null && path != null && path.Length >= 2 && path[0] != null && path[path.Length - 1] != null && topStand != null;

        public override void SnapToRest()
        {
            if (anchor != null && path != null && path.Length > 0 && path[0] != null)
                anchor.SetPositionAndRotation(path[0].position, path[0].rotation);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            // The kid is gone from the anchor by now; nothing to show.
            SnapToRest();
            yield break;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (!IsSetUp)
            {
                Debug.LogWarning("CoolSchool toys: the climb routine of " + name + " is not set up.");
                yield break;
            }

            SnapToRest();
            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            // Up the path.
            ctx.SetPose(climbPose);
            ctx.MarkPlayed();
            var points = new List<Vector3>();
            var rotations = new List<Quaternion>();
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == null) continue;
                points.Add(path[i].position);
                rotations.Add(path[i].rotation);
            }
            float length = PathLength(points);
            float duration = Mathf.Clamp(length / Mathf.Max(0.1f, climbSpeed), 1.2f, 3.6f);
            float nextStep = 0f;
            yield return ToyMoves.Over(ctx, duration, k =>
            {
                Evaluate(points, rotations, k * length, out Vector3 position, out Quaternion rotation);
                anchor.SetPositionAndRotation(position, rotation);
                if (k * duration >= nextStep)
                {
                    ctx.Cue("toys.climb_step");
                    nextStep += StepCueSeconds;
                }
            });
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            // Step onto the top and turn round.
            Vector3 last = points[points.Count - 1];
            Quaternion lastRotation = rotations[rotations.Count - 1];
            Vector3 stand = topStand.position;
            Quaternion facingOut = topStand.rotation;
            yield return ToyMoves.Over(ctx, StepOverSeconds, k =>
            {
                Vector3 position = Vector3.Lerp(last, stand, ToyMoves.Smooth(k));
                position.y += Mathf.Sin(k * Mathf.PI) * 0.12f;
                anchor.SetPositionAndRotation(position, Quaternion.Slerp(lastRotation, facingOut, ToyMoves.Smooth(k)));
            });
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            // Cheer at the top.
            ctx.SetPose(ToyPose.None);
            if (ctx.Kid != null) ctx.Kid.PlayCheer();
            ctx.Say(topLine);
            yield return ctx.Wait(cheerSeconds);
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            if (slideDown) yield return SlideDown(ctx, points, stand);
            else yield return JumpDown(ctx, stand, facingOut);

            yield return Leave(ctx);
        }

        IEnumerator JumpDown(ToyPlayContext ctx, Vector3 stand, Quaternion facingOut)
        {
            Vector3 landing = ExitPosition(stand);
            if (ctx.Kid != null) ctx.Kid.PlayJump();
            yield return ToyMoves.Over(ctx, JumpSeconds, k =>
                anchor.SetPositionAndRotation(ToyMoves.Arc(stand, landing, JumpHeight, k), facingOut));
        }

        /// <summary>Sits down on the top and slides back along the way he came, faster and faster.</summary>
        IEnumerator SlideDown(ToyPlayContext ctx, List<Vector3> up, Vector3 stand)
        {
            float hips = ctx.Toy != null && ctx.Toy.PoseOffsets.sitSlideHips > 0.01f ? ctx.Toy.PoseOffsets.sitSlideHips : DefaultSlideHips;

            // Down: from the top stand, then the path from its last point back to the first.
            var down = new List<Vector3> { stand };
            for (int i = up.Count - 1; i >= 0; i--) down.Add(up[i]);
            var downRotations = new List<Quaternion>();
            for (int i = 0; i < down.Count; i++)
            {
                Vector3 toNext = i + 1 < down.Count ? down[i + 1] - down[i] : down[i] - down[i - 1];
                toNext.y = 0f;
                downRotations.Add(toNext.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toNext.normalized, Vector3.up) : anchor.rotation);
            }
            for (int i = 0; i < down.Count; i++) down[i] -= Vector3.up * hips;

            ctx.SetPose(ToyPose.SitSlide);
            ctx.Cue("toys.slide_whoosh");
            ctx.Say("toys.whee", 0.7f);
            float length = PathLength(down);
            yield return ToyMoves.Over(ctx, SlideSeconds, k =>
            {
                // Starts slowly and speeds up (distance grows with the square of the time).
                Evaluate(down, downRotations, k * k * length, out Vector3 position, out Quaternion rotation);
                anchor.SetPositionAndRotation(position, rotation);
            });
        }

        IEnumerator Leave(ToyPlayContext ctx)
        {
            yield return ctx.HopOff(ExitPosition(anchor != null ? anchor.position : transform.position));
        }

        Vector3 ExitPosition(Vector3 fallback)
        {
            return exitPoint != null ? exitPoint.position : new Vector3(fallback.x, 0f, fallback.z);
        }

        static float PathLength(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        /// <summary>The point and the facing at a distance along the polyline (clamped to its ends).</summary>
        static void Evaluate(List<Vector3> points, List<Quaternion> rotations, float distance, out Vector3 position, out Quaternion rotation)
        {
            position = points[0];
            rotation = rotations[0];
            float remaining = Mathf.Max(0f, distance);
            for (int i = 1; i < points.Count; i++)
            {
                float segment = Vector3.Distance(points[i - 1], points[i]);
                if (remaining <= segment || i == points.Count - 1)
                {
                    float t = segment > 0.0001f ? Mathf.Clamp01(remaining / segment) : 1f;
                    position = Vector3.Lerp(points[i - 1], points[i], t);
                    rotation = Quaternion.Slerp(rotations[i - 1], rotations[i], t);
                    return;
                }
                remaining -= segment;
            }
        }
    }
}
