using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;

namespace CoolSchool.Pirates
{
    /// <summary>
    /// A silly skeleton pirate. It rides the skeleton boat waving, hops off on the island beach and challenges the player
    /// to a sword duel: slow, and very clear about what it does (it stands still, raises its sword and wobbles first). Only
    /// one skeleton attacks at a time. When the sword has bumped it enough it falls apart into giggling bones, rebuilds
    /// itself and becomes a friend who follows the player. A bump never hurts: the player only wobbles.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable))]
    public class SkeletonPirateAI : MonoBehaviour
    {
        [Tooltip("Slower than the player, who runs at about 4.")]
        [SerializeField] float moveSpeed = 1.5f;
        [SerializeField] float attackRange = 1.3f;
        [Tooltip("How long it stands still, raising its sword and wobbling, before it swings.")]
        [SerializeField] float windUpSeconds = 1.0f;
        [Tooltip("Rest after a swing, so the player has plenty of time to answer.")]
        [SerializeField] float recoverSeconds = 2.5f;
        [Tooltip("A ring or '!' shown during the wind-up. Optional.")]
        [SerializeField] GameObject telegraph;
        [Header("As a friend")]
        [SerializeField] float friendSpeed = 2.6f;
        [Tooltip("Closer than this to the player: stand still. Further than followMax: walk over.")]
        [SerializeField] float followMin = 2.2f;
        [SerializeField] float followMax = 4.5f;
        [SerializeField] Color friendConfettiColor = new Color(1f, 0.95f, 0.8f, 1f);

        enum Mode { Off, Riding, Hopping, Waiting, Fighting, Falling, Friend }

        const float HitPauseSeconds = 0.8f;
        const float GiveUpApproachSeconds = 10f;
        const float WaitDistanceExtra = 2.2f;
        const float SwingDelaySeconds = 0.25f;
        const float PreferPlayerChance = 0.6f;
        const float HopSeconds = 0.5f;
        const float RingTolerance = 1.3f;

        // Only one skeleton swings at a time across all of them, so it never gets crowded.
        static SkeletonPirateAI attackToken;

        NavMeshAgent agent;
        Hittable hittable;
        KidAnimator kidAnimator;
        BonePile bonePile;
        WeaponHolder weapons;
        Transform visualRoot;
        Vector3 visualBaseScale = Vector3.one;

        Mode mode = Mode.Off;
        Transform originalParent;
        bool mounted;
        Transform target;
        Transform followTarget;
        int followSlot = -1;
        Hittable playerHittable;
        bool wantFight;
        int hitsToDefeat = 1;
        Coroutine routine;
        Coroutine walkRoutine;
        float hitPauseUntil;
        float nextRepathTime;
        float nextCheerTime;
        bool dancing;

        /// <summary>True once it has been bumped, fallen apart and rebuilt itself as a friend.</summary>
        public bool IsFriend { get; private set; }

        /// <summary>Raised when the skeleton has put itself back together as a friend.</summary>
        public event Action<SkeletonPirateAI> BecameFriend;

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => attackToken = null;

        void Awake() => CacheReferences();

        /// <summary>Skeletons start switched off, so Ride may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            kidAnimator = GetComponent<KidAnimator>();
            bonePile = GetComponent<BonePile>();
            weapons = GetComponent<WeaponHolder>();

            // Walk-through: a solid body would let skeletons shove the player away from what he has to do. Covers scenes
            // built before the builder made the capsule a trigger.
            foreach (Collider body in GetComponents<Collider>()) body.isTrigger = true;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visualRoot = animator.transform;
                visualBaseScale = visualRoot.localScale;
            }
        }

        void OnEnable()
        {
            CacheReferences();
            hittable.Hit += OnHit;
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Defeated -= OnDefeated;
            }

            routine = null;
            walkRoutine = null;
            ReleaseToken();
            SetTelegraph(false);
            RestoreScale();
        }

        // ----- Setting up (called by the adventure) ------------------------------------------------

        /// <summary>How many sword hits make it fall apart (1 or 2). Set before the fight.</summary>
        public void SetHitsToDefeat(int hits)
        {
            CacheReferences();
            hitsToDefeat = Mathf.Max(1, hits);
            hittable.SetHitsToDefeat(hitsToDefeat);
        }

        /// <summary>Appears on a seat of the boat (switched on, the navigation off, parented) and waves now and then.</summary>
        public void Ride(Transform seat)
        {
            CacheReferences();
            if (seat == null) return;

            StopRoutines();
            ReleaseToken();
            hittable.ResetState();
            hittable.SetHitsToDefeat(hitsToDefeat);
            IsFriend = false;
            dancing = false;
            target = null;
            followTarget = null;
            followSlot = -1;
            wantFight = false;

            if (!mounted) originalParent = transform.parent;
            else transform.SetParent(originalParent, true);

            // Off before switching on: an agent that starts away from the NavMesh would only complain.
            agent.enabled = false;
            hittable.enabled = false;
            if (kidAnimator != null) kidAnimator.ForceIdle = true;

            transform.SetPositionAndRotation(seat.position, seat.rotation);
            gameObject.SetActive(true);
            SeatUtil.Mount(transform, seat);
            mounted = true;
            mode = Mode.Riding;

            // The weapon could not be shown while switched off, so do it now.
            if (weapons != null) weapons.ShowWeapons(true);
            nextCheerTime = Time.time + UnityEngine.Random.Range(0.5f, 2f);
            routine = StartCoroutine(RideRoutine());
        }

        /// <summary>Hops off the boat to the ground point (a sparkle where it lands), then stands ready on the beach.</summary>
        public void HopOff(Vector3 groundPoint)
        {
            CacheReferences();
            if (mode == Mode.Hopping || mode == Mode.Waiting || mode == Mode.Fighting) return;

            StopRoutines();
            if (mounted)
            {
                SeatUtil.Dismount(transform, originalParent);
                mounted = false;
            }
            mode = Mode.Hopping;
            routine = StartCoroutine(HopOffRoutine(groundPoint));
        }

        /// <summary>Starts the duel with the player. Can be called before the hop has finished: it starts on landing.</summary>
        public void Fight(Transform player)
        {
            CacheReferences();
            target = player;
            playerHittable = player != null ? player.GetComponent<Hittable>() : null;
            wantFight = true;
            if (mode == Mode.Waiting) StartFighting();
        }

        /// <summary>
        /// As a friend: follows this loosely, 2 to 4 metres behind. Null: stays and cheers. With a slot (0, 1, 2...) it keeps its
        /// own place on a wide ring beside and behind the leader instead, so a crowd of friends spreads out and never bunches up.
        /// </summary>
        public void Follow(Transform leader, int slot = -1)
        {
            followTarget = leader;
            followSlot = slot;
        }

        /// <summary>Walks to a point (as a friend) and turns to face a direction there. arrived is called when it gets there.</summary>
        public void WalkTo(Vector3 point, Vector3 faceDirection, Action arrived = null)
        {
            CacheReferences();
            if (walkRoutine != null) StopCoroutine(walkRoutine);
            if (!isActiveAndEnabled) return;

            walkRoutine = StartCoroutine(WalkRoutine(point, faceDirection, arrived));
        }

        /// <summary>Dances (a random dance) or stops.</summary>
        public void Dance(bool on)
        {
            CacheReferences();
            dancing = on;
            if (on)
            {
                if (AgentReady)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                if (kidAnimator != null) kidAnimator.SetDancing(true, UnityEngine.Random.Range(0, 3));
            }
            else if (kidAnimator != null)
            {
                kidAnimator.SetDancing(false);
            }
        }

        /// <summary>
        /// Safety net: turns this skeleton into a friend now, as if it had been bumped enough. Does nothing before it has
        /// landed on the beach.
        /// </summary>
        public void Befriend()
        {
            CacheReferences();
            if (IsFriend || mode == Mode.Falling || mode == Mode.Off || mode == Mode.Riding || mode == Mode.Hopping) return;

            if (!hittable.IsDefeated)
            {
                hittable.SetHitsToDefeat(1);
                hittable.TakeHit(transform.position, 99);
            }
            if (hittable.IsDefeated) BeginFallApart();
            else StartCoroutine(BefriendSoon());
        }

        /// <summary>Back to the way it was before the game: not a friend, whole, off the boat, switched off.</summary>
        public void ResetSkeleton()
        {
            CacheReferences();

            StopRoutines();
            ReleaseToken();
            if (bonePile != null) bonePile.ResetPile();

            if (mounted)
            {
                SeatUtil.Dismount(transform, originalParent);
                mounted = false;
            }
            if (kidAnimator != null)
            {
                kidAnimator.ForceIdle = false;
                if (dancing) kidAnimator.SetDancing(false);
            }
            dancing = false;
            SetTelegraph(false);
            RestoreScale();

            IsFriend = false;
            mode = Mode.Off;
            target = null;
            followTarget = null;
            followSlot = -1;
            wantFight = false;
            hittable.ResetState();
            hittable.enabled = true;

            gameObject.SetActive(false);
        }

        // ----- On the boat and getting off ---------------------------------------------------------

        IEnumerator RideRoutine()
        {
            while (true)
            {
                if (Time.time >= nextCheerTime)
                {
                    nextCheerTime = Time.time + UnityEngine.Random.Range(3f, 6f);
                    if (kidAnimator != null) kidAnimator.PlayCheer();
                }
                yield return null;
            }
        }

        IEnumerator HopOffRoutine(Vector3 groundPoint)
        {
            Vector3 land = groundPoint;
            if (NavMesh.SamplePosition(groundPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas)) land = hit.position;

            Vector3 flat = (target != null ? target.position : land + Vector3.forward) - land;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat, Vector3.up) : transform.rotation;

            if (kidAnimator != null) kidAnimator.PlayJump();
            yield return SeatUtil.HopTo(transform, () => land, HopSeconds, 0.8f, () => facing);

            if (kidAnimator != null) kidAnimator.ForceIdle = false;
            agent.enabled = true;
            if (!agent.isOnNavMesh) agent.Warp(land);
            hittable.enabled = true;
            BattleEffects.Sparkle(land + Vector3.up * 0.8f);

            routine = null;
            mode = Mode.Waiting;
            if (wantFight) StartFighting();
        }

        // ----- The duel ----------------------------------------------------------------------------

        void StartFighting()
        {
            mode = Mode.Fighting;
            hittable.SetHitsToDefeat(hitsToDefeat);
            if (AgentReady)
            {
                agent.speed = moveSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }
            routine = StartCoroutine(FightRoutine());
        }

        IEnumerator FightRoutine()
        {
            // A moment to look around before the first challenge.
            yield return new WaitForSeconds(1.2f);

            while (true)
            {
                if (target == null)
                {
                    StopMoving();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                // Somebody else is swinging: wait at a polite distance and watch.
                if (attackToken != null && attackToken != this && attackToken.isActiveAndEnabled)
                {
                    HoldBack();
                    yield return null;
                    continue;
                }

                attackToken = this;
                Hittable victim = PickVictim();
                if (victim == null)
                {
                    ReleaseToken();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                yield return Approach(victim);

                if (victim.IsAlive && Hittable.FlatDistance(transform.position, victim.transform.position) <= attackRange + 0.8f)
                {
                    yield return WindUpAndSwing(victim);
                    yield return Recover(victim);
                }

                ReleaseToken();
                // A short breather, so another skeleton gets its turn.
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>Walks up to the player's side of the beach and stands there facing him, until the token is free.</summary>
        void HoldBack()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            float hold = attackRange + WaitDistanceExtra;

            if (distance > hold + 0.5f && Time.time >= hitPauseUntil)
            {
                if (AgentReady && Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + 0.3f;
                    agent.updateRotation = true;
                    agent.speed = moveSpeed;
                    agent.isStopped = false;
                    MoveTo(target.position - toTarget / distance * hold);
                }
            }
            else
            {
                StopMoving();
                FaceDirection(toTarget, 360f);
            }
        }

        IEnumerator Approach(Hittable victim)
        {
            float giveUp = Time.time + GiveUpApproachSeconds;
            while (victim.IsAlive && Time.time < giveUp)
            {
                if (Hittable.FlatDistance(transform.position, victim.transform.position) <= attackRange) break;

                if (Time.time < hitPauseUntil) StopMoving();
                else if (AgentReady)
                {
                    agent.updateRotation = true;
                    agent.speed = moveSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(victim.transform.position);
                }
                yield return null;
            }
            StopMoving();
        }

        IEnumerator WindUpAndSwing(Hittable victim)
        {
            StopMoving();
            if (AgentReady) agent.updateRotation = false;
            SetTelegraph(true);

            bool interrupted = false;
            float elapsed = 0f;
            while (elapsed < windUpSeconds)
            {
                if (Time.time < hitPauseUntil || !victim.IsAlive)
                {
                    interrupted = true;
                    break;
                }

                FaceVictim(victim);
                SetCrouch(Mathf.Sin(elapsed / windUpSeconds * Mathf.PI));
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetTelegraph(false);
            RestoreScale();
            if (interrupted) yield break;

            FaceVictim(victim);
            if (kidAnimator != null) kidAnimator.PlayAttack();
            yield return new WaitForSeconds(SwingDelaySeconds);

            Sfx.Play("battle.sword_swing", transform.position);
            BattleEffects.Swoosh(transform.position + transform.forward * 0.8f + Vector3.up * 0.9f);

            // Only a wobble: the player is never knocked out.
            if (victim.IsAlive && Hittable.FlatDistance(transform.position, victim.transform.position) <= attackRange + 0.6f)
                victim.TakeHit(transform.position);

            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Recover(Hittable victim)
        {
            float elapsed = 0f;
            bool steppedBack = false;
            while (elapsed < recoverSeconds)
            {
                if (!steppedBack && elapsed >= 0.2f && Time.time >= hitPauseUntil && AgentReady)
                {
                    steppedBack = true;
                    Vector3 away = -Hittable.FlatDirection(transform.position, victim.transform.position);
                    Vector3 spot = transform.position + away * 1f;
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    {
                        agent.updateRotation = false;
                        agent.speed = moveSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(hit.position);
                    }
                }

                if (victim.IsAlive) FaceVictim(victim);
                elapsed += Time.deltaTime;
                yield return null;
            }

            StopMoving();
            if (AgentReady) agent.updateRotation = true;
        }

        /// <summary>The player most of the time, now and then a crew classmate who is close by.</summary>
        Hittable PickVictim()
        {
            if (playerHittable != null && playerHittable.IsAlive && UnityEngine.Random.value < PreferPlayerChance)
                return playerHittable;

            Hittable nearest = null;
            float best = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (other.Team != BattleTeam.Heroes || !other.IsAlive) continue;

                float distance = Hittable.FlatDistance(transform.position, other.transform.position);
                if (distance >= best) continue;

                best = distance;
                nearest = other;
            }
            return nearest != null ? nearest : playerHittable;
        }

        // ----- Bumped, falling apart, friends ------------------------------------------------------

        void OnHit(Hittable who)
        {
            if (who.IsDefeated) return;

            hitPauseUntil = Time.time + HitPauseSeconds;
            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        void OnDefeated(Hittable who)
        {
            BeginFallApart();
        }

        void BeginFallApart()
        {
            if (mode == Mode.Falling || IsFriend) return;

            mode = Mode.Falling;
            StopRoutines();
            ReleaseToken();
            SetTelegraph(false);
            RestoreScale();
            StopMoving();

            Sfx.Play("battle.giggle", transform.position);
            if (bonePile != null) bonePile.FallApart(OnRebuilt);
            else routine = StartCoroutine(RebuildWithoutBones());
        }

        /// <summary>Without a bone pile (not set up): a tumble and a giggle instead.</summary>
        IEnumerator RebuildWithoutBones()
        {
            if (kidAnimator != null) kidAnimator.PlayTumble();
            yield return new WaitForSeconds(2.2f);
            routine = null;
            OnRebuilt();
        }

        void OnRebuilt()
        {
            if (mode != Mode.Falling) return;

            IsFriend = true;
            mode = Mode.Friend;

            Vector3 above = transform.position + Vector3.up * 1f;
            BattleEffects.Confetti(above, friendConfettiColor);
            BattleEffects.Sparkle(above);
            if (kidAnimator != null) kidAnimator.PlayCheer();
            VoicePlayer.Say("pirates.skeleton_friend");

            if (AgentReady)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = friendSpeed;
            }
            nextCheerTime = Time.time + UnityEngine.Random.Range(3f, 6f);
            BecameFriend?.Invoke(this);
        }

        IEnumerator BefriendSoon()
        {
            for (int tries = 0; tries < 4 && mode != Mode.Falling && !IsFriend; tries++)
            {
                yield return new WaitForSeconds(0.7f);
                if (hittable.TakeHit(transform.position, 99) || hittable.IsDefeated) break;
            }
            if (hittable.IsDefeated) BeginFallApart();
        }

        void Update()
        {
            if (mode != Mode.Friend || dancing || walkRoutine != null || !AgentReady) return;

            FollowLeader();

            if (Time.time >= nextCheerTime && agent.velocity.sqrMagnitude < 0.05f)
            {
                nextCheerTime = Time.time + UnityEngine.Random.Range(4f, 8f);
                if (kidAnimator != null) kidAnimator.PlayCheer();
            }
        }

        void FollowLeader()
        {
            agent.speed = friendSpeed;
            if (followTarget == null) return;

            Vector3 leader = followTarget.position;
            float distance = Hittable.FlatDistance(transform.position, leader);

            if (followSlot >= 0)
            {
                // Own place on the ring: stays put until the leader has moved away from it.
                Vector3 place = RingPlace(leader, followSlot);
                if (Hittable.FlatDistance(transform.position, place) > RingTolerance)
                {
                    agent.isStopped = false;
                    agent.updateRotation = true;
                    MoveTo(place);
                }
                else if (agent.velocity.sqrMagnitude < 0.05f)
                {
                    FaceDirection(leader - transform.position, 360f);
                }
                return;
            }

            if (distance > followMax)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                Vector3 away = -Hittable.FlatDirection(transform.position, leader);
                MoveTo(leader + away * ((followMin + followMax) * 0.5f));
            }
            else if (distance < followMin)
            {
                agent.ResetPath();
                FaceDirection(leader - transform.position, 360f);
            }
        }

        /// <summary>
        /// A friend's place on the wide ring around the leader. Slots alternate east and west, starting beside the leader (70
        /// degrees from north) and moving round to the back, and the radius alternates too, so nobody stands in front or on top
        /// of anybody else. The places are fixed to the world's compass, so friends do not circle around when he turns.
        /// </summary>
        static Vector3 RingPlace(Vector3 leader, int slot)
        {
            float side = slot % 2 == 0 ? 1f : -1f;
            int step = slot / 2;
            float degrees = side * (70f + 35f * step);
            float radius = 2.8f + (step % 2) * 1.1f;
            return leader + Quaternion.Euler(0f, degrees, 0f) * Vector3.forward * radius;
        }

        IEnumerator WalkRoutine(Vector3 point, Vector3 faceDirection, Action arrived)
        {
            dancing = false;
            if (kidAnimator != null) kidAnimator.SetDancing(false);

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) point = hit.position;
            if (AgentReady)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = friendSpeed;
                agent.SetDestination(point);

                float giveUp = Time.time + 8f;
                yield return null;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > 0.4f))
                    yield return null;
                agent.isStopped = true;
            }

            for (float elapsed = 0f; elapsed < 0.5f; elapsed += Time.deltaTime)
            {
                FaceDirection(faceDirection, 540f);
                yield return null;
            }

            walkRoutine = null;
            arrived?.Invoke();
        }

        // ----- Helpers -----------------------------------------------------------------------------

        void StopRoutines()
        {
            if (routine != null) StopCoroutine(routine);
            if (walkRoutine != null) StopCoroutine(walkRoutine);
            routine = null;
            walkRoutine = null;
        }

        void ReleaseToken()
        {
            if (attackToken == this) attackToken = null;
        }

        void MoveTo(Vector3 destination)
        {
            if (Time.time < nextRepathTime) return;
            nextRepathTime = Time.time + 0.25f;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        void StopMoving()
        {
            if (!AgentReady) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        void FaceVictim(Hittable victim)
        {
            FaceDirection(Hittable.FlatDirection(transform.position, victim.transform.position), 360f);
        }

        void FaceDirection(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, degreesPerSecond * Time.deltaTime);
        }

        void SetTelegraph(bool on)
        {
            if (telegraph != null && telegraph.activeSelf != on) telegraph.SetActive(on);
        }

        /// <summary>Squashes the model a little, as if getting ready to swing. 0 = normal, 1 = most crouched.</summary>
        void SetCrouch(float amount)
        {
            if (visualRoot == null) return;

            visualRoot.localScale = new Vector3(
                visualBaseScale.x * (1f + 0.06f * amount),
                visualBaseScale.y * (1f - 0.12f * amount),
                visualBaseScale.z * (1f + 0.06f * amount));
        }

        void RestoreScale()
        {
            if (visualRoot != null) visualRoot.localScale = visualBaseScale;
        }
    }
}
