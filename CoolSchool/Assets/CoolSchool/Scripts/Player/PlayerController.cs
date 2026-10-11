using CoolSchool.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Player
{
    /// <summary>
    /// Moves the player's kid relative to the camera. Reads the on-screen joystick
    /// first and falls back to keyboard/gamepad so the game can be tested in the editor.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] FloatingJoystick joystick;
        [SerializeField] ActionButton jumpButton;
        [SerializeField] Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float runSpeed = 4f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float jumpHeight = 0.7f;

        /// <summary>Multiplier for power-ups such as faster sneakers.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Turned off while the kid is counting, cheering or in a cutscene.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Turned off while the jump button is busy being something else, e.g. a sword button.</summary>
        public bool JumpEnabled { get; set; } = true;

        /// <summary>Raised when the kid leaves the ground, so the animator can play a jump.</summary>
        public event System.Action Jumped;

        CharacterController controller;
        InputAction moveAction;
        InputAction jumpAction;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable()
        {
            moveAction.Enable();
            jumpAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable();
            jumpAction.Disable();
        }

        void OnDestroy()
        {
            moveAction.Dispose();
            jumpAction.Dispose();
        }

        void Update()
        {
            Vector2 input = InputEnabled ? ReadMoveInput() : Vector2.zero;
            Vector3 direction = CameraRelative(input);

            // Any real push makes the kid run; a gentle push walks.
            float push = Mathf.Clamp01(input.magnitude);
            float speed = Mathf.Lerp(walkSpeed, runSpeed, Mathf.InverseLerp(0.2f, 0.7f, push)) * SpeedMultiplier;
            Vector3 horizontal = direction * (push > 0f ? speed : 0f);

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (InputEnabled && JumpEnabled && WantsToJump())
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    Jumped?.Invoke();
                }
            }
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        Vector2 ReadMoveInput()
        {
            Vector2 stick = joystick != null ? joystick.Value : Vector2.zero;
            return stick.sqrMagnitude > 0.0001f ? stick : Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }

        bool WantsToJump()
        {
            return (jumpButton != null && jumpButton.WasPressedThisFrame) || jumpAction.WasPressedThisFrame();
        }

        Vector3 CameraRelative(Vector2 input)
        {
            if (cameraTransform == null) return new Vector3(input.x, 0f, input.y);

            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
