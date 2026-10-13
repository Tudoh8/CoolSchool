using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Castle;
using CoolSchool.Characters;
using UnityEngine;

namespace CoolSchool.Battle
{
    /// <summary>
    /// The friendly side of the dragon (castle adventure, contract Docs/specs/castle-adventure-contract.md). It flies in and lands
    /// without fighting, can be sad or hungry or happy, eats cookies, lies down so the knight can climb on its back, flies a fixed
    /// path with him and flies away at the end. Bumps from swords never make it tumble. The old fight code in DragonBoss.cs
    /// is unchanged and still used by free play.
    /// </summary>
    public partial class DragonBoss
    {
        static readonly int EatId = Animator.StringToHash("Eat");
        static readonly int SittingId = Animator.StringToHash("Sitting");
        static readonly int GlidingId = Animator.StringToHash("Gliding");

        [Header("Friendly mode (castle adventure)")]
        [Tooltip("The seat on the dragon's back where the knight rides: a child of the spine bone, facing forward.")]
        [SerializeField] Transform seat;

        const float TurnToPlayerFactor = 0.5f;
        const float TummyRumbleSeconds = 4f;
        const float SighSeconds = 5f;
        const float RideSpeed = 6f;
        const float RideTurnDegrees = 150f;
        const float MaxBank = 20f;
        const float MaxPitch = 22f;
        const float StarCatchMeters = 1.6f;
        const float ConfettiBurstSeconds = 0.6f;
        const float ConfettiCooldownSeconds = 0.5f;
        static readonly Color CrumbColor = new Color(0.55f, 0.33f, 0.15f);
        static readonly Color HeartColor = new Color(1f, 0.55f, 0.7f);

        bool friendly;
        int friendlyBusy;
        bool airborne;
        bool sitting;
        DragonMood mood = DragonMood.Idle;
        Coroutine moodRoutine;
        Coroutine confettiStopRoutine;
        float nextConfettiTime;
        Vector3 modelRestPosition;
        Vector3 modelRestScale = Vector3.one;
        bool haveModelRest;

        /// <summary>Where the knight sits when he rides.</summary>
        public Transform Seat => seat;

        /// <summary>The mouth: where the confetti fire comes out and where the cookies go.</summary>
        public Transform MouthPoint => fireOrigin != null ? fireOrigin : transform;

        /// <summary>True once it was flown in friendly (until ResetDragon).</summary>
        public bool IsFriendly => friendly;

        // ----- Arriving and moods ----------------------------------------------------------------

        /// <summary>
        /// Flies in from "from" and lands at "landing" like FlyIn, but does not fight: it stands there and slowly turns to the
        /// player. Raises Arrived when it has landed.
        /// </summary>
        public void FlyInFriendly(Vector3 from, Vector3 landing)
        {
            Prepare(from, landing);
            friendly = true;
            friendlyBusy = 0;
            airborne = false;
            sitting = false;
            mood = DragonMood.Idle;
            moodRoutine = null;
            confettiStopRoutine = null;
            CaptureModelRest();

            routine = StartCoroutine(FlyInRoutine(from.y));
        }

        /// <summary>After landing: stand and slowly turn to the player, unless a scripted move is going on.</summary>
        IEnumerator FriendlyIdleRoutine()
        {
            while (true)
            {
                if (friendlyBusy == 0 && !airborne && !sitting)
                {
                    Transform target = PlayerTransform();
                    if (target != null)
                        RotateToward(Hittable.FlatDirection(transform.position, target.position), turnSpeed * TurnToPlayerFactor);
                }
                yield return null;
            }
        }

        /// <summary>
        /// Sad: slow and droopy with a sigh every 5 s. Hungry: the tummy rumbles every 4 s and shakes it a little.
        /// Happy: the happy dance once, then calm. Idle: nothing special.
        /// </summary>
        public void SetMood(DragonMood newMood)
        {
            mood = newMood;
            if (moodRoutine != null) StopCoroutine(moodRoutine);
            moodRoutine = null;
            SetSpeedScale(1f);
            RestoreModel();
            if (!isActiveAndEnabled) return;

            switch (newMood)
            {
                case DragonMood.Sad:
                    SetSpeedScale(0.6f);
                    moodRoutine = StartCoroutine(SadRoutine());
                    break;
                case DragonMood.Hungry:
                    moodRoutine = StartCoroutine(HungryRoutine());
                    break;
                case DragonMood.Happy:
                    SetTrigger(HappyId);
                    Sfx.Play("dragon.happy", transform.position);
                    BattleEffects.Confetti(hittable.AimPosition + Vector3.up * 1f);
                    break;
            }
        }

        IEnumerator SadRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            while (mood == DragonMood.Sad)
            {
                // A sigh: the whole dragon sinks a little and comes back up.
                Transform model = ModelRoot();
                if (model != null && haveModelRest)
                {
                    for (float elapsed = 0f; elapsed < 1.4f; elapsed += Time.deltaTime)
                    {
                        float dip = Mathf.Sin(elapsed / 1.4f * Mathf.PI);
                        model.localScale = new Vector3(modelRestScale.x * (1f + 0.03f * dip), modelRestScale.y * (1f - 0.05f * dip), modelRestScale.z * (1f + 0.03f * dip));
                        yield return null;
                    }
                    model.localScale = modelRestScale;
                }
                yield return new WaitForSeconds(SighSeconds);
            }
        }

        IEnumerator HungryRoutine()
        {
            yield return new WaitForSeconds(1f);
            while (mood == DragonMood.Hungry)
            {
                Sfx.Play("castle.tummy_rumble", MouthPoint.position);

                // A small shake from side to side while the tummy rumbles.
                Transform model = ModelRoot();
                if (model != null && haveModelRest)
                {
                    for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
                    {
                        float fade = 1f - elapsed / 0.6f;
                        model.localPosition = modelRestPosition + new Vector3(Mathf.Sin(elapsed * 60f) * 0.03f * fade, 0f, 0f);
                        yield return null;
                    }
                    model.localPosition = modelRestPosition;
                }
                yield return new WaitForSeconds(TummyRumbleSeconds);
            }
        }

        // ----- Doing things ----------------------------------------------------------------------

        /// <summary>One breath of confetti fire after the usual warning. It still bumps knights who do not hold their shield up.</summary>
        public IEnumerator BreatheOnce()
        {
            friendlyBusy++;
            yield return BreatheFire();
            friendlyBusy = Mathf.Max(0, friendlyBusy - 1);
        }

        /// <summary>Turns to the food, bites and munches with crumbs flying.</summary>
        public IEnumerator Eat(Vector3 foodPoint)
        {
            friendlyBusy++;
            if (mood == DragonMood.Hungry) SetMood(DragonMood.Idle);

            Vector3 direction = Hittable.FlatDirection(transform.position, foodPoint);
            for (float elapsed = 0f; elapsed < 1f && direction != Vector3.zero; elapsed += Time.deltaTime)
            {
                RotateToward(direction, turnSpeed * 4f);
                if (Vector3.Angle(FlatForward(), direction) < 6f) break;
                yield return null;
            }

            SetTrigger(EatId);
            yield return new WaitForSeconds(0.45f);
            Sfx.Play("castle.cookie_munch", MouthPoint.position);
            for (int i = 0; i < 3; i++)
            {
                BattleEffects.Confetti(MouthPoint.position, CrumbColor);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.4f);
            friendlyBusy = Mathf.Max(0, friendlyBusy - 1);
        }

        /// <summary>A gentle nuzzle (the eating move without munching) with a heart-coloured sparkle.</summary>
        public void Nuzzle()
        {
            SetTrigger(EatId);
            BattleEffects.Sparkle(MouthPoint.position + Vector3.up * 0.3f);
            BattleEffects.Confetti(MouthPoint.position + Vector3.up * 0.5f, HeartColor);
        }

        /// <summary>Sits down on the ground so the knight can climb on.</summary>
        public IEnumerator LieDown()
        {
            sitting = true;
            SetBool(SittingId, true);
            yield return new WaitForSeconds(1.2f);
        }

        /// <summary>Gets up again.</summary>
        public IEnumerator StandUp()
        {
            SetBool(SittingId, false);
            yield return new WaitForSeconds(0.8f);
            sitting = false;
        }

        /// <summary>
        /// The ride: takes off, follows the path smoothly at about 6 m/s (turning to face the way it goes, leaning into the turns),
        /// pops the golden stars its back touches, and lands where it took off. The knight rides on the Seat.
        /// </summary>
        public IEnumerator FlyPath(DragonRidePath path)
        {
            if (path == null || path.Length < 1f) yield break;

            friendlyBusy++;
            StopFireEffects();
            SetMood(DragonMood.Idle);
            sitting = false;
            SetBool(SittingId, false);

            Vector3 startPosition = transform.position;
            Vector3 landing = new Vector3(arenaCenter.x, groundY, arenaCenter.z);
            float length = path.Length;
            Vector3 startOffset = startPosition - path.Evaluate(0f);
            Vector3 endOffset = landing - path.Evaluate(1f);

            path.ShowStars();
            airborne = true;
            SetBool(GlidingId, false);
            SetBool(FlyingId, true);
            Sfx.Play("castle.ride_whoosh", transform.position);
            Sfx.Play("dragon.wing_flap", transform.position);

            float distance = 0f;
            float speed = 0f;
            float yaw = transform.eulerAngles.y;
            float previousYaw = yaw;
            float pitch = 0f;
            float roll = 0f;
            bool gliding = false;

            while (distance < length)
            {
                float dt = Time.deltaTime;
                if (dt < 0.0001f)
                {
                    yield return null;
                    continue;
                }

                // Speeds up over the first few metres and slows down for the landing.
                float remaining = length - distance;
                float wanted = RideSpeed * Mathf.Clamp01(Mathf.Min(distance / 6f, remaining / 6f) + 0.25f);
                speed = Mathf.MoveTowards(speed, wanted, 8f * dt);
                distance = Mathf.Min(length, distance + Mathf.Max(0.5f, speed) * dt);

                float t = distance / length;
                Vector3 position = path.Evaluate(t)
                    + startOffset * (1f - Mathf.SmoothStep(0f, 1f, t / 0.12f))
                    + endOffset * Mathf.SmoothStep(0f, 1f, (t - 0.88f) / 0.12f);

                // Face along the path (looking two metres ahead), lean into turns, nose up when climbing.
                Vector3 ahead = path.Evaluate(Mathf.Min(1f, t + 2f / length)) - path.Evaluate(t);
                Vector3 flat = ahead;
                flat.y = 0f;
                float targetYaw = flat.sqrMagnitude > 0.001f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : yaw;
                yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, RideTurnDegrees * dt);
                float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
                previousYaw = yaw;
                roll = Mathf.Lerp(roll, Mathf.Clamp(-yawRate * 0.25f, -MaxBank, MaxBank), 4f * dt);
                float slope = flat.magnitude > 0.05f ? Mathf.Atan2(ahead.y, flat.magnitude) * Mathf.Rad2Deg : 0f;
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(-slope, -MaxPitch, MaxPitch), 3f * dt);

                transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, roll));

                // Glide on the way down.
                bool glide = ahead.y < -0.3f * Mathf.Max(0.1f, flat.magnitude) || t > 0.9f;
                if (glide != gliding)
                {
                    gliding = glide;
                    SetBool(GlidingId, gliding);
                }

                CatchStars(path);
                yield return null;
            }

            // Down on the ground, upright, with confetti.
            transform.SetPositionAndRotation(landing, Quaternion.Euler(0f, yaw, 0f));
            SetBool(GlidingId, false);
            SetBool(FlyingId, false);
            airborne = false;
            path.HideStars();
            BattleEffects.Confetti(landing);
            Sfx.Play("dragon.roar", landing);
            yield return new WaitForSeconds(0.6f);
            friendlyBusy = Mathf.Max(0, friendlyBusy - 1);
        }

        void CatchStars(DragonRidePath path)
        {
            if (path.Stars == null) return;

            Vector3 back = seat != null ? seat.position : transform.position;
            foreach (RideStar star in path.Stars)
            {
                if (star == null || star.Caught || !star.gameObject.activeInHierarchy) continue;

                if (Vector3.Distance(star.transform.position, back) <= StarCatchMeters) star.Catch();
            }
        }

        /// <summary>A short burst of confetti fire from the mouth. It bumps nobody. At most two a second.</summary>
        public void BreatheConfettiForward()
        {
            if (Time.time < nextConfettiTime) return;

            nextConfettiTime = Time.time + ConfettiCooldownSeconds;
            Sfx.Play("dragon.fire", MouthPoint.position);

            // The breathing move only on the ground: in the air its trigger would wait for the landing.
            if (!airborne && !sitting) SetTrigger(BreatheFireId);

            if (fireConfetti == null) return;

            fireConfetti.Play();
            if (confettiStopRoutine != null) StopCoroutine(confettiStopRoutine);
            confettiStopRoutine = StartCoroutine(StopConfettiAfter(ConfettiBurstSeconds));
        }

        IEnumerator StopConfettiAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (fireConfetti != null) fireConfetti.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            confettiStopRoutine = null;
        }

        /// <summary>Waves goodbye by flying off (the take-off part of the old happy ending). FlewAway is raised at the end.</summary>
        public void FlyAwayHappy()
        {
            if (!isActiveAndEnabled) return;

            StopAllCoroutines();
            moodRoutine = null;
            confettiStopRoutine = null;
            StopFireEffects();
            RestoreModel();
            SetSpeedScale(1f);
            friendlyBusy = 0;
            airborne = false;
            sitting = false;
            SetBool(SittingId, false);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            routine = StartCoroutine(TakeOffAndAway());
        }

        // ----- Resetting and helpers -------------------------------------------------------------

        /// <summary>Everything friendly back to normal. Called by ResetDragon.</summary>
        void ResetFriendly()
        {
            RestoreModel();
            SetSpeedScale(1f);
            friendly = false;
            friendlyBusy = 0;
            airborne = false;
            sitting = false;
            mood = DragonMood.Idle;
            moodRoutine = null;
            confettiStopRoutine = null;
            haveModelRest = false;
        }

        Transform ModelRoot()
        {
            return animator != null && animator.transform != transform ? animator.transform : null;
        }

        void CaptureModelRest()
        {
            Transform model = ModelRoot();
            if (model == null) return;

            modelRestPosition = model.localPosition;
            modelRestScale = model.localScale;
            haveModelRest = true;
        }

        void RestoreModel()
        {
            Transform model = ModelRoot();
            if (model == null || !haveModelRest) return;

            model.localPosition = modelRestPosition;
            model.localScale = modelRestScale;
        }

        /// <summary>Plays the animations slower (a sad dragon) or at normal speed.</summary>
        void SetSpeedScale(float scale)
        {
            if (animator == null) return;

            DragonRootLock rootLock = animator.GetComponent<DragonRootLock>();
            if (rootLock != null) rootLock.SpeedScale = scale;
        }
    }
}
