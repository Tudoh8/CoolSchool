using System;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// The pirate ship. The child stands at the wheel and steers with the joystick: left and right turn the ship a little,
    /// but the island pulls the heading back, so it can't go wrong, and the ship can't leave the sea area. Near the island
    /// it glides into its landing spot and tells the adventure. The ship's forward direction (+Z of this object) is the bow,
    /// and the root should have a scale of 1 (put the scaled model on a child).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PirateShip : MonoBehaviour
    {
        [SerializeField] Transform helmSeat;
        [SerializeField] Transform[] crewSeats;
        [Tooltip("The wheel spins around its local forward axis with the steering.")]
        [SerializeField] Transform wheel;
        [Tooltip("Where the ship ends up at the island (its position and the way it faces).")]
        [SerializeField] Transform islandDockPoint;
        [Tooltip("World bounds the ship's position is kept inside (x and z). Zero size = no limit.")]
        [SerializeField] Bounds seaArea;
        [SerializeField] FloatingJoystick joystick;
        [SerializeField] float sailSpeed = 0.9f;
        [SerializeField] float maxSteerAngle = 35f;
        [Tooltip("Degrees per second the heading turns.")]
        [SerializeField] float turnRate = 45f;
        [Tooltip("0..1: how strongly the heading drifts back toward the island. 0 = full steering, 1 = no steering.")]
        [SerializeField, Range(0f, 1f)] float islandPull = 0.35f;
        [SerializeField] float arriveRadius = 2.5f;
        [Header("Look")]
        [SerializeField] float wheelMaxDegrees = 220f;
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float rollDegrees = 2.5f;
        [SerializeField] float pitchDegrees = 1.5f;

        const float GlideSeconds = 1f;
        const float StearSoundGapSeconds = 1.6f;

        public Transform HelmSeat => helmSeat;
        public Transform[] CrewSeats => crewSeats;

        /// <summary>True from StartSailing until the ship has arrived (also while it is paused).</summary>
        public bool IsSailing => sailing && !arrived;

        /// <summary>True once it has glided into the island dock pose.</summary>
        public bool HasArrived => arrived;

        /// <summary>0 at the dock, 1 at the island.</summary>
        public float Progress01 { get; private set; }

        /// <summary>Gently bobbing on the (pretend) sea. The adventure turns it on with the sea.</summary>
        public bool Afloat { get; set; }

        public event Action ArrivedAtIsland;

        bool cached;
        Vector3 dockPosition;
        Quaternion dockRotation;
        float baseY;
        Vector3 position;
        float yaw;
        float steer;
        float wheelAngle;
        bool sailing;
        bool paused;
        bool arrived;
        float startDistance = 1f;
        float nextSteerSound;
        Quaternion wheelBase = Quaternion.identity;
        bool wheelCached;
        float glideTime;
        Vector3 glideFrom;
        float glideFromYaw;

        void Awake()
        {
            CacheDock();
        }

        /// <summary>Casts off. The joystick steers from now on.</summary>
        public void StartSailing()
        {
            CacheDock();
            sailing = true;
            paused = false;
            arrived = false;
            position = transform.position;
            yaw = transform.eulerAngles.y;
        }

        /// <summary>Holds the ship still (the cannon moment) or sets sail again.</summary>
        public void PauseSailing(bool value)
        {
            paused = value;
        }

        /// <summary>Back at the dock where it started, with a sparkle.</summary>
        public void ResetToDock()
        {
            CacheDock();
            sailing = false;
            paused = false;
            arrived = false;
            steer = 0f;
            wheelAngle = 0f;
            Progress01 = 0f;
            Afloat = false;

            position = dockPosition;
            yaw = dockRotation.eulerAngles.y;
            transform.SetPositionAndRotation(dockPosition, dockRotation);
            ApplyWheel();

            for (int i = 0; i < 3; i++)
                BattleEffects.Sparkle(dockPosition + new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(0.5f, 2.5f), UnityEngine.Random.Range(-2f, 2f)));
        }

        void Update()
        {
            if (sailing && !arrived && !paused) Sail();
            else if (arrived) Glide();
            else steer = Mathf.MoveTowards(steer, 0f, 4f * Time.deltaTime);

            if (!sailing && !arrived && !Afloat) return;

            ApplyPose();
            SpinWheel();
        }

        // ----- Sailing -----------------------------------------------------------------------------

        void Sail()
        {
            Vector3 target = TargetPoint();
            Vector3 toTarget = target - position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Progress01 = Mathf.Clamp01((startDistance - distance) / Mathf.Max(0.1f, startDistance - arriveRadius));

            if (distance <= arriveRadius)
            {
                BeginGlide();
                return;
            }

            // The child's steering leans the heading to one side of "straight at the island"; the pull keeps it from getting far.
            steer = Mathf.MoveTowards(steer, ReadSteer(), 4f * Time.deltaTime);
            float bearing = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float wantedOffset = steer * maxSteerAngle * (1f - islandPull);
            float wantedYaw = bearing + Mathf.Clamp(wantedOffset, -maxSteerAngle, maxSteerAngle);

            yaw = Mathf.MoveTowardsAngle(yaw, wantedYaw, turnRate * Time.deltaTime);
            // Never more than the steering angle away from straight at the island, even if the bearing swings quickly.
            float off = Mathf.DeltaAngle(bearing, yaw);
            if (Mathf.Abs(off) > maxSteerAngle) yaw = bearing + Mathf.Sign(off) * maxSteerAngle;

            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            position += forward * (sailSpeed * Time.deltaTime);
            ClampToSea();

            if (Mathf.Abs(steer) > 0.35f && Time.time >= nextSteerSound)
            {
                nextSteerSound = Time.time + StearSoundGapSeconds;
                Sfx.Play("pirates.wheel", transform.position);
            }
        }

        /// <summary>The joystick's sideways value (touch), or A/D, the arrow keys or the gamepad stick in the editor.</summary>
        float ReadSteer()
        {
            if (CoolSchool.Parent.ParentGate.InputBlocked) return 0f;

            float x = joystick != null ? joystick.Value.x : 0f;
            if (Mathf.Abs(x) > 0.01f) return Mathf.Clamp(x, -1f, 1f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float key = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) key -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) key += 1f;
                if (key != 0f) return key;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null ? Mathf.Clamp(gamepad.leftStick.x.ReadValue(), -1f, 1f) : 0f;
        }

        Vector3 TargetPoint()
        {
            return islandDockPoint != null ? islandDockPoint.position : dockPosition + dockRotation * Vector3.forward * 14f;
        }

        void ClampToSea()
        {
            if (seaArea.size.sqrMagnitude < 0.01f) return;

            position.x = Mathf.Clamp(position.x, seaArea.min.x, seaArea.max.x);
            position.z = Mathf.Clamp(position.z, seaArea.min.z, seaArea.max.z);
        }

        // ----- Arriving ----------------------------------------------------------------------------

        void BeginGlide()
        {
            arrived = true;
            glideTime = 0f;
            glideFrom = position;
            glideFromYaw = yaw;
            Progress01 = 1f;
        }

        /// <summary>One second of easing into the exact landing pose, then the adventure is told.</summary>
        void Glide()
        {
            if (glideTime >= GlideSeconds) return;

            glideTime += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(glideTime / GlideSeconds));
            Vector3 target = TargetPoint();
            target.y = baseY;
            float targetYaw = islandDockPoint != null ? islandDockPoint.eulerAngles.y : yaw;

            position = Vector3.Lerp(glideFrom, target, k);
            yaw = Mathf.LerpAngle(glideFromYaw, targetYaw, k);

            if (glideTime >= GlideSeconds)
            {
                steer = 0f;
                ArrivedAtIsland?.Invoke();
            }
        }

        // ----- Look --------------------------------------------------------------------------------

        void ApplyPose()
        {
            float bob = 0f;
            float roll = 0f;
            float pitch = 0f;
            if (Afloat)
            {
                bob = Mathf.Sin(Time.time * 1.6f) * bobHeight;
                roll = Mathf.Sin(Time.time * 1.2f) * rollDegrees - steer * 2f;
                pitch = Mathf.Sin(Time.time * 1.9f + 0.7f) * pitchDegrees;
            }

            Vector3 shown = new Vector3(position.x, baseY + bob, position.z);
            transform.SetPositionAndRotation(shown, Quaternion.Euler(pitch, yaw, roll));
        }

        void SpinWheel()
        {
            float wanted = steer * wheelMaxDegrees;
            wheelAngle = Mathf.MoveTowards(wheelAngle, wanted, 400f * Time.deltaTime);
            ApplyWheel();
        }

        void ApplyWheel()
        {
            if (wheel == null) return;

            if (!wheelCached)
            {
                wheelBase = wheel.localRotation;
                wheelCached = true;
            }
            wheel.localRotation = wheelBase * Quaternion.AngleAxis(wheelAngle, Vector3.forward);
        }

        void CacheDock()
        {
            if (cached) return;

            cached = true;
            dockPosition = transform.position;
            dockRotation = transform.rotation;
            baseY = dockPosition.y;
            position = dockPosition;
            yaw = dockRotation.eulerAngles.y;

            Vector3 flat = TargetPoint() - dockPosition;
            flat.y = 0f;
            startDistance = Mathf.Max(arriveRadius + 1f, flat.magnitude);
        }
    }
}
