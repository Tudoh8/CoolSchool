using System.Collections;
using System.Collections.Generic;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// The last four kinds of exercise: the number line (after, before, missing), counting back to zero, counting on from the
    /// basket, and the hiding cup. Like all of them they can not be failed.
    /// </summary>
    public partial class NumbersLesson
    {
        // Number line
        const int LineLength = 4;
        const int LineChoiceReach = 2;
        const float FillBlankPauseSeconds = 0.8f;

        // Count back
        const int CountBackMin = 5;
        const int CountBackMaxToys = 10;

        // Count on
        const int CountOnMaxStart = 17;
        const int CountOnMaxExtra = 3;
        const int CountOnSilentLevel = 8;
        // The basket stands at the right end of the table and the new toys lie in a row left of it, not behind it.
        const float CountOnToysX = 0.3f;
        const float CountOnToysWidth = 1.1f;
        // The big starting number floats this far above the basket, clear of the number of the toy that just went in.
        const float CountOnLabelLift = 0.85f;

        // Hidden
        const int HiddenMinTotal = 3;
        const int HiddenMaxTotalEarly = 5;
        const int HiddenMaxTotal = 10;
        const int HiddenFullLevel = 8;
        const float HiddenRowX = 0.3f;
        const float HiddenRowWidth = 1.7f;
        const float HiddenCupX = -1.1f;
        const float HiddenCupZ = 0f;
        const float HiddenToyScale = 0.15f;
        const float HiddenToyFlightSeconds = 0.45f;
        const float HiddenToyFlightArc = 0.3f;
        const float HiddenStaggerSeconds = 0.22f;
        const float HiddenStillGiveUpSeconds = 2f;

        FloatingDigit countOnLabel;

        // ----- Number line -----------------------------------------------------------------------

        /// <summary>
        /// Four numbers in a row on the blackboard with one hidden behind a "?": which number comes after this one? Which comes
        /// before? (From level 8 also: which one is missing in the middle?) The hint counts along the row and stops before the "?".
        /// </summary>
        IEnumerator RunNumberLine(int level, int maxNumber)
        {
            // 0 = after, 1 = before, 2 = a gap in the middle (from the level that unlocks it)
            int variant = Random.Range(0, NumberCurriculum.IsUnlocked(NumberSkill.Gap, level) ? 3 : 2);
            int first = PickNumber(1, maxNumber - LineLength + 1);

            var values = new int[LineLength];
            for (int i = 0; i < LineLength; i++) values[i] = first + i;

            int blank = variant == 0 ? LineLength - 1 : variant == 1 ? 0 : Random.Range(1, LineLength - 1);
            int answer = values[blank];

            string question = "lesson.number_gap";
            if (variant == 0) question = "lesson.number_after." + values[LineLength - 2];
            else if (variant == 1) question = "lesson.number_before." + values[1];

            if (blackboard != null) blackboard.ShowNumberLine(values, blank);
            yield return new WaitForSeconds(0.6f);

            System.Action prompt = () => Say(question);
            prompt();

            int[] choices = BuildNearChoices(answer, 1, maxNumber, LineChoiceReach);
            yield return AskForNumber(answer, maxNumber, prompt, () => NumberLineHint(values, blank, variant == 1), choices);

            if (blackboard != null) blackboard.FillBlank(answer);
            yield return new WaitForSeconds(FillBlankPauseSeconds);
            yield return CelebrateAnswer(answer);
        }

        /// <summary>The teacher counts along the row, pointing at each number, and stops before the "?". For "before" she points at the number asked about.</summary>
        IEnumerator NumberLineHint(int[] values, int blank, bool before)
        {
            Say("lesson.hint", true);
            yield return WaitForSpeech();

            int last = before ? 1 : blank - 1;
            for (int i = before ? 1 : 0; i <= last; i++)
            {
                if (blackboard != null) blackboard.PulseLineSlot(i);
                Say("number." + values[i], true);
                yield return WaitForSpeech();
            }

            if (blackboard != null) blackboard.PulseLineSlot(blank);
            yield return new WaitForSeconds(0.4f);
        }

        // ----- Count back ------------------------------------------------------------------------

        /// <summary>
        /// A row of toys and the number on the board. Every tap sends a toy away, the board shows what is left, and the teacher
        /// counts down to zero. It can never be wrong.
        /// </summary>
        IEnumerator RunCountBack(Countable entry, int maxNumber)
        {
            int number = PickNumber(CountBackMin, Mathf.Min(maxNumber, CountBackMaxToys));
            SpawnToys(entry, number, 0f, allowTrays: false);
            yield return new WaitForSeconds(SpawnWaitSeconds(number));

            int left = number;
            if (blackboard != null) blackboard.ShowNumber(number);

            void SendOff(CountableObject tapped)
            {
                if (tapped == null || tapped.IsMoving || left <= 0) return;

                left--;
                LaunchToyAway(tapped);
                if (blackboard != null) blackboard.ShowNumber(left);
                if (left > 0) Say("number." + left, true);
            }

            System.Action prompt = () => Say("lesson.count_back");
            tapHandler = SendOff;
            prompt();

            var idle = new IdleTimer();
            while (left > 0 || AnyMoving())
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(NextUncounted()), () => SendOff(NextUncounted()));
                yield return null;
            }

            tapHandler = null;
            Say("lesson.count_back_done", true);
            Cheer();
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        // ----- Count on --------------------------------------------------------------------------

        /// <summary>
        /// The basket already holds some: a big number floats above it and stays the same. A few more toys lie beside it. At
        /// level 7 the teacher counts on while each toy goes in; from level 8 the toys only plop in, and the child says how many
        /// the basket holds now. The hint counts on aloud from the floating number.
        /// </summary>
        IEnumerator RunCountOn(Countable entry, int level, int maxNumber)
        {
            int start = PickNumber(2, Mathf.Min(CountOnMaxStart, maxNumber - 1));
            int extra = Random.Range(1, Mathf.Min(CountOnMaxExtra, maxNumber - start) + 1);
            int total = start + extra;
            bool spoken = level < CountOnSilentLevel;

            PlaceBasket();
            basketToys.Clear();
            ShowCountOnLabel(start);

            Vector2[] layout = ToyLayout(extra, basketPosition.y, CountOnToysX, CountOnToysWidth);
            for (int i = 0; i < extra; i++)
                SpawnToy(entry, stage.TablePoint(layout[i].x, layout[i].y), 1f, i * ToyAppearGapSeconds);
            yield return new WaitForSeconds(SpawnWaitSeconds(extra));

            void Add(CountableObject tapped)
            {
                if (tapped == null || tapped.IsMoving || basketToys.Contains(tapped) || basketToys.Count >= extra) return;

                PutInBasket(tapped, start, spoken);
            }

            System.Action prompt = () => Say("lesson.count_on." + start);
            tapHandler = Add;
            prompt();

            var idle = new IdleTimer();
            while (basketToys.Count != extra || AnyMoving())
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => Pulse(NextOnTable()), () => Add(NextOnTable()));
                yield return null;
            }
            tapHandler = null;

            if (spoken)
            {
                yield return SettleAfterLastNumber();
                yield return ShowTotal(total);
                yield break;
            }

            System.Action ask = () => Say("lesson.count_on_ask");
            ask();
            yield return AskForNumber(total, maxNumber, ask, () => CountOnHint(start, extra));
            yield return CelebrateAnswer(total);
        }

        /// <summary>Counting on aloud: the floating number pulses and is said, then each toy in the basket hops while the next numbers are said.</summary>
        IEnumerator CountOnHint(int start, int extra)
        {
            Say("lesson.hint", true);
            yield return WaitForSpeech();

            if (countOnLabel != null) countOnLabel.Pulse();
            Say("number." + start, true);
            yield return WaitForSpeech();

            for (int i = 0; i < extra && i < basketToys.Count; i++)
            {
                if (basketToys[i] != null) basketToys[i].Hop();
                Say("number." + (start + i + 1), true);
                yield return WaitForSpeech();
            }
            yield return new WaitForSeconds(0.4f);
        }

        void ShowCountOnLabel(int number)
        {
            ClearCountOnLabel();
            Vector3 above = new Vector3(basketBounds.center.x, basketBounds.max.y + CountOnLabelLift, basketBounds.center.z);
            countOnLabel = FloatingDigit.Create(numberFont, number, above);
        }

        void ClearCountOnLabel()
        {
            if (countOnLabel != null) Destroy(countOnLabel.gameObject);
            countOnLabel = null;
        }

        // ----- Hidden ----------------------------------------------------------------------------

        /// <summary>
        /// Some toys on the table; the total stays on the board. A cup lands and some toys hop under it. How many are hiding?
        /// The hint lifts the cup, counts the hidden toys aloud and puts them back; the right answer lifts it for good.
        /// </summary>
        IEnumerator RunHidden(Countable entry, int level, int maxNumber)
        {
            int cap = Mathf.Min(level >= HiddenFullLevel ? HiddenMaxTotal : HiddenMaxTotalEarly, maxNumber);
            int total = PickNumber(HiddenMinTotal, cap);
            int hidden = Random.Range(1, total);

            Vector2[] layout = ToyLayout(total, 0f, HiddenRowX, HiddenRowWidth);
            for (int i = 0; i < total; i++)
                SpawnToy(entry, stage.TablePoint(layout[i].x, layout[i].y), 1f, i * ToyAppearGapSeconds);
            yield return new WaitForSeconds(SpawnWaitSeconds(total));

            // The number stays on the board as a reminder of how many there were.
            yield return ShowTotal(total);

            Say("lesson.hidden");
            yield return hidingCup.CoverAt(stage.TablePoint(HiddenCupX, HiddenCupZ));

            // The rightmost toys, the ones nearest the cup, hop under it.
            List<CountableObject> hiddenToys = toys.GetRange(total - hidden, hidden);
            for (int i = hiddenToys.Count - 1; i >= 0; i--)
            {
                HideUnderCup(hiddenToys[i]);
                yield return new WaitForSeconds(HiddenStaggerSeconds);
            }
            yield return WaitUntilStill();
            yield return WaitForSpeech();

            System.Action prompt = () => Say("lesson.hidden_ask");
            prompt();
            yield return AskForNumber(hidden, total - 1, prompt, () => HiddenHint(hiddenToys));

            yield return RevealHiddenToys(hiddenToys);
            yield return CelebrateAnswer(hidden);
        }

        /// <summary>A toy hops under the cup, shrinking, and is switched off there.</summary>
        void HideUnderCup(CountableObject toy)
        {
            if (toy == null) return;

            toy.FlyTo(hidingCup.UnderPoint + Vector3.up * 0.04f, HiddenToyFlightSeconds, HiddenToyFlightArc, HiddenToyScale, () =>
            {
                if (toy != null) toy.gameObject.SetActive(false);
            });
        }

        /// <summary>The cup lifts and the hidden toys hop out to their places in the row, one after the other.</summary>
        IEnumerator RevealHiddenToys(List<CountableObject> hiddenToys)
        {
            yield return hidingCup.Lift();

            foreach (CountableObject toy in hiddenToys)
            {
                if (toy == null) continue;

                toy.gameObject.SetActive(true);
                toy.transform.position = hidingCup.UnderPoint;
                toy.ReturnToRest(HiddenToyFlightSeconds, null);
                yield return new WaitForSeconds(HiddenStaggerSeconds);
            }
            yield return WaitUntilStill();
        }

        /// <summary>"Shall we look under the cup?": the cup lifts, the hidden toys are counted aloud, then they hop back and the cup comes down again.</summary>
        IEnumerator HiddenHint(List<CountableObject> hiddenToys)
        {
            Say("lesson.hidden_hint", true);
            yield return WaitForSpeech();

            yield return RevealHiddenToys(hiddenToys);
            yield return CountGroupAloud(hiddenToys);

            foreach (CountableObject toy in hiddenToys)
            {
                if (toy == null) continue;

                toy.ResetCounted();
                HideUnderCup(toy);
                yield return new WaitForSeconds(HiddenStaggerSeconds);
            }
            yield return WaitUntilStill();
            yield return hidingCup.CoverAt(hidingCup.UnderPoint);
        }

        /// <summary>Waits until no toy is hopping or flying (never for long).</summary>
        IEnumerator WaitUntilStill()
        {
            float giveUp = Time.time + HiddenStillGiveUpSeconds;
            while (AnyMoving() && Time.time < giveUp) yield return null;
        }
    }
}
