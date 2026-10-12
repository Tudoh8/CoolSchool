using System.Collections.Generic;
using CoolSchool.Audio;
using UnityEngine;

namespace CoolSchool.Race
{
    /// <summary>
    /// An animal (a cat, a dog, a hen with chicks, a rabbit) waiting on the sidewalk. When the player's cart gets near, it
    /// walks into the road to stand in its lane. When any cart comes close it hops aside to the nearest edge of the road: it
    /// is never hit. If the cart was the player's, it dips its speed for a moment (the animal made him brake).
    ///
    /// Everything is scripted (no physics). The animal moves this object's transform; the chicks are separate transforms that
    /// must NOT be children of this object. The animator's yes/no parameters (isWalking, isRunning ...) differ per animal, so
    /// only those the animator really has are set. A cart that is jumping high enough passes over the animal and it stays.
    /// </summary>
    public class AnimalCrosser : RaceTrackItem
    {
        enum Phase { Waiting, Walking, Standing, Hopping, Away }

        /// <summary>One chick (or other follower) and what it needs to move.</summary>
        sealed class Follower
        {
            public Transform transform;
            public Animator animator;
            public HashSet<string> flags;
            public Vector3 homePosition;
            public Quaternion homeRotation;
            public Vector3 hopStart;
            public Vector3 hopEnd;
        }

        const float HopTriggerDistance = 3.5f;
        const float LastMomentDistance = 1f;
        const float HopMinSeconds = 0.4f;
        const float HopMaxSeconds = 0.8f;
        const float HopMetresPerSecond = 6f;
        const float HopHeight = 0.7f;
        const float FollowerSpacing = 0.45f;
        const float FollowerStagger = 0.07f;
        const float FollowerSpeedFactor = 1.4f;
        // How far a cart can be beside the animal (sideways) and still scare it: half a lane.
        const float ScareReach = 1.1f;
        const float AirborneIgnoreHeight = 0.4f;
        const float TurnSpeed = 8f;
        // Spare seconds the animal wants in the road before the cart arrives.
        const float SpareSeconds = 1.5f;

        [Tooltip("Polyperfect animal controller: yes/no parameters isWalking / isRunning / isJumping, set only if they exist.")]
        [SerializeField] Animator animator;
        [Tooltip("The animal's own call, copied from the prefab by the builder.")]
        [SerializeField] AudioClip sound;
        [Tooltip("cat | dog | hen | rabbit (the voice line race.animal_<kind>).")]
        [SerializeField] string kind = "cat";
        [Tooltip("Where it waits (west or east sidewalk). Not a child of this object. Falls back to where this object stands.")]
        [SerializeField] Transform sidewalkPoint;
        [Tooltip("Optional: chicks that follow the hen. Not children of this object.")]
        [SerializeField] Transform[] followers;
        [SerializeField] float triggerDistance = 30f;
        [SerializeField] float walkSpeed = 1.4f;

        readonly List<Follower> chicks = new List<Follower>();
        HashSet<string> flags;
        bool flagsRead;
        bool homeKnown;
        Vector3 homePosition;
        Quaternion homeRotation;

        Phase phase = Phase.Waiting;
        float walkProgress;
        float walkLength;
        Vector3 roadPosition;
        float hopTime;
        float hopSeconds;
        Vector3 hopStart;
        Vector3 hopEnd;
        int hopSide;

        /// <summary>cat | dog | hen | rabbit.</summary>
        public string Kind => kind;

        /// <summary>True while the animal is walking into the road or standing in it.</summary>
        public bool InRoad => phase == Phase.Walking || phase == Phase.Standing;

        /// <summary>Jumping over it is fine (it stays then).</summary>
        public override float Clearance => 1f;

        public override bool Jumpable => true;

        /// <summary>It is in the way until it has hopped aside.</summary>
        public override bool Blocking => phase == Phase.Waiting || phase == Phase.Walking || phase == Phase.Standing;

        public override bool MayBlock => true;

        /// <summary>Its distance along the track is where it waits on the sidewalk.</summary>
        protected override Vector3 AnchorPosition
        {
            get
            {
                EnsureHome();
                return homePosition;
            }
        }

        void Awake()
        {
            EnsureHome();
        }

        void EnsureHome()
        {
            if (homeKnown) return;

            homeKnown = true;
            homePosition = sidewalkPoint != null ? sidewalkPoint.position : transform.position;
            homeRotation = sidewalkPoint != null ? sidewalkPoint.rotation : transform.rotation;

            chicks.Clear();
            if (followers == null) return;
            foreach (Transform chick in followers)
            {
                if (chick == null) continue;
                chicks.Add(new Follower
                {
                    transform = chick,
                    animator = chick.GetComponentInChildren<Animator>(),
                    homePosition = chick.position,
                    homeRotation = chick.rotation,
                });
            }
        }

        // ----- The animal's life ----------------------------------------------------------------------

        void Update()
        {
            if (Track == null) return;

            switch (phase)
            {
                case Phase.Waiting:
                    TryStartCrossing();
                    break;
                case Phase.Walking:
                    UpdateWalking(Time.deltaTime);
                    ScareCheck();
                    break;
                case Phase.Standing:
                    FaceTheCarts(Time.deltaTime);
                    ScareCheck();
                    break;
                case Phase.Hopping:
                    UpdateHopping(Time.deltaTime);
                    break;
            }
        }

        void TryStartCrossing()
        {
            CartController player = Track.PlayerCart;
            if (player == null) return;

            float gap = Distance - player.Distance;
            if (gap <= 0f) return;

            // Far enough ahead to be seen, and early enough to be in the road before a fast cart gets here.
            roadPosition = RoadPosition();
            float walkTime = Vector3.Distance(Flat(homePosition), Flat(roadPosition)) / Mathf.Max(0.1f, walkSpeed);
            float trigger = Mathf.Max(triggerDistance, player.Speed * (walkTime + SpareSeconds));
            if (gap > trigger) return;

            phase = Phase.Walking;
            walkProgress = 0f;
            walkLength = Mathf.Max(0.1f, Vector3.Distance(homePosition, roadPosition));
            SetAnimation(animator, ref flags, ref flagsRead, walking: true, running: false);
            foreach (Follower chick in chicks) SetAnimation(chick, walking: true, running: false);
            Track.RaiseAnimalCrossing(this);
        }

        void UpdateWalking(float dt)
        {
            walkProgress = Mathf.Min(1f, walkProgress + walkSpeed * dt / walkLength);
            Vector3 position = Vector3.Lerp(homePosition, roadPosition, walkProgress);
            transform.position = position;

            Vector3 direction = Flat(roadPosition - homePosition);
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            // The chicks trail behind in a little line, a bit faster than the hen so they keep up.
            Vector3 back = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            for (int i = 0; i < chicks.Count; i++)
            {
                Follower chick = chicks[i];
                Vector3 target = position - back * (FollowerSpacing * (i + 1));
                target.y = position.y;
                chick.transform.position = Vector3.MoveTowards(chick.transform.position, target, walkSpeed * FollowerSpeedFactor * dt);
                if (back != Vector3.zero) chick.transform.rotation = Quaternion.LookRotation(back, Vector3.up);
            }

            if (walkProgress < 1f) return;

            phase = Phase.Standing;
            SetAnimation(animator, ref flags, ref flagsRead, walking: false, running: false);
            foreach (Follower chick in chicks) SetAnimation(chick, walking: false, running: false);
        }

        /// <summary>Standing in the road, it looks toward the carts that come.</summary>
        void FaceTheCarts(float dt)
        {
            Quaternion look = Quaternion.LookRotation(-Track.Forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Clamp01(dt * TurnSpeed));
            foreach (Follower chick in chicks)
                chick.transform.rotation = Quaternion.Slerp(chick.transform.rotation, look, Mathf.Clamp01(dt * TurnSpeed));
        }

        // ----- Scared by a cart -----------------------------------------------------------------------

        void ScareCheck()
        {
            IReadOnlyList<CartController> carts = Track.Carts;
            for (int i = 0; i < carts.Count; i++)
            {
                CartController cart = carts[i];
                if (cart == null) continue;

                float gap = Distance - cart.Distance;
                if (gap < -0.5f || gap > HopTriggerDistance) continue;
                if (!IsBeside(cart)) continue;

                // A cart that is jumping over is no danger; one that is still coming down on it is.
                if (gap > LastMomentDistance)
                {
                    if (cart.IsAirborne && cart.Height > AirborneIgnoreHeight) continue;
                }
                else if (cart.Height >= Clearance)
                {
                    continue;
                }

                StartHop(cart);
                return;
            }
        }

        /// <summary>True when the cart is right beside the animal or one of its chicks, sideways.</summary>
        bool IsBeside(CartController cart)
        {
            if (Mathf.Abs(Track.LateralOf(transform.position) - cart.LaneX) < ScareReach) return true;
            foreach (Follower chick in chicks)
                if (Mathf.Abs(Track.LateralOf(chick.transform.position) - cart.LaneX) < ScareReach) return true;
            return false;
        }

        public override void OnCartPassing(CartController cart, RaceTrack track)
        {
            // The sweep is the safety net: a cart that got here without ScareCheck seeing it still never hits the animal.
            if (!InRoad || cart == null) return;
            if (!IsBeside(cart) || cart.Height >= Clearance) return;

            StartHop(cart);
        }

        void StartHop(CartController scaredBy)
        {
            phase = Phase.Hopping;
            hopTime = 0f;

            // To the nearest edge of the road; from the middle lane, back to the side it came from.
            float lateral = Track.LateralOf(transform.position);
            hopSide = Mathf.Abs(lateral) > 0.5f ? (lateral < 0f ? -1 : 1) : (Track.LateralOf(homePosition) < 0f ? -1 : 1);
            float edge = 1.5f * Track.LaneWidth + 0.5f;

            hopStart = transform.position;
            hopEnd = Track.PointAt(Distance, hopSide * edge);
            hopEnd.y = RoadPosition().y;
            hopSeconds = Mathf.Clamp(Vector3.Distance(Flat(hopStart), Flat(hopEnd)) / HopMetresPerSecond, HopMinSeconds, HopMaxSeconds);

            for (int i = 0; i < chicks.Count; i++)
            {
                Follower chick = chicks[i];
                chick.hopStart = chick.transform.position;
                // The chicks hop out beyond the hen, onto the sidewalk side.
                chick.hopEnd = Track.PointAt(Distance - 0.2f * (i + 1), hopSide * (edge + FollowerSpacing * (i + 1)));
                chick.hopEnd.y = hopEnd.y;
            }

            SetAnimation(animator, ref flags, ref flagsRead, walking: false, running: true);
            foreach (Follower chick in chicks) SetAnimation(chick, walking: false, running: true);
            PlaySound(transform.position);
            FaceDirection(transform, hopEnd - hopStart);

            if (scaredBy != null && scaredBy.IsPlayer)
            {
                scaredBy.Brake(0.85f, 0.5f);
                Track.RaiseItemHit(this, scaredBy);
            }
        }

        void UpdateHopping(float dt)
        {
            hopTime += dt;

            float k = Mathf.Clamp01(hopTime / hopSeconds);
            transform.position = Vector3.Lerp(hopStart, hopEnd, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * HopHeight);

            float lastK = k;
            for (int i = 0; i < chicks.Count; i++)
            {
                Follower chick = chicks[i];
                float ck = Mathf.Clamp01((hopTime - FollowerStagger * (i + 1)) / hopSeconds);
                chick.transform.position = Vector3.Lerp(chick.hopStart, chick.hopEnd, ck) + Vector3.up * (Mathf.Sin(ck * Mathf.PI) * HopHeight * 0.7f);
                if (ck > 0f && ck < 1f) FaceDirection(chick.transform, chick.hopEnd - chick.hopStart);
                lastK = Mathf.Min(lastK, ck);
            }

            if (lastK < 1f) return;

            // Landed at the side of the road: stand there and watch the race.
            phase = Phase.Away;
            SetAnimation(animator, ref flags, ref flagsRead, walking: false, running: false);
            foreach (Follower chick in chicks) SetAnimation(chick, walking: false, running: false);

            Quaternion towardRoad = Quaternion.LookRotation(-Track.Right * hopSide, Vector3.up);
            transform.rotation = towardRoad;
            foreach (Follower chick in chicks) chick.transform.rotation = towardRoad;
        }

        public override void ResetItem()
        {
            EnsureHome();
            phase = Phase.Waiting;
            walkProgress = 0f;
            hopTime = 0f;

            transform.position = homePosition;
            transform.rotation = homeRotation;
            foreach (Follower chick in chicks)
            {
                chick.transform.position = chick.homePosition;
                chick.transform.rotation = chick.homeRotation;
            }

            SetAnimation(animator, ref flags, ref flagsRead, walking: false, running: false);
            foreach (Follower chick in chicks) SetAnimation(chick, walking: false, running: false);
        }

        // ----- Helpers ----------------------------------------------------------------------------------

        /// <summary>Where it stands in the road: its lane's centre, level with the road.</summary>
        Vector3 RoadPosition()
        {
            return Track.PointAt(Distance, Track.LaneOffset(lane));
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static void FaceDirection(Transform who, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) who.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        void PlaySound(Vector3 where)
        {
            if (sound != null) AudioSource.PlayClipAtPoint(sound, where, Mathf.Clamp01(0.7f * Sfx.SfxVolume));
        }

        void SetAnimation(Follower chick, bool walking, bool running)
        {
            bool read = chick.flags != null;
            SetAnimation(chick.animator, ref chick.flags, ref read, walking, running);
        }

        /// <summary>
        /// Sets isWalking / isRunning / isJumping on an animator, but only those it has (the animals' controllers differ).
        /// Does nothing while the animator is switched off, then the next change sets them right.
        /// </summary>
        static void SetAnimation(Animator target, ref HashSet<string> known, ref bool read, bool walking, bool running)
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return;

            if (!read)
            {
                known = new HashSet<string>();
                foreach (AnimatorControllerParameter parameter in target.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Bool) known.Add(parameter.name);
                read = true;
            }

            if (known.Contains("isWalking")) target.SetBool("isWalking", walking);
            if (known.Contains("isRunning")) target.SetBool("isRunning", running);
            if (known.Contains("isJumping")) target.SetBool("isJumping", false);
        }
    }
}
