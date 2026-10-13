using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// Base of every toy routine. Lives on the toy's root, next to PlaygroundToy. A routine moves its anchor (and the kid with it,
    /// since he is parented to it) by changing the anchor's transform over time; the anchor's forward is the way the kid faces.
    /// </summary>
    public abstract class ToyRoutine : MonoBehaviour
    {
        [Tooltip("On the ground, where he lands after getting off.")]
        [SerializeField] protected Transform exitPoint;

        public Transform ExitPoint => exitPoint;

        /// <summary>
        /// Get on, play, get off. Must check ctx.SkipRequested often (use ctx.Wait) and, when it is set, go straight to ctx.HopOff.
        /// Must always end with the kid off the toy (ctx.HopOff), also when skipped.
        /// </summary>
        public abstract IEnumerator Play(ToyPlayContext ctx);

        /// <summary>Puts moving parts back to rest at once (StopNow, OnDisable). Default: nothing.</summary>
        public virtual void SnapToRest() { }

        /// <summary>Eases moving parts back to rest (after a skip or at the end). Default: SnapToRest and finish.</summary>
        public virtual IEnumerator EaseToRest(float seconds)
        {
            SnapToRest();
            yield break;
        }

        // ---- Small helpers for the routines ----

        /// <summary>Eases an angle or distance 0..1 in and out.</summary>
        protected static float Smooth(float k)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        }
    }
}
