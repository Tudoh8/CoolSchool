using System.Collections;
using CoolSchool.Lessons;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The treasure map in the top centre of the screen: a parchment with a picture for every landmark on the route, a dotted
    /// path between them and a red X at the end. Pictures only, no words. Each landmark that is reached gets a green tick and
    /// a glowing frame slides to the next picture, which pulses big and bright (the others are dimmer), so he always sees where to
    /// go next. After the last picture the X gets the frame.
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
        // The picture to go to is bigger than the rest and pulses around that size; the others are dimmer.
        const float CurrentScale = 1.3f;
        const float DoneTint = 0.85f;
        const float WaitingTint = 0.55f;
        // The glowing frame behind it: size as a multiple of the picture, how fast it slides over to the next one.
        const float HighlightSize = 1.9f;
        const float HighlightFollow = 14f;
        static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
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
        Color[] iconBaseColor;
        Color finalBaseColor = Color.white;
        Image highlight;
        bool highlightPlaced;
        static Sprite glowSprite;
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
            if (finalX != null)
            {
                finalBaseScale = finalX.rectTransform.localScale;
                finalBaseColor = finalX.color;
            }

            iconBaseColor = new Color[stepIcons.Length];
            for (int i = 0; i < iconBaseColor.Length; i++)
                iconBaseColor[i] = stepIcons[i] != null ? stepIcons[i].color : Color.white;
            CreateHighlight();

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

            // The frame starts on the first picture.
            highlightPlaced = false;
            UpdateHighlight(1f);

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

                // The picture to go to next is big, bright and pulses; the ones done are a little dimmer, the ones to come dimmer still.
                bool current = i == reached;
                float scale = current ? CurrentScale * pulse : 1f;
                stepIcons[i].rectTransform.localScale = iconBaseScale[i] * scale;
                stepIcons[i].color = Tinted(iconBaseColor[i], current ? 1f : (i < reached ? DoneTint : WaitingTint));
            }

            for (int i = 0; i < reached && stepChecks != null && i < stepChecks.Length; i++)
            {
                if (stepChecks[i] == null) continue;

                float age = (Time.time - checkPopTime[i]) / CheckPopSeconds;
                stepChecks[i].rectTransform.localScale = checkBaseScale[i] * LessonMotion.OutBack(age);
            }

            // When every picture is done the X is the thing to go to.
            if (finalX != null)
            {
                bool xTurn = visibleSteps > 0 && reached >= visibleSteps;
                finalX.rectTransform.localScale = finalBaseScale * (xTurn ? CurrentScale * pulse : 1f);
                finalX.color = Tinted(finalBaseColor, xTurn ? 1f : WaitingTint);
            }

            UpdateHighlight(1f - Mathf.Exp(-HighlightFollow * Time.unscaledDeltaTime));
        }

        static Color Tinted(Color color, float tint)
        {
            return new Color(color.r * tint, color.g * tint, color.b * tint, color.a);
        }

        // ----- The glowing frame -------------------------------------------------------------------

        /// <summary>A soft glow with a bright ring in it, behind the pictures (in front of the dots), made here so the builder need not supply a sprite.</summary>
        void CreateHighlight()
        {
            if (panel == null) return;

            var go = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            go.layer = panel.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            highlight = go.GetComponent<Image>();
            highlight.sprite = GlowSprite();
            highlight.raycastTarget = false;
            highlight.color = HighlightColor;
            go.SetActive(false);

            if (stepIcons.Length > 0 && stepIcons[0] != null)
                rect.SetSiblingIndex(stepIcons[0].transform.GetSiblingIndex());
        }

        /// <summary>
        /// The frame follows the picture to go to next (the X after the last picture): it slides over when the next picture
        /// becomes the one, and glows brighter and dimmer. `follow` is how much of the way it goes this frame (1 = jump there).
        /// </summary>
        void UpdateHighlight(float follow)
        {
            if (highlight == null) return;

            int current = Mathf.Min(reached, visibleSteps);
            Image target = current < visibleSteps && current < stepIcons.Length ? stepIcons[current] : finalX;
            bool show = visibleSteps > 0 && target != null && target.gameObject.activeInHierarchy;
            highlight.gameObject.SetActive(show);
            if (!show) return;

            RectTransform rect = highlight.rectTransform;
            Vector3 wanted = target.rectTransform.position;
            rect.position = highlightPlaced ? Vector3.Lerp(rect.position, wanted, follow) : wanted;
            highlightPlaced = true;

            float baseSize = Mathf.Max(target.rectTransform.rect.width, target.rectTransform.rect.height);
            float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;
            float size = baseSize * HighlightSize * pulse;
            rect.sizeDelta = new Vector2(size, size);

            Color color = HighlightColor;
            color.a = 0.75f + Mathf.Sin(Time.time * PulseSpeed) * 0.25f;
            highlight.color = color;
        }

        static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TreasureMapGlow",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float disc = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - distance)) * 0.6f;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.8f) / 0.1f);
                    byte alpha = (byte)(Mathf.Max(disc, ring) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            glowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return glowSprite;
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
