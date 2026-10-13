using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// A soapbox cart on the race track. Movement is kinematic (no physics): the cart keeps a distance along the track and a
    /// sideways offset, and puts itself at <c>Track.PointAt(Distance, LaneX)</c> every frame. The track items decide what a
    /// bump or a puddle does (they call Bonk, Splash, Brake, Launch); the race and the rival AI decide where the cart steers.
    /// The model child wobbles, tilts and spins; the root only follows the track, so the seat stays easy to place.
    /// Nothing here can stop a cart for good: every slowdown is short and the cart always speeds up again.
    /// </summary>
    public class CartController : MonoBehaviour
    {
        [Tooltip("Where the kid's root sits (the SitGround pose has the hips on the seat).")]
        [SerializeField] Transform seat;
        [Tooltip("Where a plush buddy sits when it rides along (beside his legs or behind the seat). Under the model, so it wobbles with the cart.")]
        [SerializeField] Transform buddySeat;
        [Tooltip("The child that wobbles, tilts and spins. The cart bodies live under it.")]
        [SerializeField] Transform model;
        [Tooltip("Optional. Spun by the speed. The Synty carts are one mesh, so this is usually empty.")]
        [SerializeField] Transform[] wheels;
        [SerializeField] float laneChangeSeconds = 0.22f;
        [SerializeField] float jumpHeight = 1.4f;
        [SerializeField] float jumpSeconds = 0.75f;
        [Tooltip("Metres per second squared toward the target speed.")]
        [SerializeField] float acceleration = 4f;

        // Slowing down is quicker than speeding up, so a bump is felt.
        const float DecelerationFactor = 3f;
        const float BrakeSeconds = 1.5f;
        const float WheelRadius = 0.2f;
        const float FinishRunOutMeters = 22f;
        const float MaxRollDegrees = 14f;
        const float MaxYawDegrees = 10f;
        const float EdgeWiggleSeconds = 0.3f;
        const float EdgeWiggleMeters = 0.22f;
        const float BonkShakeDegrees = 14f;
        const float BonkShakeHertz = 4.5f;
        const float SplashShakeDegrees = 5f;
        const float SplashShakeHertz = 7f;
        const float SpinSeconds = 1f;
        const float SpinSlowFactor = 0.3f;
        const float SpinSlowSeconds = 0.8f;
        const float LaunchTiltUpDegrees = 14f;
        const float LaunchTiltDownDegrees = 12f;
        const float JumpTiltDegrees = 5f;
        const float BobMeters = 0.02f;

        /// <summary>The track this cart drives on. Set by the race (and the builder's test scenes).</summary>
        public RaceTrack Track { get; set; }

        public Transform Seat => seat;

        /// <summary>Where a riding plush buddy sits. Empty when the cart has none.</summary>
        public Transform BuddySeat => buddySeat;

        /// <summary>The lane the cart is in or heading for, 0..2.</summary>
        public int Lane { get; private set; } = 1;

        /// <summary>The sideways offset right now (smoothed through a lane change).</summary>
        public float LaneX { get; private set; }

        /// <summary>Metres along the track from the start line.</summary>
        public float Distance { get; private set; }

        /// <summary>Height above the road.</summary>
        public float Height { get; private set; }

        public bool IsAirborne => airT >= 0f;

        /// <summary>True during the spin after a bump (rivals only).</summary>
        public bool IsSpinning => spinT >= 0f;

        public float Speed { get; private set; }

        /// <summary>The cruise speed the cart accelerates toward while it is running.</summary>
        public float TargetSpeed { get; set; }

        public bool Running { get; private set; }

        public bool Finished { get; private set; }

        /// <summary>The moment (Time.time) the cart crossed the finish line.</summary>
        public float FinishTime { get; private set; }

        public int Crashes { get; private set; }

        public bool IsPlayer { get; set; }

        public event System.Action<CartController> CrossedFinish;

        /// <summary>A crash: the cart bonked into something (Bonk).</summary>
        public event System.Action<CartController> Bonked;

        /// <summary>A puddle or a sprinkler (Splash).</summary>
        public event System.Action<CartController> Splashed;

        // Lane change.
        float laneFromX;
        float laneToX;
        float laneT = 1f;
        float lateralVelocity;
        float wiggleT = -1f;
        float wiggleDirection;

        // Jumps and ramps: a parabola of the given height over the given time.
        float airT = -1f;
        float airSeconds = 1f;
        float airPeak;
        bool airIsLaunch;

        // Slowdowns: a speed factor held for some seconds.
        float slowFactor = 1f;
        float slowLeft;
        float brakeRate;

        // Wobble, nod and spin of the model.
        float shakeT = -1f;
        float shakeSeconds = 1f;
        float shakeDegrees;
        float shakeHertz;
        bool shakeBounces;
        float nodT = -1f;
        float nodSeconds = 0.5f;
        float nodDegrees;
        float spinT = -1f;
        float bobPhase;

        Vector3 modelBasePosition;
        Quaternion modelBaseRotation = Quaternion.identity;

        void Awake()
        {
            if (model != null)
            {
                modelBasePosition = model.localPosition;
                modelBaseRotation = model.localRotation;
            }
        }

        // ----- Commands ----------------------------------------------------------------------------

        /// <summary>Poses the cart at the start line in a lane, standing still, with all the effects cleared.</summary>
        public void PlaceAtStart(int startLane)
        {
            Lane = Mathf.Clamp(startLane, 0, 2);
            LaneX = OffsetOf(Lane);
            laneFromX = LaneX;
            laneToX = LaneX;
            laneT = 1f;
            lateralVelocity = 0f;
            wiggleT = -1f;

            Distance = 0f;
            Height = 0f;
            Speed = 0f;
            Running = false;
            Finished = false;
            FinishTime = 0f;
            Crashes = 0;
            brakeRate = 0f;

            airT = -1f;
            slowFactor = 1f;
            slowLeft = 0f;
            shakeT = -1f;
            nodT = -1f;
            spinT = -1f;
            bobPhase = 0f;

            ApplyPose(0f);
        }

        public void ResetToStart(int startLane)
        {
            PlaceAtStart(startLane);
        }

        public void StartRunning()
        {
            Running = true;
            Finished = false;
        }

        /// <summary>Stops driving. With brake the cart slows to a halt over about a second and a half, otherwise it stops at once.</summary>
        public void StopRunning(bool brake)
        {
            Running = false;
            if (!brake)
            {
                Speed = 0f;
                return;
            }
            brakeRate = Mathf.Max(0.5f, Speed / BrakeSeconds);
        }

        /// <summary>One lane over: -1 left, +1 right. At the edge of the road the cart wiggles a little and false is returned.</summary>
        public bool ChangeLane(int direction)
        {
            if (direction == 0) return false;

            int target = Lane + (direction < 0 ? -1 : 1);
            if (target < 0 || target > 2)
            {
                wiggleT = 0f;
                wiggleDirection = direction < 0 ? -1f : 1f;
                return false;
            }

            laneFromX = LaneX;
            laneToX = OffsetOf(target);
            laneT = 0f;
            Lane = target;
            if (IsPlayer) Sfx.Play("race.lane", transform.position);
            return true;
        }

        /// <summary>A small hop, only when the cart is on the ground.</summary>
        public bool Jump()
        {
            if (IsAirborne) return false;

            StartAir(jumpHeight, jumpSeconds, false);
            if (IsPlayer) Sfx.Play("race.jump", transform.position);
            return true;
        }

        /// <summary>A ramp: a big, slow arc with a tilt of the nose.</summary>
        public void Launch(float height, float seconds)
        {
            StartAir(Mathf.Max(0.1f, height), Mathf.Max(0.2f, seconds), true);
        }

        /// <summary>A crash: the model wobbles and the cart slows for a moment. Counts as a crash.</summary>
        public void Bonk(float speedFactor, float seconds)
        {
            Crashes++;
            Slow(speedFactor, seconds);
            StartShake(BonkShakeDegrees, BonkShakeHertz, Mathf.Max(0.4f, seconds), false);
            Bonked?.Invoke(this);
        }

        /// <summary>Like Bonk, but a happy little shake, and not counted as a crash.</summary>
        public void Splash(float speedFactor, float seconds)
        {
            Slow(speedFactor, seconds);
            StartShake(SplashShakeDegrees, SplashShakeHertz, Mathf.Max(0.3f, seconds), true);
            Splashed?.Invoke(this);
        }

        /// <summary>A short dip in speed with the nose nodding down (animals hopping out of the way).</summary>
        public void Brake(float speedFactor, float seconds)
        {
            Slow(speedFactor, seconds);
            nodT = 0f;
            nodSeconds = Mathf.Max(0.3f, seconds);
            nodDegrees = 6f;
            if (IsPlayer) Sfx.Play("race.brake", transform.position);
        }

        /// <summary>Only a wobble of the model (a bump from the side), no slowdown and no event.</summary>
        public void Wobble(float seconds)
        {
            StartShake(SplashShakeDegrees, SplashShakeHertz, Mathf.Max(0.2f, seconds), false);
        }

        /// <summary>A 360 degree spin of the model; the cart falls back a few metres and drives on.</summary>
        public void SpinOut()
        {
            if (IsSpinning) return;

            spinT = 0f;
            Slow(SpinSlowFactor, SpinSlowSeconds);
            Sfx.Play("race.spinout", transform.position);
        }

        // ----- Every frame -------------------------------------------------------------------------

        void Update()
        {
            if (Track == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateLane(dt);
            UpdateAir(dt);
            UpdateSpeed(dt);

            float previous = Distance;
            Distance += Speed * dt;
            ApplyPose(dt);

            // The track decides what the cart touched on the way.
            if (Distance > previous) Track.Sweep(this, previous, Distance);

            CheckFinish();
            SpinWheels(dt);
        }

        void UpdateLane(float dt)
        {
            float before = LaneX;
            if (laneT < 1f)
            {
                laneT = Mathf.Min(1f, laneT + dt / Mathf.Max(0.05f, laneChangeSeconds));
                LaneX = Mathf.Lerp(laneFromX, laneToX, Mathf.SmoothStep(0f, 1f, laneT));
            }
            lateralVelocity = (LaneX - before) / dt;

            if (wiggleT >= 0f)
            {
                wiggleT += dt / EdgeWiggleSeconds;
                if (wiggleT >= 1f) wiggleT = -1f;
            }
        }

        void UpdateAir(float dt)
        {
            if (airT < 0f) return;

            airT += dt / airSeconds;
            if (airT >= 1f)
            {
                airT = -1f;
                Height = 0f;
                if (IsPlayer) Sfx.Play("race.land", transform.position);
                return;
            }
            Height = airPeak * 4f * airT * (1f - airT);
        }

        void UpdateSpeed(float dt)
        {
            if (slowLeft > 0f) slowLeft -= dt;

            if (Running)
            {
                float wanted = TargetSpeed * (slowLeft > 0f ? slowFactor : 1f);
                float rate = wanted < Speed ? acceleration * DecelerationFactor : acceleration;
                Speed = Mathf.MoveTowards(Speed, wanted, rate * dt);
            }
            else if (Speed > 0f)
            {
                Speed = Mathf.MoveTowards(Speed, 0f, brakeRate * dt);
            }
        }

        void CheckFinish()
        {
            float finishDistance = Track.FinishDistance;
            if (Distance > finishDistance + FinishRunOutMeters)
            {
                Distance = finishDistance + FinishRunOutMeters;
                Speed = 0f;
            }

            if (Finished || Distance < finishDistance) return;

            // The exact moment the line was crossed, not the frame after.
            Finished = true;
            FinishTime = Time.time - (Distance - finishDistance) / Mathf.Max(Speed, 0.1f);
            CrossedFinish?.Invoke(this);
        }

        void SpinWheels(float dt)
        {
            if (wheels == null || Speed <= 0.01f) return;

            float degrees = Speed * dt / WheelRadius * Mathf.Rad2Deg;
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, degrees, Space.Self);
        }

        // ----- The pose ----------------------------------------------------------------------------

        void ApplyPose(float dt)
        {
            if (Track == null) return;

            float wiggle = 0f;
            if (wiggleT >= 0f)
                wiggle = wiggleDirection * Mathf.Sin(wiggleT * Mathf.PI * 3f) * (1f - wiggleT) * EdgeWiggleMeters;

            Vector3 position = Track.PointAt(Distance, LaneX + wiggle) + Vector3.up * Height;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(Track.Forward, Vector3.up));

            if (model == null) return;

            // Banking into a lane change, a nod for jumps and brakes, a spin after a bump, a wobble after a crash.
            float roll = -Mathf.Clamp(lateralVelocity * 1.2f, -MaxRollDegrees, MaxRollDegrees);
            float yaw = Mathf.Clamp(lateralVelocity * 0.8f, -MaxYawDegrees, MaxYawDegrees);
            float pitch = 0f;
            float bounce = 0f;

            if (wiggleT >= 0f) roll += wiggleDirection * Mathf.Sin(wiggleT * Mathf.PI * 3f) * (1f - wiggleT) * 6f;

            if (airT >= 0f)
            {
                // Nose up first, then down: a ramp tilts more than a hop.
                float up = airIsLaunch ? LaunchTiltUpDegrees : JumpTiltDegrees;
                float down = airIsLaunch ? LaunchTiltDownDegrees : JumpTiltDegrees;
                pitch += Mathf.Lerp(-up, down, airT);
            }

            if (nodT >= 0f)
            {
                nodT += dt / nodSeconds;
                if (nodT >= 1f) nodT = -1f;
                else pitch += Mathf.Sin(nodT * Mathf.PI) * nodDegrees;
            }

            if (shakeT >= 0f)
            {
                shakeT += dt / shakeSeconds;
                if (shakeT >= 1f)
                {
                    shakeT = -1f;
                }
                else
                {
                    float fade = 1f - shakeT;
                    float wave = Mathf.Sin(shakeT * shakeSeconds * shakeHertz * Mathf.PI * 2f);
                    roll += wave * shakeDegrees * fade;
                    yaw += wave * shakeDegrees * 0.4f * fade;
                    if (shakeBounces) bounce += Mathf.Abs(wave) * 0.05f * fade;
                }
            }

            float spin = 0f;
            if (spinT >= 0f)
            {
                spinT += dt / SpinSeconds;
                if (spinT >= 1f) spinT = -1f;
                else spin = 360f * Mathf.SmoothStep(0f, 1f, spinT);
            }

            // A little bounce from the road, stronger when faster.
            if (Height <= 0.01f && Speed > 0.5f)
            {
                bobPhase += Speed * dt * 1.8f;
                bounce += Mathf.Abs(Mathf.Sin(bobPhase)) * BobMeters;
            }

            model.localPosition = modelBasePosition + Vector3.up * bounce;
            model.localRotation = modelBaseRotation * Quaternion.Euler(pitch, yaw + spin, roll);
        }

        // ----- Helpers -----------------------------------------------------------------------------

        float OffsetOf(int laneIndex)
        {
            return Track != null ? Track.LaneOffset(laneIndex) : (laneIndex - 1) * 2.2f;
        }

        void StartAir(float peak, float seconds, bool launch)
        {
            airT = 0f;
            airPeak = peak;
            airSeconds = seconds;
            airIsLaunch = launch;
        }

        /// <summary>Keeps the stronger slowdown and the longer time when two overlap.</summary>
        void Slow(float factor, float seconds)
        {
            factor = Mathf.Clamp(factor, 0.1f, 1f);
            if (slowLeft > 0f)
            {
                slowFactor = Mathf.Min(slowFactor, factor);
                slowLeft = Mathf.Max(slowLeft, seconds);
            }
            else
            {
                slowFactor = factor;
                slowLeft = seconds;
            }
        }

        void StartShake(float degrees, float hertz, float seconds, bool bounces)
        {
            shakeT = 0f;
            shakeDegrees = degrees;
            shakeHertz = hertz;
            shakeSeconds = seconds;
            shakeBounces = bounces;
        }
    }
}
