using System.Collections;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The thing a thief goblin steals in the castle adventure: the cake slice, the golden crown or the dragon egg.
    /// The goblin grabs it (it sits in the goblin's hand, a bit smaller), and when the goblin is caught it flies back
    /// to where it was in an arc with a sparkle. It has no colliders and never blocks anything.
    /// </summary>
    public class MissionLoot : MonoBehaviour
    {
        const float HandScale = 0.6f;
        const float ReturnSeconds = 0.8f;
        const float ReturnArcHeight = 1.4f;
        static readonly Vector3 HandOffset = new Vector3(0f, 0.05f, 0.05f);

        Transform homeParent;
        Vector3 homeLocalPosition;
        Quaternion homeLocalRotation;
        Vector3 homeLocalScale = Vector3.one;
        Vector3 homeWorldScale = Vector3.one;
        bool homeCached;
        Coroutine returning;

        /// <summary>True when it lies at its own place (not carried, not flying back).</summary>
        public bool IsHome { get; private set; } = true;

        void Awake() => CacheHome();

        void OnDisable()
        {
            returning = null;
        }

        /// <summary>Remembers where it belongs, the first time (before a thief ever touched it).</summary>
        void CacheHome()
        {
            if (homeCached) return;

            homeCached = true;
            homeParent = transform.parent;
            homeLocalPosition = transform.localPosition;
            homeLocalRotation = transform.localRotation;
            homeLocalScale = transform.localScale;
            homeWorldScale = transform.lossyScale;
        }

        /// <summary>Picks it up: it hangs in the hand a bit smaller.</summary>
        public void Grab(Transform hand)
        {
            CacheHome();
            if (hand == null) return;

            StopReturn();
            IsHome = false;

            Vector3 worldScale = homeWorldScale;
            transform.SetParent(hand, false);
            transform.localPosition = HandOffset;
            transform.localRotation = Quaternion.identity;

            // Keep the size the loot has in the world (a goblin's hand bone may be scaled), but a bit smaller.
            Vector3 handScale = hand.lossyScale;
            transform.localScale = new Vector3(
                SafeDivide(worldScale.x * HandScale, handScale.x),
                SafeDivide(worldScale.y * HandScale, handScale.y),
                SafeDivide(worldScale.z * HandScale, handScale.z));
        }

        /// <summary>Lets go and arcs back to its place over 0.8 s, with a sparkle when it lands.</summary>
        public void ReturnHome()
        {
            CacheHome();
            if (IsHome && returning == null) return;

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                ResetLoot();
                return;
            }

            StopReturn();
            returning = StartCoroutine(ReturnRoutine());
        }

        /// <summary>Home at once, whatever it was doing.</summary>
        public void ResetLoot()
        {
            CacheHome();
            StopReturn();
            PutHome();
        }

        void StopReturn()
        {
            if (returning != null) StopCoroutine(returning);
            returning = null;
        }

        void PutHome()
        {
            transform.SetParent(homeParent, false);
            transform.localPosition = homeLocalPosition;
            transform.localRotation = homeLocalRotation;
            transform.localScale = homeLocalScale;
            IsHome = true;
        }

        IEnumerator ReturnRoutine()
        {
            // Let go where it is, then fly to where it belongs.
            transform.SetParent(homeParent, true);
            Vector3 start = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startLocalScale = transform.localScale;

            Vector3 end = homeParent != null ? homeParent.TransformPoint(homeLocalPosition) : homeLocalPosition;
            Quaternion endRotation = homeParent != null ? homeParent.rotation * homeLocalRotation : homeLocalRotation;

            for (float elapsed = 0f; elapsed < ReturnSeconds; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / ReturnSeconds);
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * ReturnArcHeight;
                transform.position = position;
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                transform.localScale = Vector3.Lerp(startLocalScale, homeLocalScale, t);
                yield return null;
            }

            PutHome();
            returning = null;
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            Sfx.Play("castle.star_ping", transform.position);
        }

        static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
        }
    }
}
