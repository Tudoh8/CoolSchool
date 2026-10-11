using System.Collections;
using CoolSchool.Activities;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Input;
using CoolSchool.Lessons;
using CoolSchool.Player;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.Events;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// "Alien landing". A cardboard rocket lands west of the playground and silly alien kids walk out. The player is a
    /// superhero with a cape, a mask and a ray gun: a zapped alien is not hurt, it turns into a friend and dances.
    /// A baby alien is stuck up in a tree, so the player jumps on the glowing pad, flies up and brings it down. Then
    /// the dancing friends are counted, there is a dance party, and the rocket takes off.
    /// The kid can never fail: every step has a hint and a safety net. Everything that was changed is put back in OnEnd.
    /// </summary>
    public class AlienLanding : Activity
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
        [SerializeField] Transform[] alienSpawnPoints;
        [SerializeField] AlienKidAI[] aliens;
        [SerializeField] RescueTarget babyAlien;
        [Tooltip("Where the baby sits in the tree (its rotation is the way it faces).")]
        [SerializeField] Transform treePerchPoint;
        [SerializeField] JumpPad jumpPad;
        [Tooltip("The party line in front of the camera: one point for each alien, then one for the baby.")]
        [SerializeField] Transform[] partyLinePoints;

        [Tooltip("Where the baby waits while the aliens are counted: behind the counting camera, so it is never in the shot.")]
        [SerializeField] Transform babyWaitPoint;

        [Header("Counting (skipped when there is no lesson UI)")]
        [SerializeField] LessonUI lessonUI;
        [Tooltip("The close-up shot of the dancing aliens during the question (the baby is not in it). Optional.")]
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

        [Header("Hooks for voice lines and sounds")]
        public UnityEvent onRescue;
        public UnityEvent onVictory;

        // The camera is south of the player, looking north, so the party line faces south.
        static readonly Vector3 CameraSide = Vector3.back;

        const float AlienGapSeconds = 1.2f;
        const float FirstSlimeDelaySeconds = 3f;
        const float SlimeRangeMeters = 14f;
        const float HintRepeatSeconds = 15f;
        // Safety nets, so the kid is never stuck: slow zapping turns the rest of the aliens friendly, a long rescue puts the baby down.
        const float WaveTimeoutSeconds = 150f;
        const float RescueTimeoutSeconds = 120f;
        const float FloatFallSpeed = 1.6f;
        const float TrailSpacing = 1.5f;
        const float TrailRefreshSeconds = 0.7f;
        const float TrailHeight = 0.3f;
        const int MaxTrailPoints = 16;
        const float MaxVoiceWaitSeconds = 8f;

        Coroutine flowRoutine;
        Coroutine rocketRoutine;
        bool quitting;
        bool hudHidden;
        bool playerDancing;
        float savedMaxFallSpeed = 100f;
        int pickedCard = -1;

        protected override void OnBegin()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();

            if (player != null)
            {
                savedMaxFallSpeed = player.MaxFallSpeed;
                player.JumpEnabled = false;
            }

            ResetEverything();

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

        // ---- 2. The aliens come out, and get zapped into friends ----

        IEnumerator RunWave()
        {
            int total = aliens != null ? aliens.Length : 0;
            int spawned = 0;
            float nextSpawn = Time.time;
            float waveStart = Time.time;
            float firstAlienTime = -1f;
            float nextSlime = float.MaxValue;
            float nextHint = float.MaxValue;
            bool warned = false;

            while (true)
            {
                if (spawned < total && Time.time >= nextSpawn)
                {
                    SpawnAlien(spawned);
                    spawned++;
                    nextSpawn = Time.time + AlienGapSeconds;

                    if (firstAlienTime < 0f)
                    {
                        firstAlienTime = Time.time;
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

                // Safety net: nobody is ever stuck. After a very long time the rest of the aliens turn friendly by themselves.
                if (Time.time - waveStart > WaveTimeoutSeconds)
                {
                    for (int i = spawned; i < total; i++) SpawnAlien(i);
                    spawned = total;
                    foreach (AlienKidAI alien in aliens)
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
            AlienKidAI alien = aliens[index];
            if (alien == null) return;

            Vector3 spawn = SpawnPosition(index);
            alien.Activate(spawn, player != null ? player.transform : null, PartySpot(index), PartySpot(index) + CameraSide * 5f, index);
            BattleEffects.Sparkle(spawn + Vector3.up * 0.8f);
        }

        /// <summary>Picks one alien in range that is not busy and lets it throw. Returns false if nobody could.</summary>
        bool ThrowSlime()
        {
            if (player == null || aliens == null) return false;

            int candidates = 0;
            AlienKidAI chosen = null;
            foreach (AlienKidAI alien in aliens)
            {
                if (alien == null || !alien.isActiveAndEnabled || alien.IsFriend || alien.IsThrowing) continue;

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
            if (aliens == null) return true;

            foreach (AlienKidAI alien in aliens)
                if (alien != null && !alien.IsFriend) return false;
            return true;
        }

        bool AllDancing()
        {
            if (aliens == null) return true;

            foreach (AlienKidAI alien in aliens)
                if (alien != null && !alien.IsDancing) return false;
            return true;
        }

        // ---- 3. The baby in the tree ----

        IEnumerator RunRescue()
        {
            if (babyAlien == null || treePerchPoint == null || player == null) yield break;

            babyAlien.ShowAt(treePerchPoint);
            if (jumpPad != null) jumpPad.SetLit(true);
            VoicePlayer.Say("aliens.rescue");

            float start = Time.time;
            float nextTrail = 0f;
            float nextHint = Time.time + 25f;
            float nextSparkle = 0f;

            while (!babyAlien.IsCarried)
            {
                // In the air near the baby: it jumps into his arms.
                if (!player.IsGrounded) babyAlien.TryGrab(player.transform);

                if (jumpPad != null && player.IsGrounded && Time.time >= nextTrail)
                {
                    nextTrail = Time.time + TrailRefreshSeconds;
                    ShowTrailTo(jumpPad.Position, jumpPad.Radius);
                }

                if (Time.time >= nextSparkle)
                {
                    nextSparkle = Time.time + 1f;
                    BattleEffects.Sparkle(babyAlien.transform.position + Vector3.up * 1.2f);
                }

                if (Time.time >= nextHint)
                {
                    nextHint = Time.time + 25f;
                    VoicePlayer.Say("aliens.rescue");
                }

                // Safety net: the baby climbs down by itself after a very long time.
                if (Time.time - start > RescueTimeoutSeconds) break;

                yield return null;
            }

            if (jumpPad != null) jumpPad.SetLit(false);
            Sfx.Play("aliens.rescue", babyAlien.transform.position);

            if (babyAlien.IsCarried)
            {
                // Hang for a moment at the top, then float down together like a parachute.
                player.SetVerticalVelocity(Mathf.Min(player.VerticalVelocity, 1f));
                player.MaxFallSpeed = FloatFallSpeed;

                float floatStart = Time.time;
                float nextTwinkle = 0f;
                while ((!player.IsGrounded || Time.time - floatStart < 0.4f) && Time.time - floatStart < 12f)
                {
                    if (Time.time >= nextTwinkle)
                    {
                        nextTwinkle = Time.time + 0.2f;
                        BattleEffects.Sparkle(player.transform.position + Vector3.up * Random.Range(0.3f, 1.5f));
                    }
                    yield return null;
                }

                player.MaxFallSpeed = savedMaxFallSpeed;
                babyAlien.PutDown(player.transform.position + player.transform.forward * 1f);
            }
            else
            {
                Vector3 ground = jumpPad != null ? jumpPad.Position : treePerchPoint.position;
                ground.y = 0f;
                babyAlien.PutDown(ground + Vector3.back * 1.5f);
            }

            VoicePlayer.Say("aliens.rescued", true);
            BattleEffects.Confetti(player.transform.position + Vector3.up * 1.5f);
            BattleEffects.Confetti(babyAlien.transform.position + Vector3.up * 1f);
            onRescue?.Invoke();

            float arriveBy = Time.time + 8f;
            if (BabyWaitsForCount)
            {
                // The question is only about the aliens: the baby watches from behind the camera, out of the shot,
                // and joins the dance party after the counting.
                babyAlien.WalkTo(babyWaitPoint.position, PartySpot(0));
                yield return new WaitForSeconds(1.5f);
                while (!babyAlien.HasArrived && Time.time < arriveBy)
                    yield return null;
            }
            else
            {
                // Nothing to count: straight to its place in the party line.
                SendBabyToParty();
                yield return new WaitForSeconds(1.5f);
                while (!babyAlien.IsDancing && Time.time < arriveBy)
                    yield return null;
            }
        }

        /// <summary>True when there is a question to ask, so the baby waits out of the shot until it is answered.</summary>
        bool BabyWaitsForCount => lessonUI != null && babyWaitPoint != null;

        /// <summary>The baby runs to the last party slot (the one after the aliens) and dances.</summary>
        void SendBabyToParty()
        {
            int babySlot = aliens != null ? aliens.Length : 0;
            babyAlien.RunTo(PartySpot(babySlot), PartySpot(babySlot) + CameraSide * 5f, 1);
        }

        /// <summary>After the counting the baby comes running in from behind the camera and joins the party line.</summary>
        IEnumerator RunBabyJoinsParty()
        {
            if (babyAlien == null || !babyAlien.IsRescued || babyAlien.IsDancing) yield break;

            SendBabyToParty();

            float arriveBy = Time.time + 8f;
            yield return new WaitForSeconds(1.5f);
            while (!babyAlien.IsDancing && Time.time < arriveBy)
                yield return null;
        }

        /// <summary>Sparkles every few metres along the straight line from the player toward the pad (the same trail the inviter uses).</summary>
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
            lessonUI.ShowNumberCards(MakeCards(correct), OnCardPicked);

            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;

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
            lessonUI.HideCards();
            lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;

            // Back to the normal follow camera.
            if (cameraRig != null) yield return cameraRig.ExitLesson();
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
            if (aliens != null && index < aliens.Length && aliens[index] != null)
                aliens[index].Hop();
        }

        /// <summary>The number of dancing aliens in the shot. The baby is out of the shot and does not count.</summary>
        int DancerCount()
        {
            int count = 0;
            if (aliens != null)
                foreach (AlienKidAI alien in aliens)
                    if (alien != null) count++;
            return Mathf.Max(1, count);
        }

        /// <summary>The right answer and two other numbers, shuffled. All between 1 and 9.</summary>
        static int[] MakeCards(int correct)
        {
            var cards = new System.Collections.Generic.List<int> { correct };
            int[] offsets = { -1, 1, 2, -2, 3, -3 };
            foreach (int offset in offsets)
            {
                int candidate = correct + offset;
                if (candidate < 1 || candidate > 9 || cards.Contains(candidate)) continue;

                cards.Add(candidate);
                if (cards.Count == 3) break;
            }

            for (int i = cards.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                int held = cards[i];
                cards[i] = cards[swap];
                cards[swap] = held;
            }
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
            onVictory?.Invoke();

            // Confetti over the dancing crowd.
            for (float elapsed = 0f; elapsed < partySeconds; elapsed += 0.6f)
            {
                PartyConfetti();
                yield return new WaitForSeconds(0.6f);
            }

            // Wave goodbye, then the rocket lifts off and the aliens vanish one by one in a sparkle.
            if (aliens != null)
                foreach (AlienKidAI alien in aliens)
                    if (alien != null) alien.Wave();
            if (babyAlien != null) babyAlien.Wave();
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
            if (aliens != null)
            {
                foreach (AlienKidAI alien in aliens)
                {
                    if (alien == null) continue;

                    alien.Vanish();
                    yield return new WaitForSeconds(0.25f);
                }
            }
            if (babyAlien != null) babyAlien.Vanish();

            while (rocketRoutine != null)
                yield return null;
        }

        void PartyConfetti()
        {
            if (player != null) BattleEffects.Confetti(player.transform.position + Vector3.up * 2f);

            if (aliens != null)
                foreach (AlienKidAI alien in aliens)
                    if (alien != null && alien.isActiveAndEnabled) BattleEffects.Confetti(alien.transform.position + Vector3.up * 2f);
            if (babyAlien != null && babyAlien.isActiveAndEnabled) BattleEffects.Confetti(babyAlien.transform.position + Vector3.up * 1.5f);
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

        /// <summary>Everybody back to where they were before the game: aliens and baby off, pad out, rocket hidden in the sky.</summary>
        void ResetEverything()
        {
            if (aliens != null)
                foreach (AlienKidAI alien in aliens)
                    if (alien != null) alien.ResetAlien();

            if (babyAlien != null) babyAlien.ResetTarget();
            if (jumpPad != null) jumpPad.SetLit(false);

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

        Vector3 SpawnPosition(int index)
        {
            if (alienSpawnPoints != null && alienSpawnPoints.Length > 0)
            {
                Transform point = alienSpawnPoints[index % alienSpawnPoints.Length];
                if (point != null) return point.position;
            }
            return RocketLandingPosition();
        }

        /// <summary>The party line point for this dancer (aliens first, then the baby).</summary>
        Vector3 PartySpot(int index)
        {
            if (partyLinePoints != null && partyLinePoints.Length > 0)
            {
                Transform point = partyLinePoints[Mathf.Min(index, partyLinePoints.Length - 1)];
                if (point != null) return point.position;
            }
            return RocketLandingPosition() + CameraSide * (3f + index);
        }

        Vector3 PlayerPosition()
        {
            return player != null ? player.transform.position : transform.position;
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
