using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The cable ride: he hops up to hang from the handle at the high end, zooms along the cable to the low end (ease in, ease out),
    /// swings back and forth a little, then drops. The handle is split off the cable and travels with him (a pivot at its top, so it
    /// hangs straight). After the ride it slides back to the high end.
    /// </summary>
    public class TrackRideRoutine : ToyRoutine
    {
        const float HangFirstSeconds = 0.4f;
        const float RideSeconds = 2.5f;
        const float SwingSeconds = 1.6f;
        const float SwingDegrees = 16f;
        const float SlideBackSeconds = 1.0f;

        [Tooltip("The handle's pivot (at the top of the handle, on the cable). The handle mesh and the anchor are its children.")]
        [SerializeField] Transform handle;
        [Tooltip("The kid's hanging point (a child of the handle pivot). Its forward is the way he faces: along the ride.")]
        [SerializeField] Transform anchor;
        [Tooltip("Where the handle pivot rests: on the cable at the high end.")]
        [SerializeField] Transform highPoint;
        [Tooltip("Where the handle pivot stops: on the cable at the low end.")]
        [SerializeField] Transform lowPoint;

        Quaternion restRotation = Quaternion.identity;
        bool restKnown;
        bool easing;

        void Awake()
        {
            CaptureRest();
        }

        void OnDisable()
        {
            SnapToRest();
        }

        void CaptureRest()
        {
            if (restKnown || handle == null) return;
            restRotation = handle.rotation;
            restKnown = true;
        }

        /// <summary>Puts the handle a part of the way along the cable (0 = high end, 1 = low end) at once. For the preview shots.</summary>
        public void Apply(float along)
        {
            if (handle == null || highPoint == null || lowPoint == null) return;
            CaptureRest();
            handle.SetPositionAndRotation(Vector3.Lerp(highPoint.position, lowPoint.position, Mathf.Clamp01(along)), restRotation);
        }

        public override void SnapToRest()
        {
            easing = false;
            CaptureRest();
            if (handle != null && highPoint != null) handle.SetPositionAndRotation(highPoint.position, restRotation);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            while (easing) yield return null;
            if (handle == null || highPoint == null) yield break;

            CaptureRest();
            easing = true;
            Vector3 from = handle.position;
            Quaternion fromRotation = handle.rotation;
            float duration = Mathf.Max(seconds, SlideBackSeconds);
            for (float elapsed = 0f; elapsed < duration && easing; elapsed += Time.deltaTime)
            {
                float k = ToyMoves.Smooth(elapsed / duration);
                handle.SetPositionAndRotation(Vector3.Lerp(from, highPoint.position, k), Quaternion.Slerp(fromRotation, restRotation, k));
                yield return null;
            }
            if (easing) handle.SetPositionAndRotation(highPoint.position, restRotation);
            easing = false;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (handle == null || anchor == null || highPoint == null || lowPoint == null)
            {
                Debug.LogWarning("CoolSchool toys: the track ride routine of " + name + " is not set up.");
                yield break;
            }

            SnapToRest();
            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Drop(ctx);
                yield break;
            }

            ctx.SetPose(ToyPose.Hang);
            ctx.MarkPlayed();
            yield return ctx.Wait(HangFirstSeconds);

            // Zoom along the cable.
            if (!ctx.SkipRequested)
            {
                ctx.Cue("toys.zip");
                ctx.Say("toys.whee", 0.6f);

                Vector3 high = highPoint.position;
                Vector3 low = lowPoint.position;
                yield return ToyMoves.Over(ctx, RideSeconds, k =>
                    handle.SetPositionAndRotation(Vector3.Lerp(high, low, ToyMoves.Smooth(k)), restRotation));
            }

            // A little swing back and forth at the end, along the cable.
            if (!ctx.SkipRequested)
            {
                Vector3 stop = lowPoint.position;
                yield return ToyMoves.Over(ctx, SwingSeconds, k =>
                {
                    float angle = SwingDegrees * (1f - k) * Mathf.Sin(k * Mathf.PI * 2f * 1.5f);
                    handle.SetPositionAndRotation(stop, restRotation * Quaternion.AngleAxis(angle, Vector3.right));
                });
            }

            yield return Drop(ctx);
        }

        IEnumerator Drop(ToyPlayContext ctx)
        {
            // Past the end of the cable he lands at the exit point; if he gets off early he drops straight down.
            Vector3 here = anchor != null ? anchor.position : transform.position;
            float groundY = exitPoint != null ? exitPoint.position.y : 0f;
            Vector3 exit = new Vector3(here.x, groundY, here.z);
            if (exitPoint != null && lowPoint != null)
            {
                Vector3 toLow = lowPoint.position - here;
                toLow.y = 0f;
                if (toLow.magnitude < 1.0f) exit = exitPoint.position;
            }
            yield return ctx.HopOff(exit);
        }
    }
}
