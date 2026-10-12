using System;
using System.Collections;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Characters;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace CoolSchool.Aliens
{
    /// <summary>
    /// The harder-numbers part of the alien landing (contract: Docs/specs/harder-numbers-contract.md, Agent C).
    /// Once the classroom has taught "one more, one less" a second little question follows the dancers' count: either the baby
    /// alien walks into the shot (one more) or the last dancer runs off to the rocket (one less). Once counting backwards is
    /// known, the rocket counts down before the lift-off. Neither part changes the aliens level, and the kid can never fail.
    /// Everything runs inside the flow coroutine, so the back button (OnEnd) stops it, hides the cards and restores the HUD.
    /// </summary>
    public partial class AlienLanding
    {
        const float FollowUpPulseSeconds = 12f;
        const float FollowUpSolveSeconds = 26f;
        const float CountdownStepSeconds = 1.6f;
        const float CountdownMinTapGap = 0.7f;
        const int CountdownStart = 10;
        const int CountdownStartLevel9 = 20;
        const float LeaverSpeed = 3.4f;
        const float LeaverMaxRunSeconds = 3.5f;
        const float BabyJoinsMaxSeconds = 8f;

        // ----- The follow-up after the count: one more (the baby) or one less (a dancer runs off) -----

        /// <summary>Called after the dancers' count was answered. Picks "one more" or "one less" 50/50, whichever is possible.</summary>
        IEnumerator RunFollowUp(int dancers)
        {
            if (lessonUI == null || !NumberCurriculum.IsUnlocked(NumberSkill.OneMoreOneLess)) yield break;

            bool canMore = CanBabyJoinCount(dancers);
            bool canLess = CanOneLeave(dancers);
            if (!canMore && !canLess) yield break;

            bool more = canMore && (!canLess || Random.value < 0.5f);
            if (more) yield return BabyJoinsCount(dancers);
            else yield return OneLeavesCount(dancers);
        }

        /// <summary>The baby is rescued and waiting out of the shot, and one more still fits in the line and on the cards.</summary>
        bool CanBabyJoinCount(int dancers)
        {
            return BabyWaitsForCount && activeBaby.isActiveAndEnabled && activeBaby.IsRescued && activeBaby.HasArrived
                && !activeBaby.IsDancing && dancers + 1 <= NumberCurriculum.HighestNumber;
        }

        /// <summary>There are at least three dancers, and the last one is dancing in the line.</summary>
        bool CanOneLeave(int dancers)
        {
            if (dancers < 3 || visit.Count < 3) return false;

            AlienKidAI last = visit[visit.Count - 1];
            return last != null && last.isActiveAndEnabled && last.IsDancing;
        }

        // ---- One more: the baby walks into the shot and dances ----

        IEnumerator BabyJoinsCount(int dancers)
        {
            VoicePlayer.Say("aliens.baby_joins", true);

            // The baby runs to the spot next to the last dancer while the camera pulls back a little to fit one more.
            SendBabyToParty();
            yield return GlideCameraTo(CountShotPosition(visit.Count), 0.9f);

            float arriveBy = Time.time + BabyJoinsMaxSeconds;
            while (!activeBaby.IsDancing && Time.time < arriveBy)
                yield return null;
            yield return WaitForVoice();

            yield return AskFollowUp(dancers + 1, () => CountAlongWithBaby(dancers));
        }

        /// <summary>The dancers hop one by one while their numbers are said, then the baby hops and is the last number.</summary>
        IEnumerator CountAlongWithBaby(int dancers)
        {
            yield return CountAlong(dancers);

            if (activeBaby != null) activeBaby.Hop();
            VoicePlayer.Say("number." + (dancers + 1), true);
            yield return new WaitForSeconds(1.1f);
        }

        // ---- One less: the last dancer runs to the rocket ----

        IEnumerator OneLeavesCount(int dancers)
        {
            AlienKidAI leaver = visit[visit.Count - 1];
            VoicePlayer.Say("aliens.one_leaves", true);

            yield return LeaverRunsOff(leaver);
            yield return WaitForVoice();

            yield return AskFollowUp(dancers - 1, () => CountAlong(dancers - 1));

            yield return LeaverComesBack(leaver, visit.Count - 1);
        }

        /// <summary>The dancer stops dancing and runs toward the rocket, then pops away in a sparkle.</summary>
        IEnumerator LeaverRunsOff(AlienKidAI leaver)
        {
            var kid = leaver.GetComponent<KidAnimator>();
            if (kid != null) kid.SetDancing(false);

            var agent = leaver.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                Vector3 target = RocketLandingPosition();
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 8f, NavMesh.AllAreas)) target = hit.position;

                agent.isStopped = false;
                agent.updateRotation = true;
                agent.speed = LeaverSpeed;
                agent.SetDestination(target);

                float giveUp = Time.time + LeaverMaxRunSeconds;
                yield return null;
                while (Time.time < giveUp && (agent.pathPending || agent.remainingDistance > 2.5f))
                    yield return null;

                agent.isStopped = true;
                agent.updateRotation = false;
            }
            else
            {
                leaver.Hop();
                yield return new WaitForSeconds(0.9f);
            }

            BattleEffects.Sparkle(leaver.transform.position + Vector3.up * 1f);
            BattleEffects.Sparkle(leaver.transform.position + Vector3.up * 0.4f);
            leaver.gameObject.SetActive(false);
        }

        /// <summary>It pops back into its place in the line with a sparkle and dances again (Hop puts its own dance back).</summary>
        IEnumerator LeaverComesBack(AlienKidAI leaver, int index)
        {
            Vector3 spot = PartySpot(index);
            leaver.transform.position = spot;
            leaver.transform.rotation = Quaternion.LookRotation(CameraSide, Vector3.up);
            leaver.gameObject.SetActive(true);

            var agent = leaver.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.Warp(spot);
                agent.isStopped = true;
                agent.updateRotation = false;
            }
            leaver.transform.rotation = Quaternion.LookRotation(CameraSide, Vector3.up);

            BattleEffects.Sparkle(spot + Vector3.up * 1f);
            BattleEffects.Confetti(spot + Vector3.up * 1.5f);
            leaver.Hop();
            yield return new WaitForSeconds(1.2f);
        }

        // ---- The question itself ----

        /// <summary>
        /// Number cards for the answer. A wrong card wiggles and dims, we count along and the right card wins; a quiet child
        /// is shown the right card after 12 s and helped after 26 s. It never touches the aliens level.
        /// </summary>
        IEnumerator AskFollowUp(int answer, Func<IEnumerator> countAlong)
        {
            pickedCard = -1;
            lessonUI.ShowNumberCards(MakeCards(answer), OnCardPicked);
            float shownAt = Time.time;
            bool pulsing = false;

            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;
                    if (picked == answer) break;

                    lessonUI.WiggleCard(picked);
                    lessonUI.DimCard(picked);
                    VoicePlayer.Say("lesson.try_again", true);
                    yield return new WaitForSeconds(1.8f);
                    yield return countAlong();
                    break;
                }

                if (!pulsing && Time.time - shownAt >= FollowUpPulseSeconds)
                {
                    pulsing = true;
                    lessonUI.PulseCard(answer);
                }

                if (Time.time - shownAt >= FollowUpSolveSeconds)
                {
                    yield return countAlong();
                    break;
                }

                yield return null;
            }

            lessonUI.StopPulse();
            lessonUI.CelebrateCard(answer);
            VoicePlayer.Say("lesson.correct", true);
            yield return new WaitForSeconds(2.2f);
            pickedCard = -1;
        }

        /// <summary>Where the count camera stands to fit the dancers from the first spot to the given one (same framing rules as FrameCountShot).</summary>
        Vector3 CountShotPosition(int lastIndex)
        {
            Vector3 first = PartySpot(0);
            Vector3 last = PartySpot(lastIndex);
            Vector3 centre = (first + last) * 0.5f;
            float spread = Mathf.Abs(Vector3.Dot(last - first, Vector3.right));

            Camera cam = Camera.main;
            float verticalFov = cam != null ? cam.fieldOfView : 50f;
            float aspect = cam != null && cam.aspect > 1f ? cam.aspect : 16f / 9f;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect);

            float halfWidth = spread * 0.5f + CountCameraMargin;
            float back = Mathf.Max(CountCameraMinBack, halfWidth / Mathf.Tan(halfHorizontal));

            Vector3 position = centre + CameraSide * back;
            position.y = centre.y + CountCameraHeight;
            return position;
        }

        /// <summary>Slides the (lesson-controlled) camera sideways or back to a new position. Skipped when the camera is not in the close-up.</summary>
        IEnumerator GlideCameraTo(Vector3 position, float seconds)
        {
            Camera cam = Camera.main;
            if (cam == null || cameraRig == null || !cameraRig.InLesson) yield break;

            Transform t = cam.transform;
            Vector3 from = t.position;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, position, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
                yield return null;
            }

            t.position = position;
            if (countCameraPoint != null) countCameraPoint.position = position;
        }

        // ----- The rocket counts down before lift-off -----

        /// <summary>
        /// "Tel mee terug": one big card in the middle, from 10 (or 20 at numbers level 9) down to 0. A tap or 1.6 s goes to the
        /// next number, which is said out loud. Then number.0, "Lancering!" and the take-off follows in RunParty.
        /// </summary>
        IEnumerator RunCountdown()
        {
            if (lessonUI == null || !NumberCurriculum.IsUnlocked(NumberSkill.CountBack)) yield break;

            int k = NumberCurriculum.CurrentLevel() >= NumberCurriculum.MaxLevel ? CountdownStartLevel9 : CountdownStart;

            // Joystick and button are tucked away while the card shows (OnEnd puts them back if the game stops here).
            lessonUI.SetGameplayHudVisible(false);
            hudHidden = true;

            VoicePlayer.Say("aliens.countdown", true);
            pickedCard = -1;
            lessonUI.ShowNumberCards(new[] { k }, OnCardPicked);
            yield return WaitForVoice();

            VoicePlayer.Say("number." + k, true);
            RocketSparkle();
            float shownAt = Time.time;
            pickedCard = -1;

            while (k > 0)
            {
                bool advance = false;
                float sinceShown = Time.time - shownAt;
                if (pickedCard >= 0)
                {
                    pickedCard = -1;
                    // Taps closer together than this are ignored, so a drumming finger does not skip numbers.
                    if (sinceShown >= CountdownMinTapGap) advance = true;
                }
                else if (sinceShown >= CountdownStepSeconds)
                {
                    advance = true;
                }

                if (advance)
                {
                    k--;
                    VoicePlayer.Say("number." + k, true);
                    RocketSparkle();
                    if (k > 0)
                    {
                        lessonUI.ShowNumberCards(new[] { k }, OnCardPicked);
                        shownAt = Time.time;
                    }
                }
                yield return null;
            }

            lessonUI.HideCards();
            yield return new WaitForSeconds(0.9f);
            lessonUI.SetGameplayHudVisible(true);
            hudHidden = false;

            for (int i = 0; i < 4; i++) RocketSparkle();
            VoicePlayer.Say("aliens.liftoff");
        }
    }
}
