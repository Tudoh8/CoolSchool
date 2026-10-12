using System.Collections;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A green puddle where the baby alien is stuck, with blobs of slime around it (3, 4 or 5, by level). Every blob pops
    /// with one zap. When the last one is gone the puddle shrinks away and the baby is free.
    /// </summary>
    public class SlimeTrap : MonoBehaviour
    {
        [Tooltip("The flat green puddle. Optional: a child called Puddle is used when empty.")]
        [SerializeField] GameObject puddle;
        [Tooltip("Where the baby stands, in the middle of the puddle. Optional: a child called BabySpot, else this object.")]
        [SerializeField] Transform babySpot;
        [Tooltip("The pool of blobs (5). Optional: the SlimeBlobs below this object are used when empty.")]
        [SerializeField] SlimeBlob[] blobs;
        [Tooltip("How far from the baby the blobs sit.")]
        [SerializeField] float blobRingRadius = 1.6f;
        [Tooltip("Height of a blob's middle above the ground.")]
        [SerializeField] float blobHeight = 0.45f;

        const float ShrinkSeconds = 0.8f;
        const float PopGapSeconds = 0.6f;

        Vector3 puddleBaseScale = Vector3.one;
        bool puddleScaleCached;
        int remaining;
        bool shown;

        /// <summary>Where the baby stands.</summary>
        public Transform BabySpot
        {
            get
            {
                CacheReferences();
                return babySpot;
            }
        }

        /// <summary>The middle of the puddle on the ground.</summary>
        public Vector3 Position => BabySpot.position;

        public int Remaining => remaining;

        /// <summary>True when the trap was shown and every blob has popped.</summary>
        public bool IsCleared => shown && remaining <= 0;

        void Awake() => CacheReferences();

        void CacheReferences()
        {
            if (babySpot == null)
            {
                babySpot = transform.Find("BabySpot");
                if (babySpot == null) babySpot = transform;
            }

            if (puddle == null)
            {
                Transform found = transform.Find("Puddle");
                if (found != null) puddle = found.gameObject;
            }

            if (blobs == null || blobs.Length == 0) blobs = GetComponentsInChildren<SlimeBlob>(true);

            if (!puddleScaleCached && puddle != null)
            {
                puddleBaseScale = puddle.transform.localScale;
                puddleScaleCached = true;
            }
        }

        /// <summary>The puddle appears with this many blobs around the baby (3 to 5, never more than the pool).</summary>
        public void Show(int blobCount)
        {
            CacheReferences();
            ResetTrap();

            int count = Mathf.Clamp(blobCount, 1, blobs.Length);
            gameObject.SetActive(true);
            if (puddle != null)
            {
                puddle.transform.localScale = puddleBaseScale;
                puddle.SetActive(true);
            }

            Vector3 middle = babySpot.position;
            float firstAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                SlimeBlob blob = blobs[i];
                if (blob == null) continue;

                float angle = (firstAngle + 360f * i / count) * Mathf.Deg2Rad;
                Vector3 position = middle + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * blobRingRadius;
                position.y = middle.y + blobHeight;

                blob.Popped += OnBlobPopped;
                blob.Show(position);
                remaining++;
            }

            shown = true;
            BattleEffects.Sparkle(middle + Vector3.up * 0.3f);
        }

        void OnBlobPopped(SlimeBlob blob)
        {
            blob.Popped -= OnBlobPopped;
            remaining = Mathf.Max(0, remaining - 1);
        }

        /// <summary>The puddle shrinks away to nothing (after the last blob has popped).</summary>
        public IEnumerator Shrink()
        {
            if (puddle == null || !puddle.activeSelf) yield break;

            Transform puddleTransform = puddle.transform;
            Vector3 from = puddleTransform.localScale;
            for (float elapsed = 0f; elapsed < ShrinkSeconds; elapsed += Time.deltaTime)
            {
                puddleTransform.localScale = Vector3.Lerp(from, Vector3.zero, Mathf.SmoothStep(0f, 1f, elapsed / ShrinkSeconds));
                yield return null;
            }

            puddle.SetActive(false);
            puddleTransform.localScale = puddleBaseScale;
            BattleEffects.Sparkle(babySpot.position + Vector3.up * 0.3f);
        }

        /// <summary>The safety net: the blobs that are left pop one by one by themselves.</summary>
        public IEnumerator PopRemaining()
        {
            while (remaining > 0)
            {
                SlimeBlob next = null;
                foreach (SlimeBlob blob in blobs)
                {
                    if (blob != null && blob.IsActive)
                    {
                        next = blob;
                        break;
                    }
                }

                if (next == null)
                {
                    // Nothing left to pop (a blob was switched off some other way).
                    remaining = 0;
                    yield break;
                }

                next.PopByItself();
                yield return new WaitForSeconds(PopGapSeconds);
            }
        }

        /// <summary>Back to hidden: no blobs, no puddle.</summary>
        public void ResetTrap()
        {
            CacheReferences();

            if (blobs != null)
            {
                foreach (SlimeBlob blob in blobs)
                {
                    if (blob == null) continue;

                    blob.Popped -= OnBlobPopped;
                    blob.ResetBlob();
                }
            }

            if (puddle != null)
            {
                puddle.transform.localScale = puddleBaseScale;
                puddle.SetActive(false);
            }

            remaining = 0;
            shown = false;
        }
    }
}
