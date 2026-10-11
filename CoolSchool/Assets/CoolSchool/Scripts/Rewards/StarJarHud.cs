using System;
using System.Collections;
using CoolSchool.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// The star jar in the top-right corner. A star that fills up from the bottom as stars are collected.
    /// Won stars fly across the screen into it, and it bounces for every star that lands.
    /// </summary>
    public class StarJarHud : MonoBehaviour
    {
        [Tooltip("The gold picture that fills up from the bottom (Image type Filled, Vertical).")]
        [SerializeField] Image fill;
        [Tooltip("Where the flying stars go. Defaults to this object.")]
        [SerializeField] RectTransform target;
        [SerializeField] Sprite flyingStarSprite;
        [Tooltip("Size of a flying star in canvas units.")]
        [SerializeField] float flyingStarSize = 140f;
        [SerializeField] float flightSeconds = 0.7f;
        [Tooltip("How high the flight arcs above a straight line, in canvas units.")]
        [SerializeField] float arcHeight = 150f;

        const float BounceSeconds = 0.35f;
        const float BounceScale = 1.35f;
        const float StartJitter = 40f;

        Coroutine bounceRoutine;
        Vector3 baseScale = Vector3.one;
        bool hasBaseScale;

        RectTransform Target => target != null ? target : (RectTransform)transform;

        public void Refresh(int inJar, int capacity)
        {
            if (fill == null) return;

            fill.fillAmount = capacity > 0 ? Mathf.Clamp01((float)inJar / capacity) : 0f;
        }

        public void Bounce()
        {
            if (!isActiveAndEnabled) return;

            RememberBaseScale();
            if (bounceRoutine != null) StopCoroutine(bounceRoutine);
            bounceRoutine = StartCoroutine(BounceRoutine());
        }

        /// <summary>Sends a star from a screen point (in pixels) to the jar. landed is called when it arrives.</summary>
        public void FlyStar(Vector2 screenPoint, Action landed)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || !isActiveAndEnabled)
            {
                landed?.Invoke();
                return;
            }

            canvas = canvas.rootCanvas;
            RectTransform star = CreateFlyingStar(canvas);
            Sfx.Play("reward.star_fly");
            Vector2 start = screenPoint + UnityEngine.Random.insideUnitCircle * StartJitter * canvas.scaleFactor;
            StartCoroutine(FlyRoutine(star, canvas.scaleFactor, start, landed));
        }

        RectTransform CreateFlyingStar(Canvas canvas)
        {
            var go = new GameObject("FlyingStar", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.sizeDelta = Vector2.one * flyingStarSize;

            var image = go.GetComponent<Image>();
            image.sprite = flyingStarSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        IEnumerator FlyRoutine(RectTransform star, float canvasScale, Vector2 start, Action landed)
        {
            Vector3 from = start;
            Vector3 to = Target.position;
            Vector3 control = (from + to) * 0.5f + Vector3.up * (arcHeight * canvasScale);
            float duration = Mathf.Max(0.1f, flightSeconds);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (star == null) break;

                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                star.position = Quadratic(from, control, to, k);
                star.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Sin(k * Mathf.PI * 0.5f));
                star.localRotation = Quaternion.Euler(0f, 0f, 360f * k);
                yield return null;
            }

            if (star != null) Destroy(star.gameObject);
            Sfx.Play("reward.star_land");
            Bounce();
            landed?.Invoke();
        }

        IEnumerator BounceRoutine()
        {
            for (float elapsed = 0f; elapsed < BounceSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float k = elapsed / BounceSeconds;
                transform.localScale = baseScale * Mathf.Lerp(1f, BounceScale, Mathf.Sin(k * Mathf.PI));
                yield return null;
            }

            transform.localScale = baseScale;
            bounceRoutine = null;
        }

        void RememberBaseScale()
        {
            if (hasBaseScale) return;

            baseScale = transform.localScale;
            hasBaseScale = true;
        }

        void OnDisable()
        {
            bounceRoutine = null;
            if (hasBaseScale) transform.localScale = baseScale;
        }

        static Vector3 Quadratic(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }
    }
}
