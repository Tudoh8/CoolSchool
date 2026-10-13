using System.Collections;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// An upside-down cup (or pot) for the "how many are hiding?" game. It drops onto the table with a little bounce,
    /// lifts to show what is under it, and can be switched off at any moment. The setup builder makes the model,
    /// sizes it to about 0.35 m wide and flips it upside down; this class only moves it.
    /// </summary>
    public class HidingCup : MonoBehaviour
    {
        [Tooltip("How high the cup is when it starts to fall onto the table, in metres.")]
        [SerializeField] float dropHeight = 0.7f;
        [Tooltip("How far the cup rises when it is lifted, in metres.")]
        [SerializeField] float liftHeight = 0.5f;

        const float FallSeconds = 0.28f;
        const float BounceSeconds = 0.2f;
        const float BounceHeight = 0.05f;
        const float LiftSeconds = 0.35f;

        /// <summary>The spot on the table under the cup, where hidden toys go.</summary>
        public Vector3 UnderPoint { get; private set; }

        /// <summary>True while the cup is up in the air after a Lift.</summary>
        public bool IsLifted { get; private set; }

        Vector3 restPosition;

        /// <summary>Puts the cup on the table at this point at once, without the drop. The editor's preview pictures use it.</summary>
        public void PlaceAt(Vector3 tablePoint)
        {
            UnderPoint = tablePoint;
            IsLifted = false;
            gameObject.SetActive(true);

            // Pivots can't be trusted: stand the cup on the table by its bounds, centred over the point.
            transform.position = tablePoint;
            restPosition = tablePoint;
            if (TryGetBounds(out Bounds bounds))
            {
                restPosition += new Vector3(tablePoint.x - bounds.center.x, tablePoint.y - bounds.min.y, tablePoint.z - bounds.center.z);
            }
            transform.position = restPosition;
        }

        /// <summary>Drops the cup onto the table at this point (from above, with a little bounce). Run it as part of a coroutine.</summary>
        public IEnumerator CoverAt(Vector3 tablePoint)
        {
            PlaceAt(tablePoint);

            Vector3 start = restPosition + Vector3.up * dropHeight;
            transform.position = start;
            for (float elapsed = 0f; elapsed < FallSeconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / FallSeconds;
                transform.position = Vector3.Lerp(start, restPosition, k * k);
                yield return null;
            }

            Sfx.Play("lesson.toy_into_basket", restPosition);
            for (float elapsed = 0f; elapsed < BounceSeconds; elapsed += Time.deltaTime)
            {
                transform.position = restPosition + Vector3.up * (LessonMotion.Hump(elapsed / BounceSeconds) * BounceHeight);
                yield return null;
            }

            transform.position = restPosition;
        }

        /// <summary>Lifts the cup straight up so what is under it can be seen. Run it as part of a coroutine.</summary>
        public IEnumerator Lift()
        {
            if (!IsLifted) Sfx.Play("lesson.toy_pop", restPosition);
            IsLifted = true;

            Vector3 from = transform.position;
            Vector3 to = restPosition + Vector3.up * liftHeight;
            for (float elapsed = 0f; elapsed < LiftSeconds; elapsed += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / LiftSeconds));
                yield return null;
            }

            transform.position = to;
        }

        /// <summary>Switches the cup off. Safe to call at any moment, also when it is not shown.</summary>
        public void Hide()
        {
            StopAllCoroutines();
            IsLifted = false;
            gameObject.SetActive(false);
        }

        bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(false))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

                if (!found) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                found = true;
            }
            return found;
        }
    }
}
