using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Everything chalk about letters on the blackboard: a big letter written stroke by stroke, the dotted path with its
    /// start dot and arrows that the child traces with a finger, the chalk line that follows him, and the empty slots
    /// for his name. Sits on the same object as the BlackboardDisplay and draws on its surface, so wiping the board
    /// wipes the letters too. All the letters are drawn from the stroke data, so a and g are single-storey.
    /// </summary>
    public class LetterBoard : MonoBehaviour
    {
        [SerializeField] BlackboardDisplay display;

        /// <summary>What a call to TraceStep did.</summary>
        public enum TraceEvent
        {
            None,
            /// <summary>The chalk line got longer.</summary>
            Progress,
            /// <summary>A stroke is finished and the next one is waiting.</summary>
            StrokeDone,
            /// <summary>The last stroke is finished.</summary>
            LetterDone,
        }

        // Sizes as a fraction of the letter's square (side), which is about 80% of the board height.
        const float SquareOfHeight = 0.8f;
        const float SquareOfWidth = 0.6f;
        const float ChalkDot = 0.085f;
        const float GuideDot = 0.035f;
        const float StartDot = 0.13f;
        const float TipDot = 0.11f;
        const float ArrowSize = 0.06f;
        const float ArrowEvery = 0.4f;

        // How generous the tracing is, as a fraction of the board height, or of the letter's square.
        const float ToleranceOfHeight = 0.12f;
        const float LookAheadOfSquare = 0.45f;
        const float EndSlackOfSquare = 0.1f;

        const float WriteSecondsPerSquare = 0.35f;
        const float SparkleEverySeconds = 0.22f;
        const float ScratchEverySeconds = 0.16f;
        const float PopSeconds = 0.3f;
        const float GhostUnitsPerSecond = 0.9f;

        static readonly Color GuideColor = new Color(0.96f, 0.96f, 0.9f, 0.4f);
        static readonly Color ArrowColor = new Color(1f, 0.85f, 0.35f, 0.95f);
        static readonly Color StartColor = new Color(0.55f, 1f, 0.6f, 0.95f);
        static readonly Color TipColor = new Color(1f, 0.95f, 0.55f, 0.9f);
        static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color DoneColor = new Color(1f, 0.97f, 0.75f, 1f);
        static readonly Color SlotColor = new Color(0.96f, 0.96f, 0.9f, 0.5f);

        // ----- The letter being traced ----------------------------------------------------------
        sealed class TraceStroke
        {
            public Vector2[] Points;
            public float[] Cumulative;
            public float Length;
        }

        RectTransform layer;
        RectTransform guideRoot;
        RectTransform chalkRoot;
        RectTransform nameRoot;
        readonly List<Image> chalk = new List<Image>();
        readonly List<RectTransform> slots = new List<RectTransform>();
        readonly List<RectTransform> slotLetters = new List<RectTransform>();
        readonly List<TraceStroke> strokes = new List<TraceStroke>();

        Image startDot;
        Image tipDot;
        Image ghostDot;
        Coroutine ghost;

        float square;
        int strokeChalkStart;
        bool tracing;
        int strokeIndex;
        float progress;
        float drawnTo;
        bool touchedStroke;
        float lastSparkle;
        float lastScratch;

        /// <summary>True from BeginTrace until the whole letter has been traced.</summary>
        public bool IsTracing => tracing;

        public bool TraceDone => strokes.Count > 0 && strokeIndex >= strokes.Count;

        /// <summary>How many strokes the traced letter has, and the one the child is on.</summary>
        public int StrokeCount => strokes.Count;
        public int StrokeIndex => strokeIndex;

        /// <summary>How far along the whole letter he is, from 0 to 1.</summary>
        public float TraceFraction
        {
            get
            {
                float total = 0f, done = 0f;
                for (int i = 0; i < strokes.Count; i++)
                {
                    total += strokes[i].Length;
                    if (i < strokeIndex) done += strokes[i].Length;
                    else if (i == strokeIndex) done += progress;
                }
                return total > 0.0001f ? Mathf.Clamp01(done / total) : 0f;
            }
        }

        BlackboardDisplay Display
        {
            get
            {
                if (display == null) display = GetComponent<BlackboardDisplay>();
                return display;
            }
        }

        void Update()
        {
            AnimateMarkers();
        }

        void OnDisable()
        {
            ghost = null;
        }

        // ----- Wiping ----------------------------------------------------------------------------

        /// <summary>Wipes the board, letters and all, and stops every chalk animation.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            ghost = null;
            tracing = false;
            strokes.Clear();
            strokeIndex = 0;
            progress = 0f;
            drawnTo = 0f;

            chalk.Clear();
            slots.Clear();
            slotLetters.Clear();
            layer = null;
            guideRoot = null;
            chalkRoot = null;
            nameRoot = null;
            startDot = null;
            tipDot = null;
            ghostDot = null;

            if (Display != null) Display.Clear();
        }

        // ----- A big written letter --------------------------------------------------------------

        /// <summary>
        /// Writes the letter big on the board with chalk: the strokes appear one after the other with a chalk sound and
        /// a sparkle at the tip. Wipes the board first. Takes about a second.
        /// </summary>
        public IEnumerator WriteLetter(LetterEntry letter)
        {
            if (letter == null || !letter.HasStrokes || !Prepare()) yield break;

            BuildStrokes(letter);
            chalkRoot = NewRoot("Chalk");

            float perSquare = WriteSecondsPerSquare;
            for (int i = 0; i < strokes.Count; i++)
            {
                Sfx.Play("lesson.chalk", WorldPoint(strokes[i].Points[0]));
                float spacing = ChalkSize * 0.3f;
                int count = Mathf.Max(1, Mathf.CeilToInt(strokes[i].Length / spacing));
                float perDot = strokes[i].Length / square * perSquare / count;

                float sparkleAt = 0f;
                for (int d = 0; d <= count; d++)
                {
                    Vector2 point = PointAt(strokes[i], Mathf.Min(strokes[i].Length, d * spacing));
                    AddChalk(point, 1f);
                    if (Time.time >= sparkleAt)
                    {
                        sparkleAt = Time.time + SparkleEverySeconds;
                        BattleEffects.Sparkle(WorldPoint(point));
                    }
                    if (perDot > 0f && d % 3 == 2) yield return new WaitForSeconds(perDot * 3f);
                }
                yield return new WaitForSeconds(0.08f);
            }

            Sfx.Play("lesson.letter_pop", WorldPoint(Vector2.zero));
            yield return Punch(chalkRoot, 1.12f);
        }

        // ----- Tracing ---------------------------------------------------------------------------

        /// <summary>Shows the dotted path with arrows and a pulsing start dot. Wipes the board first.</summary>
        public void BeginTrace(LetterEntry letter)
        {
            tracing = false;
            if (letter == null || !letter.HasStrokes || !Prepare()) return;

            BuildStrokes(letter);
            guideRoot = NewRoot("Guide");
            chalkRoot = NewRoot("Chalk");

            foreach (TraceStroke stroke in strokes)
            {
                foreach (Vector2 point in LetterGlyph.Resample(stroke.Points, GuideSize * 1.7f))
                    LetterGlyph.CreateDot(guideRoot, point, GuideSize, GuideColor, Dot);

                // A few arrows along the path show which way to go.
                float spacing = square * ArrowEvery;
                for (float s = spacing * 0.6f; s < stroke.Length - GuideSize * 3f; s += spacing)
                    BuildArrow(guideRoot, PointAt(stroke, s), DirectionAt(stroke, s));
            }

            startDot = LetterGlyph.CreateDot(layer, Vector2.zero, StartSize, StartColor, Dot);
            tipDot = LetterGlyph.CreateDot(layer, Vector2.zero, TipSize, TipColor, Dot);
            ghostDot = LetterGlyph.CreateDot(layer, Vector2.zero, TipSize, GhostColor, Dot);
            tipDot.gameObject.SetActive(false);
            ghostDot.gameObject.SetActive(false);

            tracing = true;
            strokeIndex = 0;
            progress = 0f;
            drawnTo = 0f;
            touchedStroke = false;
            PlaceStartDot();
            StartCoroutine(Appear(guideRoot));
        }

        /// <summary>
        /// Call every frame while tracing, with the finger. Progress along the current stroke counts when the finger is within
        /// about 12% of the board height of the path just ahead of where he got to, and lifting the finger keeps the progress.
        /// </summary>
        public TraceEvent TraceStep(bool pressed, Vector2 screenPosition)
        {
            if (!tracing || TraceDone) return TraceEvent.None;

            if (!pressed)
            {
                if (tipDot != null) tipDot.gameObject.SetActive(false);
                return TraceEvent.None;
            }
            if (!ScreenToBoard(screenPosition, out Vector2 finger)) return TraceEvent.None;

            TraceStroke stroke = strokes[strokeIndex];
            float tolerance = Display.SurfaceUnits.y * ToleranceOfHeight;
            if (!ProjectAhead(stroke, finger, progress, square * LookAheadOfSquare, tolerance, out float found))
            {
                if (tipDot != null) tipDot.gameObject.SetActive(false);
                return TraceEvent.None;
            }

            touchedStroke = true;
            TraceEvent result = TraceEvent.None;
            if (found > progress + 0.001f)
            {
                progress = found;
                result = TraceEvent.Progress;
                Scratch(PointAt(stroke, progress));
            }

            TraceEvent finished = DrawAndCheck();
            return finished != TraceEvent.None ? finished : result;
        }

        /// <summary>The teacher does it: the chalk runs along the rest of the current stroke, then on through the later strokes.</summary>
        public IEnumerator AutoDrawRest()
        {
            StopGhost();
            while (tracing && !TraceDone)
            {
                TraceStroke stroke = strokes[strokeIndex];
                int index = strokeIndex;
                while (tracing && strokeIndex == index)
                {
                    progress = Mathf.Min(stroke.Length, progress + square * GhostUnitsPerSecond * 1.3f * Time.deltaTime);
                    touchedStroke = true;
                    Scratch(PointAt(stroke, progress));
                    DrawAndCheck(force: progress >= stroke.Length);
                    yield return null;
                }
            }
        }

        /// <summary>A shining dot shows how to draw the rest of the current stroke, once. Stops by itself.</summary>
        public void ShowGhost()
        {
            if (!tracing || TraceDone || ghost != null) return;

            ghost = StartCoroutine(GhostRoutine());
        }

        public void StopGhost()
        {
            if (ghost != null) StopCoroutine(ghost);
            ghost = null;
            if (ghostDot != null) ghostDot.gameObject.SetActive(false);
        }

        /// <summary>True while the ghost dot is showing the way.</summary>
        public bool GhostPlaying => ghost != null;

        /// <summary>The finished letter fills in: the chalk thickens, glows, bounces, and the guide dots go away.</summary>
        public IEnumerator FillIn()
        {
            StopGhost();
            tracing = false;
            if (startDot != null) startDot.gameObject.SetActive(false);
            if (tipDot != null) tipDot.gameObject.SetActive(false);
            if (guideRoot != null) Destroy(guideRoot.gameObject);
            if (chalkRoot == null) yield break;

            Sfx.Play("lesson.letter_pop", WorldPoint(Vector2.zero));
            foreach (Image dot in chalk)
            {
                if (dot == null) continue;

                dot.color = DoneColor;
                dot.rectTransform.sizeDelta *= 1.35f;
            }

            for (int i = 0; i < strokes.Count; i += 1)
                BattleEffects.Sparkle(WorldPoint(PointAt(strokes[i], strokes[i].Length * 0.5f)));
            yield return Punch(chalkRoot, 1.18f);
        }

        // ----- The name --------------------------------------------------------------------------

        /// <summary>Empty slots in a row, one per letter. Wipes the board first.</summary>
        public void ShowNameSlots(int count)
        {
            if (count <= 0 || !Prepare()) return;

            nameRoot = NewRoot("Name");
            Vector2 units = Display.SurfaceUnits;
            float cell = Mathf.Min(units.x * 0.92f / count, units.y * 0.55f);
            float slotSide = cell * 0.82f;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Slot" + (i + 1), typeof(RectTransform));
                go.layer = layer.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(nameRoot, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * slotSide;
                rect.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * cell, 0f);

                // A chalk line to write on.
                Image line = LetterGlyph.CreateDot(rect, new Vector2(0f, -slotSide * 0.5f), slotSide * 0.08f, SlotColor, null);
                line.rectTransform.sizeDelta = new Vector2(slotSide * 0.9f, slotSide * 0.08f);

                slots.Add(rect);
                slotLetters.Add(null);
                StartCoroutine(PopIn(rect, i * 0.08f));
            }
            Sfx.Play("lesson.chalk", WorldPoint(Vector2.zero));
        }

        /// <summary>Where a slot is on the screen, for a card to fly to.</summary>
        public bool TryGetSlotScreenPoint(int index, out Vector2 screenPoint)
        {
            screenPoint = default;
            Camera view = Camera.main;
            if (view == null || index < 0 || index >= slots.Count || slots[index] == null) return false;

            Vector3 screen = view.WorldToScreenPoint(slots[index].position);
            if (screen.z <= 0f) return false;

            screenPoint = screen;
            return true;
        }

        /// <summary>Writes the letter into a slot with a pop and a sparkle.</summary>
        public void FillSlot(int index, LetterEntry letter)
        {
            if (index < 0 || index >= slots.Count || slots[index] == null || letter == null) return;

            RectTransform slot = slots[index];
            if (slotLetters[index] != null) Destroy(slotLetters[index].gameObject);

            var go = new GameObject("Letter", typeof(RectTransform));
            go.layer = slot.gameObject.layer;
            var holder = (RectTransform)go.transform;
            holder.SetParent(slot, false);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = Vector2.zero;
            holder.sizeDelta = slot.sizeDelta;

            float side = slot.sizeDelta.x * 0.9f;
            LetterGlyph.Build(holder, letter, side, side * ChalkDot * 1.15f, Display.ChalkColor, Dot);
            slotLetters[index] = holder;

            Sfx.Play("lesson.letter_pop", slot.position);
            BattleEffects.Sparkle(slot.position + SurfaceNormal * 0.03f);
            StartCoroutine(PopIn(holder, 0f));
        }

        /// <summary>All the letters of the name bounce one after the other.</summary>
        public IEnumerator BounceName()
        {
            for (int i = 0; i < slotLetters.Count; i++)
            {
                if (slotLetters[i] == null) continue;

                BattleEffects.Sparkle(slotLetters[i].position + SurfaceNormal * 0.03f);
                StartCoroutine(Punch(slotLetters[i], 1.3f));
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(0.5f);
        }

        // ----- Geometry --------------------------------------------------------------------------

        Sprite Dot => Display != null ? Display.DotSprite : null;

        float ChalkSize => square * ChalkDot;
        float GuideSize => square * GuideDot;
        float StartSize => square * StartDot;
        float TipSize => square * TipDot;

        /// <summary>Vector pointing from the board toward the room (toward the viewer).</summary>
        Vector3 SurfaceNormal => layer != null ? -layer.forward : Vector3.back;

        Vector3 WorldPoint(Vector2 boardPoint)
        {
            Transform surface = layer != null ? layer.parent : null;
            if (surface == null) return transform.position;
            return surface.TransformPoint(new Vector3(boardPoint.x, boardPoint.y, 0f)) + SurfaceNormal * 0.05f;
        }

        /// <summary>The finger's place on the board in board units, from the camera ray through it.</summary>
        bool ScreenToBoard(Vector2 screenPosition, out Vector2 boardPoint)
        {
            boardPoint = default;
            Camera view = Camera.main;
            Transform surface = layer != null ? layer.parent : null;
            if (view == null || surface == null) return false;

            Ray ray = view.ScreenPointToRay(screenPosition);
            var plane = new Plane(surface.forward, surface.position);
            if (!plane.Raycast(ray, out float distance)) return false;

            Vector3 local = surface.InverseTransformPoint(ray.GetPoint(distance));
            boardPoint = new Vector2(local.x, local.y);
            return true;
        }

        /// <summary>Wipes the board and makes a fresh layer to draw on. False when there is no board to draw on.</summary>
        bool Prepare()
        {
            if (Display == null) return false;

            Clear();
            RectTransform surface = Display.Surface;
            if (surface == null) return false;

            Vector2 units = Display.SurfaceUnits;
            square = Mathf.Min(units.y * SquareOfHeight, units.x * SquareOfWidth);

            var go = new GameObject("Letters", typeof(RectTransform));
            go.layer = surface.gameObject.layer;
            layer = (RectTransform)go.transform;
            layer.SetParent(surface, false);
            layer.anchorMin = layer.anchorMax = layer.pivot = new Vector2(0.5f, 0.5f);
            layer.anchoredPosition = Vector2.zero;
            layer.sizeDelta = units;
            return true;
        }

        RectTransform NewRoot(string rootName)
        {
            var go = new GameObject(rootName, typeof(RectTransform));
            go.layer = layer.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        void BuildStrokes(LetterEntry letter)
        {
            strokes.Clear();
            foreach (LetterStroke source in letter.strokes)
            {
                if (source == null || source.points == null || source.points.Length == 0) continue;

                var stroke = new TraceStroke
                {
                    Points = new Vector2[source.points.Length],
                    Cumulative = new float[source.points.Length],
                };
                for (int i = 0; i < source.points.Length; i++)
                {
                    stroke.Points[i] = LetterGlyph.ToSquare(source.points[i], square);
                    if (i > 0) stroke.Cumulative[i] = stroke.Cumulative[i - 1] + Vector2.Distance(stroke.Points[i - 1], stroke.Points[i]);
                }
                stroke.Length = stroke.Cumulative[stroke.Cumulative.Length - 1];
                strokes.Add(stroke);
            }
        }

        static Vector2 PointAt(TraceStroke stroke, float s)
        {
            if (stroke.Points.Length == 1 || s <= 0f) return stroke.Points[0];
            if (s >= stroke.Length) return stroke.Points[stroke.Points.Length - 1];

            for (int i = 1; i < stroke.Points.Length; i++)
            {
                if (stroke.Cumulative[i] < s) continue;

                float span = stroke.Cumulative[i] - stroke.Cumulative[i - 1];
                float k = span > 0.0001f ? (s - stroke.Cumulative[i - 1]) / span : 0f;
                return Vector2.Lerp(stroke.Points[i - 1], stroke.Points[i], k);
            }
            return stroke.Points[stroke.Points.Length - 1];
        }

        static Vector2 DirectionAt(TraceStroke stroke, float s)
        {
            Vector2 direction = PointAt(stroke, s + 0.02f) - PointAt(stroke, Mathf.Max(0f, s - 0.02f));
            return direction.sqrMagnitude > 0.0000001f ? direction.normalized : Vector2.right;
        }

        /// <summary>
        /// The point of the stroke closest to the finger among those not behind "from" and not further than lookAhead in front of
        /// it, if that is within the tolerance. The window stops a finger near the end of a curly letter skipping ahead.
        /// </summary>
        static bool ProjectAhead(TraceStroke stroke, Vector2 finger, float from, float lookAhead, float tolerance, out float found)
        {
            found = from;
            if (stroke.Points.Length == 1)
            {
                found = 0f;
                return Vector2.Distance(finger, stroke.Points[0]) <= tolerance;
            }

            float limit = from + lookAhead;
            float bestDistance = tolerance;
            bool any = false;
            for (int i = 1; i < stroke.Points.Length; i++)
            {
                float start = stroke.Cumulative[i - 1];
                float end = stroke.Cumulative[i];
                if (end < from || start > limit) continue;

                Vector2 a = stroke.Points[i - 1];
                Vector2 b = stroke.Points[i];
                Vector2 edge = b - a;
                float length = edge.magnitude;
                float t = length > 0.0001f ? Mathf.Clamp01(Vector2.Dot(finger - a, edge) / (length * length)) : 0f;
                float s = Mathf.Clamp(start + t * length, from, limit);
                float distance = Vector2.Distance(finger, PointAt(stroke, s));

                if (distance > bestDistance) continue;
                bestDistance = distance;
                found = s;
                any = true;
            }
            return any;
        }

        // ----- Drawing the chalk line ------------------------------------------------------------

        /// <summary>Lays chalk up to the progress, checks whether the stroke is finished and moves on to the next one.</summary>
        TraceEvent DrawAndCheck(bool force = false)
        {
            TraceStroke stroke = strokes[strokeIndex];
            DrawChalkTo(stroke, progress);

            if (tipDot != null)
            {
                tipDot.gameObject.SetActive(true);
                tipDot.rectTransform.anchoredPosition = PointAt(stroke, progress);
            }

            float slack = Mathf.Min(square * EndSlackOfSquare, stroke.Length);
            bool finished = force || (touchedStroke && progress >= stroke.Length - slack);
            if (!finished) return TraceEvent.None;

            progress = stroke.Length;
            DrawChalkTo(stroke, progress);
            BattleEffects.Sparkle(WorldPoint(PointAt(stroke, stroke.Length)));
            Sfx.Play("lesson.letter_pop", WorldPoint(PointAt(stroke, stroke.Length)));

            strokeIndex++;
            progress = 0f;
            drawnTo = 0f;
            touchedStroke = false;
            if (tipDot != null) tipDot.gameObject.SetActive(false);

            if (strokeIndex >= strokes.Count)
            {
                if (startDot != null) startDot.gameObject.SetActive(false);
                return TraceEvent.LetterDone;
            }

            PlaceStartDot();
            return TraceEvent.StrokeDone;
        }

        void DrawChalkTo(TraceStroke stroke, float s)
        {
            float spacing = ChalkSize * 0.3f;
            if (drawnTo == 0f && chalkRoot != null && ChalkCountOfStroke() == 0) AddChalk(PointAt(stroke, 0f), 1f);

            while (drawnTo + spacing <= s)
            {
                drawnTo += spacing;
                AddChalk(PointAt(stroke, drawnTo), 1f);
            }
            if (s >= stroke.Length && drawnTo < stroke.Length) AddChalk(PointAt(stroke, stroke.Length), 1f);
        }

        int ChalkCountOfStroke()
        {
            return chalk.Count - strokeChalkStart;
        }

        void AddChalk(Vector2 position, float scale)
        {
            Image dot = LetterGlyph.CreateDot(chalkRoot, position, ChalkSize, Display.ChalkColor, Dot);
            dot.rectTransform.localScale = Vector3.one * scale;
            chalk.Add(dot);
        }

        void PlaceStartDot()
        {
            strokeChalkStart = chalk.Count;
            if (startDot == null || strokeIndex >= strokes.Count) return;

            startDot.gameObject.SetActive(true);
            startDot.rectTransform.anchoredPosition = strokes[strokeIndex].Points[0];
        }

        void Scratch(Vector2 boardPoint)
        {
            if (Time.time - lastScratch >= ScratchEverySeconds)
            {
                lastScratch = Time.time;
                Sfx.Play("lesson.trace_progress", WorldPoint(boardPoint));
            }
            if (Time.time - lastSparkle >= SparkleEverySeconds)
            {
                lastSparkle = Time.time;
                BattleEffects.Sparkle(WorldPoint(boardPoint));
            }
        }

        // ----- Small animations ------------------------------------------------------------------

        void AnimateMarkers()
        {
            if (!tracing) return;

            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            if (startDot != null && startDot.gameObject.activeSelf)
                startDot.rectTransform.localScale = Vector3.one * (0.85f + beat * 0.35f);
            if (tipDot != null && tipDot.gameObject.activeSelf)
                tipDot.rectTransform.localScale = Vector3.one * (0.9f + beat * 0.2f);
            if (ghostDot != null && ghostDot.gameObject.activeSelf)
                ghostDot.rectTransform.localScale = Vector3.one * (0.9f + beat * 0.3f);
        }

        void BuildArrow(RectTransform parent, Vector2 position, Vector2 direction)
        {
            var go = new GameObject("Arrow", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            // Two short bars make a chevron that points the way along the stroke.
            float size = square * ArrowSize;
            for (int side = -1; side <= 1; side += 2)
            {
                Image bar = LetterGlyph.CreateDot(rect, new Vector2(-size * 0.25f, side * size * 0.35f), size * 0.22f, ArrowColor, null);
                bar.rectTransform.sizeDelta = new Vector2(size, size * 0.22f);
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -side * 40f);
            }
        }

        IEnumerator GhostRoutine()
        {
            TraceStroke stroke = strokes[strokeIndex];
            int index = strokeIndex;
            float from = progress;
            float seconds = Mathf.Max(0.8f, (stroke.Length - from) / (square * GhostUnitsPerSecond));

            ghostDot.gameObject.SetActive(true);
            float sparkleAt = 0f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (!tracing || strokeIndex != index) break;

                Vector2 point = PointAt(stroke, Mathf.Lerp(from, stroke.Length, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
                ghostDot.rectTransform.anchoredPosition = point;
                if (Time.time >= sparkleAt)
                {
                    sparkleAt = Time.time + SparkleEverySeconds;
                    BattleEffects.Sparkle(WorldPoint(point));
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.25f);
            if (ghostDot != null) ghostDot.gameObject.SetActive(false);
            ghost = null;
        }

        IEnumerator Appear(RectTransform target)
        {
            if (target == null) yield break;

            var group = target.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            for (float elapsed = 0f; elapsed < 0.4f; elapsed += Time.deltaTime)
            {
                if (group == null) yield break;
                group.alpha = elapsed / 0.4f;
                yield return null;
            }
            if (group != null) group.alpha = 1f;
        }

        IEnumerator PopIn(RectTransform target, float delay)
        {
            if (target == null) yield break;

            target.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            for (float elapsed = 0f; elapsed < PopSeconds; elapsed += Time.deltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.one * LessonMotion.OutBack(elapsed / PopSeconds);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        /// <summary>A short scale bounce that ends back at normal size.</summary>
        IEnumerator Punch(RectTransform target, float peak)
        {
            for (float elapsed = 0f; elapsed < PopSeconds * 1.4f; elapsed += Time.deltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.one * (1f + (peak - 1f) * LessonMotion.Hump(elapsed / (PopSeconds * 1.4f)));
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }
    }
}
