using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The roundabout: hop onto the edge of the disc facing along the turn, sit, spin up to one turn per 2.2 s, hold, slow down (always
    /// stopping at a whole number of turns, so the disc ends where it began), hop off, and get dizzy. The builder turns the whole mesh
    /// about its middle (the spinner) and puts the seat anchor on the disc as a child of the spinner.
    /// </summary>
    public class RoundaboutRoutine : ToyRoutine
    {
        [Tooltip("The part that turns about its own vertical axis.")]
        [SerializeField] Transform spinner;
        [Tooltip("The anchor the kid rides on, on the disc as a child of the spinner; its forward is along the turn.")]
        [SerializeField] Transform seat;
        [Tooltip("The pose to sit in. SitSlide (on the floor of a flat disc) or SitSeat (on a bar or seat).")]
        [SerializeField] ToyPose sitPose = ToyPose.SitSlide;
        [SerializeField] float secondsPerTurn = 2.2f;
        [SerializeField] float spinUpSeconds = 1.6f;
        [Tooltip("The shortest time at full speed. The real time is worked out so the disc stops at a whole number of turns.")]
        [SerializeField] float holdSeconds = 1.5f;
        [SerializeField] float minSlowDownSeconds = 1.8f;

        Quaternion restRotation = Quaternion.identity;
        bool restKnown;
        float angle;      // degrees turned since the start, in the direction of the turn
        float omega;      // degrees per second now

        float MaxOmega => 360f / Mathf.Max(0.5f, secondsPerTurn);

        void Awake()
        {
            RememberRest();
        }

        void RememberRest()
        {
            if (restKnown || spinner == null) return;

            restRotation = spinner.localRotation;
            restKnown = true;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            RememberRest();
            if (spinner == null || seat == null)
            {
                yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);
                yield break;
            }

            angle = 0f;
            omega = 0f;
            ctx.CheerOnLand = false;

            yield return ctx.HopOnto(seat);
            ctx.SetPose(sitPose);
            ctx.MarkPlayed();
            ctx.Say("toys.whee", 0.5f);
            ctx.Cue("toys.spin");

            // Spin up (the angle grows with time squared), then hold.
            float max = MaxOmega;
            for (float t = 0f; t < spinUpSeconds && !ctx.SkipRequested; t += Time.deltaTime)
            {
                omega = max * Mathf.Clamp01(t / spinUpSeconds);
                angle += omega * Time.deltaTime;
                Apply();
                yield return null;
            }
            // Plan the hold so spin-up + hold + braking add up to a whole number of turns (about 6 s in all).
            float upAngle = max * spinUpSeconds * 0.5f;
            float brakeAngle = max * minSlowDownSeconds * 0.5f;
            int turns = 1;
            float hold = (turns * 360f - upAngle - brakeAngle) / max;
            while (hold < holdSeconds && turns < 8)
            {
                turns++;
                hold = (turns * 360f - upAngle - brakeAngle) / max;
            }
            for (float t = 0f; t < hold && !ctx.SkipRequested; t += Time.deltaTime)
            {
                omega = max;
                angle += omega * Time.deltaTime;
                Apply();
                yield return null;
            }

            // Slow down to a stop at a whole number of turns, while he is still on it. (A skip hops him off first; the disc then
            // slows down by itself in EaseToRest.)
            if (!ctx.SkipRequested)
                yield return SlowDown(ctx, minSlowDownSeconds);

            bool skipped = ctx.SkipRequested;
            yield return ctx.HopOff(ExitPoint != null ? ExitPoint.position : ctx.Player.transform.position);

            // Dizzy after the ride (not when he skipped).
            if (!skipped && ctx.Kid != null)
            {
                ctx.Kid.PlayHit();
                ctx.Say("toys.dizzy");
                yield return ctx.Wait(1.1f);
            }
        }

        /// <summary>
        /// Brakes evenly from the present speed to a stop exactly at the next whole turn that is at least minSeconds of braking away.
        /// With a skip, the braking carries on (it does not check for one).
        /// </summary>
        IEnumerator SlowDown(ToyPlayContext ctx, float minSeconds)
        {
            if (omega < 1f)
            {
                omega = 0f;
                yield break;
            }

            // Constant braking from omega to 0 turns the disc by omega * T / 2; make that land on a whole turn.
            float needed = omega * minSeconds * 0.5f;
            float target = Mathf.Ceil((angle + needed) / 360f - 0.02f) * 360f;
            float distance = target - angle;
            float seconds = 2f * distance / omega;
            float startAngle = angle;
            float startOmega = omega;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                angle = startAngle + distance * (2f * k - k * k);
                omega = startOmega * (1f - k);
                Apply();
                yield return null;
            }
            angle = target;
            omega = 0f;
            Apply();
        }

        /// <summary>Slows the disc down by itself after the kid has hopped off, stopping at a whole turn.</summary>
        public override IEnumerator EaseToRest(float seconds)
        {
            if (spinner == null || !restKnown) yield break;

            if (omega > 1f) yield return SlowDown(null, 0.7f);
            SnapToRest();
        }

        void Apply()
        {
            if (spinner == null) return;
            spinner.localRotation = restRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override void SnapToRest()
        {
            angle = 0f;
            omega = 0f;
            if (spinner != null && restKnown) spinner.localRotation = restRotation;
        }

        void OnDisable()
        {
            SnapToRest();
        }
    }
}
