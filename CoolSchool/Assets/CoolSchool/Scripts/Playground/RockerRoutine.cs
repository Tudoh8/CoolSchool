using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The spring riders (both rockers): hop onto the seat, sit, and rock forward and back about the top of the spring, with a little up
    /// and down bob and a "boing" on every swing. The builder finds the body (or uses the whole toy) and the seat. The rocking pivot sits at
    /// the spring top and the seat is a child of it, so the kid rocks with the animal.
    /// </summary>
    public class RockerRoutine : ToyRoutine
    {
        [Tooltip("The part that rocks. Its position is the top of the spring.")]
        [SerializeField] Transform pivot;
        [Tooltip("The anchor the kid rides on. A child of the pivot.")]
        [SerializeField] Transform seat;
        [Tooltip("The axis the pivot rocks about, in the pivot's own space (across the animal).")]
        [SerializeField] Vector3 rockAxis = Vector3.right;
        [SerializeField] float amplitudeDegrees = 14f;
        [SerializeField] float cyclesPerSecond = 1.1f;
        [SerializeField] float bobMeters = 0.03f;
        [SerializeField] float rockSeconds = 6f;

        Quaternion restRotation = Quaternion.identity;
        Vector3 restPosition;
        bool restKnown;

        void Awake()
        {
            RememberRest();
        }

        void RememberRest()
        {
            if (restKnown || pivot == null) return;

            restRotation = pivot.localRotation;
            restPosition = pivot.localPosition;
            restKnown = true;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            RememberRest();
            if (pivot == null || seat == null)
            {
                yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);
                yield break;
            }

            yield return ctx.HopOnto(seat);
            ctx.SetPose(ToyPose.SitSeat);
            ctx.MarkPlayed();
            ctx.Say("toys.whee", 0.5f);

            float lastBoing = -1f;
            for (float t = 0f; t < rockSeconds && !ctx.SkipRequested; t += Time.deltaTime)
            {
                // Eases in over the first half second and out over the last half second.
                float strength = Mathf.Min(Smooth(t / 0.5f), Smooth((rockSeconds - t) / 0.5f));
                float phase = t * cyclesPerSecond;
                Apply(Mathf.Sin(phase * Mathf.PI * 2f) * strength, strength);

                // A boing every time the animal tips all the way forward.
                float cycle = Mathf.Floor(phase + 0.25f);
                if (cycle > lastBoing && t > 0.2f)
                {
                    lastBoing = cycle;
                    ctx.Cue("toys.spring_boing");
                }
                yield return null;
            }

            yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);
        }

        /// <summary>Tilts the animal: swing -1..1 is full back to full forward. Also used by the preview shots.</summary>
        public void Apply(float swing, float strength = 1f)
        {
            if (pivot == null) return;
            RememberRest();

            pivot.localRotation = restRotation * Quaternion.AngleAxis(swing * amplitudeDegrees, rockAxis);
            // The seat is lowest at both ends of the swing and highest in the middle (twice per cycle).
            pivot.localPosition = restPosition + Vector3.up * (bobMeters * strength * (1f - Mathf.Abs(swing)));
        }

        public override void SnapToRest()
        {
            if (pivot == null || !restKnown) return;

            pivot.localRotation = restRotation;
            pivot.localPosition = restPosition;
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            if (pivot == null || !restKnown) yield break;

            Quaternion from = pivot.localRotation;
            Vector3 fromPosition = pivot.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Smooth(t / seconds);
                pivot.localRotation = Quaternion.Slerp(from, restRotation, k);
                pivot.localPosition = Vector3.Lerp(fromPosition, restPosition, k);
                yield return null;
            }
            SnapToRest();
        }

        void OnDisable()
        {
            SnapToRest();
        }
    }
}
