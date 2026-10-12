using System.Collections.Generic;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.EditorTools
{
    /// <summary>
    /// The writing strokes of the 26 lowercase letters, in school print: single-storey a and g, the stem first and then the
    /// round part, the way Dutch kindergartens teach. Each letter is written below as a few lines and arcs in "typographic"
    /// units (baseline 0, x-height 1, ascenders up to 1.65, descenders down to -0.65, y UP, in writing order and direction).
    /// Build turns that into stroke data: it fits the whole letter into a 0 to 1 box (centred, aspect kept) and cuts every
    /// stroke into points about 0.08 apart, keeping the corners. That baked data is what goes into the Letters asset.
    /// </summary>
    static class LetterStrokeData
    {
        const float PointSpacing = 0.08f;
        const float ArcStepDegrees = 20f;

        /// <summary>The strokes of one letter, ready for the asset. Empty for anything that is not a to z.</summary>
        public static List<LetterStroke> Build(char letter)
        {
            List<List<Vector2>> drawn = Define(letter);
            var result = new List<LetterStroke>();
            if (drawn == null || drawn.Count == 0) return result;

            // Fit the whole letter in a 0 to 1 box, and keep its shape.
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (List<Vector2> stroke in drawn)
            {
                foreach (Vector2 point in stroke)
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            Vector2 size = max - min;
            float scale = 1f / Mathf.Max(0.0001f, Mathf.Max(size.x, size.y));
            Vector2 offset = new Vector2((1f - size.x * scale) * 0.5f, (1f - size.y * scale) * 0.5f);

            foreach (List<Vector2> stroke in drawn)
            {
                var fitted = new List<Vector2>();
                foreach (Vector2 point in stroke)
                    fitted.Add((point - min) * scale + offset);

                result.Add(new LetterStroke { points = Resample(fitted).ToArray() });
            }
            return result;
        }

        /// <summary>Every segment is cut into equal pieces of at most PointSpacing, so corners stay and the points are evenly spaced.</summary>
        static List<Vector2> Resample(List<Vector2> polyline)
        {
            var points = new List<Vector2>();
            if (polyline.Count == 0) return points;

            points.Add(polyline[0]);
            for (int i = 1; i < polyline.Count; i++)
            {
                Vector2 from = polyline[i - 1];
                Vector2 to = polyline[i];
                float length = Vector2.Distance(from, to);
                if (length < 0.001f) continue;

                int pieces = Mathf.Max(1, Mathf.CeilToInt(length / PointSpacing));
                for (int piece = 1; piece <= pieces; piece++)
                    points.Add(Vector2.Lerp(from, to, piece / (float)pieces));
            }
            return points;
        }

        // ----- The letters -----------------------------------------------------------------------

        static List<List<Vector2>> Define(char letter)
        {
            switch (letter)
            {
                case 'a': return Strokes(Arc(0.45f, 0.5f, 0.45f, 0.5f, 45f, 405f), Line(0.9f, 1f, 0.9f, 0f));
                case 'b': return Strokes(Line(0f, 1.65f, 0f, 0f), Arc(0.45f, 0.5f, 0.45f, 0.5f, 180f, -180f));
                case 'c': return Strokes(Arc(0.5f, 0.5f, 0.5f, 0.5f, 45f, 315f));
                case 'd': return Strokes(Arc(0.45f, 0.5f, 0.45f, 0.5f, 45f, 405f), Line(0.9f, 1.65f, 0.9f, 0f));
                case 'e': return Strokes(Join(Line(0.02f, 0.5f, 0.9f, 0.5f), Arc(0.45f, 0.5f, 0.45f, 0.5f, 0f, 320f)));
                case 'f':
                    return Strokes(
                        Join(Arc(0.7f, 1.35f, 0.3f, 0.3f, 30f, 180f), Line(0.4f, 1.35f, 0.4f, 0f)),
                        Line(0.05f, 1f, 0.8f, 1f));
                case 'g':
                    return Strokes(
                        Arc(0.45f, 0.5f, 0.45f, 0.5f, 45f, 405f),
                        Join(Line(0.9f, 1f, 0.9f, -0.2f), Arc(0.55f, -0.2f, 0.35f, 0.45f, 0f, -150f)));
                case 'h':
                    return Strokes(
                        Line(0f, 1.65f, 0f, 0f),
                        Join(Arc(0.4f, 0.45f, 0.4f, 0.55f, 180f, 0f), Line(0.8f, 0.45f, 0.8f, 0f)));
                case 'i': return Strokes(Line(0.1f, 1f, 0.1f, 0f), Line(0.1f, 1.4f, 0.1f, 1.36f));
                case 'j':
                    return Strokes(
                        Join(Line(0.3f, 1f, 0.3f, -0.35f), Arc(0f, -0.35f, 0.3f, 0.35f, 0f, -100f)),
                        Line(0.3f, 1.4f, 0.3f, 1.36f));
                case 'k': return Strokes(Line(0f, 1.65f, 0f, 0f), Poly(0.75f, 1f, 0f, 0.35f, 0.8f, 0f));
                case 'l': return Strokes(Line(0.05f, 1.65f, 0.05f, 0f));
                case 'm':
                    return Strokes(
                        Line(0f, 1f, 0f, 0f),
                        Join(Arc(0.3f, 0.65f, 0.3f, 0.35f, 180f, 0f), Line(0.6f, 0.65f, 0.6f, 0f)),
                        Join(Arc(0.9f, 0.65f, 0.3f, 0.35f, 180f, 0f), Line(1.2f, 0.65f, 1.2f, 0f)));
                case 'n':
                    return Strokes(
                        Line(0f, 1f, 0f, 0f),
                        Join(Arc(0.35f, 0.65f, 0.35f, 0.35f, 180f, 0f), Line(0.7f, 0.65f, 0.7f, 0f)));
                case 'o': return Strokes(Arc(0.45f, 0.5f, 0.45f, 0.5f, 90f, 450f));
                case 'p': return Strokes(Line(0f, 1f, 0f, -0.65f), Arc(0.45f, 0.5f, 0.45f, 0.5f, 180f, -180f));
                case 'q': return Strokes(Arc(0.45f, 0.5f, 0.45f, 0.5f, 45f, 405f), Line(0.9f, 1f, 0.9f, -0.65f));
                case 'r': return Strokes(Line(0f, 1f, 0f, 0f), Arc(0.5f, 0.45f, 0.5f, 0.5f, 180f, 75f));
                case 's': return Strokes(Join(Arc(0.4f, 0.75f, 0.35f, 0.25f, 30f, 270f), Arc(0.4f, 0.25f, 0.35f, 0.25f, 90f, -150f)));
                case 't':
                    return Strokes(
                        Join(Line(0.3f, 1.5f, 0.3f, 0.3f), Arc(0.55f, 0.3f, 0.25f, 0.3f, 180f, 270f)),
                        Line(0f, 1f, 0.6f, 1f));
                case 'u':
                    return Strokes(
                        Join(Line(0f, 1f, 0f, 0.35f), Arc(0.35f, 0.35f, 0.35f, 0.35f, 180f, 360f), Line(0.7f, 0.35f, 0.7f, 1f)),
                        Line(0.7f, 1f, 0.7f, 0f));
                case 'v': return Strokes(Poly(0f, 1f, 0.4f, 0f, 0.8f, 1f));
                case 'w': return Strokes(Poly(0f, 1f, 0.3f, 0f, 0.6f, 0.8f, 0.9f, 0f, 1.2f, 1f));
                case 'x': return Strokes(Line(0f, 1f, 0.8f, 0f), Line(0.8f, 1f, 0f, 0f));
                case 'y': return Strokes(Line(0f, 1f, 0.4f, 0f), Poly(0.8f, 1f, 0.4f, 0f, 0.05f, -0.65f));
                case 'z': return Strokes(Poly(0f, 1f, 0.8f, 1f, 0f, 0f, 0.8f, 0f));
                default: return null;
            }
        }

        // ----- Tiny drawing language -------------------------------------------------------------

        static List<List<Vector2>> Strokes(params List<Vector2>[] strokes)
        {
            return new List<List<Vector2>>(strokes);
        }

        static List<Vector2> Line(float x0, float y0, float x1, float y1)
        {
            return new List<Vector2> { new Vector2(x0, y0), new Vector2(x1, y1) };
        }

        /// <summary>A path through the given x, y pairs, with sharp corners.</summary>
        static List<Vector2> Poly(params float[] xy)
        {
            var points = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2)
                points.Add(new Vector2(xy[i], xy[i + 1]));
            return points;
        }

        /// <summary>Part of an ellipse. Angles in degrees, 0 = right, 90 = up. From a smaller to a bigger angle goes counter-clockwise.</summary>
        static List<Vector2> Arc(float cx, float cy, float rx, float ry, float fromDegrees, float toDegrees)
        {
            var points = new List<Vector2>();
            float span = toDegrees - fromDegrees;
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(span) / ArcStepDegrees));
            for (int i = 0; i <= steps; i++)
            {
                float angle = (fromDegrees + span * i / steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(cx + Mathf.Cos(angle) * rx, cy + Mathf.Sin(angle) * ry));
            }
            return points;
        }

        /// <summary>Several paths one after the other as a single stroke; a start that sits on the previous end is not repeated.</summary>
        static List<Vector2> Join(params List<Vector2>[] parts)
        {
            var points = new List<Vector2>();
            foreach (List<Vector2> part in parts)
            {
                foreach (Vector2 point in part)
                {
                    if (points.Count > 0 && Vector2.Distance(points[points.Count - 1], point) < 0.02f) continue;
                    points.Add(point);
                }
            }
            return points;
        }
    }
}
