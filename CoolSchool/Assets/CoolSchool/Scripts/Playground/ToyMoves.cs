using System;
using System.Collections;
using UnityEngine;

namespace CoolSchool.Playground
{
    /// <summary>
    /// Small motion helpers shared by the routines of the monkey bars, seesaw, swings, fort, tyre wall, track ride and ship
    /// (everything that "does a thing over some seconds, and stops at once when he pushes the stick").
    /// </summary>
    internal static class ToyMoves
    {
        /// <summary>
        /// Calls step(0..1) every frame for the given seconds, ending with step(1). Stops early (without the last step) when the routine
        /// is skipped, so the caller can go straight to the hop off.
        /// </summary>
        public static IEnumerator Over(ToyPlayContext ctx, float seconds, Action<float> step)
        {
            if (seconds <= 0.0001f)
            {
                step(1f);
                yield break;
            }

            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (ctx.SkipRequested) yield break;
                step(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            if (!ctx.SkipRequested) step(1f);
        }

        /// <summary>The same as Over, but runs even when skipped (for eases back to rest, where nobody is asked).</summary>
        public static IEnumerator OverAlways(float seconds, Action<float> step)
        {
            if (seconds <= 0.0001f)
            {
                step(1f);
                yield break;
            }

            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                step(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            step(1f);
        }

        public static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));

        /// <summary>A straight line from a to b with a parabola of the given height added on top (a hop).</summary>
        public static Vector3 Arc(Vector3 a, Vector3 b, float height, float k)
        {
            Vector3 point = Vector3.Lerp(a, b, k);
            point.y += 4f * height * k * (1f - k);
            return point;
        }

        /// <summary>A rotation that faces the direction on the flat; the fallback when it has no length.</summary>
        public static Quaternion FlatLook(Vector3 direction, Quaternion fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? fallback : Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>A transform's point or its own position when there is none.</summary>
        public static Vector3 PositionOf(Transform t, Vector3 fallback)
        {
            return t != null ? t.position : fallback;
        }
    }
}
