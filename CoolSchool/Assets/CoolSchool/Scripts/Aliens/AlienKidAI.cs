using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// A silly alien kid. First it walks out toward the player and keeps its distance, throwing slime when the alien
    /// landing asks it to. One zap turns it into a friend: it cheers, runs to its spot in the party line and dances
    /// there. Friends never throw again.
    /// There are a few kinds (see AlienKind): big ones need three zaps and shrink, tiny ones run off and get tired,
    /// shy ones hide behind a cover and peek out, robots dance the robot dance.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Hittable))]
    public class AlienKidAI : MonoBehaviour, IZapTarget
    {
        [Tooltip("A robot model. The alien landing always makes it a Robot; the other kids can be any other kind.")]
        [SerializeField] bool isRobot;
        [SerializeField] float walkSpeed = 1.4f;
        [SerializeField] float friendSpeed = 2.4f;
        [Tooltip("The alien stops walking this far from the player and waits there.")]
        [SerializeField] float keepDistance = 5f;
        [Tooltip("The alien stands still for this long when it throws.")]
        [SerializeField] float throwStandSeconds = 1.6f;
        [SerializeField] Color friendConfettiColor = new Color(0.35f, 0.9f, 0.25f, 1f);

        const float RepathSeconds = 0.4f;
        const float ArriveDistance = 0.25f;
        const float GiveUpWalkSeconds = 10f;
        const float HopSeconds = 0.9f;
        const float HopHeight = 0.4f;

        // Big: slow, needs three zaps, shrinks a step each time.
        const float BigWalkSpeed = 1f;
        const int BigHits = 3;
        static readonly float[] BigScales = { 1.45f, 1.3f, 1.15f };

        // Tiny: runs away from the player, then rests (the easy moment to zap it).
        const float TinyScale = 0.7f;
        const float TinyRunSpeed = 3.6f;
        const float TinyAlertDistance = 6f;
        const float TinyFleeDistance = 10f;
        const float TinyRestSeconds = 3.5f;
        const float TinyRunGiveUpSeconds = 6f;

        // Shy: hides behind a cover, peeks out now and then, and comes out for good after a few peeks.
        const float ShyWalkSpeed = 2.2f;
        const float ShyGoToCoverSeconds = 14f;
        const float ShyHiddenMinSeconds = 3f;
        const float ShyHiddenMaxSeconds = 5f;
        const float ShyPeekSeconds = 2f;
        const float ShyPlayerNearDistance = 3f;
        const float ShyStayOutDistance = 4f;
        const float ShyStayOutMaxSeconds = 20f;
        const int ShyMaxPeeks = 3;

        // "Slight" tints: multiplied into the material's own colour.
        static readonly Color BigTint = new Color(0.85f, 0.7f, 1f, 1f);
        static readonly Color TinyTint = new Color(1f, 0.95f, 0.55f, 1f);
        static readonly Color ShyTint = new Color(0.7f, 0.85f, 1f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock tintBlock;

        // Kind lines (tease, tired, peek, shrink giggle) are spaced out so they never pile up in the voice queue.
        static float nextKindVoiceTime;
        const float KindVoiceGapSeconds = 3f;

        NavMeshAgent agent;
        Hittable hittable;
        KidAnimator kidAnimator;
        SlimeThrower thrower;
        Transform visual;
        Vector3 visualBasePosition;
        Vector3 baseScale = Vector3.one;
        Renderer[] bodyRenderers;
        bool tinted;
        Transform player;
        Coroutine throwRoutine;
        Coroutine friendRoutine;
        Coroutine hopRoutine;
        Coroutine kindRoutine;
        Coroutine scaleRoutine;
        float nextRepathTime;

        Vector3 partySpot;
        Vector3 partyLookAt;
        int danceVariant;
        bool hasPartySpot;
        bool dancing;

        Bounds fieldBounds;
        AlienCover cover;
        bool moveArrived;

        public Hittable Hittable
        {
            get
            {
                CacheReferences();
                return hittable;
            }
        }

        public bool IsFriend { get; private set; }

        public AlienKind Kind { get; private set; }

        /// <summary>True for the robot models (serialized isRobot). They are always robots; the other kids never are.</summary>
        public bool IsRobotModel => isRobot;

        /// <summary>True while a shy alien is behind its cover, out of sight. It cannot be zapped then.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>True while it is winding up or lobbing slime.</summary>
        public bool IsThrowing => throwRoutine != null;

        /// <summary>True once it is standing at its spot in the party line, dancing.</summary>
        public bool IsDancing => dancing;

        /// <summary>The ray gun may aim at it: not a friend, not hiding, and switched on.</summary>
        public bool CanBeZapped => isActiveAndEnabled && !IsFriend && !IsHidden && Hittable.IsAlive;

        /// <summary>Tiny and shy aliens never throw slime. The others may when they are standing about.</summary>
        public bool CanThrowSlime =>
            isActiveAndEnabled && !IsFriend && !IsHidden && !IsThrowing && kindRoutine == null
            && (Kind == AlienKind.Normal || Kind == AlienKind.Big || Kind == AlienKind.Robot);

        /// <summary>Raised when a zap (or the safety net) has turned this alien into a friend.</summary>
        public event Action<AlienKidAI> BecameFriend;

        void Awake() => CacheReferences();

        /// <summary>Aliens start switched off, so Activate may run before Awake. This is safe to call twice.</summary>
        void CacheReferences()
        {
            if (agent != null) return;

            agent = GetComponent<NavMeshAgent>();
            hittable = GetComponent<Hittable>();
            kidAnimator = GetComponent<KidAnimator>();
            thrower = GetComponent<SlimeThrower>();
            baseScale = transform.localScale;

            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.transform != transform)
            {
                visual = animator.transform;
                visualBasePosition = visual.localPosition;
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

            throwRoutine = null;
            friendRoutine = null;
            hopRoutine = null;
            kindRoutine = null;
            scaleRoutine = null;
            RestoreVisual();
        }

        /// <summary>
        /// Appears at the spawn point and starts walking toward the player (or, for a shy one, toward its cover).
        /// The party spot is where it will dance if it turns into a friend. The field is where a tiny one may run to.
        /// A shy alien with no free cover simply acts like a normal one.
        /// </summary>
        public void Activate(Vector3 spawnPosition, Transform playerTransform, Vector3 spot, Vector3 lookAt, int dance,
            AlienKind kind, AlienCover coverToHideBehind, Bounds field)
        {
            CacheReferences();
            ResetAlien();

            player = playerTransform;
            partySpot = spot;
            partyLookAt = lookAt;
            danceVariant = dance;
            hasPartySpot = true;
            fieldBounds = field;

            // A robot is always a robot; a kid in a robot's place is never asked to be one.
            Kind = isRobot ? AlienKind.Robot : (kind == AlienKind.Robot ? AlienKind.Normal : kind);
            ApplyKind(coverToHideBehind);

            // Place it on the floor before switching on, so the agent starts on the NavMesh.
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                spawnPosition = hit.position;
            transform.position = spawnPosition;
            gameObject.SetActive(true);

            if (agent.enabled && !agent.isOnNavMesh) agent.Warp(spawnPosition);
            if (AgentReady)
            {
                agent.speed = CurrentWalkSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }

            if (Kind == AlienKind.Shy && cover != null)
                kindRoutine = StartCoroutine(ShyRoutine());
        }

        /// <summary>Size, tint, zaps needed and so on for this kind.</summary>
        void ApplyKind(AlienCover coverToHideBehind)
        {
            switch (Kind)
            {
                case AlienKind.Big:
                    transform.localScale = baseScale * BigScales[0];
                    hittable.SetHitsToDefeat(BigHits);
                    ApplyTint(BigTint);
                    break;

                case AlienKind.Tiny:
                    transform.localScale = baseScale * TinyScale;
                    ApplyTint(TinyTint);
                    break;

                case AlienKind.Shy:
                    ApplyTint(ShyTint);
                    if (coverToHideBehind != null && coverToHideBehind.Claim()) cover = coverToHideBehind;
                    break;

                case AlienKind.Robot:
                    // Always the robot dance.
                    danceVariant = 2;
                    break;
            }
        }

        bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        float CurrentWalkSpeed => Kind == AlienKind.Big ? BigWalkSpeed : walkSpeed;

        void Update()
        {
            if (!AgentReady || IsFriend || IsThrowing || player == null) return;

            // A tiny or shy alien's own routine is in charge of its legs.
            if (kindRoutine != null) return;

            if (Kind == AlienKind.Tiny && FlatDistance(player.position, transform.position) < TinyAlertDistance)
            {
                kindRoutine = StartCoroutine(TinyRoutine());
                return;
            }

            WalkTowardPlayer();
        }

        /// <summary>Walks until it is keepDistance from the player, then stands and watches him.</summary>
        void WalkTowardPlayer()
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (distance <= keepDistance + 0.5f)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
                FaceDirection(toPlayer, 360f);
                return;
            }

            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = CurrentWalkSpeed;
            if (Time.time < nextRepathTime) return;

            nextRepathTime = Time.time + RepathSeconds;
            Vector3 stopPoint = player.position - toPlayer / distance * keepDistance;
            if (NavMesh.SamplePosition(stopPoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        // ----- Tiny: runs away, then gets tired ------------------------------------------------

        IEnumerator TinyRoutine()
        {
            if (TryFindFleePoint(out Vector3 point) && AgentReady)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = TinyRunSpeed;
                agent.SetDestination(point);

                if (Random.value < 0.5f)
                {
                    Sfx.Play("aliens.giggle", transform.position);
                    SayKindLine("aliens.tiny_tease");
                }

                float giveUp = Time.time + TinyRunGiveUpSeconds;
                yield return null;
                while (Time.time < giveUp && AgentReady && (agent.pathPending || agent.remainingDistance > ArriveDistance))
                    yield return null;
            }

            // Rest: stands still, puffs sparkles and says it is tired. This is the easy moment to zap it.
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
                agent.velocity = Vector3.zero;
            }
            SayKindLine("aliens.tiny_tired");

            float nextPuff = 0f;
            for (float elapsed = 0f; elapsed < TinyRestSeconds; elapsed += Time.deltaTime)
            {
                if (player != null) FaceDirection(player.position - transform.position, 360f);
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.6f;
                    BattleEffects.Sparkle(transform.position + Vector3.up * 0.6f);
                }
                yield return null;
            }

            kindRoutine = null;
        }

        /// <summary>A walkable point about 10 m away inside the field, preferably further from the player than it is now.</summary>
        bool TryFindFleePoint(out Vector3 point)
        {
            point = transform.position;

            Vector3 from = transform.position;
            Vector3 away = from - player.position;
            away.y = 0f;
            away = away.sqrMagnitude < 0.01f ? transform.forward : away.normalized;
            float currentDistance = FlatDistance(from, player.position);

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 direction = Quaternion.Euler(0f, Random.Range(-110f, 110f), 0f) * away;
                Vector3 candidate = from + direction * Random.Range(TinyFleeDistance - 2f, TinyFleeDistance + 1f);
                if (!InField(candidate)) continue;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;
                if (!InField(hit.position)) continue;

                // The first tries insist on running away from the player; later ones take any walkable spot.
                if (attempt < 8 && FlatDistance(hit.position, player.position) < currentDistance + 1f) continue;

                point = hit.position;
                return true;
            }
            return false;
        }

        bool InField(Vector3 point)
        {
            if (fieldBounds.size == Vector3.zero) return true;

            return point.x >= fieldBounds.min.x && point.x <= fieldBounds.max.x
                && point.z >= fieldBounds.min.z && point.z <= fieldBounds.max.z;
        }

        // ----- Shy: hides, peeks, comes out ----------------------------------------------------

        IEnumerator ShyRoutine()
        {
            // To the hiding place, behind the cover.
            yield return MoveTo(cover.HidePoint.position, ShyWalkSpeed, ShyGoToCoverSeconds);
            if (!moveArrived)
            {
                ComeOutForGood();
                yield break;
            }

            int peeks = 0;
            while (peeks < ShyMaxPeeks)
            {
                // Hidden: out of sight, cannot be zapped. Waits 3-5 s, or peeks as soon as the player comes close.
                IsHidden = true;
                if (AgentReady)
                {
                    agent.isStopped = true;
                    agent.updateRotation = false;
                }
                transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

                float wait = Random.Range(ShyHiddenMinSeconds, ShyHiddenMaxSeconds);
                while (wait > 0f && !PlayerNearCover(ShyPlayerNearDistance))
                {
                    wait -= Time.deltaTime;
                    yield return null;
                }

                // Peek: steps out beside the cover, says peekaboo, stays 2 s (or as long as the player stands close).
                IsHidden = false;
                peeks++;
                yield return MoveTo(cover.PeekPoint.position, ShyWalkSpeed, 6f);
                if (AgentReady)
                {
                    agent.isStopped = true;
                    agent.updateRotation = false;
                }

                Sfx.Play("aliens.peek", transform.position);
                SayKindLine("aliens.shy_peek");
                BattleEffects.Sparkle(transform.position + Vector3.up * 1f);

                float stay = ShyPeekSeconds;
                float extra = 0f;
                while (stay > 0f || (PlayerNearCover(ShyStayOutDistance) && extra < ShyStayOutMaxSeconds))
                {
                    if (player != null) FaceDirection(player.position - transform.position, 360f);
                    if (stay > 0f) stay -= Time.deltaTime;
                    else extra += Time.deltaTime;
                    yield return null;
                }

                if (peeks >= ShyMaxPeeks) break;

                yield return MoveTo(cover.HidePoint.position, ShyWalkSpeed, 8f);
                if (!moveArrived) break;
            }

            // Safety net: after three peeks it stops hiding and acts like a normal alien.
            ComeOutForGood();
        }

        /// <summary>Gives the cover back and lets the normal walking take over.</summary>
        void ComeOutForGood()
        {
            IsHidden = false;
            ReleaseCover();
            kindRoutine = null;
            if (AgentReady)
            {
                agent.speed = CurrentWalkSpeed;
                agent.isStopped = false;
                agent.updateRotation = true;
            }
        }

        bool PlayerNearCover(float distance)
        {
            if (player == null || cover == null) return false;

            return FlatDistance(player.position, cover.transform.position) <= distance;
        }

        void ReleaseCover()
        {
            if (cover != null) cover.Release();
            cover = null;
        }

        /// <summary>Walks to a point on the NavMesh. moveArrived tells whether it got there in time.</summary>
        IEnumerator MoveTo(Vector3 target, float speed, float giveUpSeconds)
        {
            moveArrived = false;
            if (!AgentReady) yield break;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) target = hit.position;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.speed = speed;
            agent.SetDestination(target);

            float giveUp = Time.time + giveUpSeconds;
            yield return null;
            while (Time.time < giveUp && AgentReady)
            {
                if (!agent.pathPending && agent.remainingDistance <= ArriveDistance + 0.1f)
                {
                    moveArrived = true;
                    yield break;
                }
                yield return null;
            }
        }

        // ----- Slime ---------------------------------------------------------------------------

        /// <summary>Stands still, swings its arm and lobs a slime ball at the target.</summary>
        public void ThrowAt(Transform target)
        {
            if (!CanThrowSlime || target == null) return;

            throwRoutine = StartCoroutine(ThrowRoutine(target));
        }

        IEnumerator ThrowRoutine(Transform target)
        {
            if (AgentReady)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
            }

            FaceDirection(target.position - transform.position, 720f);
            if (kidAnimator != null) kidAnimator.PlayThrow();
            if (thrower != null) thrower.Throw(target);

            for (float elapsed = 0f; elapsed < throwStandSeconds; elapsed += Time.deltaTime)
            {
                FaceDirection(target.position - transform.position, 360f);
                yield return null;
            }

            throwRoutine = null;
        }

        // ----- Zaps and friends ----------------------------------------------------------------

        void OnHit(Hittable who)
        {
            if (who.IsDefeated) return;

            if (kidAnimator != null) kidAnimator.PlayHit();

            // A big alien that is not down yet giggles and shrinks one step.
            if (Kind == AlienKind.Big)
            {
                int step = Mathf.Clamp(who.HitsTaken, 0, BigScales.Length - 1);
                StartScaleTo(baseScale * BigScales[step]);
                Sfx.Play("aliens.giggle", transform.position);
                Sfx.Play("aliens.shrink", transform.position);
                SayKindLine("aliens.big_hit");
            }
        }

        void OnDefeated(Hittable who)
        {
            BecomeFriend();
        }

        /// <summary>
        /// The alien turns friendly: green confetti, a happy shout, and off to the party line. Also used as the safety net
        /// that makes a stubborn alien a friend after a long time (hiding ones too).
        /// </summary>
        public void BecomeFriend()
        {
            if (IsFriend) return;

            IsFriend = true;
            CancelThrow();

            if (kindRoutine != null) StopCoroutine(kindRoutine);
            kindRoutine = null;
            IsHidden = false;
            ReleaseCover();
            if (Kind == AlienKind.Big) StartScaleTo(baseScale);

            Vector3 above = transform.position + Vector3.up * 1f;
            BattleEffects.Confetti(above, friendConfettiColor);
            BattleEffects.Sparkle(above);
            VoicePlayer.Say(Kind == AlienKind.Robot ? "aliens.robot_zapped" : "aliens.zapped");
            if (kidAnimator != null) kidAnimator.PlayCheer();

            BecameFriend?.Invoke(this);
            friendRoutine = StartCoroutine(FriendRoutine());
        }

        IEnumerator FriendRoutine()
        {
            // A moment of cheering where it stands.
            yield return new WaitForSeconds(0.9f);

            if (hasPartySpot && AgentReady)
            {
                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = friendSpeed;
                agent.SetDestination(partySpot);

                float giveUp = Time.time + GiveUpWalkSeconds;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > ArriveDistance))
                    yield return null;

                agent.isStopped = true;
                agent.updateRotation = false;
            }

            // Face the camera side, then dance.
            if (hasPartySpot)
            {
                Vector3 facing = partyLookAt - transform.position;
                for (float elapsed = 0f; elapsed < 0.6f; elapsed += Time.deltaTime)
                {
                    FaceDirection(facing, 540f);
                    yield return null;
                }
            }

            StartDancing();
            friendRoutine = null;
        }

        void StartDancing()
        {
            dancing = true;
            if (kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
        }

        /// <summary>A little hop in the line, used while the aliens are counted out loud.</summary>
        public void Hop()
        {
            if (!isActiveAndEnabled) return;

            if (hopRoutine != null) StopCoroutine(hopRoutine);
            hopRoutine = StartCoroutine(HopRoutine());
        }

        IEnumerator HopRoutine()
        {
            if (kidAnimator != null)
            {
                kidAnimator.SetDancing(false);
                kidAnimator.PlayJump();
            }
            BattleEffects.Sparkle(transform.position + Vector3.up * 1.2f);

            for (float elapsed = 0f; elapsed < HopSeconds; elapsed += Time.deltaTime)
            {
                if (visual != null)
                    visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(elapsed / HopSeconds * Mathf.PI) * HopHeight);
                yield return null;
            }

            RestoreVisual();
            hopRoutine = null;
            if (dancing && kidAnimator != null) kidAnimator.SetDancing(true, danceVariant);
        }

        /// <summary>Stops the dance and cheers goodbye.</summary>
        public void Wave()
        {
            dancing = false;
            if (kidAnimator == null) return;

            kidAnimator.SetDancing(false);
            kidAnimator.PlayCheer();
        }

        /// <summary>Disappears with a puff of sparkles.</summary>
        public void Vanish()
        {
            BattleEffects.Sparkle(transform.position + Vector3.up * 1f);
            BattleEffects.Sparkle(transform.position + Vector3.up * 0.4f);
            ResetAlien();
        }

        /// <summary>
        /// Back to the way it was before the game: switched off, a normal kid again (kind, size, tint, zaps needed),
        /// not a friend, no dance, no slime, no cover claimed.
        /// </summary>
        public void ResetAlien()
        {
            CacheReferences();

            if (throwRoutine != null) StopCoroutine(throwRoutine);
            if (friendRoutine != null) StopCoroutine(friendRoutine);
            if (hopRoutine != null) StopCoroutine(hopRoutine);
            if (kindRoutine != null) StopCoroutine(kindRoutine);
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            throwRoutine = null;
            friendRoutine = null;
            hopRoutine = null;
            kindRoutine = null;
            scaleRoutine = null;
            CancelThrow();
            RestoreVisual();

            if (kidAnimator != null && dancing) kidAnimator.SetDancing(false);
            dancing = false;
            IsFriend = false;
            IsHidden = false;
            hasPartySpot = false;
            Kind = AlienKind.Normal;
            ReleaseCover();

            transform.localScale = baseScale;
            ClearTint();
            hittable.SetHitsToDefeat(1);
            hittable.ResetState();

            gameObject.SetActive(false);
        }

        // ----- Size and colour -----------------------------------------------------------------

        void StartScaleTo(Vector3 target)
        {
            if (!isActiveAndEnabled) return;

            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(ScaleRoutine(target));
        }

        /// <summary>A squash wobble while it changes size: a little flatter in the middle of the move.</summary>
        IEnumerator ScaleRoutine(Vector3 target)
        {
            const float seconds = 0.4f;
            Vector3 from = transform.localScale;

            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float k = elapsed / seconds;
                Vector3 size = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, k));
                size.y *= 1f - 0.2f * Mathf.Sin(k * Mathf.PI);
                size.x *= 1f + 0.1f * Mathf.Sin(k * Mathf.PI);
                transform.localScale = size;
                yield return null;
            }

            transform.localScale = target;
            scaleRoutine = null;
        }

        /// <summary>Multiplies the tint into each material's own base colour through a property block (no material copies).</summary>
        void ApplyTint(Color tint)
        {
            if (bodyRenderers == null) bodyRenderers = GetComponentsInChildren<Renderer>(true);
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();

            foreach (Renderer body in bodyRenderers)
            {
                if (!(body is SkinnedMeshRenderer) && !(body is MeshRenderer)) continue;

                Material[] materials = body.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || !material.HasProperty(BaseColorId)) continue;

                    body.GetPropertyBlock(tintBlock, i);
                    tintBlock.SetColor(BaseColorId, material.GetColor(BaseColorId) * tint);
                    body.SetPropertyBlock(tintBlock, i);
                }
            }
            tinted = true;
        }

        void ClearTint()
        {
            if (!tinted || bodyRenderers == null) return;

            foreach (Renderer body in bodyRenderers)
            {
                if (body == null) continue;

                int count = body.sharedMaterials.Length;
                for (int i = 0; i < count; i++) body.SetPropertyBlock(null, i);
            }
            tinted = false;
        }

        // ----- Helpers -------------------------------------------------------------------------

        /// <summary>Says a kind-specific line, but not more than one every few seconds.</summary>
        static void SayKindLine(string group)
        {
            if (Time.time < nextKindVoiceTime) return;

            nextKindVoiceTime = Time.time + KindVoiceGapSeconds;
            VoicePlayer.Say(group);
        }

        void CancelThrow()
        {
            if (throwRoutine != null) StopCoroutine(throwRoutine);
            throwRoutine = null;
            if (thrower != null) thrower.CancelThrow();
        }

        void RestoreVisual()
        {
            if (visual != null) visual.localPosition = visualBasePosition;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void FaceDirection(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, degreesPerSecond * Time.deltaTime);
        }
    }
}
