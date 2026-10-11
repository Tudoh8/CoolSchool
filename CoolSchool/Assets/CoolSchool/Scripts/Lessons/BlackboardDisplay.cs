using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Chalk writing on the blackboard: a huge digit, or dots in dice-like groups.
    /// Sits on the BlackboardFace anchor and makes its own world-space canvas on first use.
    /// Its forward axis points into the wall, so the writing faces the room.
    /// </summary>
    public class BlackboardDisplay : MonoBehaviour
    {
        [SerializeField] Font font;
        [Tooltip("A round picture for the chalk dots. A plain square is used when empty.")]
        [SerializeField] Sprite dotSprite;
        [Tooltip("Width and height of the writing area in metres.")]
        [SerializeField] Vector2 boardSize = new Vector2(1.6f, 0.9f);
        [SerializeField] Color chalkColor = new Color(0.96f, 0.96f, 0.9f, 0.96f);

        const float UnitsPerMetre = 1000f;
        const float AppearSeconds = 0.35f;
        const float PulseSeconds = 0.5f;
        const float DotGapSeconds = 0.08f;
        const float DotsFill = 0.8f;
        const float DotSize = 0.26f;

        static readonly Vector2[][] DiceFaces =
        {
            new[] { new Vector2(0f, 0f) },
            new[] { new Vector2(-0.3f, 0.3f), new Vector2(0.3f, -0.3f) },
            new[] { new Vector2(-0.3f, 0.3f), new Vector2(0f, 0f), new Vector2(0.3f, -0.3f) },
            new[] { new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f), new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f) },
            new[] { new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f), new Vector2(0f, 0f), new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f) },
            new[]
            {
                new Vector2(-0.3f, 0.3f), new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0f),
                new Vector2(0.3f, 0f), new Vector2(-0.3f, -0.3f), new Vector2(0.3f, -0.3f),
            },
        };

        RectTransform board;
        readonly List<Coroutine> animations = new List<Coroutine>();
        readonly List<RectTransform> dots = new List<RectTransform>();

        /// <summary>The writing area in canvas units.</summary>
        Vector2 BoardUnits => boardSize * UnitsPerMetre;

        void OnDisable()
        {
            animations.Clear();
        }

        /// <summary>One huge chalk digit.</summary>
        public void ShowNumber(int number)
        {
            Clear();
            EnsureBoard();

            Text text = CreateText("Digit", board, number.ToString(), Mathf.RoundToInt(BoardUnits.y * 0.85f));
            StartAnimation(PopIn(text.rectTransform));
        }

        /// <summary>The number as chalk dots, in groups like the faces of a dice.</summary>
        public void ShowDots(int count)
        {
            Clear();
            if (count <= 0) return;
            EnsureBoard();

            // Up to six is one dice face. More is split in two, e.g. 7 as 4 and 3.
            int groups = count > 6 ? 2 : 1;
            float cell = Mathf.Min(BoardUnits.y * DotsFill, BoardUnits.x * DotsFill / groups);
            int placed = 0;

            for (int group = 0; group < groups; group++)
            {
                int inGroup = groups == 1 ? count : (group == 0 ? (count + 1) / 2 : count / 2);
                float centerX = groups == 1 ? 0f : (group - 0.5f) * BoardUnits.x * 0.5f;
                BuildGroup(inGroup, new Vector2(centerX, 0f), cell, placed);
                placed += inGroup;
            }
        }

        /// <summary>One dot swells for a moment, so the teacher can "point" while counting them.</summary>
        public void PulseDot(int index)
        {
            if (index < 0 || index >= dots.Count || dots[index] == null || !isActiveAndEnabled) return;

            StartCoroutine(PulseRoutine(dots[index]));
        }

        /// <summary>Wipes the board.</summary>
        public void Clear()
        {
            foreach (Coroutine animation in animations)
                if (animation != null) StopCoroutine(animation);
            animations.Clear();
            dots.Clear();

            if (board == null) return;

            for (int i = board.childCount - 1; i >= 0; i--)
                Destroy(board.GetChild(i).gameObject);
        }

        // ----- Building --------------------------------------------------------------------------

        void BuildGroup(int count, Vector2 center, float cell, int firstIndex)
        {
            Vector2[] face = DiceFaces[Mathf.Clamp(count, 1, DiceFaces.Length) - 1];
            for (int i = 0; i < face.Length; i++)
            {
                Image dot = CreateDot(board, center + face[i] * cell, cell * DotSize);
                dots.Add(dot.rectTransform);
                StartAnimation(PopIn(dot.rectTransform, (firstIndex + i) * DotGapSeconds));
            }
        }

        void EnsureBoard()
        {
            if (board != null) return;

            var canvasObject = new GameObject("BlackboardCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);

            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            board = (RectTransform)canvasObject.transform;
            board.sizeDelta = BoardUnits;
            board.localScale = Vector3.one / UnitsPerMetre;
        }

        Text CreateText(string objectName, RectTransform parent, string value, int fontSize)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = chalkColor;
            text.raycastTarget = false;
            return text;
        }

        Image CreateDot(RectTransform parent, Vector2 position, float size)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;

            var image = go.GetComponent<Image>();
            image.sprite = dotSprite;
            image.color = chalkColor;
            image.raycastTarget = false;
            return image;
        }

        // ----- Animation -------------------------------------------------------------------------

        void StartAnimation(IEnumerator routine)
        {
            if (isActiveAndEnabled) animations.Add(StartCoroutine(routine));
        }

        static IEnumerator PulseRoutine(RectTransform target)
        {
            for (float elapsed = 0f; elapsed < PulseSeconds; elapsed += Time.deltaTime)
            {
                if (target == null) yield break;

                target.localScale = Vector3.one * (1f + LessonMotion.Hump(elapsed / PulseSeconds) * 0.6f);
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
        }

        static IEnumerator PopIn(RectTransform target, float delay = 0f)
        {
            target.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            for (float elapsed = 0f; elapsed < AppearSeconds; elapsed += Time.deltaTime)
            {
                if (target == null) yield break;

                target.localScale = Vector3.one * LessonMotion.OutBack(elapsed / AppearSeconds);
                yield return null;
            }

            if (target != null) target.localScale = Vector3.one;
        }
    }
}
