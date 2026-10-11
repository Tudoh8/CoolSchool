using System.Collections;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// Small movements shared by the scoops, the cone and the coins. They run inside the caller's coroutine,
    /// so they stop together with the shop activity and nothing is left hanging when it ends.
    /// </summary>
    public static class ShopMotion
    {
        /// <summary>Flies the object to a point in an arc (the arc is how high the middle of the flight is above the straight line).</summary>
        public static IEnumerator Fly(Transform thing, Vector3 target, float seconds, float arcHeight)
        {
            if (thing == null) yield break;

            Vector3 start = thing.position;
            float duration = Mathf.Max(0.05f, seconds);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                thing.position = Vector3.Lerp(start, target, k) + Vector3.up * (LessonMotion.Hump(k) * arcHeight);
                yield return null;
            }

            if (thing != null) thing.position = target;
        }

        /// <summary>A scale punch: bigger (or flatter) for a moment and back, like a toy being squeezed.</summary>
        public static IEnumerator Punch(Transform thing, Vector3 baseScale, Vector3 punchFactor, float seconds)
        {
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float wobble = LessonMotion.DampedWobble(elapsed / duration, 1.5f);
                float hump = Mathf.Abs(wobble);
                thing.localScale = Vector3.Scale(baseScale, Vector3.Lerp(Vector3.one, punchFactor, hump));
                yield return null;
            }

            if (thing != null) thing.localScale = baseScale;
        }

        /// <summary>Grows from nothing with a little overshoot, like a bubble popping into being.</summary>
        public static IEnumerator PopIn(Transform thing, Vector3 targetScale, float seconds)
        {
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                thing.localScale = targetScale * LessonMotion.OutBack(elapsed / duration);
                yield return null;
            }

            if (thing != null) thing.localScale = targetScale;
        }
    }
}
