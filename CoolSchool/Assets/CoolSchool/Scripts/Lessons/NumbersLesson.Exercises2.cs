using System.Collections;
using System.Collections.Generic;
using CoolSchool.Battle;
using UnityEngine;

namespace CoolSchool.Lessons
{
    /// <summary>
    /// Four more kinds of exercise, with the ten-frame trays: a quick look at some dots, which group has more (or fewer),
    /// one more / one less, and making the same amount. Like all of them they can not be failed: a wrong pick gets a
    /// friendly hint, and a child who does nothing is helped step by step.
    /// </summary>
    public partial class NumbersLesson
    {
        // Quick look
        const int QuickLookMaxDots = 6;
        const float QuickLookSeconds = 1.5f;
        const float QuickLookFastSeconds = 1f;
        const int QuickLookFastLevel = 6;

        // Trays
        const float TrayGap = 0.12f;
        const float TrayHintPauseSeconds = 0.3f;
        // The counted number above a toy in a 0.2 m cell: two digits are then about as wide as a cell.
        const float TrayLabelSize = 0.33f;

        // Compare
        const int CompareMaxGroup = 10;
        const int CompareFewerLevel = 6;
        const int CompareCloseLevel = 6;

        // One more, one less
        const float ToyLeaveSeconds = 0.45f;
        const float ToyLeaveHeight = 0.9f;
        const float ToyLeaveGiveUpSeconds = 2f;

        // Same amount: the tray in front on the left and the basket on the right (both low, so they hide nothing), and the
        // loose toys behind them, at least 0.4 m away, in one row or two staggered rows. The basket has nothing behind it.
        const int SameAmountMaxNumber = 8;
        const int SameAmountExtraToys = 3;
        const int SameAmountMaxLoose = 10;
        const float SameAmountLooseScale = 0.85f;
        const float SameAmountTrayX = 0.82f;
        const float SameAmountTrayZ = 0.37f;
        const float SameAmountBasketX = -1.1f;
        const float SameAmountBasketZ = 0.2f;
        const float SameAmountLooseX = 0.35f;
        const float SameAmountLooseZ = -0.3f;
        const float SameAmountLooseWidth = 1.8f;
        const float SameAmountLooseRowGap = 0.42f;

        // ----- The trays -------------------------------------------------------------------------

        bool TraysReady => tenFrames != null && tenFrames.Length >= 2 && tenFrames[0] != null && tenFrames[1] != null;

        /// <summary>Both trays side by side, the first on the left of the screen, with the middle of the pair at this z.</summary>
        void ShowTrayPair(float z)
        {
            float half = (tenFrames[0].Width + TrayGap) * 0.5f;
            ShowTray(0, half, z);
            ShowTray(1, -half, z);
        }

        void ShowTray(int tray, float x, float z)
        {
            tenFrames[tray].Show(stage.TablePoint(x, z), stage.TableFacing(), !stagingForPreview);
        }

        /// <summary>Takes both trays off the table. Safe at any moment.</summary>
        void HideTrays()
        {
            if (tenFrames == null) return;

            foreach (TenFrameTray tray in tenFrames)
                if (tray != null) tray.Hide();
        }

        CountableObject SpawnTrayToy(Countable entry, int tray, int slot, float appearDelay)
        {
            CountableObject toy = SpawnToy(entry, tenFrames[tray].SlotPosition(slot), trayToyScale, appearDelay);
            toy.LabelSizeMultiplier = TrayLabelSize;
            return toy;
        }

        /// <summary>
        /// Counts a group of toys out loud, one hop and one number at a time. The numbers stay above the toys, unless the toys
        /// are close together (in the basket): then each number replaces the one before, so they do not pile up.
        /// </summary>
        IEnumerator CountGroupAloud(List<CountableObject> group, bool oneNumberAtATime = false)
        {
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] == null) continue;

                group[i].Pop(i + 1);
                if (oneNumberAtATime && i > 0 && group[i - 1] != null) group[i - 1].ClearNumber();
                Say("number." + (i + 1), true);
                yield return WaitForSpeech();
            }
            yield return new WaitForSeconds(TrayHintPauseSeconds);
        }

        /// <summary>
        /// A toy rises, shrinks and is gone with a sparkle. It stays in the toys list until it has left, so ending the lesson
        /// at any moment cleans it up (nothing is left running).
        /// </summary>
        void LaunchToyAway(CountableObject toy)
        {
            if (toy == null) return;

            Vector3 up = toy.transform.position + Vector3.up * ToyLeaveHeight;
            toy.FlyTo(up, ToyLeaveSeconds, 0.1f, 0f, () =>
            {
                BattleEffects.Sparkle(up);
                if (toy == null) return;

                toys.Remove(toy);
                Destroy(toy.gameObject);
            });
        }

        /// <summary>Sends a toy away and waits until it is gone (never for long).</summary>
        IEnumerator SendToyAway(CountableObject toy)
        {
            if (toy == null) yield break;

            LaunchToyAway(toy);
            float giveUp = Time.time + ToyLeaveGiveUpSeconds;
            while (toy != null && Time.time < giveUp) yield return null;
        }

        // ----- Quick look ------------------------------------------------------------------------

        /// <summary>Some dots show on the blackboard for a moment. How many were there? The hint shows them again and counts them.</summary>
        IEnumerator RunQuickLook(int level, int maxNumber)
        {
            int cap = Mathf.Min(maxNumber, QuickLookMaxDots);
            int number = PickNumber(1, cap);

            Say("lesson.quick_look");
            yield return WaitForSpeech();

            if (blackboard != null) blackboard.ShowDots(number);
            yield return new WaitForSeconds(level >= QuickLookFastLevel ? QuickLookFastSeconds : QuickLookSeconds);
            if (blackboard != null) blackboard.Clear();

            System.Action prompt = () => Say("lesson.quick_look_ask");
            prompt();

            yield return AskForNumber(number, cap, prompt, () => QuickLookHint(number));
            yield return CelebrateAnswer(number);
        }

        IEnumerator QuickLookHint(int number)
        {
            if (blackboard != null) blackboard.ShowDots(number);
            yield return CountDotsAloud(number);
        }

        // ----- Compare ---------------------------------------------------------------------------

        /// <summary>
        /// Two groups of the same toy, one in each tray. Which has more? (From level 6 sometimes: which has fewer?) Tapping any
        /// toy of a group picks the group. After the second wrong pick both groups are counted aloud.
        /// </summary>
        IEnumerator RunCompare(Countable entry, int level, int maxNumber)
        {
            int cap = Mathf.Min(maxNumber, CompareMaxGroup);
            (int leftCount, int rightCount) = PickDifferentPair(cap, level >= CompareCloseLevel ? 1 : 2);
            bool askMore = level < CompareFewerLevel || Random.value < 0.5f;

            int bigger = leftCount > rightCount ? 0 : 1;
            int target = askMore ? bigger : 1 - bigger;

            ShowTrayPair(0f);
            var groups = new[] { new List<CountableObject>(), new List<CountableObject>() };
            for (int i = 0; i < leftCount; i++)
                groups[0].Add(SpawnTrayToy(entry, 0, i, i * ToyAppearGapSeconds));
            for (int i = 0; i < rightCount; i++)
                groups[1].Add(SpawnTrayToy(entry, 1, i, i * ToyAppearGapSeconds));
            yield return new WaitForSeconds(SpawnWaitSeconds(Mathf.Max(leftCount, rightCount)));

            int picked = -1;
            bool helping = false;
            tapHandler = tapped =>
            {
                if (helping || picked >= 0 || tapped == null) return;
                if (groups[0].Contains(tapped)) picked = 0;
                else if (groups[1].Contains(tapped)) picked = 1;
            };

            System.Action prompt = () => Say(askMore ? "lesson.compare_more" : "lesson.compare_fewer");
            prompt();

            int misses = 0;
            var idle = new IdleTimer();
            while (true)
            {
                if (picked >= 0)
                {
                    int group = picked;
                    picked = -1;
                    if (group == target) break;

                    exerciseMadeMistake = true;
                    misses++;
                    foreach (CountableObject toy in groups[group])
                        if (toy != null) toy.Wobble();

                    if (misses == 2)
                    {
                        exerciseNeededHelp = true;
                        helping = true;
                        yield return CompareHint(groups, target);
                        helping = false;
                        picked = -1;
                        idle = new IdleTimer();
                    }
                    else
                    {
                        Say("lesson.try_again", true);
                    }
                }
                else if (idle.Tick(IsBusy(), lastActivityTime))
                {
                    HandleIdle(idle.Cycles, prompt, () => PulseGroup(groups[target]), () => picked = target);
                }
                yield return null;
            }

            tapHandler = null;
            foreach (List<CountableObject> group in groups)
                foreach (CountableObject toy in group)
                    if (toy != null) toy.Highlight(false);
            foreach (CountableObject toy in groups[target])
                if (toy != null) toy.Hop();

            Say("lesson.correct", true);
            Cheer();
            yield return WaitForSpeech();
            yield return new WaitForSeconds(CelebrationPauseSeconds);
        }

        /// <summary>Two different counts, at least minGap apart, both from 1 to cap. Either can be the bigger one.</summary>
        static (int, int) PickDifferentPair(int cap, int minGap)
        {
            int first = Random.Range(1, cap + 1);

            var options = new List<int>();
            for (int n = 1; n <= cap; n++)
                if (Mathf.Abs(n - first) >= minGap) options.Add(n);

            if (options.Count == 0) return (first, first > 1 ? first - 1 : first + 1);
            return (first, options[Random.Range(0, options.Count)]);
        }

        /// <summary>
        /// "We count them both": each group is counted aloud (the last toy of each keeps its number), then the right group pulses.
        /// </summary>
        IEnumerator CompareHint(List<CountableObject>[] groups, int target)
        {
            Say("lesson.compare_hint", true);
            yield return WaitForSpeech();

            for (int g = 0; g < groups.Length; g++)
                yield return CountGroupAloud(groups[g]);

            foreach (List<CountableObject> group in groups)
                for (int i = 0; i < group.Count - 1; i++)
                    if (group[i] != null) group[i].ResetCounted();

            PulseGroup(groups[target]);
        }

        static void PulseGroup(List<CountableObject> group)
        {
            foreach (CountableObject toy in group)
                Pulse(toy);
        }

        // ----- One more, one less ----------------------------------------------------------------

        /// <summary>
        /// The toys appear and are counted for the child (the total shows on the board). Then one more toy arrives, or one goes
        /// away. How many are there now? The layout is made for the bigger count up front, so the extra toy has its place.
        /// </summary>
        IEnumerator RunOneMoreOneLess(Countable entry, int maxNumber)
        {
            int number = PickNumber(2, maxNumber - 1);
            bool more = Random.value < 0.5f;

            int reserved = more ? number + 1 : number;
            bool inTrays = reserved > TenFrameTray.Capacity && TraysReady;
            Vector2[] layout = inTrays ? null : ToyLayout(reserved, 0f);
            if (inTrays) ShowTrayPair(0f);

            CountableObject SpawnAt(int index, float delay)
            {
                if (inTrays) return SpawnTrayToy(entry, index / TenFrameTray.Capacity, index % TenFrameTray.Capacity, delay);
                return SpawnToy(entry, stage.TablePoint(layout[index].x, layout[index].y), 1f, delay);
            }

            for (int i = 0; i < number; i++)
                SpawnAt(i, i * ToyAppearGapSeconds);
            yield return new WaitForSeconds(SpawnWaitSeconds(number));

            yield return ShowTotal(number);

            if (more)
            {
                Say("lesson.one_more");
                yield return WaitForSpeech();

                CountableObject extra = SpawnAt(number, 0f);
                BattleEffects.Sparkle(extra.transform.position + Vector3.up * 0.2f);
                yield return new WaitForSeconds(SpawnWaitSeconds(1));
            }
            else
            {
                Say("lesson.one_less");
                yield return WaitForSpeech();

                yield return SendToyAway(toys.Count > 0 ? toys[toys.Count - 1] : null);
                yield return new WaitForSeconds(0.3f);
            }

            if (blackboard != null) blackboard.Clear();

            int answer = more ? number + 1 : number - 1;
            System.Action prompt = () => Say("lesson.how_many_now");
            prompt();

            yield return AskForNumber(answer, maxNumber, prompt, CountToysAloud);
            yield return CelebrateAnswer(answer);
        }

        // ----- Same amount -----------------------------------------------------------------------

        /// <summary>
        /// A few toys lie in a tray, and the basket stands empty with more toys than needed behind it. Put just as many in
        /// the basket as there are in the tray. When it is right, both are counted aloud, ending on the same number.
        /// </summary>
        IEnumerator RunSameAmount(Countable entry, int maxNumber)
        {
            int number = PickNumber(2, Mathf.Min(maxNumber, SameAmountMaxNumber));
            int loose = Mathf.Min(number + SameAmountExtraToys, SameAmountMaxLoose);

            ShowTray(0, SameAmountTrayX, SameAmountTrayZ);
            var trayToys = new List<CountableObject>();
            for (int i = 0; i < number; i++)
                trayToys.Add(SpawnTrayToy(entry, 0, i, i * ToyAppearGapSeconds));

            PlaceBasket(new Vector2(SameAmountBasketX, SameAmountBasketZ));
            basketToys.Clear();

            var looseToys = new List<CountableObject>();
            Vector2[] layout = ToyLayout(loose, SameAmountLooseZ, SameAmountLooseX, SameAmountLooseWidth, SameAmountLooseRowGap);
            for (int i = 0; i < loose; i++)
                looseToys.Add(SpawnToy(entry, stage.TablePoint(layout[i].x, layout[i].y), SameAmountLooseScale, i * ToyAppearGapSeconds));
            yield return new WaitForSeconds(SpawnWaitSeconds(number + loose));

            tapHandler = tapped =>
            {
                if (tapped == null) return;
                if (trayToys.Contains(tapped))
                {
                    if (!tapped.IsMoving) tapped.Hop();
                    return;
                }
                GiveToBasket(tapped, number);
            };

            System.Action prompt = () => Say("lesson.same_amount");
            prompt();

            bool helpWanted = false;
            var idle = new IdleTimer();
            while (basketToys.Count != number || AnyMoving())
            {
                if (idle.Tick(IsBusy(), lastActivityTime))
                    HandleIdle(idle.Cycles, prompt, () => helpWanted = true, () => GiveToBasket(NextOnTable(looseToys), number));

                if (helpWanted)
                {
                    helpWanted = false;
                    yield return SameAmountHelp(trayToys, looseToys);
                    idle = new IdleTimer();
                }
                yield return null;
            }

            tapHandler = null;
            yield return SettleAfterLastNumber();
            yield return CountGroupAloud(trayToys);
            yield return CountGroupAloud(basketToys, true);
            yield return CelebrateAnswer(number);
        }

        /// <summary>The idle help: the toys in the tray are counted aloud (so the child knows how many), then the next loose toy pulses.</summary>
        IEnumerator SameAmountHelp(List<CountableObject> trayToys, List<CountableObject> looseToys)
        {
            yield return CountGroupAloud(trayToys);
            foreach (CountableObject toy in trayToys)
                if (toy != null) toy.ResetCounted();

            Pulse(NextOnTable(looseToys));
        }
    }
}
