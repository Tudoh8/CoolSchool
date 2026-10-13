using System.Collections;
using System.Collections.Generic;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Rewards;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace CoolSchool.Aliens
{
    /// <summary>For testing: force one kind of rescue every visit (Random is the normal game).</summary>
    public enum DebugRescue
    {
        Random,
        Tree,
        Rocket,
        Bubble,
        Slime
    }

    /// <summary>
    /// "Alien landing". A cardboard rocket lands west of the playground and silly alien kids come out of it and from all
    /// over the field. The player is a superhero with a cape, a mask and a ray gun: a zapped alien is not hurt, it turns
    /// into a friend and dances. A baby alien is stuck somewhere (in a tree, on the rocket, in a bubble or in slime), so
    /// the player bounces up on a trampoline or zaps the slime away and brings it to safety. Then the dancing friends
    /// are counted, there is a dance party, and the rocket takes off.
    /// Every visit is different: 2 to 8 aliens by level, a mix of kinds, other spawn spots and another rescue.
    /// The kid can never fail: every step has a hint and a safety net. Everything that was changed is put back in OnEnd.
    /// </summary>
    public partial class AlienLanding : Activity
    {
        [Header("Player")]
        [SerializeField] PlayerController player;
        [SerializeField] RayGunShooter rayGun;
        [SerializeField] ActionButton actionButton;
        [SerializeField] Sprite boltIcon;
        [SerializeField] ImaginationEffect imagination;

        [Header("Rocket")]
        [SerializeField] Transform rocket;
        [SerializeField] Transform rocketLandingPoint;
        [SerializeField] Transform rocketSkyPoint;

        [Header("Aliens")]
        [Tooltip("Spread over the field: the rocket door, the north side of each tree, behind covers.")]
        [SerializeField] Transform[] alienSpawnPoints;
        [Tooltip("The pool: 8 alien kids and 2 robots (isRobot). Each visit uses as many as the level asks for.")]
        [SerializeField] AlienKidAI[] aliens;
        [Tooltip("The two baby aliens. One is picked at random each visit.")]
        [SerializeField] RescueTarget[] babies;
        [Tooltip("Every place the baby can be stuck: trees, the rocket nose, the bubble and the slime puddles.")]
        [SerializeField] AlienRescueSite[] rescueSites;
        [Tooltip("Rocks and bushes the shy aliens hide behind.")]
        [SerializeField] AlienCover[] covers;
        [Tooltip("The party line in front of the camera, 10 spots: a visit uses a centred slice for the aliens, then one for the baby.")]
        [SerializeField] Transform[] partyLinePoints;
        [Tooltip("The field a tiny alien may run around in (only the ground plan counts).")]
        [SerializeField] Bounds fieldBounds;

        [Tooltip("Where the baby waits while the aliens are counted. It is moved behind the counting camera every visit.")]
        [SerializeField] Transform babyWaitPoint;

        [Header("Counting (skipped when there is no lesson UI)")]
        [SerializeField] LessonUI lessonUI;
        [Tooltip("The close-up shot of the dancing aliens during the question (the baby is not in it). It is moved every visit to fit everybody.")]
        [SerializeField] Transform countCameraPoint;
        [Tooltip("Glides the camera to the close-up and back. Added automatically when it is missing.")]
        [SerializeField] LessonCameraRig cameraRig;

        [Header("Timing")]
        [SerializeField] float descentSeconds = 3f;
        [SerializeField] float takeoffSeconds = 2.5f;
        [SerializeField] float partySeconds = 4f;
        [SerializeField] float zapHintSeconds = 5f;
        [Tooltip("Seconds between slimes: one alien throws every so often, a random time between these two.")]
        [SerializeField] Vector2 slimeGapSeconds = new Vector2(4f, 6f);

        [Header("Testing")]
        [Tooltip("Random in the real game. Pick a kind to get that rescue every visit.")]
        [SerializeField] DebugRescue debugForceRescue = DebugRescue.Random;
        [Tooltip("0 in the real game (the number of aliens follows the level). Anything else is the number of aliens.")]
        [SerializeField] int debugForceCount;

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent onRescue;
        public UnityEvent onVictory;

        // The camera is south of the player, looking north, so the party line faces south.
        static readonly Vector3 CameraSide = Vector3.back;

        const string Skill = "aliens";
        const int MaxLevel = 3;
        const int PartyLineSpots = 10;

        const float AlienGapSeconds = 1.2f;
        const float FirstSlimeDelaySeconds = 3f;
        const float SlimeRangeMeters = 14f;
        const float HintRepeatSeconds = 15f;
        // Safety nets, so the kid is never stuck: slow zapping turns the rest of the aliens friendly, a long rescue puts the baby down.
        const float WaveTimeoutSeconds = 150f;
        const float RescueTimeoutSeconds = 120f;
        const float BubbleTimeoutSeconds = 90f;
        const float SlimeTimeoutSeconds = 60f;
        const float RescueRepeatSeconds = 25f;
        const float FloatFallSpeed = 1.6f;
        const float TrailSpacing = 1.5f;
        const float TrailRefreshSeconds = 0.7f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;
        const float MaxVoiceWaitSeconds = 8f;

        // Spawning from all over the field.
        const float SpawnMinPlayerDistance = 6f;
        const float SpawnNorthSlack = 2f;

        // The close-up of the dancers: the same height and pitch as the builder's default shot.
        const float CountCameraHeight = 2.2f;
        const float CountCameraMinBack = 4.5f;
        const float CountCameraPitch = 21f;
        const float CountCameraMargin = 1f;
        const float BabyWaitBehindCamera = 3f;

        // What the last visit did, so the next one is different.
        static int lastCount;
        static int lastRescueKind = -1;
        static int lastTreeSiteId;

        static readonly Vector2Int[] CountByLevel = { new Vector2Int(2, 4), new Vector2Int(3, 6), new Vector2Int(5, 8) };

        Coroutine flowRoutine;
        Coroutine rocketRoutine;
        bool quitting;
        bool hudHidden;
        bool playerDancing;
        float savedMaxFallSpeed = 100f;
        int pickedCard = -1;

        // This visit.
        int level = 1;
        readonly List<AlienKidAI> visit = new List<AlienKidAI>();
        readonly List<AlienKind> visitKinds = new List<AlienKind>();
        readonly HashSet<AlienKind> hintedKinds = new HashSet<AlienKind>();
        readonly List<Transform> chosenSpawns = new List<Transform>();
        RescueTarget activeBaby;
        AlienRescueSite activeSite;
        int slotStart;
        int firstPick;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            if (player != null)
            {
                savedMaxFallSpeed = player.MaxFallSpeed;
                player.JumpEnabled = false;
            }

            ResetEverything();
            PlanVisit();

            // The superhero: cape and mask appear in the imagination sparkle, and the button becomes a ray gun trigger.
            if (imagination != null) imagination.Play(true);
            SetCostume(true);
            if (rayGun != null)
            {
                rayGun.enabled = true;
                rayGun.ControlLocked = false;
                rayGun.ShowGun(true);
            }
            if (actionButton != null) actionButton.SetIcon(boltIcon);

            VoicePlayer.Say("aliens.start");
            Sfx.Play("aliens.rocket_land", RocketLandingPosition());
            Sfx.PlayMusic("music.aliens");

            flowRoutine = StartCoroutine(FlowRoutine());
        }

        protected override void OnEnd()
        {
            if (flowRoutine != null)
            {
                StopCoroutine(flowRoutine);
                flowRoutine = null;
            }
            if (rocketRoutine != null)
            {
                StopCoroutine(rocketRoutine);
                rocketRoutine = null;
            }
            pickedCard = -1;

            // The close-up camera goes back to the follow camera, even when the game ends in the middle of the question.
            if (cameraRig != null) cameraRig.RestoreNow();

            if (hudHidden && lessonUI != null) lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;
            if (lessonUI != null) lessonUI.HideCards();

            if (playerDancing)
            {
                var kid = player != null ? player.GetComponent<KidAnimator>() : null;
                if (kid != null) kid.SetDancing(false);
                playerDancing = false;
            }

            if (player != null)
            {
                player.JumpEnabled = true;
                player.MaxFallSpeed = savedMaxFallSpeed;
                player.InputEnabled = true;
            }
            if (rayGun != null)
            {
                rayGun.ControlLocked = false;
                rayGun.ShowGun(false);
                rayGun.enabled = false;
            }
            if (actionButton != null) actionButton.ResetIcon();
            SetCostume(false);

            ResetEverything();
            JumpPad.Unblock();
            if (imagination != null) imagination.Play(false);
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDisable()
        {
            // If this object is switched off mid-game, put everything back first.
            if (IsRunning && !quitting) End();
        }

        // ----- Planning the visit ------------------------------------------------------------------

        /// <summary>Works out how many aliens come, which kinds, and where they appear. Every visit is different.</summary>
        void PlanVisit()
        {
            level = Mathf.Clamp(ProgressStore.Current.GetLevel(Skill), 1, MaxLevel);
            firstPick = -1;
            visit.Clear();
            visitKinds.Clear();
            hintedKinds.Clear();
            activeBaby = null;
            activeSite = null;

            var kids = new List<AlienKidAI>();
            var robots = new List<AlienKidAI>();
            if (aliens != null)
            {
                foreach (AlienKidAI alien in aliens)
                {
                    if (alien == null) continue;

                    if (alien.IsRobotModel) robots.Add(alien);
                    else kids.Add(alien);
                }
            }
            Shuffle(kids);
            Shuffle(robots);

            int count = Mathf.Min(ChooseCount(), kids.Count + robots.Count);
            List<AlienKind> kinds = ChooseKinds(count, robots.Count);

            foreach (AlienKind kind in kinds)
            {
                AlienKidAI alien = null;
                if (kind == AlienKind.Robot)
                {
                    if (robots.Count > 0)
                    {
                        alien = robots[robots.Count - 1];
                        robots.RemoveAt(robots.Count - 1);
                    }
                }
                else if (kids.Count > 0)
                {
                    alien = kids[kids.Count - 1];
                    kids.RemoveAt(kids.Count - 1);
                }

                if (alien == null) continue;

                visit.Add(alien);
                visitKinds.Add(kind);
            }

            int spots = partyLinePoints != null && partyLinePoints.Length > 0 ? partyLinePoints.Length : PartyLineSpots;
            slotStart = Mathf.Max(0, (spots - visit.Count) / 2);

            ChooseSpawnSpots();
        }

        /// <summary>
        /// 2-4 at level 1, 3-6 at level 2, 5-8 at level 3 (random inside the range), never the same as the last visit.
        /// </summary>
        int ChooseCount()
        {
            int poolSize = aliens != null ? aliens.Length : 0;
            if (debugForceCount > 0)
            {
                lastCount = Mathf.Min(debugForceCount, poolSize);
                return lastCount;
            }

            Vector2Int range = CountByLevel[level - 1];
            var options = new List<int>();
            for (int n = range.x; n <= range.y; n++)
                if (n != lastCount) options.Add(n);

            int count = options.Count > 0 ? options[Random.Range(0, options.Count)] : range.x;
            lastCount = count;
            return count;
        }

        /// <summary>
        /// The kinds for this visit: a few specials (Tiny, Shy, Robot and at level 2-3 Big), the rest Normal.
        /// At most one Big and two Robots. Shuffled, so the order of arrival is a surprise.
        /// </summary>
        List<AlienKind> ChooseKinds(int count, int robotsAvailable)
        {
            int specials = level == 1 ? 1 : (level == 2 ? Random.Range(1, 3) : Random.Range(1, 4));
            specials = Mathf.Min(specials, count);

            var kinds = new List<AlienKind>();
            int bigs = 0;
            int robots = 0;
            for (int i = 0; i < specials; i++)
            {
                AlienKind kind = AlienKind.Tiny;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int roll = Random.Range(level == 1 ? 1 : 0, 4);
                    AlienKind candidate = roll == 0 ? AlienKind.Big : (roll == 1 ? AlienKind.Tiny : (roll == 2 ? AlienKind.Shy : AlienKind.Robot));
                    if (candidate == AlienKind.Big && bigs >= 1) continue;
                    if (candidate == AlienKind.Robot && (robots >= 2 || robots >= robotsAvailable)) continue;

                    kind = candidate;
                    break;
                }

                if (kind == AlienKind.Big) bigs++;
                if (kind == AlienKind.Robot) robots++;
                kinds.Add(kind);
            }

            while (kinds.Count < count) kinds.Add(AlienKind.Normal);
            Shuffle(kinds);
            return kinds;
        }

        /// <summary>
        /// Picks 2-3 spawn spots for this visit: at least 6 m from the player and not far behind him (so they are in view).
        /// When too few qualify the rest are any spots.
        /// </summary>
        void ChooseSpawnSpots()
        {
            chosenSpawns.Clear();
            if (alienSpawnPoints == null) return;

            var inView = new List<Transform>();
            var others = new List<Transform>();
            foreach (Transform point in alienSpawnPoints)
            {
                if (point == null) continue;

                bool qualifies = true;
                if (player != null)
                {
                    Vector3 offset = point.position - player.transform.position;
                    offset.y = 0f;
                    qualifies = offset.magnitude >= SpawnMinPlayerDistance && point.position.z >= player.transform.position.z - SpawnNorthSlack;
                }
                (qualifies ? inView : others).Add(point);
            }
            Shuffle(inView);
            Shuffle(others);

            int wanted = Random.Range(2, 4);
            foreach (Transform point in inView)
                if (chosenSpawns.Count < wanted) chosenSpawns.Add(point);
            foreach (Transform point in others)
                if (chosenSpawns.Count < wanted) chosenSpawns.Add(point);
        }

        // ----- The flow of the game ----------------------------------------------------------------

        IEnumerator FlowRoutine()
        {
            yield return DescendRocket();
            yield return RunWave();
            yield return RunRescue();
            yield return RunCount();
            yield return RunBabyJoinsParty();
            yield return RunParty();

            flowRoutine = null;
            End();
        }

        // ---- 1. The rocket lands ----

        IEnumerator DescendRocket()
        {
            if (rocket == null) yield break;

            Vector3 landing = RocketLandingPosition();
            Vector3 sky = rocketSkyPoint != null ? rocketSkyPoint.position : landing + Vector3.up * 25f;
            rocket.position = sky;
            rocket.gameObject.SetActive(true);

            float nextSparkle = 0f;
            for (float elapsed = 0f; elapsed < descentSeconds; elapsed += Time.deltaTime)
            {
                // Fast at first, then slowing down for a soft landing.
                float t = 1f - Mathf.Pow(1f - elapsed / descentSeconds, 3f);
                rocket.position = Vector3.Lerp(sky, landing, t);

                if (Time.time >= nextSparkle)
                {
                    nextSparkle = Time.time + 0.2f;
                    RocketSparkle();
                }
                yield return null;
            }

            rocket.position = landing;
            for (int i = 0; i < 5; i++) RocketSparkle();
            yield return new WaitForSeconds(0.8f);
        }

        void RocketSparkle()
        {
            if (rocket == null) return;

            Vector2 ring = Random.insideUnitCircle * 1.2f;
            BattleEffects.Sparkle(rocket.position + new Vector3(ring.x, Random.Range(0.1f, 1.2f), ring.y));
        }

        // ---- 2. The aliens come from everywhere, and get zapped into friends ----

        IEnumerator RunWave()
        {
            int total = visit.Count;
            int spawned = 0;
            float nextSpawn = Time.time;
            float waveStart = Time.time;
            float nextSlime = float.MaxValue;
            float nextHint = float.MaxValue;
            bool warned = false;
            bool firstSpawn = true;

            while (true)
            {
                if (spawned < total && Time.time >= nextSpawn)
                {
                    SpawnAlien(spawned);
                    spawned++;
                    nextSpawn = Time.time + AlienGapSeconds;

                    if (firstSpawn)
                    {
                        firstSpawn = false;
                        VoicePlayer.Say("aliens.arrive");
                        nextSlime = Time.time + FirstSlimeDelaySeconds;
                        nextHint = Time.time + zapHintSeconds;
                    }
                }

                if (spawned >= total && AllFriends()) break;

                // One alien at a time throws slime, every few seconds.
                if (Time.time >= nextSlime)
                {
                    nextSlime = Time.time + Random.Range(slimeGapSeconds.x, slimeGapSeconds.y);
                    if (ThrowSlime() && !warned)
                    {
                        warned = true;
                        VoicePlayer.Say("aliens.slime_warning");
                    }
                }

                // Hasn't zapped yet: show him what the button does.
                if (Time.time >= nextHint)
                {
                    nextHint = Time.time + HintRepeatSeconds;
                    if (rayGun == null || rayGun.ShotCount == 0) VoicePlayer.Say("aliens.zap_hint");
                }

                // Safety net: nobody is ever stuck. After a very long time the rest of the aliens turn friendly by themselves
                // (the ones hiding behind a cover too).
                if (Time.time - waveStart > WaveTimeoutSeconds)
                {
                    for (int i = spawned; i < total; i++) SpawnAlien(i);
                    spawned = total;
                    foreach (AlienKidAI alien in visit)
                        if (alien != null) alien.BecomeFriend();
                    yield return new WaitForSeconds(2f);
                    break;
                }

                yield return null;
            }

            // Wait for the last friend to reach the party line and start dancing (but not for long).
            float giveUp = Time.time + 12f;
            while (!AllDancing() && Time.time < giveUp)
                yield return null;
        }

        void SpawnAlien(int index)
        {
            AlienKidAI alien = visit[index];
            if (alien == null) return;

            Vector3 spawn = SpawnPosition(index);
            AlienKind kind = visitKinds[index];

            // A shy one needs a free cover; without one it simply acts like a normal alien.
            AlienCover cover = null;
            if (kind == AlienKind.Shy)
            {
                cover = PickFreeCover();
                if (cover == null) kind = AlienKind.Normal;
            }

            Vector3 spot = PartySpot(index);
            alien.Activate(spawn, player != null ? player.transform : null, spot, spot + CameraSide * 5f, index, kind, cover, fieldBounds);
            visitKinds[index] = alien.Kind;
            BattleEffects.Sparkle(spawn + Vector3.up * 0.8f);
            Sfx.Play("aliens.spawn", spawn);

            // The first time a big, tiny or shy alien shows up in a visit, the teacher says what to do.
            if (alien.Kind != AlienKind.Normal && alien.Kind != AlienKind.Robot && hintedKinds.Add(alien.Kind))
                VoicePlayer.Say(HintKey(alien.Kind));
        }

        static string HintKey(AlienKind kind)
        {
            switch (kind)
            {
                case AlienKind.Big: return "aliens.big_hint";
                case AlienKind.Tiny: return "aliens.tiny_hint";
                default: return "aliens.shy_hint";
            }
        }

        AlienCover PickFreeCover()
        {
            if (covers == null) return null;

            AlienCover chosen = null;
            int free = 0;
            foreach (AlienCover cover in covers)
            {
                if (cover == null || cover.IsTaken) continue;

                // Reservoir sampling: every free cover has the same chance.
                free++;
                if (Random.Range(0, free) == 0) chosen = cover;
            }
            return chosen;
        }

        /// <summary>Picks one alien in range that is not busy and lets it throw. Returns false if nobody could.</summary>
        bool ThrowSlime()
        {
            if (player == null) return false;

            int candidates = 0;
            AlienKidAI chosen = null;
            foreach (AlienKidAI alien in visit)
            {
                if (alien == null || !alien.CanThrowSlime) continue;

                Vector3 offset = alien.transform.position - player.transform.position;
                offset.y = 0f;
                if (offset.magnitude > SlimeRangeMeters) continue;

                // Reservoir sampling: every candidate has the same chance.
                candidates++;
                if (Random.Range(0, candidates) == 0) chosen = alien;
            }

            if (chosen == null) return false;

            chosen.ThrowAt(player.transform);
            return true;
        }

        bool AllFriends()
        {
            foreach (AlienKidAI alien in visit)
                if (alien != null && !alien.IsFriend) return false;
            return true;
        }

        bool AllDancing()
        {
            foreach (AlienKidAI alien in visit)
                if (alien != null && !alien.IsDancing) return false;
            return true;
        }

        // ---- 3. The baby alien is stuck ----

        IEnumerator RunRescue()
        {
            // The counting shot and the baby's waiting place are worked out now, when everybody's party spot is known.
            FrameCountShot();

            activeBaby = PickBaby();
            activeSite = PickSite();
            if (activeBaby == null || activeSite == null || player == null)
            {
                activeBaby = null;
                activeSite = null;
                yield break;
            }

            if (activeSite.Kind == RescueKind.Slime) yield return RescueFromSlime(activeSite, activeBaby);
            else yield return RescueFromHeight(activeSite, activeBaby);

            // The baby is safe.
            activeSite.ResetSite();
            VoicePlayer.Say("aliens.rescued", true);
            BattleEffects.Confetti(player.transform.position + Vector3.up * 1.5f);
            BattleEffects.Confetti(activeBaby.transform.position + Vector3.up * 1f);
            onRescue?.Invoke();

            float arriveBy = Time.time + 8f;
            if (BabyWaitsForCount)
            {
                // The question is only about the aliens: the baby watches from behind the camera, out of the shot,
                // and joins the dance party after the counting.
                activeBaby.WalkTo(babyWaitPoint.position, PartySpot(0));
                yield return new WaitForSeconds(1.5f);
                while (!activeBaby.HasArrived && Time.time < arriveBy)
                    yield return null;
            }
            else
            {
                // Nothing to count: straight to its place in the party line.
                SendBabyToParty();
                yield return new WaitForSeconds(1.5f);
                while (!activeBaby.IsDancing && Time.time < arriveBy)
                    yield return null;
            }
        }

        /// <summary>Tree, rocket nose or bubble: bounce up on the glowing trampoline and grab the baby in the air.</summary>
        IEnumerator RescueFromHeight(AlienRescueSite site, RescueTarget baby)
        {
            JumpPad pad = site.Pad;
            RescueKind kind = site.Kind;
            Transform seat = site.Perch;

            if (kind == RescueKind.Rocket)
            {
                // The rocket's trampoline pops in, and bounces just high enough to reach the nose (about 1 m above it).
                pad.gameObject.SetActive(true);
                float height = site.Perch.position.y - pad.Position.y + 1f;
                pad.SetLaunchSpeed(JumpPad.SpeedForHeight(height, player.Gravity));
                BattleEffects.Sparkle(pad.Position + Vector3.up * 0.4f);
                BattleEffects.Sparkle(pad.Position + Vector3.up * 1.2f);
            }
            else if (kind == RescueKind.Bubble)
            {
                site.Bubble.Show(site.BubbleCentre());
                seat = site.Bubble.Seat;
            }

            baby.ShowAt(seat);
            pad.SetLit(true);
            pad.AimAt(seat);
            string voiceKey = RescueVoiceKey(kind);
            VoicePlayer.Say(voiceKey);

            float start = Time.time;
            float nextTrail = 0f;
            float nextHint = Time.time + RescueRepeatSeconds;
            float nextSparkle = 0f;

            while (!baby.IsCarried)
            {
                if (!player.IsGrounded)
                {
                    if (kind == RescueKind.Bubble)
                    {
                        // Close enough in the air: the bubble pops and the baby grabs on.
                        if (site.Bubble.TryPop(player.transform)) baby.Grab(player.transform);
                    }
                    else
                    {
                        // In the air near the baby: it jumps into his arms.
                        baby.TryGrab(player.transform);
                    }
                }

                if (player.IsGrounded && Time.time >= nextTrail)
                {
                    nextTrail = Time.time + TrailRefreshSeconds;
                    ShowTrailTo(pad.Position, pad.Radius);
                }

                if (Time.time >= nextSparkle)
                {
                    nextSparkle = Time.time + 1f;
                    BattleEffects.Sparkle(baby.transform.position + Vector3.up * 1.2f);
                }

                if (Time.time >= nextHint)
                {
                    nextHint = Time.time + RescueRepeatSeconds;
                    VoicePlayer.Say(voiceKey);
                }

                // Safety net for the bubble: after 90 s it sinks to the ground by itself and pops.
                if (kind == RescueKind.Bubble && Time.time - start > BubbleTimeoutSeconds && !site.Bubble.IsPopped)
                {
                    yield return site.Bubble.SinkAndPop(pad.Position.y + 1f);
                    break;
                }

                // Safety net: the baby climbs down by itself after a very long time.
                if (Time.time - start > RescueTimeoutSeconds) break;

                yield return null;
            }

            pad.SetLit(false);
            Sfx.Play("aliens.rescue", baby.transform.position);

            if (baby.IsCarried)
            {
                // Hang for a moment at the top, then float down together like a parachute. The trampolines do not
                // bounce him again until the baby is down.
                player.SetVerticalVelocity(Mathf.Min(player.VerticalVelocity, 1f));
                player.MaxFallSpeed = FloatFallSpeed;

                float floatStart = Time.time;
                float nextTwinkle = 0f;
                while ((!player.IsGrounded || Time.time - floatStart < 0.4f) && Time.time - floatStart < 12f)
                {
                    JumpPad.BlockFor(1f);
                    if (Time.time >= nextTwinkle)
                    {
                        nextTwinkle = Time.time + 0.2f;
                        BattleEffects.Sparkle(player.transform.position + Vector3.up * Random.Range(0.3f, 1.5f));
                    }
                    yield return null;
                }

                player.MaxFallSpeed = savedMaxFallSpeed;
                baby.PutDown(player.transform.position + player.transform.forward * 1f);
            }
            else
            {
                Vector3 ground = pad.Position;
                ground.y = 0f;
                baby.PutDown(ground + Vector3.back * 1.5f);
            }

            JumpPad.BlockFor(1.5f);
        }

        /// <summary>Slime: zap the blobs around the stuck baby. When the last one pops the puddle shrinks and the baby runs to the player.</summary>
        IEnumerator RescueFromSlime(AlienRescueSite site, RescueTarget baby)
        {
            SlimeTrap trap = site.Slime;
            trap.Show(SlimeBlobCount());
            baby.ShowStuck(trap.BabySpot);
            VoicePlayer.Say("aliens.rescue_slime");

            float start = Time.time;
            float nextTrail = 0f;
            float nextHint = Time.time + RescueRepeatSeconds;
            float nextSparkle = 0f;

            while (!trap.IsCleared)
            {
                if (Time.time >= nextTrail)
                {
                    nextTrail = Time.time + TrailRefreshSeconds;
                    ShowTrailTo(trap.Position, 3f);
                }

                if (Time.time >= nextSparkle)
                {
                    nextSparkle = Time.time + 1f;
                    BattleEffects.Sparkle(baby.transform.position + Vector3.up * 1.2f);
                }

                if (Time.time >= nextHint)
                {
                    nextHint = Time.time + RescueRepeatSeconds;
                    VoicePlayer.Say("aliens.rescue_slime");
                }

                // Safety net: after a minute the blobs that are left pop one by one by themselves.
                if (Time.time - start > SlimeTimeoutSeconds)
                {
                    yield return trap.PopRemaining();
                    break;
                }

                yield return null;
            }

            // Free: the puddle shrinks away, the baby hops out and runs to the player.
            Sfx.Play("aliens.rescue", baby.transform.position);
            BattleEffects.Confetti(baby.transform.position + Vector3.up * 1f, new Color(0.35f, 0.9f, 0.25f, 1f));
            VoicePlayer.Say("aliens.slime_free");
            yield return trap.Shrink();

            Vector3 toPlayer = player.transform.position - trap.Position;
            toPlayer.y = 0f;
            Vector3 meetPoint = player.transform.position - (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back) * 1.5f;
            baby.PutDown(trap.Position);
            baby.WalkTo(meetPoint, player.transform.position);
            yield return new WaitForSeconds(1.8f);
        }

        int SlimeBlobCount()
        {
            return 2 + level;
        }

        static string RescueVoiceKey(RescueKind kind)
        {
            switch (kind)
            {
                case RescueKind.Rocket: return "aliens.rescue_rocket";
                case RescueKind.Bubble: return "aliens.rescue_bubble";
                case RescueKind.Slime: return "aliens.rescue_slime";
                default: return "aliens.rescue";
            }
        }

        RescueTarget PickBaby()
        {
            if (babies == null) return null;

            var usable = new List<RescueTarget>();
            foreach (RescueTarget baby in babies)
                if (baby != null) usable.Add(baby);

            return usable.Count > 0 ? usable[Random.Range(0, usable.Count)] : null;
        }

        /// <summary>
        /// Picks the rescue for this visit: Tree is twice as likely as the others, never the same kind twice in a row, and
        /// never the same tree twice in a row. A kind whose pieces are missing falls back to a tree.
        /// </summary>
        AlienRescueSite PickSite()
        {
            if (rescueSites == null) return null;

            var byKind = new List<AlienRescueSite>[4];
            for (int i = 0; i < byKind.Length; i++) byKind[i] = new List<AlienRescueSite>();
            foreach (AlienRescueSite site in rescueSites)
                if (site != null && site.IsUsable) byKind[(int)site.Kind].Add(site);

            int kind;
            if (debugForceRescue != DebugRescue.Random)
            {
                kind = (int)debugForceRescue - 1;
            }
            else
            {
                // Weighted pick: Tree 2, the rest 1, skipping the kind of the last visit and kinds with nothing usable.
                int[] weights = { 2, 1, 1, 1 };
                int total = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    if (byKind[i].Count == 0 || i == lastRescueKind) weights[i] = 0;
                    total += weights[i];
                }

                kind = (int)RescueKind.Tree;
                if (total > 0)
                {
                    int roll = Random.Range(0, total);
                    for (int i = 0; i < weights.Length; i++)
                    {
                        if (roll < weights[i])
                        {
                            kind = i;
                            break;
                        }
                        roll -= weights[i];
                    }
                }
            }

            // Missing pieces: fall back to a tree, then to anything that works.
            if (byKind[kind].Count == 0) kind = (int)RescueKind.Tree;
            if (byKind[kind].Count == 0)
            {
                for (int i = 0; i < byKind.Length; i++)
                {
                    if (byKind[i].Count > 0)
                    {
                        kind = i;
                        break;
                    }
                }
            }
            if (byKind[kind].Count == 0) return null;

            List<AlienRescueSite> options = byKind[kind];
            if (kind == (int)RescueKind.Tree && options.Count > 1)
            {
                options = new List<AlienRescueSite>(options);
                options.RemoveAll(site => site.GetInstanceID() == lastTreeSiteId);
            }

            AlienRescueSite chosen = options[Random.Range(0, options.Count)];
            lastRescueKind = kind;
            if (kind == (int)RescueKind.Tree) lastTreeSiteId = chosen.GetInstanceID();
            return chosen;
        }

        /// <summary>True when there is a question to ask, so the baby waits out of the shot until it is answered.</summary>
        bool BabyWaitsForCount => lessonUI != null && babyWaitPoint != null && activeBaby != null;

        /// <summary>The baby runs to the spot after the aliens in the party line and dances.</summary>
        void SendBabyToParty()
        {
            Vector3 spot = PartySpot(visit.Count);
            activeBaby.RunTo(spot, spot + CameraSide * 5f, 1);
        }

        /// <summary>After the counting the baby comes running in from behind the camera and joins the party line.</summary>
        IEnumerator RunBabyJoinsParty()
        {
            if (activeBaby == null || !activeBaby.IsRescued || activeBaby.IsDancing) yield break;

            SendBabyToParty();

            float arriveBy = Time.time + 8f;
            yield return new WaitForSeconds(1.5f);
            while (!activeBaby.IsDancing && Time.time < arriveBy)
                yield return null;
        }

        /// <summary>Sparkles every few metres along the straight line from the player toward the target (the same trail the inviter uses).</summary>
        void ShowTrailTo(Vector3 target, float stopRadius)
        {
            Vector3 start = player.transform.position;
            Vector3 toTarget = target - start;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < stopRadius + 1f) return;

            Vector3 direction = toTarget / distance;
            float end = Mathf.Max(0f, distance - stopRadius);
            float along = 1f;
            for (int i = 0; i < MaxTrailPoints && along <= end; i++)
            {
                BattleEffects.Sparkle(start + direction * along + Vector3.up * TrailHeight);
                along += TrailSpacing;
            }
        }

        // ---- 4. Counting the dancers ----

        IEnumerator RunCount()
        {
            if (lessonUI == null) yield break;

            // The kid stands and listens; the joystick and the button are tucked away.
            if (rayGun != null) rayGun.ControlLocked = true;
            lessonUI.SetGameplayHudVisible(false);
            hudHidden = true;

            // The camera glides in on the dancing aliens (the baby waits behind it) while the question is asked.
            VoicePlayer.Say("aliens.count", true);
            yield return EnterCountShot();
            yield return WaitForVoice();

            int correct = DancerCount();
            pickedCard = -1;
            firstPick = -1;
            lessonUI.ShowNumberCards(MakeCards(correct), OnCardPicked);

            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;

                    // The first card he picks decides the level for next time: right goes up, wrong goes down.
                    if (firstPick < 0)
                    {
                        firstPick = picked;
                        AdaptLevel(picked == correct);
                    }

                    if (picked != correct)
                    {
                        // Never a failure: a friendly wiggle, then we count along together and the right card wins.
                        lessonUI.WiggleCard(picked);
                        lessonUI.DimCard(picked);
                        VoicePlayer.Say("lesson.try_again", true);
                        yield return new WaitForSeconds(1.8f);
                        yield return CountAlong(correct);
                    }

                    lessonUI.CelebrateCard(correct);
                    VoicePlayer.Say("lesson.correct", true);
                    break;
                }
                yield return null;
            }

            yield return new WaitForSeconds(2.2f);

            // Harder numbers (AlienLanding.Numbers.cs): once one more / one less is known, a second little question follows.
            // It does not change the aliens level.
            yield return RunFollowUp(correct);

            lessonUI.HideCards();
            lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;

            // Back to the normal follow camera.
            if (cameraRig != null) yield return cameraRig.ExitLesson();
        }

        /// <summary>Right first time: one level up. Wrong: one level down. Always between 1 and 3 (like the pirates).</summary>
        void AdaptLevel(bool right)
        {
            PlayerProgress progress = ProgressStore.Current;
            int current = Mathf.Clamp(progress.GetLevel(Skill), 1, MaxLevel);
            if (right && current < MaxLevel) progress.SetLevel(Skill, current + 1);
            else if (!right && current > 1) progress.SetLevel(Skill, current - 1);
            ProgressStore.Save();
        }

        /// <summary>
        /// Moves the counting camera so all the dancers fit: centred on them, pulled back until the outer two plus a metre
        /// of margin are inside the horizontal view. Height and pitch stay. The baby waits 3 m behind the camera.
        /// </summary>
        void FrameCountShot()
        {
            if (visit.Count == 0) return;

            Vector3 first = PartySpot(0);
            Vector3 last = PartySpot(visit.Count - 1);
            Vector3 centre = (first + last) * 0.5f;
            float spread = Mathf.Abs(Vector3.Dot(last - first, Vector3.right));

            Camera cam = Camera.main;
            float verticalFov = cam != null ? cam.fieldOfView : 50f;
            float aspect = cam != null && cam.aspect > 1f ? cam.aspect : 16f / 9f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect);

            float halfWidth = spread * 0.5f + CountCameraMargin;
            float back = Mathf.Max(CountCameraMinBack, halfWidth / Mathf.Tan(halfHorizontal));

            Vector3 cameraPosition = centre + CameraSide * back;
            cameraPosition.y = centre.y + CountCameraHeight;
            if (countCameraPoint != null)
                countCameraPoint.SetPositionAndRotation(cameraPosition, Quaternion.Euler(CountCameraPitch, 0f, 0f));

            if (babyWaitPoint != null)
            {
                Vector3 wait = centre + CameraSide * (back + BabyWaitBehindCamera);
                if (NavMesh.SamplePosition(wait, out NavMeshHit hit, 4f, NavMesh.AllAreas)) wait = hit.position;
                babyWaitPoint.position = wait;
            }
        }

        /// <summary>Glides the camera to the close-up of the aliens. Does nothing when there is no shot placed.</summary>
        IEnumerator EnterCountShot()
        {
            if (countCameraPoint == null) yield break;

            if (cameraRig == null)
            {
                cameraRig = GetComponent<LessonCameraRig>();
                if (cameraRig == null) cameraRig = gameObject.AddComponent<LessonCameraRig>();
            }
            yield return cameraRig.EnterLesson(countCameraPoint);
        }

        void OnCardPicked(int number)
        {
            pickedCard = number;
        }

        /// <summary>Each alien hops while its number is said out loud. The baby is not part of the count.</summary>
        IEnumerator CountAlong(int count)
        {
            for (int k = 1; k <= count; k++)
            {
                HopDancer(k - 1);
                VoicePlayer.Say("number." + k, true);
                yield return new WaitForSeconds(1.1f);
            }
        }

        void HopDancer(int index)
        {
            if (index < visit.Count && visit[index] != null)
                visit[index].Hop();
        }

        /// <summary>The number of dancing aliens in the shot. The baby is out of the shot and does not count.</summary>
        int DancerCount()
        {
            int count = 0;
            foreach (AlienKidAI alien in visit)
                if (alien != null) count++;
            return Mathf.Max(1, count);
        }

        /// <summary>The right answer and two other numbers, shuffled. All between 1 and 20.</summary>
        static int[] MakeCards(int correct)
        {
            var cards = new List<int> { correct };
            int[] offsets = { -1, 1, 2, -2, 3, -3 };
            foreach (int offset in offsets)
            {
                int candidate = correct + offset;
                if (candidate < 1 || candidate > NumberCurriculum.HighestNumber || cards.Contains(candidate)) continue;

                cards.Add(candidate);
                if (cards.Count == 3) break;
            }

            Shuffle(cards);
            return cards.ToArray();
        }

        // ---- 5. Party and take-off ----

        IEnumerator RunParty()
        {
            if (rayGun != null) rayGun.ControlLocked = true;
            if (player != null) player.InputEnabled = false;

            // The superhero dances too, facing the camera.
            var kid = player != null ? player.GetComponent<KidAnimator>() : null;
            if (kid != null)
            {
                player.transform.rotation = Quaternion.LookRotation(CameraSide, Vector3.up);
                kid.SetDancing(true, 1);
                playerDancing = true;
            }

            VoicePlayer.Say("aliens.victory", true);
            Sfx.Play("aliens.dance_party", PlayerPosition());

            // Confetti over the dancing crowd.
            for (float elapsed = 0f; elapsed < partySeconds; elapsed += 0.6f)
            {
                PartyConfetti();
                yield return new WaitForSeconds(0.6f);
            }

            // Once counting backwards is known, the rocket counts down first (AlienLanding.Numbers.cs).
            yield return RunCountdown();

            // The stars come after the countdown, so they never fly (or open a present) in the middle of it.
            onVictory?.Invoke();

            // Wave goodbye, then the rocket lifts off and the aliens vanish one by one in a sparkle.
            foreach (AlienKidAI alien in visit)
                if (alien != null) alien.Wave();
            if (activeBaby != null) activeBaby.Wave();
            if (kid != null)
            {
                kid.SetDancing(false);
                kid.PlayCheer();
                playerDancing = false;
            }
            yield return new WaitForSeconds(1.2f);

            Sfx.Play("aliens.rocket_takeoff", RocketLandingPosition());
            rocketRoutine = StartCoroutine(TakeOffRoutine());

            yield return new WaitForSeconds(0.5f);
            foreach (AlienKidAI alien in visit)
            {
                if (alien == null) continue;

                alien.Vanish();
                yield return new WaitForSeconds(0.25f);
            }
            if (activeBaby != null) activeBaby.Vanish();

            while (rocketRoutine != null)
                yield return null;
        }

        void PartyConfetti()
        {
            if (player != null) BattleEffects.Confetti(player.transform.position + Vector3.up * 2f);

            foreach (AlienKidAI alien in visit)
                if (alien != null && alien.isActiveAndEnabled) BattleEffects.Confetti(alien.transform.position + Vector3.up * 2f);
            if (activeBaby != null && activeBaby.isActiveAndEnabled) BattleEffects.Confetti(activeBaby.transform.position + Vector3.up * 1.5f);
        }

        IEnumerator TakeOffRoutine()
        {
            if (rocket != null)
            {
                Vector3 from = rocket.position;
                Vector3 sky = rocketSkyPoint != null ? rocketSkyPoint.position : from + Vector3.up * 25f;
                float nextSparkle = 0f;

                for (float elapsed = 0f; elapsed < takeoffSeconds; elapsed += Time.deltaTime)
                {
                    // Slow at first, then faster and faster.
                    float t = Mathf.Pow(elapsed / takeoffSeconds, 2.2f);
                    rocket.position = Vector3.Lerp(from, sky, t);

                    if (Time.time >= nextSparkle)
                    {
                        nextSparkle = Time.time + 0.15f;
                        RocketSparkle();
                    }
                    yield return null;
                }
                rocket.gameObject.SetActive(false);
            }

            rocketRoutine = null;
        }

        // ----- Putting things back ------------------------------------------------------------------

        /// <summary>
        /// Everybody back to where they were before the game: aliens (kind, size, tint) and babies reset, every rescue site
        /// reset (highlights out, rocket trampoline hidden, bubble and slime gone), covers free, rocket hidden in the sky.
        /// </summary>
        void ResetEverything()
        {
            if (aliens != null)
                foreach (AlienKidAI alien in aliens)
                    if (alien != null) alien.ResetAlien();

            // The babies first: one may still sit in a bubble or on the rocket, and must be free before its seat goes away.
            if (babies != null)
                foreach (RescueTarget baby in babies)
                    if (baby != null) baby.ResetTarget();

            if (rescueSites != null)
                foreach (AlienRescueSite site in rescueSites)
                    if (site != null) site.ResetSite();

            if (covers != null)
                foreach (AlienCover cover in covers)
                    if (cover != null) cover.Release();

            visit.Clear();
            visitKinds.Clear();
            chosenSpawns.Clear();
            activeBaby = null;
            activeSite = null;

            if (player != null)
            {
                var hittable = player.GetComponent<Hittable>();
                if (hittable != null) hittable.ResetState();
            }

            if (rocket != null)
            {
                if (rocketSkyPoint != null) rocket.position = rocketSkyPoint.position;
                rocket.gameObject.SetActive(false);
            }
        }

        void SetCostume(bool on)
        {
            if (player == null) return;

            var costume = player.GetComponent<CostumeHolder>();
            if (costume != null) costume.ShowCostume(on);
        }

        // ----- Helpers ---------------------------------------------------------------------------------

        Vector3 RocketLandingPosition()
        {
            return rocketLandingPoint != null ? rocketLandingPoint.position : transform.position;
        }

        /// <summary>Round-robin over the 2-3 spots chosen for this visit.</summary>
        Vector3 SpawnPosition(int index)
        {
            if (chosenSpawns.Count > 0)
            {
                Transform point = chosenSpawns[index % chosenSpawns.Count];
                if (point != null) return point.position;
            }
            return RocketLandingPosition();
        }

        /// <summary>
        /// The party line point for this dancer: the aliens are a centred slice of the line (so the group is always around
        /// the middle), and the baby takes the spot right after them.
        /// </summary>
        Vector3 PartySpot(int index)
        {
            if (partyLinePoints != null && partyLinePoints.Length > 0)
            {
                Transform point = partyLinePoints[Mathf.Clamp(slotStart + index, 0, partyLinePoints.Length - 1)];
                if (point != null) return point.position;
            }
            return RocketLandingPosition() + CameraSide * (3f + index);
        }

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                T held = list[i];
                list[i] = list[swap];
                list[swap] = held;
            }
        }

        /// <summary>Waits for whoever is talking to finish, but never for long.</summary>
        IEnumerator WaitForVoice()
        {
            yield return new WaitForSeconds(0.3f);

            float giveUp = Time.time + MaxVoiceWaitSeconds;
            while (VoicePlayer.Instance != null && VoicePlayer.Instance.IsSpeaking && Time.time < giveUp)
                yield return null;
        }
    }
}
