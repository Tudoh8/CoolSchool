using System.Collections;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// Small movements shared by the treasure pieces (chest, coins, lock, trophy). They run inside the caller's coroutine,
    /// so they stop together with the object and nothing is left hanging when the activity ends.
    /// </summary>
    internal static class TreasureMotion
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

                float hump = Mathf.Abs(LessonMotion.DampedWobble(elapsed / duration, 1.5f));
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

        /// <summary>Turns the object's local rotation from one pose to another, with a little overshoot (a lid swinging open).</summary>
        public static IEnumerator Rotate(Transform thing, Quaternion from, Quaternion to, float seconds)
        {
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float k = LessonMotion.OutBack(elapsed / duration);
                thing.localRotation = Quaternion.SlerpUnclamped(from, to, k);
                yield return null;
            }

            if (thing != null) thing.localRotation = to;
        }

        /// <summary>All renderers below the object, put together. False when there are none.</summary>
        public static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(root.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }
    }
}
