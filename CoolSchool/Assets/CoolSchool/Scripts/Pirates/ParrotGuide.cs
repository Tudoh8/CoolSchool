using CoolSchool.Audio;
using CoolSchool.Lessons;
using UnityEngine;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// Lorre the parrot, the guide of the pirate cove. The toy parrot has no rig, so the flapping is faked: the model child
    /// rolls and pitches in a fast small wobble and bobs, while the root does the moving (an arcing flight, a hover above
    /// something, a circle, or sitting on the kid's shoulder). Starts hidden; every move call shows it again.
    /// It never gets in the way: no colliders are used.
    /// </summary>
    public class ParrotGuide : MonoBehaviour
    {
        enum Mode { Hidden, Rest, Fly, Perch, Hover, Circle }

        [Tooltip("The child that bobs and tilts. The root moves.")]
        [SerializeField] Transform model;
        [SerializeField] float flySpeed = 6f;
        [Tooltip("Where the parrot sits, from the shoulder bone, in the kid's own directions (x right, y up, z forward).")]
        [SerializeField] Vector3 shoulderOffset = new Vector3(0.12f, 0.1f, 0f);

        // How the model moves in each mode: bob height, wobble in degrees, flap speed (radians per second).
        const float FlyBob = 0.05f, FlyWobble = 16f, FlyFlap = 32f;
        const float HoverBob = 0.07f, HoverWobble = 10f, HoverFlap = 22f;
        const float RestBob = 0.07f, RestWobble = 6f, RestFlap = 12f;
        const float PerchBob = 0.012f, PerchWobble = 2f, PerchFlap = 2.5f;
        const float BobSpeed = 4f;
        const float FlyNosePitch = 12f;
        const float FollowSharpness = 5f;
        const float TurnSharpness = 9f;
        const float SquawkSeconds = 0.35f;
        const float SquawkHop = 0.22f;
        const float MinFlySeconds = 0.5f;
        const float MaxFlySeconds = 3f;

        Mode mode = Mode.Hidden;
        bool visible = true;
        float phase;

        // Flight
        Vector3 flyStart;
        Vector3 flyTarget;
        float flyElapsed;
        float flyDuration;
        float flyArc;
        bool flyingToPerch;
        System.Action arrived;

        // Perch
        Animator perchAnimator;
        Transform perchBone;

        // Hover and circle
        Transform hoverTarget;
        float hoverHeight;
        Vector3 circleCentre;
        float circleRadius;
        float circleHeight;
        float circleAngle;

        // The model's own pose and the eased animation values.
        Vector3 modelBasePosition;
        Quaternion modelBaseRotation = Quaternion.identity;
        float bobHeight;
        float wobbleDegrees;
        float flapSpeed;
        float nosePitch;
        float flapClock;
        float squawkStart = -10f;
        Vector3 lastPosition;

        /// <summary>True while the parrot is on an arcing flight (FlyTo, or the flight to the shoulder).</summary>
        public bool IsFlying => mode == Mode.Fly;

        /// <summary>True while the parrot is shown.</summary>
        public bool IsVisible => visible;

        void Awake()
        {
            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
            if (model != null && model != transform)
            {
                modelBasePosition = model.localPosition;
                modelBaseRotation = model.localRotation;
            }

            phase = Random.value * 10f;
            lastPosition = transform.position;
            Hide();
        }

        // ----- The calls ---------------------------------------------------------------------------

        /// <summary>Sits on the kid's right shoulder. From a distance it flies there first, then follows the shoulder bone.</summary>
        public void PerchOnShoulder(Animator kid)
        {
            Show();
            perchAnimator = kid;
            perchBone = null;
            if (kid != null && kid.isHuman)
            {
                perchBone = kid.GetBoneTransform(HumanBodyBones.RightShoulder);
                if (perchBone == null) perchBone = kid.GetBoneTransform(HumanBodyBones.UpperChest);
                if (perchBone == null) perchBone = kid.GetBoneTransform(HumanBodyBones.Chest);
                if (perchBone == null) perchBone = kid.GetBoneTransform(HumanBodyBones.Spine);
            }

            arrived = null;
            if (perchAnimator == null)
            {
                mode = Mode.Rest;
                return;
            }

            if (Vector3.Distance(transform.position, PerchPoint()) < 0.4f)
            {
                mode = Mode.Perch;
                return;
            }

            flyingToPerch = true;
            BeginFlight(PerchPoint());
        }

        /// <summary>Flies to the point in an arc, then hovers there (bobbing). The callback runs on arrival.</summary>
        public void FlyTo(Vector3 point, System.Action arrivedCallback = null)
        {
            Show();
            flyingToPerch = false;
            arrived = arrivedCallback;
            BeginFlight(point);
        }

        /// <summary>Follows the target, hovering this high above it.</summary>
        public void HoverAbove(Transform target, float height = 2.2f)
        {
            Show();
            arrived = null;
            hoverTarget = target;
            hoverHeight = height;
            mode = target != null ? Mode.Hover : Mode.Rest;
        }

        /// <summary>Circles around a point at this radius and height (the party).</summary>
        public void Circle(Vector3 centre, float radius, float height)
        {
            Show();
            arrived = null;
            circleCentre = centre;
            circleRadius = Mathf.Max(0.3f, radius);
            circleHeight = height;
            Vector3 flat = transform.position - centre;
            circleAngle = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.z, flat.x) : 0f;
            mode = Mode.Circle;
        }

        /// <summary>A little hop and a squawk.</summary>
        public void Squawk()
        {
            if (!visible) return;

            squawkStart = Time.time;
            Sfx.Play("pirates.parrot", transform.position);
        }

        /// <summary>Gone from sight (the parrot stays where it is and keeps its object alive for ShoulderParrot).</summary>
        public void Hide()
        {
            mode = Mode.Hidden;
            arrived = null;
            SetVisible(false);
        }

        // ----- Movement ----------------------------------------------------------------------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (mode)
            {
                case Mode.Fly: UpdateFlight(dt); break;
                case Mode.Perch: UpdatePerch(); break;
                case Mode.Hover: UpdateHover(dt); break;
                case Mode.Circle: UpdateCircle(dt); break;
            }

            if (mode != Mode.Hidden) AnimateModel(dt);
            lastPosition = transform.position;
        }

        void BeginFlight(Vector3 target)
        {
            flyStart = transform.position;
            flyTarget = target;
            flyElapsed = 0f;
            float distance = Vector3.Distance(flyStart, target);
            flyDuration = Mathf.Clamp(distance / Mathf.Max(0.5f, flySpeed), MinFlySeconds, MaxFlySeconds);
            flyArc = Mathf.Clamp(distance * 0.18f, 0.3f, 2.5f);
            mode = Mode.Fly;
        }

        void UpdateFlight(float dt)
        {
            // The shoulder moves while the parrot flies, so the goal is looked up again every frame.
            Vector3 goal = flyingToPerch ? PerchPoint() : flyTarget;

            flyElapsed += dt;
            float t = Mathf.Clamp01(flyElapsed / flyDuration);
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position = Vector3.Lerp(flyStart, goal, k) + Vector3.up * (LessonMotion.Hump(t) * flyArc);

            Vector3 move = position - transform.position;
            transform.position = position;
            FaceDirection(move, dt);

            if (t < 1f) return;

            if (flyingToPerch)
            {
                flyingToPerch = false;
                mode = Mode.Perch;
                return;
            }

            mode = Mode.Rest;
            System.Action callback = arrived;
            arrived = null;
            callback?.Invoke();
        }

        void UpdatePerch()
        {
            if (perchAnimator == null)
            {
                mode = Mode.Rest;
                return;
            }

            // After the animator has moved the bones this frame, so the parrot never lags behind the shoulder.
            transform.position = PerchPoint();
            Quaternion kid = perchAnimator.transform.rotation;
            Vector3 forward = kid * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        void UpdateHover(float dt)
        {
            if (hoverTarget == null)
            {
                mode = Mode.Rest;
                return;
            }

            Vector3 desired = hoverTarget.position + Vector3.up * hoverHeight;
            Vector3 move = StepToward(desired, flySpeed * 1.5f, dt);
            FaceDirection(move, dt);
        }

        void UpdateCircle(float dt)
        {
            // A lap takes about as long as the parrot would need at half its flying speed.
            circleAngle += (flySpeed * 0.5f / circleRadius) * dt;
            Vector3 desired = circleCentre + new Vector3(Mathf.Cos(circleAngle), 0f, Mathf.Sin(circleAngle)) * circleRadius
                + Vector3.up * circleHeight;
            Vector3 move = StepToward(desired, flySpeed * 2f, dt);
            FaceDirection(move, dt);
        }

        /// <summary>Eases toward a moving goal without ever going faster than the limit. Returns how far it moved.</summary>
        Vector3 StepToward(Vector3 desired, float maxSpeed, float dt)
        {
            Vector3 toGoal = desired - transform.position;
            float distance = toGoal.magnitude;
            if (distance < 0.0001f) return Vector3.zero;

            float step = Mathf.Min(distance * FollowSharpness * dt, maxSpeed * dt);
            Vector3 move = toGoal / distance * step;
            transform.position += move;
            return move;
        }

        void FaceDirection(Vector3 direction, float dt)
        {
            direction.y = 0f;
            // Hardly moving: keep looking the way it was looking.
            if (direction.sqrMagnitude < 0.00001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-TurnSharpness * dt));
        }

        Vector3 PerchPoint()
        {
            if (perchAnimator == null) return transform.position;

            Quaternion kid = perchAnimator.transform.rotation;
            if (perchBone == null) return perchAnimator.transform.position + Vector3.up * 1.2f + kid * shoulderOffset;
            return perchBone.position + kid * shoulderOffset;
        }

        // ----- The faked flapping ------------------------------------------------------------------

        void AnimateModel(float dt)
        {
            if (model == null || model == transform) return;

            float targetBob, targetWobble, targetFlap, targetNose = 0f;
            switch (mode)
            {
                case Mode.Fly:
                case Mode.Circle:
                    targetBob = FlyBob; targetWobble = FlyWobble; targetFlap = FlyFlap; targetNose = FlyNosePitch;
                    break;
                case Mode.Hover:
                    // Hovering above a kid who is running along is still a flight.
                    bool chasing = (transform.position - lastPosition).magnitude / dt > 1.2f;
                    targetBob = chasing ? FlyBob : HoverBob;
                    targetWobble = chasing ? FlyWobble : HoverWobble;
                    targetFlap = chasing ? FlyFlap : HoverFlap;
                    targetNose = chasing ? FlyNosePitch * 0.6f : 0f;
                    break;
                case Mode.Perch:
                    targetBob = PerchBob; targetWobble = PerchWobble; targetFlap = PerchFlap;
                    break;
                default:
                    targetBob = RestBob; targetWobble = RestWobble; targetFlap = RestFlap;
                    break;
            }

            float ease = 1f - Mathf.Exp(-8f * dt);
            bobHeight = Mathf.Lerp(bobHeight, targetBob, ease);
            wobbleDegrees = Mathf.Lerp(wobbleDegrees, targetWobble, ease);
            flapSpeed = Mathf.Lerp(flapSpeed, targetFlap, ease);
            nosePitch = Mathf.Lerp(nosePitch, targetNose, ease);
            flapClock += flapSpeed * dt;

            float time = Time.time + phase;
            float bob = Mathf.Sin(time * BobSpeed) * bobHeight;
            float roll = Mathf.Sin(flapClock) * wobbleDegrees;
            float pitch = Mathf.Sin(flapClock * 0.5f + 1f) * wobbleDegrees * 0.4f + nosePitch;

            float hop = 0f;
            float squawkT = (Time.time - squawkStart) / SquawkSeconds;
            if (squawkT >= 0f && squawkT <= 1f)
            {
                hop = LessonMotion.Hump(squawkT) * SquawkHop;
                // Head thrown back while squawking.
                pitch -= LessonMotion.Hump(squawkT) * 25f;
            }

            model.localPosition = modelBasePosition + Vector3.up * (bob + hop);
            model.localRotation = modelBaseRotation * Quaternion.Euler(pitch, 0f, roll);
        }

        // ----- Showing and hiding ------------------------------------------------------------------

        void Show()
        {
            if (visible) return;

            SetVisible(true);
            if (mode == Mode.Hidden) mode = Mode.Rest;
        }

        /// <summary>The root stays active (ShoulderParrot may live on it), so hiding switches the model off instead.</summary>
        void SetVisible(bool show)
        {
            visible = show;
            if (model != null && model != transform)
            {
                model.gameObject.SetActive(show);
                return;
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = show;
        }
    }
}
