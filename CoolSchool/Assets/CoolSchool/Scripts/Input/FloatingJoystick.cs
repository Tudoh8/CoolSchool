using UnityEngine;
using UnityEngine.EventSystems;

namespace CoolSchool.Input
{
    /// <summary>
    /// A joystick that rests visibly (semi-transparent) at its starting spot and jumps to wherever
    /// the thumb touches inside its zone, going fully opaque until release.
    /// Put this on a large transparent Image (e.g. the left half of the screen)
    /// on a Screen Space - Overlay canvas.
    /// </summary>
    public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [Tooltip("How far (in canvas units) the handle can travel from the centre.")]
        [SerializeField] float radius = 110f;
        [Tooltip("Ignore tiny thumb wobbles below this fraction of the radius.")]
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.1f;
        [Tooltip("Hide the joystick completely while nobody is touching it.")]
        [SerializeField] bool hideWhenIdle = false;
        [Tooltip("Opacity while resting, so a small child can see where to put a thumb.")]
        [SerializeField, Range(0f, 1f)] float idleAlpha = 0.5f;

        public Vector2 Value { get; private set; }

        Canvas canvas;
        CanvasGroup canvasGroup;
        Vector2 restAnchoredPosition;
        int activePointer = int.MinValue;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (!background.TryGetComponent(out canvasGroup))
                canvasGroup = background.gameObject.AddComponent<CanvasGroup>();

            restAnchoredPosition = background.anchoredPosition;
            ShowIdle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != int.MinValue) return;

            activePointer = eventData.pointerId;
            background.position = eventData.position;
            ShowActive();
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;

            float scaledRadius = radius * canvas.scaleFactor;
            Vector2 offset = eventData.position - (Vector2)background.position;
            Vector2 raw = Vector2.ClampMagnitude(offset / scaledRadius, 1f);

            Value = raw.magnitude < deadZone ? Vector2.zero : raw;
            handle.position = background.position + (Vector3)(raw * scaledRadius);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;

            activePointer = int.MinValue;
            Value = Vector2.zero;
            background.anchoredPosition = restAnchoredPosition;
            handle.position = background.position;
            ShowIdle();
        }

        void OnDisable()
        {
            activePointer = int.MinValue;
            Value = Vector2.zero;
        }

        void ShowActive()
        {
            background.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
        }

        void ShowIdle()
        {
            background.gameObject.SetActive(!hideWhenIdle);
            canvasGroup.alpha = idleAlpha;
        }
    }
}
