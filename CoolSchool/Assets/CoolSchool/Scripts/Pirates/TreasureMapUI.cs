using System.Collections;
using CoolSchool.Lessons;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The treasure map in the top centre of the screen: a parchment with a picture for every landmark on the route, a dotted
    /// path between them and a red X at the end. Pictures only, no words. Each landmark that is reached gets a green tick and
    /// the next picture pulses, so he always sees where to go next.
    /// The pictures, ticks, dots and the X are laid out along the middle of the parchment by this script every time the map
    /// is shown (the route has two or three landmarks), so the builder only has to create them with the right sizes.
    /// </summary>
    public class TreasureMapUI : MonoBehaviour
    {
        [Tooltip("The parchment. Must be a child of the object this script is on, because it is switched off and on.")]
        [SerializeField] RectTransform panel;
        [SerializeField] Image[] stepIcons;
        [Tooltip("The green tick over each picture.")]
        [SerializeField] Image[] stepChecks;
        [SerializeField] Image finalX;
        [Tooltip("The dotted path between the steps. They are shared out evenly over the gaps.")]
        [SerializeField] RectTransform[] dots;

        const float UnrollSeconds = 0.5f;
        const float RollUpSeconds = 0.22f;
        const float CheckPopSeconds = 0.35f;
        const float PulseSpeed = 7f;
        const float PulseAmount = 0.12f;
        // How far (as a fraction of a picture's size) the tick sits down and right of the picture's middle.
        const float CheckShift = 0.24f;
        const float DotGapPadding = 8f;
        const float UnwalkedDotAlpha = 0.5f;

        int visibleSteps;
        int reached;
        Vector3 panelBaseScale = Vector3.one;
        Vector3[] iconBaseScale;
        Vector3[] checkBaseScale;
        Vector3 finalBaseScale = Vector3.one;
        float[] checkPopTime;
        int[] dotGap;
        Vector3[] dotBaseScale;
        Coroutine panelRoutine;

        void Awake()
        {
            if (stepIcons == null) stepIcons = new Image[0];
            if (stepChecks == null) stepChecks = new Image[0];
            if (dots == null) dots = new RectTransform[0];

            if (panel != null)
            {
                panelBaseScale = panel.localScale;
                // The panel must be a child, or switching it off would stop this script.
                if (panel.gameObject != gameObject) panel.gameObject.SetActive(false);
            }

            iconBaseScale = BaseScales(stepIcons);
            checkBaseScale = BaseScales(stepChecks);
            checkPopTime = new float[stepChecks != null ? stepChecks.Length : 0];
            if (finalX != null) finalBaseScale = finalX.rectTransform.localScale;

            dotGap = new int[dots != null ? dots.Length : 0];
            dotBaseScale = new Vector3[dotGap.Length];
            for (int i = 0; i < dotGap.Length; i++)
            {
                dotGap[i] = -1;
                if (dots[i] != null) dotBaseScale[i] = dots[i].localScale;
            }
        }

        static Vector3[] BaseScales(Image[] images)
        {
            var scales = new Vector3[images != null ? images.Length : 0];
            for (int i = 0; i < scales.Length; i++)
                scales[i] = images[i] != null ? images[i].rectTransform.localScale : Vector3.one;
            return scales;
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Unrolls the map with a picture for every landmark on the route (at most as many as there are slots), then the X.</summary>
        public void Show(Sprite[] icons)
        {
            if (panel == null) return;

            int slots = stepIcons != null ? stepIcons.Length : 0;
            visibleSteps = icons != null ? Mathf.Min(icons.Length, slots) : 0;
            reached = 0;

            for (int i = 0; i < slots; i++)
            {
                if (stepIcons[i] == null) continue;

                bool used = i < visibleSteps;
                stepIcons[i].gameObject.SetActive(used);
                if (used)
                {
                    stepIcons[i].sprite = icons[i];
                    stepIcons[i].preserveAspect = true;
                    stepIcons[i].rectTransform.localScale = iconBaseScale[i];
                }
            }

            if (stepChecks != null)
                for (int i = 0; i < stepChecks.Length; i++)
                    if (stepChecks[i] != null) stepChecks[i].gameObject.SetActive(false);

            if (finalX != null)
            {
                finalX.gameObject.SetActive(true);
                finalX.rectTransform.localScale = finalBaseScale;
            }

            // Laid out at full size, even if the parchment was in the middle of rolling up or out.
            panel.localScale = panelBaseScale;
            panel.gameObject.SetActive(true);
            LayOut();
            RefreshDots();

            if (panelRoutine != null) StopCoroutine(panelRoutine);
            panelRoutine = StartCoroutine(UnrollRoutine());
        }

        /// <summary>The first `count` landmarks are done: they get a tick with a pop, and the next picture pulses.</summary>
        public void SetReached(int count)
        {
            count = Mathf.Clamp(count, 0, visibleSteps);
            if (count == reached) return;

            for (int i = reached; i < count; i++)
            {
                if (stepChecks == null || i >= stepChecks.Length || stepChecks[i] == null) continue;

                stepChecks[i].gameObject.SetActive(true);
                stepChecks[i].rectTransform.localScale = Vector3.zero;
                checkPopTime[i] = Time.time;
            }

            // Pictures that were done and are not any more (a restart) lose their tick.
            for (int i = count; i < reached; i++)
                if (stepChecks != null && i < stepChecks.Length && stepChecks[i] != null) stepChecks[i].gameObject.SetActive(false);

            reached = count;
            RefreshDots();
        }

        /// <summary>Rolls the map up and away.</summary>
        public void Hide()
        {
            if (panel == null) return;

            if (panelRoutine != null)
            {
                StopCoroutine(panelRoutine);
                panelRoutine = null;
            }

            if (!isActiveAndEnabled || !panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panelRoutine = StartCoroutine(RollUpRoutine());
        }

        // ----- Layout ------------------------------------------------------------------------------

        /// <summary>Spreads the pictures and the X evenly along the middle of the parchment and shares the dots out over the gaps.</summary>
        void LayOut()
        {
            int slots = visibleSteps + 1;
            Rect rect = panel.rect;
            float half = ElementHalfSize();
            float padding = Mathf.Min(half + 24f, rect.width * 0.25f);
            float left = rect.xMin + padding;
            float right = rect.xMax - padding;
            float y = rect.center.y;

            var points = new Vector2[slots];
            for (int i = 0; i < slots; i++)
            {
                float k = slots > 1 ? i / (float)(slots - 1) : 0.5f;
                points[i] = new Vector2(Mathf.Lerp(left, right, k), y);
            }

            for (int i = 0; i < visibleSteps; i++)
            {
                Place(stepIcons[i], points[i]);
                if (stepChecks != null && i < stepChecks.Length && stepChecks[i] != null)
                {
                    float size = stepIcons[i].rectTransform.rect.width * stepIcons[i].rectTransform.lossyScale.x
                        / Mathf.Max(0.0001f, panel.lossyScale.x);
                    Place(stepChecks[i], points[i] + new Vector2(size * CheckShift, -size * CheckShift));
                }
            }
            if (finalX != null) Place(finalX, points[slots - 1]);

            // The dots: the same number in every gap, between the edges of the two pictures.
            int gaps = visibleSteps;
            int perGap = gaps > 0 ? dots.Length / gaps : 0;
            for (int d = 0; d < dots.Length; d++)
            {
                if (dots[d] == null) continue;

                int gap = perGap > 0 ? d / perGap : gaps;
                bool used = gap < gaps && perGap > 0;
                dotGap[d] = used ? gap : -1;
                dots[d].gameObject.SetActive(used);
                if (!used) continue;

                int index = d % perGap;
                Vector2 from = points[gap];
                Vector2 to = points[gap + 1];
                Vector2 direction = (to - from).normalized;
                Vector2 a = from + direction * (half + DotGapPadding);
                Vector2 b = to - direction * (half + DotGapPadding);
                dots[d].position = panel.TransformPoint(Vector2.Lerp(a, b, (index + 1f) / (perGap + 1f)));
            }
        }

        /// <summary>Half the size of a picture in the parchment's own units (all pictures are the same size).</summary>
        float ElementHalfSize()
        {
            if (stepIcons == null || stepIcons.Length == 0 || stepIcons[0] == null) return 60f;

            RectTransform icon = stepIcons[0].rectTransform;
            float width = icon.rect.width * icon.lossyScale.x / Mathf.Max(0.0001f, panel.lossyScale.x);
            return Mathf.Max(20f, width * 0.5f);
        }

        /// <summary>Puts the middle of the element on a point of the parchment (given in the parchment's own units).</summary>
        void Place(Image element, Vector2 point)
        {
            if (element == null) return;

            element.rectTransform.position = panel.TransformPoint(point);
        }

        /// <summary>Dots on a stretch that has been walked are fully there; the rest are faint and small.</summary>
        void RefreshDots()
        {
            for (int d = 0; d < dots.Length; d++)
            {
                if (dots[d] == null || dotGap[d] < 0) continue;

                bool walked = reached > dotGap[d];
                dots[d].localScale = dotBaseScale[d] * (walked ? 1f : 0.75f);

                var image = dots[d].GetComponent<Image>();
                if (image == null) continue;

                Color color = image.color;
                color.a = walked ? 1f : UnwalkedDotAlpha;
                image.color = color;
            }
        }

        // ----- Animation ---------------------------------------------------------------------------

        void Update()
        {
            if (panel == null || !panel.gameObject.activeSelf) return;

            float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;

            for (int i = 0; i < visibleSteps; i++)
            {
                if (stepIcons[i] == null) continue;

                // The picture to go to next pulses.
                float scale = i == reached ? pulse : 1f;
                stepIcons[i].rectTransform.localScale = iconBaseScale[i] * scale;
            }

            for (int i = 0; i < reached && stepChecks != null && i < stepChecks.Length; i++)
            {
                if (stepChecks[i] == null) continue;

                float age = (Time.time - checkPopTime[i]) / CheckPopSeconds;
                stepChecks[i].rectTransform.localScale = checkBaseScale[i] * LessonMotion.OutBack(age);
            }

            // When every picture is done the X is the thing to go to.
            if (finalX != null)
                finalX.rectTransform.localScale = finalBaseScale * (visibleSteps > 0 && reached >= visibleSteps ? pulse : 1f);
        }

        IEnumerator UnrollRoutine()
        {
            for (float elapsed = 0f; elapsed < UnrollSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / UnrollSeconds;
                panel.localScale = new Vector3(panelBaseScale.x * LessonMotion.OutBack(t), panelBaseScale.y * Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, t)), panelBaseScale.z);
                yield return null;
            }

            panel.localScale = panelBaseScale;
            panelRoutine = null;
        }

        IEnumerator RollUpRoutine()
        {
            Vector3 from = panel.localScale;
            for (float elapsed = 0f; elapsed < RollUpSeconds; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / RollUpSeconds);
                panel.localScale = new Vector3(Mathf.Lerp(from.x, 0f, t), from.y, from.z);
                yield return null;
            }

            panel.localScale = panelBaseScale;
            panel.gameObject.SetActive(false);
            panelRoutine = null;
        }

        void OnDisable()
        {
            // Switched off mid-animation: leave the parchment tidy for next time.
            panelRoutine = null;
            if (panel != null && panel.gameObject != gameObject)
            {
                panel.localScale = panelBaseScale;
                panel.gameObject.SetActive(false);
            }
        }
    }
}
