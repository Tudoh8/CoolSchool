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
    /// is shown, for any number of landmarks: the pictures shrink to fit, the dots are shared out over every gap and the
    /// parchment narrows on a squarer screen so it stays clear of the buttons. The builder only has to create one of each as
    /// a template (it makes four pictures and twelve dots to start with); this script copies them when the route needs more.
    /// </summary>
    public class TreasureMapUI : MonoBehaviour
    {
        [Tooltip("The parchment. Must be a child of the object this script is on, because it is switched off and on.")]
        [SerializeField] RectTransform panel;
        [SerializeField] Image[] stepIcons;
        [Tooltip("The green tick over each picture.")]
        [SerializeField] Image[] stepChecks;
        [SerializeField] Image finalX;
        [Tooltip("The dotted path between the steps. They are shared out evenly over the gaps; copies are made when the route needs more.")]
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
        // The glowing frame behind it: size as a multiple of the big picture, how fast it slides over to the next one.
        const float HighlightSize = 1.4f;
        const float HighlightFollow = 14f;
        static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        // How far (as a fraction of a picture's size) the tick sits down and right of the picture's middle.
        const float CheckShift = 0.24f;
        const float DotGapPadding = 8f;
        const float UnwalkedDotAlpha = 0.5f;
        // How many landmarks the map can show (the adventure uses two or three).
        const int MaxSteps = 8;
        // Layout, in the parchment's own units: room kept at the parchment's ends, the smallest a picture may get, and the room a
        // gap needs between the biggest picture and the next for a few dots (and how far apart they sit).
        const float EdgePadding = 12f;
        const float MinPictureSize = 56f;
        const float MinDotRoom = 54f;
        const float DotSpacing = 18f;
        const int MaxDotsPerGap = 6;
        // The back button reaches 370 from the left edge of the 1920x1080 canvas and the golden sword badge 400 from the right,
        // so the parchment keeps this much clear on both sides (it only matters on squarer screens).
        const float SideReserve = 400f;
        const float MinPanelWidth = 360f;

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
        Vector2[] dotTarget;
        Vector2[] dotCurrent;
        Vector2[] slotPoint;
        float[] slotSize;
        int dotsPerGap;
        // How much the pictures are shrunk (or not) to fit this route on the parchment.
        float layoutFit = 1f;
        float panelBaseWidth;
        Coroutine panelRoutine;

        void Awake()
        {
            if (stepIcons == null) stepIcons = new Image[0];
            if (stepChecks == null) stepChecks = new Image[0];
            if (dots == null) dots = new RectTransform[0];

            if (panel != null)
            {
                panelBaseScale = panel.localScale;
                panelBaseWidth = panel.rect.width;
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

            dotGap = new int[dots.Length];
            dotTarget = new Vector2[dots.Length];
            dotCurrent = new Vector2[dots.Length];
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

        /// <summary>Unrolls the map with a picture for every landmark on the route (up to MaxSteps), then the X.</summary>
        public void Show(Sprite[] icons)
        {
            if (panel == null) return;

            // More slots are copied from the first one when the route is longer than what the builder made.
            EnsureSlots(icons != null ? Mathf.Min(icons.Length, MaxSteps) : 0);
            int slots = stepIcons.Length;
            visibleSteps = icons != null ? Mathf.Min(icons.Length, Mathf.Min(slots, MaxSteps)) : 0;
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
            PlanDots(true);
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

            // The big picture is a new one, so the dots around it make room (or close up) by sliding.
            PlanDots(false);
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

        /// <summary>
        /// Makes sure there are at least `count` picture slots and ticks by copying the first of each (the builder made a few;
        /// a longer route needs more). New ones start switched off, like the builder's.
        /// </summary>
        void EnsureSlots(int count)
        {
            if (stepIcons.Length == 0 || stepIcons[0] == null) return;

            int oldIcons = stepIcons.Length;
            if (oldIcons < count)
            {
                System.Array.Resize(ref stepIcons, count);
                System.Array.Resize(ref iconBaseScale, count);
                System.Array.Resize(ref iconBaseColor, count);
                for (int i = oldIcons; i < count; i++)
                {
                    stepIcons[i] = Instantiate(stepIcons[0], stepIcons[0].transform.parent);
                    stepIcons[i].name = $"Step_{i + 1}";
                    stepIcons[i].gameObject.SetActive(false);
                    iconBaseScale[i] = iconBaseScale[0];
                    iconBaseColor[i] = iconBaseColor[0];
                }
            }

            int oldChecks = stepChecks.Length;
            if (oldChecks > 0 && stepChecks[0] != null && oldChecks < count)
            {
                System.Array.Resize(ref stepChecks, count);
                System.Array.Resize(ref checkBaseScale, count);
                System.Array.Resize(ref checkPopTime, count);
                for (int i = oldChecks; i < count; i++)
                {
                    stepChecks[i] = Instantiate(stepChecks[0], stepChecks[0].transform.parent);
                    stepChecks[i].name = $"Tick_{i + 1}";
                    stepChecks[i].gameObject.SetActive(false);
                    checkBaseScale[i] = checkBaseScale[0];
                }
            }

            // Copies were added at the end of the parchment: pictures, then ticks, then the X, all in front of the dots and frame.
            for (int i = 0; i < stepIcons.Length; i++)
                if (stepIcons[i] != null) stepIcons[i].transform.SetAsLastSibling();
            for (int i = 0; i < stepChecks.Length; i++)
                if (stepChecks[i] != null) stepChecks[i].transform.SetAsLastSibling();
            if (finalX != null) finalX.transform.SetAsLastSibling();
        }

        /// <summary>Makes sure there are at least `count` dots by copying the first one. New ones go right after it, behind the pictures.</summary>
        void EnsureDots(int count)
        {
            if (dots.Length == 0 || dots[0] == null || dots.Length >= count) return;

            int old = dots.Length;
            System.Array.Resize(ref dots, count);
            System.Array.Resize(ref dotGap, count);
            System.Array.Resize(ref dotBaseScale, count);
            System.Array.Resize(ref dotTarget, count);
            System.Array.Resize(ref dotCurrent, count);
            for (int d = old; d < count; d++)
            {
                dots[d] = Instantiate(dots[0], dots[0].parent);
                dots[d].name = $"Dot_{d + 1}";
                dots[d].SetSiblingIndex(dots[0].GetSiblingIndex() + 1);
                dots[d].gameObject.SetActive(false);
                dotGap[d] = -1;
                dotBaseScale[d] = dotBaseScale[0];
            }
        }

        /// <summary>
        /// Spreads the pictures and the X evenly along the middle of the parchment. The pictures shrink when there are many, so
        /// that the biggest (pulsing) picture never reaches the next one and every gap keeps room for dots.
        /// </summary>
        void LayOut()
        {
            FitPanelWidth();

            int slots = visibleSteps + 1;
            Rect rect = panel.rect;
            float baseSize = PictureBaseSize();
            float size = PictureSize(slots, rect, baseSize);
            layoutFit = size / baseSize;

            // The ends keep room for the biggest a picture gets, so nothing sticks out of the parchment.
            float peakHalf = 0.5f * size * CurrentScale * (1f + PulseAmount);
            float left = rect.xMin + EdgePadding + peakHalf;
            float right = rect.xMax - EdgePadding - peakHalf;
            float y = rect.center.y;

            slotPoint = new Vector2[slots];
            slotSize = new float[slots];
            float xSize = finalX != null ? finalX.rectTransform.rect.width * finalBaseScale.x * layoutFit : size;
            for (int i = 0; i < slots; i++)
            {
                float k = slots > 1 ? i / (float)(slots - 1) : 0.5f;
                slotPoint[i] = new Vector2(Mathf.Lerp(left, right, k), y);
                slotSize[i] = i < visibleSteps ? size : xSize;
            }

            for (int i = 0; i < visibleSteps; i++)
            {
                Place(stepIcons[i], slotPoint[i]);
                if (stepIcons[i] != null) stepIcons[i].rectTransform.localScale = iconBaseScale[i] * layoutFit;
                if (i < stepChecks.Length && stepChecks[i] != null)
                    Place(stepChecks[i], slotPoint[i] + new Vector2(size * CheckShift, -size * CheckShift));
            }
            if (finalX != null)
            {
                Place(finalX, slotPoint[slots - 1]);
                finalX.rectTransform.localScale = finalBaseScale * layoutFit;
            }

            // The dots: the same number in every gap, as many as fit in the tightest one (next to the big picture).
            int gaps = visibleSteps;
            float spacing = slots > 1 ? (right - left) / gaps : 0f;
            float tightestGap = spacing - 0.5f * size * (CurrentScale * (1f + PulseAmount) + 1f) - 2f * DotGapPadding;
            int perGap = Mathf.Clamp(Mathf.FloorToInt(tightestGap / DotSpacing) - 1, 0, MaxDotsPerGap);
            EnsureDots(gaps * perGap);
            dotsPerGap = gaps > 0 && dots.Length > 0 ? Mathf.Min(perGap, dots.Length / gaps) : 0;
        }

        /// <summary>The unshrunk size of a picture in the parchment's own units (all pictures are the same size).</summary>
        float PictureBaseSize()
        {
            if (stepIcons.Length == 0 || stepIcons[0] == null) return 130f;

            return Mathf.Max(20f, stepIcons[0].rectTransform.rect.width * iconBaseScale[0].x);
        }

        /// <summary>
        /// The biggest picture size at which `slots` pictures fit: the pulsing big picture and its neighbour leave a gap with
        /// room for dots, and the big picture fits the parchment's height. Never more than the builder's size.
        /// </summary>
        static float PictureSize(int slots, Rect rect, float baseSize)
        {
            float peak = 0.5f * CurrentScale * (1f + PulseAmount);
            float gaps = Mathf.Max(1, slots - 1);
            float usable = rect.width - 2f * EdgePadding;

            // The spacing between pictures is (usable - 2 * peak * size) / gaps; it must hold the big picture's half, the
            // neighbour's half, the two paddings and the room for dots.
            float size = (usable / gaps - MinDotRoom - 2f * DotGapPadding) / (2f * peak / gaps + peak + 0.5f);
            size = Mathf.Min(size, (rect.height - 2f * EdgePadding) / (2f * peak));
            return Mathf.Clamp(size, Mathf.Min(MinPictureSize, baseSize), baseSize);
        }

        /// <summary>
        /// Keeps the parchment clear of the back button (left) and the star jar and sword badge (right): on a 16:9 screen the
        /// builder's width fits, on a squarer one (4:3) it narrows. The screen is the holder this script is on.
        /// </summary>
        void FitPanelWidth()
        {
            var holder = transform as RectTransform;
            float screen = holder != null ? holder.rect.width : 0f;
            if (screen <= 0f || panelBaseWidth <= 0f) return;

            float width = Mathf.Clamp(screen - 2f * SideReserve, MinPanelWidth, panelBaseWidth);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        /// <summary>Puts the middle of the element on a point of the parchment (given in the parchment's own units).</summary>
        void Place(Image element, Vector2 point)
        {
            if (element == null) return;

            element.rectTransform.position = panel.TransformPoint(point);
        }

        /// <summary>
        /// Decides where every dot goes: evenly along each gap, from the edge of one picture to the edge of the next. The big
        /// picture is wider than the others, so its two gaps are shorter. `jump` puts the dots there at once; otherwise they slide.
        /// </summary>
        void PlanDots(bool jump)
        {
            int current = Mathf.Min(reached, visibleSteps);
            for (int d = 0; d < dots.Length; d++)
            {
                int gap = dotsPerGap > 0 ? d / dotsPerGap : -1;
                bool used = slotPoint != null && gap >= 0 && gap < visibleSteps;
                dotGap[d] = used ? gap : -1;
                if (dots[d] == null) continue;

                dots[d].gameObject.SetActive(used);
                if (!used) continue;

                Vector2 from = slotPoint[gap];
                Vector2 to = slotPoint[gap + 1];
                Vector2 direction = (to - from).normalized;
                Vector2 a = from + direction * (SlotRadius(gap, current) + DotGapPadding);
                Vector2 b = to - direction * (SlotRadius(gap + 1, current) + DotGapPadding);
                dotTarget[d] = Vector2.Lerp(a, b, (d % dotsPerGap + 1f) / (dotsPerGap + 1f));
                if (jump) dotCurrent[d] = dotTarget[d];
                dots[d].position = panel.TransformPoint(dotCurrent[d]);
            }
        }

        /// <summary>Half the room a picture takes: the big one at its most swollen, the others as they are.</summary>
        float SlotRadius(int slot, int current)
        {
            return 0.5f * slotSize[slot] * (slot == current ? CurrentScale * (1f + PulseAmount) : 1f);
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
                stepIcons[i].rectTransform.localScale = iconBaseScale[i] * (scale * layoutFit);
                stepIcons[i].color = Tinted(iconBaseColor[i], current ? 1f : (i < reached ? DoneTint : WaitingTint));
            }

            for (int i = 0; i < reached && stepChecks != null && i < stepChecks.Length; i++)
            {
                if (stepChecks[i] == null) continue;

                float age = (Time.time - checkPopTime[i]) / CheckPopSeconds;
                stepChecks[i].rectTransform.localScale = checkBaseScale[i] * (LessonMotion.OutBack(age) * layoutFit);
            }

            // The dots slide to their new places when the big picture moves on.
            float follow = 1f - Mathf.Exp(-HighlightFollow * Time.unscaledDeltaTime);
            for (int d = 0; d < dots.Length; d++)
            {
                if (dots[d] == null || dotGap[d] < 0) continue;

                dotCurrent[d] = Vector2.Lerp(dotCurrent[d], dotTarget[d], follow);
                dots[d].position = panel.TransformPoint(dotCurrent[d]);
            }

            // When every picture is done the X is the thing to go to.
            if (finalX != null)
            {
                bool xTurn = visibleSteps > 0 && reached >= visibleSteps;
                finalX.rectTransform.localScale = finalBaseScale * ((xTurn ? CurrentScale * pulse : 1f) * layoutFit);
                finalX.color = Tinted(finalBaseColor, xTurn ? 1f : WaitingTint);
            }

            UpdateHighlight(follow);
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

            // Under the dots, which are under the pictures.
            if (dots.Length > 0 && dots[0] != null)
                rect.SetSiblingIndex(dots[0].transform.GetSiblingIndex());
            else if (stepIcons.Length > 0 && stepIcons[0] != null)
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

            // As big around as the big picture (at its normal size, as it pulses too) times HighlightSize.
            Vector3 targetScale = target == finalX ? finalBaseScale : iconBaseScale[current];
            float baseSize = Mathf.Max(target.rectTransform.rect.width, target.rectTransform.rect.height) * targetScale.x * layoutFit;
            float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;
            float size = baseSize * CurrentScale * HighlightSize * pulse;
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
