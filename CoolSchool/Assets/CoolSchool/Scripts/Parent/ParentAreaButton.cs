using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoolSchool.Parent
{
    /// <summary>
    /// The small gear in the top-left corner. A tap does nothing: the parent area opens only after the gear has been
    /// held for a few seconds, with a ring filling up around it, so a small child will not open it by accident.
    /// Releasing (or sliding off) resets the ring. The hold is timed on unscaled time.
    /// </summary>
    public class ParentAreaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] ParentPanel panel;
        [Tooltip("The ring around the gear (Image type Filled, Radial 360).")]
        [SerializeField] Image ring;
        [Tooltip("The picture that swells a little while it is held. Defaults to this object.")]
        [SerializeField] RectTransform visual;
        [SerializeField] float holdSeconds = 3f;
        [SerializeField] float heldScale = 1.15f;

        int activePointer = int.MinValue;
        float holdStart;

        bool Holding => activePointer != int.MinValue;
        RectTransform Visual => visual != null ? visual : (RectTransform)transform;

        void Awake()
        {
            SetRing(0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Holding || ParentGate.IsOpen || ParentGate.QuitPromptOpen) return;

            activePointer = eventData.pointerId;
            holdStart = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Cancel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Cancel();
        }

        void OnDisable()
        {
            Cancel();
        }

        void Update()
        {
            float targetScale = 1f;
            if (Holding)
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - holdStart) / Mathf.Max(0.1f, holdSeconds));
                SetRing(progress);
                targetScale = heldScale;

                if (progress >= 1f)
                {
                    Cancel();
                    if (panel != null) panel.Open();
                }
            }

            Visual.localScale = Vector3.Lerp(Visual.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 10f);
        }

        void Cancel()
        {
            activePointer = int.MinValue;
            SetRing(0f);
        }

        void SetRing(float fill)
        {
            if (ring == null) return;

            ring.fillAmount = fill;
            ring.enabled = fill > 0f;
        }
    }
}
