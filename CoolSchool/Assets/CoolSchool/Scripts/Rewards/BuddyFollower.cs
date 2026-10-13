using CoolSchool.Battle;
using CoolSchool.Player;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>
    /// The plush toy that comes along: the dino, unicorn or bear (or their gold versions) hops after the kid in free play.
    /// Lives on its own scene object (not a child of the player). The parrot is not a follower: it sits on the shoulder
    /// (ShoulderParrot), so for the parrot or for no buddy there is simply nothing to show here.
    /// EquipmentApplier chooses the buddy and hides it in the activities where it does not belong; the race lets it ride
    /// in the cart (RideOn) and the dress-up mirror makes it stand still beside him (HoldAt).
    /// No colliders are used, so it never pushes anyone or blocks the camera.
    /// </summary>
    public class BuddyFollower : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("The toy's tallest side is scaled to this, in metres.")]
        [SerializeField] float buddyHeight = 0.45f;
        [Header("Following")]
        [SerializeField] float followBehind = 0.9f;
        [SerializeField] float followRight = 0.6f;
        [Tooltip("How much faster than the kid it may move, so it catches up.")]
        [SerializeField] float speedFactor = 1.3f;
        [Tooltip("Slowest it ever walks, so it still settles when the kid stands still, in metres per second.")]
        [SerializeField] float minSpeed = 1.6f;
        [Tooltip("Further than this from the kid and it jumps next to him with a sparkle, in metres.")]
        [SerializeField] float teleportDistance = 6f;
        [Header("Hopping")]
        [SerializeField] float hopHeight = 0.15f;
        [SerializeField] float hopsPerSecond = 2.5f;

        const float StartMoveDistance = 0.35f;
        const float StopMoveDistance = 0.12f;
        const float TurnDegreesPerSecond = 540f;
        const float GroundSharpness = 18f;
        const float MaxPlayerSpeed = 8f;
        const float IdleActionMin = 4f;
        const float IdleActionMax = 7f;
        const float IdleHopSeconds = 0.45f;
        const float IdleSpinSeconds = 0.7f;
        const float IdleHopHeight = 0.25f;

        // Pieces: this object moves over the ground, "Visual" does the hopping and squashing, the model hangs under it.
        Transform visual;
        GameObject model;
        string modelId = "";
        bool modelGold;

        bool hidden;
        bool shown;
        Transform seat;
        Transform holdSpot;
        Transform originalParent;

        bool moving;
        float hopPhase;
        Vector3 lastPlayerPosition;
        bool havePlayerPosition;
        float playerSpeed;
        float groundY;
        bool haveGround;
        int groundMask = ~0;

        // Idle fidget
        float nextIdleAction;
        float idleActionElapsed;
        float idleActionDuration;
        bool idleSpin;

        /// <summary>True while the buddy is on screen.</summary>
        public bool IsShown => shown;

        void Awake()
        {
            EnsureSetup();
        }

        void EnsureSetup()
        {
            if (visual != null) return;

            originalParent = transform.parent;

            var visualObject = new GameObject("Visual");
            visual = visualObject.transform;
            visual.SetParent(transform, false);

            int kids = LayerMask.NameToLayer("Kids");
            if (kids >= 0) groundMask = ~(1 << kids);

            nextIdleAction = Time.time + Random.Range(IdleActionMin, IdleActionMax);
            visualObject.SetActive(false);
        }

        /// <summary>
        /// Chooses which plush comes along. Null, the parrot, or an item without a model means nothing to show here.
        /// </summary>
        public void SetBuddy(RewardItem item)
        {
            EnsureSetup();
            bool none = item == null || item.prefab == null || item.id == EquipSlots.ParrotId;
            string id = none ? "" : item.id;
            bool gold = !none && item.IsGold;
            if (id == modelId && (none || gold == modelGold)) return;

            if (model != null)
            {
                Destroy(model);
                model = null;
            }
            modelId = id;
            modelGold = gold;

            if (!none) model = SpawnModel(item);

            // Swapping one toy for another while it is out: a sparkle, as it is for appearing.
            if (shown && model != null) BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            UpdateVisibility();
        }

        /// <summary>Hides the buddy (true) or brings it back (false), with a sparkle either way.</summary>
        public void SetHidden(bool hide)
        {
            EnsureSetup();
            if (hidden == hide) return;

            hidden = hide;
            UpdateVisibility();
        }

        /// <summary>The buddy sits on this seat (in the race cart). It does not hop and it shows even when hidden.</summary>
        public void RideOn(Transform riding)
        {
            if (riding == null) return;

            EnsureSetup();
            seat = riding;
            transform.SetParent(seat, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            ResetVisual();
            UpdateVisibility();
        }

        /// <summary>The buddy gets off the seat and goes back to following.</summary>
        public void StopRiding()
        {
            if (seat == null) return;

            seat = null;
            transform.SetParent(originalParent, true);
            haveGround = false;
            UpdateVisibility();
        }

        /// <summary>The buddy stands still at this spot (for the dress-up mirror). It shows even when hidden.</summary>
        public void HoldAt(Transform spot)
        {
            if (spot == null) return;

            EnsureSetup();
            holdSpot = spot;
            if (seat == null) PlaceAtHold();
            UpdateVisibility();
        }

        /// <summary>The buddy goes back to following.</summary>
        public void ReleaseHold()
        {
            if (holdSpot == null) return;

            holdSpot = null;
            haveGround = false;
            UpdateVisibility();
        }

        void LateUpdate()
        {
            if (seat == null && transform.parent != originalParent)
            {
                // The seat was destroyed under us (the race was torn down).
                transform.SetParent(originalParent, true);
            }

            if (model == null) return;

            if (seat != null)
            {
                RideStep();
                return;
            }
            if (!shown) return;

            if (holdSpot != null)
            {
                PlaceAtHold();
                return;
            }

            FollowStep(Time.deltaTime);
        }

        // ----- Modes -------------------------------------------------------------------------------

        void RideStep()
        {
            // Sitting still on the seat; the cart moves it.
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        void PlaceAtHold()
        {
            transform.SetPositionAndRotation(holdSpot.position, holdSpot.rotation);
            ResetVisual();
            moving = false;
        }

        void FollowStep(float dt)
        {
            PlayerController kid = FindPlayer();
            if (kid == null || dt <= 0f) return;

            TrackPlayerSpeed(kid.transform.position, dt);

            Vector3 target = FollowTarget(kid.transform);
            Vector3 position = transform.position;
            Vector3 flatToTarget = target - position;
            flatToTarget.y = 0f;
            float distance = flatToTarget.magnitude;
            Vector3 flatToPlayer = kid.transform.position - position;
            flatToPlayer.y = 0f;

            if (flatToPlayer.magnitude > teleportDistance)
            {
                Teleport(target);
                return;
            }

            if (moving) moving = distance > StopMoveDistance;
            else moving = distance > StartMoveDistance;

            Vector3 facing = Vector3.zero;
            if (moving)
            {
                float speed = Mathf.Max(playerSpeed * speedFactor, minSpeed);
                float step = Mathf.Min(speed * dt, distance);
                position += flatToTarget / distance * step;
                facing = flatToTarget;
            }
            else
            {
                facing = flatToPlayer;
            }

            position.y = SmoothGround(position, kid.transform.position.y, dt);
            transform.position = position;
            if (facing.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, TurnDegreesPerSecond * dt);
            }

            AnimateVisual(dt);
        }

        void Teleport(Vector3 target)
        {
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            transform.position = new Vector3(target.x, GroundAt(target, FindPlayer().transform.position.y), target.z);
            haveGround = false;
            moving = false;
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
        }

        // ----- Looks -------------------------------------------------------------------------------

        void AnimateVisual(float dt)
        {
            float hop = 0f;
            float squash = 0f;
            float yaw = 0f;

            // The hop only stops on the ground: when he stops, the current hop finishes.
            float before = hopPhase;
            if (moving)
            {
                hopPhase += dt * hopsPerSecond;
            }
            else if (hopPhase - Mathf.Floor(hopPhase) > 0f)
            {
                hopPhase += dt * hopsPerSecond;
                if (Mathf.Floor(hopPhase) > Mathf.Floor(before)) hopPhase = Mathf.Floor(hopPhase);
            }

            float bounce = Mathf.Abs(Mathf.Sin(hopPhase * Mathf.PI));
            hop = bounce * hopHeight;
            squash = Mathf.Pow(1f - bounce, 8f) * (hopPhase - Mathf.Floor(hopPhase) > 0f ? 0.15f : 0f);

            if (!moving && hopPhase == Mathf.Floor(hopPhase))
            {
                IdleFidget(dt, ref hop, ref yaw);
            }
            else
            {
                idleActionElapsed = idleActionDuration;
                idleSpin = false;
            }

            visual.localPosition = new Vector3(0f, hop, 0f);
            visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
            visual.localScale = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f);
        }

        /// <summary>Every few seconds, standing still, it does a little hop or a spin.</summary>
        void IdleFidget(float dt, ref float hop, ref float yaw)
        {
            if (idleActionElapsed >= idleActionDuration)
            {
                idleSpin = false;
                if (Time.time < nextIdleAction) return;

                idleSpin = Random.value < 0.5f;
                idleActionDuration = idleSpin ? IdleSpinSeconds : IdleHopSeconds;
                idleActionElapsed = 0f;
                nextIdleAction = Time.time + idleActionDuration + Random.Range(IdleActionMin, IdleActionMax);
            }

            idleActionElapsed += dt;
            float u = Mathf.Clamp01(idleActionElapsed / idleActionDuration);
            if (idleSpin) yaw = Mathf.SmoothStep(0f, 360f, u);
            else hop = Mathf.Sin(u * Mathf.PI) * IdleHopHeight;
        }

        void ResetVisual()
        {
            if (visual == null) return;

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            hopPhase = 0f;
        }

        // ----- Showing and hiding ------------------------------------------------------------------

        void UpdateVisibility()
        {
            bool want = model != null && (seat != null || holdSpot != null || !hidden);
            if (want == shown) return;

            if (want)
            {
                PlaceForAppearing();
                shown = true;
                visual.gameObject.SetActive(true);
                ResetVisual();
                BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
            }
            else
            {
                BattleEffects.Sparkle(transform.position + Vector3.up * 0.3f);
                shown = false;
                visual.gameObject.SetActive(false);
            }
        }

        /// <summary>When it appears it is already next to the kid (or on its seat or spot), not walking in from far away.</summary>
        void PlaceForAppearing()
        {
            if (seat != null)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }
            if (holdSpot != null)
            {
                PlaceAtHold();
                return;
            }

            PlayerController kid = FindPlayer();
            if (kid == null) return;

            Vector3 target = FollowTarget(kid.transform);
            transform.position = new Vector3(target.x, GroundAt(target, kid.transform.position.y), target.z);
            transform.rotation = Quaternion.LookRotation(Flat(kid.transform.position - target, kid.transform.forward), Vector3.up);
            haveGround = false;
            moving = false;
        }

        // ----- Helpers -----------------------------------------------------------------------------

        GameObject SpawnModel(RewardItem item)
        {
            GameObject instance = Instantiate(item.prefab, visual);

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
            if (item.IsGold) GoldPaint.Apply(instance);

            // An animated buddy (the baby dragon) keeps its Animator and plays its Idle; our code moves it, not the clips.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CoolSchool.Characters.DragonRootLock.Ensure(animator, 0f);
            }

            // Measure at the world origin without turning or scaling, so the bounds are the model's own.
            Vector3 oldPosition = transform.position;
            Quaternion oldRotation = transform.rotation;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (TryGetBounds(instance, out Bounds bounds))
            {
                float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float scale = biggest > 0.0001f ? buddyHeight / biggest : 1f;
                instance.transform.localScale = Vector3.one * scale;

                // Feet on the ground, centred over the object's middle.
                TryGetBounds(instance, out bounds);
                instance.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            }

            transform.SetPositionAndRotation(oldPosition, oldRotation);
            return instance;
        }

        static bool TryGetBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer) continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }

        PlayerController FindPlayer()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            return player;
        }

        /// <summary>The spot behind and to the right of the kid, in his own directions.</summary>
        Vector3 FollowTarget(Transform kid)
        {
            Vector3 forward = Flat(kid.forward, Vector3.forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return kid.position - forward * followBehind + right * followRight;
        }

        void TrackPlayerSpeed(Vector3 position, float dt)
        {
            if (havePlayerPosition)
            {
                Vector3 delta = position - lastPlayerPosition;
                delta.y = 0f;
                float measured = delta.magnitude / dt;
                // A teleport (the wardrobe, the race) is not walking.
                if (measured > MaxPlayerSpeed) measured = 0f;
                playerSpeed = Mathf.Lerp(playerSpeed, measured, 1f - Mathf.Exp(-12f * dt));
            }
            lastPlayerPosition = position;
            havePlayerPosition = true;
        }

        float SmoothGround(Vector3 position, float fallbackY, float dt)
        {
            float wanted = GroundAt(position, fallbackY);
            if (!haveGround)
            {
                groundY = wanted;
                haveGround = true;
            }
            else
            {
                groundY = Mathf.Lerp(groundY, wanted, 1f - Mathf.Exp(-GroundSharpness * dt));
            }
            return groundY;
        }

        /// <summary>Finds the ground with a ray down from 2 m above the kid's feet, ignoring triggers and the kids.</summary>
        float GroundAt(Vector3 position, float referenceY)
        {
            var origin = new Vector3(position.x, referenceY + 2f, position.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return referenceY;
        }

        static Vector3 Flat(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallback;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            }
            return direction.normalized;
        }
    }
}
