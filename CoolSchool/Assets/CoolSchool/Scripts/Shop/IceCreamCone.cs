using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// The waffle cone. Its origin is the bottom tip and the scoops stack upward from the rim, as children,
    /// so everything travels together when the cone is handed over. The shop makes a new one for each customer.
    /// </summary>
    public class IceCreamCone : MonoBehaviour
    {
        [Tooltip("How high the rim of the cone is above its tip.")]
        [SerializeField] float rimHeight = 0.42f;
        [Tooltip("Height of the middle of each next scoop above the one below.")]
        [SerializeField] float scoopSpacing = 0.2f;
        [Tooltip("A see-through ball that shows where the next scoop will land. Off until a scoop is carried.")]
        [SerializeField] GameObject dropHint;

        // A scoop sits a little into the rim, not on top of it.
        const float FirstScoopLift = 0.06f;

        readonly List<ScoopBall> scoops = new List<ScoopBall>();
        Vector3 baseScale = Vector3.one;

        public int ScoopCount => scoops.Count;

        /// <summary>Where the middle of the next scoop will sit, in the world.</summary>
        public Vector3 NextScoopPosition => transform.TransformPoint(ScoopLocalPosition(scoops.Count));

        /// <summary>Where the middle of the scoop with this number (0 is the bottom one) sits, in the world.</summary>
        public Vector3 ScoopPosition(int index) => transform.TransformPoint(ScoopLocalPosition(index));

        Vector3 ScoopLocalPosition(int index)
        {
            return new Vector3(0f, rimHeight + FirstScoopLift + index * scoopSpacing, 0f);
        }

        void Awake()
        {
            baseScale = transform.localScale;
            if (dropHint != null) dropHint.SetActive(false);
        }

        void Update()
        {
            if (dropHint == null || !dropHint.activeSelf) return;

            dropHint.transform.localPosition = ScoopLocalPosition(scoops.Count);
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            dropHint.transform.localScale = Vector3.one * (ScoopBall.Radius * 2f * (1.05f + beat * 0.2f));
        }

        /// <summary>The empty cone pops into the holder.</summary>
        public void PopIn()
        {
            StartCoroutine(ShopMotion.PopIn(transform, baseScale, 0.35f));
        }

        public void SetDropHint(bool on)
        {
            if (dropHint != null) dropHint.SetActive(on);
        }

        /// <summary>Takes the scoop onto the top of the stack. It should be (nearly) at NextScoopPosition already.</summary>
        public void Seat(ScoopBall ball)
        {
            ball.transform.SetParent(transform, true);
            ball.transform.localPosition = ScoopLocalPosition(scoops.Count);
            ball.transform.localRotation = Quaternion.identity;
            scoops.Add(ball);
            ball.Squash();
        }

        /// <summary>The scoop hops once. Used for counting the scoops one by one.</summary>
        public IEnumerator HopScoop(int index)
        {
            if (index < 0 || index >= scoops.Count || scoops[index] == null) yield break;

            yield return scoops[index].Hop(0.1f, 0.35f);
        }
    }
}
