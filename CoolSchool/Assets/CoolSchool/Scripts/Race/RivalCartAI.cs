using System.Collections;
using System.Collections.Generic;
using CoolSchool.Characters;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// A classmate in his own soapbox cart. He races the player but is rubber-banded: the gap to the player swings back and
    /// forth, so he is always close, and in the last 60 metres he settles behind (usually) or just ahead of the player, as the
    /// race decided before the start. He steers around obstacles most of the time, jumps some, and now and then simply
    /// bumps into one (that is funny, not bad). He never drives into the player's lane while they are side by side.
    /// </summary>
    public class RivalCartAI : MonoBehaviour
    {
        [SerializeField] CartController cart;
        [Tooltip("The seated rival kid (a child of the cart's seat).")]
        [SerializeField] KidAnimator driver;
        [SerializeField] float lookAhead = 18f;
        [Tooltip("The rubber band: the wanted gap to the player swings by this many metres either way.")]
        [SerializeField] float gapAmplitude = 5f;
        [SerializeField] float gapPeriod = 14f;
        [Tooltip("Waves from the cart now and then while driving. Switch off if the wave breaks the sitting pose.")]
        [SerializeField] bool cheerWhileDriving = true;

        // Per race level 1..3.
        static readonly float[] CruiseSpeedByLevel = { 6f, 7f, 8f };
        static readonly float[] DodgeChanceByLevel = { 0.85f, 0.9f, 0.9f };

        const float RubberBandStrength = 0.8f;
        const float MinSpeedFactor = 0.6f;
        const float MaxSpeedFactor = 1.35f;
        const float SettleMeters = 60f;
        const float StartBoostSeconds = 2.5f;
        const float FinishedCruiseFactor = 0.8f;
        const float JumpChance = 0.7f;
        const float JumpLeadSeconds = 0.4f;
        const float MinReactMeters = 7f;
        const float MaxReactMeters = 13f;
        const float PlayerSafeMeters = 3f;
        const float OverlapMeters = 2.5f;
        const float LaneChangeGapSeconds = 0.6f;
        const float DecisionSeconds = 0.1f;
        const float HopSeconds = 0.6f;
        const float HopHeight = 0.8f;

        enum Plan { Dodge, Jump, Ignore }

        struct Decision
        {
            public Plan plan;
            public float reactMeters;
        }

        public CartController PlayerCart { get; set; }

        /// <summary>The rival's cart.</summary>
        public CartController Cart => cart;

        int level = 1;
        bool playerWins = true;
        int rivalIndex;
        bool racing;
        float raceTime;
        float phase;
        float nextDecision;
        float nextLaneChange;
        float nextWander;
        float nextCheer;
        bool driverSeated;
        Transform driverSeat;
        Coroutine hopRoutine;
        readonly Dictionary<RaceTrackItem, Decision> decisions = new Dictionary<RaceTrackItem, Decision>();

        void Start()
        {
            // The rival sits in his cart, helmet on, from the very beginning.
            SeatDriver();
            nextCheer = Time.time + Random.Range(4f, 9f);
        }

        // ----- Commands ----------------------------------------------------------------------------

        /// <summary>Decided before the start: the race level, whether the player wins, and which rival this is (0 or 1).</summary>
        public void Prepare(int raceLevel, bool playerWinsRace, int index)
        {
            level = Mathf.Clamp(raceLevel, 1, 3);
            playerWins = playerWinsRace;
            rivalIndex = index;
            // Each rival gets his own phase, so they do not swing together.
            phase = index * Mathf.PI;
            decisions.Clear();
        }

        /// <summary>Starts the cart; from now on it races.</summary>
        public void Begin()
        {
            if (cart == null) return;

            racing = true;
            raceTime = 0f;
            nextDecision = 0f;
            nextLaneChange = 0f;
            nextWander = Random.Range(4f, 8f);
            nextCheer = Random.Range(10f, 18f);   // in race time while driving
            cart.IsPlayer = false;
            cart.TargetSpeed = CruiseSpeed;
            cart.StartRunning();
        }

        /// <summary>Hops out of the cart onto a spot on the podium, stands there and cheers, then dances.</summary>
        public void HopOutTo(Vector3 point)
        {
            racing = false;
            if (driver == null) return;

            if (hopRoutine != null) StopCoroutine(hopRoutine);
            hopRoutine = StartCoroutine(HopOutRoutine(point));
        }

        /// <summary>Back in the cart, sitting, with the helmet on.</summary>
        public void SeatDriver()
        {
            if (driver == null) return;

            if (hopRoutine != null)
            {
                StopCoroutine(hopRoutine);
                hopRoutine = null;
            }

            if (driverSeat == null) driverSeat = driver.transform.parent;
            Transform seat = cart != null && cart.Seat != null ? cart.Seat : driverSeat;
            driver.SetDancing(false);
            if (seat != null) SeatUtil.Mount(driver.transform, seat);
            driverSeated = true;

            driver.SetSitting(true);
            driver.GetComponent<RaceHelmet>()?.Show(true);
        }

        /// <summary>The cart back at the start line in a lane, the driver seated, nothing decided yet.</summary>
        public void ResetRival(int lane)
        {
            racing = false;
            decisions.Clear();
            nextCheer = Time.time + Random.Range(4f, 9f);
            if (cart != null) cart.ResetToStart(lane);
            SeatDriver();
        }

        // ----- Racing ------------------------------------------------------------------------------

        float CruiseSpeed => CruiseSpeedByLevel[level - 1];

        void Update()
        {
            if (cart == null) return;

            if (!racing)
            {
                WaveWhileWaiting();
                return;
            }

            if (cart.Finished || !cart.Running) return;

            raceTime += Time.deltaTime;
            UpdateSpeed();

            if (cart.IsSpinning) return;
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + DecisionSeconds;
                UpdateLane();
            }
            WaveWhileDriving();
        }

        /// <summary>The rubber band: stay near the player, swing a little, settle in the last 60 metres.</summary>
        void UpdateSpeed()
        {
            float cruise = CruiseSpeed;
            float minimum = cruise * MinSpeedFactor;
            float maximum = cruise * MaxSpeedFactor;

            // Once the player is over the line the rival just drives to the finish.
            if (PlayerCart == null || PlayerCart.Finished)
            {
                cart.TargetSpeed = cruise * FinishedCruiseFactor;
                return;
            }

            float wanted = WantedGap();
            float error = PlayerCart.Distance + wanted - cart.Distance;
            float target = PlayerCart.Speed + RubberBandStrength * error;
            if (raceTime < StartBoostSeconds) target = Mathf.Max(target, cruise);

            cart.TargetSpeed = Mathf.Clamp(target, minimum, maximum);
        }

        /// <summary>The gap to the player we would like (positive = ahead of him).</summary>
        float WantedGap()
        {
            float swing = gapAmplitude * Mathf.Sin(raceTime * Mathf.PI * 2f / Mathf.Max(1f, gapPeriod) + phase);

            float finish = cart.Track != null ? cart.Track.FinishDistance : 450f;
            float settleStart = finish - SettleMeters;
            if (PlayerCart.Distance <= settleStart) return swing;

            float settleGap;
            if (playerWins) settleGap = rivalIndex == 0 ? -2.5f : -4f;
            else settleGap = rivalIndex == 0 ? 1.5f : -3f;

            float weight = Mathf.Clamp01((PlayerCart.Distance - settleStart) / (SettleMeters * 0.5f));
            return Mathf.Lerp(swing, settleGap, weight);
        }

        void UpdateLane()
        {
            if (cart.Track == null) return;

            float distance = cart.Distance;
            bool canChange = Time.time >= nextLaneChange;

            // Never inside the player's cart: sideways out of the way, or hold back.
            if (PlayerCart != null && cart.Lane == PlayerCart.Lane && Mathf.Abs(distance - PlayerCart.Distance) < OverlapMeters)
            {
                if (canChange && TryMoveToFreeLane(distance, distance + lookAhead * 0.5f)) return;
                if (distance > PlayerCart.Distance - 0.5f) cart.TargetSpeed = Mathf.Min(cart.TargetSpeed, PlayerCart.Speed);
            }

            // Something in the way in this lane?
            if (cart.Track.Blocks(cart.Lane, distance, distance + lookAhead, out RaceTrackItem item) && item != null)
            {
                float ahead = item.Start - (distance + RaceTrack.CartHalfLength);
                if (ahead > -0.5f)
                {
                    Decision decision = DecisionFor(item);
                    if (decision.plan == Plan.Dodge && ahead <= decision.reactMeters && canChange)
                    {
                        if (!TryMoveToFreeLane(distance, item.End + 3f))
                        {
                            // Nowhere to go: jump it if it can be jumped, otherwise take the bump.
                            decision.plan = item.Jumpable ? Plan.Jump : Plan.Ignore;
                            decisions[item] = decision;
                        }
                    }
                    else if (decision.plan == Plan.Jump && ahead <= cart.Speed * JumpLeadSeconds && !cart.IsAirborne)
                    {
                        cart.Jump();
                    }
                }
                return;
            }

            // Nothing in the way: sometimes wander to another lane, like a kid would.
            if (raceTime >= nextWander && canChange)
            {
                nextWander = raceTime + Random.Range(5f, 9f);
                TryMoveToFreeLane(distance, distance + lookAhead);
            }
        }

        Decision DecisionFor(RaceTrackItem item)
        {
            if (decisions.TryGetValue(item, out Decision known)) return known;

            var decision = new Decision { reactMeters = Random.Range(MinReactMeters, MaxReactMeters) };
            if (Random.value < DodgeChanceByLevel[level - 1]) decision.plan = Plan.Dodge;
            else if (item.Jumpable && Random.value < JumpChance) decision.plan = Plan.Jump;
            else decision.plan = Plan.Ignore;

            decisions[item] = decision;
            return decision;
        }

        /// <summary>Changes to a neighbouring lane that is free of obstacles up to the given distance and not the player's.</summary>
        bool TryMoveToFreeLane(float from, float to)
        {
            int first = Random.value < 0.5f ? -1 : 1;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int direction = attempt == 0 ? first : -first;
                int target = cart.Lane + direction;
                if (target < 0 || target > 2) continue;
                if (!CanEnter(target)) continue;
                if (cart.Track.Blocks(target, from, to, out RaceTrackItem _)) continue;

                if (!cart.ChangeLane(direction)) continue;
                nextLaneChange = Time.time + LaneChangeGapSeconds;
                return true;
            }
            return false;
        }

        /// <summary>False when the lane is the player's and he is within a few metres.</summary>
        bool CanEnter(int lane)
        {
            if (PlayerCart == null) return true;
            return !(lane == PlayerCart.Lane && Mathf.Abs(PlayerCart.Distance - cart.Distance) < PlayerSafeMeters);
        }

        // ----- The driver --------------------------------------------------------------------------

        void WaveWhileWaiting()
        {
            if (driver == null || !driverSeated || !cheerWhileDriving) return;
            if (Time.time < nextCheer) return;

            nextCheer = Time.time + Random.Range(5f, 9f);
            driver.PlayCheer();
        }

        void WaveWhileDriving()
        {
            if (driver == null || !cheerWhileDriving || cart.IsAirborne) return;
            if (raceTime < nextCheer) return;

            nextCheer = raceTime + Random.Range(12f, 20f);
            driver.PlayCheer();
        }

        IEnumerator HopOutRoutine(Vector3 point)
        {
            Transform kid = driver.transform;
            driver.SetSitting(false);
            driverSeated = false;
            yield return new WaitForSeconds(0.15f);

            // Off the seat, to the podium, facing the camera down the street.
            Transform home = cart != null ? cart.transform.parent : null;
            SeatUtil.Dismount(kid, home);
            Vector3 toward = cart != null && cart.Track != null ? -cart.Track.Forward : Vector3.back;
            Quaternion facing = Quaternion.LookRotation(toward, Vector3.up);

            driver.PlayJump();
            yield return SeatUtil.HopTo(kid, () => point, HopSeconds, HopHeight, () => facing);

            driver.PlayCheer();
            yield return new WaitForSeconds(1.2f);
            driver.SetDancing(true);
            hopRoutine = null;
        }
    }
}
