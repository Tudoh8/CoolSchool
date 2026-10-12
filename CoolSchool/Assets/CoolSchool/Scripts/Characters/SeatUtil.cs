using System;
using System.Collections;
using UnityEngine;

namespace CoolSchool.Characters
{
    /// <summary>
    /// Helpers for riding on something that moves (the pirate ship, the skeleton boat): a hop on a little arc to a point
    /// that may be moving, and parenting to a seat without the kid being stretched by a scaled ship.
    /// </summary>
    public static class SeatUtil
    {
        /// <summary>
        /// Hops along an arc to a point that is asked for every frame (so it can sit on a moving ship). Optionally turns to
        /// the wanted rotation on the way. Run it with StartCoroutine on any active object.
        /// </summary>
        public static IEnumerator HopTo(Transform who, Func<Vector3> target, float seconds, float height, Func<Quaternion> faceRotation = null)
        {
            if (who == null || target == null) yield break;

            Vector3 start = who.position;
            Quaternion startRotation = who.rotation;
            float duration = Mathf.Max(0.05f, seconds);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (who == null) yield break;

                float k = elapsed / duration;
                float eased = Mathf.SmoothStep(0f, 1f, k);
                who.position = Vector3.Lerp(start, target(), eased) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * height);
                if (faceRotation != null) who.rotation = Quaternion.Slerp(startRotation, faceRotation(), eased);
                yield return null;
            }

            if (who == null) yield break;
            who.position = target();
            if (faceRotation != null) who.rotation = faceRotation();
        }

        /// <summary>Parents to the seat with the pose zeroed. The size stays the same even when the seat's parent is scaled.</summary>
        public static void Mount(Transform who, Transform seat)
        {
            if (who == null || seat == null) return;

            Vector3 lossy = who.lossyScale;
            who.SetParent(seat, false);
            who.localPosition = Vector3.zero;
            who.localRotation = Quaternion.identity;

            Vector3 seatScale = seat.lossyScale;
            who.localScale = new Vector3(Divide(lossy.x, seatScale.x), Divide(lossy.y, seatScale.y), Divide(lossy.z, seatScale.z));
        }

        /// <summary>Back to the original parent, upright (the ship may be rolling). Keeps the world pose and size.</summary>
        public static void Dismount(Transform who, Transform originalParent)
        {
            if (who == null) return;

            who.SetParent(originalParent, true);
            who.rotation = Quaternion.Euler(0f, who.eulerAngles.y, 0f);
        }

        static float Divide(float value, float by)
        {
            return Mathf.Abs(by) < 0.0001f ? value : value / by;
        }
    }
}
