using System.Collections;
using System.Collections.Generic;
using CoolSchool.Audio;
using CoolSchool.Battle;
using CoolSchool.Lessons;
using CoolSchool.Voice;
using UnityEngine;

namespace CoolSchool.Zoo
{
    /// <summary>The first half of the game: counting with his eyes closed, searching the hiding spots, the hints and the "how many are left" question.</summary>
    public partial class HideAndSeekGame
    {
        const float SpotRevealPause = 0.7f;
        const float TrailRepeatSeconds = 12f;
        const float AutoFindExtraSeconds = 90f;
        const float QuestionReminderSeconds = 15f;
        const float QuestionGiveUpSeconds = 45f;

        // Hints: the time of his last find (or of the eyes opening) and how far the hints have got.
        float lastFindTime;
        int hintStage;
        float nextTrailTime;
        bool leftAsked;
        Coroutine hintRoutine;
        int pickedCard = -1;

        void ResetSeekState()
        {
            hintStage = 0;
            leftAsked = false;
            hintRoutine = null;
            pickedCard = -1;
        }

        // ----- 2. Counting with his eyes closed ----------------------------------------------------

        IEnumerator RunCount()
        {
            Say("zoo.close_eyes", true);
            FreezePlayer(true);
            if (player != null) player.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // The keeper stands by the base tree and faces it, with her back to the camera.
            if (keeper != null)
            {
                if (countPoint != null) keeper.CountAt(countPoint.position, null, 5f);
                else keeper.FaceNorth();
            }

            yield return new WaitForSeconds(1.2f);
            if (eyes != null) eyes.Close();
            yield return new WaitForSeconds(0.45f);

            // While he cannot see: every animal runs off to its hiding place.
            foreach (Hider hider in hiders)
            {
                hider.spot.Occupy(hider.animal);
                Sfx.Play("zoo.giggle", hider.spot.CoverBounds.center);
            }
            Sfx.Play("zoo.patter");
            yield return WaitForVoice(6f);

            int countTo = SeekCountTo;
            for (int k = 1; k <= countTo; k++)
            {
                if (eyes != null) eyes.ShowDigit(k);
                Say("number." + k, true);
                if (k % 3 == 0) Sfx.Play("zoo.patter");
                yield return new WaitForSeconds(countStepSeconds);
            }

            Say("zoo.ready", true);
            yield return new WaitForSeconds(0.5f);
            if (eyes != null) eyes.Open();
            yield return new WaitForSeconds(0.45f);
            yield return WaitForVoice(5f);

            FreezePlayer(false);
        }

        // ----- 3. Searching -----------------------------------------------------------------------

        IEnumerator RunSeek()
        {
            if (conga != null)
            {
                conga.Paused = false;
                conga.Leader = player.transform;
            }
            if (keeper != null) keeper.Follow(player.transform, 3f);

            lastFindTime = Time.time;
            hintStage = 0;
            nextTrailTime = 0f;
            ScheduleHiders();

            if (debugSkipToHideTurn)
            {
                yield return FindEverythingAtOnce();
                yield break;
            }

            while (RemainingCount() > 0)
            {
                Vector3 position = PlayerPosition();

                HidingSpot near = FindSpotToCheck(position);
                if (near != null)
                {
                    yield return CheckSpot(near);
                    continue;
                }

                UpdatePeeksAndCalls(position);
                Hider autoFind = UpdateHints(position);
                if (autoFind != null)
                {
                    // Nobody found it for a very long time: it comes out by itself.
                    yield return FoundHider(autoFind);
                    continue;
                }
                yield return null;
            }

            StopHintRoutine();
            if (trail != null) trail.Stop();
            Say("zoo.all_found", true);
            if (checklist != null) checklist.Celebrate();
            if (keeper != null) keeper.PlayCheer();
            yield return new WaitForSeconds(0.5f);
            yield return WaitForVoice(6f);
        }

        int RemainingCount()
        {
            int remaining = 0;
            foreach (Hider hider in hiders)
                if (!hider.found) remaining++;
            return remaining;
        }

        /// <summary>The first spot nobody has looked in yet that the child is close enough to (to its edge, not its middle).</summary>
        HidingSpot FindSpotToCheck(Vector3 position)
        {
            foreach (HidingSpot spot in spots)
                if (spot != null && !spot.Checked && spot.DistanceTo(position) <= spot.CheckRadius) return spot;
            return null;
        }

        Hider HiderAt(HidingSpot spot)
        {
            foreach (Hider hider in hiders)
                if (!hider.found && hider.spot == spot) return hider;
            return null;
        }

        IEnumerator CheckSpot(HidingSpot spot)
        {
            Hider hider = HiderAt(spot);
            if (hider != null) yield return FoundHider(hider);
            else yield return EmptySpot(spot);
        }

        /// <summary>A hider is found: it pops out, the checklist ticks it off, it joins the conga line, and he hears how many are left.</summary>
        IEnumerator FoundHider(Hider hider)
        {
            StopHintRoutine();
            if (trail != null) trail.Stop();

            ZooAnimal animal = hider.spot.Reveal();
            if (animal == null) animal = hider.animal;
            hider.found = true;
            foundOrder.Add(hider);
            if (checklist != null) checklist.SetFound(hider.slot);

            // "A new animal!" first when he has never found this kind.
            if (!hider.wasKnown)
            {
                Say("zoo.found_new", true);
                Say("zoo.found." + hider.kind.Id);
            }
            else
            {
                Say("zoo.found." + hider.kind.Id, true);
            }

            yield return new WaitForSeconds(SpotRevealPause);
            animal.Call();
            yield return new WaitForSeconds(0.4f);
            if (conga != null) conga.Add(animal);
            yield return WaitForVoice(6f);

            int remaining = RemainingCount();
            if (remaining > 0) yield return LeftAnnouncement(remaining);

            // The hints start again from this find.
            lastFindTime = Time.time;
            hintStage = 0;
            ScheduleHiders();
        }

        /// <summary>"Nog twee dieren!" or, once per round when he has learnt it, the question "how many are still hiding?".</summary>
        IEnumerator LeftAnnouncement(int remaining)
        {
            if (!leftAsked && lessonUI != null && remaining >= 2 && NumberCurriculum.IsUnlocked(NumberSkill.Hidden))
            {
                leftAsked = true;
                System.Func<IEnumerator> hint = checklist != null ? (System.Func<IEnumerator>)(() => checklist.CountSilhouettes()) : null;
                yield return AskNumber(remaining, "zoo.how_many_left", hint, true);
                yield break;
            }

            Say("zoo.left." + Mathf.Clamp(remaining, 1, 4), true);
            yield return WaitForVoice(4f);
        }

        /// <summary>An empty spot: a funny critter pops out ("Oh! Dat is geen olifant!") and the spot can't surprise again.</summary>
        IEnumerator EmptySpot(HidingSpot spot)
        {
            spot.Reveal();
            SurpriseCritter critter = PickCritter();
            if (critter != null) critter.PopOut(spot.PopPoint);
            Say("zoo.surprise", true);
            if (keeper != null) keeper.PlayLaugh();
            yield return new WaitForSeconds(1.2f);
            yield return WaitForVoice(4f);
        }

        SurpriseCritter PickCritter()
        {
            if (critters == null || critters.Length == 0) return null;

            int start = Random.Range(0, critters.Length);
            for (int i = 0; i < critters.Length; i++)
            {
                SurpriseCritter critter = critters[(start + i) % critters.Length];
                if (critter != null && !critter.IsBusy) return critter;
            }
            return null;
        }

        /// <summary>For testing: everybody is found right away (no voice, no questions).</summary>
        IEnumerator FindEverythingAtOnce()
        {
            foreach (Hider hider in hiders)
            {
                if (hider.found) continue;

                ZooAnimal animal = hider.spot.Reveal();
                hider.found = true;
                foundOrder.Add(hider);
                if (checklist != null) checklist.SetFound(hider.slot);
                if (conga != null && animal != null) conga.Add(animal);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.8f);
        }

        // ----- Peeking, giggling and the hints -----------------------------------------------------

        void ScheduleHiders()
        {
            float min = ByLevel(PeekMinByLevel);
            float max = ByLevel(PeekMaxByLevel);
            int order = 0;
            foreach (Hider hider in hiders)
            {
                if (hider.found) continue;

                hider.nextPeek = Time.time + Random.Range(min, max) + order * 0.7f;
                hider.nextCall = Time.time + Random.Range(2f, 4f) + order * 0.5f;
                order++;
            }
        }

        /// <summary>Hidden animals peek out now and then, and the ones near him giggle or call, louder the closer he comes.</summary>
        void UpdatePeeksAndCalls(Vector3 position)
        {
            float min = ByLevel(PeekMinByLevel);
            float max = ByLevel(PeekMaxByLevel);
            foreach (Hider hider in hiders)
            {
                if (hider.found) continue;

                if (Time.time >= hider.nextPeek)
                {
                    hider.nextPeek = Time.time + Random.Range(min, max);
                    hider.spot.Peek();
                }

                if (Time.time >= hider.nextCall)
                {
                    hider.nextCall = Time.time + Random.Range(4f, 6f);
                    if (FlatDistance(hider.spot.CoverBounds.center, position) <= callRadius)
                    {
                        if (Random.value < 0.5f) hider.animal.Giggle();
                        else hider.animal.Call();
                    }
                }
            }
        }

        Hider NearestRemaining(Vector3 position)
        {
            Hider best = null;
            float bestDistance = float.MaxValue;
            foreach (Hider hider in hiders)
            {
                if (hider.found) continue;

                float distance = hider.spot.DistanceTo(position);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = hider;
            }
            return best;
        }

        /// <summary>
        /// The hints get stronger the longer he is stuck: the first sound and the position word, then the cover shakes, then a sparkle
        /// trail (again every 12 s). Returns a hider that comes out by itself after a very long time, otherwise null.
        /// </summary>
        Hider UpdateHints(Vector3 position)
        {
            float quiet = Time.time - lastFindTime;
            Hider target = NearestRemaining(position);
            if (target == null) return null;

            if (hintStage < 1 && quiet >= ByLevel(HintOneByLevel))
            {
                hintStage = 1;
                StopHintRoutine();
                hintRoutine = StartCoroutine(HintAudio(target));
            }

            if (hintStage < 2 && quiet >= ByLevel(HintTwoByLevel))
            {
                hintStage = 2;
                target.spot.Shake();
                Say("zoo.shake", true);
            }

            float thirdAt = ByLevel(HintThreeByLevel);
            if (hintStage < 3 && quiet >= thirdAt)
            {
                // Each time the trail is needed counts as a mistake (for the level).
                hintStage = 3;
                mistakes++;
                ShowHintTrail(target);
                Say("zoo.trail", true);
                nextTrailTime = Time.time + TrailRepeatSeconds;
            }
            else if (hintStage >= 3 && Time.time >= nextTrailTime)
            {
                nextTrailTime = Time.time + TrailRepeatSeconds;
                ShowHintTrail(target);
            }

            return quiet >= thirdAt + AutoFindExtraSeconds ? target : null;
        }

        void ShowHintTrail(Hider target)
        {
            if (trail != null) trail.Show(player.transform, target.spot.PopPoint, 6f, 1.2f);
        }

        /// <summary>"Ik hoor een dier met de... o!" and then "Ik hoor iets achter de heg!" One line after the other.</summary>
        IEnumerator HintAudio(Hider target)
        {
            string sound = "letter.sound." + target.kind.FirstSound;
            if (HasClip("zoo.clue") && HasClip(sound))
            {
                Say("zoo.clue", true);
                yield return WaitForVoice(5f);
                Say(sound);
                yield return WaitForVoice(4f);
            }

            Say("zoo.hint." + target.spot.SpotId, true);
            yield return WaitForVoice(5f);
            hintRoutine = null;
        }

        void StopHintRoutine()
        {
            if (hintRoutine != null) StopCoroutine(hintRoutine);
            hintRoutine = null;
        }

        // ----- Number cards -----------------------------------------------------------------------

        /// <summary>
        /// Asks "how many?" with the number cards. A wrong card wiggles and dims, a friendly line plays and the hint (counting along)
        /// runs; nothing ever fails. If he does nothing the question repeats with a pulsing card, and after a long time it carries on.
        /// </summary>
        IEnumerator AskNumber(int answer, string promptGroup, System.Func<IEnumerator> wrongHint, bool silhouetteLine)
        {
            if (lessonUI == null) yield break;

            FreezePlayer(true);
            SetHudVisible(false);

            Say(promptGroup, true);
            int[] cards = MakeNumberCards(answer);
            pickedCard = -1;
            lessonUI.ShowNumberCards(cards, n => pickedCard = n);

            float quietSince = Time.time;
            bool reminded = false;
            int wrong = 0;
            // The numbers still on offer (a wrong one leaves for good).
            var live = new List<int>(cards);
            while (true)
            {
                if (pickedCard >= 0)
                {
                    int picked = pickedCard;
                    pickedCard = -1;
                    quietSince = Time.time;
                    reminded = false;
                    lessonUI.StopPulse();
                    if (picked == answer) break;

                    // Never a failure: a wiggle, a friendly line, and we count along together.
                    mistakes++;
                    wrong++;
                    live.Remove(picked);
                    lessonUI.WiggleCard(picked);
                    // During the hint nothing can be tapped, and he sees it: every card is dimmed.
                    foreach (int number in cards) lessonUI.DimCard(number);
                    Say(silhouetteLine ? "zoo.count_silhouettes" : "lesson.try_again", true);
                    yield return new WaitForSeconds(1.8f);
                    if (wrongHint != null) yield return RunLimited(wrongHint(), 16f);

                    // The cards come back (without the wrong one) and can be tapped again.
                    pickedCard = -1;
                    lessonUI.ShowNumberCards(live.ToArray(), n => pickedCard = n);
                    if (wrong >= 2 || live.Count <= 1) lessonUI.PulseCard(answer);
                    quietSince = Time.time;
                }
                else if (!reminded && Time.time - quietSince > QuestionReminderSeconds)
                {
                    reminded = true;
                    Say(promptGroup, true);
                    lessonUI.PulseCard(answer);
                }
                else if (Time.time - quietSince > QuestionGiveUpSeconds)
                {
                    // Safety net: he is not answering, so the game carries on.
                    break;
                }
                yield return null;
            }

            lessonUI.StopPulse();
            lessonUI.CelebrateCard(answer);
            Say(HasLine("lesson.right") ? "lesson.right" : "zoo.right", true);
            yield return new WaitForSeconds(1.8f);
            lessonUI.HideCards();
            SetHudVisible(true);
            FreezePlayer(false);
        }

        static bool HasLine(string group)
        {
            return VoicePlayer.TryGetLine(group, out AudioClip clip, out string text);
        }

        /// <summary>The right answer and distinct other numbers from 1 to max(5, answer + 2), shuffled.</summary>
        int[] MakeNumberCards(int answer)
        {
            int count = Mathf.Clamp(ByLevel(CardsByLevel), 2, 3);
            int max = Mathf.Max(5, answer + 2);
            var others = new List<int>();
            for (int n = 1; n <= max; n++)
                if (n != answer) others.Add(n);
            Shuffle(others);

            var cards = new List<int> { answer };
            for (int i = 0; i < others.Count && cards.Count < count; i++) cards.Add(others[i]);
            Shuffle(cards);
            return cards.ToArray();
        }
    }
}
