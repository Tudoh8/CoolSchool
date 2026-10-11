using System.Collections;
using System.Collections.Generic;
using CoolSchool.Lessons;
using UnityEngine;
using UnityEngine.UI;

namespace CoolSchool.Shop
{
    /// <summary>
    /// The picture above the customer's head: a big white speech bubble with a cone and the ordered scoops stacked on it.
    /// A scoop that is still missing is a pale outlined circle of its colour, a scoop that is on the cone is solid.
    /// No words and no digits: the number is the circles. It follows the customer on the screen and stays inside it.
    /// </summary>
    public class OrderBubble : MonoBehaviour
    {
        [Header("Pictures (ModularGameUIKit shapes and a generated triangle)")]
        [SerializeField] Sprite bubbleSprite;
        [SerializeField] Sprite circleSprite;
        [SerializeField] Sprite outlineSprite;
        [Tooltip("A white triangle with the point down: the cone, and the tail of the bubble.")]
        [SerializeField] Sprite triangleSprite;

        [Header("Size")]
        [Tooltip("Scales the whole bubble. 1 is about 40% of the screen height with three scoops.")]
        [SerializeField] float scale = 1f;

        // Sizes in canvas units (the canvas is 1080 high). The root's origin is the tip of the tail.
        const float BubbleWidth = 270f;
        const float BubbleHeight = 380f;
        const float TailHeight = 40f;
        const float TailWidth = 50f;
        const float ConeWidth = 118f;
        const float ConeHeight = 150f;
        const float ScoopSize = 84f;
        const float ScoopSpacing = 56f;
        const float Margin = 14f;
        const int MaxSlots = 3;

        static readonly Color ConeColour = new Color(0.93f, 0.68f, 0.3f, 1f);
        static readonly Color PaleAlpha = new Color(1f, 1f, 1f, 0.28f);

        sealed class Slot
        {
            public RectTransform Rect;
            public Image Fill;
            public Image Ring;
            public Color Colour;
            public bool Filled;
        }

        RectTransform canvasRect;
        RectTransform root;
        Transform anchor;
        readonly List<Slot> slots = new List<Slot>();
        Coroutine popRoutine;
        Coroutine pulseRoutine;

        public bool IsShowing { get; private set; }

        /// <summary>Shows the bubble over the anchor. The slots go from the bottom of the stack to the top.</summary>
        public void Show(Transform above, IReadOnlyList<Flavour> flavours)
        {
            Build();
            if (root == null) return;

            StopAnimations();
            Clear();
            anchor = above;

            // The top scoop is made first, so it is drawn first and each scoop below it overlaps it, like a real stack.
            int count = Mathf.Min(flavours.Count, MaxSlots);
            var built = new Slot[count];
            for (int i = count - 1; i >= 0; i--)
                built[i] = BuildSlot(i, FlavourInfo.ColourOf(flavours[i]));
            slots.AddRange(built);

            IsShowing = true;
            root.gameObject.SetActive(true);
            FollowAnchor();
            popRoutine = StartCoroutine(ScaleRoutine(0f, 1f, 0.4f, true));
        }

        /// <summary>The slot's scoop is on the cone: it turns solid and pops.</summary>
        public void FillSlot(int index)
        {
            if (index < 0 || index >= slots.Count || slots[index].Filled) return;

            Slot slot = slots[index];
            slot.Filled = true;
            slot.Fill.color = slot.Colour;
            slot.Ring.color = Color.Lerp(slot.Colour, Color.black, 0.35f);
            if (isActiveAndEnabled) StartCoroutine(PopSlot(slot.Rect));
        }

        /// <summary>The bubble bounces a few times: "look here".</summary>
        public void Pulse()
        {
            if (!IsShowing || !isActiveAndEnabled) return;

            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseRoutine());
        }

        /// <summary>Pops away.</summary>
        public void Hide()
        {
            if (!IsShowing) return;

            IsShowing = false;
            if (!isActiveAndEnabled)
            {
                HideNow();
                return;
            }

            StopAnimations();
            popRoutine = StartCoroutine(ScaleRoutine(1f, 0f, 0.25f, false));
        }

        public void HideNow()
        {
            IsShowing = false;
            StopAnimations();
            Clear();
            if (root != null) root.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            HideNow();
        }

        void LateUpdate()
        {
            if (root != null && root.gameObject.activeSelf) FollowAnchor();
        }

        // ----- Building --------------------------------------------------------------------------

        void Build()
        {
            if (root != null) return;

            var canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;

            root = NewRect("Bubble", transform);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(BubbleWidth, BubbleHeight + TailHeight);

            // Back to front: bubble, tail, cone.
            Image back = NewImage("Back", root, bubbleSprite, Color.white, new Vector2(BubbleWidth, BubbleHeight), TailHeight + BubbleHeight * 0.5f);
            back.type = bubbleSprite != null && bubbleSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            NewImage("Tail", root, triangleSprite, Color.white, new Vector2(TailWidth, TailHeight + 6f), (TailHeight + 6f) * 0.5f);

            // The cone points down; its rim is below the first scoop.
            float coneCentre = TailHeight + 24f + ConeHeight * 0.5f;
            NewImage("Cone", root, triangleSprite, ConeColour, new Vector2(ConeWidth, ConeHeight), coneCentre);

            root.gameObject.SetActive(false);
        }

        Slot BuildSlot(int index, Color colour)
        {
            float rimY = TailHeight + 24f + ConeHeight;
            float y = rimY + ScoopSize * 0.3f + index * ScoopSpacing;

            RectTransform rect = NewRect("Scoop" + (index + 1), root);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(ScoopSize, ScoopSize);

            var pale = new Color(colour.r, colour.g, colour.b, PaleAlpha.a);
            Image fill = NewImage("Fill", rect, circleSprite, pale, new Vector2(ScoopSize, ScoopSize), ScoopSize * 0.5f);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            Image ring = NewImage("Ring", rect, outlineSprite, colour, new Vector2(ScoopSize, ScoopSize), ScoopSize * 0.5f);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.anchoredPosition = Vector2.zero;

            return new Slot { Rect = rect, Fill = fill, Ring = ring, Colour = colour };
        }

        RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>An image anchored to the bottom middle of its parent, with its middle at the given height.</summary>
        Image NewImage(string objectName, Transform parent, Sprite sprite, Color colour, Vector2 size, float centreY)
        {
            RectTransform rect = NewRect(objectName, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, centreY);
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        void Clear()
        {
            foreach (Slot slot in slots)
                if (slot.Rect != null) Destroy(slot.Rect.gameObject);
            slots.Clear();
        }

        void StopAnimations()
        {
            if (popRoutine != null) StopCoroutine(popRoutine);
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            popRoutine = null;
            pulseRoutine = null;
            if (root != null) root.localScale = Vector3.one * scale;
        }

        // ----- Following the customer ------------------------------------------------------------

        /// <summary>The tail tip sits on the anchor on the screen, pushed back inside the screen when it would stick out.</summary>
        void FollowAnchor()
        {
            Camera view = Camera.main;
            if (anchor == null || view == null || canvasRect == null) return;

            Vector3 screen = view.WorldToScreenPoint(anchor.position);
            if (screen.z <= 0f) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

            Rect bounds = canvasRect.rect;
            float halfWidth = BubbleWidth * 0.5f * scale + Margin;
            float height = (BubbleHeight + TailHeight) * scale + Margin;
            local.x = Mathf.Clamp(local.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            local.y = Mathf.Clamp(local.y, bounds.yMin + Margin, bounds.yMax - height);
            root.anchoredPosition = local;
        }

        // ----- Animations ------------------------------------------------------------------------

        IEnumerator ScaleRoutine(float from, float to, float seconds, bool overshoot)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / seconds;
                float k = overshoot ? LessonMotion.OutBack(t) : Mathf.SmoothStep(0f, 1f, t);
                root.localScale = Vector3.one * (Mathf.LerpUnclamped(from, to, k) * scale);
                yield return null;
            }

            root.localScale = Vector3.one * (to * scale);
            popRoutine = null;
            if (to <= 0f)
            {
                Clear();
                root.gameObject.SetActive(false);
            }
        }

        IEnumerator PopSlot(RectTransform rect)
        {
            const float seconds = 0.4f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                if (rect == null) yield break;

                rect.localScale = Vector3.one * (1f + LessonMotion.Hump(elapsed / seconds) * 0.45f);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        IEnumerator PulseRoutine()
        {
            const float seconds = 1.2f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float wobble = 1f + Mathf.Abs(LessonMotion.DampedWobble(elapsed / seconds, 3f)) * 0.18f;
                root.localScale = Vector3.one * (wobble * scale);
                yield return null;
            }

            root.localScale = Vector3.one * scale;
            pulseRoutine = null;
        }
    }
}
