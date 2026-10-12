using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Chalk writing on the blackboard: a huge digit, dots in dice-like groups (two chalk ten-frames above ten),
    /// or a row of numbers with one box holding a question mark.
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

        // Ten-frames (counts above ten): two stacked frames of 2 rows x 5 cells, a full ten and the rest.
        const int FrameColumns = 5;
        const int FrameCells = 10;
        const float FrameAspect = 0.4f;
        const float FrameGapFraction = 0.06f;
        const float FrameLineWidth = 6f;
        const float FrameDotFraction = 0.62f;
        const float FrameLineAlpha = 0.55f;

        // The number line: up to five numbers in a row, one of them maybe a "?" in a chalk box.
        const int MaxLineSlots = 5;
        const float LineWidthFill = 0.92f;
        const float LineGlyphWidth = 0.62f;
        const float BlankBoxHeight = 0.6f;
        const float BlankBoxLine = 8f;
        const float LineSlotGapSeconds = 0.12f;
        const string BorderName = "Border";

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
        readonly List<RectTransform> lineSlots = new List<RectTransform>();
        Text blankText;

        /// <summary>The writing area in canvas units.</summary>
        Vector2 BoardUnits => boardSize * UnitsPerMetre;

        /// <summary>The writing surface (made on first use). Other chalk writers, like the LetterBoard, put their drawing on it.</summary>
        public RectTransform Surface
        {
            get
            {
                EnsureBoard();
                return board;
            }
        }

        /// <summary>The surface size in its own units, centred on the surface.</summary>
        public Vector2 SurfaceUnits => BoardUnits;

        public Color ChalkColor => chalkColor;

        public Sprite DotSprite => dotSprite;

        void OnDisable()
        {
            animations.Clear();
        }

        /// <summary>One huge chalk digit.</summary>
        public void ShowNumber(int number)
        {
            Clear();
            EnsureBoard();

            Sfx.Play("lesson.chalk", transform.position);
            Text text = CreateText("Digit", board, number.ToString(), Mathf.RoundToInt(BoardUnits.y * 0.85f));
            StartAnimation(PopIn(text.rectTransform));
        }

        /// <summary>The number as chalk dots, in groups like the faces of a dice.</summary>
        public void ShowDots(int count)
        {
            Clear();
            if (count <= 0) return;
            EnsureBoard();
            Sfx.Play("lesson.chalk", transform.position);

            if (count > FrameCells)
            {
                BuildTenFrames(count);
                return;
            }

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

        /// <summary>
        /// A row of big chalk numbers (up to five), with a chalk box holding a "?" at blankIndex (use -1 for no blank).
        /// The value at the blank is not shown: FillBlank turns the "?" into whatever number it is given.
        /// </summary>
        public void ShowNumberLine(int[] values, int blankIndex)
        {
            Clear();
            if (values == null || values.Length == 0) return;
            EnsureBoard();
            Sfx.Play("lesson.chalk", transform.position);

            int count = Mathf.Min(values.Length, MaxLineSlots);
            float slot = Mathf.Min(BoardUnits.x * LineWidthFill / count, BoardUnits.y * 0.8f);

            int digits = 1;
            for (int i = 0; i < count; i++)
                if (i != blankIndex) digits = Mathf.Max(digits, values[i].ToString().Length);
            int fontSize = Mathf.RoundToInt(Mathf.Min(BoardUnits.y * 0.5f, slot * 0.9f / (LineGlyphWidth * digits)));

            for (int i = 0; i < count; i++)
            {
                bool blank = i == blankIndex;
                RectTransform holder = CreateSlot(i, (i - (count - 1) * 0.5f) * slot, slot * 0.9f, BoardUnits.y * BlankBoxHeight);
                Text text = CreateText("Digit", holder, blank ? "?" : values[i].ToString(), fontSize);

                if (blank)
                {
                    blankText = text;
                    BuildBorder(holder);
                }
                lineSlots.Add(holder);
                StartAnimation(PopIn(holder, i * LineSlotGapSeconds));
            }
        }

        /// <summary>One number of the row swells for a moment, so the teacher can "point" while counting along.</summary>
        public void PulseLineSlot(int index)
        {
            if (index < 0 || index >= lineSlots.Count || lineSlots[index] == null || !isActiveAndEnabled) return;

            StartCoroutine(PulseRoutine(lineSlots[index], 0.3f));
        }

        /// <summary>The question mark turns into the number, with a chalk pop, and its box disappears.</summary>
        public void FillBlank(int value)
        {
            if (blankText == null) return;

            Sfx.Play("lesson.chalk", transform.position);
            blankText.text = value.ToString();

            Transform holder = blankText.transform.parent;
            for (int i = holder.childCount - 1; i >= 0; i--)
                if (holder.GetChild(i).name == BorderName) Destroy(holder.GetChild(i).gameObject);

            StartAnimation(PopIn(blankText.rectTransform));
            blankText = null;
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
            lineSlots.Clear();
            blankText = null;

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

        /// <summary>Two chalk ten-frames above each other: a full ten, then the rest. Dots are numbered frame by frame.</summary>
        void BuildTenFrames(int count)
        {
            float width = Mathf.Min(BoardUnits.x * 0.9f, BoardUnits.y * 0.88f / (2f * FrameAspect + FrameGapFraction));
            float centerY = (FrameAspect + FrameGapFraction) * width * 0.5f;

            BuildFrame(new Vector2(0f, centerY), width, FrameCells, 0);
            BuildFrame(new Vector2(0f, -centerY), width, count - FrameCells, FrameCells);
        }

        /// <summary>One ten-frame: a chalk box with a middle line and four dividers, and the first cells holding a dot.</summary>
        void BuildFrame(Vector2 center, float width, int filled, int firstIndex)
        {
            float cell = width / FrameColumns;
            float height = cell * 2f;

            var holder = new GameObject("TenFrame", typeof(RectTransform));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(board, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(width, height);

            Color line = chalkColor;
            line.a *= FrameLineAlpha;
            CreateLine(rect, new Vector2(0f, height * 0.5f), new Vector2(width + FrameLineWidth, FrameLineWidth), line);
            CreateLine(rect, new Vector2(0f, -height * 0.5f), new Vector2(width + FrameLineWidth, FrameLineWidth), line);
            CreateLine(rect, new Vector2(-width * 0.5f, 0f), new Vector2(FrameLineWidth, height + FrameLineWidth), line);
            CreateLine(rect, new Vector2(width * 0.5f, 0f), new Vector2(FrameLineWidth, height + FrameLineWidth), line);
            CreateLine(rect, Vector2.zero, new Vector2(width, FrameLineWidth), line);
            for (int divider = 1; divider < FrameColumns; divider++)
                CreateLine(rect, new Vector2((divider - FrameColumns * 0.5f) * cell, 0f), new Vector2(FrameLineWidth, height), line);

            for (int i = 0; i < Mathf.Min(filled, FrameCells); i++)
            {
                int row = i / FrameColumns;
                int column = i % FrameColumns;
                var position = new Vector2((column - (FrameColumns - 1) * 0.5f) * cell, (0.5f - row) * cell);

                Image dot = CreateDot(rect, position, cell * FrameDotFraction);
                dots.Add(dot.rectTransform);
                StartAnimation(PopIn(dot.rectTransform, (firstIndex + i) * DotGapSeconds));
            }
        }

        /// <summary>A holder for one number of the number line, centred at this x on the board.</summary>
        RectTransform CreateSlot(int index, float x, float width, float height)
        {
            var go = new GameObject("Slot" + (index + 1), typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(board, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>A chalk box around a holder, made of four lines that FillBlank removes together.</summary>
        void BuildBorder(RectTransform holder)
        {
            Vector2 size = holder.sizeDelta;
            Image top = CreateLine(holder, new Vector2(0f, size.y * 0.5f), new Vector2(size.x + BlankBoxLine, BlankBoxLine), chalkColor);
            Image bottom = CreateLine(holder, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x + BlankBoxLine, BlankBoxLine), chalkColor);
            Image left = CreateLine(holder, new Vector2(-size.x * 0.5f, 0f), new Vector2(BlankBoxLine, size.y + BlankBoxLine), chalkColor);
            Image right = CreateLine(holder, new Vector2(size.x * 0.5f, 0f), new Vector2(BlankBoxLine, size.y + BlankBoxLine), chalkColor);
            top.name = bottom.name = left.name = right.name = BorderName;
        }

        Image CreateLine(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        static IEnumerator PulseRoutine(RectTransform target, float amount = 0.6f)
        {
            for (float elapsed = 0f; elapsed < PulseSeconds; elapsed += Time.deltaTime)
            {
                if (target == null) yield break;

                target.localScale = Vector3.one * (1f + LessonMotion.Hump(elapsed / PulseSeconds) * amount);
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
