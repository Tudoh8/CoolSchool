using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The playground ship: he hops onto the deck where the wheel is, stands and steers (his yaw sways +-20 degrees and the wheel
    /// turns with him) for 5 seconds with "Ahoi! Land in zicht!", cheers, and hops off. The ship itself does not move.
    /// </summary>
    public class ShipWheelRoutine : ToyRoutine
    {
        const float SteerSeconds = 5f;
        const float SwayDegrees = 20f;
        const float SwayFrequency = 0.4f;
        const float WheelTurnFactor = 2.5f;
        const float CheerSeconds = 1.0f;

        [Tooltip("Where he stands on the deck (his feet), facing the way the ship points.")]
        [SerializeField] Transform anchor;
        [Tooltip("The steering wheel (optional); it turns about its own axis.")]
        [SerializeField] Transform wheel;
        [Tooltip("The wheel's axle in its own frame.")]
        [SerializeField] Vector3 wheelAxis = Vector3.forward;

        Quaternion anchorRest = Quaternion.identity;
        Quaternion wheelRest = Quaternion.identity;
        bool restKnown;

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
            if (restKnown) return;

            if (anchor != null) anchorRest = anchor.rotation;
            if (wheel != null) wheelRest = wheel.localRotation;
            restKnown = true;
        }

        void SetYaw(float degrees)
        {
            CaptureRest();
            if (anchor != null) anchor.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * anchorRest;
            if (wheel != null) wheel.localRotation = wheelRest * Quaternion.AngleAxis(-degrees * WheelTurnFactor, wheelAxis.normalized);
        }

        public override void SnapToRest()
        {
            SetYaw(0f);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            SnapToRest();
            yield break;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (anchor == null)
            {
                Debug.LogWarning("CoolSchool toys: the ship routine of " + name + " is not set up.");
                yield break;
            }

            // The routine cheers itself, so landing does not cheer a second time.
            ctx.CheerOnLand = false;

            SnapToRest();
            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            ctx.MarkPlayed();
            ctx.Say("toys.ahoy");

            // Steer: look from side to side while the wheel turns.
            yield return ToyMoves.Over(ctx, SteerSeconds, k =>
            {
                float fade = Mathf.Min(1f, k * 6f) * Mathf.Min(1f, (1f - k) * 6f);
                SetYaw(SwayDegrees * fade * Mathf.Sin(2f * Mathf.PI * SwayFrequency * k * SteerSeconds));
            });
            SetYaw(0f);

            if (!ctx.SkipRequested)
            {
                if (ctx.Kid != null) ctx.Kid.PlayCheer();
                yield return ctx.Wait(CheerSeconds);
            }

            yield return Leave(ctx);
        }

        IEnumerator Leave(ToyPlayContext ctx)
        {
            Vector3 here = anchor != null ? anchor.position : transform.position;
            Vector3 exit = exitPoint != null ? exitPoint.position : new Vector3(here.x, 0f, here.z);
            yield return ctx.HopOff(exit);
        }
    }
}
