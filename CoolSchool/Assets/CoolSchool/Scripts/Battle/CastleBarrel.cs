using System.Collections;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A wooden barrel in the courtyard where the tiny goblin hides. It wobbles now and then, and when the goblin is
    /// found it tips over (opening toward the camera) so the goblin can hop out. It has no collider. ResetBarrel stands it
    /// up again, so the castle is the same for the next visit.
    /// </summary>
    public class CastleBarrel : MonoBehaviour
    {
        const float WobbleSeconds = 0.55f;
        const float WobbleDegrees = 7f;
        const float TipSeconds = 0.3f;
        const float TipDegrees = 80f;
        const float DefaultLift = 0.3f;
        const float DefaultHopOutDistance = 1.3f;

        [Tooltip("Where the goblin sits inside the barrel (low, so only its head shows when it peeks). Optional: a spot just above the barrel's base is used when empty.")]
        [SerializeField] Transform inside;
        [Tooltip("Where the goblin lands on the floor when it hops out, on the NavMesh. Optional: a spot toward the camera is used when empty.")]
        [SerializeField] Transform hopOutPoint;

        Vector3 homePosition;
        Quaternion homeRotation;
        float homeMinY;
        bool homeCached;
        Coroutine motion;
        Transform fallbackInside;

        /// <summary>Where the hidden goblin sits.</summary>
        public Transform Inside
        {
            get
            {
                if (inside != null) return inside;

                if (fallbackInside == null)
                {
                    var spot = new GameObject("Inside").transform;
                    spot.SetParent(transform, false);
                    spot.localPosition = new Vector3(0f, 0.3f, 0f);
                    fallbackInside = spot;
                }
                return fallbackInside;
            }
        }

        /// <summary>Where the goblin hops out to (the barrel's upright place, so it does not move when the barrel tips).</summary>
        public Vector3 HopOutPoint
        {
            get
            {
                CacheHome();
                if (hopOutPoint != null) return hopOutPoint.position;
                return new Vector3(homePosition.x, homePosition.y, homePosition.z - DefaultHopOutDistance);
            }
        }

        void Awake() => CacheHome();

        void OnDisable()
        {
            motion = null;
        }

        /// <summary>Remembers the upright pose the first time (before anything moved).</summary>
        void CacheHome()
        {
            if (homeCached) return;

            homeCached = true;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeMinY = CurrentMinY(homePosition.y);
        }

        /// <summary>A short shake, as if something inside bumped it.</summary>
        public void Wobble()
        {
            CacheHome();
            if (!isActiveAndEnabled) return;

            StopMotion();
            Sfx.Play("castle.barrel_wobble", transform.position);
            motion = StartCoroutine(WobbleRoutine());
        }

        /// <summary>Tips over (80 degrees in 0.3 s) with the opening toward the camera.</summary>
        public void TipOver()
        {
            CacheHome();
            if (!isActiveAndEnabled) return;

            StopMotion();
            Sfx.Play("castle.barrel_tip", transform.position);
            motion = StartCoroutine(TipRoutine());
        }

        /// <summary>Stands the barrel up again where it was.</summary>
        public void ResetBarrel()
        {
            CacheHome();
            StopMotion();
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        void StopMotion()
        {
            if (motion != null) StopCoroutine(motion);
            motion = null;
        }

        IEnumerator WobbleRoutine()
        {
            for (float elapsed = 0f; elapsed < WobbleSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / WobbleSeconds;
                float decay = 1f - t;
                float angle = Mathf.Sin(t * Mathf.PI * 6f) * WobbleDegrees * decay;
                transform.rotation = homeRotation * Quaternion.Euler(angle * 0.5f, 0f, angle);
                yield return null;
            }

            transform.SetPositionAndRotation(homePosition, homeRotation);
            motion = null;
        }

        IEnumerator TipRoutine()
        {
            Quaternion from = transform.rotation;
            Vector3 fromPosition = transform.position;
            Quaternion to = Quaternion.AngleAxis(-TipDegrees, Vector3.right) * homeRotation;

            // Measure how far the lying barrel would sink into the floor, and lift it by that much.
            transform.rotation = to;
            float lift = homeMinY - CurrentMinY(fromPosition.y);
            transform.rotation = from;
            if (float.IsNaN(lift) || lift < 0f || lift > 1f) lift = DefaultLift;

            Vector3 toPosition = homePosition + Vector3.up * lift;
            for (float elapsed = 0f; elapsed < TipSeconds; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / TipSeconds);
                transform.rotation = Quaternion.Slerp(from, to, t);
                transform.position = Vector3.Lerp(fromPosition, toPosition, t);
                yield return null;
            }

            transform.SetPositionAndRotation(toPosition, to);
            motion = null;
        }

        /// <summary>The lowest point of the barrel's meshes, or the fallback when it has none.</summary>
        float CurrentMinY(float fallback)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool any = false;
            float min = float.MaxValue;
            foreach (Renderer part in renderers)
            {
                if (part == null || !part.enabled) continue;

                // Skip the goblin's own meshes if it happens to be parented here.
                if (part.GetComponentInParent<BattleOpponentAI>() != null) continue;

                any = true;
                min = Mathf.Min(min, part.bounds.min.y);
            }
            return any ? min : fallback;
        }
    }
}
