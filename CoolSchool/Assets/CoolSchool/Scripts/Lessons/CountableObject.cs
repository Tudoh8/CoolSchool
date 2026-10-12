using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// A toy on the lesson table that can be counted. The lesson does the tap raycast; when a toy is counted
    /// it hops, sparkles and shows its number above it, and the number stays. Builds its own roomy tap box.
    /// </summary>
    public class CountableObject : MonoBehaviour
    {
        const float HitBoxGrowth = 1.25f;
        const float LabelLift = 0.2f;
        const float LabelScale = 0.004f;
        const float PopSeconds = 0.45f;
        const float PopHeight = 0.14f;
        const float PopPunch = 0.3f;
        const float AppearSeconds = 0.35f;
        const float PulseSpeed = 7f;
        const float PulseAmount = 0.16f;
        const float WobbleSeconds = 0.5f;
        const float WobbleDegrees = 22f;

        static readonly Color LabelColor = new Color(1f, 0.93f, 0.45f, 1f);
        static readonly Color LabelOutline = new Color(0.25f, 0.12f, 0.02f, 1f);

        /// <summary>The font for the number above the toy. The lesson passes Inter Bold.</summary>
        public Font LabelFont { get; set; }

        public bool Counted { get; private set; }

        /// <summary>True while the toy is hopping or flying, so another tap does not disturb it.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Where the toy sits on the table.</summary>
        public Vector3 RestPosition { get; private set; }

        /// <summary>How far the toy's pivot sits above its lowest point, so it can be set down at a given height.</summary>
        public float PivotLift { get; private set; }

        Vector3 baseScale = Vector3.one;
        float topHeight = 0.35f;
        Coroutine motion;
        Coroutine pulse;
        bool wobbling;
        Transform label;
        Text labelText;

        void Awake()
        {
            BuildHitBox();
        }

        void LateUpdate()
        {
            FollowWithLabel();
        }

        void OnDestroy()
        {
            ClearNumber();
        }

        // ----- Placing ---------------------------------------------------------------------------

        /// <summary>Stands the toy on the table at this point, facing this way, scaled by the given factor.</summary>
        public void Place(Vector3 point, Quaternion rotation, float scale)
        {
            transform.localScale *= scale;
            transform.rotation = rotation;
            transform.position = point;

            if (TryGetBounds(out Bounds bounds))
            {
                // Pivots in the toy packs are not reliable, so rest the lowest point on the table.
                transform.position += Vector3.up * (point.y - bounds.min.y);
                topHeight = bounds.size.y;
            }

            RestPosition = transform.position;
            PivotLift = RestPosition.y - point.y;
            baseScale = transform.localScale;
        }

        /// <summary>Pops into view after a short delay.</summary>
        public void Appear(float delay)
        {
            transform.localScale = Vector3.zero;
            StartMotion(AppearRoutine(delay));
        }

        // ----- What the lesson can do with a toy -------------------------------------------------

        /// <summary>The toy is counted: a bounce, a sparkle, and its number floats above it.</summary>
        public void Pop(int shownNumber)
        {
            Counted = true;
            Highlight(false);
            ShowNumber(shownNumber);
            BattleEffects.Sparkle(transform.position + Vector3.up * topHeight);
            StartMotion(PopRoutine());
        }

        /// <summary>A happy hop and sparkle without counting: for props that are not counted, like the words of the letters lesson.</summary>
        public void Hop()
        {
            Highlight(false);
            BattleEffects.Sparkle(transform.position + Vector3.up * topHeight);
            StartMotion(PopRoutine());
        }

        /// <summary>A friendly sideways wobble for a wrong pick. Never red, never a buzzer.</summary>
        public void Wobble()
        {
            if (!isActiveAndEnabled || wobbling) return;

            StartCoroutine(WobbleRoutine());
        }

        /// <summary>Changes the number above the toy without any fuss.</summary>
        public void ShowNumber(int number)
        {
            EnsureLabel();
            if (labelText != null) labelText.text = number.ToString();
        }

        /// <summary>Takes the number away and makes the toy countable again.</summary>
        public void ResetCounted()
        {
            Counted = false;
            ClearNumber();
        }

        public void ClearNumber()
        {
            if (label != null) Destroy(label.gameObject);
            label = null;
            labelText = null;
        }

        /// <summary>A gentle pulse that says "this one!". Used to help a child who is stuck.</summary>
        public void Highlight(bool on)
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = null;

            if (on && !Counted && isActiveAndEnabled)
                pulse = StartCoroutine(PulseRoutine());
            else if (!IsMoving)
                transform.localScale = baseScale;
        }

        /// <summary>Flies the toy in an arc to a spot, shrinking or growing to the factor on the way.</summary>
        public void FlyTo(Vector3 target, float seconds, float arcHeight, float scaleFactor, Action done)
        {
            Highlight(false);
            StartMotion(FlyRoutine(target, seconds, arcHeight, scaleFactor, done));
        }

        public void ReturnToRest(float seconds, Action done)
        {
            FlyTo(RestPosition, seconds, 0.25f, 1f, done);
        }

        // ----- Animations ------------------------------------------------------------------------

        void StartMotion(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;

            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(routine);
        }

        IEnumerator AppearRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Sfx.Play("lesson.toy_pop", transform.position);

            for (float elapsed = 0f; elapsed < AppearSeconds; elapsed += Time.deltaTime)
            {
                transform.localScale = baseScale * LessonMotion.OutBack(elapsed / AppearSeconds);
                yield return null;
            }

            transform.localScale = baseScale;
            motion = null;
        }

        IEnumerator PopRoutine()
        {
            IsMoving = true;
            Vector3 start = transform.position;
            Vector3 startScale = transform.localScale;
            for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.deltaTime)
            {
                float hump = LessonMotion.Hump(elapsed / PopSeconds);
                transform.position = start + Vector3.up * (hump * PopHeight);
                transform.localScale = startScale * (1f + hump * PopPunch);
                yield return null;
            }

            transform.position = start;
            transform.localScale = startScale;
            IsMoving = false;
            motion = null;
        }

        IEnumerator FlyRoutine(Vector3 target, float seconds, float arcHeight, float scaleFactor, Action done)
        {
            IsMoving = true;
            Vector3 start = transform.position;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = baseScale * scaleFactor;
            float duration = Mathf.Max(0.05f, seconds);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(start, target, k) + Vector3.up * (LessonMotion.Hump(k) * arcHeight);
                transform.localScale = Vector3.Lerp(startScale, endScale, k);
                yield return null;
            }

            transform.position = target;
            transform.localScale = endScale;
            IsMoving = false;
            motion = null;
            done?.Invoke();
        }

        IEnumerator WobbleRoutine()
        {
            wobbling = true;
            Quaternion start = transform.rotation;
            for (float elapsed = 0f; elapsed < WobbleSeconds; elapsed += Time.deltaTime)
            {
                float angle = LessonMotion.DampedWobble(elapsed / WobbleSeconds, 3f) * WobbleDegrees;
                transform.rotation = start * Quaternion.Euler(0f, angle, 0f);
                yield return null;
            }

            transform.rotation = start;
            wobbling = false;
        }

        IEnumerator PulseRoutine()
        {
            while (true)
            {
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
                if (!IsMoving) transform.localScale = baseScale * (1f + beat * PulseAmount);
                yield return null;
            }
        }

        // ----- Number above the toy --------------------------------------------------------------

        void EnsureLabel()
        {
            if (label != null) return;

            var root = new GameObject("CountNumber", typeof(RectTransform), typeof(Canvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(200f, 160f);
            rect.localScale = Vector3.one * LabelScale;

            var textObject = new GameObject("Digit", typeof(RectTransform), typeof(Text), typeof(Outline));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            labelText = textObject.GetComponent<Text>();
            labelText.font = LabelFont != null ? LabelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 130;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.color = LabelColor;
            labelText.raycastTarget = false;

            var outline = textObject.GetComponent<Outline>();
            outline.effectColor = LabelOutline;
            outline.effectDistance = new Vector2(6f, -6f);

            label = root.transform;
            FollowWithLabel();
        }

        /// <summary>The number hangs above the toy and always faces the camera.</summary>
        void FollowWithLabel()
        {
            if (label == null) return;

            label.position = transform.position + Vector3.up * (topHeight + LabelLift);
            Camera main = Camera.main;
            if (main != null) label.rotation = main.transform.rotation;
        }

        // ----- Tap box ---------------------------------------------------------------------------

        /// <summary>Swaps the toy's own colliders for one roomy box, so small fingers do not miss. It is wider but not taller, so it does not cover the toy behind.</summary>
        void BuildHitBox()
        {
            foreach (Collider existing in GetComponentsInChildren<Collider>(true))
                Destroy(existing);

            if (!TryGetBounds(out Bounds bounds)) return;

            Vector3 scale = transform.lossyScale;
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)) * HitBoxGrowth,
                bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)) * HitBoxGrowth);
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
