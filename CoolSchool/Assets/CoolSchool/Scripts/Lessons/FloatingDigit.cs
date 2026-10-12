using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// A big chalk-white number that floats in the air and always faces the camera, like the "12" over the basket in the
    /// count-on game. It stays the same number; it can swell for a moment so the teacher can point at it while counting on.
    /// The lesson destroys it when the exercise (or the lesson) ends.
    /// </summary>
    public sealed class FloatingDigit : MonoBehaviour
    {
        const float LabelScale = 0.004f;
        const int FontSize = 150;
        const float PulseSeconds = 0.5f;
        const float PulseAmount = 0.5f;
        const float BobHeight = 0.02f;
        const float BobSpeed = 2f;

        static readonly Color Chalk = new Color(0.97f, 0.97f, 0.92f, 1f);
        static readonly Color ChalkOutline = new Color(0.12f, 0.16f, 0.14f, 1f);

        Vector3 basePosition;
        Coroutine pulse;

        /// <summary>Makes a floating number at a point in the world. The font should be Inter Bold.</summary>
        public static FloatingDigit Create(Font font, int number, Vector3 position)
        {
            var root = new GameObject("FloatingDigit", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<Canvas>().sortingOrder = 20;

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(260f, 200f);
            rect.localScale = Vector3.one * LabelScale;

            var textObject = new GameObject("Digit", typeof(RectTransform), typeof(Text), typeof(Outline));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = number.ToString();
            text.fontSize = FontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Chalk;
            text.raycastTarget = false;

            var outline = textObject.GetComponent<Outline>();
            outline.effectColor = ChalkOutline;
            outline.effectDistance = new Vector2(6f, -6f);

            var digit = root.AddComponent<FloatingDigit>();
            digit.basePosition = position;
            digit.Face();
            return digit;
        }

        /// <summary>The number swells for a moment.</summary>
        public void Pulse()
        {
            if (!isActiveAndEnabled) return;

            if (pulse != null) StopCoroutine(pulse);
            pulse = StartCoroutine(PulseRoutine());
        }

        void LateUpdate()
        {
            transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
            Face();
        }

        void Face()
        {
            Camera main = Camera.main;
            if (main != null) transform.rotation = main.transform.rotation;
        }

        IEnumerator PulseRoutine()
        {
            Vector3 normal = Vector3.one * LabelScale;
            for (float elapsed = 0f; elapsed < PulseSeconds; elapsed += Time.deltaTime)
            {
                transform.localScale = normal * (1f + LessonMotion.Hump(elapsed / PulseSeconds) * PulseAmount);
                yield return null;
            }

            transform.localScale = normal;
            pulse = null;
        }
    }
}
