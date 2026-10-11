using System.Collections;
using UnityEngine;

namespace CoolSchool.Shop
{
    /// <summary>
    /// A tub of ice cream on the counter. Pressing it gives a scoop of its flavour. It squashes when it is used,
    /// and can glow and pulse to show the child which tub is the right one.
    /// </summary>
    public class FlavourTub : MonoBehaviour
    {
        [SerializeField] Flavour flavour;
        [Tooltip("The top of the mound: a new scoop appears here and flies back here.")]
        [SerializeField] Transform scoopPoint;
        [Tooltip("The mound of ice cream on the tub. Squashes when a scoop is taken.")]
        [SerializeField] Transform mound;
        [SerializeField] Material scoopMaterial;
        [Tooltip("A glowing disc under the tub, switched on to show it is the right one.")]
        [SerializeField] GameObject glow;

        const float PulseSpeed = 6f;
        const float PulseAmount = 0.07f;

        public Flavour Flavour => flavour;
        public Transform ScoopPoint => scoopPoint != null ? scoopPoint : transform;
        public Material ScoopMaterial => scoopMaterial;
        public bool IsGlowing { get; private set; }

        Vector3 baseScale;
        Vector3 moundBaseScale;
        Coroutine squash;

        void Awake()
        {
            baseScale = transform.localScale;
            if (mound != null) moundBaseScale = mound.localScale;
            if (glow != null) glow.SetActive(false);
        }

        void OnDisable()
        {
            SetGlow(false);
        }

        void Update()
        {
            if (!IsGlowing) return;

            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
            transform.localScale = baseScale * (1f + beat * PulseAmount);
        }

        /// <summary>A quick squash of the mound, for when the tub is pressed or a scoop comes home.</summary>
        public void Squash()
        {
            if (mound == null || !isActiveAndEnabled) return;

            if (squash != null)
            {
                StopCoroutine(squash);
                mound.localScale = moundBaseScale;
            }
            squash = StartCoroutine(SquashRoutine());
        }

        public void SetGlow(bool on)
        {
            IsGlowing = on;
            if (glow != null) glow.SetActive(on);
            if (!on) transform.localScale = baseScale;
        }

        IEnumerator SquashRoutine()
        {
            yield return ShopMotion.Punch(mound, moundBaseScale, new Vector3(1.25f, 0.6f, 1.25f), 0.3f);
            squash = null;
        }
    }
}
