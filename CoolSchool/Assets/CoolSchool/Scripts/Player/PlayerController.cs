using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Characters;
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

        /// <summary>The fastest the kid may fall, in metres per second. Lowered to float down like a parachute.</summary>
        public float MaxFallSpeed { get; set; } = 100f;

        /// <summary>True while standing on the ground.</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        /// <summary>Up (positive) or down (negative) speed in metres per second.</summary>
        public float VerticalVelocity => verticalVelocity;

        /// <summary>The pull of gravity in metres per second squared (negative), for working out how hard a trampoline must launch.</summary>
        public float Gravity => gravity;

        /// <summary>True from RideOn until the hop off in StopRiding has landed. The kid does not walk while riding.</summary>
        public bool IsRiding => riding;

        /// <summary>Raised when the kid leaves the ground, so the animator can play a jump.</summary>
        public event System.Action Jumped;

        CharacterController controller;
        InputAction moveAction;
        InputAction jumpAction;
        float verticalVelocity;
        Vector3 carryVelocity;
        bool launchPending;
        float airSeconds;
        float nextStepTime;
        bool riding;
        Transform ridingSeat;
        Transform ridingOriginalParent;
        Coroutine rideRoutine;

        // How fast a launch's sideways push fades, in metres per second per second.
        public const float CarryDecay = 5f;
        const float LandSoundMinAirSeconds = 0.2f;
        const float StepSeconds = 0.35f;

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
            // On a ship the seat carries the kid; walking, gravity and the controller are all off.
            if (riding) return;

            // The parent panel freezes the game; keys typed into its name field must not queue a move or a jump.
            bool inputOn = InputEnabled && !CoolSchool.Parent.ParentGate.InputBlocked;
            Vector2 input = inputOn ? ReadMoveInput() : Vector2.zero;
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

            // A launch from a jump pad must not be wiped out by the "standing on the ground" reset below.
            bool launched = launchPending;
            launchPending = false;

            if (controller.isGrounded && !launched)
            {
                verticalVelocity = -2f;
                carryVelocity = Vector3.zero;
                if (inputOn && JumpEnabled && WantsToJump())
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    Sfx.Play("player.jump", transform.position);
                    Jumped?.Invoke();
                }
            }
            verticalVelocity += gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -MaxFallSpeed);
            carryVelocity = Vector3.MoveTowards(carryVelocity, Vector3.zero, CarryDecay * Time.deltaTime);

            controller.Move((horizontal + carryVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            PlayMovementSounds(horizontal.magnitude);
        }

        /// <summary>
        /// Shoots the kid into the air, e.g. from a jump pad. The up part is the new vertical speed; the flat part is a
        /// sideways push that fades away. Steering stays on the whole time.
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            verticalVelocity = velocity.y;
            carryVelocity = new Vector3(velocity.x, 0f, velocity.z);
            launchPending = true;
            Jumped?.Invoke();
        }

        /// <summary>Sets the up/down speed directly, e.g. to hang for a moment at the top of a jump.</summary>
        public void SetVerticalVelocity(float value)
        {
            verticalVelocity = value;
        }

        /// <summary>
        /// Hops onto a seat (a short arc), then rides there: the character controller and walking are off and the kid is
        /// parented to the seat (size kept), standing still in the animator. Used for the pirate ship's wheel.
        /// </summary>
        public void RideOn(Transform seat)
        {
            if (seat == null) return;

            if (rideRoutine != null) StopCoroutine(rideRoutine);
            rideRoutine = null;

            if (!riding) ridingOriginalParent = transform.parent;
            else transform.SetParent(ridingOriginalParent, true);

            riding = true;
            ridingSeat = seat;
            controller.enabled = false;
            verticalVelocity = 0f;
            carryVelocity = Vector3.zero;
            launchPending = false;
            SetRidingIdle(true);

            rideRoutine = StartCoroutine(RideRoutine(seat));
        }

        /// <summary>Hops off the seat to the ground point and walks again. Does nothing when not riding.</summary>
        public void StopRiding(Vector3 groundPoint)
        {
            if (!riding) return;

            if (rideRoutine != null) StopCoroutine(rideRoutine);
            rideRoutine = null;

            SeatUtil.Dismount(transform, ridingOriginalParent);
            rideRoutine = StartCoroutine(DismountRoutine(groundPoint));
        }

        IEnumerator RideRoutine(Transform seat)
        {
            GetComponent<KidAnimator>()?.PlayJump();
            yield return SeatUtil.HopTo(transform, () => seat.position, 0.4f, 0.7f, () => seat.rotation);

            SeatUtil.Mount(transform, seat);
            rideRoutine = null;
        }

        IEnumerator DismountRoutine(Vector3 groundPoint)
        {
            Vector3 flat = groundPoint - transform.position;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat, Vector3.up) : transform.rotation;

            GetComponent<KidAnimator>()?.PlayJump();
            yield return SeatUtil.HopTo(transform, () => groundPoint, 0.4f, 0.7f, () => facing);

            verticalVelocity = -2f;
            carryVelocity = Vector3.zero;
            controller.enabled = true;
            riding = false;
            ridingSeat = null;
            SetRidingIdle(false);
            rideRoutine = null;
        }

        void SetRidingIdle(bool idle)
        {
            var kid = GetComponent<KidAnimator>();
            if (kid != null) kid.ForceIdle = idle;
        }

        /// <summary>A thump when landing after a real jump, and a footstep every 0.35 s while running.</summary>
        void PlayMovementSounds(float horizontalSpeed)
        {
            if (!controller.isGrounded)
            {
                airSeconds += Time.deltaTime;
                return;
            }

            if (airSeconds > LandSoundMinAirSeconds) Sfx.Play("player.land", transform.position);
            airSeconds = 0f;

            bool running = horizontalSpeed > walkSpeed * 1.25f;
            if (running && Time.time >= nextStepTime)
            {
                nextStepTime = Time.time + StepSeconds;
                Sfx.Play("player.step", transform.position);
            }
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
