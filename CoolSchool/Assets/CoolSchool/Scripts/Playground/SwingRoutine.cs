using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// The swings: he hops onto the seat nearest to him, sits, and swings about the top bar, building up from 8 to 35 degrees and back
    /// down over 8 seconds, with a creak at each end and a "whee" near the top. Each swing (seat and chains) is a moving part under a
    /// pivot at the top bar; its anchor (the seat's sitting point) is a child of that pivot.
    /// </summary>
    public class SwingRoutine : ToyRoutine
    {
        const float StartDegrees = 8f;
        const float TopDegrees = 35f;
        const float PlaySeconds = 8f;
        const float FadeOutSeconds = 0.8f;
        const float Frequency = 0.42f;
        const float ExitDistance = 1.25f;

        [Tooltip("One pivot per swing, at the top bar. The seat and chains are children of it.")]
        [SerializeField] Transform[] pivots;
        [Tooltip("The sitting point of each swing (a child of its pivot), in the same order. Its forward is the way he faces.")]
        [SerializeField] Transform[] anchors;
        [Tooltip("The swing axis (along the top bar) in the pivots' own frame.")]
        [SerializeField] Vector3 axis = Vector3.right;

        Quaternion[] restRotations;
        float[] angles;
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
            if (restRotations != null || pivots == null) return;

            restRotations = new Quaternion[pivots.Length];
            angles = new float[pivots.Length];
            for (int i = 0; i < pivots.Length; i++)
                restRotations[i] = pivots[i] != null ? pivots[i].localRotation : Quaternion.identity;
        }

        void SetAngle(int index, float degrees)
        {
            CaptureRest();
            if (pivots == null || index < 0 || index >= pivots.Length || pivots[index] == null) return;

            angles[index] = degrees;
            pivots[index].localRotation = restRotations[index] * Quaternion.AngleAxis(degrees, axis.normalized);
        }

        /// <summary>Swings one seat to an angle at once. For the preview shots.</summary>
        public void Apply(int index, float degrees)
        {
            SetAngle(index, degrees);
        }

        public override void SnapToRest()
        {
            easing = false;
            CaptureRest();
            if (pivots == null) return;
            for (int i = 0; i < pivots.Length; i++) SetAngle(i, 0f);
        }

        public override IEnumerator EaseToRest(float seconds)
        {
            while (easing) yield return null;

            CaptureRest();
            if (pivots == null) yield break;

            easing = true;
            float[] from = (float[])angles.Clone();
            for (float elapsed = 0f; elapsed < seconds && easing; elapsed += Time.deltaTime)
            {
                float k = ToyMoves.Smooth(elapsed / seconds);
                for (int i = 0; i < pivots.Length; i++) SetAngle(i, Mathf.Lerp(from[i], 0f, k));
                yield return null;
            }
            if (easing) for (int i = 0; i < pivots.Length; i++) SetAngle(i, 0f);
            easing = false;
        }

        public override IEnumerator Play(ToyPlayContext ctx)
        {
            if (pivots == null || anchors == null || pivots.Length == 0 || anchors.Length < pivots.Length)
            {
                Debug.LogWarning("CoolSchool toys: the swing routine of " + name + " is not set up.");
                yield break;
            }

            SnapToRest();
            int seat = NearestSeat(ctx);
            Transform anchor = anchors[seat];
            if (anchor == null)
            {
                yield break;
            }

            yield return ctx.HopOnto(anchor);
            if (ctx.SkipRequested)
            {
                yield return Leave(ctx, anchor);
                yield break;
            }

            ctx.SetPose(ToyPose.SitSeat);
            ctx.MarkPlayed();

            float lastSign = 0f;
            bool wheeDone = false;
            float time = 0f;
            while (time < PlaySeconds && !ctx.SkipRequested)
            {
                // Builds up to the top angle in the middle and back down, then eases out so the seat is nearly still at the end.
                float u = time / PlaySeconds;
                float amplitude = StartDegrees + (TopDegrees - StartDegrees) * Mathf.Sin(u * Mathf.PI);
                amplitude *= Mathf.Clamp01((PlaySeconds - time) / FadeOutSeconds);
                float swing = amplitude * Mathf.Sin(2f * Mathf.PI * Frequency * time);
                SetAngle(seat, swing);

                // A creak when the swing turns round at either end.
                float sign = Mathf.Sign(Mathf.Cos(2f * Mathf.PI * Frequency * time));
                if (lastSign != 0f && sign != lastSign && amplitude > StartDegrees * 0.8f) ctx.Cue("toys.swing_creak");
                lastSign = sign;

                if (!wheeDone && amplitude > TopDegrees * 0.85f)
                {
                    wheeDone = true;
                    ctx.Say("toys.whee");
                }

                time += Time.deltaTime;
                yield return null;
            }

            if (!ctx.SkipRequested) SetAngle(seat, 0f);
            else StartCoroutine(EaseToRest(0.5f));

            yield return Leave(ctx, anchor);
        }

        int NearestSeat(ToyPlayContext ctx)
        {
            if (ctx.Player == null) return 0;

            Vector3 here = ctx.Player.transform.position;
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;
                Vector3 delta = anchors[i].position - here;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestDistance)
                {
                    bestDistance = delta.sqrMagnitude;
                    best = i;
                }
            }
            return best;
        }

        IEnumerator Leave(ToyPlayContext ctx, Transform anchor)
        {
            // He hops forward off the seat he used (both swings share one exit point's height, not its place).
            Vector3 here = anchor != null ? anchor.position : transform.position;
            Vector3 forward = anchor != null ? anchor.forward : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            float groundY = exitPoint != null ? exitPoint.position.y : 0f;
            Vector3 exit = new Vector3(here.x, groundY, here.z) + forward * ExitDistance;
            yield return ctx.HopOff(exit);
        }
    }
}
