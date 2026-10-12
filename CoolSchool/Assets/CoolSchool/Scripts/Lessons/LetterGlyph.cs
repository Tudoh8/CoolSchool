using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Draws a letter as a line of round chalk dots, from the same stroke data the child traces. Using the one
    /// shape everywhere (cards, board, name slots) means the letter always looks the same, with a single-storey a and g.
    /// </summary>
    public static class LetterGlyph
    {
        /// <summary>A point of the 0 to 1 box as a position inside a square of this side, centred on the origin.</summary>
        public static Vector2 ToSquare(Vector2 boxPoint, float side)
        {
            return (boxPoint - new Vector2(0.5f, 0.5f)) * side;
        }

        /// <summary>Points along a polyline, one every "spacing" units, including the start and the end.</summary>
        public static List<Vector2> Resample(IList<Vector2> polyline, float spacing)
        {
            var result = new List<Vector2>();
            if (polyline == null || polyline.Count == 0) return result;

            result.Add(polyline[0]);
            float carried = 0f;
            for (int i = 1; i < polyline.Count; i++)
            {
                Vector2 from = polyline[i - 1];
                Vector2 to = polyline[i];
                float length = Vector2.Distance(from, to);
                if (length < 0.0001f) continue;

                float next = spacing - carried;
                while (next <= length)
                {
                    result.Add(Vector2.Lerp(from, to, next / length));
                    next += spacing;
                }
                carried = length - (next - spacing);
            }

            Vector2 last = polyline[polyline.Count - 1];
            if (Vector2.Distance(result[result.Count - 1], last) > spacing * 0.2f) result.Add(last);
            return result;
        }

        /// <summary>
        /// Puts the whole letter into a rect as chalk dots, in a square of this side centred on the rect. The new dots
        /// are returned in drawing order so the caller can fade or pop them. Strokes without points are skipped.
        /// </summary>
        public static List<Image> Build(RectTransform parent, LetterEntry letter, float side, float dotSize, Color color, Sprite dot)
        {
            var dots = new List<Image>();
            if (parent == null || letter == null || letter.strokes == null) return dots;

            foreach (LetterStroke stroke in letter.strokes)
            {
                if (stroke == null || stroke.points == null || stroke.points.Length == 0) continue;

                var local = new Vector2[stroke.points.Length];
                for (int i = 0; i < local.Length; i++) local[i] = ToSquare(stroke.points[i], side);

                foreach (Vector2 point in Resample(local, dotSize * 0.3f))
                    dots.Add(CreateDot(parent, point, dotSize, color, dot));
            }
            return dots;
        }

        public static Image CreateDot(RectTransform parent, Vector2 position, float size, Color color, Sprite sprite)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
