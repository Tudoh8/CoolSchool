using CoolSchool.Input;
using CoolSchool.Parent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CoolSchool.Race
{
    /// <summary>
    /// What the child does during the soapbox race: tap the left half of the screen to go one lane left, the right half to go
    /// one lane right, and the big action button to jump. The lane changes happen on the press itself, because an instant
    /// answer matters more to a small child than a swipe. Keyboard in the editor: A / D or the arrows, and Space / W / Up to jump.
    /// A press that starts on a button (the action button, the parent gear, the star jar) never counts as a lane change.
    /// </summary>
    public class RaceInput : MonoBehaviour
    {
        [SerializeField] ActionButton actionButton;
        [Tooltip("Reserved for swipes (not used in v1: lane changes happen on the press).")]
        [SerializeField] float swipePixels = 60f;

        /// <summary>Only when true do taps and keys count.</summary>
        public bool Active { get; set; }

        /// <summary>-1 for a lane to the left, +1 for a lane to the right.</summary>
        public event System.Action<int> LaneChange;

        public event System.Action Jump;

        RectTransform actionRect;
        RectTransform extraButtonRect;
        Camera actionCamera;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>Caches the action button's rect and the camera its canvas needs (none for a Screen Space Overlay canvas).</summary>
        void Awake()
        {
            if (actionButton == null) return;

            actionRect = actionButton.transform as RectTransform;
            Canvas canvas = actionButton.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) actionCamera = canvas.worldCamera;
        }

        void OnEnable()
        {
            if (actionButton != null) actionButton.Pressed += OnJumpPressed;
        }

        void OnDisable()
        {
            if (actionButton != null) actionButton.Pressed -= OnJumpPressed;
        }

        void Update()
        {
            // The parent panel freezes the game: taps and keys on it must not steer the cart.
            if (!Active || ParentGate.IsOpen) return;

            PollKeyboard();
            PollPointers();
        }

        void OnJumpPressed()
        {
            if (!Active || ParentGate.IsOpen) return;

            Jump?.Invoke();
        }

        void PollKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) LaneChange?.Invoke(-1);
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) LaneChange?.Invoke(1);
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                Jump?.Invoke();
        }

        /// <summary>Each new finger is its own press. The mouse only counts when no finger started a press this frame.</summary>
        void PollPointers()
        {
            bool touched = false;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.wasPressedThisFrame) continue;

                    touched = true;
                    HandlePress(touch.position.ReadValue(), touch.touchId.ReadValue());
                }
            }

            Mouse mouse = Mouse.current;
            if (!touched && mouse != null && mouse.leftButton.wasPressedThisFrame)
                HandlePress(mouse.position.ReadValue(), -1);
        }

        void HandlePress(Vector2 position, int pointerId)
        {
            if (IsOverUi(pointerId) || IsOverRect(actionRect, position) || IsOverRect(extraButtonRect, position)) return;

            LaneChange?.Invoke(position.x < Screen.width * 0.5f ? -1 : 1);
        }

        /// <summary>A second button whose taps never change lanes (the honk button). Set by the race.</summary>
        public void SetExtraButton(RectTransform rect)
        {
            extraButtonRect = rect;
        }

        /// <summary>
        /// Geometric backup for <see cref="IsOverUi"/>: the EventSystem may not have seen a brand-new touch yet when Update runs,
        /// so a tap on the jump button (or the honk button) must also be recognised by position, with a 10% margin around the button.
        /// </summary>
        bool IsOverRect(RectTransform rect, Vector2 position)
        {
            if (rect == null) return false;

            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(actionCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(actionCamera, corners[2]);
            Vector2 margin = (max - min) * 0.1f;
            var area = Rect.MinMaxRect(min.x - margin.x, min.y - margin.y, max.x + margin.x, max.y + margin.y);
            return area.Contains(position);
        }

        static bool IsOverUi(int pointerId)
        {
            EventSystem system = EventSystem.current;
            return system != null && system.IsPointerOverGameObject(pointerId);
        }
    }
}
