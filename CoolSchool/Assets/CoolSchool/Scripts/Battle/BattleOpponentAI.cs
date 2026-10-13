using System;
using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Characters;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace CoolSchool.Battle
{
    /// <summary>
    /// A goblin or troll classmate in the knights game. It climbs a ladder up the outside of the castle wall and
    /// jumps down into the courtyard (LadderClimb). Then it is slow and very clear about what it will do:
    /// it walks up, stands still and shows a "!" while winding up, swings, then steps back.
    /// When the knights bump it enough it tumbles, giggles and runs back to its ladder's landing, where it
    /// vanishes in a sparkle until the next wave.
    /// In the castle adventure it comes in four kinds (GoblinKind): Normal, Shield (shield up until it swings; a block makes
    /// it dizzy), Big (a troll that shrinks with every bump) and Tiny (hides in a barrel and only runs around). Any goblin can
    /// also wait on a wall perch to be counted (ActivateOnPerch), call out a thief (BecomeThief) or chase a knight who needs
    /// help (ForceTarget).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable), typeof(SwordFighter))]
    public class BattleOpponentAI : MonoBehaviour
    {
        [Tooltip("Slower than the player, who runs at about 4.")]
        [SerializeField] float moveSpeed = 1.6f;
        [Tooltip("How long it stands still showing the '!' before swinging.")]
        [SerializeField] float windUpSeconds = 0.9f;
        [Tooltip("Rest after a swing, so the player has plenty of time to answer.")]
        [SerializeField] float recoverSeconds = 2.5f;
        [Tooltip("Chance of picking the player rather than a knight classmate as the target.")]
        [SerializeField, Range(0f, 1f)] float preferPlayerChance = 0.6f;
        [Tooltip("A '!' object shown during the wind-up. Optional.")]
        [SerializeField] GameObject telegraph;

        [Header("Castle adventure")]
        [Tooltip("Which sort of goblin this is. Set by the builder per pool member.")]
        [SerializeField] GoblinKind kind = GoblinKind.Normal;
        [Tooltip("A ring of three small spinning yellow stars just above the head, shown while a shield goblin is dizzy. Optional.")]
        [SerializeField] GameObject dizzyStars;

        const float AttackDistance = 1.4f;
        const float HitPauseSeconds = 0.8f;
        const float GiveUpApproachSeconds = 10f;
        const float DefeatedRunSpeed = 3f;
        const float GoHomeTimeoutSeconds = 8f;
        const float GoHomeArriveDistance = 0.6f;

        // The troll.
        const float BigScale = 1.35f;
        const float BigSpeed = 1.2f;
        const float ShrinkPerHit = 0.12f;
        const float ShrinkSeconds = 0.25f;
        const float MinTrollScale = 0.55f;

        // The shield goblin.
        const float DizzySeconds = 3f;

        // The wall perches (counting goblins).
        const float PerchBelowMeters = 1.2f;
        const float PerchBehindMeters = 0.5f;
        const float PerchHopSeconds = 0.4f;
        const float PerchHopHeight = 0.55f;
        const float PerchCheerSeconds = 2.5f;
        const float PerchSafetySeconds = 60f;
        const float JumpDownSeconds = 0.6f;
        const float JumpDownHeight = 1f;

        // The tiny goblin in the barrel.
        const float TinySpeed = 2.6f;
        const float BarrelTipDistance = 2.5f;
        const float BarrelTipAfterSeconds = 20f;
        const float PeekHeight = 0.4f;
        const float PeekSeconds = 0.6f;
        const float TinyRunSeconds = 40f;
        const int RunCandidates = 8;

        // The thief.
        const float ThiefGrabSpeed = 2.6f;
        const float ThiefRunSpeed = 2.2f;
        const float ThiefTripAfterSeconds = 40f;
        const float ThiefReachLootSeconds = 10f;

        // What was said already in this castle visit (the once-per-visit lines). KnightsGame clears it with BeginVisit.
        static readonly HashSet<string> saidThisVisit = new HashSet<string>();

        NavMeshAgent agent;
        Hittable hittable;
        SwordFighter fighter;
        KidAnimator kidAnimator;
        LadderClimb ladderClimb;
        Collider body;
        WeaponHolder weapons;
        Transform visualRoot;
        Vector3 visualBaseScale = Vector3.one;
        PlayerController player;
        WallLadder ladder;
        Coroutine routine;
        float hitPauseUntil;

        // Size: the troll starts big and shrinks; the wind-up squashes a little on top.
        float sizeFactor = 1f;
        float sizeTarget = 1f;
        float crouchAmount;
        float currentSpeed;

        // Where it runs to when it is sent home or tumbles, when it has no ladder (perch and barrel goblins).
        Vector3 homePoint;
        bool hasHomePoint;
        bool hasLanded;

        // Perch.
        Transform perch;
        Vector3 perchLanding;
        bool perchBusy;
        bool onPerch;
        bool perchJumpRequested;
        bool perchBackRequested;
        bool perchHopRequested;
        float perchSince;

        // Barrel.
        CastleBarrel barrel;
        bool barrelHidden;
        Bounds playArea;
        bool hasPlayArea;
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        // Thief.
        bool isThief;
        MissionLoot loot;
        Transform[] runPoints;
        string thiefGrabLine;
        Transform lootHold;

        // Shield and dizzy.
        bool swingBlocked;
        Hittable watchedTarget;
        Action<Hittable> onTargetBlocked;

        // Help a friend.
        Hittable forcedTarget;

        /// <summary>True when this one is defeated or switched off, so it can be used again.</summary>
        public bool IsOut => !gameObject.activeInHierarchy || (hittable != null && hittable.IsDefeated);

        /// <summary>Which sort of goblin this is.</summary>
        public GoblinKind Kind => kind;

        /// <summary>True from the moment it has landed in the courtyard (after the ladder or the jump from the perch) until it is switched off.</summary>
        public bool HasLanded => hasLanded;

        /// <summary>True while it stands on a wall perch waiting to be counted (not while it hops up, and not any more once it jumps down).</summary>
        public bool IsOnPerch => onPerch;

        /// <summary>True for the goblin that stole the mission's treasure, until it is switched off.</summary>
        public bool IsThief => isThief;

        /// <summary>Raised when a bump on this shield goblin was blocked by its shield.</summary>
        public event Action<BattleOpponentAI> ShieldBlocked;

        /// <summary>Starts a new castle visit: the once-per-visit voice lines (dizzy, troll, barrel...) may be said again.</summary>
        public static void BeginVisit()
        {
            saidThisVisit.Clear();
        }

        static void SayOnce(string group)
        {
            if (saidThisVisit.Add(group)) VoicePlayer.Say(group);
        }

        void Awake() => CacheReferences();

        /// <summary>Opponents start switched off, so Activate may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            fighter = GetComponent<SwordFighter>();
            kidAnimator = GetComponent<KidAnimator>();
            ladderClimb = GetComponent<LadderClimb>();
            body = GetComponent<Collider>();
            weapons = GetComponent<WeaponHolder>();
            currentSpeed = moveSpeed;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visualRoot = animator.transform;
                visualBaseScale = visualRoot.localScale;
            }
            else
            {
                // The model sits on the root itself: the whole goblin is scaled then.
                visualRoot = transform;
                visualBaseScale = transform.localScale;
            }
        }

        void OnEnable()
        {
            hittable.Hit += OnHit;
            hittable.Blocked += OnBlocked;
            hittable.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            if (hittable != null)
            {
                hittable.Hit -= OnHit;
                hittable.Blocked -= OnBlocked;
                hittable.Defeated -= OnDefeated;
            }

            routine = null;
            SetTelegraph(false);
            ClearStates();
        }

        void Update()
        {
            if (!Mathf.Approximately(sizeFactor, sizeTarget))
            {
                float step = BigScale * ShrinkPerHit / ShrinkSeconds * Time.deltaTime;
                sizeFactor = Mathf.MoveTowards(sizeFactor, sizeTarget, step);
                ApplyScale();
            }

            if (dizzyStars != null && dizzyStars.activeSelf)
                dizzyStars.transform.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
        }

        // ---- Starting and stopping ----

        /// <summary>
        /// Starts at the foot of the ladder outside the wall, climbs over and jumps down into the courtyard, then starts
        /// looking for a knight to challenge. Without a ladder (or a LadderClimb) it just appears at the fallback position.
        /// </summary>
        public void Activate(WallLadder wallLadder, Vector3 fallbackPosition)
        {
            CacheReferences();
            bool climbs = wallLadder != null && ladderClimb != null;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            hittable.ResetState();
            hitPauseUntil = 0f;
            SetTelegraph(false);
            ClearStates();
            StartKind();
            ladder = wallLadder;

            if (climbs)
            {
                // The climber switches the agent off and stands at the ladder's foot, so the agent never wakes up off the NavMesh.
                ladderClimb.Begin(ladder);
                gameObject.SetActive(true);
            }
            else
            {
                // Place it on the floor before switching on, so the agent starts on the NavMesh.
                if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    fallbackPosition = hit.position;

                if (AgentReady) agent.Warp(fallbackPosition);
                else transform.position = fallbackPosition;
                gameObject.SetActive(true);
                if (agent.enabled && !agent.isOnNavMesh) agent.Warp(fallbackPosition);
                hasLanded = true;
            }

            // The knights game could not show our weapon while we were switched off, so do it now.
            ShowWeapons();

            if (!climbs) PrepareAgent();
            routine = StartCoroutine(climbs ? ClimbThenFight() : FightRoutine());
        }

        /// <summary>
        /// Counting: appears 1.2 m below the perch, behind the wall, hops up onto the perch (0.4 s) with a sparkle, faces south
        /// and waves, bobbing, until JumpDown (or HopBackDown) is called. After 60 s without anybody calling it jumps by itself.
        /// While it stands there it cannot be hit.
        /// </summary>
        public void ActivateOnPerch(Transform perchPoint, Vector3 landing)
        {
            CacheReferences();
            if (routine != null) StopCoroutine(routine);
            routine = null;

            hittable.ResetState();
            hitPauseUntil = 0f;
            SetTelegraph(false);
            ClearStates();
            StartKind();

            ladder = null;
            perch = perchPoint;
            perchLanding = landing;
            homePoint = landing;
            hasHomePoint = true;
            perchBusy = true;
            perchSince = Time.time;

            // The agent and the hitting stay off while it is on the wall (like on the ladder).
            agent.enabled = false;
            hittable.enabled = false;
            if (body != null) body.enabled = false;

            Vector3 flatForward = PerchForward();
            transform.SetPositionAndRotation(
                perchPoint.position - flatForward * PerchBehindMeters + Vector3.down * PerchBelowMeters,
                Quaternion.LookRotation(flatForward, Vector3.up));
            gameObject.SetActive(true);
            ShowWeapons();

            routine = StartCoroutine(PerchRoutine());
        }

        /// <summary>Tells a goblin on the perch to jump down into the courtyard now (0.6 s), then fight.</summary>
        public void JumpDown()
        {
            if (perchBusy) perchJumpRequested = true;
        }

        /// <summary>One less: hops back down behind the wall, with a sparkle, and is gone.</summary>
        public void HopBackDown()
        {
            if (perchBusy) perchBackRequested = true;
        }

        /// <summary>A small hop on the spot, for counting along. Also keeps it from jumping down by itself for a while.</summary>
        public void PerchHop()
        {
            if (!onPerch) return;

            perchHopRequested = true;
            perchSince = Time.time;
        }

        /// <summary>
        /// The tiny goblin: sits hidden inside the barrel (no renderers, no agent, cannot be hit). The barrel wobbles now and then
        /// and the goblin peeks out. When the player comes near (or after 20 s) the barrel tips over, the goblin hops out and runs
        /// around the courtyard (inside "area" when given). It never attacks.
        /// </summary>
        public void HideInBarrel(CastleBarrel hidingPlace, Bounds? area = null)
        {
            CacheReferences();
            if (hidingPlace == null) return;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            hittable.ResetState();
            hitPauseUntil = 0f;
            SetTelegraph(false);
            ClearStates();
            StartKind();

            ladder = null;
            barrel = hidingPlace;
            barrelHidden = true;
            homePoint = hidingPlace.HopOutPoint;
            hasHomePoint = true;
            hasPlayArea = area.HasValue;
            if (area.HasValue) playArea = area.Value;

            agent.enabled = false;
            hittable.enabled = false;
            if (body != null) body.enabled = false;

            Vector3 flat = hidingPlace.HopOutPoint - hidingPlace.Inside.position;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat, Vector3.up) : hidingPlace.transform.rotation;
            transform.SetPositionAndRotation(hidingPlace.Inside.position, facing);
            gameObject.SetActive(true);
            ShowWeapons();
            SetHidden(true);

            routine = StartCoroutine(BarrelRoutine());
        }

        /// <summary>
        /// Makes this goblin the thief. Call right after Activate. After it lands it runs to the loot, grabs it, says the
        /// grab line, and runs from point to point around the courtyard until it is bumped (it needs one hit) or trips by itself.
        /// </summary>
        public void BecomeThief(MissionLoot lootToSteal, Transform[] pointsToRunTo, string grabLine = null)
        {
            CacheReferences();
            if (lootToSteal == null) return;

            isThief = true;
            loot = lootToSteal;
            runPoints = pointsToRunTo;
            thiefGrabLine = grabLine;
            hittable.SetHitsToDefeat(1);
        }

        /// <summary>While set, this goblin always goes for this target (a knight who called for help), as long as it is alive.</summary>
        public void ForceTarget(Hittable target)
        {
            forcedTarget = target;
        }

        public void ClearForcedTarget()
        {
            forcedTarget = null;
        }

        /// <summary>Sends a goblin that is still on the field home (a round that dragged on): it runs to its ladder and vanishes in a sparkle.</summary>
        public void SendHome()
        {
            if (!gameObject.activeInHierarchy || hittable.IsDefeated) return;

            if (routine != null) StopCoroutine(routine);
            routine = null;

            // The thief gives the treasure back before it goes.
            if (isThief && loot != null && !loot.IsHome) loot.ReturnHome();
            ClearBehaviour();

            // Still on the ladder, on the wall, in the barrel or in the air: just vanish where it is.
            if ((ladderClimb != null && ladderClimb.IsClimbing) || perchBusy || barrelHidden)
            {
                if (!barrelHidden) BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);
                gameObject.SetActive(false);
                return;
            }

            routine = StartCoroutine(GoHomeThenVanish());
        }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        void PrepareAgent()
        {
            if (!AgentReady) return;

            agent.speed = currentSpeed;
            agent.isStopped = false;
            agent.updateRotation = true;
        }

        void ShowWeapons()
        {
            if (weapons != null) weapons.ShowWeapons(true);
        }

        /// <summary>What this kind starts with: the troll is big and slow, the others are as built.</summary>
        void StartKind()
        {
            currentSpeed = kind == GoblinKind.Big ? BigSpeed : moveSpeed;
            sizeFactor = sizeTarget = kind == GoblinKind.Big ? BigScale : 1f;
            ApplyScale();
        }

        /// <summary>Everything the castle adventure adds, back to normal: used by Activate, OnDisable and the other starts.</summary>
        void ClearStates()
        {
            ClearBehaviour();

            perchBusy = false;
            onPerch = false;
            perchJumpRequested = false;
            perchBackRequested = false;
            perchHopRequested = false;
            perch = null;
            barrelHidden = false;
            barrel = null;
            hasPlayArea = false;
            hasLanded = false;
            hasHomePoint = false;
            ladder = null;

            // The thief must never leave the treasure hanging on a goblin that is switched off.
            if (loot != null && !loot.IsHome) loot.ResetLoot();
            isThief = false;
            loot = null;
            runPoints = null;
            thiefGrabLine = null;

            SetHidden(false);
            RestorePhysics();

            sizeFactor = sizeTarget = 1f;
            crouchAmount = 0f;
            currentSpeed = moveSpeed;
            ApplyScale();
        }

        /// <summary>Shield, dizzy, forced target and watching: the things that must not outlive a fight.</summary>
        void ClearBehaviour()
        {
            StopWatchingTarget();
            swingBlocked = false;
            forcedTarget = null;
            SetDizzyStars(false);
            if (kind == GoblinKind.Shield && fighter != null) fighter.SetBlocking(false);
        }

        /// <summary>The agent, the Hittable and the collider back on (the perch and the barrel switch them off).</summary>
        void RestorePhysics()
        {
            if (agent != null) agent.enabled = true;
            if (hittable != null) hittable.enabled = true;
            if (body != null) body.enabled = true;
        }

        // ---- Fighting ----

        IEnumerator ClimbThenFight()
        {
            yield return ladderClimb.Run(ladder);

            hasLanded = true;
            if (kind == GoblinKind.Big) SayOnce("castle.troll");
            PrepareAgent();
            yield return FightRoutine();
        }

        IEnumerator FightRoutine()
        {
            // A moment to look around before the first challenge.
            yield return new WaitForSeconds(0.5f);

            if (isThief && loot != null)
            {
                yield return ThiefRoutine();
                yield break;
            }

            while (true)
            {
                Hittable target = PickTarget();
                if (target == null)
                {
                    StopMoving();
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                if (kind == GoblinKind.Shield) fighter.SetBlocking(true);
                yield return Approach(target);

                if (!target.IsAlive || Hittable.FlatDistance(transform.position, target.transform.position) > AttackDistance + 1f)
                {
                    yield return null;
                    continue;
                }

                yield return WindUpAndSwing(target);
                if (swingBlocked) yield return Dizzy();
                else yield return Recover(target);
            }
        }

        IEnumerator Approach(Hittable target)
        {
            float giveUpTime = Time.time + GiveUpApproachSeconds;
            while (target.IsAlive && Time.time < giveUpTime)
            {
                if (Hittable.FlatDistance(transform.position, target.transform.position) <= AttackDistance) break;

                if (Time.time < hitPauseUntil)
                {
                    StopMoving();
                }
                else if (AgentReady)
                {
                    agent.updateRotation = true;
                    agent.speed = currentSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(target.transform.position);
                }
                yield return null;
            }
            StopMoving();
        }

        IEnumerator WindUpAndSwing(Hittable target)
        {
            StopMoving();
            if (AgentReady) agent.updateRotation = false;
            SetTelegraph(true);

            bool interrupted = false;
            float elapsed = 0f;
            while (elapsed < windUpSeconds)
            {
                if (Time.time < hitPauseUntil || !target.IsAlive)
                {
                    interrupted = true;
                    break;
                }

                FaceTarget(target);
                float pulse = Mathf.Sin(elapsed / windUpSeconds * Mathf.PI);
                SetCrouch(pulse);

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetTelegraph(false);
            RestoreScale();

            if (!interrupted)
            {
                FaceTarget(target);
                swingBlocked = false;
                if (kind == GoblinKind.Shield) WatchTarget(target);

                // The shield pose makes way for the swing, but the shield still blocks until the swing is over.
                if (kind == GoblinKind.Shield && kidAnimator != null) kidAnimator.SetBlocking(false);
                fighter.Attack();
                // Let the swing play out before stepping back.
                yield return new WaitForSeconds(0.6f);
                StopWatchingTarget();
            }
        }

        IEnumerator Recover(Hittable target)
        {
            // The shield is down while it rests: that is the moment to bump it.
            if (kind == GoblinKind.Shield) fighter.SetBlocking(false);

            float elapsed = 0f;
            bool steppedBack = false;
            while (elapsed < recoverSeconds)
            {
                // Step back a little (facing the target), then stand and rest.
                if (!steppedBack && elapsed >= 0.2f && Time.time >= hitPauseUntil && AgentReady)
                {
                    steppedBack = true;
                    Vector3 away = -Hittable.FlatDirection(transform.position, target.transform.position);
                    Vector3 spot = transform.position + away * 1f;
                    if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    {
                        agent.updateRotation = false;
                        agent.speed = currentSpeed;
                        agent.isStopped = false;
                        agent.SetDestination(hit.position);
                    }
                }

                if (target.IsAlive) FaceTarget(target);
                elapsed += Time.deltaTime;
                yield return null;
            }

            StopMoving();
            if (AgentReady) agent.updateRotation = true;
        }

        /// <summary>The swing hit a raised shield: the goblin sees stars for three seconds, with its own shield down.</summary>
        IEnumerator Dizzy()
        {
            swingBlocked = false;
            fighter.SetBlocking(false);
            StopMoving();
            if (AgentReady) agent.updateRotation = true;

            SetDizzyStars(true);
            Sfx.Play("castle.dizzy_stars", transform.position);
            SayOnce("castle.dizzy");
            if (kidAnimator != null) kidAnimator.PlayBlockHit();

            float end = Time.time + DizzySeconds;
            while (Time.time < end)
                yield return null;

            SetDizzyStars(false);
        }

        IEnumerator DefeatedRoutine()
        {
            StopMoving();
            SetTelegraph(false);
            RestoreScale();
            SetDizzyStars(false);

            if (kidAnimator != null) kidAnimator.PlayTumble();
            yield return new WaitForSeconds(1.5f);
            if (kidAnimator != null) kidAnimator.PlayLaugh();
            Sfx.Play("battle.giggle", transform.position);
            yield return new WaitForSeconds(0.8f);

            yield return GoHomeThenVanish();
        }

        /// <summary>Runs to the ladder's landing spot (away from the wall, never into it) and vanishes in a sparkle.</summary>
        IEnumerator GoHomeThenVanish()
        {
            StopMoving();
            SetTelegraph(false);
            RestoreScale();

            bool hasDestination = ladder != null || hasHomePoint;
            if (hasDestination && AgentReady)
            {
                agent.updateRotation = true;
                agent.speed = DefeatedRunSpeed;
                agent.isStopped = false;
                agent.SetDestination(ladder != null ? ladder.Landing : homePoint);

                float timeout = Time.time + GoHomeTimeoutSeconds;
                while (Time.time < timeout && (agent.pathPending || agent.remainingDistance > GoHomeArriveDistance))
                    yield return null;
            }

            BattleEffects.Sparkle(transform.position + Vector3.up * 0.8f);
            routine = null;
            gameObject.SetActive(false);
        }

        void OnHit(Hittable who)
        {
            // The troll shrinks with every real bump, so it ends small when it tumbles.
            if (kind == GoblinKind.Big)
            {
                sizeTarget = Mathf.Max(MinTrollScale, sizeTarget - BigScale * ShrinkPerHit);
                Sfx.Play("castle.troll_shrink", transform.position);
            }

            if (who.IsDefeated) return;

            hitPauseUntil = Time.time + HitPauseSeconds;
            if (kidAnimator != null) kidAnimator.PlayHit();
        }

        void OnBlocked(Hittable who)
        {
            if (kidAnimator != null) kidAnimator.PlayBlockHit();
            if (kind != GoblinKind.Shield) return;

            ShieldBlocked?.Invoke(this);

            // The first time the player's own sword bounces off a shield, the teacher explains it.
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player != null && Hittable.FlatDistance(player.transform.position, transform.position) < 2.6f)
                SayOnce("castle.shield_goblin");
        }

        void OnDefeated(Hittable who)
        {
            if (routine != null) StopCoroutine(routine);
            StopWatchingTarget();
            forcedTarget = null;

            // The thief drops the treasure, and it flies back where it belongs.
            if (isThief && loot != null && !loot.IsHome)
            {
                loot.ReturnHome();
                VoicePlayer.Say("castle.loot_back");
            }

            routine = StartCoroutine(DefeatedRoutine());
        }

        Hittable PickTarget()
        {
            if (forcedTarget != null && forcedTarget.IsAlive) return forcedTarget;

            Hittable playerHittable = FindPlayerHittable();
            if (playerHittable != null && playerHittable.IsAlive && Random.value < preferPlayerChance)
                return playerHittable;

            Hittable nearest = null;
            float best = float.MaxValue;
            foreach (Hittable other in Hittable.All)
            {
                if (other.Team != BattleTeam.Heroes || !other.IsAlive) continue;

                float distance = Hittable.FlatDistance(transform.position, other.transform.position);
                if (distance < best)
                {
                    best = distance;
                    nearest = other;
                }
            }
            return nearest;
        }

        Hittable FindPlayerHittable()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            return player != null ? player.GetComponent<Hittable>() : null;
        }

        void FaceTarget(Hittable target)
        {
            Vector3 direction = Hittable.FlatDirection(transform.position, target.transform.position);
            if (direction == Vector3.zero) return;

            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 360f * Time.deltaTime);
        }

        void StopMoving()
        {
            if (!AgentReady) return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        void SetTelegraph(bool on)
        {
            if (telegraph != null && telegraph.activeSelf != on) telegraph.SetActive(on);
        }

        void SetDizzyStars(bool on)
        {
            if (dizzyStars != null && dizzyStars.activeSelf != on) dizzyStars.SetActive(on);
        }

        /// <summary>Watches whether the target's shield stops the swing that is about to land (then this goblin gets dizzy).</summary>
        void WatchTarget(Hittable target)
        {
            StopWatchingTarget();
            if (target == null) return;

            watchedTarget = target;
            if (onTargetBlocked == null) onTargetBlocked = _ => swingBlocked = true;
            watchedTarget.Blocked += onTargetBlocked;
        }

        void StopWatchingTarget()
        {
            if (watchedTarget != null && onTargetBlocked != null) watchedTarget.Blocked -= onTargetBlocked;
            watchedTarget = null;
        }

        /// <summary>Squashes the model a little, as if getting ready to swing. 0 = normal, 1 = most crouched.</summary>
        void SetCrouch(float amount)
        {
            crouchAmount = amount;
            ApplyScale();
        }

        /// <summary>Ends the wind-up squash. The troll keeps its (shrinking) size.</summary>
        void RestoreScale()
        {
            crouchAmount = 0f;
            ApplyScale();
        }

        void ApplyScale()
        {
            if (visualRoot == null) return;

            visualRoot.localScale = new Vector3(
                visualBaseScale.x * sizeFactor * (1f + 0.06f * crouchAmount),
                visualBaseScale.y * sizeFactor * (1f - 0.12f * crouchAmount),
                visualBaseScale.z * sizeFactor * (1f + 0.06f * crouchAmount));
        }

        // ---- On the wall perch ----

        Vector3 PerchForward()
        {
            Vector3 forward = perch != null ? perch.forward : Vector3.back;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.back : forward.normalized;
        }

        IEnumerator PerchRoutine()
        {
            Vector3 start = transform.position;
            Vector3 top = perch.position;
            Quaternion facing = Quaternion.LookRotation(PerchForward(), Vector3.up);

            // Pops up over the wall.
            Sfx.Play("battle.goblin_appear", top);
            BattleEffects.Sparkle(top + Vector3.up * 0.6f);
            for (float elapsed = 0f; elapsed < PerchHopSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / PerchHopSeconds;
                transform.position = Vector3.Lerp(start, top, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * PerchHopHeight);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 720f * Time.deltaTime);
                yield return null;
            }
            transform.position = top;
            transform.rotation = facing;

            // Stands there and waves.
            onPerch = true;
            perchSince = Mathf.Max(perchSince, Time.time);
            float nextCheer = Time.time + 0.5f;
            float bobOffset = Random.value * 6f;
            while (!perchJumpRequested && !perchBackRequested && Time.time - perchSince < PerchSafetySeconds)
            {
                if (perchHopRequested)
                {
                    perchHopRequested = false;
                    yield return PerchSmallHop(top);
                }

                transform.position = top + Vector3.up * (Mathf.Sin(Time.time * 4f + bobOffset) * 0.03f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 360f * Time.deltaTime);
                if (Time.time >= nextCheer)
                {
                    nextCheer = Time.time + PerchCheerSeconds;
                    if (kidAnimator != null) kidAnimator.PlayCheer();
                }
                yield return null;
            }
            onPerch = false;

            if (perchBackRequested)
            {
                yield return HopBackBehindWall(top, start);
                yield break;
            }

            yield return JumpDownRoutine(top);
        }

        IEnumerator PerchSmallHop(Vector3 top)
        {
            const float seconds = 0.3f;
            if (kidAnimator != null) kidAnimator.PlayJump();
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / seconds;
                transform.position = top + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.3f);
                yield return null;
            }
            transform.position = top;
        }

        IEnumerator HopBackBehindWall(Vector3 top, Vector3 behind)
        {
            for (float elapsed = 0f; elapsed < PerchHopSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / PerchHopSeconds;
                transform.position = Vector3.Lerp(top, behind, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * PerchHopHeight);
                yield return null;
            }

            BattleEffects.Sparkle(top + Vector3.up * 0.6f);
            routine = null;
            perchBusy = false;
            gameObject.SetActive(false);
        }

        /// <summary>The hop from the perch down into the courtyard, then the normal fight.</summary>
        IEnumerator JumpDownRoutine(Vector3 top)
        {
            Vector3 landing = perchLanding;
            Vector3 flat = landing - top;
            flat.y = 0f;
            Quaternion landingRotation = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : transform.rotation;

            if (kidAnimator != null) kidAnimator.PlayJump();
            for (float elapsed = 0f; elapsed < JumpDownSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / JumpDownSeconds;
                Vector3 position = Vector3.Lerp(top, landing, t);
                position.y = Mathf.Lerp(top.y, landing.y, t * t) + 4f * JumpDownHeight * t * (1f - t);
                transform.position = position;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, landingRotation, 720f * Time.deltaTime);
                yield return null;
            }

            transform.position = landing;
            transform.rotation = landingRotation;
            BattleEffects.Sparkle(landing + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.3f);

            // Back on the NavMesh and hittable: the normal fighting takes over.
            perchBusy = false;
            RestorePhysics();
            if (agent != null && agent.isActiveAndEnabled)
            {
                if (!agent.Warp(landing) && NavMesh.SamplePosition(landing, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }

            hasLanded = true;
            if (kind == GoblinKind.Big) SayOnce("castle.troll");
            PrepareAgent();
            yield return FightRoutine();
        }

        // ---- In the barrel ----

        IEnumerator BarrelRoutine()
        {
            float startTime = Time.time;
            float nextWobble = Time.time + Random.Range(1f, 1.6f);
            int wobbles = 0;

            while (true)
            {
                if (Time.time - startTime >= BarrelTipAfterSeconds) break;
                if (PlayerCloseTo(barrel.transform.position, BarrelTipDistance)) break;

                if (Time.time >= nextWobble)
                {
                    wobbles++;
                    barrel.Wobble();
                    if (wobbles == 1) SayOnce("castle.barrel");
                    nextWobble = Time.time + Random.Range(2f, 3f);

                    // Every other wobble the goblin peeks out and giggles.
                    if (wobbles % 2 == 0) yield return Peek();
                }
                yield return null;
            }

            // Found: the barrel tips over and the goblin hops out onto the floor beside it.
            barrel.TipOver();
            SetHidden(false);
            yield return new WaitForSeconds(0.35f);

            Vector3 outPoint = barrel.HopOutPoint;
            if (NavMesh.SamplePosition(outPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas)) outPoint = hit.position;
            Vector3 from = transform.position;
            Sfx.Play("battle.giggle", transform.position);
            SayOnce("castle.barrel_out");
            if (kidAnimator != null) kidAnimator.PlayJump();
            for (float elapsed = 0f; elapsed < PerchHopSeconds; elapsed += Time.deltaTime)
            {
                float t = elapsed / PerchHopSeconds;
                transform.position = Vector3.Lerp(from, outPoint, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.6f);
                yield return null;
            }
            transform.position = outPoint;

            barrelHidden = false;
            RestorePhysics();
            if (agent != null && agent.isActiveAndEnabled) agent.Warp(outPoint);
            hasLanded = true;
            currentSpeed = TinySpeed;
            PrepareAgent();
            yield return RunAroundRoutine();
        }

        /// <summary>The goblin's head pops up out of the barrel for a moment.</summary>
        IEnumerator Peek()
        {
            Vector3 basePosition = barrel.Inside.position;
            SetHidden(false);
            Sfx.Play("battle.giggle", transform.position);

            float up = PeekSeconds * 0.3f;
            for (float elapsed = 0f; elapsed < up; elapsed += Time.deltaTime)
            {
                transform.position = basePosition + Vector3.up * (PeekHeight * (elapsed / up));
                yield return null;
            }
            transform.position = basePosition + Vector3.up * PeekHeight;
            yield return new WaitForSeconds(PeekSeconds * 0.4f);

            float down = PeekSeconds * 0.3f;
            for (float elapsed = 0f; elapsed < down; elapsed += Time.deltaTime)
            {
                transform.position = basePosition + Vector3.up * (PeekHeight * (1f - elapsed / down));
                yield return null;
            }
            transform.position = basePosition;
            SetHidden(true);
        }

        /// <summary>Runs from spot to spot in the courtyard, away from the player. Never attacks. Goes home after a while.</summary>
        IEnumerator RunAroundRoutine()
        {
            float end = Time.time + TinyRunSeconds;
            while (Time.time < end)
            {
                if (Time.time < hitPauseUntil)
                {
                    StopMoving();
                    yield return null;
                    continue;
                }

                if (AgentReady && TryPickRunSpot(out Vector3 spot))
                {
                    agent.updateRotation = true;
                    agent.speed = TinySpeed;
                    agent.isStopped = false;
                    agent.SetDestination(spot);

                    float giveUp = Time.time + 4f;
                    yield return null;
                    while (Time.time < giveUp && Time.time < end && (agent.pathPending || agent.remainingDistance > 0.6f))
                        yield return null;
                }
                else
                {
                    yield return new WaitForSeconds(0.3f);
                }
            }

            yield return GoHomeThenVanish();
        }

        /// <summary>A random spot 3 to 6 m away that is on the NavMesh (and in the play area), preferably far from the player.</summary>
        bool TryPickRunSpot(out Vector3 spot)
        {
            spot = transform.position;
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            Vector3 playerPosition = player != null ? player.transform.position : transform.position + transform.forward * 10f;

            float bestScore = float.MinValue;
            bool found = false;
            for (int i = 0; i < RunCandidates; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(3f, 6f);
                Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (hasPlayArea)
                {
                    candidate.x = Mathf.Clamp(candidate.x, playArea.min.x, playArea.max.x);
                    candidate.z = Mathf.Clamp(candidate.z, playArea.min.z, playArea.max.z);
                }
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;

                float score = Hittable.FlatDistance(hit.position, playerPosition) + Random.value * 1.5f;
                if (Hittable.FlatDistance(hit.position, transform.position) < 1.5f) score -= 5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    spot = hit.position;
                    found = true;
                }
            }
            return found;
        }

        bool PlayerCloseTo(Vector3 point, float meters)
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            return player != null && Hittable.FlatDistance(player.transform.position, point) <= meters;
        }

        /// <summary>Hides or shows every mesh of the goblin (it sits inside the barrel). Only meshes that were on come back on.</summary>
        void SetHidden(bool hidden)
        {
            if (hidden)
            {
                if (hiddenRenderers.Count > 0) return;

                foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
                {
                    if (part == null || !part.enabled) continue;

                    part.enabled = false;
                    hiddenRenderers.Add(part);
                }
            }
            else
            {
                foreach (Renderer part in hiddenRenderers)
                    if (part != null) part.enabled = true;
                hiddenRenderers.Clear();
            }
        }

        // ---- The thief ----

        IEnumerator ThiefRoutine()
        {
            // Runs to the treasure.
            float reachBy = Time.time + ThiefReachLootSeconds;
            while (Time.time < reachBy && loot != null && loot.IsHome)
            {
                Vector3 lootPosition = loot.transform.position;
                if (Hittable.FlatDistance(transform.position, lootPosition) <= 1.2f) break;

                if (Time.time < hitPauseUntil)
                {
                    StopMoving();
                }
                else if (AgentReady)
                {
                    if (NavMesh.SamplePosition(lootPosition, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) lootPosition = hit.position;
                    agent.updateRotation = true;
                    agent.speed = ThiefGrabSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(lootPosition);
                }
                yield return null;
            }

            StopMoving();
            if (loot != null) loot.Grab(FindHand());
            if (kidAnimator != null) kidAnimator.PlayCheer();
            Sfx.Play("battle.giggle", transform.position);
            if (!string.IsNullOrEmpty(thiefGrabLine)) VoicePlayer.Say(thiefGrabLine);
            VoicePlayer.Say("castle.thief_chase");
            yield return new WaitForSeconds(0.8f);

            // Runs round and round; the player is faster, so he catches it.
            float tripAt = Time.time + ThiefTripAfterSeconds;
            Vector3 lastSpot = transform.position;
            while (Time.time < tripAt)
            {
                if (Time.time < hitPauseUntil || !AgentReady)
                {
                    StopMoving();
                    yield return null;
                    continue;
                }

                Vector3 spot = PickThiefSpot(lastSpot);
                lastSpot = spot;
                agent.updateRotation = true;
                agent.speed = ThiefRunSpeed;
                agent.isStopped = false;
                agent.SetDestination(spot);

                float giveUp = Time.time + 6f;
                yield return null;
                while (Time.time < giveUp && Time.time < tripAt && (agent.pathPending || agent.remainingDistance > 0.8f))
                    yield return null;
            }

            // It trips over its own feet: the same as being bumped down.
            for (int attempt = 0; attempt < 60 && !hittable.IsDefeated; attempt++)
            {
                if (hittable.TakeHit(transform.position - transform.forward, 1, true)) break;
                yield return null;
            }
        }

        /// <summary>The next run point: far from the player and from where it just was, a bit random.</summary>
        Vector3 PickThiefSpot(Vector3 avoid)
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            Vector3 playerPosition = player != null ? player.transform.position : transform.position;

            Vector3 best = transform.position;
            float bestScore = float.MinValue;
            bool any = false;

            if (runPoints != null)
            {
                foreach (Transform point in runPoints)
                {
                    if (point == null) continue;

                    Vector3 candidate = point.position;
                    if (Hittable.FlatDistance(candidate, transform.position) < 2.5f) continue;
                    if (Hittable.FlatDistance(candidate, avoid) < 0.5f) continue;

                    float score = Hittable.FlatDistance(candidate, playerPosition) + Random.value * 4f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        any = true;
                    }
                }
            }

            if (!any) TryPickRunSpot(out best);
            if (NavMesh.SamplePosition(best, out NavMeshHit hit, 2f, NavMesh.AllAreas)) best = hit.position;
            return best;
        }

        /// <summary>The goblin's right hand, or a spot about a metre above its feet when the model has no hand bone.</summary>
        Transform FindHand()
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) return hand;
            }

            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
            {
                string lowered = candidate.name.ToLowerInvariant();
                if (!lowered.Contains("hand")) continue;
                if (lowered.Contains("right") || lowered.EndsWith("_r") || lowered.EndsWith(".r") || lowered.EndsWith(" r") || lowered.Contains("hand_r"))
                    return candidate;
            }

            if (lootHold == null)
            {
                lootHold = new GameObject("LootHold").transform;
                lootHold.SetParent(transform, false);
                lootHold.localPosition = new Vector3(0f, 1f, 0.3f);
            }
            return lootHold;
        }
    }
}
