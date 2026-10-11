using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolSchool.Input
{
    /// <summary>
    /// A big round on-screen button. Bounces when pressed so a small child
    /// can see that the tap worked. The picture on it can change, e.g. a sword while fighting.
    /// </summary>
    public class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float pressedScale = 0.85f;
        [Tooltip("The picture shown on the button. Optional; without it SetIcon does nothing.")]
        [SerializeField] Image icon;

        public event Action Pressed;
        public bool IsHeld { get; private set; }

        int pressedFrame = -1;
        Vector3 baseScale;
        Sprite originalSprite;
        Quaternion originalRotation = Quaternion.identity;
        bool hasOriginalSprite;

        public bool WasPressedThisFrame => pressedFrame == Time.frameCount;

        void Awake() => baseScale = transform.localScale;

        /// <summary>Shows another picture on the button. The first picture is remembered for ResetIcon.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (icon == null || sprite == null) return;

            if (!hasOriginalSprite)
            {
                originalSprite = icon.sprite;
                originalRotation = icon.rectTransform.localRotation;
                hasOriginalSprite = true;
            }
            icon.sprite = sprite;
            // The original picture may be turned (e.g. an arrow pointing up); new pictures stand upright.
            icon.rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>Puts back the picture the button had before the first SetIcon.</summary>
        public void ResetIcon()
        {
            if (icon == null || !hasOriginalSprite) return;

            icon.sprite = originalSprite;
            icon.rectTransform.localRotation = originalRotation;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            pressedFrame = Time.frameCount;
            transform.localScale = baseScale * pressedScale;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsHeld = false;
        }

        void Update()
        {
            // Spring back to full size.
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.unscaledDeltaTime * 12f);
        }

        void OnDisable()
        {
            IsHeld = false;
            transform.localScale = baseScale;
        }
    }
}
