using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The seesaw: a plush teddy sits on the far seat for good. The kid hops onto the near seat, sits, and the plank tilts between
    /// -12 and +12 degrees about its middle at about 0.6 Hz for 7 seconds, with a thump at each end. The plank is a moving part under
    /// a pivot at its middle; the anchor (the kid's seat) is a child of the plank pivot so it tilts with it.
    /// </summary>
    public class SeesawRoutine : ToyRoutine
    {
        const float RampInSeconds = 0.7f;
        const float RampOutSeconds = 0.8f;

        [Tooltip("The plank's pivot, at its middle at the height of the fulcrum. The plank and the seats are its children.")]
        [SerializeField] Transform plank;
        [Tooltip("The kid's seat on the plank (a child of the plank pivot). Its forward is the way he faces.")]
        [SerializeField] Transform anchor;
        [Tooltip("The tilt axis in the plank pivot's own frame, signed so that a positive angle lowers the kid's end.")]
        [SerializeField] Vector3 tiltAxis = Vector3.right;
        [SerializeField] float maxDegrees = 12f;
        [SerializeField] float frequency = 0.6f;
        [SerializeField] float playSeconds = 7f;

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
            if (restKnown || plank == null) return;
            restRotation = plank.localRotation;
            restKnown = true;
        }

        void SetAngle(float degrees)
        {
            if (plank == null) return;
            CaptureRest();
            plank.localRotation = restRotation * Quaternion.AngleAxis(degrees, tiltAxis.normalized);
        }

        float CurrentAngle()
        {
            if (plank == null) return 0f;
            CaptureRest();
            Quaternion difference = Quaternion.Inverse(restRotation) * plank.localRotation;
            difference.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(axis, tiltAxis.normalized) >= 0f ? angle : -angle;
        }

        /// <summary>Tilts the plank to an angle at once (positive lowers his end). For the preview shots.</summary>
        public void Apply(float degrees)
        {
            SetAngle(degrees);
        }

        public override void SnapToRest()
        {
            easing = false;
            SetAngle(0f);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            while (easing) yield return null;
            if (plank == null) yield break;

            easing = true;
            float from = CurrentAngle();
            for (float elapsed = 0f; elapsed < seconds && easing; elapsed += Time.deltaTime)
            {
                SetAngle(Mathf.Lerp(from, 0f, ToyMoves.Smooth(elapsed / seconds)));
                yield return null;
            }
            if (easing) SetAngle(0f);
            easing = false;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (plank == null || anchor == null)
            {
                Debug.LogWarning("CoolSchool toys: the seesaw routine of " + name + " is not set up.");
                yield break;
            }

            SnapToRest();
            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx);
                yield break;
            }

            ctx.SetPose(ToyPose.SitSeat);
            ctx.MarkPlayed();
            ctx.Say("toys.whee", 0.5f);

            // Tilt back and forth. The kid's end goes down first. The plank is level again at the end, so he steps off a still seat.
            int lastExtreme = -1;
            float time = 0f;
            while (time < playSeconds && !ctx.SkipRequested)
            {
                float fadeIn = Mathf.Clamp01(time / RampInSeconds);
                float fadeOut = Mathf.Clamp01((playSeconds - time) / RampOutSeconds);
                float phase = 2f * Mathf.PI * frequency * time;
                SetAngle(maxDegrees * ToyMoves.Smooth(Mathf.Min(fadeIn, fadeOut)) * Mathf.Sin(phase));

                int extreme = Mathf.FloorToInt((phase - Mathf.PI * 0.5f) / Mathf.PI);
                if (extreme != lastExtreme)
                {
                    if (extreme >= 0 && time < playSeconds - RampOutSeconds) ctx.Cue("toys.seesaw_thump");
                    lastExtreme = extreme;
                }

                time += Time.deltaTime;
                yield return null;
            }

            if (!ctx.SkipRequested) SetAngle(0f);
            else StartCoroutine(EaseToRest(0.5f));

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
