using System.Collections;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Castle
{
    /// <summary>
    /// Small helpers the castle moments share: waiting for the voice, hops, arcs and pops of 3D props, and measuring a prop.
    /// All motion is by transform, with no physics and no colliders, so nothing ever blocks the child or the NavMesh.
    /// </summary>
    public static class CastleMotion
    {
        /// <summary>Starts a coroutine on this component when it is active (a helper animation that runs next to the main wait loop).</summary>
        public static Coroutine Fork(this MonoBehaviour owner, IEnumerator routine)
        {
            return owner != null && owner.isActiveAndEnabled ? owner.StartCoroutine(routine) : null;
        }

        /// <summary>Waits until the voice has finished the line(s) asked for (and its queue), but never longer than maxSeconds.</summary>
        public static IEnumerator WaitForVoice(float maxSeconds = 6f)
        {
            // One frame, so a line asked for just now is already in the player.
            yield return null;

            float end = Time.time + maxSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsBusy && Time.time < end)
                yield return null;
        }

        /// <summary>Says a line and waits for it. Kid, goblin and alien lines are dropped while the teacher talks, so lines go one at a time.</summary>
        public static IEnumerator SayAndWait(string group, bool interrupt = false, float maxSeconds = 6f)
        {
            VoicePlayer.Say(group, interrupt);
            yield return WaitForVoice(maxSeconds);
        }

        /// <summary>True when nobody is talking right now (so a hint can be said without piling up).</summary>
        public static bool VoiceIsFree()
        {
            return VoicePlayer.Instance == null || !VoicePlayer.Instance.IsBusy;
        }

        /// <summary>The voice group of a letter's sound, with the fallback used everywhere else.</summary>
        public static string SoundKeyOf(LetterEntry entry)
        {
            return entry == null ? string.Empty : string.IsNullOrEmpty(entry.soundKey) ? "letter.sound." + entry.letter : entry.soundKey;
        }

        // ----- Moving props ----------------------------------------------------------------------

        /// <summary>Flies a prop to a world point along a high arc.</summary>
        public static IEnumerator Arc(Transform thing, Vector3 to, float seconds, float height)
        {
            if (thing == null) yield break;

            Vector3 from = thing.position;
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float k = elapsed / duration;
                thing.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * (LessonMotion.Hump(k) * height);
                yield return null;
            }

            if (thing != null) thing.position = to;
        }

        /// <summary>Straight, smooth slide to a world point.</summary>
        public static IEnumerator Slide(Transform thing, Vector3 to, float seconds)
        {
            return Arc(thing, to, seconds, 0f);
        }

        /// <summary>A hop on the spot: up and down once.</summary>
        public static IEnumerator Hop(Transform thing, float height, float seconds)
        {
            if (thing == null) yield break;

            Vector3 start = thing.position;
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                thing.position = start + Vector3.up * (LessonMotion.Hump(elapsed / duration) * height);
                yield return null;
            }

            if (thing != null) thing.position = start;
        }

        /// <summary>Scales a prop from nothing up to its size with a little overshoot, like a toy popping into place.</summary>
        public static IEnumerator PopIn(Transform thing, Vector3 finalScale, float seconds)
        {
            if (thing == null) yield break;

            float duration = Mathf.Max(0.05f, seconds);
            thing.localScale = Vector3.zero;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                thing.localScale = finalScale * LessonMotion.OutBack(elapsed / duration);
                yield return null;
            }

            if (thing != null) thing.localScale = finalScale;
        }

        /// <summary>Shrinks a prop to nothing.</summary>
        public static IEnumerator PopOut(Transform thing, float seconds)
        {
            if (thing == null) yield break;

            Vector3 start = thing.localScale;
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                thing.localScale = start * (1f - Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            if (thing != null) thing.localScale = Vector3.zero;
        }

        /// <summary>A friendly sideways wobble that dies away: a wrong pick, never a buzzer.</summary>
        public static IEnumerator Wobble(Transform thing, float seconds = 0.5f, float degrees = 18f)
        {
            if (thing == null) yield break;

            Quaternion start = thing.localRotation;
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float angle = LessonMotion.DampedWobble(elapsed / duration, 3f) * degrees;
                thing.localRotation = start * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            if (thing != null) thing.localRotation = start;
        }

        /// <summary>The prop turns once round (about the up axis) while it hops, for a right answer.</summary>
        public static IEnumerator SpinHop(Transform thing, float height, float seconds)
        {
            if (thing == null) yield break;

            Vector3 start = thing.position;
            Quaternion startRotation = thing.rotation;
            float duration = Mathf.Max(0.05f, seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                float k = elapsed / duration;
                thing.position = start + Vector3.up * (LessonMotion.Hump(k) * height);
                thing.rotation = Quaternion.AngleAxis(Mathf.SmoothStep(0f, 360f, k), Vector3.up) * startRotation;
                yield return null;
            }

            if (thing != null)
            {
                thing.position = start;
                thing.rotation = startRotation;
            }
        }

        /// <summary>Turns a transform on the spot to look along a flat direction, at this many degrees a second.</summary>
        public static IEnumerator TurnTo(Transform thing, Vector3 flatDirection, float degreesPerSecond = 540f, float maxSeconds = 1.5f)
        {
            flatDirection.y = 0f;
            if (thing == null || flatDirection.sqrMagnitude < 0.0001f) yield break;

            Quaternion wanted = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            for (float elapsed = 0f; elapsed < maxSeconds; elapsed += Time.deltaTime)
            {
                if (thing == null) yield break;

                thing.rotation = Quaternion.RotateTowards(thing.rotation, wanted, degreesPerSecond * Time.deltaTime);
                if (Quaternion.Angle(thing.rotation, wanted) < 1f) break;
                yield return null;
            }
        }

        // ----- Measuring -------------------------------------------------------------------------

        /// <summary>The combined bounds of all the pictures under a prop (no particles or trails). False when there are none.</summary>
        public static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (root == null) return false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

                if (found) bounds.Encapsulate(renderer.bounds);
                else bounds = renderer.bounds;
                found = true;
            }
            return found;
        }

        /// <summary>Where a tap should aim: the middle of the prop's pictures, or its own position.</summary>
        public static Vector3 CenterOf(Transform thing)
        {
            if (thing == null) return Vector3.zero;

            return TryGetBounds(thing.gameObject, out Bounds bounds) ? bounds.center : thing.position;
        }

        /// <summary>The top of a prop: the highest point of its pictures.</summary>
        public static Vector3 TopOf(Transform thing)
        {
            if (thing == null) return Vector3.zero;

            return TryGetBounds(thing.gameObject, out Bounds bounds) ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : thing.position;
        }

        /// <summary>
        /// Scales a prop so that its biggest side is "side" metres, then stands it on a point (its lowest point on the point's
        /// height, its middle over the point). Pivots in the packs are not reliable, so it is placed by its bounds.
        /// </summary>
        public static void FitAndStand(Transform prop, Vector3 point, float side)
        {
            if (prop == null) return;

            if (TryGetBounds(prop.gameObject, out Bounds bounds))
            {
                float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (biggest > 0.0001f) prop.localScale *= side / biggest;
            }

            if (TryGetBounds(prop.gameObject, out bounds))
                prop.position += new Vector3(point.x - bounds.center.x, point.y - bounds.min.y, point.z - bounds.center.z);
            else
                prop.position = point;
        }

        /// <summary>Removes colliders from a spawned prop, so it can never block the child or the NavMesh.</summary>
        public static void StripColliders(GameObject root)
        {
            if (root == null) return;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }
        }

        /// <summary>Flat (ground) distance between two points.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
